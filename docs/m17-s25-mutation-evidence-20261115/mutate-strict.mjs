// usage: node mutate-strict.mjs <repo-copy> <target> <tests> <modulus> <remainder> <out.jsonl>
// Exhaustive and strict. Only a genuine failing test counts as a kill:
//  - an unmutated CONTROL run of the same test set must pass before and after the shard;
//  - a mutant is KILLED only when node exits with status 1, no signal, no spawn error, no timeout, no
//    infrastructure signature in the output and at least one failing test that is not a file-level load failure;
//  - a mutant whose module no longer loads is KILLED_LOAD_ERROR (counted separately, not a behavioural kill);
//  - anything else abnormal is INVALID_INFRASTRUCTURE, retried once, and reported (never counted as a kill).
// The target file hash is checked after every mutant so a leaked mutation cannot affect a later one.
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {spawnSync} from "node:child_process";
const [repo,target,testsArg,modArg,remArg,out]=process.argv.slice(2);
const tests=testsArg.split(","),mod=Number(modArg),rem=Number(remArg);
const file=path.join(repo,target);const original=fs.readFileSync(file,"utf8");const lines=original.split("\n");
const sha=s=>crypto.createHash("sha256").update(s).digest("hex");const originalHash=sha(original);
const ops=[[/===/,"!=="],[/!==/,"==="],[/>=/,">"],[/<=/,"<"],[/&&/,"||"],[/\|\|/,"&&"],[/\btrue\b/,"false"],[/\bfalse\b/,"true"],
 [/\.length>0/,".length>1000"],[/\.length===(\d+)/,m=>".length==="+(Number(m[1])+1)],[/ > (\d+)/,m=>" > "+(Number(m[1])+1000)],[/ < (\d+)/,()=>" < 0"]];
const opNames=["===>!==","!==>===",">=>>","<=><","&&>||","||>&&","true>false","false>true",".length>0>.length>1000",".length===N>N+1"," > N>N+1000"," < N>< 0"];
const cands=[];
for(let i=0;i<lines.length;i++){
  const line=lines[i],t=line.trim();
  if(t.startsWith("//")||t.startsWith("*")||t===""||/^import\s/.test(t))continue;
  if(!/(assert|return|=>|if\s*\(|\?|&&|\|\||===|!==|>=|<=)/.test(line))continue;
  for(let k=0;k<ops.length;k++){const [re,rep]=ops[k];const m=line.match(re);if(!m)continue;
    const r=typeof rep==="function"?line.replace(re,rep(m)):line.replace(re,rep);if(r!==line)cands.push({line:i,op:k,replaced:r});}
}
const INFRA=/EADDRINUSE|EMFILE|ENOSPC|ENOMEM|EBUSY|EPERM|EACCES|ETIMEDOUT|spawn .* ENOENT|heap out of memory|Cannot find module|ERR_MODULE_NOT_FOUND/;
function run(){
  const r=spawnSync(process.execPath,["--test",...tests],{cwd:repo,encoding:"utf8",timeout:180000});
  const stdout=r.stdout||"",stderr=r.stderr||"";
  const failed=[...new Set([...stdout.matchAll(/^\s*✖ (.+?) \(\d/gm)].map(m=>m[1]))];
  const fileLevel=failed.filter(n=>/\.mjs$/.test(n));
  const behavioural=failed.filter(n=>!/\.mjs$/.test(n));
  const pass=Number((stdout.match(/^ℹ pass (\d+)/m)||[])[1]??-1),fail=Number((stdout.match(/^ℹ fail (\d+)/m)||[])[1]??-1);
  return {status:r.status,signal:r.signal||null,error:r.error?String(r.error.code||r.error.message):null,
    timed_out:Boolean(r.error&&r.error.code==="ETIMEDOUT"),failed,behavioural,fileLevel,pass,fail,
    infra:INFRA.test(stdout+stderr),syntax:/SyntaxError/.test(stdout+stderr)};
}
function classify(r){
  if(r.signal||r.error||r.timed_out||r.status===null)return "INVALID_INFRASTRUCTURE";
  if(r.status===0)return r.fail===0?"SURVIVED":"INVALID_INFRASTRUCTURE";
  if(r.infra)return "INVALID_INFRASTRUCTURE";
  if((r.syntax||(r.pass===0&&r.fileLevel.length>0))&&r.behavioural.length===0)return "KILLED_LOAD_ERROR";
  if(r.behavioural.length>=1&&r.fail>=1)return "KILLED";
  return "INVALID_INFRASTRUCTURE";
}
const control=()=>{const r=run();return {status:r.status,pass:r.pass,fail:r.fail,ok:r.status===0&&r.fail===0&&r.pass>0};};
const before=control();
if(!before.ok){console.log(JSON.stringify({target,shard:rem+"/"+mod,aborted:"CONTROL_FAILED_BEFORE",before}));process.exit(2);}
const ONLY=process.env.ONLY?new Set(process.env.ONLY.split(",").map(Number)):null;
let ord=0;
const recs=[];let leaked=0;
try{
  cands.forEach((c,idx)=>{
    if(ONLY&&!ONLY.has(idx+1))return;
    if((ONLY?ord++:idx)%mod!==rem)return;
    const m=[...lines];m[c.line]=c.replaced;fs.writeFileSync(file,m.join("\n"));
    let r=run(),status=classify(r),attempts=1;
    if(status==="INVALID_INFRASTRUCTURE"){r=run();status=classify(r);attempts=2;}
    fs.writeFileSync(file,original);
    if(sha(fs.readFileSync(file,"utf8"))!==originalHash)leaked++;
    recs.push({id:path.basename(target)+"@c"+String(idx+1).padStart(3,"0"),target,line:c.line+1,operator:opNames[c.op],
      original:lines[c.line].trim(),mutated:c.replaced.trim(),status,attempts,exit_status:r.status,signal:r.signal,error:r.error,
      timed_out:r.timed_out,tests_failed:r.fail,tests_passed:r.pass,killed_by:r.behavioural.slice(0,5)});
  });
}finally{fs.writeFileSync(file,original);}
const after=control();
fs.writeFileSync(out,recs.map(r=>JSON.stringify(r)).join("\n")+"\n");
const n=s=>recs.filter(r=>r.status===s).length;
console.log(JSON.stringify({target,candidates:cands.length,shard:rem+"/"+mod,tried:recs.length,killed:n("KILLED"),killed_load_error:n("KILLED_LOAD_ERROR"),
  survived:n("SURVIVED"),invalid_infrastructure:n("INVALID_INFRASTRUCTURE"),leaked_mutation_files:leaked,control_before:before,control_after:after,
  target_hash_unchanged:sha(fs.readFileSync(file,"utf8"))===originalHash}));
