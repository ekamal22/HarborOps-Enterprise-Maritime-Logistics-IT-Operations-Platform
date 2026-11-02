import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync, spawnSync } from "node:child_process";

const root=process.argv[2],sha=process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root),"E: verification root required");
assert.match(sha||"",/^[0-9a-f]{40}$/);
const evidence=path.join(root,"evidence","mutations");
fs.mkdirSync(evidence,{recursive:true});
const cache=path.join(root,"cache");
fs.mkdirSync(cache,{recursive:true});
const sourceTar=path.join(cache,"g6-mutation-"+sha.slice(0,12)+".tar");
const exported=path.join(cache,"g6-mutation-export-"+sha.slice(0,12));
assert.ok(!fs.existsSync(exported),"Mutation export already exists; use a new isolated verification root");
const run=(cmd,args,options={})=>{
 const result=spawnSync(cmd,args,{encoding:"utf8",timeout:300000, ...options});
 if(result.error)throw result.error;
 return result;
};
const expectOk=(cmd,args)=>{const result=run(cmd,args);assert.equal(result.status,0,
  cmd+" failed: "+(result.stderr||result.stdout||"").slice(-600));return result.stdout||"";};
expectOk("git",["archive","--format=tar","--output",sourceTar,sha]);
const variants=[
  ["control",null,"ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant|QueuedAssignmentWriteRechecksEveryAuthorizationDependency|OverlapMutationRequiresBehavioralExclusionRejection|ParentGuardMutationRequiresBehavioralArchiveRejection"],
  ["tenant-read","ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant","ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant"],
  ["post-lock","QueuedAssignmentWriteRechecksEveryAuthorizationDependency","QueuedAssignmentWriteRechecksEveryAuthorizationDependency"],
  ["overlap","OverlapMutationRequiresBehavioralExclusionRejection","OverlapMutationRequiresBehavioralExclusionRejection"],
  ["parent-guard","ParentGuardMutationRequiresBehavioralArchiveRejection","ParentGuardMutationRequiresBehavioralArchiveRejection"]
];
const summary={sourceSha:sha,checkedAtUtc:new Date().toISOString(),control:null,mutants:[]};
const report=path.join(root,"evidence","g6-mutations.json");
const save=()=>fs.writeFileSync(report,JSON.stringify(summary,null,2));
const name="ho-g6-mutant-pg";
assert.equal(expectOk("docker",["ps","-aq","--filter","name=^/"+name+"$"]).trim(),"",
  "Existing mutant database container found");
try{
 for(const [variant,expected,testExpression] of variants){
   const work=path.join(exported,variant);
   fs.mkdirSync(work,{recursive:true});
   expectOk("tar",["-xf",sourceTar,"-C",work]);
   if(expected)expectOk("node",["scripts/apply-g6-mutation.mjs",variant,work]);
   const results=path.join(evidence,variant);
   fs.mkdirSync(results,{recursive:true});
   let owned=false;
   try{
     expectOk("docker",["run","-d","--name",name,"--label","harborops.g6=disposable",
       "--ulimit","core=0","--tmpfs","/var/lib/postgresql/data:uid=70,gid=70,mode=0700",
       "--cap-drop","ALL","--security-opt","no-new-privileges",
       "-e","POSTGRES_DB=harborops_integration","-e","POSTGRES_USER=harborops",
       "-e","POSTGRES_HOST_AUTH_METHOD=trust","harborops-postgres:16.15-rootless"]);
     owned=true;
     let ready=false;
     for(let k=0;k<50;k++){
       const test=run("docker",["exec",name,"pg_isready","-U","harborops",
         "-d","harborops_integration"]);
       if(test.status===0){ready=true;break;}
       await new Promise(resolve=>setTimeout(resolve,500));
     }
     assert.ok(ready,"Mutation database not ready");
     const filter=testExpression.split("|").map(x=>"FullyQualifiedName~"+x).join("|");
     const shell="set -e; dotnet restore HarborOps.sln -v q; "+
       "dotnet build HarborOps.sln -c Release --no-restore -v q; "+
       "dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj "+
       "--no-build -c Release --filter '"+filter+"' "+
       "--logger 'trx;LogFileName=results.trx' --results-directory /cache/evidence/mutations/"+variant;
     const command=["run","--rm","--ulimit","core=0","--network","container:"+name,
       "-v",work+":/src","-v",root+":/cache","-w","/src",
       "-e","HARBOROPS_TEST_DB=Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops",
       "-e","NUGET_PACKAGES=/cache/nuget/packages",
       "-e","NUGET_HTTP_CACHE_PATH=/cache/nuget/http-cache",
       "-e","DOTNET_CLI_HOME=/cache/dotnet-home","-e","TMPDIR=/cache/tmp",
       "mcr.microsoft.com/dotnet/sdk:10.0","bash","-lc",shell];
     const executed=run("docker",command,{timeout:600000});
     fs.writeFileSync(path.join(results,"console.log"),
       (executed.stdout||"")+"\n"+(executed.stderr||""));
     const trx=path.join(results,"results.trx");
     assert.ok(fs.existsSync(trx),"Test results missing, build or test process failed: "+variant);
     const xml=fs.readFileSync(trx,"utf8");
     const rows=[...xml.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)].map(m=>({
       name:/testName="([^"]+)"/.exec(m[1])?.[1],
       outcome:/outcome="([^"]+)"/.exec(m[1])?.[1]
     }));
     assert.ok(rows.length>0,"Mutation did not execute tests: "+variant);
     if(variant==="control"){
       assert.equal(executed.status,0,"Unmodified source control must pass");
       assert.ok(rows.every(x=>x.outcome==="Passed"),"Unmodified control has failed/skipped tests");
       summary.control={status:"PASS",testNames:rows.map(x=>x.name),passed:rows.length};
     }else{
       assert.notEqual(executed.status,0,"Mutation survived: "+variant);
       const failed=rows.filter(x=>x.outcome==="Failed");
       assert.ok(failed.some(x=>(x.name||"").includes(expected)),
         "Mutation did not fail intended regression: "+variant);
       // Keep the assertion text so a reviewer can confirm each test failed for the intended reason.
       const messages={};
       for(const block of xml.matchAll(/<UnitTestResult([^>]*?)>([sS]*?)</UnitTestResult>/g)){
         const testName=/testName="([^"]+)"/.exec(block[1])?.[1];
         if(!testName||!/outcome="Failed"/.test(block[1]))continue;
         const text=/<Message>([sS]*?)</Message>/.exec(block[2])?.[1]||"";
         messages[testName]=text.replace(/&#xD;|&#xA;||
/g," ").slice(0,600);
       }
       summary.mutants.push({variant,status:"KILLED",expectedFailure:expected,
         failedTests:failed.map(x=>x.name),failureMessages:messages,total:rows.length});
     }
     save();
   }finally{
     if(owned)expectOk("docker",["rm","-f",name]);
   }
 }
 assert.equal(summary.mutants.length,4,"All four meaningful mutations required");
 save();console.log("G6 controlled security mutation proofs: PASS");
}finally{
 // Only the source export created under this verification root is disposable.
 assert.ok(exported.startsWith(cache+path.sep));
 if(fs.existsSync(exported))fs.rmSync(exported,{recursive:true,force:true});
 if(fs.existsSync(sourceTar))fs.unlinkSync(sourceTar);
}
