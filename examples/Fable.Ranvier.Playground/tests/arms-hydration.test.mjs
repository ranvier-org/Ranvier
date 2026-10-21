import test, { after } from "node:test";
import { readFileSync } from "node:fs";
import { JSDOM } from "jsdom";
import { hydrations } from "../generated-tests/ArmsChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; });

const golden = readFileSync(new URL("../../../experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt", import.meta.url), "utf8")
  .replace(/\r\n/g, "\n").replace(/\n+$/, "").split("\n").map(line => line.split("|"));

for (const [name, check] of hydrations)
  test(`${name} hydrates its .NET server HTML under Strict and updates like its twin`, () => {
    const line = golden.find(([fixture, state]) => fixture === name && state === "Loaded done 3");
    check(line.slice(3).join("|"));
  });
