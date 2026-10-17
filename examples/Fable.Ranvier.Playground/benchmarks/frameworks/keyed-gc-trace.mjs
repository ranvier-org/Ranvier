// Traces the garbage collections around each sample of one keyed operation. Each sample runs under one protocol:
// `cdp-gc` (a CDP forced GC, then `keyedPerf.sample`, as keyed-rows.mjs times) or `loop` (back-to-back prepared lists in
// one evaluate), or, with `--js-flags=--expose-gc`, `loop-minor1`, `loop-minor2` and `loop-full` (the loop with one or two
// young generation collections, or a full collection, between the setup and the timed step). Per sample: the timed step's duration and the young and old generation collections inside the setup
// and inside the timed step, from Chrome tracing on the renderer main thread.
// Usage: node benchmarks/frameworks/keyed-gc-trace.mjs --dist=<vite outDir> [--ops=remove1k] [--modes=ranvier,solid] [--n=40]
//        [--protocols=cdp-gc,loop] [--pin=<rows>: keep a list of that many rows per mode mounted throughout] [--js-flags=<v8 flags>] [--json=<file>] [--port=5193]
import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { writeFile } from "node:fs/promises";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
const argument = (name, fallback) => process.argv.find(arg => arg.startsWith(`--${name}=`))?.slice(name.length + 3) ?? fallback;
const dist = argument("dist", "generated-keyed-dist"), port = Number(argument("port", 5193)), n = Number(argument("n", 40));
const ops = argument("ops", "remove1k").split(","), modes = argument("modes", "ranvier,solid").split(",");
const jsFlags = argument("js-flags");
const protocols = argument("protocols", "cdp-gc,loop").split(",");
const pin = Number(argument("pin", 0));
const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env: { ...process.env, RANVIER_PERF_OUTPUT: dist }, windowsHide: true, stdio: "ignore" });
const median = values => values.length ? [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)] : NaN;
const categories = ["devtools.timeline", "v8", "disabled-by-default-v8.gc", "blink.user_timing"];
const young = new Set(["MinorGC", "ScavengeTask", "V8.GC_SCAVENGER", "MinorMS"]);
const old = new Set(["MajorGC", "V8.GC_MARK_COMPACTOR", "V8.GCFinalizeMC"]);

// The page runs each sample between user-timing marks: `s<i>` before setup, `r<i>` before the timed step, `e<i>` after it.
const pageSample = ({ mode, op, i, collect }) => {
  const step = window.keyedPerf.prepared(mode, op, () => performance.mark(`s${i}`));
  if (collect === "minor1") gc({ type: "minor" });
  else if (collect === "minor2") { gc({ type: "minor" }); gc({ type: "minor" }); }
  else if (collect === "full") gc();
  performance.mark(`r${i}`);
  const start = performance.now();
  step.run();
  const ms = performance.now() - start;
  performance.mark(`e${i}`);
  step.cleanup();
  return ms;
};

function collections(events, from, to, tid) {
  const inside = { young: 0, youngMs: 0, old: 0, oldMs: 0 };
  for (const event of events) {
    if (event.tid !== tid || event.ph !== "X" || event.ts < from || event.ts > to) continue;
    const kind = young.has(event.name) ? "young" : old.has(event.name) ? "old" : null;
    if (!kind) continue;
    inside[kind]++;
    inside[`${kind}Ms`] += (event.dur ?? 0) / 1000;
  }
  return inside;
}

let browser;
try {
  for (let ready = false, attempt = 0; !ready; attempt++) {
    if (attempt > 100) throw Error("preview did not start");
    try { ready = (await fetch(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`)).ok; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
  }
  browser = await chromium.launch(jsFlags ? { args: [`--js-flags=${jsFlags}`] } : {});
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`);
  await page.waitForFunction(() => window.keyedPerf);
  if (!(await page.evaluate(() => window.keyedPerf.prepared.length >= 3)))
    throw Error("keyedPerf.prepared takes no setup marker; rebuild the bundle");
  const cdp = await page.context().newCDPSession(page);
  // `--pin-op`: the pinned lists are prepared steps of each operation that ran once, instead of created lists.
  if (pin > 0) await page.evaluate(({ modes, pin }) => { window.pinned = modes.map(mode => window.keyedPerf.retain(mode, pin)); }, { modes, pin });
  if (process.argv.includes("--pin-op")) await page.evaluate(({ modes, ops }) => {
    window.pinnedSteps = modes.flatMap(mode => ops.map(op => { const step = window.keyedPerf.prepared(mode, op); step.run(); return step; }));
  }, { modes, ops });
  const rows = [];
  for (const op of ops) for (const mode of modes) for (const protocol of protocols) {
    // Warmup outside the trace.
    for (let i = 0; i < 5; i++) await page.evaluate(pageSample, { mode, op, i: -1 });
    await browser.startTracing(page, { categories });
    const times = [];
    // `full-wait<ms>`: a full collection in the page, then a timer of <ms> before the sample.
    if (protocol.startsWith("full-wait")) for (let i = 0; i < n; i++) {
      times.push(await page.evaluate(async ({ mode, op, i, pageSample, wait }) => {
        gc();
        await new Promise(resolve => setTimeout(resolve, wait));
        return eval(`(${pageSample})`)({ mode, op, i });
      }, { mode, op, i, pageSample: pageSample.toString(), wait: Number(protocol.slice(9)) }));
    }
    else if (protocol === "cdp-gc") for (let i = 0; i < n; i++) {
      await cdp.send("HeapProfiler.collectGarbage");
      times.push(await page.evaluate(pageSample, { mode, op, i }));
    }
    else times.push(...await page.evaluate(({ mode, op, n, pageSample, collect }) => {
      const sample = eval(`(${pageSample})`);
      return Array.from({ length: n }, (_, i) => sample({ mode, op, i, collect }));
    }, { mode, op, n, pageSample: pageSample.toString(), collect: protocol.startsWith("loop-") ? protocol.slice(5) : null }));
    await page.evaluate(() => performance.clearMarks());
    const events = JSON.parse((await browser.stopTracing()).toString()).traceEvents;
    const marks = new Map(events.filter(event => event.cat?.includes("blink.user_timing") && /^[sre]\d+$/.test(event.name)).map(event => [event.name, event]));
    const samples = [];
    for (let i = 0; i < n; i++) {
      const s = marks.get(`s${i}`), r = marks.get(`r${i}`), e = marks.get(`e${i}`);
      if (!s || !r || !e) continue;
      samples.push({ ms: times[i], setup: collections(events, s.ts, r.ts, r.tid), timed: collections(events, r.ts, e.ts, r.tid) });
    }
    const share = (pick) => samples.filter(pick).length / samples.length;
    const row = {
      op, mode, protocol, samples: samples.length,
      medianMs: median(samples.map(s => s.ms)),
      medianMsWithoutTimedGc: median(samples.filter(s => s.timed.young + s.timed.old === 0).map(s => s.ms)),
      medianMsWithTimedGc: median(samples.filter(s => s.timed.young + s.timed.old > 0).map(s => s.ms)),
      timedYoungShare: share(s => s.timed.young > 0), timedOldShare: share(s => s.timed.old > 0),
      setupYoungShare: share(s => s.setup.young > 0), setupOldShare: share(s => s.setup.old > 0),
      medianTimedGcMs: median(samples.filter(s => s.timed.young + s.timed.old > 0).map(s => s.timed.youngMs + s.timed.oldMs)),
      samplesDetail: samples
    };
    rows.push(row);
    console.log(`${op} ${mode.padEnd(7)} ${protocol.padEnd(6)} n=${row.samples} median ${row.medianMs.toFixed(3)} | no timed GC ${row.medianMsWithoutTimedGc?.toFixed(3)} | with timed GC ${row.medianMsWithTimedGc?.toFixed?.(3)} | timed young ${(row.timedYoungShare * 100).toFixed(0)}% old ${(row.timedOldShare * 100).toFixed(0)}% | setup young ${(row.setupYoungShare * 100).toFixed(0)}% old ${(row.setupOldShare * 100).toFixed(0)}% | timed GC ${row.medianTimedGcMs?.toFixed?.(3)} ms`);
  }
  if (argument("json")) await writeFile(argument("json"), JSON.stringify({ dist, n, jsFlags, rows }, null, 1) + "\n");
} finally {
  if (browser) await browser.close();
  server.kill();
}
