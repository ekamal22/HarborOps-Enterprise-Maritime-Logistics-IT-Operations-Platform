#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
[[ -z "$(git status --porcelain)" ]] || { echo "Dirty source tree" >&2; exit 1; }
: "${HARBOROPS_WORKSPACE_ROOT:?Set an E:-backed workspace root}"
: "${HARBOROPS_TEST_DB:?Set a disposable local integration database}"
case "$HARBOROPS_WORKSPACE_ROOT" in
  /mnt/e/*|/e/*|E:/*|E:\\*) ;;
  *) echo "Storage must be E:-backed" >&2; exit 1 ;;
esac
sha="$(git rev-parse HEAD)"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
out="${HARBOROPS_WORKSPACE_ROOT%/}/g11-evidence/$sha/$stamp"
mkdir -p "$out/tmp"
export TMPDIR="$out/tmp"
export NUGET_PACKAGES="${HARBOROPS_WORKSPACE_ROOT%/}/nuget/packages"
export DOTNET_CLI_HOME="${HARBOROPS_WORKSPACE_ROOT%/}/dotnet/home"
export npm_config_cache="${HARBOROPS_WORKSPACE_ROOT%/}/npm/cache"
export PLAYWRIGHT_BROWSERS_PATH="${HARBOROPS_WORKSPACE_ROOT%/}/playwright/browsers"
mkdir -p "$NUGET_PACKAGES" "$DOTNET_CLI_HOME" "$npm_config_cache" "$PLAYWRIGHT_BROWSERS_PATH"
printf "sha=%s\nstarted_utc=%s\n" "$sha" "$stamp" > "$out/provenance.txt"
node scripts/check-m11-contract.mjs 2>&1 | tee "$out/source-check.log"
node scripts/render-execution-graph.mjs --check 2>&1 | tee "$out/graph-check.log"
node scripts/check-planning-docs.mjs 2>&1 | tee "$out/docs-check.log"
dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj --logger "trx;LogFileName=m11-unit.trx" --results-directory "$out" 2>&1 | tee "$out/unit.log"
dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj --filter "FullyQualifiedName~M11ExceptionRuntimeTests" --logger "trx;LogFileName=m11-integration.trx" --results-directory "$out" 2>&1 | tee "$out/integration.log"
npm --prefix web run build 2>&1 | tee "$out/web-build.log"
if [[ "${HARBOROPS_M5_FIXTURES:-0}" == "1" && -n "${HARBOROPS_TEST_USER_PASSWORD:-}" ]]; then
  npm --prefix tests/browser test -- shipment-exceptions.spec.mjs 2>&1 | tee "$out/browser.log"
else
  echo "NOT EXECUTED: browser fixtures unavailable" > "$out/browser.txt"
fi
printf "finished_utc=%s\n" "$(date -u +%Y%m%dT%H%M%SZ)" >> "$out/provenance.txt"
(cd "$out" && find . -type f ! -name sha256.txt -print0 | sort -z | xargs -0 -r sha256sum > sha256.txt)
echo "Focused M11 evidence saved to E:. G11 acceptance pending."
