#!/usr/bin/env bash
# Complete local M3 reference-data verification. GitHub Actions is not an execution option.
# All controllable project storage must resolve to E:; tests use disposable resources only.
set -euo pipefail

root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "Set HARBOROPS_VERIFICATION_ROOT to the approved E: verification directory" >&2; exit 2 ;;
esac
mkdir -p "$root/evidence"
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

# Reuse the G2 runner E: guards and existing disposable-only Compose setup.
bash scripts/verify-m2-local.sh preflight
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

bash scripts/verify-m2-local.sh dotnet deps web docs stack accounts organizations session

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

node scripts/prepare-m3-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1
bash scripts/verify-m2-local.sh browsers recovery scan
printf '%s\n' "$sha" > "$root/evidence/m3-source-sha.txt"
echo "M3 local verification stages finished. Review all G3-S01 to G3-S25 evidence before declaring G3 verified."
