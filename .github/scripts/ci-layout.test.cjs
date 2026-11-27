const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const workflow = fs.readFileSync(path.join(__dirname, '../workflows/ci.yml'), 'utf8');
const jobs = Object.fromEntries([...workflow.slice(workflow.indexOf('\njobs:')).matchAll(/^  ([a-z][a-z-]*):\r?\n([\s\S]*?)(?=^  [a-z][a-z-]*:\r?$|(?![\s\S]))/gm)].map(([, id, body]) => [id, body]));

test('each core .NET gate runs in exactly one job', () => {
  const gates = {
    dotnet: ['Test Ranvier.Tests', 'Test Ranvier.CSharp.Tests', 'Test Ranvier.Wire.Tests', 'Test shared mutation observation', 'Check the vendored Thoth.Json'],
    'dotnet-traced': ['Test Ranvier.Tests, traced', 'Test Ranvier.Docs.Maps.Tests, traced'],
    'package-consumers': ['Check package compatibility'],
  };
  for (const [id, names] of Object.entries(gates)) {
    assert.match(jobs[id], /needs: changes/);
    for (const name of names) {
      const owners = Object.entries(jobs).filter(([, body]) => body.split(/\r?\n/).some(line => line.trim() === `- name: ${name}`)).map(([owner]) => owner);
      assert.deepEqual(owners, [id], name);
    }
  }
});

test('the untraced and traced test jobs each build one flavour of the core', () => {
  const tests = id => jobs[id].split(/\r?\n/).filter(line => /run: dotnet (test|run) /.test(line));
  assert.ok(tests('dotnet').length > 0);
  for (const line of tests('dotnet')) assert.doesNotMatch(line, /RanvierTrace=true/, line);
  assert.ok(tests('dotnet-traced').length > 0);
  for (const line of tests('dotnet-traced')) assert.match(line, /-p:RanvierTrace=true/, line);
});

test('one job packs for both package consumer checks', () => {
  const checks = Object.entries(jobs).filter(([, body]) => body.includes('check-package-compatibility.ps1')).map(([id]) => id);
  assert.deepEqual(checks, ['package-consumers']);
  assert.match(jobs['package-consumers'], /check-package-compatibility\.ps1 -CheckFable/);
  assert.match(jobs['package-consumers'], /dotnet-version: 8\.0\.x/);
  assert.match(jobs['package-consumers'], /dotnet tool restore/);
});

test('required CI result waits for every verification job and rejects failed or cancelled jobs', () => {
  const needed = /needs: \[([^\]]+)\]/.exec(jobs.result)[1].split(',').map(x => x.trim()).sort();
  assert.deepEqual(needed, Object.keys(jobs).filter(id => id !== 'result').sort());
  assert.match(jobs.result, /if: always\(\)/);
  assert.match(jobs.result, /success\|skipped\) ;; \*\) exit 1/);
});
