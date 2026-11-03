#!/usr/bin/env bash
# Disposable source-locked G7 technical verification, local E: storage only.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Windows Git Bash required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
root="$(cygpath -u "$root_input")"
case "$root" in /e/*/m7) ;; *) echo "Dedicated E: path ending in /m7 required" >&2; exit 2 ;; esac
export HARBOROPS_VERIFICATION_ROOT="$(cygpath -w "$root")"
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
[[ -n "$HARBOROPS_DB_PASSWORD" ]] || export HARBOROPS_DB_PASSWORD="disposable-g7-only"
project="ho-verify-g7"
sdk_db="ho-g7-pg"
compose() { docker compose -p "$project" "$@"; }
if [[ -n "$(docker ps -aq --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker volume ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$")" ]]; then
  echo "Existing G7 resources detected, refusing takeover" >&2; exit 2
fi
if [[ -e "$root/private/source-bearer.txt" || -e "$root/private/test-user-password.txt" ]]; then
  echo "Existing private fixture detected, refusing overwrite" >&2; exit 2
fi
mkdir -p "$root/archives"
if [[ -d "$root/evidence" ]]; then
  old=$(cat "$root/evidence/source-sha.txt" 2>/dev/null || echo unknown)
  mv "$root/evidence" "$root/archives/g7-prior-$(date -u +%Y%m%dT%H%M%SZ)-$old-$$"
fi
evidence="$root/evidence"
mkdir -p "$evidence/trx" "$evidence/trivy" "$root/private"
printf '%s\n' "$HARBOROPS_SOURCE_SHA" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
: > "$evidence/stages.tsv"
override="$root/compose.core0.yaml"
cat > "$override" <<'YAML'
services:
  postgres:
    ulimits: { core: 0 }
  migrator:
    ulimits: { core: 0 }
  api:
    ulimits: { core: 0 }
    environment:
      Identity__TrustedLocalHttpPeers: "172.30.253.1,172.30.253.10"
      ReverseProxy__KnownProxies__0: "172.30.253.10"
  web:
    ulimits: { core: 0 }
    networks:
      internal:
        ipv4_address: 172.30.253.10
networks:
  internal:
    ipam:
      config:
        - subnet: 172.30.253.0/24
YAML
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="$(cygpath -w "$(pwd)/compose.yaml");$(cygpath -w "$override")"
owned_stack=0
owned_sdk=0
stage() {
  local label="$1" rc=0 start=$SECONDS
  shift
  echo "Starting G7 stage $label"
  if "$@" > "$evidence/$label.log" 2>&1; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\n' "$label" "$HARBOROPS_SOURCE_SHA" "$rc" "$((SECONDS-start))" >> "$evidence/stages.tsv"
  if [[ "$rc" != 0 ]]; then
    echo "Stage $label failed; see E:-backed $label.log" >&2
    tail -45 "$evidence/$label.log" >&2 || true
  fi
  return "$rc"
}
cleanup() {
  local rc=$?
  trap - EXIT
  if [[ "$owned_stack" == 1 ]]; then
    compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || true
    compose down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  if [[ "$owned_sdk" == 1 ]]; then
    docker rm -f "$sdk_db" >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  if [[ -f "$root/private/source-bearer.txt" ]]; then
    if grep -R -F -q -- "$(cat "$root/private/source-bearer.txt")" "$evidence"; then
      echo "Source credential detected in evidence" >&2; rc=1
    fi
    rm -f "$root/private/source-bearer.txt"
  fi
  if [[ -f "$root/private/test-user-password.txt" ]] &&
     grep -R -F -q -- "$(cat "$root/private/test-user-password.txt")" "$evidence"; then
    echo "Fixture password detected in evidence" >&2; rc=1
  fi
  rm -f "$root/private/test-user-password.txt"
  local leftover vols pg
  leftover=$(docker ps -aq --filter "label=com.docker.compose.project=$project" || true)
  vols=$(docker volume ls -q --filter "label=com.docker.compose.project=$project" || true)
  pg=$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$" || true)
  [[ -z "$leftover" && -z "$vols" && -z "$pg" ]] || rc=1
  printf '%s\n' "containers=$leftover volumes=$vols database=$pg" > "$evidence/teardown.txt"
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"
  printf '%s\n' "$rc" > "$evidence/exit-code.txt"
  (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
    > "$root/archives/g7-$HARBOROPS_SOURCE_SHA-manifest.sha256" || rc=1
  tar -czf "$root/archives/g7-$HARBOROPS_SOURCE_SHA-evidence.tar.gz" -C "$root" evidence || rc=1
  exit "$rc"
}
trap cleanup EXIT

stage preflight bash -c 'git rev-parse HEAD; node --version; npm --version; docker version; docker compose version'
# Compose must render the isolated G7 network, never the retained default subnet.
stage network-contract node scripts/check-m7-compose-network.mjs
stage documentation bash -c 'node scripts/render-execution-graph.mjs --check && node scripts/check-planning-docs.mjs'
stage tracking-contract node --test scripts/check-m7-contract.test.mjs
stage postgres-image docker build --build-arg "SOURCE_REVISION=$HARBOROPS_SOURCE_SHA" \
  -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
docker run -d --name "$sdk_db" --label harborops.g7=disposable --ulimit core=0 \
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
cat > "$root/run-dotnet-g7.sh" <<'INNER'
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
      dotnet test "$file" -c Release --no-build --logger "trx;LogFileName=g7-$kind.trx" \
        --results-directory /cache/evidence/trx
    done
    dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj \
      -c Release --no-build --logger "trx;LogFileName=g7-Integration-repeat.trx" \
      --results-directory /cache/evidence/trx
    ;;
  nuget)
    dotnet list HarborOps.sln package --vulnerable --include-transitive \
      > /cache/evidence/nuget-vulnerable.log
    if grep -q "has the following vulnerable packages" /cache/evidence/nuget-vulnerable.log; then
      echo "NuGet vulnerabilities found" >&2; exit 1
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
    mcr.microsoft.com/dotnet/sdk:10.0 bash /cache/run-dotnet-g7.sh "$1"
}
stage build sdk build
stage dotnet-regressions sdk tests
stage nuget-advisories sdk nuget
stage web-build bash -c 'npm --prefix web ci --no-audit --no-fund && npm --prefix web test && npm --prefix web run build'
stage npm-advisories bash -c 'npm --prefix web audit --audit-level=high && npm --prefix tests/browser audit --audit-level=high'

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
    "m2-operator@example.test|Synthetic Administrator" \
    "m2-second-admin@example.test|Second Administrator" \
    "m2-outage@example.test|Outage Member" \
    "m2-browser-chromium@example.test|Browser Chromium Member" \
    "m2-browser-firefox@example.test|Browser Firefox Member" \
    "m2-browser-webkit@example.test|Browser Webkit Member" \
    "m3-admin-a@example.test|M3 North Administrator" \
    "m3-admin-b@example.test|M3 East Administrator" \
    "m3-manager@example.test|M3 Manager" \
    "m3-analyst@example.test|M3 Analyst" \
    "m4-analyst-east@example.test|East Analyst" \
    "m4-support@example.test|Support" \
    "m4-auditor@example.test|Auditor"; do
    IFS='|' read -r email display <<< "$entry"
    provision "$email" "$display"
  done
}
stage accounts accounts
bootstrap() {
  compose run --rm -T -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api \
    --organization-cli bootstrap "$1" "$2" "$3"
}
organizations() {
  bootstrap north-terminal "North Terminal" m2-operator@example.test
  bootstrap east-terminal "East Terminal" m2-second-admin@example.test
  bootstrap m3-north "M3 North Terminal" m3-admin-a@example.test
  bootstrap m3-east "M3 East Terminal" m3-admin-b@example.test
  bootstrap m3-pagination "M3 Pagination" m3-admin-a@example.test
  for engine in chromium firefox webkit; do
    bootstrap "m3-revocation-$engine" "M3 Revocation $engine" m3-admin-a@example.test
  done
}
stage organizations organizations
stage source-fixture node scripts/seed-m7-source-fixture.mjs

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
  compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
    dotnet HarborOps.Api.dll --source-cli inspect "$org" g7_disposable
}
stage source-cli-negative cli_negative
if [[ "$(printenv HARBOROPS_G7_INTERACTIVE_CLI || true)" == "1" ]]; then
  # Deliberately not captured by stage(): only a protected terminal may display issued keys.
  bash scripts/check-m7-source-cli-interactive.sh
  printf 'G7-S06\tOPERATOR_RUN\tProtected lifecycle checked in terminal\n' > "$evidence/source-cli-operator.tsv"
fi
stage ingress-before node scripts/check-m7-ingress.mjs healthy
stage browser-fixtures bash -c \
  'node scripts/prepare-m3-browser-fixtures.mjs && node scripts/prepare-m4-browser-fixtures.mjs && node scripts/prepare-m5-browser-fixtures.mjs && node scripts/prepare-m6-browser-fixtures.mjs'
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
stage browsers bash scripts/run-m7-browsers-local.sh

recovery() {
  compose stop postgres
  local rc=0
  node scripts/check-m7-ingress.mjs outage || rc=$?
  compose start postgres
  local ready=0
  for _ in $(seq 1 45); do
    if curl --silent --fail --max-time 5 http://127.0.0.1:8080/health/ready >/dev/null; then
      ready=1; break
    fi
    sleep 2
  done
  [[ "$rc" == 0 && "$ready" == 1 ]] || return 1
  node scripts/check-m7-ingress.mjs recovered
}
stage source-outage-recovery recovery

scan_images() {
  for service in postgres api web; do
    local container image revision
    container=$(compose ps -q "$service")
    [[ -n "$container" ]] || return 1
    image=$(docker inspect --format '{{.Image}}' "$container")
    revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")
    [[ "$revision" == "$HARBOROPS_SOURCE_SHA" ]] || return 1
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 \
      -v /var/run/docker.sock:/var/run/docker.sock \
      -v "$(cygpath -w "$root/cache/trivy"):/root/.cache/trivy" \
      -v "$(cygpath -w "$evidence/trivy"):/reports" \
      aquasec/trivy:0.76.1 image --image-src docker --scanners vuln \
      --severity HIGH,CRITICAL --exit-code 1 --format json \
      --output "/reports/$service.json" "$image"
  done
}
mkdir -p "$root/cache/trivy"
stage image-scans scan_images

# G7 acceptance requires normative D-002 references and independent raw-evidence review.
# The source credential is never packaged; protected interactive CLI issuance is a separate check.
printf 'G7-S01\tBLOCKED\tD-002 acceptance and upstream pin pending\n' > "$evidence/open-gates.tsv"
if [[ ! -s "$evidence/source-cli-operator.tsv" ]]; then
  printf 'G7-S06\tMANUAL_PENDING\tProtected terminal lifecycle not executed\n' >> "$evidence/open-gates.tsv"
fi
printf 'G7-S24\tREVIEW_PENDING\tIndependent mutation and security review pending\n' >> "$evidence/open-gates.tsv"
printf 'G7-S25\tREVIEW_PENDING\tIndependent raw-evidence review pending\n' >> "$evidence/open-gates.tsv"
echo "Technical stages completed. G7 remains blocked on the open-gate register." >&2
exit 2
