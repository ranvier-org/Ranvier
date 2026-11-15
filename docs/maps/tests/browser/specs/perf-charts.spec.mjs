import { test, expect } from "./fixtures.mjs";

function makeSyntheticHistory(count = 40) {
  const startMs = Date.parse("2026-10-01T12:00:00Z");
  const commits = [];
  for (let i = 0; i < count; i++) {
    const sha = `${i.toString(16).padStart(7, "0")}${"a".repeat(33)}`;
    const date = new Date(startMs + i * 86_400_000).toISOString();
    const version = i >= 32 ? "0.2.0" : "0.1.0";
    // Include an early drop at commit 2 (outside default last-30 window starting at index 10).
    let chainVal = i < 2 ? 4000 : 2500;
    if (i >= 25) chainVal = 2000; // -20.0% from index 10 (2500 -> 2000)

    const cutoffVal = i >= 20 ? 1100 : 1000; // +10.0% from index 10 (1000 -> 1100)
    const steadyVal = 500;
    const refVal = 6000;

    const counters = i >= 25
      ? [{ Runtime: "dotnet", Scenario: "chain", Engine: "Ranvier", Name: "MemoRecomputes", Value: 2 }]
      : [{ Runtime: "dotnet", Scenario: "chain", Engine: "Ranvier", Name: "MemoRecomputes", Value: 4 }];

    const accepted = i === 20
      ? [{
          Leg: "arm64-counters",
          Runtime: "dotnet",
          Scenario: "cutoff",
          Engine: "Ranvier",
          Metric: "instructions",
          Reason: "extra guard check",
        }]
      : [];

    commits.push({
      Commit: sha,
      Date: date,
      Subject: `commit ${i}`,
      Packages: [{ Name: "Ranvier", Version: version }],
      Medians: [
        { Leg: "arm64-counters", Runtime: "dotnet", Scenario: "chain", Engine: "Ranvier", Metric: "instructions", Value: chainVal, Min: chainVal - 10, Max: chainVal + 10 },
        { Leg: "arm64-counters", Runtime: "dotnet", Scenario: "chain", Engine: "R3 (1.3.1)", Metric: "instructions", Value: refVal },
        { Leg: "arm64-counters", Runtime: "dotnet", Scenario: "cutoff", Engine: "Ranvier", Metric: "instructions", Value: cutoffVal, Min: cutoffVal - 5, Max: cutoffVal + 5 },
        { Leg: "arm64-counters", Runtime: "dotnet", Scenario: "cutoff-steady", Engine: "Ranvier", Metric: "instructions", Value: steadyVal },
      ],
      Counters: counters,
      Accepted: accepted,
    });
  }
  return commits;
}

test("trends default From to last 30 commits on real site data and switch to all", async ({ page, problems }) => {
  await page.goto("benchmarks/signals/#perf-trends");
  const fromSelect = page.getByLabel("From", { exact: true });
  await expect(fromSelect).toBeVisible();
  const initialValue = await fromSelect.inputValue();
  const options = await fromSelect.locator("option").evaluateAll(els => els.map(e => e.value));
  if (options.includes("last:30")) {
    expect(initialValue).toBe("last:30");
    await expect(page.locator(".rv-perf-provenance")).toContainText("Showing 30 of");
  } else {
    expect(initialValue).toBe("all");
  }

  await fromSelect.selectOption("all");
  await expect(page.locator(".rv-perf-provenance")).toContainText("recorded commits");
  await expect(page.locator(".rv-perf-spark").first()).toBeVisible();
  expect(problems).toEqual([]);
});

test("trends filter, sort, pin, URL sync, step deltas, counters and accepted notes", async ({ page, problems }) => {
  const synthetic = makeSyntheticHistory(40);
  await page.route("**/perf-data/signals.json", async route => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ commits: synthetic }),
    });
  });

  await page.goto("benchmarks/signals/#perf-trends");
  const fromSelect = page.getByLabel("From", { exact: true });
  await expect(fromSelect).toHaveValue("last:30");
  await expect(page.locator(".rv-perf-provenance")).toContainText("Showing 30 of 40 commits");

  // Default From (index 10) ignores the pre-window commit 2 drop on chain and shows -20.0%.
  const panels = page.locator(".rv-perf-spark");
  await expect(panels).toHaveCount(3);
  await expect(panels.nth(0).locator("header")).toContainText("−20.0% since 000000a");
  await expect(panels.nth(1).locator("header")).toContainText("+10.0% since 000000a");

  // Movement summary pills filter panels accurately.
  const improvedPill = page.getByRole("button", { name: /^Improved ≤ −5% \(1\)$/ });
  const regressedPill = page.getByRole("button", { name: /^Regressed ≥ \+5% \(1\)$/ });
  const allPill = page.getByRole("button", { name: /^All \(3\)$/ });
  await expect(improvedPill).toBeVisible();
  await expect(regressedPill).toBeVisible();

  await regressedPill.click();
  await expect(panels).toHaveCount(1);
  await expect(panels.first().locator("h4")).toHaveText("cutoff");
  await allPill.click();
  await expect(panels).toHaveCount(3);

  // Order "Regressions first" places cutoff first.
  await page.getByLabel("Order", { exact: true }).selectOption("regressions");
  await expect(panels.nth(0).locator("h4")).toHaveText("cutoff");
  await expect(panels.nth(2).locator("h4")).toHaveText("chain");
  await page.getByLabel("Order", { exact: true }).selectOption("name");

  // Selecting Since 0.2.0 slices to 8 commits and persists ?from=ver:0.2.0.
  await fromSelect.selectOption("ver:0.2.0");
  await expect(page.locator(".rv-perf-provenance")).toContainText("Showing 8 of 40 commits");
  expect(new URL(page.url()).searchParams.get("from")).toBe("ver:0.2.0");

  // Navigating directly with ?from=<sha7> starts from commit 25.
  const c25Sha7 = synthetic[25].Commit.slice(0, 7);
  await page.goto(`benchmarks/signals/?from=${c25Sha7}#perf-trends`);
  await expect(page.getByLabel("From", { exact: true })).toHaveValue(synthetic[25].Commit);
  await expect(page.locator(".rv-perf-provenance")).toContainText("Showing 15 of 40 commits");

  // Reset window returns to last:30.
  await page.getByRole("button", { name: "Reset window" }).click();
  await expect(page.getByLabel("From", { exact: true })).toHaveValue("last:30");

  // Scrubbing commit 20 on cutoff panel updates all crosshairs and inspector details (Δ prev, accepted reason).
  const cutoffSvg = panels.nth(1).locator("svg");
  await cutoffSvg.evaluate(el => el.scrollIntoView({ block: "center" }));
  const box = await cutoffSvg.boundingBox();
  const pos20 = {
    x: ((96 + (10 / 29) * (628 - 96)) / 640) * box.width,
    y: box.height / 2,
  };
  await cutoffSvg.hover({ position: pos20 });

  const inspect = page.locator(".rv-perf-inspect--sticky");
  await expect(inspect).toContainText("commit 20");
  await expect(inspect).toContainText("Δ prev:");
  await expect(inspect).toContainText("+10.0%");
  await expect(inspect).toContainText("◆ accepted: extra guard check");
  // Sibling panels synchronize crosshairs while scrubbing.
  await expect(panels.nth(0).locator(".rv-perf-crosshair")).toHaveAttribute("visibility", "visible");

  // Clicking pins the commit even when pointer moves away.
  await cutoffSvg.click({ position: pos20 });
  await expect(inspect.locator(".rv-perf-badge")).toHaveText("Pinned");
  await page.mouse.move(10, 10);
  await expect(inspect.locator(".rv-perf-badge")).toHaveText("Pinned");

  // Clicking "Start from <sha7>" re-anchors From to commit 20.
  await inspect.getByRole("button", { name: `Start from ${synthetic[20].Commit.slice(0, 7)}` }).click();
  await expect(page.getByLabel("From", { exact: true })).toHaveValue(synthetic[20].Commit);
  await expect(page.locator(".rv-perf-provenance")).toContainText("Showing 20 of 40 commits");

  // Keyboard scrubbing (Home + ArrowRight x 5 -> commit 25) shows counter change annotation on chain.
  const chainSvg = panels.nth(0).locator("svg");
  await chainSvg.focus();
  await page.keyboard.press("Home");
  for (let k = 0; k < 5; k++) await page.keyboard.press("ArrowRight");
  await expect(inspect).toContainText("commit 25");
  await expect(inspect).toContainText("▲ library counters changed: MemoRecomputes 2");
  expect(problems).toEqual([]);
});

test("counters history respects From window and supports Change since From", async ({ page, problems }) => {
  const synthetic = makeSyntheticHistory(40);
  await page.route("**/perf-data/index.json", async route => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ commits: synthetic }),
    });
  });

  await page.goto("benchmarks/counters/");
  const historyRoot = page.locator("[data-perf-history]").first();
  await expect(historyRoot.getByLabel("From", { exact: true })).toHaveValue("last:30");
  await expect(historyRoot.getByLabel("Commit", { exact: true }).locator("option")).toHaveCount(30);

  await historyRoot.getByLabel("Values", { exact: true }).selectOption("change");
  await expect(historyRoot.locator(".rv-perf-trend")).toContainText("Change since From");

  await historyRoot.getByLabel("From", { exact: true }).selectOption("all");
  await expect(historyRoot.getByLabel("Commit", { exact: true }).locator("option")).toHaveCount(40);
  expect(problems).toEqual([]);
});
