#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const [root,phase]=process.argv.slice(2);
assert.ok(root && ["before","after"].includes(phase));
assert.match(root.replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/);
const ps="Get-PSDrive -Name C,E | Select-Object Name,Used,Free | ConvertTo-Json -Compress";
const output=execFileSync("powershell.exe",["-NoProfile","-NonInteractive","-Command",ps],
  {encoding:"utf8",timeout:15000});
const drives=JSON.parse(output);
assert.ok(Array.isArray(drives) && drives.length===2,"Drive records unavailable");
const roots=["TEMP","TMP","TMPDIR","NPM_CONFIG_CACHE","PLAYWRIGHT_BROWSERS_PATH",
  "NUGET_PACKAGES","DOTNET_CLI_HOME"];
for(const name of roots){
  const value=process.env[name];
  assert.ok(value,"Missing task temp/cache setting "+name);
  assert.match(value.replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/,
    "Controlled task temp/cache is not on E: "+name);
}
const report={phase,time:new Date().toISOString(),
  sourceSha:process.env.HARBOROPS_SOURCE_SHA,drives,
  controlledPaths:Object.fromEntries(roots.map(x=>[x,process.env[x]])),
  controlledCWritesAllowed:false,
  cAttribution:"Only paths explicitly recorded as created by this run can be treated as task-owned. Operating-system and Docker Desktop log activity is not automatically attributable.",
  cCleanup:"No controlled task writes to C:. Do not remove system or unrelated temporary files without positive ownership evidence."};
fs.mkdirSync(path.join(root,"evidence"),{recursive:true});
fs.writeFileSync(path.join(root,"evidence","g5-storage-"+phase+".json"),
  JSON.stringify(report,null,2));
console.log("G5 "+phase+" E:-rooted cache and C:/E: capacity record saved");
