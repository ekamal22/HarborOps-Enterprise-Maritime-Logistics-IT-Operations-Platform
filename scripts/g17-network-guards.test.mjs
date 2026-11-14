import test from "node:test";
import assert from "node:assert/strict";
import {verifyServiceState, isRefusedConnection} from "./g17-network-guards.mjs";

const running = {Status:"running",Running:true,ExitCode:0,OOMKilled:false};
const migrated = {Status:"exited",Running:false,ExitCode:0,OOMKilled:false};

test("running collector and application services require live Docker state",()=>{
  for (const service of ["otelcol","tempo","prometheus","grafana","api","web","postgres"])
    assert.equal(verifyServiceState(service,running).status,"running");
  assert.equal(verifyServiceState("migrator",migrated).exit_code,0);
});
test("collector crash, stopped state, OOM and failed migration all reject the gate",()=>{
  for (const state of [
    {...running,Status:"exited",Running:false,ExitCode:1},
    {...running,Status:"created",Running:false},
    {...running,Status:"restarting",Running:false},
    {...running,OOMKilled:true},
    {...running,Running:false}
  ]) assert.throws(()=>verifyServiceState("otelcol",state));
  assert.throws(()=>verifyServiceState("migrator",{...migrated,ExitCode:1}));
  assert.throws(()=>verifyServiceState("migrator",running));
});
test("host isolation accepts only a refused TCP connection",()=>{
  assert.equal(isRefusedConnection({name:"TypeError",cause:{code:"ECONNREFUSED"}}),true);
  for (const error of [
    {name:"TimeoutError"},
    {name:"TypeError",cause:{code:"ECONNRESET"}},
    {name:"TypeError",cause:{code:"ETIMEDOUT"}},
    {name:"Error",message:"HTTP 503"},
    null
  ]) assert.equal(isRefusedConnection(error),false);
});
