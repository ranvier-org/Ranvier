import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases } from "../generated-tests-counters/SugarChecks.js";
import { create } from "../generated-tests-counters/JsdomHarness.js";
import { makeClock, interceptIn } from "./conformance-host.mjs";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget", "InputEvent"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
globalThis.ranvierExpectCounters = true;
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; delete globalThis.ranvierExpectCounters; });

// The sugar conformance cases with the core counters compiled in, each with the harness clock over the one document.
for (const [name, run] of cases)
  test(name, () => {
    const clock = makeClock([globalThis, dom.window]);
    const host = { post: clock.post, runMicrotasks: clock.runMicrotasks, advanceBy: clock.advanceBy, record: () => {}, errors: () => [], intercept: interceptIn(dom.window) };
    try { run(create(dom.window.document, host)); }
    finally { clock.restore(); }
  });
