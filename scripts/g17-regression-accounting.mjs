import assert from "node:assert/strict";

// A named accepted-milestone suite is only a witness when every test it declares
// ran and passed. One surviving test must not stand in for a trimmed suite, and a
// source-level skip is refused even if the runner would report it neutrally.
export function declaredTestMethods(source, className) {
  assert.equal(typeof source, "string");
  assert.ok(!/\bSkip\s*=/.test(source), "Source-level skip in accepted regression suite " + className);
  const declared = [];
  // Custom attributes such as ExclusiveM13PostgresFact derive from Fact or Theory.
  const pattern = /\[(?:\w*Fact|\w*Theory)\b[^\]]*\](?:\s*\[[^\]]*\])*\s*(?:public|internal)\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(/g;
  for (const match of source.matchAll(pattern)) declared.push(match[1]);
  const attributes = (source.match(/\[(?:\w*Fact|\w*Theory)\b/g) || []).length;
  assert.equal(declared.length, attributes,
    "Unparsed test attribute in " + className + " (" + attributes + " attributes, " +
    declared.length + " methods)");
  assert.ok(declared.length >= 1, "No declared tests in " + className);
  assert.equal(new Set(declared).size, declared.length, "Duplicate test method in " + className);
  return declared;
}

export function accountSuite(className, source, executedNames) {
  const declared = declaredTestMethods(source, className);
  const executed = new Set(executedNames
    .filter(name => name.includes("." + className + "."))
    .map(name => name.split("(")[0].split("." + className + ".").pop()));
  const missing = declared.filter(name => !executed.has(name));
  assert.deepEqual(missing, [],
    "Declared tests that did not run in " + className + ": " + missing.join(", "));
  return {declared: declared.length, executed_distinct: executed.size};
}
