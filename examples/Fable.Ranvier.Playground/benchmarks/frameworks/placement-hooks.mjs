// Placement benchmark: keyed rows that each run one hook once in the document, Ranvier's mounted against Solid 2's onSettled,
// and the same rows without hooks. hookCost is hooked minus plain per width and mode. Reported, not gated.
// Usage: node benchmarks/frameworks/placement-hooks.mjs [--build] [--dist=<dir>] [--label=<name>] [--out=<dir>] [--port=<n>]
import { chromium } from "@playwright/test";
import { spawn, execFileSync } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import os from "node:os";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
const argument = (name, fallback) => process.argv.find(arg => arg.startsWith(`--${name}=`))?.slice(name.length + 3) ?? fallback;
const output = argument("out") ?? fileURLToPath(new URL("../../../../docs/research/framework-performance/investigation/view-integrations/", import.meta.url));
const label = argument("label", "placement-hooks");
const outDir = argument("dist") ?? "generated-placement-dist";
const env = { ...process.env, RanvierCounters: "false", RANVIER_PERF_OUTPUT: outDir };
const port = Number(argument("port", 5189));
const scenarios = ["hooked", "plain"], modes = ["ranvier", "solid"], widths = [1, 16, 128], processes = 3, rounds = 15, warmups = 3;
const counts = { 1: 64, 16: 32, 128: 8 };

if (process.argv.includes("--build")) {
  for (const args of [
    ["dotnet", "fable", "tests/DomTests.fsproj", "--outDir", "generated-tests", "--configuration", "Release", "--noCache"],
    ["dotnet", "fable", "benchmarks/ripple/RipplePerf.fsproj", "--outDir", "generated-ripple", "--configuration", "Release", "--noCache"],
    ["node", "node_modules/vite/bin/vite.js", "build", "--config", "benchmarks/frameworks/vite.config.mjs"]
  ]) execFileSync("rtk", args, { cwd, stdio: "inherit", windowsHide: true, env });
}

const server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "preview", "--config", "benchmarks/frameworks/vite.config.mjs", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd, env, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
let serverOutput = "", browser;
server.stdout.on("data", data => serverOutput += data);
server.stderr.on("data", data => serverOutput += data);
const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
const url = `http://127.0.0.1:${port}/benchmarks/frameworks/index.html`;
const samples = [], verified = [];

try {
  let ready = false;
  for (let attempt = 0; attempt < 100 && !ready; attempt++) {
    if (server.exitCode !== null) throw Error(serverOutput);
    try { ready = (await fetch(url)).ok; } catch {}
    if (!ready) await new Promise(resolve => setTimeout(resolve, 100));
  }
  if (!ready) throw Error(serverOutput);
  for (let replicate = 0; replicate < processes; replicate++) {
    browser = await chromium.launch();
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(String(error)));
    await page.goto(url);
    await page.waitForFunction(() => window.placementHooks);
    for (const scenario of scenarios) for (const width of widths) for (const mode of modes)
      verified.push(await page.evaluate(({ mode, width, scenario }) => window.placementHooks.verify(mode, width, scenario), { mode, width, scenario }));
    for (const scenario of scenarios) for (const width of widths) {
      samples.push(...await page.evaluate(async ({ modes, width, count, rounds, warmups, replicate, scenario }) => {
        const rows = [];
        for (let round = -warmups; round < rounds; round++) for (let offset = 0; offset < modes.length; offset++) {
          const mode = modes[(round + warmups + offset + replicate) % modes.length];
          const ms = await window.placementHooks.sample(mode, width, count, scenario);
          if (!Number.isFinite(ms) || ms <= 0) throw Error(`Invalid timing ${scenario}/${mode}/${width}: ${ms}`);
          if (round >= 0) rows.push({ scenario, mode, width, replicate, round, count, ms });
        }
        return rows;
      }, { modes, width, count: counts[width], rounds, warmups, replicate, scenario }));
      console.log(`Measured ${scenario} width ${width}, process ${replicate + 1}`);
    }
    if (errors.length) throw Error(errors.join("\n"));
    await browser.close();
    browser = undefined;
  }
  const summaries = scenarios.flatMap(scenario => widths.flatMap(width => modes.map(mode => {
    const rows = samples.filter(row => row.scenario === scenario && row.width === width && row.mode === mode);
    const byProcessUs = [0, 1, 2].map(replicate => median(rows.filter(row => row.replicate === replicate).map(row => row.ms)) * 1000);
    return { scenario, width, mode, medianUs: median(rows.map(row => row.ms)) * 1000, byProcessUs };
  })));
  const hookCost = widths.flatMap(width => modes.map(mode => {
    const find = scenario => summaries.find(s => s.scenario === scenario && s.width === width && s.mode === mode);
    const hooked = find("hooked"), plain = find("plain");
    return { width, mode, costUs: hooked.medianUs - plain.medianUs, byProcessUs: hooked.byProcessUs.map((value, index) => value - plain.byProcessUs[index]) };
  }));
  const result = {
    measuredAt: new Date().toISOString(),
    baseCommit: execFileSync("git", ["rev-parse", "HEAD"], { cwd, encoding: "utf8" }).trim(),
    node: process.version, cpu: os.cpus()[0].model, platform: `${os.platform()} ${os.release()}`,
    unit: "milliseconds per list",
    notes: [
      "Each sample mounts `count` keyed lists of `width` rows on fresh hosts. A hooked row sets data-placed from Ranvier's t.Hooks.mounted or Solid 2's onSettled; the adapter waits until every hooked row is marked.",
      "hookCost is hooked minus plain per width and mode, with per-process values. Reported, not gated."
    ],
    verified, samples, summaries, hookCost
  };
  await mkdir(output, { recursive: true });
  await writeFile(`${output}/${label}.json`, JSON.stringify(result, null, 2) + "\n");
  console.table(summaries.map(({ scenario, width, mode, medianUs, byProcessUs }) => ({ scenario, width, mode, medianUs: medianUs.toFixed(1), byProcessUs: byProcessUs.map(value => value.toFixed(1)).join(" / ") })));
} finally {
  if (browser) await browser.close();
  server.kill();
}
