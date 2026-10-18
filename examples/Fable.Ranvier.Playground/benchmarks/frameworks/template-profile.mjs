// Profiles template-component mount and dispose against Solid 2 at one width, writing CPU profiles that
// keyed-profile-buckets.mjs reads. Needs the unminified bundle from `keyed-rows.mjs --build --profile`.
// Usage: node benchmarks/frameworks/template-profile.mjs <playground dir> <out dir> [width=128] [repeats=300]
import { chromium } from "playwright";
import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
const [cwd, out, width = "128", repeats = "300"] = process.argv.slice(2);
const port = 4791;
const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env: { ...process.env, RANVIER_PERF_OUTPUT: "generated-keyed-profile", RANVIER_PERF_PROFILE: "true" }, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
await new Promise(r => server.stdout.on("data", d => { if (String(d).includes(String(port))) r(); }));
const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`);
  await page.waitForFunction(() => window.frameworkPerf);
  const cdp = await page.context().newCDPSession(page);
  await cdp.send("Profiler.enable");
  await cdp.send("Profiler.setSamplingInterval", { interval: 50 });
  await mkdir(out, { recursive: true });
  for (const [mode, file] of [["ranvier-template", "ranvier"], ["solid", "solid"]]) {
    for (const phase of ["dispose", "mount"]) {
      const count = Number(width) === 128 ? 32 : 128;
      // Warmup, discarded.
      await page.evaluate(({ mode, width, phase, count }) => { for (let i = 0; i < 20; i++) { const p = window.frameworkPerf.prepare(mode, width, phase, count); p.run(); p.cleanup?.(); } }, { mode, width: Number(width), phase, count });
      await cdp.send("HeapProfiler.collectGarbage");
      await cdp.send("Profiler.start");
      await page.evaluate(({ mode, width, phase, count, repeats }) => {
        function measuredRun(p) { p.run(); }
        for (let i = 0; i < repeats; i++) { const p = window.frameworkPerf.prepare(mode, width, phase, count); measuredRun(p); p.cleanup?.(); }
      }, { mode, width: Number(width), phase, count, repeats: Number(repeats) });
      const { profile } = await cdp.send("Profiler.stop");
      await writeFile(`${out}/tmpl${phase}${width}-x${Number(repeats) * count}-${file}-${phase}.cpuprofile`, JSON.stringify(profile));
      console.log("profiled", mode, phase);
    }
  }
} finally { await browser.close(); server.kill(); }
