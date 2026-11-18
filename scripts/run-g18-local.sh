#!/usr/bin/env bash
# G18 local verification stage dispatcher. Everything runs on this machine against owned disposable resources.
# No hosted workflow is dispatched or configured. Usage:
#   HARBOROPS_G18_ROOT=<E: directory> scripts/run-g18-local.sh <attempt-id> <stage>
# Stages: preflight source-checks build unit contract web m18-integration regress-neighbours inventory-after
#         reconcile manifest cleanup
# The server backed stages (three-engine browser run, fault journey, mutation campaign) are supervised
# separately because each owns long-lived processes: see docs/M18_OPERATIONS_RUNBOOK.md section 8.
set -uo pipefail
root="${HARBOROPS_G18_ROOT:?set HARBOROPS_G18_ROOT to the E: verification directory}"
case "$root" in [Ee]:[/\\]*|/e/*) ;; *) echo "E:-backed root required" >&2; exit 2 ;; esac
attempt="${1:?attempt id}"; stage="${2:?stage}"
repo="$(git rev-parse --show-toplevel)"
cd "$repo"
sha="$(git rev-parse HEAD)"
tree="$(git rev-parse 'HEAD^{tree}')"
ev="$(cygpath -u "$root")/evidence/$sha/$attempt"
mkdir -p "$ev/trx"
evw="$(cygpath -w "$ev")"
log() { printf '%s\n' "$*" | tee -a "$ev/runner.log"; }
record() { # name exit
  printf '{"name":"%s","status":"%s","exit_code":%s,"ended_at_utc":"%s"}\n' "$1" "$([ "$2" -eq 0 ] && echo PASS || echo FAIL)" "$2" "$(date -u +%FT%TZ)" >> "$ev/stages.jsonl"
}
run() { # name command...
  local name="$1"; shift
  log "STAGE $name START $(date -u +%FT%TZ)"
  "$@" > "$ev/$name.log" 2>&1
  local rc=$?
  log "STAGE $name EXIT $rc"
  record "$name" "$rc"
  return $rc
}
db() { # start an owned disposable database: name port
  docker rm -f "$1" >/dev/null 2>&1
  docker run -d --name "$1" --label harborops.g18=disposable --label harborops.g18.attempt="$attempt" --ulimit core=0 \
    --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 --cap-drop ALL --security-opt no-new-privileges \
    -p "127.0.0.1:$2:5432" -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops -e POSTGRES_HOST_AUTH_METHOD=trust \
    harborops-g13g15-postgres:16.15-rootless -c fsync=off -c max_connections=300 >/dev/null || return 1
  local i; for i in $(seq 1 60); do docker exec "$1" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1 && return 0; sleep 1; done
  return 1
}
case "$stage" in
  preflight)
    [[ -z "$(git status --porcelain)" ]] || { echo "dirty worktree: refusing to verify" >&2; exit 3; }
    { echo "sha=$sha"; echo "tree=$tree"; echo "branch=$(git branch --show-current)"; echo "started_utc=$(date -u +%FT%TZ)"
      echo "dotnet=$(dotnet --version)"; echo "node=$(node -v)"; echo "npm=$(npm -v)"; echo "docker=$(docker --version)"
      sha256sum global.json web/package-lock.json tests/browser/package-lock.json
      echo "--- owned and foreign containers before"; docker ps -a --format '{{.Names}} {{.Status}} {{.Label "com.docker.compose.project"}}'
      echo "--- disk"; df -h /c /e; } > "$ev/preflight.txt" 2>&1
    node scripts/g18-retained-inventory.mjs --out "$evw\\retained-inventory-before.json" >> "$ev/preflight.txt"
    record preflight 0 ;;
  source-checks)
    # the json is written even on failure; the stage mirrors the exit code
    run source-checks node scripts/check-m18-source.mjs --json "$evw\\source-checks.json" ;;
  build)
    run build dotnet build HarborOps.sln -c Release -warnaserror ;;
  unit)
    run unit-tests dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=unit.trx" --results-directory "$evw\\trx" ;;
  contract)
    run contract-tests dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build --logger "trx;LogFileName=contract.trx" --results-directory "$evw\\trx" ;;
  web)
    run web-install npm --prefix web ci --no-audit --no-fund
    run web-typecheck bash -c 'cd web && npx tsc -b'
    ( cd web && node --test --test-reporter=spec tests/*.test.mjs > "$ev/web-test.log" 2>&1; echo $? > "$ev/web-test.exit" )
    record web-tests "$(cat "$ev/web-test.exit")"
    run web-build npm --prefix web run build ;;
  m18-integration)
    port="${HARBOROPS_G18_PORT:-55441}"
    db "ho-verify-g18-$attempt-m18" "$port" || { record m18-database 1; exit 1; }
    export HARBOROPS_TEST_DB="Host=127.0.0.1;Port=$port;Database=harborops_integration;Username=harborops" HARBOROPS_G12_EXCLUSIVE_POSTGRES=1
    run m18-integration dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build \
      --filter "FullyQualifiedName~M18&FullyQualifiedName!~M18BrowserSeed" --logger "trx;LogFileName=m18-integration.trx" --results-directory "$evw\\trx"
    docker logs "ho-verify-g18-$attempt-m18" > "$ev/m18-integration-postgres.log" 2>&1 ;;
  regress-neighbours)
    port="${HARBOROPS_G18_PORT:-55442}"
    db "ho-verify-g18-$attempt-reg" "$port" || { record regress-database 1; exit 1; }
    export HARBOROPS_TEST_DB="Host=127.0.0.1;Port=$port;Database=harborops_integration;Username=harborops" HARBOROPS_G12_EXCLUSIVE_POSTGRES=1 HARBOROPS_G13_EXCLUSIVE_POSTGRES=1
    run regress-neighbours dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build \
      --filter "FullyQualifiedName!~M18" --logger "trx;LogFileName=regress-neighbours.trx" --results-directory "$evw\\trx" ;;
  inventory-after)
    node scripts/g18-retained-inventory.mjs --out "$evw\\retained-inventory-after.json" > "$ev/inventory-after.log" 2>&1; record inventory-after $? ;;
  reconcile)
    node scripts/reconcile-g18-assertions.mjs --evidence "$evw" --sha "$sha" --out "$evw\\g18-assertion-ledger.json" > "$ev/reconcile.log" 2>&1
    echo "reconcile exit $?" >> "$ev/runner.log" ;;
  manifest)
    ( cd "$ev" && find . -type f ! -name sha256-manifest.txt -print0 | sort -z | xargs -0 sha256sum > sha256-manifest.txt )
    printf '%s  %s\n' "$sha" "$tree" > "$ev/source-identity.txt" ;;
  cleanup)
    for name in $(docker ps -a --filter "label=harborops.g18.attempt=$attempt" --filter label=harborops.g18=disposable --format '{{.Names}}'); do
      echo "removing owned container $name" >> "$ev/cleanup.log"; docker rm -f "$name" >> "$ev/cleanup.log" 2>&1
    done ;;
  *) echo "unknown stage $stage" >&2; exit 2 ;;
esac
