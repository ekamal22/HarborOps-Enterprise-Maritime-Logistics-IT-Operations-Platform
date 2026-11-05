# G16 Independent Review: S07, S08 and S28 Supplement

**Review date:** 2026-11-05  
**Evidence source:** Two privately supplied files, `supplement-s07-s08-s28.log` and `supplement-s07-s08-s28-results.json`.  
**Source association:** The [published corrective campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/04-final-report.md) attributes the supplementary probe to final standalone M16 revision `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13`. Neither supplied file independently embeds a source SHA or immutable run attestation.  
**Disposition:** **Both supplied supplemental files REVIEWED. All 6/6 recorded checks pass and match the committed probe logic.** This review does not mean the probe was rerun or that the standalone revision is cryptographically bound to these output files. G16 acceptance pending.

## Integrity and provenance

| File | SHA-256 | Observed contents |
| --- | --- | --- |
| `supplement-s07-s08-s28.log` | `e578cc0e3b60270b9e8969e5138c1079112642260b8ff42dcebf18d55a0af368` | Six PASS records, process termination/restart output, summary pass=6 fail=0 |
| `supplement-s07-s08-s28-results.json` | `5e715aa9da9e6f37a6619529f6e2ad217bca26e2c523f85e6df82fe2b66ea642` | Six structured records with `pass: true`, matching the log |

The serialized results match the version committed in [the G16 verification branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/supplement-s07-s08-s28-results.json). The [probe implementation](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/supp-s07-s08-s28.mjs) was inspected to understand each assertion. These two files are supplemental artifacts and are not part of the previously reviewed 162-file main-campaign manifest. Do not rewrite or silently enlarge that manifest.

## Result-by-result reconciliation

| Scenario | Reported observation | Independent review |
| --- | --- | --- |
| G16-S07 | Stale-version update 409, empty update 400, unauthorized selection 409, malformed organization ID 400; zero new audit rows, unchanged organization version 1 to 1 | PASS for these four negative paths. The unauthorized path uses an account without selected membership and is not a dedicated forbidden-role mutation test |
| G16-S08 | Ten rounds of eight parallel same-version updates produced ten successful responses and no unexpected response status | PASS for observed optimistic-concurrency behavior. The script treats any non-200 response other than 409 as unexpected |
| G16-S08 | Committed organization version increased ten and corresponding audit-update count increased ten | PASS for audit/business one-to-one counts in this exercised path |
| G16-S08 | Container `ho-verify-dev16-api-1` killed and restarted during the burst; organization version increased three and audit count increased three | PASS for process interruption and the observed durable pairing. The probe is not a physical disk-failure or arbitrary COMMIT-ack interruption test |
| G16-S28 | Operator-provisioned audited rows include `users.insert`, `user_credentials.insert`, `organizations.insert` and `organization_memberships.insert`, each attributed to `OperatorCli` | PASS for observed actor classification |
| G16-S28 | Zero `OperatorCli` audit rows have a human user or session ID | PASS for observed context separation |

The log abbreviates the operator event listing on its console line; the uploaded structured JSON contains the full listing and is consistent with the exact SQL assertion in the committed probe.

## Scope and outstanding conditions

The supplementary evidence for the specific G16-S07, G16-S08 and operator-identity portion of G16-S28 checks is now **REVIEWED**. The other parts of S28, including integration-source attribution, are supported by the main-campaign TRX, HTTP and mutation results reviewed separately. The initial G16 verification matrix's 30/30 standalone PASS notation is a technical-campaign disposition and is not an acceptance decision.

See the [main-campaign independent review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md), which verified the original 162/162 evidence manifest and test outcomes. Taken together, the main campaign and these two independently inspected supplemental files cover the stated standalone evidence package. Remaining limitations include the lack of a separate 1024-byte metadata CHECK negative probe, audit cursor commit-order edge cases, the superuser migration identity, in-place editing of an unreleased migration and **no complete combined M9/M16 Compose, browser or outage gate** against the accepted M9 baseline. Decide the worker-role provisioning authority and whether M9 worker-effect history requires M16 auditing before integration.

**G16 evidence status:** Main 162-file campaign REVIEWED; supplementary S07/S08/S28 pair REVIEWED; full integrated M9/M16 verification INCOMPLETE; G16 acceptance pending; no merge or deployment authorization.
