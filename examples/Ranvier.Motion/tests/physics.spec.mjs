// The physics page in Chromium under page.clock: a real mouse throw, the spring release and home, the followers, the
// scrubber and the motion policy.
import { test, expect } from "@playwright/test";
import { expectNoErrors, openPage, shoot } from "./support.mjs";

const centreOf = async locator => {
  const box = await locator.boundingBox();
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
};

const puck = page => page.evaluate(() => Array.from(window.__motion.puck()));

test.beforeEach(async ({ page }) => {
  await openPage(page, "/physics/");
});

test.afterEach(async ({ page }) => expectNoErrors(page));

test("a mouse throw moves the puck, the release settles it in the arena, and the followers catch up", async ({ page }) => {
  const start = await centreOf(page.locator(".puck"));
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  for (let step = 1; step <= 8; step++) {
    await page.mouse.move(start.x + step * 30, start.y + step * 8);
    await page.clock.runFor(16);
  }
  expect(await page.evaluate(() => window.__motion.dragging())).toBe(true);
  const lead = await centreOf(page.locator('[data-follower="0"]'));
  const tail = await centreOf(page.locator('[data-follower="4"]'));
  expect(lead.x - start.x).toBeGreaterThan(tail.x - start.x);
  await page.mouse.up();
  await page.clock.runFor(3000);
  const settled = await puck(page);
  await page.clock.runFor(500);
  expect(await puck(page)).toEqual(settled);
  expect(settled[0]).toBeGreaterThan(100);
  const arena = await page.locator(".arena").boundingBox();
  const box = await page.locator(".puck").boundingBox();
  expect(box.x).toBeGreaterThanOrEqual(arena.x - 1);
  expect(box.x + box.width).toBeLessThanOrEqual(arena.x + arena.width + 1);
  const caught = await centreOf(page.locator('[data-follower="4"]'));
  const at = await centreOf(page.locator(".puck"));
  expect(Math.abs(caught.x - at.x)).toBeLessThan(0.5);
  await shoot(page, "physics");
});

test("spring home brings the puck back along a spring", async ({ page }) => {
  await page.evaluate(() => window.__motion.place(160, 60));
  await page.clock.runFor(32);
  await page.click("button.home");
  await page.clock.runFor(120);
  const [midway] = await puck(page);
  await page.clock.runFor(3000);
  expect(midway).toBeGreaterThan(0);
  expect(midway).toBeLessThan(160);
  expect(await puck(page)).toEqual([0, 0]);
});

test("the slider seeks the timeline forward and back", async ({ page }) => {
  const dot = page.locator('[data-dot="1"]');
  await page.locator(".scrub").fill("750");
  expect(await page.evaluate(() => window.__motion.progress())).toBeCloseTo(0.75, 3);
  await expect(page.locator(".scrubber .readout")).toHaveText("75%");
  const forward = await dot.evaluate(element => element.style.transform);
  await page.locator(".scrub").fill("0");
  expect(await page.evaluate(() => window.__motion.progress())).toBe(0);
  expect(await dot.evaluate(element => element.style.transform)).not.toBe(forward);
});

test("the reduced policy shows the tips without their rise and sends the puck straight home", async ({ page }) => {
  await page.click("button.tips-toggle");
  await page.clock.runFor(32);
  const rising = await page.locator(".tips").evaluate(element => element.style.opacity);
  expect(Number(rising)).toBeLessThan(1);
  await page.click("button.tips-toggle");
  await page.clock.runFor(400);
  await page.check("input.reduced");
  expect(await page.evaluate(() => window.__motion.reduced())).toBe(true);
  await page.click("button.tips-toggle");
  await expect(page.locator(".tips")).toHaveCount(1);
  expect(await page.locator(".tips").evaluate(element => element.style.opacity)).toBe("");
  await page.evaluate(() => window.__motion.place(80, 20));
  await page.click("button.home");
  expect(await puck(page)).toEqual([0, 0]);
});

// A regression check of a fixed bug: Motion.drag's Position held the position at anime's settle callback, before the
// release animation ended.
test("after a throw settles, Motion.drag's Position equals the element's offset", async ({ page }) => {
  const start = await centreOf(page.locator(".puck"));
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  for (let step = 1; step <= 8; step++) {
    await page.mouse.move(start.x + step * 30, start.y + step * 8);
    await page.clock.runFor(16);
  }
  await page.mouse.up();
  await page.clock.runFor(3000);
  const [x] = await puck(page);
  const at = await centreOf(page.locator(".puck"));
  expect(Math.abs(at.x - start.x - x)).toBeLessThan(0.5);
});
