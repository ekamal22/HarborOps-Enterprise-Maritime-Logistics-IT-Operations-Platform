import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { verifyPlaywrightCases, verifyTrxCases, G17_REQUIRED_METHODS } from "./g17-results-contract.mjs";

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
      typeof expected.require_full_g17 !== "boolean" ||
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
    const preflight = JSON.parse(fs.readFileSync(path.join(root,
      "g17-coverage-preflight.json"),"utf8"));
    if (preflight.passed !== true || preflight.strict !== true ||
        preflight.technically_authored !== 72 || preflight.assertions !== 75 ||
        preflight.scenarios !== 25 || preflight.errors?.length !== 0)
      throw Error("Incomplete or incorrectly reported 75-assertion S25 technical preflight");
    if (expected.require_full_g17 &&
        (expected.disposable_stack !== true || expected.worker_fixture !== true ||
         expected.fault_probe !== true || status.full_campaign_requested !== true))
      throw Error("Mandatory full G17 fault and worker scenarios were omitted");
    // Presence and a green wrapper exit are not enough: inspect raw test outcomes.
    const trxFiles = ["g17-unit.trx","g17-contract.trx",
      ...(expected.disposable_stack ? ["g17-integration.trx"] : [])];
    for (const file of trxFiles) {
      const xml = fs.readFileSync(path.join(root,file),"utf8");
      const methods = file === "g17-contract.trx" ? G17_REQUIRED_METHODS.contract :
        file === "g17-integration.trx" ? G17_REQUIRED_METHODS.integration : [];
      verifyTrxCases(xml,methods);
    }
    if (expected.disposable_stack) {
      const network = JSON.parse(fs.readFileSync(
        path.join(root,"m17-network-results.json"),"utf8"));
      if (network.result !== "PASS" || network.source_sha !== manifest.source_sha ||
          network.project !== expected.project ||
          network.observed?.web?.length !== 1 ||
          network.observed.web[0] !== "internal")
        throw Error("Missing positive Docker network-isolation evidence");
      for (const [file,mode] of [
        ["m17-browser-results.json","dashboard"],
        ["m17-cardinality-results.json","cardinality"]
      ]) {
        const report = JSON.parse(fs.readFileSync(path.join(root,file),"utf8"));
        verifyPlaywrightCases(report,mode);
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
      const restart = JSON.parse(fs.readFileSync(path.join(root,
        "m17-restart-results.json"),"utf8"));
      if (restart.source_sha !== manifest.source_sha ||
          restart.project !== expected.project || restart.result !== "PASS" ||
          restart.api_restored !== true || restart.checks?.length !== 4 ||
          restart.checks.some(step => step.status !== "PASS"))
        throw Error("API cancellation, shutdown, restart or durable invariant evidence is absent");
      const overhead = JSON.parse(fs.readFileSync(path.join(root,
        "m17-performance-raw.json"),"utf8"));
      if (overhead.source_sha !== manifest.source_sha ||
          overhead.project !== expected.project ||
          overhead.run_kind !== "THREE_MATCHED_PAIRS" ||
          overhead.result !== "PASS" ||
          overhead.rows?.length !== 6 || overhead.pairs?.length !== 3 ||
          overhead.system?.cpus < 1 ||
          !Number.isFinite(overhead.p95_increase_stddev_pct) ||
          !Number.isFinite(overhead.rss_increase_stddev_pct) ||
          overhead.pairs.some(p => p.pass !== true ||
            p.p95_increase_pct > 10 || p.memory_increase_pct > 15))
        throw Error("Three complete matched P09 overhead pairs are missing or above budget");
      if (expected.fault_probe) {
        const fault = JSON.parse(fs.readFileSync(path.join(root,
          "m17-collector-fault-results.json"),"utf8"));
        const requiredSteps = ["baseline-good","collector-scrape-reports-outage",
          "collector-fault-business-isolation","collector-recovery-automatic"];
        if (fault.source_sha !== manifest.source_sha ||
            fault.project !== expected.project || fault.result !== "PASS" ||
            fault.collector_restored !== true || fault.checks?.length !== 4 ||
            fault.checks.some((step,i) => step.name !== requiredSteps[i] ||
              step.result !== "PASS"))
          throw Error("Collector fault/recovery campaign has missing or invalid witnesses");
      }
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
  const inventoryBytes = fs.readFileSync(path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../docs/M17_G17_ASSERTION_INVENTORY_20261112.json"));
  const inventory = JSON.parse(inventoryBytes.toString("utf8"));
  const assertions = inventory.scenarios?.flatMap(s => s.assertions) ?? [];
  if (manifest.assertion_inventory_sha256 !== crypto.createHash("sha256")
      .update(inventoryBytes).digest("hex") ||
      inventory.scenarios?.length !== 25 || assertions.length !== 75 ||
      manifest.scenario_count !== 25 || manifest.assertion_count !== 75 ||
      manifest.reviewed_assertion_count !== 0 ||
      inventory.gate_acceptance !== "NOT_ACCEPTED" ||
      assertions.some(a => a.status !== "NOT_EXECUTED" || a.witness !== null))
    throw Error("Unreconciled or falsely reviewed G17 atomic assertion inventory");
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
