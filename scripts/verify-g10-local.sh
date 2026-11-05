#!/usr/bin/env bash
# Local M10 source-locked verification helper. E:-backed resources only.
# A successful helper is technical evidence, never G10 acceptance.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Use Windows Git Bash" >&2; exit 2; }
command -v dotnet >/dev/null || { echo "dotnet 10 SDK is required locally" >&2; exit 2; }
command -v node >/dev/null || exit 2
command -v npm >/dev/null || exit 2
command -v docker >/dev/null || exit 2
root_input="${HARBOROPS_VERIFICATION_ROOT:-}"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
case "$(cygpath -m "$root_input")" in [Ee]:/*/m10) ;; *)
  echo "Choose a fresh E:-backed root ending in /m10" >&2; exit 2 ;; esac
case "$(pwd -W)" in [Ee]:[/\\]*) ;; *) echo "Checkout must be on E:" >&2; exit 2 ;; esac
[[ -z "$(git status --porcelain)" ]] || { echo "Clean committed checkout required" >&2; exit 2; }
root="$(cygpath -u "$(cygpath -w "$root_input")")"
[[ ! -e "$root/evidence" && ! -e "$root/private" ]] || {
  echo "Refusing to overwrite prior evidence or private files" >&2; exit 2; }
project="${HARBOROPS_G10_PROJECT:-}"
[[ "$project" =~ ^ho-verify-g10-[a-z0-9]{1,12}$ ]] || {
  echo "Choose a unique ho-verify-g10-<suffix> project" >&2; exit 2; }
for port in 55410 8090 5180; do
  if netstat -ano 2>/dev/null | grep -E "[:.]$port .*LISTENING" >/dev/null; then
    echo "Reserved port $port is in use" >&2; exit 2
  fi
done
[[ -z "$(docker ps -aq --filter "label=com.docker.compose.project=$project")" ]] || {
  echo "A Compose project with that name already exists" >&2; exit 2; }
mkdir -p "$root/evidence" "$root/private" "$root/cache"
for name in TEMP TMP TMPDIR NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH DOTNET_CLI_HOME XDG_CACHE_HOME; do
  mkdir -p "$root/cache/$name"
  export "$name=$(cygpath -w "$root/cache/$name")"
done
export HOME="$(cygpath -w "$root/cache")" USERPROFILE="$(cygpath -w "$root/cache")"
export NUGET_PACKAGES="$(cygpath -w "$root/cache/nuget")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$root/cache/nuget"
node scripts/check-m3-docker-storage.mjs
export HARBOROPS_SOURCE_SHA="$(git rev-parse HEAD)"
printf '%s\n' "$HARBOROPS_SOURCE_SHA" > "$root/evidence/source-sha.txt"
git status --porcelain > "$root/evidence/status-before.txt"
node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))'   > "$root/private/db-password.txt"
node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))'   > "$root/private/worker-password.txt"
export HARBOROPS_DB_PASSWORD="$(cat "$root/private/db-password.txt")"
export HARBOROPS_WORKER_DB_PASSWORD="$(cat "$root/private/worker-password.txt")"
export HARBOROPS_TEST_DB="Host=127.0.0.1;Port=55410;Database=harborops_integration;Username=harborops;Password=$HARBOROPS_DB_PASSWORD"
export HARBOROPS_G10_PROJECT="$project"
node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))' > "$root/private/user-password.txt"
export HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/private/user-password.txt")"
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5180"
export HARBOROPS_G10_PLAYWRIGHT_OUT="$root/evidence/playwright/artifacts"
mkdir -p "$HARBOROPS_G10_PLAYWRIGHT_OUT"
compose() { docker compose -f compose.g10.verify.yaml -p "$project" "$@"; }
own_stack=0
cleanup() {
  local prior="$?"
  trap - EXIT
  if (( own_stack )); then
    compose down --volumes --remove-orphans >> "$root/evidence/teardown.log" 2>&1 || prior=1
  fi
  # Delete only task-created disposable secret files after the owned stack stops.
  rm -f "$root/private/db-password.txt" "$root/private/worker-password.txt" \
        "$root/private/user-password.txt"
  rmdir "$root/private" 2>/dev/null || true
  printf '%s\n' "$prior" > "$root/evidence/technical-exit-code.txt"
  exit "$prior"
}
trap cleanup EXIT
stage() {
  local name="$1"
  shift
  if "$@" > "$root/evidence/$name.log" 2>&1; then
    printf '%s\tPASS\n' "$name" >> "$root/evidence/stages.tsv"
  else
    printf '%s\tFAIL\n' "$name" >> "$root/evidence/stages.tsv"
    echo "Stage $name failed. See E:-backed evidence" >&2
    return 1
  fi
}
stage docs node scripts/check-planning-docs.mjs
stage graph node scripts/render-execution-graph.mjs --check
stage scripts node --test scripts/*.test.mjs
stage restore dotnet restore HarborOps.sln
stage unit dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-restore
stage contract dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-restore
stage solution-build dotnet build HarborOps.sln -c Release --no-restore -warnaserror
stage worker-build dotnet build src/HarborOps.Worker/HarborOps.Worker.csproj -c Release --no-restore -warnaserror
own_stack=1
stage postgres compose up -d --build postgres
stage testdb compose exec -T postgres psql -U harborops -d harborops -v ON_ERROR_STOP=1 -c "CREATE DATABASE harborops_integration"
stage integration dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-restore
stage web-install npm --prefix web ci
stage web-test npm --prefix web test
stage web-build npm --prefix web run build
stage compose-api compose up -d --build migrator api web
stage config compose config -q

ready() {
  local code="000"
  for i in $(seq 1 90); do
    code="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8090/health/ready || true)"
    [[ "$code" == "200" ]] && return 0
    sleep 2
  done
  echo "Isolated G10 API was not ready" >&2
  return 1
}
stage ready ready

projector_role() {
  compose exec -T postgres psql -U harborops -d harborops -v ON_ERROR_STOP=1 \
    < scripts/configure-m10-projector-db.sql
}
stage projector-role-setup projector_role
projector_privileges() {
  local got
  got="$(compose exec -T postgres psql -U harborops -d harborops -At -c \
    "SELECT has_table_privilege('harborops_projector','tracking_processing_effects','INSERT'),
            has_table_privilege('harborops_projector','tracking_processing_effects','UPDATE'),
            has_table_privilege('harborops_projector','tracking_processing_effects','DELETE'),
            has_table_privilege('harborops_projector','tracking_processing_effects','SELECT'),
            has_table_privilege('harborops_projector','shipment_tracking_projections','UPDATE')")"
  [[ "$got" == "f|f|f|t|t" ]]
}
stage projector-role-privileges projector_privileges

provision() {
  printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | compose run --rm -T \
    -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision "$1" "$2"
}
accounts() {
  for entry in \
    "operator@example.test|Synthetic Operator" \
    "m2-operator@example.test|M2 Synthetic Administrator" \
    "m2-second-admin@example.test|Second Administrator" \
    "m2-outage@example.test|Outage Synthetic Member" \
    "m2-browser-chromium@example.test|Synthetic Browser Chromium Member" \
    "m2-browser-firefox@example.test|Synthetic Browser Firefox Member" \
    "m2-browser-webkit@example.test|Synthetic Browser Webkit Member" \
    "m3-admin-a@example.test|M3 Synthetic North Administrator" \
    "m3-admin-b@example.test|M3 Synthetic East Administrator" \
    "m3-manager@example.test|M3 Synthetic Manager" \
    "m3-analyst@example.test|M3 Synthetic Analyst" \
    "m4-analyst-east@example.test|M4 Synthetic East Analyst" \
    "m4-support@example.test|M4 Synthetic Support" \
    "m4-auditor@example.test|M4 Synthetic Auditor"; do
    IFS='|' read -r email display <<< "$entry"
    provision "$email" "$display" || return 1
  done
}
stage browser-accounts accounts

bootstrap() {
  compose run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap "$1" "$2" "$3"
}
organizations() {
  bootstrap north-terminal "North Terminal" m2-operator@example.test || return 1
  bootstrap east-terminal "East Terminal" m2-second-admin@example.test || return 1
  bootstrap m3-north "M3 North Terminal" m3-admin-a@example.test || return 1
  bootstrap m3-east "M3 East Terminal" m3-admin-b@example.test || return 1
  bootstrap m3-pagination "M3 Pagination Fixtures" m3-admin-a@example.test || return 1
  for engine in chromium firefox webkit; do
    bootstrap "m3-revocation-$engine" "M3 Revocation $engine" m3-admin-a@example.test || return 1
  done
}
stage browser-organizations organizations
stage browser-fixtures bash -c \
  'node scripts/prepare-m3-browser-fixtures.mjs && node scripts/prepare-m4-browser-fixtures.mjs && node scripts/prepare-m5-browser-fixtures.mjs && node scripts/prepare-m6-browser-fixtures.mjs'
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
stage browser-install npm --prefix tests/browser ci
stage browser-drivers bash -c 'cd tests/browser && npx playwright install chromium firefox webkit'
stage browser-3-engines bash -c \
  'cd tests/browser && npx playwright test --project=chromium --project=firefox --project=webkit --reporter=line --output="$HARBOROPS_G10_PLAYWRIGHT_OUT"'

# Store a source-locked manifest without including the temporary credentials.
git status --porcelain > "$root/evidence/status-after.txt"
printf '%s\n' "browser functional coverage completed; independent outage, mutation, vulnerability and evidence reviews remain pending" \
  > "$root/evidence/remaining-gates.txt"
( cd "$root/evidence" && find . -type f ! -name 'sha256-manifest.txt' -print0 \
  | sort -z | xargs -0 sha256sum ) > "$root/evidence/sha256-manifest.txt"
echo "Local technical stages completed; source-locked independent G10 review is still required."

