import { defineConfig, devices } from "@playwright/test";

// G18 incident workspace campaign. Runs against the owned disposable stack only: the base URL, the
// private fixture and the session directory are supplied by the verification runner.
export default defineConfig({
  testDir: "./g18",
  testMatch: /.*\.spec\.mjs/,
  globalSetup: "./g18/global-setup.mjs",
  timeout: 150_000,
  expect: { timeout: 15_000 },
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: [
    ["list"],
    ...(process.env.HARBOROPS_G18_JSON_REPORT
      ? [["json", { outputFile: process.env.HARBOROPS_G18_JSON_REPORT }]] : [])
  ],
  outputDir: process.env.HARBOROPS_G18_ARTIFACTS || "test-results/g18",
  use: {
    baseURL: process.env.HARBOROPS_BROWSER_BASE_URL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } }
  ]
});
