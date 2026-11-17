// Read-only variance analysis of the preserved f6 and f7 raw P09 evidence. It never changes an acceptance rule.
import fs from "node:fs";
import {recalculateP09} from "file:///E:/APP%20IDEAS/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/.claude/worktrees/harborops-g17-s25-audit-6439ae/scripts/g17-p09-review.mjs";
const dir="E:/HarborOps-Verification/g17-s25-audit/";
const load=id=>JSON.parse(fs.readFileSync(dir+"final-"+id+"-evidence/m17-performance-raw.json","utf8"));
const raw={f6:load("f6"),f7:load("f7")};
const p95=a=>{const s=[...a].sort((x,y)=>x-y);return s[Math.ceil(s.length*0.95)-1];};
const q=(a,p)=>{const s=[...a].sort((x,y)=>x-y);return s[Math.min(s.length-1,Math.ceil(s.length*p)-1)];};
const mean=a=>a.reduce((x,y)=>x+y,0)/a.length;
const sd=a=>{const m=mean(a);return Math.sqrt(a.reduce((x,y)=>x+(y-m)**2,0)/(a.length-1));};
const pct=(on,off)=>100*(on/off-1);
let seed=12345;const rnd=()=>{seed=(seed*1664525+1013904223)%4294967296;return seed/4294967296;};
const out={};
for(const id of ["f6","f7"]) {
  const r=raw[id];
  const approved=recalculateP09(r,r.rows[0].raw_ms.baseline.length);
  const rows=r.rows.map((row,order)=>({order:order+1,round:row.round,on:row.telemetry_enabled,rss_peak:row.rss_peak_mib,
    routes:Object.fromEntries(["baseline","authorized"].map(route=>{
      const a=row.raw_ms[route];const med=q(a,0.5);
      // rolling 100-sample p95 along execution order: a burst shows as one contiguous high window
      const w=100;let worst=0,worstAt=0;
      for(let i=0;i+w<=a.length;i+=10){const v=p95(a.slice(i,i+w));if(v>worst){worst=v;worstAt=i;}}
      const slow=a.map(x=>x>2*med);const slowCount=slow.filter(Boolean).length;
      // share of the slow samples that sit inside the single densest 10 percent index span
      const span=Math.floor(a.length/10);let best=0;for(let i=0;i+span<=a.length;i+=20){let c=0;for(let j=i;j<i+span;j++)if(slow[j])c++;best=Math.max(best,c);}
      return [route,{n:a.length,mean:+mean(a).toFixed(2),median:+med.toFixed(2),p90:+q(a,0.9).toFixed(2),p95:+p95(a).toFixed(2),p99:+q(a,0.99).toFixed(2),
        max:+Math.max(...a).toFixed(1),sd:+sd(a).toFixed(2),slow_over_2x_median:slowCount,
        slow_in_densest_tenth_pct:slowCount?+(100*best/slowCount).toFixed(1):0,worst_100_window_p95:+worst.toFixed(1),worst_window_start:worstAt}];}))}));
  // noise floor: the same configuration (telemetry off) measured in three rounds, no telemetry involved
  const offRounds=r.rows.filter(x=>!x.telemetry_enabled).sort((a,b)=>a.round-b.round);
  const onRounds=r.rows.filter(x=>x.telemetry_enabled).sort((a,b)=>a.round-b.round);
  const noise={};
  for(const route of ["baseline","authorized"]) {
    const o=offRounds.map(x=>p95(x.raw_ms[route])),n=onRounds.map(x=>p95(x.raw_ms[route]));
    noise[route]={off_round_p95:o.map(x=>+x.toFixed(2)),on_round_p95:n.map(x=>+x.toFixed(2)),
      off_round_range_pct_of_mean:+(100*(Math.max(...o)-Math.min(...o))/mean(o)).toFixed(1),
      on_round_range_pct_of_mean:+(100*(Math.max(...n)-Math.min(...n))/mean(n)).toFixed(1)};
  }
  // block bootstrap (blocks of 50 consecutive samples) of the pooled p95 increase
  const pooledCI={};
  for(const route of ["baseline","authorized"]) {
    const blocks=(rowSet)=>rowSet.map(x=>{const a=x.raw_ms[route],b=[];for(let i=0;i+50<=a.length;i+=50)b.push(a.slice(i,i+50));return b;});
    const offB=blocks(offRounds),onB=blocks(onRounds);
    const draw=bs=>bs.flatMap(rb=>Array.from({length:rb.length},()=>rb[Math.floor(rnd()*rb.length)]).flat());
    const reps=[];for(let k=0;k<2000;k++)reps.push(pct(p95(draw(onB)),p95(draw(offB))));
    reps.sort((a,b)=>a-b);
    pooledCI[route]={estimate:+approved.pooled[route].increase_pct.toFixed(2),ci95:[+reps[49].toFixed(2),+reps[1949].toFixed(2)],
      share_of_bootstrap_over_10:+(100*reps.filter(x=>x>10).length/reps.length).toFixed(1)};
  }
  // sensitivity only (not an acceptance rule): the pooled authorized p95 without each single row
  const sens={};
  for(const route of ["authorized"]) {
    for(const drop of onRounds) {
      const on=onRounds.filter(x=>x!==drop).flatMap(x=>x.raw_ms[route]),off=offRounds.filter(x=>x.round!==drop.round).flatMap(x=>x.raw_ms[route]);
      sens["without_round_"+drop.round+"_pair_"+route]=+pct(p95(on),p95(off)).toFixed(2);
    }
  }
  out[id]={result:approved.result,pooled:Object.fromEntries(Object.entries(approved.pooled).map(([k,v])=>[k,{off_p95:+v.off_p95_ms.toFixed(2),on_p95:+v.on_p95_ms.toFixed(2),increase_pct:+v.increase_pct.toFixed(2)}])),
    mean_memory_pct:+approved.mean_memory_increase_pct.toFixed(2),pairs:approved.pairs.map(p=>({round:p.round,baseline:+p.routes.baseline.increase_pct.toFixed(2),authorized:+p.routes.authorized.increase_pct.toFixed(2),memory:+p.memory_increase_pct.toFixed(2)})),
    execution_order:r.rows.map(x=>x.round+(x.telemetry_enabled?"-on":"-off")).join(" "),rows,noise,pooled_bootstrap:pooledCI,sensitivity_authorized:sens,
    individual_outliers:approved.individual_p95_outliers.map(x=>x.round),system:r.system,sample_size_per_mode_per_round:r.sample_size_per_mode_per_round};
}
// the same source measured twice: all six pairs pooled with the approved estimator, reported as a pooled characterization
const both=["baseline","authorized"].map(route=>{
  const on=[...raw.f6.rows,...raw.f7.rows].filter(x=>x.telemetry_enabled).flatMap(x=>x.raw_ms[route]);
  const off=[...raw.f6.rows,...raw.f7.rows].filter(x=>!x.telemetry_enabled).flatMap(x=>x.raw_ms[route]);
  return [route,{n_per_mode:on.length,off_p95:+p95(off).toFixed(2),on_p95:+p95(on).toFixed(2),increase_pct:+pct(p95(on),p95(off)).toFixed(2)}];});
out.six_pairs_pooled_characterization=Object.fromEntries(both);
out.rss={f6:raw.f6.rows.map(x=>x.rss_peak_mib),f7:raw.f7.rows.map(x=>x.rss_peak_mib)};
fs.writeFileSync(dir+"preserved-20261115/p09-variance-analysis.json",JSON.stringify(out,null,2)+"\n");
console.log(JSON.stringify({f6:{r:out.f6.result,pooled:out.f6.pooled,order:out.f6.execution_order,boot:out.f6.pooled_bootstrap,noise:out.f6.noise,sens:out.f6.sensitivity_authorized},f7:{r:out.f7.result,pooled:out.f7.pooled,order:out.f7.execution_order,boot:out.f7.pooled_bootstrap,noise:out.f7.noise,sens:out.f7.sensitivity_authorized},six:out.six_pairs_pooled_characterization,rss:out.rss},null,1));
