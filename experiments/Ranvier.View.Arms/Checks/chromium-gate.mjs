// The Chromium and heap gates of Arms.Match (spec, Testing 11-12) over the committed results in docs/research/arms-match:
// - for each arm operation, the median paired ratio of ranvier-arms-match over ranvier-sugar (same process and round)
//   is at most 1.12; its 95 % bootstrap interval is printed;
// - the retained JavaScript heap per row after create is within 10 % of ranvier-sugar at 1,000 and 10,000 rows;
// - the retained JavaScript heap per row after one switch of every row is printed against ranvier-sugar at both counts.
// Exits 1 on a breach. Usage: node experiments/Ranvier.View.Arms/Checks/chromium-gate.mjs
import { readFileSync } from "node:fs";

const results = new URL("../../../docs/research/arms-match/", import.meta.url);
const read = name => JSON.parse(readFileSync(new URL(name, results), "utf8"));
const operations = ["armsCreate1k", "armsSwitch1k", "armsPayload1k", "armsClear1k"];
const budget = 1.12, heapBudget = 1.10;
let failed = false;

const timings = read("arms-match.json");
for (const operation of operations) {
  const pair = timings.pairRatios.find(r => r.operation === operation && r.numerator === "ranvier-arms-match" && r.denominator === "ranvier-sugar");
  if (!pair) { console.log(`MISSING ${operation}: no ranvier-arms-match:ranvier-sugar pair`); failed = true; continue; }
  const ok = pair.medianRatio <= budget;
  failed ||= !ok;
  console.log(`${ok ? "PASS" : "FAIL"} ${operation} paired median ${pair.medianRatio.toFixed(3)} (95 % ${pair.ci95.map(x => x.toFixed(3)).join("-")}), budget ${budget}`);
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

console.log(`load: ${timings.notes.slice(-1)[0]}`);
if (failed) process.exitCode = 1;
