# G13-S25 Independent Evidence Review

**Review date:** 2026-11-10
**Review disposition:** NOT PASS. Final exact-source acceptance evidence and a permanent numerical resource policy remain pending.
**Accepted main baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`.
**Previously verified implementation SHA:** `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`.
**Final closure candidate:** `15cabd8c542159753c927c2bd65b4346223e84f6`, subject to a separately completed campaign.

## Evidence independently read back

The 15 retained archives from [the preceding technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) were inspected directly, not merely by copying the workflow's success label. Every one of 169 listed ZIP-internal SHA-256 manifest entries matched its archived bytes. Source markers agreed on the old verified SHA. Direct TRX totals were 541 passing, zero failed and zero skipped. All 48 technical scenario contract checks passed, with 224/227 mapped technical assertions witnessed and three absent. The missing IDs were G13-S08-A04, G13-S09-A03 and G13-S09-A04. The prior run cannot prove their execution.

The artifact packet includes 30 measured 170,000-shipment reports, four real restricted-role EXPLAIN captures for empty, small, medium and skewed large workloads, a separate privileged plan, 18 effective KPI mutants, four restricted-role SQL policy mutants, four-principal concurrency, source reconciliation, three browser engines, eight crash phases and accepted-milestone regressions. All mandatory results must be tied to the final source SHA before a new PASS.

## Mandatory independent review findings

1. **G13-S01 and G13-S20 remain blocked by permanent numerical policy.** The existing 2 GiB additional table-growth and 1 GiB process working-set figures are provisional performance-test ceilings, not enforceable perpetual storage budgets. The current application has a 10-minute token expiry, 500,000 members per report and at most eight expired-report deletions per newly built report in the selected organization. It does not establish an enforced overall per-tenant report-storage bound or scheduled idle cleanup. A service with no subsequent report builds has no guaranteed periodic pruning. Report token expiry alone does not physically remove retained member rows.
2. **G13-S09 and feature-default source coverage:** dedicated methods `M10ProjectorAndM11EvaluatorRequireExplicitOptIn` and `LateWorkerEffectDuringRebuildIsNeverLostAndMarksTheProjectionStale` exist. Both have passed in a corrective source-locked preflight on `a1fa2ee2bd1c742a1ac4529c49f28b9434501bb6`. This is encouraging but final complete exact-SHA evidence is required.
3. **Scenario completeness:** the prior technical ledger marked S02 PASS, S01 and S03-S24 BLOCKED, and S25 NOT EXECUTED. The 48 green technical evidence contracts cannot be promoted automatically into 48 complete accepted scenarios. Each G13-S01 through S24 requires a direct requirement-versus-evidence disposition, preserving all exclusions and prior failed attempts.
4. **Performance uncertainty:** the passing measured latency approaches the 15-second SQL timeout in the worst samples. There is little headroom for unmeasured production workload distribution, and no production SLA is approved by this campaign.

## S25 completion requirements

Read the new source-locked raw artifacts, match every artifact SHA and manifest, verify all 227 technical assertion witnesses and each G13 scenario, review settled D-005 metric definitions, approve and verify an enforceable numerical resource and cleanup policy, then record an explicit G13-S25 recommendation and a separate G13 gate decision. Until then this review is NOT PASS, not acceptance or merge authority. No production migrations, report cleanup changes or activation are authorized.

## Additional assertion-mapping quality finding

The current G13-S24-A01 through A06 inventory entries use suite-level evidence IDs (`g13-mutations` and `g13-security-mutations`) for several separate required negative mutations. The assertion audit verifies those whole campaigns passed but does not independently link each demanded tenant, Auditor-grant, unknown-to-zero, correction-generation, rollback, revocation and forged-cursor mutant to a particular killed source mutation and its detecting test. The 18-plus-4 mutation records show broad sensitivity, but the independent review must validate each required category by mutant ID, actual source diff and test failure. In particular, membership/session revocation bypass is not clearly identified as an individual G13 mutation in the retained manifest. An aggregate `TECHNICAL_EVIDENCE_PRESENT` verdict should not be treated as proof of every named G13-S24 clause without this additional mapping.
