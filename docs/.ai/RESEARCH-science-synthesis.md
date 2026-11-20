# Scientific computation on Ranvier: synthesis

**Status.** Research synthesis, 2026-11-19, revised the same day after a review of every
ranvier-org repository in this workspace. No code changed. The four research files are untracked
in this checkout. Section 4 describes the platform in layers; no design or implementation plan
exists yet.

**Question.** Is there a sound basis for a platform that delivers scientific and academic
algorithms (statistics, physics, chemistry, mathematics) as fine-grained reactive computations on
Ranvier, pulled on demand, and if so, what shape should it take?

**Scope.** This file joins three research threads and two local investigations:

| Thread | File |
| --- | --- |
| Academic and implementation prior art | `RESEARCH-science-prior-art.md` |
| Market viability | `RESEARCH-science-market.md` |
| Workflows in science and academia | `RESEARCH-science-workflows.md` |
| How the existing science app models computation | section 1 of this file |
| What the ranvier-org ecosystem already provides | section 2 of this file |

**Conventions.** Every figure carries one of three labels: *measured here* (with the command),
*from documentation* (with the file or page), or *general knowledge* (not checked). ⚠️ marks a
claim that was not confirmed in its primary source. Two figures are compared only when both come
from the same benchmark.

**Evaluation rule, set by the owner.** The existence of a competitor, an incumbent tool or an
earlier implementation of any part of the idea is not a reason against it. Other systems appear
in these notes in three roles: as design input, as evidence that demand for a capability exists,
and as context. Section 2 records the evidence behind the rule. Limits that do not depend on
another tool existing (model fit, numeric correctness, missing capabilities, authoring language,
and gaps in demand evidence) are kept and stated plainly.

---

## 1. Local evidence: how AstroShowcase models science today

`AstroShowcase` is the one application in this workspace that computes science on the published
packages: 29 pages of orbit design, flight dynamics and astrophysics, each with a live 3D scene
and charts (*from documentation*: `AstroShowcase/LEARNING-REPORT.md`; the page count matches 29
`Title = ` lines, *measured here* by `grep -c "Title = "` over the four `Pages*.fs` files).

### 1.1 The science is outside the reactive graph

*Measured here*, by `grep -c -E "createMemo|createSignal|createEffect|createAsync|createStream|createProjection|Reactive\.derive"`
over `AstroShowcase/*.fs`:

| File group | Lines using a Ranvier primitive |
| --- | ---: |
| `App.fs` (shell, camera, controls) | 25 |
| `Astro.fs`, `Models.fs`, `Fmm.fs`, `Bodies.fs` (the models) | 0 |
| `Pages.fs`, `PagesAstro.fs`, `PagesFlight.fs`, `PagesOrbit.fs` (the 29 pages) | 0 |

The models are pure functions (`keplerToState`, `rk4`, `integrate`, `bisect`). A page is a record
of functions that each take the page's control values as a positional `float[]`
(`Kit.fs`, `Page`). Units are strings on the control (`Control.Unit`), so no dimension is checked.

### 1.2 Two Ranvier primitives were rewritten by hand

| Hand-written helper | Where | What it is | Ranvier primitive of the same shape |
| --- | --- | --- | --- |
| `Kit.memo` | `Kit.fs:73` | A one-entry cache keyed by the whole control vector. | `createMemo`, with tracked dependencies |
| `Kit.Flow<'s>` | `Kit.fs:157` | A state advanced by the animation clock that restarts when `keyOf p` changes. | `createStreamFold seed fold start`, which restarts when the values read by `start` change |

*Measured here*: 73 lines call `Kit.memo` (`grep -c -E "\bmemo \(|\bmemo\b <\||= memo\b|\(memo "`
over the four `Pages*.fs` files) and 5 `Flow` instances exist (`grep -c -E "Flow(<[^>]*>)?\("`,
same files).

### 1.3 The hand-written memo recomputes work whose inputs did not change

`Kit.memo` compares the whole control vector, so a change to any control invalidates every memo on
the page. In the orbital-decay page:

- `PagesOrbit.fs:606`: `history = memo (fun p -> decay p[0] p[1] (int p[2]))` reads three controls.
- `PagesOrbit.fs:655` and `663-665`: the chart builder runs `decay p[0] bc activity` for 37
  ballistic coefficients at three activity levels, 111 integrations that read only `p[0]`
  (start altitude).
- The same builder draws a marker at `p[1]` (`PagesOrbit.fs:666-667`).

Moving the ballistic-coefficient control therefore re-runs 111 decay integrations with unchanged
inputs. With tracked dependencies the sweep is a node that reads `p[0]` alone, and the marker is a
node that reads `p[1]`.

`Flow` shows the opposite habit. Its `keyOf` argument lists the controls a simulation depends on by
hand, for example `fun p -> [| p[0]; p[1]; p[2]; p[4] |]` at `PagesAstro.fs:754`. That is a
dependency declaration maintained by the author, with no check that it matches what `advance`
reads.

### 1.4 What this says about the design

1. **Demand exists in our own code.** Memoised derived quantities and restartable folds were
   needed on every page, and were built again outside the graph.
2. **The natural grain was the quantity, not the operator.** A node was a trajectory, a decay
   history or a chart scene: tens to thousands of floating-point operations. No page wanted a node
   per arithmetic operation.
3. **Inputs need names, types and units.** Positional `p[0]`, `p[4]` access is where both the
   over-invalidation and the hand-kept key lists come from.
4. **Per-frame work stayed imperative.** `Page.Live` fills a buffer every frame. A design must say
   how a per-frame pull of "the state at time t" meets the graph, since that is where `Flow` was
   used.

### 1.5 The cost of a node, for scale

*From documentation* (`docs/content/benchmarks/memos.md`, BenchmarkDotNet, recorded 2026-09-28 at
commit `d87920f` on an AMD Ryzen 9 9900X, .NET 10.0.12): a cached untracked memo read takes
0.22 ns, a cached tracked read 0.48 ns, and one invalidate-then-read recomputation 15.5 ns with no
allocation. A chain of 64 memos written at the head and read at the tail takes 999 ns, about
15.6 ns per memo. These time the engine's bookkeeping around a trivial body, and the page warns
that absolute times do not transfer between machines.

*General knowledge*: one double-precision multiply or add costs under a nanosecond, and a
transcendental call such as `sin` costs on the order of 10 ns.

Taken together, a node per arithmetic operation would cost an order of magnitude more than the
arithmetic, while a node per formula of a few hundred nanoseconds or more carries overhead of a few
per cent. No benchmark of a science workload on Ranvier nodes has been run; this is an estimate
that a spike must replace with a measurement.

---

## 2. The ecosystem the platform stands on

The first version of this synthesis weighed prior art and incumbent tools against the idea before
this review was done. This section is the review, and it is the basis of the evaluation rule.

### 2.1 Targets reached from one model

| Target | Evidence |
| --- | --- |
| .NET 10, .NET 8, .NET Standard 2.1 | *Measured here*: `TargetFrameworks` in the source projects of twelve repositories. The core is `net10.0;netstandard2.1`; the view, CSS, editor and keyboard packages are `net10.0;net8.0` |
| NativeAOT | *From documentation*: READMEs of `Ranvier.Wgpu`, `Ranvier.Anime`, `Ranvier.Keyboard` and `Ranvier.BAREWire` |
| JavaScript, in the browser and under Node.js | *From documentation*: `Ranvier/README.md`; the `Fable.*` source projects; `fickl/README.md` |
| A native window on five runtime identifiers | *From documentation*: `Ranvier.WebCore/README.md`. WebKit's WebCore and JavaScriptCore behind a C ABI host, packaged for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` and `osx-arm64`, in GPU and CPU flavours |
| Server HTML with hydration | *From documentation*: `Ranvier.View/README.md` |
| Terminal on Windows, Linux and macOS | *From documentation*: `Ranvier.Terminal/README.md` (Windows verified locally; Linux and macOS exercised in CI) |
| GPU, native and in the browser | *From documentation*: `Ranvier.Wgpu/README.md`. `wgpu-native` on six runtime identifiers; WebGPU in the browser with a WebGL2 fallback |
| C# and Elmish-style MVU | *From documentation*: `Ranvier/README.md` |
| An Avalonia host | *From documentation*: `Ranvier.Examples/README.md` |

Around the core sit a typed view layer, typed CSS, charts and geometry (`Ranvier.D5`), animation,
keyboard handling, a text editor as a fold over a log of edits (`Ranvier.Plume`), persistence to
memory, file, browser storage, PostgreSQL and KurrentDB, JSON and BARE binary wire codecs, remote
queries, a hot reload host, graph tracing, an input journal and Rocq proofs of the synchronous
core (*from documentation*: the READMEs of each repository).

### 2.2 Published performance comparisons

Each row is one benchmark; figures in a row are comparable with each other and with nothing else.

**Reactive core on .NET**, retired instructions per operation (*from documentation*:
`docs/.ai/benchmarks/counters/e13f159.md`, 2026-09-30, .NET 10.0.12):

| Scenario | Ranvier | FSharp.Data.Adaptive 1.2.27 | R3 1.3.1 |
| --- | ---: | ---: | ---: |
| Create one root of 1000 rows | 2,239,733 | 12,687,324 | 1,572,257 |
| Write every 10th of 1000 rows | 63,408 | 874,969 | 26,406 |
| Write a chain of 4 memos, read the tail | 2,047 | 8,419 | 267 |
| Write an equal value to an observed source | 51.9 | 736.5 | 43.0 |
| Dispose one root of 1000 rows | 396,898 | 3,625,429 | 512,466 |

FSharp.Data.Adaptive is the nearest earlier implementation of the same mechanism (prior-art
§2.2). It takes between 4.1 and 14.2 times the instructions of Ranvier across the five scenarios
(*calculated here* from the table). The update scenario allocates 0 bytes per operation in Ranvier
and 63,200 in FSharp.Data.Adaptive (same report). R3 is lower than Ranvier in four of the five;
the report notes that R3 pushes each write straight to its subscribers, without batching or
glitch-free ordering. None of the five scenarios is a `foldGroup` aggregate.

**Reactive core compiled to JavaScript**, instructions per operation on the main thread under
Node.js 26.7.0 (same report):

| Scenario | Ranvier | Fable.Ripple 1.0.0-beta.5 |
| --- | ---: | ---: |
| Create one root of 1000 rows | 12,506,746 | 9,884,432 |
| Write every 10th of 1000 rows | 381,078 | 483,225 |
| Write a chain of 4 memos, read the tail | 3,911 | 5,031 |
| Write an equal value to an observed source | 68.9 | 55.9 |
| Dispose one root of 1000 rows | 885,911 | 479,842 |

A later change to the generated JavaScript (#57) has no fresh instruction counts
(*from documentation*: `docs/content/benchmarks/counters.md`).

**View layer in Chromium**, microseconds per operation at 128 rows (*from documentation*:
`docs/research/2026-10-13-owner-lifetime.md`):

| Phase | Ranvier | Solid 2.0.0-rc.14 | Ripple | React |
| --- | ---: | ---: | ---: | ---: |
| Mount | 884 | 353 | 512 | 661 |
| Update | 133 | 122 | 130 | 283 |
| Dispose | 168 | 37 | 59 | 102 |

**An F# language server built on the Ranvier graph**, against the incumbent server on the same
corpora (*from documentation*: `ranvier-fsharp/README.md`, `results/server-final/`):

| Scenario and metric | Incumbent, two configurations | On Ranvier |
| --- | ---: | ---: |
| `mid-body` edit, p50 / p95 ms | 87.5 / 291.1 and 74.9 / 261.0 | 41.3 / 104.5 |
| `mid-references` find references, p50 ms | 65.3 and 103.5 | 7.6 |
| `mid-body` peak working set, MB | 762.7 and 811.8 | 422.8 |
| `mid-openmany` peak working set, MB | 3,120.4 and 706.2 | 441.4 |

### 2.3 The graph already carries heavy non-UI computation

The language server models parsing, dependency extraction, declaration-level type checking and
cross-project signatures as a Ranvier graph. An edit re-checks the affected declarations and
stops when an inferred signature is unchanged (*from documentation*: `ranvier-fsharp/README.md`).
That is a demand-driven incremental computation with an equality cut-off. Its `rfs` tool is
installed and used in this workspace. It is the same use of the graph that a scientific layer
makes.

### 2.4 One science application on two targets

`AstroShowcase.Web` compiles the AstroShowcase sources to JavaScript; the same sources build the
native window (*from documentation*: `AstroShowcase.Web/README.md`). Measured on 2026-11-18 on one
machine at a 165 Hz display (*from documentation*: `AstroShowcase.Web/measure/README.md`):

| Question | Native | Browser |
| --- | --- | --- |
| Frames drawn a second, 28 of 29 pages | 148 to 167 | 163 |
| Frames a second, N-Body Cluster | 160 | 24 |
| 3D draw call on the UI thread | 1.60 ms | 0.18 ms |
| Building the scene, mean over 29 pages | 0.11 ms | 1.43 ms |

Both sides are held to the display rate on 28 pages. The N-Body Cluster page later ran at 143 to
162 frames a second in the browser after a compiler fix (*from documentation*:
`AstroShowcase.Web/README.md`, "Known limits").

### 2.5 What this changes

1. **An earlier implementation of the mechanism is not a ceiling.** The nearest one costs 4 to 14
   times the instructions in the published scenarios (2.2).
2. **Delivery is decoupled from .NET.** A person can use a Ranvier application in a browser, a
   native window or a terminal without installing or writing .NET (2.1, 2.4). Authoring is in F#
   or C#.
3. **The family is the size of an environment.** It has an editor component, an incremental
   compiler graph, a view layer, charts, GPU rendering, a native shell and persistence. The market
   thread found that value in scientific software is captured by environments and distribution
   (market §2), which is what the family is, and what a formula library alone is not.
4. **Replacing an incumbent with a demand-driven graph has been done here once.** Section 2.3 is
   that case.

No surveyed system was found that combines these targets with a reactive core at this measured
cost (prior-art §5.2). The survey was bounded, so that is a statement about what it found.

---

## 3. Findings across the three threads

Bracketed references such as `prior-art §4.3` point into the thread files, which hold the sources.
The thread files were revised under the evaluation rule on 2026-11-19.

### 3.1 Prior art: what to adopt, and what it shows about demand

| Part of the idea | Where it appears | What to take from it |
| --- | --- | --- |
| Each formula is a node, recalculated on change | Excel; Mathcad, which also checks units on recalculation ⚠️ | Forty years of use show the concept is wanted. Mathcad shows units belong in the recalculation |
| A keyed fold with `add`, `subtract` and `zero` | FSharp.Data.Adaptive `ASet.foldGroup`, Jane Street Incremental `sum`, Trill | The algebraic contract: cheap exactly when the aggregate maps into an abelian group. A half-group form with `trySubtract` for min, max and top-k |
| Demand-driven evaluation consistent with a from-scratch run | Adapton | The consistency statement to make, and to extend to aggregates |
| Async values with a pending state | Observable runtime at cell level; Excel async functions | Scientists use pending values when a tool provides them |
| A catalogue of online statistics | OnlineStats.jl, River, Math.NET | The algorithms are known and can be ported; most catalogues add only, and removal is where a keyed aggregate differs |
| Reactive notebooks | marimo, Pluto.jl, Observable | marimo had 2.95M PyPI downloads in a month (market §5). Demand for reactivity in scientific work is measured |

The parts the incremental-computation lineage does not cover are the ones Ranvier already has:
async at formula granularity with cancellation, stream folds, and one model on many targets
(prior-art §5.2).

### 3.2 A node should hold a quantity, an array or a block

- Self-adjusting computation reports a first run about 8 times slower on average, and 25 times
  at worst, than conventional code (*from documentation*: prior-art §1.1, Acar et al. PLDI 2006).
- Jane Street's Incremental fires a node in "on the order of 30ns" on its author's laptop, and
  the author advises using it when the work in a node is large relative to that (*from
  documentation*: the Incremental announcement post, read here).
- Ranvier's own figure is in section 1.5. It comes from a different benchmark on a different
  machine, so it is not compared with the two above.
- A change to one matrix entry becomes a dense delta in later results (prior-art §5.3, LINVIEW).
- An explicit integrator changes every state variable at every step, so the graph should hold
  the parameters of a simulation and treat the run as one node (prior-art §5.3).

The local evidence agrees: section 1.4, points 2 and 4. This is a rule for the design, and it is
what "optimised by default" has to mean: the library chooses the grain.

### 3.3 Floating-point aggregates need a stated policy

An aggregate maintained by subtract-then-add is in general not bitwise equal to one computed from
scratch (prior-art §4.3):

- PostgreSQL requires an inverse transition function to reconstruct the state exactly, and gives
  a moving sum over `1e20` and `1` that returns 0 where 1 is expected (*from documentation*:
  PostgreSQL manual, "User-Defined Aggregates", read here).
- Incremental's `sum_float` recomputes in full every `length ts` changes "to cut down on
  floating-point error" (*from documentation*: `incremental_intf.ml`, read here).
- pandas moved its rolling sums to Kahan summation (prior-art §4.3).

Ranvier today (*measured here*, by reading `src/Ranvier/Combinators.fs:1858-1873` and by
`grep -n -i -E "kahan|neumaier|compensat|drift"` over `src/Ranvier/*.fs`, which matched nothing
relevant): `Projection.sumBy` re-folds from cached values only when a difference is infinite or
NaN. It has no compensated accumulator and no scheduled re-fold. `foldGroup` accepts any state
type, so a compensated accumulator can be passed as the state today; none ships.

The Rocq proofs cover the synchronous core and reach `Core.fs` through a correspondence table and
differential testing (`proofs/README.md`). They say nothing about the arithmetic inside `add` and
`subtract`, so the proved consistency does not extend to a float aggregate.

Only one surveyed system states a consistency requirement for float aggregates (prior-art §5.2).
A stated and tested policy is therefore a place to lead.

### 3.4 Audience: authoring and delivery

| Finding | Figure | Label |
| --- | --- | --- |
| Languages of research software engineers, 2022 | Python 708 (71.01%), JavaScript 246 (24.67%), Julia 65 (6.52%), C# 41 (4.11%), F# 4 (0.40%) | *from documentation*: RSE International Survey 2022, read here |
| Math.NET Numerics, all-time NuGet downloads | 83,939,607 | *measured here*: NuGet search API, 2026-11-19 |
| FSharp.Stats, all-time NuGet downloads | 259,781 | *measured here*: NuGet search API, 2026-11-19 |
| numpy, PyPI downloads in the last month | 1,113,208,204 | *measured here*: pypistats API, 2026-11-19 |
| .NET Interactive repository | archived; last push 2026-04-27 | *measured here*: GitHub API, 2026-11-19 |

These figures describe who writes scientific code in .NET today. They bear on authoring. They do
not bear on delivery, because section 2.5 point 2 holds: the people who use a result need a
browser, a window or a terminal.

Two limits remain and are not about competitors:

- **Authoring is in F# or C#.** A scientist who wants to write a formula, and not only use an
  application, needs an authoring surface. Section 4, layer 4 addresses it.
- **No direct evidence of demand or of willingness to pay was gathered.** The market thread found
  populations and precedents and recommends five to ten interviews (market §7.4). Grant
  programmes found fund established software (market §3).

The state of numerical libraries on .NET is an opening as much as a limit: the main one has had
no stable release since April 2022 (market §4.2), so a maintained scientific layer has room.

### 3.5 Where the model fits

Ranked by the workflow thread on three axes: strength of the pain evidence, fit of the model, and
reach through the targets of 2.1 (workflows §6).

| Rank | Workflow | Pain evidence | Fit | Reach, and what is to build |
| --- | --- | --- | --- | --- |
| 1 | Always-valid monitoring and online estimation | Formal results | Exact: folds over running sums | Library code on every target |
| 2 | Notebook hidden state and live documents | The strongest in the survey | Each cell a memo | Parts exist; a host is to build (section 4, layer 4) |
| 3 | Dashboards over live, revised data | Documented whole-script reruns; measured revisions | Keyed collection with grouped aggregates | Delivery parts exist |
| 4 | Assay and calibration calculations kept in spreadsheets | Measured spreadsheet errors | The collection model unchanged | A grid and run-time units are to build |
| 5 | Teaching demonstrations and explorables | Authoring effort | Signals and memos | Shown: 29 pages on two targets |
| 6 | Uncertainty and sensitivity over a declared formula graph | A systematic review | Strong, with one gap | Scenario evaluation is to build |
| 7 | Conjunction assessment and mission budgets | Handbook-level | Strong | A demonstrator page exists; operations are closed |
| 8 | Living and cumulative meta-analysis | Measured staleness ⚠️ | Exact in shape | Library code; human screening dominates the cost |

On rank 1: the abstract of Johari, Pekelis and Walsh states that fixed-horizon inferences "are
wholly unreliable if users endogenously choose samples sizes by continuously monitoring their
tests", and Howard et al. define a confidence sequence as "a sequence of confidence intervals
that is uniformly valid over an unbounded time horizon" (*from documentation*: arXiv 1512.04922
and 1810.08240, abstracts read here). A pull-based value invites exactly that monitoring, so a
platform that lets any value be read at any time needs statistics that are valid at any time.

On rank 2: of 863,878 attempted executions of notebooks from GitHub, 24.11% ran without errors
and 4.03% produced the same results (*from documentation*: workflows X1, Pimentel et al., read by
that thread).

Limits that stay, none of which rests on another tool:

- Small-N workflows gain consistency, not speed. A meta-analysis has tens of studies.
- In one study 41.65% of notebook failures were missing modules or imports (workflows X1). A
  reactive runtime does not repair an environment.
- The documented spreadsheet errors are type coercion, which typed formulas remove. They are not
  stale results.
- Cells and formulas would be written in F# (3.4).

Poor fits by model (workflows §7, prior-art §5.3): anything finer than one node around an array
or GPU kernel; time-stepped state held inside the graph; coupled implicit equations without a
solver node; pipelines whose steps take hours, until a durable cache exists; MCMC sampling
itself, though its monitoring fits; global fits with shared residuals; computer algebra, which
needs an algebra system; regulated trial monitoring, which needs validation and access control;
and the broker core of survey alert streams. The workflow thread withdrew its single-thread
objection to alert streams after reading the threading contract: running statistics and
downstream filters fit.

### 3.6 Capabilities a scientific layer would need

Checked against `src/Ranvier` where stated. The other rows rest on the guide pages the workflow
thread read (workflows §8) and remain *check* items.

| Capability | State in Ranvier today | Checked how |
| --- | --- | --- |
| Float policy for aggregates | Missing, apart from the infinite/NaN re-fold | Section 3.3 |
| General half-group fold (`trySubtract`) | Not exposed. `tryMinBy` and `tryMaxBy` behave this way: O(1) unless the holder's row is removed or edited, then an O(N) re-fold | *Measured here*: `grep -i "halfgroup\|trySubtract"` matched nothing; `Combinators.fs:2216` |
| Order statistics | Composable as `sortBy` then `tryItem`, at O(N) per order change. No sublinear structure | *Measured here*: `Combinators.fs:1636`; grep for `quantile\|median\|percentile` matched nothing |
| Tolerance comparer for float cut-off | Missing | *Measured here*: grep for `tolerance\|epsilon\|approx` matched nothing |
| Evaluating one graph over many input vectors | No API found | *Measured here*: grep for `fork\|scenario\|whatIf` matched nothing. A graph per scenario was not costed |
| Durable cache of derived values | Missing. `Ranvier.Persistence` restores inputs | *From documentation*: `Ranvier.Persistence/README.md` |
| Windowed or decayed stream folds | Missing for streams. Positional `take`, `skip` and `sub` views exist for projections | *Measured here*: grep for `sliding` matched nothing; `Combinators.fs:1667-1706` |
| Replayable streams | A restart folds from the seed | *From documentation*: `docs/content/guide/streams.md` |
| Run-time units | F# units of measure are erased at compile time | *General knowledge* |
| Coupled or implicit equations | Out of model; a solver must be one node | prior-art §5.3 |
| Lineage in release builds | Tracing is compiled out of untraced builds; the journal records inputs | *From documentation*: `tracing.md`, `journal.md` |
| Derivatives across nodes | Missing | Not checked beyond the API list |
| Append cost through an aggregate | Unsettled. The guide says a key added costs an O(N) key diff (`aggregates.fsx:71`); the doc comment on `foldGroup` says O(Δ) up to `max(64, N)` changes (`Combinators.fs:1774`) | *Measured here*: both read; neither was benchmarked |
| Evaluation on more than one thread | The constructing thread owns a graph. Stream items from another thread are folded on the delivering thread and published once per inbox drain | *From documentation*: `docs/content/guide/threading.md` |

The append row is also a documentation discrepancy between the guide and the source.

An untracked folder, `experiments/parallel-runtime`, appeared in this checkout during the session
and describes a staged study of parallel evaluation. It is another session's work in progress and
was not evaluated here.

### 3.7 What was verified directly

Thirteen claims from the threads were re-read in their primary sources by the coordinating
session: the five rows of the table in 3.4, marimo's announcement of 2025-10-30, JuliaHub's $65M
Series B of 2026-04-30 (company post), the `ASet.foldGroup` and `foldHalfGroup` signatures
(`AdaptiveHashSet.fsi`), Incremental's `sum_float` rule, PostgreSQL's `1e20` example, the 30 ns
figure, and the abstracts of Johari et al. (arXiv 1512.04922) and Howard et al. (arXiv
1810.08240).

One correction came out of it: the Johari abstract says the method "has been implemented in a
large scale commercial A/B testing platform" and gives no year, so the workflow thread's "since
2015" stays ⚠️.

Section 2 was read from this workspace by the coordinating session. Its benchmark figures are the
repositories' own published measurements; none was re-run.

Not re-verified: the remaining claims. The market thread read about 35 of its 63 sources through
search summaries, and the workflow thread about 30 of 69; each file marks them.

---

## 4. The platform in layers

The first version of this synthesis set aside a notebook host and browser-first documents because
other tools occupy that ground. Under the evaluation rule, and with the parts listed in section 2,
both are in scope.

| Layer | What it is | Exists today | To build |
| --- | --- | --- | --- |
| 0. Core numerics | Aggregate and comparison behaviour that scientific values need | `foldGroup`; min and max with a re-fold; stream folds with coalesced publication | A float policy per aggregate; a general half-group fold; a tolerance comparer; windowed and decayed stream folds; a settled append cost (3.6) |
| 1. Quantities and runs | Named, typed inputs with units; constants with their source; derived quantities at the grain of 3.2; a simulation as one restartable node; one graph evaluated over many input vectors | Memos, async memos and stream memos; F# units of measure | The layer itself. It replaces `Kit.memo` and `Kit.Flow` (section 1) |
| 2. Catalogue | Domain packages written on layer 1 | Astrodynamics models in `AstroShowcase/Astro.fs`, as pure functions | Statistics, physics, chemistry and mathematics packages. Array kernels and solvers sit inside one node |
| 3. Delivery | Live pages, documents and dashboards on every target of 2.1 | View, D5, Wgpu, Terminal, WebCore and server HTML; 29 pages on two targets | Science components: unit-aware axes, uncertainty bands, and a visible pending or stale state |
| 4. Authoring | A surface where a scientist writes and re-runs formulas | `Ranvier.Plume`; the language server's declaration-level incremental checking; the hot reload host | The cell and evaluation model, session persistence and packaging |
| Across. Provenance | Where a number came from, and replay | The input journal, tracing and replay | Lineage in release builds; a durable cache of derived values |

Layer 4 answers the authoring limit of 3.4 for people willing to write F#. It does not answer it
for people who are not.

### Three ways to sequence it

**S1. One vertical slice through every layer, then widen.** One example end to end: a stream of
observations folded into moments under a float policy (layer 0), exposed as quantities with units
(layer 1), a confidence sequence over them (layer 2), shown on a live page that builds for the
native window and the browser (layer 3), with its formulas editable in place (a thin layer 4). A
second slice ports one AstroShowcase page for the simulation side.

- **For:** each layer's interface is shaped by a real consumer, and a working artefact exists on
  several targets early.
- **Against:** every layer starts thin, so early interfaces will change.

**S2. Bottom-up.** Finish layers 0 and 1, prove them by porting all 29 pages, then build the
catalogue, then authoring.

- **For:** the foundations are settled first, and they have a user today.
- **Against:** the authoring surface arrives last, and lower layers are designed before the
  layers that use them exist.

**S3. Authoring surface first.** Start with layer 4 and grow the lower layers as cells need them.

- **For:** it addresses the workflow with the strongest pain evidence (notebook hidden state,
  workflows X1) and the authoring limit.
- **Against:** it is the largest build before any scientific content exists, and it forces
  layer 0 and 1 decisions early.

### Recommendation

S1. Its first step is the same under any sequence: one AstroShowcase page on tracked quantities,
with recomputation counts before and after, and a benchmark of node overhead around a real
formula body (section 1.5).

Interviews with teams in the market thread's first three segments (market §7.4) run alongside.
They answer who would pay. They do not decide whether to build.

---

## 5. Open decisions

1. **Sequence**: S1, S2 or S3.
2. **First catalogue domain.** Statistics has the closest fit to the model (3.5). Astrodynamics
   already exists in the workspace as pure functions.
3. **Authoring language.** F# with the incremental checker only, or also a formula notation for
   people who do not program.
4. **Where the core changes land.** The float policy, a general half-group fold and a tolerance
   comparer are core features (`src/Ranvier`), with proofs and benchmarks to keep in step.
5. **Units outside F#.** Compile-time units cover F# callers. C# callers, the JavaScript target
   and formulas entered at run time get no check unless a run-time dimension exists.
6. **How much numerics to own.** Linear algebra, ODE integration and implicit solvers can be
   written, ported or wrapped. The main .NET numerical library has had no stable release since
   April 2022 (market §4.2).
7. **What success means**: adoption, income or a research contribution. It sets how much weight
   the interviews carry.
