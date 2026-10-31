# D-003 Container Identifier Policy: Decision Proposal

**Decision ID:** D-003  
**Milestone:** M4-01  
**State:** Proposed, decision pending. No implementation authorization or accepted decision recorded.  
**Basis:** [Roadmap](ROADMAP.md), [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [Requirements](REQUIREMENTS.md), [Decisions](DECISIONS.md).

## Context

M4 adds organization-owned container reference records. FR-005 ultimately requires shipment/container/voyage assignment in M6, not M4. The identifier policy must allow a free, deterministic demonstration without claiming that fictional equipment is officially registered, while preventing incorrect or ambiguous real-format container numbers.

The BIC description and ISO 6346:2022 container identification model use three owner letters, one equipment category letter, six serial digits and one check digit. Categories include U (freight container), J (detachable equipment) and Z (trailer/chassis). A valid checksum verifies transcription, not BIC registration, equipment ownership or physical existence. Authoritative references:
- BIC container identification: https://www.bic-code.org/identification-number/
- BIC check digit reference: https://www.bic-code.org/check-digit-calculator/
- ISO 6346 catalog: https://www.iso.org/standard/90821.html
- WCO reproduction of ISO 6346 identification structure: https://www.wcoomd.org/-/media/wco/public/global/pdf/topics/facilitation/instruments-and-tools/conventions/convention-containers-1972/handbook_convention_containers_1972.pdf?la=en

The referenced standard edition, owner-code registry and permitted equipment categories must be explicitly pinned when D-003 is accepted. Do not silently switch the parser when a standards revision is published.

## Alternatives

| Option | Rule | Benefit | Cost or risk |
| --- | --- | --- | --- |
| A: Strict ISO-only | Every container has an 11-character ISO 6346-formatted ID with checksum | Simple uniform parser | Cannot represent explicitly fictional demonstration references without generating plausible external identities |
| B: Tagged real-format and synthetic | Distinct `ISO6346` and `Synthetic` kinds, with separate mutually exclusive parsers | Strong demo honesty, offline support, unambiguous identifiers | Requires kind column and two validation paths |
| C: Free-form local identifier | Arbitrary normalized tenant-local ID, no ISO validation | Easy data entry | Loses useful equipment-ID validation and risks confusing demo strings with actual registered boxes |

## Recommended option: B, pending decision

1. Every container receives immutable server-generated UUID `id` and immutable tenant-local `reference_code`, independent of external or demonstration identifiers.
2. The mutually exclusive `identifier_kind` is either `ISO6346` or `Synthetic`. This classification is immutable. Supply one required `container_identifier` and validate using the selected kind.
3. `ISO6346`: remove only documented presentation spaces, uppercase, require exactly 11 characters matching `[A-Z]{3}U[0-9]{7}` for the freight-container scope, then verify the ISO 6346 weighted check digit. Accept only category U at M4. This M4 scope deliberately excludes J/Z accessories and chassis, which need separate domain modeling. Never imply that a passing check digit proves BIC registration. Retain the check digit even if it is `0`.
4. `Synthetic`: require visibly nonreal local identifiers matching `SYN-BOX-[A-Z0-9]{4,16}`, with no ability to parse as an ISO ID. Store `source_kind=Synthetic`; return `isSynthetic=true` and show a conspicuous Synthetic label in all views and exported representations.
5. Normalize the identifier in one pure, well-tested domain function. Store the canonical result and enforce matching PostgreSQL CHECK constraints, including an independent SQL checksum verification for ISO identifiers so direct SQL cannot bypass policy.
6. Enforce uniqueness of `(organization_id, container_identifier)` across active and archived rows, regardless of kind. Permit the same identifier in different organizations because the model is an organization-specific operational reference catalog, not a global equipment title registry.
7. Never reuse archived identifiers for another record in the same organization. Use soft archive plus optimistic versioning and reject changes to identity, source kind and immutable creation fields.
8. A synthetic identifier cannot be silently converted to a real identifier in place. A separate future migration/import and review would be needed if converting such a record into verified real equipment.
9. M4 implements syntax/checksum validation only. No registry API, BIC owner lookup, BoxTech verification, external license or paid account is required.

## Check digit algorithm to pin

For the ten-character `owner+U+serial` prefix:
- Map A-Z to the ISO letter values starting at A=10, B=12, C=13, skipping multiples of 11 and continuing through Z=38; digits retain numeric values.
- Multiply each character's value by `2^position` for zero-based positions 0 through 9.
- Compute total modulo 11, then modulo 10; compare with the eleventh character.
- Document and test valid known check-digit vectors, invalid checks, leading-zero serials, prohibited category and wrong-length values. Do not auto-correct a user-supplied invalid checksum.

The exact equivalence to the pinned normative standard must be verified before implementation acceptance; this proposal is not an approved parser specification.

## Decisions required before M4 implementation

- Accept option B or explicitly choose A/C with documented trade-offs.
- Pin the ISO 6346 edition and decide whether M4 covers freight containers only (recommended U only).
- Approve the canonical synthetic prefix, allowed length, and whether storage/response fields are `containerIdentifier` and `identifierKind`.
- Confirm type-catalog minimum for M4: `Dry20`, `Dry40`, `HighCube40`, `Reefer20`, `Reefer40`, `Other`; these are local equipment categories and are not asserted to be ISO size-type codes.
- Approve ownership-scoped uniqueness and immutable physical identifier; cross-organization duplicate numbers are intentionally allowed.

**Decision is pending.** This proposal does not close D-003, start M4-02/M4-03, or satisfy G4. Record explicit acceptance and update [Decisions](DECISIONS.md), [M4 architecture](M4_ARCHITECTURE_DESIGN.md), validation tests and migration design when a choice is made.
