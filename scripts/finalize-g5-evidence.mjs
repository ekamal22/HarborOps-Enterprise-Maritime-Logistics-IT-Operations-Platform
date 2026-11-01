#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";

const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const source = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && source && /^[0-9a-f]{40}$/.test(source), "G5 source/root required");
assert.match(root.replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/);
const dir=path.join(root,"evidence"),archiveRoot=path.join(root,"archives");
assert.ok(fs.existsSync(dir),"G5 evidence missing");
assert.ok(fs.existsSync(path.join(dir,"g5-teardown.json")),"G5 teardown proof missing");
const clean=JSON.parse(fs.readFileSync(path.join(dir,"g5-teardown.json"),"utf8"));
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
const files=walk(dir).filter(p=>path.basename(p)!=="g5-file-hashes.csv").sort();
assert.ok(files.length>25,"Insufficient G5 raw evidence files");
const manifest=["path,sha256,bytes",...files.map(p=>{
  const relative=path.relative(dir,p).replaceAll("\\","/");
  assert.ok(!relative.includes(",") && !relative.includes('"'),"Unsupported evidence path");
  return relative+","+hash(p)+","+fs.statSync(p).size;
})].join("\n")+"\n";
fs.writeFileSync(path.join(dir,"g5-file-hashes.csv"),manifest);
fs.mkdirSync(archiveRoot,{recursive:true});
const filename="G5-final-"+new Date().toISOString().replace(/[-:]/g,"").replace(/\.\d+Z$/,"Z")+
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
  files:files.length,manifest:path.join(dir,"g5-file-hashes.csv"),createdAtUtc:new Date().toISOString()};
fs.writeFileSync(path.join(archiveRoot,filename+".json"),JSON.stringify(output,null,2));
const check=fs.readFileSync(output.manifest,"utf8").trim().split(/\r?\n/).slice(1);
for(const line of check){
  const [name,digest,bytes]=line.split(",");
  const p=path.join(dir,name);
  assert.equal(hash(p),digest,"Evidence hash mismatch: "+name);
  assert.equal(fs.statSync(p).size,Number(bytes),"Evidence size mismatch");
}
console.log(JSON.stringify(output,null,2));
