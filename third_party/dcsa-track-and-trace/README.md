# DCSA Track & Trace 2.2.0 reference snapshot

This directory holds the pinned third-party reference for the HarborOps tracking profile `tnt-2.2-harborops-v1`, accepted under D-002 Option A on 2026-11-03. See the [D-002 decision record](../../docs/M7_D002_TRACKING_REFERENCE_PROPOSAL.md).

## What these files are, and are not

- They are **immutable local snapshots of upstream content retrieved on 2026-11-03**. The bytes are stored exactly as downloaded and pinned by SHA-256.
- They are **not official immutable DCSA releases**. The upstream definitions are hosted on SwaggerHub, which serves mutable versions; no tagged or signed release was found. The pins record what was reviewed, not that DCSA still publishes identical content. Accepting this limitation was part of the D-002 decision for M7.
- The snapshot is **deliberately limited to the reviewed HarborOps subset**. It is **not a complete transitive DCSA schema snapshot** (see the scope section below).
- The HarborOps profile is a **deliberately narrower, HarborOps-specific adapter**. It is **not DCSA conformant** and makes no conformance claim.
- **Events in the DCSA wire format cannot be consumed directly.** A translation layer is required, because the profile renames fields, restricts values and omits required upstream fields (see below).

## Scope of the snapshot

The four pinned artifacts are the definitions that the reviewed HarborOps subset needs: the Track & Trace 2.2.0 definition and the event, common and error domains it references. Following references further is outside the reviewed subset:

| Not pinned | Version | Referenced by | Shapes it defines that the 2.2.0 and event definitions use |
| --- | --- | --- | --- |
| `DOCUMENTATION_DOMAIN` | 1.0.4 | `EVENT_DOMAIN` 1.0.4 (6 references) | `references`, `seals`, `partyNoID`, `vessel` |
| `LOCATION_DOMAIN` | 1.0.3 | `EVENT_DOMAIN` 1.0.4 (4 references) | `location`, `geoLocation` |

These two domains were not retrieved, hashed, reviewed or license-checked, and they are not part of this reference. The pinned files therefore still contain external references that cannot be resolved from the snapshot alone, and the snapshot cannot be used to regenerate the complete upstream schema.

The approved mapping does not depend on them. The HarborOps profile accepts none of those shapes: it rejects `documentReferences`, `eventLocation` and the other fields in the difference table as unknown fields, and it reduces the upstream `transportCall` object to the single `transportReference`. Adding any of those fields later is a new D-002 decision with an explicit adapter version, as described under "Changing a pin".

## Artifacts

| Artifact | Version | Upstream URL | SHA-256 | Bytes | Declared license | In this repository |
| --- | --- | --- | --- | --- | --- | --- |
| Track & Trace API definition | 2.2.0 | `https://api.swaggerhub.com/apis/dcsaorg/DCSA_TNT/2.2.0` | `b3c02f97f19833b3dca8edd95a328b1d6c0c2cacb1a0606d882a12d0f698c127` | 19889 | Apache 2.0 | Yes: `snapshots/swaggerhub-DCSA_TNT-2.2.0.json` |
| Event domain (event and code definitions) | 1.0.4 | `https://api.swaggerhub.com/domains/dcsaorg/EVENT_DOMAIN/1.0.4` | `5b9efaba91ea6ea9d855a59ae0670e252fc472eba5d3e9419962b9b5bcbc644c` | 41249 | none declared | **Withheld** |
| Common domain | 1.0.3 | `https://api.swaggerhub.com/domains/dcsaorg/DCSA_DOMAIN/1.0.3` | `b6ed1f10185df3e680d20a5c5aee63da26cc76e49da33616400f517aa7ffb5e4` | 34259 | none declared | **Withheld** |
| Error domain | 1.0.0 | `https://api.swaggerhub.com/domains/dcsaorg/ERROR_DOMAIN/1.0.0` | `15fcd4c9d3b5e529a8c170ca941ca9ec8a957df356322b38e41d25d022ca54e5` | 1330 | none declared | **Withheld** |

All four were retrieved on 2026-11-03. The machine-readable record is [`MANIFEST.json`](MANIFEST.json).

### Redistribution exception

Only the Track & Trace 2.2.0 definition declares a license inside the artifact (`info.license`: Apache 2.0, contact: Digital Container Shipping Association). The three domain definitions declare no license, copyright or contact, the hosting metadata names an owner but no license, and the DCSA website states only a general copyright notice. Redistribution permission could not be confirmed for them, so they are **not published here**. Their exact bytes are preserved in the maintainers' verification evidence on E:, and their URLs and hashes are pinned in the manifest. This is a technical reading of the declarations in the artifacts, not a legal opinion.

## License and notice

The Track & Trace 2.2.0 definition is Copyright the Digital Container Shipping Association (DCSA) and is licensed under the Apache License, Version 2.0. A copy of the license is in [`LICENSE-APACHE-2.0.txt`](LICENSE-APACHE-2.0.txt). The snapshot is reproduced without modification, and the artifact contains no NOTICE text. The HarborOps repository license does not apply to this directory; its contents remain under their own terms.

## Verification

Offline, no network (also run as part of the M7 contract tests):

```bash
node scripts/verify-dcsa-reference.mjs
```

With the withheld copies, which also confirms that every supported event code and classifier exists in the pinned upstream enumerations:

```bash
node scripts/verify-dcsa-reference.mjs --external-dir <directory holding the withheld files>
```

Optional upstream drift check. It fetches the hosted versions and compares them with the pins; it never writes a file and never changes a pin. Exit code 3 means drift, 4 means the upstream could not be reached:

```bash
node scripts/verify-dcsa-reference.mjs --upstream
```

## Differences between the HarborOps profile and the upstream definitions

| Upstream (2.2.0 with `EVENT_DOMAIN` 1.0.4) | HarborOps profile `tnt-2.2-harborops-v1` | Difference |
| --- | --- | --- |
| `eventID` | `sourceEventId` | Source-local identifier; upstream identifier format not adopted |
| `eventType` with `equipmentEventTypeCode` or `transportEventTypeCode` | `eventFamily` with `eventCode` | Renamed and combined; only `GTIN`, `GTOT`, `ARRI`, `DEPA` accepted (upstream equipment codes also include LOAD, DISC, STUF, STRP, PICK, DROP, INSP, RSEA, RMVD) |
| `eventClassifierCode` (`ACT`, `PLN`, `EST`) | `classifier` | `ACT` only |
| `eventDateTime` | `occurredAtUtc` | Upstream accepts an offset date-time; the profile requires UTC |
| `eventCreatedDateTime` (required upstream, all events) | not accepted | **Required upstream field absent** |
| `emptyIndicatorCode` (required upstream, equipment events) | not accepted | **Required upstream field absent** |
| `transportCall` (required upstream, transport events) | `transportReference` | **Reduced**: a single reference replaces the upstream call object |
| `equipmentReference` | `equipmentReference` | Same name; value rules follow D-003 (synthetic and ISO forms) |
| `ISOEquipmentCode`, `eventLocation`, `documentReferences`, `delayReasonCode`, `transportCallID` | rejected as unknown fields | Intentional narrowing |
| none | `externalSchemaVersion` | HarborOps field that records the reference version |

## Changing a pin

A pin changes only through a new D-002 decision that adds an explicit adapter version, new fixtures and a new verification record. A drift report alone never updates a pin.
