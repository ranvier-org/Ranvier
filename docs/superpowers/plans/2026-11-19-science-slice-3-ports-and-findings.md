# Science Slice, Plan 3: AstroTracked Ports and Findings — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two AstroShowcase pages run on tracked quantities and a restartable run beside their originals, in a versioned copy of the app, with counted integrations, bit-identical simulation state and frame times from one run; then the findings report of the whole slice.

**Architecture:** `Ranvier.Science/examples/AstroTracked` is a copy of the `AstroShowcase` sources under git, committed unmodified first. Two new files add a registry of tracked pages and the two ports, published as a fifth rail section. The originals keep their code: they gain two counters, lose `private` on the numerical helpers the ports call, and expose one state reader. A new scripted mode, `--tracked`, drives both versions of each page in a hidden window and exits 1 on any difference.

**Tech Stack:** F# on .NET 10. The copy keeps the package versions of `AstroShowcase/AstroShowcase.fsproj`: `Ranvier.View` and `Ranvier.View.Dom` 0.2.0, `Ranvier.View.WebCore.Sdl` 0.2.1, `Ranvier.Css` and `Ranvier.Css.View` 0.1.1, `Ranvier.Wgpu` and `Ranvier.Wgpu.Sdl` 0.2.1, `Ranvier.D5`, `.Charts` and `.View` 0.1.1, `Ranvier.View.WebCore.Hmr` 0.1.3 (Debug), and the local tool `ranvier.css.compile` 0.1.0. `Ranvier.Quantities` is a project reference.

**Spec:** `C:\wk\ranvier-release\Ranvier\docs\superpowers\specs\2026-11-19-science-vertical-slice-design.md` (approved 2026-11-19). Section numbers below refer to it.

**Scope.** This is the third of three plans.

| Plan | Covers (spec sections) | State |
|---|---|---|
| 1, library, benchmark and probes | §6, §7.3, §7.4, §7.5 units and quantities, §8 `Run` | merged at `794d8be` |
| 2, sequential monitor page | §7.1, §7.2, §7.5 formula notation, §7.6 | written beside this plan |
| **3, this plan** | §8 pages, §10 | Tasks 1 to 5 wait on nothing. Task 6 waits on Plan 2's records |

Run Plan 2 before this plan, or run Tasks 1 to 5 here, then Plan 2, then Task 6. The two plans edit `README.md` and `docs/`, so they do not run at the same time.

## Core and view state

Required by `Ranvier/.claude/rules/view-uses-core.md`.

**Core primitives used.**

| Primitive | Job in this plan |
|---|---|
| `createSignal`, through `Quantity.input` | One input per control of a tracked page |
| `createMemo`, through `Quantity.derive`, `Quantity.block` and `Run.createWith` | Derived quantities, chart blocks, the simulation |
| `batch` | One control row written to `State.Params` and to the page's inputs as one change |
| The app's `State.Time` signal (`App.fs:444`) | The clock of the run |
| The owner of the app's `setup` | Lifetime of a tracked page's nodes |

**View-owned state.** None is added. The copy's view is unchanged apart from the two calls in `App.fs` named in Task 3.

**State outside the graph.** Each tracked page keeps one module-level mutable reference to the record of its nodes, set by `Create`. It is a reference to nodes the graph owns, replaced on each mount; it caches no value.

## Global Constraints

- `C:\wk\ranvier-release\AstroShowcase` is read and never edited. No file under `C:\wk\ranvier-release\Ranvier\src` changes.
- Create and edit files only under `C:\wk\ranvier-release\Ranvier.Science`.
- The first commit of the copy holds the copied files unmodified. Every later change to a copied file is a reviewed diff.
- **Edits to the two original pages are limited to:** a counter increment, the removal of `private`, and one added state reader. No expression of an original page changes.
- The other 27 pages are untouched: no hunk in `Pages.fs` or `PagesAstro.fs`, and in `PagesOrbit.fs` and `PagesFlight.fs` only inside the `Decay` and `Attitude` modules. Outside those two modules a copied file changes only where a task names it: `Stage.fs` (two counters), `Catalog.fs`, `App.fs`, `Harness.fs` and the project file.
- A node holds a quantity, an array or a block. The simulation state stays inside one node.
- The copy keeps no dependency tracker, cache or scheduler of its own for the tracked pages: no `Kit.memo` and no `Kit.Flow` in `PagesTracked.fs`.
- Every window is created hidden (`Window.Create(hidden = true)`, as `Harness.scripted` does). No task runs the window mode or the `dev` host.
- Every figure written to a file carries a label: *measured here* (with the command and what was timed), *from documentation* (with the file), or *general knowledge*. Two figures are compared only when both come from one run.
- Thresholds and expected values in this plan are used verbatim. A count or comparison that differs is a finding: stop and report it with the printed lines; do not change the expectation.
- Commits are local. No remote is added, nothing is pushed, tagged or published. Each commit message ends with the session's attribution line.
- Shell: write multi-line scripts to a file and run the file; use absolute paths; run commands plainly.
- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`. Prose uses British spelling.
- Coordinator steps (merge, tag, publish, remote branches) are not part of any task.

## Review Focus

1. **A control set to the value it already has** (typing the current number). Expected: no decay integration, no restart, on both versions. Test in Task 3 (`typed` line) and Task 4 (script step 6).
2. **Several controls changed by one write** (Reset model). Expected: each derived quantity recomputes once; the tracked decay page runs 1 integration when the altitude is unchanged; the tracked run restarts once. Tests in Tasks 3 and 4.
3. **A control at an end of its range** (altitude 250 and 600 km, coefficient 20 and 200 kg/m², tilt 0.5 and 10°). Expected: the tracked page's status text and lifetime equal the original's; the simulation states stay bit-identical. Tests in Tasks 3 and 4.
4. **A second mount in one process** (hot reload runs `setup` again). Expected: `Tracked.create` builds a fresh model and fresh nodes and raises nothing; the pages read the new nodes. Test in Task 3.
5. **The clock set back, a frame gap above 0.1 s, and a control written away and back inside one batch.** Expected: the tracked run restarts, limits the step, and does not restart, exactly as the original does. Tests in Task 4.

---

## File Structure

```
Ranvier.Science/
  examples/AstroTracked/
    AstroShowcase.fsproj  nuget.config  dotnet-tools.json     copied; the project file gains two Compile items and one ProjectReference
    <20 copied .fs files>                                      Png Astro Plot Shaders Bodies Stage Host Kit Fmm Models Pages PagesOrbit
                                                               PagesFlight PagesAstro Catalog Settings Styles App Harness Program
    Tracked.fs                                                 new, after Kit.fs: the registry of tracked pages
    PagesTracked.fs                                            new, after PagesAstro.fs: DecayTracked, AttitudeTracked, the rail section
    COPY.md  COPY.sha256                                       where the copy came from, and a hash of every copied file
  docs/measurements/tracked-pages.md
  docs/FINDINGS.md
  docs/pictures/                                               page pictures for the findings report
```

The project file keeps the name `AstroShowcase.fsproj` and the namespace stays `AstroShowcase`, so the `ExtractCss` and `ShaderCheck` targets and every `open` work unchanged. The project is not added to `Ranvier.Science.slnx`: its build restores a local tool and runs a shader check, which the root build does not need.

Commands, run from `C:\wk\ranvier-release\Ranvier.Science`:

- **Build the copy:** `dotnet build -c Release examples/AstroTracked/AstroShowcase.fsproj`
- **Scripted modes:** `dotnet run -c Release --no-build --project examples/AstroTracked/AstroShowcase.fsproj -- <mode>`
- **Root gate:** `dotnet build -c Release`; `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then `npx mocha dist/tests/Main.fs.js`

Read before starting: `AstroShowcase/Kit.fs` (`Page`, `memo`, `Flow`, `publish`), `App.fs:71-85` (`writeRow`), `App.fs:437-461` and `744-870` (`setup`, `draw`, the effect), `Harness.fs:341-437` (`scripted`) and `756-861` (`bench`), `PagesOrbit.fs:564-700`, `PagesFlight.fs:145-314`, and `Ranvier.Science/docs/PROBES.md`, "Differences from Kit.Flow". Notes made while this plan was written, with file and line references, are in `Ranvier.Science/.superpowers/plan-research/astroshowcase-native.md`; they are scratch, and the source files are the authority.

---

### Task 1: The versioned copy

**Files:**
- Create: `examples/AstroTracked/` with the 20 `.fs` files, `AstroShowcase.fsproj`, `nuget.config` and `dotnet-tools.json` of `C:\wk\ranvier-release\AstroShowcase`; `examples/AstroTracked/COPY.md`; `examples/AstroTracked/COPY.sha256`

**Interfaces:**
- Produces: a building copy whose scripted modes run hidden. Later tasks name pages by `Title`, never by number.

- [ ] **Step 1: Copy.** Copy the 23 files named above and nothing else (not `bin`, `obj`, `verification`, `scratch`, the Markdown files or the script). Change no byte.
- [ ] **Step 2: Record the copy.** `COPY.sha256` is the output of `sha256sum *.fs AstroShowcase.fsproj nuget.config dotnet-tools.json`, run in the copy. `COPY.md` states: the date; the source folder; that the source has no version control, so no commit names it; the file and line counts from `wc -l *.fs` (20 files and 11,089 lines when this plan was written: *measured here*, `wc -l`, 2026-11-19); the package versions; and that the copy takes no fixes from `AstroShowcase` or `AstroShowcase.Web` during the slice.
- [ ] **Step 3: Build.** Run the build command. Expected: `ExtractCss` writes `app.css`, the shader check prints `PASS  shader check`, 0 errors. Record the warning count in `COPY.md` as the baseline; later tasks add none.
- [ ] **Step 4: Run the benchmark mode hidden.** `-- --bench --frames 30`. Expected: exit 0, one `page NN | frame: mean ...` line per page, each ending `30 frames, one draw each | <Title>`, then the summary line. Record the number of pages in `COPY.md` (29, *from documentation*: spec §2 counts the two ported pages and 27 others); Tasks 3 and 4 each add one.
- [ ] **Step 5: Commit** `chore(examples): AstroTracked, an unmodified copy of the AstroShowcase sources`. `git status` shows no build output staged (`bin/` and `obj/` are ignored by the root `.gitignore`).

### Task 2: A decay counter and the scripted mode, on the original page

**Files:**
- Modify: `examples/AstroTracked/Stage.fs` (the `Probe` type), `PagesOrbit.fs` (`Decay.decay`), `Harness.fs` (`Mode`, `usage`, `parse`, `run`, a new `tracked` body)

**Interfaces:**
- Produces:

```fsharp
// Stage.fs, on the existing Probe type; Reset() clears both.
static member val Decays = 0 with get, set       // calls of Decay.decay
static member val Advances = 0 with get, set     // calls of Attitude.advance (Task 4 increments it)

// Harness.fs
type Mode = ... | Tracked
// "--tracked" parses to Tracked; `run` dispatches it as: scripted "tracked pages" options false (fun _ -> None) tracked
```

`tracked (run: Run)` raises `Failed` on any failed expectation, which `scripted` turns into exit 1, and ends with the line `PASS  tracked pages`.

- [ ] **Step 1: Add the counters** to `Probe`, and `Probe.Decays <- Probe.Decays + 1` as the first statement of `Decay.decay`.
- [ ] **Step 2: Write the mode, counting the original page.** Find the page by `Title = "Atmospheric Drag Decay"`. Start: `goTo`; clock to 0; one frame (`clock <- clock + 1.0 / 60.0`, then `graph.Run(fun () -> state.Time.Value <- clock)`). A *count* is the difference of `Probe.Decays` across one change and the frame after it. Take four counts, in this order:

  1. the ballistic coefficient one step up (`bump state 1 1.0`);
  2. the solar activity to its next choice (`cycle state 2`);
  3. `resetModel state`, which restores the coefficient and the activity in one write and leaves the altitude where it was;
  4. the altitude one step up (`bump state 0 1.0`).

  Print one line, with the altitude first, and assert the four values shown:

```
decay counts | original | Initial altitude 112 | Ballistic coeff. 112 | Solar activity 112 | reset after two changes 112
```
- [ ] **Step 3: Build and run** `-- --tracked`. Expected: the line above, `PASS  tracked pages`, exit 0. The four numbers are the spec's prediction (§8), now *measured here*. A different number stops the task.
- [ ] **Step 4: Run** `-- --bench --frames 30`. Expected: exit 0, as in Task 1.
- [ ] **Step 5: Commit** `test(astrotracked): a decay counter and a scripted mode that counts the original page`.

### Task 3: Atmospheric Drag Decay on tracked quantities

**Files:**
- Create: `examples/AstroTracked/Tracked.fs`, `examples/AstroTracked/PagesTracked.fs`
- Modify: `AstroShowcase.fsproj` (`Tracked.fs` after `Kit.fs`; `PagesTracked.fs` after `PagesAstro.fs`; a `ProjectReference` to `..\..\src\Ranvier.Quantities\Ranvier.Quantities.fsproj`), `Catalog.fs`, `App.fs` (`writeRow`, `setup`), `PagesOrbit.fs` (`module private Decay` becomes `module Decay`; `private` removed from `fracAt`, `drawn`, `turns`), `Harness.fs`

**Interfaces:**
- Consumes: `Quantity.input`, `Quantity.derive`, `Quantity.block`, `Model.create`, `UnitOf.define`, `UnitOf.squared`, `Units.kilometre`, `Units.kilogram`, `Units.metre`, `Units.one` (Plan 1); `Decay.decay : float -> float -> int -> (float * float)[]`, `Decay.rate`, `Decay.fracAt`, `Decay.drawn`, `Decay.turns`.
- Produces:

```fsharp
module AstroShowcase.Tracked

/// A page whose model is nodes of the graph.
type TrackedPage =
    { Page: Page
      /// Builds the page's model and nodes under the current owner, from a clock and the page's control row.
      Create: (unit -> float) -> float[] -> unit
      /// Sets the page's inputs from a control row.
      Write: float[] -> unit }

/// Adds a tracked page to the registry and returns its Page for publishing.
val register : TrackedPage -> Page
/// Calls Create of every registered page. Each call builds a new model, so a second mount raises nothing.
val create : clock: (unit -> float) -> rowOf: (Page -> float[]) -> unit
/// Calls Write of the registered page with this title; does nothing for any other page.
val write : page: Page -> row: float[] -> unit
```

```fsharp
module AstroShowcase.PagesTracked

module DecayTracked =
    val page : Page                      // Title "Atmospheric Drag Decay, tracked"; the original's Controls, Blurb and camera
    /// The lifetime in days, a tracked read.
    val lifetime : unit -> float

val pages : Page[]                       // published with: publish 4 "On the graph"
```

`App.fs` changes, and no other line of it:

```fsharp
let private writeRow (s: State) index (row: float[]) =
    let all, pages = s.Params.Peek, s.Pages.Peek
    Api.batch (fun () ->
        s.Params.Value <- Array.init pages.Length (fun i -> if i = index then row else rowOf pages all i)
        Tracked.write pages[index] row)

// in setup, after the `values` signal is created:
Tracked.create (fun () -> time.Value) defaultsOf
```

**The nodes of the tracked page.** Units: altitude in `km`; ballistic coefficient in `kg/m^2` (`Units.kilogram / UnitOf.squared Units.metre`); solar activity dimensionless; lifetime in a new measure `day` (`UnitOf.define "d" Dimension.time 86400.0`).

| Node | Kind | Reads | `decay` calls per recomputation |
|---|---|---|---|
| `altitude`, `coefficient`, `activity` | `Quantity.input` | | |
| `history` | `Quantity.block`, `(float * float)[]` | all three inputs | 1 |
| `lifetime` | `Quantity.derive`, `day` | `history` | 0 |
| `sweep` | `Quantity.block`, three arrays of 37 `(coefficient, days)` | `altitude` alone | 111 |
| `marker` | `Quantity.block`, `float * float` | `coefficient`, `lifetime` | 0 |
| `fixedPart` | `Quantity.block`, `Buf` | `history`, `altitude` | 0 |
| altitude panel, sweep lines, marker marks | `Quantity.block` each | `history` and `lifetime`; `sweep`; `marker` | 0 |
| `scene` | `Quantity.block`, `Scene * Panel` | the three blocks above | 0 |

The page record's `Fixed`, `Charts`, `Status` and `Cursor` ignore their `float[]` argument and read these nodes; `Live`, `Status` and `Cursor` use their time argument through `Decay.fracAt`, as the original does. The drawing code is written again in `PagesTracked.fs` against the nodes, because the original's drawing helpers take the control row; the numbers come from `Decay.decay` and `Decay.rate`.

- [ ] **Step 1: Extend the mode first** (the failing test). Add to `tracked`, after the original's line, the same sequence on the page titled `"Atmospheric Drag Decay, tracked"`, and these checks. They fail to compile until Step 3.

| Line printed | Asserts |
|---|---|
| `decay counts \| tracked \| Initial altitude 112 \| Ballistic coeff. 1 \| Solar activity 1 \| reset after two changes 1` | the four differences equal `112, 1, 1, 1` |
| `decay typed \| a control typed to its current value \| original 0 \| tracked 0` | `typed state slot` with the current text of the coefficient, then a frame: no integration on either page |
| `decay values \| 7 settings at 2 times \| status text equal \| lifetime bits equal` | at the default row and with one control at each end of its range (altitude 250 and 600, coefficient 20 and 200, activity Low and High, each set with `typed` or `cycle` on both pages) and at `t = 1.0` and `t = 16.0`: `page.Status row t` of the two pages are equal strings, and `DecayTracked.lifetime ()` has the bits of `fst (Array.last (Decay.decay h bc a))`. Compute the reference outside the counted windows |
| `tracked \| second creation: no error` | run last, after `resetModel` on every tracked page: `graph.Run(fun () -> Api.createRoot (fun _ -> Tracked.create clock defaults))`, with `defaults` each page's control defaults, raises nothing, and one more frame draws |

- [ ] **Step 2: Build.** Expected: FAIL, `PagesTracked` is not defined.
- [ ] **Step 3: Implement** `Tracked.fs`, `DecayTracked`, the `App.fs` and `Catalog.fs` changes and the `private` removals. `Catalog.load` adds `PagesTracked.pages` to its list.
- [ ] **Step 4: Build and run** `-- --tracked`. Expected: the five lines of Tasks 2 and 3 with the values shown, `PASS  tracked pages`, exit 0.
- [ ] **Step 5: Run** `-- --bench --frames 30`. Expected: exit 0 and one page line more than Task 1 recorded; the two decay pages each report `one draw each`.
- [ ] **Step 6: Save a picture of each version.** `-- --shots <scratch folder>`; copy the two decay pages' first pictures to `docs/pictures/decay-original.png` and `docs/pictures/decay-tracked.png`. Read both pictures and confirm the spiral, the two chart panels and the marker match. A visible difference stops the task.
- [ ] **Step 7: Commit** `feat(astrotracked): Atmospheric Drag Decay on tracked quantities`.

### Task 4: Attitude Dynamics on a restartable run

**Files:**
- Modify: `examples/AstroTracked/PagesFlight.fs` (`module private Attitude` becomes `module Attitude`; `private` removed from `window`, `inertia`, `momentum`, `begin'`, `advance`, `rotation`, `box`; the counter; `stateAt`), `PagesTracked.fs`, `Harness.fs`

**Interfaces:**
- Consumes: `Run.createWith : maxStep: float -> clock: (unit -> float) -> start: (unit -> 's) -> advance: ('s -> float -> unit) -> Run<'s>` and `Run<'s>.State` (Plan 1); `Attitude.Body`, `Attitude.begin' : float[] -> Body`, `Attitude.advance : float[] -> Body -> float -> unit`.
- Produces:

```fsharp
// PagesFlight.fs, module Attitude
/// The original page's simulation state at an animation time.
val stateAt : p: float[] -> t: float -> Body          // flow.At(p, t)
// Probe.Advances <- Probe.Advances + 1 is the first statement of advance.

// PagesTracked.fs
module AttitudeTracked =
    val page : Page                  // Title "Attitude Dynamics, tracked"; wrapped with `flowing`
    /// The tracked page's simulation state at the clock's current value.
    val state : unit -> Attitude.Body
```

**The run.** `Run.createWith 0.1 clock start advance`, with `0.1` the step limit of `Flow.At`:

- `start` builds a control row from three memos created for the run alone, one per control, and calls `Attitude.begin'`. It reads the inertia as well as the axis and the tilt, so every control restarts the run, as the original's key `id` does (spec §8).
- `advance` builds a control row from the inputs and calls `Attitude.advance`. The run leaves those reads untracked.
- No other code reads the three memos. The page shows control values from the inputs. This is the rule recorded in `docs/PROBES.md`, "A control written away and back between reads": a memo that something else reads between two writes gives a restart the original does not make.

`Fixed`, `Charts`, `Live`, `Status` and `Cursor` read the inputs and `run.State`. The static chart frame and the drawing code are written again in `PagesTracked.fs`.

The rule that a quantity read only by `advance` changes a run's course without restarting it is pinned by Plan 1's test of that name on a synthetic run (`tests/Ranvier.Science.Tests/RunTests.fs`); this page cannot show it, because its `start` reads every control.

- [ ] **Step 1: Extend the mode first.** Add the script below to `tracked`. It runs once on the page titled `"Attitude Dynamics"` and once on `"Attitude Dynamics, tracked"`, each time from `goTo`, `resetModel` and clock 0. After every frame it reads the state (`Attitude.stateAt row clock` or `AttitudeTracked.state ()`) and folds the bits of `State`, `Time`, `NextSample`, every `History` row and every `Polhode` row into one 64-bit hash. The first run stores a hash per frame; the second compares. A new state object since the last frame counts as a restart.

| # | Step | Frames after it |
|---|---|---|
| 1 | nothing | 200 at `dt = 1/60` |
| 2 | `bump` the inertia up | 30 |
| 3 | `cycle` the spin axis | 30 |
| 4 | `bump` the tilt up | 30 |
| 5 | inside one `Api.batch`: `bump` the inertia up, then down | 30 |
| 6 | `typed` the tilt's current text | 30 |
| 7 | clock back to 0 | 30 |
| 8 | one frame with `dt = 5.0` | 10 |
| 9 | `goTo` the page titled `"Atmospheric Drag Decay"`, 30 frames there, `goTo` back | 30 |
| 10 | `typed` the tilt to `0.5`, then to `10` | 30 after each |
| 11 | `resetModel` | 30 |
| 12 | nothing | 1,400 at `dt = 0.1`, which passes the history trim at 664 samples |

  Lines printed and asserted:

```
attitude | <frames> frames | restarts: original <r>, tracked <r> | all states bit-identical
attitude | steps 5 and 6 | restarts: original 0, tracked 0
attitude | away for 30 frames | advances while away: original 0, tracked 0
```

  `<r>` is equal for the two versions. On the first differing frame the mode prints the step number, the frame number and both `Time` and `State` values, then fails. "Advances while away" is the difference of `Probe.Advances` across the 30 frames of step 9.
- [ ] **Step 2: Build.** Expected: FAIL, `AttitudeTracked` is not defined.
- [ ] **Step 3: Implement** the `PagesFlight.fs` edits and `AttitudeTracked`; add its page to `PagesTracked.pages`.
- [ ] **Step 4: Build and run** `-- --tracked`. Expected: every line of Tasks 2 to 4, `PASS  tracked pages`, exit 0.
- [ ] **Step 5: Run** `-- --bench --frames 30`. Expected: exit 0 and two page lines more than Task 1 recorded.
- [ ] **Step 6: Save a picture of each version** as in Task 3, to `docs/pictures/attitude-original.png` and `attitude-tracked.png`, and compare them by reading.
- [ ] **Step 7: Commit** `feat(astrotracked): Attitude Dynamics on a restartable run`.

### Task 5: Counts, comparison and frame times from one run

**Files:**
- Modify: `examples/AstroTracked/Harness.fs`
- Create: `docs/measurements/tracked-pages.md`
- Modify: `README.md`

- [ ] **Step 1: Time a control change.** Add one line to `tracked`: on each decay page, 21 changes of the ballistic coefficient (alternately up and down), each timed with `Stopwatch.GetTimestamp` around the change and the frame after it. Print `decay change | median ms over 21 changes of the ballistic coefficient | original <x> | tracked <y>`. No assertion: it is a measurement.
- [ ] **Step 2: Run the two modes once each.** `-- --tracked > <scratch>/tracked.log` and `-- --bench > <scratch>/bench.log` (default 120 frames, 20 warm-up). Both exit 0.
- [ ] **Step 3: Write `docs/measurements/tracked-pages.md`**: the machine and runtime; the two commands; every line of `tracked.log`; the four `bench.log` lines of the two decay pages and the two attitude pages, with what `--bench` times (the write of the clock until the graph settles, one draw; the window loop between frames is not timed: *from documentation*, `Harness.fs`, the `bench` function). State each comparison once and only within its run: the counts and the control-change medians from the `--tracked` run, the frame means from the `--bench` run. Label every figure *measured here*.
- [ ] **Step 4: Update `README.md`**: what `examples/AstroTracked` is, the three commands, and links to `COPY.md` and the measurement file.
- [ ] **Step 5: Root gate.** `dotnet build -c Release` with 0 warnings; both suites, unfiltered.
- [ ] **Step 6: Commit** `docs: counts, state comparison and frame times of the tracked pages`.

### Task 6: The findings report (spec §10)

Waits on Plan 2: its entries in `docs/PROBES.md` (probe B, hot reload), `docs/measurements/monitor.md` and the two pictures of the monitor page.

**Files:**
- Create: `docs/FINDINGS.md`
- Modify: `README.md` (a link)

`docs/FINDINGS.md` has these sections, in this order. Every figure is copied from a file under `docs/` and carries that file's label and name; the report measures nothing new.

1. **Answers to Q1 to Q5** (spec §1), each with its figure and source file:
   - Q1 from `docs/measurements/node-cost.md`: what a node adds per write-then-read on each body, the allocation, the range in which no body was measured, and what that leaves unsaid about the 10% grain.
   - Q2 from `docs/PROBES.md` ("Raising from `subtract`") and `docs/measurements/aggregates.md`: the float policy fits as fold state with no core change; a re-fold is requested by raising.
   - Q3 from `src/Ranvier.Quantities/Model.fs` and Plan 2's records: what an `Entry` carries, what the formula notation and the table needed beyond it.
   - Q4 from `docs/PROBES.md` ("`Run` publishing a new wrapper", "Differences from Kit.Flow") and `docs/measurements/tracked-pages.md`: `Run` is two memos; the counts; the bit-identical comparison; the rule that `start` reads a control through a memo of its own.
   - Q5 from `docs/measurements/monitor.md`: the table text on both targets, the frame work per target, the package versions each head ended on.
2. **Core proposals.** One entry per workaround, each stating what the core would offer, what it would replace in the slice, and the proof and benchmark obligations it brings (the Rocq proofs and the counter benchmarks of the core repository). The entries:
   - a float policy per aggregate;
   - a direct re-fold request in `foldGroupBy`, in place of an exception;
   - a memo that keeps its stored value when a recomputed one compares equal (`Tolerance.hold`);
   - a comparer for a stream fold's state and for `foldGroupBy`;
   - a memo whose computation reports whether it moved (`Run`'s wrapper per step);
   - an owner-scoped registry (a `Model` holds entries after their nodes' owner is disposed);
   - enumeration of a node's sources in release builds;
   - observations with no proposal yet: a signal written away and back between two reads invalidates its direct readers; the doc comment at `Ranvier/src/Ranvier/Types.fs:292` words the .NET comparer loosely; under the Expecto shim, `Expect.throwsT` for a `System` exception type accepts any exception under JavaScript, so exception types are pinned on .NET only.
3. **Interfaces to change before widening**, each with why: the unit-equality rule (`sameUnit` exact, `equivalent` within a relative `1e-12`); the burn-in of the confidence sequence; the re-fold floor; `Quantity<'u>` has a unit fixed at compile time and a formula's unit is known at run time; a model's entries outlive their nodes; the tracked pages repeat drawing code because the originals' helpers take a control row; anything Plan 2 or Tasks 3 and 4 recorded.
4. **The cost of adding a key**, from `docs/measurements/aggregates.md`: a keyed collection builds an array of all its keys once per read that follows key changes (`Collections.fs:28-41, 55-63`); what the aggregate adds over the control; and that the difference between the guide and the source on the fold's own cost is not settled by these runs.
5. **Decisions for the owner:** whether a compensated sum reads ±∞ for an infinite total; the 10% grain figure; whether the ports move into `AstroShowcase`; whether `Ranvier.Science` becomes an organisation repository.
6. **Plan defects found while building**, one line each. From Plan 1: two tests whose sample points fell on re-folds (its Task 6); `Quantity` declared sealed and then inherited (Task 10); Review Focus 1's "the state shows NaN", which does not hold for ±∞ under the fast policy; Task 13's "raises unless n > 0" beside NaN bounds for an empty sample. From Plans 2 and 3: what their ledgers record.
7. **Not verified:** each item with what would verify it. At least: hot reload of the monitor page and of a tracked page under the `dev` host (needs a window the owner asks for); a paced, visible frame-rate run on both targets; `Kit.Flow` run side by side with `Run` on inputs other than this plan's script; the PDF of arXiv 2103.06476; coverage at settings other than the two tested.
8. **Pictures:** the monitor page on each target, and each ported page beside its original.
9. **What to widen first:** one recommendation with its reason, and the two alternatives it was weighed against.

- [ ] **Step 1: Write the report.**
- [ ] **Step 2: Check it against its sources.** For each figure in the report, open the file it names and confirm the figure and the label match. For each ⚠️ in the spec (§7.3), state whether it was cleared and where. List every check in the task report.
- [ ] **Step 3: Final gate.** The root gate of Task 5; `-- --tracked` exits 0; `git status` clean after the commit.
- [ ] **Step 4: Commit** `docs: findings of the science slice`.

---

## Not done in this plan

- No visible window and no `dev` host: a tracked page under hot reload is covered only by the second-creation check of Task 3.
- The ports are native only; `AstroShowcase.Web` is not touched (spec §2, non-goals).
- `Kit.Flow` and `Run` are compared on Task 4's script, not exhaustively.
