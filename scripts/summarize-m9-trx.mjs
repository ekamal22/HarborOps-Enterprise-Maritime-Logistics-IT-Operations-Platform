#!/usr/bin/env node
// Summarize TRX files and fail closed: a run that executed nothing, skipped tests, failed any test
// or has an unfinished result summary is never reported as a technical success.
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";

const dir = process.argv[2];
if (!dir) {
    console.error("usage: summarize-m9-trx.mjs <trx-directory>");
    process.exit(2);
}
const files = readdirSync(dir).filter(name => name.endsWith(".trx")).sort();
if (files.length === 0) {
    console.error("No TRX files found; nothing was executed.");
    process.exit(1);
}
let bad = 0;
const totals = { total: 0, executed: 0, passed: 0, failed: 0, notExecuted: 0 };
for (const file of files) {
    const text = readFileSync(path.join(dir, file), "utf8");
    const counters = /<Counters\b([^>]*)\/>/.exec(text);
    const outcome = /<ResultSummary\s+outcome="([^"]+)"/.exec(text)?.[1] ?? "missing";
    if (!counters) {
        console.error(`${file}: no Counters element`);
        bad++;
        continue;
    }
    const read = name => Number(new RegExp(`\\b${name}="(\\d+)"`).exec(counters[1])?.[1] ?? NaN);
    const row = { total: read("total"), executed: read("executed"), passed: read("passed"),
        failed: read("failed"), notExecuted: read("notExecuted") };
    for (const key of Object.keys(totals)) totals[key] += row[key];
    const ok = outcome === "Completed" && row.total > 0 && row.failed === 0 &&
        row.notExecuted === 0 && row.executed === row.total && row.passed === row.total;
    console.log(`${ok ? "OK  " : "BAD "} ${file} outcome=${outcome} total=${row.total} executed=${row.executed} ` +
        `passed=${row.passed} failed=${row.failed} notExecuted=${row.notExecuted}`);
    if (!ok) bad++;
}
console.log(`TOTAL files=${files.length} total=${totals.total} executed=${totals.executed} ` +
    `passed=${totals.passed} failed=${totals.failed} notExecuted=${totals.notExecuted}`);
process.exit(bad === 0 ? 0 : 1);
