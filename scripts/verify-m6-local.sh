#!/usr/bin/env bash
# Local, disposable M6/G6 verification. Never dispatch GitHub Actions.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be on E:" >&2; exit 2 ;;
esac
case "$root" in
  *[/\\]m6|*[/\\]m6[/\\]*) ;;
  *) echo "Use a dedicated E:-backed M6 verification root ending in m6" >&2; exit 2 ;;
esac
command -v cygpath >/dev/null || { echo "Windows Git Bash is required" >&2; exit 2; }
node scripts/check-m3-docker-storage.mjs
[[ -z "$(git status --porcelain)" ]] || { echo "Clean source worktree required" >&2; exit 2; }
sha="$(git rev-parse HEAD)"
export HARBOROPS_SOURCE_SHA="$sha"
# All controllable stores, including the PowerShell archive workspace, remain on E:.
for name in TEMP TMP TMPDIR NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH NUGET_PACKAGES DOTNET_CLI_HOME; do
  current="${!name:-}"
  if [[ -z "$current" ]]; then
    current="$root/cache/$name"
    export "$name=$current"
  fi
  mkdir -p "${!name}"
done
export HOME="$root/cache/home"
mkdir -p "$HOME"
export HARBOROPS_G6_COMPOSE_PROJECT="ho-verify-g6"
export HARBOROPS_DB_PASSWORD="$(printenv HARBOROPS_DB_PASSWORD || true)"
if [[ -z "$HARBOROPS_DB_PASSWORD" ]]; then export HARBOROPS_DB_PASSWORD="disposable-m3-check-only"; fi
mkdir -p "$root"
# Archive rather than reuse test results from an older source revision.
if [[ -d "$root/evidence" ]]; then
  mkdir -p "$root/archives"
  # Name the archive after the revision whose evidence it holds, not the revision about to run.
  previous="$(tr -d '\r\n' < "$root/evidence/g6-source-sha.txt" 2>/dev/null || true)"
  archive="$root/archives/g6-$(date -u +%Y%m%dT%H%M%SZ)-${previous:-unknown-revision}-$$"
  mv "$root/evidence" "$archive"
fi
evidence="$root/evidence"
mkdir -p "$evidence"
printf '%s\n' "$sha" > "$evidence/m6-source-sha.txt"
printf '%s\n' "$sha" > "$evidence/g6-source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g6-start-utc.txt"
: > "$evidence/g6-stages.tsv"
# Retain the complete original console stream in the E:-backed evidence directory.
exec 3>&1
exec > >(tee "$evidence/g6-console.log" >&3) 2>&1
g6_console_pid=$!
export HARBOROPS_G3_JSON_REPORT="$evidence/playwright-g6.json"
override="$root/compose.core0.yaml"
cat > "$override" <<'YAML'
services:
  postgres:
    ulimits:
      core: 0
  migrator:
    ulimits:
      core: 0
  api:
    ulimits:
      core: 0
  web:
    ulimits:
      core: 0
YAML
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="compose.yaml;$(cygpath -w "$override")"
# Do not take over an existing project, even if it shares the disposable name.
existing_containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-verify-g6)"
existing_volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-verify-g6)"
# Git Bash rewrites a leading slash inside an argument unless path conversion is disabled.
# Without this the name filters matched nothing and a disposable database was left behind.
existing_pg="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-')"
if [[ -n "$existing_containers" || -n "$existing_volumes" || -n "$existing_pg" ]]; then
  echo "Existing verification resources found. Refusing takeover." >&2
  exit 2
fi
owned=1
cleanup() {
  rc=$?
  trap - EXIT
  if [[ "$owned" == "1" ]]; then
    docker compose -p ho-verify-g6 down --volumes --remove-orphans \
      >> "$evidence/g6-cleanup.log" 2>&1 || true
    id="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-' \
      --filter label=harborops.g6=disposable)"
    if [[ -n "$id" ]]; then
      docker rm -f "$id" >> "$evidence/g6-cleanup.log" 2>&1 || true
    fi
  fi
  if [[ ! -s "$evidence/g6-end-utc.txt" ]]; then
    date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g6-end-utc.txt"
  fi
  if (( rc != 0 )); then
    printf 'G6 run failed, exit=%s\n' "$rc" > "$evidence/g6-aborted.txt"
  fi
  exit "$rc"
}
trap cleanup EXIT
run_stage() {
  local label="$1" rc=0
  shift
  if "$@" 2>&1 | tee "$evidence/g6-$label.log"; then
    rc=0
  else
    rc=$?
  fi
  printf '%s\t%s\t%s\n' "$label" "$sha" "$rc" >> "$evidence/g6-stages.tsv"
  return "$rc"
}
run_stage storage-before node scripts/record-g6-storage.mjs "$root" before
run_stage decision-unit node --test scripts/g4-decision-gate.test.mjs
run_stage schema-index node scripts/check-g4-index-contract.mjs
run_stage shipment-schema node scripts/check-m5-schema-contract.mjs
run_stage assignment-schema node scripts/check-g6-schema-contract.mjs
run_stage coverage-source node scripts/check-g6-source-coverage.mjs
run_stage preflight bash scripts/verify-m6-stages-local.sh preflight
run_stage postgres-image docker build --build-arg "SOURCE_REVISION=$sha" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
# Fail-fast order: cheap compile and unit-level gates, then the independent disposable-database
# suites (.NET, mutations, snapshot), and only then the long Compose browser and outage stages.
run_stage build bash scripts/verify-m6-stages-local.sh build
for stage in deps web docs; do
  run_stage "$stage" bash scripts/verify-m6-stages-local.sh "$stage"
done
# The .NET suites use their own disposable database container and do not need the compose stack.
run_stage dotnet bash scripts/verify-m6-stages-local.sh dotnet
run_stage mutation node scripts/run-g6-mutations.mjs "$root"
run_stage snapshot node scripts/check-g6-snapshot.mjs "$root"
for stage in stack accounts organizations session; do
  run_stage "$stage" bash scripts/verify-m6-stages-local.sh "$stage"
done
# The accepted local runner generated the synthetic password in an E: file.
export HARBOROPS_TEST_USER_PASSWORD
HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/test-user-password.txt")"
test -n "$HARBOROPS_TEST_USER_PASSWORD"
run_stage setup-fixtures bash scripts/verify-m6-stages-local.sh setup_fixtures
run_stage fixtures node scripts/prepare-m3-browser-fixtures.mjs
run_stage m4-fixtures node scripts/prepare-m4-browser-fixtures.mjs
run_stage m5-fixtures node scripts/prepare-m5-browser-fixtures.mjs
run_stage m6-fixtures node scripts/prepare-m6-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1
export HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1
export HARBOROPS_M6_FIXTURES=1
run_stage browsers bash scripts/run-m6-browsers-local.sh
# The reference and maritime outage checks end by downgrading the shared Manager account, so the
# shipment outage check, which needs that Manager, must run before them.
# Each check logs in with shared accounts. A fresh API process resets the in-memory login limiter, so a
# check is not rejected with 429 by the attempts of the check before it.
outage_check() {
  bash scripts/verify-m6-stages-local.sh restart_api && node "$1"
}
run_stage assignment-outage outage_check scripts/check-m6-assignment-outage.mjs
run_stage shipment-outage outage_check scripts/check-m6-shipment-outage.mjs
run_stage reference-outage outage_check scripts/check-m6-reference-outage.mjs
run_stage maritime-outage outage_check scripts/check-m6-maritime-outage.mjs
run_stage recovery bash -c 'bash scripts/verify-m6-stages-local.sh restart_api && bash scripts/verify-m6-stages-local.sh recovery'
run_stage scan bash scripts/verify-m6-stages-local.sh scan
teardown_g6() {
  # Preserve original service logs before disposing of G6-owned containers.
  docker compose -p ho-verify-g6 logs --no-color --timestamps > "$evidence/g6-docker-services.log" 2>&1 || true
  docker compose -p ho-verify-g6 down --volumes --remove-orphans || return 1
  local integration
  integration="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-' --filter label=harborops.g6=disposable)"
  if [[ -n "$integration" ]]; then docker rm -f "$integration" >/dev/null || return 1; fi
  local containers volumes database
  containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-verify-g6)"
  volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-verify-g6)"
  database="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-')"
  node -e '
    const fs=require("fs");
    const [dest,sha,c,v,d]=process.argv.slice(1);
    fs.writeFileSync(dest,JSON.stringify({sourceSha:sha,
      remainingContainers:c?c.split(/\r?\n/).length:0,
      remainingVolumes:v?v.split(/\r?\n/).length:0,
      remainingIntegrationContainers:d?d.split(/\r?\n/).length:0,
      checkedAtUtc:new Date().toISOString()},null,2));
  ' "$evidence/g6-teardown.json" "$sha" "$containers" "$volumes" "$database"
  [[ -z "$containers" && -z "$volumes" && -z "$database" ]] || return 1
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g6-end-utc.txt"
}
run_stage teardown teardown_g6
owned=0
run_stage storage-after node scripts/record-g6-storage.mjs "$root" after
evidence_status=0
run_stage evidence node scripts/assert-g6-evidence.mjs "$evidence" || evidence_status=$?
if (( evidence_status != 0 )); then
  printf 'G6 evidence gate incomplete (exit %s); inspect g6-evidence.log and scenario mappings.\n' \
    "$evidence_status" > "$evidence/g6-gate-incomplete.txt"
fi
# Teardown has completed and its zero-owned-resource check passed. Preserve the
# full evidence bundle even when the independent acceptance decision remains pending.
trap - EXIT
# Final packaging occurs after the evidence stage closes its log.
# Seal the live console file before computing evidence hashes.
exec 1>&3 2>&3
wait "$g6_console_pid"
mkdir -p "$root/archives"
node scripts/finalize-g6-evidence.mjs 2>&1 | tee "$root/archives/g6-finalize-console.log"
if (( evidence_status != 0 )); then
  echo "G6 raw evidence archived; evidence gate remains incomplete (exit $evidence_status)." >&2
  exit "$evidence_status"
fi
echo "G6 executable stages and evidence bundle finished. Acceptance requires independent review."
