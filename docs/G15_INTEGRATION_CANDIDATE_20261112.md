# G15 integration candidate, 2026-11-12

## Baseline and source provenance
This branch is based on protected `main` at `757bab53e898944c1ba14481ceebdb4f18bc5842`, following the conditional G13 integration and its documentation reconciliation. The G15 implementation, tests, shared authorization hooks and client wiring were reconciled from post-C3 source `6681f5da338ec4e585d37861b1d20af82060e846`. Application source on that branch is unchanged from combined source `c6d2dc86e30913b8d67a011131f9980422e4c197`, which passed hosted run 43122895445.

The existing combined run does not certify this newly assembled G15-only integration candidate. All accepted G13 reporting functionality remains part of the base, not a separately imported feature. No temporary verification workflow, production activation or retention deletion is included.

## Gate disposition
G13 is conditionally accepted and merged, with reviewer independence not attested. G15 remains NOT ACCEPTED and NOT MERGE READY. A fresh exact-SHA compilation, migration and upgrade verification, restricted database and HTTP authorization tests, all three browser engines, adversarial mutation verification, crash recovery and bounded performance evidence are required. An independent G15 S25 assessment and an explicit acceptance decision must follow those results.

The existing combined mutation job labels the `outbox-claim-skip-locked-removed` mutant KILLED. That label alone does not resolve the outstanding review concern about failure during test unwinding instead of the prompt-progress assertion. Its test and raw log must prove that the second dispatcher either fails the intended explicit progress deadline in the mutant or a clean control passes the exact assertion, without treating a teardown cancellation as a behavioral kill.

## Performance and migration review
Recheck revoked or superseded membership backlogs, read-scope planner behavior, autovacuum statistics sensitivity, one-million-receipt read timing, dispatcher fanout, bounded quarantined-intent handling, and the 17-to-18 migration upgrade with G13 already installed. These items remain under review until measured on this candidate.

## Restrictions
Do not merge G15, deploy, enable default-off delivery in a persistent environment, trigger retention deletion, delete historical failed evidence, change accepted G13 policy, or start unrelated integration. All results must identify exact source revisions.
