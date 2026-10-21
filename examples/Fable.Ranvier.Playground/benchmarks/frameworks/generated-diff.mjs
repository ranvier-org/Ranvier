// Gate 3 of the DOM twin spec (§7.1): the Fable output files that differ between a base and a branch directory, with
// lines starting `import ` ignored and `fable_modules/` (package output, which `build.fsx test` deletes) skipped. Exits 1 when a differing file does not match the allowed pattern.
// Usage: node benchmarks/frameworks/generated-diff.mjs <base dir> <branch dir> [--allow <regex>]
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const filesUnder = root => {
  const result = [];
  const walk = directory => {
    for (const name of readdirSync(directory)) {
      const path = join(directory, name);
      if (statSync(path).isDirectory()) { if (name !== "fable_modules") walk(path); }
      else if (name.endsWith(".js")) result.push(relative(root, path).split(sep).join("/"));
    }
  };
  walk(root);
  return result;
};

const body = path => readFileSync(path, "utf8").split("\n").filter(line => !line.startsWith("import ")).join("\n");

export function diffDirectories(base, branch, allow) {
  const names = new Set([...filesUnder(base), ...filesUnder(branch)]);
  const differing = [];
  for (const name of [...names].sort()) {
    let left = null, right = null;
    try { left = body(join(base, name)); } catch { }
    try { right = body(join(branch, name)); } catch { }
    if (left !== right) differing.push(name);
  }
  return { differing, disallowed: differing.filter(name => !allow.test(name)) };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [base, branch] = process.argv.slice(2);
  const index = process.argv.indexOf("--allow");
  const allow = new RegExp(index < 0 ? "(^|/)Native\\.js$" : process.argv[index + 1]);
  const { differing, disallowed } = diffDirectories(base, branch, allow);
  for (const name of differing) console.log(`${disallowed.includes(name) ? "DIFFERS" : "allowed"} ${name}`);
  process.exitCode = disallowed.length ? 1 : 0;
}
