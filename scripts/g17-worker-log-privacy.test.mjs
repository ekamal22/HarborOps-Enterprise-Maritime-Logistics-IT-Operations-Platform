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
 assert.throws(()=>verifyOwnedWorkerLog("",secrets));
 assert.throws(()=>verifyOwnedWorkerLog("api-1 | no worker",secrets));
 for(const secret of secrets)
  assert.throws(()=>verifyOwnedWorkerLog(logs+"\n"+secret,secrets));
 assert.throws(()=>verifyOwnedWorkerLog(logs+"\nBearer abcdefghijklmnopqrstuvwxyz01234",secrets));
 assert.throws(()=>verifyOwnedWorkerLog(logs,[]));
});
