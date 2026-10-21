// Edit-to-result loop timings for the DOM twin spec (§7.2). Run behind the bench lock.
// --loop jsdom: edits tests/DelegationChecks.fs (appends, then removes, one exported value, so the emitted JavaScript changes), then measures
//   compile:  `dotnet fable tests/DomTests.fsproj --outDir generated-tests --configuration Release` (cold: the first run
//             after `--noCache`; warm: the later runs)
//   watch:    the time from the edit to the rewrite of generated-tests/DelegationChecks.js under `dotnet fable watch`
//   node:     `node --test tests/delegation.test.mjs`
// Writes { loop, runs, steps } with per-step cold and warm samples, median, min and max, in milliseconds. The edited
// file is restored before the script exits.
import { execFileSync, spawn } from "node:child_process";
import { readFileSync, writeFileSync, statSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";

const cwd = new URL("../../", import.meta.url);
const option = (name, fallback) => { const i = process.argv.indexOf(name); return i < 0 ? fallback : process.argv[i + 1]; };
const loop = option("--loop", "jsdom"), runs = Number(option("--runs", "10")), out = option("--out");
const fixture = new URL("tests/DelegationChecks.fs", cwd);
const emitted = new URL("generated-tests/DelegationChecks.js", cwd);
const original = readFileSync(fixture, "utf8");
const edit = i => writeFileSync(fixture, original + `\nlet loopTimeEdit = ${i}\n`);
const time = action => { const start = performance.now(); action(); return performance.now() - start; };
const run = (file, args) => execFileSync(file, args, { cwd, stdio: ["ignore", "ignore", 2], windowsHide: true });
const summary = samples => {
  const sorted = [...samples].sort((a, b) => a - b);
  return { median: sorted[Math.floor(sorted.length / 2)], min: sorted[0], max: sorted[sorted.length - 1] };
};
const waitForRewrite = async (since, limitMs) => {
  const start = performance.now();
  while (performance.now() - start < limitMs) {
    if (existsSync(emitted) && statSync(emitted).mtimeMs > since) return performance.now() - start;
    await new Promise(r => setTimeout(r, 20));
  }
  throw new Error(`generated-tests/DelegationChecks.js was not rewritten within ${limitMs} ms`);
};

if (loop !== "jsdom") throw new Error(`Unknown loop ${loop}; this script measures jsdom, Task 23 adds twin-test`);
const steps = { compile: { cold: [], warm: [] }, watch: { cold: [], warm: [] }, node: { cold: [], warm: [] } };
try {
  for (let i = 0; i < runs; i++) {
    edit(i);
    const bucket = i === 0 ? "cold" : "warm";
    steps.compile[bucket].push(time(() => run("dotnet", ["fable", "tests/DomTests.fsproj", "--outDir", "generated-tests", "--configuration", "Release", ...(i === 0 ? ["--noCache"] : [])])));
    steps.node[bucket].push(time(() => run(process.execPath, ["--test", "tests/delegation.test.mjs"])));
  }
  const watcher = spawn("dotnet", ["fable", "watch", "tests/DomTests.fsproj", "--outDir", "generated-tests", "--configuration", "Release"], { cwd, stdio: ["ignore", "pipe", "ignore"], windowsHide: true });
  try {
    await new Promise((ok, fail) => { watcher.stdout.on("data", d => { if (/Watching/.test(String(d))) ok(); }); watcher.on("exit", c => fail(new Error(`fable watch exited ${c}`))); });
    for (let i = 0; i < runs; i++) {
      const since = statSync(emitted).mtimeMs;
      edit(1000 + i);
      steps.watch[i === 0 ? "cold" : "warm"].push(await waitForRewrite(since, 120000));
    }
  } finally {
    watcher.kill();
  }
} finally {
  writeFileSync(fixture, original);
}
for (const step of Object.values(steps)) Object.assign(step, summary([...step.cold, ...step.warm]));
const result = { loop, runs, steps };
if (out) { mkdirSync(dirname(resolve(out)), { recursive: true }); writeFileSync(resolve(out), JSON.stringify(result, null, 2) + "\n"); }
console.log(JSON.stringify(result, null, 2));
