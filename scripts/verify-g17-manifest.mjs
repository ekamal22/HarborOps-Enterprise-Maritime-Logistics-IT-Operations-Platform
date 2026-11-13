import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

function crawl(root, prefix = "") {
  const files = [];
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const name = path.posix.join(prefix, entry.name);
    if (name === "manifest.json") continue;
    if (entry.isSymbolicLink()) throw Error("Unexpected symlink " + name);
    if (entry.isDirectory()) {
      files.push(...crawl(path.join(root, entry.name), name));
    } else if (entry.isFile()) {
      const bytes = fs.readFileSync(path.join(root, entry.name));
      files.push({
        path: name, bytes: bytes.length,
        sha256: crypto.createHash("sha256").update(bytes).digest("hex")
      });
    } else throw Error("Unsupported evidence object " + name);
  }
  return files.sort((a,b) => a.path.localeCompare(b.path));
}

export function verifyManifest(manifestPath, expectedSha) {
  const root = path.dirname(manifestPath);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  if (manifest.schema_version !== 2 || manifest.gate !== "G17" ||
      manifest.gate_acceptance !== "NOT_ACCEPTED" ||
      !/^[a-f0-9]{40}$/.test(manifest.source_sha)) {
    throw Error("Invalid source-locked G17 candidate manifest header");
  }
  if (expectedSha && manifest.source_sha !== expectedSha)
    throw Error("Wrong source SHA in G17 manifest");
  if (!Array.isArray(manifest.files)) throw Error("Missing file index");
  const actual = crawl(root);
  if (manifest.files.length !== actual.length)
    throw Error("Manifest coverage mismatch, extra or missing files");
  for (let i = 0; i < actual.length; i++) {
    const claimed = manifest.files[i], observed = actual[i];
    if (claimed.path !== observed.path || claimed.sha256 !== observed.sha256 ||
        claimed.bytes !== observed.bytes) throw Error("Evidence tampered or unlisted: " + observed.path);
  }
  const requiredLedger = ["source-sha.txt","expected-artifacts.json","run-status.json"];
  for (const name of requiredLedger)
    if (!actual.some(e => e.path === name))
      throw Error("Missing required G17 run ledger: " + name);
  const status = JSON.parse(fs.readFileSync(path.join(root,"run-status.json"),"utf8"));
  const expected = JSON.parse(fs.readFileSync(path.join(root,"expected-artifacts.json"),"utf8"));
  if (status.schema_version !== 2 || expected.schema_version !== 1 ||
      status.gate !== "G17" || expected.gate !== "G17" ||
      status.source_sha !== manifest.source_sha ||
      expected.source_sha !== manifest.source_sha ||
      status.source_verified !== true ||
      status.requested_source_sha !== manifest.source_sha ||
      status.gate_acceptance !== "NOT_ACCEPTED" ||
      status.g17_assertion_review !== "NOT_REVIEWED" ||
      !["COMPLETE","FAILED"].includes(status.run_state) ||
      status.run_state !== manifest.run_state ||
      !Array.isArray(expected.required_artifacts) ||
      !Array.isArray(status.stage_results))
    throw Error("Invalid or contradictory G17 stage ledger");
  if (JSON.stringify(expected.required_artifacts.slice().sort()) !==
      JSON.stringify(status.required_artifacts.slice().sort()))
    throw Error("Expected artifact contracts differ");
  const fileNames = new Set(actual.map(e => e.path));
  const missing = expected.required_artifacts.filter(p => !fileNames.has(p));
  if (JSON.stringify(missing) !== JSON.stringify(manifest.missing_expected_artifacts))
    throw Error("Manifest expected-artifact discrepancy");
  const complete = status.run_state === "COMPLETE";
  if (complete && (missing.length || status.stage_results.length === 0 ||
      status.stage_results.some(row => row.status !== "PASS" || row.exit_code !== 0)))
    throw Error("Completed run has missing evidence or a failed stage");
  if (complete) {
    // Presence and a green wrapper exit are not enough: inspect raw test outcomes.
    const trxFiles = ["g17-unit.trx","g17-contract.trx",
      ...(expected.disposable_stack ? ["g17-integration.trx"] : [])];
    for (const file of trxFiles) {
      const xml = fs.readFileSync(path.join(root,file),"utf8");
      const counters = xml.match(/<Counters\\b[^>]*\\btotal="(\\d+)"[^>]*>/i)?.[0];
      if (!counters) throw Error("Missing test counters: " + file);
      const attr = key => Number(counters.match(new RegExp('\\b' + key + '="(\\d+)"'))?.[1] ?? -1);
      if (!(attr("total") > 0) || attr("failed") !== 0 ||
          attr("notExecuted") !== 0 || attr("passed") <= 0)
        throw Error("Incomplete or failed TRX cases: " + file);
    }
    if (expected.disposable_stack) {
      for (const file of ["m17-browser-results.json","m17-cardinality-results.json"]) {
        const report = JSON.parse(fs.readFileSync(path.join(root,file),"utf8"));
        if (report.stats?.unexpected !== 0 || report.stats?.expected < 1 ||
            report.stats?.skipped !== 0)
          throw Error("Browser report contains missing or failing test cases: " + file);
      }
      const cardinality = JSON.parse(fs.readFileSync(
        path.join(root,"m17-cardinality-raw.json"),"utf8"));
      if (cardinality.requested_paths !== 10000 ||
          cardinality.all_requests_returned_404 !== true ||
          cardinality.exported_series_across_window < 1 ||
          cardinality.exported_series_across_window > 500 ||
          !Array.isArray(cardinality.memory_samples) ||
          cardinality.memory_samples.length < 2)
        throw Error("Cardinality or memory observations incomplete");
      if (expected.worker_fixture) {
        const worker = JSON.parse(fs.readFileSync(
          path.join(root,"m17-worker-results.json"),"utf8"));
        if (worker.result !== "PASS" || worker.source_sha !== manifest.source_sha ||
            !Array.isArray(worker.checks) || worker.checks.length < 4 ||
            worker.checks.some(check => check.status !== "PASS"))
          throw Error("Worker diagnostic fault campaign incomplete");
      }
    }
  }
  if (manifest.assertion_disposition !== "NOT_REVIEWED")
    throw Error("Unverified evidence may not claim reviewed assertions");
  return {source_sha: manifest.source_sha,files:actual.length,
    complete, missing_expected_artifacts: missing,
    gate_acceptance: "NOT_ACCEPTED", assertions_reviewed: 0};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const manifest = process.argv[2], sha = process.argv[3];
  if (!manifest || !sha || !/^[a-f0-9]{40}$/.test(sha))
    throw Error("Usage: node scripts/verify-g17-manifest.mjs <path> <40-character SHA>");
  console.log("Verified G17 evidence index:", verifyManifest(manifest, sha));
}
