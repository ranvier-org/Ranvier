// Range comparison of the sugared adapters with their explicit twins.
// Usage: node benchmarks/frameworks/sugar-compare.mjs template|keyed|heap|alloc [--allow-class-tuple] <result.json>...
// alloc fails on a sugar frame and on sampled bytes above the explicit twin's; --allow-class-tuple reports the ofClass row's excess as KNOWN instead.
import { readFileSync } from "node:fs";
import { isSugarFrame } from "./sugar-frames.mjs";

const [kind, ...rest] = process.argv.slice(2);
const allowClassTuple = rest.includes("--allow-class-tuple"), files = rest.filter(arg => !arg.startsWith("--"));
const load = file => JSON.parse(readFileSync(file, "utf8"));
// The fixture module of each sugared row; the bytes it allocates per sample are printed beside the byte check.
const rowModules = { "ranvier-template-sugar": "FrameworkSugarPerf.js", "ranvier-template-members": "FrameworkMembersPerf.js", "ranvier-template-class": "FrameworkClassPerf.js", "ranvier-sugar": "KeyedRowsSugarPerf.js" };
const median = values => {
  const sorted = [...values].sort((a, b) => a - b), middle = sorted.length >> 1;
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
};
const range = values => [Math.min(...values), Math.max(...values)];
const overlaps = ([a, b], [c, d]) => a <= d && c <= b;
let failed = 0;
const report = (name, explicit, sugar) => {
  const ok = overlaps(explicit, sugar);
  if (!ok) failed++;
  console.log(`${ok ? "overlap " : "DISJOINT"} ${name} explicit=[${explicit.map(v => v.toFixed(3))}] sugar=[${sugar.map(v => v.toFixed(3))}]`);
};
for (const file of files) {
  const data = load(file);
  if (kind === "template")
    for (const mode of ["ranvier-template-sugar", "ranvier-template-members", "ranvier-template-class"])
      for (const row of data.summaries.filter(row => row.mode === mode)) {
        const twin = data.summaries.find(other => other.mode === "ranvier-template" && other.width === row.width && other.phase === row.phase);
        report(`${mode} ${row.phase}@${row.width}`, range(twin.byProcessUs), range(row.byProcessUs));
      }
  if (kind === "keyed")
    for (const row of data.summaries.filter(row => row.mode === "ranvier-sugar")) {
      const twin = data.summaries.find(other => other.mode === "ranvier" && other.operation === row.operation);
      report(row.operation, range(twin.byProcessMs), range(row.byProcessMs));
    }
  if (kind === "heap")
    for (const rows of [...new Set(data.results.map(row => row.rows))]) {
      const of = mode => data.results.filter(row => row.mode === mode && row.rows === rows).map(row => row.jsBytesPerRow);
      const explicit = of("ranvier"), sugar = of("ranvier-sugar");
      // A sugared sample above the explicit median fails; one below it is reported as LOWER.
      const limit = 0.005 * median(explicit);
      const higher = sugar.some(value => value - median(explicit) > limit);
      const lower = sugar.some(value => median(explicit) - value > limit);
      if (higher) failed++;
      console.log(`${higher ? "DIFFER  " : lower ? "LOWER   " : "same    "} heap ${rows} rows: explicit ${explicit.join(", ")} B/row, sugar ${sugar.join(", ")} B/row`);
    }
  if (kind === "alloc") {
    const sugared = /-(sugar|members|class)$/;
    const explicitOf = sample => data.allocations.filter(other => other.mode === sample.mode.replace(sugared, "") && other.operation === sample.operation && other.width === sample.width);
    if (data.allocations.some(sample => sugared.test(sample.mode) && sample.self.some(row => row.module === undefined)))
      console.log("note     frames without a recorded module are classified by name");
    for (const sample of data.allocations.filter(sample => sugared.test(sample.mode))) {
      // Every sampled function is searched; the benchmark adapter entries are excluded.
      const named = sample.self.filter(row => isSugarFrame(row) && !/^mountTemplate(Sugar|Members|Class) /.test(row.name));
      const twin = explicitOf(sample).find(other => other.process === sample.process);
      if (named.length) failed++;
      console.log(`${named.length ? "SUGAR   " : "clean   "} ${sample.mode} ${sample.operation ?? `width ${sample.width}`}${sample.process === undefined ? "" : ` process ${sample.process + 1}`}: ${sample.self.length} functions${twin ? `, ${sample.totalBytes} B against ${twin.totalBytes} B explicit` : ""}${named.length ? ", " + named.map(row => row.name).join(", ") : ""}`);
    }
    // Sampled bytes: the sugared median may exceed the explicit median by at most the explicit samples' own range.
    for (const [mode, samples] of Object.entries(Object.groupBy(data.allocations.filter(sample => sugared.test(sample.mode)), sample => `${sample.mode}\u0000${sample.operation ?? sample.width}`))) {
      const [name] = mode.split("\u0000"), explicit = explicitOf(samples[0]).map(sample => sample.totalBytes);
      const rows = samples[0].width && (samples[0].mounts ?? data.mounts) ? samples[0].width * (samples[0].mounts ?? data.mounts) : 1;
      const unit = rows === 1 ? "B" : "B/row", perRow = bytes => bytes / rows, fixed = value => value.toFixed(rows === 1 ? 0 : 1);
      const at = samples[0].operation ?? `width ${samples[0].width}`;
      if (explicit.length < 2) { console.log(`UNTESTED bytes ${name} ${at}: ${explicit.length} explicit sample, no tolerance`); continue; }
      const tolerance = perRow(Math.max(...explicit) - Math.min(...explicit));
      const excess = perRow(median(samples.map(sample => sample.totalBytes)) - median(explicit));
      const over = excess > tolerance;
      const known = over && name.endsWith("-class") && allowClassTuple;
      if (over && !known) failed++;
      const own = rowModules[name] && samples.map(sample => sample.self.filter(row => row.module?.endsWith(rowModules[name])).reduce((sum, row) => sum + row.bytes, 0));
      if (own?.some(Boolean)) console.log(`info     ${name} ${at}: ${rowModules[name]} allocates ${own.map(bytes => fixed(perRow(bytes))).join(", ")} ${unit}`);
      console.log(`${!over ? "bytes ok" : known ? "KNOWN   " : "BYTES   "} ${name} ${at}: median ${fixed(perRow(median(samples.map(sample => sample.totalBytes))))} against ${fixed(perRow(median(explicit)))} ${unit} explicit, excess ${fixed(excess)} ${unit}, tolerance ${fixed(tolerance)} ${unit} (explicit range, ${explicit.length} samples)${over && name.endsWith("-class") ? "; criterion 3 known item: the ofClass constructor tuple" + (known ? ", allowed by --allow-class-tuple" : "") : ""}`);
    }
  }
}
process.exitCode = failed ? 1 : 0;
