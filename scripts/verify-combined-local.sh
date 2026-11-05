#!/usr/bin/env bash
# Combined M9/M16 source-locked local verification (PR 25 integration candidate).
# Must execute on a Windows E: checkout. No GitHub Actions, shared database, non-E: project storage or
# retained stack. Exit 0 means every scripted stage passed; it is not G16 acceptance.
#
# Required environment:
#   HARBOROPS_VERIFICATION_ROOT   new E: path ending in /cg16 (single use)
# Optional: HARBOROPS_COMBINED_STOP_AFTER=<stage> stops cleanly after that stage (development only).
set -euo pipefail
cd "$(dirname "$0")/.."
for tool in cygpath git node npm docker curl sha256sum; do
  command -v "$tool" >/dev/null || { echo "Required tool missing: $tool" >&2; exit 2; }
done
case "$(pwd -W)" in [Ee]:[/\\]*) ;; *) echo "Use an E: checkout" >&2; exit 2 ;; esac
root_input="${HARBOROPS_VERIFICATION_ROOT:-}"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "HARBOROPS_VERIFICATION_ROOT must be E:-backed" >&2; exit 2 ;; esac
case "$(cygpath -m "$root_input")" in [Ee]:/*/cg16) ;; *)
  echo "Dedicated E: verification root ending /cg16 required" >&2; exit 2 ;; esac
root="$(cygpath -u "$(cygpath -w "$root_input")")"
repo_win="$(pwd -W)"
[[ -z "$(git status --porcelain)" ]] || { echo "Source checkout must be clean" >&2; exit 2; }
revision="$(git rev-parse HEAD)"
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || exit 2
[[ ! -e "$root/evidence" ]] || { echo "Existing combined evidence cannot be overwritten" >&2; exit 2; }
mkdir -p "$root/evidence" "$root/cache" "$root/private"
evidence="$root/evidence"
printf '%s\n' "$revision" > "$evidence/source-sha.txt"
date -u +%FT%TZ > "$evidence/started-utc.txt"
printf 'stage\tsha\trc\tseconds\tstart_utc\texecuted\n' > "$evidence/stages.tsv"
sha256sum scripts/verify-combined-local.sh scripts/check-combined-stack.mjs scripts/run-combined-browsers-local.sh \
  scripts/run-combined-mutations.mjs scripts/check-m16-db-roles.sh scripts/check-m16-audit-http.mjs \
  scripts/check-m16-audit-outage.mjs scripts/check-m16-evidence-safety.mjs scripts/smoke-compose.sh \
  compose.g16.verify.yaml compose.m9.worker.yaml compose.cg16.override.yaml > "$evidence/verification-script-hashes.txt"
export HARBOROPS_SOURCE_SHA="$revision"
export HARBOROPS_CG16_PG_IMAGE="harborops-cg16-postgres:${revision:0:12}"
export HARBOROPS_VERIFICATION_ROOT="$(cygpath -w "$root")"
export DOCKER_HOST="npipe:////./pipe/dockerDesktopLinuxEngine"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5176"
# Windows Compose joins multiple files with a semicolon.
export COMPOSE_FILE="$(cygpath -w "$(pwd)/compose.g16.verify.yaml");$(cygpath -w "$(pwd)/compose.m9.worker.yaml");$(cygpath -w "$(pwd)/compose.cg16.override.yaml")"
export COMPOSE_PROJECT_DIRECTORY="$(cygpath -w "$(pwd)")"
for var in TEMP TMP NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH NUGET_PACKAGES DOTNET_CLI_HOME XDG_CACHE_HOME; do
  mkdir -p "$root/cache/$var"
  export "$var=$(cygpath -w "$root/cache/$var")"
done
export TMPDIR="$root/cache/TMPDIR" HOME="$root/cache/HOME"
mkdir -p "$TMPDIR" "$HOME" "$root/cache/nuget/http" "$root/cache/nuget/packages"
export USERPROFILE="$(cygpath -w "$HOME")"
# The Docker Desktop disk/WSL registration preflight must succeed before creating any resource.
node scripts/check-m3-docker-storage.mjs
for key in HARBOROPS_DB_PASSWORD HARBOROPS_API_DB_PASSWORD HARBOROPS_WORKER_DB_PASSWORD; do
  if [[ -z "${!key:-}" ]]; then
    export "$key=$(node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))')"
  fi
done
[[ "$HARBOROPS_DB_PASSWORD" != "$HARBOROPS_API_DB_PASSWORD" &&
   "$HARBOROPS_DB_PASSWORD" != "$HARBOROPS_WORKER_DB_PASSWORD" &&
   "$HARBOROPS_API_DB_PASSWORD" != "$HARBOROPS_WORKER_DB_PASSWORD" ]] ||
   { echo "Distinct database secrets are mandatory" >&2; exit 2; }
project="ho-verify-cg16"
export G16_PROJECT="$project" HARBOROPS_CG16_PROJECT="$project"
sdk_db="ho-cg16-run-pg"
owned_sdk=0
owned_stack=0
compose() { docker compose -p "$project" --profile worker "$@"; }
if [[ -n "$(docker ps -aq --filter "name=^/${project}-")" ||
      -n "$(docker volume ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$")" ]]; then
  echo "Existing combined resources found, refusing takeover" >&2; exit 2
fi
stopped_early=0
stage() {
  local name="$1" start="$SECONDS" rc=0 startutc
  startutc="$(date -u +%FT%TZ)"
  shift
  # Development rehearsal only: a skipped stage is recorded as not executed, so the independent
  # summary can never accept such a run as gate evidence.
  if [[ ",${HARBOROPS_COMBINED_DEV_SKIP:-}," == *",$name,"* ]]; then
    printf '%s\t%s\t0\t0\t%s\tDEV-SKIPPED\n' "$name" "$revision" "$startutc" >> "$evidence/stages.tsv"
    echo "Combined stage $name skipped (development rehearsal)"
    return 0
  fi
  echo "Combined stage $name"
  if "$@" > "$evidence/$name.log" 2>&1; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\t%s\tyes\n' "$name" "$revision" "$rc" "$((SECONDS-start))" "$startutc" >> "$evidence/stages.tsv"
  if ((rc)); then
    echo "Combined stage $name failed; see E:-backed evidence log" >&2
    tail -25 "$evidence/$name.log" >&2 || true
  elif [[ "${HARBOROPS_COMBINED_STOP_AFTER:-}" == "$name" ]]; then
    stopped_early=1
    echo "Development stop after $name" >&2
    exit 3
  fi
  return "$rc"
}
cleanup() {
  local rc=$?
  trap - EXIT
  if [[ "$owned_stack" == 1 ]]; then
    compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || rc=1
    compose down --volumes --remove-orphans > "$evidence/compose-teardown.log" 2>&1 || rc=1
  fi
  if [[ "$owned_sdk" == 1 ]]; then
    docker rm -f "$sdk_db" > "$evidence/postgres-teardown.log" 2>&1 || rc=1
  fi
  local containers volumes leftover
  containers="$(docker ps -aq --filter "name=^/${project}-" || true)"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=$project" || true)"
  leftover="$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$" || true)"
  [[ -z "$containers" && -z "$volumes" && -z "$leftover" ]] || rc=1
  printf 'containers=%s volumes=%s sdk=%s\n' "$containers" "$volumes" "$leftover" > "$evidence/teardown.txt"
  date -u +%FT%TZ > "$evidence/ended-utc.txt"
  # Traces may contain session cookies. Preserve them privately on E:, never in the shareable evidence tree.
  if [[ -d "$evidence/playwright" ]]; then
    while IFS= read -r -d '' trace; do
      relative="${trace#"$evidence/"}"
      mkdir -p "$root/private/quarantine/$(dirname "$relative")"
      mv "$trace" "$root/private/quarantine/$relative" || rc=1
    done < <(find "$evidence/playwright" -type f \( -name '*.zip' -o -name '*.gz' -o -name '*.7z' \) -print0)
  fi
  if [[ -d "$root/private/quarantine" ]]; then
    (cd "$root/private/quarantine" &&
      find . -type f -print0 | sort -z | xargs -0 sha256sum) \
      > "$root/private/quarantine-manifest.sha256" || rc=1
    echo "Private browser traces quarantined under E:; do not publish them."
  fi
  if ! node scripts/check-m16-evidence-safety.mjs "$HARBOROPS_VERIFICATION_ROOT" \
      > "$evidence/evidence-safety.log" 2>&1; then
    echo "Combined evidence may contain private data; quarantine on E: and do not share" >&2
    rc=1
  fi
  (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
    > "$root/evidence-manifest.sha256" || rc=1
  if [[ "$stopped_early" == 1 ]]; then rc=3; fi
  printf '%s\n' "$rc" > "$root/technical-exit-code.txt"
  if [[ -n "${test_password_file:-}" ]]; then rm -f "$test_password_file"; fi
  if ((rc)); then
    echo "Combined technical run FAILED, stopped or incomplete. Gate acceptance pending." >&2
  else
    echo "Combined scripted stages completed. Scenario reconciliation, independent review and gate acceptance pending."
  fi
  exit "$rc"
}
trap cleanup EXIT
test_password_file="$root/private/test-password.txt"
node -e 'process.stdout.write(require("node:crypto").randomBytes(32).toString("hex"))' > "$test_password_file"
chmod 600 "$test_password_file"
export HARBOROPS_TEST_USER_PASSWORD="$(cat "$test_password_file")"

stage preflight bash -c 'git rev-parse HEAD; node --version; npm --version; docker version; docker compose version'
stage docs bash -c 'node scripts/render-execution-graph.mjs --check && node scripts/check-planning-docs.mjs && node --test scripts/check-m8-contract.test.mjs scripts/check-m9-contract.test.mjs'
stage scripts bash -c 'bash -n scripts/verify-combined-local.sh scripts/run-combined-browsers-local.sh scripts/check-m16-db-roles.sh && node --check scripts/check-combined-stack.mjs && node --check scripts/run-combined-mutations.mjs && node --check scripts/check-m16-evidence-safety.mjs'
stage web-build bash -c 'npm --prefix web ci --no-audit --no-fund && npm --prefix web test && npm --prefix web run build && ! grep -R -l "hop_v1_" web/dist'
stage package-audit bash -c 'npm --prefix web audit --audit-level=high && npm --prefix tests/browser audit --audit-level=high'
stage postgres-image docker build --build-arg "SOURCE_REVISION=$revision" \
  -f docker/postgres/Dockerfile -t "$HARBOROPS_CG16_PG_IMAGE" .

start_sdk_db() {
  docker run -d --name "$sdk_db" --label harborops.cg16=disposable --ulimit core=0 \
    --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 \
    --cap-drop ALL --security-opt no-new-privileges \
    -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
    -e POSTGRES_HOST_AUTH_METHOD=trust "$HARBOROPS_CG16_PG_IMAGE" || return 1
  owned_sdk=1
  local i
  for i in $(seq 1 50); do
    if docker exec "$sdk_db" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  echo "Disposable PostgreSQL is not ready" >&2
  return 1
}
fresh_sdk_db() {
  docker rm -f "$sdk_db" >/dev/null 2>&1 || true
  start_sdk_db
}
stage sdk-postgres-1 start_sdk_db
sdk() {
  local command="$1"
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --network "container:$sdk_db" \
    -v "$repo_win:/src" -v "$(cygpath -w "$root"):/cache" -w /src \
    -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e NUGET_PACKAGES=/cache/cache/nuget/packages \
    -e NUGET_HTTP_CACHE_PATH=/cache/cache/nuget/http \
    -e DOTNET_CLI_HOME=/cache/cache/DOTNET_CLI_HOME \
    -e HOME=/cache/cache/HOME -e TMPDIR=/cache/cache/TMPDIR \
    -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -euc "$command"
}
mkdir -p "$evidence/trx"
stage dotnet-build sdk 'dotnet restore HarborOps.sln --verbosity minimal && dotnet build HarborOps.sln -c Release --no-restore -warnaserror'
stage unit sdk 'dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=cg16-unit.trx" --results-directory /cache/evidence/trx'
stage contract sdk 'dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build --logger "trx;LogFileName=cg16-contract.trx" --results-directory /cache/evidence/trx'
stage dependencies sdk 'dotnet list HarborOps.sln package --vulnerable --include-transitive'
stage integration sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=cg16-integration.trx" --results-directory /cache/evidence/trx'
# The repeat pass uses a brand new database container so it cannot depend on state left by the first pass.
stage sdk-postgres-2 fresh_sdk_db
stage integration-repeat sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=cg16-integration-repeat.trx" --results-directory /cache/evidence/trx'

owned_stack=1
stage compose bash scripts/smoke-compose.sh --project "$project" --confirm-disposable
worker_up() {
  compose build worker || return 1
  compose up -d --no-build worker || return 1
  local i health
  for i in $(seq 1 60); do
    health="$(compose ps worker --format '{{.State}} {{.Health}}')"
    [[ "$health" == "running healthy" ]] && return 0
    sleep 5
  done
  echo "Worker did not become healthy: $health" >&2
  return 1
}
stage worker-up worker_up
stage restricted-db-roles bash scripts/check-m16-db-roles.sh
image_scans() {
  local trivy="aquasec/trivy:0.76.1"
  local cache="$(cygpath -w "$root/cache/trivy"):/root/.cache/trivy"
  local reports="$(cygpath -w "$evidence/trivy"):/reports"
  mkdir -p "$root/cache/trivy" "$evidence/trivy"
  local ready=0
  for attempt in 1 2 3; do
    if MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$cache" \
        "$trivy" image --download-db-only; then ready=1; break; fi
    sleep 10
  done
  [[ "$ready" == 1 ]] || { echo "Vulnerability database unavailable" >&2; return 1; }
  for service in postgres api web worker; do
    local id image revision
    id="$(compose ps -q "$service")" || return 1
    [[ -n "$id" ]] || return 1
    image="$(docker inspect --format '{{.Image}}' "$id")" || return 1
    revision="$(docker image inspect \
      --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")" || return 1
    [[ "$revision" == "$HARBOROPS_SOURCE_SHA" ]] || return 1
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 \
      -v /var/run/docker.sock:/var/run/docker.sock \
      -v "$cache" -v "$reports" "$trivy" \
      image --image-src docker --scanners vuln --severity HIGH,CRITICAL \
      --exit-code 1 --format json --output "/reports/$service.json" "$image" || return 1
    [[ -s "$evidence/trivy/$service.json" ]] || return 1
  done
}
stage image-scans image_scans
provision() {
  printf '%s\n' "$HARBOROPS_TEST_USER_PASSWORD" | compose run --rm -T \
    -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision "$1" "$2"
}
accounts() {
  for row in \
    "operator@example.test|Synthetic Operator" \
    "m2-operator@example.test|M2 Administrator" \
    "m2-second-admin@example.test|M2 Second Administrator" \
    "m2-outage@example.test|M2 Outage" \
    "m2-browser-chromium@example.test|Synthetic Browser Chromium Member" \
    "m2-browser-firefox@example.test|Synthetic Browser Firefox Member" \
    "m2-browser-webkit@example.test|Synthetic Browser Webkit Member" \
    "m3-admin-a@example.test|M3 North Administrator" \
    "m3-admin-b@example.test|M3 East Administrator" \
    "m3-manager@example.test|M3 Manager" \
    "m3-analyst@example.test|M3 Analyst" \
    "m4-analyst-east@example.test|M4 East Analyst" \
    "m4-support@example.test|M4 Support" \
    "m4-auditor@example.test|M4 Auditor" \
    "g16h-admin@example.test|G16 HTTP admin" \
    "g16h-aud@example.test|G16 HTTP aud" \
    "g16h-mgr@example.test|G16 HTTP mgr" \
    "g16h-ana@example.test|G16 HTTP ana" \
    "g16h-sup@example.test|G16 HTTP sup" \
    "g16h-rev1@example.test|G16 HTTP rev1" \
    "g16h-rev2@example.test|G16 HTTP rev2" \
    "g16h-rev3@example.test|G16 HTTP rev3" \
    "g16h-rev4@example.test|G16 HTTP rev4" \
    "g16h-multi@example.test|G16 HTTP multi" \
    "g16h-mix@example.test|G16 HTTP mix" \
    "g16h-fault@example.test|G16 HTTP fault" \
    "g16h-flood@example.test|G16 HTTP flood" \
    "g16h-out@example.test|G16 HTTP out" \
    "g16h-outmgr@example.test|G16 HTTP outmgr"; do
    IFS='|' read -r email display <<< "$row"
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
  bootstrap g16h-a "G16 HTTP A" g16h-admin@example.test || return 1
  bootstrap g16h-b "G16 HTTP B" g16h-admin@example.test || return 1
  for engine in chromium firefox webkit; do
    bootstrap "m3-revocation-$engine" "M3 Revocation $engine" m3-admin-a@example.test || return 1
  done
}
stage browser-organizations organizations
stage browser-m3-fixtures node scripts/prepare-m3-browser-fixtures.mjs
stage browser-m4-fixtures node scripts/prepare-m4-browser-fixtures.mjs
stage browser-m5-fixtures node scripts/prepare-m5-browser-fixtures.mjs
stage browser-m6-fixtures node scripts/prepare-m6-browser-fixtures.mjs
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
export G16_API_BASE="http://localhost:8086"
export G16_PSQL="docker compose -p $project exec -T postgres psql -X -U harborops -d harborops -Atq"
export G16_COMPOSE="docker compose -p $project"
export G16_NETWORK="${project}_internal"
stage audit-http-matrix node scripts/check-m16-audit-http.mjs "$evidence/audit-http-results.json"
stage audit-outage node scripts/check-m16-audit-outage.mjs "$evidence/audit-outage-results.json"
stage combined-stack-check node scripts/check-combined-stack.mjs "$evidence/combined-stack"
stage browser-engines bash scripts/run-combined-browsers-local.sh
stage security-mutations node scripts/run-combined-mutations.mjs "$HARBOROPS_VERIFICATION_ROOT"
# Final teardown, evidence safety and manifest run in the exit trap; no acceptance is inferred.
