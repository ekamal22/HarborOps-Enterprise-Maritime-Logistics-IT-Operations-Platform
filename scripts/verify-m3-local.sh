#!/usr/bin/env bash
# Local, disposable M3/G3 verification. Never dispatch GitHub Actions.
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
export HARBOROPS_DB_PASSWORD="$(printenv HARBOROPS_DB_PASSWORD || true)"
if [[ -z "$HARBOROPS_DB_PASSWORD" ]]; then export HARBOROPS_DB_PASSWORD="disposable-m3-check-only"; fi
mkdir -p "$root"
# Archive rather than reuse test results from an older source revision.
if [[ -d "$root/evidence" ]]; then
  mkdir -p "$root/archives"
  archive="$root/archives/g3-$(date -u +%Y%m%dT%H%M%SZ)-$sha-$$"
  mv "$root/evidence" "$archive"
fi
evidence="$root/evidence"
mkdir -p "$evidence"
printf '%s\n' "$sha" > "$evidence/m3-source-sha.txt"
printf '%s\n' "$sha" > "$evidence/g3-source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g3-start-utc.txt"
: > "$evidence/g3-stages.tsv"
export HARBOROPS_G3_JSON_REPORT="$evidence/playwright-g3.json"
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
existing_containers="$(docker ps -aq --filter label=com.docker.compose.project=ho-verify)"
existing_volumes="$(docker volume ls -q --filter label=com.docker.compose.project=ho-verify)"
existing_pg="$(docker ps -aq --filter name='^/ho-g2-pg$')"
if [[ -n "$existing_containers" || -n "$existing_volumes" || -n "$existing_pg" ]]; then
  echo "Existing verification resources found. Refusing takeover." >&2
  exit 2
fi
owned=1
cleanup() {
  rc=$?
  trap - EXIT
  if [[ "$owned" == "1" ]]; then
    docker compose -p ho-verify down --volumes --remove-orphans \
      >> "$evidence/g3-cleanup.log" 2>&1 || true
    id="$(docker ps -aq --filter name='^/ho-g2-pg$' \
      --filter label=harborops.g2=disposable)"
    if [[ -n "$id" ]]; then
      docker rm -f "$id" >> "$evidence/g3-cleanup.log" 2>&1 || true
    fi
  fi
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/g3-end-utc.txt"
  if (( rc != 0 )); then
    printf 'G3 run failed, exit=%s\n' "$rc" > "$evidence/g3-aborted.txt"
  fi
  exit "$rc"
}
trap cleanup EXIT
run_stage() {
  local label="$1" rc=0
  shift
  if "$@" 2>&1 | tee "$evidence/g3-$label.log"; then
    rc=0
  else
    rc=$?
  fi
  printf '%s\t%s\t%s\n' "$label" "$sha" "$rc" >> "$evidence/g3-stages.tsv"
  return "$rc"
}
run_stage preflight bash scripts/verify-m2-local.sh preflight
run_stage postgres-image docker build --build-arg "SOURCE_REVISION=$sha" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
for stage in dotnet deps web docs stack accounts organizations session; do
  run_stage "$stage" bash scripts/verify-m2-local.sh "$stage"
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
    "m3-analyst@example.test|M3 Synthetic Analyst"; do
    email="$(printf '%s' "$entry" | cut -d'|' -f1)"
    name="$(printf '%s' "$entry" | cut -d'|' -f2)"
    printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | \
      docker compose -p ho-verify run --rm -T \
        -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api \
        --account-cli provision "$email" "$name" || return 1
  done
  docker compose -p ho-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-north "M3 North Terminal" m3-admin-a@example.test || return 1
  docker compose -p ho-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-east "M3 East Terminal" m3-admin-b@example.test || return 1
  docker compose -p ho-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap m3-pagination "M3 Pagination Fixtures" m3-admin-a@example.test || return 1
}
run_stage setup-fixtures setup_m3
run_stage fixtures node scripts/prepare-m3-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1
run_stage browsers bash scripts/verify-m2-local.sh browsers
run_stage reference-outage node scripts/check-m3-reference-outage.mjs
run_stage recovery bash scripts/verify-m2-local.sh recovery
run_stage scan bash scripts/verify-m2-local.sh scan
run_stage evidence node scripts/assert-g3-evidence.mjs "$evidence"
echo "G3 executable checks finished. Review all 25 scenario records before acceptance."
