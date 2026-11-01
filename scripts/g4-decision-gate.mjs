// Evaluate only D-003's own decision record, never a later decision's status.
function section(document, heading) {
  const lines = document.split(/\r?\n/);
  const start = lines.findIndex(line => heading.test(line));
  if (start < 0) return null;
  let end = start + 1;
  while (end < lines.length && !/^##\s+/.test(lines[end])) end++;
  return lines.slice(start + 1, end).join("\n");
}
export function isD003Accepted(document) {
  const record = section(document, /^##\s+(?:Proposed\s+)?D-003\b/);
  const open = section(document, /^##\s+Open decisions\s*$/);
  if (!record || !open || /^\|\s*D-003\s*\|/m.test(open)) return false;
  const required = [
    /^\*\*Status:\*\*\s*Accepted\s*$/m,
    /^\*\*Identifier policy:\*\*\s*\S.+$/m,
    /^\*\*Standard edition:\*\*\s*ISO\s*6346:\d{4}\s*$/mi,
    /^\*\*Category scope:\*\*\s*\S.+$/m
  ];
  return required.every(rule => rule.test(record));
}
