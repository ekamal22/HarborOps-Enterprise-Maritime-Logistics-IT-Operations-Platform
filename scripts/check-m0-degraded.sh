#!/usr/bin/env bash
set -euo pipefail

project=""
confirmed=false
while (($#)); do
  case "$1" in
    --project) project="${2:-}"; shift 2 ;;
    --confirm-local-outage) confirmed=true; shift ;;
    *) echo "Usage: bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage" >&2; exit 2 ;;
  esac
done

# This script deliberately stops the database. Require explicit confirmation
# and a known disposable test-project naming convention, never the main stack.
if [[ ! "$project" =~ ^ho-verify(-[a-z0-9-]+)?$ || "$confirmed" != true ]]; then
  echo "Refusing to interrupt a non-disposable or unconfirmed Compose project" >&2
  exit 2
fi
for tool in docker curl node; do
  command -v "$tool" >/dev/null || { echo "Missing $tool" >&2; exit 1; }
done

context_host=$(docker context inspect --format '{{ (index .Endpoints "docker").Host }}')
case "${DOCKER_HOST:-$context_host}" in
  unix://*|npipe://*|tcp://localhost:*|tcp://127.0.0.1:*|tcp://\[::1\]:*) ;;
  *) echo "Refusing remote Docker context" >&2; exit 1 ;;
esac

compose=(docker compose --project-name "$project")
actual=$("${compose[@]}" config --format json | node -e '
let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{
const cfg=JSON.parse(s);
if(!cfg.services?.postgres||!cfg.services?.api)process.exit(1);
process.stdout.write(cfg.name);
});')
[[ "$actual" == "$project" ]] || { echo "Unexpected Compose project" >&2; exit 1; }

for service in api postgres; do
  id=$("${compose[@]}" ps -q "$service")
  [[ -n "$id" ]] || { echo "$service is not running in $project" >&2; exit 1; }
  label=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$id")
  [[ "$label" == "$project" ]] || { echo "Unexpected container ownership" >&2; exit 1; }
done

api_address=$("${compose[@]}" port api 8080)
[[ "$api_address" =~ ^127\.0\.0\.1:[0-9]+$ ]] || { echo "API port is not loopback-only" >&2; exit 1; }
api="http://$api_address"

check_status() {
  local path="$1" expected="$2" actual
  actual=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --connect-timeout 3 --max-time 10 "$api$path") || return 1
  [[ "$actual" == "$expected" ]] || {
    echo "$path returned $actual, expected $expected" >&2
    return 1
  }
}
wait_status() {
  local path="$1" status="$2"
  for i in $(seq 1 20); do
    if check_status "$path" "$status" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  echo "Timeout waiting for $path to return $status" >&2
  return 1
}

stopped=false
restore_database() {
  if [[ "$stopped" == true ]]; then
    echo "Restoring PostgreSQL in project $project" >&2
    "${compose[@]}" start postgres >&2
    stopped=false
  fi
}
trap restore_database EXIT

echo "Checking healthy dependencies in $project"
wait_status /health/live 200
wait_status /health/ready 200
wait_status /api/v1/system/baseline 200

stopped=true
"${compose[@]}" stop postgres >/dev/null
wait_status /health/live 200
wait_status /health/ready 503
wait_status /api/v1/system/baseline 503

restore_database
wait_status /health/live 200
wait_status /health/ready 200
wait_status /api/v1/system/baseline 200
echo "M0 isolated database outage and recovery passed on $project"
