const types = ['build', 'bench', 'chore', 'ci', 'docs', 'feat', 'fix', 'perf', 'refactor', 'revert', 'style', 'test'];
const header = new RegExp(`^(${types.join('|')})(\\([a-z0-9][a-z0-9._/-]*\\))?!?: \\S.*$`);

// The problem with a commit subject or pull-request title, or null when it is a conventional commit header.
function headerProblem(subject) {
  const line = (subject ?? '').split(/\r?\n/)[0];
  if (header.test(line)) return null;
  if (!line.trim()) return 'is empty';
  return `"${line}" is not "type(scope): summary" with a type from ${types.join(', ')}`;
}

// Problems in a pull request's title and commits. Merge commits are exempt; their subjects are generated.
function commitProblems({ title, commits }) {
  const problems = [];
  const t = headerProblem(title);
  if (t) problems.push(`Title ${t}`);
  for (const c of commits ?? []) {
    if ((c.parents ?? []).length > 1) continue;
    const p = headerProblem(c.message);
    if (p) problems.push(`Commit ${c.sha.slice(0, 7)} ${p}`);
  }
  return problems;
}

// The problem with a pull request's source branch, or null. master accepts develop from this repository, and
// any branch from the repository owner.
function branchProblem({ base, head, headRepo, repo, association }) {
  if (base !== 'master' || association === 'OWNER') return null;
  if (head === 'develop' && headRepo === repo) return null;
  return `Pull requests into master come from develop; open this one against develop (head is ${headRepo}:${head})`;
}

async function check({ github, context, core }) {
  const pr = context.payload.pull_request;
  if (pr.user.type === 'Bot') {
    core.notice(`${pr.user.login} is a bot; commit messages are not checked.`);
    return;
  }
  const commits = await github.paginate(github.rest.pulls.listCommits, { ...context.repo, pull_number: pr.number, per_page: 100 });
  const problems = commitProblems({
    title: pr.title,
    commits: commits.map(c => ({ sha: c.sha, message: c.commit.message, parents: c.parents })),
  });
  for (const p of problems) core.error(p);
  if (problems.length) core.setFailed(`${problems.length} message(s) are not conventional commits. See CONTRIBUTING.md#commit-messages.`);
}

function policy({ context, core }) {
  const pr = context.payload.pull_request;
  const problem = branchProblem({
    base: pr.base.ref, head: pr.head.ref, headRepo: pr.head.repo?.full_name, repo: `${context.repo.owner}/${context.repo.repo}`,
    association: pr.author_association,
  });
  if (problem) core.setFailed(problem);
}

module.exports = { types, headerProblem, commitProblems, branchProblem, check, policy };
