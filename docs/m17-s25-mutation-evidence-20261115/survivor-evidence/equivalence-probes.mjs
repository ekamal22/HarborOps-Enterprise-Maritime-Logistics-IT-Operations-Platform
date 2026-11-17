// usage: node equivalence-probes.mjs <repo>  : runs each probe against the unmutated and the mutated module and compares.
import fs from "node:fs";import path from "node:path";import {spawnSync} from "node:child_process";import {fileURLToPath} from "node:url";
const repo=process.argv[2];const here=path.dirname(fileURLToPath(import.meta.url));
const cases=[
 {id:"g17-p09-review.mjs#001",file:"scripts/g17-p09-review.mjs",line:4,from:"Array.isArray(values)&&values.length>0",to:"Array.isArray(values)||values.length>0",probe:"probe-p09.mjs"},
 {id:"g17-p09-review.mjs#008",file:"scripts/g17-p09-review.mjs",line:39,from:"off&&on",to:"off||on",probe:"probe-p09.mjs"},
 {id:"audit-g17-test-coverage.mjs#045",file:"scripts/audit-g17-test-coverage.mjs",line:89,from:"seen.size !== 75 || matrix.length !== 75",to:"seen.size !== 75 && matrix.length !== 75",probe:"probe-audit.mjs"}];
const out=[];
for(const c of cases){
 const file=path.join(repo,c.file);const orig=fs.readFileSync(file,"utf8");const lines=orig.split("\n");
 if(!lines[c.line-1].includes(c.from))throw Error("site moved "+c.id);
 const run=()=>{const r=spawnSync(process.execPath,[path.join(here,c.probe),repo],{encoding:"utf8"});try{return JSON.parse(r.stdout.trim().split("\n").pop());}catch{return {unparsed:(r.stdout+r.stderr).slice(0,300)};}};
 const unmutated=run();lines[c.line-1]=lines[c.line-1].replace(c.from,c.to);fs.writeFileSync(file,lines.join("\n"));
 let mutant;try{mutant=run();}finally{fs.writeFileSync(file,orig);}
 out.push({id:c.id,mutation:c.from+" -> "+c.to,identical_observable_behaviour:JSON.stringify(unmutated)===JSON.stringify(mutant),unmutated,mutant});
}
console.log(JSON.stringify(out,null,1));
