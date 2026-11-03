import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const [variant,root]=process.argv.slice(2);
assert.ok(variant && root && fs.existsSync(root), "Mutation variant and isolated worktree required");
const rules={
  digest: {
    path:"src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs",
    from:"CryptographicOperations.FixedTimeEquals(expected, digest)",
    to:"true"
  },
  suspension: {
    path:"src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs",
    from:'reader.GetString(5) == "Active"',
    to:"true"
  },
  revocation: {
    path:"src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs",
    from:"AND c.revoked_at_utc IS NULL",
    to:"AND (c.revoked_at_utc IS NULL OR c.revoked_at_utc IS NOT NULL)"
  },
  size: {
    path:"src/HarborOps.Domain/Tracking/TrackingEventPolicy.cs",
    from:"MaximumBodyBytes = 16384",
    to:"MaximumBodyBytes = 65536"
  },
  "credential-guard": {
    path:"src/HarborOps.Infrastructure/Persistence/Migrations/20261103090000_IntegrationSources.cs",
    from:"CREATE TRIGGER m7_credential_guard BEFORE INSERT OR UPDATE OR DELETE ON integration_credentials\n                FOR EACH ROW EXECUTE FUNCTION m7_guard_credential();",
    to:"-- Credential lifecycle trigger absent in this disposable variant"
  }
};
const rule=rules[variant];
assert.ok(rule, "Unknown controlled mutation");
const filename=path.join(root,rule.path);
const content=fs.readFileSync(filename,"utf8");
assert.equal(content.split(rule.from).length-1,1, "Mutation anchor missing or ambiguous");
fs.writeFileSync(filename,content.replace(rule.from,rule.to));
console.log("Applied controlled mutation in isolated E: export: "+variant);
