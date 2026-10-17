// Times one keyed operation under three protocols in one Chromium process: a forced GC before each sample (the
// keyed-rows.mjs protocol), no forced GC, and back-to-back prepared lists (the profiler's protocol).
// Usage: node benchmarks/frameworks/keyed-gc-check.mjs --dist=<vite outDir> [--ops=remove1k,swap1k] [--modes=ranvier,solid] [--n=60] [--port=5191]
import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
const argument = (name, fallback) => process.argv.find(arg => arg.startsWith(`--${name}=`))?.slice(name.length + 3) ?? fallback;
const dist = argument("dist", "generated-keyed-dist"), port = Number(argument("port", 5191)), n = Number(argument("n", 60));
const ops = argument("ops", "remove1k,swap1k,clear1k").split(","), modes = argument("modes", "ranvier,solid").split(",");
const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env: { ...process.env, RANVIER_PERF_OUTPUT: dist }, windowsHide: true, stdio: "ignore" });
const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
let browser;
try {
  for (let ready = false, attempt = 0; !ready; attempt++) {
    if (attempt > 100) throw Error("preview did not start");
    try { ready = (await fetch(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`)).ok; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
  }
  browser = await chromium.launch(argument("js-flags") ? { args: [`--js-flags=${argument("js-flags")}`] } : {});
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`);
  await page.waitForFunction(() => window.keyedPerf);
  const cdp = await page.context().newCDPSession(page);
  const rows = [];
  for (const op of ops) for (const mode of modes) {
    const gc = [], plain = [];
    for (let i = 0; i < n; i++) {
      await cdp.send("HeapProfiler.collectGarbage");
      gc.push(await page.evaluate(({ mode, op }) => window.keyedPerf.sample(mode, op), { mode, op }));
      plain.push(await page.evaluate(({ mode, op }) => window.keyedPerf.sample(mode, op), { mode, op }));
    }
    const loop = await page.evaluate(({ mode, op, n }) => {
      const times = [];
      for (let i = 0; i < n; i++) { const step = window.keyedPerf.prepared(mode, op); const start = performance.now(); step.run(); times.push(performance.now() - start); step.cleanup(); }
      return times;
    }, { mode, op, n });
    // The loop again with a 5 ms timer between iterations, and with the timer between setup and the operation.
    const spaced = await page.evaluate(async ({ mode, op, n }) => {
      const between = [], before = [];
      for (let i = 0; i < n; i++) {
        await new Promise(resolve => setTimeout(resolve, 5));
        let step = window.keyedPerf.prepared(mode, op); let start = performance.now(); step.run(); between.push(performance.now() - start); step.cleanup();
        step = window.keyedPerf.prepared(mode, op);
        await new Promise(resolve => setTimeout(resolve, 5));
        start = performance.now(); step.run(); before.push(performance.now() - start); step.cleanup();
      }
      return { between, before };
    }, { mode, op, n });
    // With `--js-flags=--expose-gc`: a full GC inside the page before each step, then the same with one untimed step
    // of the same operation between the GC and the timed step.
    const exposed = await page.evaluate(({ mode, op, n }) => {
      if (typeof gc !== "function") return null;
      const first = [], second = [];
      for (let i = 0; i < n; i++) {
        gc();
        let step = window.keyedPerf.prepared(mode, op); let start = performance.now(); step.run(); first.push(performance.now() - start); step.cleanup();
        gc();
        step = window.keyedPerf.prepared(mode, op); step.run(); step.cleanup();
        step = window.keyedPerf.prepared(mode, op); start = performance.now(); step.run(); second.push(performance.now() - start); step.cleanup();
      }
      return { first, second };
    }, { mode, op, n });
    if (exposed) console.log(`${op.padEnd(10)} ${mode.padEnd(8)} page-gc-then-op ${median(exposed.first).toFixed(3)}  page-gc-then-second-op ${median(exposed.second).toFixed(3)}`);
    // `sample` called back to back inside one evaluate, and one prepared step per evaluate.
    const sampleLoop = await page.evaluate(({ mode, op, n }) => Array.from({ length: n }, () => window.keyedPerf.sample(mode, op)), { mode, op, n });
    const perEvaluate = [];
    for (let i = 0; i < n; i++) perEvaluate.push(await page.evaluate(({ mode, op }) => { const step = window.keyedPerf.prepared(mode, op); const start = performance.now(); step.run(); const ms = performance.now() - start; step.cleanup(); return ms; }, { mode, op }));
    console.log(`${op.padEnd(10)} ${mode.padEnd(8)} sample-loop ${median(sampleLoop).toFixed(3)}  prepared-per-evaluate ${median(perEvaluate).toFixed(3)}`);
    rows.push({ op, mode, gcMs: median(gc), noGcMs: median(plain), loopMs: median(loop), idleBetweenMs: median(spaced.between), idleBeforeOpMs: median(spaced.before), sampleLoopMs: median(sampleLoop), preparedPerEvaluateMs: median(perEvaluate) });
    console.log(`${op.padEnd(10)} ${mode.padEnd(8)} gc ${median(gc).toFixed(3)}  no-gc ${median(plain).toFixed(3)}  loop ${median(loop).toFixed(3)}  idle-between ${median(spaced.between).toFixed(3)}  idle-before-op ${median(spaced.before).toFixed(3)}`);
  }
  if (argument("json")) await (await import("node:fs/promises")).writeFile(argument("json"), JSON.stringify({ dist, n, rows }, null, 1) + "\n");
} finally {
  if (browser) await browser.close();
  server.kill();
}
