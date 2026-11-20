# Science Slice, Plan 2: The Sequential Monitor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One page, the sequential monitor, built from one set of sources for a native window and a browser: streams folded into moments under the float policy, a confidence sequence per channel, a pooled summary, a typed formula with units, the same table text on both targets and measured frame work on each.

**Architecture:** The page's graph is a library project, `examples/Monitor/model`, with no view dependency, so the two-target test suite tests it directly. The view is one `Ranvier.View.Dsl` component in `examples/Monitor/src`, compiled into a native head (`native/`) and a browser head (`web/`) that differ only in a small platform seam. A scripted mode in the shared sources drives the mounted page in a hidden window and in a headless browser and prints the lines the acceptance reads.

**Tech Stack:** F# on .NET 10; `Ranvier` 0.2.0. Native head: `Ranvier.View` and `Ranvier.View.Dom` 0.2.0, `Ranvier.View.WebCore.Sdl` 0.2.1, `Ranvier.D5` and `Ranvier.D5.View` 0.1.1, `Ranvier.View.WebCore.Hmr` 0.1.3 in Debug (the versions `AstroShowcase/AstroShowcase.fsproj` references). Browser head: Fickl 0.10.1 (the repository's local tool), the `Ranvier.View` and `Ranvier.View.Dom` 0.2.0 packages, `Ranvier.D5` and `Ranvier.D5.View` by project reference to the `Ranvier.D5` clone; Node 26; headless Chrome. Task 1 confirms the browser head's versions or replaces them.

**Spec:** `C:\wk\ranvier-release\Ranvier\docs\superpowers\specs\2026-11-19-science-vertical-slice-design.md` (approved 2026-11-19). Section numbers below refer to it.

**Scope.** The second of three plans. Plan 1 (library, benchmark, probes) is merged at `794d8be`. Plan 3 (the AstroTracked ports and the findings report) is written beside this plan and its last task reads this plan's records. This plan covers §7.1, §7.2, the formula notation of §7.5, §7.6, and §9 steps 6 and 7.

## Core and view state

Required by `Ranvier/.claude/rules/view-uses-core.md`.

**Core primitives used.**

| Primitive | Job on the page |
|---|---|
| `createSignal`, through `Quantity.input` | Named inputs; the formula text; running and restart |
| `createMemo`, through `Quantity.derive`, `deriveWithin`, `blockFrom` | Derived quantities; the checked formula; each row's formula value; the chart scene; the band, held within a tolerance by returning its previous value |
| `createStreamFold` | One observation fold per channel, created in the row's factory |
| `createKeyedCollection`, `createProjectionWith` | The channels; one row of nodes per key, owned by the key's scope; a second projection of each row's settled moments |
| `Projection.foldGroupBy`, through `Aggregate.momentsBy` | The pooled moments, kept current per row change |
| `Projection.fold`, through `Aggregate.momentsFromScratchBy` | The from-scratch pooled moments of the diagnostics |
| `batch`, `onCleanup`, `untrack` | One publication per frame of items; cleanup of a row's feed; untracked reads in the frame step |

**View-owned state.** DOM nodes and their layout; the current DOM value of each text field; the frame callback's last timestamp and its `alive` flag. The table uses the view library's `each`, which lays rows out on a core projection of its own (`Ranvier.View.Dom/Mounting.fs:4348`); the page keeps no key matcher, cache or selection state.

**State outside the graph that is not view state.** A `Feed` (the simulated instrument: generator state, a fractional item carry, the current sink) and the counters the acceptance reads.

## Decisions made in this plan

Each is a proposal the owner can correct before or after the build. D1 to D4 answer the four questions Plan 1 left open.

| # | Decision | Why |
|---|---|---|
| D1 | A compensated sum keeps reading NaN for an infinite total. The page shows a channel whose mean is not finite as "not finite" and reads none of its statistics | The owner's call (Plan 1). The page needs no change either way |
| D2 | `FloatPolicy` gains `RefoldFloor`, 0 in `standard` and `fast`. The page pools under `standard` with a floor of 64 | Under `standard` a four-row aggregate whose rows all change each frame re-folds on every read, one raised exception per frame on .NET (*measured here*: `docs/PROBES.md`, "Raising from `subtract`") |
| D3 | The formula checker treats two units as the same when `UnitInfo.equivalent` holds: equal dimension and scales within a relative `1e-12`. `sameUnit` stays exact | Two routes to one unit can differ in the last place; no two units in use differ by so little |
| D4 | The burn-in lives in one library function, `ConfidenceSequence.ofMomentsAfter`. The page uses 30 observations | The guarantee is asymptotic; 30 is the count Plan 1's coverage test read from. Task 2 tests the page's own setting |
| D5 | The formula library is built before the page (spec §9 has it after) | The page's graph is then written once |
| D6 | Every control is a text field applied on `change`, as `AstroShowcase/App.fs:1146-1157` does | A range or number input was found in no native example |
| D7 | Plain class names and one `<style>` element; ES modules loaded without a bundler | No stylesheet tool or bundler to keep in step across two heads |
| D8 | `tests/Ranvier.Science.Tests/Recorded.fs` moves to the model project, literals unchanged | The page's scripted run needs the recorded stream on both targets |
| D9 | The chart's vertical range is the target ± 3 mK; its horizontal axis is the count on a log scale from the burn-in to 10⁷ | Fixed scales give the tenth-of-a-pixel tolerance a meaning and keep the chart from rebuilding |
| D10 | The pooled row leaves out a channel that is waiting, failed or not finite, and says how many channels it holds | One bad channel must not turn the pooled row to NaN |

## Global Constraints

- No file under `C:\wk\ranvier-release\Ranvier\src` changes. `AstroShowcase`, `AstroShowcase.Web`, `Ranvier.D5`, `fickl` and every other clone are read and never edited or built.
- Create and edit files only under `C:\wk\ranvier-release\Ranvier.Science`.
- A node holds a quantity, an array or a block: never an arithmetic operation, an observation or an array element.
- Library, model, shared page and shared test code is Fable-compatible F#: no reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`. Shared test files use only what the Expecto shim provides.
- The page keeps no dependency tracker, cache or scheduler of its own beside the core's.
- New view code uses `Ranvier.View.Dsl`.
- Every window is created hidden. The browser is headless. No task runs the window mode or the `dev` host.
- The chart's mark keys are fixed per channel: a frame that adds or removes no channel adds or removes no mark (`Ranvier.D5.View/Renderer.fs:90` puts seven listeners on each mark's group, and a mark that comes or goes costs the native window a restyle of the whole page: *from documentation*, `AstroShowcase.Web/measure/README.md`, lines 130 to 160).
- Signal writes from a frame callback run inside `graph.Run`.
- Every figure written to a file carries a label: *measured here* (with the command and what was timed), *from documentation* (with the file), or *general knowledge*. Two figures are compared only when both come from one run; a native figure is never compared with a browser figure.
- Thresholds, tolerances and expected values in this plan are used verbatim. A value that cannot be met is a finding: stop and report the observed values.
- Commits are local. No remote is added, nothing is pushed, tagged or published. Each commit message ends with the session's attribution line.
- Shell: write multi-line scripts to a file and run the file; use absolute paths; run commands plainly.
- F# inner loop: SageFs when it is available, otherwise `rfs check --fail-on-error`. Each task ends with one clean `dotnet build -c Release` and one unfiltered run of both test suites.
- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`. Prose uses British spelling.

## Review Focus

1. **A control typed out of range or not a number** (α of 0, 1 or "abc"; a tightest-at count of 0; a rate of −1 or 10¹²). Expected: the entry is refused with a message, the last value stays, nothing raises. Test 10 of Task 5.
2. **No channels.** Expected: an empty table, the pooled row waiting, α per channel equal to α, and a channel can be added again. Test 8 of Task 5.
3. **A non-finite observation, or a stream that fails, on one channel.** Expected: that row says so; the other rows and the pooled row keep finite values. Tests 11 and 12 of Task 5.
4. **A long gap between frames** (a hidden tab, a breakpoint). Expected: the next frame delivers at most a tenth of a second of items. Test 13 of Task 5.
5. **A formula that is empty, does not parse, does not check, or divides by zero.** Expected: empty clears the column; a bad one keeps the last valid values beside its message; a division by zero shows ∞ or NaN and raises nothing. Tests in Task 3 and test 9 of Task 5.

---

## File Structure

```
Ranvier.Science/
  src/Ranvier.Quantities/Formula.fs                        new
  src/Ranvier.Quantities/Units.fs  src/Ranvier.Numerics/{FloatPolicy,Aggregate}.fs  src/Ranvier.Statistics/ConfidenceSequence.fs   one addition each
  examples/Monitor/
    model/   Monitor.Model.fsproj  Recorded.fs  Format.fs  History.fs  Feed.fs  Monitor.fs     the page's graph; no view dependency
    src/     Host.fs  Plot.fs  Charts.fs  App.fs  Script.fs                                    shared by both heads
    native/  Monitor.Native.fsproj  Png.fs  Program.fs
    web/     Monitor.Web.fsproj  Browser.fs  index.html  serve.mjs  run.mjs
  tests/Ranvier.Science.Tests/   FormulaTests.fs  MonitorPartsTests.fs  MonitorTests.fs
  docs/measurements/monitor.md   docs/pictures/monitor-native.png  docs/pictures/monitor-browser.png
```

The model's namespace is `SequentialMonitor`. `Monitor.Model` references the three libraries; both test projects and both heads reference it. `Monitor.Model` and `Monitor.Native` join `Ranvier.Science.slnx`; `Monitor.Web`, like the Fable test project, does not.

Commands, run from `C:\wk\ranvier-release\Ranvier.Science`:

- **.NET suite:** `dotnet run -c Release --project tests/Ranvier.Science.Tests`
- **JavaScript suite:** `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then `npx mocha dist/tests/Main.fs.js`
- **Native scripted run:** `dotnet run -c Release --project examples/Monitor/native -- --script <name>`
- **Browser build:** `dotnet fickl examples/Monitor/web -e .fs.js -o examples/Monitor/web/dist -c Release` (or the command `docs/PROBES.md`, section B, records after Task 1)
- **Browser scripted run:** `node examples/Monitor/web/run.mjs <name>`

Read before starting: `docs/PROBES.md` (sections C, D, E and "Workarounds"); `Ranvier/src/Ranvier/Api.fs:425-440` and `588-627` (`createStreamFold`, `createProjectionWith`); `Streams.fs:14-35` and `747-750` (`StreamSink`, `StreamSource.create`); `Ranvier.View/src/Ranvier.View/Dsl.fs:346-431`; `AstroShowcase/App.fs:437-461` and `1138-1170`; `AstroShowcase/Plot.fs`; `AstroShowcase.Web/web/{Browser.fs, serve.mjs}` and `AstroShowcase.Web/measure/tools/nbody/cdp-bench.mjs`. Notes made while this plan was written, with file and line references, are in `Ranvier.Science/.superpowers/plan-research/` (`web-head-layout.md`, `view-and-d5.md`); they are scratch, and the source files are the authority.

---

### Task 1: Probe B, an empty page on both heads

**Files:**
- Create: `examples/Monitor/src/{Host.fs, App.fs, Script.fs}`; `examples/Monitor/native/{Monitor.Native.fsproj, Png.fs, Program.fs}`; `examples/Monitor/web/{Monitor.Web.fsproj, Browser.fs, index.html, serve.mjs, run.mjs}`
- Modify: `.gitignore` (`examples/Monitor/web/.chrome-profile/`, `examples/Monitor/web/probe.log`, `examples/Monitor/out/`), `Ranvier.Science.slnx`, `docs/PROBES.md` (section B)

**Interfaces:**
- Produces:

```fsharp
namespace SequentialMonitor

/// What the page needs from the window or the browser tab it runs in.
type IPlatform =
    abstract Document: Document
    /// Calls back before the next frame is shown.
    abstract RequestFrame: callback: (float -> unit) -> unit
    /// A setting given from outside, or null: `--script frames` for a window, `?script=frames` for a page.
    abstract Variable: name: string -> string
    /// Milliseconds on a monotonic clock.
    abstract Now: unit -> float
    /// Writes one line of a scripted run: standard output for a window, the local server's log for a page.
    abstract Report: line: string -> unit
    /// Saves a picture of the page under `name` and returns a line saying where it went.
    abstract SavePicture: name: string -> string

/// What one mount shares with the code that drives it.
type Session =
    new : unit -> Session
    /// The mounted page, set by its setup.
    member Page : obj with get, set
    /// False while a scripted run calls the frames itself.
    member Auto : bool with get, set

type Props = { Platform: IPlatform; Session: Session }

module App =
    val app : Component<Props>          // Component.template setup view
module Script =
    /// Runs the named script against the page mounted for `props` and returns its exit code.
    /// Its last line is `script done <code>`.
    val run : props: Props -> name: string -> int
```

In this task the page is a placeholder: one element with `id "probe"` whose text is bound to a signal holding `"probe page"`, and one `Renderer.passive` scene holding one text mark. The script `hello` reports `hello | <the element's text> | marks <n>`.

- The native head creates `Window.Create(title, 1280, 900, hidden = true)`, mounts with `Mounting.mount graph None report container None (fun ctx -> Program.run (Component.render App.app props) ctx.Backend)`, and, with `--script <name>`, steps the window until the page has drawn, runs the script and returns its code. With `dev` in a Debug build it calls `Ranvier.View.WebCore.Hmr.Dev.RunWith`. With no argument it shows the window; no task runs that. `Png.fs` is a copy of `AstroShowcase/Png.fs` under the new namespace; `SavePicture` writes `examples/Monitor/out/<name>.png` from `window.ReadPixels()`.
- The browser head reads `?script=`, posts each `Report` line to `POST /log`, and returns a fixed line from `SavePicture`. `serve.mjs` is a copy of `AstroShowcase.Web/web/serve.mjs`. `index.html` loads the entry module with `<script type="module">`.
- `run.mjs <name> [--picture <png>]` starts `serve.mjs` on a free port, empties `probe.log`, and launches Chrome (`CHROME`, or `C:\Program Files\Google\Chrome\Application\chrome.exe`) with `--headless=new --user-data-dir=<web>/.chrome-profile --no-first-run --no-default-browser-check --disable-features=Translate --window-size=1280,900`. Without `--picture` it adds `--remote-debugging-port=0`, waits up to 180 s for `script done` in the log and stops Chrome. With `--picture` it adds `--screenshot=<png> --virtual-time-budget=20000` and waits for Chrome to exit. It prints the log and exits with the script's code.

- [ ] **Step 1: Scaffold** the files above. The native project references the packages of the Tech Stack line; the browser project references `Ranvier.View` and `Ranvier.View.Dom` 0.2.0, `Fable.Core` 5.3.0, and `../../../../Ranvier.D5/src/Ranvier.D5.View/Ranvier.D5.View.fsproj`.
- [ ] **Step 2: Native half.** `dotnet build -c Release`, then `-- --script hello`. Expected: `hello | probe page | marks 1`, `script done 0`, exit 0.
- [ ] **Step 3: Browser half.** The browser build, then `node examples/Monitor/web/run.mjs hello`. Expected: the same two lines, exit 0.
- [ ] **Step 4: If Step 3 fails, try in this order and stop at the first that passes**, recording each attempt and its first error line: (a) add `--noCache` to the build; (b) compile with the compiler already built in the sibling clone, as `AstroShowcase.Web/web/package.json` runs it (`dotnet ../../../../fickl/src/Fable.Cli/bin/Release/net10.0/fable.dll ... --noCache --noPackageCache`), reading that clone's build output only; (c) pin `Ranvier.View` and `Ranvier.View.Dom` to 0.1.50 in the browser project alone (spec §11); (d) if the modules compile and the browser cannot load them unbundled, bundle with `rolldown` 1.2.14 as `AstroShowcase.Web` does. If none passes, report `BLOCKED` with the attempts.
- [ ] **Step 5: Record section B of `docs/PROBES.md`**: the question; the two commands; the output lines; the package versions each head ended on; which fallback, if any, was needed; and the state of the `Ranvier.D5` clone at build time (`git -C ../Ranvier.D5 status --short` and `log --oneline -1`), labelled *measured here*. The later tasks use the browser build command this section records.
- [ ] **Step 6: Both suites**, unfiltered. **Commit** `chore(monitor): an empty page on a native head and a browser head (probe B)`.

### Task 2: Three library additions the page needs (D2, D3, D4)

**Files:**
- Modify: `src/Ranvier.Quantities/Units.fs`, `src/Ranvier.Statistics/ConfidenceSequence.fs`, `src/Ranvier.Numerics/FloatPolicy.fs`, `src/Ranvier.Numerics/Aggregate.fs`; `tests/Ranvier.Science.Tests/{UnitsTests.fs, ConfidenceSequenceTests.fs, AggregateTests.fs}`; `docs/PROBES.md` ("Coverage")

**Interfaces:**
- Produces:

```fsharp
module UnitInfo =
    /// Same dimension, and scales equal within a relative 1e-12.
    val equivalent : a: UnitInfo -> b: UnitInfo -> bool

module ConfidenceSequence =
    /// `ofMoments`, with NaN bounds while the count is below `burnIn`. Raises ArgumentOutOfRangeException
    /// unless `burnIn` is finite and at least 1, and as `ofMoments` does.
    val ofMomentsAfter : burnIn: float -> alpha: float -> rho: float -> Moments -> Interval

type FloatPolicy =
    { Compensated: bool
      RefoldAfterRemovals: bool
      /// With RefoldAfterRemovals, the removals since the last re-fold must reach this many as well as the row count.
      RefoldFloor: int }

module FloatPolicy =
    // standard and fast keep their behaviour: RefoldFloor = 0.
    /// `policy` with the given floor. Raises ArgumentOutOfRangeException for a negative floor.
    val withRefoldFloor : floor: int -> policy: FloatPolicy -> FloatPolicy
```

`Aggregate.maintain` re-folds when `policy.RefoldAfterRemovals && removals >= max s.Rows policy.RefoldFloor`.

- [ ] **Step 1: Write the failing tests.**

| List | Test name | Asserts |
|---|---|---|
| `Units` | `equivalent holds across routes that differ in the last place` | for `u` of scale `0.7`: `sameUnit (sqrt (power u -2)) (power u -1)` is false (the pair `docs/PROBES.md` names) and `equivalent` is true |
| `Units` | `equivalent separates sizes and dimensions` | false for millikelvin and kelvin, for `kilometre / metre` and `dimensionless`, for metre and second; true for a unit and itself |
| `ConfidenceSequence` | `no interval before the burn-in` | 29 observations with `burnIn = 30.0`: both bounds NaN; 30 observations: bounds have the bits of `ofMoments` |
| `ConfidenceSequence` | `a burn-in out of range is rejected` | `throwsT<System.ArgumentOutOfRangeException>` for `0.5`, `nan`, `infinity` |
| `ConfidenceSequence` | `the sequence covers at the page's setting` | below |
| `Aggregate` | `a floor delays the re-fold of a small aggregate` | four rows; 256 edits of one row each, a read after each: `Refolds ()` is 4 under `standard |> withRefoldFloor 64` and 64 under `standard` |
| `Aggregate` | `a floored aggregate equals the from-scratch one after each re-fold` | in the run above, at each read where `Refolds ()` rose, the maintained moments have the bits of `momentsFromScratchBy`; at every other read mean and variance are within the bound the existing test of guarantee 4 in this file uses |
| `Aggregate` | `a negative floor is rejected` | `throwsT<System.ArgumentOutOfRangeException>` |

  The coverage test at the page's setting: as Plan 1's, with `alpha = 0.05 / 4.0` and `rho = rhoTightestAt alpha 1000.0`, 2,000 streams of 10,000 standard normal deviates from seeds `1..2000`, read through `ofMomentsAfter 30.0`. A stream misses when its interval excludes 0 at any count. Assert the miss fraction is `<= 0.0225` (α plus a margin of 0.01). The test prints the fraction.
- [ ] **Step 2: Run** on .NET. Expected: FAIL to compile.
- [ ] **Step 3: Implement** the three additions.
- [ ] **Step 4: Run both suites.** Expected: PASS. If the coverage fraction exceeds `0.0225`, change nothing: report `DONE_WITH_CONCERNS` with the fraction on each target, because D4's burn-in of 30 then needs the owner.
- [ ] **Step 5: Record** the fraction per target under "Coverage" in `docs/PROBES.md`, with its setting, labelled *measured here*.
- [ ] **Step 6: Commit** in three commits: `feat(quantities): unit equivalence within a tolerance`, `feat(statistics): a confidence sequence that waits for a burn-in`, `feat(numerics): a floor on the removals before a re-fold`.

### Task 3: The formula notation (spec §7.5)

**Files:**
- Create: `src/Ranvier.Quantities/Formula.fs` (after `Quantity.fs`), `tests/Ranvier.Science.Tests/FormulaTests.fs`

**Interfaces:**
- Consumes: `Model.TryFind`, `Entry.Unit`, `Entry.Read`, `UnitInfo.multiply`, `divide`, `power`, `sqrt`, `symbol`, `equivalent`.
- Produces:

```fsharp
namespace Ranvier.Quantities

/// One line of the formula notation, parsed.
type Expr =
    | Number of float
    | Name of string
    | Negate of Expr
    | Add of Expr * Expr
    | Subtract of Expr * Expr
    | Multiply of Expr * Expr
    | Divide of Expr * Expr
    | Power of Expr * int
    | Call of string * Expr list

module Formula =
    /// The tree of `text`, or a message with the 1-based character where reading stopped.
    val parse : text: string -> Result<Expr, string>
    /// The unit of `expr` over the identifiers of `model`, or a message naming the operation and the units it met.
    val check : model: Model -> expr: Expr -> Result<UnitInfo, string>
    /// A function that evaluates `expr` with tracked reads of the entries of `model`.
    /// Raises ArgumentException for an identifier `model` lacks.
    val evaluator : model: Model -> expr: Expr -> (unit -> float)
```

**Grammar.** `expr := term (('+' | '-') term)*`; `term := unary (('*' | '/') unary)*`; `unary := '-' unary | power`; `power := atom ('^' integer)?`, the integer optionally signed; `atom := number | name | name '(' expr (',' expr)* ')' | '(' expr ')'`. `−`, `×`, `·` and `÷` are read as `-`, `*`, `*` and `/`. A number is digits, an optional `.` and digits, and an optional exponent. Functions: `sqrt abs exp ln log10` take one argument, `min max` take two. Spaces are ignored. Nothing else is in the notation.

**Units.** A number is dimensionless. `+`, `-`, `min` and `max` need `UnitInfo.equivalent` operands; the message for the same dimension at different sizes names both units and says the notation does not convert. `exp`, `ln` and `log10` need an operand equivalent to `UnitInfo.dimensionless`. `sqrt` follows `UnitInfo.sqrt`. `*`, `/` and `^` follow `UnitInfo.multiply`, `divide` and `power`.

**Evaluation.** IEEE arithmetic with no exception: a division by zero gives an infinity or NaN. An integer power is repeated multiplication, and its reciprocal for a negative power, so both targets round alike.

- [ ] **Step 1: Write the failing tests** (`"Formula"`), over a model with `mean`, `se`, `target` in mK, `variance` in mK², `n` and `alpha` dimensionless, `rate` in 1/s and `temp_k` in K.

| Test name | Asserts |
|---|---|
| `precedence and association` | `1 + 2 * 3` is `Add (Number 1.0, Multiply (Number 2.0, Number 3.0))`; `8 - 2 - 1` is `Subtract (Subtract (..), Number 1.0)`; `-x^2` is `Negate (Power (Name "x", 2))`; `x^-2` is `Power (Name "x", -2)` |
| `the page's formula` | `(mean - target) / se` is `Divide (Subtract (Name "mean", Name "target"), Name "se")` |
| `calls and numbers` | `min(a, max(b, 1.5e-3))` is `Call ("min", [ Name "a"; Call ("max", [ Name "b"; Number 0.0015 ]) ])`; `0.1` parses to `Number 0.1` exactly |
| `typographic operators` | `a × b ÷ c − d` equals the tree of `a * b / c - d` |
| `text that is not a formula` | `Error` for `""`, `"   "`, `"1 +"`, `"(1"`, `"1 2"`, `"x^1.5"`, `"2^3^2"`, `"foo(1)"`, `"sqrt(1, 2)"`, `"a $ b"`, `".5"`; the message for `"1 +"` contains `4` |
| `units of the page's formula` | `check` of `(mean - target) / se` is `Ok u` with `equivalent u UnitInfo.dimensionless` |
| `sums need one unit` | `Error` for `mean + temp_k` (message contains `mK`, `K` and `convert`), `mean + rate`, `mean + 1`, `min(mean, n)` |
| `products, powers and roots` | `2 * mean` is mK; `mean^2` is equivalent to the unit of `variance`; `sqrt(variance)` is equivalent to mK; `sqrt(mean)` is `Error`; `abs(mean)` and `-mean` are mK |
| `transcendental functions need a pure number` | `Error` for `exp(mean)`; `Ok` dimensionless for `exp(mean / se)` and `ln(n)` |
| `an unknown name` | `check` of `mean + nope` is `Error` whose message contains `nope` |
| `evaluation tracks its reads` | a memo over `evaluator model e ()` for the page's formula has the bits of the F# expression, and recomputes once when `mean` is set |
| `integer powers multiply` | `x^3` has the bits of `x * x * x`, `x^-2` of `1.0 / (x * x)`, `x^0` is `1.0`, for `x = 0.7` |
| `no exception from arithmetic` | `1 / (n - n)` is `infinity`; `ln(0 - 1)` is NaN |
| `a row's name hides the page's` | in `Model.child`, `mean` reads the child's entry and `alpha` the parent's |
| `a pending entry leaves the formula pending` | a memo over an evaluator that reads a quantity over a pending stream reports `Pending` through `TryValue` |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Formula` is not defined.
- [ ] **Step 3: Implement** `Formula.fs`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): a one-line formula notation with units`.

### Task 4: The parts of the page's graph: formatting, history, feed

**Files:**
- Create: `examples/Monitor/model/{Monitor.Model.fsproj, Format.fs, History.fs, Feed.fs}`, `tests/Ranvier.Science.Tests/MonitorPartsTests.fs`
- Move: `tests/Ranvier.Science.Tests/Recorded.fs` to `examples/Monitor/model/Recorded.fs` with `git mv`; its module becomes `SequentialMonitor.Recorded` and no literal changes
- Modify: `tests/Ranvier.Science.Tests/TestFiles.props`, `CrossTargetTests.fs` (the `open`), both test projects (a reference to `Monitor.Model`), both `Main.fs` files, `Ranvier.Science.slnx`

**Interfaces:**
- Produces:

```fsharp
namespace SequentialMonitor

module Format =
    /// `x` rounded half away from zero to `decimals` places: digits, ".", and a leading "-" unless every digit is 0.
    /// "NaN", "∞" and "-∞" for the rest. The same text on .NET and in JavaScript.
    val fixed' : decimals: int -> x: float -> string
    /// A whole number with "," between thousands.
    val count : n: float -> string
    /// The number written by `text`, read as the formula notation reads a number with an optional sign.
    val tryNumber : text: string -> float voption

[<Struct>]
type Sample = { Count: float; Mean: float; Sd: float }

/// Samples of a fold at geometrically spaced counts. Immutable.
type History                                  // representation private
module History =
    val Capacity : int                        // 512
    val empty : History
    /// `h` with a sample of `m` when its count has reached the next sampling count; `h` itself otherwise.
    val add : m: Moments -> h: History -> History
    /// Oldest first. The array is the stored instance.
    val samples : History -> Sample[]

/// What a channel has folded so far.
type Folded = { Moments: Moments; History: History }
module Folded =
    val zero : Folded
    val add : f: Folded -> x: float -> Folded      // Moments.add true, then History.add

/// A simulated instrument: normal deviates from a seed, delivered when asked.
[<Sealed>]
type Feed =
    new : unit -> Feed
    /// A source whose every start begins the sequence again from `seed`.
    member Source : mean: float * sd: float * seed: int -> StreamSource<float>
    /// Delivers `rate * dt` deviates to the current start, carrying the fraction to the next call. Returns the number delivered.
    member EmitAtRate : rate: float * dt: float -> int
    /// Delivers the given values to the current start.
    member Push : values: float[] -> unit
    /// Fails the current start.
    member Fail : error: exn -> unit
    member Starts : int
    member Delivered : int
```

`Format` uses multiplication, addition and `floor` on doubles and builds digits itself; it calls no `ToString` format and no `toFixed`. Sampling rule of `History`: the first sample is at a count of 1; after a sample at count `c` the next is due at `max (c + 1) (ceil (c * 1.05))`; at `Capacity` the newest sample replaces the last. `Feed.Source` is built on `StreamSource.create`; its deviates come from `Generator.normal`. `EmitAtRate` is called on the graph thread; `Push` is called on the graph thread or on one other thread at a time.

- [ ] **Step 1: Write the failing tests** (`"MonitorParts"`).

| Test name | Asserts |
|---|---|
| `fixed rounds half away from zero` | `fixed' 2 0.84 = "0.84"`, `fixed' 2 -0.016 = "-0.02"`, `fixed' 2 -0.004 = "0.00"`, `fixed' 1 9.25 = "9.3"`, `fixed' 0 2.5 = "3"`, `fixed' 0 -2.5 = "-3"`, `fixed' 2 1000000000.125 = "1000000000.13"` |
| `fixed names the values it cannot write` | `"NaN"`, `"∞"`, `"-∞"` |
| `count groups thousands` | `"999"`, `"12,400"`, `"1,234,567"`, `"0"` |
| `tryNumber` | `ValueSome 0.05` for `"0.05"`, `ValueSome -1.0` for `"-1"`, `ValueSome 1000.0` for `" 1e3 "`; `ValueNone` for `""`, `"abc"`, `"1,5"`, `"0x10"` |
| `history samples at spaced counts` | after counts `1..100000`: the first sample count is `1.0`; counts rise strictly; each is at least `1.05` times the one before or one more than it; the number of samples equals a literal taken from the .NET run |
| `history returns itself between samples` | `add` at a count below the next sampling count returns the same instance, and `samples` the same array |
| `history stops growing at its capacity` | after counts up to `1e13` in steps that always sample, `samples` has `Capacity` elements and the last has the latest count |
| `a feed delivers a seeded sequence and restarts it` | a `createStreamFold Folded.zero Folded.add` over `feed.Source (0.0, 1.0, 7)`: after `EmitAtRate (1024.0, 0.015625)` twice, the count is 32 (`16 + 16`) and `Delivered` is 32; a second start (a signal read by the start changes) gives the same first 16 values |
| `a feed carries the fraction` | `EmitAtRate (96.0, 0.015625)`, which is 1.5 items a call, returns `1, 2, 1, 2` over four calls |
| `a failed feed fails its fold` | after `Fail (exn "boom")`, `TryValue` is `Failed` with that message |

- [ ] **Step 2: Run** on .NET. Expected: FAIL to compile.
- [ ] **Step 3: Implement** the three files; paste the history literal from the .NET run.
- [ ] **Step 4: Run both suites.** Expected: PASS, including `CrossTarget` after the move.
- [ ] **Step 5: Commit** `feat(monitor): formatting, a spaced history and a simulated feed`.

### Task 5: The page's graph (spec §7.2)

**Files:**
- Create: `examples/Monitor/model/Monitor.fs`, `tests/Ranvier.Science.Tests/MonitorTests.fs`

**Interfaces:**
- Consumes: Tasks 2 to 4; `Quantity.*`, `Model.child`, `Tolerance.absolute`, `Tolerance.holdSeries`, `Aggregate.momentsBy`, `Aggregate.momentsFromScratchBy`, `ConfidenceSequence.rhoTightestAt`, `FixedSample.ofMoments`, `Units.millikelvin`, `Units.one`, `Units.second`.
- Produces:

```fsharp
namespace SequentialMonitor

[<RequireQualifiedAccess>]
type RowState =
    | Waiting
    | NotFinite
    | Failed of message: string
    | Showing

/// Recomputations of one row's nodes since it was created.
type RowCounts =
    { mutable Starts: int; mutable Means: int; mutable Bounds: int; mutable Bands: int; mutable Settled: int }

/// A channel's chart series, one element per history sample at or past the burn-in.
type Band =
    { Counts: float[]; Mean: float[]; Lower: float[]; Upper: float[]; FixedLower: float[]; FixedUpper: float[] }

type Row =
    { Key: string
      Model: Model                                  // a child of the page's model
      TrueMean: Input<mK>; TrueSd: Input<mK>; Seed: Input<1>
      Feed: Feed
      Observations: StreamMemo<Folded>
      State: unit -> RowState                        // a tracked read
      N: Quantity<1>; Mean: Quantity<mK>; Variance: Quantity<mK^2>; Se: Quantity<mK>
      Lower: Quantity<mK>; Upper: Quantity<mK>
      Band: Block<Band>
      User: Memo<float>                              // the formula's value on this row; NaN with no valid formula
      /// The row's six table cells: channel, n, mean, standard error, sequence, formula. A tracked read.
      Cells: unit -> string[]
      Counts: RowCounts }

type Checked = { Expr: Expr; Unit: UnitInfo }

/// The formula as typed, the last one that parsed and checked, and why the typed one did not.
type FormulaState = { Text: string; Valid: Checked voption; Error: string voption }

type Diagnostics = { Items: int; Rows: int; Restarts: int }      // of the frame before the last `frame` call

type Monitor =
    { Model: Model
      Alpha: Input<1>; TightestAt: Input<1>; Target: Input<mK>; Rate: Input<1/s>
      AlphaPerChannel: Quantity<1>
      Running: Signal<bool>
      Rows: Projection<string, Row>
      Pooled: Maintained<Moments>
      PooledFromScratch: Memo<Moments>
      FormulaText: Signal<string>
      Formula: Memo<FormulaState>
      Diagnostics: Signal<Diagnostics>
      mutable AlphaSplits: int }

module Monitor =
    val BurnIn : float                     // 30.0
    val ChartHalfRange : float             // 3.0, in mK about the target
    val ChartPlotHeight : float            // 320.0, in pixels
    /// The page's graph with no channel, under the current owner.
    val create : unit -> Monitor
    /// Adds the next channel (A, B, C ...) and returns its key.
    val addChannel : Monitor -> string
    val removeChannel : Monitor -> key: string -> unit
    /// Starts every channel again from its seed.
    val restart : Monitor -> unit
    /// The rows in channel order. A tracked read of the keys.
    val rows : Monitor -> Row[]
    /// One frame of `dt` seconds, limited to 0.1: every feed delivers at the page's rate, in one batch.
    val frame : Monitor -> dt: float -> unit
    /// Sets an input from the text of its field, or says why not. The input keeps its value on Error.
    val setAlpha : Monitor -> text: string -> Result<unit, string>           // 0 < α < 1
    val setTightestAt : Monitor -> text: string -> Result<unit, string>      // finite and at least 1
    val setTarget : Monitor -> text: string -> Result<unit, string>          // finite
    val setRate : Monitor -> text: string -> Result<unit, string>            // 0 to 1,000,000
    /// The pooled row's cells. A tracked read.
    val pooledCells : Monitor -> string[]
    /// Units in the last place between the maintained and the from-scratch pooled mean. A tracked read.
    val pooledDistance : Monitor -> float
```

**How the nodes are built.** Defaults: α `0.05`, tightest at `1000`, target `0.0` mK, rate `1000` /s. Identifiers: `alpha`, `tightest_at`, `target`, `rate`, `alpha_per_channel` on the page; `true_mean`, `true_sd`, `seed`, `n`, `mean`, `variance`, `se`, `lower`, `upper` on a row. A new channel has `true_sd` 10 mK, a seed equal to its position counted from 1, and `true_mean` `0.84`, `-0.02`, `0.0`, `0.3` mK for the first four and `0.0` after.

| Node | Built with | Reads | Counter |
|---|---|---|---|
| channels | `createKeyedCollection id` over the keys | | |
| `Rows` | `createProjectionWith`; every row node is created in the factory body, in a `Model.child` of the page's model made there; the reader returns the `Row` | | |
| `Observations` | `createStreamFold Folded.zero Folded.add`, whose start returns `feed.Source (true_mean, true_sd, seed)` and reads the restart signal | the three source inputs | `Starts` |
| `n`, `mean`, `variance`, `se` | `Quantity.derive`; `variance` and `se` are NaN when the mean is not finite | `Observations` | `Means`, in `mean` |
| `alpha_per_channel` | `Quantity.derive`: `alpha / max 1 (number of channels)` | `alpha`, the keys | `AlphaSplits` |
| the sequence | one `Quantity.block` of `ConfidenceSequence.ofMomentsAfter BurnIn alpha_per_channel (rhoTightestAt alpha_per_channel tightest_at)`; `lower` and `upper` derive from it | the moments, `alpha_per_channel`, `tightest_at` | `Bounds` |
| `Band` | `Quantity.blockFrom`: the series from the history samples at or past the burn-in, each array held with `Tolerance.holdSeries (Tolerance.absolute (0.1 * 2.0 * ChartHalfRange / ChartPlotHeight))`; the previous `Band` is returned when every array was held | the history, `alpha_per_channel`, `tightest_at` | `Bands` |
| settled moments | a second `createProjectionWith` over `rows`, whose reader returns the row's `Moments` when its state is `Showing` and `Moments.zero` otherwise | `Observations` | `Settled` |
| `Pooled`, `PooledFromScratch` | `Aggregate.momentsBy` and `momentsFromScratchBy` with `id`, under `FloatPolicy.standard \|> FloatPolicy.withRefoldFloor 64` | the settled moments | |
| `Formula` | `createMemo` with its previous value: parse, then `Formula.check` against a schema model that declares the row identifiers with the rows' units; an empty text clears `Valid`; a text that fails keeps the previous `Valid` and sets `Error` | `FormulaText` | |
| `User` | two memos per row: the evaluator bound to the row's model, and its value | `Formula`, the names it uses | |

A row's state comes from one tracked read of `Observations.TryValue`. The page does not read `Entry.Status` per quantity: an effect that reads it re-runs on every change of the value (Plan 1, `docs/PROBES.md`).

**Cells.** `n` through `Format.count`; mean and standard error through `Format.fixed' 2` followed by a space and the unit's symbol; the sequence as `[lo, hi] mK`, or `after 30 observations` before the burn-in; the formula through `Format.fixed' 1`, or empty. A waiting row is `[ key; "waiting for data"; ""; ""; ""; "" ]`; a failed one puts `failed: <message>` in the second cell; a not-finite one shows its count and `not finite` in the third. The pooled row is `All`, its count, mean, standard error and `(pooled, descriptive)`, or `(pooled, k of m channels)` when it leaves some out.

- [ ] **Step 1: Write the failing tests** (`"Monitor"`). Each runs inside `withGraph`; items reach a row through `row.Feed.Push` inside `batch`; "reading the page" means reading every row's `Cells`, every row's `Band.Value` and `pooledCells`.

| # | Test name | Asserts |
|---|---|---|
| 1 | `a channel waits for its first item` | state `Waiting` and the waiting cells; after one item `Showing` |
| 2 | `the recorded stream gives the pinned table text` | channels A and B; A receives `Recorded.stream`, B receives each value `x` as `2.0 - x`; formula `(mean - target) / se`. The three lines (A, B, All; cells joined by `" \| "`) equal three literals, pasted from the .NET run and then run under JavaScript |
| 3 | `changing alpha folds nothing and recomputes each channel's bounds and band once` | two channels of 2,000 items; α `0.05` to `0.01`; after reading the page, per row: `Bounds` +1, `Bands` +1, `Starts` +0, `Means` +0; `Feed.Delivered` unchanged; `Observations.Peek` is the same instance |
| 4 | `adding a channel recomputes the split and re-runs no fold` | `AlphaSplits` +1; per existing row `Bounds` +1, `Starts` +0, `Observations.Peek` the same instance |
| 5 | `a publication on one channel recomputes that row alone and edits the pooled moments once` | one item to A: A has `Means` +1, `Bounds` +1, `Settled` +1; B has +0 for all three; `Pooled.Refolds ()` unchanged |
| 6 | `a source parameter restarts that channel from its seed` | setting A's `TrueMean`: A's `Starts` +1 and its count back to the items delivered since; B untouched |
| 7 | `removing a channel cancels its stream` | the removed row's feed receives no further start; the pooled moments have the bits of the from-scratch ones; `alpha_per_channel` rises |
| 8 | `no channels` | after removing the last: `rows` is empty, `pooledCells` are the waiting cells for `All`, `AlphaPerChannel` equals `Alpha`; `addChannel` then gives a waiting row |
| 9 | `a formula that fails keeps the last valid values` | `mean + rate` after a valid formula: `Valid` is the previous instance, `Error` is `ValueSome`, every row's `User` unchanged; an empty text gives `Valid = ValueNone`, `Error = ValueNone` and an empty cell |
| 10 | `a control out of range is refused` | `setAlpha` for `"0"`, `"1"`, `"abc"`; `setTightestAt` for `"0"`; `setRate` for `"-1"` and `"1e12"`; `setTarget` for `"nan"`: each `Error`, the input unchanged, nothing recomputed |
| 11 | `a non-finite observation marks its channel alone` | NaN to A: A is `NotFinite` with `not finite` in its cells; B's cells unchanged; the pooled row has finite values and says `1 of 2 channels` |
| 12 | `a failed stream shows its message` | `Feed.Fail (exn "boom")`: the row is `Failed "boom"`; reading the page raises nothing |
| 13 | `a long frame is limited, and a paused page delivers nothing` | with the rate set to `1024`, `frame m 60.0` delivers 102 items per channel (`floor (1024 × 0.1)`); with `Running` false a frame delivers none |
| 14 | `the sequence waits for the burn-in` | at 29 items the sequence cell is `after 30 observations` and the band has no element; at 30 it shows numbers |
| 15 | `a small change of alpha returns the same band` | α `0.05` to `0.05000001`: `Band.Value` is the same instance; to `0.01`: a new one |
| 16 | `pooled, maintained and from scratch` | four channels, 600 calls of `frame m (1.0 / 60.0)` each followed by a read of the page: `pooledDistance` is 0 at each read where `Refolds ()` rose and within the bound of Task 2's aggregate test at every other; `Refolds ()` is at most 40 at the end |
| 17 | `restart begins every channel from its seed` | after `restart`: every `Starts` +1, and the first 16 observations of A equal those of its first start |
| 18 | `the schema covers a row` | every identifier a row's model declares is in the schema model with an equivalent unit |
| 19 | (.NET only) `items from a second thread fold to the same state` | 10,000 values given to `row.Feed.Push` inside `Support.offThread`: once the graph has taken them in, the count is 10,000 and the moments have the bits of a same-thread fold |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Monitor` is not defined.
- [ ] **Step 3: Implement** `Monitor.fs`; paste the three lines of test 2 from the .NET run.
- [ ] **Step 4: Run both suites.** Expected: PASS, with test 2's literals unchanged under JavaScript. A line that differs under JavaScript is a finding: record both lines in `docs/PROBES.md` and stop for review (spec §7.6).
- [ ] **Step 5: Commit** `feat(monitor): the page's graph, with its reactivity counts pinned on both targets`.

### Task 6: The view and the native head (spec §7.1, §7.6)

**Files:**
- Create: `examples/Monitor/src/{Plot.fs, Charts.fs}`
- Modify: `examples/Monitor/src/{App.fs, Script.fs}`, `examples/Monitor/native/{Monitor.Native.fsproj, Program.fs}`

**Interfaces:**
- Consumes: Task 5; `Renderer.passive : Component<unit -> Scene>`; `each`, `show`, `render`, `Component.template` of `Ranvier.View.Dsl`.
- Produces:

```fsharp
module SequentialMonitor.Charts
    /// "<name> [<unit symbol>]", from a quantity's entry.
    val axisLabel : entry: Entry -> string
    /// The chart of every channel: per channel a band, a mean line and the two lines of the fixed-sample interval,
    /// and one line at the target. A tracked read of each row's Band and of the target.
    val scene : Monitor -> Scene

module SequentialMonitor.App
    type Page = { Monitor: Monitor; Scene: Memo<Scene>; Messages: Signal<string>; Props: Props; mutable SceneBuilds: int }
    val app : Component<Props>
```

`Plot.fs` is a copy of `AstroShowcase/Plot.fs` (it needs `Ranvier.D5` alone), under the new namespace, with a first comment saying so. `Charts.scene` builds one `panelWith` with a `Log` horizontal axis over `(Monitor.BurnIn, 1e7)` and a `Linear` vertical axis over the target ± `ChartHalfRange`, labelled through `axisLabel`. Mark keys per channel are `band/<key>`, `mean/<key>`, `fixlo/<key>`, `fixhi/<key>`. A channel with no band element still has its four marks, each a path of one `MoveTo` at the plot's lower-left corner. Points that are not finite are dropped before they reach a scale (`Scale.map` raises on them). The legend names the band `confidence sequence` and the thin lines `fixed-sample interval: valid at one planned n only` (spec §7.1).

The view follows the layout of spec §7.1: the title with Pause and Restart; the four controls; the table, one row per channel through `each (fun (row: Row) -> row.Key) (fun (page: Page) -> Monitor.rows page.Monitor) rowComponent`, the row component reading only its `Row`; the pooled row; add and remove (remove takes the last channel); the formula field with its unit, `ok`, or `stale` and the message; the chart through `render Renderer.passive`; the diagnostics. A refused control entry writes the field back and shows its message in `Messages`. `setup` creates the monitor with four channels, stores the page in `props.Session.Page`, and starts the frame callback, which calls `Monitor.frame` inside `graph.Run` with the elapsed seconds while `props.Session.Auto` is true; a scripted run sets it false and calls the frames itself. `Page.SceneBuilds` counts computations of the `Scene` memo.

Scripts added to `Script.run`:

| Name | Does | Lines |
|---|---|---|
| `table` | stops the frame callback; rebuilds the page as test 2 of Task 5 does; compares each row's cells with the three literals of that test, and each table row's `textContent` with its cells joined by nothing | `table \| A \| ...` per row, then `table \| pinned text: equal \| DOM text: equal` |
| `picture` | `table`, then `SavePicture "monitor"` | the line `SavePicture` returns |
| `counts` | on the default page of four channels after 120 frames at `1/60`: the three expectations of tests 3, 4 and 5 of Task 5, read after the view has drawn with no read by the script; `SceneBuilds` +0 over a frame that appends no history sample (the script steps frames until one leaves every row's sample count unchanged) and over α `0.05` to `0.05000001`, +1 over α to `0.01`; the mark keys of two consecutive scenes are equal, and adding a channel adds four keys | one `counts \| ...` line each, then `counts \| all as expected` |

- [ ] **Step 1: Write the three scripts first.**
- [ ] **Step 2: Build.** Expected: FAIL to compile, `App.Page` is not defined.
- [ ] **Step 3: Implement** `Plot.fs`, `Charts.fs` and the view.
- [ ] **Step 4: Run** `-- --script table`, `-- --script counts`, `-- --script picture`. Expected: each ends `script done 0`. Copy the picture to `docs/pictures/monitor-native.png` and read it: the table, the formula line, the chart with four bands and the diagnostics are on it.
- [ ] **Step 5: Build the Debug configuration** of the native head, which compiles the `dev` entry. Expected: 0 errors. Do not run it.
- [ ] **Step 6: Both suites**, unfiltered. **Commit** `feat(monitor): the page in a native window, with scripted table, counts and picture`.

### Task 7: The browser head (spec §7.6)

**Files:**
- Modify: `examples/Monitor/web/{Monitor.Web.fsproj, Browser.fs, index.html, run.mjs}`

- [ ] **Step 1: Build** the browser head with the command section B of `docs/PROBES.md` records. Expected: `Fable compilation finished`, no error.
- [ ] **Step 2: Run** `node examples/Monitor/web/run.mjs table`, then `counts`. Expected: each ends `script done 0`.
- [ ] **Step 3: Compare the table text of the two targets.** Save the `table |` lines of the native run and of the browser run to two files under `examples/Monitor/out` and run `diff` on them. Expected: no difference. A difference is a finding: record both files' lines in `docs/PROBES.md` and stop for review.
- [ ] **Step 4: Take the picture.** `node examples/Monitor/web/run.mjs picture --picture docs/pictures/monitor-browser.png`. Read it beside the native picture and note in the task report any difference a person would see. No pixel equality is claimed.
- [ ] **Step 5: Both suites**, unfiltered. **Commit** `feat(monitor): the page in a browser, with the same table text as the window`.

### Task 8: Frame work on both targets (spec §7.6)

**Files:**
- Modify: `examples/Monitor/src/Script.fs`
- Create: `docs/measurements/monitor.md`

**What is timed.** One frame is `platform.Now ()` before and after: `graph.Run (fun () -> Monitor.frame monitor (1.0 / 60.0))`, which folds the items and, as the batch ends, runs the recomputation, the chart scene and the DOM writes; then one `getBoundingClientRect ()` on the page's root element, which makes the engine compute style and layout. Paint and presentation are outside the timed region on both targets, so no frame rate follows from these figures.

- [ ] **Step 1: Add the script `frames`.** On the default page (four channels at 1,000 observations a second): 60 frames unrecorded, then 600 timed. It reports the mean and the 95th percentile and exits 1 unless both are under `6.06` ms, the period of a 165 Hz display (*from documentation*: `AstroShowcase.Web/measure/README.md`, line 11, names the display). It then calls `Monitor.restart` and repeats with 20 unrecorded and 120 timed frames at rates of 2,000, 4,000 and so on, doubling up to 1,024,000, and reports the highest rate at which both figures stay under `6.06` ms.

```
frames | 4 channels at 1000 /s | mean <x> ms | p95 <y> ms | 600 frames | budget 6.06 ms
frames | highest rate under the budget | <r> /s per channel | mean <x> ms | p95 <y> ms | 120 frames
```

- [ ] **Step 2: Run on each target**, once, with nothing else building: `-- --script frames` and `node examples/Monitor/web/run.mjs frames`. Expected: both end `script done 0`. A run over the budget is a finding: report the two lines and stop.
- [ ] **Step 3: Write `docs/measurements/monitor.md`**: the machine, the runtime and the browser version; the package versions of each head; the commands; what is timed and what is not, as above; the two lines per target; the `counts` and `table` lines of Tasks 6 and 7. Each target's figures are compared with the budget only. Label every figure *measured here*.
- [ ] **Step 4: Commit** `bench(monitor): frame work in a hidden window and a headless browser`.

### Task 9: Records

**Files:**
- Modify: `docs/PROBES.md`, `README.md`

- [ ] **Step 1: `docs/PROBES.md`.** Add under "Workarounds": the bridge from a core projection to the view's `each` (an array read from `Rows.Keys`, on which the view builds a projection of its own), with what the view or the core could offer in its place; and the cost of a mark that comes or goes, with its source. Add a section "Hot reload": the `dev` entry of the native head compiles in Debug and was not run, because no task opens a window; what a reload does to stream state is not measured.
- [ ] **Step 2: `README.md`.** What `examples/Monitor` is, the five commands, links to the measurement file and the two pictures.
- [ ] **Step 3: Final gate.** `dotnet build -c Release` with 0 warnings from the library and model projects; both suites, unfiltered; the browser build; `table`, `counts` and `frames` on both targets, each ending `script done 0`; `git status` clean after the commit.
- [ ] **Step 4: Commit** `docs: the monitor page's probes, commands and pictures`.

---

## Not done in this plan

- No visible window and no `dev` host. Hot reload of the page is compiled and not exercised; a paced, visible frame-rate run on both targets waits for the owner (spec §5).
- The source parameters of a channel (`true_mean`, `true_sd`, `seed`) have no field on the page; tests set them.
- A real data source is not wired in. The seam is the start function of a row's fold, which today returns `feed.Source (...)` (spec §7.2).
- The coverage of the sequence is tested at two settings only: Plan 1's, and α of 0.0125 from a burn-in of 30.
