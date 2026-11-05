#!/usr/bin/env bash
# Supplemental G9-S22 evidence: the existing M0 to M8 browser journeys in Chromium, Firefox and WebKit
# against the isolated G9 stack built from a clean, source-locked checkout. E: storage only, no GitHub
# Actions. Exit 0 means every journey passed; it is not G9 acceptance.
#
# Required environment:
#   HARBOROPS_VERIFICATION_ROOT   new E: path ending in /m9b
#   HARBOROPS_G9_PROJECT          new ho-verify-g9-<suffix> Compose project
#   HARBOROPS_SOURCE_WORKTREE     clean E: checkout (detached) of the implementation revision
#   HARBOROPS_EXPECTED_SOURCE_SHA the 40-character revision that checkout must be at
set -euo pipefail
cd "$(dirname "$0")/.."
tooling_dir="$(pwd)"
tooling_win="$(pwd -W)"
tooling_sha="$(git rev-parse HEAD)"
command -v cygpath >/dev/null || { echo "Windows Git Bash required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
case "$(cygpath -m "$root_input")" in [Ee]:/*/m9b) ;; *) echo "Dedicated E: path ending in /m9b required" >&2; exit 2 ;; esac
root_win="$(cygpath -w "$root_input")"
root="$(cygpath -u "$root_win")"
export HARBOROPS_VERIFICATION_ROOT="$root_win"
src_input="$(printenv HARBOROPS_SOURCE_WORKTREE || true)"
case "$src_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: source worktree required" >&2; exit 2 ;; esac
src="$(cygpath -u "$(cygpath -w "$src_input")")"
expected="$(printenv HARBOROPS_EXPECTED_SOURCE_SHA || true)"
[[ "$expected" =~ ^[0-9a-f]{40}$ ]] || { echo "Expected source SHA required" >&2; exit 2; }
[[ "$(git -C "$src" rev-parse HEAD)" == "$expected" ]] || { echo "Source worktree is not at the expected revision" >&2; exit 2; }
[[ -z "$(git -C "$src" status --porcelain)" ]] || { echo "Source worktree must be clean" >&2; exit 2; }
[[ -z "$(git status --porcelain)" ]] || { echo "Tooling checkout must be clean" >&2; exit 2; }
export HARBOROPS_SOURCE_SHA="$expected"
project="${HARBOROPS_G9_PROJECT:-}"
[[ "$project" =~ ^ho-verify-g9-[a-z0-9]{1,12}$ ]] || { echo "A new ho-verify-g9-<suffix> project is required" >&2; exit 2; }
export HARBOROPS_G9_PROJECT="$project"
mkdir -p "$root"
for var in TEMP TMP NPM_CONFIG_CACHE PLAYWRIGHT_BROWSERS_PATH DOTNET_CLI_HOME DOCKER_CONFIG XDG_CACHE_HOME; do
  folder="$root/cache/$var"
  mkdir -p "$folder"
  export "$var=$(cygpath -w "$folder")"
done
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
mkdir -p "$TMPDIR" "$HOME" "$root/cache/tmp"
export USERPROFILE="$(cygpath -w "$HOME")"
export DOCKER_HOST="npipe:////./pipe/dockerDesktopLinuxEngine"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
node scripts/check-m3-docker-storage.mjs
src_win="$(cygpath -w "$src")"
compose() { docker compose -f "$src_win\\compose.g9.verify.yaml" -p "$project" --project-directory "$src_win" --profile worker "$@"; }
if [[ -n "$(docker ps -aq --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker volume ls -q --filter "label=com.docker.compose.project=$project")" ||
      -n "$(docker network ls -q --filter "label=com.docker.compose.project=$project")" ]]; then
  echo "Existing resources for this project detected, refusing takeover" >&2; exit 2
fi
for port in 8089 5179 55439; do
  if netstat -ano 2>/dev/null | grep -E "[:.]$port .*LISTENING" >/dev/null; then
    echo "Port $port is already in use; another stack is running" >&2; exit 2
  fi
done
if [[ -e "$root/evidence" || -e "$root/private" ]]; then
  echo "Evidence already exists; select a new E:-backed /m9b root" >&2; exit 2
fi
run_id="$(date -u +%Y%m%dT%H%M%SZ)-$(node -e 'process.stdout.write(require("node:crypto").randomBytes(8).toString("hex"))')"
archive_dir="$root/archives/$run_id"
mkdir -p "$root/archives" "$root/private"
mkdir "$archive_dir" || { echo "Archive collision; refusing overwrite" >&2; exit 2; }
archive_name="g9-browsers-$expected-$run_id"
evidence="$root/evidence"
mkdir "$evidence"
mkdir -p "$evidence/playwright"
printf '%s\n' "$run_id" > "$evidence/run-id.txt"
printf '%s\n' "$expected" > "$evidence/source-sha.txt"
printf '%s\n' "$tooling_sha" > "$evidence/tooling-sha.txt"
date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/started-utc.txt"
git -C "$src" status --porcelain > "$evidence/source-worktree-state.txt"
git status --porcelain > "$evidence/tooling-worktree-state.txt"
git -C "$src" diff --stat "$expected" "$tooling_sha" -- src web tests compose.g9.verify.yaml docker \
  scripts/prepare-m3-browser-fixtures.mjs scripts/prepare-m4-browser-fixtures.mjs \
  scripts/prepare-m5-browser-fixtures.mjs scripts/prepare-m6-browser-fixtures.mjs > "$evidence/source-vs-tooling-diff.txt" 2>&1 || true
: > "$evidence/stages.tsv"
for name in db worker user; do
  node -e 'process.stdout.write(require("node:crypto").randomBytes(24).toString("hex"))' > "$root/private/$name.pw"
done
export HARBOROPS_DB_PASSWORD="$(cat "$root/private/db.pw")"
export HARBOROPS_WORKER_DB_PASSWORD="$(cat "$root/private/worker.pw")"
export HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/private/user.pw")"
free_c() { node -e 'const s=require("fs").statfsSync("C:/");console.log(Math.round(s.bavail*s.bsize/1048576)+" MiB free on C:")'; }
free_c > "$evidence/c-drive-before.txt"
owned_stack=0
stage() {
  local label="$1" rc=0 start=$SECONDS
  shift
  echo "Starting browser supplement stage $label"
  if "$@" > "$evidence/$label.log" 2>&1; then rc=0; else rc=$?; fi
  printf '%s\t%s\t%s\t%s\n' "$label" "$expected" "$rc" "$((SECONDS-start))" >> "$evidence/stages.tsv"
  if [[ "$rc" != 0 ]]; then
    echo "Stage $label failed; see E:-backed $label.log" >&2
    tail -45 "$evidence/$label.log" >&2 || true
  fi
  return "$rc"
}
resources_left() {
  printf 'containers=%s volumes=%s networks=%s\n' \
    "$(docker ps -aq --filter "label=com.docker.compose.project=$project" || true)" \
    "$(docker volume ls -q --filter "label=com.docker.compose.project=$project" || true)" \
    "$(docker network ls -q --filter "label=com.docker.compose.project=$project" || true)"
}
cleanup() {
  local rc=$? safety_rc=0 summary_rc=0 archive_rc=0
  trap - EXIT
  if [[ "$owned_stack" == 1 ]]; then
    compose logs --no-color --timestamps > "$evidence/compose.log" 2>&1 || rc=1
    compose down --volumes --remove-orphans >> "$evidence/cleanup.log" 2>&1 || rc=1
  fi
  resources_left > "$evidence/teardown.txt"
  [[ "$(tr -d '\n' < "$evidence/teardown.txt")" == "containers= volumes= networks=" ]] || rc=1
  free_c > "$evidence/c-drive-after.txt"
  date -u +%Y-%m-%dT%H:%M:%SZ > "$evidence/ended-utc.txt"
  stage evidence-safety node "$tooling_win/scripts/check-g9-evidence-safety.mjs" evidence "$(cygpath -w "$evidence")" \
    "$(cygpath -w "$root/private/db.pw")" "$(cygpath -w "$root/private/worker.pw")" \
    "$(cygpath -w "$root/private/user.pw")" || safety_rc=1
  stage browser-summary node "$tooling_win/scripts/summarize-g9-browsers.mjs" "$(cygpath -w "$evidence")" \
    "$src_win\\tests\\browser\\specs" || summary_rc=1
  if [[ "$safety_rc" != 0 || "$summary_rc" != 0 ]]; then rc=1; fi
  printf '%s\n' "$rc" > "$evidence/technical-stage-exit-code.txt"
  (cd "$evidence" && find . -type f -print0 | sort -z | xargs -0 sha256sum) \
    > "$archive_dir/$archive_name-manifest.sha256" || archive_rc=1
  local draft="$root/private/$archive_name-evidence.tar.gz.pending"
  if [[ "$archive_rc" == 0 ]]; then tar -czf "$draft" -C "$root" evidence || archive_rc=1; fi
  if [[ "$archive_rc" == 0 ]]; then
    node "$tooling_win/scripts/check-g9-evidence-safety.mjs" archive "$(cygpath -w "$root/cache/archive-verify")" \
      "$(cygpath -w "$draft")" "$(cygpath -w "$archive_dir/$archive_name-manifest.sha256")" \
      "$(cygpath -w "$root/private/db.pw")" "$(cygpath -w "$root/private/worker.pw")" \
      "$(cygpath -w "$root/private/user.pw")" > "$archive_dir/archive-safety.log" 2>&1 || archive_rc=1
  fi
  if [[ "$archive_rc" == 0 ]]; then
    [[ ! -e "$archive_dir/$archive_name-evidence.tar.gz" ]] || archive_rc=1
    [[ "$archive_rc" != 0 ]] || mv -n "$draft" "$archive_dir/$archive_name-evidence.tar.gz" || archive_rc=1
  fi
  if [[ "$archive_rc" != 0 ]]; then
    rm -f "$draft"
    echo "Archive quarantined: creation or integrity check failed" >&2
    rc=1
  fi
  local archive_hash=""
  [[ -f "$archive_dir/$archive_name-evidence.tar.gz" ]] && archive_hash="$(sha256sum "$archive_dir/$archive_name-evidence.tar.gz" | cut -d' ' -f1)"
  node -e '
    const [sha,tool,run,rc,arc,hash,file]=process.argv.slice(1);
    require("fs").writeFileSync(file,JSON.stringify({sourceSha:sha,toolingSha:tool,runId:run,
      technicalStageExit:Number(rc),archiveStatus:arc==="0"?"CREATED_AND_VERIFIED":"FAILED",archiveSha256:hash,
      gateStatus:"Supplemental G9-S22 browser evidence only; G9 acceptance pending"},null,2));
  ' "$expected" "$tooling_sha" "$run_id" "$rc" "$archive_rc" "$archive_hash" "$(cygpath -w "$archive_dir/finalization.json")" || rc=1
  rm -rf "$root/cache/archive-verify"
  if [[ "$rc" == 0 ]]; then
    printf 'BROWSER_SUPPLEMENT=PASS\nG9_ACCEPTANCE=PENDING\n' > "$root/gate-status.txt"
    echo "G9 browser supplement PASS for $run_id on $expected. G9 acceptance pending."
    exit 0
  fi
  printf 'BROWSER_SUPPLEMENT=FAILED\nG9_ACCEPTANCE=PENDING\n' > "$root/gate-status.txt"
  echo "G9 browser supplement FAILED for $run_id. Inspect stage evidence, teardown and archive." >&2
  exit 1
}
trap cleanup EXIT

stage preflight bash -c 'set -euo pipefail; git -C "$0" rev-parse HEAD; node --version; npm --version; docker version; docker compose version; echo tooling "$1"; cat "$DOCKER_CONFIG/config.json"' "$src" "$tooling_sha"
stack_up() {
  owned_stack=1
  compose config -q || return 1
  compose build postgres migrator api web worker || return 1
  compose up -d --no-build postgres migrator api web || return 1
  local i code
  for i in $(seq 1 90); do
    code=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8089/health/ready || true)
    [[ "$code" == 200 ]] && break
    sleep 3
  done
  [[ "$code" == 200 ]] || { echo "API did not become ready" >&2; return 1; }
  compose exec -T postgres psql -v ON_ERROR_STOP=1 -U harborops -d harborops < "$src/scripts/m9-worker-role.sql" || return 1
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
cd "$src"
stage stack-up stack_up
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
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5179"
stage browser-fixtures bash -c \
  'node scripts/prepare-m3-browser-fixtures.mjs && node scripts/prepare-m4-browser-fixtures.mjs && node scripts/prepare-m5-browser-fixtures.mjs && node scripts/prepare-m6-browser-fixtures.mjs'
export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1
export HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
stage browsers bash "$tooling_win/scripts/run-g9-browsers-local.sh"
echo "All scheduled browser supplement stages completed; finalization runs on exit."
