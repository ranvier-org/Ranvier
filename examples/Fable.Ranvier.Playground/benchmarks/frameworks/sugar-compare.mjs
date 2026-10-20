// Range comparison of the sugared adapters with their explicit twins.
// Usage: node benchmarks/frameworks/sugar-compare.mjs template|keyed|heap|alloc <result.json>...
import { readFileSync } from "node:fs";

const [kind, ...files] = process.argv.slice(2);
const load = file => JSON.parse(readFileSync(file, "utf8"));
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
    for (const row of data.summaries.filter(row => row.mode === "ranvier-template-sugar")) {
      const twin = data.summaries.find(other => other.mode === "ranvier-template" && other.width === row.width && other.phase === row.phase);
      report(`${row.phase}@${row.width}`, range(twin.byProcessUs), range(row.byProcessUs));
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
      const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
      const ok = Math.abs(median(sugar) - median(explicit)) <= 0.005 * median(explicit);
      if (!ok) failed++;
      console.log(`${ok ? "same    " : "DIFFER  "} heap ${rows} rows: explicit ${explicit.join(", ")} B/row, sugar ${sugar.join(", ")} B/row`);
    }
  if (kind === "alloc")
    for (const sample of data.allocations.filter(sample => sample.mode === "ranvier-sugar")) {
      const named = sample.self.filter(row => /ElementBuilder|Builders_|Sugar/.test(row.name));
      if (named.length) failed++;
      console.log(`${named.length ? "SUGAR   " : "clean   "} ${sample.operation}: ${named.map(row => row.name).join(", ")}`);
    }
}
process.exitCode = failed ? 1 : 0;
