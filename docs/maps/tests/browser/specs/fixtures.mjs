import { test as base, expect } from "@playwright/test";
import { writeFile } from "node:fs/promises";

/** A page that records console errors and uncaught exceptions; each test ends by expecting none. */
export const test = base.extend({
  problems: async ({ page }, use, testInfo) => {
    const problems = [];
    const requests = new Map();
    const events = [];
    const started = Date.now();
    const record = (kind, detail) => {
      if (events.length < 200) events.push({ ms: Date.now() - started, kind, detail });
    };
    page.on("request", request => requests.set(request, Date.now()));
    page.on("requestfinished", request => {
      record("requestfinished", { url: request.url(), ms: Date.now() - requests.get(request) });
      requests.delete(request);
    });
    page.on("requestfailed", request => {
      record("requestfailed", { url: request.url(), failure: request.failure() });
      requests.delete(request);
    });
    page.on("response", response => {
      if (response.status() >= 400) record("response", { url: response.url(), status: response.status() });
    });
    page.on("worker", worker => record("worker", worker.url()));
    page.on("pageerror", error => problems.push(`pageerror: ${error.stack ?? error.message}`));
    page.on("console", message => { if (message.type() === "error") problems.push(`console: ${message.text()}`); });
    await use(problems);
    if (testInfo.status !== testInfo.expectedStatus) {
      const live = await page.locator(".nacara-live__actions").evaluateAll(actions => actions.map(action => ({
        status: action.querySelector(".nacara-live__status")?.textContent,
        disabled: action.querySelector(".nacara-live__run")?.disabled
      }))).catch(error => ({ error: String(error) }));
      const diagnostics = { problems, live, events, pending: [...requests].map(([request, time]) => ({
        url: request.url(), ms: Date.now() - time
      })) };
      console.log("DOCS_BROWSER_FAILURE", JSON.stringify(diagnostics));
      const path = testInfo.outputPath("browser-diagnostics.json");
      await writeFile(path, JSON.stringify(diagnostics, null, 2));
      await testInfo.attach("browser-diagnostics", { path, contentType: "application/json" });
    }
    expect(problems).toEqual([]);
  }
});

export { expect };

/** The map cards of a page, in page order, once every map has run its scenario. */
export async function openMaps(page, path) {
  await page.goto(path);
  const maps = page.locator(".rv-map");
  await expect(maps.first()).toBeVisible();
  await expect.poll(() => page.locator(".rv-map .rv-map-node").count()).toBeGreaterThan(0);
  for (const map of await maps.all()) {
    await expect(map).toHaveAttribute("aria-busy", "false");
    await expect(map.locator(".rv-map-node").first()).toBeAttached();
  }
  return maps;
}
