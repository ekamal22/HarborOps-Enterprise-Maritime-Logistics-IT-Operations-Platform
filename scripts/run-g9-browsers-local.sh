#!/usr/bin/env bash
# Run every existing browser spec in each engine against the isolated G9 stack, independently and
# without shared login-rate-limit state. Playwright JSON evidence stays on E:. A failing spec never
# hides the specs after it: all specs in all engines run and the stage fails at the end if any failed.
# Must run with the working directory set to the source-locked checkout.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "E:-backed verification root required" >&2; exit 2 ;;
esac
if command -v cygpath >/dev/null; then root=$(cygpath -u "$root"); fi
project=$(printenv HARBOROPS_G9_PROJECT || true)
[[ "$project" =~ ^ho-verify-g9(-[a-z0-9]{1,12})?$ ]] || { echo "Invalid G9 project" >&2; exit 2; }
out="$root/evidence/playwright"
mkdir -p "$out"
export COMPOSE_FILE="$(cygpath -w "$(pwd)/compose.g9.verify.yaml")"
export COMPOSE_PROJECT_DIRECTORY="$(cygpath -w "$(pwd)")"
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5179"
cd tests/browser
npm ci --no-audit --no-fund
npx playwright --version > "$out/playwright-version.txt"
npx playwright install chromium firefox webkit
ls "$PLAYWRIGHT_BROWSERS_PATH" > "$out/browser-builds.txt"
failed=()
# Defaults run every spec in every engine. HARBOROPS_BROWSER_ENGINES and HARBOROPS_BROWSER_SPECS
# (space separated names) narrow a documented partial rerun; the summarizer reads the same filters.
read -ra engines <<< "${HARBOROPS_BROWSER_ENGINES:-chromium firefox webkit}"
if [[ -n "${HARBOROPS_BROWSER_SPECS:-}" ]]; then
  spec_files=()
  for name in $HARBOROPS_BROWSER_SPECS; do spec_files+=("specs/$name.spec.mjs"); done
else
  spec_files=(specs/*.spec.mjs)
fi
for engine in "${engines[@]}"; do
  for spec in "${spec_files[@]}"; do
    basename="$(basename "$spec" .spec.mjs)"
    docker compose -p "$project" --profile worker restart api
    # The API needs a moment before the web proxy can reach it again.
    for _ in $(seq 1 40); do
      curl --silent --fail --max-time 3 http://127.0.0.1:8089/health/ready >/dev/null && break
      sleep 1
    done
    export HARBOROPS_G3_JSON_REPORT="$(cygpath -w "$out/$engine-$basename.json")"
    echo "SPEC $engine/$basename start $(date -u +%H:%M:%SZ)"
    if ! npx playwright test "$spec" --project="$engine" --workers=1; then
      failed+=("$engine/$basename")
    fi
    echo "SPEC $engine/$basename end $(date -u +%H:%M:%SZ)"
    # Failure screenshots, traces and error context are overwritten by the next spec, so keep them.
    if [[ -d test-results/artifacts ]] && [[ -n "$(ls -A test-results/artifacts)" ]]; then
      mkdir -p "$out/artifacts/$engine-$basename"
      cp -r test-results/artifacts/. "$out/artifacts/$engine-$basename/"
    fi
  done
done
if (( ${#failed[@]} )); then
  echo "Browser specs failed: ${failed[*]}" >&2
  exit 1
fi
echo "All declared browser specs ran across Chromium, Firefox and WebKit"
