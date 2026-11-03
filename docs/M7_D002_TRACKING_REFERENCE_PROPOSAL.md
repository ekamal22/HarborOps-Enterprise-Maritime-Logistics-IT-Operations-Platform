# D-002 Proposal: M7 Track & Trace Reference and Supported Subset

**State:** Proposed, acceptance pending. This document does not resolve D-002 by itself.
**Prepared:** 2026-11-03
**Base:** Accepted M0-G0 through M6-G6 on `main` at `97b67739cf1dad543bf25fa56326fb4702990f41`
**Decision authority:** [Decisions](DECISIONS.md), [Roadmap](ROADMAP.md), [M7 architecture](M7_ARCHITECTURE_DESIGN.md)
**Implementation gate:** Accept D-002 before freezing M7-01 contracts and beginning M7-02/M7-03.

## 1. Decision to make

Select a traceable reference edition and a bounded translation profile for HarborOps tracking events. HarborOps is building an internal, source-authenticated intake interface, not a claim to implement every external DCSA publisher endpoint, offer a certified DCSA API, or operate an actual shipping carrier feed.

DCSA announced Track & Trace 3.0.0 on 2026-09-30. Its announcement states that it initially serves members and DCSA+ partners and is planned for general public release in the second half of 2027. The public DCSA implementation guide currently describes Track & Trace 2.2. Consequently the proposed publicly reproducible baseline is **DCSA Track & Trace 2.2.0**, with HarborOps adapter profile **`tnt-2.2-harborops-v1`**, explicitly not a DCSA conformance claim.

Sources:
- DCSA public implementation guide: https://reference.dcsa.org/content/standards/guidelines/implementation-guides/implementing-track-and-trace
- DCSA v3.0.0 announcement and availability: https://dcsa.org/newsroom/track-and-trace-3-0-0-iot-reefer-visibility
- DCSA reference entry: https://dcsa.org/standards/track-and-trace
- DCSA GitHub organization: https://github.com/dcsaorg (the repository `dcsaorg/DCSA-OpenAPI` named in the first draft of this proposal does not exist; see section 6)

Before D-002 acceptance, obtain an immutable upstream 2.2.0 OpenAPI or canonical reference artifact, confirm its exact version, upstream URL, commit/tag or release identifier, SHA-256, redistribution terms, and field/code semantics. A moving documentation URL or an unreviewed vendor variant is not a pinned normative artifact. If the exact public 2.2.0 artifact cannot be independently obtained and hashed, **D-002 remains blocked** rather than substituting a guessed schema.

## 2. Alternatives

| Option | Benefits | Costs and decision |
| --- | --- | --- |
| DCSA Track & Trace 2.2.0, bounded adapter profile | Public documentation and existing implementation guidance; implementable without paid credentials | **Recommended**, pending exact artifact and explicit acceptance |
| DCSA Track & Trace 3.0.0 | Newer IoT/reefer and correction semantics | General-public availability is staged; licensing and artifact access must be verified; **defer** |
| Fully proprietary HarborOps-only vocabulary | Simplest short-term implementation | Loses traceable external event mapping and violates the intent of D-002; reject as sole reference |
| Entire 2.2 surface | Broadest conceptual coverage | Unnecessary scope, ambiguous conformance claims, more implementation risk; reject for M7 |

## 3. Proposed bounded mapping

Three external event families are recognized as *categories*, but M7 implementation should freeze only an explicitly reviewed fixture subset after inspection of the pinned upstream schema.

| Profile category | Candidate public 2.2 fields | HarborOps canonical field | Rules |
| --- | --- | --- | --- |
| Common identity | eventID, eventType, eventClassifierCode, eventDateTime | sourceEventId, family, classifier, occurredAtUtc | Preserve exact external semantics and classify supported versus unsupported |
| Equipment | equipmentEventTypeCode, equipmentReference | code, equipmentReference | Only supported codes from pinned upstream artifact, with identifier syntax validation; never assert equipment ownership |
| Transport | transportEventTypeCode and documented transport/voyage references | code, transportReference | Require a recognized transport reference; no inferred shipment assignment |
| Shipment | shipmentEventTypeCode and documented shipment references | code, shipmentReference | Initially recognized for mapping and unsupported-variant denial until specifically enumerated |
| Location and provenance | documented location fields and carrier/source metadata | location, externalSchemaVersion | Optional allowlisted fields; no inferred geographic precision |

The table is a candidate field mapping, not an assertion that these names are sufficient for DCSA conformance. Final M7-01 mapping must mark every supported field as required, optional, derived, unsupported, or rejected, with fixture provenance and normative constraints. No free-form unknown keys silently pass. Do not copy restricted standard materials or unlicensed sample payloads into the public repository.

Recommended initial usable subset after upstream review:
- Equipment gate-in and gate-out event types, only if their exact 2.2 codes and requirements are confirmed.
- Transport arrival and departure event types, only if their exact 2.2 codes and requirements are confirmed.
- Shipment/document events and any IoT/reefer events explicitly out of the first supported set.
- Classifier `ACT` and other documented values only after confirming the pinned vocabulary. Reject unsupported classifications with a stable, non-disclosing error.

## 4. Contract boundary

`POST /integrations/v1/tracking-events` is **HarborOps's own inbound adapter**, not a claim that DCSA 2.2 defines that POST route. DCSA's publisher/consumer API and the HarborOps intake contract are different surfaces.

The internal wire model is `harborops-tracking-v1`: an explicit single-event JSON object with source-local ID, event family, event code, classifier, UTC occurrence, relevant equipment/transport/shipment reference and a declared `externalSchemaVersion`. Its canonical schema, fields, validation limits and error contract are described in [M7 architecture](M7_ARCHITECTURE_DESIGN.md).

Unknown future upstream fields and codes are rejected until an adapter version is deliberately added. Upstream 3.0 is not implicitly backward compatible, particularly for IoT/reefer or correction semantics.

## 5. Explicit decision requirements

Approval of this proposal must establish:
1. Exact upstream 2.2.0 reference artifact identity and its pinned SHA-256.
2. Field-by-field translation and first supported code fixtures.
3. Supported/unsupported event families, classifier values, optional references and maximum bounds.
4. Ownership of the HarborOps-specific inbound POST contract and limitation of interoperability claims.
5. Change process for future upstream revisions: add an explicit adapter version and regression fixtures; no silent mapping change.

**Decision:** D-002 acceptance pending. This proposal is ready for review; G7 acceptance and M7 implementation are not implied. M8's separate D-006 inbox/dead-letter decision remains open.

## 6. Upstream artifact observations (2026-11-03)

These are observations made during G7 execution. They do not accept D-002.

| Item | Observation |
| --- | --- |
| Repository named in the first draft | `dcsaorg/DCSA-OpenAPI` returns 404. `dcsaorg/DCSA-TNT` has tags `v2.0.0`, `v1.2.0` and `0.0.1-reactive` only and was last pushed 2023-08-04, so no 2.2.0 tag exists there |
| Public release listing | `reference.dcsa.org` lists Track & Trace v3 only; 2.2 is described by the public implementation guide |
| Candidate 2.2.0 definition | SwaggerHub API `dcsaorg/DCSA_TNT` version 2.2.0, OpenAPI 3.0.3, license declared as Apache 2.0, `last-modified` 2021-10-28 |
| Shared event definitions | Event code lists are in SwaggerHub domain `dcsaorg/EVENT_DOMAIN` version 1.0.4, which the 2.2.0 definition references; it also references `DCSA_DOMAIN` 1.0.3 and `ERROR_DOMAIN` 1.0.0 |
| Codes in the provisional subset | Equipment `GTIN` and `GTOT`, transport `ARRI` and `DEPA`, and classifier `ACT` are all present in `EVENT_DOMAIN` 1.0.4. `EVENT_DOMAIN` also defines further codes and classifiers (for example `PLN`, `EST`) that the provisional subset does not accept |
| Immutability | SwaggerHub versions are not content addressed, and no signed or tagged release was found. A hash pins only the content observed on the day it was fetched |

SHA-256 of the content fetched on 2026-11-03 (JSON as served, retained in the E: verification root):

| File | SHA-256 |
| --- | --- |
| `DCSA_TNT` 2.2.0 | `b3c02f97f19833b3dca8edd95a328b1d6c0c2cacb1a0606d882a12d0f698c127` |
| `EVENT_DOMAIN` 1.0.4 | `5b9efaba91ea6ea9d855a59ae0670e252fc472eba5d3e9419962b9b5bcbc644c` |
| `DCSA_DOMAIN` 1.0.3 | `b6ed1f10185df3e680d20a5c5aee63da26cc76e49da33616400f517aa7ffb5e4` |
| `ERROR_DOMAIN` 1.0.0 | `15fcd4c9d3b5e529a8c170ca941ca9ec8a957df356322b38e41d25d022ca54e5` |

Remaining before acceptance: a decision on whether a hash of mutable hosted content is an acceptable pin or a tagged repository release must be located, redistribution review of the fixtures, the field-by-field mapping review, and the explicit decision itself. Until then G7-S01 is BLOCKED, G7-S17 is BLOCKED for the upstream-mapping part, and the standard version and supported subset are unchanged.
