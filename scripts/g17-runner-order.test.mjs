import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const runnerText = fs.readFileSync(path.join(repo, "scripts", "run-g17-local.ps1"), "utf8");
const read = relative => fs.readFileSync(path.join(repo, relative), "utf8");

// Attempt k2 failed because the database fault stage ran a page journey that imports playwright while the runner
// installed tests/browser dependencies only in a later stage. Earlier local campaigns passed only because their
// worktree already held a node_modules directory from an unrelated session. This test pins the order.
const installPattern = /'--prefix',\s*'tests\/browser',\s*'ci'|Push-Location[^\n]*tests\/browser[\s\S]{0,200}?'npm'\s*@\('ci'/;

export function browserDependencyOrderProblems(runner, readScript) {
  const problems = [];
  const install = installPattern.exec(runner);
  const consumers = [...new Set([...runner.matchAll(/scripts\/check-g17-[A-Za-z0-9-]+\.mjs/g)].map(m => m[0]))]
    .filter(script => /tests\/browser\//.test(readScript(script)));
  if (!install) problems.push("no stage installs the tests/browser dependencies");
  for (const script of consumers) {
    const at = runner.indexOf(script);
    if (install && install.index > at)
      problems.push(`${script} runs before the tests/browser dependencies are installed`);
  }
  return {problems, consumers};
}

test("the runner installs the browser test dependencies before any stage that executes a browser journey", () => {
  const {problems, consumers} = browserDependencyOrderProblems(runnerText, read);
  assert.deepEqual(problems, []);
  assert.ok(consumers.includes("scripts/check-g17-database-fault.mjs"),
    "the control must actually find the stage that drives the page-fault journey");
});

test("the install is a named, required stage and the Playwright stages no longer depend on a later duplicate", () => {
  assert.match(runnerText, /-Stage 'browser-test-dependencies'/);
  assert.equal([...runnerText.matchAll(/'tests\/browser',\s*'ci'/g)].length, 1);
  assert.equal([...runnerText.matchAll(/Invoke-Required 'npm' @\('ci'/g)].length, 0);
});

test("negative control: the order that failed attempt k2 is reported", () => {
  const withoutEarlyInstall = runnerText.replace(/\s*Invoke-Required 'npm' @\('--prefix', 'tests\/browser', 'ci'[^\n]*\n/, "\n");
  assert.notEqual(withoutEarlyInstall, runnerText, "the control must really remove the early install");
  const lateInstall = withoutEarlyInstall.replace(
    "Invoke-Required 'npx' @('--no-install', 'playwright', 'test',",
    "Invoke-Required 'npm' @('ci', '--no-audit', '--no-fund')\n            Invoke-Required 'npx' @('--no-install', 'playwright', 'test',");
  assert.notEqual(lateInstall, withoutEarlyInstall);
  const result = browserDependencyOrderProblems(lateInstall, read);
  assert.deepEqual(result.problems, ["scripts/check-g17-database-fault.mjs runs before the tests/browser dependencies are installed"]);
});

test("negative control: a runner with no browser dependency install at all is reported", () => {
  const none = runnerText.replace(/\s*Invoke-Required 'npm' @\('--prefix', 'tests\/browser', 'ci'[^\n]*\n/, "\n");
  assert.notEqual(none, runnerText);
  const {problems} = browserDependencyOrderProblems(none, read);
  assert.ok(problems.includes("no stage installs the tests/browser dependencies"));
});

test("negative control: a stage that does not use the browser is not forced to wait for the install", () => {
  const harmless = "Invoke-Required 'node' @('scripts/check-g17-network.mjs')\n";
  assert.deepEqual(browserDependencyOrderProblems(
    "Invoke-Required 'npm' @('--prefix', 'tests/browser', 'ci')\n" + harmless, () => "no browser use").consumers, []);
});
