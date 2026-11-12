import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./m17",
  timeout: 45_000,
  expect: { timeout: 15_000 },
  forbidOnly: true,
  retries: 0,
  reporter: [["list"], ["json", { outputFile: "test-results/m17-results.json" }]],
  outputDir: "test-results/m17-artifacts",
  use: {
    baseURL: process.env.HARBOROPS_M17_DASHBOARD_BASE_URL || "http://127.0.0.1:3301",
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } }
  ]
});
