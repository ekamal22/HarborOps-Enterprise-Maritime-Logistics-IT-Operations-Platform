#!/usr/bin/env bash
# Disposable source-locked G8 technical verification, local E: storage only.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Windows Git Bash required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
# Judge the drive from the Windows form: Git Bash may mount E: at /tmp or /e depending on TEMP.
case "$(cygpath -m "$root_input")" in [Ee]:/*/m8) ;; *) echo "Dedicated E: path ending in /m8 required" >&2; exit 2 ;; esac
root_win="$(cygpath -w "$root_input")"
root="$(cygpath -u "$root_win")"
export HARBOROPS_VERIFICATION_ROOT="$root_win"
case "$(pwd -W)" in [Ee]:[/\\]*) ;; *) echo "Checkout must be on E:" >&2; exit 2 ;; esac
[[ -z "$(git status --porcelain)" ]] || { echo "Clean checkout required" >&2; exit 2; }
repo_win="$(pwd -W)"
mkdir -p "$root"
for var in TEMP TMP NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH NUGET_PACKAGES DOTNET_CLI_HOME DOCKER_CONFIG XDG_CACHE_HOME; do
  folder="$root/cache/$var"
  mkdir -p "$folder"
  export "$var=$(cygpath -w "$folder")"
done
export TMPDIR="$root/cache/TMPDIR" HOME="$root/cache/HOME"
mkdir -p "$TMPDIR" "$HOME"
mkdir -p "$root/cache/nuget/packages" "$root/cache/nuget/http" "$root/cache/dotnet-home" "$root/cache/tmp"
export USERPROFILE="$(cygpath -w "$HOME")"
export DOCKER_HOST="npipe:////./pipe/dockerDesktopLinuxEngine"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
node scripts/check-m3-docker-storage.mjs
export HARBOROPS_SOURCE_SHA="$(git rev-parse HEAD)"
[[ "$HARBOROPS_SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]] || exit 2
export HARBOROPS_DB_PASSWORD="$(printenv HARBOROPS_DB_PASSWORD || true)"
[[ -n "$HARBOROPS_DB_PASSWORD" ]] || export HARBOROPS_DB_PASSWORD="disposable-g8-only"
project="ho-verify-g8"
sdk_db="ho-g8-pg"
compose() { docker compose -p "$project" "$@"; }
if [[ -n "$(docker ps -aq --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker volume ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$")" ]]; then
  echo "Existing G8 resources detected, refusing takeover" >&2; exit 2
fi
if [[ -e "$root/private/source-bearer.txt" || -e "$root/private/test-user-password.txt" ]]; then
  echo "Existing private fixture detected, refusing overwrite" >&2; exit 2
fi
# A verification root is single-use. Never rotate or replace historical runs.
if [[ -e "$root/evidence" ]]; then
  echo "G8 evidence already exists; select a new E:-backed /m8 root" >&2
  exit 2
fi
run_id="$(date -u +%Y%m%dT%H%M%SZ)-$(node -e 'process.stdout.write(require("node:crypto").randomBytes(8).toString("hex"))')"
archive_dir="$root/archives/$run_id"
mkdir -p "$root/archives" "$root/private"
mkdir "$archive_dir" || { echo "G8 run archive collision; refusing overwrite" >&2; exit 2; }
archive_name="g8-$HARBOROPS_SOURCE_SHA-$run_id"
evidence="$root/evidence"
mkdir "$evidence"
mkdir -p "$evidence/trx" "$evidence/trivy"
printf '%s\n' "$run_id" > "$evidence/run-id.txt"
printf '%s\n' "$HARBOROPS_SOURCE_SHA" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
: > "$evidence/stages.tsv"
# G8 has its own complete Compose file with distinct ports and network.
export COMPOSE_FILE="$(cygpath -w "$(pwd)/compose.g8.verify.yaml")"
export COMPOSE_PROJECT_DIRECTORY="$(cygpath -w "$(pwd)")"
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5178"
owned_stack=0
owned_sdk=0
stage() {
  local label="$1" rc=0 start=$SECONDS
  shift
  echo "Starting G8 stage $label"
  if "$@" > "$evidence/$label.log" 2>&1; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\n' "$label" "$HARBOROPS_SOURCE_SHA" "$rc" "$((SECONDS-start))" >> "$evidence/stages.tsv"
  if [[ "$rc" != 0 ]]; then
    echo "Stage $label failed; see E:-backed $label.log" >&2
    tail -45 "$evidence/$label.log" >&2 || true
  fi
  return "$rc"
}
cleanup() {
  local rc=$? scan_rc=0 summary_rc=0 archive_rc=0
  trap - EXIT
  # Include the last logs, actual teardown outcome and timeline in the scan.
  if [[ "$owned_stack" == 1 ]]; then
    compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || rc=1
    compose down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  if [[ "$owned_sdk" == 1 ]]; then
    docker rm -f "$sdk_db" >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  local leftover vols pg
  leftover=$(docker ps -aq --filter "label=com.docker.compose.project=$project" || true)
  vols=$(docker volume ls -q --filter "label=com.docker.compose.project=$project" || true)
  pg=$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$" || true)
  [[ -z "$leftover" && -z "$vols" && -z "$pg" ]] || rc=1
  printf '%s\n' "containers=$leftover volumes=$vols database=$pg" > "$evidence/teardown.txt"
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"

  # The scan executes AFTER the final logs are collected. Private canaries stay
  # available until the archive itself has also passed a second inspection.
  stage evidence-safety node scripts/check-m8-evidence-safety.mjs "$HARBOROPS_VERIFICATION_ROOT" || scan_rc=1
  stage evidence-summary node scripts/summarize-g8-evidence.mjs "$evidence" || summary_rc=1
  if [[ "$scan_rc" != 0 || "$summary_rc" != 0 ]]; then rc=1; fi

  # Formal gate decisions are independent of technical execution status.
  if [[ -f "$evidence/g8-scenarios.json" ]]; then
    node scripts/record-g8-disposition.mjs "$evidence" "$rc" || rc=1
  else
    rc=1
  fi
  if [[ -f "$evidence/g8-technical-status.json" ]]; then
    technical_status=$(node -p 'require(process.argv[1]).status' "$evidence/g8-technical-status.json" || echo FAILED)
  else
    technical_status=FAILED
  fi
  if [[ "$technical_status" != "PASS" ]]; then rc=1; fi
  printf '%s\n' "$rc" > "$evidence/technical-stage-exit-code.txt"
  # The completed manifest and archive are exclusively created inside the
  # previously created, unique E:-backed run directory.
  (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
    > "$archive_dir/$archive_name-manifest.sha256" || archive_rc=1
  draft_archive="$root/private/$archive_name-evidence.tar.gz.pending"
  if [[ "$archive_rc" == 0 ]]; then
    tar -czf "$draft_archive" -C "$root" evidence || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 ]]; then
    node scripts/check-m8-archive-safety.mjs "$HARBOROPS_VERIFICATION_ROOT" "$(cygpath -w "$draft_archive")" "$(cygpath -w "$archive_dir/$archive_name-manifest.sha256")" \
      > "$archive_dir/archive-safety.log" 2>&1 || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 ]]; then
    [[ ! -e "$archive_dir/$archive_name-evidence.tar.gz" ]] || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 ]]; then
    mv -n "$draft_archive" "$archive_dir/$archive_name-evidence.tar.gz" || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 && ! -f "$archive_dir/$archive_name-evidence.tar.gz" ]]; then
    archive_rc=1
  fi
  if [[ "$archive_rc" != 0 ]]; then
    rm -f "$draft_archive"
    echo "G8 archive quarantined: archive creation or final sanitization failed" >&2
    rc=1
  fi
  # This report is outside the already sealed evidence tarball. It records
  # archive publication outcome without rewriting the original technical run.
  node scripts/record-g8-finalization.mjs "$evidence" "$archive_dir" "$rc" "$archive_rc" || rc=1
  rm -f "$root/private/source-bearer.txt" "$root/private/test-user-password.txt" "$root/private/g8-receipt-snapshot.json"
  if [[ "$rc" == 0 && "$technical_status" == "PASS" ]]; then
    echo "G8 technical evidence PASS for $run_id. D-006, independent review and G8 acceptance pending."
    exit 2
  fi
  echo "G8 technical verification FAILED for $run_id. Check stage evidence, source and teardown." >&2
  exit 1
}
trap cleanup EXIT

stage preflight bash -c 'git rev-parse HEAD; node --version; npm --version; docker version; docker compose version'
stage script-syntax bash -c 'set -euo pipefail; for script in scripts/*.mjs; do node --check "$script"; done; bash -n scripts/verify-m8-local.sh scripts/run-m8-browsers-local.sh scripts/smoke-compose.sh'
# Compose must render the isolated G8 network, never the retained default subnet.
stage network-contract node scripts/check-m8-compose-network.mjs
stage documentation bash -c 'node scripts/render-execution-graph.mjs --check && node scripts/check-planning-docs.mjs'
stage tracking-contract node --test scripts/check-m7-contract.test.mjs scripts/check-m8-contract.test.mjs
stage postgres-image docker build --build-arg "SOURCE_REVISION=$HARBOROPS_SOURCE_SHA" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
docker run -d --name "$sdk_db" --label harborops.g8=disposable --ulimit core=0 \
  --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 \
  --cap-drop ALL --security-opt no-new-privileges \
  -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
  -e POSTGRES_HOST_AUTH_METHOD=trust harborops-postgres:16.15-rootless \
  > "$evidence/sdk-postgres-container-id.txt"
owned_sdk=1
ready=0
for _ in $(seq 1 45); do
  if docker exec "$sdk_db" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 2
done
[[ "$ready" == 1 ]] || { echo "Disposable PostgreSQL not ready" >&2; exit 1; }
cat > "$root/run-dotnet-g8.sh" <<'INNER'
set -euo pipefail
cd /src
case "$1" in
  build)
    dotnet restore HarborOps.sln --verbosity minimal
    dotnet build HarborOps.sln -c Release --no-restore -warnaserror
    ;;
  tests)
    for kind in Unit Contract Integration; do
      case "$kind" in
        Unit) file=tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj ;;
        Contract) file=tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj ;;
        Integration) file=tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj ;;
      esac
      dotnet test "$file" -c Release --no-build --logger "trx;LogFileName=g8-$kind.trx" \
        --results-directory /cache/evidence/trx
    done
    dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj \
      -c Release --no-build --logger "trx;LogFileName=g8-Integration-repeat.trx" \
      --results-directory /cache/evidence/trx
    ;;
  nuget)
    dotnet list HarborOps.sln package --vulnerable --include-transitive \
      > /cache/evidence/nuget-vulnerable.log 2>&1
    if grep -Eiq "has the following vulnerable packages|NU1900|NU1301|Unable to load the service index|failed to retrieve|advisory.+unavailable" /cache/evidence/nuget-vulnerable.log; then
      echo "NuGet vulnerability findings or an unavailable advisory source" >&2; exit 1
    fi
    ;;
  *) exit 2 ;;
esac
INNER
sdk() {
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --network "container:$sdk_db" \
    -v "$repo_win:/src" -v "$(cygpath -w "$root"):/cache" -w /src \
    -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e NUGET_PACKAGES=/cache/cache/nuget/packages \
    -e NUGET_HTTP_CACHE_PATH=/cache/cache/nuget/http \
    -e DOTNET_CLI_HOME=/cache/cache/dotnet-home \
    -e HOME=/cache/cache/dotnet-home -e TMPDIR=/cache/cache/tmp \
    -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
    mcr.microsoft.com/dotnet/sdk:10.0 bash /cache/run-dotnet-g8.sh "$1"
}
# Run cheap build, advisory and frontend gates before repeated PostgreSQL,
# real browsers and the full controlled mutation campaign.
stage build sdk build
stage nuget-advisories sdk nuget
stage npm-advisories bash -c 'npm --prefix web audit --audit-level=high && npm --prefix tests/browser audit --audit-level=high'
stage web-build bash -c 'npm --prefix web ci --no-audit --no-fund && npm --prefix web test && npm --prefix web run build && ! grep -R -l "hop_v1_" web/dist'
stage dotnet-regressions sdk tests
if [[ -n "${HARBOROPS_G8_MUTATION_SUBSET:-}" ]]; then
  echo "Full G8 cannot use a reduced mutation subset" >&2
  exit 2
fi

# Only this project's newly created resources may be stopped or destroyed.
owned_stack=1
stage compose bash scripts/smoke-compose.sh --project "$project" --confirm-disposable
password_file="$root/private/test-user-password.txt"
node -e 'process.stdout.write(require("node:crypto").randomBytes(32).toString("hex"))' > "$password_file"
chmod 600 "$password_file"
export HARBOROPS_TEST_USER_PASSWORD="$(cat "$password_file")"
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
stage accounts accounts
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
stage organizations organizations
stage source-fixture node scripts/seed-m8-source-fixture.mjs

# A redirected operator terminal must never reveal a source secret.
cli_negative() {
  local org rc=0
  org=$(compose exec -T postgres psql -U harborops -d harborops -Atc \
    "SELECT id FROM organizations WHERE slug='north-terminal'")
  compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
    dotnet HarborOps.Api.dll --source-cli create "$org" refused_cli "Refused" \
    > "$evidence/source-cli-rejected.txt" 2>&1 || rc=$?
  [[ "$rc" == 1 ]] || return 1
  if grep -qi "Bearer (displayed once)" "$evidence/source-cli-rejected.txt"; then return 1; fi
  local denied=0
  compose exec -T api dotnet HarborOps.Api.dll --source-cli inspect "$org" g8_disposable \
    > "$evidence/source-cli-no-optin.txt" 2>&1 || denied=$?
  [[ "$denied" == 1 ]] || return 1
  compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
    dotnet HarborOps.Api.dll --source-cli inspect "$org" g8_disposable
}
stage source-cli-negative cli_negative
stage ingress-before node scripts/check-m8-ingress.mjs healthy
stage browser-fixtures bash -c \
  'node scripts/prepare-m3-browser-fixtures.mjs && node scripts/prepare-m4-browser-fixtures.mjs && node scripts/prepare-m5-browser-fixtures.mjs && node scripts/prepare-m6-browser-fixtures.mjs'
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
stage browsers bash scripts/run-m8-browsers-local.sh

recovery() {
  compose stop postgres
  local rc=0
  node scripts/check-m8-ingress.mjs outage || rc=$?
  compose start postgres
  local ready=0
  for _ in $(seq 1 45); do
    if curl --silent --fail --max-time 5 http://127.0.0.1:8088/health/ready >/dev/null; then
      ready=1; break
    fi
    sleep 2
  done
  [[ "$rc" == 0 && "$ready" == 1 ]] || return 1
  node scripts/check-m8-ingress.mjs recovered
}
stage source-outage-recovery recovery

scan_images() {
  # Every step must fail the stage. This function runs inside an if, where set -e is
  # inactive, so each command is checked explicitly: a failed scan must never be hidden
  # by a later successful scan.
  local trivy="aquasec/trivy:0.76.1"
  local cachemount="$(cygpath -w "$root/cache/trivy"):/root/.cache/trivy"
  # Acquire the vulnerability database first, with a bounded retry for transient DNS or
  # registry errors. Scans never proceed without a current database.
  local attempt db_ready=0
  for attempt in 1 2 3; do
    if MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$cachemount" \
         "$trivy" image --download-db-only >> "$evidence/trivy/db-download.log" 2>&1; then
      db_ready=1; break
    fi
    echo "Vulnerability database download attempt $attempt failed" >> "$evidence/trivy/db-download.log"
    sleep 10
  done
  [[ "$db_ready" == 1 ]] || return 1
  # Record scanner and advisory database identity so a stale or missing database is visible.
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$cachemount" \
    "$trivy" version > "$evidence/trivy/version.txt" 2>&1 || return 1
  for service in postgres api web; do
    local container image revision
    container=$(compose ps -q "$service") || return 1
    [[ -n "$container" ]] || return 1
    image=$(docker inspect --format '{{.Image}}' "$container") || return 1
    revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image") || return 1
    [[ "$revision" == "$HARBOROPS_SOURCE_SHA" ]] || return 1
    printf '%s\t%s\t%s\n' "$service" "$image" "$revision" >> "$evidence/image-ids.tsv" || return 1
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 \
      -v /var/run/docker.sock:/var/run/docker.sock \
      -v "$cachemount" \
      -v "$(cygpath -w "$evidence/trivy"):/reports" \
      "$trivy" image --image-src docker --scanners vuln \
      --severity HIGH,CRITICAL --exit-code 1 --format json \
      --output "/reports/$service.json" "$image" || return 1
    [[ -s "$evidence/trivy/$service.json" ]] || return 1
  done
}
mkdir -p "$root/cache/trivy"
stage image-scans scan_images
# Run all 11 mutations after lower-cost mandatory checks have passed.
# No test or mutation is omitted to improve elapsed time.
stage security-mutations node scripts/run-g8-mutations.mjs "$HARBOROPS_VERIFICATION_ROOT"
# The EXIT trap performs final teardown, post-log evidence scanning, immutable
# packaging and technical-vs-governance disposition. Do not run these early.
echo "All scheduled G8 technical stages completed; finalization runs on exit."
