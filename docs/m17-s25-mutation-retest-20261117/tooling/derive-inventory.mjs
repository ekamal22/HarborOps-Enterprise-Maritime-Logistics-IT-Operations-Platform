import fs from "node:fs"; import {inventoryDocument} from "/tmp/claude-0/mut/src2/scripts/g17-evidence-archive.mjs";
const [root, out] = process.argv.slice(2); fs.writeFileSync(out, JSON.stringify(inventoryDocument(root), null, 2) + "\n"); const d = JSON.parse(fs.readFileSync(out)); console.log(out, d.file_count, d.total_bytes);
