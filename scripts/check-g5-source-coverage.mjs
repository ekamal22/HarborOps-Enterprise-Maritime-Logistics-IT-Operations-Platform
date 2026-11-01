#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const read=p=>fs.readFileSync(p,"utf8");
const checker=read("scripts/assert-g5-evidence.mjs");
const integrationDir="tests/HarborOps.IntegrationTests";
const unitDir="tests/HarborOps.UnitTests";
const load=dir=>fs.readdirSync(dir).filter(f=>f.endsWith(".cs")).map(f=>read(path.join(dir,f)));
const integrationSources=load(integrationDir);
const unitSources=load(unitDir);
for(const [kind,pattern,files] of [
  ["integration",/\bintegration\("([^"]+)"/g,integrationSources],
  ["unit",/\bunit\("([^"]+)"/g,unitSources]
]){
  const names=[...checker.matchAll(pattern)].map(x=>x[1]);
  assert.ok(names.length>0,"No G5 "+kind+" evidence requirements");
  for(const full of names){
    const bits=full.split(".");
    assert.equal(bits.length,2,"Invalid G5 method identifier "+full);
    const [name,method]=bits;
    const classToken=new RegExp("\\bclass\\s+"+name+"\\b");
    const methodToken=new RegExp("\\b"+method+"\\s*\\(");
    assert.ok(files.some(s=>classToken.test(s)&&methodToken.test(s)),
      "No source method for G5 "+kind+" "+full);
  }
}
const browser=read("tests/browser/specs/shipment.spec.mjs");
const declared=new Set([...browser.matchAll(/\btest\("([^"]+)"/g)].map(x=>x[1]));
const required=[...checker.matchAll(/\bbrowser\("([^"]+)"/g)].map(x=>x[1]);
assert.ok(required.length>=8,"G5 browser evidence too narrow");
for(const name of required)assert.ok(declared.has(name),"Missing G5 browser source "+name);
const matrix=[...checker.matchAll(/\["S(\d\d)"/g)].map(x=>Number(x[1]));
assert.deepEqual(matrix,Array.from({length:25},(_,i)=>i+1),"G5 matrix not complete");
const isolated=[
  "scripts/verify-m5-local.sh","scripts/verify-m5-stages-local.sh",
  "scripts/run-m5-browsers-local.sh","scripts/check-m5-scoped-recovery.mjs",
  "scripts/check-m5-reference-outage.mjs","scripts/check-m5-maritime-outage.mjs",
  "scripts/check-m5-shipment-outage.mjs"
];
for(const file of isolated){
  const text=read(file);
  assert.ok(text.includes("ho-verify-g5"),"Wrong G5 project in "+file);
  assert.ok(!/ho-g2-pg|ho-g2-sdk|harborops\.g2|ho-g5-verify/.test(text),
    "Shared verification resource in "+file);
}
assert.ok(checker.includes("integration-fresh")&&checker.includes("integration-repeat"));
assert.ok(checker.includes("g5-teardown.json")&&checker.includes("g5-storage-after.json"));
console.log("G5 mapped source, stage isolation and all 25 scenario identifiers: PASS");
