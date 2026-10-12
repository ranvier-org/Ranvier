import { defineConfig } from "vite";
import { transformAsync } from "@babel/core";
import { fileURLToPath } from "node:url";

const headers = {
  "Cross-Origin-Opener-Policy": "same-origin",
  "Cross-Origin-Embedder-Policy": "require-corp"
};
export default defineConfig({
  plugins: [{
    name: "benchmark-solid-jsx", enforce: "pre",
    async transform(code, id) {
      if (!id.endsWith("/frameworks/solid.jsx")) return;
      return await transformAsync(code, {
        filename: id, babelrc: false, configFile: false,
        presets: [["babel-preset-solid", { generate: "dom", hydratable: false }]]
      });
    }
  }],
  build: {
    outDir: "generated-framework-dist", emptyOutDir: true,
    rollupOptions: { input: fileURLToPath(new URL("index.html", import.meta.url)) }
  },
  server: { headers }, preview: { headers }
});
