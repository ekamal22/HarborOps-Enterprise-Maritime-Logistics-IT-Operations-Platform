import path from "node:path";import {pathToFileURL} from "node:url";
const repo=process.argv[2];const {recalculateP09}=await import(pathToFileURL(path.join(repo,"scripts/g17-p09-review.mjs")).href);
const rank95=v=>[...v].sort((a,b)=>a-b)[Math.ceil(v.length*0.95)-1];
function build(n,raw){const rows=[];for(let round=1;round<=3;round++)for(const en of [false,true]){const base=en?10.5:10;const r=Array.from({length:n},()=>base);
 rows.push({round,telemetry_enabled:en,rss_peak_mib:en?110:100,raw_ms:{baseline:raw??[...r],authorized:[...r]},per_route:{baseline:{p95_ms:base},authorized:{p95_ms:base}}});}
 const f=(r,e)=>rows.find(x=>x.round===r&&x.telemetry_enabled===e);
 const pairs=[1,2,3].map(round=>({round,route_pairs:{baseline:{p95_increase_pct:5},authorized:{p95_increase_pct:5}},memory_increase_pct:10}));
 const pooled={};for(const rt of ["baseline","authorized"])pooled[rt]={telemetry_off_samples:3*n,telemetry_on_samples:3*n,telemetry_off_p95_ms:10,telemetry_on_p95_ms:10.5,p95_increase_pct:5};
 return {run_kind:"THREE_MATCHED_PAIRS",gate:{method:"POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS",p95_budget_pct:10,memory_budget_pct:15,mean_memory_increase_pct:10,pass:true,pairs_over_p95_budget:0},rows,pairs,pooled,result:"PASS"};}
const attempt=(label,report,n)=>{try{recalculateP09(report,n);return {label,threw:false};}catch(e){return {label,threw:true,name:e.name,message:String(e.message).split("\n")[0]};}};
const out=[];
out.push(attempt("string of 40 digits in place of the sample array",build(40,"1".repeat(40)),40));
out.push(attempt("array-like object with length 40",build(40,{length:40}),40));
out.push(attempt("empty sample arrays with perRouteSamples 0",build(0),0));
const dup=build(40);dup.rows[1].telemetry_enabled=false;out.push(attempt("rows where round 1 repeats the same mode (six rows, one pairing missing)",dup,40));
const badRound=build(40);badRound.rows[5].round=1;out.push(attempt("rows where round 3 on-mode is replaced by a second round-1 on-mode",badRound,40));
console.log(JSON.stringify(out));
