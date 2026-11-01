#!/usr/bin/env bash
# Local, disposable M5/G5 verification. Never dispatch GitHub Actions.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be on E:" >&2; exit 2 ;;
esac
case "$root" in
  *[/\\]m5|*[/\\]m5[/\\]*) ;;
  *) echo "Use a dedicated E:-backed M5 verification root ending in m5" >&2; exit 2 ;;
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
export HARBOROPS_G5_COMPOSE_PROJECT="ho-g5-verify"
export HARBOROPS_DB_PASSWORD="$(printenv HARBOROPS_DB_PASSWORD || true)"
if [[ -z "$HARBOROPS_DB_PASSWORD" ]]; then export HARBOROPS_DB_PASSWORD="disposable-m3-check-only"; fi
mkdir -p "$root"
# Archive rather than reuse test results from an older source revision.
if [[ -d "$root/evidence" ]]; then
  mkdir -p "$root/archives"
  # Name the archive after the revision whose evidence it holds, not the revision about to run.
  previous="$(tr -d '\r\n' < "$root/evidence/g5-source-sha.txt" 2>/dev/null || true)"
  archive="$root/archives/g5-$(date -u +%Y%m%dT%H%M%SZ)-${previous:-unknown-revision}-$$"
  mv "$root/evidence" "$archive"
fi
evidence="$root/evidence"
mkdir -p "$evidence"
printf '%s\n' "$sha" > "$evidence/m5-source-sha.txt"
printf '%s\n' "$sha" > "$evidence/g5-source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g5-start-utc.txt"
: > "$evidence/g5-stages.tsv"
export HARBOROPS_G3_JSON_REPORT="$evidence/playwright-g5.json"
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
existing_containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-g5-verify)"
existing_volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-g5-verify)"
# Git Bash rewrites a leading slash inside an argument unless path conversion is disabled.
# Without this the name filters matched nothing and a disposable database was left behind.
existing_pg="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g5-pg$')"
if [[ -n "$existing_containers" || -n "$existing_volumes" || -n "$existing_pg" ]]; then
  echo "Existing verification resources found. Refusing takeover." >&2
  exit 2
fi
owned=1
cleanup() {
  rc=$?
  trap - EXIT
  if [[ "$owned" == "1" ]]; then
    docker compose -p ho-g5-verify down --volumes --remove-orphans \
      >> "$evidence/g5-cleanup.log" 2>&1 || true
    id="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g5-pg$' \
      --filter label=harborops.g5=disposable)"
    if [[ -n "$id" ]]; then
      docker rm -f "$id" >> "$evidence/g5-cleanup.log" 2>&1 || true
    fi
  fi
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g5-end-utc.txt"
  if (( rc != 0 )); then
    printf 'G5 run failed, exit=%s\n' "$rc" > "$evidence/g5-aborted.txt"
  fi
  exit "$rc"
}
trap cleanup EXIT
run_stage() {
  local label="$1" rc=0
  shift
  if "$@" 2>&1 | tee "$evidence/g5-$label.log"; then
    rc=0
  else
    rc=$?
  fi
  printf '%s\t%s\t%s\n' "$label" "$sha" "$rc" >> "$evidence/g5-stages.tsv"
  return "$rc"
}
run_stage storage-before node scripts/record-g5-storage.mjs "$root" before
run_stage decision-unit node --test scripts/g4-decision-gate.test.mjs
run_stage schema-index node scripts/check-g4-index-contract.mjs
run_stage shipment-schema node scripts/check-m5-schema-contract.mjs
run_stage preflight bash scripts/verify-m5-stages-local.sh preflight
run_stage postgres-image docker build --build-arg "SOURCE_REVISION=$sha" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
for stage in dotnet deps web docs stack accounts organizations session; do
  run_stage "$stage" bash scripts/verify-m5-stages-local.sh "$stage"
done
# The accepted local runner generated the synthetic password in an E: file.
export HARBOROPS_TEST_USER_PASSWORD
HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/test-user-password.txt")"
test -n "$HARBOROPS_TEST_USER_PASSWORD"
setup_m3() {
  for entry in \
    "m3-admin-a@example.test|M3 Synthetic North Administrator" \
    "m3-admin-b@example.test|M3 Synthetic East Administrator" \
    "m3-manager@example.test|M3 Synthetic Manager" \
    "m3-analyst@example.test|M3 Synthetic Analyst" \
    "m4-analyst-east@example.test|M4 Synthetic East Analyst" \
    "m4-support@example.test|M4 Synthetic Support" \
    "m4-auditor@example.test|M4 Synthetic Auditor"; do
    email="$(printf '%s' "$entry" | cut -d'|' -f1)"
    name="$(printf '%s' "$entry" | cut -d'|' -f2)"
    printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | \
      docker compose -p ho-g5-verify run --rm -T \
        -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api \
        --account-cli provision "$email" "$name" || return 1
  done
  docker compose -p ho-g5-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-north "M3 North Terminal" m3-admin-a@example.test || return 1
  docker compose -p ho-g5-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-east "M3 East Terminal" m3-admin-b@example.test || return 1
  docker compose -p ho-g5-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-pagination "M3 Pagination Fixtures" m3-admin-a@example.test || return 1
  for engine in chromium firefox webkit; do
    docker compose -p ho-g5-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
      --organization-cli bootstrap "m3-revocation-$engine" "M3 Revocation $engine" \
      m3-admin-a@example.test || return 1
  done
}
run_stage setup-fixtures setup_m3
run_stage fixtures node scripts/prepare-m3-browser-fixtures.mjs
run_stage m4-fixtures node scripts/prepare-m4-browser-fixtures.mjs
run_stage m5-fixtures node scripts/prepare-m5-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1
export HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1
run_stage browsers bash scripts/run-m5-browsers-local.sh
run_stage reference-outage node scripts/check-m5-reference-outage.mjs
run_stage maritime-outage node scripts/check-m5-maritime-outage.mjs
run_stage shipment-outage node scripts/check-m5-shipment-outage.mjs
run_stage recovery bash scripts/verify-m5-stages-local.sh recovery
run_stage scan bash scripts/verify-m5-stages-local.sh scan
teardown_g5() {
  # Preserve original service logs before disposing of G5-owned containers.
  docker compose -p ho-g5-verify logs --no-color --timestamps > "$evidence/g5-docker-services.log" 2>&1 || true
  docker compose -p ho-g5-verify down --volumes --remove-orphans || return 1
  local integration
  integration="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g5-pg$' --filter label=harborops.g5=disposable)"
  if [[ -n "$integration" ]]; then docker rm -f "$integration" >/dev/null || return 1; fi
  local containers volumes database
  containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-g5-verify)"
  volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-g5-verify)"
  database="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g5-pg$')"
  node -e '
    const fs=require("fs");
    const [dest,sha,c,v,d]=process.argv.slice(1);
    fs.writeFileSync(dest,JSON.stringify({sourceSha:sha,
      remainingContainers:c?c.split(/\r?\n/).length:0,
      remainingVolumes:v?v.split(/\r?\n/).length:0,
      remainingIntegrationContainers:d?d.split(/\r?\n/).length:0,
      checkedAtUtc:new Date().toISOString()},null,2));
  ' "$evidence/g5-teardown.json" "$sha" "$containers" "$volumes" "$database"
  [[ -z "$containers" && -z "$volumes" && -z "$database" ]]
}
run_stage teardown teardown_g5
owned=0
run_stage storage-after node scripts/record-g5-storage.mjs "$root" after
run_stage evidence node scripts/assert-g5-evidence.mjs "$evidence"
# Final packaging occurs after the evidence stage closes its log.
node scripts/finalize-g5-evidence.mjs
echo "G5 executable stages and evidence bundle finished. Acceptance requires independent review."
