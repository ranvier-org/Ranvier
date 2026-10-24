// The per-operation Chromium budgets of chromium-gate.mjs against the committed runs (plan 2026-10-24-issue-157.md,
// Decision P9). Usage: node --test experiments/Ranvier.View.Arms/Checks/pooled-interval.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { pairsOf, interval, pooled, runs } from "./pooled-interval.mjs";
import { budgets, judge } from "./chromium-gate.mjs";

const operations = ["armsCreate1k", "armsSwitch1k", "armsPayload1k", "armsClear1k"];

test("a single run's interval equals the one keyed-rows.mjs recorded", () => {
  for (const run of runs.map(r => r.read()))
    for (const recorded of run.pairRatios.filter(r => r.numerator === "ranvier-arms-match" && r.denominator === "ranvier-sugar")) {
      const { median, ci95, pairs } = interval(pairsOf(run, recorded.operation));
      assert.equal(pairs, recorded.pairs);
      assert.equal(median, recorded.medianRatio);
      assert.deepEqual(ci95, recorded.ci95);
    }
});

test("the budgets are fixed", () => {
  assert.deepEqual(budgets, { armsCreate1k: 1.05, armsSwitch1k: 1.04, armsPayload1k: 1.07, armsClear1k: 1.05 });
});

test("every pooled median is within its budget", () => {
  for (const operation of operations) assert.ok(pooled(operation).median <= budgets[operation], operation);
});

test("the gate judges the pooled median and interval", () => {
  for (const operation of operations) {
    const all = pooled(operation);
    const verdict = judge(operation);
    assert.equal(verdict.median, all.median, operation);
    assert.deepEqual(verdict.ci95, all.ci95, operation);
    assert.equal(verdict.ok, all.median <= budgets[operation], operation);
  }
  assert.equal(judge("armsClear1k").interval, "straddles");
  assert.equal(judge("armsSwitch1k").interval, "below");
});

test("the pool holds every committed run", () => {
  assert.equal(runs.length, 6);
  for (const operation of operations) assert.equal(pooled(operation).pairs, 270);
});

test("the pool holds the gate input and every earlier run", () => {
  const paths = runs.map(r => r.path);
  assert.ok(paths.includes("docs/research/arms-match/arms-match.json"));
  assert.ok(paths.includes("docs/research/arms-match/arms-match-dc8b3d5b.json"));
  assert.ok(paths.includes("docs/research/arms-match/arms-match-5369d6a0.json"));
  assert.equal(new Set(runs.map(r => r.read().commit)).size, runs.length);
});
