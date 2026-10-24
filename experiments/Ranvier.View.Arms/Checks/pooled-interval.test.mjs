// The per-operation Chromium budgets of chromium-gate.mjs against the committed runs (plan 2026-10-24-issue-157.md,
// Decision P9). Usage: node --test experiments/Ranvier.View.Arms/Checks/pooled-interval.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { pairsOf, interval, pooled, roundUp, runs } from "./pooled-interval.mjs";
import { budgets } from "./chromium-gate.mjs";

test("a single run's interval equals the one keyed-rows.mjs recorded", () => {
  for (const run of runs.map(r => r.read()))
    for (const recorded of run.pairRatios.filter(r => r.numerator === "ranvier-arms-match" && r.denominator === "ranvier-sugar")) {
      const { median, ci95, pairs } = interval(pairsOf(run, recorded.operation));
      assert.equal(pairs, recorded.pairs);
      assert.equal(median, recorded.medianRatio);
      assert.deepEqual(ci95, recorded.ci95);
    }
});

test("create and clear keep the 1.05 budget", () => {
  assert.equal(budgets.armsCreate1k, 1.05);
  assert.equal(budgets.armsClear1k, 1.05);
});

test("switch and payload budgets are the pooled upper bounds rounded up to 0.01", () => {
  for (const operation of ["armsSwitch1k", "armsPayload1k"]) {
    const { ci95 } = pooled(operation);
    assert.equal(budgets[operation], roundUp(ci95[1]), operation);
  }
});

test("the pool holds every committed run", () => {
  assert.equal(runs.length, 5);
  for (const operation of ["armsCreate1k", "armsSwitch1k", "armsPayload1k", "armsClear1k"])
    assert.equal(pooled(operation).pairs, 225);
});

test("the pool holds the gate input and the run on the rebased branch", () => {
  const paths = runs.map(r => r.path);
  assert.ok(paths.includes("docs/research/arms-match/arms-match.json"));
  assert.ok(paths.includes("docs/research/arms-match/arms-match-5369d6a0.json"));
  assert.equal(new Set(runs.map(r => r.read().commit)).size, runs.length);
});
