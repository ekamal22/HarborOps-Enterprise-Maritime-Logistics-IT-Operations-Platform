import fs from "node:fs";
import path from "node:path";
import { request } from "@playwright/test";

// One sign-in per fixture user per run, kept as a cookie jar on the verification drive. The
// sign-in form itself is exercised by the specs; this only avoids the per-address login limiter.
export default async function globalSetup() {
  const fixture = JSON.parse(fs.readFileSync(process.env.HARBOROPS_G18_FIXTURE, "utf8"));
  const dir = process.env.HARBOROPS_G18_STATE_DIR;
  fs.mkdirSync(dir, { recursive: true });
  const base = process.env.HARBOROPS_BROWSER_BASE_URL;
  for (const [name, user] of Object.entries(fixture.users)) {
    const api = await request.newContext({ baseURL: base, extraHTTPHeaders: { Origin: base } });
    const csrf = await (await api.get("/api/v1/auth/csrf")).json();
    const response = await api.post("/api/v1/auth/login", {
      data: { email: user.email, password: fixture.password },
      headers: { "X-HarborOps-CSRF": csrf.token }
    });
    if (response.status() !== 200) throw new Error("fixture sign-in failed for " + name + ": " + response.status());
    await api.storageState({ path: path.join(dir, name + ".json") });
    await api.dispose();
  }
}
