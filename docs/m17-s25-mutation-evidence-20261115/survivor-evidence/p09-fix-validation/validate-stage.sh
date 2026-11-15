#!/usr/bin/env bash
# usage: validate-stage.sh <label> <worktree> <count>
# Runs scripts/check-g17-malformed-export.mjs from <worktree> <count> times against the rehearsal stack v1,
# each time with a fresh evidence directory, and reports the Tempo query line and the outcome.
LABEL=${1:?label}; WT=${2:?worktree}; COUNT=${3:?count}
export N=v1
source /e/HarborOps-Verification/g17-s25-audit/common.sh
OUT=$AUDIT/validate-$LABEL; mkdir -p "$OUT"
cd "$WT"
for i in $(seq 1 "$COUNT"); do
  EV="$OUT/ev-$i"; mkdir -p "$EV"
  export HARBOROPS_G17_EVIDENCE_DIR="$(cygpath -w "$EV")"
  node scripts/check-g17-malformed-export.mjs > "$OUT/run-$i.log" 2>&1; rc=$?
  res=$(node -e 'try{const r=JSON.parse(require("fs").readFileSync(process.argv[1],"utf8"));console.log(r.result,r.validated_exported_traces,String(r.failure_message).replace(/\n/g," "))}catch(e){console.log("no result file")}' "$(cygpath -w "$EV/m17-malformed-export-results.json")")
  echo "$LABEL run $i rc=$rc $res"
done
