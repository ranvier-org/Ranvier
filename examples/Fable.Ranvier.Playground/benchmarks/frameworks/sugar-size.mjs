// Criterion 4: bundle bytes of the explicit fixtures, of each sugared declaration style (free, ofMembers, ofClass) and of all three combined,
// raw, gzip and Brotli, each delta against explicit held to budgetGzip; whether the explicit and sugared entries hold sugar code; and frameCheck:
// every declaration in the combined bundle's sugar modules is a sugar name, the explicit bundle holds none (exit code 1 otherwise),
// and nameCollisions lists the explicit declarations that a frame without a recorded module would misattribute by name.
// Writes docs/research/framework-performance/investigation/view-sugar-v2/bundle.json; Vite's log goes to stderr.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { gzipSync, brotliCompressSync, constants } from "node:zlib";
import { bundleSugar, isSugarName } from "./sugar-frames.mjs";

const cwd = new URL("../../", import.meta.url);
const output = new URL("../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json", cwd);
const size = file => {
  const raw = readFileSync(file);
  return { raw: raw.length, gzip: gzipSync(raw, { level: 9 }).length, brotli: brotliCompressSync(raw, { params: { [constants.BROTLI_PARAM_QUALITY]: 11 } }).length };
};
const build = (entry, minify) => {
  execFileSync(process.execPath, ["node_modules/vite/bin/vite.js", "build", "--config", "benchmarks/frameworks/sugar-size.config.mjs"], { cwd, stdio: ["ignore", 2, 2], windowsHide: true, env: { ...process.env, SUGAR_SIZE_ENTRY: entry, SUGAR_SIZE_MINIFY: String(minify) } });
  return new URL(`generated-sugar-size/${entry}${minify ? "" : "-unminified"}/bundle.js`, cwd);
};
// Sugar is detected on unminified builds, by the bundle regions of the sugar modules.
const entries = ["explicit", "sugared", "members", "class", "combined"];
const result = {};
for (const entry of entries) result[entry] = size(build(entry, true));
result.delta = {};
for (const entry of entries.slice(1)) result.delta[entry] = Object.fromEntries(["raw", "gzip", "brotli"].map(key => [key, result[entry][key] - result.explicit[key]]));
result.budgetGzip = 1024;
result.withinBudget = entries.slice(1).every(entry => result.delta[entry].gzip <= result.budgetGzip);
const explicitSugar = bundleSugar(readFileSync(build("explicit", false), "utf8"));
result.explicitHoldsSugar = explicitSugar.inSugar.length > 0;
const unminified = readFileSync(build("sugared", false), "utf8");
result.sugaredHoldsSugar = bundleSugar(unminified).inSugar.length > 0;
const combinedSugar = bundleSugar(readFileSync(build("combined", false), "utf8"));
const uncovered = combinedSugar.inSugar.filter(name => !isSugarName(name));
result.frameCheck = {
  sugarDeclarations: combinedSugar.inSugar.length,
  uncovered,
  nameCollisions: explicitSugar.collisions,
  violations: [
    ...(combinedSugar.inSugar.length ? [] : ["the combined bundle holds no sugar-module declaration"]),
    ...uncovered.map(name => `${name} is declared in a sugar module but is no sugar name`),
    ...(result.explicitHoldsSugar ? [`the explicit bundle declares ${explicitSugar.inSugar.slice(0, 5).join(", ")} in a sugar module`] : [])
  ]
};
if (result.frameCheck.violations.length) process.exitCode = 1;
mkdirSync(new URL("./", output), { recursive: true });
writeFileSync(output, JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify(result, null, 2));
