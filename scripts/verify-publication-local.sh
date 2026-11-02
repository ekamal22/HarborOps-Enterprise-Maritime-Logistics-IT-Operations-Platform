#!/usr/bin/env bash
# Local, disposable publication-security regression run. Never dispatches GitHub Actions.
# Reuses the accepted M6 stage implementations, then adds the publication transport stages.
# Usage: bash scripts/verify-publication-local.sh   (from a clean committed worktree, E: environment sourced)
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be on E:" >&2; exit 2 ;;
esac
command -v cygpath >/dev/null || { echo "Windows Git Bash is required" >&2; exit 2; }
node scripts/check-m3-docker-storage.mjs
[[ -z "$(git status --porcelain)" ]] || { echo "Clean source worktree required" >&2; exit 2; }
sha="$(git rev-parse HEAD)"
export HARBOROPS_SOURCE_SHA="$sha"
export HOME="$root/cache/home"
mkdir -p "$HOME" "$root"
export HARBOROPS_DB_PASSWORD="${HARBOROPS_DB_PASSWORD:-disposable-publication-check-only}"
if [[ -d "$root/evidence" ]]; then
  mkdir -p "$root/archives"
  mv "$root/evidence" "$root/archives/publication-$(date -u +%Y%m%dT%H%M%SZ)-$$"
fi
evidence="$root/evidence"
mkdir -p "$evidence"
printf '%s\n' "$sha" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/start-utc.txt"
: > "$evidence/stages.tsv"
exec 3>&1
exec > >(tee "$evidence/console.log" >&3) 2>&1
export HARBOROPS_G3_JSON_REPORT="$evidence/playwright.json"
cat > "$root/compose.core0.yaml" <<'YAML'
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
export COMPOSE_FILE="compose.yaml;$(cygpath -w "$root/compose.core0.yaml")"
existing_containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-verify-g6)"
existing_volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-verify-g6)"
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
    docker compose -p ho-verify-g6 down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || true
    id="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-' --filter label=harborops.g6=disposable)"
    if [[ -n "$id" ]]; then docker rm -f "$id" >> "$evidence/cleanup.log" 2>&1 || true; fi
  fi
  [[ -s "$evidence/end-utc.txt" ]] || date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/end-utc.txt"
  if (( rc != 0 )); then printf 'Run failed, exit=%s\n' "$rc" > "$evidence/aborted.txt"; fi
  exit "$rc"
}
trap cleanup EXIT
failed=0
run_stage() {
  local label="$1" rc=0 began=$SECONDS
  shift
  if "$@" 2>&1 | tee "$evidence/$label.log"; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\n' "$label" "$sha" "$rc" "$((SECONDS - began))" >> "$evidence/stages.tsv"
  return "$rc"
}
# Independent stages keep running after a failure so one defect does not hide unrelated results.
soft_stage() { run_stage "$@" || failed=$((failed + 1)); }
stages=scripts/verify-m6-stages-local.sh
pub=scripts/verify-publication-stages-local.sh
soft_stage storage-before node scripts/record-g6-storage.mjs "$root" before
soft_stage publication-scripts bash $pub scripts
soft_stage postgres-image docker build --build-arg "SOURCE_REVISION=$sha" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
run_stage build bash $stages build
soft_stage deps bash $stages deps
soft_stage web bash $stages web
soft_stage docs bash $stages docs
soft_stage dotnet bash $stages dotnet
run_stage stack bash $stages stack
soft_stage accounts bash $stages accounts
soft_stage organizations bash $stages organizations
soft_stage session bash $stages session
soft_stage transport bash $pub transport
export HARBOROPS_TEST_USER_PASSWORD
HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/test-user-password.txt")"
test -n "$HARBOROPS_TEST_USER_PASSWORD"
run_stage setup-fixtures bash $stages setup_fixtures
run_stage fixtures node scripts/prepare-m3-browser-fixtures.mjs
run_stage m4-fixtures node scripts/prepare-m4-browser-fixtures.mjs
run_stage m5-fixtures node scripts/prepare-m5-browser-fixtures.mjs
run_stage m6-fixtures node scripts/prepare-m6-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1 HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
soft_stage browsers bash scripts/run-m6-browsers-local.sh
outage_check() { bash $stages restart_api && node "$1"; }
soft_stage assignment-outage outage_check scripts/check-m6-assignment-outage.mjs
soft_stage shipment-outage outage_check scripts/check-m6-shipment-outage.mjs
soft_stage reference-outage outage_check scripts/check-m6-reference-outage.mjs
soft_stage maritime-outage outage_check scripts/check-m6-maritime-outage.mjs
soft_stage recovery bash -c "bash $stages restart_api && bash $stages recovery"
soft_stage scan bash $stages scan
# Source-locked advisory and image review through the pinned scanner wrappers when present
# (see docs/PUBLICATION_REMEDIATION_STATUS.md); skipped, and reported as such, when they are not.
if [[ -f "$root/bin/trivy.cmd" ]]; then
  supply_chain() {
    PATH="$root/bin:$PATH" TRIVY_CACHE_DIR="$(cygpath -m "$root/trivy-cache")"       COMPOSE_PROJECT_NAME=ho-verify-g6 bash -c '
        dotnet restore HarborOps.sln >/dev/null &&
        python scripts/audit-publication-supply-chain.py "$1"' _ "$(cygpath -m "$evidence/supply-chain")"
  }
  soft_stage supply-chain supply_chain
else
  echo "supply-chain stage not executed: scanner wrappers missing" | tee "$evidence/supply-chain.log"
fi
teardown() {
  docker compose -p ho-verify-g6 logs --no-color --timestamps > "$evidence/docker-services.log" 2>&1 || true
  docker compose -p ho-verify-g6 down --volumes --remove-orphans || return 1
  local leftovers
  leftovers="$(docker ps -aq --filter label=com.docker.compose.project=ho-verify-g6)$(docker volume ls -q --filter label=com.docker.compose.project=ho-verify-g6)$(MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-')"
  [[ -z "$leftovers" ]] || { echo "Leftover disposable resources" >&2; return 1; }
}
run_stage teardown teardown
owned=0
soft_stage storage-after node scripts/record-g6-storage.mjs "$root" after
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/end-utc.txt"
echo "Stage results (label, sha, exit, seconds):"
cat "$evidence/stages.tsv"
trap - EXIT
if (( failed > 0 )); then echo "$failed stage(s) failed" >&2; exit 1; fi
echo "All publication regression stages passed."
