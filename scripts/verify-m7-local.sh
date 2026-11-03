#!/usr/bin/env bash
# Run M7 source-locked local checks using disposable resources and E: storage.
set -euo pipefail
cd "$(dirname "$0")/.."
root="${HARBOROPS_VERIFICATION_ROOT:-}"
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must point to E: .../m7" >&2; exit 2 ;;
esac
case "$root" in
  *[/\\]m7) ;;
  *) echo "Use a dedicated E: verification folder ending in m7" >&2; exit 2 ;;
esac
command -v cygpath >/dev/null || { echo "Windows Git Bash is required" >&2; exit 2; }
case "$(pwd -W)" in
  [Ee]:[/\\]*) ;;
  *) echo "The repository must be on E:" >&2; exit 2 ;;
esac
[[ -z "$(git status --porcelain)" ]] || { echo "Clean worktree required" >&2; exit 2; }
node scripts/check-m3-docker-storage.mjs
sha="$(git rev-parse HEAD)"
export HARBOROPS_SOURCE_SHA="$sha"
for var in TEMP TMP TMPDIR NPM_CONFIG_CACHE NUGET_PACKAGES DOTNET_CLI_HOME PLAYWRIGHT_BROWSERS_PATH PYTHONPYCACHEPREFIX; do
  export "$var=$root/cache/$var"
  mkdir -p "${!var}"
done
export HOME="$root/cache/HOME"
mkdir -p "$HOME"
evidence="$root/evidence-$sha"
mkdir -p "$evidence"
echo "$sha" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
: > "$evidence/stages.tsv"
export HARBOROPS_DB_PASSWORD="${HARBOROPS_DB_PASSWORD:-disposable-g7-db-only}"
project="ho-verify-g7"
postgres="ho-g7-postgres"
owned=0
stage() {
  label="$1"; shift
  echo "START $label"
  set +e
  "$@" > "$evidence/$label.log" 2>&1
  code=$?
  set -e
  printf '%s\t%s\t%s\n' "$label" "$sha" "$code" >> "$evidence/stages.tsv"
  cat "$evidence/$label.log"
  return "$code"
}
cleanup() {
  rc=$?
  trap - EXIT
  if [[ "$owned" == 1 ]]; then
    docker rm -f "$postgres" >> "$evidence/cleanup.log" 2>&1 || true
    docker compose -p "$project" down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || true
  fi
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"
  printf '%s\n' "$rc" > "$evidence/exit-code.txt"
  find "$evidence" -type f -print0 | sort -z | xargs -0 sha256sum > "$root/manifest-$sha.sha256"
  exit "$rc"
}
trap cleanup EXIT
if [[ -n "$(docker ps -aq --filter name="^/$postgres$")" ||
      -n "$(docker ps -aq --filter label="com.docker.compose.project=$project")" ||
      -n "$(docker volume ls -q --filter label="com.docker.compose.project=$project")" ]]; then
  echo "Existing G7 resources detected; refusing takeover" >&2; exit 2
fi
stage preflight bash -c 'git rev-parse HEAD; dotnet --info; docker info; node --version; npm --version'
stage docs node scripts/check-planning-docs.mjs
stage graph node scripts/render-execution-graph.mjs --check
stage restore dotnet restore HarborOps.sln --verbosity quiet
stage build dotnet build HarborOps.sln -c Release --no-restore
stage unit dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=g7-unit.trx" --results-directory "$evidence"
stage contracts dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build --logger "trx;LogFileName=g7-contract.trx" --results-directory "$evidence"
stage frontend npm --prefix web ci
stage web-build npm --prefix web run build
stage web-unit npm --prefix web test
stage pg-image docker build --build-arg "SOURCE_REVISION=$sha" -f docker/postgres/Dockerfile -t harborops-postgres:16.15-rootless .
docker run -d --name "$postgres" --label harborops.g7=disposable \
  --ulimit core=0 --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 \
  --cap-drop ALL --security-opt no-new-privileges \
  -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
  -e POSTGRES_HOST_AUTH_METHOD=trust -p 127.0.0.1:55417:5432 \
  harborops-postgres:16.15-rootless > "$evidence/postgres-id.txt"
owned=1
ready=0
for _ in $(seq 1 45); do
  if docker exec "$postgres" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 2
done
[[ "$ready" == 1 ]] || { echo "G7 disposable database not ready" >&2; exit 1; }
export HARBOROPS_TEST_DB="Host=127.0.0.1;Port=55417;Database=harborops_integration;Username=harborops;Password=disposable;Trust Server Certificate=true"
stage integration dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=g7-integration.trx" --results-directory "$evidence"
stage integration-repeat dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=g7-integration-repeat.trx" --results-directory "$evidence"
stage nuget-audit dotnet package list HarborOps.sln --vulnerable --include-transitive
stage npm-audit npm --prefix web audit --audit-level=high
if command -v trivy >/dev/null; then
  stage pg-scan trivy image --exit-code 1 --severity HIGH,CRITICAL harborops-postgres:16.15-rootless
else
  echo "Required image scanner unavailable" >&2; exit 2
fi
# Browser, proxy, image-composition and outage checks require the dedicated full-stack G7
# matrix. No acceptance is inferred from this targeted runner.
echo -e "browser-proxy-recovery\t$sha\tNOT_EXECUTED" >> "$evidence/stages.tsv"
echo "G7 targeted checks completed. Full G7 verification and acceptance remain pending."
