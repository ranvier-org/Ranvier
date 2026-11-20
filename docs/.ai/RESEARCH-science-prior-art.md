# Science on a reactive graph: academic and implementation prior art

**Status.** Research survey, 2026-11-19. No code changed. One of three parallel threads of a
feasibility study; this thread covers prior art only. Revised on 2026-11-19 under the owner's
evaluation rule (see Conventions) and with facts verified in the workspace by the coordinating
session; the revision changed judgements and added workspace facts, and removed no source, figure
or label.

**Question.** If scientific and academic algorithms (statistics, physics, chemistry, mathematics)
are packaged as reactive computations on Ranvier's primitives, what already exists, what do the
underlying theories guarantee and cost, and where is fine-grained reactivity the wrong tool?

**Scope.** Five groups, surveyed from primary sources: the papers themselves, official
documentation and source repositories.

| Group | Covered |
| --- | --- |
| Theory | Self-adjusting computation, Adapton, incremental λ-calculus, Build Systems à la Carte, differential dataflow, Naiad, DBSP, Salsa, Jane Street Incremental, FRP and signals |
| Systems for scientists | Shiny, Pluto.jl, Observable runtime, marimo, Vega, Mosaic, Param/Panel, Traits, Wolfram Dynamic, Streamlit, VTK, SCIRun, LabVIEW, Simulink, OpenMDAO, GNU Radio, RDataFrame, Dask, Hamilton, targets, Snakemake, Nextflow, joblib; added during the survey: Excel, Mathcad, ipyflow, Loman, FSharp.Data.Adaptive, Trill, DynamicData, streamz |
| Scientific libraries | NumPy/SciPy, SciML/ModelingToolkit, Modelica, JAX, SymPy, Stan/PyMC/Gen, OnlineStats.jl, River, Math.NET Numerics, FSharp.Stats, DiffSharp, Infer.NET; units and uncertainty: F# units of measure, Pint, Unitful.jl, `uncertainties`, Measurements.jl |
| Algorithms | Online statistics, sliding-window aggregation, Kalman/RLS, sliding DFT, incremental SVD, incremental AD, sequential Monte Carlo, floating-point summation |
| Gap analysis | What no surveyed system does; where per-node reactivity loses |

**Conventions.** A bracketed number such as [14] refers to the source list in §6. Every figure in
this file is *from documentation*: the bracket names the paper or page, and the section where the
thread recorded it. No figure was measured here. A figure derived by arithmetic from two documented
figures says so. ⚠️ **unverified** marks a claim the thread did not confirm in its primary source;
most are general knowledge that the brief asked to be flagged. A claim of the form "X does not do
Y" rests on Y being absent from the pages read, and is weaker than a positive claim. Direct
quotation is avoided; statements are paraphrases of the cited passage. **Design note:** lines say
what a finding implies for the proposed library.

**Evaluation rule (set by the owner).** The existence of a competitor, or of an earlier
implementation of any part of the idea, is not a reason against the idea. Another system appears
in this file in one of three roles: *design input* (what to adopt or avoid), *evidence of demand*
for a capability, or *neutral context*. Reasons that are about fit, and not about who got there
first, stand as written: granularity and model fit (§5.3), floating-point correctness (§4.3),
implicit and coupled equations, run-time units.

**Workspace facts.** A reference of the form [W1] points to a document in this workspace, listed at
the end of §6. Figures cited that way are *from documentation* at the path given. They were
supplied by the coordinating session and were not measured by this thread. This thread opened the
benchmark report [W1], the two cited passages of `Combinators.fs` [W2], the benchmark table of the
language-server README [W3] and the first lines of the browser README [W4], and found them as
reported. It did not check the page count in [W4], the list of targets [W5], the scope of the
Rocq proofs, or the absence of a general `trySubtract` fold. Ranvier's primitives are otherwise
taken from the study brief.

**Reading limits.** Papers behind a paywall were read in an author-hosted copy where one existed.
The TOPLAS 2009 version of the self-adjusting computation experiments, Higham's summation paper,
the sliding DFT article and the ReproBLAS page could not be fetched; claims that depend on them are
marked.

---

## §0. Executive summary

| # | Finding | Evidence | Bearing on the idea |
| --- | --- | --- | --- |
| 1 | A fold over a collection with `add`, `subtract` and `zero` is established prior art, under the same name, on the same platform | `ASet.foldGroup` and `foldHalfGroup` in FSharp.Data.Adaptive [49]; `sum` in Jane Street Incremental [15]; Trill `Accumulate`/`Deaccumulate` [51] | Design input: the same contract appears in three implementations, so adopt it explicitly (§4.2). On the engine benchmark of 2026-09-30, FSharp.Data.Adaptive took about 4 to 14 times Ranvier's retired instructions in five scenarios, none of which is an aggregate [W1] (§2.2) |
| 2 | The theory says exactly when such a fold is cheap: when the aggregate is a homomorphism into an abelian group | ILC `foldBag` over a group [7] §4.3–4.4; DBSP linear operators [12] Thm 3.3, §7.2 | State the group law as the contract of `foldGroup`. MIN, MAX and medians fall outside it |
| 3 | Floating-point subtract-then-add is not exact, and every mature system handles this on purpose | PostgreSQL forbids an inexact inverse [74]; Incremental's `sum_float` recomputes periodically [15]; pandas uses Kahan and Welford in rolling windows [75] | The proved consistency of the core does not carry over to float aggregates. Choose a policy per aggregate (§4.3) |
| 4 | Per-scalar dependency tracking costs roughly one order of magnitude in time on a first run, and more in memory | Self-adjusting computation: about 8× on average, 25× worst [1] §1; per-element matrix product against a blocked one [3] §4.6; about 30 ns per node firing [14] | A node should wrap a vectorised kernel. A node per array element is the wrong granularity |
| 5 | Changes to one matrix entry do not stay small through linear algebra | LINVIEW's avalanche effect [79] | Dense linear algebra gains little from change propagation unless deltas are kept in factored form |
| 6 | Async values with a pending state at formula granularity are rare in the prior art | Observable runtime (cell level) [22]; Excel async UDFs [45]; the incremental-computation lineage is synchronous [1][5][14][17] | This is where the idea differs most from existing libraries |
| 7 | Catalogues of online statistics exist, without a dependency graph | OnlineStats.jl [57], River [58][59], Math.NET [60][61] | Design input and evidence of demand: the algorithms are published and can be adopted. A graph adds composition, removal and a consistency statement |
| 8 | Science needs things a directed formula graph does not give: implicit and coupled equations, derivatives, run-time units, correlated uncertainty | OpenMDAO [35], ModelingToolkit [54], F# units erased at compile time [63], `uncertainties` [66] | Decide early which of these are in scope |
| 9 | The spreadsheet is "each formula a node, recalculated on change", and Mathcad adds units | Excel [45][9]; Mathcad [46] | Evidence of long-standing demand for the concept. Take from it: dirty marking, asynchronous functions, volatile inputs, units checked on recalculation. It has no early cut-off [9] and is an application, not a library |
| 10 | No surveyed library combines formula nodes, pending async, stream folds, keyed incremental aggregates and a scientific catalogue | §5.2; absence of evidence, from two searches and the systems above | The combination offers one graph on which a formula, a pending input, a stream and a keyed aggregate compose, under one consistency statement |
| 11 | The survey found no system that combines a formula-level graph, Ranvier's target reach, and a compiler-class incremental workload on the same graph. Salsa carries the last of these alone [13] | Workspace documents [W3][W4][W5]; §5.6, with its bounded-search caveat | One catalogue could serve .NET, JavaScript, native, server and terminal hosts. The language server shows the graph under a heavy non-UI incremental load |

---

## §1. Theory of incremental and demand-driven computation

### §1.1 Comparison

| Lineage | Model | Guarantee | Costs the authors report | Numeric or scientific workloads |
| --- | --- | --- | --- | --- |
| Self-adjusting computation (Acar et al.) | A run records a dynamic dependence graph of reads of modifiable references, plus memo tables. Change propagation re-runs the affected reads in recorded order and reuses memoised sub-computations. Eager: all outputs are brought up to date | Consistency with a from-scratch run ⚠️ unverified (proved in the earlier papers of the line, not read). Space never exceeds that of a from-scratch run of the self-adjusting program on the current input [1] §4.2 | First run 70% slower at best, 25× at worst, about 8× on average [1] §1. Updates often orders of magnitude faster [1] abstract. Trace size is often proportional to the program's run time [2] abstract | Convex hulls [1]; dot product, motion simulation of 3D convex hulls [2] §6–7; vector and matrix kernels, a ray tracer [3] §4; exact inference in graphical models [18] |
| Adapton | Demand-driven. A write only marks dependants dirty. Re-evaluation waits until an observer forces a thunk. The demanded computation graph is hierarchical, with no global order of events | Patched results match what recomputation from scratch would have produced [5] §1, §4 | Lazy interactions: 7× to 2000× over naive recomputation, against 2× to 20× for eager incremental computation. Batch pattern: about half the eager speed-up at 103–120% of its memory [5] §1, §7 | List programs, expression trees and a spreadsheet (speed-ups up to 20×) [5] §1. Nothing numeric beyond `fold` with sum and min |
| Incremental λ-calculus | Static. `Derive` turns a program `f` into a derivative that maps an input change to an output change. Base types supply change structures | `f (a ⊕ da) = f a ⊕ Derive(f) a da`, Theorem 3.11, machine-checked in Agda [7] | A MapReduce-style histogram improved by orders of magnitude [7] abstract, §4.4. No overhead table | None. The case study is word count |
| Build Systems à la Carte | A store of keys and values, a task per key, and two choices: a scheduler and a rebuilder | Definitions of correctness (3.1) and minimality (2.1), with executable models. It classifies systems; it does not verify one [9] | None measured | None. Excel is one of the four worked systems |
| Differential dataflow, Naiad | Collections are multisets. Changes are stored as differences indexed by a partially ordered version (input epoch, loop iteration). Operators are data-parallel | ⚠️ unverified: the formal treatment is in a separate paper, not read | A one-second change to a 24-hour Twitter mention graph is absorbed in 24.4 ms, against 7.1 s and 36.4 s for the full computations [10] §5.1. Operators keep their full input difference traces indexed in memory [10] §3 | Connected and strongly connected components [10]; logistic regression on 312M records, a tweet stream at 482,988 tweets/s on 32 computers [11] §6.2–6.3 |
| DBSP | Streams over an abelian group, with integration `I` and differentiation `D`. The incremental form of a query `Q` is `D ∘ Q ∘ I`, simplified by algebraic rules | Linear operators equal their own incremental form (Thm 3.3); bilinear ones such as join have a three-term form (Thm 3.4); recursion is correct (Thm 5.4) [12] | Work per step proportional to the size of the change for linear operators. MIN needs the full set on a deletion [12] §7.2. No benchmark figures in the arXiv version read | None in the version read. SUM and COUNT are treated as linear maps |
| Salsa (and attribute grammars) | Inputs carry the revision in which they last changed. A tracked function caches its value and its dependency list, and on demand checks whether any dependency changed. An unchanged recomputed value is backdated, so dependants are not re-run | None stated in the documentation [13] | None in the page read | Compilers and IDEs. Optimal incremental attribute evaluation (Reps, Teitelbaum, Demers 1983) ⚠️ unverified |
| Jane Street Incremental | An OCaml library derived from self-adjusting computation. `stabilize` walks the graph from changed variables in height order. Only nodes with a path to an observer are computed. Each node has a cutoff, physical equality by default | None formal [14][15] | About 30 ns to fire one node on the author's laptop [14]. The post calls the overhead "not inconsiderable" and says the library pays when the work in a node is large relative to it [14] | Portfolio risk over live market data; user interfaces [14]. No published figures for these |
| FRP and signals | Fran evaluates by pull, which the survey criticises for latency and space leaks. FrTime, Flapjax and Scala.React push. Glitches are a known hazard of push [16] §3.2. The TC39 Signals proposal pushes dirtiness and pulls values, with lazy computeds, an equality cut-off and dependencies recorded on each run [17] | None proved | None | None. Async is explicitly out of scope of the TC39 proposal [17] |

### §1.2 Notes that bear on the design

**Granularity is the main cost lever in self-adjusting computation.** The traceable data types
paper states the problem directly: tracing each memory operation slows every operation, keeps a
trace as large as the run, and can make updates asymptotically worse than a purpose-built
structure [2] §1. Replacing fine-grained tracing by tracing at the level of data-type operations
cut from-scratch time by 3–20×, space by 4–50× and update time by 4–5,000× on its benchmarks
[2] §6. One of those benchmarks is a dot product whose sum is kept by an *accumulator* data type
instead of a self-adjusting fold over a list [2] §6. That is the same move as Ranvier's
`foldGroup`: a coarse node with an algebraic update rule in place of a chain of fine nodes.

**The matrix experiment.** The type-directed compiler paper measures matrix multiplication two
ways: every element changeable, and elements grouped into 20 × 20 blocks. The blocked version has a
smaller speed-up, since one changed element recomputes a block, and much less overhead [3] §4.6.
The thread reads the last two rows of Table 1 as an overhead of 8.5 for the per-element version at
n = 400 and 1.2 for the blocked version at n = 1000 (⚠️ the row mapping was reconstructed from
extracted text and should be checked against the PDF). Overheads across the table run from 1.0 to
64.2, and they do not grow with input size [3] §4.5. With 50 × 50 blocks the benchmark holds about
300 MB, about 10 times the space of the two inputs and the result, and gives a 280× speed-up for a
single-element change [3] §4.6. The authors say plainly that memory use can be high. A later paper
from the same group targets that cost with a type system that records dependencies more precisely
and with probabilistic chunking of data [4] (abstract only).

**Demand matters for interactive use.** Adapton's argument is that eager change propagation
updates results nobody is looking at. On its lazy patterns the eager baseline was sometimes slower
than naive recomputation (a 6.5× slowdown on mergesort where Adapton gained 300×) [5] §1. On the
batch pattern, where every output is demanded, eager propagation won by about a factor of two
[5] §7. Ranvier's memos are pull-based, which places it on the Adapton side of this trade.

**The algebra behind `foldGroup`.** ILC builds change structures from abelian groups, and treats
`foldBag` over a group as a group homomorphism from bags to the result type. The derivative of such
a fold uses only the change, not the base input, so its running time need not depend on the input
size; the paper calls this self-maintainable, borrowing the database term [7] §2.1, §4.3–4.4. DBSP
reaches the same place from the database side: SUM and COUNT are linear, so their incremental form
is themselves, while MIN is not and must look at the whole set when a row is deleted [12] §7.2.
A later paper shows that ILC, modified to propagate tangents, is a model of forward-mode automatic
differentiation, with machine-checked proofs [8].

**What "from-scratch consistency" means in each line.** Adapton and ILC state and prove it for a
core calculus [5][7], and Nominal Adapton does the same for its calculus [6] (abstract only). Build Systems à la Carte defines it for a store of keys [9]. DBSP proves
algebraic identities over groups [12]. All of these assume the value domain obeys the algebra.
None of them addresses IEEE floating point, where addition is not associative. §4.3 returns to
this.

**Design note:** among the surveyed lines, Adapton and ILC are the ones that state and prove
consistency for a core calculus. Ranvier's Rocq proofs are of that kind and cover the synchronous
core only (coordinating session's reading of the source). They say nothing yet about async memos,
stream folds or aggregates, and for float aggregates a consistency statement can hold only up to
the summation order, unless the aggregate state is exact.

---

## §2. Reactive and dataflow systems built for scientists

### §2.1 Comparison

| System | Unit of recomputation | Propagation | Staleness test | Async and streaming | Collections, incremental aggregates | What it lacks for the idea |
| --- | --- | --- | --- | --- | --- | --- |
| Shiny [19][20] | Reactive value, reactive expression, output | Invalidation is pushed; expressions re-run lazily when read; observers and outputs are eager | An invalidated flag. Invalidated nodes drop their dependencies and rediscover them on the next run | `invalidateLater`, `reactivePoll`, `reactiveFileReader` | None in the pages read. A data frame is recomputed whole | Incremental collections; equality cut-off ⚠️ unverified |
| Pluto.jl [21] | Notebook cell | A changed cell re-runs the cells that depend on it | Static analysis of each cell's assignments and references. One definition per global | Not covered in the page read | None | Sub-cell granularity; anything outside a notebook |
| Observable runtime [22] | Variable (a cell), with inputs declared by name | A variable is computed only if it, or a transitive output, has an observer | The runtime recomputes dependants of a redefined or resolved variable | A definition may return a promise, and dependants wait for it. A generator is pulled once per animation frame. Observers receive pending, fulfilled and rejected | None | Collections; cancellation of superseded work ⚠️ unverified; a library form outside the notebook runtime |
| marimo [23] | Notebook cell | Running a cell runs the cells that reference its globals; a lazy mode marks them stale instead | Static analysis of definitions and references. Mutation is not tracked | Not covered | None | As Pluto |
| Vega [24] | Dataflow operator; scalar signals; tuples within a dataset | Push, in topological order | Tuple-level change tracking, so only modified tuples propagate | Streaming data and input events are both first-class streams | Tuple-level streaming transforms. Incremental aggregate measures ⚠️ unverified | A general-purpose API: the graph is built from a visualisation specification |
| Mosaic [25] | Client query against DuckDB | A selection change makes the coordinator re-issue queries | Query caching; pre-aggregated tables named by a hash of the query | Queries are asynchronous | Pre-aggregated tables built when the cursor enters a view; interactive over the 1M–1G-row range shown in its benchmark charts | Formula-level nodes; it delegates computation to SQL |
| Param / Panel [26] | Parameter; `rx` expression; bound function | Push on parameter change; `.rx.when` gates recomputation | Change events on parameters | Generators and async ⚠️ unverified (a separate guide exists) | None in the page read | Laziness by default; collections |
| Enthought Traits [27] | Trait attribute; `Property` | Notification is pushed; a `@cached_property` is recomputed the next time it is read | Cache cleared when an observed trait changes | Not covered | None | Collections; async |
| Wolfram `Dynamic` [28][29] | A `Dynamic` expression | The front end re-evaluates an expression when a variable it met during evaluation changes, and only while it is on screen | Dependencies recorded during evaluation. Updates are capped at 20 per second by default | Synchronous by default; asynchronous with a placeholder as an option. `Refresh` with `UpdateInterval` polls | None | It is tied to the notebook front end |
| Streamlit [30] | The whole script | Any widget change re-runs the script from the top | None; `st.cache_data`, forms and fragments limit the work | Not covered | None | A dependency graph |
| VTK pipeline [31] | Filter, over a whole dataset | Demand-driven: `Update()` runs a filter only if its output is out of date with respect to its inputs | Pipeline modification time | Streaming by piece or extent ⚠️ unverified | None | Fine granularity; value-level cut-off |
| SCIRun [32] | Module in a dataflow network | A widget change asks a central scheduler to run the module; the scheduler adds downstream modules and re-sends cached upstream data. Mixes data-driven and demand-driven | Cached datasets on output ports [32] ch. 4 | Computational steering of running simulations | None | As VTK |
| LabVIEW [33] | Block-diagram node | A node runs when all its required inputs have arrived | None found; a diagram runs when the VI runs ⚠️ unverified | Hardware streams | None | Change-driven recomputation |
| Simulink [34] | Block | Time-driven: a block produces output at its sample time | None; no change-driven recomputation in the page read | Continuous and discrete time | None | Change-driven recomputation |
| OpenMDAO [35][36] | Component with declared inputs, outputs, units and partial derivatives; groups form a hierarchy | `run_model()` runs the model; solvers handle coupled groups | None found ⚠️ unverified | None | Array-valued variables. Total derivatives are assembled from component partials [35] §4 | Change-driven recomputation; async; streams |
| GNU Radio [37] | Block over a stream of samples | Push through buffers; one thread per block ⚠️ unverified | None | Sample streams, plus asynchronous message passing | None | Change-driven recomputation |
| ROOT RDataFrame [38] | Column definition, filter, action | Lazy: the first access to any result runs one event loop that fills every booked result | None across runs. A defined column is evaluated at most once per event | None | Aggregating actions over one pass | Incremental update after the loop |
| Dask [39] | Task: a function call on a sizeable piece of data | Graph built lazily, run by a scheduler | None found ⚠️ unverified | Futures | Chunked arrays and frames | Change propagation |
| Hamilton [40] | Function; its parameter names are its dependencies | The driver runs the nodes needed for requested outputs ⚠️ unverified | None in the page read | Not covered | None | Change propagation |
| targets (R) [41] | Target: one saved R object | A target is skipped when it is up to date | Hashes of data files; static analysis of commands and functions | None | None | Sub-second interactive use |
| Snakemake [42] | Job: a rule applied to files | Works backwards from the requested output | Input changed, output missing, or workflow definition changed | None | None | As targets |
| Nextflow [43] | Task | Dataflow between processes | A hash of inputs, script, container and environment, checked on `-resume` | Channels | None | As targets |
| joblib.Memory [44] | Function call | Explicit call | Hash of the arguments; results stored on disk | None | None | A graph |

### §2.2 Systems outside the brief's list that are closer than those in it

**Excel.** Excel builds a dependency tree and a calculation chain, marks every direct and indirect
dependant of a changed cell dirty, and recalculates dirty cells in chain order. It has recalculated
on several threads since Excel 2007. An asynchronous user-defined function lets the rest of the
calculation proceed, and its result triggers a further pass over the cells that depend on it.
Volatile functions recalculate every time [45]. Build Systems à la Carte models Excel as a
restarting scheduler with one dirty bit per cell, with dynamic dependencies, and notes that Excel
has no early cut-off: the dependants of a changed cell are recalculated even when an intermediate
cell's value comes out the same [9] §2.2, §2.3. Each formula is a node, inputs may arrive asynchronously, and the catalogue of
statistical and mathematical functions is large ⚠️ unverified. Role here: evidence of demand for
formula-level recalculation with asynchronous inputs, and design input through its dirty marking
and calculation chain.

**Mathcad.** PTC's help says that changing any number, variable or equation recalculates the
worksheet through to the end, and that units are checked automatically on recalculation [46]
(⚠️ read as search-result excerpts of the help pages, not the full pages). A live worksheet of
formulas with units is the closest end-user product to "scientific formulas as reactive nodes".
Role here: evidence that unit-aware live formulas are wanted, in an application and not a library.

**FSharp.Data.Adaptive.** An F# library of changeable and adaptive values and adaptive sets, lists
and maps, started in 2013 for the Aardvark platform; nothing is recomputed until a result is
demanded [50]. Its `ASet` module has `foldGroup` taking `add`, `subtract` and `zero`,
`foldHalfGroup` whose `trySubtract` may return `None` to force a recomputation, a plain `fold` that
recomputes on every removal, and `sum`, `average`, `count`, `countBy`, `tryMin`, `tryMax` and
`reduce` built on them [49]. The mechanism, the name and the language coincide with Ranvier's,
which makes this the most direct design input for the contract of the fold. The pages read show no
async or pending state, no stream fold, no scientific catalogue, and no statement on
floating-point drift in `sum`.

The workspace holds a measured comparison of the two engines [W1]: retired instructions per
operation on .NET 10.0.12, one benchmark, dated 2026-09-30, for Ranvier, FSharp.Data.Adaptive
1.2.27 and R3 1.3.1.

| Scenario | Ranvier | FSharp.Data.Adaptive | R3 |
| --- | ---: | ---: | ---: |
| Create one root of 1000 rows | 2,239,733 | 12,687,324 | 1,572,257 |
| Update every 10th of 1000 rows | 63,408 | 874,969 | 26,406 |
| Chain of 4 memos: write the source, read the tail | 2,047 | 8,419 | 267 |
| Cut-off: write an equal value to an observed source | 51.9 | 736.5 | 43.0 |
| Dispose one root of 1000 rows | 396,898 | 3,625,429 | 512,466 |

FSharp.Data.Adaptive takes about 4 to 14 times Ranvier's instructions across the five scenarios
(derived: the ratios are 5.7, 13.8, 4.1, 14.2 and 9.1). R3 is lower than Ranvier in four of the
five; the report notes that R3 pushes each write straight to its subscribers without batching or
glitch-free ordering, and that its chain holds no cached value [W1]. The limits of this
comparison: it counts instructions, not time; it ran with tiered compilation off on one machine;
and no scenario is a `foldGroup` aggregate, so it says nothing about the cost of the two libraries'
folds.

**Jane Street Incremental.** `sum` keeps a total with one `sub` and one `add` per changed input.
`sum_float` does the same and recomputes the whole sum every `length ts` changes to limit
floating-point error. `unordered_array_fold` takes either an inverse or an `update` function, and
an optional count of changes after which it recomputes in full [15]. It is used for portfolio risk
[14], which is a numeric workload over live inputs.

**Trill, DynamicData, streamz.** Trill, Microsoft's .NET streaming engine, defines an aggregate by
`InitialState`, `Accumulate`, `Deaccumulate`, `Difference` and `ComputeResult` [51]. DynamicData
lists `Count`, `Max`, `Min`, `Avg` and `StdDev` over observable change sets [52]. In Python, streamz
defines an aggregation by `initial`, `on_new` and `on_old`, and its windowed `Sum` subtracts the
sum of the rows that left the window [53].

**Loman and ipyflow.** Loman is a Python library of named nodes in a DAG, computing on demand only
what a requested node needs, written for quantitative research and for real-time systems whose
inputs update at different rates [48]. ipyflow is a Jupyter kernel that tracks dataflow between
symbols at run time, down to a subscript in some cases, with an opt-in reactive mode [47].

### §2.3 Reading of the table

Four observations follow from §2.1 and §2.2.

1. **Notebook reactivity is cell-level and static.** Pluto and marimo find dependencies by reading
   source text [21][23]. Neither tracks mutation, and neither offers incremental collections.
2. **Scientific dataflow systems are coarse and mostly run-driven.** VTK and SCIRun avoid
   re-running an unchanged filter [31][32]; LabVIEW, Simulink and GNU Radio run on data arrival or
   on a clock [33][34][37]. Their unit is a filter over a whole dataset.
3. **Workflow tools are change-driven but file-grained.** targets, Snakemake, Nextflow and joblib
   decide by hash or timestamp whether to re-run a step that takes seconds to hours [41]–[44].
4. **Incremental aggregates live in streaming and view-maintenance systems, not in scientific
   ones.** FSharp.Data.Adaptive, Incremental, Trill, streamz, differential dataflow and DBSP have
   them [49][15][51][53][10][12]. None of the scientist-facing systems in the brief's list does,
   except Vega at tuple level and Mosaic through SQL pre-aggregation [24][25].

**Design note:** the Observable runtime is the only system in the brief's list whose model includes
pending, fulfilled and rejected states and treats a stream as a first-class cell value [22]. It is
the nearest prior art for Ranvier's async and stream memos, at cell granularity.

---

## §3. Scientific computation libraries

### §3.1 Is any of them reactive or incremental at formula granularity?

| Library | What it is | Reactive or incremental at formula level? | Source |
| --- | --- | --- | --- |
| NumPy / SciPy | Eager array kernels | No ⚠️ unverified. `numpy.sum` uses partial pairwise summation when no axis is given, and precision depends on the axis otherwise | [77] |
| SciML / ModelingToolkit | Symbolic, equation-based modelling; composable transformations such as index reduction and automatic parallelisation; generates numerical code | No run-time change propagation is mentioned. A changed parameter means a new solve ⚠️ unverified | [54] |
| Modelica | Acausal equations compiled to simulation code; time-driven | No ⚠️ unverified | — |
| JAX | Function transformations over traced array programs | No ⚠️ unverified | — |
| SymPy | Symbolic algebra and code generation | No ⚠️ unverified | — |
| Stan / PyMC | Batch Bayesian inference | No ⚠️ unverified | — |
| Gen | Probabilistic programming with programmable inference | **Yes, inside inference.** The static modelling language builds a dependency graph when a model is defined; an `update` skips nodes whose inputs cannot have changed, and caches Julia values in the trace at a memory cost | [55] |
| C3 (lightweight MCMC) | Incremental Metropolis–Hastings by continuation-passing and call-site caching | **Yes.** Proposal run time cut by 20–100× on common models | [56] |
| Adaptive exact inference | Marginals and MAP in graphical models under model changes | **Yes.** Updates in time logarithmic in model size after linear preprocessing | [18] |
| OnlineStats.jl | Single-pass statistics with O(1) memory, updated one observation at a time by `fit!` | Incremental per statistic. No dependency graph. Removal not mentioned ⚠️ unverified | [57] |
| River | Online machine learning and statistics for streams | Incremental per statistic. `stats.Var` uses Welford's algorithm and has `revert`; `utils.Rolling` gives windows | [58][59] |
| Math.NET Numerics | `RunningStatistics`: push and combine; count, min, max, mean, variance, skewness, kurtosis. `MovingStatistics`: a fixed window | Incremental per statistic. `RunningStatistics` has no removal. `MovingStatistics` updates mean and squared deviation from the difference between the entering and leaving value, and rescans the window when the extreme leaves | [60][61] |
| FSharp.Stats, DiffSharp | Statistics; automatic differentiation | Not checked ⚠️ unverified | — |
| Infer.NET | Message-passing inference compiled from a model | Online learning by making each batch's posterior the next prior, or by accumulating a message. Batch-grained | [62] |

The one scientific area where formula-level incrementality is established is probabilistic
inference: Gen, C3 and the adaptive inference work all re-execute only the part of a model that a
proposal or a model edit touches [55][56][18]. Their reported gains are evidence that the approach
pays inside a scientific method, and their dependency analysis is design input.

### §3.2 Units and uncertainty

| Package | Mechanism | Cost or limit | Source |
| --- | --- | --- | --- |
| F# units of measure | Compile-time checking on floating-point, signed integral and decimal types | Units are erased at compilation and absent at run time, so a value cannot report or check its unit dynamically | [63] |
| Pint | Run-time quantity objects | The documentation warns of significant overhead: a subtraction of two quantities takes 8.24 µs against 214 ns on bare magnitudes, about 38× (derived); a root-finder call takes 286 µs against 1.14 µs | [64] |
| Unitful.jl | Units in Julia's type system ⚠️ unverified | States the aim of minimising or eliminating the run-time penalty; no measurement in the README | [65] |
| `uncertainties` | Linear error propagation; derivatives by reverse-mode automatic differentiation; each result remembers the independent variables it depends on, so correlated terms cancel | Valid for small uncertainties and near-linear functions; `NaN` where the derivative is undefined. In the documentation's example, changing a variable's standard deviation changes the uncertainty of an existing result | [66] |
| Measurements.jl | Linear error propagation with functional correlation; arbitrary precision; units through other packages | Quantities related by a correlation matrix cannot yet be defined | [67] |

**Design note:** F# units of measure give Ranvier compile-time safety for formulas written in F#
at no run-time cost, and nothing at all for data whose unit is known only at run time, for C#
callers, or for the JavaScript target. Pint's figures show what a run-time unit object costs per
operation when it wraps a scalar. A unit attached to a *node* is checked once when the graph is
built; a unit attached to each *value* is paid on every operation.

**Design note:** `uncertainties` and Measurements.jl already keep a dependency structure: each
result records its partial derivatives with respect to independent inputs [66][67]. A reactive
graph that propagates a value and its tangents is the same structure made live, and by [8] it is
forward-mode differentiation.

---

## §4. Algorithms that make scientific computation incremental

### §4.1 Catalogue

| Algorithm | State kept | Removal of an observation | Notes | Source |
| --- | --- | --- | --- | --- |
| Welford mean and variance | Count, mean, sum of squared deviations | Possible by reversing the update (River's `revert`); less stable than adding ⚠️ unverified | pandas moved rolling variance to Welford's method with Kahan summation in 1.2.0 | [59][75]; Welford 1962 ⚠️ not read |
| Higher moments and covariance | Centred moments | Pairwise merge formulas; removal not discussed in the abstract | One-pass methods based on sums of powers are fast and can be numerically unstable | [71] |
| P² quantile | Five markers | Not provided | Heuristic; small fixed storage whatever the number of observations | [68] |
| t-digest | Centroids | Not provided ⚠️ unverified | Error relative to max(q, 1−q), so tails are accurate; digests merge without loss of accuracy | [69] |
| Count-Min, HyperLogLog, KLL, DDSketch | Sketch | Linear sketches subtract; max-based ones do not ⚠️ unverified | Not read in primary sources | — |
| Sliding window with an inverse | Running aggregate | Subtract on evict | Exact only if the inverse is exact (§4.3) | [74][53] |
| Sliding window without an inverse | Partial aggregates | FIFO eviction only | DABA gives worst-case O(1) operator calls per step with 2n stored partial aggregates; DABA Lite needs n+2. Requires associativity only. Earlier general algorithms needed O(log n). Two-Stacks: amortised O(1) ⚠️ unverified | [70] |
| Kalman filter, recursive least squares | State estimate and covariance | Forgetting factor in place of removal ⚠️ unverified | A fold over a stream of observations | Kalman 1960 ⚠️ not read |
| Sliding DFT | One complex value per bin | Subtracts the leaving sample | The article examines stability problems of the recursive form; the mechanism (rounding moves poles off the unit circle, fixed by a damping factor) ⚠️ unverified | [73] |
| Incremental SVD / PCA | Thin SVD factors | Not covered in the pages read | Updates as rows or columns arrive; earlier single-vector methods were vulnerable to loss of orthogonality | [72] §1 |
| Incremental AD | Tangents alongside values | — | ILC modified to carry tangents is forward-mode AD. OpenMDAO assembles total derivatives from component partials | [8][35] |
| Sequential Monte Carlo | Weighted particles | — | A stream fold with randomness ⚠️ unverified | — |
| Regression over joins | Ring-valued view payloads | Ring subtraction | F-IVM maintains the gradient of a linear regression under updates and reports orders-of-magnitude gains over first-order maintenance and recomputation | [80] |

Most of this catalogue maps onto two Ranvier primitives. A statistic updated per observation is a
`createStreamFold`. A statistic over a keyed collection with removal is a `foldGroup` when its
state forms a group, and something else when it does not.

### §4.2 The algebraic contract

Three classes of aggregate appear across the sources.

- **Group.** Sum, count, and anything expressed through them: mean, and variance through
  (count, Σx, Σx²) or through Welford state. ILC and DBSP both identify this as the case where an
  update needs only the change [7][12]. F-IVM generalises the state to a ring so that a regression
  gradient is such an aggregate [80].
- **Half-group.** Min, max, top-k. Adding is cheap; removing the current extreme needs the rest of
  the data. FSharp.Data.Adaptive models this with `trySubtract` returning `None` [49]; DBSP
  recomputes from the integrated set [12] §7.2; Math.NET rescans the window [61]; DABA avoids the
  rescan when eviction is first-in-first-out [70].
- **Neither.** Medians and quantiles. Exact maintenance needs an ordered structure over all the
  data; the sketches in §4.1 trade exactness for bounded memory, and most do not support removal.

**Design note:** Ranvier's `foldGroup add subtract zero` covers the first class. Its min and max
are not built on `foldGroup` as a group: `tryMinBy` and `tryMaxBy` cost O(1) per row change unless
the change removes or edits the holder's row, which re-folds the cached ranks in O(N)
(`Combinators.fs:2216` [W2]). That is half-group behaviour, the same that `foldHalfGroup` [49]
and DBSP's treatment of MIN [12] describe. No general `trySubtract` fold is exposed
(coordinating session's reading of the source), so a caller cannot yet define a half-group
aggregate of their own. A quantile remains a different kind of node.

### §4.3 Floating point: is an incrementally maintained result bitwise equal to a from-scratch one?

In general it is not, and the sources that confront the question agree.

1. **The inverse is not exact.** PostgreSQL's documentation gives the example of a moving sum over
   1e20 and 1: with a naive inverse transition, the second window yields 0 instead of 1, because
   1e20 + 1 rounds to 1e20. It requires an inverse transition function to reconstruct the state
   exactly, and lets it return null to force a recomputation from scratch [74].
2. **Drift is bounded by recomputing.** Incremental's `sum_float` recomputes in full every
   `length ts` changes, and `unordered_array_fold` exposes the interval as a parameter [15].
3. **Drift is reduced by compensation.** pandas 1.2.0 changed rolling sum and mean to Kahan
   summation, and rolling variance and standard deviation to Kahan summation with Welford's
   method, citing a series of numerical-problem reports [75]. Kahan 1965 and Neumaier 1974
   ⚠️ not read.
4. **"From scratch" is not one number either.** NumPy sums pairwise when no axis is given and may
   not when one is; its documentation says the precision varies with such parameters [77]. A
   from-scratch reference result exists only relative to a stated summation order. Higham's
   survey of summation methods ⚠️ not read.
5. **Exact accumulation removes the problem at a price.** Python's `math.fsum` avoids loss of
   precision by keeping several partial sums [76]. Neal's superaccumulators compute the exact sum,
   correctly rounded and independent of order, in less than twice the time of naive summation in
   the serial case [78]. If an aggregate's state is an exact accumulator, subtracting is adding
   the negation, the state is the same for any history, and the rounded result equals the
   from-scratch one bit for bit ⚠️ unverified (this is an inference from [78], not a claim the
   paper makes about removal).
6. **Non-finite values break subtraction.** Once an infinity or NaN enters a running sum it cannot
   be subtracted out. Math.NET's `MovingStatistics` carries separate counters for NaN and
   infinities for this reason [61].
7. **Sums of squares cancel.** Variance from Σx² and Σx loses digits when the mean is large
   relative to the spread; this is the instability Pébay's report warns about, and why pandas and
   River use Welford-style state [71][75][59].

**Design note:** each float aggregate needs a declared policy: exact state, compensated state with
a recompute interval, or plain state with a recompute interval. Integer, decimal and rational
aggregates need none. A cut-off on a float memo also deserves a tolerance option; Incremental's
documentation gives exactly that as its example of a custom cutoff [15].

**Design note:** Ranvier's `sumBy` today covers item 6 and not items 2, 3 or 5. Its subtraction
raises when the difference is infinite or NaN, and the sum is then re-folded from the cached
values; while the sum is infinite or NaN, each row change re-adds every cached value in O(N)
(`Combinators.fs:1858-1873` [W2]). There is no compensated accumulator and no scheduled re-fold
(coordinating session's reading of the source), so nothing bounds the rounding error that
accumulates on finite values over a long series of edits.

---

## §5. Gap analysis

### §5.1 Where each part of the idea appears, and what to take from it

Each part of the idea has been built somewhere. Under the evaluation rule that is not a point
against it: the table records where each part appears, as design input and as evidence that the
capability is wanted.

| Part of the idea | Where it appears | What to take from it |
| --- | --- | --- |
| Each formula is a node with declared inputs, recomputed when an input changes | Excel [45]; Mathcad [46]; Jane Street Incremental [14]; FSharp.Data.Adaptive [50]; Adapton's spreadsheet [5]; Loman [48]; Hamilton (without change propagation) [40] | Demand is shown in finance, spreadsheets and dataflow tools. Hamilton's and OpenMDAO's declarations show how a formula can name its inputs |
| Declared inputs, outputs, units and derivatives per scientific component | OpenMDAO [35][36] | The shape of a component declaration, and total derivatives assembled from partials |
| Lazy, pull-based evaluation with early cut-off | Adapton [5]; Salsa [13]; TC39 Signals [17]; Traits cached properties [27]; VTK at filter level [31] | Adapton's measurements of when demand-driven evaluation wins and loses [5] §7; Salsa's durability levels [13] |
| Inputs that arrive asynchronously, with a pending state | Observable runtime [22]; Excel async UDFs [45]; Wolfram asynchronous `Dynamic` [29] | Evidence that users want a pending state. Observable's three observer states are the model to compare against |
| Inputs that are streams folded into a value | Observable generators [22]; Vega [24]; streamz [53]; OnlineStats and River per statistic [57][58] | Vega's tuple-level change sets; the per-observation update rules of the online-statistics libraries |
| Keyed collections with O(1) incremental aggregates by add and subtract | FSharp.Data.Adaptive `foldGroup` [49]; Incremental `sum` [15]; Trill [51]; streamz [53]; DBSP and differential dataflow [12][10] | The group and half-group contracts (§4.2); Incremental's recompute interval for floats [15] |
| A catalogue of online scientific algorithms | OnlineStats.jl [57]; River [58]; Math.NET running and moving statistics [60][61] | The algorithms themselves (§4.1); River's `revert` as the pattern for removal [59] |
| Incremental re-execution inside a scientific method | Gen, C3, adaptive exact inference [55][56][18]; F-IVM for regression [80] | Evidence that formula-level incrementality pays in a scientific method; ring-valued aggregate state [80] |
| Units checked on live recalculation | Mathcad [46] | Evidence of demand; check units when the graph changes, not on every operation (§3.2) |
| Proved consistency of the core | Adapton (proof in the paper) [5]; ILC (machine-checked in Agda) [7] | The form of the theorem: the incremental result equals the from-scratch result. Ranvier's Rocq proofs cover the synchronous core only (§1.2) |

### §5.2 What this thread did not find

No surveyed system offers all of the following in one general-purpose library: formula-level nodes
with declared inputs and constants; async inputs with an explicit pending state and cancellation
of superseded work; stream folds; keyed collections with group-based incremental aggregates; a
catalogue of scientific algorithms written against those primitives; and a stated policy for
floating-point consistency. Two web searches for such a library returned uncertainty and unit
packages and general incremental-computation libraries, and nothing that combines them. This is
absence of evidence from a bounded search. In-house systems at trading firms and engineering
companies are the likeliest place for a counter-example, and they do not publish.

The narrower gaps are:

- **Async at formula granularity.** Self-adjusting computation, Adapton, Incremental,
  FSharp.Data.Adaptive and the TC39 proposal are synchronous in the sources read
  [1][5][14][50][17]. The Observable runtime has the states but is a notebook runtime [22].
- **Removal in online-statistics catalogues.** OnlineStats' README and Math.NET's
  `RunningStatistics` add only [57][60]. River's `revert` is the exception found [59].
- **A consistency statement for float aggregates.** Only PostgreSQL states a requirement [74].
  Incremental and pandas document mitigations without a guarantee [15][75].
- **Scientific catalogue on .NET with a reactive graph.** Math.NET has the statistics without the
  graph [60][61]; FSharp.Data.Adaptive has the graph without the statistics [49].

### §5.3 Where fine-grained reactivity is the wrong tool

**Dense linear algebra and vectorised kernels.** The evidence points one way.

- Per-element tracking of a matrix product costs several times the conventional run and about ten
  times the memory of the operands even after blocking; coarser blocks reduce the overhead and
  the speed-up together [3] §4.6.
- The authors of the fine-grained approach list its costs themselves: a slowdown on every memory
  operation, a trace as large as the run, and constant factors they call moderately large [2] §1.
- Incremental's author puts node firing at about 30 ns and advises using the library when the work
  in a node is large relative to that [14]. A floating-point add is a fraction of a nanosecond
  ⚠️ unverified, so a node per scalar operation is dominated by overhead.
- LINVIEW shows that the problem is not only overhead. A small change to an input matrix spreads
  through intermediate results until the delta is as dense as the result, which the paper calls
  an avalanche effect; its remedy is to keep deltas in factored form [79]. A dependency graph over
  matrix elements would find that everything depends on the changed entry.
- Pint's figures show the same effect for a different per-value wrapper: about 38× on a scalar
  subtraction [64].

**Time-stepped simulation.** Change propagation pays when the computation is stable under the
change, a point the self-adjusting computation papers make about traces [2] §1. An explicit
integrator changes every state variable at every step, so there is nothing to reuse within a step.
Simulink and Modelica tools schedule by time, not by change [34]. A reactive graph can hold the
*parameters* and re-run a simulation node when one changes; it should not hold the state vector.

**Coupled and implicit equations.** A signal graph is acyclic and causal: each node is an
assignment. OpenMDAO exists because engineering models contain implicit components and coupled
groups that need a nonlinear solver [35]; ModelingToolkit and Modelica exist because physical
models are written as equations and need index reduction before they can be solved [54]. A formula
library on a reactive DAG handles explicit formulas only, unless a solver is wrapped inside one
node.

**Whole-input changes.** When every row changes at once (a new dataset, a rescaled column), a
subtract-then-add fold does twice the work of a fresh sum. Adapton measured the same effect for
demand-driven propagation on its batch pattern [5] §7.

**Non-invertible aggregates under removal.** See §4.2. A reactive wrapper does not make a median
incremental.

**Very small updates at very high rates.** Differential dataflow's authors note that per-iteration
overheads show when the amount of work per update is tiny [10] §5.1. Wolfram's front end caps
updates at 20 per second by default for a related reason [29].

### §5.4 Where it fits

- **Many inputs, few change at a time, some nodes expensive.** Risk over live market data [14];
  spreadsheets [5][45]; exact inference under model edits with logarithmic-time updates [18];
  MCMC proposals at 20–100× [56].
- **Interactive exploration where only what is visible should be computed.** Adapton's lazy
  patterns [5]; Wolfram's on-screen rule [29]; the Observable runtime's observer rule [22].
- **Keyed collections with small deltas and group aggregates.** ILC [7], DBSP [12], F-IVM [80],
  and the libraries in §2.2.
- **Per-observation statistics on a stream.** §4.1; each is a fold with constant state.
- **Chains of cheap scalar formulas, when the reader is a person.** A unit conversion feeding a
  rate law feeding a plot has a handful of nodes; per-node overhead is irrelevant at that size,
  and the value is in the bookkeeping. This is the Mathcad case [46].

### §5.5 Design implications

1. **Granularity.** Make the array, the column or the block the value of a node, and call a
   vectorised kernel inside it. Offer per-row nodes only through keyed collections, where the
   aggregate is algebraic. [2][3][14][79]
2. **Algebraic contracts for aggregates.** Document `foldGroup` as requiring an abelian group.
   `tryMinBy` and `tryMaxBy` already re-fold when the holder leaves [W2]; decide whether to expose
   that as a general half-group fold with a `trySubtract`, and treat order statistics separately.
   [7][12][49][70]
3. **Float policy.** Decide per aggregate between exact state, compensation and periodic
   recomputation, and say what "equal to from scratch" means. `sumBy` re-folds today only on an
   infinite or NaN difference [W2]. [74][15][75][77][78]
4. **Lead with async and streams.** These are the primitives the nearest libraries lack, and the
   Observable runtime shows scientists use them when a notebook provides them. [22]
5. **Scope the science.** Explicit formulas, online statistics and group aggregates fit. Implicit
   systems, ODE integration and dense factorisations belong inside a node, behind an existing
   numerical library. [35][54]
6. **Units and uncertainty at node level.** Check units when the graph is built; propagate
   tangents for uncertainty as `uncertainties` does, and note that this is forward-mode AD.
   [63][64][66][8]
7. **Credit the sources of each contract.** The fold contract is shared with FSharp.Data.Adaptive
   and Incremental, and the recalculation model with Excel; saying so, with the measured engine
   comparison where one exists [W1], gives a reader the context. [49][15][45]

### §5.6 Target reach, and a precedent on the same graph

Three facts from the workspace bear on how the prior art should be read. They are *from
documentation* at the paths listed at the end of §6, and were not measured by this thread.

- **Reach.** Ranvier targets .NET 10, .NET 8 and .NET Standard 2.1, with packages compatible with
  NativeAOT; JavaScript through an F#-to-JavaScript compiler, in the browser and on Node.js; a
  native window host built on WebKit's WebCore and JavaScriptCore for win-x64, win-arm64,
  linux-x64, linux-arm64 and osx-arm64; server HTML with hydration; a terminal UI on Windows,
  Linux and macOS; and WebGPU natively and in the browser, with a WebGL2 fallback [W5].
- **One science application on two hosts.** An application of 29 pages with live 3D and charts
  builds from the same sources for the native window and for the browser [W4].
- **A heavy non-UI computation on the graph.** A language server for F# is built on the same
  reactive graph, with declaration-level incremental checking and an equality cut-off when a
  signature is unchanged. Its README reports, against two configurations of the incumbent server
  on the same corpora: edit latency p50/p95 of 41.3/104.5 ms against 87.5/291.1 and 74.9/261.0;
  find-references p50 of 7.6 ms against 65.3 and 103.5; and a peak working set of 422.8 MB against
  762.7 and 811.8 [W3].

What this implies for positioning, stated as what the survey found:

1. **Reach.** As far as the pages read show, each system in §2 is tied to one language runtime or
   one host application: R for Shiny [19], Julia for Pluto [21], Python for marimo, Param, OpenMDAO
   and Loman [23][26][35][48], JavaScript for the Observable runtime and Vega [22][24], OCaml for
   Incremental [14], a desktop application for Excel and Mathcad [45][46], and .NET for Trill and
   FSharp.Data.Adaptive, whose README also shows a Fable demonstration [51][50]. Hosts beyond
   those are ⚠️ unverified. The survey did not find one that offers a formula-level graph on .NET,
   JavaScript, a native window, a server and a terminal from one set of sources. The caveat is the same as in
   §5.2, and stronger: target platforms were not a question this survey put to each system, and
   most rows of §2.1 rest on one documentation page.
2. **A compiler-class workload on a general graph.** Salsa is the surveyed system built for
   incremental compilation and IDE queries [13]; it is not a general reactive library and was not
   found to serve user interfaces or numeric aggregates. The language server on Ranvier is the
   same class of workload, declaration-level checking with early cut-off, on the graph library
   that also provides the UI bindings and the keyed aggregates. That is evidence that the graph
   supports an expensive incremental computation that is not a user interface, which is the
   regime §5.4 names as the one where change propagation pays.
3. **What this does not change.** The language-server figures measure editing and queries on
   source code, not a numeric kernel. The granularity and linear-algebra limits of §5.3, the
   floating-point limits of §4.3 and the scope of the proofs (§1.2) stand as written.

### §5.7 Limits of this survey

- The overhead figures for self-adjusting computation come from SML and OCaml implementations
  between 2006 and 2014. They indicate an order of magnitude, not what Ranvier costs on .NET.
- The workspace benchmark gives Ranvier's own cost per operation in retired instructions, 2,047
  for a write through a chain of four memos and 51.9 for a cut-off [W1]. It has no numeric kernel
  and no aggregate scenario, so the granularity argument of §5.3 still rests on the papers.
- No benchmark compares a signals library with an array library on a numeric kernel. The case
  against per-element nodes is assembled from [2], [3], [14], [64] and [79].
- Several rows in §2.1 rest on one documentation page per system.
- Feldera, Materialize, Pathway, Skip and other current incremental engines were not read; DBSP
  and differential dataflow stand in for them.

---

## §6. Sources

Read in full or in the sections cited, unless noted.

**Theory**

1. Acar, Blelloch, Blume, Tangwongsan. *An Experimental Analysis of Self-Adjusting Computation.* PLDI 2006. https://www.cs.cmu.edu/~guyb/papers/ABBT06.pdf (the TOPLAS 2009 version, doi:10.1145/1596527.1596530, was not accessible)
2. Acar, Blelloch, Ley-Wild, Tangwongsan, Türkoğlu. *Traceable Data Types for Self-Adjusting Computation.* PLDI 2010. https://condor.depaul.edu/dturkogl/research/traceable-pldi10.pdf
3. Chen, Dunfield, Acar. *Type-Directed Automatic Incrementalization.* PLDI 2012. https://static.aminer.org/pdf/20170130/pdfs/pldi/icdmzl0fo9dlsxnw6aqvfubya83zgvhp.pdf
4. Chen, Acar, Tangwongsan. *Functional Programming for Dynamic and Large Data with Self-Adjusting Computation.* ICFP 2014. Abstract only: https://murex.mahidol.ac.th/en/publications/functional-programming-for-dynamic-and-large-data-with-self-adjus/
5. Hammer, Phang, Hicks, Foster. *Adapton: Composable, Demand-Driven Incremental Computation.* PLDI 2014. https://www.cs.tufts.edu/~jfoster/papers/pldi14.pdf
6. Hammer et al. *Incremental Computation with Names.* OOPSLA 2015. Abstract only: https://arxiv.org/abs/1503.07792
7. Cai, Giarrusso, Rendel, Ostermann. *A Theory of Changes for Higher-Order Languages: Incrementalizing λ-Calculi by Static Differentiation.* PLDI 2014. https://arxiv.org/abs/1312.0658
8. Kelly, Pearlmutter, Siskind. *Evolving the Incremental λ Calculus into a Model of Forward Automatic Differentiation.* 2016. Abstract only: https://arxiv.org/abs/1611.03429
9. Mokhov, Mitchell, Peyton Jones. *Build Systems à la Carte.* ICFP 2018. https://www.microsoft.com/en-us/research/uploads/prod/2018/03/build-systems.pdf
10. McSherry, Murray, Isaacs, Isard. *Differential Dataflow.* CIDR 2013. https://www.cidrdb.org/cidr2013/Papers/CIDR13_Paper111.pdf
11. Murray, McSherry, Isaacs, Isard, Barham, Abadi. *Naiad: A Timely Dataflow System.* SOSP 2013. https://sigops.org/s/conferences/sosp/2013/papers/p439-murray.pdf
12. Budiu, McSherry, Ryzhyk, Tannen et al. *DBSP: Automatic Incremental View Maintenance for Rich Query Languages.* 2022. https://arxiv.org/abs/2203.16684
13. Salsa book, *The red-green algorithm.* https://salsa-rs.github.io/salsa/reference/algorithm.html
14. Minsky. *Introducing Incremental.* Jane Street blog. https://blog.janestreet.com/introducing-incremental/
15. Jane Street Incremental, `incremental_intf.ml`. https://github.com/janestreet/incremental/blob/master/src/incremental_intf.ml
16. Bainomugisha, Lombide Carreton, Van Cutsem, Mostinckx, De Meuter. *A Survey on Reactive Programming.* ACM Computing Surveys, 2013 (technical-report copy). http://soft.vub.ac.be/Publications/2012/vub-soft-tr-12-13.pdf
17. TC39 Signals proposal, README. https://github.com/tc39/proposal-signals
18. Sümer, Acar, Ihler, Mettu. *Adaptive Exact Inference in Graphical Models.* JMLR 12, 2011. Abstract only: https://jmlr.org/papers/v12/sumer11a.html

**Systems**

19. Wickham. *Mastering Shiny*, "The reactive graph". https://mastering-shiny.org/reactive-graph.html
20. Wickham. *Mastering Shiny*, "Reactive building blocks". https://mastering-shiny.org/reactivity-objects.html
21. Pluto.jl documentation, "Reactivity". https://plutojl.org/en/docs/reactivity/
22. Observable runtime, README. https://github.com/observablehq/runtime
23. marimo documentation, "Reactivity". https://docs.marimo.io/guides/reactivity/
24. Satyanarayan, Russell, Hoffswell, Heer. *Reactive Vega: A Streaming Dataflow Architecture for Declarative Interactive Visualization.* InfoVis 2015. https://idl.cs.washington.edu/files/2015-ReactiveVega-InfoVis.pdf
25. Mosaic documentation, "Why Mosaic?". https://idl.uw.edu/mosaic/why-mosaic/
26. Param user guide, "Reactive Functions and Expressions". https://param.holoviz.org/user_guide/Reactive_Expressions.html
27. Traits user manual, "Advanced Topics". https://docs.enthought.com/traits/traits_user_manual/advanced.html
28. Wolfram Language tutorial, "Introduction to Dynamic". https://reference.wolfram.com/language/tutorial/IntroductionToDynamic.html
29. Wolfram Language tutorial, "Advanced Dynamic Functionality". https://reference.wolfram.com/language/tutorial/AdvancedDynamicFunctionality.html
30. Streamlit documentation, "Basic concepts". https://docs.streamlit.io/get-started/fundamentals/main-concepts
31. VTK class reference, `vtkDemandDrivenPipeline`. https://vtk.org/doc/nightly/html/classvtkDemandDrivenPipeline.html
32. Parker. *The SCIRun Problem Solving Environment and Computational Steering Software System.* PhD dissertation, University of Utah, 1999, ch. 4. https://users.cs.utah.edu/~sparker/publications/thesis.pdf
33. NI LabVIEW documentation, "Block Diagram Data Flow". https://www.ni.com/docs/en-US/bundle/labview/page/block-diagram-data-flow.html (the page body did not load; read as search-result excerpts)
34. MathWorks, "What Is Sample Time?". https://www.mathworks.com/help/simulink/ug/what-is-sample-time.html
35. Gray, Hwang, Martins, Moore, Naylor. *OpenMDAO: an open-source framework for multidisciplinary design, analysis, and optimization.* Structural and Multidisciplinary Optimization, 2019. https://openmdao.org/pubs/openmdao_overview_2019.pdf
36. OpenMDAO documentation, "Paraboloid: a single-discipline model". https://openmdao.org/newdocs/versions/latest/basic_user_guide/single_disciplinary_optimization/first_analysis.html
37. GNU Radio wiki, "Message Passing". https://wiki.gnuradio.org/index.php/Message_Passing (direct fetch refused; read as search-result excerpts)
38. ROOT reference, `ROOT::RDataFrame`. https://root.cern/doc/master/classROOT_1_1RDataFrame.html
39. Dask documentation, "Task Graphs". https://docs.dask.org/en/stable/graphs.html
40. Hamilton documentation, "Node". https://hamilton.dagworks.io/en/latest/concepts/node/
41. Landau. *The targets R package user manual*, "A walkthrough to get started". https://books.ropensci.org/targets/targets.html
42. Snakemake tutorial, "Basics". https://snakemake.readthedocs.io/en/stable/tutorial/basics.html
43. Nextflow documentation, "Caching and resuming". https://docs.seqera.io/nextflow/cache-and-resume
44. joblib documentation, `joblib.Memory`. https://joblib.readthedocs.io/en/stable/generated/joblib.Memory.html
45. Microsoft, "Excel Recalculation". https://learn.microsoft.com/en-us/office/client-developer/excel/excel-recalculation
46. PTC Mathcad Prime help, "About Using PTC Mathcad Prime" and "About Units". https://support.ptc.com/help/mathcad/r8.0/en/PTC_Mathcad_Help/about_using_mathcad.html and https://help.ptc.com/mathcad/en/PTC_Mathcad_Help/about_units.html (read as search-result excerpts)
47. ipyflow, README. https://github.com/ipyflow/ipyflow
48. Loman, README. https://github.com/janushendersonassetallocation/loman
49. FSharp.Data.Adaptive, `AdaptiveHashSet.fsi`. https://github.com/fsprojects/FSharp.Data.Adaptive/blob/master/src/FSharp.Data.Adaptive/AdaptiveHashSet/AdaptiveHashSet.fsi
50. FSharp.Data.Adaptive, README. https://github.com/fsprojects/FSharp.Data.Adaptive
51. Trill, `IAggregate.cs`. https://github.com/microsoft/Trill/blob/master/Sources/Core/Microsoft.StreamProcessing/Aggregates/IAggregate.cs
52. DynamicData, README. https://github.com/reactivemarbles/DynamicData
53. streamz, `dataframe/aggregations.py`. https://github.com/python-streamz/streamz/blob/master/streamz/dataframe/aggregations.py

**Scientific libraries, units and uncertainty**

54. Ma et al. *ModelingToolkit: A Composable Graph Transformation System for Equation-Based Modeling.* 2021. Abstract only: https://arxiv.org/abs/2103.05244
55. Gen documentation, "Static Modeling Language". https://www.gen.dev/docs/stable/ref/modeling/sml/
56. Ritchie, Stuhlmüller, Goodman. *C3: Lightweight Incrementalized MCMC for Probabilistic Programs using Continuations and Callsite Caching.* 2015. Abstract only: https://arxiv.org/abs/1509.02151
57. OnlineStats.jl, README. https://github.com/joshday/OnlineStats.jl
58. Montiel et al. *River: machine learning for streaming data in Python.* 2020. Abstract only: https://arxiv.org/abs/2012.04740
59. River API reference, `stats.Var`. https://riverml.xyz/latest/api/stats/Var/
60. Math.NET Numerics, `RunningStatistics.cs`. https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/Statistics/RunningStatistics.cs
61. Math.NET Numerics, `MovingStatistics.cs`. https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/Statistics/MovingStatistics.cs
62. Infer.NET user guide, "Online learning". https://dotnet.github.io/infer/userguide/Online%20learning.html
63. F# language reference, "Units of Measure". https://learn.microsoft.com/dotnet/fsharp/language-reference/units-of-measure (read from the source file in the dotnet/docs repository)
64. Pint documentation, "Optimizing Performance". https://pint.readthedocs.io/en/stable/advanced/performance.html
65. Unitful.jl, README. https://github.com/PainterQubits/Unitful.jl
66. `uncertainties` documentation, "Technical Guide". https://uncertainties.readthedocs.io/en/latest/tech_guide.html
67. Measurements.jl, README. https://github.com/JuliaPhysics/Measurements.jl

**Algorithms and numerics**

68. Jain, Chlamtac. *The P² Algorithm for Dynamic Calculation of Quantiles and Histograms Without Storing Observations.* Communications of the ACM, 1985. https://www.cse.wustl.edu/~jain/papers/ftp/psqr.pdf
69. Dunning, Ertl. *Computing Extremely Accurate Quantiles Using t-Digests.* 2019. Abstract only: https://arxiv.org/abs/1902.04023
70. Tangwongsan, Hirzel, Schneider. *In-Order Sliding-Window Aggregation in Worst-Case Constant Time.* 2020. Abstract only: https://arxiv.org/abs/2009.13768
71. Pébay. *Formulas for Robust, One-Pass Parallel Computation of Covariances and Arbitrary-Order Statistical Moments.* Sandia report SAND2008-6212, 2008. Abstract only: https://www.osti.gov/biblio/1028931
72. Brand. *Incremental Singular Value Decomposition of Uncertain Data with Missing Values.* ECCV 2002. https://www.merl.com/publications/docs/TR2002-24.pdf
73. Jacobsen, Lyons. *The Sliding DFT.* IEEE Signal Processing Magazine 20(2), 2003. doi:10.1109/MSP.2003.1184347 (abstract seen through an index; article not read)
74. PostgreSQL documentation, "User-Defined Aggregates", Moving-Aggregate Mode. https://www.postgresql.org/docs/current/xaggr.html
75. pandas release notes, 1.2.0. https://pandas.pydata.org/docs/whatsnew/v1.2.0.html
76. Python documentation, `math.fsum`. https://docs.python.org/3/library/math.html
77. NumPy reference, `numpy.sum`, Notes. https://numpy.org/doc/stable/reference/generated/numpy.sum.html
78. Neal. *Fast Exact Summation Using Small and Large Superaccumulators.* 2015. Abstract only: https://arxiv.org/abs/1505.05571
79. Nikolic, ElSeidy, Koch. *LINVIEW: Incremental View Maintenance for Complex Analytical Queries.* SIGMOD 2014. Abstract only: https://arxiv.org/abs/1403.6968
80. Nikolic, Olteanu. *Incremental View Maintenance with Triple Lock Factorization Benefits.* SIGMOD 2018. Abstract only: https://arxiv.org/abs/1703.07484

**Workspace documents** (paths relative to `C:\wk\ranvier-release`; figures supplied by the
coordinating session, see Conventions)

- W1. `Ranvier/docs/.ai/benchmarks/counters/e13f159.md`. Counter bench of 2026-09-30: retired
  instructions per operation on .NET 10.0.12 for Ranvier e13f159, FSharp.Data.Adaptive 1.2.27 and
  R3 1.3.1, with tiered compilation off.
- W2. `Ranvier/src/Ranvier/Combinators.fs`, lines 1858–1873 (`sumBy` and its finite subtraction)
  and line 2216 (`tryMinBy` cost).
- W3. `ranvier-fsharp/README.md`, "Benchmark Highlights".
- W4. `AstroShowcase.Web/README.md`.
- W5. Project files and READMEs of the repositories in the workspace, as reported by the
  coordinating session (target frameworks, hosts and rendering back ends).

**Cited by name and not read** (every statement that depends on one of these is marked ⚠️ in the
text): Welford 1962; Kahan 1965; Neumaier 1974; Higham, *The Accuracy of Floating Point Summation*,
1993; Kalman 1960; Reps, Teitelbaum and Demers 1983; Elliott and Hudak 1997; the Count-Min,
HyperLogLog, KLL and DDSketch papers; Demmel and Nguyen on reproducible summation.
