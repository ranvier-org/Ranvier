const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');

const key = (...parts) => parts.join('\u0000');

// The libraries the counter bench compares with Ranvier, each with the project that pins its version.
const COMPARATORS = {
  'FSharp.Data.Adaptive': 'bench/Ranvier.Counters/Ranvier.Counters.fsproj',
  R3: 'bench/Ranvier.Counters/Ranvier.Counters.fsproj',
  'Fable.Ripple': 'fable/Ranvier.Counters/Ranvier.Counters.fsproj',
};

// Version labels of the build and runtime tools, by label name; `head:` labels describe the measured commit.
const TOOLCHAIN = { 'head:.NET': '.NET', 'Node.js': 'Node.js', 'head:Fable': 'Fable', 'head:fable-library': 'fable-library' };

const plain = text => text.replace(/\u001b\[[0-9;]*m/g, '').trim();
const recorded = value => typeof value === 'string' && plain(value) !== '' && !value.startsWith('unavailable');

// Comparator versions the head side of a record measured, from its `head:<name>` version labels.
function measuredComparators(runs) {
  const found = {};
  for (const run of runs) {
    for (const label of run.Versions || []) {
      const name = typeof label?.Name === 'string' && label.Name.startsWith('head:') ? label.Name.slice(5) : null;
      if (name && Object.hasOwn(COMPARATORS, name) && recorded(label.Value)) found[name] = plain(label.Value);
    }
  }
  return found;
}

// The `Version` of each `PackageReference` in a project file, by package name.
function packageVersions(project) {
  const versions = {};
  for (const [, attributes] of project.matchAll(/<PackageReference\b([^>]*)>/g)) {
    const include = /\bInclude="([^"]+)"/.exec(attributes)?.[1];
    const version = /\bVersion="([^"]+)"/.exec(attributes)?.[1];
    if (include && version) versions[include] = version;
  }
  return versions;
}

// The comparator versions pinned by the counter projects at each of `commits` in the git repository at
// `repository`, by commit, read through one `git cat-file --batch`. A commit maps to `{}` when its projects are absent.
// A floating pin such as `*` or `*-*` declares no version; the record's measured labels carry it.
function declaredComparators(repository, commits) {
  const files = [...new Set(Object.values(COMPARATORS))];
  const requests = commits.flatMap(commit => files.map(file => ({ commit, file })));
  const declared = new Map(commits.map(commit => [commit, {}]));
  if (!requests.length) return declared;
  const output = execFileSync('git', ['-C', repository, 'cat-file', '--batch'], {
    input: requests.map(({ commit, file }) => `${commit}:${file}\n`).join(''),
    maxBuffer: 1 << 28,
  });
  let offset = 0;
  for (const { commit, file } of requests) {
    const end = output.indexOf(10, offset);
    const header = output.toString('utf8', offset, end).split(' ');
    offset = end + 1;
    if (header.length !== 3 || header[1] !== 'blob') continue;
    const size = Number(header[2]);
    const versions = packageVersions(output.toString('utf8', offset, offset + size));
    offset += size + 1;
    for (const [name, project] of Object.entries(COMPARATORS)) {
      if (project === file && /^\d[^*]*$/.test(versions[name] || '')) declared.get(commit)[name] = versions[name];
    }
  }
  return declared;
}

// Each comparator's measured version, or its declared version when the record predates the measured labels.
function comparators(runs, declared) {
  const measured = measuredComparators(runs);
  return Object.keys(COMPARATORS)
    .filter(name => measured[name] || declared[name])
    .map(name => ({ Name: name, Version: measured[name] || declared[name], Source: measured[name] ? 'measured' : 'declared' }));
}

// The build and runtime tool versions of a record's first run that labels them.
function toolchain(runs) {
  const found = new Map();
  for (const run of runs) {
    for (const label of run.Versions || []) {
      const name = TOOLCHAIN[label?.Name];
      if (name && !found.has(name) && recorded(label.Value)) found.set(name, plain(label.Value));
    }
  }
  return Object.values(TOOLCHAIN).filter(name => found.has(name)).map(name => ({ Name: name, Version: found.get(name) }));
}

// Lowest and highest head-process value for each median, keyed by leg, runtime, scenario, engine and metric.
function processRanges(runs) {
  const ranges = new Map();
  for (const run of runs) {
    for (const process of run.Processes || []) {
      if (process.Side !== 'head') continue;
      for (const scenario of process.Scenarios) {
        for (const metric of scenario.Metrics) {
          const id = key(run.Leg, process.Runtime, scenario.Scenario, scenario.Engine, metric.Name);
          const range = ranges.get(id);
          if (range) {
            range.Min = Math.min(range.Min, metric.Value);
            range.Max = Math.max(range.Max, metric.Value);
          } else {
            ranges.set(id, { Min: metric.Value, Max: metric.Value });
          }
        }
      }
    }
  }
  return ranges;
}

// Nonzero head-side library counters per operation from the arm64 counter leg.
function libraryCounters(runs) {
  const run = runs.find(item => item.Leg === 'arm64-counters');
  const counters = [];
  for (const process of run?.Counters || []) {
    if (process.Side !== 'head') continue;
    for (const scenario of process.Scenarios) {
      for (const metric of scenario.Metrics) {
        if (!metric.Name.startsWith('lib:') || metric.Value === 0) continue;
        counters.push({ Runtime: process.Runtime, Scenario: scenario.Scenario, Engine: scenario.Engine, Name: metric.Name.slice(4), Value: metric.Value });
      }
    }
  }
  return counters;
}

// Scoped regressions accepted for a record, with each entry's reasons joined.
function acceptedRegressions(record) {
  if (!Array.isArray(record.AcceptedRegressions)) return [];
  return record.AcceptedRegressions
    .filter(item => item && typeof item.Runtime === 'string' && typeof item.Scenario === 'string' && typeof item.Engine === 'string')
    .map(item => ({
      Leg: item.Leg || 'arm64-counters',
      Runtime: item.Runtime,
      Scenario: item.Scenario,
      Engine: item.Engine,
      Metric: item.Metric || 'instructions',
      Reason: Array.isArray(item.Reasons)
        ? item.Reasons.map(entry => entry?.Reason).filter(Boolean).join('; ')
        : '',
    }));
}

// Each commit restricted to the medians, counters and accepted regressions of scenarios whose names start with one of the prefixes.
// Commits without matching rows remain, so every file shares the full commit axis.
function restrict(commits, prefixes) {
  const matches = row => prefixes.some(prefix => row.Scenario.startsWith(prefix));
  return commits.map(commit => ({
    ...commit,
    Medians: commit.Medians.filter(matches),
    Counters: commit.Counters.filter(matches),
    Accepted: (commit.Accepted || []).filter(matches),
  }));
}

// Writes every commit to the output file and, for each named group of scenario prefixes, the restricted
// commits to `<name>.json` in the same directory. `declared` maps a commit to the comparator versions its
// projects pin; it fills in comparators that a record's runs did not label.
function buildIndex(runsDirectory, outputFile, groups = {}, { declared = () => ({}) } = {}) {
  const files = fs.readdirSync(runsDirectory).filter(file => file.endsWith('.json')).sort();
  if (files.length === 0) throw new Error(`No performance records found in ${runsDirectory}`);

  const seen = new Set();
  const commits = files.map(file => {
    const match = /^([0-9a-f]{40})\.json$/.exec(file);
    assert.ok(match, `Invalid performance record filename: ${file}`);

    const record = JSON.parse(fs.readFileSync(path.join(runsDirectory, file), 'utf8'));
    assert.equal(record.Commit, match[1], `Commit in ${file} does not match its filename`);
    assert.ok(!seen.has(record.Commit), `Duplicate performance record for ${record.Commit}`);
    seen.add(record.Commit);
    assert.ok(!Number.isNaN(Date.parse(record.Date)), `Invalid date in ${file}`);
    assert.equal(typeof record.Subject, 'string', `Missing subject in ${file}`);
    assert.ok(Array.isArray(record.Medians), `Missing median data in ${file}`);

    const runs = Array.isArray(record.Runs) ? record.Runs : [];
    const ranges = processRanges(runs);
    const medians = record.Medians.filter(median => median.Side === 'head').map(median => {
      for (const field of ['Leg', 'Runtime', 'Scenario', 'Engine', 'Metric']) {
        assert.equal(typeof median[field], 'string', `Missing ${field} in ${file}`);
      }
      assert.ok(Number.isFinite(median.Value), `Invalid measurement in ${file}`);
      const { Side, ...row } = median;
      const range = ranges.get(key(median.Leg, median.Runtime, median.Scenario, median.Engine, median.Metric));
      return range ? { ...row, ...range } : row;
    });
    assert.ok(medians.length > 0, `No head measurements in ${file}`);

    return {
      Commit: record.Commit,
      Date: record.Date,
      Subject: record.Subject,
      Packages: Array.isArray(record.Packages) ? record.Packages : [],
      Comparators: comparators(runs, declared(record.Commit)),
      Toolchain: toolchain(runs),
      Medians: medians,
      Counters: libraryCounters(runs),
      Accepted: acceptedRegressions(record),
    };
  });

  commits.sort((left, right) => Date.parse(left.Date) - Date.parse(right.Date));
  const directory = path.dirname(outputFile);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(outputFile, `${JSON.stringify({ commits })}\n`);
  for (const [name, prefixes] of Object.entries(groups)) {
    assert.match(name, /^[a-z0-9-]+$/, `Invalid group name: ${name}`);
    assert.ok(Array.isArray(prefixes) && prefixes.length > 0 && prefixes.every(prefix => typeof prefix === 'string' && prefix),
      `Group ${name} requires scenario prefixes`);
    const file = path.join(directory, `${name}.json`);
    assert.notEqual(path.resolve(file), path.resolve(outputFile), `Group ${name} would overwrite ${outputFile}`);
    fs.writeFileSync(file, `${JSON.stringify({ commits: restrict(commits, prefixes) })}\n`);
  }
}

if (require.main === module) {
  const [, , runsDirectory, outputFile, ...rest] = process.argv;
  const usage = 'Usage: perf-site-data.cjs <runs-directory> <output-file> [--repository <git-directory>]';
  if (!runsDirectory || !outputFile || (rest.length && (rest.length !== 2 || rest[0] !== '--repository'))) throw new Error(usage);
  const groups = JSON.parse(fs.readFileSync(path.join(__dirname, 'perf-groups.json'), 'utf8'));
  const repository = rest[1];
  const commits = fs.readdirSync(runsDirectory).map(file => /^([0-9a-f]{40})\.json$/.exec(file)?.[1]).filter(Boolean);
  const declared = repository ? declaredComparators(repository, commits) : new Map();
  buildIndex(runsDirectory, outputFile, groups, { declared: commit => declared.get(commit) || {} });
}

module.exports = { buildIndex, packageVersions, declaredComparators };
