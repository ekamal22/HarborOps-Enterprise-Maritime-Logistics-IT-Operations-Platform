#!/usr/bin/env bash
# Run every browser spec in each engine, independently and without shared
# login-rate-limit state. Playwright JSON evidence remains on E:.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "E:-backed verification root required" >&2; exit 2 ;;
esac
out="$root/evidence/playwright"
mkdir -p "$out"
cd tests/browser
npm ci --no-audit --no-fund
npx playwright install chromium firefox webkit
for engine in chromium firefox webkit; do
  for spec in specs/*.spec.mjs; do
    basename="$(basename "$spec" .spec.mjs)"
    docker compose -p ho-verify restart api
    export HARBOROPS_G3_JSON_REPORT="$out/$engine-$basename.json"
    npx playwright test "$spec" --project="$engine" --workers=1
  done
done
echo "All declared browser specs ran across Chromium, Firefox and WebKit"
