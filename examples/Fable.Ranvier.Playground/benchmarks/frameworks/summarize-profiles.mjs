import { TraceMap, originalPositionFor } from "@jridgewell/trace-mapping";
import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { gunzipSync } from "node:zlib";

const output = fileURLToPath(new URL("../../../../docs/research/framework-performance/investigation/", import.meta.url));
const metadata = JSON.parse(await readFile(`${output}/profiles.json`, "utf8"));
if (!metadata.sourceMap) throw Error("Profile source map was not archived. Run diagnose.mjs --build before mapping profiles.");
const mapPath = metadata.sourceMap;
const sourceMap = new TraceMap(JSON.parse(gunzipSync(await readFile(`${output}/profile-source-map.json.gz`)).toString("utf8")));
const results = [];
for (const mode of ["ranvier", "solid", "ripple", "react"]) for (const phase of ["mount", "dispose"]) {
  const profile = JSON.parse(await readFile(`${output}/${mode}-${phase}.cpuprofile`, "utf8"));
  for (const node of profile.nodes) {
    const url = node.callFrame.url;
    if (url.includes("/assets/") && !url.endsWith(mapPath.slice(0, -4))) throw Error(`Source map does not match profile asset: ${url}`);
  }
  const nodes = new Map(profile.nodes.map(node => [node.id, node]));
  const parents = new Map();
  for (const node of profile.nodes) for (const child of node.children || []) parents.set(child, node.id);
  const self = new Map(), inclusive = new Map(), sourceTotals = new Map();
  const label = node => {
    const frame = node.callFrame;
    const original = frame.url.includes("/assets/") ? originalPositionFor(sourceMap, { line: frame.lineNumber + 1, column: frame.columnNumber }) : null;
    return { name: frame.functionName || "(anonymous)", source: original?.source || frame.url || "(runtime)", line: original?.line || frame.lineNumber + 1 };
  };
  for (let i = 0; i < profile.samples.length; i++) {
    let id = profile.samples[i], leaf = true;
    const delta = profile.timeDeltas[i], seen = new Set();
    while (id !== undefined) {
      const mapped = label(nodes.get(id));
      const key = `${mapped.source}:${mapped.line} ${mapped.name}`;
      if (leaf) { self.set(key, (self.get(key) || 0) + delta); sourceTotals.set(mapped.source, (sourceTotals.get(mapped.source) || 0) + delta); }
      if (!seen.has(key)) inclusive.set(key, (inclusive.get(key) || 0) + delta);
      seen.add(key);
      leaf = false; id = parents.get(id);
    }
  }
  const top = totals => [...totals].sort((a, b) => b[1] - a[1]).slice(0, 30).map(([name, us]) => ({ name, ms: us / 1000 }));
  results.push({ mode, phase, self: top(self), inclusive: top(inclusive), sources: top(sourceTotals) });
}
await writeFile(`${output}/mapped-profiles.json`, JSON.stringify({ map: mapPath, results }, null, 2) + "\n");
console.log(JSON.stringify(results.filter(row => row.mode === "ranvier")));
