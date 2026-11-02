import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const root=process.argv[2];
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root),"G6 snapshot must use E:-backed verification root");
const evidence=path.join(root,"evidence");
fs.mkdirSync(evidence,{recursive:true});
const expected=process.env.HARBOROPS_G6_HISTORICAL_SNAPSHOT;
const discovery=[
 path.join(root,"historical"),
 path.join(path.dirname(root),"historical-snapshots"),
 path.join(path.dirname(root),"backups")
];
const found=[];
for(const folder of discovery){
 if(!fs.existsSync(folder))continue;
 const candidates=fs.readdirSync(folder,{withFileTypes:true})
   .filter(x=>x.isFile()&&/\.(dump|backup|sql|tar|gz)$/i.test(x.name));
 for(const x of candidates)found.push(path.join(folder,x.name));
}
if(expected && fs.existsSync(expected))found.push(expected);
const report={checkedAtUtc:new Date().toISOString(),
 sourceSha:process.env.HARBOROPS_SOURCE_SHA,
 discoveryPaths:discovery,configuredSnapshot:expected||null,
 candidates:[...new Set(found)],
 outcome:"NOT APPLICABLE",reason:"No representative historical snapshot was found in the allowed E: locations",
 evidence:null};
if(found.length>0){
 report.outcome="REVIEW REQUIRED";
 report.reason="Historical snapshot exists. A disposable clone and actual PostgreSQL boot, migrate and read proof is required; this inventory is not a compatibility PASS.";
}
if(expected && !fs.existsSync(expected)){
 report.outcome="REVIEW REQUIRED";
 report.reason="Configured historical snapshot path is missing; investigate before declaring NOT APPLICABLE.";
}
fs.writeFileSync(path.join(evidence,"g6-snapshot.json"),JSON.stringify(report,null,2));
if(report.outcome==="REVIEW REQUIRED"){
 console.error(report.reason);
 process.exitCode=1;
}else console.log("G6 Q1 snapshot checkpoint: NOT APPLICABLE, no representative snapshot found in checked E: locations");
