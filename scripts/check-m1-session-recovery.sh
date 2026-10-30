#!/usr/bin/env bash
set -euo pipefail

project=""
confirmed=false
while (($#)); do
  case "$1" in
    --project) project="${2:-}"; shift 2 ;;
    --confirm-local-outage) confirmed=true; shift ;;
    *) echo "Usage: bash scripts/check-m1-session-recovery.sh --project ho-verify --confirm-local-outage" >&2; exit 2 ;;
  esac
done
if [[ ! "$project" =~ ^ho-verify(-[a-z0-9-]+)?$ || "$confirmed" != true ]]; then
  echo "Refusing unconfirmed or non-disposable project" >&2
  exit 2
fi
for command in docker curl node mktemp; do command -v "$command" >/dev/null; done
test -n "${HARBOROPS_TEST_USER_PASSWORD:-}"
compose=(docker compose -p "$project")
for service in api postgres web; do
  container=$("${compose[@]}" ps -q "$service")
  [[ -n "$container" ]] || exit 1
  label=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$container")
  [[ "$label" == "$project" ]] || exit 1
done
jar=$(mktemp)
replay=$(mktemp)
stopped=false
cleanup() {
  if [[ "$stopped" == true ]]; then "${compose[@]}" start postgres >&2; fi
  rm -f "$jar" "$replay"
}
trap cleanup EXIT

base="http://localhost:5173"
csrf=$(curl --silent --show-error --fail -c "$jar" -b "$jar" "$base/api/v1/auth/csrf" |
  node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>process.stdout.write(JSON.parse(s).token))')
payload=$(node -e 'process.stdout.write(JSON.stringify({email:"operator@example.test",password:process.env.HARBOROPS_TEST_USER_PASSWORD}))')
status=$(curl --silent --show-error -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
  -H "Origin: $base" -H "X-HarborOps-CSRF: $csrf" -H "Content-Type: application/json" \
  -d "$payload" "$base/api/v1/auth/login")
[[ "$status" == 200 ]] || { echo "Login returned $status" >&2; exit 1; }
check() {
  local expected="$1" actual
  for _ in $(seq 1 25); do
    actual=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 8 \
      -b "$jar" "$base/api/v1/auth/session" || true)
    if [[ "$actual" == "$expected" ]]; then return 0; fi
    sleep 2
  done
  echo "Session returned $actual, expected $expected" >&2
  return 1
}
check 200
"${compose[@]}" restart api >/dev/null
check 200

stopped=true
"${compose[@]}" stop postgres >/dev/null
check 503
"${compose[@]}" start postgres >/dev/null
stopped=false
check 200

cp "$jar" "$replay"
csrf=$(curl --silent --show-error --fail -c "$jar" -b "$jar" "$base/api/v1/auth/csrf" |
  node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>process.stdout.write(JSON.parse(s).token))')
status=$(curl --silent --show-error -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
  -H "Origin: $base" -H "X-HarborOps-CSRF: $csrf" -H "Content-Type: application/json" \
  -d '{}' "$base/api/v1/auth/logout-all")
[[ "$status" == 204 ]] || exit 1
replayed=$(curl -sS -o /dev/null -w '%{http_code}' -b "$replay" "$base/api/v1/auth/session/guard")
[[ "$replayed" == 401 ]] || { echo "Revoked cookie replay returned $replayed" >&2; exit 1; }
echo "M1 cookie restart, dependency outage, global logout and replay checks passed"
