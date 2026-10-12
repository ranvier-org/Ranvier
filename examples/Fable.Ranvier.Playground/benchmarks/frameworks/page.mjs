import { mount as ranvierMount } from "../../generated-tests/FrameworkPerf.js";
import { mount as rippleMount } from "../../generated-ripple/RipplePerf.js";
import { mount as solidMount } from "./solid.jsx";
import { mount as reactMount } from "./react.mjs";
import { verify, expectState } from "./contract.mjs";

const adapt = (mount, id = false) => (host, width) => {
  const [update, dispose] = mount(id ? host.id : host, width);
  return { update, dispose, flush() {} };
};
const adapters = { ranvier: adapt(ranvierMount), ripple: adapt(rippleMount, true), solid: solidMount, react: reactMount };
let hostIndex = 0;
function host() {
  const node = document.createElement("main");
  node.id = `framework-host-${++hostIndex}`;
  document.body.append(node);
  return node;
}

window.frameworkPerf = {
  verify(mode, width) {
    try { return verify(adapters[mode], width); }
    catch (error) { throw Error(`${mode}, width ${width}: ${error.message}`); }
  },
  sample(mode, width, phase, iterations) {
    const mount = adapters[mode];
    const hosts = [], instances = [];
    try {
      const count = phase === "update" ? 1 : iterations;
      for (let i = 0; i < count; i++) hosts.push(host());
      if (phase !== "mount") for (const node of hosts) instances.push(mount(node, width));
      const start = performance.now();
      if (phase === "mount") {
        for (const node of hosts) instances.push(mount(node, width));
      } else if (phase === "update") {
        for (let i = 1; i <= iterations; i++) instances[0].update(i % 2 ? "blocked" : "a");
      } else if (phase === "dispose") {
        for (const instance of instances) instance.dispose();
      } else throw Error(`Unknown phase ${phase}`);
      const ms = (performance.now() - start) / iterations;
      if (phase === "update") expectState(hosts[0], width, iterations % 2 ? "blocked" : "a");
      else if (phase === "mount") for (const node of hosts) expectState(node, width, "a");
      else if (hosts.some(node => node.childNodes.length)) throw Error("Disposal retained DOM");
      return ms;
    } finally {
      for (const instance of instances) instance.dispose();
      for (const node of hosts) node.remove();
    }
  },
  retain(mode, width, count) {
    const hosts = Array.from({ length: count }, host);
    const instances = hosts.map(node => adapters[mode](node, width));
    return () => {
      for (const instance of instances) instance.dispose();
      for (const node of hosts) node.remove();
      hosts.length = 0; instances.length = 0;
    };
  }
};
