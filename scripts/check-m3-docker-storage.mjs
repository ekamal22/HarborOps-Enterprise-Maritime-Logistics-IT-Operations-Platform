#!/usr/bin/env node
// Fail closed unless the Docker Desktop WSL disk configured by the active
// desktop-linux backend is the expected E: disk image.
import { existsSync, readFileSync, realpathSync, statSync } from "node:fs";
import { win32 as win } from "node:path";
import { execFileSync } from "node:child_process";

function fail(message) {
  console.error("Docker storage preflight rejected: " + message);
  process.exit(2);
}
if (process.platform !== "win32") fail("Windows Docker Desktop is required");
const candidate = process.env.HARBOROPS_DOCKER_VHDX;
if (!candidate || !/^E:[\\/]/i.test(candidate)) fail("An E: Docker data image must be supplied");
if (!existsSync(candidate) || !statSync(candidate).isFile() ||
    !candidate.toLowerCase().endsWith(".vhdx"))
  fail("The supplied Docker data image does not exist");
const appdata = process.env.APPDATA;
if (!appdata) fail("APPDATA is unavailable");
const settingsPath = win.join(appdata, "Docker", "settings-store.json");
if (!existsSync(settingsPath)) fail("Docker Desktop settings-store.json is unavailable");
let settings;
try {
  settings = JSON.parse(readFileSync(settingsPath, "utf8"));
} catch {
  fail("Cannot read Docker Desktop settings");
}
const activeLocation = settings.diskImageLocation;
if (typeof activeLocation !== "string" || !/^[A-Z]:[\\/]/i.test(activeLocation))
  fail("Docker Desktop diskImageLocation is not explicitly configured");
const physical = win.normalize(realpathSync.native(candidate)).toLowerCase();
const configured = win.normalize(activeLocation).toLowerCase().replace(/[\\/]+$/, "");
if (!physical.startsWith(configured + "\\") && physical !== configured)
  fail("The active configured image location does not contain the supplied VHDX");
const expectedName = win.basename(physical);
if (expectedName !== "docker_data.vhdx")
  fail("This preflight supports the Docker Desktop WSL 2 data image only");
function command(binary, args) {
  try {
    return execFileSync(binary, args, { encoding: "utf8", timeout: 15000 }).trim();
  } catch {
    fail("Unable to verify Docker backend");
  }
}
if (command("docker", ["context", "show"]) !== "desktop-linux")
  fail("Active Docker context is not desktop-linux");
if (command("docker", ["info", "--format", "{{.OSType}}"]) !== "linux")
  fail("Docker is not running the Linux container backend");
const repo = command("git", ["rev-parse", "--show-toplevel"]);
if (!/^E:[\\/]/i.test(win.normalize(repo)))
  fail("Repository worktree must be on E:");
console.log("Docker Desktop configured disk: " + configured);
console.log("Docker WSL data image verified on E: with desktop-linux context");
