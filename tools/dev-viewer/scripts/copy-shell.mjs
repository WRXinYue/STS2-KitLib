import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const src = resolve(root, "dist", "index.html");
const dest = resolve(
  root,
  "..",
  "..",
  "src",
  "KitLib.Modules.Dev",
  "CombatStats",
  "viewer-shell.html",
);

mkdirSync(dirname(dest), { recursive: true });
copyFileSync(src, dest);
console.log(`Copied viewer shell -> ${dest}`);
