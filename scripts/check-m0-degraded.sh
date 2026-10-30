#!/usr/bin/env bash
set -euo pipefail

# This script intentionally interrupts the LOCAL Compose database. It is not
# suitable for environments containing production or shared business data.
if [[ "${1:-}" != "--confirm-local-outage" || "${#}" != 1 ]]; then
  echo "Usage: bash scripts/check-m0-degraded.sh --confirm-local-outage" >&2
  echo "This temporarily stops the harborops Compose PostgreSQL service and restores it." >&2
  exit 2
fi

for tool in docker curl node; do
  command -v "$tool" >/dev/null || {
    echo "Required tool unavailable: $tool" >&2
    exit 1
  }
done

# A local Compose project name alone is not sufficient if Docker points to a remote daemon.
# Permit only local Unix sockets, Windows named pipes, and loopback TCP Docker endpoints.
context_host=$(docker context inspect --format '{{ (index .Endpoints "docker").Host }}')
case "${DOCKER_HOST:-$context_host}" in
  unix://*|npipe://*|tcp://localhost:*|tcp://127.0.0.1:*|tcp://\[::1\]:*) ;;
  *)
    echo "Refusing to interrupt PostgreSQL through a remote Docker context" >&2
    exit 1
    ;;
esac

# Do not allow a customized Compose project to target an unrelated stack.
if [[ -n "${COMPOSE_PROJECT_NAME:-}" && "${COMPOSE_PROJECT_NAME}" != "harborops" ]]; then
  echo "Refusing to stop services for a non-harborops Compose project" >&2
  exit 1
fi

project_name=$(docker compose config --format json | node -e '
  let body = "";
  process.stdin.on("data", chunk => body += chunk);
  process.stdin.on("end", () => {
    const config = JSON.parse(body);
    if (config.name !== "harborops" || !config.services?.postgres || !config.services?.api) {
      process.stderr.write("Expected local harborops Compose configuration not found\\n");
      process.exitCode = 1;
    } else {
      process.stdout.write(config.name);
    }
  });
')
[[ "$project_name" == "harborops" ]] || exit 1

postgres_id=$(docker compose ps -q postgres)
api_id=$(docker compose ps -q api)
if [[ -z "$postgres_id" || -z "$api_id" ]]; then
  echo "Both the local postgres and api Compose services must already be running" >&2
  exit 1
fi

check_http_status() {
  local path="$1" expected="$2" actual
  actual=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --connect-timeout 3 --max-time 25 "http://localhost:8080$path") || return 1
  if [[ "$actual" != "$expected" ]]; then
    echo "Unexpected HTTP response for $path: expected $expected, received $actual" >&2
    return 1
  fi
}

wait_for_status() {
  local path="$1" expected="$2" attempt
  for attempt in $(seq 1 12); do
    if check_http_status "$path" "$expected" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for $path to return HTTP $expected" >&2
  return 1
}

stopped=false
restore_database() {
  if [[ "$stopped" == true ]]; then
    echo "Restoring local PostgreSQL service..." >&2
    docker compose start postgres >&2 || return 1
    stopped=false
  fi
}
trap restore_database EXIT

echo "Checking healthy service before fault injection"
wait_for_status /health/live 200
wait_for_status /health/ready 200
wait_for_status /api/v1/system/baseline 200

echo "Stopping the local PostgreSQL service to validate degraded responses"
stopped=true
docker compose stop postgres >/dev/null

wait_for_status /health/live 200
wait_for_status /health/ready 503
wait_for_status /api/v1/system/baseline 503

restore_database
wait_for_status /health/live 200
wait_for_status /health/ready 200
wait_for_status /api/v1/system/baseline 200

echo "M0 database fault, liveness/readiness divergence, API degradation and recovery checks passed"
