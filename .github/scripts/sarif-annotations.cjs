// GitHub annotations for the results of SARIF reports, with SARIF's 1-based columns. The fsharp-analyzers CLI's own
// `--output-format github` prints 0-based columns, one column left of the cause.
const fs = require('node:fs');
const path = require('node:path');

const levels = { error: 'error', warning: 'warning', note: 'notice', none: 'notice' };

function escapeData(text) {
  return String(text).replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
}

function escapeProperty(text) {
  return escapeData(text).replace(/:/g, '%3A').replace(/,/g, '%2C');
}

// One workflow command per result of the SARIF document.
function annotations(sarif) {
  const lines = [];
  for (const run of sarif.runs ?? []) {
    for (const result of run.results ?? []) {
      const location = result.locations?.[0]?.physicalLocation;
      const region = location?.region ?? {};
      const properties = [
        `file=${escapeProperty(location?.artifactLocation?.uri ?? '')}`,
        `line=${region.startLine ?? 1}`,
        `col=${region.startColumn ?? 1}`,
        `endLine=${region.endLine ?? region.startLine ?? 1}`,
        `endColumn=${region.endColumn ?? region.startColumn ?? 1}`,
        `title=${escapeProperty(result.ruleId ?? '')}`,
      ];
      lines.push(`::${levels[result.level] ?? 'warning'} ${properties.join(',')}::${escapeData(result.message?.text ?? '')}`);
    }
  }
  return lines;
}

module.exports = { annotations };

if (require.main === module) {
  // node sarif-annotations.cjs <directory>: prints the annotations of every .sarif file in the directory.
  const [directory = 'artifacts/analyzers'] = process.argv.slice(2);
  const files = fs.existsSync(directory) ? fs.readdirSync(directory).filter(f => f.endsWith('.sarif')).sort() : [];
  for (const file of files) {
    for (const line of annotations(JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8')))) console.log(line);
  }
}
