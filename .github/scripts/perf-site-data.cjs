const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const key = (...parts) => parts.join('\u0000');

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

function buildIndex(runsDirectory, outputFile) {
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
      Medians: medians,
      Counters: libraryCounters(runs),
    };
  });

  commits.sort((left, right) => Date.parse(left.Date) - Date.parse(right.Date));
  fs.mkdirSync(path.dirname(outputFile), { recursive: true });
  fs.writeFileSync(outputFile, `${JSON.stringify({ commits })}\n`);
}

if (require.main === module) {
  const [, , runsDirectory, outputFile] = process.argv;
  if (!runsDirectory || !outputFile) throw new Error('Usage: perf-site-data.cjs <runs-directory> <output-file>');
  buildIndex(runsDirectory, outputFile);
}

module.exports = { buildIndex };
