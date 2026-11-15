// usage: node mutate-record.mjs <repo-copy> <target.mjs> <test1.mjs,test2.mjs> <maxMutants> <out.jsonl>
// Same candidate generation and deterministic spread selection as mutate.mjs, but EVERY tried mutant is
// recorded (id, location, operator, original and mutated line, killed or survived, killing test names).
import fs from "node:fs";
import path from "node:path";
import {spawnSync} from "node:child_process";
const [repo,target,testsArg,maxArg,out]=process.argv.slice(2);
const tests=testsArg.split(",");
const file=path.join(repo,target);
const original=fs.readFileSync(file,"utf8");
const lines=original.split("\n");
const ops=[
  [/===/,"!=="],[/!==/,"==="],[/>=/,">"],[/<=/,"<"],[/&&/,"||"],[/\|\|/,"&&"],
  [/\btrue\b/,"false"],[/\bfalse\b/,"true"],[/\.length>0/,".length>1000"],
  [/\.length===(\d+)/,m=>".length==="+(Number(m[1])+1)],[/ > (\d+)/,m=>" > "+(Number(m[1])+1000)],
  [/ < (\d+)/,m=>" < 0"]
];
const opNames=["===>!==","!==>===",">=>>","<=><","&&>||","||>&&","true>false","false>true",".length>0>.length>1000",".length===N>N+1"," > N>N+1000"," < N>< 0"];
const candidates=[];
for(let i=0;i<lines.length;i++){
  const line=lines[i];
  const trimmed=line.trim();
  if(trimmed.startsWith("//")||trimmed.startsWith("*")||trimmed===""||/^import\s/.test(trimmed))continue;
  if(!/(assert|return|=>|if\s*\(|\?|&&|\|\||===|!==|>=|<=)/.test(line))continue;
  for(let k=0;k<ops.length;k++){
    const [re,rep]=ops[k];
    const m=line.match(re);
    if(!m)continue;
    const replaced=typeof rep==="function"?line.replace(re,rep(m)):line.replace(re,rep);
    if(replaced!==line)candidates.push({line:i,op:k,replaced});
  }
}
const max=Number(maxArg||80);
const step=Math.max(1,Math.floor(candidates.length/max));
const picked=candidates.filter((_,i)=>i%step===0).slice(0,max);
const records=[];
try{
  let n=0;
  for(const mutant of picked){
    n++;
    const mutated=[...lines];mutated[mutant.line]=mutant.replaced;
    fs.writeFileSync(file,mutated.join("\n"));
    const run=spawnSync(process.execPath,["--test",...tests],{cwd:repo,encoding:"utf8",timeout:120000});
    const killedBy=[...new Set([...(run.stdout||"").matchAll(/^\s*✖ (.+?) \(\d/gm)].map(m=>m[1]))].slice(0,5);
    records.push({id:target.replace(/^scripts\//,"")+"#"+String(n).padStart(3,"0"),target,line:mutant.line+1,
      operator:opNames[mutant.op],original:lines[mutant.line].trim(),mutated:mutant.replaced.trim(),
      status:run.status!==0?"killed":"survived",killed_by:run.status!==0?killedBy:[]});
  }
}finally{fs.writeFileSync(file,original);}
fs.writeFileSync(out,records.map(r=>JSON.stringify(r)).join("\n")+"\n");
console.log(JSON.stringify({target,candidates:candidates.length,tried:records.length,
  killed:records.filter(r=>r.status==="killed").length,survived:records.filter(r=>r.status==="survived").length}));
