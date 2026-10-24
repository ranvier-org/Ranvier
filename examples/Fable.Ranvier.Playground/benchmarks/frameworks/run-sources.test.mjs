// Usage: node --test benchmarks/frameworks/run-sources.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { posix } from "node:path";
import { fileURLToPath } from "node:url";
import { hashedSources, lineEndingHashes, mismatchedSources, staleSources, uncommittedOf } from "./run-sources.mjs";

const plugin = "../../experiments/Ranvier.View.Arms.FablePlugin/";

test("every file compiled into the Fable plugin, and its project, is hashed", () => {
  const project = readFileSync(new URL(`../../${plugin}Ranvier.View.Arms.FablePlugin.fsproj`, import.meta.url), "utf8");
  const compiled = [...project.matchAll(/<Compile Include="([^"]+)"/g)].map(m => posix.normalize(plugin + m[1]));
  assert.ok(compiled.length >= 4, "the plugin's Compile items");
  for (const path of [...compiled, `${plugin}Ranvier.View.Arms.FablePlugin.fsproj`]) assert.ok(hashedSources.includes(path), path);
});

test("a recorded hash matches its source under LF and CRLF line endings", () => {
  const lf = Buffer.from("a\nb\n"), crlf = Buffer.from("a\r\nb\r\n");
  assert.deepEqual(lineEndingHashes(lf).slice(1), lineEndingHashes(crlf).slice(1));
  assert.ok(lineEndingHashes(crlf).includes(lineEndingHashes(lf)[0]));
});

test("a run matches the tree when it records every hashed source with the source's hash", () => {
  const read = path => Buffer.from(`source of ${path}\n`);
  const recorded = Object.fromEntries(hashedSources.map(path => [path, lineEndingHashes(read(path))[2]]));
  assert.deepEqual(mismatchedSources(recorded, read), []);
  const { [hashedSources.at(-1)]: _, ...missing } = recorded;
  assert.deepEqual(mismatchedSources(missing, read), [hashedSources.at(-1)]);
  assert.deepEqual(mismatchedSources({ ...recorded, [hashedSources[0]]: "0" }, read), [hashedSources[0]]);
});

test("sources unchanged through the run and committed are current", () => {
  assert.deepEqual(staleSources({ before: { a: "1", b: "2" }, after: { a: "1", b: "2" }, uncommitted: [] }), []);
});

test("a source edited during the run is stale", () => {
  assert.deepEqual(staleSources({ before: { a: "1", b: "2" }, after: { a: "1", b: "3" }, uncommitted: [] }), ["b"]);
});

test("an uncommitted source is stale", () => {
  assert.deepEqual(staleSources({ before: { a: "1", b: "2" }, after: { a: "1", b: "2" }, uncommitted: ["a"] }), ["a"]);
});

test("a committed, unmodified file is not uncommitted", () => {
  const cwd = fileURLToPath(new URL("../../", import.meta.url));
  assert.deepEqual(uncommittedOf(cwd, ["benchmarks/frameworks/keyed-page.mjs"]), []);
});
