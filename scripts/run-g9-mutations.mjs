import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { mutations } from "./apply-g9-mutation.mjs";

// G9-S23: clean control plus twelve targeted, non-equivalent mutants. Each mutant is applied to an
// isolated export of the exact source commit, built, and killed by its intended negative tests.
const root = process.argv[2], sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "E: verification root required");
assert.match(sha || "", /^[0-9a-f]{40}$/);
const evidence = path.join(root, "evidence", "mutations");
fs.mkdirSync(evidence, { recursive: true });
const cache = path.join(root, "cache");
fs.mkdirSync(cache, { recursive: true });
const sourceTar = path.join(cache, "g9-mutation-" + sha.slice(0, 12) + ".tar");
const exported = path.join(cache, "g9-mutation-export-" + sha.slice(0, 12));
assert.ok(!fs.existsSync(exported), "Mutation export already exists; use a new isolated verification root");
const run = (cmd, args, options = {}) => {
    const result = spawnSync(cmd, args, { encoding: "utf8", timeout: 300000, maxBuffer: 20 * 1024 * 1024, ...options });
    if (result.error) throw result.error;
    return result;
};
const expectOk = (cmd, args) => {
    const result = run(cmd, args);
    assert.equal(result.status, 0, cmd + " failed: " + (result.stderr || result.stdout || "").slice(-600));
    return result.stdout || "";
};
expectOk("git", ["archive", "--format=tar", "--output", sourceTar, sha]);

const T = {
    lease: "HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered",
    race: "DistinctConcurrentDbContextsCannotShareOneLease",
    process: "TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt",
    lock: "IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock",
    effect: "ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect",
    refs: "ReferenceResolutionIsTenantTimeSafeAndNeverGuessed",
    sqlForge: "DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence",
    sqlSkip: "InvalidDirectSqlCannotSkipStateOrRewriteEvidence",
    quota: "LeasedAndReconciliationStatesStillConsumeAdmissionQuota",
    ceiling: "UnresolvedCeilingCountsLeasedRetryAndReconciliationAtTheExactLimit",
    redrive: "UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager",
    retry: "TransientFailuresFollowBoundedScheduleAndStopAtFiveAttempts",
    commit: "AmbiguousFinalizeCommitIsReconstructedFromDurableState"
};
const every = Object.values(T);
// [variant, family number and name, tests that must fail, tests to execute]
const variants = [
    ["control", "clean unmodified control", [], every],
    ["effect-unique", "1 effect uniqueness removed", [T.sqlForge], [T.sqlForge]],
    ["lease-token", "2 token predicate removed", [T.lease, T.race], [T.lease, T.race]],
    ["lease-epoch", "3 epoch predicate removed", [T.lease], [T.lease]],
    ["lease-expiry", "4 lease expiry ignored", [T.lease], [T.lease]],
    ["claim-lock", "5 two-worker claim protection removed", [T.process, T.lock], [T.process, T.lock]],
    ["capacity-lock", "6 organization capacity lock removed", [T.lock], [T.lock]],
    ["unresolved-count", "7 unprocessed state omitted from count", [T.quota, T.ceiling], [T.quota, T.ceiling]],
    ["retry-budget", "8 retry budget bypassed", [T.retry], [T.retry]],
    ["redrive-auth", "9 unauthorized redrive allowed", [T.redrive], [T.redrive]],
    ["tenant-filter", "10 tenant resolver filter removed", [T.refs], [T.refs]],
    ["transition-guard", "11 work transition SQL guard disabled", [T.sqlForge, T.sqlSkip], [T.sqlForge, T.sqlSkip]],
    ["commit-ack", "12 post-commit effect acknowledgment weakened", [T.effect, T.commit], [T.effect, T.commit]]
];
for (const [variant] of variants.slice(1)) assert.ok(mutations[variant], "Missing mutation definition: " + variant);
// Development rehearsal only: a reduced subset is never full-gate evidence.
const subset = process.env.HARBOROPS_G9_MUTATION_SUBSET?.split(",");
assert.ok(!subset || process.env.HARBOROPS_G9_MUTATION_SUBSET_DEV === "1", "Subset runs are development only");

const summary = { sourceSha: sha, checkedAtUtc: new Date().toISOString(), control: null, mutants: [] };
const report = path.join(root, "evidence", "g9-mutations.json");
const save = () => fs.writeFileSync(report, JSON.stringify(summary, null, 2));
const name = "ho-g9-mutant-pg";
assert.equal(expectOk("docker", ["ps", "-aq", "--filter", "name=^/" + name + "$"]).trim(), "",
    "Existing mutant database container found");
try {
    for (const [variant, family, mustFail, execute] of variants.filter(v => !subset || subset.includes(v[0]))) {
        const work = path.join(exported, variant);
        fs.mkdirSync(work, { recursive: true });
        // GNU tar reads "E:\..." as host:path, so extract with forward-slash paths relative to the cache.
        const relativeTar = path.relative(cache, sourceTar).split(path.sep).join("/");
        const relativeWork = path.relative(cache, work).split(path.sep).join("/");
        const extract = run("tar", ["-xf", relativeTar, "-C", relativeWork], { cwd: cache });
        assert.equal(extract.status, 0, "tar failed: " + (extract.stderr || extract.stdout || "").slice(-600));
        if (variant !== "control") expectOk("node", ["scripts/apply-g9-mutation.mjs", variant, work]);
        const results = path.join(evidence, variant);
        fs.mkdirSync(results, { recursive: true });
        let owned = false;
        try {
            expectOk("docker", ["run", "-d", "--name", name, "--label", "harborops.g9=disposable",
                "--ulimit", "core=0", "--tmpfs", "/var/lib/postgresql/data:uid=70,gid=70,mode=0700",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                "-e", "POSTGRES_DB=harborops_integration", "-e", "POSTGRES_USER=harborops",
                "-e", "POSTGRES_HOST_AUTH_METHOD=trust", "harborops-postgres:16.15-rootless"]);
            owned = true;
            let ready = false;
            for (let k = 0; k < 60; k++) {
                const probe = run("docker", ["exec", name, "pg_isready", "-U", "harborops", "-d", "harborops_integration"]);
                if (probe.status === 0) { ready = true; break; }
                await new Promise(resolve => setTimeout(resolve, 500));
            }
            assert.ok(ready, "Mutation database not ready");
            const filter = execute.map(x => "FullyQualifiedName~" + x).join("|");
            const shell = "set -e; dotnet restore HarborOps.sln -v q; " +
                "dotnet build HarborOps.sln -c Release --no-restore -v q -warnaserror; " +
                "dotnet build src/HarborOps.Worker/HarborOps.Worker.csproj -c Release --no-restore -v q -warnaserror; " +
                "dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj " +
                "--no-build -c Release --filter '" + filter + "' " +
                "--logger 'trx;LogFileName=results.trx' --results-directory /cache/evidence/mutations/" + variant;
            const command = ["run", "--rm", "--ulimit", "core=0", "--network", "container:" + name,
                "-v", work + ":/src", "-v", root + ":/cache", "-w", "/src",
                "-e", "HARBOROPS_TEST_DB=Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops",
                "-e", "NUGET_PACKAGES=/cache/cache/nuget", "-e", "DOTNET_CLI_HOME=/cache/cache/dotnet",
                "-e", "HOME=/cache/cache/dotnet", "-e", "DOTNET_NOLOGO=1", "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                "-e", "TMPDIR=/cache/cache/tmp",
                "mcr.microsoft.com/dotnet/sdk:10.0", "bash", "-lc", shell];
            const executed = run("docker", command, { timeout: 1500000 });
            fs.writeFileSync(path.join(results, "console.log"), (executed.stdout || "") + "\n" + (executed.stderr || ""));
            const trx = path.join(results, "results.trx");
            // A mutant that does not compile is not a valid kill.
            assert.ok(fs.existsSync(trx), "Test results missing, mutant did not build or run: " + variant);
            const xml = fs.readFileSync(trx, "utf8");
            const rows = [...xml.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)].map(m => ({
                name: /testName="([^"]+)"/.exec(m[1])?.[1], outcome: /outcome="([^"]+)"/.exec(m[1])?.[1]
            }));
            assert.ok(rows.length > 0, "Mutation did not execute tests: " + variant);
            for (const required of execute)
                assert.ok(rows.some(x => (x.name || "").includes(required)), "Required test not discovered: " + required);
            if (variant === "control") {
                assert.equal(executed.status, 0, "Unmodified source control must pass");
                assert.ok(rows.every(x => x.outcome === "Passed"), "Unmodified control has failed or skipped tests");
                summary.control = { status: "PASS", passed: rows.length, testNames: rows.map(x => x.name) };
            } else {
                assert.notEqual(executed.status, 0, "Mutation survived: " + variant);
                const failed = rows.filter(x => x.outcome === "Failed");
                const killed = mustFail.filter(test => failed.some(x => (x.name || "").includes(test)));
                assert.ok(killed.length > 0, "Mutation did not fail an intended regression: " + variant);
                const messages = {};
                for (const block of xml.matchAll(/<UnitTestResult\b([^>]*?)>([\s\S]*?)<\/UnitTestResult>/g)) {
                    const testName = /testName="([^"]+)"/.exec(block[1])?.[1];
                    if (!testName || !/outcome="Failed"/.test(block[1])) continue;
                    const text = /<Message>([\s\S]*?)<\/Message>/.exec(block[2])?.[1] || "";
                    messages[testName] = text.replace(/&#xD;|&#xA;|\r|\n/g, " ").slice(0, 600);
                }
                summary.mutants.push({ variant, family, status: "KILLED", intendedTests: mustFail, killedBy: killed,
                    failedTests: failed.map(x => x.name), failureMessages: messages, total: rows.length });
            }
            save();
            console.log((variant === "control" ? "control PASS" : variant + " KILLED") + " (" + family + ")");
        } finally {
            if (owned) expectOk("docker", ["rm", "-f", name]);
        }
    }
    if (!subset) assert.equal(summary.mutants.length, 12, "All twelve mutation families are required");
    save();
    console.log("G9 controlled mutation proofs: PASS");
} finally {
    // Only the source export created under this verification root is disposable.
    assert.ok(exported.startsWith(cache + path.sep));
    if (fs.existsSync(exported)) fs.rmSync(exported, { recursive: true, force: true });
    if (fs.existsSync(sourceTar)) fs.unlinkSync(sourceTar);
}
