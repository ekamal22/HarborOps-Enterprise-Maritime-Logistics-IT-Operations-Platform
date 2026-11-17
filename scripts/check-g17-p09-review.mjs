import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {recalculateP09} from "./g17-p09-review.mjs";
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
assert.match(sha??"",/^[0-9a-f]{40}$/);
assert.match(project??"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(root&&fs.existsSync(root));
const input=JSON.parse(fs.readFileSync(path.join(root,"m17-performance-raw.json"),"utf8"));
assert.equal(input.source_sha,sha);
assert.equal(input.project,project);
const reviewed=recalculateP09(input);
assert.equal(reviewed.result,"PASS","Approved P09 gate must not be relaxed");
fs.writeFileSync(path.join(root,"m17-p09-independent-recalculation.json"),
 JSON.stringify({source_sha:sha,project,...reviewed},null,2)+"\n",{flag:"wx"});
