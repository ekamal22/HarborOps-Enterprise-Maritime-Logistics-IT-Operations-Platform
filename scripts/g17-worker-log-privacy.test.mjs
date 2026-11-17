import test from "node:test";
import assert from "node:assert/strict";
import {verifyOwnedWorkerLog} from "./g17-worker-log-privacy.mjs";
const logs="worker-1  | Tracking work polling started\nworker-1  | Tracking worker disposition NeedsReconciliation";
const secrets=["g17-example-private-account","g17-private-org-source",
 "g17-private-receipt-uuid","g17-private-bearer-credential"];
test("real worker log with synthetic private controls is safe",()=>{
 assert.equal(verifyOwnedWorkerLog(logs,secrets).checked_values,4);
});
test("missing worker signal and private string leaks fail closed",()=>{
 assert.throws(()=>verifyOwnedWorkerLog("",secrets),/real bounded disposable worker log/);
 assert.throws(()=>verifyOwnedWorkerLog("api-1 | no worker",secrets),/positive working-service log/);
 for(const secret of secrets)
  assert.throws(()=>verifyOwnedWorkerLog(logs+"\n"+secret,secrets),/synthetic private identifier/);
 assert.throws(()=>verifyOwnedWorkerLog(logs+"\nBearer abcdefghijklmnopqrstuvwxyz01234",secrets),
  /credential-shaped value/);
 assert.throws(()=>verifyOwnedWorkerLog(logs,[]),/negative synthetic canary controls/);
});
test("a credential, account or tenant shape not on the canary list is still rejected",()=>{
 for(const injected of [
  "hop_v1_"+"b".repeat(32)+"_abcdefghijklmnopqrstu",
  "g17-0a1b2c3d@example.test","g17-worker-0a1b2c3d4e5f-7"])
  assert.throws(()=>verifyOwnedWorkerLog(logs+"\n"+injected,secrets),
   /credential, account or tenant shaped value/);
});

test("each canary must be a real string of at least eight characters",()=>{
  assert.equal(verifyOwnedWorkerLog(logs,["12345678","abcdefgh","ABCDEFGH","zzzzzzzz"]).checked_values,4);
  for(const bad of ["1234567","",null,undefined,42,{}])
    assert.throws(()=>verifyOwnedWorkerLog(logs,[bad,"abcdefgh","ABCDEFGH","zzzzzzzz"]));
});
