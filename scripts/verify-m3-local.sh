#!/usr/bin/env bash
# Complete local M3 reference-data verification. GitHub Actions is not an execution option.
# All controllable project storage must resolve to E:; tests use disposable resources only.
set -euo pipefail

root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "Set HARBOROPS_VERIFICATION_ROOT to the approved E: verification directory" >&2; exit 2 ;;
esac
if ! command -v cygpath >/dev/null 2>&1; then
  echo "This runner requires Windows Git Bash with cygpath" >&2
  exit 2
fi
if [[ -z "${HARBOROPS_DOCKER_VHDX:-}" ]]; then
  echo "Set HARBOROPS_DOCKER_VHDX to the verified E: Docker Desktop data image" >&2
  exit 2
fi
docker_vhdx="$(cygpath -w "$HARBOROPS_DOCKER_VHDX")"
case "$docker_vhdx" in
  [Ee]:[/\\]*) ;;
  *) echo "Docker Desktop data image is not on E:" >&2; exit 2 ;;
esac
if [[ ! -f "$(cygpath -u "$docker_vhdx")" ]]; then
  echo "Docker Desktop data image path cannot be verified" >&2
  exit 2
fi
echo "Docker backing image checked: $docker_vhdx"
mkdir -p "$root/evidence"
export HARBOROPS_G3_JSON_REPORT="$root/evidence/playwright-g3.json"
run_stage() {
  local label="$1"
  shift
  "$@" 2>&1 | tee "$root/evidence/g3-${label}.log"
}
trap 'status=$?; if (( status != 0 )); then printf "G3 run aborted, exit=%s\n" "$status" > "$root/evidence/g3-aborted.txt"; fi' EXIT
export HARBOROPS_DB_PASSWORD="$(printenv HARBOROPS_DB_PASSWORD || true)"
if [[ -z "$HARBOROPS_DB_PASSWORD" ]]; then export HARBOROPS_DB_PASSWORD="disposable-m3-check-only"; fi
override="$root/compose.core0.yaml"
if [[ ! -f "$override" ]]; then
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
fi
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="compose.yaml;$(cygpath -w "$override")"

# Reuse the G2 runner E: guards and existing disposable-only Compose setup.
run_stage preflight bash scripts/verify-m2-local.sh preflight
sha=$(git rev-parse HEAD)
export HARBOROPS_SOURCE_SHA="$sha"
if [[ -n "$(git status --porcelain)" ]]; then
  echo "Verification requires a clean source revision" >&2
  exit 2
fi
echo "M3 verification source: $sha"
echo "Building revision-labelled disposable PostgreSQL test image"
docker build --build-arg "SOURCE_REVISION=$sha" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .

for stage in dotnet deps web docs stack accounts organizations session; do
  run_stage "$stage" bash scripts/verify-m2-local.sh "$stage"
done

# Read the synthetic credential from the disposable E: evidence root only.
export HARBOROPS_TEST_USER_PASSWORD
HARBOROPS_TEST_USER_PASSWORD=$(cat "$root/test-user-password.txt")
test -n "$HARBOROPS_TEST_USER_PASSWORD"

provision() {
  printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | docker compose -p ho-verify run --rm -T \
    -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision "$1" "$2"
}
provision m3-admin-a@example.test "M3 Synthetic North Administrator"
provision m3-admin-b@example.test "M3 Synthetic East Administrator"
provision m3-manager@example.test "M3 Synthetic Manager"
provision m3-analyst@example.test "M3 Synthetic Analyst"

docker compose -p ho-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
  --organization-cli bootstrap m3-north "M3 North Terminal" m3-admin-a@example.test
docker compose -p ho-verify run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
  --organization-cli bootstrap m3-east "M3 East Terminal" m3-admin-b@example.test

run_stage fixtures node scripts/prepare-m3-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1
for stage in browsers recovery scan; do
  run_stage "$stage" bash scripts/verify-m2-local.sh "$stage"
done
printf '%s\n' "$sha" > "$root/evidence/m3-source-sha.txt"
node scripts/assert-g3-evidence.mjs "$root/evidence"
echo "M3 local verification stages finished. Review all G3-S01 to G3-S25 evidence before declaring G3 verified."
