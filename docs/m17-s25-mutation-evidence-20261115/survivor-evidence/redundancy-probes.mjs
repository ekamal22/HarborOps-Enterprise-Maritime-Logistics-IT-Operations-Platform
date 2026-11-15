// For each survivor believed redundant: run a one-fact probe against (a) the unmutated source and (b) the mutated source,
// in separate child processes, and record which module and line rejects the input.
import fs from "node:fs";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
const repo=process.argv[2];
const child=path.join(path.dirname(fileURLToPath(import.meta.url)),"probe-child.mjs");
const cases=[
 {id:"verify-g17-manifest.mjs#091",file:"scripts/verify-g17-manifest.mjs",line:218,from:"||",to:"&&",
  fact:"m17-performance-raw.json: only source_sha differs",report:"m17-performance-raw.json",edit:"source_sha"},
 {id:"verify-g17-manifest.mjs#052",file:"scripts/verify-g17-manifest.mjs",line:151,from:"||",to:"&&",
  fact:"m17-export-privacy-results.json: only exported_metric_series_inspected is 0",report:"m17-export-privacy-results.json",edit:"zero_series"},
 {id:"verify-g17-manifest.mjs#053",file:"scripts/verify-g17-manifest.mjs",line:152,from:"< 2",to:"< 0",
  fact:"m17-export-privacy-results.json: only the snapshot list is cut to one entry",report:"m17-export-privacy-results.json",edit:"one_snapshot"},
 {id:"verify-g17-manifest.mjs@c096",file:"scripts/verify-g17-manifest.mjs",line:147,from:"||",to:"&&",fact:"m17-export-privacy-results.json: only source_sha differs",report:"m17-export-privacy-results.json",edit:"source_sha"},
 {id:"verify-g17-manifest.mjs@c098",file:"scripts/verify-g17-manifest.mjs",line:148,from:"||",to:"&&",fact:"m17-export-privacy-results.json: only project differs",report:"m17-export-privacy-results.json",edit:"project"},
 {id:"verify-g17-manifest.mjs@c104",file:"scripts/verify-g17-manifest.mjs",line:151,from:"< 1",to:"< 0",fact:"m17-export-privacy-results.json: only exported_metric_series_inspected is 0",report:"m17-export-privacy-results.json",edit:"zero_series"},
 {id:"verify-g17-manifest.mjs@c254",file:"scripts/verify-g17-manifest.mjs",line:285,from:"||",to:"&&",fact:"m17-worker-journey-results.json: only source_sha differs",report:"m17-worker-journey-results.json",edit:"source_sha"},
 {id:"verify-g17-manifest.mjs@c256",file:"scripts/verify-g17-manifest.mjs",line:286,from:"||",to:"&&",fact:"m17-worker-journey-results.json: only project differs",report:"m17-worker-journey-results.json",edit:"project"},
 {id:"verify-g17-manifest.mjs@c200",file:"scripts/verify-g17-manifest.mjs",line:229,from:"<= 10",to:"< 10",fact:"m17-performance-raw.json: recorded pooled p95 increase of exactly 10",report:"m17-performance-raw.json",edit:"p95_ten"},
 {id:"verify-g17-manifest.mjs@c204",file:"scripts/verify-g17-manifest.mjs",line:231,from:"<= 15",to:"< 15",fact:"m17-performance-raw.json: recorded mean memory increase of exactly 15",report:"m17-performance-raw.json",edit:"mem_fifteen"}
];
const out=[];
for(const c of cases){
  const file=path.join(repo,c.file);const orig=fs.readFileSync(file,"utf8");const lines=orig.split("\n");
  const run=()=>{const r=spawnSync(process.execPath,[child,repo,c.report,c.edit],{encoding:"utf8"});
    try{return JSON.parse(r.stdout.trim().split("\n").pop());}catch{return {unparsed:(r.stdout+r.stderr).slice(0,400)};}};
  const unmutated=run();
  if(!lines[c.line-1].includes(c.from))throw Error("mutation site moved: "+c.id);
  lines[c.line-1]=lines[c.line-1].replace(c.from,c.to);fs.writeFileSync(file,lines.join("\n"));
  let mutant;try{mutant=run();}finally{fs.writeFileSync(file,orig);}
  out.push({id:c.id,line:c.line,mutation:c.from+" -> "+c.to,fact:c.fact,unmutated,mutant,
    both_reject:unmutated.threw===true&&mutant.threw===true,
    same_rejecting_check:unmutated.threw===true&&mutant.threw===true&&unmutated.message===mutant.message&&
      JSON.stringify(unmutated.first_frames)===JSON.stringify(mutant.first_frames)});
}
console.log(JSON.stringify(out,null,2));
