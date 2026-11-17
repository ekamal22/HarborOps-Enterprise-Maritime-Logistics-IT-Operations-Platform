// Evidence for the P09 floating point boundary survivors (g17-p09-review.mjs L72 and the same predicate in g17-raw-witness).
// pct(after,before) = 100*(after/before-1), exactly as in scripts/g17-p09-review.mjs.
const pct=(a,b)=>100*(a/b-1);
const buf=new DataView(new ArrayBuffer(8));
const next=(x,k)=>{buf.setFloat64(0,x);let v=buf.getBigUint64(0)+BigInt(k);buf.setBigUint64(0,v);return buf.getFloat64(0);};
const rows=[];
for(let k=-4;k<=4;k++){const r=next(1.1,k);const d=r-1;rows.push({ulp_offset_from_1_1:k,ratio:r,ratio_minus_one:d,pct:100*d,pct_equals_10:100*d===10});}
console.log("Ratios adjacent to 1.1 and the percentage they produce (r-1 is exact for r in [1,2)):");
console.table(rows);
// The double nearest 0.1 is not a multiple of 2^-52, so no ratio r in [1,2) can give r-1 === 0.1.
const tenth=3602879701896397n; // 0.1 = 3602879701896397 / 2^55
console.log("numerator of double(0.1) over 2^55:",tenth.toString(),"odd:",tenth%2n===1n,"-> not representable as k/2^52");
// Exhaustive-style scan: many denominators and numerators, count how many give exactly 10.
let hits=0,scanned=0;
for(const off of [1,2,3,5,7,8,10,12.5,16,20,25,33.3,40,50,64,100,123.456,1000]){
  const base=off*1.1;
  for(let k=-2000;k<=2000;k++){const on=next(base,k);scanned++;if(pct(on,off)===10)hits++;}
}
console.log(JSON.stringify({scanned,pairs_giving_exactly_10:hits}));
// What this means for the gate: increase_pct<=10 and increase_pct<10 differ only at exactly 10.
console.log("consequence: '<= 10' and '< 10' are equivalent for every computable increase; the largest value that passes is",
  Math.max(...rows.filter(x=>x.pct<10).map(x=>x.pct)),"and the smallest that fails is",Math.min(...rows.filter(x=>x.pct>10).map(x=>x.pct)));
