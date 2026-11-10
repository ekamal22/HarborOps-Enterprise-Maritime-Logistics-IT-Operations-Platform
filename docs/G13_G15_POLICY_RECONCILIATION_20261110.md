# G13/G15 Conditional Policy Reconciliation

**Date:** 2026-11-10
**Disposition:** The approved business policies remain unchanged. G13 permanent numerical storage and idle-cleanup decisions are still open; M15 P10 recovery conformance is under final exact-SHA review and P12 cleanup remains prohibited.

## G13 measured envelope and unresolved permanent bound

D-005 Option A and the versioned K01-K14 definitions are approved. The source implements a maximum of 500,000 report members, 10-minute snapshot token lifetime, 100-row member pages, 15-second SQL timeout, 25-second request budget and one concurrent report build per actor and organization. On new report builds it prunes at most eight expired reports from that organization, then removes earlier reports belonging to the same actor/session. The existing 30-report performance test demonstrates a bounded 170,000-shipment sample with 2 GiB temporary report relation-growth ceiling and 1 GiB peak process working-set ceiling. These are not permanent production storage budgets or idle cleanup guarantees.

**Resource decision still required:** establish a measured and enforceable per-tenant limit for total retained report/member rows or bytes, an admission policy for contention, and automatic bounded expiration cleanup during idle periods. Specify maximum cleanup delay and monitor it at the service and database levels. Do not treat access-token expiry as row disposal. Do not relax an existing benchmark merely to obtain a passing gate. G13-S01-A04 and G13-S20-A07 cannot PASS until these decisions are approved and, where relevant, source-verifiably implemented.

## M15 P10, P12 and prior approvals

P01-P09, P11, P13 and P14 are approved and unchanged. P10 authorizes transactional fail-closed M11 history/outbox coupling subject to observed rollback, SIGKILL recovery, retry and accepted M11 regression compatibility on the final exact source. The existing predecessor evidence is strong, but final review is required. P12 permits provisional 90-day unread/read and 30-day acknowledged periods, with all physical deletion and background cleanup OFF and not authorized. Preserve immutable histories, replay protection, audit records, holds and undelivered intents. This limitation is a deliberate policy boundary, not a waiver to discard evidence.

## Review and integration boundary

The formal G13/G15 S25 reviews and separate milestone decisions do not follow automatically from 227 witnessed technical assertions, one source-locked success or a policy note. The unresolved numerical G13 retention/resource policy remains a gate blocker. No production activation, database migration, merge, data deletion or change to M9/M10/M11 defaults is approved by this reconciliation.
