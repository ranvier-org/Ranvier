// Maps each Arms.Match prologue call (armsValues_L<line>C<column>(...)) of an emitted file back to its F# source line,
// and exits 1 unless every one lands inside a reader: on a line holding "fun" or "Func<", or at most two lines below one.
// Usage: node sourcemap.mjs <emitted .js> <F# source>   (the .js.map beside the .js; Fable --sourceMaps writes it)
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

const require = createRequire(new URL("../../../examples/Fable.Ranvier.Playground/package.json", import.meta.url));
const { TraceMap, originalPositionFor } = require("@jridgewell/trace-mapping");
const [emitted, source] = process.argv.slice(2);
const map = new TraceMap(readFileSync(emitted + ".map", "utf8"));
const lines = readFileSync(source, "utf8").split(/\r?\n/);
const prologue = /armsValues_L\d+C\d+\(/g;
const opensReader = row => row >= 0 && /\bfun\b|Func</.test(lines[row] ?? "");
const misplaced = [];
let checked = 0;

readFileSync(emitted, "utf8").split("\n").forEach((text, index) => {
  for (const found of text.matchAll(prologue)) {
    checked++;
    const original = originalPositionFor(map, { line: index + 1, column: found.index });
    const at = original.line == null ? -1 : original.line - 1;
    if (![at, at - 1, at - 2].some(opensReader)) misplaced.push(`${index + 1}:${found.index} -> ${original.line}: ${(lines[at] ?? "").trim()}`);
  }
});

console.log(`${checked} prologues checked`);
if (checked === 0 || misplaced.length > 0) {
  console.error(checked === 0 ? "no prologue found" : misplaced.join("\n"));
  process.exit(1);
}
