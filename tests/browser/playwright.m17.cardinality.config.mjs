import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./m17",
  testMatch: "cardinality.spec.mjs",
  timeout: 240_000,
  expect: { timeout: 30_000 },
  forbidOnly: true,
  retries: 0,
  reporter: [["list"], ["json", {
    outputFile: process.env.HARBOROPS_G17_CARDINALITY_JSON ||
      "test-results/m17-cardinality-results.json"
  }]],
  outputDir: "test-results/m17-cardinality-artifacts",
  use: {
    baseURL: process.env.HARBOROPS_M17_DASHBOARD_BASE_URL || "http://127.0.0.1:3301",
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } }
  ]
});
