// The Chromium and heap gates of Arms.Match (spec, Testing 11-12) over the committed results in docs/research/arms-match:
// - for each arm operation, the median paired ratio of ranvier-arms-match over ranvier-sugar (same process and round)
//   is at most the operation's entry in `budgets`; its 95 % bootstrap interval is printed as below, above or
//   straddling the budget;
// - the retained JavaScript heap per row after create is within 10 % of ranvier-sugar at 1,000 and 10,000 rows;
// - the retained JavaScript heap per row after one switch of every row is printed against ranvier-sugar at both counts.
// Exits 1 on a breach. Usage: node experiments/Ranvier.View.Arms/Checks/chromium-gate.mjs
import { readFileSync } from "node:fs";

/**
 * The paired median budget per operation. Create and clear hold the original 1.05 target. Switch and payload hold the
 * upper bound of the pooled paired 95 % interval, rounded up to 0.01, over the committed runs 8ee10018, 299ab276,
 * fccc740c, dc8b3d5b, 5369d6a0 and 4d00179e (plan 2026-10-24-issue-157.md, Decision P9; pooled-interval.mjs rederives
 * them): pooled switch 1.012 (0.995-1.037), payload 1.030 (0.992-1.066). The first four commits predate the rebase onto
 * integration/issues at 0967cc02; 5369d6a0 and 4d00179e, the gate input, are on the rebased branch.
 */
export const budgets = { armsCreate1k: 1.05, armsSwitch1k: 1.04, armsPayload1k: 1.07, armsClear1k: 1.05 };
const heapBudget = 1.10;

const results = new URL("../../../docs/research/arms-match/", import.meta.url);
const read = name => JSON.parse(readFileSync(new URL(name, results), "utf8"));

if (import.meta.main) {
  let failed = false;

  const timings = read("arms-match.json");
  for (const [operation, budget] of Object.entries(budgets)) {
    const pair = timings.pairRatios.find(r => r.operation === operation && r.numerator === "ranvier-arms-match" && r.denominator === "ranvier-sugar");
    if (!pair) { console.log(`MISSING ${operation}: no ranvier-arms-match:ranvier-sugar pair`); failed = true; continue; }
    const ok = pair.medianRatio <= budget;
    const [low, high] = pair.ci95;
    const interval = high <= budget ? "below" : low > budget ? "above" : "straddles";
    failed ||= !ok;
    console.log(`${ok ? "PASS" : "FAIL"} ${operation} paired median ${pair.medianRatio.toFixed(3)} (95 % ${pair.ci95.map(x => x.toFixed(3)).join("-")}, ${interval} the budget), budget ${budget}`);
  }

  const heapRows = [1000, 10000];
  const heapOf = (name, after) => read(name).results.filter(r => (r.after ?? "create") === after);
  const medianOf = (heap, rows, mode) => {
    const v = heap.filter(r => r.rows === rows && r.mode === mode).map(r => r.jsBytesPerRow).sort((a, b) => a - b);
    return v[Math.floor(v.length / 2)];
  };
  const heaps = { create: heapOf("arms-match-heap.json", "create"), switch: heapOf("arms-match-heap-switch.json", "switch") };
  for (const [after, heap] of Object.entries(heaps))
    for (const rows of heapRows) {
      const twin = medianOf(heap, rows, "ranvier-sugar"), arms = medianOf(heap, rows, "ranvier-arms-match");
      if (twin === undefined || arms === undefined) { console.log(`MISSING heap after ${after}, ${rows} rows`); failed = true; continue; }
      const ratio = arms / twin;
      const delta = `${arms.toFixed(0)} B per row against ${twin.toFixed(0)}, ${(100 * (ratio - 1)).toFixed(1)} % over ranvier-sugar`;
      if (after === "create") {
        const ok = ratio <= heapBudget;
        failed ||= !ok;
        console.log(`${ok ? "PASS" : "FAIL"} heap after create, ${rows} rows: ${delta}, budget 10 %`);
      } else console.log(`INFO heap after switch, ${rows} rows: ${delta}`);
    }

  for (const note of timings.notes.filter(note => note.startsWith("CPU load"))) console.log(`load: ${note}`);
  if (failed) process.exitCode = 1;
}
