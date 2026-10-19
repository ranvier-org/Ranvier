import { mountAuthored, mountTemplate, clickCount } from "../../generated-tests/DispatchPerf.js";

const mounts = { authored: mountAuthored, template: mountTemplate };

window.dispatchClicks = {
  // Mounts `count` clickable rows, clicks each `clicksPerRow` times, and returns microseconds per click.
  sample(mode, count, clicksPerRow) {
    const host = document.createElement("div");
    document.body.append(host);
    const dispose = mounts[mode](host, count);
    const targets = Array.from(host.querySelectorAll("button, a"));
    if (targets.length !== count) throw Error(`${mode}: ${targets.length} targets for ${count}`);
    const before = clickCount();
    const start = performance.now();
    for (let pass = 0; pass < clicksPerRow; pass++) for (const target of targets) target.click();
    const us = (performance.now() - start) * 1000 / (count * clicksPerRow);
    if (clickCount() - before !== count * clicksPerRow) throw Error(`${mode}: ${clickCount() - before} handler calls`);
    dispose();
    host.remove();
    return us;
  }
};
