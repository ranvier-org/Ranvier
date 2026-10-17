// The time of each of `k` consecutive prepared steps after one full collection, as medians over `n` repetitions.
// Usage: node benchmarks/frameworks/keyed-gc-decay.mjs --dist=<vite outDir> [--op=remove1k] [--modes=ranvier,solid] [--k=30] [--n=20]
//        [--js-flags=<v8 flags, with --expose-gc>] [--port=5194]
import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
const argument = (name, fallback) => process.argv.find(arg => arg.startsWith(`--${name}=`))?.slice(name.length + 3) ?? fallback;
const dist = argument("dist", "generated-keyed-dist"), port = Number(argument("port", 5194));
const op = argument("op", "remove1k"), modes = argument("modes", "ranvier,solid").split(","), k = Number(argument("k", 30)), n = Number(argument("n", 20));
const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env: { ...process.env, RANVIER_PERF_OUTPUT: dist }, windowsHide: true, stdio: "ignore" });
const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
let browser;
try {
  for (let ready = false, attempt = 0; !ready; attempt++) {
    if (attempt > 100) throw Error("preview did not start");
    try { ready = (await fetch(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`)).ok; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
  }
  browser = await chromium.launch({ args: [`--js-flags=${argument("js-flags", "--expose-gc")}`] });
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`);
  await page.waitForFunction(() => window.keyedPerf);
  for (const mode of modes) {
    const curves = await page.evaluate(({ mode, op, k, n }) => {
      for (let i = 0; i < 20; i++) { const step = window.keyedPerf.prepared(mode, op); step.run(); step.cleanup(); }
      return Array.from({ length: n }, () => {
        gc();
        return Array.from({ length: k }, () => {
          const step = window.keyedPerf.prepared(mode, op); const start = performance.now(); step.run(); const ms = performance.now() - start; step.cleanup(); return ms;
        });
      });
    }, { mode, op, k, n });
    const byIndex = Array.from({ length: k }, (_, i) => median(curves.map(curve => curve[i])));
    console.log(`${op} ${mode.padEnd(7)} ${byIndex.map(ms => ms.toFixed(3)).join(" ")}`);
  }
} finally {
  if (browser) await browser.close();
  server.kill();
}
