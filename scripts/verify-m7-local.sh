#!/usr/bin/env bash
# Disposable source-locked G7 technical verification, local E: storage only.
set -euo pipefail
cd "$(dirname "$0")/.."
command -v cygpath >/dev/null || { echo "Windows Git Bash required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
root="$(cygpath -u "$root_input")"
case "$root" in /e/*/m7) ;; *) echo "Dedicated E: path ending in /m7 required" >&2; exit 2 ;; esac
export HARBOROPS_VERIFICATION_ROOT="$root"
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
  web:
    ulimits: { core: 0 }
YAML
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="$repo_win/compose.yaml;$(cygpath -w "$override")"
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

