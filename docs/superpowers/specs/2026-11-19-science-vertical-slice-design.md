# Science vertical slice (S1) design

Date: 2026-11-19
Status: approved by the owner on 2026-11-19. Built in [`Ranvier.Science`](https://github.com/ranvier-org/Ranvier.Science), whose `docs/FINDINGS.md` reports what the slice found.
Baseline: Ranvier `master` at `25a4452` (core 0.2.0); the package versions `AstroShowcase/AstroShowcase.fsproj` references (View 0.2.0, D5 0.1.1, Css 0.1.1, WebCore.Sdl 0.2.1, Hmr 0.1.3).
Research: [`RESEARCH-science-synthesis.md`](../../.ai/RESEARCH-science-synthesis.md) (start there), with the prior-art, market and workflows files beside it.

Every figure carries a label: *measured here* (with what was run), *from documentation* (with the
file), or *general knowledge* (not checked). ⚠️ marks a claim not confirmed in its primary source.

## 1. Intent

The owner's goal is a platform that delivers science in a pull-based model, efficient and reactive,
optimised by default: a formula is inputs, constants and a computation, formulas chain as memos,
inputs may arrive asynchronously or as streams, and results are presented as collections and
projections.

The research describes that platform in five layers (synthesis §4). The owner chose sequence S1: one
example built through every layer, then widen. This document designs that first example.

The slice exists to answer five questions with working code and measurements. Each answer shapes a
layer before the layer is widened.

| # | Question | Layer |
|---|---|---|
| Q1 | What does a node cost around a real formula body, and where is the grain below which a node is not worth it? | 0, 1 |
| Q2 | Does a float policy fit as accumulator state passed to today's `createStreamFold` and `Projection.foldGroup`, or must the core change? | 0 |
| Q3 | What must a quantity carry at run time (name, unit, dimension, source, status) for delivery and for editing a formula in place? | 1, 3, 4 |
| Q4 | How does a per-frame pull of "the state at time t" meet the graph? | 1 |
| Q5 | Does one page build and behave the same in the native window and the browser, numerically as well as visually? | 3 |

## 2. Goals, non-goals, anti-goals

**Goals.** The slice is done when all of these hold.

1. A benchmark reports node overhead around four formula bodies of increasing cost, and names the
   smallest body for which a node adds under 10% (section 6).
2. The Atmospheric Drag Decay page of AstroShowcase runs on tracked quantities beside the original,
   in a versioned copy of the app, with the count of decay integrations per control change for both
   (section 8).
3. A new page, the **sequential monitor**, runs in the native window and in the browser from the
   same sources (section 7). It folds streams of observations into moments under a stated float
   policy, shows an always-valid confidence sequence per channel, presents the channels through a
   projection, and keeps a pooled summary by `foldGroup`.
4. A person using the page can type a formula over the page's quantities and see it evaluated per
   channel, with its unit, on both targets (section 7.5).
5. One AstroShowcase simulation page runs with its simulation as one restartable node (section 8).
6. A findings report states the answers to Q1 to Q5, each core change the slice argues for with
   its measurement, and each interface that should change before widening (section 10).

**Non-goals.** Out of this slice, and candidates for the widening that follows:

- Any change under `Ranvier/src`. Core changes are an output of the slice, as proposals.
- Publishing a package, creating a remote repository, tagging or releasing anything.
- A windowed or decayed stream fold, a general half-group fold, order statistics.
- Evaluating one graph over many input vectors (scenarios, uncertainty propagation).
- A notebook or cell host, session persistence, multi-line editing with `Ranvier.Plume`.
- Lineage of F#-defined quantities in release builds, and a durable cache of derived values.
- C# surface, terminal and server-HTML delivery of the page.
- Porting the other 27 AstroShowcase pages, and building the two ported pages for the browser.
- A two-sample or multi-arm test. The page estimates one mean per channel.
- Reading real data files or instruments. The source seam is named in section 7.2.

**Anti-goals.** The slice has failed, even if it runs, when any of these is true.

- A node per arithmetic operation or per array element appears anywhere (synthesis §3.2).
- The page shows a fixed-sample interval as if it were valid under continuous monitoring.
- A number on the page differs between the native window and the browser with no stated tolerance.
- The package keeps its own dependency tracker, cache or scheduler beside the core's. That is how
  `Kit.memo` and `Kit.Flow` came to exist (synthesis §1.2).
- A reason of the form "another tool already does this" decides any part of the design. Other
  systems are design input and evidence of demand only (the owner's evaluation rule).

## 3. Core and view state

The slice includes view code, so this section follows `.claude/rules/view-uses-core.md`.

**Core primitives used.**

| Primitive | Job in the slice |
|---|---|
| `createSignal`, `createSignalWithComparer` | Named inputs and the formula text |
| `createMemo` | Derived quantities; the parsed formula; display values held within a tolerance by returning the previous value (section 7.4) |
| `createStreamFold` | One observation fold per channel |
| `createKeyedCollection`, `createProjectionWith` | The channels and their rows; each row's nodes live in the row's scope |
| `Projection.foldGroup` | The pooled moments, maintained per row change |
| `Projection.fold` | The from-scratch pooled moments shown in the diagnostics panel |
| `createRoot`, `onCleanup`, `batch` | Lifetime of the page's nodes; one publication per frame of simulated items |

**View-owned state.** DOM nodes and their layout, and the text input's current DOM value. The view
keeps no cache, key matcher or selection state. A chart is a `Scene` value returned by a memo.

## 4. Constraints

- **No file under `Ranvier/src` changes.** The float policy, tolerances, quantities and runs
  are built on the public API of core 0.2.0. Reasons: the Rocq proofs and counter benchmarks stay in
  step with no extra work, the slice needs no core release, and it stays clear of the
  parallel-evaluation work in progress in `experiments/parallel-runtime` (another session's).
- **Grain.** A node holds a quantity, an array or a block. Simulation state stays inside one node.
- **Fable-compatible F#.** The library and the page compile with Fickl, as `AstroShowcase.Web` does.
  No reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`.
- **Demo rules of the workspace.** The page runs under the hot reload host from its first version
  (`dotnet run -- dev`), is live by default, and uses `Ranvier.View.Dsl`. Benchmark, soak and
  capture windows are hidden. The app is launched on the desktop only when the owner asks.
- **Figures are labelled**, and two figures are compared only when they come from the same run.
- **`AstroShowcase` is not edited.** It has no version control (*measured here*:
  `git rev-parse --show-toplevel` fails in the folder), and the ports need edits to shipped files:
  `Catalog.fs` to load the new section, `App.fs` where it sets up state, `Harness.fs` for a new
  scripted mode, and a counter and exposed state on the two original pages (section 8). The ports
  are therefore built in a copy of its sources inside the new repository,
  `Ranvier.Science/examples/AstroTracked`, as `AstroShowcase.Web/src` is a copy. The copy is under
  git from its first commit, so those edits are ordinary reviewed changes. Editing `AstroShowcase`
  in place, after putting it under git, is the alternative and is the owner's call (section 5).
- **Coordinator steps** (merge, tag, publish, remote branches) are not part of any task in the plan.

## 5. Approaches

**Sequence, chosen by the owner.**

| | What it is | Trade-off |
|---|---|---|
| **S1, chosen** | One vertical slice through every layer, then widen | Each layer's interface is shaped by a real consumer and a working artefact exists on two targets early. Every layer starts thin, so early interfaces will change. |
| S2 | Bottom-up: finish layers 0 and 1, port all 29 pages, then catalogue, then authoring | Foundations settle first and have a user today. The authoring surface arrives last, and lower layers are designed before their consumers exist. |
| S3 | Authoring surface first | Addresses the workflow with the strongest pain evidence. The largest build before any scientific content exists. |

The owner picked S1 on 2026-11-19.

**Calls made inside S1 by the author of this document.** Each is a proposal the owner can correct.

| Call | Alternative considered | Why this one |
|---|---|---|
| The code lives in a new sibling folder, working name `Ranvier.Science`, as a local git repository with no remote | A folder under `Ranvier/experiments` | The slice depends on View and D5, which sit downstream of the core repository. The name and whether it becomes an organisation repository are the owner's to decide later; nothing in the slice depends on the name. |
| The first catalogue content is statistics; astrodynamics appears as two ported pages | Astrodynamics first | Always-valid monitoring is the workflow with the closest model fit (synthesis §3.5, rank 1), and a pull-based value needs it: a value anyone can read at any time needs an interval valid at any time. |
| The page estimates one mean per channel | A two-arm A/B comparison | The confidence sequence read for this design (section 7.3) is stated for one sequence of observations. A two-sample form was not read, so the page does not claim one. |
| In-place editing is a small formula notation typed into a text input, evaluated at run time | Edit F# and rely on hot reload only | Hot reload exists only in a native Debug build. A notation works on every target, gives run-time units a consumer, and is the first answer for people who do not write F# (synthesis §5, decision 3). Hot reload remains the loop for F# authors. |
| Units are F# units of measure joined to a run-time unit descriptor at the point a unit is defined | Compile-time units only | Display, the notation, C# and JavaScript callers all need a dimension at run time (synthesis §5, decision 5). |
| The slice owns only the numerics it uses: a compensated sum, moments, one boundary function and a small generator | Wrap an existing numerical library | Nothing larger is needed here; the question of how much to own (synthesis §5, decision 6) is answered after the slice. |
| The two ported pages are built in a versioned copy of the AstroShowcase sources inside the new folder, native only | Edit `AstroShowcase` in place; or port in `AstroShowcase.Web/src` | The ports need edits to shipped files (section 4) and `AstroShowcase` has no version control. `AstroShowcase.Web/src` is on older packages (View 0.1.50) and lacks the scripted modes (*measured here*: `diff -rq` shows eleven shared source files differ and no `Harness.fs` there). The cost is a third copy of those sources; moving the ports into `AstroShowcase` later is a separate decision. |
| Frame cost is accepted as work time per frame, measured hidden; a paced, visible run happens when the owner asks | Show a window and a browser tab during the build | The workspace keeps benchmark windows hidden and launches apps only on request, and a hidden window is not paced by the display (`AstroShowcase/Harness.fs:771-772`). |
| The ported rigid-body page restarts on every control, as the original does | Let the run continue when the inertia changes | The port is accepted on bit-identical state with the original. The rule that a quantity read only by `advance` does not restart a run is tested on a synthetic run. |

## 6. Step one: what a node costs (Q1)

A BenchmarkDotNet project in `Ranvier.Science/bench`, against the published core 0.2.0.

Four bodies, all taken from `AstroShowcase` so they are real formulas: a closed-form scalar
(the decay rate, `PagesOrbit.fs:590`), `keplerToState`, one `rk4` step of the rigid-body equations,
and one `decay` integration (`PagesOrbit.fs:593`).

Four ways to call each body: directly; through a copy of `Kit.memo`; as a `createMemo` node read
while cached; as a `createMemo` node whose input is written before each read.

The report gives time and allocation per operation, and for each body the ratio of the node
variants to the direct call. **The grain threshold** is the cheapest body for which the
write-then-read node costs under 10% more than the direct call. The 10% is this document's
proposal for what "worth a node" means; the owner can set another figure.

The existing published figure is 15.5 ns for one invalidate-then-read of a trivial body (*from
documentation*: `docs/content/benchmarks/memos.md`, recorded 2026-09-28 at commit `d87920f`). It is not
compared with the new run; the new run replaces the estimate in synthesis §1.5.

JavaScript node overhead is not benchmarked in the slice. The page's frame time in the browser is
(section 7.6).

## 7. Slice A: the sequential monitor

### 7.1 What the person sees

```
 Sequential monitor                                   [ Pause ] [ Restart ]
 ---------------------------------------------------------------------------
 Level α  [0.05]   Tightest near n  [1000]   Target  [0.0 mK]   Rate [1000 /s]

 Channel   n        mean        std error   95% sequence        your formula
 A        12,400    0.84 mK     0.09 mK     [0.52, 1.16] mK     9.3
 B        12,400   -0.02 mK     0.09 mK     [-0.34, 0.30] mK   -0.2
 C         waiting for data
 All      24,800    0.41 mK     0.07 mK     (pooled, descriptive)
                                                    [ + channel ] [ remove ]

 your formula:  (mean - target) / se                        dimensionless  ok

 mean with its confidence sequence, per channel
   mK |   ░░░▒▒▒▓▓▓▓████████████████████████   band narrows as n grows
      |  ──────────────────────────────────── target
      +-------------------------------------- n (log scale)

 Diagnostics
   pooled, maintained vs from scratch: 0 ulp     re-folds: 3
   last frame: 16 items folded, 2 rows recomputed, 0 streams restarted
```

The fixed-sample 95% interval is drawn as a thin line inside the band, labelled "valid at one
planned n only", so the difference is visible and not mistaken.

### 7.2 Data flow

| Node | Kind | Reads | Notes |
|---|---|---|---|
| `alpha`, `tightestAt`, `target`, `rate` | inputs | | `target` carries the observation unit |
| per channel: `trueMean`, `trueSd`, `seed` | inputs | | Parameters of the simulated source |
| per channel: `observations` | stream fold | `trueMean`, `trueSd`, `seed` in `start` | Folds items into `Moments` and a bounded history. Changing a source parameter restarts the fold from its seed, which is the core's restart rule |
| per channel: `n`, `mean`, `variance`, `se` | derived | `observations` | Units: `u`, `u²`, `u` |
| `alphaPerChannel` | derived | `alpha`, the channel count | `alpha / count`; adding a channel widens every channel's sequence |
| per channel: `sequence` | derived | the channel's moments, `alphaPerChannel`, `tightestAt` | Lower and upper bound, unit `u` |
| per channel: `band` | derived | the channel's history, `alphaPerChannel`, `tightestAt` | The chart series. Changing α recomputes the band from history and folds no item |
| `pooled` | `foldGroup` over rows | each row's `Moments` | Merge on add, unmerge on remove or change, under the float policy |
| `pooledFromScratch` | `Projection.fold` | each row's `Moments` | Diagnostics only |
| per channel: `user` | derived | the parsed formula, the names it uses | Section 7.5 |

Channels are a keyed collection. Each row's nodes are created in the row's factory scope, which
`createProjectionWith` keeps until the key is removed (`Api.fs:606-619`). The builder confirms that
a stream fold may be created in a factory body; if it may not, each channel gets a `createRoot`
disposed with its key.

**The simulated source** produces `rate × dt` normal deviates per frame per channel, inside one
`batch`, from a small deterministic generator written in the library (32-bit arithmetic, so it runs
at full speed in JavaScript). It sits behind `StreamSource.create`. A real source replaces it
through the converters the core already has (`ofAsyncEnumerable`, `ofObservable`, `ofAsyncIterable`,
`ofChannel`); the page's nodes do not change. A test on .NET delivers items from a second thread to
check the fold's state is safe there.

**Pending, failed, stale.** A channel with no item yet is pending and its row says so. A failed
stream shows its error in the row. A formula that does not parse or check keeps the last valid
formula's values and is marked stale beside its message.

### 7.3 Layer 2: the statistics

**Moments.** Count, mean and sum of squared deviations of the observations folded so far.

**Confidence sequence.** For level `α` and tuning parameter `ρ > 0`, at each `t`:

```
mean_t  ±  sd_t · sqrt( (2(tρ² + 1) / (t²ρ²)) · ln( sqrt(tρ² + 1) / α ) )
```

where `sd_t² = (1/t)·Σ yᵢ² − mean_t²`. ⚠️ *From documentation*: Waudby-Smith, Arbour, Sinha, Kennedy
and Ramdas, "Time-uniform central limit theory and asymptotic confidence sequences", arXiv
2103.06476, Theorem 2.2, equation 13. The title and authors were read on the arXiv page; the formula
and its conditions were read through a summarising fetch of an HTML rendering, not from the PDF.
As read: the guarantee is asymptotic and time-uniform, the time-uniform analogue of a central-limit
interval, for independent identically distributed observations with finite variance. Versions of
the paper may scale `ρ` differently. Before coding, the builder reads the theorem in the PDF and
checks the function against the authors' reference implementation on a few inputs.

`tightestAt` sets `ρ` so the sequence is narrowest near a chosen sample size. The paper gives the
exact choice in its appendix B.2, which was not read ⚠️; the builder takes it from there.

**The fixed-sample interval** `mean ± z·sd/√t` is computed for the contrast line only.

**Acceptance for the statistics** is empirical and deterministic. With fixed seeds, over 2,000
simulated streams of 10,000 observations each, the fraction of streams whose sequence excludes the
true mean at any time from `t = 30` on is at most `α` plus a stated simulation margin, and the same
fraction for the fixed-sample interval is several times `α`. The test records both fractions. (That
a fixed-sample interval fails under continuous monitoring is *from documentation*: Johari, Pekelis
and Walsh, arXiv 1512.04922, abstract.)

Multiplicity across channels is handled by `alphaPerChannel` (a Bonferroni split, *general
knowledge*). The pooled row is descriptive and shows no sequence.

### 7.4 Layer 0: the float policy

A policy is chosen where an aggregate is built. It states how the aggregate accumulates and when it
re-folds from its cached inputs.

| Policy | Accumulation | Re-fold | Use |
|---|---|---|---|
| `standard` (the default) | Compensated (Neumaier) for every running sum | After as many removals as the aggregate has rows, and whenever the state is not finite | Everything on the page |
| `fast` | Plain | Only when the state is not finite | What `Projection.sumBy` does today (`Combinators.fs:1858-1873`); kept for comparison in the benchmark |

**What the policy guarantees, as tests.**

1. *Add-only fold.* The state after folding a sequence equals the state of a fresh fold over the
   same items in the same order, bit for bit, on one target.
2. *Across targets.* For a recorded stream of doubles, the moments are bit-identical on .NET and
   in JavaScript. They use `+ − × ÷` only, which both platforms round correctly (*general
   knowledge*; the test establishes it). The sequence bounds use `ln` and `sqrt`; `ln` is not
   specified to the last bit on either platform, so bounds are compared within a relative `1e-12`.
3. *Accuracy.* On ill-conditioned inputs, including the sequence `1e20, 1, −1e20` whose plain sum is
   0 (*from documentation*: the PostgreSQL manual's moving-aggregate example, synthesis §3.3), the
   compensated sum is within 2 units in the last place of an exact reference sum. The moments of
   `1e9 + k` for small `k` have a variance within a relative `1e-9` of the exact value.
4. *Maintained aggregate.* Immediately after a re-fold, the maintained state equals the from-scratch
   state bit for bit. Between re-folds the two agree within a bound the test states in units in the
   last place, over one million row changes.
5. *Cut-off.* Folding an item that leaves the state bitwise unchanged notifies no reader.

**How it fits today's core.** The fold state is an immutable value, so it can be handed between
threads and a published state is never changed under a reader. The stream memo notifies when the
new state differs from the last under the graph's equality policy (`Streams.fs:308`), and
`createStreamFold` takes no comparer. That policy differs by target: on .NET a value type compares
by value, and in JavaScript every non-primitive compares by reference, structs included
(`Types.fs:279-307`). Two rules make behaviour the same on both:

- a fold returns the previous state instance when an item leaves the state unchanged;
- every quantity whose recomputations the acceptance counts is a `float`, which compares by value
  on both targets.

For `foldGroup` the state carries a count of removals. When the count reaches the policy's limit,
`subtract` raises, and the core re-folds from its cached row values: `ProjectionFold.Step` treats
any exception from a step as a request for a full re-fold (`Combinators.fs:1058-1064`), which is
the mechanism `subtractFinite` uses. Using an exception as a request is a finding for the report:
the core could offer a direct one.

**Tolerance.** `Tolerance.relative` and `Tolerance.absolute` decide whether two values are close
enough to count as unchanged. A memo stores a recomputed value even when its comparer calls it
equal to the previous one (`Core.fs:4854`, `4886`), so a tolerance passed as a comparer would let
small changes accumulate unseen. A quantity with a tolerance therefore compares the new value with
the last published one inside `compute` and returns the published one when they are close;
returning the previous value keeps dependents clean (`Api.fs:258-260`). A change is then published
exactly when the value has moved from the last published value by more than the tolerance.

Its one use on the page: a chart series returns its previous array when every point is within a
tenth of a pixel of it, so a chart is rebuilt only when it would look different. A tolerance is
opt-in per quantity, because a quantity held within a tolerance, and everything derived from it, is
then consistent with a from-scratch evaluation only within a propagated bound. Default equality
stays exact. A tolerance comparison is not transitive; what the core's proofs assume of a comparer
is read before a core change is proposed.

The same accumulators are also run through `Projection.foldGroup` over 100,000 rows in the
benchmark, with 1% of rows revised per pass, under both policies. That run also measures the cost of
adding a key, which the guide and the source describe differently (synthesis §3.6, the append row).

### 7.5 Layers 1 and 4: quantities, units and the formula

**Units.** A unit is defined once, as a value that carries both an F# unit of measure and its
run-time descriptor:

```fsharp
/// A unit of measure with the symbol, dimension and size its values carry at run time.
type UnitOf<[<Measure>] 'u>

module Units =
    val one : UnitOf<1>
    val kelvin : UnitOf<K>
    val milli : UnitOf<'u> -> UnitOf<...>      // the builder settles prefixes
    // products, quotients and integer powers of units give the unit of the product type
```

The run-time descriptor is a symbol, seven integer exponents over the SI base dimensions, and a
size relative to the SI base unit. F# checks dimensions for F# authors at compile time. The
descriptor serves display, the formula notation and non-F# callers. Fractional exponents are
rejected at definition in this slice.

**Quantities.** Each is a core node with a name, an identifier usable in formulas, a unit and a kind.

```fsharp
module Quantity =
    /// A named value a person or a program sets.
    val input    : id: string -> name: string -> unit: UnitOf<'u> -> initial: float<'u> -> Input<'u>
    /// A fixed value with the reference it was taken from.
    val constant : id: string -> name: string -> unit: UnitOf<'u> -> value: float<'u> -> source: string -> Quantity<'u>
    /// A value computed from other quantities; it recomputes when a quantity read by `compute` changes.
    val derive   : id: string -> name: string -> unit: UnitOf<'u> -> compute: (unit -> float<'u>) -> Quantity<'u>
    /// `derive` whose readers are notified only when the value moves by more than `tolerance`
    /// from the value last published.
    val deriveWithin : tolerance: Tolerance -> ... -> Quantity<'u>
    /// A computed array, record or other block with no single unit.
    val block    : id: string -> name: string -> compute: (unit -> 'T) -> Block<'T>
```

These wrap `createSignal` and `createMemo`; reads inside `compute` are tracked by the core. A
**model** groups quantities and resolves an identifier to a reader, a unit descriptor and a status.
A projection row has its own model whose parent is the page's, so `mean` in a row means that
channel's mean and `alpha` means the page's.

Names and exact signatures are the builder's to settle within these contracts.

**The formula notation.** One line of text: numbers, identifiers of quantities in scope, `+ − × ÷`,
integer powers, parentheses, and `sqrt abs exp ln log10 min max`. It is parsed and checked once per
edit into a tree held by a memo:

- every identifier resolves in the row's model;
- `+`, `−`, `min` and `max` need operands of the same unit. Operands of the same dimension and
  different size are rejected with a message naming both units; the slice does no implicit conversion;
- `exp`, `ln` and `log10` need a dimensionless operand; `sqrt` needs even exponents;
- the result's unit is derived and shown beside the formula.

Each row holds one derived quantity that evaluates the tree against the row's model. Editing the
formula replaces the tree; no node is created or disposed per edit. This is not a programming
language: it has no definitions, conditionals, arrays or strings.

Hot reload is the other half of in-place editing: under `dotnet run -- dev`, an edit to a formula
body in F# is patched in on save. A reload may reset stream state; the report records what it does.

### 7.6 Layer 3: delivery

One set of sources, two heads, laid out as `AstroShowcase.Web` is: `src/` for the page, `native/`
for the window (WebCore through `Ranvier.View.WebCore.Sdl`, with the hot reload package in Debug),
`web/` for the Fickl build. The platform seam follows `AstroShowcase.Web/src/Host.fs` (`IPlatform`),
reduced to what a page with no 3D view needs.

Science components, thin: an axis label built from a quantity's unit, a band between two series,
and the pending, failed and stale states of 7.2. Charts are `Ranvier.D5` scenes. The D5 0.1.1
package carries no Fable sources, so the browser head compiles the `Ranvier.D5` clone, as
`AstroShowcase.Web` does (*from documentation*: its project file).

A scripted mode saves a picture and prints per-frame timings: in a hidden native window, as
`AstroShowcase/Harness.fs` does, and in a headless browser that posts its lines to the local
server, as `AstroShowcase.Web` does.

**Acceptance for delivery.**

- **Frame work.** At the default setting (four channels at 1,000 observations a second each) the
  app's work for one frame is timed with a stopwatch on both targets: folding, recomputation,
  building the chart scenes, the flush and the layout it forces. The mean and the 95th percentile
  over 600 frames are under 6.06 ms, the period of the 165 Hz display the earlier comparison ran on
  (*from documentation*: `AstroShowcase.Web/measure/README.md`). The run also records the highest
  observation rate that stays under it. No frame rate is claimed from a hidden run. A paced,
  visible run on both targets is made when the owner asks for it.
- **Same page on both targets.** On the recorded stream, the text of the table is equal on the two
  targets, digit for digit at the page's displayed precision. A picture from each target goes in
  the findings report for the owner to compare by eye. No pixel equality is claimed.
- Reactivity counts, taken by counters in the formula bodies: changing `α` folds no item, restarts
  no stream and recomputes each channel's bounds and band once; adding a channel recomputes
  `alphaPerChannel` and each channel's bounds, and re-runs no existing channel's fold; a publication
  on one channel recomputes that channel's row and makes one unmerge and one merge on `pooled`.
- The cross-target numeric test of 7.4 passes on the recorded stream.

## 8. Slice B: two AstroShowcase pages on the graph (Q4)

Both are built in `Ranvier.Science/examples/AstroTracked`, the versioned copy of the AstroShowcase
sources (section 4). The tracked pages are new files published as a new rail section. The
originals stay in the copy, so one run times and counts both and the figures compare.

**Tracked inputs for a page.** The app writes control values in one place (`writeRow`,
`App.fs:85`). A tracked page receives one signal per control, written there in one batch. The
app's time is already a signal (`State.Time`, created at `App.fs:444`), written by `App.fs` and by
the scripted modes in `Harness.fs`; tracked pages read that signal, so a scripted run drives them
as it drives the others. The tracked pages' nodes are created in the app's `setup`, where the graph
and a stable owner exist; `Catalog.load` runs before the window and its graph do
(`Program.fs:26-28`). `Page` keeps its shape: the tracked page's `Fixed`, `Charts`, `Status` and
`Cursor` ignore their `float[]` argument and read quantities. The other 27 pages are untouched.

**Checks in the copy.** `decay` gets a counter that both versions of the page share. The original
rigid-body page exposes its simulation state for comparison. A new scripted mode changes one
control at a time, steps the clock, and prints the counts and the comparison.

**Atmospheric Drag Decay** (`PagesOrbit.fs:564-700`). Inputs: initial altitude, ballistic
coefficient, solar activity, each with its unit. Derived: the decay history (reads all three), the
lifetime, the lifetime sweep over 37 coefficients at three activity levels (reads the altitude
alone), the marker (reads the coefficient and the lifetime), and the two chart panels.

Predicted from reading the code, to be confirmed by the count: moving the ballistic coefficient or
the solar activity runs 112 decay integrations on the original page and 1 on the tracked page;
moving the altitude runs 112 on both.

**A restartable run.** Today `Kit.Flow` (`Kit.fs:157`) advances a state with the animation clock
and restarts when a hand-listed key changes. Its replacement is a `Run`:

```fsharp
module Run =
    /// A simulation advanced with `clock`. It starts again when a quantity read by `start` changes
    /// or the clock goes back. `advance` may read quantities; those reads do not restart it.
    val create : clock: (unit -> float) -> start: (unit -> 's) -> advance: ('s -> float -> unit) -> Run<'s>
```

Its value is pulled: the state advances when something reads it at a later clock value, so a page
that is not shown is not simulated, as with `Flow`. The whole state is one node. The reference
construction is two memos on today's core, one holding the run that `start` produced and one
stepping it from its previous value (`createMemo` passes the previous value to `compute`); a state
mutated in place is published with a step stamp so readers are notified. The builder may construct
it differently within the contract. `Run` replaces the hand-listed key with tracked reads.

**Rigid body page** (`PagesFlight.fs:145-312`), ported onto `Run`. Its state is mutated in place
and keeps sampled histories, which is the hard case. The original restarts on any control
(`Flow<Body>(id, …)`, line 209), although its start state reads the spin axis and tilt alone and
only `advance` reads the inertia. The port keeps the original's behaviour: its `start` reads the
inertia as well, so every control restarts the run.

**Acceptance.** For any script of control changes and stepped clock values, the tracked and
original rigid-body pages produce bit-identical simulation state. The run restarts when a quantity
read by `start` changes and when the clock goes back, and does not advance while its page is not
shown. A synthetic run in the library's tests shows that a quantity read only by `advance` changes
the run's course without restarting it. The `--bench` frame times of both versions of each page are
reported from one run.

## 9. Order of work

1. The node-cost benchmark (section 6). It depends on nothing else.
2. Probes that could change the design: F# units of measure through Fickl; an empty View page on
   the current packages through Fickl; a stream fold created in a projection row factory; a memo
   reading a pending stream.
3. Units, quantities and models; then the copy of AstroShowcase, the Decay page and its counts.
4. Numerics and the float policy, with their tests on .NET and in JavaScript.
5. Statistics, with the coverage test.
6. The monitor page: native first, under hot reload, then the browser head and the cross-target test.
7. The formula notation.
8. `Run` and the rigid-body page.
9. The findings report.

Each of 3 to 8 ends with one clean build and one unfiltered test run.

## 10. The findings report

A file in `Ranvier.Science/docs`, written last. It contains:

- the answers to Q1 to Q5, each with its measurement and label;
- every place the slice worked around the core, each as a proposal: what the core would offer, what
  it would replace in the slice, and the proof and benchmark obligations it would bring. Expected
  entries: a float policy per aggregate, a direct re-fold request, a memo that keeps its stored
  value when a recomputed one compares equal, a comparer for a stream fold's state, and enumeration
  of a node's sources in release builds;
- each layer-1 interface that should change before widening, and why;
- the guide-against-source discrepancy on the cost of adding a key, with the measured answer;
- a recommendation on what to widen first.

## 11. Risks

| Risk | What happens then |
|---|---|
| Fickl does not compile F# units of measure as wanted (no source in the family uses them: *measured here*, `grep "[<Measure>]"` over core, D5, View and AstroShowcase matched nothing) | Quantities take a marker type parameter in place of a measure; unit arithmetic moves to the run-time descriptor. Probe 2 decides before layer 1 is built |
| The current package line has not been built for the browser by this workspace (`AstroShowcase.Web` uses View 0.1.50) | Probe 2 builds an empty page first. If it fails, the browser head pins the versions `AstroShowcase.Web` uses, which still sit on core 0.2.0 (*from documentation*: the View 0.1.50 nuspec in the NuGet cache, read by this document's reviewer), and the report says so |
| The sequence's formula was read through a summarising tool | Section 7.3's check against the PDF and the reference implementation, and the coverage test |
| A third copy of the AstroShowcase sources drifts from the other two | The copy exists to measure two pages and takes no fixes from the others during the slice. `AstroShowcase` has no commit to name, so the copy's first commit records the date and the folder it was copied from |
| The `Ranvier.D5` clone the browser head compiles has an uncommitted change to its project file (seen by this document's reviewer) | The plan records the clone's state at build time; the slice does not edit that clone |
| Every layer is thin, so interfaces will move | Expected under S1. Section 10 collects the changes; nothing is published from the slice |

## 12. Left to the builder

Internal representation of the accumulators and the history; names and signatures within the
contracts of 7.4, 7.5 and 8; the parser's technique; the generator's algorithm; file layout inside
each project; the test framework (the one `Ranvier/tests/Ranvier.Tests` uses, unless it does not run
under Fickl); how the tracked inputs are wired into the copy's `App.fs`; chart composition.

## 13. Review record

A reviewer read this document on 2026-11-19 as the person who would plan the build, with read
access to the workspace. It found five statements that did not match the source; each was checked
against the file by the author and corrected above: what a memo stores on an equal recompute
(7.4), equality of struct state under JavaScript (7.4), the existing time signal (8), the edits the
ports need (4), and two line and file counts. It raised three questions; sections 4, 5, 7.6 and 8
now answer them as the author's calls.
