import { test, expect } from "@playwright/test";

test("owned range lifecycle and attachment rollback", async ({ page }) => {
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto("/tests/range.html");
  await page.waitForFunction(() => window.rangeCases);
  const results = await page.evaluate(() => window.rangeCases.map(([name, check]) => {
    try { check(); return { name, passed: true }; }
    catch (error) { return { name, passed: false, error: String(error) }; }
  }));
  expect(results.length).toBeGreaterThan(0);
  expect(results.filter(result => !result.passed)).toEqual([]);
  expect(errors).toEqual([]);
});
