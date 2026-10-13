import { chromium } from "@playwright/test";
import { spawn, execFileSync } from "node:child_process";
import { readFile, readdir, mkdir, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";

// Each rung adds one renderer step to the previous rung; the last rung is production.
const naive = { checks: "LADDER_NAIVE_CHECKS", range: "LADDER_NAIVE_RANGE", components: "LADDER_NAIVE_COMPONENTS", bindings: "LADDER_NAIVE_BINDINGS" };
const rungs = [
  { name: "r0-floor", adds: null, defines: [naive.checks, naive.range, naive.components, naive.bindings] },
  { name: "r1-checks", adds: "setup checks", defines: [naive.range, naive.components, naive.bindings] },
  { name: "r2-range", adds: "range ownership", defines: [naive.components, naive.bindings] },
  { name: "r3-components", adds: "component ownership", defines: [naive.bindings] },
  { name: "r4-production", adds: "binding guards", defines: [] }
];
// native-create and reactive-native bypass the gated renderer, so they are identical in every bundle and serve as page-variance controls.
const modes = ["ranvier", "ranvier-direct", "reactive-native", "native-create"], widths = [16, 128], basePort = 5200;
const cwd = fileURLToPath(new URL("../../", import.meta.url));
const out = fileURLToPath(new URL("../../../../docs/research/framework-performance/investigation/", import.meta.url));
const testsDir = rung => `generated-ladder/${rung.name}`, distDir = rung => `generated-ladder-dist/${rung.name}`;
const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
const run = (args, env = {}) => execFileSync("rtk", args, { cwd, stdio: "inherit", windowsHide: true, env: { ...process.env, RanvierCounters: "false", RANVIER_SHARED_BACKEND: "false", RANVIER_PERF_PROFILE: "false", ...env } });

if (process.argv.includes("--build")) {
  run(["dotnet", "fable", "benchmarks/ripple/RipplePerf.fsproj", "--outDir", "generated-ripple", "--configuration", "Release", "--noCache"]);
  for (const rung of rungs) {
    run(["dotnet", "fable", "tests/DomTests.fsproj", "--outDir", testsDir(rung), "--configuration", "Release", "--noCache", ...rung.defines.flatMap(symbol => ["--define", symbol])]);
    run(["node", "node_modules/vite/bin/vite.js", "build", "--config", "benchmarks/frameworks/vite.config.mjs"], { RANVIER_LADDER_TESTS: testsDir(rung), RANVIER_PERF_OUTPUT: distDir(rung) });
  }
}

const servers = [], samples = [], checks = [], bundleHashes = {};
let browser, chromiumVersion;
try {
  for (const [index, rung] of rungs.entries()) {
    const assets = (await readdir(`${cwd}/${distDir(rung)}/assets`)).filter(name => name.endsWith(".js"));
    bundleHashes[rung.name] = Object.fromEntries(await Promise.all(assets.map(async name => [name, createHash("sha256").update(await readFile(`${cwd}/${distDir(rung)}/assets/${name}`)).digest("hex")])));
    const port = basePort + index;
    const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env: { ...process.env, RANVIER_PERF_OUTPUT: distDir(rung) }, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
    servers.push(server);
    let output = "", ready = false;
    server.stdout.on("data", data => output += data);
    server.stderr.on("data", data => output += data);
    for (let attempt = 0; attempt < 100; attempt++) {
      if (server.exitCode !== null) throw Error(output);
      try { ready = (await fetch(`http://127.0.0.1:${port}/benchmarks/frameworks/index.html`)).ok; } catch {}
      if (ready) break;
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    if (!ready) throw Error(output);
  }
  for (let replicate = 0; replicate < 3; replicate++) {
    browser = await chromium.launch();
    chromiumVersion = browser.version();
    const pages = [], errors = [];
    for (const [index, rung] of rungs.entries()) {
      const page = await browser.newPage();
      page.on("pageerror", error => errors.push(String(error)));
      await page.goto(`http://127.0.0.1:${basePort + index}/benchmarks/frameworks/index.html`);
      await page.waitForFunction(() => window.frameworkPerf);
      checks.push(...await page.evaluate(({ rung, modes, replicate }) => {
        if (!crossOriginIsolated) throw Error("Timer isolation missing");
        return [1, 16, 128].flatMap(width => modes.map(mode => ({ rung, mode, replicate, ...window.frameworkPerf.verify(mode, width) })));
      }, { rung: rung.name, modes, replicate }));
      pages.push(page);
    }
    console.log(`Verified ${rungs.length} rungs, Chromium process ${replicate + 1}`);
    for (const width of widths) {
      const iterations = width === 16 ? 128 : 32;
      for (let round = -3; round < 11; round++) for (let offset = 0; offset < rungs.length; offset++) {
        const index = (round + offset + replicate + 5) % rungs.length;
        const rows = await pages[index].evaluate(({ modes, width, iterations, rotation }) =>
          modes.map((_, step) => {
            const mode = modes[(step + rotation) % modes.length];
            const ms = window.frameworkPerf.sample(mode, width, "mount", iterations);
            if (!Number.isFinite(ms) || ms <= 0) throw Error(`Invalid timing ${mode}: ${ms}`);
            return { mode, ms };
          }), { modes, width, iterations, rotation: round + 3 + replicate });
        if (round >= 0) for (const row of rows) samples.push({ rung: rungs[index].name, replicate, width, round, iterations, ...row });
      }
      console.log(`Measured width ${width}, process ${replicate + 1}`);
    }
    if (errors.length) throw Error(errors.join("\n"));
    await browser.close(); browser = undefined;
  }
  const summarize = (rung, mode, width) => {
    const rows = samples.filter(row => row.rung === rung && row.mode === mode && row.width === width);
    return { medianUs: median(rows.map(row => row.ms)) * 1000, byProcessUs: [0, 1, 2].map(replicate => median(rows.filter(row => row.replicate === replicate).map(row => row.ms)) * 1000) };
  };
  const summaries = widths.flatMap(width => modes.flatMap(mode => rungs.map(rung => ({ width, mode, rung: rung.name, ...summarize(rung.name, mode, width) }))));
  const find = (rung, mode, width) => summaries.find(row => row.rung === rung && row.mode === mode && row.width === width);
  const steps = widths.flatMap(width => ["ranvier", "ranvier-direct"].flatMap(mode => {
    const total = find(rungs.at(-1).name, mode, width).medianUs;
    return rungs.slice(1).map((rung, index) => {
      const current = find(rung.name, mode, width), previous = find(rungs[index].name, mode, width);
      const deltaUs = current.medianUs - previous.medianUs;
      return { width, mode, step: rung.adds, from: rungs[index].name, to: rung.name, deltaUs, shareOfProduction: deltaUs / total, byProcessDeltaUs: current.byProcessUs.map((value, replicate) => value - previous.byProcessUs[replicate]) };
    });
  }));
  // The floor splits along adapters that each add one layer on the R0 page.
  const floorChain = [["native-create", null], ["reactive-native", "reactive core"], ["ranvier-direct", "backend dispatch and mount"], ["ranvier", "authoring layer"]];
  const floor = widths.flatMap(width => {
    const total = find(rungs.at(-1).name, "ranvier", width).medianUs;
    return floorChain.slice(1).map(([mode, step], index) => {
      const current = find(rungs[0].name, mode, width), previous = find(rungs[0].name, floorChain[index][0], width);
      const deltaUs = current.medianUs - previous.medianUs;
      return { width, step, from: floorChain[index][0], to: mode, deltaUs, shareOfProduction: deltaUs / total, byProcessDeltaUs: current.byProcessUs.map((value, replicate) => value - previous.byProcessUs[replicate]) };
    });
  });
  const files = ["benchmarks/frameworks/ladder.mjs", "benchmarks/frameworks/page.mjs", "benchmarks/frameworks/contract.mjs", "benchmarks/frameworks/native.mjs", "benchmarks/frameworks/vite.config.mjs", "tests/FrameworkPerf.fs", "tests/FrameworkDiagnostics.fs", "../../experiments/Ranvier.View.Foundation/Mounting.fs", "../../experiments/Ranvier.View.Foundation/AuthoringPerfFixtures.fs"];
  const sourceHashes = Object.fromEntries(await Promise.all(files.map(async path => [path, createHash("sha256").update(await readFile(`${cwd}/${path}`)).digest("hex")])));
  await mkdir(out, { recursive: true });
  await writeFile(`${out}/ladder-results.json`, JSON.stringify({
    measuredAt: new Date().toISOString(), chromium: chromiumVersion,
    configuration: "counter-free Release Fable, minified production; one bundle per rung compiled with its naive-step defines; rung order rotated per round in three independent browser processes",
    warmups: 3, rounds: 11, independentProcesses: 3, unit: "milliseconds per mount",
    notes: [
      "Each rung adds one renderer step to the previous rung, so step deltas sum to production minus the floor. A step's delta depends on its position in this order.",
      "native-create and reactive-native are identical code in every bundle; their spread across rungs bounds page-to-page variance.",
      "The floor split compares adapters on the R0 page: native DOM, then bare effects and signals, then the gated backend through direct calls, then public authoring.",
      "ranvier-direct bypasses Program views and authoring helpers through the same gated backend."
    ],
    rungs, sourceHashes, bundleHashes, checks, samples, summaries, steps, floor
  }, null, 2) + "\n");
  console.log(JSON.stringify({ summaries: summaries.filter(row => row.width === 128), steps: steps.filter(row => row.width === 128), floor: floor.filter(row => row.width === 128) }));
} finally {
  if (browser) await browser.close();
  for (const server of servers) if (server.exitCode === null) { const stopped = new Promise(resolve => server.once("exit", resolve)); server.kill(); await stopped; }
}
