#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";

const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const source = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && source && /^[0-9a-f]{40}$/.test(source), "G6 source/root required");
assert.match(root.replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/);
const dir=path.join(root,"evidence"),archiveRoot=path.join(root,"archives");
assert.ok(fs.existsSync(dir),"G6 evidence missing");
assert.ok(fs.existsSync(path.join(dir,"g6-teardown.json")),"G6 teardown proof missing");
const clean=JSON.parse(fs.readFileSync(path.join(dir,"g6-teardown.json"),"utf8"));
assert.equal(clean.remainingContainers,0);
assert.equal(clean.remainingVolumes,0);
assert.equal(clean.remainingIntegrationContainers,0);
assert.equal(clean.sourceSha,source);
const hash=file=>crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex").toUpperCase();
function walk(folder){
  return fs.readdirSync(folder,{withFileTypes:true}).flatMap(x=>{
    const p=path.join(folder,x.name);
    return x.isDirectory()?walk(p):[p];
  });
}
const files=walk(dir).filter(p=>path.basename(p)!=="g6-file-hashes.csv").sort();
assert.ok(files.length>25,"Insufficient G6 raw evidence files");
const manifest=["path,sha256,bytes",...files.map(p=>{
  const relative=path.relative(dir,p).replaceAll("\\","/");
  assert.ok(!relative.includes(",") && !relative.includes('"'),"Unsupported evidence path");
  return relative+","+hash(p)+","+fs.statSync(p).size;
})].join("\n")+"\n";
fs.writeFileSync(path.join(dir,"g6-file-hashes.csv"),manifest);
fs.mkdirSync(archiveRoot,{recursive:true});
const gateComplete=!fs.existsSync(path.join(dir,"g6-gate-incomplete.txt"));
const prefix=gateComplete?"G6-final-":"G6-incomplete-";
const filename=prefix+new Date().toISOString().replace(/[-:]/g,"").replace(/\.\d+Z$/,"Z")+
  "-"+source.slice(0,12)+".zip";
const destination=path.join(archiveRoot,filename);
const from=dir.replaceAll("'","''"),to=destination.replaceAll("'","''");
execFileSync("powershell.exe",["-NoProfile","-NonInteractive","-Command",
  "Compress-Archive -LiteralPath '"+from+"' -DestinationPath '"+to+"' -Force"],{
    stdio:"pipe",timeout:120000
  });
assert.ok(fs.existsSync(destination) && fs.statSync(destination).size>1000,"Archive creation failed");
const zipHash=hash(destination);
const output={sourceSha:source,archive:destination,sha256:zipHash,
  evidenceGate:gateComplete?"complete":"incomplete",
  files:files.length,manifest:path.join(dir,"g6-file-hashes.csv"),createdAtUtc:new Date().toISOString(),
  integrityVerified:false};
fs.writeFileSync(path.join(archiveRoot,filename+".json"),JSON.stringify(output,null,2));
fs.writeFileSync(path.join(archiveRoot,filename+".sha256"),zipHash+"  "+filename+"\n");

const verifyRoot=path.join(root,"cache","g6-archive-verify-"+source.slice(0,12));
assert.ok(!fs.existsSync(verifyRoot),"G6 archive verification directory already exists");
fs.mkdirSync(verifyRoot,{recursive:true});
try {
execFileSync("powershell.exe",["-NoProfile","-NonInteractive","-Command",
  "Expand-Archive -LiteralPath '"+to+"' -DestinationPath '"+verifyRoot.replaceAll("'","''")+"'"],{
  stdio:"pipe",timeout:120000
});
const extracted=fs.existsSync(path.join(verifyRoot,"g6-file-hashes.csv"))
  ? verifyRoot : path.join(verifyRoot,"evidence");
const extractedManifest=path.join(extracted,"g6-file-hashes.csv");
assert.ok(fs.existsSync(extractedManifest),"ZIP does not contain the manifest");
assert.equal(hash(extractedManifest),hash(output.manifest),
  "ZIP manifest differs from generated manifest");
const check=fs.readFileSync(extractedManifest,"utf8").trim().split(/\r?\n/).slice(1);
const names=new Set();
for(const line of check){
  const [name,digest,bytes]=line.split(",");
  assert.ok(name && digest && bytes && !name.includes("..") &&
    !name.startsWith("/") && !names.has(name),"Invalid or duplicate ZIP path");
  names.add(name);
  const p=path.resolve(extracted,name);
  assert.ok(p.startsWith(path.resolve(extracted)+path.sep),"ZIP path escape");
  assert.ok(fs.existsSync(p),"ZIP entry missing: "+name);
  assert.equal(hash(p),digest,"ZIP checksum mismatch: "+name);
  assert.equal(fs.statSync(p).size,Number(bytes),"ZIP byte count mismatch: "+name);
}
const members=walk(extracted).map(p=>path.relative(extracted,p).replaceAll("\\","/"))
  .filter(x=>x!=="g6-file-hashes.csv");
assert.deepEqual(members.sort(),[...names].sort(),"ZIP extra or missing entries");
assert.equal(names.size,files.length,"ZIP evidence count mismatch");
output.extractedVerified=true;
output.integrityVerified=true;
output.verifiedMembers=members.length+1;
fs.writeFileSync(path.join(archiveRoot,filename+".json"),JSON.stringify(output,null,2));
} finally {
  // Clean only the verification-specific extraction directory, also on failure.
  assert.equal(path.dirname(verifyRoot),path.join(root,"cache"));
  fs.rmSync(verifyRoot,{recursive:true,force:true});
}
console.log(JSON.stringify(output,null,2));
