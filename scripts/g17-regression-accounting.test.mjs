import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {accountSuite,declaredTestMethods} from "./g17-regression-accounting.mjs";
const source=`public sealed class Suite {
  [Fact] public async Task One() {}
  [Theory]
  [InlineData(1)]
  public async Task Two(int x) {}
  [Fact]
  public void Three() {}
}`;
test("every declared test must have executed",()=>{
  assert.deepEqual(accountSuite("Suite",source,[
    "N.Suite.One","N.Suite.Two(x: 1)","N.Suite.Two(x: 2)","N.Suite.Three"]),
    {declared:3,executed_distinct:3});
  assert.throws(()=>accountSuite("Suite",source,["N.Suite.One","N.Suite.Two(x: 1)"]),
    /did not run in Suite: Three/);
  assert.throws(()=>accountSuite("Suite",source,[]),/did not run in Suite: One, Two, Three/);
});
test("source skips, unparsed attributes and empty suites are refused",()=>{
  assert.throws(()=>declaredTestMethods(source.replace("[Fact] public async Task One","[Fact(Skip = \"x\")] public async Task One"),"Suite"),/Source-level skip/);
  assert.throws(()=>declaredTestMethods("class A { [Fact] int Weird() {} }","A"),/Unparsed test attribute/);
  assert.throws(()=>declaredTestMethods("class A { }","A"),/No declared tests/);
  assert.throws(()=>declaredTestMethods("class A { [Fact] public void X(){} [Fact] public void X(){} }","A"),/Duplicate test method/);
});
test("the real accepted regression classes parse and declare tests",()=>{
  for(const name of ["TrackingWorkerG9ProcessTests","TrackingProjectionG10Tests","M11ClosureRuntimeTests",
    "M13AdmissionControlAcceptanceTests","M15NotificationRuntimeTests","G15AuditAcceptanceTests",
    "ReferenceDataHttpTests","G13G15FeatureDefaultAcceptanceTests"]) {
    const text=fs.readFileSync(new URL("../tests/HarborOps.IntegrationTests/"+name+".cs",import.meta.url),"utf8");
    assert.ok(declaredTestMethods(text,name).length>=1,name);
  }
});
