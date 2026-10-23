import test from "node:test";
import assert from "node:assert/strict";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { execSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { join } from "node:path";

const root = fileURLToPath(new URL("../", import.meta.url));
const document = body => `<!doctype html><html><head><title>Fixture</title></head><body>${body}</body></html>`;

function fixture(t) {
  const directory = mkdtempSync(join(root, ".build-test-"));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  for (const name of ["story", "src", "generated-server"]) mkdirSync(join(directory, name));
  copyFileSync(join(root, "vite.config.mjs"), join(directory, "vite.config.mjs"));
  writeFileSync(join(directory, "index.html"), document("Index"));
  writeFileSync(join(directory, "story/index.html"), document('<div id="app"></div>'));
  writeFileSync(join(directory, "src/Story.fs"), "");
  return directory;
}

test("Story refuses a missing server render instead of returning the client shell", async t => {
  const directory = fixture(t);
  const { default: config } = await import(pathToFileURL(join(directory, "vite.config.mjs")));
  const transform = config.plugins.find(plugin => plugin.name === "ranvier-server-render").transformIndexHtml.handler;
  assert.throws(() => transform("client shell", { path: "/story/index.html" }), /generated-server\/story\.html.*npm run server/);
  assert.equal(transform("other page", { path: "/physics/index.html" }), "other page");
  writeFileSync(join(directory, "generated-server/story.html"), document("Server Story"));
  assert.equal(transform("client shell", { path: "/story/index.html" }), document("Server Story"));
});

for (const initial of ["missing", "stale"]) {
  test(`the supported build replaces ${initial} Story output before bundling`, t => {
    const directory = fixture(t);
    if (initial === "stale") writeFileSync(join(directory, "generated-server/story.html"), document("Stale Story"));
    const pkg = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
    pkg.scripts.compile = "node compile.mjs";
    pkg.scripts.server = "node server.mjs";
    writeFileSync(join(directory, "package.json"), JSON.stringify(pkg));
    writeFileSync(join(directory, "compile.mjs"), 'import { writeFileSync } from "node:fs"; writeFileSync("compiled", "ready");');
    writeFileSync(join(directory, "server.mjs"), `import { readFileSync, writeFileSync } from "node:fs"; readFileSync("compiled"); writeFileSync("generated-server/story.html", ${JSON.stringify(document("Fresh server Story"))});`);
    execSync("npm run build", { cwd: directory, stdio: "pipe" });
    assert.match(readFileSync(join(directory, "dist/story/index.html"), "utf8"), /Fresh server Story/);
  });
}

test("a failed server render stops the supported build before publishing stale output", t => {
  const directory = fixture(t);
  writeFileSync(join(directory, "generated-server/story.html"), document("Stale Story"));
  const pkg = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
  pkg.scripts.compile = "node -e \"process.exit(0)\"";
  pkg.scripts.server = "node -e \"process.exit(23)\"";
  writeFileSync(join(directory, "package.json"), JSON.stringify(pkg));
  assert.throws(() => execSync("npm run build", { cwd: directory, stdio: "pipe" }));
  assert.equal(existsSync(join(directory, "dist/story/index.html")), false);
});
