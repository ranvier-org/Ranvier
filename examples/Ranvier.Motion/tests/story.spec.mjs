// The story page in Chromium: served by Vite with the .NET server's render (npm run server writes it), hydrated strictly,
// and scrolled to fixed offsets under page.clock.
import { test, expect } from "@playwright/test";
import { expectNoErrors, opacityOf, openPage, shoot } from "./support.mjs";

// Scrolls so that the element's centre sits at the viewport's centre, then lets the observers and engine run.
async function centre(page, selector) {
  await page.evaluate(selector => {
    const box = document.querySelector(selector).getBoundingClientRect();
    window.scrollTo(0, window.scrollY + box.top + box.height / 2 - window.innerHeight / 2);
  }, selector);
  await page.clock.runFor(400);
}

const progress = page => page.evaluate(() => Array.from(window.__motion.progress()));

test.beforeEach(async ({ page }) => {
  await openPage(page, "/story/");
});

test.afterEach(async ({ page }) => expectNoErrors(page));

test("the page arrives server-rendered and hydrates strictly without a report", async ({ page, request }) => {
  const served = await (await request.get("/story/")).text();
  expect(served).toContain('<main id="app"><!--[r--><article class="story">');
  expect(served).toContain("Motion, owned by the view.");
  expect(await page.evaluate(() => [window.__motion.hydrated, window.__motion.errors()])).toEqual([true, []]);
  await expect(page.locator(".eyebrow")).not.toHaveAttribute("style");
  expect(await progress(page)).toEqual([0, 0, 0, 0]);
});

test("the headline splits into letters that rise in", async ({ page }) => {
  const letters = page.locator(".headline [data-char]");
  await expect(letters).toHaveCount(22);
  const last = letters.last();
  await page.clock.runFor(100);
  const early = await opacityOf(last);
  await page.clock.runFor(1600);
  expect(early).toBeLessThan(1);
  expect(await opacityOf(last)).toBe(1);
});

test("scrolling to each chapter writes its progress into the rail", async ({ page }) => {
  await centre(page, 'section[data-chapter="1"]');
  const middle = await progress(page);
  expect(middle[1]).toBeGreaterThan(0.4);
  expect(middle[1]).toBeLessThan(0.6);
  expect(middle[0]).toBeGreaterThan(middle[1]);
  expect(middle[2]).toBeLessThan(middle[1]);
  await expect(page.locator(".rail-item").nth(1)).toHaveClass(/seen/);
  await expect(page.locator(".rail-item .rail-value").nth(1)).toHaveText(`${Math.round(middle[1] * 100)}%`);
  await shoot(page, "story");
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.clock.runFor(400);
  expect((await progress(page))[1]).toBe(0);
  await expect(page.locator(".rail-item .rail-value").nth(1)).toHaveText("0%");
});

test("past half the gallery the client-created tiles appear", async ({ page }) => {
  await expect(page.locator(".extra")).toHaveCount(0);
  await page.evaluate(() => {
    const gallery = document.querySelector(".gallery").getBoundingClientRect();
    window.scrollTo(0, window.scrollY + gallery.bottom - window.innerHeight * 0.2);
  });
  await page.clock.runFor(400);
  expect((await progress(page))[3]).toBeGreaterThan(0.5);
  await expect(page.locator(".extra")).toHaveCount(4);
  await page.clock.runFor(800);
  expect(await opacityOf(page.locator(".extra").first())).toBe(1);
  expect(await page.evaluate(() => window.__motion.errors())).toEqual([]);
});
