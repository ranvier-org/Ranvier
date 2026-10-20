import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases as counterCases } from "../generated-tests-counters/SugarCounterChecks.js";
import { cases as twinCases } from "../generated-tests-counters/SugarChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
globalThis.ranvierExpectCounters = true;
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; delete globalThis.ranvierExpectCounters; });
for (const [name, check] of [...counterCases, ...twinCases]) test(name, () => check());
