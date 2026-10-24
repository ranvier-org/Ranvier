// The pooled paired interval of ranvier-arms-match over ranvier-sugar across the committed Chromium runs of #157 (plan
// 2026-10-24-issue-157.md, Decision P9). The armsSwitch1k and armsPayload1k budgets of chromium-gate.mjs are the pooled
// upper bounds rounded up to 0.01; this script rederives them.
// Usage: node experiments/Ranvier.View.Arms/Checks/pooled-interval.mjs
import { readFileSync } from "node:fs";

const at = path => ({ path, read: () => JSON.parse(readFileSync(new URL(`../../../${path}`, import.meta.url), "utf8")) });

/** The committed runs of the pool, oldest first. */
export const runs = [
  at("docs/.ai/benchmarks/arms-157-noisy-probe.json"),
  at("docs/research/arms-match/arms-match-299ab276.json"),
  at("docs/research/arms-match/arms-match-shape.json"),
  at("docs/research/arms-match/arms-match.json"),
  at("docs/research/arms-match/arms-match-5369d6a0.json")
];

const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];

/** Each ranvier-arms-match sample of the operation divided by the ranvier-sugar sample of the same process and round. */
export const pairsOf = (run, operation) =>
  run.samples.filter(s => s.operation === operation && s.mode === "ranvier-arms-match").map(s => {
    const other = run.samples.find(o => o.operation === operation && o.mode === "ranvier-sugar" && o.replicate === s.replicate && o.round === s.round);
    return s.ms / other.ms;
  });

/** The median of the ratios and its percentile bootstrap 95 % interval, with the resampling of keyed-rows.mjs. */
export const interval = ratios => {
  const sorted = [...ratios].sort((a, b) => a - b);
  let seed = 0x9e3779b9;
  const random = () => (seed = (seed * 1664525 + 1013904223) >>> 0) / 2 ** 32;
  const medians = Array.from({ length: 10000 }, () => median(Array.from(sorted, () => sorted[Math.floor(random() * sorted.length)]))).sort((a, b) => a - b);
  return { pairs: sorted.length, median: median(sorted), ci95: [medians[249], medians[9750]] };
};

/** The interval over the pairs of every run in `runs`. */
export const pooled = operation => interval(runs.flatMap(run => pairsOf(run.read(), operation)));

/** The value rounded up to 0.01, ignoring floating-point error below 1e-6. */
export const roundUp = value => Math.ceil(Math.round(value * 1e6) / 1e4) / 100;

if (import.meta.main) {
  const loaded = runs.map(run => ({ ...run, data: run.read() }));
  for (const operation of ["armsCreate1k", "armsSwitch1k", "armsPayload1k", "armsClear1k"]) {
    const each = loaded.map(run => { const r = interval(pairsOf(run.data, operation)); return `${run.data.commit.slice(0, 8)} ${r.median.toFixed(3)} (${r.ci95.map(x => x.toFixed(3)).join("-")})`; });
    const all = pooled(operation);
    console.log(`${operation}: pooled ${all.median.toFixed(3)} (95 % ${all.ci95.map(x => x.toFixed(3)).join("-")}, ${all.pairs} pairs), upper bound rounded up ${roundUp(all.ci95[1]).toFixed(2)}; ${each.join("; ")}`);
  }
}
