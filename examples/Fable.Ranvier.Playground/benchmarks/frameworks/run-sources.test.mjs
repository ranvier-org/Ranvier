// Usage: node --test benchmarks/frameworks/run-sources.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { staleSources, uncommittedOf } from "./run-sources.mjs";

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
