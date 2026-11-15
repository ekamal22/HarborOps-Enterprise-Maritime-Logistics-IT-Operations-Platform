# G17-S25 Mutation Survivor Classification (Second Pass)

**Recorded:** 2026-11-15. **Branch:** `feat/harborops-g17-s25-audit-6439ae`. **Tested campaign source (f7):** `5b1dcc41d5b19dcbacd66de38193dc5020964647`. **Gate:** G17 NOT ACCEPTED; independent S25 review pending (A01, A02 and A03 are not signed by this work).

This document answers the independent review's second open issue: the 69 reported mutation survivors out of 637 mutations. It retrieves the complete per-mutant results, reconciles the denominators of every mutation campaign run on this branch, classifies every surviving mutation individually as equivalent, unreachable, redundant or genuinely material, and records the corrections. Original results are preserved unchanged and are referenced, not rewritten.

## 1. Result

| Measure | Value |
| --- | --- |
| Final strict exhaustive campaign | 805 candidate mutations over 14 evidence-gating modules, every candidate tried |
| Killed by a failing test | 790 |
| Killed at module load (the mutated module throws on import) | 4 |
| Survived | 11 |
| Invalid infrastructure results counted as kills | 0 (none ever counted; see section 3) |
| Distinct surviving mutations seen in any campaign | 120 |
| Of those: material, corrected by a new negative test | 108 |
| Equivalent (identical behaviour) | 2 |
| Unreachable | 1 |
| Redundant (a second check always rejects first) | 8 |
| Identity mutant from a contaminated baseline (retired) | 1 |
| Material weaknesses still open in this mutation class | 0 |

The 11 mutations that still survive are all equivalent, unreachable or redundant, each with executed evidence (section 6). None can change an accept or reject decision of an evidence verifier.

This is a statement about the 14 modules and 12 mutation operators listed in section 3. It is not a claim that every source file in the repository has been mutation tested. The .NET code, the PowerShell runner, the Prometheus rules and the browser specs were not mutated by this tool (section 8).

## 2. What was wrong in the original report

The reviewed figure was 69 survivors out of 637 mutations. Three facts explain it and one of them retires part of it.

1. **Only the manifest verifier was sampled.** `scripts/verify-g17-manifest.mjs` has 288 candidate mutations. Every campaign before the final one tried a deterministic spread of 120 of them (every second candidate). The other 13 modules had every candidate tried (517 in total). So "637" is 517 plus a 120 sample, and 168 manifest mutations were never tried until the final campaign.
2. **The original manifest result is not usable.** The original run reported 119 of 120 manifest mutations killed with one survivor, at line 161. In the preserved text of that run, the original line 161 reads with the conjunction, but every committed version of that line since `ee2f9133` has the disjunction. The campaign worktree therefore carried a mutation that had leaked from an earlier mutant (the original harness did not check the target file after each mutant). The reported mutant reproduced the committed source byte for byte, so it could never be killed, and the candidate count differed by one (289 instead of 288), which moved the sample onto the other half of the candidates. The 119 of 120 figure is retired. The sound manifest figures are in the final campaign.
3. **The other 12 modules were reproduced exactly.** The recorded rerun at the original test state gives the same per-module killed and survived counts as the original report for those modules (for example raw witness 254 killed and 8 survived, S25 raw evidence 40 and 16, reconcile 24 and 33, audit 49 and 3). Their original survivor lists are therefore sound.

## 3. Method and isolation rules

All campaign worktrees are separate detached `git worktree` directories under `E:\HarborOps-Verification\g17-s25-audit\`, one per concurrent shard, so no two mutants ever share a file. The tests use `mkdtemp` directories and no fixed ports, so the shards share no state. The final harness (`mutate-strict.mjs`, copied into the evidence folder) adds these rules:

1. An unmutated control run of the same test set must pass before and after every shard; otherwise the shard aborts. Every control in the final campaign passed (see `strict-control-summaries.txt`).
2. The hash of the target file is compared with the original after every mutant, and a shard reports any leak. Zero leaks were found.
3. A mutant counts as killed only when node exits with status 1, with no signal, no spawn error and no timeout, no infrastructure signature in the output (port, memory, file handle or module-resolution errors), and at least one named failing test.
4. A mutant whose run is abnormal is retried once, and if it is still abnormal it is reported as invalid and is never counted as a kill. A file-level failure with zero tests executed (the mutated module throws on import) is reported in its own category.

Operators: `===`/`!==`, `>=`, `<=`, `&&`/`||`, `true`/`false`, `.length>0`, `.length===N` and the numeric thresholds ` > N` and ` < N`. Candidate lines exclude comments and imports.

The first exhaustive manifest attempt was stopped by hand when this harness was written. Its four worktrees were found with an unreverted mutation, which confirmed the need for rule 2; they were restored with `git checkout` and checked clean before use.

First strict pass: 12 results were invalid, none counted as kills. Eight manifest timeouts (latency and alert guards, lines 237 to 247) coincided with a long wall-clock gap in the machine and were rerun on an awake machine, where all eight were killed by failing tests. Four were module-load failures (the mutated main guard makes the module throw its usage error on import): two in `reconcile-g17-assertions.mjs` (reproduction in `survivor-evidence/reconcile-main-guard-load-failure.txt`) and two in the manifest verifier. These are reported as the separate load-time category.

## 4. Campaigns and denominators

| Id | Campaign | Source and tests | Mutants tried | Killed | Survived | Note |
| --- | --- | --- | --- | --- | --- | --- |
| A | Original, as reported (preserved text) | original test state | 637 | 568 | 69 | manifest sampled 120 of 289 in a contaminated worktree; retired for the manifest |
| B | Recorded rerun, original test state (`486e4082`) | per-mutant records | 637 | 542 | 95 | 13 modules identical to A; manifest sample is the other half (27 survivors) |
| M | Manifest rerun at `c954edec` | per-mutant records | 120 | 101 | 19 | same sample half as B at a later test state |
| C | Recorded rerun after the first corrections (`71ff5cff`) | per-mutant records | 637 | 609 | 28 | 19 of the 28 are manifest field guards |
| R | Retest of the 28 against later test commits | targeted | 28 | 18 | 10 | before the exhaustive campaign existed |
| D | Final strict exhaustive campaign | every candidate, strict harness | 805 | 794 (790 behavioural, 4 at load) | 11 | the denominator of record |

Campaign D is 517 candidates in 13 modules plus 288 in the manifest verifier. Its first pass (at `0ff7d4a5`) left 3 survivors outside the manifest verifier and 27 inside it, plus 12 invalid results. Those 30 survivors were closed or classified as follows: 37 manifest candidates (the 27 survivors and the 10 invalid results) were rerun at `7aa5fca9`, which left 10 survivors; two of those (`c200`, `c204`, the P09 inclusive budgets) were then killed by a test added in `85b74032` and verified by direct runs. The final 11 survivors are 3 outside the manifest verifier and 8 inside it.

Identities are the module, the line and the mutated text. They are stable across campaigns because no product or verifier source file changed between `5b1dcc41` and the final commit (section 7).

The machine-readable record is `docs/m17-s25-mutation-evidence-20261115/`:

| File | Content |
| --- | --- |
| `final-strict-campaign-805.jsonl` | one line per candidate of campaign D, with first-pass status, retest status and killing tests |
| `survivors-classified.json` | every distinct surviving mutation, the campaigns it survived in, its final status, its classification and its evidence |
| `campaign-A-original-reported.txt` | the original text, copied unchanged |
| `campaign-B-original-state-637.jsonl`, `campaign-M-manifest-at-c954edec-120.jsonl`, `campaign-C-post-correction-637.jsonl` | complete per-mutant results of the recorded reruns |
| `strict-control-summaries.txt` | control runs and counters of every strict shard |
| `survivor-evidence/` | the executable probes and their output for every residual survivor |
| `mutate-strict.mjs`, `mutate-record.mjs`, `combine-final.mjs` | the harnesses and the reconciliation script |

The original raw outputs and the intermediate per-mutant files are preserved outside the repository in `E:\HarborOps-Verification\g17-s25-audit\preserved-20261115\original-mutation\` (with its hash file) and in the `mutation-*-record` folders next to it.

## 5. Classification of all 120 surviving mutations

Classification rules. **Material** means a reachable input existed that the original tests accepted although it should be refused; every such mutation is now killed by a named negative test. **Equivalent** means the mutation cannot change observable behaviour. **Unreachable** means no input reaches the changed branch. **Redundant** means the changed guard is never the check that rejects, because an earlier check rejects every such input; it is demonstrated by running the same one-fact input against the unmutated and the mutated verifier and comparing which check rejects.

Counts by campaign: of the 69 originally reported, 65 are material and corrected, 2 are equivalent, 1 is unreachable and 1 is the retired identity mutant. Of the 95 in the recorded original-state rerun, 89 are material and corrected, 2 equivalent, 1 unreachable and 3 redundant.

Notable corrections (all negative tests; no verifier or product source was changed):

| Weakness | Closed by |
| --- | --- |
| A Node witness under `web/tests/` (outside the approved roots) would have been accepted if its case name appeared in the JUnit file. Found while attempting to prove the mutation unreachable; it was reachable. | derivation test "a real Node test outside the supported witness roots cannot satisfy a witness even when its case ran" |
| Every per-report identity guard (source, project, result) of the complete-campaign verifier was untested one field at a time for most reports | table-driven single-fact tests for 14 reports (source, project and result each broken alone) and for the worker results |
| Manifest entry path, hash and size guards, the three required ledger files, the inventory fact guards, the command line usage guard | tests for each fact, including a private verifier copy beside an edited inventory |
| Inclusive P09 budgets (exactly 10 percent, exactly 15 percent, 3000 samples) | boundary acceptance and just-above rejection tests |
| P09 recalculation tolerance edge and non-array sample lists | tolerance edge test at exactly 0.00005; assertion type test |
| Retained trace events, links, state and status text, series edges, four-fact observation match, viewport and alert column, worker series label, storage and low-traffic edges | the S25 raw evidence cases added in the first pass |
| Raw witness predicates failing closed, storage tables and row hash edges, regression counts | the raw witness cases |
| Ledger count error messages when only one count is wrong | derivation tests that pin the message |
| Audit command line behaviour and import without side effects | audit command test |

### Individual classification

Every row is one distinct surviving mutation. The "Change" column shows the changed fragment with its surrounding text.

### audit-g17-test-coverage.mjs (3)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| audit-g17-test-coverage.mjs@c045 | 89 | `...n.size !== 75 \|\| matrix.length...` to `...n.size !== 75 && matrix.length...` | A, B, C, D | SURVIVED | Equivalent | seen.size and matrix.length move in lockstep: a duplicate id takes the continue branch before both seen.add and matrix.push, so the two counts are ... |
| audit-g17-test-coverage.mjs@c051 | 101 | `...cess.argv[1]) === fileURLToPa...` to `...cess.argv[1]) !== fileURLToPa...` | A, B | KILLED | Material, corrected | Test: the command runs its audit, writes its report and never overwrites it, while an import ... |
| audit-g17-test-coverage.mjs@c052 | 101 | `...ocess.argv[1] && path.resolve(...` to `...ocess.argv[1] \|\| path.resolve(...` | A, B | KILLED | Material, corrected | Test: the command runs its audit, writes its report and never overwrites it, while an import ... |

### g17-diagnostic-evidence.mjs (1)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-diagnostic-evidence.mjs@c004 | 16 | `...some(s=>s.name==="http.reques...` to `...some(s=>s.name!=="http.reques...` | A, B | KILLED | Material, corrected | Test: a trace with two spans but no http.request server span is rejected |

### g17-malformed-trace-evidence.mjs (1)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-malformed-trace-evidence.mjs@c004 | 9 | `...sert.ok(canary&&typeof canary=...` to `...sert.ok(canary\|\|typeof canary=...` | A, B, C | KILLED | Material, corrected | Test: a canary that is not a non-empty string is refused by the canary assertion itself, not ... |

### g17-p09-review.mjs (5)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-p09-review.mjs@c001 | 4 | `...sArray(values)&&values.length>...` to `...sArray(values)\|\|values.length>...` | A, B, C | KILLED | Material, corrected | Test: a sample list that is not an array is refused by an assertion, never by a crash or an a... |
| g17-p09-review.mjs@c004 | 11 | `...ual-expected)<=tolerance,` to `...ual-expected)<tolerance,` | A, B, C | KILLED | Material, corrected | Test: a recorded pair percentage is accepted at exactly the approved tolerance and refused ju... |
| g17-p09-review.mjs@c006 | 28 | `....rss_peak_mib)&&row.rss_peak_m...` to `....rss_peak_mib)\|\|row.rss_peak_m...` | A, B | KILLED | Material, corrected | Test: a negative peak memory reading is rejected even when every derived number is consistent... |
| g17-p09-review.mjs@c008 | 39 | `assert.ok(off&&on);` to `assert.ok(off\|\|on);` | A, B, C, D | SURVIVED | Unreachable | Six rows with rounds in {1,2,3} and booleans in {false,true} and no duplicate key cover all six round and mode pairs, so off and on always exist; d... |
| g17-p09-review.mjs@c010 | 72 | `....increase_pct<=10)&&meanMemor...` to `....increase_pct<10)&&meanMemor...` | A, B, C, D | SURVIVED | Equivalent | r-1 is exact for r in [1,2) and double(0.1) is not a multiple of 2^-52, so no ratio produces exactly 10 after the multiplication by 100: the neares... |

### g17-raw-witness.mjs (8)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-raw-witness.mjs@c196 | 185 | `...system?.cpus >= 1 &&` to `...system?.cpus > 1 &&` | A, B | KILLED | Material, corrected | Test: storage and regression predicates are exact at their edges and need each fact in every row |
| g17-raw-witness.mjs@c202 | 188 | `...emory_bytes > 0]` to `...emory_bytes > 1000]` | A, B, C | KILLED | Material, corrected | Test: the host memory size is only required to be a positive number |
| g17-raw-witness.mjs@c234 | 215 | `...s??{}).length>=8],` to `...s??{}).length>8],` | A, B | KILLED | Material, corrected | Test: storage and regression predicates are exact at their edges and need each fact in every row |
| g17-raw-witness.mjs@c245 | 226 | `...name]?.rows>0 &&` to `...name]?.rows>0 \|\|` | A, B | KILLED | Material, corrected | Test: storage and regression predicates are exact at their edges and need each fact in every row |
| g17-raw-witness.mjs@c248 | 232 | `...s.M13.passed >= 1 && r.cases....` to `...s.M13.passed > 1 && r.cases....` | A, B | KILLED | Material, corrected | Test: storage and regression predicates are exact at their edges and need each fact in every row |
| g17-raw-witness.mjs@c253 | 236 | `...audit.passed >= 1 && r.cases....` to `...audit.passed > 1 && r.cases....` | A, B | KILLED | Material, corrected | Test: storage and regression predicates are exact at their edges and need each fact in every row |
| g17-raw-witness.mjs@c260 | 247 | `...atch { return false; }` to `...atch { return true; }` | A, B | KILLED | Material, corrected | Test: a predicate that throws on a malformed report fails closed, and unknown cases have no p... |
| g17-raw-witness.mjs@c262 | 255 | `...oolean(table) && [...table.key...` to `...oolean(table) \|\| [...table.key...` | A, B | KILLED | Material, corrected | Test: a predicate that throws on a malformed report fails closed, and unknown cases have no p... |

### g17-results-contract.mjs (1)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-results-contract.mjs@c002 | 17 | `... === b.length && a.every((x,i)...` to `... === b.length \|\| a.every((x,i)...` | A, B | KILLED | Material, corrected | Test: a browser case renamed to another title with the same total count is rejected |

### g17-s25-raw-evidence.mjs (16)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| g17-s25-raw-evidence.mjs@c002 | 23 | `...events?.length&&!span.links?.l...` to `...events?.length\|\|!span.links?.l...` | A, B | KILLED | Material, corrected | Test: a retained trace with only one of events, links, state or status text is rejected |
| g17-s25-raw-evidence.mjs@c003 | 24 | `...pan.traceState&&!span.status?....` to `...pan.traceState\|\|!span.status?....` | A, B | KILLED | Material, corrected | Test: a retained trace with only one of events, links, state or status text is rejected |
| g17-s25-raw-evidence.mjs@c004 | 30 | `...sArray(series)&&series.length>...` to `...sArray(series)\|\|series.length>...` | A, B | KILLED | Material, corrected | Test: an empty, non-object or over-long exported series row is rejected and the 120 character... |
| g17-s25-raw-evidence.mjs@c007 | 32 | `assert.ok(row&&typeof row==="...` to `assert.ok(row\|\|typeof row==="...` | A, B | KILLED | Material, corrected | Test: an empty, non-object or over-long exported series row is rejected and the 120 character... |
| g17-s25-raw-evidence.mjs@c008 | 35 | `...value).length<=120,"Unbounded...` to `...value).length<120,"Unbounded...` | A, B | KILLED | Material, corrected | Test: an empty, non-object or over-long exported series row is rejected and the 120 character... |
| g17-s25-raw-evidence.mjs@c011 | 48 | `...stic.trace_id &&` to `...stic.trace_id \|\|` | A, B | KILLED | Material, corrected | Test: the page-originated observation must match the journey on every one of four facts |
| g17-s25-raw-evidence.mjs@c013 | 49 | `...r_correlation &&` to `...r_correlation \|\|` | A, B | KILLED | Material, corrected | Test: the page-originated observation must match the journey on every one of four facts |
| g17-s25-raw-evidence.mjs@c015 | 50 | `....status===503 && x.body_correl...` to `....status===503 \|\| x.body_correl...` | A, B | KILLED | Material, corrected | Test: the page-originated observation must match the journey on every one of four facts |
| g17-s25-raw-evidence.mjs@c019 | 62 | `...wport?.width>0&&display.viewpo...` to `...wport?.width>0\|\|display.viewpo...` | A, B | KILLED | Material, corrected | Test: each viewport dimension and a first-column alert name are validated |
| g17-s25-raw-evidence.mjs@c020 | 67 | `...ok(nameColumn>=0&&stateColumn...` to `...ok(nameColumn>0&&stateColumn...` | A, B | KILLED | Material, corrected | Test: each viewport dimension and a first-column alert name are validated |
| g17-s25-raw-evidence.mjs@c032 | 124 | `....__name__??"")&&` to `....__name__??"")\|\|` | A, B | KILLED | Material, corrected | Test: worker metric series need the worker metric name and the tracking worker label together |
| g17-s25-raw-evidence.mjs@c044 | 174 | `...e_age_seconds>=45,` to `...e_age_seconds>45,` | A, B | KILLED | Material, corrected | Test: storage degradation and recovery edges are inclusive and a failed recovery step is reje... |
| g17-s25-raw-evidence.mjs@c049 | 178 | `...s-after-fault"&&` to `...s-after-fault"\|\|` | A, B | KILLED | Material, corrected | Test: storage degradation and recovery edges are inclusive and a failed recovery step is reje... |
| g17-s25-raw-evidence.mjs@c053 | 207 | `....eligible_min>=1&&lowWindow.e...` to `....eligible_min>1&&lowWindow.e...` | A, B | KILLED | Material, corrected | Test: the below-threshold window accepts exactly its approved minimums |
| g17-s25-raw-evidence.mjs@c055 | 209 | `...igible_points>=4);` to `...igible_points>4);` | A, B | KILLED | Material, corrected | Test: the below-threshold window accepts exactly its approved minimums |
| g17-s25-raw-evidence.mjs@c056 | 212 | `...x_zero_points>=1,` to `...x_zero_points>1,` | A, B | KILLED | Material, corrected | Test: the below-threshold window accepts exactly its approved minimums |

### reconcile-g17-assertions.mjs (33)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| reconcile-g17-assertions.mjs@c005 | 21 | `...(group.id!==id\|\|group.assertio...` to `...(group.id!==id&&group.assertio...` | A, B | KILLED | Material, corrected | Test: a mislabeled scenario or an extra fourth clause is reported on its own |
| reconcile-g17-assertions.mjs@c012 | 30 | `...ENDENT_REVIEW"&&` to `...ENDENT_REVIEW"\|\|` | A, B | KILLED | Material, corrected | Test: the ledger states its own count error when only the independent count is wrong |
| reconcile-g17-assertions.mjs@c020 | 56 | `...al_passes!==72\|\|independent_pe...` to `...al_passes!==72&&independent_pe...` | A, B, C | KILLED | Material, corrected | Test: the ledger states its own count error when only the technical count is wrong |
| reconcile-g17-assertions.mjs@c026 | 105 | `...ult.source_sha===sha &&` to `...ult.source_sha!==sha &&` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c027 | 105 | `...rce_sha===sha &&` to `...rce_sha===sha \|\|` | A, B | KILLED | Material, corrected | Test: derivation refuses: raw report with another source sha |
| reconcile-g17-assertions.mjs@c028 | 106 | `result.project===runStatus.pr...` to `result.project!==runStatus.pr...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c029 | 106 | `...tatus.project && result.result...` to `...tatus.project \|\| result.result...` | A, B | KILLED | Material, corrected | Test: derivation refuses: raw report with another source sha |
| reconcile-g17-assertions.mjs@c030 | 111 | `x.stage==="promtool-te...` to `x.stage!=="promtool-te...` | A, B | KILLED | Material, corrected | Test: derivation refuses: promtool stage failed |
| reconcile-g17-assertions.mjs@c031 | 111 | `...romtool-test" && x.status==="P...` to `...romtool-test" \|\| x.status==="P...` | A, B | KILLED | Material, corrected | Test: derivation refuses: promtool stage failed |
| reconcile-g17-assertions.mjs@c032 | 117 | `...atus.run_state==="COMPLETE" &...` to `...atus.run_state!=="COMPLETE" &...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c033 | 117 | `...==="COMPLETE" &&` to `...==="COMPLETE" \|\|` | A, B | KILLED | Material, corrected | Test: derivation refuses: run state FAILED |
| reconcile-g17-assertions.mjs@c034 | 118 | `...lts.length>10 &&` to `...lts.length>10 \|\|` | A, B | KILLED | Material, corrected | Test: derivation refuses: run state FAILED |
| reconcile-g17-assertions.mjs@c035 | 119 | `...ry(x=>x.status==="PASS" && x....` to `...ry(x=>x.status!=="PASS" && x....` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c036 | 119 | `...atus==="PASS" && x.exit_code==...` to `...atus==="PASS" \|\| x.exit_code==...` | A, B | KILLED | Material, corrected | Test: derivation refuses: a stage failed |
| reconcile-g17-assertions.mjs@c037 | 127 | `x.stage===name&&x.stat...` to `x.stage!==name&&x.stat...` | A, B | KILLED | Material, corrected | Test: derivation refuses: required stage dotnet-build missing |
| reconcile-g17-assertions.mjs@c038 | 127 | `x.stage===name&&x.status==="PA...` to `x.stage===name\|\|x.status==="PA...` | A, B | KILLED | Material, corrected | Test: derivation refuses: required stage dotnet-build missing |
| reconcile-g17-assertions.mjs@c039 | 132 | `...With("tests/")\|\|` to `...With("tests/")&&` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c040 | 133 | `...th("scripts/")\|\|` to `...th("scripts/")&&` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c041 | 134 | `w.path==="monitoring/...` to `w.path!=="monitoring/...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c042 | 135 | `if(!supported\|\|!w.case\|\|!w.pa...` to `if(!supported&&!w.case\|\|!w.pa...` | A, B, C | KILLED | Material, corrected | Test: a real Node test outside the supported witness roots cannot satisfy a witness even when... |
| reconcile-g17-assertions.mjs@c043 | 144 | `...[0]+"\u0000") &&` to `...[0]+"\u0000") \|\|` | A, B | KILLED | Material, corrected | Test: ledger names the assertions affected by: a contract method missing from the contract TRX |
| reconcile-g17-assertions.mjs@c044 | 145 | `...thodName+"(") \|\|` to `...thodName+"(") &&` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c045 | 147 | `...n hits.length>=1 ? hits[0][1]...` to `...n hits.length>1 ? hits[0][1]...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c046 | 153 | `...an) ? {passed:true,artifact:fou...` to `...an) ? {passed:false,artifact:fou...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c047 | 157 | `if(w.path==="monitoring/...` to `if(w.path!=="monitoring/...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c048 | 159 | `if(w.path==="scripts/run...` to `if(w.path!=="scripts/run...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c049 | 163 | `return raw&&rawWitnessPass...` to `return raw\|\|rawWitnessPass...` | A, B | KILLED | Material, corrected | Test: ledger names the assertions affected by: a raw report failing its predicate |
| reconcile-g17-assertions.mjs@c050 | 164 | `?{passed:true,artifact:raw...` to `?{passed:false,artifact:raw...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c051 | 166 | `if(w.path==="scripts/ver...` to `if(w.path!=="scripts/ver...` | A, B | KILLED | Material, corrected | Test: the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending |
| reconcile-g17-assertions.mjs@c054 | 174 | `...(!evidenceRoot\|\|!/^[a-f0-9]{40...` to `...(!evidenceRoot&&!/^[a-f0-9]{40...` | A, B | KILLED | Material, corrected | Test: the command fails for no source sha |
| reconcile-g17-assertions.mjs@c055 | 177 | `...ual.source_sha===sha && actua...` to `...ual.source_sha!==sha && actua...` | A, B | KILLED | Material, corrected | Test: the command writes the ledger and reports 72 of 72 |
| reconcile-g17-assertions.mjs@c057 | 177 | `...ce_verified===true,` to `...ce_verified===false,` | A, B | KILLED | Material, corrected | Test: the command writes the ledger and reports 72 of 72 |
| reconcile-g17-assertions.mjs@c056 | 177 | `...rce_sha===sha && actual.source...` to `...rce_sha===sha \|\| actual.source...` | A, B | KILLED | Material, corrected | Test: the command fails for a source sha that differs from the run ledger |

### verify-g17-manifest.mjs (52)

| Id | Line | Change | Seen surviving in | Final | Classification | Closed by or reason |
| --- | --- | --- | --- | --- | --- | --- |
| verify-g17-manifest.mjs@c003 | 31 | `...version !== 2 \|\| manifest.gate...` to `...version !== 2 && manifest.gate...` | B | KILLED | Material, corrected | Test: a manifest header with a wrong schema version, gate or source is rejected one field at ... |
| verify-g17-manifest.mjs@c010 | 44 | `...observed.path \|\| claimed.sha25...` to `...observed.path && claimed.sha25...` | D | KILLED | Material, corrected | Test: a manifest entry that differs from the file in only its path, only its hash or only its... |
| verify-g17-manifest.mjs@c012 | 49 | `...e(e => e.path === name))` to `...e(e => e.path !== name))` | D | KILLED | Material, corrected | Test: each required run ledger file must exist even when the index agrees with the directory |
| verify-g17-manifest.mjs@c025 | 58 | `...st.source_sha \|\|` to `...st.source_sha &&` | B | KILLED | Material, corrected | Test: complete campaign header guard: requested source sha |
| verify-g17-manifest.mjs@c027 | 59 | `...NOT_ACCEPTED" \|\|` to `...NOT_ACCEPTED" &&` | B | KILLED | Material, corrected | Test: complete campaign header guard: gate accepted in the run status |
| verify-g17-manifest.mjs@c029 | 60 | `...NOT_REVIEWED" \|\|` to `...NOT_REVIEWED" &&` | B | KILLED | Material, corrected | Test: complete campaign header guard: assertions already reviewed in the run status |
| verify-g17-manifest.mjs@c033 | 63 | `...ed_artifacts) \|\|` to `...ed_artifacts) &&` | B | KILLED | Material, corrected | Test: complete campaign header guard: required artifacts not an array |
| verify-g17-manifest.mjs@c035 | 64 | `...!== "boolean" \|\|` to `...!== "boolean" &&` | B | KILLED | Material, corrected | Test: complete campaign header guard: full campaign flag not boolean |
| verify-g17-manifest.mjs@c043 | 76 | `...us !== "PASS" \|\| row.exit_code...` to `...us !== "PASS" && row.exit_code...` | B | KILLED | Material, corrected | Test: complete campaign header guard: stage with exit code 1 but PASS |
| verify-g17-manifest.mjs@c063 | 95 | `...rtions !== 75 \|\| technical.len...` to `...rtions !== 75 && technical.len...` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects: ledger claims 74 total assertions |
| verify-g17-manifest.mjs@c065 | 96 | `....length !== 3 \|\| new Set(ids)....` to `....length !== 3 && new Set(ids)....` | B | KILLED | Material, corrected | Test: complete campaign header guard: ledger duplicate id |
| verify-g17-manifest.mjs@c096 | 147 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | SURVIVED | Redundant | verifyS25RawSnapshots runs on the next statements and rejects a foreign source_sha in the privacy report first (g17-s25-raw-evidence.mjs line 13); ... |
| verify-g17-manifest.mjs@c098 | 148 | `...ected.project \|\|` to `...ected.project &&` | D | SURVIVED | Redundant | A foreign project in the privacy report is rejected by g17-s25-raw-evidence.mjs line 14 with the mutation applied. |
| verify-g17-manifest.mjs@c104 | 151 | `...s_inspected < 1 \|\|` to `...s_inspected < 0 \|\|` | D | SURVIVED | Redundant | Same fact and same shadowing check as the line 151 conjunction (g17-s25-raw-evidence.mjs line 81). |
| verify-g17-manifest.mjs@c103 | 151 | `...inspected < 1 \|\|` to `...inspected < 1 &&` | B, C, D, M | SURVIVED | Redundant | A zero exported metric series count is rejected by the raw series equality at g17-s25-raw-evidence.mjs line 81 with the mutation applied. |
| verify-g17-manifest.mjs@c105 | 152 | `...ots?.length < 2)` to `...ots?.length < 0)` | B, C, D, M | SURVIVED | Redundant | A snapshot list reduced to one entry is rejected by the digest count assertion at g17-s25-raw-evidence.mjs line 93 with the mutation applied. |
| verify-g17-manifest.mjs@c107 | 157 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-cardinality-memory-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c109 | 158 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-cardinality-memory-results.json with only project wrong |
| verify-g17-manifest.mjs@c111 | 159 | `...lt !== "PASS" \|\|` to `...lt !== "PASS" &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-cardinality-memory-results.json with only result wrong |
| verify-g17-manifest.mjs@c123 | 169 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-restart-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c125 | 170 | `...ected.project \|\| restart.resul...` to `...ected.project && restart.resul...` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-restart-results.json with only project wrong |
| verify-g17-manifest.mjs@c131 | 176 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-retention-age-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c133 | 177 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-retention-age-results.json with only project wrong |
| verify-g17-manifest.mjs@c144 | 185 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-tempo-retention-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c146 | 186 | `...ected.project \|\|` to `...ected.project &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-tempo-retention-results.json with only project wrong |
| verify-g17-manifest.mjs@c163 | 196 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-retention-matrix-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c165 | 197 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-retention-matrix-results.json with only project wrong |
| verify-g17-manifest.mjs@c174 | 207 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | KILLED | Material, corrected | Test: complete campaign rejects g17-regression-matrix-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c176 | 208 | `...ected.project \|\| regress.resul...` to `...ected.project && regress.resul...` | D | KILLED | Material, corrected | Test: complete campaign rejects g17-regression-matrix-results.json with only project wrong |
| verify-g17-manifest.mjs@c181 | 218 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, D, M | SURVIVED | Redundant | The performance report is loaded and its source_sha asserted by g17-s25-raw-evidence.mjs (load, line 13, called at line 191) before the manifest re... |
| verify-g17-manifest.mjs@c183 | 219 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects: performance run kind |
| verify-g17-manifest.mjs@c185 | 220 | `...ATCHED_PAIRS" \|\|` to `...ATCHED_PAIRS" &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-performance-raw.json with only result wrong |
| verify-g17-manifest.mjs@c200 | 229 | `...increase_pct <= 10) \|\|` to `...increase_pct < 10) \|\|` | D | KILLED | Material, corrected | killed by the inclusive-budget test (direct run, exit 1) |
| verify-g17-manifest.mjs@c202 | 230 | `...y_on_samples >= 3000)) \|\|` to `...y_on_samples > 3000)) \|\|` | D | KILLED | Material, corrected | Test: boundary values the campaign accepts: 3000 telemetry samples, exactly 10 percent p95 an... |
| verify-g17-manifest.mjs@c204 | 231 | `...increase_pct <= 15) \|\|` to `...increase_pct < 15) \|\|` | D | KILLED | Material, corrected | killed by the inclusive-budget test (direct run, exit 1) |
| verify-g17-manifest.mjs@c207 | 236 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-live-latency-alert-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c209 | 237 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-live-latency-alert-results.json with only project wrong |
| verify-g17-manifest.mjs@c218 | 248 | `...ected.project \|\|` to `...ected.project &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-alert-matrix-results.json with only project wrong |
| verify-g17-manifest.mjs@c224 | 251 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-pressure-matrix-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c226 | 252 | `...ected.project \|\|` to `...ected.project &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-pressure-matrix-results.json with only project wrong |
| verify-g17-manifest.mjs@c231 | 264 | `...st.source_sha \|\|` to `...st.source_sha &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-database-fault-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c233 | 265 | `...ected.project \|\|` to `...ected.project &&` | B, C, M | KILLED | Material, corrected | Test: complete campaign rejects m17-database-fault-results.json with only project wrong |
| verify-g17-manifest.mjs@c244 | 275 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-collector-fault-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c246 | 276 | `...ected.project \|\| fault.result ...` to `...ected.project && fault.result ...` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-collector-fault-results.json with only project wrong |
| verify-g17-manifest.mjs@c254 | 285 | `...st.source_sha \|\|` to `...st.source_sha &&` | D | SURVIVED | Redundant | The worker journey report is loaded through the same asserting loader (g17-s25-raw-evidence.mjs line 13, called at line 112) before line 285; unmut... |
| verify-g17-manifest.mjs@c256 | 286 | `...ected.project \|\|` to `...ected.project &&` | D | SURVIVED | Redundant | The same loader asserts the project (g17-s25-raw-evidence.mjs line 14); unmutated and mutated runs are rejected by the same check. |
| verify-g17-manifest.mjs@c269 | 296 | `...lt !== "PASS" \|\| worker.source...` to `...lt !== "PASS" && worker.source...` | D | KILLED | Material, corrected | Test: complete campaign rejects m17-worker-results.json with only source_sha wrong |
| verify-g17-manifest.mjs@c276 | 310 | `...length !== 25 \|\| assertions.le...` to `...length !== 25 && assertions.le...` | D | KILLED | Material, corrected | Test: an inventory with 24 scenarios holding all 75 assertions is refused on that fact alone |
| verify-g17-manifest.mjs@c282 | 313 | `...NOT_ACCEPTED" \|\|` to `...NOT_ACCEPTED" &&` | D | KILLED | Material, corrected | Test: an inventory with inventory marked accepted is refused on that fact alone |
| verify-g17-manifest.mjs@c284 | 314 | `...NOT_EXECUTED" \|\| a.witness !==...` to `...NOT_EXECUTED" && a.witness !==...` | D | KILLED | Material, corrected | Test: an inventory with one assertion already executed is refused on that fact alone |
| verify-g17-manifest.mjs@c288 | 325 | `if (!manifest \|\| !sha \|\| !/^[a...` to `if (!manifest && !sha \|\| !/^[a...` | D | KILLED | Material, corrected | Test: the command line verifies a complete campaign, and refuses a missing manifest path or S... |
| A-manifest-L161 | 161 | `...ths !== 40000 && memory.rows?....` to `...ths !== 40000 \|\| memory.rows?....` | A | NOT_APPLICABLE | Identity mutant (contaminated baseline) | The campaign A worktree carried a leaked mutation: its line 161 read with the conjunction while every committed version since ee2f9133 has the disj... |



## 6. Evidence for the 11 final survivors

| Survivor | Class | Evidence |
| --- | --- | --- |
| `audit-g17-test-coverage.mjs` line 89 | Equivalent | A duplicate identifier takes the `continue` branch before both `seen.add` and `matrix.push`, so the two counts are always equal and `a || b` equals `a && b`. Five structural faults give identical error lists with and without the mutation (`survivor-evidence/equivalence-probes.out.json`). |
| `g17-p09-review.mjs` line 39 (`off && on`) | Unreachable | Six rows with rounds in 1 to 3 and a boolean mode, no duplicates, cover all six pairs by pigeonhole, and a repeated pair is refused earlier with "Duplicate paired mode" (same probe file). |
| `g17-p09-review.mjs` line 72 (`<= 10` to `< 10`) | Equivalent | See below. |
| `verify-g17-manifest.mjs` line 218 (survivor #091) | Redundant | See below. |
| `verify-g17-manifest.mjs` lines 147, 148, 151 (two mutations) and 152 | Redundant | The next statement runs `verifyS25RawSnapshots`, which rejects the same input (source line 13, project line 14, series equality line 81, digest count line 93). Probe output shows the check that rejects with and without the mutation. |
| `verify-g17-manifest.mjs` lines 285 and 286 | Redundant | The worker journey report is loaded through the same asserting loader at `g17-s25-raw-evidence.mjs` line 112, which rejects first in both runs. |

### Survivor #091 (performance report source guard, line 218)

The condition `overhead.source_sha !== manifest.source_sha || ...` was changed to use `&&` after the first term. A performance report that differs from the manifest only in `source_sha` must be refused. The probe builds the complete synthetic campaign, changes only that field, and runs `verifyManifest` against the unmutated and the mutated source in separate processes. Both are rejected with the same message, "Raw privacy source SHA mismatch", at the same location, `g17-s25-raw-evidence.mjs:13` called from `:191`. `verifyS25RawSnapshots` loads `m17-performance-raw.json` through its asserting loader before the manifest reaches line 218, so for a complete campaign line 218 never rejects a source mismatch by itself. The guard is left unchanged, because removing or reordering verifier code is outside this work. Its other conditions are not shadowed and are protected by their own tests: the project, the run kind and the result of the same report are each killed independently (the neighbouring mutations 092 and 093 are closed).

### The P09 floating point boundary (line 72 and the matching predicates)

The gate computes `pct = 100 * (after / before - 1)` and passes when `pct <= 10`. Changing it to `pct < 10` differs only at an increase of exactly 10. For ratios between 1 and 2, `after / before - 1` is exact in binary, and the double nearest 0.1 has an odd numerator over 2^55, so it is not a multiple of 2^-52 and no ratio can yield exactly 0.1. The nearest representable results after multiplying by 100 are 9.999999999999986 and 10.000000000000009 (`survivor-evidence/p09-float-boundary.out.txt`). A scan of 72,018 numerator and denominator pairs near 1.1 found none that give exactly 10. The two forms are therefore equivalent for every computable increase.

This also documents an earlier observation: a mathematically exact 10 percent increase (for example 11 against 10) evaluates to 10.000000000000009 and is judged to exceed the budget. That is fail-closed, real measurements never land on it, and it is left unchanged and pinned by a test. By contrast, the recorded values checked by the manifest verifier (`p95_increase_pct <= 10`, `mean_memory_increase_pct <= 15`) are data from a file and can be exactly 10 or 15, so they are reachable; those two mutations were material and are killed by the inclusive budget test.

## 7. Verification of the final state

| Check | Result |
| --- | --- |
| G17 Node suite, the runner's 21 files | 357 test cases, exit 0, 0 failures, 0 skipped, empty stderr (355 before the Tempo window fix was brought onto this branch, which added two cases) |
| Milestone Node tests outside the G17 runner (M7, M8, M9, G4, DCSA) | 42 of 42 pass (they are not part of the G17 candidate and were run as a regression check) |
| Strict coverage preflight | 75 assertions, 0 errors, 72 individually authored technical witnesses |
| f7 evidence, ledger re-derived with the final verifier code | 72 of 72 technical witnesses, SHA-256 identical to the preserved ledger (`8d0342fc...da22`) |
| f7 manifest verification in place, final verifier code | complete, 54 files, no missing artifact, gate NOT_ACCEPTED, 0 assertions reviewed |
| f6 and f7 preserved hash lists | f6: 67 of 67 match; f7: 55 of 55 present files match (the one missing listed file is the owner's archive, moved to `preserved-20261115\` with an unchanged hash) |
| Files changed since the tested source `5b1dcc41` | tests, test fixtures, the node test list in `scripts/run-g17-local.ps1`, documentation and the evidence copy, plus one verification-stage fix added later the same day: `tempoSearchWindow` in `scripts/g17-malformed-trace-evidence.mjs`, used by `scripts/check-g17-malformed-export.mjs` (commit `1374779f`; see the [controlled P09 results](M17_G17_S25_P09_CONTROLLED_VERIFICATION_RESULTS_20261115.md)). No product code, other verifier, monitoring rule or dashboard changed. The mutation campaigns above ran before that fix; the changed module was mutation tested separately afterwards (15 of 15 candidates killed) |

Therefore the f7 campaign evidence remains evidence for source `5b1dcc41`. The tests added after f7 were run locally and in the mutation campaigns described here; they were not part of the f7 run. No full campaign was repeated, and none was requested: no executable code under test changed. No performance measurement was run while any mutation job was running; the P09 runs are the f6 and f7 records only.

## 8. Limits and open items

1. **Scope of the mutation tool.** Twelve operators were applied to 14 Node evidence-gating modules. Surviving classes outside that scope (other operators, the .NET tests, the PowerShell runner, Prometheus rules, browser specs) are not assessed by this work. The campaign shows that the evidence gates themselves are not weakened at the tested operator level; it does not prove the absence of other defects.
2. **Redundant guards are kept.** Eight manifest guards are shadowed by an earlier check in the complete flow. They were not removed because they cost nothing and protect a verifier used on its own; they carry no unique protection today.
3. **P09 remains thin.** See `M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md`. The recommendation for additional controlled verification stands and needs an owner decision.
4. **Independent review.** S25-F06 (the independent reviewer) and the independent A01, A02 and A03 assertions remain pending. G17 remains NOT ACCEPTED.
5. **One historical figure is retired, not rewritten.** The original manifest result (119 of 120) is preserved in `campaign-A-original-reported.txt` and marked unusable above.

## 9. Reproduction

```
node docs/m17-s25-mutation-evidence-20261115/mutate-strict.mjs <worktree> scripts/<module>.mjs <comma separated test files> <shard count> <shard index> <out.jsonl>
node docs/m17-s25-mutation-evidence-20261115/combine-final.mjs
```

The harness expects an isolated worktree per shard, restores the target after every mutant and aborts if its control run fails. The paths inside `combine-final.mjs` name the evidence drive of the verifying machine.
