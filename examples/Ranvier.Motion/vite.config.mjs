import { defineConfig } from "vite";
import { existsSync, readFileSync, statSync } from "node:fs";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));
const pages = ["kanban", "dashboard", "story", "physics", "routes"].filter(page => existsSync(`${here}${page}/index.html`));
const served = new URL("./generated-server/story.html", import.meta.url);
const source = new URL("./src/Story.fs", import.meta.url);

const serverRender = {
  name: "ranvier-server-render",
  transformIndexHtml: {
    order: "pre",
    handler(html, context) {
      if (!context.path.includes("/story/")) return html;
      if (!existsSync(served))
        throw new Error("Missing generated-server/story.html: run npm run server before serving Story, or npm run build for a complete build");
      if (statSync(served).mtimeMs < statSync(source).mtimeMs)
        console.warn("generated-server/story.html is older than src/Story.fs: run npm run server");
      return readFileSync(served, "utf8");
    }
  }
};

export default defineConfig({
  root: here,
  // Relative asset urls, so the build serves from any subpath (the docs site publishes it under examples/motion/).
  base: "./",
  plugins: [serverRender],
  // Pre-bundled at start, so the dev server does not reload a page mid-test when it discovers the dependency.
  optimizeDeps: { include: ["animejs"] },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    manifest: true,
    rollupOptions: {
      input: Object.fromEntries([["index", `${here}index.html`], ...pages.map(page => [page, `${here}${page}/index.html`])])
    }
  }
});
