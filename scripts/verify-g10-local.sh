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
compose() { docker compose -f compose.g10.verify.yaml -p "$project" "$@"; }
own_stack=0
cleanup() {
  local prior="$?"
  trap - EXIT
  if (( own_stack )); then
    compose down --volumes --remove-orphans >> "$root/evidence/teardown.log" 2>&1 || prior=1
  fi
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
stage worker-build dotnet build src/HarborOps.Worker/HarborOps.Worker.csproj -c Release --no-restore
own_stack=1
stage postgres compose up -d --build postgres
stage testdb compose exec -T postgres psql -U harborops -d harborops -v ON_ERROR_STOP=1 -c "CREATE DATABASE harborops_integration"
stage integration dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-restore
stage web-install npm --prefix web ci
stage web-test npm --prefix web test
stage web-build npm --prefix web run build
stage compose-api compose up -d --build migrator api web
stage config compose config
git status --porcelain > "$root/evidence/status-after.txt"
echo "Technical helper completed. Browser and outage gates still require separate review."
