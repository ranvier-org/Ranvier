import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases } from "../generated-tests/MountingInvestigationChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
globalThis.window = dom.window;
globalThis.document = dom.window.document;
after(() => { dom.window.close(); delete globalThis.window; delete globalThis.document; });
for (const [name, run] of cases) test(name, run);
