import { mountReveal as ranvierReveal, mountSwitchReveal as ranvierSwitch } from "../../generated-tests/BoundaryPerf.js";
import { mountReveal as solidReveal, mountSwitchReveal as solidSwitch } from "./boundary-solid.jsx";

// "reveal" mounts the boundary at the root; "switch" mounts it from a branch switch, inside a flush.
const scenarios = { reveal: { ranvier: ranvierReveal, solid: solidReveal }, switch: { ranvier: ranvierSwitch, solid: solidSwitch } };
let hostIndex = 0;
const freshHost = () => {
  const host = document.createElement("div");
  host.id = `boundary-${hostIndex++}`;
  document.body.append(host);
  return host;
};

window.boundaryReveal = {
  // Mounts `count` boundaries of `width` rows on fresh hosts, each revealed; returns milliseconds per boundary.
  async sample(mode, width, count, scenario = "reveal") {
    const mount = scenarios[scenario][mode];
    const hosts = Array.from({ length: count }, freshHost), disposals = [];
    const start = performance.now();
    for (const host of hosts) disposals.push(await mount(host, width));
    const ms = (performance.now() - start) / count;
    for (const dispose of disposals) dispose();
    for (const host of hosts) host.remove();
    return ms;
  },
  async verify(mode, width, scenario = "reveal") {
    const mount = scenarios[scenario][mode];
    const host = freshHost();
    const dispose = await mount(host, width);
    const rows = Array.from(host.querySelectorAll(".row"), row => row.textContent);
    dispose();
    host.remove();
    const expected = Array.from({ length: width }, (_, i) => `ready${i}`);
    if (JSON.stringify(rows) !== JSON.stringify(expected)) throw Error(`${scenario}/${mode}/${width}: ${JSON.stringify(rows)}`);
    return { scenario, mode, width, rows: rows.length };
  },
  async retain(mode, width, count, scenario = "reveal") {
    const mount = scenarios[scenario][mode];
    const hosts = Array.from({ length: count }, freshHost), disposals = [];
    for (const host of hosts) disposals.push(await mount(host, width));
    return () => { for (const dispose of disposals) dispose(); for (const host of hosts) host.remove(); };
  }
};
