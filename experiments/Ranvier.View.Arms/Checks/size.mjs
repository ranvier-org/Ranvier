// Bundle budget of Arms.Match (spec, "Bundle budget"), over the ArmsSizeEntries functions in the Playground's
// generated-tests (run npm run compile:tests there first), bundled by the Playground's Vite with minification, gzip
// level 9 and Brotli quality 11:
// - one: the Arms.Match loader against its explicit twin, at most 250 B gzip;
// - perSite: a second site's delta (two sites minus one) against its twin's, at most 60 B gzip;
// - plain: an entry calling no Arms.Match holds no Ranvier.View.Arms module (Ruling 12);
// - quotation: the unminified Arms.Match entry holds no quotation code;
// - helpers: the emitted Ranvier.View.Arms modules and ArmsSizeEntries.js hold no curry, uncurry or partialApply.
// Exits 1 on a breach. Writes docs/research/arms-match/bundle.json.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { gzipSync, brotliCompressSync, constants } from "node:zlib";

const here = new URL("./", import.meta.url);
const playground = new URL("../../../examples/Fable.Ranvier.Playground/", here);
const generated = new URL("generated-tests/", playground);
const output = new URL("../../../docs/research/arms-match/bundle.json", here);
const pathOf = url => decodeURIComponent(url.pathname).replace(/^\/([A-Za-z]:)/, "$1");
mkdirSync(new URL("generated-size/", here), { recursive: true });

const build = (name, minify) => {
  writeFileSync(new URL(`generated-size/entry-${name}.mjs`, here),
    `export { ${name} as mount } from "../../../../examples/Fable.Ranvier.Playground/generated-tests/ArmsSizeEntries.js";\n`);
  execFileSync(process.execPath, [pathOf(new URL("node_modules/vite/bin/vite.js", playground)), "build", "--config", "size.config.mjs"], {
    cwd: pathOf(here), stdio: ["ignore", 2, 2], windowsHide: true, env: { ...process.env, ARMS_SIZE_ENTRY: name, ARMS_SIZE_MINIFY: String(minify) }
  });
  return readFileSync(new URL(`generated-size/${name}${minify ? "" : "-unminified"}/bundle.js`, here));
};
const sizeOf = code => ({
  raw: code.length,
  gzip: gzipSync(code, { level: 9 }).length,
  brotli: brotliCompressSync(code, { params: { [constants.BROTLI_PARAM_QUALITY]: 11 } }).length
});

const sizes = Object.fromEntries(["explicitOne", "matchOne", "explicitTwo", "matchTwo", "plain"].map(name => [name, sizeOf(build(name, true))]));
const one = sizes.matchOne.gzip - sizes.explicitOne.gzip;
const perSite = sizes.matchTwo.gzip - sizes.explicitTwo.gzip - one;
// Unminified bundles keep a region comment naming each bundled module's path.
const armsModule = /Ranvier\.View\.Arms\//;
const matchUnminified = build("matchOne", false).toString();
const plainHoldsArms = armsModule.test(build("plain", false).toString());
const quotation = /quotation/i.test(matchUnminified);
const helperFiles = ["experiments/Ranvier.View.Arms/Cells.js", "experiments/Ranvier.View.Arms/Site.js", "experiments/Ranvier.View.Arms/Arms.js", "ArmsSizeEntries.js"];
const helpers = Object.fromEntries(helperFiles.map(file =>
  [file, (readFileSync(new URL(file, generated), "utf8").match(/\b(curry\d*|uncurry\d*|partialApply)\b/g) ?? []).length]));

const result = {
  sizes,
  one: { deltaGzip: one, budgetGzip: 250, withinBudget: one <= 250 },
  perSite: { deltaGzip: perSite, budgetGzip: 60, withinBudget: perSite <= 60 },
  plain: { matchEntryHoldsArmsModule: armsModule.test(matchUnminified), plainHoldsArmsModule: plainHoldsArms, withinBudget: armsModule.test(matchUnminified) && !plainHoldsArms },
  quotation: { present: quotation, withinBudget: !quotation },
  helpers: { counts: helpers, withinBudget: Object.values(helpers).every(count => count === 0) }
};
mkdirSync(new URL("./", output), { recursive: true });
writeFileSync(output, JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify(result, null, 2));
if (["one", "perSite", "plain", "quotation", "helpers"].some(key => !result[key].withinBudget)) process.exitCode = 1;
