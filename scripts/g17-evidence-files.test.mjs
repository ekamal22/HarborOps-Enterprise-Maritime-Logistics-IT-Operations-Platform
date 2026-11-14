import test from "node:test";
import assert from "node:assert/strict";
import {allowedEvidencePath,textEvidencePath} from "./g17-evidence-files.mjs";
test("owned failure service logs are permitted and scanned as text evidence",()=>{
  for(const service of ["postgres","postgres_tests","migrator","api","web",
    "otelcol","tempo","prometheus","grafana","dashboard_gateway","worker"]) {
    const p="m17-failure-diagnostics/"+service+".log";
    assert.equal(allowedEvidencePath(p),true);
    assert.equal(textEvidencePath(p),true);
  }
});
test("non-owned logs and traversal cannot enter the manifest",()=>{
  for(const p of ["private.log","other/worker.log",
    "m17-failure-diagnostics/unknown.log","../m17-failure-diagnostics/api.log",
    "/m17-failure-diagnostics/api.log","m17-failure-diagnostics\\api.log",
    "nested/../m17-failure-diagnostics/api.log"]) {
    assert.equal(allowedEvidencePath(p),false,p);
    assert.equal(textEvidencePath(p),false,p);
  }
  assert.equal(allowedEvidencePath("g17-unit.trx"),true);
  assert.equal(textEvidencePath("g17-unit.trx"),true);
  assert.equal(allowedEvidencePath("browser-artifacts/shot.png"),true);
  assert.equal(textEvidencePath("browser-artifacts/shot.png"),false);
});
