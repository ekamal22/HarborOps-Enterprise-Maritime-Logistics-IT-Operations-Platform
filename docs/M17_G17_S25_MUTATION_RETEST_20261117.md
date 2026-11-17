# G17-S25 mutation non-kill retest and classification

**Status: G17 NOT ACCEPTED. Independent S25-A01 engineering obligations are NOT yet closed (see section 7). Independent S25-A02, S25-A03 and S25-F06 sign-off remains separate and unsigned.**

This record answers the independent review remark that 17 distinct mutation candidates had no recorded killing run and no individual classification. It does not change any approved threshold, does not alter the preserved m3 performance failure or the existing QUALIFIED PASS decision, and no GitHub Actions workflow was run.

## 1. Sources and authority

| Item | Value |
| --- | --- |
| Frozen source retested | `634053c4d0c3a2cb8c51b2c29fda87b126e08993` (the m4 source), extracted with `git archive` into a disposable directory per mutant |
| Candidate source with added negative controls | `60576b16ec43cfa99472bbeecee7f9ee673a8d24` on the review branch (tests only, see section 5) |
| Inputs | `g17-m-mutation-and-control-evidence.zip` (SHA-256 `e95a906d5486f8f1cf4a9b5abc3c634c91708958d6aec236e6c2efae3296e5e2`, 187 files), `g17-m4-evidence.zip` (`0a5324bcb33987fde1a565684ea65e356af81c44d34ff85f4d554f0c10098d0b`), `g17-m-run-logs.zip` (`b679be2e4d8672d85dc15d93ec9c4eac2362db8ec448b985e8ecbacb9fb0aca3`), plus the m1, m2 and k1 to k3 failed attempt archives (hashes verified against their `.sha256` files where one was supplied) |
| Environment | Linux 6.18 container, Node 22.23.4, PowerShell 7.4.6, Docker 29.9.2 (real daemon, real containers and networks), bsdtar 3.7.2 standing in for the Windows `tar.exe` through `SystemRoot`. No Windows host was available |

## 2. Independent reconstruction of the 17 candidates

`records/reconstruction-from-raw-jsonl.json` was produced by a new script (`tooling/reconstruct.mjs`) that reads only the `*-N.jsonl` files and does not read `survivor-status-derived.*`.

- 369 mutation rows over 248 distinct (family, name) mutants; 61 non-kill rows plus 2 voided control rows (planning rounds 1 and 2, `VOID_CONTROL_FAILED`) give the 63 non-kill rows of the generated summary.
- 17 distinct mutants never have a KILLED row under their own name: reconcile binding 2, evidence archive 3, Docker ownership 6, diagnostic producer 2, diagnostic verifier 2, witness binding 2. Names and families agree with the generated summary.
- Locator status in the raw files: PATTERN_NOT_FOUND occurs for `promtool binding bypassed` and `manifest rejection binding bypassed` (round 1 only), `existing evidence directory overwritten` (its only row) and `bom not stripped` (both rounds). A mutation that was never applied is not counted as killed or equivalent anywhere in this record.
- Also disclosed, outside the 17: `dotnet-1.jsonl` and `dotnet-3.jsonl` each end with `{"control_after":"FAIL"}`. A separate unmutated control after those runs passed (`control-logs/readiness-control-3.log`, 12 of 12, and `control-after-1.log`, 70 of 70), and the readiness mutants themselves are recorded KILLED in `dotnet-3`. Infrastructure failures in `dotnet-1` were never counted as kills.

### Why the earlier rows differ from the retest

The mutation rounds ran against working tree states that preceded the frozen source. Two facts were established from history, nothing more is claimed:
- The ledger tests that kill M01 and M02 were added in `cc80be0` after the contract-reconcile rounds. They survived then and are killed now.
- The BOM strip in `scripts/g17-diagnostic-hash-list.mjs` was introduced in `ca14bcf`. The verifier rounds that reported `bom not stripped` as PATTERN_NOT_FOUND ran before it existed. On the frozen source the original locator occurs exactly once.
- The old harness decides KILLED only when it finds the spec reporter text `fail N`. Under the TAP reporter of this host it reports INFRASTRUCTURE_FAILURE for a mutant that is in fact killed (`records/original-harness-reproduction/`). The retest harness reads the TAP records itself, requires AssertionError for a kill, and treats exit with any other error name, a timeout or a signal as infrastructure.

## 3. Method of the retest

`tooling/retest.mjs`, one disposable copy of the exact frozen tree per mutant:
1. Unmutated control must exit 0 with no signal or timeout.
2. The locator must occur exactly once (or the stated occurrence of a duplicated pattern); otherwise the row is PATTERN_NOT_FOUND_OR_AMBIGUOUS and is not tested.
3. Source hash, mutated hash, command, exit code, signal, timeout flag, test counts, failing test names and raw output are recorded for the mutated run.
4. The file is restored, its hash is compared with the original, and the unmutated control must pass again.

Tests that need Windows (`where.exe`, `tar.exe`, `Win32_Process`) are separated: the ownership tests that drive a Windows-only docker front (3 of 12) and the diagnostic producer tests fail in the unmutated control on this host. They are never counted. For the archive tests `SystemRoot` points to a directory whose `System32/tar.exe` is bsdtar (same libarchive writer); all 8 archive tests pass unmutated.

## 4. Per-mutant classification (all 17, plus the second occurrence of one pattern)

Each row links to the raw retest output in `docs/m17-s25-mutation-retest-20261117/`. Source and mutated hashes are SHA-256 of the whole target file (first 16 hex digits; full values in `classification.json`).

| ID | Family | Mutant | Frozen source 634053c4 | Candidate source | Final disposition |
| --- | --- | --- | --- | --- | --- |
| M01 | reconcile binding | promtool binding bypassed | KILLED (exit 1, 1 failing) | n/a | **KILLED_ON_FROZEN** |
| M02 | reconcile binding | manifest rejection binding bypassed | KILLED (exit 1, 1 failing) | n/a | **KILLED_ON_FROZEN** |
| M03 | evidence archive | extracted files not compared by size | SURVIVED (exit 0) | n/a | **REDUNDANT_VERDICT_EQUIVALENT** |
| M04 | evidence archive | extra extracted files tolerated | SURVIVED (exit 0) | n/a | **REDUNDANT_DEFENCE_IN_DEPTH** |
| M05 | evidence archive | unextracted files tolerated | SURVIVED (exit 0) | n/a | **REDUNDANT_VERDICT_EQUIVALENT** |
| M06 | docker ownership | project label not compared (container) | SURVIVED (exit 0) | SURVIVED (exit 0) | **UNREACHABLE_GUARD** |
| M07 | docker ownership | network label project not compared | SURVIVED (exit 0) | SURVIVED (exit 0) | **UNREACHABLE_GUARD** |
| M08 | docker ownership | residual resources tolerated | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M09 | docker ownership | address range release not proven | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M10 | docker ownership | volumes removed with containers | KILLED (exit 1, 1 failing) | KILLED (exit 1, 2 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M11 | docker ownership | existing evidence directory overwritten | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M12 | diagnostic producer | exit of children not awaited | control failed here (exit 1) | n/a | **OPEN_PLATFORM_BLOCKED** |
| M13 | diagnostic producer | lingering child tolerated | control failed here (exit 1) | n/a | **OPEN_PLATFORM_BLOCKED** |
| M14 | diagnostic verifier | stream names accepted | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M14b | diagnostic verifier | stream names accepted (second position) | KILLED (exit 1, 1 failing) | KILLED (exit 1, 1 failing) | **KILLED_ON_FROZEN** |
| M15 | diagnostic verifier | bom not stripped | KILLED (exit 1, 16 failing) | n/a | **KILLED_ON_FROZEN** |
| M16 | witness binding | marker may be non-comment text | SURVIVED (exit 0) | KILLED (exit 1, 2 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M17 | witness binding | block start not required | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |


#### M01 promtool binding bypassed (reconcile binding)

- Target: `scripts/reconcile-g17-assertions.mjs`; original SHA-256 `d3288e317aa77020`, mutated `ea6ef983d892dd6f`; locator occurrences in the source: 1.
- Mutation: replace `return alertCaseBound(original,w.case)?results.get(w.path+"\u0000*")??null:null;` with `return results.get(w.path+"\u0000*")??null;`.
- Command: `node --test scripts/g17-first-failure-ledger.test.mjs scripts/g17-reconcile-derivation.test.mjs scripts/reconcile-g17-assertions.test.mjs`
- Frozen source: exit 1, signal none, timeout no; 63/64 tests passed; control before exit 0 (64/64), control after exit 0 (64/64), restored hash matches. Failing assertion: "a promtool witness whose text appears only outside a real case comment is unresolved" (AssertionError: Expected values to be strictly equal: | true !== false at g17-first-failure-ledger.test.mjs:80)
- Raw output: `frozen-run-1/M01.*.out.txt`.
- **Disposition KILLED_ON_FROZEN.** The ledger test a promtool witness whose text appears only outside a real case comment is unresolved (scripts/g17-first-failure-ledger.test.mjs) fails with an AssertionError when the alertCaseBound gate is bypassed. The earlier SURVIVED rows (contract-reconcile-2) were produced before this test existed: it was added in cc80be0 (2026-11-17 00:26 local commit time), after that round.
- Demonstration: retest/frozen: 64 of 64 control, mutant exit 1 with 1 failing test, 64 of 64 control after.

#### M02 manifest rejection binding bypassed (reconcile binding)

- Target: `scripts/reconcile-g17-assertions.mjs`; original SHA-256 `d3288e317aa77020`, mutated `5cc9f07d1fa370f6`; locator occurrences in the source: 1.
- Mutation: replace `return manifestRejectionBound(node)?results.get(w.path+"\u0000*")??null:null;` with `return results.get(w.path+"\u0000*")??null;`.
- Command: `node --test scripts/g17-first-failure-ledger.test.mjs scripts/g17-reconcile-derivation.test.mjs scripts/reconcile-g17-assertions.test.mjs`
- Frozen source: exit 1, signal none, timeout no; 63/64 tests passed; control before exit 0 (64/64), control after exit 0 (64/64), restored hash matches. Failing assertion: "the manifest-rejection witness is unresolved when any of its three Node cases did not execute" (AssertionError: fake complete claim without required raw result is rejected | true !== false at g17-first-failure-ledger.test.mjs:69)
- Raw output: `frozen-run-1/M02.*.out.txt`.
- **Disposition KILLED_ON_FROZEN.** The ledger test the manifest-rejection witness is unresolved when any of its three Node cases did not execute fails when the manifestRejectionBound gate is bypassed. Same explanation as M01: the test post-dates the earlier round.
- Demonstration: retest/frozen: 64 of 64 control, mutant exit 1 with 1 failing test.

#### M03 extracted files not compared by size (evidence archive)

- Target: `scripts/g17-evidence-archive.mjs`; original SHA-256 `59f002ea1bfafc68`, mutated `152a4bdfc24ebd7a`; locator occurrences in the source: 1.
- Mutation: replace `else if (found.bytes !== file.bytes) problems.push` with `else if (false) problems.push`.
- Command: `node --test scripts/g17-evidence-archive.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 8/8 tests passed; control before exit 0 (8/8), control after exit 0 (8/8), restored hash matches.
- Raw output: `frozen-run-1/M03.*.out.txt`.
- **Disposition REDUNDANT_VERDICT_EQUIVALENT.** The next branch of the same else-if chain compares the SHA-256 of the extracted file. Two byte strings of different length cannot have the same SHA-256 (collision resistance), so a size difference is always reported by the hash branch. The verdict (ok false) is identical for every input; only the wording of the problem changes. The earlier layer (entry size from the zip table against the inventory) also reports it.
- Demonstration: proofs/archive-demo.json: five honest tamper cases and three simulated extractor failures (extra file, skipped file, truncated file); the M03 verdict equals the original verdict in all eight.

#### M04 extra extracted files tolerated (evidence archive)

- Target: `scripts/g17-evidence-archive.mjs`; original SHA-256 `59f002ea1bfafc68`, mutated `e64a6629f5f06327`; locator occurrences in the source: 1.
- Mutation: replace `for (const name of actualMap.keys()) if (!expected.has(name)) problems.push` with `for (const name of actualMap.keys()) if (false) problems.push`.
- Command: `node --test scripts/g17-evidence-archive.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 8/8 tests passed; control before exit 0 (8/8), control after exit 0 (8/8), restored hash matches.
- Raw output: `frozen-run-1/M04.*.out.txt`.
- **Disposition REDUNDANT_DEFENCE_IN_DEPTH.** Every file the standard reader (System.IO.Compression ExtractToDirectory) writes corresponds to an entry of the zip table, and the entry loop already reports any entry that is not in the inventory. The guard can only fire if the extractor writes a file that has no entry. Residual: under a simulated misbehaving extractor that plants an extra file the mutant accepts the archive (verdict differs); no input that the standard reader produces reaches this difference. No executable change was made. This is a disclosed non-material residual for S25-A02.
- Demonstration: proofs/archive-demo.json: honest reader cases identical; simulated extra file: original false, M04 true.

#### M05 unextracted files tolerated (evidence archive)

- Target: `scripts/g17-evidence-archive.mjs`; original SHA-256 `59f002ea1bfafc68`, mutated `e9db48b37d18701a`; locator occurrences in the source: 1.
- Mutation: replace `if (!found) problems.push('not extracted` with `if (false) problems.push('not extracted`.
- Command: `node --test scripts/g17-evidence-archive.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 8/8 tests passed; control before exit 0 (8/8), control after exit 0 (8/8), restored hash matches.
- Raw output: `frozen-run-1/M05.*.out.txt`.
- **Disposition REDUNDANT_VERDICT_EQUIVALENT.** With the guard disabled the else-if chain dereferences found.bytes on an undefined value, which throws a TypeError that the surrounding catch converts into a problem. The verdict is therefore still ok false for a file that was not extracted. The entry-table check inventory file missing from the archive reports the same condition earlier.
- Demonstration: proofs/archive-demo.json: simulated skipped file: original false, M05 false.

#### M06 project label not compared (container) (docker ownership)

- Target: `scripts/g17-docker-ownership.ps1`; original SHA-256 `f41a69207b2e156d`, mutated `c7c1701c97ee9002`; locator occurrences in the source: 1.
- Mutation: replace `if ($labelProject -cne $Project) { $problems.Add("container $name carries project label` with `if ($false) { $problems.Add("container $name carries project label`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 9/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches.
- Candidate source: exit 0 (survived), signal none, timeout no; 1/1 tests passed; control before exit 0 (1/1), control after exit 0 (1/1), restored hash matches.
- Raw output: `frozen-run-3-broad/M06.*.out.txt`, `candidate-run/M06.*.out.txt`.
- **Disposition UNREACHABLE_GUARD.** Containers are listed with docker ps --filter label=com.docker.compose.project=<Project>. The daemon matches the label value exactly, so every returned container carries exactly Project and the in-script comparison cannot be false. The real daemon (Docker 29.9.2) was shown to return only the exact value and not the case, whitespace, prefix, suffix or substring variants.
- Demonstration: proofs/label-filter-demo.out.txt and proofs/own-label-demo.json: near-miss decoys never reach the guard; original and mutant return identical inventories.

#### M07 network label project not compared (docker ownership)

- Target: `scripts/g17-docker-ownership.ps1`; original SHA-256 `f41a69207b2e156d`, mutated `72b874bffcf070ea`; locator occurrences in the source: 1.
- Mutation: replace `if ($labelProject -cne $Project) { $problems.Add("network` with `if ($false) { $problems.Add("network`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 9/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches.
- Candidate source: exit 0 (survived), signal none, timeout no; 1/1 tests passed; control before exit 0 (1/1), control after exit 0 (1/1), restored hash matches.
- Raw output: `frozen-run-3-broad/M07.*.out.txt`, `candidate-run/M07.*.out.txt`.
- **Disposition UNREACHABLE_GUARD.** Same as M06 for docker network ls --filter label=com.docker.compose.project=<Project>.
- Demonstration: proofs/label-filter-demo.out.txt and proofs/own-label-demo.json.

#### M08 residual resources tolerated (docker ownership)

- Target: `scripts/g17-docker-ownership.ps1`; original SHA-256 `f41a69207b2e156d`, mutated `b5e04e59c8275ad9`; locator occurrences in the source: 1.
- Mutation: replace `if ($after.Containers.Count -ne 0 -or $after.Networks.Count -ne 0) { throw` with `if ($false) { throw`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 9/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 11/12 tests passed; control before exit 0 (12/12), control after exit 0 (12/12), restored hash matches. Failing assertion: "containers that docker reports as removed but that remain make the cleanup fail instead of reporting success" (AssertionError: Removed 2 container(s) and 1 network(s) of ho-verify-m17-selftest-fa2dd257-lierm; 0 volume(s) retained. at g17-docker-ownership.test.mjs:365)
- Raw output: `frozen-run-3-broad/M08.*.out.txt`, `candidate-run/M08.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** Real defect class: containers that docker reports as removed but that remain (or that appear again) were reported as success. Nothing in the frozen tests exercised the post-removal re-enumeration. New behavioural control: containers that docker reports as removed but that remain make the cleanup fail instead of reporting success.
- Demonstration: candidate: control 12 of 12, mutant exit 1 on the new test, control after 12 of 12.

#### M09 address range release not proven (docker ownership)

- Target: `scripts/g17-docker-ownership.ps1`; original SHA-256 `f41a69207b2e156d`, mutated `5d827c8459803052`; locator occurrences in the source: 1.
- Mutation: replace `if ($held.Count -gt 0) { throw` with `if ($false) { throw`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 9/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 11/12 tests passed; control before exit 0 (12/12), control after exit 0 (12/12), restored hash matches. Failing assertion: "an address range taken by another network right after the removal fails the cleanup and names the holder" (AssertionError: Removed 3 container(s) and 1 network(s) of ho-verify-m17-selftest-5b3ed5ff-heldafter; 1 volume(s) retained. at g17-docker-ownership.test.mjs:385)
- Raw output: `frozen-run-3-broad/M09.*.out.txt`, `candidate-run/M09.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** Real defect class: the address range being taken by another network right after removal went unreported. The frozen tests only exercised Test-G17SubnetFree separately. New behavioural control: an address range taken by another network right after the removal fails the cleanup and names the holder.
- Demonstration: candidate: control 12 of 12, mutant exit 1 on the new test, control after 12 of 12.

#### M10 volumes removed with containers (docker ownership)

- Target: `scripts/g17-docker-ownership.ps1`; original SHA-256 `f41a69207b2e156d`, mutated `e1dd8498ebe5b38b`; locator occurrences in the source: 1.
- Mutation: replace `Invoke-G17Docker @('rm', $container.Id)` with `Invoke-G17Docker @('rm', '-v', $container.Id)`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 1, signal none, timeout no; 8/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches. Failing assertion: "the runner preserves before it removes, removes only through the ownership checks, and never prunes" (AssertionError: 'container removal never uses -v')
- Candidate source: exit 1, signal none, timeout no; 10/12 tests passed; control before exit 0 (12/12), control after exit 0 (12/12), restored hash matches. Failing assertion: "the runner preserves before it removes, removes only through the ownership checks, and never prunes" (AssertionError: 'container removal never uses -v'); "a volume that docker created for a container is retained when the container is removed" (AssertionError: removing the container never removes its volume, anonymous or named | false !== true at g17-docker-ownership.test.mjs:411)
- Raw output: `frozen-run-3-broad/M10.*.out.txt`, `candidate-run/M10.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** On the frozen source the mutant is detected only by a source-text assertion (the runner preserves before it removes test greps for rm -v). That is not a behavioural detection: only named volumes were ever created and docker rm -v does not remove named volumes. New behavioural control: a container with a docker-created anonymous volume is cleaned up and the volume must still exist.
- Demonstration: candidate: control 12 of 12, mutant exit 1 with both the textual test and the new anonymous volume test failing.

#### M11 existing evidence directory overwritten (docker ownership)

- Target: `scripts/cleanup-g17-owned.ps1`; original SHA-256 `c2c901e15b24bf7a`, mutated `d2a89b438d1ca25e`; locator occurrences in the source: 1.
- Mutation: replace `if (Test-Path -LiteralPath $evidencePath) { throw 'The cleanup evidence directory already exists. Never overwrite evidence' }` with `(nothing)`.
- Command: `node --test --test-name-pattern=finished stack|running stack|partially initialised|ambiguous ownership|another project|project names|already exists|held address|runner preserves scripts/g17-docker-ownership.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 9/9 tests passed; control before exit 0 (9/9), control after exit 0 (9/9), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 11/12 tests passed; control before exit 0 (12/12), control after exit 0 (12/12), restored hash matches. Failing assertion: "an evidence directory that already exists is refused (never overwrite evidence)" (AssertionError: The input did not match the regular expression /Never overwrite evidence/. Input: | '\n' + | '\x1B[31;1mNew-Item: \x1B[0m/tmp/claude-0/mut/retest/out-new/work-M11/scripts/cleanup-g17-owned.ps1:16\x1B[ at g17-docker-ownership.test.mjs:233)
- Raw output: `frozen-run-3-broad/M11.*.out.txt`, `candidate-run/M11.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** Locator defect in the earlier round: the guard is in scripts/cleanup-g17-owned.ps1 while the mutation config targeted g17-docker-ownership.ps1, so the pattern was never applied. Applied to the right file the mutant survived on the frozen source because the test only matched already exists, which the generic New-Item error also contains. Behaviour is verdict-equivalent (New-Item refuses an existing directory, nothing is written, exit 1), but the refusal is the script's own documented message. The test now asserts the Never overwrite evidence text and that no record is written.
- Demonstration: proofs/m11-m17-demo.json: original and mutant both exit 1 with the evidence directory byte-identical; the error source differs (explicit throw versus New-Item). candidate: mutant exit 1 on the tightened test.

#### M12 exit of children not awaited (diagnostic producer)

- Target: `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1`; original SHA-256 `a21e50774122d523`, mutated `eed67a295480a723`; locator occurrences in the source: 1.
- Mutation: replace `try { Wait-Process -Id $id -Timeout 10 -ErrorAction Stop } catch [System.TimeoutException] { throw "Child $kind (process $id) did not exit within 10 seconds." } catch { }` with `(nothing)`.
- Command: `node --test --test-name-pattern=owned run stops only scripts/g17-diagnostic-producer-guard.test.mjs`
- Frozen source: the unmutated control already fails on this host (exit 1; 0/1 passed, the stand-in process is not visible because Get-CimInstance is unavailable), so the mutant is NOT counted as tested
- Raw output: `frozen-run-1/M12.*.out.txt`.
- **Disposition OPEN_PLATFORM_BLOCKED.** The producer reads process identity through Get-CimInstance Win32_Process, which does not exist on this Linux host. The control (1 test) fails before any mutation (exit 1) so the mutant was not counted. The existing test cannot tell the mutant apart on Windows because Stop-Process -Force terminates a stand-in promptly; a negative control needs a child that outlives the stop request for a few seconds (design in section 7 of the retest record). Not retestable here.
- Demonstration: retest/frozen: control exit 1, Get-CimInstance not recognised.

#### M13 lingering child tolerated (diagnostic producer)

- Target: `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1`; original SHA-256 `a21e50774122d523`, mutated `723ef56f6cb0424e`; locator occurrences in the source: 1.
- Mutation: replace `if (Get-ProcessIdentity $id) { throw "Child $kind (process $id) is still running after the stop request." }` with `(nothing)`.
- Command: `node --test --test-name-pattern=owned run stops only scripts/g17-diagnostic-producer-guard.test.mjs`
- Frozen source: the unmutated control already fails on this host (exit 1; 0/1 passed, the stand-in process is not visible because Get-CimInstance is unavailable), so the mutant is NOT counted as tested
- Raw output: `frozen-run-1/M13.*.out.txt`.
- **Disposition OPEN_PLATFORM_BLOCKED.** Same platform block as M12. The guard is reached only when the wait ends without the timeout error while the child is still present, for example when Wait-Process fails with an error other than the timeout (swallowed by the empty catch). No current test provides that condition (design in section 7 of the retest record). Not retestable here.
- Demonstration: retest/frozen: control exit 1, Get-CimInstance not recognised.

#### M14 stream names accepted (diagnostic verifier)

- Target: `scripts/g17-diagnostic-hash-list.mjs`; original SHA-256 `662f9863ea2e2730`, mutated `85d95ce05a4d7dae`; locator occurrences in the source: 2.
- Mutation: replace `[^\\/:<>"|?*\x00-\x1f\s]` with `[^\\/<>"|?*\x00-\x1f\s]`.
- Command: `node --test --test-skip-pattern=corrected producer finalises --test-skip-pattern=stopping twice leaves --test-skip-pattern=every kind of tampering scripts/g17-diagnostic-hash-list.test.mjs scripts/g17-diagnostic-verifier-strict.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 26/26 tests passed; control before exit 0 (26/26), control after exit 0 (26/26), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 16/17 tests passed; control before exit 0 (17/17), control after exit 0 (17/17), restored hash matches. Failing assertion: "names containing a colon anywhere are rejected, whatever the position" (AssertionError: ':leading' at g17-diagnostic-verifier-strict.test.mjs:207)
- Raw output: `frozen-run-3-broad/M14.*.out.txt`, `candidate-run/M14.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** The mutation removes the colon from the first-character class of the hash list line pattern. The existing test used a colon only in the middle or at the end of a name, so a name that starts with a colon was accepted undetected on the frozen source. New cases : leading, : and :: were added to the colon position test.
- Demonstration: candidate: control 17 of 17, mutant exit 1 on the colon position test.

#### M14b stream names accepted (second position) (diagnostic verifier)

- Target: `scripts/g17-diagnostic-hash-list.mjs`; original SHA-256 `662f9863ea2e2730`, mutated `e1bfe3e49b0f2296`; locator occurrences in the source: 2.
- Mutation: replace `[^\\/:<>"|?*\x00-\x1f\s]` with `[^\\/<>"|?*\x00-\x1f\s]`.
- Command: `node --test --test-skip-pattern=corrected producer finalises --test-skip-pattern=stopping twice leaves --test-skip-pattern=every kind of tampering scripts/g17-diagnostic-hash-list.test.mjs scripts/g17-diagnostic-verifier-strict.test.mjs`
- Frozen source: exit 1, signal none, timeout no; 25/26 tests passed; control before exit 0 (26/26), control after exit 0 (26/26), restored hash matches. Failing assertion: "names containing a colon anywhere are rejected, whatever the position" (AssertionError: 'ends:' at g17-diagnostic-verifier-strict.test.mjs:207)
- Candidate source: exit 1, signal none, timeout no; 16/17 tests passed; control before exit 0 (17/17), control after exit 0 (17/17), restored hash matches. Failing assertion: "names containing a colon anywhere are rejected, whatever the position" (AssertionError: 'ends:' at g17-diagnostic-verifier-strict.test.mjs:207)
- Raw output: `frozen-run-3-broad/M14b.*.out.txt`, `candidate-run/M14b.*.out.txt`.
- **Disposition KILLED_ON_FROZEN.** Supplementary retest of the same mutation applied to the last-character class (the pattern has two occurrences). The existing ends: case detects it. Recorded so that both occurrences of the locator are accounted for.
- Demonstration: frozen: exit 1 on the colon position test; candidate: same.

#### M15 bom not stripped (diagnostic verifier)

- Target: `scripts/g17-diagnostic-hash-list.mjs`; original SHA-256 `662f9863ea2e2730`, mutated `342b652b46f3d0c3`; locator occurrences in the source: 1.
- Mutation: replace `.replace(/^\uFEFF/, "")` with `(nothing)`.
- Command: `node --test --test-skip-pattern=corrected producer finalises --test-skip-pattern=stopping twice leaves --test-skip-pattern=every kind of tampering scripts/g17-diagnostic-hash-list.test.mjs scripts/g17-diagnostic-verifier-strict.test.mjs`
- Frozen source: exit 1, signal none, timeout no; 10/26 tests passed; control before exit 0 (26/26), control after exit 0 (26/26), restored hash matches. Failing assertion: "a state that was never finalised fails even when its listed hash is internally correct" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | [ | +   'diag-state.json is not valid JSON' | -   'diag-state.json has no valid stopped_utc' at g17-diagnostic-hash-list.test.mjs:167); "historical r1c, r2 and r3 records keep exactly their one documented state-file mismatch" (AssertionError: r1c | + actual - expected | [ | +   'diag-state.json is not valid JSON', | 'sha256 mismatch for diag-state.json', at g17-diagnostic-hash-list.test.mjs:189); "the directly built consistent directory verifies, and a subdirectory is rejected because nothing could cover its contents" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | + [ | +   'diag-state.json is not valid JSON' | + ] at g17-diagnostic-hash-list.test.mjs:212); "each rejection names exactly its own reason" (AssertionError: a self-listing entry | + actual - expected | [ | 'diag-hashes.txt lists itself', | +   'diag-state.json is not valid JSON' at g17-diagnostic-hash-list.test.mjs:242); "a state record must carry a real, ordered stopped_utc and be valid JSON" (AssertionError: no stopped_utc | + actual - expected | [ | +   'diag-state.json is not valid JSON' | -   'diag-state.json has no valid stopped_utc' at g17-diagnostic-hash-list.test.mjs:257); "a listed file that is absent is tolerated only in the explicit historical mode" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | + [ | +   'diag-state.json is not valid JSON' | + ] at g17-diagnostic-hash-list.test.mjs:266); "a missing started_utc is rejected on its own" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | [ | +   'diag-state.json is not valid JSON' | -   'diag-state.json has no valid started_utc' at g17-diagnostic-verifier-strict.test.mjs:39); "every malformed started_utc is rejected on its own" (AssertionError: "" | + actual - expected | [ | +   'diag-state.json is not valid JSON' | -   'diag-state.json has no valid started_utc' at g17-diagnostic-verifier-strict.test.mjs:39); "a missing or malformed stopped_utc is reported separately from started_utc" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | [ | +   'diag-state.json is not valid JSON' | -   'diag-state.json has no valid started_utc', at g17-diagnostic-verifier-strict.test.mjs:39); "ordering is exact to the 100 nanosecond tick" (AssertionError: equal instants | + actual - expected | + [ | +   'diag-state.json is not valid JSON' | + ] at g17-diagnostic-verifier-strict.test.mjs:39); "a symbolic link standing in for a listed file is rejected, not followed" (AssertionError: identical bytes reached through a link must still be refused | + actual - expected | [ | 'host-counters.csv is a symbolic link', | +   'diag-state.json is not valid JSON' at g17-diagnostic-verifier-strict.test.mjs:93); "an unlisted symbolic link, a directory link and a plain subdirectory are each rejected" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | [ | 'linked-directory is a symbolic link', | +   'diag-state.json is not valid JSON' at g17-diagnostic-verifier-strict.test.mjs:103); "schema version 2 states must confirm every child exit, the sidecar removal and their own ownership" (AssertionError: the complete v2 record verifies | + actual - expected | + [ | +   'diag-state.json is not valid JSON' | + ] at g17-diagnostic-verifier-strict.test.mjs:39); "historical characterization is explicit, labelled, and can never report a trustworthy directory" (AssertionError: Expected values to be strictly equal: | false !== true at g17-diagnostic-verifier-strict.test.mjs:162); "the command line is strict by default and exits non-zero for the historical mode" (AssertionError: { | "directory": "/tmp/g17-diag-strict-wRCyOy", | "mode": "strict", | "ok": false, | "errors": [ at g17-diagnostic-verifier-strict.test.mjs:178); "a byte order mark on the state file is tolerated, because the producer writes one" (AssertionError: Expected values to be strictly deep-equal: | + actual - expected | + [ | +   'diag-state.json is not valid JSON' | + ] at g17-diagnostic-verifier-strict.test.mjs:201)
- Raw output: `frozen-run-3-broad/M15.*.out.txt`.
- **Disposition KILLED_ON_FROZEN.** The earlier PATTERN_NOT_FOUND rows came from a source revision before the BOM strip existed (introduced in ca14bcf, 2026-11-16 23:54; the verifier rounds ran earlier). On the frozen source the original locator occurs exactly once and the mutant is killed by 16 failing assertions, including the explicit a byte order mark on the state file is tolerated test.
- Demonstration: retest/frozen: exit 1, 16 failing tests.

#### M16 marker may be non-comment text (witness binding)

- Target: `scripts/g17-witness-binding.mjs`; original SHA-256 `7ee128ccc8a6bf63`, mutated `c32bc9a252097941`; locator occurrences in the source: 1.
- Mutation: replace `/^\s*#/.test(line) && line.includes(marker)` with `line.includes(marker)`.
- Command: `node --test scripts/g17-witness-binding.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 8/8 tests passed; control before exit 0 (8/8), control after exit 0 (8/8), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 9/11 tests passed; control before exit 0 (11/11), control after exit 0 (11/11), restored hash matches. Failing assertion: "a mention of the marker in non-comment text neither anchors a case nor hides the real comment that follows" (AssertionError: the later real comment still binds its block | false !== true at g17-witness-binding.test.mjs:52); "a marker trailing the interval line is not a comment above the block" (AssertionError: Expected values to be strictly equal: | + actual - expected | + '  - interval: 15s # Readiness must not remain critical\n' + | +   '    input_series:\n' + | +   "      - series: 'harborops_db_readines at g17-witness-binding.test.mjs:60)
- Raw output: `frozen-run-1/M16.*.out.txt`, `candidate-run/M16.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** Not equivalent. Without the comment requirement a marker in non-comment text becomes the anchor: (a) an earlier mention of the marker hides the real comment that follows (alertCaseBound true becomes false), and (b) a marker trailing the interval line itself anchors a block that has no comment above it (false becomes true). Neither was tested. New tests cover both.
- Demonstration: candidate: control 11 of 11, mutant exit 1 with 2 failing tests.

#### M17 block start not required (witness binding)

- Target: `scripts/g17-witness-binding.mjs`; original SHA-256 `7ee128ccc8a6bf63`, mutated `80b766790b6897f8`; locator occurrences in the source: 1.
- Mutation: replace `if (start < 0) return null;` with `(nothing)`.
- Command: `node --test scripts/g17-witness-binding.test.mjs`
- Frozen source: exit 0 (survived), signal none, timeout no; 8/8 tests passed; control before exit 0 (8/8), control after exit 0 (8/8), restored hash matches.
- Candidate source: exit 1, signal none, timeout no; 10/11 tests passed; control before exit 0 (11/11), control after exit 0 (11/11), restored hash matches. Failing assertion: "a case comment with no block after it yields no block, whatever follows" (AssertionError: "" | + actual - expected | + '  # Readiness must not remain critical after the probe recovers.' | - null at g17-witness-binding.test.mjs:68)
- Raw output: `frozen-run-1/M17.*.out.txt`, `candidate-run/M17.*.out.txt`.
- **Disposition GAP_CLOSED_KILLED_ON_CANDIDATE.** At the production boundary alertCaseBound the guard is redundant: with no block start the slice is empty or a single line, which can never contain input_series followed by a series line, so alertCaseBound is false either way (0 differences in 80004 generated comparisons). At the exported alertCaseBlock the result differs (null versus an empty or single line string). A test now asserts null for a case comment with no block after it.
- Demonstration: proofs/m11-m17-demo.json; candidate: control 11 of 11, mutant exit 1 on the new test.


## 5. Added negative controls (candidate `60576b16`, tests only, no product or script change)

| Test file | Added or tightened | Kills |
| --- | --- | --- |
| `scripts/g17-docker-ownership.test.mjs` | Cross-platform rule driven docker front. New tests: containers that docker reports as removed but that remain make the cleanup fail; an address range taken by another network right after the removal fails the cleanup and names the holder; a docker-created anonymous volume survives removal of its container. The existing evidence directory test now asserts the script's own `Never overwrite evidence` message and that no record is written | M08, M09, M10 (behaviourally), M11 |
| `scripts/g17-witness-binding.test.mjs` | A mention of the marker in non-comment text neither anchors a case nor hides the real comment; a marker trailing the interval line is not a comment above the block; a case comment with no block yields no block | M16, M17 |
| `scripts/g17-diagnostic-verifier-strict.test.mjs` | Colon position test gains `:leading`, `:` and `::` | M14 |

All new and tightened tests pass on the unmutated candidate. The Windows launcher branch of the rule front (`docker.cmd` calling `node`) could not be executed on this host and must run in the next campaign.

## 6. Worker top-up provenance (S25-A02 limitation)

- The forced top-up rehearsal reports source `826e096f` and executed a generated, modified `_rehearse-topup.mjs` copy whose required traced count was raised to 8. The committed top-up implementation is in `634053c4`; `scripts/check-g17-worker-journey.mjs` differs between `826e096` and `634053c` (17 insertions, 2 deletions). The rehearsal is therefore **not** an exact-source witness of the final code. Its raw results and traces are preserved unchanged in `g17-m-worker-topup-rehearsal-evidence.zip`.
- The top-up loop is not mandatory for any technical atomic assertion. The assertions that rest on the worker journey need, at the end of the stage, at least 50 receipts all reconciled, at least 3 sampled journeys, a duplicate replay, and at least 2 real API and worker trace pairs in Tempo (`scripts/check-g17-worker-journey.mjs` final condition, `scripts/g17-raw-witness.mjs`, `scripts/verify-g17-manifest.mjs`). They are satisfied identically whether the sample count came from the first 50 receipts or from a top-up. The m4 run at `634053c4` satisfied them with 50 receipts, 50 reconciled, 4 sampled, 4 pairs and `top_up_batches` 0, so the branch was dormant.
- The branch is a supplemental reliability measure: with 50 receipts the chance of fewer than 3 sampled journeys is about 11.2 percent (binomial, p = 0.1), the case it exists for. Its sampling arithmetic is covered by the cross-language vector table and unit tests, and its rules by mutation (6 of 6). Its live loop has no exact-source witness. This limitation is disclosed for S25-A02 and is not presented as closed. No source change was made to create one.

## 7. Outstanding exceptions and S25-A01 status

Closed on the retest evidence: 15 of 17 candidates (killed on the frozen source, killed after a new negative control, or individually justified as redundant or unreachable). Not closed:

1. **M12 and M13 (diagnostic producer child exit)** cannot be executed here: the producer reads `Win32_Process`. They need a Windows run. Design of the missing negative controls: PowerShell resolves functions before cmdlets, so a wrapper `-Command` that defines `Stop-Process` (delayed termination of a stand-in child by about 3 seconds) before invoking the producer shows that the exit is awaited and that a second kill-less outcome is not recorded as terminated (M12); a wrapper that defines a `Wait-Process` which throws a non-timeout error and a `Stop-Process` that does nothing reaches the still-running check (M13). Neither test exists and neither was demonstrated.
2. **New frozen source required.** Tests changed, so `60576b16` (or a later test-affecting commit) is a new source and the full mandatory source-locked campaign has not been run on it. The m4 result remains valid for `634053c4` only. The recommendation is to add the M12 and M13 controls on a Windows host first so that a single campaign covers all test changes.
3. **Residual disclosures:** M04 is redundant defence in depth against a misbehaving extractor (verdict differs under a simulated extra file); M06 and M07 are guards made unreachable by the daemon's exact label filter; the worker top-up live loop has no exact-source witness (section 6).

Therefore **independent S25-A01 engineering and evidence obligations are not all closed**: the blockers are items 1 and 2 above. No G17 acceptance is declared.
