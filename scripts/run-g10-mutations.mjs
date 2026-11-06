import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const root = process.argv[2], sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "Fresh E: root required");
assert.match(sha || "", /^[0-9a-f]{40}$/);
const workspace = path.join(root, "cache", "g10-mutations");
const evidence = path.join(root, "evidence", "mutations");
assert.ok(!fs.existsSync(workspace), "Mutation workspace must be fresh");
fs.mkdirSync(workspace, { recursive: true });
fs.mkdirSync(evidence, { recursive: true });

function run(cmd, args, cwd = process.cwd(), timeout = 240000) {
    const result = spawnSync(cmd, args, {
        cwd, encoding: "utf8", timeout, maxBuffer: 16 * 1024 * 1024,
        env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: "1" }
    });
    if (result.error) throw result.error;
    return result;
}
const head = run("git", ["rev-parse", "HEAD"]);
assert.equal(head.status, 0);
assert.equal(head.stdout.trim(), sha, "Source revision changed");
const archive = path.join(workspace, "control.tar");
assert.equal(run("git", ["archive", "--format=tar", "--output", archive, sha]).status, 0);

const file = "src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs";
const tests = "tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj";
const families = [
    {
        name: "superseded-accepted",
        from: 'if (e.Superseded || e.EventCode is not ("GTIN" or "GTOT")) return;',
        to: 'if (e.EventCode is not ("GTIN" or "GTOT")) return;'
    },
    {
        name: "mixed-aggregated-as-in",
        from: 'else result = "Mixed";',
        to: 'else result = "EquipmentIn";'
    },
    {
        name: "same-container-conflict-ignored",
        from: 'else if (active.Any(x => x.ObservedState == "Conflict")) result = "Conflict";',
        to: 'else if (false && active.Any(x => x.ObservedState == "Conflict")) result = "Conflict";'
    }
];
const register = [];
for (const family of families) {
    const dir = path.join(workspace, family.name);
    fs.mkdirSync(dir);
    const extraction = run("tar", ["-xf", archive, "-C", dir], process.cwd());
    assert.equal(extraction.status, 0, "Archive extraction failed");
    const target = path.join(dir, file);
    const current = fs.readFileSync(target, "utf8");
    assert.ok(current.includes(family.from), "Mutation sentinel missing: " + family.name);
    assert.equal(current.split(family.from).length, 2, "Mutation must replace one occurrence");
    fs.writeFileSync(target, current.replace(family.from, family.to), "utf8");
    const execution = run("dotnet", [
        "test", tests, "-c", "Release",
        "--filter", "FullyQualifiedName~TrackingTimelinePolicyTests",
        "--nologo"
    ], dir);
    const output = (execution.stdout || "") + "\n" + (execution.stderr || "");
    fs.writeFileSync(path.join(evidence, family.name + ".log"), output);
    const testFailure = execution.status !== 0 &&
        /Failed:|Failed\s+HarborOps|\[FAIL\]|\[xUnit.net\].*FAIL/i.test(output) &&
        !/error CS\d+|NU1301|NU1900|Unable to load the service index/i.test(output);
    register.push({ family: family.name, exitCode: execution.status,
        result: testFailure ? "KILLED_BY_TEST" : "INVALID_OR_SURVIVED" });
}
fs.writeFileSync(path.join(evidence, "results.json"),
    JSON.stringify({ sourceSha: sha, families: register }, null, 2) + "\n");
assert.ok(register.every(item => item.result === "KILLED_BY_TEST"),
    "All G10 mutation families must be killed by test assertions");
