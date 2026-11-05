#!/usr/bin/env bash
# Combined M9/M16 campaign: run every browser spec (M0 to M9 journeys and the M16 audit spec) in each engine,
# independently and without shared
# login-rate-limit state. Playwright JSON evidence remains on E:.
# A failing spec no longer hides the specs that follow it: every spec in every engine
# runs, and the stage fails at the end if any of them failed. Failure artifacts are copied to E:.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
# Judge the drive before translating: Git Bash may mount E: at /tmp or /e depending on TEMP.
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "E:-backed verification root required" >&2; exit 2 ;;
esac
if command -v cygpath >/dev/null; then root=$(cygpath -u "$root"); fi
out="$root/evidence/playwright"
mkdir -p "$out"
# Compose files must stay rooted at the checkout after entering tests/browser.
project="${G16_PROJECT:?G16_PROJECT required}"
[[ "$project" =~ ^ho-verify-cg16(-[a-z0-9]{1,12})?$ ]] || { echo "Invalid combined project" >&2; exit 2; }
# COMPOSE_FILE is exported by the combined runner and layers the combined Compose files.
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5176"
cd tests/browser
npm ci --no-audit --no-fund
npx playwright install chromium firefox webkit
failed=()
for engine in chromium firefox webkit; do
  for spec in specs/*.spec.mjs; do
    basename="$(basename "$spec" .spec.mjs)"
    docker compose -p "$project" --profile worker restart api
    for _ in $(seq 1 40); do curl --silent --fail --max-time 3 http://127.0.0.1:8086/health/ready >/dev/null && break; sleep 1; done
    # The shared Playwright reporter consumes HARBOROPS_G3_JSON_REPORT.
    export HARBOROPS_G3_JSON_REPORT="$(cygpath -w "$out/$engine-$basename.json")"
    if ! npx playwright test "$spec" --project="$engine" --workers=1; then
      failed+=("$engine/$basename")
    fi
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
