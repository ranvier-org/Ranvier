// The fickl release named in .config/dotnet-tools.json carries the browser compiler the docs site's live editor
// runs: fickl-standalone-<V>.tgz and fickl-metadata-<V>.tgz. Run on every pull request, so a roll to a release
// without them fails before it merges.
// Usage: node .github/scripts/fickl-archives.cjs   (exits 1 and lists each missing archive)
const MANIFEST = '.config/dotnet-tools.json';
const BASE = 'https://github.com/shayanhabibi/fickl/releases/download';

/** The fickl version in the tool manifest's text. */
function version(manifestText) {
  let manifest;
  try {
    manifest = JSON.parse(manifestText);
  } catch (error) {
    throw new Error(`${MANIFEST} is not JSON: ${error.message}`);
  }
  const found = manifest?.tools?.fickl?.version;
  if (typeof found !== 'string' || found === '') throw new Error(`No fickl entry in ${MANIFEST}`);
  return found;
}

/** The two archives of a fickl release. */
function urls(v) {
  return {
    standalone: `${BASE}/v${v}/fickl-standalone-${v}.tgz`,
    metadata: `${BASE}/v${v}/fickl-metadata-${v}.tgz`,
  };
}

/** Asks for both archives of the manifest's release; a release asset answers through a redirect. */
async function check({ manifestText, fetch }) {
  const v = version(manifestText);
  const missing = [];
  for (const url of Object.values(urls(v))) {
    let present = false;
    try {
      present = (await fetch(url, { method: 'HEAD', redirect: 'follow' })).ok;
    } catch {
      present = false;
    }
    if (!present) missing.push(url);
  }
  return { version: v, missing };
}

module.exports = { version, urls, check };

if (require.main === module) {
  const { readFileSync } = require('node:fs');
  const { join } = require('node:path');
  (async () => {
    const manifestText = readFileSync(join(__dirname, '../..', MANIFEST), 'utf8');
    const { version: v, missing } = await check({ manifestText, fetch: globalThis.fetch });
    console.log(`fickl ${v}`);
    for (const url of missing) console.log(`missing: ${url}`);
    if (missing.length) {
      console.log(`The fickl release in ${MANIFEST} lacks the browser compiler the docs live editor runs.`);
      process.exit(1);
    }
  })().catch(error => {
    console.error(error.message);
    process.exit(1);
  });
}
