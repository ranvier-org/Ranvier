import { test, expect } from "@playwright/test";

for (const [label, key] of [["hydration checks in Chromium", "hydrationCases"], ["hydration browser checks in Chromium", "hydrationBrowserCases"]]) {
  test(label, async ({ page }) => {
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto("/tests/hydration.html");
    await page.waitForFunction(key => window[key], key);
    const results = await page.evaluate(key => window[key].map(([name, check]) => {
      try { check(); return { name, passed: true }; }
      catch (error) { return { name, passed: false, error: String(error) }; }
    }), key);
    expect(results.length).toBeGreaterThan(0);
    expect(results.filter(result => !result.passed)).toEqual([]);
    expect(errors).toEqual([]);
  });
}
