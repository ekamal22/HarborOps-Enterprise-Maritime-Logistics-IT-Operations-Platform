#!/usr/bin/env node
// Reconciles the Playwright JSON reports of the G9 browser supplement. Fail closed: a missing report,
// any failed, skipped, interrupted or flaky test, or zero executed tests is never a pass.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const evidence = process.argv[2];
const specsDir = process.argv[3];
assert.ok(evidence && specsDir, "usage: summarize-g9-browsers.mjs <evidence-dir> <specs-dir>");
const dir = path.join(evidence, "playwright");
// Optional filters mirror the runner's, for a documented partial rerun. The default is every spec in every engine.
const allSpecs = fs.readdirSync(specsDir).filter(x => x.endsWith(".spec.mjs")).map(x => x.replace(".spec.mjs", "")).sort();
const specs = process.env.HARBOROPS_BROWSER_SPECS ? process.env.HARBOROPS_BROWSER_SPECS.split(/\s+/).filter(Boolean).sort() : allSpecs;
for (const name of specs) assert.ok(allSpecs.includes(name), "Unknown spec in filter: " + name);
const engines = process.env.HARBOROPS_BROWSER_ENGINES ? process.env.HARBOROPS_BROWSER_ENGINES.split(/\s+/).filter(Boolean) : ["chromium", "firefox", "webkit"];
const rows = [];
const tests = [];
let problems = 0;
for (const engine of engines) {
    for (const spec of specs) {
        const file = path.join(dir, `${engine}-${spec}.json`);
        if (!fs.existsSync(file)) {
            rows.push({ engine, spec, status: "MISSING" });
            problems++;
            continue;
        }
        const report = JSON.parse(fs.readFileSync(file, "utf8"));
        const stats = report.stats ?? {};
        const collect = suite => {
            for (const child of suite.suites ?? []) collect({ ...child, file: suite.file ?? child.file });
            for (const item of suite.specs ?? []) for (const test of item.tests ?? []) {
                const results = test.results ?? [];
                tests.push({ engine, spec, title: item.title, status: test.status,
                    durationMs: results.reduce((sum, r) => sum + (r.duration ?? 0), 0) });
            }
        };
        for (const suite of report.suites ?? []) collect(suite);
        const expected = stats.expected ?? 0, unexpected = stats.unexpected ?? 0,
            skipped = stats.skipped ?? 0, flaky = stats.flaky ?? 0;
        const ok = expected > 0 && unexpected === 0 && skipped === 0 && flaky === 0 && (report.errors ?? []).length === 0;
        rows.push({ engine, spec, status: ok ? "PASS" : "FAIL", expected, unexpected, skipped, flaky,
            durationMs: Math.round(stats.duration ?? 0) });
        if (!ok) problems++;
    }
}
const total = rows.reduce((s, r) => s + (r.expected ?? 0), 0);
const result = {
    status: problems === 0 ? "PASS" : "FAILED",
    engines, specs, journeyRuns: rows.length, testsExecuted: total,
    passed: rows.filter(r => r.status === "PASS").length, problems, rows, tests
};
fs.writeFileSync(path.join(evidence, "g9-browser-summary.json"), JSON.stringify(result, null, 2));
for (const engine of engines) {
    const mine = rows.filter(r => r.engine === engine);
    console.log(`${engine}: ${mine.filter(r => r.status === "PASS").length}/${mine.length} specs pass, ` +
        `${mine.reduce((s, r) => s + (r.expected ?? 0), 0)} tests`);
}
for (const row of rows.filter(r => r.status !== "PASS"))
    console.log(`PROBLEM ${row.engine}/${row.spec}: ${row.status} unexpected=${row.unexpected ?? "-"} skipped=${row.skipped ?? "-"} flaky=${row.flaky ?? "-"}`);
console.log(`Browser supplement: ${result.status}, ${total} test executions across ${rows.length} spec runs`);
process.exit(problems === 0 ? 0 : 1);
