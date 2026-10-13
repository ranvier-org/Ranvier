import { chromium } from "@playwright/test";
import { spawn, execFileSync } from "node:child_process";
import { readFile, readdir, mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { TraceMap, originalPositionFor } from "@jridgewell/trace-mapping";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
// `--out=<name>` writes to investigation/<name>/ instead of the baseline directories.
const outName = process.argv.find(arg => arg.startsWith("--out="))?.slice(6);
const out = fileURLToPath(new URL(outName ? `../../../../docs/research/framework-performance/investigation/${outName}/` : process.argv.includes("--cleanup") ? "../../../../docs/research/framework-performance/investigation/cleanup-profile/" : "../../../../docs/research/framework-performance/investigation/", import.meta.url));
const assets = fileURLToPath(new URL("../../generated-framework-dist/assets/", import.meta.url));
execFileSync("rtk", ["node", "node_modules/vite/bin/vite.js", "build", "--config", "benchmarks/frameworks/vite.config.mjs"], { cwd, stdio: "inherit", windowsHide: true, env: { ...process.env, RANVIER_PERF_PROFILE: "true", RANVIER_SHARED_BACKEND: "false" } });
const mapFile = (await readdir(assets)).find(name => name.endsWith(".js.map"));
const sourceMap = new TraceMap(JSON.parse(await readFile(`${assets}/${mapFile}`, "utf8")));
const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", "5191", "--strictPort"], { cwd, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
let output = "", browser;
server.stdout.on("data", data => output += data);
server.stderr.on("data", data => output += data);
try {
  const url = "http://127.0.0.1:5191/benchmarks/frameworks/index.html";
  let ready = false;
  for (let i = 0; i < 100; i++) {
    if (server.exitCode !== null) throw Error(output);
    try { ready = (await fetch(url)).ok; } catch {}
    if (ready) break;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  if (!ready) throw Error(output);
  browser = await chromium.launch();
  const results = [];
  for (const mode of ["ranvier", "ranvier-template", "ranvier-direct", "solid", "ripple", "native-descendants"]) {
    const page = await browser.newPage();
    await page.goto(url);
    await page.waitForFunction(() => window.frameworkPerf);
    await page.evaluate(mode => {
      window.frameworkPerf.verify(mode, 128);
      for (let i = 0; i < 3; i++) window.frameworkPerf.sample(mode, 128, "mount", 32);
      window.sampleAllocations = window.frameworkPerf.prepare(mode, 128, "mount", 128);
    }, mode);
    const cdp = await page.context().newCDPSession(page);
    await cdp.send("HeapProfiler.collectGarbage");
    await cdp.send("HeapProfiler.startSampling", { samplingInterval: 4096, includeObjectsCollectedByMajorGC: true, includeObjectsCollectedByMinorGC: true });
    await page.evaluate(() => window.sampleAllocations.run());
    const { profile } = await cdp.send("HeapProfiler.stopSampling");
    const functions = new Map(), sources = new Map();
    const walk = node => {
      const frame = node.callFrame;
      const mapped = frame.url.includes("/assets/") ? originalPositionFor(sourceMap, { line: frame.lineNumber + 1, column: frame.columnNumber }) : null;
      const source = mapped?.source || frame.url || "(runtime)";
      const name = `${source}:${mapped?.line || frame.lineNumber + 1} ${frame.functionName || "(anonymous)"}`;
      functions.set(name, (functions.get(name) || 0) + node.selfSize);
      sources.set(source, (sources.get(source) || 0) + node.selfSize);
      for (const child of node.children) walk(child);
    };
    walk(profile.head);
    const top = data => [...data].sort((a, b) => b[1] - a[1]).slice(0, 30).map(([name, bytes]) => ({ name, estimatedBytesPerMount: bytes / 128 }));
    const result = { mode, width: 128, roots: 128, estimatedBytesPerMount: [...functions.values()].reduce((a, b) => a + b, 0) / 128, functions: top(functions), sources: top(sources) };
    results.push(result);
    console.log(JSON.stringify({ mode, estimatedBytesPerMount: result.estimatedBytesPerMount, sources: result.sources.slice(0, 6) }));
    await page.evaluate(() => { window.sampleAllocations.cleanup(); delete window.sampleAllocations; });
    await page.close();
  }
  await mkdir(out, { recursive: true });
  await writeFile(`${out}/allocations.json`, JSON.stringify({ measuredAt: new Date().toISOString(), chromium: browser.version(), configuration: "production unminified; CDP allocation sampling at 4096-byte interval, including collected objects", note: "Sampled allocation estimates, one browser process, exclude total native DOM memory and are not exact allocation counts", results }, null, 2) + "\n");
} finally {
  if (browser) await browser.close();
  if (server.exitCode === null) { const stopped = new Promise(resolve => server.once("exit", resolve)); server.kill(); await stopped; }
}
