#!/usr/bin/env bash
# One controlled P09 run (r<N>) per docs/M17_G17_S25_P09_CONTROLLED_VERIFICATION_PLAN_20261115.md.
# usage: run-one.sh <ID> [cooldown seconds]   ID is r1, r1b (replacement of an aborted r1), r2 or r3     Run it with run_in_background; the final line is "RUN_ONE_DONE <N> status=<...>".
# Nothing here changes the measurement: it runs the unmodified runner of the tested source in a clean worktree.
N=${1:?run id such as 1, 1b, 2, 3}
COOL=${2:-600}
AUDIT=/e/HarborOps-Verification/g17-s25-audit
P=$AUDIT/p09-controlled
SHA=8d5e24137ad2943a6b2787b6472e4cea7bd9947e   # 5b1dcc41 plus the Tempo window fix (plan Amendment 1)
PROJECT=ho-verify-m17-p09-r$N
MAIN="/e/APP IDEAS/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/.claude/worktrees/harborops-g17-s25-audit-6439ae"
PLAN="$MAIN/docs/m17-s25-p09-controlled-protocol-20261115"
SRC=$P/src-r$N; EV=$P/r$N-evidence; DIAG=$P/r$N-diagnostics
LOG=$P/r$N-driver.log
log(){ echo "$(date -u +%FT%TZ) $*" | tee -a "$LOG"; }
win(){ cygpath -w "$1"; }
finish(){ log "RUN_ONE_DONE $N status=$1"; echo "RUN_ONE_DONE $N status=$1"; exit "${2:-0}"; }

[ -e "$EV" ] && { log "evidence directory already exists; never overwritten"; finish refused 4; }
log "run r$N begins; plan commit $(git -C "$MAIN" rev-parse HEAD); project $PROJECT; tested source $SHA"
if [ "$COOL" -gt 0 ]; then log "cooldown $COOL s"; sleep "$COOL"; fi

# Clean worktree at the tested source.
if [ ! -d "$SRC" ]; then git -C "$MAIN" worktree add --detach "$(win "$SRC")" "$SHA" >> "$LOG" 2>&1 || finish worktree-failed 5; fi

# Environment prerequisite, not part of the measurement: the browser package set is installed by the runner only at a later
# stage, and the page-fault stage that runs earlier imports it. Earlier attempts found it in their long-lived worktree. Install it
# from the committed lockfile before the preflight (the runner repeats the same npm ci later).
if [ ! -d "$SRC/tests/browser/node_modules/playwright" ]; then
  ( export npm_config_cache='E:\HarborOps-Verification\npm-cache'; cd "$SRC/tests/browser" && npm ci --no-audit --no-fund ) >> "$LOG" 2>&1 || finish npm-ci-failed 6
  log "browser package set installed from the lockfile"
fi

# Keep the system awake for the whole run (stopped at the end).
rm -f "$P/stop-awake-r$N"
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "$(win "$P/keepawake.ps1")" -StopFile "$(win "$P/stop-awake-r$N")" > /dev/null 2>&1 < /dev/null &
AWAKE_PID=$!

# Preflight gate; a failed preflight is recorded and is not an attempt. Up to 40 tries, 120 s apart.
OK=0
for k in $(seq 1 40); do
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(win "$PLAN/preflight.ps1")" -OutFile "$(win "$P/r$N-preflight-$k.json")" \
    -Worktree "$(win "$SRC")" -ExpectedSha $SHA > "$P/r$N-preflight-$k.out" 2>&1 < /dev/null
  if [ $? -eq 0 ]; then OK=1; log "preflight passed on try $k"; cp "$P/r$N-preflight-$k.json" "$P/r$N-preflight.json"; break; fi
  log "preflight try $k failed: $(grep FAIL "$P/r$N-preflight-$k.out" | tr '\n' ' ')"
  sleep 120
done
[ $OK -eq 1 ] || { touch "$P/stop-awake-r$N"; finish preflight-never-passed 3; }

git -C "$MAIN" rev-parse HEAD > "$P/r$N-plan-commit.txt"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(win "$PLAN/diagnostics.ps1")" -Action Start -OutDir "$(win "$DIAG")" -Project "$PROJECT" > "$P/r$N-diag-start.out" 2>&1 < /dev/null
log "diagnostics: $(tr '\n' ' ' < "$P/r$N-diag-start.out")"

# The measurement itself: the unmodified runner of the tested source, flags identical to f6 and f7.
source "$AUDIT/final-secrets.env"
source /e/HarborOps-Verification/rehearsal/hostdotnet.sh
export PLAYWRIGHT_BROWSERS_PATH='E:\APP IDEAS\harborops-m0-cache\playwright-browsers' npm_config_cache='E:\HarborOps-Verification\npm-cache'
cd "$SRC"
log "RUNNER_START $(date -u +%FT%TZ) head=$(git rev-parse HEAD)"
/e/HarborOps-Verification/tools/pwsh/pwsh.exe -NoProfile -File scripts/run-g17-local.ps1 \
  -EvidenceDirectory "$(win "$EV")" -ExpectedSha "$SHA" -ProjectName "$PROJECT" \
  -StartDisposableStack -FaultProbe -VerifyDisposableWorker -RequireFullG17 -StopOwnedContainers > "$P/r$N-run.log" 2>&1 < /dev/null
RC=$?
log "RUNNER_EXIT=$RC $(date -u +%FT%TZ)"

# Stop diagnostics, then close out. Nothing is deleted except the finished owned project's containers and networks, by label.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(win "$PLAN/diagnostics.ps1")" -Action Stop -OutDir "$(win "$DIAG")" > "$P/r$N-diag-stop.out" 2>&1 < /dev/null
log "diagnostics: $(tr '\n' ' ' < "$P/r$N-diag-stop.out")"
RAW="$EV/m17-performance-raw.json"
if [ -f "$RAW" ]; then
  log "P09 raw file present sha256=$(sha256sum "$RAW" | cut -c1-64) result=$(node -e 'const r=JSON.parse(require("fs").readFileSync(process.argv[1],"utf8"));console.log(r.result,"rows",r.rows.length,"pairs",r.pairs.length)' "$(win "$RAW")")"
  COUNTED=counted
else
  log "no P09 raw file: this attempt is aborted and is not a measurement"; COUNTED=aborted
fi
[ -f "$EV/manifest.json" ] && log "manifest check (final verifier): $(node "$MAIN/scripts/verify-g17-manifest.mjs" "$(win "$EV/manifest.json")" $SHA 2>&1 | tr '\n' ' ' | cut -c1-300)"
docker ps -aq --filter "label=com.docker.compose.project=$PROJECT" | xargs -r docker rm -f > /dev/null 2>&1
docker network ls -q --filter "label=com.docker.compose.project=$PROJECT" | xargs -r docker network rm > /dev/null 2>&1
log "owned project $PROJECT containers left: $(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT" | wc -l)"
touch "$P/stop-awake-r$N"
finish "$COUNTED runner_exit=$RC" 0
