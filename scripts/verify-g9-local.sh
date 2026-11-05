#!/usr/bin/env bash
# Source-locked local G9 technical verification campaign. E: storage only, no GitHub Actions.
# Exit 0  : every technical stage passed and the register has no FAIL. This is NOT G9 acceptance:
#           G9-S01 stays BLOCKED (contract acceptance pending) and G9-S25 stays NOT EXECUTED.
# Exit 1  : a stage, teardown, safety scan or archive check failed.
# Exit 2  : a precondition was refused before any work started.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Windows Git Bash required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
case "$(cygpath -m "$root_input")" in [Ee]:/*/m9) ;; *) echo "Dedicated E: path ending in /m9 required" >&2; exit 2 ;; esac
root_win="$(cygpath -w "$root_input")"
root="$(cygpath -u "$root_win")"
export HARBOROPS_VERIFICATION_ROOT="$root_win"
case "$(pwd -W)" in [Ee]:[/\\]*) ;; *) echo "Checkout must be on E:" >&2; exit 2 ;; esac
[[ -z "$(git status --porcelain)" ]] || { echo "Clean checkout required" >&2; exit 2; }
repo_win="$(pwd -W)"
mkdir -p "$root"
for var in TEMP TMP NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH DOTNET_CLI_HOME DOCKER_CONFIG XDG_CACHE_HOME; do
  folder="$root/cache/$var"
  mkdir -p "$folder"
  export "$var=$(cygpath -w "$folder")"
done
# A fresh DOCKER_CONFIG has no plugin path, so `docker compose` would be unknown. Write one that
# points at the installed Docker Desktop plugins, and refuse to start if they cannot be found.
plugin_dir="$(node -e '
const fs = require("fs"), path = require("path");
const candidates = [
  path.join(process.env.LOCALAPPDATA || "", "Programs", "DockerDesktop", "resources", "cli-plugins"),
  path.join(process.env.ProgramFiles || "C:\\Program Files", "Docker", "Docker", "resources", "cli-plugins")];
const found = candidates.find(item => fs.existsSync(item));
if (!found) process.exit(1);
fs.writeFileSync(path.join(process.env.DOCKER_CONFIG, "config.json"), JSON.stringify({ cliPluginsExtraDirs: [found] }));
console.log(found);
')" || { echo "Docker Desktop CLI plugin directory not found" >&2; exit 2; }
export TMPDIR="$root/cache/TMPDIR" HOME="$root/cache/HOME"
mkdir -p "$TMPDIR" "$HOME" "$root/cache/nuget" "$root/cache/dotnet" "$root/cache/tmp" "$root/cache/trivy"
export NUGET_PACKAGES="$(cygpath -w "$root/cache/nuget")"
export USERPROFILE="$(cygpath -w "$HOME")"
export DOCKER_HOST="npipe:////./pipe/dockerDesktopLinuxEngine"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
node scripts/check-m3-docker-storage.mjs
export HARBOROPS_SOURCE_SHA="$(git rev-parse HEAD)"
[[ "$HARBOROPS_SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]] || exit 2
# A per-attempt project name keeps this run's label-based guards and teardown away from any other
# session's containers. The G9-specific database image tag never reuses the tag other streams share.
project="${HARBOROPS_G9_PROJECT:-ho-verify-g9}"
[[ "$project" =~ ^ho-verify-g9(-[a-z0-9]{1,12})?$ ]] || { echo "Invalid G9 Compose project name" >&2; exit 2; }
export HARBOROPS_G9_PROJECT="$project"
sdk_db="ho-g9-sdk-pg"
db_image="harborops-g9-postgres:${HARBOROPS_SOURCE_SHA:0:12}"
export HARBOROPS_G9_DB_IMAGE="$db_image"
compose() { docker compose -f compose.g9.verify.yaml -p "$project" --profile worker "$@"; }
if [[ -n "$(docker ps -aq --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker volume ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker network ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$")" ||
      -n "$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/ho-g9-mutant-pg$")" ]]; then
  echo "Existing G9 resources detected, refusing takeover" >&2; exit 2
fi
if [[ -e "$root/evidence" || -e "$root/private" ]]; then
  echo "G9 evidence already exists; select a new E:-backed /m9 root" >&2; exit 2
fi
run_id="$(date -u +%Y%m%dT%H%M%SZ)-$(node -e 'process.stdout.write(require("node:crypto").randomBytes(8).toString("hex"))')"
archive_dir="$root/archives/$run_id"
mkdir -p "$root/archives" "$root/private"
mkdir "$archive_dir" || { echo "G9 run archive collision; refusing overwrite" >&2; exit 2; }
archive_name="g9-$HARBOROPS_SOURCE_SHA-$run_id"
evidence="$root/evidence"
mkdir "$evidence"
mkdir -p "$evidence/trx" "$evidence/trivy"
printf '%s\n' "$run_id" > "$evidence/run-id.txt"
printf '%s\n' "$HARBOROPS_SOURCE_SHA" > "$evidence/source-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
git rev-parse --abbrev-ref HEAD > "$evidence/branch.txt"
git status --porcelain > "$evidence/worktree-state.txt"
: > "$evidence/stages.tsv"
# Disposable secrets for the isolated stack only; canaries for the evidence safety scan.
node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))' > "$root/private/db.pw"
node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))' > "$root/private/worker.pw"
export HARBOROPS_DB_PASSWORD="$(cat "$root/private/db.pw")"
export HARBOROPS_WORKER_DB_PASSWORD="$(cat "$root/private/worker.pw")"
free_c() { node -e 'const s=require("fs").statfsSync("C:/");console.log(Math.round(s.bavail*s.bsize/1048576)+" MiB free on C:")'; }
free_c > "$evidence/c-drive-before.txt"
owned_stack=0
owned_sdk=0
stage() {
  local label="$1" rc=0 start=$SECONDS
  shift
  echo "Starting G9 stage $label"
  if "$@" > "$evidence/$label.log" 2>&1; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\n' "$label" "$HARBOROPS_SOURCE_SHA" "$rc" "$((SECONDS-start))" >> "$evidence/stages.tsv"
  if [[ "$rc" != 0 ]]; then
    echo "Stage $label failed; see E:-backed $label.log" >&2
    tail -45 "$evidence/$label.log" >&2 || true
  fi
  return "$rc"
}
resources_left() {
  local c v n d
  c=$(docker ps -aq --filter "label=com.docker.compose.project=$project" || true)
  v=$(docker volume ls -q --filter "label=com.docker.compose.project=$project" || true)
  n=$(docker network ls -q --filter "label=com.docker.compose.project=$project" || true)
  d=$(MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$sdk_db$" --filter "name=^/ho-g9-mutant-pg$" || true)
  printf 'containers=%s volumes=%s networks=%s database=%s\n' "$c" "$v" "$n" "$d"
}
cleanup() {
  local rc=$? safety_rc=0 summary_rc=0 archive_rc=0
  trap - EXIT
  if [[ "$owned_stack" == 1 ]]; then
    compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || rc=1
    compose down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  if [[ "$owned_sdk" == 1 ]]; then
    docker rm -f "$sdk_db" >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  resources_left > "$evidence/teardown.txt"
  [[ "$(tr -d '\n' < "$evidence/teardown.txt")" == "containers= volumes= networks= database=" ]] || rc=1
  free_c > "$evidence/c-drive-after.txt"
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"
  # Scan after the final logs exist; the disposable secrets remain available until the archive is checked.
  stage evidence-safety node scripts/check-g9-evidence-safety.mjs evidence "$(cygpath -w "$evidence")" \
    "$(cygpath -w "$root/private/db.pw")" "$(cygpath -w "$root/private/worker.pw")" || safety_rc=1
  stage evidence-summary node scripts/summarize-g9-evidence.mjs "$(cygpath -w "$evidence")" || summary_rc=1
  if [[ "$safety_rc" != 0 || "$summary_rc" != 0 ]]; then rc=1; fi
  printf '%s\n' "$rc" > "$evidence/technical-stage-exit-code.txt"
  (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
    > "$archive_dir/$archive_name-manifest.sha256" || archive_rc=1
  local draft="$root/private/$archive_name-evidence.tar.gz.pending"
  if [[ "$archive_rc" == 0 ]]; then tar -czf "$draft" -C "$root" evidence || archive_rc=1; fi
  if [[ "$archive_rc" == 0 ]]; then
    node scripts/check-g9-evidence-safety.mjs archive "$(cygpath -w "$root/cache/archive-verify")" \
      "$(cygpath -w "$draft")" "$(cygpath -w "$archive_dir/$archive_name-manifest.sha256")" \
      "$(cygpath -w "$root/private/db.pw")" "$(cygpath -w "$root/private/worker.pw")" \
      > "$archive_dir/archive-safety.log" 2>&1 || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 ]]; then
    [[ ! -e "$archive_dir/$archive_name-evidence.tar.gz" ]] || archive_rc=1
    [[ "$archive_rc" != 0 ]] || mv -n "$draft" "$archive_dir/$archive_name-evidence.tar.gz" || archive_rc=1
  fi
  if [[ "$archive_rc" != 0 ]]; then
    rm -f "$draft"
    echo "G9 archive quarantined: creation or integrity check failed" >&2
    rc=1
  fi
  local archive_hash=""
  [[ -f "$archive_dir/$archive_name-evidence.tar.gz" ]] && archive_hash="$(sha256sum "$archive_dir/$archive_name-evidence.tar.gz" | cut -d' ' -f1)"
  node -e '
    const [sha,run,rc,arc,hash]=process.argv.slice(1);
    require("fs").writeFileSync(process.argv[6],JSON.stringify({sourceSha:sha,runId:run,
      technicalStageExit:Number(rc),archiveStatus:arc==="0"?"CREATED_AND_VERIFIED":"FAILED",archiveSha256:hash,
      gateStatus:"G9 acceptance pending; G9-S01 blocked by contract acceptance; G9-S25 awaiting independent review"},null,2));
  ' "$HARBOROPS_SOURCE_SHA" "$run_id" "$rc" "$archive_rc" "$archive_hash" "$(cygpath -w "$archive_dir/finalization.json")" || rc=1
  rm -rf "$root/cache/archive-verify"
  local technical=FAILED
  [[ -f "$evidence/g9-technical-status.json" ]] && technical="$(node -p 'require(process.argv[1]).status' "$(cygpath -w "$evidence/g9-technical-status.json")" || echo FAILED)"
  if [[ "$rc" == 0 && "$technical" == "PASS" ]]; then
    printf 'DEVELOPMENT_STAGES=PASS\nTECHNICAL_STATUS=PASS\nG9_ACCEPTANCE=PENDING\n' > "$root/gate-status.txt"
    echo "G9 technical campaign PASS for $run_id on $HARBOROPS_SOURCE_SHA."
    echo "Contract acceptance (G9-S01), independent evidence review (G9-S25) and G9 acceptance remain pending."
    exit 0
  fi
  printf 'TECHNICAL_STATUS=FAILED\nG9_ACCEPTANCE=PENDING\n' > "$root/gate-status.txt"
  echo "G9 technical campaign FAILED for $run_id. Inspect stage evidence, teardown and archive." >&2
  exit 1
}
trap cleanup EXIT

stage preflight bash -c 'set -euo pipefail; git rev-parse HEAD; node --version; npm --version; docker version; docker compose version; docker compose version --short; git log -1 --format="%H %an %s"; echo "docker plugins: $DOCKER_CONFIG"; cat "$DOCKER_CONFIG/config.json"'
stage script-syntax bash -c 'set -euo pipefail; for script in scripts/*.mjs; do node --check "$script"; done; bash -n scripts/verify-g9-local.sh scripts/verify-m9-local.sh scripts/smoke-compose.sh'
stage compose-contract bash -c 'set -euo pipefail; export HARBOROPS_DB_PASSWORD=x HARBOROPS_WORKER_DB_PASSWORD=y; docker compose -f compose.g9.verify.yaml -p ho-verify-g9 --profile worker config -q; docker compose -f compose.g9.verify.yaml -p ho-verify-g9 --profile worker config | grep -E "127.0.0.1\"?:?|published|subnet" | head -20'
stage documentation bash -c 'node scripts/render-execution-graph.mjs --check && node scripts/check-planning-docs.mjs'
stage tracking-contract node --test scripts/check-m7-contract.test.mjs scripts/check-m8-contract.test.mjs scripts/check-m9-contract.test.mjs
no_actions() {
  # GitHub Actions stay disabled: no workflow may run automatically on push or pull_request.
  local found=0 file
  for file in .github/workflows/*.yml .github/workflows/*.yaml; do
    [[ -e "$file" ]] || continue
    if grep -Eq '^\s*(push|pull_request|pull_request_target|schedule|workflow_run):' "$file"; then
      echo "Automatic trigger found in $file" >&2; found=1
    fi
  done
  echo "automatic workflow triggers found: $found"
  [[ "$found" == 0 ]]
}
stage no-automatic-actions no_actions
pull_base_images() {
  local image attempt
  for image in mcr.microsoft.com/dotnet/sdk:10.0 mcr.microsoft.com/dotnet/aspnet:10.0 mcr.microsoft.com/dotnet/runtime:10.0 \
               node:22-alpine nginx:1.30.6-alpine postgres:16.15-alpine3.24 aquasec/trivy:0.76.1; do
    if ! docker image inspect "$image" > /dev/null 2>&1; then
      for attempt in 1 2 3 4; do
        docker pull "$image" && break
        echo "pull attempt $attempt failed for $image" >&2
        sleep 15
      done
    fi
    docker image inspect --format "$image {{.Id}} {{.RepoDigests}}" "$image" || return 1
  done
}
stage base-images pull_base_images
stage postgres-image docker build --build-arg "SOURCE_REVISION=$HARBOROPS_SOURCE_SHA" \
  -f docker/postgres/Dockerfile -t "$db_image" .
docker run -d --name "$sdk_db" --label harborops.g9=disposable --ulimit core=0 \
  --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 \
  --cap-drop ALL --security-opt no-new-privileges \
  -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
  -e POSTGRES_HOST_AUTH_METHOD=trust "$db_image" \
  > "$evidence/sdk-postgres-container-id.txt"
owned_sdk=1
ready=0
for _ in $(seq 1 45); do
  if docker exec "$sdk_db" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1; then ready=1; break; fi
  sleep 2
done
[[ "$ready" == 1 ]] || { echo "Disposable PostgreSQL not ready" >&2; exit 1; }
sdk() {
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --network "container:$sdk_db" \
    -v "$repo_win:/src" -v "$root_win:/cache" -w /src \
    -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e NUGET_PACKAGES=/cache/cache/nuget -e DOTNET_CLI_HOME=/cache/cache/dotnet \
    -e HOME=/cache/cache/dotnet -e TMPDIR=/cache/cache/tmp \
    -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -euc "$1"
}
# Cheap, fast-failing gates run before database suites, the stack, scans and mutations.
stage build sdk 'dotnet --info | head -12; dotnet restore HarborOps.sln -v q && dotnet build HarborOps.sln -c Release --no-restore -warnaserror && dotnet build src/HarborOps.Worker/HarborOps.Worker.csproj -c Release --no-restore -warnaserror'
nuget_advisories() {
  sdk 'dotnet list HarborOps.sln package --vulnerable --include-transitive > /cache/evidence/nuget-vulnerable.log 2>&1; cat /cache/evidence/nuget-vulnerable.log
       if grep -Eiq "has the following vulnerable packages|NU1900|NU1301|Unable to load the service index|failed to retrieve|advisory.+unavailable" /cache/evidence/nuget-vulnerable.log; then
         echo "NuGet vulnerability findings or unavailable advisory source" >&2; exit 1; fi'
}
stage nuget-advisories nuget_advisories
stage npm-advisories bash -c 'npm --prefix web audit --audit-level=high && npm --prefix tests/browser audit --audit-level=high'
stage web-build bash -c 'npm --prefix web ci --no-audit --no-fund && npm --prefix web test && npm --prefix web run build && ! grep -R -l "hop_v1_" web/dist'
stage unit-tests sdk 'dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=m9-unit.trx" --results-directory /cache/evidence/trx'
stage contract-tests sdk 'dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build --logger "trx;LogFileName=m9-contract.trx" --results-directory /cache/evidence/trx'

# The isolated Compose stack, real restricted-role worker and API, before the long database suites.
stack_up() {
  owned_stack=1
  compose config -q || return 1
  # Build every image once. A later `up --build` would rebuild dependencies and recreate PostgreSQL mid-run.
  compose build postgres migrator api web worker || return 1
  compose up -d --no-build postgres migrator api web || return 1
  local i code
  for i in $(seq 1 90); do
    code=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8089/health/ready || true)
    [[ "$code" == 200 ]] && break
    sleep 3
  done
  [[ "$code" == 200 ]] || { echo "API did not become ready" >&2; return 1; }
  compose exec -T postgres psql -v ON_ERROR_STOP=1 -U harborops -d harborops < scripts/m9-worker-role.sql || return 1
  printf "ALTER ROLE harborops_worker PASSWORD :'pw'\n" | compose exec -T -e PW="$HARBOROPS_WORKER_DB_PASSWORD" postgres \
    sh -c 'psql -v ON_ERROR_STOP=1 -v pw="$PW" -U harborops -d harborops' || return 1
  compose up -d --no-build worker || return 1
  local health
  for i in $(seq 1 60); do
    health=$(compose ps worker --format '{{.State}} {{.Health}}')
    [[ "$health" == "running healthy" ]] && break
    sleep 5
  done
  [[ "$health" == "running healthy" ]] || { echo "Worker did not become healthy" >&2; return 1; }
  compose ps -a --format '{{.Service}} {{.State}} {{.Health}} {{.ExitCode}}'
}
stage stack-up stack_up
stage stack-e2e node scripts/check-m9-stack.mjs "$(cygpath -w "$evidence")"
scan_images() {
  local trivy="aquasec/trivy:0.76.1"
  local cachemount="$(cygpath -w "$root/cache/trivy"):/root/.cache/trivy"
  local attempt db_ready=0
  for attempt in 1 2 3; do
    if MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$cachemount" "$trivy" image --download-db-only \
         >> "$evidence/trivy/db-download.log" 2>&1; then db_ready=1; break; fi
    echo "Vulnerability database download attempt $attempt failed" >> "$evidence/trivy/db-download.log"
    sleep 10
  done
  [[ "$db_ready" == 1 ]] || return 1
  MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v "$cachemount" "$trivy" version > "$evidence/trivy/version.txt" 2>&1 || return 1
  : > "$evidence/image-ids.tsv"
  local service container image revision
  for service in postgres api web worker; do
    container=$(compose ps -q "$service") || return 1
    [[ -n "$container" ]] || return 1
    image=$(docker inspect --format '{{.Image}}' "$container") || return 1
    revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image") || return 1
    [[ "$revision" == "$HARBOROPS_SOURCE_SHA" ]] || return 1
    printf '%s\t%s\t%s\n' "$service" "$image" "$revision" >> "$evidence/image-ids.tsv" || return 1
    MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 -v /var/run/docker.sock:/var/run/docker.sock -v "$cachemount" \
      -v "$(cygpath -w "$evidence/trivy"):/reports" \
      "$trivy" image --image-src docker --scanners vuln --severity HIGH,CRITICAL --exit-code 1 --format json \
      --output "/reports/$service.json" "$image" || return 1
    [[ -s "$evidence/trivy/$service.json" ]] || return 1
  done
}
stage image-scans scan_images
stack_teardown() {
  compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || return 1
  compose down --volumes --remove-orphans || return 1
  owned_stack=0
  local left
  left=$(resources_left)
  echo "$left"
  [[ "$(echo "$left" | sed 's/ database=.*//')" == "containers= volumes= networks=" ]]
}
stage stack-teardown stack_teardown

stage integration-tests sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=m9-integration.trx" --results-directory /cache/evidence/trx'
stage integration-repeat sdk 'dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=m9-integration-repeat.trx" --results-directory /cache/evidence/trx'
stage trx-summary node scripts/summarize-m9-trx.mjs "$(cygpath -w "$evidence/trx")"
if [[ -n "${HARBOROPS_G9_MUTATION_SUBSET:-}" ]]; then
  echo "Full G9 cannot use a reduced mutation subset" >&2
  exit 2
fi
# Mutations run last, with no competing work, against disposable source exports of this exact commit.
stage security-mutations node scripts/run-g9-mutations.mjs "$HARBOROPS_VERIFICATION_ROOT"
echo "All scheduled G9 technical stages completed; finalization runs on exit."
