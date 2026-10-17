// Buckets the CPU profiles written by `keyed-rows.mjs --profile --save-cpu=<dir>` by source file and call stack.
// Usage: node benchmarks/frameworks/keyed-profile-buckets.mjs <dir> <bundle.js.map> [--out=<file.json>]
// Only samples inside `measuredRun` count; stackless GC samples count with the sample before them.
import { TraceMap, originalPositionFor } from "@jridgewell/trace-mapping";
import { readFile, readdir, writeFile } from "node:fs/promises";

const [dir, mapPath] = process.argv.slice(2);
const out = process.argv.find(arg => arg.startsWith("--out="))?.slice(6);
const map = new TraceMap(JSON.parse(await readFile(mapPath, "utf8")));

const sourceOf = frame => {
  if (!frame.url) return "(native)";
  const position = originalPositionFor(map, { line: frame.lineNumber + 1, column: frame.columnNumber });
  return (position.source || frame.url).replace(/^.*?\/(generated-tests|node_modules)\//, "$1/");
};
const project = source => /generated-tests\/(src|experiments|tests|KeyedRowsPerf)/.test(source) || /node_modules\/(solid-js|@solidjs)/.test(source) || source.includes("keyed-solid");
const bucketOfSource = source =>
  /generated-tests\/src\/Ranvier\/Projections/.test(source) ? "core projection pass"
  : /generated-tests\/src\//.test(source) ? "core graph"
  : /generated-tests\/(experiments|tests|KeyedRowsPerf)/.test(source) ? "view list region"
  : /solid/.test(source) ? "solid" : "other";
// Frames at or below which the work is row teardown: owner and effect disposal and the cleanups they run.
const teardown = name => /^(Owner__Dispose|Owner__DisposeScope|Owner__DisposeLate|Graph__Retire|Effect__Dispose|Memo\$1__Dispose|Signal\$1__Dispose|RegionSite__ReleaseRow|Row_Remove|MountState__OwnedCleanup|RowEntry\$2__Dispose|Computation__Dispose|cleanNode|disposeOwner|dispose)/.test(name);

const results = [];
for (const file of (await readdir(dir)).filter(name => name.endsWith(".cpuprofile")).sort()) {
  const cpu = JSON.parse(await readFile(`${dir}/${file}`, "utf8"));
  const [, label, mode, operation] = file.match(/^(.*)-(ranvier|solid)-(\w+)\.cpuprofile$/);
  const repeats = Number(label.match(/x(\d+)$/)?.[1] ?? NaN);
  const nodes = new Map(cpu.nodes.map(node => [node.id, node]));
  const parents = new Map();
  for (const node of cpu.nodes) for (const child of node.children || []) parents.set(child, node.id);
  const sources = new Map(cpu.nodes.map(node => [node.id, sourceOf(node.callFrame)]));
  const stackOf = id => { const stack = []; for (let at = id; at !== undefined; at = parents.get(at)) { const frame = nodes.get(at).callFrame; if (frame.functionName === "measuredRun") return stack; stack.push(at); } return null; };
  const buckets = {}, self = new Map(), inclusive = new Map();
  let total = 0, previous = null;
  for (let i = 0; i < cpu.samples.length; i++) {
    const id = cpu.samples[i], leaf = nodes.get(id).callFrame;
    let stack = stackOf(id), gc = false;
    if (!stack && leaf.functionName === "(garbage collector)" && previous) { stack = previous; gc = true; }
    if (stack && stack.length === 0) stack = null;
    if (!stack) { if (!gc) previous = null; continue; }
    if (!gc) previous = stack;
    const delta = cpu.timeDeltas[i] || 0;
    total += delta;
    const leafId = stack[0];
    const leafFrame = nodes.get(leafId).callFrame;
    const nearest = stack.find(at => project(sources.get(at)));
    let bucket;
    if (gc) bucket = "GC";
    else if (!leafFrame.url) bucket = "DOM operations";
    else if (mode === "ranvier" && stack.some(at => teardown(nodes.get(at).callFrame.functionName))) bucket = "row teardown";
    else bucket = nearest === undefined ? "other" : bucketOfSource(sources.get(nearest));
    if (mode === "solid" && bucket !== "GC" && bucket !== "DOM operations") bucket = "solid";
    buckets[bucket] = (buckets[bucket] || 0) + delta;
    const name = at => `${nodes.get(at).callFrame.functionName || "(anonymous)"} ${sources.get(at).replace(/^generated-tests\//, "")}`;
    const key = gc ? `(garbage collector) under ${name(leafId)}` : `${name(leafId)}`;
    const entry = self.get(key) || { ms: 0, bucket }; entry.ms += delta; self.set(key, entry);
    const seen = new Set();
    for (const at of stack) { const n = name(at); if (!seen.has(n)) { seen.add(n); inclusive.set(n, (inclusive.get(n) || 0) + delta); } }
  }
  const perOp = us => +(us / 1000 / repeats).toFixed(4);
  results.push({
    mode, operation, repeats, sampledMsPerOperation: perOp(total),
    buckets: Object.fromEntries(Object.entries(buckets).sort((a, b) => b[1] - a[1]).map(([name, us]) => [name, { msPerOperation: perOp(us), percent: +(100 * us / total).toFixed(1) }])),
    self: [...self].sort((a, b) => b[1].ms - a[1].ms).slice(0, 40).map(([name, { ms, bucket }]) => ({ name, bucket, msPerOperation: perOp(ms), percent: +(100 * ms / total).toFixed(1) })),
    inclusive: [...inclusive].sort((a, b) => b[1] - a[1]).slice(0, 50).map(([name, us]) => ({ name, msPerOperation: perOp(us), percent: +(100 * us / total).toFixed(1) }))
  });
}
if (out) await writeFile(out, JSON.stringify(results, null, 1) + "\n");
for (const r of results) {
  console.log(`\n== ${r.mode}/${r.operation} x${r.repeats}: ${r.sampledMsPerOperation} ms/op`);
  for (const [name, b] of Object.entries(r.buckets)) console.log(`  ${name.padEnd(22)} ${b.msPerOperation.toFixed(4)}  ${b.percent}%`);
  console.log("  -- self");
  for (const s of r.self.slice(0, 25)) console.log(`  ${s.msPerOperation.toFixed(4)} ${String(s.percent).padStart(5)}%  [${s.bucket}] ${s.name}`);
  if (process.argv.includes("--inclusive")) { console.log("  -- inclusive"); for (const s of r.inclusive.slice(0, 40)) console.log(`  ${s.msPerOperation.toFixed(4)} ${String(s.percent).padStart(5)}%  ${s.name}`); }
}
