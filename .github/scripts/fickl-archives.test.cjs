const test = require('node:test');
const assert = require('node:assert/strict');
const { version, urls, check } = require('./fickl-archives.cjs');

const manifest = v => JSON.stringify({ version: 1, tools: { fickl: { version: v, commands: ['fickl'] } } });
const base = 'https://github.com/shayanhabibi/fickl/releases/download';

test('the version is the manifest\'s fickl entry', () => {
  assert.equal(version(manifest('0.11.1')), '0.11.1');
  assert.equal(version(manifest('0.12.0-beta.1')), '0.12.0-beta.1');
});

test('a manifest with no fickl entry, or that is not JSON, names the file', () => {
  assert.throws(() => version(JSON.stringify({ tools: {} })), /\.config\/dotnet-tools\.json/);
  assert.throws(() => version('not json'), /\.config\/dotnet-tools\.json/);
});

test('the two archives of a version', () => {
  assert.deepEqual(urls('0.11.1'), {
    standalone: `${base}/v0.11.1/fickl-standalone-0.11.1.tgz`,
    metadata: `${base}/v0.11.1/fickl-metadata-0.11.1.tgz`,
  });
});

test('a release with both archives has none missing', async () => {
  const asked = [];
  const fetch = async (url, options) => { asked.push([url, options.method, options.redirect]); return { ok: true }; };
  assert.deepEqual(await check({ manifestText: manifest('0.11.1'), fetch }), { version: '0.11.1', missing: [] });
  assert.deepEqual(asked.map(a => a.slice(1)), [['HEAD', 'follow'], ['HEAD', 'follow']]);
});

test('a release that lacks one archive names it', async () => {
  const fetch = async url => ({ ok: !url.includes('fickl-metadata'), status: 404 });
  assert.deepEqual(await check({ manifestText: manifest('0.11.0'), fetch }),
    { version: '0.11.0', missing: [`${base}/v0.11.0/fickl-metadata-0.11.0.tgz`] });
});
