import assert from "node:assert/strict";
import fs from "node:fs";
const [variant,root]=process.argv.slice(2);
assert.ok(variant && root && fs.existsSync(root),"Mutation name and exported source root required");
const rules={
  "tenant-read":{
    path:"src/HarborOps.Infrastructure/Assignments/AssignmentStore.cs",
    from:"x.OrganizationId == org && x.ShipmentId == shipment && x.Id == id, ct",
    to:"x.Id == id, ct"
  },
  "post-lock":{
    path:"src/HarborOps.Infrastructure/Assignments/AssignmentStore.cs",
    from:"return resolved.Value is null ? resolved.Error : OrganizationAccessError.None;",
    to:"return OrganizationAccessError.None;"
  },
  "overlap":{
    path:"src/HarborOps.Infrastructure/Persistence/Migrations/20261102080000_ShipmentAssignments.cs",
    from:"ALTER TABLE shipment_container_assignments\n                ADD CONSTRAINT ex_m6_container_overlap EXCLUDE USING gist\n                (organization_id WITH =, container_id WITH =,\n                 tstzrange(starts_at_utc,ends_at_utc,'[)') WITH &&);",
    to:"-- exclusion intentionally absent in disposable mutation"
  },
  "parent-guard":{
    path:"src/HarborOps.Infrastructure/Persistence/Migrations/20261102080000_ShipmentAssignments.cs",
    from:"CREATE TRIGGER m6_guard_container BEFORE UPDATE OR DELETE ON containers\n                FOR EACH ROW EXECUTE FUNCTION m6_guard_parent();",
    to:"-- container archival trigger intentionally absent in disposable mutation"
  }
};
const rule=rules[variant];
assert.ok(rule,"Unsupported mutation");
const file=root+"/"+rule.path;
const source=fs.readFileSync(file,"utf8");
const count=source.split(rule.from).length-1;
assert.equal(count,1,"Mutation anchor changed or became ambiguous");
fs.writeFileSync(file,source.replace(rule.from,rule.to));
console.log("Applied "+variant+" in disposable exported source only");
