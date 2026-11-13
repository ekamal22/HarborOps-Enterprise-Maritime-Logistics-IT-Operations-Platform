import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {verifyPlaywrightCases,verifyTrxCases} from "./g17-results-contract.mjs";

const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const identity=id=>/^G17-S(?:0[1-9]|1[0-9]|2[0-5])-A0[1-3]$/.test(id);
function requireTruth(ok,message) {if(!ok)throw Error(message);}
const source=relative=>fs.readFileSync(path.join(repo,relative),"utf8");
const load=(root,file)=>JSON.parse(fs.readFileSync(path.join(root,file),"utf8"));
const REQUIRED_SCRIPT_REPORTS=new Map([
  ["scripts/check-g17-network.mjs","m17-network-results.json"],
  ["scripts/check-g17-worker.mjs","m17-worker-results.json"],
  ["scripts/check-g17-collector-fault.mjs","m17-collector-fault-results.json"],
  ["scripts/check-g17-database-fault.mjs","m17-database-fault-results.json"],
  ["scripts/check-g17-restart.mjs","m17-restart-results.json"],
  ["scripts/check-g17-export-privacy.mjs","m17-export-privacy-results.json"],
  ["scripts/check-g17-performance.mjs","m17-performance-raw.json"],
  ["scripts/check-g17-storage.mjs","m17-storage-results.json"],
  ["scripts/check-g17-retention.mjs","m17-retention-age-results.json"],
  ["scripts/check-g17-tempo-retention.mjs","m17-tempo-retention-results.json"],
  ["scripts/check-g17-retention-matrix.mjs","m17-retention-matrix-results.json"],
  ["scripts/check-g17-alert-matrix.mjs","m17-alert-matrix-results.json"],
  ["scripts/check-g17-pressure-matrix.mjs","m17-pressure-matrix-results.json"],
  ["scripts/check-g17-regression-matrix.mjs","g17-regression-matrix-results.json"]
]);

export function checkPerAssertion(coverage,hasWitness) {
  const errors=[],records=[],seen=new Set();
  requireTruth(coverage.scenarios?.length===25,
    "A complete 25-scenario coverage contract is required");
  for(let i=1;i<=25;i++) {
    const group=coverage.scenarios[i-1];
    const id="G17-S"+String(i).padStart(2,"0");
    if(group.id!==id||group.assertions?.length!==3)
      errors.push("Missing or reordered scenario: "+id);
    for(let n=1;n<=3;n++) {
      const clause=group.assertions?.[n-1]||{};
      const key=id+"-A0"+n;
      if(clause.id!==key||!identity(key)||seen.has(key))
        errors.push("Missing or duplicate assertion "+key);
      seen.add(key);
      if(i===25) {
        if(clause.assessment!=="INDEPENDENT_REVIEW"||clause.witness!==null)
          errors.push(key+": independent reviewer must remain unassigned");
        records.push({id:key,status:"PENDING_INDEPENDENT_REVIEW",
          expected:clause.expected,source_artifact:null});
        continue;
      }
      if(clause.assessment!=="TEST_AUTHORED" || !clause.witness) {
        errors.push(key+": technical test missing or partial");
        records.push({id:key,status:"NOT_EXECUTED",expected:clause.expected,
          source_artifact:null});
        continue;
      }
      const w=clause.witness;
      const result=hasWitness(w,key);
      if(!result?.passed)errors.push(key+": missing or nonpassing raw witness");
      records.push({id:key,status:result?.passed?"TECHNICAL_PASS":"NOT_EXECUTED",
        expected:clause.expected,witness:w,
        source_artifact:result?.artifact??null});
    }
  }
  if(seen.size!==75)errors.push("Not all 75 atomic IDs are unique");
  const technical_passes=records.filter(x=>x.status==="TECHNICAL_PASS").length;
  const independent_pending=records.filter(x=>x.status==="PENDING_INDEPENDENT_REVIEW").length;
  if(technical_passes!==72||independent_pending!==3)
    errors.push("Full campaign requires 72 technical witnesses and 3 independent clauses");
  return {gate:"G17",technical_passes,independent_pending,
    total_assertions:records.length,pass:errors.length===0,
    errors,records,acceptance:"PENDING"};
}

function nodeTestNames(xml) {
  if(/<(?:failure|error|skipped)\b/i.test(xml))
    throw Error("Source Node JUnit contains a failed or skipped test");
  const all=[...xml.matchAll(/<testcase\b[^>]*name="([^"]+)"[^>]*>/g)].map(x=>
    x[1].replaceAll("&quot;",'"').replaceAll("&amp;","&")
      .replaceAll("&lt;","<").replaceAll("&gt;",">"));
  requireTruth(all.length>0 && new Set(all).size===all.length,
    "Node JUnit has no unique executed cases");
  return new Set(all);
}

export function deriveWitnessChecks(evidenceRoot,runStatus,sha) {
  const results=new Map();
  const put=(name,caseName,artifact)=>results.set(name+"\u0000"+caseName,
    {passed:true,artifact});
  const trx=[
    ["tests/HarborOps.ContractTests/","g17-contract.trx"],
    ["tests/HarborOps.IntegrationTests/","g17-integration.trx"],
    ["tests/HarborOps.UnitTests/","g17-unit.trx"]
  ];
  for(const [prefix,file] of trx) {
    const raw=fs.readFileSync(path.join(evidenceRoot,file),"utf8");
    const parsed=verifyTrxCases(raw);
    for(const method of parsed.methods)
      results.set(prefix+"\u0000"+method,{passed:true,artifact:file});
  }
  const node=nodeTestNames(fs.readFileSync(path.join(evidenceRoot,
    "g17-node-tests.xml"),"utf8"));
  for(const title of node)results.set("node\u0000"+title,
    {passed:true,artifact:"g17-node-tests.xml"});
  const browsers=[
    ["tests/browser/m17/dashboard.spec.mjs","m17-browser-results.json","dashboard"],
    ["tests/browser/m17/cardinality.spec.mjs","m17-cardinality-results.json","cardinality"]
  ];
  for(const [name,file,mode] of browsers) {
    const tested=verifyPlaywrightCases(load(evidenceRoot,file),mode);
    for(const entry of tested.case_ids)
      results.set(name+"\u0000"+entry,{passed:true,artifact:file});
  }
  for(const [script,file] of REQUIRED_SCRIPT_REPORTS) {
    const result=load(evidenceRoot,file);
    requireTruth(result.source_sha===sha &&
      result.project===runStatus.project && result.result==="PASS",
      script+": failed or foreign execution report");
    results.set(script+"\u0000*",{passed:true,artifact:file});
  }
  requireTruth(runStatus.stage_results.some(x=>
    x.stage==="promtool-test" && x.status==="PASS"),
    "No passing Prometheus rule fixture stage");
  requireTruth(fs.readFileSync(path.join(evidenceRoot,"m17-promtool-test.txt"),"utf8")
    .includes("SUCCESS"),"Prometheus rule fixtures did not report SUCCESS");
  results.set("monitoring/m17/alert-test.yaml\u0000*",
    {passed:true,artifact:"m17-promtool-test.txt"});
  requireTruth(runStatus.run_state==="COMPLETE" &&
    runStatus.stage_results.length>10 &&
    runStatus.stage_results.every(x=>x.status==="PASS" && x.exit_code===0),
    "One or more source-locked verification stages did not succeed");
  results.set("scripts/run-g17-local.ps1\u0000*",
    {passed:true,artifact:"run-status.json"});
  return function resolve(w,key) {
    const supported=w.path.startsWith("tests/")||
      w.path.startsWith("scripts/")||
      w.path==="monitoring/m17/alert-test.yaml";
    if(!supported||!w.case||!w.path||w.path.includes(".."))return null;
    const original=source(w.path);
    if(!original.includes(w.case))return null;
    if(w.path.startsWith("tests/HarborOps.")) {
      const group=trx.find(([prefix])=>w.path.startsWith(prefix));
      if(!group)return null;
      const className=path.posix.basename(w.path,".cs");
      const methodName=w.case.split("(")[0];
      const hits=[...results.entries()].filter(([key])=>
        key.startsWith(group[0]+"\u0000") &&
        (key.includes("."+className+"."+methodName+"(") ||
          key.endsWith("."+className+"."+methodName)));
      return hits.length>=1 ? hits[0][1] : null;
    }
    if(w.path.startsWith("tests/browser/")) {
      const required=w.path.includes("dashboard.spec.mjs")
        ? ["chromium","firefox","webkit"] : ["chromium"];
      const found=required.map(engine=>results.get(w.path+"\u0000"+engine+"::"+w.case));
      return found.every(Boolean) ? {passed:true,artifact:found[0].artifact} : null;
    }
    if(w.path.endsWith(".test.mjs"))
      return results.get("node\u0000"+w.case)??null;
    if(w.path==="monitoring/m17/alert-test.yaml")
      return results.get(w.path+"\u0000*")??null;
    if(w.path==="scripts/run-g17-local.ps1")
      return results.get(w.path+"\u0000*")??null;
    if(REQUIRED_SCRIPT_REPORTS.has(w.path))
      return results.get(w.path+"\u0000*")??null;
    return null;
  };
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const evidenceRoot=process.argv[2],sha=process.argv[3];
  if(!evidenceRoot||!/^[a-f0-9]{40}$/.test(sha))
    throw Error("Usage: node scripts/reconcile-g17-assertions.mjs <evidence-root> <source-sha>");
  const actual=load(evidenceRoot,"run-status.json");
  requireTruth(actual.source_sha===sha && actual.source_verified===true,
    "Candidate run ledger source differs from requested source");
  const coverage=JSON.parse(source("docs/M17_G17_TEST_COVERAGE_20261113.json"));
  const resolved=deriveWitnessChecks(evidenceRoot,actual,sha);
  const ledger=checkPerAssertion(coverage,resolved);
  fs.writeFileSync(path.join(evidenceRoot,"g17-technical-assertion-ledger.json"),
    JSON.stringify({source_sha:sha,project:actual.project,...ledger},null,2)+"\n",
    {flag:"wx"});
  if(!ledger.pass)throw Error("Missing or skipped technical witnesses: "+
    ledger.errors.slice(0,10).join("; "));
  console.log("72/72 separately witnessed technical G17 assertions; independent S25 pending");
}
