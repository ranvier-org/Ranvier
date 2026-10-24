// The source check keyed-rows.mjs applies before it writes a run into docs/research/arms-match/, the committed runs the
// Arms.Match budget gate pools (plan 2026-10-24-issue-157.md, Decision P9).
import { execFileSync } from "node:child_process";

/**
 * The hashed sources a committed run may not record: each path whose hash at the start of the run differs from its hash
 * at the end, and each path in `uncommitted`.
 */
export const staleSources = ({ before, after, uncommitted }) =>
  Object.keys(after).filter(path => before[path] !== after[path] || uncommitted.includes(path));

/** The paths of `files` that differ from HEAD in the working tree or the index, untracked paths included. */
export const uncommittedOf = (cwd, files) =>
  files.filter(path => execFileSync("git", ["status", "--porcelain", "--untracked-files=all", "--", path], { cwd, encoding: "utf8" }).trim() !== "");
