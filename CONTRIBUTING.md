# Contributing to Ranvier

## Branches

Branch a change from `master` and open its pull request against `master`. Pull requests are
squash-merged, so each change lands as one conventional commit. `master` rejects direct pushes,
force pushes and deletion; pull requests merge only when the required checks pass. Repository
administrators bypass these rules, and push directly only changes that are not source code, such as
documentation.

GitHub Actions runs on a pull request from a fork only after a maintainer approves the run.

The rules are defined in [`.github/rulesets`](.github/rulesets) and applied with the GitHub CLI:

```shell
gh api -X POST repos/ranvier-org/Ranvier/rulesets --input .github/rulesets/master.json
gh api -X PUT repos/ranvier-org/Ranvier/rulesets/<id> --input .github/rulesets/master.json   # update
```

## Build CLI and pre-commit checks

Every repository task runs through `build.fsx`, a [Partas.Build](https://github.com/shayanhabibi/partas.build) script:

```shell
dotnet fsi build.fsx -- --help
```

| Command | What it does |
|---------|--------------|
| `build` | Restores and builds the source projects |
| `test` | Cleans, checks formatting, and runs .NET tests plus traced signal-map tests (`--skip-tests` skips suites) |
| `test-fable` | Compiles and runs traced and untraced Fable suites under Node.js, writing `docs/.ai/fable-compat.md` |
| `format` | Formats `.fs` source files with Fantomas (`--dry-format` checks without writing) |
| `analyze` | Runs F# analyzers per group (`--group library\|examples`, optional `--build`); `library` also checks traced `Ranvier` |
| `compile` | Compiles Fable projects to JavaScript (`--watch` stays resident) |
| `docs` | Builds the Nacara site from `docs/docs.fsproj` (`--watch` serves it) |
| `perf` | Subcommands `trace`, `ab`, `compare`, and `record` for counter measurements |
| `bump` | Bumps project versions |
| `publish` | Builds, tests, packs, and pushes to NuGet (`--api-key` / `NUGET_API_KEY`; `--dry-publish` validates only) |

Global flags: `--quick` skips restores and cleaning, `--format` formats before building, `--dry-format` checks formatting before building, and `-c` selects configuration (default `Release`).
`build.fsx` discovers `src/` and `tests/` projects automatically via `Partas.TypeProvider.BuildHelper` once added to the solution.

### Git hooks

Enable the tracked pre-commit hook after cloning (requires Git, PowerShell 7, and the .NET SDK):

```powershell
pwsh -NoProfile -File scripts/setup-hooks.ps1
```

It checks staged whitespace, whole staged F# files with the pinned Fantomas version, and `FOR-REVIEW` markers via the comment-hygiene scanner (string literals containing markers are allowed). Checks inspect a temporary copy of index contents without modifying or staging files.

Run the hook checks manually with `pwsh -NoProfile -File scripts/check-staged.ps1`, bypass when needed with `git commit --no-verify`, and test the hook itself in a disposable repository with `pwsh -NoProfile -File scripts/test-hooks.ps1`.

See [Publishing packages](docs/releasing.md) for release automation.

## Examples and playgrounds

Ordinary examples, playgrounds and experimental demos use the current public authoring syntax.
View examples live in [ranvier-org/Ranvier.View](https://github.com/ranvier-org/Ranvier.View), which
documents its authoring conventions.

Explicit builders, manual mounting and low-level operations remain appropriate when demonstrating
those semantics, testing their contracts, or measuring them against another form. State that purpose
in the example or fixture documentation. Preserve compiler diagnostic fixtures and benchmark baselines;
changing their syntax can change the behavior they are intended to test. Runtime implementations under
`experiments/` are not demo code and do not need cosmetic API rewrites.

## Commit messages

Commits follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`,
for example `perf(core): trim Fable output while preserving .NET code`. The type is one of `build`,
`bench`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style` or `test`; the
optional scope is lower case; `!` before the colon marks a breaking change. The **Conventional
commits** check applies this to the pull-request title, which becomes the squash commit's subject.
Pull requests opened by bots are exempt.

CI reads two trailers. Write them in the footer (the last paragraph of the message); `Perf-Ignore`
counts only there, and `Perf-Accepted` on any line of the message:

| Trailer | Effect |
|---------|--------|
| `Perf-Accepted: <names\|all> — <reason>` | Accepts an instruction-count regression in the listed scenarios |
| `Perf-Ignore: <reason>` | Keeps a `master` commit out of the performance history |

When a pull request is squash-merged, a trailer reaches `master` only if it is in the footer of the
squash commit message.

## Publishing

`dotnet fsi build.fsx -- publish` pushes a package only when each of its dependencies resolves on nuget.org, on a
source that [`nuget.config`](nuget.config) maps to its id, or among the packages of the same push ([`feed.cjs`](.github/scripts/feed.cjs)). It holds back the rest, then fails
naming them. `--dry-publish` lists what a push would accept and refuse, and the feed that satisfies each external
dependency.

The view, CSS and persistence packages are built and published from their own repositories:
[Ranvier.View](https://github.com/ranvier-org/Ranvier.View), [Ranvier.Css](https://github.com/ranvier-org/Ranvier.Css)
and [Ranvier.Persistence](https://github.com/ranvier-org/Ranvier.Persistence). Each references the core packages from
NuGet, or this checkout as a sibling directory for changes that span both repositories.

## Checks on a pull request

| Workflow | Job | Fails when |
|----------|-----|------------|
| CI | .NET tests | `Ranvier.Tests` (untraced or traced), `Ranvier.CSharp.Tests`, `Ranvier.Wire.Tests`, `MutationObservation/Shared.fsproj`, or traced `Ranvier.Docs.Maps.Tests` fails, vendored Thoth.Json drifts from its pin plus patches, or package compatibility fails |
| CI | NativeAOT smoke | NativeAOT publish or `Ranvier.AotSmoke` exits non-zero (traced or untraced) |
| CI | Packages | `build.fsx -- publish --dry-publish --skip-tests` fails, or a package differs from expected `.Traced`/untraced id, version, or target frameworks, or DOM consumer checks fail |
| CI | Formatting | `fantomas --check` reports an unformatted `.fs` file |
| CI | Docs menu coverage | `docs/Site.fs` (`check-menus`) finds docs pages missing from a menu |
| CI | Fable tests | traced or untraced Node.js suites, Playwright/Chromium `MutationObservation`, or Fable package consumer checks fail |
| CI | Workflow scripts | a `node --test` suite under `.github/scripts` fails |
| CI | **CI result** | any CI job above failed or was cancelled |
| Perf | verdict | a scenario's .NET or `node-jitless` instruction count regresses on arm64 and the regression is not accepted |
| Perf | **Perf result** | measuring or the verdict failed or was cancelled |
| Contribution | **Conventional commits** | the pull-request title is not a conventional commit header |
| Proofs site | site | Rocq proofs (`proofs/`), `Audit.v` extraction checks, or Navigator site build fails (runs when `proofs/**`, `src/Ranvier/**`, `global.json`, or `proofs-site.yml` changes) |

The branch rules require the three checks in bold. The Contribution check runs the scripts of the
target branch, not those of the pull request.

### Documentation-only changes

A change is documentation only when every changed path is under `docs/` (outside `docs/maps/`, the
signal map model and its tests), `brand/` or `.claude/`, is a Markdown file, a `LICENSE` file, or
`.github/FUNDING.yml` ([`changes.cjs`](.github/scripts/changes.cjs)). CI then runs **Formatting** and
**Docs menu coverage**, skipping build/test/package/script jobs, and Perf measures nothing; the required
checks pass. A change under `docs/maps/` runs every CI job and Perf measures it. A documentation-only
push to `master` gets no performance history record, and the docs are still redeployed for it.

## Performance checks

The Perf workflow builds the pull request's merge base and its head, then runs them alternately,
five processes each, for Node and for .NET, on two legs:

- **`arm64-counters`** counts hardware instructions per operation for three runtimes: .NET
  (`dotnet`), Node under tiered V8 (`node`) and Node under `--jitless` (`node-jitless`).
  - The gate covers `dotnet` and `node-jitless`. A scenario regresses when the head median exceeds
    the base median by more than max(2%, 20 instructions per operation) *and* every head process
    exceeds every base process, and the lower modes of the two sides pass the same test. A side's
    lower mode is its lowest processes, up to the first gap between sorted values wider than that
    limit. When only the medians separate, the scenario is a **state shift**: some processes landed
    in a different discrete state, and the check passes. An unaccepted regression fails the check.
  - Tiered `node` figures are reported and marked *not gated: JIT history-dependent*. V8's
    optimisation decisions depend on which objects earlier runs left alive, so a change that only
    frees memory sooner can move them by large amounts in either direction. They never fail the
    check, warn or change the comment's headline.
  - Under `--jitless` every function runs in the interpreter, and the count follows the work done.
    The harness gives each `node-jitless` case one warm-up run and no settling runs.
- Every worker process also measures `control`, a fixed workload with no Ranvier code. When the
  control regresses or improves by the same rule, the runtime's regressions on that leg are
  **inconclusive** and do not fail the check.
- **`x64-timing`** measures thread CPU time with a 15% threshold for `dotnet` and tiered `node`.
  It has no `node-jitless` runtime, so tiered `node` regressions warn there, and the leg only emits
  warnings.

The results arrive as one pull-request comment, updated on each run, with regressions,
improvements and library-counter changes.

### Accepting a regression

When a regression is intended, accept it with either:

- the `perf-accepted` label, which accepts every scenario; or
- a `Perf-Accepted` trailer in any commit between the merge base and the head:

  ```
  Perf-Accepted: app-detail, keyed-remove — the detail now disposes rows eagerly
  ```

  The value is a comma-separated list of scenario names (as written in the report), or `all`, then
  ` — ` or ` -- ` and the reason. A name matching no scenario fails the check.

Adding or removing the label reuses the previous measurement of the same head and base, so the
verdict updates without measuring again.

### Reusing measurements

A run measures nothing when a completed Perf run of this repository, among the 100 newest, has
measured a base and head with the same trees. The figures are restated for the new commits, with a
`reused` label naming the source run. When a pull request starts at the tip of `master`, the push to
`master` that squash-merges it reuses the pull request's measurement. A pull request behind `master`
is measured again on the push. An accepted regression stays in the report and in the
history, with its reason.

### Performance history

Each push to `master` that changes more than documentation is measured against its parent. The Perf
report workflow then records the run on the `perf-data` branch, and the Docs workflow rebuilds the
site's benchmark charts from it.

A commit that changes nothing measured beyond documentation (workflows, repository tooling) can
stay out of that history with a `Perf-Ignore` trailer and a reason in its footer:

```
ci: cache the .NET SDK

Perf-Ignore: workflow change
```

CI then skips measuring and recording the commit, and still redeploys the docs for it. The next
commit is measured against it as usual. `Perf-Ignore` applies to `master` pushes only: pull requests
are always measured, and `Perf-Accepted` is the way to pass an intended regression. An empty reason
fails the workflow.
