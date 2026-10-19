// Event dispatch benchmark: N clicks on mounted rows without a boundary, as authored buttons and as a template list.
// Usage: node benchmarks/frameworks/dispatch-clicks.mjs [--build] [--label=<name>] [--out=<dir>] [--port=<n>]
import { chromium } from "@playwright/test";
import { build, preview } from "vite";
import { execFileSync } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import os from "node:os";

const cwd = fileURLToPath(new URL("../../", import.meta.url));
const argument = (name, fallback) => process.argv.find(arg => arg.startsWith(`--${name}=`))?.slice(name.length + 3) ?? fallback;
const output = argument("out") ?? fileURLToPath(new URL("../../../../docs/research/framework-performance/investigation/view-boundaries/", import.meta.url));
const label = argument("label", "dispatch");
const port = Number(argument("port", 5189));
const outDir = `generated-dispatch-${label}`;
const modes = ["authored", "template"], count = 100, clicksPerRow = 50, processes = 3, rounds = 15, warmups = 3;
const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];

if (process.argv.includes("--build")) {
  execFileSync("rtk", ["dotnet", "fable", "tests/DomTests.fsproj", "--outDir", "generated-tests", "--configuration", "Release", "--noCache"], { cwd, stdio: "inherit", windowsHide: true, env: { ...process.env, RanvierCounters: "false" } });
}
const config = {
  root: fileURLToPath(new URL("./", import.meta.url)), configFile: false, logLevel: "warn",
  build: { outDir: `${cwd}${outDir}`, emptyOutDir: true, minify: true, rollupOptions: { input: fileURLToPath(new URL("dispatch.html", import.meta.url)) } },
  preview: { host: "127.0.0.1", port, strictPort: true }
};
await build(config);
const server = await preview(config);
const url = `http://127.0.0.1:${port}/dispatch.html`;
const samples = [];
let browser;
try {
  for (let replicate = 0; replicate < processes; replicate++) {
    browser = await chromium.launch();
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(String(error)));
    await page.goto(url);
    await page.waitForFunction(() => window.dispatchClicks);
    samples.push(...await page.evaluate(({ modes, count, clicksPerRow, rounds, warmups, replicate }) => {
      const rows = [];
      for (let round = -warmups; round < rounds; round++) for (let offset = 0; offset < modes.length; offset++) {
        const mode = modes[(round + warmups + offset + replicate) % modes.length];
        const us = window.dispatchClicks.sample(mode, count, clicksPerRow);
        if (!Number.isFinite(us) || us <= 0) throw Error(`Invalid timing ${mode}: ${us}`);
        if (round >= 0) rows.push({ mode, replicate, round, us });
      }
      return rows;
    }, { modes, count, clicksPerRow, rounds, warmups, replicate }));
    if (errors.length) throw Error(errors.join("\n"));
    await browser.close();
    browser = undefined;
  }
  const summaries = modes.map(mode => {
    const rows = samples.filter(row => row.mode === mode);
    const byProcessUs = [0, 1, 2].map(replicate => median(rows.filter(row => row.replicate === replicate).map(row => row.us)));
    return { mode, medianUs: median(rows.map(row => row.us)), byProcessUs };
  });
  const result = {
    measuredAt: new Date().toISOString(),
    baseCommit: execFileSync("git", ["rev-parse", "HEAD"], { cwd, encoding: "utf8" }).trim(),
    dirty: execFileSync("git", ["status", "--porcelain"], { cwd, encoding: "utf8" }).trim().split("\n").filter(line => line && !line.includes("generated-")),
    node: process.version, cpu: os.cpus()[0].model, platform: `${os.platform()} ${os.release()}`,
    unit: "microseconds per click", count, clicksPerRow, processes, rounds, warmups,
    notes: [
      "Each sample mounts `count` rows without a boundary and calls click() on each row `clicksPerRow` times; every click reaches one handler through the delegated listener.",
      "The page is not cross-origin isolated, so performance.now() resolves to about 0.1 ms; one sample spans count * clicksPerRow = 5000 clicks (about 7 ms), which bounds the quantisation of a per-click value to about 0.02 microseconds.",
      "Entries of `dirty` that name _lines.tmp.mjs are an untracked scratch file unrelated to the code under test."
    ],
    summaries, samples
  };
  await mkdir(output, { recursive: true });
  await writeFile(`${output}/${label}.json`, JSON.stringify(result, null, 2) + "\n");
  console.table(summaries.map(({ mode, medianUs, byProcessUs }) => ({ mode, medianUs: medianUs.toFixed(3), byProcessUs: byProcessUs.map(value => value.toFixed(3)).join(" / ") })));
} finally {
  if (browser) await browser.close();
  await server.close();
}
