import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import path from 'node:path';

const [compiled, fixture, reportPath] = process.argv.slice(2).map(p => path.resolve(p));
if (!compiled || !fixture || !reportPath) throw new Error('Expected compiled root, fixture and report paths');
async function entries(dir) {
  const all = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const target = path.join(dir, entry.name);
    if (entry.isDirectory()) all.push(...await entries(target));
    else if (entry.name === 'BrowserMain.js') all.push(target);
  }
  return all;
}
const candidates = await entries(compiled);
if (candidates.length !== 1) throw new Error(`Expected one BrowserMain.js, found ${candidates.length}`);
const entry = path.relative(compiled, candidates[0]).replaceAll('\\', '/');
const server = createServer(async (request, response) => {
  try {
    const pathname = new URL(request.url, 'http://localhost').pathname;
    if (pathname === '/') {
      response.setHeader('Content-Type', 'text/html');
      response.end(`<script type="module" src="/${entry}"></script>`);
    } else if (pathname === '/fixtures/hmac-provider-rows.json') {
      response.setHeader('Content-Type', 'application/json');
      response.end(await readFile(fixture));
    } else {
      const target = path.resolve(compiled, '.' + decodeURIComponent(pathname));
      if (!target.startsWith(compiled + path.sep)) throw new Error('Path outside compiled output');
      response.setHeader('Content-Type', target.endsWith('.js') ? 'text/javascript' : 'application/octet-stream');
      response.end(await readFile(target));
    }
  } catch (error) {
    response.statusCode = 404;
    response.end(String(error));
  }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
const results = [];
try {
  browser = await chromium.launch({ headless: true, args: ['--host-resolver-rules=MAP local.test 127.0.0.1'] });
  for (const [host, secure] of [['localhost', true], ['local.test', false]]) {
    const context = await browser.newContext();
    try {
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.goto(`http://${host}:${server.address().port}/`);
      await page.waitForFunction(() => globalThis.crypto289Report !== undefined, null, { timeout: 120000 });
      const result = await page.evaluate(() => ({ secure: isSecureContext, report: globalThis.crypto289Report }));
      results.push({ host, ...result, errors });
      if (result.secure !== secure) throw new Error(`Unexpected secure-context status for ${host}`);
    } finally {
      await context.close();
    }
  }
} finally {
  await browser?.close();
  await new Promise(resolve => server.close(resolve));
  await writeFile(reportPath, JSON.stringify({ results }, null, 2) + '\n');
}
const failed = results.length !== 2 || results.some(row => row.errors.length || row.report.failures.length || row.report.assertions < (row.secure ? 339 : 2));
console.log(JSON.stringify({ results, failed }));
process.exitCode = failed ? 1 : 0;
