// usage: node probe-child.mjs <repo> <file> <field-edit-name>
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {pathToFileURL} from "node:url";
const [repo,file,edit]=process.argv.slice(2);
const imp=p=>import(pathToFileURL(path.join(repo,p)).href);
const fx=await imp("scripts/g17-campaign-fixture.mjs");
const m=await imp("scripts/verify-g17-manifest.mjs");
const EDITS={
  source_sha:v=>{v.source_sha="b".repeat(40);},
  zero_series:v=>{v.exported_metric_series_inspected=0;},
  one_snapshot:v=>{v.snapshots=v.snapshots.slice(0,1);},
  project:v=>{v.project="ho-verify-m17-other";},
  p95_ten:v=>{for(const k of ["baseline","authorized"])v.pooled[k].p95_increase_pct=10;},
  mem_fifteen:v=>{v.gate.mean_memory_increase_pct=15;}
};
const dir=fs.mkdtempSync(path.join(os.tmpdir(),"probe-"));
try{
  fx.writeCampaignFiles(dir);
  const v=fx.readJson(dir,file);EDITS[edit](v);fx.writeJson(dir,file,v);
  const man=fx.writeManifest(dir);
  try{m.verifyManifest(man,fx.source);console.log(JSON.stringify({threw:false}));}
  catch(e){
    const frames=String(e.stack).split("\n").filter(x=>x.includes("/scripts/")).slice(0,2)
      .map(x=>x.trim().replace(/^.*\/(scripts\/[^)]*)\)?$/,"$1"));
    console.log(JSON.stringify({threw:true,message:String(e.message).split("\n")[0],first_frames:frames}));
  }
}finally{fs.rmSync(dir,{recursive:true,force:true});}
