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
// Docker Desktop records the data location as diskImageLocation (single-disk layout) or, when the
// WSL distributions were relocated, as CustomWslDistroDir (data image under <dir>\disk).
const activeLocation = [settings.diskImageLocation, settings.CustomWslDistroDir]
  .find((value) => typeof value === "string" && /^[A-Z]:[\\/]/i.test(value));
if (!activeLocation)
  fail("Docker Desktop diskImageLocation or CustomWslDistroDir is not explicitly configured");
const physical = win.normalize(realpathSync.native(candidate)).toLowerCase();
const configured = win.normalize(activeLocation).toLowerCase().replace(/[\\/]+$/, "");
if (!physical.startsWith(configured + "\\") && physical !== configured)
  fail("The active configured image location does not contain the supplied VHDX");
// The running docker-desktop WSL distribution must itself be registered under the configured E:
// directory, so the data image beside it is the one the engine uses. Read-only registry query.
const lxss = (() => {
  try {
    return execFileSync("reg", ["query", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Lxss", "/s"],
      { encoding: "utf8", timeout: 15000 });
  } catch {
    return fail("Unable to read WSL distribution registrations");
  }
})();
const distro = lxss.split(/\r?\n\r?\n/).find((block) => /DistributionName\s+REG_SZ\s+docker-desktop\s*$/m.test(block));
const basePath = distro && /BasePath\s+REG_SZ\s+(.+)$/m.exec(distro)?.[1].trim().replace(/^\\\\\?\\/, "");
if (!basePath || !win.normalize(basePath).toLowerCase().startsWith(configured + "\\"))
  fail("The docker-desktop WSL distribution is not registered under the configured E: directory");
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
