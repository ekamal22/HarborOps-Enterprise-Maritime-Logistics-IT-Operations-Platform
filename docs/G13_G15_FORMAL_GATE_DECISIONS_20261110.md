# G13 and G15 Formal Gate Decisions

> **Update (2026-11-10, later source):** The conditions and open items recorded here describe source `15cabd8c542159753c927c2bd65b4346223e84f6`. Tenant quota and bounded idle cleanup, the specific mutation witnesses and the real HTTP outage test were subsequently implemented and verified locally at `5bcb7e5b2ec5e590bd26e64ffd659c17aaee1f92`; see the [local closure results](G13_G15_LOCAL_CLOSURE_RESULTS_20261110.md). This record's dispositions remain in force until new positive decisions are recorded after independent review. Acceptance pending.

**Decision date:** 2026-11-10  
**Evidence source SHA:** `15cabd8c542159753c927c2bd65b4346223e84f6`  
**Technical campaign:** [42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415), PASS.  
**Acceptance authority exercised:** Separate phase-six gate disposition, without merge or production authorization.

## G13: NOT ACCEPTED

The full technical campaign passed (including 227/227 recorded technical witnesses across both gates). D-005 and reference KPI correctness have strong evidence. However, the selected permanent per-tenant report quota and idle cleanup policy is not enforced in source and is not verified at scale; G13-S01/S20 therefore cannot pass. G13-S24 also lacks individual source-mutant kills for every required revocation and rollback negative case. The G13-S25 independent evidence review returns NOT PASS. G13 is not ready for unconditional acceptance or integration.

## G15: NOT ACCEPTED

The combined technical campaign passed, the fail-closed P10 condition is technically evidenced, and P12 remains provisional with deletion disabled. Nevertheless G15-S24 has no dedicated killed source-history uniqueness mutant, G15-S21 lacks exact real HTTP dependency-outage coverage, and the independent S25 review is NOT PASS. G15 is close technically but does not yet satisfy the complete documented acceptance standard.

## Source and release boundaries

The review is an evidence-based gate decision, not a rejection of the underlying implementation, and it does not undo the successful technical source verification. A separate external reviewer has not signed either gate. The reviewed implementation remains on `verify/g13-g15-evidence-closure-20261109` and is not integrated into protected `main`. The Turkish localization branch is separate and was not incorporated into this acceptance decision.

No merge, production deploy, production migration, dashboard activation, dispatcher activation, retention deletion or source-history cleanup is authorized. M9 parked ARRI/DEPA and 10,000 unresolved-work cap, null ETA, M10 projector default-off and M11 evaluator opt-in remain binding.

## Required closure before reconsideration

1. Implement numerical per-tenant report quotas and idle bounded cleanup, prove concurrency and storage behavior on the new SHA.
2. Add or reconcile G13-S24 and G15-S24 effective specific mutation witnesses, preserving clean controls and source lock.
3. Demonstrate bounded tenant-safe G15 HTTP behavior during real database/dependency outages.
4. Independently reconcile all 48 technical acceptance scenarios at assertion behavior level, with raw artifacts and source provenance, then record two new S25 recommendations.
5. Complete separate explicit positive G13 and G15 acceptance decisions. A fresh full campaign must pass after any application or test source change.

**Final disposition:** G13 NOT ACCEPTED. G15 NOT ACCEPTED. No merge or deployment.
