import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { TURKISH } from "../../../web/src/i18n/messages.mjs";

const fixture = JSON.parse(fs.readFileSync(process.env.HARBOROPS_G18_FIXTURE, "utf8"));
const stateDir = process.env.HARBOROPS_G18_STATE_DIR;
const alpha = fixture.organizations.alpha;
const beta = fixture.organizations.beta;
const LOCALES = [["en", "English"], ["tr-TR", "Turkish"]];

const text = (locale, key) => locale === "en" ? key : (TURKISH[key] ?? `MISSING:${key}`);

async function context(browser, user, locale) {
  const ctx = await browser.newContext({
    storageState: path.join(stateDir, user + ".json"),
    locale: locale === "en" ? "en-US" : "tr-TR"
  });
  await ctx.addInitScript(value => { try { localStorage.setItem("harborops.language", value); } catch { /* storage unavailable */ } }, locale);
  return ctx;
}

async function api(ctx, method, url, body, headers = {}) {
  const origin = new URL(process.env.HARBOROPS_BROWSER_BASE_URL).origin;
  const csrf = await (await ctx.request.get("/api/v1/auth/csrf")).json();
  return ctx.request.fetch(url, {
    method, data: body,
    headers: { Origin: origin, "X-HarborOps-CSRF": csrf.token, ...headers }
  });
}

async function select(ctx, org) {
  const response = await api(ctx, "PUT", "/api/v1/auth/session/organization", { organizationId: org });
  expect(response.status()).toBe(200);
}

async function openWorkspace(ctx, org) {
  await select(ctx, org);
  const page = await ctx.newPage();
  await page.goto("/");
  await expect(page.locator("#current-organization")).toHaveValue(org, { timeout: 30_000 });
  return page;
}

const nonce = () => Math.random().toString(36).slice(2, 8);

async function expectState(page, locale, state, version) {
  const line = page.getByTestId("incident-state");
  await expect(line).toContainText(`${text(locale, "Incident state")}: ${text(locale, state)}`);
  if (version !== undefined) await expect(line).toContainText(`${text(locale, "Version")}: ${version}`);
}

async function version(page, locale) {
  const content = await page.getByTestId("incident-state").innerText();
  return Number(content.match(new RegExp(`${text(locale, "Version")}:\\s*(\\d+)`))[1]);
}

async function createViaUi(page, locale, title, severity = "SEV-2") {
  const form = page.getByTestId("incident-create");
  await form.getByLabel(text(locale, "Incident title")).fill(title);
  await form.getByLabel(text(locale, "Severity")).selectOption(severity);
  await form.getByRole("button", { name: text(locale, "Create incident"), exact: true }).click();
  await expect(page.getByTestId("incident-detail").getByRole("heading", { name: title })).toBeVisible();
  await expectState(page, locale, "New", 1);
}

// Clicks something that changes the incident and waits for the new version to render, so the
// next step never acts on a stale view.
async function act(page, locale, click) {
  const before = await version(page, locale);
  await click();
  await expect(page.getByTestId("incident-state")).toContainText(`${text(locale, "Version")}: ${before + 1}`);
}

async function move(page, locale, state) {
  const before = await version(page, locale);
  await page.getByTestId("move-" + state).click();
  await expectState(page, locale, state, before + 1);
}

for (const [locale, label] of LOCALES) {
  test.describe(`incident workspace in ${label}`, () => {
    test(`Support and a second responder complete, close and reopen an incident (${label})`, async ({ browser }) => {
      const t = key => text(locale, key);
      const title = "Database readiness " + nonce();
      const supportCtx = await context(browser, "support", locale);
      const page = await openWorkspace(supportCtx, alpha);
      const workspace = page.getByTestId("incident-workspace");
      await expect(workspace.getByRole("heading", { name: t("IT incidents") })).toBeVisible();
      await createViaUi(page, locale, title);
      const detail = page.getByTestId("incident-detail");

      await move(page, locale, "Triage");
      await act(page, locale, () => detail.getByRole("button", { name: t("Assign"), exact: true }).click());
      await move(page, locale, "Investigating");
      const noteText = "Pool saturation observed " + nonce();
      await detail.getByLabel(t("Investigation note")).fill(noteText);
      await act(page, locale, () => detail.getByRole("button", { name: t("Add note"), exact: true }).click());
      await expect(detail.getByText(noteText)).toBeVisible();
      await detail.getByLabel(t("Corrective action")).fill("Replace the connection pool");
      await act(page, locale, () => detail.getByRole("button", { name: t("Record action"), exact: true }).click());
      await expect(detail.getByText("Replace the connection pool")).toBeVisible();
      await move(page, locale, "Mitigated");
      await act(page, locale, () => detail.getByRole("button", { name: t("Record observation"), exact: true }).click());
      await expect(detail.getByText(t("ManualObservation"), { exact: false }).first()).toBeVisible();
      await move(page, locale, "Monitoring");
      await act(page, locale, () => detail.getByRole("button", { name: t("Start action"), exact: true }).click());
      await act(page, locale, () => detail.getByRole("button", { name: t("Verify action"), exact: true }).click());
      await expect(detail.getByText(`| ${t("Verified")}`)).toBeVisible();
      await act(page, locale, () => page.getByTestId("verify-recovery").click());
      await expect(detail.getByText(t("RecoveryProbe"), { exact: false }).first()).toBeVisible();
      await move(page, locale, "Resolved");

      // the same responder may not close a SEV-2: the server refuses and the page says so
      await page.getByTestId("move-Closed").click();
      await expect(page.getByTestId("incident-error")).toContainText(
        t("The incident changed or the operation conflicts with its current state"));
      await expectState(page, locale, "Resolved");

      // an independent responder confirms and closes in another session
      const otherCtx = await context(browser, "support2", locale);
      const other = await openWorkspace(otherCtx, alpha);
      await other.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await expectState(other, locale, "Resolved");
      await move(other, locale, "Closed");
      await expect(other.getByTestId("move-Closed")).toHaveCount(0);

      // reopening keeps the original closure in the immutable timeline
      await page.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await page.getByRole("button", { name: t("Refresh incidents"), exact: true }).click();
      await page.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await expectState(page, locale, "Closed");
      await page.getByTestId("reopen").click();
      await expectState(page, locale, "Investigating");
      const history = page.getByTestId("incident-history");
      await expect(history.getByText(t("Reopened"))).toBeVisible();
      expect(await history.locator("li").count()).toBeGreaterThanOrEqual(14);
      await expect(page.locator("html")).toHaveAttribute("lang", locale);
      await supportCtx.close();
      await otherCtx.close();
    });

    test(`a stale version shows a conflict, keeps the draft and succeeds after review (${label})`, async ({ browser }) => {
      const t = key => text(locale, key);
      const title = "Conflict check " + nonce();
      const aCtx = await context(browser, "support", locale);
      const bCtx = await context(browser, "support2", locale);
      const a = await openWorkspace(aCtx, alpha);
      await createViaUi(a, locale, title, "SEV-3");
      const b = await openWorkspace(bCtx, alpha);
      await b.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await expectState(b, locale, "New", 1);
      // the second responder changes the incident first
      await b.getByTestId("incident-detail").getByLabel(t("Investigation note")).fill("first change " + nonce());
      await b.getByTestId("incident-detail").getByRole("button", { name: t("Add note"), exact: true }).click();
      await expectState(b, locale, "New", 2);
      // the first responder still holds version 1 and a draft
      const draft = "stale draft " + nonce();
      const detail = a.getByTestId("incident-detail");
      await detail.getByLabel(t("Investigation note")).fill(draft);
      await detail.getByRole("button", { name: t("Add note"), exact: true }).click();
      await expect(a.getByTestId("incident-conflict")).toContainText(
        t("The incident changed. Review the latest state before retrying."));
      await expect(a.getByTestId("incident-error")).toContainText(
        t("The incident changed or the operation conflicts with its current state"));
      await expect(detail.getByLabel(t("Investigation note"))).toHaveValue(draft);
      await expectState(a, locale, "New", 2);
      await detail.getByRole("button", { name: t("Add note"), exact: true }).click();
      await expect(detail.getByText(draft)).toBeVisible();
      await expectState(a, locale, "New", 3);
      await expect(a.getByTestId("incident-conflict")).toHaveCount(0);
      await aCtx.close();
      await bCtx.close();
    });

    test(`roles see only their approved incident views and the Auditor grant is revocable (${label})`, async ({ browser }) => {
      const t = key => text(locale, key);
      const title = "Role view " + nonce();
      const supportCtx = await context(browser, "support", locale);
      const support = await openWorkspace(supportCtx, alpha);
      await createViaUi(support, locale, title, "SEV-3");
      await support.getByTestId("incident-detail").getByLabel(t("Investigation note")).fill("private technical note");
      await support.getByTestId("incident-detail").getByRole("button", { name: t("Add note"), exact: true }).click();
      await expect(support.getByText("private technical note")).toBeVisible();

      // Manager: summary and impact only
      const managerCtx = await context(browser, "manager", locale);
      const manager = await openWorkspace(managerCtx, alpha);
      await expect(manager.getByTestId("incident-create")).toBeVisible();
      await manager.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await expect(manager.getByText(t("Technical details are restricted"))).toBeVisible();
      await expect(manager.getByText("private technical note")).toHaveCount(0);
      await expect(manager.getByLabel(t("Investigation note"))).toHaveCount(0);
      await expect(manager.getByLabel(t("Update impact"))).toBeVisible();

      // Analyst: no incident surface at all
      const analystCtx = await context(browser, "analyst", locale);
      const analyst = await openWorkspace(analystCtx, alpha);
      await expect(analyst.getByTestId("incident-workspace")).toHaveCount(0);
      const denied = await analystCtx.request.get(`/api/v1/organizations/${alpha}/incidents`);
      expect(denied.status()).toBe(403);

      // Auditor without a grant is refused, then reads (history only) under an Administrator grant
      const auditorCtx = await context(browser, "auditor", locale);
      const auditor = await openWorkspace(auditorCtx, alpha);
      await expect(auditor.getByTestId("incident-error")).toContainText(
        t("Your account does not have access to this operation."));
      await expect(auditor.getByTestId("incident-list").locator("li")).toHaveCount(0);

      const adminCtx = await context(browser, "admin", locale);
      const admin = await openWorkspace(adminCtx, alpha);
      const grants = admin.getByTestId("incident-grants");
      await grants.getByLabel(t("Auditor UUID")).fill(fixture.users.auditor.id);
      await grants.getByRole("button", { name: t("Grant 24-hour access"), exact: true }).click();
      await expect(grants.getByText(fixture.users.auditor.id)).toBeVisible();
      await expect(grants.getByRole("button", { name: t("Revoke access"), exact: true })).toBeVisible();

      await auditor.getByRole("button", { name: t("Refresh incidents"), exact: true }).click();
      await auditor.getByTestId("incident-list").getByRole("button", { name: new RegExp(title) }).click();
      await expect(auditor.getByRole("heading", { name: t("Incident history") })).toBeVisible();
      await expect(auditor.getByText("private technical note")).toHaveCount(0);
      await expect(auditor.getByLabel(t("Investigation note"))).toHaveCount(0);
      await expect(auditor.getByTestId("incident-create")).toHaveCount(0);

      await grants.getByRole("button", { name: t("Revoke access"), exact: true }).click();
      await expect(grants.getByText(t("Inactive"))).toBeVisible();
      await auditor.getByRole("button", { name: t("Refresh incidents"), exact: true }).click();
      await expect(auditor.getByTestId("incident-error")).toContainText(
        t("Your account does not have access to this operation."));
      await expect(auditor.getByTestId("incident-list").locator("li")).toHaveCount(0);
      for (const c of [supportCtx, managerCtx, analystCtx, auditorCtx, adminCtx]) await c.close();
    });

    test(`organization switching removes every earlier tenant record and draft, prefill creates nothing until confirmed, paging and narrow layout work (${label})`, async ({ browser }) => {
      const t = key => text(locale, key);
      const tag = nonce();
      const ctx = await context(browser, "multi", locale);
      const posts = [];
      ctx.on("request", request => {
        if (request.method() === "POST" && /\/incidents$/.test(new URL(request.url()).pathname))
          posts.push(request.url());
      });
      const page = await openWorkspace(ctx, alpha);
      await createViaUi(page, locale, "Alpha only " + tag, "SEV-3");
      // prefill: viewing the server status creates no record and sends no create request
      const before = posts.length;
      await page.getByTestId("incident-create").getByRole("button", { name: t("Use current service status"), exact: true }).click();
      await expect(page.getByTestId("incident-prefill")).toContainText(t("Nothing is created until you confirm"));
      expect(posts.length).toBe(before);
      const prefillTitle = "Prefilled " + tag;
      await page.getByTestId("incident-create").getByLabel(t("Incident title")).fill(prefillTitle);
      await page.getByTestId("incident-create").getByRole("button", { name: t("Create incident"), exact: true }).click();
      await expect(page.getByTestId("incident-detail").getByRole("heading", { name: prefillTitle })).toBeVisible();
      expect(posts.length).toBe(before + 1);
      await expect(page.getByTestId("incident-detail").getByText(t("M17Snapshot"), { exact: false }).first()).toBeVisible();

      // draft typed in Alpha must not survive a switch to Beta
      await page.getByTestId("incident-create").getByLabel(t("Incident title")).fill("draft in alpha " + tag);
      await page.locator("#current-organization").selectOption(beta);
      await expect(page.locator("#current-organization")).toHaveValue(beta);
      await expect(page.getByText("Alpha only " + tag)).toHaveCount(0);
      await expect(page.getByTestId("incident-detail")).toHaveCount(0);
      await expect(page.getByTestId("incident-create").getByLabel(t("Incident title"))).toHaveValue("");
      await expect(page.getByTestId("incident-list").locator("li")).toHaveCount(0);
      expect(await page.content()).not.toContain("draft in alpha");
      // the old tenant is also refused at the API while Beta is selected
      expect((await ctx.request.get(`/api/v1/organizations/${alpha}/incidents`)).status()).toBe(403);
      await page.locator("#current-organization").selectOption(alpha);
      await expect(page.getByText("Alpha only " + tag)).toBeVisible();

      // paging: more than one page is reachable through the visible control and nothing repeats
      const supportCtx = await context(browser, "support", locale);
      await select(supportCtx, alpha);
      for (let i = 0; i < 27; i++) {
        const response = await api(supportCtx, "POST", `/api/v1/organizations/${alpha}/incidents`, {
          title: `Paged ${tag} ${String(i).padStart(2, "0")}`, serviceCode: "api", severity: "SEV-4",
          impactCode: "minor_issue", detectedAtUtc: new Date(Date.now() - 60_000).toISOString()
        }, { "X-HarborOps-Operation-Id": crypto.randomUUID() });
        expect(response.status()).toBe(201);
      }
      await page.getByRole("button", { name: t("Refresh incidents"), exact: true }).click();
      const items = page.getByTestId("incident-list").locator("li");
      await expect(items).toHaveCount(25);
      await page.getByRole("button", { name: t("Load more incidents"), exact: true }).click();
      await expect.poll(async () => items.count()).toBeGreaterThan(25);
      const titles = await items.locator("span.font-semibold").allInnerTexts();
      expect(new Set(titles).size).toBe(titles.length);
      for (let i = 0; i < 27; i++) expect(titles).toContain(`Paged ${tag} ${String(i).padStart(2, "0")}`);

      // narrow viewport: the workspace never forces horizontal page scrolling
      await page.setViewportSize({ width: 375, height: 812 });
      await expect(page.getByTestId("incident-workspace")).toBeVisible();
      // The surrounding account page already overflows by a few pixels in WebKit at this width
      // (outside M18, recorded in the verification notes). The incident workspace itself must stay
      // inside the viewport and must not add any overflow of its own.
      const measure = await page.evaluate(() => {
        const box = document.querySelector('[data-testid="incident-workspace"]');
        const rect = box.getBoundingClientRect();
        const withWorkspace = document.documentElement.scrollWidth - window.innerWidth;
        box.style.display = "none";
        const baseline = document.documentElement.scrollWidth - window.innerWidth;
        box.style.display = "";
        return { withWorkspace, baseline, right: rect.right, inner: window.innerWidth,
          own: box.scrollWidth - box.clientWidth };
      });
      expect(measure.right).toBeLessThanOrEqual(measure.inner);
      expect(measure.own).toBeLessThanOrEqual(1);
      expect(measure.withWorkspace).toBeLessThanOrEqual(Math.max(measure.baseline, 0));
      await supportCtx.close();
      await ctx.close();
    });
  });
}
