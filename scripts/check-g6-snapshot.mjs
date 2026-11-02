import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";

const root=process.argv[2];
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root),"Verification root must be on E:");
const evidence=path.join(root,"evidence");
fs.mkdirSync(evidence,{recursive:true});
const configured=process.env.HARBOROPS_G6_HISTORICAL_SNAPSHOT;
const locations=[path.join(root,"historical"),path.join(path.dirname(root),"historical-snapshots"),
  path.join(path.dirname(root),"backups"),path.join(path.dirname(root),"dumps")];
const candidates=[];
for(const folder of locations){
  if(!fs.existsSync(folder))continue;
  for(const entry of fs.readdirSync(folder,{withFileTypes:true})){
    if(entry.isFile()&&/\.(dump|backup|tar|sql|sql\.gz)$/i.test(entry.name))
      candidates.push(path.join(folder,entry.name));
  }
}
const report={sourceSha:process.env.HARBOROPS_SOURCE_SHA,
  checkedAtUtc:new Date().toISOString(),discoveryPaths:locations,
  configuredSnapshot:configured||null,candidates:[...new Set(candidates)],
  outcome:"NOT APPLICABLE",reason:"No representative historical backup in checked E: locations",
  evidence:null};
// Docker volume metadata only (names, creation time, labels); nothing is mounted during discovery.
const dockerVolumes=[];
try{
  const names=execFileSync("docker",["volume","ls","-q"],{encoding:"utf8",timeout:30000})
    .split(/\r?\n/).filter(x=>/^(harborops[_-]|ho-verify-)/.test(x)&&!/^ho-(verify-g6|g6)/.test(x));
  for(const name of names){
    const meta=JSON.parse(execFileSync("docker",["volume","inspect",name],{encoding:"utf8",timeout:30000}))[0];
    dockerVolumes.push({name,createdAt:meta.CreatedAt,labels:meta.Labels??{}});
  }
}catch(error){report.dockerDiscoveryError=String(error.message).slice(0,200);}
report.dockerVolumeCandidates=dockerVolumes;
const configuredVolume=process.env.HARBOROPS_G6_HISTORICAL_VOLUME;
report.configuredVolume=configuredVolume||null;
const reportPath=path.join(evidence,"g6-snapshot.json");
const save=()=>fs.writeFileSync(reportPath,JSON.stringify(report,null,2));
if(configuredVolume){
  assert.ok(dockerVolumes.some(x=>x.name===configuredVolume),"Configured volume is not a discovered candidate");
  const { runVolumeSnapshot }=await import("./check-g6-snapshot-volume.mjs");
  await runVolumeSnapshot({volume:configuredVolume,
    database:process.env.HARBOROPS_G6_HISTORICAL_DATABASE||"harborops",
    cwd:process.cwd(),root,report,save});
  console.log("G6 historical volume upgrade and preservation: PASS");
  process.exit(0);
}
if(configured){
  assert.match(configured.replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/);
  if(!fs.existsSync(configured)){
    report.outcome="REVIEW REQUIRED";report.reason="Configured snapshot is missing";
    save();throw Error(report.reason);
  }
}
const backup=configured||candidates.sort()[0];
if(!backup){
  save();console.log("G6 snapshot: NOT APPLICABLE, no representative backup");
  process.exit(0);
}
report.outcome="REVIEW REQUIRED";
report.reason="Historical restore and migration proof is incomplete";save();
const run=(...args)=>execFileSync("docker",args,{
  encoding:"utf8",stdio:["ignore","pipe","pipe"],timeout:240000
}).trim();
const name="ho-g6-snapshot-pg";
assert.equal(run("ps","-aq","--filter","name=^/"+name+"$"),"",
  "Existing snapshot container cannot be reused");
report.sourceFile=backup;
const digest=crypto.createHash("sha256");
for await (const chunk of fs.createReadStream(backup,{highWaterMark:1024*1024}))
  digest.update(chunk);
report.sha256=digest.digest("hex").toUpperCase();
report.bytes=fs.statSync(backup).size;
let owned=false;
try{
  run("run","-d","--name",name,"--label","harborops.g6=disposable",
    "--ulimit","core=0","--tmpfs","/var/lib/postgresql/data:uid=70,gid=70,mode=0700",
    "--cap-drop","ALL","--security-opt","no-new-privileges",
    "-e","POSTGRES_DB=harborops_snapshot","-e","POSTGRES_USER=harborops",
    "-e","POSTGRES_HOST_AUTH_METHOD=trust","harborops-postgres:16.15-rootless");
  owned=true;
  let ready=false;
  for(let i=0;i<50;i++){
    try{
      if(run("exec",name,"pg_isready","-U","harborops","-d","harborops_snapshot")
        .includes("accepting connections")){ready=true;break;}
    }catch{}
    await new Promise(resolve=>setTimeout(resolve,500));
  }
  assert.ok(ready,"Disposable snapshot database did not start");
  const suffix=backup.toLowerCase().endsWith(".sql.gz")?".sql.gz":path.extname(backup);
  const dest="/tmp/source"+suffix;
  run("cp",backup,name+":"+dest);
  const common=["exec",name];
  if(/\.(dump|backup|tar)$/i.test(backup)){
    run(...common,"pg_restore","--exit-on-error","--no-owner","--no-acl",
      "-U","harborops","-d","harborops_snapshot",dest);
  }else if(/\.sql$/i.test(backup)){
    run(...common,"psql","-v","ON_ERROR_STOP=1","-U","harborops",
      "-d","harborops_snapshot","-f",dest);
  }else if(/\.sql\.gz$/i.test(backup)){
    run(...common,"sh","-ec",
      "gzip -dc /tmp/source.sql.gz | psql -v ON_ERROR_STOP=1 -U harborops -d harborops_snapshot");
  }else throw Error("Unsupported historical backup format");
  const counts=[
    "SELECT json_build_object(",
    "'shipments',(SELECT count(*) FROM shipments),",
    "'containers',(SELECT count(*) FROM containers),",
    "'voyages',(SELECT count(*) FROM voyages),",
    "'statusHistory',(SELECT count(*) FROM shipment_status_changes))::text"
  ].join("");
  const readCounts=()=>JSON.parse(run(...common,"psql","-Atc",counts,"-U",
    "harborops","-d","harborops_snapshot"));
  const before=readCounts();
  const cache=path.join(root,"cache");
  fs.mkdirSync(cache,{recursive:true});
  run("run","--rm","--ulimit","core=0","--network","container:"+name,
    "-v",process.cwd()+":/src","-v",cache+":/cache","-w","/src",
    "-e","ConnectionStrings__HarborOps=Host=127.0.0.1;Port=5432;Database=harborops_snapshot;Username=harborops",
    "-e","NUGET_PACKAGES=/cache/nuget/packages","-e","DOTNET_CLI_HOME=/cache/dotnet-home",
    "-e","TMPDIR=/cache/tmp","mcr.microsoft.com/dotnet/sdk:10.0",
    "dotnet","run","--project","src/HarborOps.Api/HarborOps.Api.csproj",
    "-c","Release","--","--migrate");
  const after=readCounts();
  assert.deepEqual(after,before,"Accepted historical record counts changed");
  const sql="SELECT count(*) FROM pg_constraint WHERE conname IN "+
    "('ex_m6_container_overlap','ex_m6_voyage_overlap')";
  const n=Number(run(...common,"psql","-Atc",sql,"-U","harborops",
    "-d","harborops_snapshot"));
  assert.equal(n,2,"M6 exclusion constraints missing after upgrade");
  const history=run(...common,"psql","-Atc",
    'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"',
    "-U","harborops","-d","harborops_snapshot");
  assert.ok(history.includes("20261102080000_ShipmentAssignments"),
    "Snapshot was not migrated to M6");
  report.outcome="PASS";
  report.reason="Read-only copy restored into isolated E:-backed disposable PostgreSQL and migrated";
  report.evidence={before,after,m6Exclusions:n,migration:"20261102080000_ShipmentAssignments"};
  save();console.log("G6 historical snapshot upgrade and preservation: PASS");
}catch(error){
  report.outcome="REVIEW REQUIRED";
  report.reason="Historical restore/migrate failure: "+
    String(error instanceof Error?error.message:error).slice(0,250);
  save();throw error;
}finally{
  if(owned){
    run("rm","-f",name);
    assert.equal(run("ps","-aq","--filter","name=^/"+name+"$"),"",
      "Disposable historical database teardown failed");
  }
}
