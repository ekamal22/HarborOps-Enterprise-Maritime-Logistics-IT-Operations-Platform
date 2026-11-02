// G6 Q1 checkpoint for a retained historical PostgreSQL Docker volume.
// The source volume is only ever mounted read-only. A fingerprint of every file in it is taken before
// and after, and a disposable copy is restored, migrated to the current model and re-read.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const docker = (args, timeout = 240000) => execFileSync("docker", args, {
  encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout, maxBuffer: 64 * 1024 * 1024
}).trim();
const copyName = "ho-g6-snapshot-copy";
const containerName = "ho-g6-snapshot-pg";

function fingerprint(volume) {
  // Hash every file name and content in the volume; the volume is mounted read-only.
  return docker(["run", "--rm", "-v", volume + ":/src:ro", "alpine:3.20", "sh", "-ec",
    "cd /src && find . -type f | sort | xargs sha256sum | sha256sum | cut -d' ' -f1"]).toUpperCase();
}

export async function inspectVolume(volume) {
  const meta = JSON.parse(docker(["volume", "inspect", volume]))[0];
  const facts = docker(["run", "--rm", "-v", volume + ":/src:ro", "alpine:3.20", "sh", "-ec",
    "cat /src/PG_VERSION; du -sm /src | cut -f1; test -e /src/postmaster.pid && echo running || echo stopped"])
    .split(/\r?\n/);
  return { name: meta.Name, createdAt: meta.CreatedAt, labels: meta.Labels ?? {},
    postgresMajor: facts[0], sizeMegabytes: Number(facts[1]), clusterState: facts[2] };
}

export async function runVolumeSnapshot({ volume, database, cwd, root, report, save }) {
  assert.equal(docker(["ps", "-aq", "--filter", "name=^/" + containerName + "$"]), "",
    "Existing snapshot container cannot be reused");
  assert.equal(docker(["volume", "ls", "-q", "--filter", "name=^" + copyName + "$"]), "",
    "Existing snapshot copy volume cannot be reused");
  report.sourceVolume = await inspectVolume(volume);
  assert.equal(report.sourceVolume.clusterState, "stopped", "Source cluster must be cleanly stopped");
  report.sourceFingerprintBefore = fingerprint(volume);
  let ownedVolume = false, ownedContainer = false;
  try {
    docker(["volume", "create", "--label", "harborops.g6=disposable", copyName]);
    ownedVolume = true;
    docker(["run", "--rm", "-v", volume + ":/src:ro", "-v", copyName + ":/dst", "alpine:3.20",
      "sh", "-ec", "cp -a /src/. /dst/"]);
    docker(["run", "-d", "--name", containerName, "--label", "harborops.g6=disposable",
      "--ulimit", "core=0", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
      "-v", copyName + ":/var/lib/postgresql/data", "harborops-postgres:16.15-rootless"]);
    ownedContainer = true;
    let ready = false;
    for (let i = 0; i < 60 && !ready; i++) {
      try {
        ready = docker(["exec", containerName, "pg_isready", "-U", "harborops", "-d", database])
          .includes("accepting connections");
      } catch { /* still starting */ }
      if (!ready) await new Promise((resolve) => setTimeout(resolve, 500));
    }
    assert.ok(ready, "Disposable snapshot database did not start");
    const sql = (text) => docker(["exec", containerName, "psql", "-U", "harborops", "-d", database,
      "-Atc", text]);
    report.postgresVersion = sql("SHOW server_version");
    // Row counts and content hashes for every table that exists before the upgrade.
    const tablesQuery = "SELECT table_name FROM information_schema.tables " +
      "WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name<>'__EFMigrationsHistory' " +
      "ORDER BY 1";
    const snapshotTables = () => Object.fromEntries(sql(tablesQuery).split(/\r?\n/).filter(Boolean)
      .map((name) => [name, sql('SELECT count(*)||\':\'||coalesce(md5(string_agg(t::text,\'|\' ORDER BY t::text)),\'\') FROM "' +
        name + '" t')]));
    const migrations = () => sql('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1').split(/\r?\n/);
    const before = { migrations: migrations(), tables: snapshotTables() };
    docker(["run", "--rm", "--ulimit", "core=0", "--network", "container:" + containerName,
      "-v", cwd + ":/src", "-v", path.join(root, "cache") + ":/cache", "-w", "/src",
      "-e", "ConnectionStrings__HarborOps=Host=127.0.0.1;Port=5432;Database=" + database + ";Username=harborops",
      "-e", "NUGET_PACKAGES=/cache/nuget/packages", "-e", "DOTNET_CLI_HOME=/cache/dotnet-home",
      "-e", "HOME=/cache/dotnet-home", "-e", "TMPDIR=/cache/tmp", "-e", "DOTNET_NOLOGO=1",
      "mcr.microsoft.com/dotnet/sdk:10.0", "dotnet", "run", "--project", "src/HarborOps.Api/HarborOps.Api.csproj",
      "-c", "Release", "--", "--migrate"], 900000);
    const after = { migrations: migrations(), tables: snapshotTables() };
    for (const [name, value] of Object.entries(before.tables))
      assert.equal(after.tables[name], value, "Historical table changed during upgrade: " + name);
    assert.deepEqual(after.migrations.slice(0, before.migrations.length), before.migrations,
      "Historical migration history changed");
    assert.ok(after.migrations.includes("20261102080000_ShipmentAssignments"), "Snapshot was not migrated to M6");
    const exclusions = Number(sql("SELECT count(*) FROM pg_constraint WHERE conname IN " +
      "('ex_m6_container_overlap','ex_m6_voyage_overlap')"));
    assert.equal(exclusions, 2, "M6 exclusion constraints missing after upgrade");
    const newTables = Object.keys(after.tables).filter((name) => !(name in before.tables));
    assert.ok(newTables.includes("shipment_container_assignments") && newTables.includes("shipment_voyage_assignments"));
    report.evidence = { before, after: { migrations: after.migrations, preservedTables: Object.keys(before.tables).length },
      m6Exclusions: exclusions, addedTables: newTables, migration: "20261102080000_ShipmentAssignments" };
    report.outcome = "PASS";
    report.reason = "Read-only copy of a retained historical volume restored into an isolated disposable PostgreSQL, " +
      "migrated to M6 and every pre-existing table re-read unchanged";
    save();
  } catch (error) {
    report.outcome = "REVIEW REQUIRED";
    report.reason = "Historical volume restore/migrate failure: " +
      String(error instanceof Error ? error.message : error).slice(0, 400);
    save();
    throw error;
  } finally {
    if (ownedContainer) docker(["rm", "-f", containerName]);
    if (ownedVolume) docker(["volume", "rm", copyName]);
    assert.equal(docker(["ps", "-aq", "--filter", "name=^/" + containerName + "$"]), "");
    assert.equal(docker(["volume", "ls", "-q", "--filter", "name=^" + copyName + "$"]), "");
    report.sourceFingerprintAfter = fingerprint(volume);
    report.sourceUnmodified = report.sourceFingerprintBefore === report.sourceFingerprintAfter;
    save();
    assert.ok(report.sourceUnmodified, "The retained source volume changed");
  }
}
