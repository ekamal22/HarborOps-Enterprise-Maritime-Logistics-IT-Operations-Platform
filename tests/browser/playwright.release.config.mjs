import { defineConfig } from "@playwright/test";
import functionalConfig from "./playwright.config.mjs";

// Opt-in final release only. Not imported or executed by the M0 test command.
export default defineConfig({
  ...functionalConfig,
  testDir: "./release",
  outputDir: "test-results/final-accessibility",
  reporter: [["list"], ["html", { outputFolder: "test-results/final-report", open: "never" }]]
});
