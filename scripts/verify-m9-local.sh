#!/usr/bin/env bash
# Source-locked local M9 development verification, not a G9 acceptance runner.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Windows Git Bash is required" >&2; exit 2; }
root_input="${HARBOROPS_VERIFICATION_ROOT:-}"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "Use an E:-backed verification root" >&2; exit 2 ;; esac
case "$(cygpath -m "$root_input")" in [Ee]:/*/m9) ;; *) echo "Use a distinct path ending in /m9" >&2; exit 2 ;; esac
case "$(pwd -W)" in [Ee]:[/\\]*) ;; *) echo "Repository must reside on E:" >&2; exit 2 ;; esac
[[ -z "$(git status --porcelain)" ]] || { echo "Source checkout must be clean" >&2; exit 2; }
source_sha="$(git rev-parse HEAD)"
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] || exit 2
root_win="$(cygpath -w "$root_input")"
root="$(cygpath -u "$root_win")"
repo_win="$(pwd -W)"
[[ ! -e "$root/evidence" ]] || { echo "Evidence directory already exists; choose a new run root" >&2; exit 2; }
mkdir -p "$root/evidence/trx" "$root/cache/nuget" "$root/cache/dotnet" "$root/cache/tmp" "$root/cache/npm"
evidence="$root/evidence"
printf '%s\n' "$source_sha" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
: > "$evidence/stages.tsv"
export TEMP="$(cygpath -w "$root/cache/tmp")"
export TMP="$TEMP" NPM_CONFIG_CACHE="$(cygpath -w "$root/cache/npm")"
export NUGET_PACKAGES="$(cygpath -w "$root/cache/nuget")"
export DOTNET_CLI_HOME="$(cygpath -w "$root/cache/dotnet")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export DOCKER_HOST="npipe:////./pipe/dockerDesktopLinuxEngine"
container="ho-g9-dev-pg"
image_tag="harborops-g9-postgres:${source_sha:0:12}"
owned=0
stage() {
    local label="$1" rc=0
    shift
    echo "M9 development stage: $label"
    if "$@" > "$evidence/$label.log" 2>&1; then rc=0; else rc=$?; fi
    printf '%s\t%s\t%s\n' "$label" "$source_sha" "$rc" >> "$evidence/stages.tsv"
    if [[ "$rc" != 0 ]]; then
        tail -40 "$evidence/$label.log" >&2 || true
    fi
    return "$rc"
}
cleanup() {
    local result=$?
    trap - EXIT
    if [[ "$owned" == 1 ]]; then
        docker logs "$container" > "$evidence/postgres-container.log" 2>&1 || result=1
        docker rm -f "$container" >> "$evidence/cleanup.log" 2>&1 || result=1
    fi
    if [[ -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$container$" 2>/dev/null || true)" ]]; then
        echo "Owned database container remains after teardown" >> "$evidence/cleanup.log"
        result=1
    fi
    date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"
    (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
      > "$root/evidence-manifest.sha256" || result=1
    printf '%s\n' "$result" > "$root/technical-exit-code.txt"
    if [[ "$result" == 0 ]]; then
        echo "M9 development checks completed. Full G9 scenarios, fault injection, scans,"
        echo "independent evidence review and acceptance are still pending."
        exit 2
    fi
    echo "M9 development checks failed. Inspect E:-backed evidence; do not claim G9 PASS." >&2
    exit 1
}
trap cleanup EXIT
stage preflight bash -c 'git rev-parse HEAD; node --version; docker version; docker compose version'
stage storage node scripts/check-m3-docker-storage.mjs
stage scripts bash -c 'bash -n scripts/verify-m9-local.sh && node --check scripts/check-m9-contract.test.mjs'
stage graph bash -c 'node scripts/render-execution-graph.mjs --check && node scripts/check-planning-docs.mjs'
stage contracts node --test scripts/check-m7-contract.test.mjs scripts/check-m8-contract.test.mjs scripts/check-m9-contract.test.mjs
if [[ -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$container$")" ]]; then
    echo "M9 disposable database container already exists; refusing takeover" >&2
    exit 2
fi
stage build-postgres docker build -t "$image_tag" -f docker/postgres/Dockerfile .
docker run -d --name "$container" --label harborops.m9=disposable --ulimit core=0 \
    --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 \
    --cap-drop ALL --security-opt no-new-privileges \
    -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
    -e POSTGRES_HOST_AUTH_METHOD=trust "$image_tag" > "$evidence/database-container-id.txt"
owned=1
ready=0
for _ in $(seq 1 45); do
    if docker exec "$container" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 2
done
[[ "$ready" == 1 ]] || { echo "Disposable PostgreSQL failed readiness" >&2; exit 1; }
sdk() {
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --network "container:$container" \
      -v "$repo_win:/src" -v "$root_win:/evidence" -w /src \
      -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
      -e NUGET_PACKAGES=/evidence/cache/nuget \
      -e DOTNET_CLI_HOME=/evidence/cache/dotnet \
      -e TMPDIR=/evidence/cache/tmp -e HOME=/evidence/cache/dotnet \
      -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
      mcr.microsoft.com/dotnet/sdk:10.0 bash -euc "$1"
}
stage dotnet-build sdk 'dotnet restore HarborOps.sln && dotnet build HarborOps.sln -c Release --no-restore -warnaserror && dotnet build src/HarborOps.Worker/HarborOps.Worker.csproj -c Release -warnaserror'
stage unit-tests sdk 'dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --logger "trx;LogFileName=m9-unit.trx" --results-directory /evidence/evidence/trx'
stage contract-tests sdk 'dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --logger "trx;LogFileName=m9-contract.trx" --results-directory /evidence/evidence/trx'
stage integration-tests sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --logger "trx;LogFileName=m9-integration.trx" --results-directory /evidence/evidence/trx'
stage integration-repeat sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --logger "trx;LogFileName=m9-integration-repeat.trx" --results-directory /evidence/evidence/trx'
stage frontend bash -c 'npm --prefix web ci --no-audit --no-fund && npm --prefix web test && npm --prefix web run build'
echo "Development stages completed; local full G9 campaign remains separate."
