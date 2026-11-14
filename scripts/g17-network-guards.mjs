import assert from "node:assert/strict";

export function verifyServiceState(service, state) {
  assert.ok(state && typeof state === "object", `Missing process state for ${service}`);
  assert.equal(state.OOMKilled, false, `${service} was OOM-killed`);
  if (service === "migrator") {
    assert.equal(state.Status, "exited", "Migration must complete before application startup");
    assert.equal(state.ExitCode, 0, "The disposable migration failed");
  } else {
    assert.equal(state.Status, "running", `${service} is not running`);
    assert.equal(state.Running, true, `${service} has no live process`);
  }
  return {
    status: state.Status,
    exit_code: state.ExitCode,
    oom_killed: state.OOMKilled
  };
}

export function isRefusedConnection(error) {
  return error?.name === "TypeError" && error?.cause?.code === "ECONNREFUSED";
}
