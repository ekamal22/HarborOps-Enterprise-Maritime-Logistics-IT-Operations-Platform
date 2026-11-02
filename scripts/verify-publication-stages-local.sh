#!/usr/bin/env bash
# Publication-security stages that need the running disposable Compose project ho-verify-g6.
# Usage: bash scripts/verify-publication-stages-local.sh <stage> [<stage>...]
#   stages: scripts transport
# Everything runs against the loopback-published ports or a throwaway probe container on the
# project's own network. Nothing here touches the application's real Compose project or volumes.
set -euo pipefail

project="ho-verify-g6"
network="${project}_internal"
api="http://127.0.0.1:8080"
web="http://127.0.0.1:5173"
failures=0

expect() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    printf 'PASS  %s (HTTP %s)\n' "$label" "$actual"
  else
    printf 'FAIL  %s (expected HTTP %s, observed %s)\n' "$label" "$expected" "$actual"
    failures=$((failures + 1))
  fi
}

host_status() {
  curl --silent --output /dev/null --max-time 10 --write-out '%{http_code}' "$@"
}

# Status code seen by a client whose address is a normal, untrusted peer on the Compose network.
probe_status() {
  local code
  code=$(MSYS_NO_PATHCONV=1 docker run --rm --network "$network" --cap-drop ALL \
    --security-opt no-new-privileges --ulimit core=0 alpine:3 sh -c '
      wget -S -q -O /dev/null "$@" 2>&1 | grep -m1 "HTTP/" | grep -o "[0-9][0-9][0-9]" | head -1
    ' sh "$@" || true)
  printf '%s' "${code:-none}"
}

stage_scripts() {
  for script in scripts/audit-publication-history.py scripts/audit-publication-artifacts.py \
      scripts/audit-publication-supply-chain.py scripts/prepare-public-source-snapshot.py \
      scripts/check-publication-governance.py; do
    python -I -c 'import ast,sys;ast.parse(open(sys.argv[1],encoding="utf-8").read())' "$script"
    echo "syntax ok: $script"
  done
  node --check scripts/verify-publication-http.mjs
  node --check scripts/verify-publication-hosted.mjs
  echo "syntax ok: node publication scripts"
}

stage_transport() {
  docker network inspect "$network" >/dev/null
  docker compose -p "$project" exec -T web nginx -t
  node scripts/verify-publication-http.mjs

  # Loopback-published ports.
  expect "host to API csrf, loopback Host" 200 "$(host_status "$api/api/v1/auth/csrf")"
  expect "host to API, unexpected Host" 400 "$(host_status -H 'Host: evil.example' "$api/api/v1/auth/csrf")"
  expect "host to API, forged forwarded address from non-proxy gateway" 200 \
    "$(host_status -H 'X-Forwarded-For: 198.51.100.5' "$api/api/v1/auth/csrf")"
  expect "host to web proxy, spoofed forwarded address is overwritten" 200 \
    "$(host_status -H 'X-Forwarded-For: 198.51.100.5' "$web/api/v1/auth/csrf")"
  expect "host to web proxy, unexpected Host" 421 "$(host_status -H 'Host: evil.example' "$web/")"

  # A peer on the Compose network that is neither the gateway nor the web proxy.
  expect "untrusted Docker peer to API with localhost Host" 400 \
    "$(probe_status --header 'Host: localhost' http://api:8080/api/v1/auth/csrf)"
  expect "untrusted Docker peer to API with spoofed loopback forwarded address" 400 \
    "$(probe_status --header 'Host: localhost' --header 'X-Forwarded-For: 127.0.0.1' \
        http://api:8080/api/v1/auth/csrf)"
  expect "untrusted Docker peer through web proxy" 400 \
    "$(probe_status --header 'Host: localhost' http://web/api/v1/auth/csrf)"
  expect "untrusted Docker peer through web proxy with spoofed loopback forwarded address" 400 \
    "$(probe_status --header 'Host: localhost' --header 'X-Forwarded-For: 127.0.0.1' \
        http://web/api/v1/auth/csrf)"

  if (( failures > 0 )); then
    echo "Publication transport stage failed: $failures check(s)" >&2
    return 1
  fi
  echo "Publication transport stage: all checks passed"
}

for stage in "$@"; do
  echo "== publication stage: $stage =="
  "stage_$stage"
done
