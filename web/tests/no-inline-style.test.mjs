import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

// The web tier serves a strict policy (style-src 'self', no unsafe-inline), so no
// application source may create an inline style: not a React style prop, not a style
// element and not a direct style write.
const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..", "src");

function sources(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return sources(full);
    return /\.(tsx?|jsx?)$/.test(entry.name) ? [full] : [];
  });
}

test("application source never creates an inline style", () => {
  const offenders = [];
  for (const file of sources(root)) {
    const text = fs.readFileSync(file, "utf8")
      .replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
    for (const pattern of [/style\s*=\s*\{/, /<style[\s>]/, /\.style\./, /\.style\s*=/,
      /setAttribute\(\s*["']style["']/, /cssText/, /insertRule/, /adoptedStyleSheets/])
      if (pattern.test(text)) offenders.push(path.relative(root, file) + " " + pattern);
  }
  assert.deepEqual(offenders, []);
});
