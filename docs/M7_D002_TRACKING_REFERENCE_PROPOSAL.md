# D-002 Proposal: M7 Track & Trace Reference and Supported Subset

**State:** Accepted 2026-11-03, Option A, with the conditions in section 8. Sections 1 to 7 are the proposal and recommendation as written before the decision.
**Prepared:** 2026-11-03
**Base:** Accepted M0-G0 through M6-G6 on `main` at `97b67739cf1dad543bf25fa56326fb4702990f41`
**Decision authority:** [Decisions](DECISIONS.md), [Roadmap](ROADMAP.md), [M7 architecture](M7_ARCHITECTURE_DESIGN.md)
**Implementation gate:** Satisfied by the decision recorded in section 8.
**Reading guide:** Sections 1 to 7 were written before the decision and describe the options, the retrieved artifacts and the recommendation. Section 8 is the decision record and governs where they differ.

## 1. Decision to make

Select a traceable reference edition and a bounded translation profile for HarborOps tracking events. HarborOps is building an internal, source-authenticated intake interface, not a claim to implement every external DCSA publisher endpoint, offer a certified DCSA API, or operate an actual shipping carrier feed.

DCSA announced Track & Trace 3.0.0 on 2026-09-30. Its announcement states that it initially serves members and DCSA+ partners and is planned for general public release in the second half of 2027. The public DCSA implementation guide currently describes Track & Trace 2.2. Consequently the proposed publicly reproducible baseline is **DCSA Track & Trace 2.2.0**, with HarborOps adapter profile **`tnt-2.2-harborops-v1`**, explicitly not a DCSA conformance claim.

Sources:
- DCSA public implementation guide: https://reference.dcsa.org/content/standards/guidelines/implementation-guides/implementing-track-and-trace
- DCSA v3.0.0 announcement and availability: https://dcsa.org/newsroom/track-and-trace-3-0-0-iot-reefer-visibility
- DCSA reference entry: https://dcsa.org/standards/track-and-trace
- DCSA GitHub organization: https://github.com/dcsaorg (the repository `dcsaorg/DCSA-OpenAPI` named in the first draft of this proposal does not exist; see section 6)

Before D-002 acceptance, obtain an immutable upstream 2.2.0 OpenAPI or canonical reference artifact, confirm its exact version, upstream URL, commit/tag or release identifier, SHA-256, redistribution terms, and field/code semantics. A moving documentation URL or an unreviewed vendor variant is not a pinned normative artifact. If the exact public 2.2.0 artifact cannot be independently obtained and hashed, **D-002 remains blocked** rather than substituting a guessed schema. (Outcome: no tagged or immutable release exists, so the decision in section 8 accepted hash-pinned local snapshots of the hosted definitions, with that limitation stated.)

## 2. Alternatives

| Option | Benefits | Costs and decision |
| --- | --- | --- |
| DCSA Track & Trace 2.2.0, bounded adapter profile | Public documentation and existing implementation guidance; implementable without paid credentials | **Recommended; accepted 2026-11-03** (section 8) |
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

**Decision:** Recorded in section 8. G7 acceptance is not implied. M8's separate D-006 inbox/dead-letter decision remains open.

## 6. Upstream artifact observations (2026-11-03)

These are observations made during G7 execution. They do not accept D-002.

| Item | Observation |
| --- | --- |
| Repository named in the first draft | `dcsaorg/DCSA-OpenAPI` returns 404. `dcsaorg/DCSA-TNT` has tags `v2.0.0`, `v1.2.0` and `0.0.1-reactive` only and was last pushed 2023-08-04, so no 2.2.0 tag exists there |
| Public release listing | `reference.dcsa.org` lists Track & Trace v3 only; 2.2 is described by the public implementation guide |
| Candidate 2.2.0 definition | SwaggerHub API `dcsaorg/DCSA_TNT` version 2.2.0, OpenAPI 3.0.3, license declared as Apache 2.0, `last-modified` 2021-10-28 |
| Shared event definitions | Event code lists are in SwaggerHub domain `dcsaorg/EVENT_DOMAIN` version 1.0.4, which the 2.2.0 definition references; it also references `DCSA_DOMAIN` 1.0.3 and `ERROR_DOMAIN` 1.0.0 |
| Codes in the supported subset (as proposed) | Equipment `GTIN` and `GTOT`, transport `ARRI` and `DEPA`, and classifier `ACT` are all present in `EVENT_DOMAIN` 1.0.4. `EVENT_DOMAIN` also defines further codes and classifiers (for example `PLN`, `EST`) that the provisional subset does not accept |
| Immutability | SwaggerHub versions are not content addressed, and no signed or tagged release was found. A hash pins only the content observed on the day it was fetched |

SHA-256 of the content fetched on 2026-11-03 (JSON as served, retained in the E: verification root):

| File | SHA-256 |
| --- | --- |
| `DCSA_TNT` 2.2.0 | `b3c02f97f19833b3dca8edd95a328b1d6c0c2cacb1a0606d882a12d0f698c127` |
| `EVENT_DOMAIN` 1.0.4 | `5b9efaba91ea6ea9d855a59ae0670e252fc472eba5d3e9419962b9b5bcbc644c` |
| `DCSA_DOMAIN` 1.0.3 | `b6ed1f10185df3e680d20a5c5aee63da26cc76e49da33616400f517aa7ffb5e4` |
| `ERROR_DOMAIN` 1.0.0 | `15fcd4c9d3b5e529a8c170ca941ca9ec8a957df356322b38e41d25d022ca54e5` |

Open at the time of writing (resolved by the decision in section 8): a decision on whether a hash of mutable hosted content is an acceptable pin or a tagged repository release must be located, redistribution review of the fixtures, the field-by-field mapping review, and the explicit decision itself. Until then G7-S01 is BLOCKED, G7-S17 is BLOCKED for the upstream-mapping part, and the standard version and supported subset are unchanged.

## 7. Recommendation

This section is the advice given before the decision, which is recorded in section 8. Neither the advice nor the decision changes the supported subset or the profile `tnt-2.2-harborops-v1`.

**Recommended decision (option A):** accept DCSA Track & Trace 2.2.0 as the reference the HarborOps profile is derived from, pinned locally by SHA-256 of the four retrieved definitions, with the profile remaining a deliberately narrower HarborOps-specific interface and with no DCSA conformance claim.

### 7.1 Exact upstream version and artifacts

| Artifact | Version | URL | SHA-256 |
| --- | --- | --- | --- |
| Track & Trace API definition | 2.2.0 | `https://api.swaggerhub.com/apis/dcsaorg/DCSA_TNT/2.2.0` | `b3c02f97f19833b3dca8edd95a328b1d6c0c2cacb1a0606d882a12d0f698c127` |
| Event domain (event and code definitions) | 1.0.4 | `https://api.swaggerhub.com/domains/dcsaorg/EVENT_DOMAIN/1.0.4` | `5b9efaba91ea6ea9d855a59ae0670e252fc472eba5d3e9419962b9b5bcbc644c` |
| Common domain | 1.0.3 | `https://api.swaggerhub.com/domains/dcsaorg/DCSA_DOMAIN/1.0.3` | `b6ed1f10185df3e680d20a5c5aee63da26cc76e49da33616400f517aa7ffb5e4` |
| Error domain | 1.0.0 | `https://api.swaggerhub.com/domains/dcsaorg/ERROR_DOMAIN/1.0.0` | `15fcd4c9d3b5e529a8c170ca941ca9ec8a957df356322b38e41d25d022ca54e5` |

The files were fetched on 2026-11-03 and are retained in the E: verification root with their response headers.

### 7.2 Pinning and its limits

The hashes can be pinned locally, for example as a manifest in the repository with a verification step that fetches the four URLs and compares the hashes. That records exactly what was reviewed and detects change. It does not make the reference immutable: SwaggerHub serves mutable hosted versions, no tagged or signed release exists, and the repositories linked from the upstream descriptions for code lists (`DCSA-Information-Model`) were not found. A hash proves what was reviewed, not that DCSA still publishes the same content or that the content is the authoritative release.

### 7.3 License and redistribution

The 2.2.0 definition declares Apache 2.0 in `info.license`, and the DCSA reference site states the same license. That permits storing copies with notice. It is the publisher's own declaration, not an independent legal review. If copies are not wanted in the public repository, keep only the manifest of URLs and hashes in the repository and the files in the verification root.

### 7.4 Field-by-field differences

| Upstream (2.2.0 with `EVENT_DOMAIN` 1.0.4) | HarborOps profile | Difference |
| --- | --- | --- |
| `eventID` | `sourceEventId` | Source-local identifier; upstream identifier format not adopted |
| `eventType` with `equipmentEventTypeCode` or `transportEventTypeCode` | `eventFamily` with `eventCode` | Renamed and combined; only `GTIN`, `GTOT`, `ARRI`, `DEPA` accepted (upstream equipment codes also include LOAD, DISC, STUF, STRP, PICK, DROP, INSP, RSEA, RMVD) |
| `eventClassifierCode` (`ACT`, `PLN`, `EST`) | `classifier` | `ACT` only |
| `eventDateTime` | `occurredAtUtc` | Upstream accepts an offset date-time; the profile requires UTC |
| `eventCreatedDateTime` (required upstream, all events) | not accepted | **Required upstream field absent** |
| `emptyIndicatorCode` (required upstream, equipment events) | not accepted | **Required upstream field absent** |
| `transportCall` (required upstream, transport events) | `transportReference` | A single reference replaces the upstream call object; reduction, not an equivalent |
| `equipmentReference` | `equipmentReference` | Same name; value rules follow D-003 (synthetic and ISO forms) |
| `ISOEquipmentCode`, `eventLocation`, `documentReferences`, `delayReasonCode`, `transportCallID` | rejected as unknown fields | Intentional narrowing |
| none | `externalSchemaVersion` | HarborOps field that records the reference version |

### 7.5 Questions answered

- **Can the current adapter remain a deliberately narrower HarborOps interface?** Yes. `POST /integrations/v1/tracking-events` is already a HarborOps-specific route, not a DCSA publisher endpoint. A documented narrowing is coherent as long as it is described as such.
- **Can D-002 be accepted without claiming full DCSA conformance?** Yes. The decision would pin a reference the profile is derived from and list the two absent required fields and the reduced `transportCall` as known differences. As written, a conformance claim would be wrong because the profile omits two fields upstream requires.
- **Does option A require implementation changes?** No wire or code change. It needs a hash manifest with a drift check, documentation wording that states derivation and non-conformance, and a fixture provenance note. No new G7 evidence is needed for documentation alone.

### 7.6 Alternatives

| Option | Effect | Cost |
| --- | --- | --- |
| A (recommended) | Accept the local hash pin and the narrower profile, no conformance claim | Manifest and documentation only |
| B | Extend the profile to carry `eventCreatedDateTime`, `emptyIndicatorCode` and a `transportCall` object | New adapter version, schema, parser, fixtures and tests, a new source revision and a new G7 run; conformance still needs a separate review |
| C | Defer until a tagged release is located or adopt Track & Trace 3.0.0 when public | Delays M8; 3.0.0 is not publicly released and is not backward compatible |

### 7.7 Decisions needed (resolved in section 8)

1. Accept or reject a local SHA-256 pin of mutable hosted content as the D-002 reference (option A), or choose B or C.
2. Decide whether stored copies of the definitions may live in the public repository, or only the URL and hash manifest.
3. Confirm the intended scope of the narrowing (`ACT` only, four codes).

At the time of writing, D-002 was acceptance pending and G7-S01 and G7-S17 were BLOCKED. The decision in section 8 supersedes this status; the supported subset is unchanged.

## 8. Decision record

**Decision:** D-002 is accepted on 2026-11-03 as **Option A**. DCSA Track & Trace 2.2.0 is the reference baseline for the deliberately narrower HarborOps-specific adapter profile `tnt-2.2-harborops-v1`.

**Conditions and how they are met:**

| Condition | Status |
| --- | --- |
| Preserve the original downloaded bytes unmodified and record URL, version, retrieval date, SHA-256 and license for each artifact | Done in [`MANIFEST.json`](../third_party/dcsa-track-and-trace/MANIFEST.json) and the [reference README](../third_party/dcsa-track-and-trace/README.md) |
| Publish the four artifacts only where redistribution is confirmed | **Exception.** Only the Track & Trace 2.2.0 definition declares a license (Apache 2.0, DCSA contact) and is published. The `EVENT_DOMAIN` 1.0.4, `DCSA_DOMAIN` 1.0.3 and `ERROR_DOMAIN` 1.0.0 definitions declare no license, so they are **not published**; their exact bytes are preserved in the verification evidence on E: and their URLs and hashes are pinned |
| Copyright and license notices | The Apache 2.0 text and an attribution notice accompany the published snapshot; the repository license is stated not to apply to that directory |
| Offline hash verification of the committed snapshots | `scripts/verify-dcsa-reference.mjs`, also run by the M7 contract tests |
| Optional upstream drift check that never updates pins | `scripts/verify-dcsa-reference.mjs --upstream` |
| State plainly that the snapshots are not official immutable releases | Stated in the manifest, the reference README, this record, the API contract and the architecture |
| Keep the tracking contract and supported subset unchanged | Unchanged: Equipment `GTIN` and `GTOT`, Transport `ARRI` and `DEPA`, classifier `ACT` |
| Document the field differences | Section 7.4 and the reference README: `eventCreatedDateTime` and `emptyIndicatorCode` are absent, `transportCall` is reduced to `transportReference` |

**Approved limitation:** the pins are local snapshots of previously retrieved upstream content. They are not official immutable DCSA releases, and HarborOps does not represent them as such. This decision approves that limitation for M7.

**Consequences:**

1. The profile is HarborOps-specific and **not DCSA conformant**. No conformance claim may be made.
2. Events in the DCSA wire format cannot be consumed directly; a translation layer is required. The profile renames fields, accepts a restricted value set, requires UTC and omits two required upstream fields, so a DCSA-format event would be rejected as an unknown or incomplete HarborOps event.
3. A change to a pin, to the supported subset or to the required fields needs a new D-002 decision with an explicit adapter version, new fixtures and a new verification record. A drift report alone changes nothing.
4. The withheld artifacts' redistribution status can be revisited if DCSA publishes an explicit license for them.
5. **Scope of the pin.** The four artifacts are deliberately limited to the reviewed HarborOps subset. They are not a complete transitive DCSA schema snapshot. `EVENT_DOMAIN` 1.0.4 also references `DOCUMENTATION_DOMAIN` 1.0.4 (which defines `references`, `seals`, `partyNoID` and `vessel`) and `LOCATION_DOMAIN` 1.0.3 (which defines `location` and `geoLocation`). Neither domain was retrieved, pinned, reviewed or license-checked. The approved mapping does not depend on them, because the profile accepts none of those shapes and reduces `transportCall` to `transportReference`. Pinning them would be a new decision.
