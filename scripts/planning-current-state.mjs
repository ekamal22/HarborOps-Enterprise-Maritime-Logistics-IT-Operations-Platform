// The CURRENT accepted-gate state that the planning documents must state, kept separate from the structural checks
// in check-planning-docs.mjs so each statement has a negative control. The statements here were verified against the
// authoritative closure records (G8, G9, G11/G12, G13, G15, G16 closures and the execution graph); when a gate is
// accepted later, this file and the documents change together, never one without the other.
//
// History: the checker used to require the literal phrases "G0-G12 and G16 are accepted" and "G13-G15 and G17-G24
// remain open". Both were true on 2026-11-08 and became false when G15 was accepted and G13 conditionally accepted on
// 2026-11-12. The documents were right and the checker was stale. These rules keep the same intent (the current
// accepted set is stated, G14 and G18-G24 are not claimed) in the form the documents now use.

export const CURRENT_STATE_RULES = [
  {id: "status-accepted-set", doc: "status", test: t => /G0-G12, G15, G16 and G17 are accepted and integrated into protected `main`/.test(t),
    message: "STATUS must state the currently integrated G0-G12, G15-G17 set without treating G13 as unconditional"},
  {id: "status-m11-ledger", doc: "status", test: t => /\| \*\*M11 Delay rules and shipment exceptions\*\* \| \*\*G11 ACCEPTED, merged in PR #33\*\*/.test(t),
    message: "STATUS must show G11 accepted in the M11 ledger row"},
  {id: "status-m12-ledger", doc: "status", test: t => /\| \*\*M12 Shipment search and authorized export\*\* \| \*\*G12 ACCEPTED, merged in PR #33\*\*/.test(t),
    message: "STATUS must show G12 accepted in the M12 ledger row"},
  {id: "status-g13-conditional", doc: "status", test: t => /\| \*\*M13 [^|]*\*\* \| \*\*G13 conditionally accepted/.test(t),
    message: "STATUS must keep G13 conditionally accepted"},
  {id: "status-g15-accepted", doc: "status", test: t => /\| \*\*M15 [^|]*\*\* \| \*\*G15 and S25 ACCEPTED/.test(t),
    message: "STATUS must show G15 accepted"},
  {id: "status-g17-integrated", doc: "status", test: t => /\| \*\*M17 [^|]*\*\* \| \*\*G17 ACCEPTED AND MERGED/.test(t),
    message: "STATUS must show G17 accepted and merged"},
  {id: "status-g14-planned", doc: "status", test: t => /\| \*\*M14 [^|]*\*\* \| Planned \|/.test(t),
    message: "STATUS must keep M14 planned"},
  {id: "event-current-set", doc: "events", test: t => /Current accepted-gate position \(2026-11-17\):.*G0-G12, G15, G16 and G17 are accepted and integrated; G13 remains conditionally accepted/.test(t),
    message: "EVENT_PROCESSING must use the current acceptance baseline"},
  {id: "m17-delivery-outcome", doc: "m17Plan", test: t => /Current outcome \(2026-11-17\):\*\* M17-01 through M17-04 were implemented and integrated through PR #53/.test(t),
    message: "M17 plan must no longer claim implementation, verification or merge is absent"},
  {id: "graph-accepted-m17", doc: "graph", test: t => /Gate-status clarification \(2026-11-17\):\*\* G0-G12, G15, G16 and G17 are integrated/.test(t) && /G17 was accepted for integration after the authenticated Campaign 01 technical PASS/.test(t),
    message: "Generated graph must reflect integrated G17 and retain the S25 qualification"},
  {id: "roadmap-accepted-set", doc: "roadmap", test: t => /G0-G12, G15, G16 and G17 accepted and merged\. G13 remains conditionally accepted and merged/.test(t),
    message: "ROADMAP must keep the integrated and conditional sets distinct"},
  {id: "roadmap-release-pending", doc: "roadmap", test: t => /G24 release acceptance remain pending/.test(t),
    message: "ROADMAP must keep G24 release acceptance pending"},
  {id: "contributors-accepted-set", doc: "contributors", test: t => /G0-G12, G15, G16 and G17 are accepted and integrated\. G13 remains conditionally accepted/.test(t),
    message: "CONTRIBUTING must reflect the integrated and conditional gate sets"}
]

// G14 and G18-G24 are not accepted. G17 was integrated on 2026-11-17.
// Prerequisite wording and dated historical failure records are not affirmative claims.
// The negative controls below protect the remaining unaccepted gates, without blocking G17.
export const FORBIDDEN_ACCEPTANCE_CLAIMS = [
  /\bG(?:14|1[89]|2[0-4])(?:-S25)? ACCEPTED\b/,
  /\bG(?:14|1[89]|2[0-4])(?:-S25)? accepted (?:on )?20\d\d/,
  /\bG(?:14|1[89]|2[0-4]) acceptance (?:was |is |has been )?(?:granted|recorded|given)\b/i,
  /\bM(?:14|1[89]|2[0-4]) (?:is |was |has been )?accepted\b/
]

export function currentStateProblems(documents) {
  const problems = [];
  for (const rule of CURRENT_STATE_RULES) {
    const text = documents[rule.doc];
    if (typeof text !== "string" || !rule.test(text)) problems.push(`${rule.id}: ${rule.message}`);
  }
  return problems;
}

export function forbiddenClaimProblems(files) {
  const problems = [];
  for (const [name, text] of Object.entries(files)) {
    for (const [index, line] of text.split("\n").entries())
      for (const pattern of FORBIDDEN_ACCEPTANCE_CLAIMS)
        if (pattern.test(line.replaceAll("NOT ACCEPTED", "not-a-claim").replaceAll("not accepted", "not-a-claim")))
          problems.push(`${name}:${index + 1} claims a later gate is accepted`);
  }
  return problems;
}

// Statements that must be present in the current G17 records, without rewriting older failed evidence.
export const G17_CURRENT_STATEMENTS = [
  {id: "g17-integrated-status", doc: "status", test: t => /G17 integration status \(2026-11-17\): ACCEPTED AND MERGED/.test(t)},
  {id: "g17-s25-outstanding", doc: "status", test: t => /signed G17 S25-A03\/F06 reviewer instrument remains outstanding/.test(t)},
  {id: "p09-qualified-pass", doc: "status", test: t => /P09 remains QUALIFIED PASS/.test(t)},
  {id: "pre-release-optimization", doc: "status", test: t => /optimization|Optimization/.test(t) && /pre-release|G24|release readiness/i.test(t)},
  {id: "g17-integrated-readme", doc: "readme", test: t => /G17 integration status \(2026-11-17\): ACCEPTED AND MERGED/.test(t)},
  {id: "g17-integrated-roadmap", doc: "roadmap", test: t => /G0-G12, G15, G16 and G17 accepted and merged/.test(t)},
  {id: "g17-integrated-acceptance", doc: "acceptance", test: t => /G17[^\n]*ACCEPTED AND INTEGRATED 2026-11-17/.test(t)}
]

export function g17StatementProblems(documents) {
  return G17_CURRENT_STATEMENTS.filter(s => typeof documents[s.doc] !== "string" || !s.test(documents[s.doc])).map(s => `${s.id} is missing`);
}
