#!/usr/bin/env bash
# Local G2 verification stages for the isolated ho-verify-g6 Compose project.
# Every cache, temporary file and evidence path stays under HARBOROPS_VERIFICATION_ROOT on E:.
# Usage: bash scripts/verify-m2-local.sh <stage> [<stage>...]
#   stages: preflight build dotnet deps web docs stack accounts organizations session setup_fixtures browsers recovery scan down
set -euo pipefail

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
project="ho-verify-g6"
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
# Verification-only Compose override on E: that disables core dumps, so a crashing process inside
# a container cannot make the WSL crash handler write dump files to C:. compose.yaml is unchanged.
core_override="$root/compose.core0.yaml"
test -f "$core_override" || { echo "Missing $core_override" >&2; exit 2; }
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="compose.yaml;$(cygpath -w "$core_override")"
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

# Release build, unit, contract and PostgreSQL integration suites (fresh, then repeated against the
# now-migrated and seeded database) inside the pinned .NET 10 SDK image. The throwaway PostgreSQL
# container is the only database used; the Compose volumes are never touched.
stage_dotnet() {
  winroot=$(cygpath -w "$root")
  repo=$(pwd -W)
  if MSYS_NO_PATHCONV=1 docker ps -aq --filter name='^/ho-g6-pg$' | grep -q .; then
    echo "Existing G6 integration container: refusing takeover" >&2
    exit 2
  fi
  docker run -d --name ho-g6-pg --label harborops.g6=disposable --ulimit core=0 \
    --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 --cap-drop ALL \
    --security-opt no-new-privileges -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
    -e POSTGRES_HOST_AUTH_METHOD=trust harborops-postgres:16.15-rootless >/dev/null
  for _ in $(seq 1 45); do
    docker exec ho-g6-pg pg_isready -U harborops -d harborops_integration >/dev/null 2>&1 && break
    sleep 2
  done
  rm -rf "$evidence/trx"
  cat > "$root/run-dotnet.sh" <<'INNER'
set -uo pipefail
cd /src
dotnet --version
dotnet restore HarborOps.sln -v q || exit 10
dotnet build HarborOps.sln -c Release --no-restore 2>&1 | tail -6
test "${PIPESTATUS[0]}" -eq 0 || exit 11
T=(--no-build -c Release --logger "console;verbosity=normal" --results-directory /cache/evidence/trx)
status=0
run() {
  name="$1"; project="$2"
  echo "Starting $name; detailed results: /cache/evidence/$name.log"
  dotnet test "$project" "${T[@]}" --logger "trx;LogFileName=${name}.trx" 2>&1 | grep -v '^{"EventId' > "/cache/evidence/$name.log"
  code=${PIPESTATUS[0]}
  echo "$name exit=$code"
  grep -E "Total tests|Passed:|Failed:|Skipped:" "/cache/evidence/$name.log" || true
  if [ "$code" -ne 0 ]; then status=1; fi
}
run unit tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj
run contract tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj
run integration-fresh tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj
run integration-repeat tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj
exit $status
INNER
  # A failed build or test run must still dispose of the owned database container.
  # EXIT, not RETURN: errexit ends the shell without running a function RETURN trap.
  trap 'docker rm -f ho-g6-pg >/dev/null 2>&1 || true' EXIT
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --name ho-g6-sdk --network container:ho-g6-pg -v "$repo:/src" -v "$winroot:/cache" \
    -w /src -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e NUGET_PACKAGES=/cache/nuget/packages \
    -e NUGET_HTTP_CACHE_PATH=/cache/nuget/http-cache -e DOTNET_CLI_HOME=/cache/dotnet-home \
    -e HOME=/cache/dotnet-home -e TMPDIR=/cache/tmp \
    -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
    mcr.microsoft.com/dotnet/sdk:10.0 bash /cache/run-dotnet.sh
}

# Cheap compile gate. Restores and builds the solution in Release with warnings as errors (see
# Directory.Build.props) so a compile defect fails the run before any long stage starts.
stage_build() {
  winroot=$(cygpath -w "$root")
  repo=$(pwd -W)
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$repo:/src" -v "$winroot:/cache" -w /src -e DOTNET_NOLOGO=1     -e NUGET_PACKAGES=/cache/nuget/packages -e NUGET_HTTP_CACHE_PATH=/cache/nuget/http-cache     -e DOTNET_CLI_HOME=/cache/dotnet-home -e HOME=/cache/dotnet-home -e TMPDIR=/cache/tmp     mcr.microsoft.com/dotnet/sdk:10.0 bash -c     'set -o pipefail; dotnet --version; dotnet restore HarborOps.sln -v q && dotnet build HarborOps.sln -c Release --no-restore -warnaserror'
}

# G2-S25: dependency review with the NuGet advisory feed and npm audit.
stage_deps() {
  winroot=$(cygpath -w "$root")
  repo=$(pwd -W)
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$repo:/src" -v "$winroot:/cache" -w /src -e DOTNET_NOLOGO=1 \
    -e NUGET_PACKAGES=/cache/nuget/packages -e NUGET_HTTP_CACHE_PATH=/cache/nuget/http-cache \
    -e DOTNET_CLI_HOME=/cache/dotnet-home -e HOME=/cache/dotnet-home -e TMPDIR=/cache/tmp \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
    'set -o pipefail; dotnet list HarborOps.sln package --vulnerable --include-transitive 2>&1 | tee /cache/evidence/nuget-vulnerable.log'
  if grep -q "has the following vulnerable packages" "$evidence/nuget-vulnerable.log"; then
    echo "Vulnerable NuGet packages reported" >&2; exit 1
  fi
  (cd web && npm audit --audit-level=low)
  (cd tests/browser && npm audit --audit-level=low)
  echo "G2-S25 dependency review: no vulnerable NuGet or npm packages"
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
  # A rejected restricted command must exit cleanly with code 1. Any other non-zero code means the
  # process aborted, which would also let the host write a memory dump.
  expect_failure() {
    local rc=0
    "$@" > /dev/null 2>&1 || rc=$?
    if [[ "$rc" != 1 ]]; then
      echo "Expected a clean rejection (exit code 1) but observed $rc" >&2
      exit 1
    fi
  }
  expect_failure "${compose[@]}" run --rm -T api \
    --account-cli provision denied-account@example.test "Denied Account"
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

# Accounts, organizations and M3 browser tenants shared by every browser fixture generator.
stage_setup_fixtures() {
  for entry in     "m3-admin-a@example.test|M3 Synthetic North Administrator"     "m3-admin-b@example.test|M3 Synthetic East Administrator"     "m3-manager@example.test|M3 Synthetic Manager"     "m3-analyst@example.test|M3 Synthetic Analyst"     "m4-analyst-east@example.test|M4 Synthetic East Analyst"     "m4-support@example.test|M4 Synthetic Support"     "m4-auditor@example.test|M4 Synthetic Auditor"; do
    provision "${entry%%|*}" "${entry#*|}"
  done
  bootstrap_org() {
    "${compose[@]}" run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api       --organization-cli bootstrap "$1" "$2" "$3"
  }
  bootstrap_org m3-north "M3 North Terminal" m3-admin-a@example.test
  bootstrap_org m3-east "M3 East Terminal" m3-admin-b@example.test
  bootstrap_org m3-pagination "M3 Pagination Fixtures" m3-admin-a@example.test
  for engine in chromium firefox webkit; do
    bootstrap_org "m3-revocation-$engine" "M3 Revocation $engine" m3-admin-a@example.test
  done
}

stage_recovery() {
  node scripts/check-m6-scoped-recovery.mjs
}

stage_scan() {
  mkdir -p "$evidence/trivy" "$root/trivy-cache"
  sha=$(git rev-parse HEAD)
  for service in postgres api web; do
    container=$("${compose[@]}" ps -q "$service")
    test -n "$container"
    image=$(docker inspect --format '{{.Image}}' "$container")
    revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")
    test "$revision" = "$sha"
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 \
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
