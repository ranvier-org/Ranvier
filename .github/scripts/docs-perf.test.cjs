const test = require('node:test');
const assert = require('node:assert/strict');
const { hasProvenance, resolve } = require('./docs-perf.cjs');

const repo = { owner: 'owner', repo: 'ranvier' };
const repository = { full_name: 'owner/ranvier' };
const head = 'a'.repeat(40);
const provenance = { repository: repository.full_name, run: '123', head };
const perf = { id: 123, path: '.github/workflows/perf.yml', event: 'push', status: 'completed',
  conclusion: 'failure', head_branch: 'master', repository, head_repository: repository, head_sha: head };
const reporter = { id: 456, path: '.github/workflows/perf-report.yml', event: 'workflow_run',
  conclusion: 'success', repository, head_repository: repository };
function mock(run = perf, artifacts = []) {
  return { rest: { actions: { listWorkflowRunArtifacts: 'artifacts', getWorkflowRun: async options => {
    assert.equal(options.run_id, '123');
    assert.equal(options.owner, repo.owner);
    assert.equal(options.repo, repo.repo);
    return { data: run };
  } } }, paginate: async (method, options) => {
    assert.equal(method, 'artifacts');
    assert.equal(options.run_id, 456);
    return artifacts;
  } };
}

test('PR reporters and successful reporters without publication artifact skip docs', async () => {
  assert.equal(await hasProvenance({ github: mock(), repo, run: reporter }), false);
  assert.equal(await hasProvenance({ github: mock(perf, [{ name: 'perf-report' }]), repo, run: reporter }), false);
  assert.equal(await hasProvenance({ github: mock(perf, [{ name: 'docs-perf-provenance', expired: true }]), repo, run: reporter }), false);
});

test('only successful trusted reporter completions permit the publication artifact', async () => {
  const github = mock(perf, [{ name: 'docs-perf-provenance', expired: false }]);
  assert.equal(await hasProvenance({ github, repo, run: reporter }), true);
  for (const override of [{ conclusion: 'failure' }, { conclusion: 'cancelled' }, { event: 'push' },
    { path: '.github/workflows/other.yml' }, { repository: { full_name: 'attacker/fork' } },
    { head_repository: { full_name: 'attacker/fork' } }]) {
    assert.equal(await hasProvenance({ github, repo, run: { ...reporter, ...override } }), false);
  }
});

test('resolve uses the original measured SHA, including completed regression verdicts', async () => {
  assert.equal(await resolve({ github: mock(), repo, provenance }), head);
});

test('reject malformed and malicious provenance before fetching a run', async () => {
  let calls = 0;
  const github = { rest: { actions: { getWorkflowRun: () => { calls++; throw new Error('unexpected API call'); } } } };
  for (const override of [{ repository: 'attacker/fork' }, { run: '123\nref=master' },
    { run: 123 }, { run: '123\n' }, { head: 'master' }, { head: head + '\n' }]) {
    await assert.rejects(resolve({ github, repo, provenance: { ...provenance, ...override } }));
  }
  assert.equal(calls, 0);
});

test('reject PRs, other branches, repositories, workflows, pending runs and mismatched SHA', async () => {
  for (const override of [{ id: 124 }, { event: 'pull_request' }, { head_branch: 'feature' },
    { status: 'in_progress' }, { path: '.github/workflows/other.yml' }, { head_sha: 'b'.repeat(40) },
    { repository: { full_name: 'attacker/fork' } }, { head_repository: { full_name: 'attacker/fork' } }]) {
    await assert.rejects(resolve({ github: mock({ ...perf, ...override }), repo, provenance }));
  }
});
