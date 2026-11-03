#!/usr/bin/env bash
# Run every browser spec in each engine, independently and without shared
# login-rate-limit state. Playwright JSON evidence remains on E:.
# A failing spec no longer hides the specs that follow it: every spec in every engine
# runs, and the stage fails at the end if any of them failed. Failure artifacts are copied to E:.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "E:-backed verification root required" >&2; exit 2 ;;
esac
out="$root/evidence/playwright"
mkdir -p "$out"
# Compose files must stay rooted at the checkout after entering tests/browser.
repo_compose="$(cygpath -w "$(pwd)/compose.yaml")"
core_override="$(cygpath -w "$root/compose.core0.yaml")"
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="$repo_compose;$core_override"
cd tests/browser
npm ci --no-audit --no-fund
npx playwright install chromium firefox webkit
failed=()
for engine in chromium firefox webkit; do
  for spec in specs/*.spec.mjs; do
    basename="$(basename "$spec" .spec.mjs)"
    docker compose -p ho-verify-g7 restart api
    # The shared Playwright reporter consumes HARBOROPS_G3_JSON_REPORT.
    export HARBOROPS_G3_JSON_REPORT="$out/$engine-$basename.json"
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
