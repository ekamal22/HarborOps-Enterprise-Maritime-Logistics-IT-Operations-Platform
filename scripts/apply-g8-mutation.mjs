import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const [variant, root] = process.argv.slice(2);
assert.ok(variant && root && fs.existsSync(root), "Mutation variant and isolated export required");
const store = "src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs";
const migration = "src/HarborOps.Infrastructure/Persistence/Migrations/20261103190000_TrackingInboxAndHistory.cs";
const rules = {
  "final-digest": {
    path: store,
    from: "CryptographicOperations.FixedTimeEquals(expected, digest)",
    to: "true"
  },
  "collision-as-replay": {
    path: store,
    from: "if (originalCanonical.AsSpan().SequenceEqual(canonical.PayloadUtf8))",
    to: "if (true)"
  },
  "early-ack": {
    path: store,
    from: "        await transaction.CommitAsync(ct);\n        return new(TrackingReceiptOutcome.Received, receipt, now);",
    to: "        // Disposable mutation: return without committing the receipt transaction.\n        return new(TrackingReceiptOutcome.Received, receipt, now);"
  },
  quota: {
    path: store,
    from: "if (pending >= PendingLimit)",
    to: "if (false)"
  },
  "append-only": {
    path: migration,
    from: "            CREATE TRIGGER m8_receipt_immutable\n                BEFORE INSERT OR UPDATE OR DELETE ON tracking_inbox_receipts\n                FOR EACH ROW EXECUTE FUNCTION m8_guard_append_only();",
    to: "            -- Disposable mutation: receipt append-only guard omitted."
  },
  "no-work": {
    path: store,
    from: "        await ExecuteAsync(connection, current, \"\"\"\n            INSERT INTO tracking_inbox_work_items",
    to: "        await ExecuteAsync(connection, current, \"\"\"\n            INSERT INTO tracking_inbox_work_items",
    deleteBlock: true
  }
};
const rule = rules[variant];
assert.ok(rule, "Unsupported G8 mutation");
const file = path.join(root, rule.path);
const source = fs.readFileSync(file, "utf8");
let output;
if (rule.deleteBlock) {
  const start = source.indexOf(rule.from);
  const end = source.indexOf("\n\n        // The deferred completeness trigger", start);
  assert.ok(start >= 0 && end > start, "Work-item mutation anchor missing");
  assert.equal(source.indexOf(rule.from, start + 1), -1);
  output = source.slice(0, start) +
    "        // Disposable mutation: work-state record intentionally omitted." +
    source.slice(end);
} else {
  assert.equal(source.split(rule.from).length - 1, 1, "Mutation anchor missing or ambiguous");
  output = source.replace(rule.from, rule.to);
}
fs.writeFileSync(file, output);
console.log("Applied disposable G8 mutation: " + variant);
