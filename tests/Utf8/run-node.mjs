import { pathToFileURL } from 'node:url';
import path from 'node:path';

const program = path.resolve(process.argv[2]);
const expectedChecks = Number(process.argv[3] || 61);
await import(pathToFileURL(program));
const result = globalThis.__utf8Proof;
if (!result || result.checks !== expectedChecks) {
  throw new Error(`Incomplete packaged UTF-8 fixture: ${result?.checks ?? 'missing'}/${expectedChecks}`);
}
console.log(JSON.stringify({ ...result, runtime: process.version, pass: true }));
