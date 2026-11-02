#!/usr/bin/env bash
# Development-only browser rehearsal for M6. Not gate evidence.
# Usage:
#   bash scripts/dev-m6-browsers.sh up
#   bash scripts/dev-m6-browsers.sh run <chromium|firefox|webkit> <spec-file-name> [repeat-count] [grep]
#   bash scripts/dev-m6-browsers.sh down
# The stack uses the disposable ho-verify-g6 Compose project. The command refuses to start when any
# resource with that project label already exists, and only tears down what it created itself.
# Output is written under $HARBOROPS_VERIFICATION_ROOT/dev, never under evidence/.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be on E:" >&2; exit 2 ;;
esac
project=ho-verify-g6
marker="$root/dev/browser-stack.owned"
mkdir -p "$root/dev"
action="${1:?up|run|down}"
export HARBOROPS_SOURCE_SHA="$(git rev-parse HEAD)"
export COMPOSE_PROJECT_NAME="$project"
# Same synthetic default as the gate runner; the database is disposable.
export HARBOROPS_DB_PASSWORD="${HARBOROPS_DB_PASSWORD:-disposable-m3-check-only}"
export COMPOSE_PATH_SEPARATOR=";"
export COMPOSE_FILE="compose.yaml;$(cygpath -w "$root/compose.core0.yaml")"

case "$action" in
  up)
    [[ -z "$(git status --porcelain)" ]] || { echo "Commit changes first: images are labelled by revision" >&2; exit 2; }
    if [[ -n "$(docker ps -aq --filter label=com.docker.compose.project=$project)" ||
          -n "$(docker volume ls -q --filter label=com.docker.compose.project=$project)" ]]; then
      echo "Existing $project resources found. Refusing takeover." >&2; exit 2
    fi
    printf '%s\n' "$HARBOROPS_SOURCE_SHA" > "$marker"
    mkdir -p "$root/evidence"
    bash scripts/smoke-compose.sh --project "$project" --confirm-disposable
    for stage in accounts organizations setup_fixtures; do
      bash scripts/verify-m6-stages-local.sh "$stage"
    done
    export HARBOROPS_TEST_USER_PASSWORD
    HARBOROPS_TEST_USER_PASSWORD="$(cat "$root/test-user-password.txt")"
    for fixture in m3 m4 m5 m6; do node "scripts/prepare-$fixture-browser-fixtures.mjs"; done
    echo "Rehearsal stack ready for $HARBOROPS_SOURCE_SHA"
    ;;
  run)
    [[ -s "$marker" ]] || { echo "Run 'up' first" >&2; exit 2; }
    engine="${2:?engine}"; spec="${3:?spec file name}"; repeat="${4:-1}"; grep_arg="${5:-}"
    [[ "$(cat "$marker")" == "$HARBOROPS_SOURCE_SHA" ]] || { echo "Stack was built for another revision" >&2; exit 2; }
    export HARBOROPS_M3_FIXTURES=1 HARBOROPS_M4_FIXTURES=1 HARBOROPS_M5_FIXTURES=1 HARBOROPS_M6_FIXTURES=1
    stamp=$(date -u +%Y%m%dT%H%M%SZ)
    out="$root/dev/browser-$stamp-$engine-${spec%.spec.mjs}"
    mkdir -p "$out"
    cd tests/browser
    [[ -d node_modules ]] || npm ci --no-audit --no-fund
    npx playwright install "$engine" >/dev/null
    failures=0
    for i in $(seq 1 "$repeat"); do
      # The same per-spec API restart as the gate launcher, so login rate limits start clean.
      docker compose -p "$project" restart api >/dev/null
      export HARBOROPS_G3_JSON_REPORT="$out/run$i.json"
      args=("specs/${spec%.spec.mjs}.spec.mjs" "--project=$engine" --workers=1)
      [[ -z "$grep_arg" ]] || args+=("--grep" "$grep_arg")
      if ! npx playwright test "${args[@]}" 2>&1 | tee "$out/run$i.log" | tail -25; then :; fi
      if ! node -e '
        const r=JSON.parse(require("fs").readFileSync(process.argv[1],"utf8"));
        process.exit(r.stats.unexpected+r.stats.flaky+r.stats.skipped===0&&r.stats.expected>0?0:1);' "$out/run$i.json"; then
        failures=$((failures + 1))
      fi
    done
    echo "rehearsal $engine/$spec: $((repeat - failures))/$repeat clean runs (development only): $out"
    (( failures == 0 ))
    ;;
  down)
    [[ -s "$marker" ]] || { echo "Nothing owned by a rehearsal" >&2; exit 0; }
    docker compose -p "$project" logs --no-color --timestamps > "$root/dev/browser-stack-last.log" 2>&1 || true
    docker compose -p "$project" down --volumes --remove-orphans
    rm -f "$marker"
    echo "Rehearsal stack removed"
    ;;
  *) echo "unknown action" >&2; exit 2 ;;
esac
