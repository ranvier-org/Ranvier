// The sources keyed-rows.mjs records by hash, and the source checks over them: at write time for a run into
// docs/research/arms-match/, and in the Arms.Match budget gate, which pools only the committed runs whose recorded
// hashes match the tree (plan 2026-10-24-issue-157.md, Decision P9).
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

/**
 * The paths keyed-rows.mjs records by hash, relative to examples/Fable.Ranvier.Playground: the benchmark and its page,
 * the core and view sources both modes compile, and every input of the Arms.Match lowering, the Fable plugin project
 * and each file it compiles included.
 */
export const hashedSources = [
  "benchmarks/frameworks/keyed-rows.mjs", "benchmarks/frameworks/keyed-page.mjs", "benchmarks/frameworks/keyed-solid.jsx", "benchmarks/frameworks/vite.config.mjs", "tests/KeyedRowsPerf.fs", "package-lock.json",
  ...["Lifetime.fs", "Representation.fs", "Template.fs"].map(path => `../../src/Ranvier.View/${path}`), ...["TemplateDom.fs", "Mounting.fs"].map(path => `../../src/Ranvier.View.Dom/${path}`), "../../src/Ranvier/Core.fs", "../../src/Ranvier/Projections.fs", "../../src/Ranvier/Api.fs", "../../src/Ranvier/Combinators.fs",
  "tests/KeyedRowsSugarPerf.fs", "tests/KeyedRowsArmsMatchPerf.fs", ...["Arms.fs", "Cells.fs", "Site.fs", "Errors.fs", "Diagnostics.fs"].map(path => `../../experiments/Ranvier.View.Arms/${path}`),
  ...["Ranvier.View.Arms.FablePlugin.fsproj", "Shape.fs", "Lower.fs", "Plugin.fs"].map(path => `../../experiments/Ranvier.View.Arms.FablePlugin/${path}`)
];

const playground = new URL("../../", import.meta.url);
const sha = bytes => createHash("sha256").update(bytes).digest("hex");

/** The sha256 of `bytes` as given, with LF line endings and with CRLF line endings. */
export const lineEndingHashes = bytes => {
  const lf = Buffer.from(bytes.toString("latin1").replace(/\r\n/g, "\n"), "latin1");
  return [sha(bytes), sha(lf), sha(Buffer.from(lf.toString("latin1").replace(/\n/g, "\r\n"), "latin1"))];
};

/**
 * Each hashed source that `recorded` omits, or records with a hash other than one of `lineEndingHashes` of the source
 * `read` returns. A run with an empty result was measured on the sources of the tree.
 */
export const mismatchedSources = (recorded, read = path => readFileSync(new URL(path, playground))) =>
  hashedSources.filter(path => !Object.hasOwn(recorded ?? {}, path) || !lineEndingHashes(read(path)).includes(recorded[path]));

/**
 * The hashed sources a committed run may not record: each path whose hash at the start of the run differs from its hash
 * at the end, and each path in `uncommitted`.
 */
export const staleSources = ({ before, after, uncommitted }) =>
  Object.keys(after).filter(path => before[path] !== after[path] || uncommitted.includes(path));

/** The paths of `files` that differ from HEAD in the working tree or the index, untracked paths included. */
export const uncommittedOf = (cwd, files) =>
  files.filter(path => execFileSync("git", ["status", "--porcelain", "--untracked-files=all", "--", path], { cwd, encoding: "utf8" }).trim() !== "");
