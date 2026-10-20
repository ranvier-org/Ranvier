// Criterion 4: bundle bytes of the explicit fixtures, of each sugared declaration style (free, ofMembers, ofClass) and of all three combined,
// raw, gzip and Brotli, each delta against explicit held to budgetGzip; whether the explicit entry holds sugar code; and frameCheck,
// the sugar-frame alternatives counted in the combined and explicit bundles (exit code 1 on a violation).
// Writes docs/research/framework-performance/investigation/view-sugar-v2/bundle.json; Vite's log goes to stderr.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { gzipSync, brotliCompressSync, constants } from "node:zlib";
import { checkAlternatives, identifiers, sugarFrame } from "./sugar-frames.mjs";

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
// Sugar is detected on unminified builds, by the identifiers the allocation comparer matches.
const entries = ["explicit", "sugared", "members", "class", "combined"];
const result = {};
for (const entry of entries) result[entry] = size(build(entry, true));
result.delta = {};
for (const entry of entries.slice(1)) result.delta[entry] = Object.fromEntries(["raw", "gzip", "brotli"].map(key => [key, result[entry][key] - result.explicit[key]]));
result.budgetGzip = 1024;
result.withinBudget = entries.slice(1).every(entry => result.delta[entry].gzip <= result.budgetGzip);
const holdsSugar = source => identifiers(source).some(id => sugarFrame.test(id));
const explicitUnminified = readFileSync(build("explicit", false), "utf8");
result.explicitHoldsSugar = holdsSugar(explicitUnminified);
const unminified = readFileSync(build("sugared", false), "utf8");
result.sugaredHoldsSugar = holdsSugar(unminified);
result.frameCheck = checkAlternatives(readFileSync(build("combined", false), "utf8"), explicitUnminified);
if (result.frameCheck.violations.length) process.exitCode = 1;
mkdirSync(new URL("./", output), { recursive: true });
writeFileSync(output, JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify(result, null, 2));
