// The CURRENT accepted-gate state that the planning documents must state, kept separate from the structural checks
// in check-planning-docs.mjs so each statement has a negative control. The statements here were verified against the
// authoritative closure records (G8, G9, G11/G12, G13, G15, G16 closures and the execution graph); when a gate is
// accepted later, this file and the documents change together, never one without the other.
//
// History: the checker used to require the literal phrases "G0-G12 and G16 are accepted" and "G13-G15 and G17-G24
// remain open". Both were true on 2026-11-08 and became false when G15 was accepted and G13 conditionally accepted on
// 2026-11-12. The documents were right and the checker was stale. These rules keep the same intent (the current
// accepted set is stated, G17 and later are not claimed) in the form the documents now use.

export const CURRENT_STATE_RULES = [
  {id: "status-accepted-set", doc: "status", test: t => /M0-M12\/G0-G12 and M16\/G16 are accepted and integrated/.test(t),
    message: "STATUS must state the accepted set G0-G12 and G16 in its current form"},
  {id: "status-m11-ledger", doc: "status", test: t => /\| \*\*M11 Delay rules and shipment exceptions\*\* \| \*\*G11 ACCEPTED, merged in PR #33\*\*/.test(t),
    message: "STATUS must show G11 accepted in the M11 ledger row"},
  {id: "status-m12-ledger", doc: "status", test: t => /\| \*\*M12 Shipment search and authorized export\*\* \| \*\*G12 ACCEPTED, merged in PR #33\*\*/.test(t),
    message: "STATUS must show G12 accepted in the M12 ledger row"},
  {id: "status-g13-conditional", doc: "status", test: t => /\| \*\*M13 [^|]*\*\* \| \*\*G13 conditionally accepted/.test(t),
    message: "STATUS must keep G13 conditionally accepted"},
  {id: "status-g15-accepted", doc: "status", test: t => /\| \*\*M15 [^|]*\*\* \| \*\*G15 and S25 ACCEPTED/.test(t),
    message: "STATUS must show G15 accepted"},
  {id: "status-g14-planned", doc: "status", test: t => /\| \*\*M14 [^|]*\*\* \| Planned \|/.test(t),
    message: "STATUS must keep M14 planned"},
  {id: "roadmap-accepted-set", doc: "roadmap", test: t => /G0-G12, G15 and G16 accepted and merged\. G13 remains conditionally accepted and merged/.test(t),
    message: "ROADMAP must state G0-G12, G15 and G16 accepted and G13 conditionally accepted"},
  {id: "roadmap-release-pending", doc: "roadmap", test: t => /G24 release acceptance remain pending/.test(t),
    message: "ROADMAP must keep G24 release acceptance pending"},
  {id: "contributors-accepted-set", doc: "contributors", test: t => /G0-G12, G15 and G16 are accepted and integrated\. G13 remains conditionally accepted/.test(t),
    message: "CONTRIBUTING must state the current accepted gate set"}
];

// G17 and every later gate must never be stated as accepted in a current statement. Prerequisite lists ("G17
// accepted, G16 accepted" under an entry condition) and quoted "NOT ACCEPTED" statements are not claims, so only
// affirmative forms are rejected: an upper-case ACCEPTED in a status position, an acceptance date, a granted or
// recorded acceptance, or a milestone stated as accepted.
export const FORBIDDEN_ACCEPTANCE_CLAIMS = [
  /\bG(?:1[7-9]|2[0-4])(?:-S25)? ACCEPTED\b/,
  /\bG(?:1[7-9]|2[0-4])(?:-S25)? accepted (?:on )?20\d\d/,
  /\bG(?:1[7-9]|2[0-4]) acceptance (?:was |is |has been )?(?:granted|recorded|given)\b/i,
  /\bM(?:1[7-9]|2[0-4]) (?:is |was |has been )?accepted\b/
];

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

// Statements that must be present in the CURRENT G17 records (the exact wording of the formal decisions).
export const G17_CURRENT_STATEMENTS = [
  {id: "g17-not-accepted", doc: "status", test: t => /G17 NOT ACCEPTED/.test(t)},
  {id: "p09-qualified-pass", doc: "status", test: t => /QUALIFIED PASS/.test(t)},
  {id: "pre-release-optimization", doc: "status", test: t => /optimization|Optimization/.test(t) && /pre-release|G24|release readiness/i.test(t)},
  {id: "g17-not-accepted-readme", doc: "readme", test: t => /G17 NOT ACCEPTED/.test(t)},
  {id: "g17-not-accepted-roadmap", doc: "roadmap", test: t => /G17 NOT ACCEPTED/.test(t)},
  {id: "g17-not-accepted-acceptance", doc: "acceptance", test: t => /G17 NOT ACCEPTED|NOT ACCEPTED/.test(t)}
];

export function g17StatementProblems(documents) {
  return G17_CURRENT_STATEMENTS.filter(s => typeof documents[s.doc] !== "string" || !s.test(documents[s.doc])).map(s => `${s.id} is missing`);
}
