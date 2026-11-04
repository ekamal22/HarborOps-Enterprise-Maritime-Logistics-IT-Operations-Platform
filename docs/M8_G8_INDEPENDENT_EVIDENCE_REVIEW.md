# G8 Independent Evidence Review

**Review date:** 2026-11-04  
**Disposition:** Independent evidence review PASS. This review is distinct from the technical runner and from the separate G8 acceptance decision.  
**Tested implementation SHA:** `537af9c2af99751aa2fbedf1a2115b7715e7b9e6`  
**Source-locked run:** `20261104T073952Z-0d093512c50ec17d`, attempt 04  
**Raw technical result:** 23 of 23 executable scenarios PASS; G8-S01 and G8-S25 originally BLOCKED pending supplementary decisions.  
**Preservation:** Neither the sealed original evidence, its 25-case raw register nor failed attempts 01 to 03 were altered.

## Evidence identity and integrity

| Artifact | Independently calculated SHA-256 |
| --- | --- |
| Sealed `g8-537af9c2...-evidence.tar.gz` | `b5144d0e39fe22f2ea71f996245df49b62ff43a3ac68c10d09bbd01133fa349c` |
| Sealed 174-file manifest | `89a4adc610945152ddd821a6bfaaed49a0fc054ac41b92a61fb02dc7f91ef5c7` |
| Finalization status JSON | `9a58db4484aa3c4ef222fa055452ee945205a6cdd81e8e68dd5547bbc86dea1f` |
| Archive safety log | `44247b7f34564e55656724a463945b5d3e3ac53a10233472d33c9abbad31b6df` |
| Raw 25-case scenario JSON | `b6d92b3f53aa9398e53789fa88195021b519e4fbb8421dc6f887a9f9b1dc4cea` |
| Technical status JSON | `14d5f432703fcd3e0d37b5ceaf51fcf366be7de832803f7f105f2096ff7c6327` |
| Formal gate status JSON | `325455304cd31ae95618db22a6a7570715c47d98b9e262f1334d876a255882db` |

The uploaded archive and manifest hashes matched the execution register exactly. Independently read the `.tar.gz` without running any archive member. It contains 240 members, 174 regular files and 66 directories; no unexpected entry types or path escapes. Independently computed and matched every manifest entry, with no missing, duplicate or extra files.

## Raw outcome validation

- **Stage identity:** All 24 `stages.tsv` rows have the identical tested source SHA and exit code 0.
- **.NET TRX:** Parsed individual `UnitTestResult` records, not just summary counters: 203/203 unit, 39/39 contract, 242/242 fresh integration and 242/242 repeated integration passed, with no failed, skipped or inconclusive outcomes.
- **Browser JSON:** Parsed each of 16 separate Playwright spec reports for each of Chromium, Firefox and WebKit. Every engine records 58 expected outcomes, zero unexpected, zero flaky, zero skipped and no runner-level errors. The M8 ingress test passed in each engine.
- **COMMIT uncertainty:** In each of the two integration passes the raw TRX lists both `RealPostgresCommitTransportLossNeverAcknowledgesAndReplayResolves` cases: a database-wire cut before forwarding COMMIT, and after PostgreSQL reported commit completion but before client acknowledgment. Both passed. The separate lock-order regression passed in both passes.
- **Security mutations:** Independently compared `g8-mutations.json`, the 11 individual mutation TRX files and console logs. Unmodified control passed 12/12. All 11 variants are KILLED; each has an intended failing regression and none has a compiler/build-error kill. The source-status variant's XML-escaped parameter name corresponds to the failing `source` case in the raw TRX.
- **Recovery:** The source/run-matched `g8-restart-integrity.json` records PASS with identical before/after SHA-256 for original accepted raw bytes, canonical bytes and canonical digest, with the original Pending work and canonical event unchanged.
- **Supply chain:** The three actual Trivy JSON reports for postgres, api and web contain no HIGH or CRITICAL findings. Source-labeled image identities are present in `image-ids.tsv`. Trivy database state is recorded in `trivy/version.txt`; the result is point-in-time, not future vulnerability clearance.
- **Finalization:** `g8-finalization-status.json` separately records technical PASS, archive PASS, finalization PASS and formal acceptance pending. The archive-safety log verifies 174 files. The recorded disposable teardown shows no remaining named G8 containers, volumes or test database.
- **Sensitive data:** A byte-level scan of the archive found neither the defined machine bearer pattern nor JSON raw source-event bodies. The finalization and review files themselves were matched against the recorded digests.

## Contract review and disposition

D-006 proposed Option A is supported by the observed transactional receipt/event/Pending triple, permanent tenant/source/profile/event-key uniqueness, deterministic canonical replay, bounded collision metadata, independent final source authorization, 202 only after confirmed commit, raw-byte continuity, 10,000-Pending backpressure and no accepted-history deletion.

The uniqueness invariant is enforced by a PostgreSQL **unique index** rather than the proposal's wording of a named unique constraint. The resulting key uniqueness is equivalent for this gate. M8 deliberately prevents work-state changes and deletion; M9 must add an explicitly tested migration replacing its Pending-only work-state constraint/trigger before introducing leases, retries, Processed or DeadLetter states. At 10,000 Pending items, new keys remain blocked until M9 advances work or a separately approved capacity policy changes. These are recorded contract qualifications, not untested M8 processing claims.

The original raw register stays as recorded: G8-S01 and G8-S25 were BLOCKED during execution. This independent review supplies the later review evidence for G8-S25 only; a separately recorded D-006 decision must supply G8-S01. Separate explicit G8 acceptance is still required and is not inferred from the original runner's exit 2.

**Review limit:** This was an independent examination and recomputation from the submitted sealed artifact and the matching repository source and contract. The live workstation, third-party advisory endpoints and a new PostgreSQL execution were not independently reproduced. The accepted G0-G7 baseline and the future M9 worker were not changed by this review. The occasional earlier Firefox teardown failure remains a documented residual risk; final three-engine evidence is clean. No deployment or production-readiness claim is made.

**Conclusion:** Independent source-locked G8 evidence is complete and consistent; there is no identified M8 correctness or evidence-integrity blocker to a separate D-006 Option A decision and explicit G8 acceptance.
