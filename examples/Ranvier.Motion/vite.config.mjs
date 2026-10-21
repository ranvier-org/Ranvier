// The five pages and the index as one multi-page app. The story page is served with the body the .NET server rendered
// (npm run server writes generated-server/story.html) in place of its <!--ssr--> marker, so the client hydrates it; without
// that file the page mounts on the client.
import { defineConfig } from "vite";
import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));
const pages = ["kanban", "dashboard", "story", "physics", "routes"].filter(page => existsSync(`${here}${page}/index.html`));
const served = new URL("./generated-server/story.html", import.meta.url);

const serverRender = {
  name: "ranvier-server-render",
  transformIndexHtml(html, context) {
    if (!context.path.includes("/story/")) return html;
    const body = existsSync(served) ? readFileSync(served, "utf8") : "";
    return html.replace("<!--ssr-->", body);
  }
};

export default defineConfig({
  root: here,
  plugins: [serverRender],
  build: {
    outDir: "dist",
    emptyOutDir: true,
    manifest: true,
    rollupOptions: {
      input: Object.fromEntries([["index", `${here}index.html`], ...pages.map(page => [page, `${here}${page}/index.html`])])
    }
  }
});
