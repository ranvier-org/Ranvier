import { createRequire } from 'node:module';
import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const require = createRequire(new URL('./package.json', import.meta.url));
const { chromium } = require('playwright');
const root = path.resolve(process.argv[2]);
const expectedChecks = Number(process.argv[4] || 61);
const server = http.createServer(async (request, response) => {
  try {
    const relative = decodeURIComponent(new URL(request.url, 'http://localhost').pathname).replace(/^\//, '');
    if (relative === 'favicon.ico') {
      response.writeHead(204);
      response.end();
      return;
    }
    const file = path.resolve(root, relative || 'index.html');
    if (!file.startsWith(root + path.sep)) throw new Error('Invalid path');
    response.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : 'text/html');
    response.end(relative ? await fs.readFile(file) : '<script type="module" src="/Browser.js"></script>');
  } catch {
    response.writeHead(404);
    response.end();
  }
});

await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
try {
  browser = await chromium.launch({ headless: true, executablePath: process.argv[3] || process.env.RANVIER_CHROMIUM_PATH || undefined });
  const page = await browser.newPage();
  const errors = [];
  const diagnostics = [];
  page.on('pageerror', error => errors.push(error.stack || `${error.name}: ${error.message}`));
  page.on('console', message => {
    diagnostics.push(message.text());
    if (message.type() === 'error') errors.push(message.text());
  });
  await page.goto(`http://127.0.0.1:${server.address().port}`);
  try {
    await page.waitForFunction(() => globalThis.__utf8Proof !== undefined, null, { timeout: 10000 });
  } catch (error) {
    const nativeDecoder = await page.evaluate(() => {
      const decoder = new TextDecoder('utf-8', { fatal: true, ignoreBOM: false });
      const result = [];
      for (const [label, bytes, stream] of [
        ['prefix', [0xef], true], ['invalid', [0xff], true], ['after', [0xef, 0xbb, 0xbf, 0x44], false]
      ]) {
        try { result.push([label, decoder.decode(Uint8Array.from(bytes), { stream })]); }
        catch (error) { result.push([label, error.name]); }
      }
      return result;
    });
    throw new Error(`UTF-8 browser fixture did not publish its result: ${JSON.stringify({ errors, lastChecks: diagnostics.slice(-8), nativeDecoder })}; ${error}`);
  }
  const result = await page.evaluate(() => globalThis.__utf8Proof);
  if (errors.length) throw new Error(JSON.stringify({ ...result, errors }));
  if (result.checks !== expectedChecks) throw new Error(`Incomplete UTF-8 fixture: ${result.checks}/${expectedChecks}`);
  console.log(JSON.stringify({ ...result, browser: browser.version(), pass: true }));
} finally {
  await browser?.close();
  await new Promise(resolve => server.close(resolve));
}
