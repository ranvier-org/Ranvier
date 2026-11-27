// Merges the measurements of a leg taken one runtime per job (ab-<leg>-<runtime>.json) into the one AbRun file of the
// leg (ab-<leg>.json). Every process keeps its figures: the merge concatenates processes, counter runs and missing
// baselines in the order the leg measures its runtimes.
// Usage: node .github/scripts/perf-merge.cjs <dir>
const fs = require('node:fs');
const path = require('node:path');

// The runtimes each leg measures, in measurement order (Runtimes.measured in bench/Ranvier.Counters/AbModel.fs).
const runtimes = { 'arm64-counters': ['node', 'node-jitless', 'dotnet'], 'x64-timing': ['node', 'dotnet'] };

function runtimesOf(run) {
  return new Set([...run.Processes, ...run.Counters].map(p => p.Runtime).concat(run.NoBaseline.map(n => n.Name)));
}

function merge(runs) {
  if (runs.length === 0) throw new Error('A merge needs at least one measurement.');
  const [first] = runs;
  for (const field of ['Leg', 'Base', 'Head', 'Rounds']) {
    for (const run of runs) {
      if (run[field] !== first[field]) throw new Error(`Shards disagree on ${field}: ${first[field]} and ${run[field]}.`);
    }
  }
  const order = runtimes[first.Leg];
  if (!order) throw new Error(`Unknown leg ${first.Leg}.`);
  const owner = new Map();
  for (const run of runs) {
    for (const runtime of runtimesOf(run)) {
      if (owner.has(runtime)) throw new Error(`Runtime ${runtime} is measured by more than one shard.`);
      owner.set(runtime, run);
    }
  }
  const rank = run => Math.min(...[...runtimesOf(run)].map(r => order.indexOf(r)).filter(i => i >= 0), order.length);
  const sorted = [...runs].sort((a, b) => rank(a) - rank(b));
  const versions = new Map();
  for (const label of sorted.flatMap(run => run.Versions)) {
    if (versions.has(label.Name) && versions.get(label.Name) !== label.Value) {
      throw new Error(`Shards disagree on ${label.Name}: ${versions.get(label.Name)} and ${label.Value}.`);
    }
    versions.set(label.Name, label.Value);
  }
  return {
    Leg: first.Leg,
    Machine: [...new Set(sorted.map(run => run.Machine))].sort().join(' + '),
    Base: first.Base,
    Head: first.Head,
    Rounds: first.Rounds,
    Versions: [...versions].map(([Name, Value]) => ({ Name, Value })),
    NoBaseline: sorted.flatMap(run => run.NoBaseline),
    Processes: sorted.flatMap(run => run.Processes),
    Counters: sorted.flatMap(run => run.Counters),
  };
}

// Writes <dir>/ab-<leg>.json for each leg with shard files in <dir>, and returns those legs. A leg with shard files
// needs one for every runtime it measures.
function mergeDirectory(dir) {
  const merged = [];
  for (const [leg, legRuntimes] of Object.entries(runtimes)) {
    const files = legRuntimes.map(runtime => path.join(dir, `ab-${leg}-${runtime}.json`));
    const present = files.filter(file => fs.existsSync(file));
    if (present.length === 0) continue;
    const missing = legRuntimes.filter((_, i) => !fs.existsSync(files[i]));
    if (missing.length > 0) throw new Error(`The ${leg} shards lack ${missing.join(', ')}.`);
    const run = merge(files.map(file => JSON.parse(fs.readFileSync(file, 'utf8'))));
    fs.writeFileSync(path.join(dir, `ab-${leg}.json`), JSON.stringify(run, null, 2));
    merged.push(leg);
  }
  return merged;
}

if (require.main === module) {
  const legs = mergeDirectory(process.argv[2] ?? '.');
  console.log(legs.length ? `Merged the shards of ${legs.join(' and ')}.` : 'No shard files to merge.');
}

module.exports = { runtimes, merge, mergeDirectory };
