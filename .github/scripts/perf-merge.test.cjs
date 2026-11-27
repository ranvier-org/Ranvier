const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { merge, mergeDirectory } = require('./perf-merge.cjs');
const { usable } = require('./perf-reuse.cjs');

const base = 'a'.repeat(40);
const head = 'b'.repeat(40);
const metric = leg => (leg === 'arm64-counters' ? 'instructions' : 'cpu_ns');

// One job's measurement of `runtime` on `leg`: five processes a side, and a counter run a side except under node-jitless.
function shard(leg, runtime, { machine = 'Neoverse-N2', versions = [], ...over } = {}) {
  const sides = [['base', base], ['head', head]];
  const scenarios = [{ Scenario: 'chain', Engine: 'ranvier', Gc: false, Compiled: true, Metrics: [{ Name: metric(leg), Value: 1 }] }];
  return {
    Leg: leg,
    Machine: machine,
    Base: base,
    Head: head,
    Rounds: 5,
    Versions: [{ Name: 'Node.js', Value: 'v26.0.0' }, ...versions],
    NoBaseline: [],
    Processes: sides.flatMap(([side, commit]) => [0, 1, 2, 3, 4].map(i => ({ Side: side, Commit: commit, Runtime: runtime, Process: i, Scenarios: scenarios }))),
    Counters: runtime === 'node-jitless' ? [] : sides.map(([side, commit]) => ({ Side: side, Commit: commit, Runtime: runtime, Process: 0, Scenarios: scenarios })),
    ...over,
  };
}
const arm = ['dotnet', 'node', 'node-jitless'].map(runtime => shard('arm64-counters', runtime));

test('merged shards form the measurement of the whole leg, in the order the leg measures its runtimes', () => {
  const run = merge(arm);
  assert.ok(usable(run, 'arm64-counters', base, head));
  assert.deepEqual([...new Set(run.Processes.map(p => p.Runtime))], ['node', 'node-jitless', 'dotnet']);
  assert.equal(run.Processes.length, 30);
  assert.equal(run.Counters.length, 4);
  assert.equal(run.Machine, 'Neoverse-N2');
  assert.deepEqual(run.Versions, [{ Name: 'Node.js', Value: 'v26.0.0' }]);
});
test('shards on different machines name both machines', () => {
  const run = merge([shard('x64-timing', 'node', { machine: 'EPYC 9V74' }), shard('x64-timing', 'dotnet', { machine: 'Xeon 8370C' })]);
  assert.equal(run.Machine, 'EPYC 9V74 + Xeon 8370C');
  assert.ok(usable(run, 'x64-timing', base, head));
});
test('shards of different legs, commits or rounds do not merge', () => {
  assert.throws(() => merge([shard('arm64-counters', 'node'), shard('x64-timing', 'dotnet')]), /Leg/);
  assert.throws(() => merge([shard('x64-timing', 'node'), shard('x64-timing', 'dotnet', { Head: 'c'.repeat(40) })]), /Head/);
  assert.throws(() => merge([shard('x64-timing', 'node'), shard('x64-timing', 'dotnet', { Rounds: 3 })]), /Rounds/);
  assert.throws(() => merge([]), /at least one/);
});
test('a runtime measured by two shards, or labels that disagree, do not merge', () => {
  assert.throws(() => merge([shard('x64-timing', 'node'), shard('x64-timing', 'node')]), /node.*more than one/);
  assert.throws(() => merge([shard('x64-timing', 'node'), shard('x64-timing', 'dotnet', { versions: [{ Name: 'Node.js', Value: 'v25.0.0' }] })]), /Node\.js/);
});
test('a directory of shard files gains one file per leg, and a leg without shard files keeps its file', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'perf-merge-'));
  try {
    for (const run of arm) fs.writeFileSync(path.join(dir, `ab-arm64-counters-${run.Processes[0].Runtime}.json`), JSON.stringify(run));
    const kept = JSON.stringify(shard('x64-timing', 'node'));
    fs.writeFileSync(path.join(dir, 'ab-x64-timing.json'), kept);
    assert.deepEqual(mergeDirectory(dir), ['arm64-counters']);
    assert.ok(usable(JSON.parse(fs.readFileSync(path.join(dir, 'ab-arm64-counters.json'), 'utf8')), 'arm64-counters', base, head));
    assert.equal(fs.readFileSync(path.join(dir, 'ab-x64-timing.json'), 'utf8'), kept);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('a leg missing a runtime shard is an error', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'perf-merge-'));
  try {
    fs.writeFileSync(path.join(dir, 'ab-x64-timing-node.json'), JSON.stringify(shard('x64-timing', 'node')));
    assert.throws(() => mergeDirectory(dir), /x64-timing.*dotnet/);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
