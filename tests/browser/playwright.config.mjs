import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./specs",
  timeout: 30_000,
  expect: { timeout: 10_000 },
  forbidOnly: true,
  retries: 0,
  reporter: [
    ["list"],
    ...(process.env.HARBOROPS_G3_JSON_REPORT
      ? [["json", { outputFile: process.env.HARBOROPS_G3_JSON_REPORT }]] : []),
    ["html", { outputFolder: "test-results/report", open: "never" }]
  ],
  outputDir: "test-results/artifacts",
  use: {
    baseURL: process.env.HARBOROPS_BROWSER_BASE_URL || "http://localhost:5173",
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } }
  ]
});
