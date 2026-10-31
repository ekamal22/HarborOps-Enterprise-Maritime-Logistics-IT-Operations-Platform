#!/usr/bin/env bash
# Local G2 verification stages for the isolated ho-verify Compose project.
# Every cache, temporary file and evidence path stays under HARBOROPS_VERIFICATION_ROOT on E:.
# Usage: bash scripts/verify-m2-local.sh <stage> [<stage>...]
#   stages: preflight web docs stack accounts organizations session browsers recovery scan down
set -euo pipefail
export MSYS_NO_PATHCONV=1

root="${HARBOROPS_VERIFICATION_ROOT:-}"
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be a directory on E:" >&2; exit 2 ;;
esac
for dir in "${TEMP:-}" "${TMP:-}" "${TMPDIR:-}" "${NPM_CONFIG_CACHE:-}" "${PLAYWRIGHT_BROWSERS_PATH:-}"; do
  resolved="$dir"
  if [[ -n "$dir" ]] && command -v cygpath >/dev/null; then resolved=$(cygpath -w "$dir"); fi
  case "$resolved" in
    [Ee]:[/\\]*) ;;
    *) echo "Temporary and cache locations must resolve to E: (found '${dir}' -> '${resolved}')" >&2; exit 2 ;;
  esac
done
evidence="$root/evidence"
mkdir -p "$evidence"
project="ho-verify"
export COMPOSE_PROJECT_NAME="$project"
export HARBOROPS_DB_PASSWORD="${HARBOROPS_DB_PASSWORD:-disposable-m2-check-only}"
# One synthetic credential per verification root, kept on E: so separate stage runs agree.
secret_file="$root/test-user-password.txt"
if [[ -z "${HARBOROPS_TEST_USER_PASSWORD:-}" ]]; then
  if [[ ! -s "$secret_file" ]]; then
    node -e 'process.stdout.write(require("node:crypto").randomBytes(32).toString("hex"))' > "$secret_file"
  fi
  HARBOROPS_TEST_USER_PASSWORD=$(cat "$secret_file")
  export HARBOROPS_TEST_USER_PASSWORD
fi
compose=(docker compose -p "$project")

stage_preflight() {
  git rev-parse HEAD
  git status --short
  node --version
  npm --version
  docker version --format 'docker server {{.Server.Version}}'
  docker compose version
  docker info --format 'docker root {{.DockerRootDir}}'
}

stage_web() {
  (cd web && npm ci --no-audit --no-fund && npm test && npm run build)
}

stage_docs() {
  node scripts/render-execution-graph.mjs --check
  node scripts/check-planning-docs.mjs
}

stage_stack() {
  "${compose[@]}" down --volumes --remove-orphans || true
  bash scripts/smoke-compose.sh --project "$project" --confirm-disposable
  bash scripts/check-m0-degraded.sh --project "$project" --confirm-local-outage
}

provision() {
  printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | "${compose[@]}" run --rm -T \
    -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision "$1" "$2"
}

stage_accounts() {
  provision operator@example.test "Synthetic Operator"
  provision m2-operator@example.test "M2 Synthetic Administrator"
  for entry in \
    "m2-second-admin@example.test|Second Administrator" \
    "m2-outage@example.test|Outage Synthetic Member" \
    "m2-browser-chromium@example.test|Synthetic Browser Chromium Member" \
    "m2-browser-firefox@example.test|Synthetic Browser Firefox Member" \
    "m2-browser-webkit@example.test|Synthetic Browser Webkit Member"; do
    provision "${entry%%|*}" "${entry#*|}"
  done
}

stage_organizations() {
  "${compose[@]}" run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap north-terminal "North Terminal" m2-operator@example.test
  "${compose[@]}" run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap east-terminal "East Terminal" m2-second-admin@example.test
  expect_failure() {
    if "$@" > /dev/null 2>&1; then
      echo "An intentionally invalid organization operation succeeded" >&2
      exit 1
    fi
  }
  expect_failure "${compose[@]}" run --rm -T api \
    --organization-cli bootstrap unauthorized-terminal "Unapproved" m2-second-admin@example.test
  expect_failure "${compose[@]}" run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap unknown-terminal "Unknown" unknown-account@example.test
  expect_failure "${compose[@]}" run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap north-terminal "Duplicate" m2-operator@example.test
  count=$("${compose[@]}" exec -T postgres psql -U harborops -d harborops -Atc \
    "SELECT count(*) FROM organizations WHERE slug IN ('unauthorized-terminal', 'unknown-terminal')")
  test "$count" = "0"
  echo "G2 restricted CLI denial and atomic bootstrap: PASS"
}

stage_session() {
  bash scripts/check-m1-session-recovery.sh --project "$project" --confirm-local-outage
}

stage_browsers() {
  (cd tests/browser && npm ci --no-audit --no-fund && npx playwright install chromium firefox webkit && npm test)
}

stage_recovery() {
  node scripts/check-m2-scoped-recovery.mjs
}

stage_scan() {
  mkdir -p "$evidence/trivy"
  sha=$(git rev-parse HEAD)
  for service in postgres api web; do
    container=$("${compose[@]}" ps -q "$service")
    test -n "$container"
    image=$(docker inspect --format '{{.Image}}' "$container")
    revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")
    test "$revision" = "$sha"
    docker run --rm \
      -v /var/run/docker.sock:/var/run/docker.sock \
      -v "$root/trivy-cache:/root/.cache/trivy" \
      -v "$evidence/trivy:/reports" \
      aquasec/trivy:0.76.1 image --image-src docker \
      --scanners vuln --severity HIGH,CRITICAL --exit-code 1 \
      --format json --output "/reports/$service.json" "$image"
    echo "trivy $service ($image): no HIGH or CRITICAL findings"
  done
}

stage_down() {
  "${compose[@]}" down --volumes --remove-orphans
}

for stage in "$@"; do
  echo "== stage: $stage =="
  "stage_$stage"
done
