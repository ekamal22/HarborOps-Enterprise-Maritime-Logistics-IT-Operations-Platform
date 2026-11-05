#!/usr/bin/env bash
# Run every browser spec in each engine, independently and without shared
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
export COMPOSE_FILE="$(cygpath -w "$(pwd)/compose.g16.verify.yaml")"
export COMPOSE_PROJECT_DIRECTORY="$(cygpath -w "$(pwd)")"
export HARBOROPS_BROWSER_BASE_URL="http://localhost:5176"
cd tests/browser
npm ci --no-audit --no-fund
npx playwright install chromium firefox webkit
failed=()
for engine in chromium firefox webkit; do
  for spec in specs/*.spec.mjs; do
    basename="$(basename "$spec" .spec.mjs)"
    docker compose -p ho-verify-g16 restart api
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
