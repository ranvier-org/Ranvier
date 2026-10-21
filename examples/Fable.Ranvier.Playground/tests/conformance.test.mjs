// The shared conformance cases under jsdom. Writes each case's artefacts to generated-conformance/jsdom/<module>/<case>/.
import test, { after, mock } from "node:test";
import { JSDOM, VirtualConsole } from "jsdom";
import { mkdirSync, rmSync, writeFileSync } from "node:fs";
import { create } from "../generated-tests/JsdomHarness.js";
import { modules } from "../generated-tests/ConformanceCases.js";
import { skippedOn } from "../generated-tests/experiments/Ranvier.View.Testing/Conformance.js";

// Listener failures go to the virtual console, as they would to a browser's; the harness hands them to the case.
const listenerErrors = [];
const virtualConsole = new VirtualConsole();
virtualConsole.on("jsdomError", e => listenerErrors.push(e.detail?.message ?? e.message));
const dom = new JSDOM("<!doctype html><html><head></head><body></body></html>", { virtualConsole });
const globals = ["window", "document", "Event", "MouseEvent", "PointerEvent", "KeyboardEvent", "InputEvent", "FocusEvent"];
globalThis.window = dom.window;
for (const name of globals.slice(1)) globalThis[name] = name === "document" ? dom.window.document : dom.window[name];
after(() => { dom.window.close(); for (const name of globals) delete globalThis[name]; });

const root = new URL("../generated-conformance/jsdom/", import.meta.url);
rmSync(root, { recursive: true, force: true });
export const slug = name => name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "").slice(0, 80);

for (const [moduleName, cases] of modules) {
  for (const [name, run] of cases) {
    if (skippedOn("jsdom", name)) { test(`${moduleName}: ${name}`, { skip: "another host's case" }, () => {}); continue; }
    test(`${moduleName}: ${name}`, () => {
      const directory = new URL(`${moduleName}/${slug(name)}/`, root);
      let count = 0;
      const record = (artefact, text) => {
        mkdirSync(directory, { recursive: true });
        writeFileSync(new URL(`${String(++count).padStart(2, "0")}-${slug(artefact)}.txt`, directory), text);
      };
      mock.timers.enable({ apis: ["setTimeout", "setInterval", "Date"] });
      listenerErrors.length = 0;
      try { run(create(dom.window.document, record, ms => mock.timers.tick(ms), () => listenerErrors.splice(0))); }
      finally { mock.timers.reset(); }
    });
  }
}
