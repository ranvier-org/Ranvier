# Science workflows: where reactive computation would help, and where it would not

**Status.** Research survey, 2026-11-19. Revised on 2026-11-19 under the evaluation rule stated in
Conventions, with source and ecosystem facts checked in the workspace. No code changed. One of
three threads joined in `RESEARCH-science-synthesis.md`.

**Question.** Which workflows in science and academia recompute everything after a small change,
show stale results, or need manual orchestration, and which of them map onto Ranvier's primitives
well enough to justify a library of scientific algorithms packaged as reactive computations?

**Scope.** Thirty-four workflows in five areas, surveyed from primary sources: papers, facility
technical notes, agency handbooks and the official documentation of the tools in use.

| Area | Workflows |
| --- | --- |
| Statistics | interim analysis, always-valid monitoring, online estimation, bootstrap, MCMC monitoring, living meta-analysis |
| Physics and astronomy | survey alert brokers, gravitational-wave alerts, HEP online monitoring, beamlines, computational steering, conjunction assessment, mission-design budgets |
| Chemistry and biology | kinetics and equilibrium, peak fitting and calibration, cryo-EM, nanopore sequencing, mass spectrometry, plate-reader assays, laboratory automation |
| Mathematics and teaching | computer algebra sessions, explorable explanations, lecture demonstrations |
| Cross-cutting | notebooks, spreadsheets, parameter sweeps, sensitivity and uncertainty, units, provenance, executable papers, dashboards, digital twins, clinical-trial monitoring, nowcasting |

**Evaluation rule** (set by the owner; revision of 2026-11-19). A tool already used for a workflow
is not a reason against serving that workflow. Tools in use are recorded as context and as evidence
that the need is real. Verdicts and the ranking rest on three things: how well the model fits, how
strong the evidence of pain is, and what the family of packages can reach. The limits that remain
are of other kinds: the shape of the data, a missing capability, regulation, closed operations, a
human bottleneck, or a fact about the pain itself.

**Conventions.** `[n]` points to the numbered source list in §9. A figure followed by `[n]` is
*from documentation*: it was read in source `n`, which was opened for this survey. ⚠️ **unverified**
marks a claim taken from a search-result summary, an index-page abstract or a secondary report and
not confirmed in the primary text. *General knowledge* marks a statement that was not checked.
*Local guide* names a page under `docs/content/guide/` that states a behaviour of Ranvier.
*Source* names a file and line in the workspace's source code. *From documentation* with a path
names a README or project file in the workspace. *Measured here* names the command that produced a
figure, and *calculated here* gives the arithmetic. Each workflow has five lines: **(a)** what it is
and who does it, **(b)** evidence of the pain, **(c)** shape of the data, **(d)** the primitive that
carries it, **(e)** the tools in use today. **Fit** is *strong*, *partial* or *poor*. A line that
says *check* names a behaviour that neither the guide pages nor the source lines read here settled.
Source text is paraphrased; the file holds one direct quotation.

**Signal strength.** Evidence that a workflow hurts is uneven. Six items rest on large empirical
studies or formal results: notebook reproducibility [1][2], gene-name corruption in spreadsheets
[8][9], review staleness [11], repeated testing [13][15], sensitivity-analysis practice [57] and
nowcast revisions [66]. Most others rest on the motivation section of a tool paper, which the
tool's authors wrote. For six workflows (S1, P5, C6, M1, M3, X10) no primary evidence of the pain
was found, and each entry says so.

**How Ranvier's primitives behave, as used below.**

| Primitive | Behaviour that matters here | Where stated |
| --- | --- | --- |
| `createMemo` | Lazy; recomputes on read when a source changed; an effect over a diamond sees both branches at the same write | *local guide*: `memos.md` |
| `createAsync` | `Pending`, `Ready` or `Failed`; a tracked input change starts a new flight; a superseded flight publishes nothing; `CancelPrevious` or `KeepLatest` | *local guide*: `async-memos.md`, `timing.md` |
| `createStreamFold seed fold start` | Folds items into one state; publishes at most once per graph pass; a change to a value read by `start` restarts from the seed | *local guide*: `streams.md` |
| Stream items from another thread | Folded under a lock on the delivering thread; the graph publishes the latest state once per inbox drain. A fast source folds at its own rate and readers see coalesced state | *local guide*: `threading.md` |
| `createKeyedCollection`, projections | One reactive value per row; `filter`, `map`, `sortBy`, `groupBy` views re-run their function only for changed rows | *local guide*: `collections.fsx`, `collection-views.fsx` |
| `Projection.foldGroup add subtract zero` | O(1) calls per row edit; `subtract` must invert `add`; any state type. Cost of a key change: see §8 item 5 | *local guide*: `aggregates.fsx`; *source*: `Combinators.fs` lines 1774–1786 |
| `Projection.fold`, `tryMinBy`, `sortBy`, `tryItem` | `fold` costs O(N) per row edit; `tryMinBy` is O(1) unless the holder's row is removed or edited, then O(N); `sortBy` re-sorts in O(N log N) | *local guide*: `aggregates.fsx`, `collection-views.fsx`; *source*: `Combinators.fs` line 2216 |
| `debounce`, `throttle` | Delay publication, not evaluation of the capture | *local guide*: `timing.md` |
| Journal, tracing | Record external inputs and replay them; explain why a node ran. Both are research features; tracing is compiled out of untraced builds | *local guide*: `journal.md`, `tracing.md` |

**What the family reaches.** These facts were verified in the workspace by the coordinating
session and are cited as given; the lines marked *re-read here* were opened again for this revision.

| Fact | Label |
| --- | --- |
| Targets: .NET 10, .NET 8, .NET Standard 2.1, NativeAOT-compatible packages, and JavaScript in the browser and Node.js | *from documentation*: project files and READMEs across the family |
| Delivery: a typed view layer with server HTML and hydration; a native window host on WebKit's WebCore and JavaScriptCore for win-x64, win-arm64, linux-x64, linux-arm64 and osx-arm64; a WebGPU 3D layer, native and in the browser with a WebGL2 fallback; a charting library; a terminal UI for Windows, Linux and macOS | as above |
| Authoring and data: a text-editor component (rope, transactions, history, an editor view); a hot reload host for the native window; persistence to file, browser storage, PostgreSQL and an event store; JSON and binary wire codecs with a duplex channel | as above |
| One science application builds from the same sources for the native window and for the browser. It has 29 pages | *from documentation*: `AstroShowcase.Web/README.md` (*re-read here*, line 3); page count *measured here*: `sed -n '190,218p' AstroShowcase/README.md \| grep -c '^\| '` |
| A language server for F# is built on the same graph, with declaration-level incremental checking and cut-off on unchanged signatures. In its `mid-body` scenario an edit takes 41.3 ms at p50 and 104.5 ms at p95, against 87.5 and 291.1, and 74.9 and 261.0, for two configurations of the server it replaces. In its `mid-references` scenario find-references takes 7.6 ms at p50 against 65.3 and 103.5 | *from documentation*: `ranvier-fsharp/README.md`, "Benchmark Highlights" (*re-read here*) |

---

## §0. Executive summary

| # | Workflow | Pain evidence | Fit | Tools in use today | Verdict |
| --- | --- | --- | --- | --- | --- |
| 1 | Always-valid monitoring and online estimation (S2, S3, S4) | Formal: repeated looks inflate type I error ⚠️ [13][15] | Strong | Batch routines in R and Python (*general knowledge*) | **Top opportunity: library code on every target** |
| 2 | Notebook hidden state and live documents (X1, X7) | Strongest in the survey [1][2][3] | Strong | Jupyter; marimo, Pluto.jl, Observable | **Strongest pain; parts of a host exist; the largest build** |
| 3 | Dashboards over live, revised data (X8, X11) | Whole-script rerun per interaction [6]; measured revisions [66] | Strong | Streamlit, Shiny, scheduled R jobs | **Opportunity: delivery parts exist** |
| 4 | Assay and calibration calculations kept in spreadsheets (C6, C2, X2) | 30.9% of papers with Excel gene lists hold corrupted names [9] | Strong | Excel, GraphPad Prism, Origin | **Opportunity: needs a grid and run-time units** |
| 5 | Teaching demonstrations and explorables (M2, M3, C1) | Authoring effort [52] | Strong | Observable, Pluto.jl, GeoGebra, PhET, Mathematica | **Reachable today on two targets** |
| 6 | Uncertainty and sensitivity over a declared formula graph (X4) | 34% of surveyed sensitivity analyses move one factor at a time [57] | Strong for 10²–10⁴ scenarios | NIST Uncertainty Machine ⚠️ [58] | **Opportunity: needs scenario forking** |
| 7 | Conjunction assessment and mission-design budgets (P6, P7) | Handbook and agency pages [34][35] | Strong | Screening services; OCDT, COMET | **Analysis and teaching tools; operations are closed** |
| 8 | Living and cumulative meta-analysis (S6) | Median 5.5 years to a signal for updating ⚠️ [11] | Strong in shape | Review software, R packages | **Clean demonstrator; human work dominates the pain** |
| – | Mass-spectrometry run monitoring (C5) | Tool papers only ⚠️ [47] | Partial to strong | QCloud; Thermo's C# instrument API [46] | **Next in line: thin evidence, licensed API** |
| – | Survey alert streams (P1) | Requirements document [25] | Strong downstream, poor at the broker core | Kafka-based brokers ⚠️ [27] | **Serve the downstream consumer** |
| – | HEP event-level monitoring, computational steering (P3, P5) | Real | Poor | Experiment and in situ frameworks | **Poor fit of the model** |

**Priority read.**

1. **The best-supported pairing is statistical, not physical.** A value that a consumer may read at
   any moment needs an inference procedure that is valid at any moment. Confidence sequences and
   mixture sequential tests are folds over a few running sums [15][16], which is exactly what
   `createStreamFold` and `Projection.foldGroup` carry.
2. **The strongest evidence of pain is in notebooks, and parts of a host exist.** Two large studies
   measure it [1][2]. The family has a language service on the same graph, an editor component and
   a hot reload host (*from documentation*: `ranvier-fsharp/README.md` and the family's READMEs). §6
   lists what would still have to be built.
3. **Reacting to change is already expected behaviour in every area.** Prism [41], Origin's Auto
   mode [39], Shiny [7], Mathematica's `Dynamic` [51], Bluesky's `LiveFit` [31], CryoSPARC Live [43]
   and MCMC Monitor [23] all update results as data change. That is evidence that the need is real.
4. **Small-N workflows gain consistency, not speed.** A meta-analysis has tens of studies and a plate
   has 96 to 1,536 wells (*general knowledge*). O(1) updates are irrelevant there. The gain is that
   every dependent value updates together and that a value waiting on an input reads as `Pending`.
5. **Reach is wider for readers than for authors.** An application reaches a browser, a desktop
   window or a terminal on the targets listed above. Authors of formulas write F# or C#: in the 2022
   international survey of research software engineers, 71.01% used Python, 4.11% used C# and 0.40%
   used F# [68].
6. **Four capabilities are missing for scientific use** (§8): robust inverse folds for floating
   point, sublinear order statistics, scenario forking, and durable caching of derived values. The
   first, second and fourth were confirmed against source or the persistence README.

---

## §1. Statistics

### S1. Group-sequential interim analysis

- **(a)** Trial statisticians analyse accumulating data at a few planned looks and stop early for
  efficacy or futility.
- **(b)** Regulators require the overall type I error rate to be controlled across all planned
  interim analyses ⚠️ **unverified** (FDA adaptive-design guidance, read through a summary) [18].
  No source found describes the computation itself as slow or stale.
- **(c)** A keyed collection of participants or events; scalar design parameters; a boundary per look.
- **(d)** `Projection.foldGroup` carries the running test statistic and a memo compares it with the
  boundary. **Fit: partial.** The looks are few and scheduled, so nothing needs to be incremental.
- **(e) Tools in use today.** Validated commercial and R packages (*general knowledge*). The
  setting is regulated (X10).

### S2. Continuous monitoring with always-valid inference

- **(a)** Experimenters, platform operators and reviewers who watch a result while data arrive and
  stop when it looks conclusive.
- **(b)** Fixed-sample p-values and confidence intervals become unreliable when the person watching
  chooses the stopping time ⚠️ **unverified** (abstract, read through a summary) [15]. Updating a
  meta-analysis with standard methods inflates the chance of a false positive at each update ⚠️
  **unverified** (abstract) [13]. Always-valid p-values, e-processes and confidence sequences keep
  their guarantee under any stopping rule ⚠️ **unverified** (abstracts) [15][16][17]. One such method
  has run in a commercial testing platform since 2015 ⚠️ **unverified** [15].
- **(c)** A stream of observations, or a keyed collection of units when observations are revised.
  State is a handful of running sums.
- **(d)** `createStreamFold` holds the count, sum and sum of squares; a memo turns them into a
  confidence sequence or a mixture likelihood ratio (*general knowledge* of the formulas). With a
  keyed collection, `Projection.foldGroup` gives the same sums and admits corrections to past rows.
  **Fit: strong.** Reading at any time is the model's normal use, and it is the use these methods
  are built to make safe.
- **(e) Tools in use today.** R and Python packages compute these bounds on a batch of data
  (*general knowledge*). No source was found that pairs them with a value read on demand.

### S3. Online estimation

- **(a)** Anyone summarising data too large or too fast to hold: instrument logs, simulations, telemetry.
- **(b)** A Julia package for the purpose describes single-pass algorithms with constant memory ⚠️
  **unverified** (package documentation, read through a summary) [21]. One-pass formulas built on
  raw sums of powers are numerically unstable, which is why update formulas on central moments
  exist ⚠️ **unverified** (report summary) [22].
- **(c)** Streams, and keyed collections when rows can be withdrawn.
- **(d)** `createStreamFold` for append-only data; `Projection.foldGroup` when rows change.
  **Fit: strong for sums and moments, partial for quantiles.** A median has no inverse. It is
  composable as `Projection.sortBy` then `tryItem`, at O(N) per order change (*source*:
  `Combinators.fs` lines 1636 and 1941; §8 item 2).
- **(e) Tools in use today.** OnlineStats.jl [21]; streaming libraries in Python (*general
  knowledge*). A keyed aggregate adds removal and correction of a past row.

### S4. Bootstrap and resampling

- **(a)** Analysts who need an interval for a statistic with no closed-form variance.
- **(b)** A Google paper motivates uncertainty estimates that can be computed in one pass over a
  stream, and gives a Poisson bootstrap in which each observation receives independent Poisson
  weights with mean 1 [20]. For data that fit in memory a batch bootstrap takes milliseconds to
  seconds (*general knowledge*), so no pain is documented there.
- **(c)** A stream or keyed collection of observations; B replicate accumulators.
- **(d)** The fold state is an array of B weighted sums, updated in O(B) per item. A percentile
  interval sorts B values, not N. A progressive bootstrap that refines while the user waits is a
  stream memo that restarts when its input changes. **Fit: strong for statistics that are functions
  of sums; poor for a bootstrap of a median or a full model refit.**
- **(e) Tools in use today.** Batch routines in every statistics package (*general knowledge*).

### S5. Monitoring MCMC convergence while sampling

- **(a)** Bayesian modellers running chains for minutes to days.
- **(b)** Stan's diagnostics take a completed matrix of draws ⚠️ **unverified** (rstan reference,
  read through a summary) [24]. A Flatiron Institute tool announced in May 2023 monitors runs in
  progress by reading the CSV files that the sampler appends to; a Stan developer notes in the same
  thread that it implements the original R-hat and a per-chain effective sample size, not the
  current rank-normalised versions [23]. Data scientists report that long computations in notebooks
  give no feedback on progress [3].
- **(c)** One stream of draws per chain; a keyed collection by parameter.
- **(d)** `createStreamFold` per chain carries running means and variances, from which the original
  R-hat follows. **Fit: partial.** Rank-normalised R-hat ranks all pooled draws ⚠️ [24], which has no
  inverse: it must be recomputed over the retained draws, behind `throttleLast`, until a sublinear
  order-statistic structure exists (§8 item 2).
- **(e) Tools in use today.** MCMC Monitor [23] and post-hoc diagnostic packages. The draws arrive
  as appended CSV files [23], so a monitor does not depend on the sampler's language.

### S6. Living systematic reviews and cumulative meta-analysis

- **(a)** Evidence-synthesis teams who add each new trial to a pooled estimate.
- **(b)** In a survival analysis of 100 systematic reviews, the median time to a signal for
  updating was 5.5 years, 23% had a signal within two years and 7% already had one at publication
  ⚠️ **unverified** (PubMed abstract, read through a summary) [11]. Reviews that are not kept
  current lose accuracy and usefulness ⚠️ **unverified** (abstract) [12]. Repeated updating needs
  sequential methods to control false positives ⚠️ **unverified** (abstract) [13]. The classic
  cumulative meta-analysis re-pooled 33 streptokinase trials as each was added ⚠️ **unverified**
  (dataset documentation) [14].
- **(c)** A keyed collection of studies, each a row of effect size and variance. A study being
  extracted is a pending row.
- **(d)** The fixed-effect estimate, Cochran's Q, I² and the DerSimonian–Laird between-study variance
  are functions of four weighted sums, each with an inverse (*general knowledge* of the formulas), so
  `Projection.foldGroup` carries them. The random-effects estimate re-weights every study when the
  variance changes and costs O(k). `sortBy` on publication date gives the cumulative order.
  **Fit: strong in shape.** With k in the tens the cost saving is nil; the value is one consistent
  graph from study row to forest plot to stopping boundary. **Limit:** the cost of a living review
  is searching, screening and extraction [12], which a computation library does not reduce.
- **(e) Tools in use today.** Review-management software and R packages, which recompute a
  meta-analysis in milliseconds (*general knowledge*).

---

## §2. Physics and astronomy

### P1. Survey alert streams and brokers

- **(a)** Rubin Observatory distributes alerts to community brokers, which filter and classify them
  for astronomers.
- **(b)** The data management system must support at least 10,000 alerts per visit on average and
  40,000 in a single visit, a long-term average of 10⁷ alerts per night, and 98% of a visit's alerts
  within 60 seconds of readout. A packet is up to 82 KB; the stream averages 0.2 Gbps with bursts
  to 5.4 Gbps and about 782 GB per night [25]. Seven brokers receive the full stream [26].
- **(c)** A high-rate stream of large records, cross-matched against catalogues.
- **(d)** **Fit: poor for the broker's main work, partial for statistics over the stream, strong
  downstream.** A stream fold absorbs a fast source: items are folded under a lock on the delivering
  thread and readers see coalesced state (*local guide*: `threading.md`). Counts and running
  statistics per class are therefore within the model. Decoding, catalogue cross-matching and
  redistribution of 82 KB packets are I/O and array work spread over machines, and one fold is
  serial under its lock. Per-object state for every alert depends on the cost of a key change,
  which is unsettled (§8 item 5). Downstream, one astronomer's filter receives a small share of the
  stream: a keyed collection by object, with a light-curve fit as an async memo per row.
- **(e) Tools in use today.** Brokers built on Kafka and distributed stream processors ⚠️
  **unverified** (project slides) [27].

### P2. Gravitational-wave low-latency alerts

- **(a)** The LIGO–Virgo–KAGRA collaboration turns candidates from several search pipelines into
  public alerts.
- **(b)** Expected median latency is 29.5 s for a preliminary alert from full-bandwidth searches
  and −3.1 s for early-warning triggers, measured in a mock data challenge [28]. Candidates are
  merged into superevents, a preferred event is chosen and may change, and alerts are revised; the
  logic lives in a task-queue application ⚠️ **unverified** (project documentation, read through a
  summary) [29].
- **(c)** A keyed collection of candidates grouped into superevents; awaited sky maps and
  classifications; a stream of alerts out.
- **(d)** `groupBy` forms superevents, `tryMaxBy` picks the preferred event, and an async memo per
  superevent computes the sky map and cancels a superseded flight. **Fit: strong in shape.**
  **Limit:** the pipeline is operated inside the collaboration, which makes it closed operations.
  Public alerts are open, so a consumer of the alert stream has the shape of P1's downstream filter.
- **(e) Tools in use today.** GWCelery ⚠️ [29].

### P3. High-energy-physics online monitoring

- **(a)** Shift crews watch histograms filled from collision events to catch detector faults.
- **(b)** The CMS framework creates, fills, transports and displays histograms so that an expert can
  act while data are still being taken ⚠️ **unverified** (conference papers, read through a summary)
  [30].
- **(c)** Event streams at detector rates, folded into histograms across many processes.
- **(d)** A histogram is a stream fold, and a comparison with a reference is a memo. Merging
  per-process histograms is `foldGroup` over rows, exact on integer counts (*general knowledge*).
  **Fit: poor for filling, partial for merge and compare.** Filling happens inside the experiments'
  distributed reconstruction processes ⚠️ [30], where a .NET or JavaScript graph is not present.
- **(e) Tools in use today.** The experiments' own frameworks ⚠️ [30].

### P4. Synchrotron beamlines with Bluesky

- **(a)** Beamline scientists and users run scans and watch plots and fits while data are collected.
- **(b)** Bluesky emits start, descriptor, event and stop documents during a run, and callbacks
  subscribe to them. `LiveFit` refits a model after each new point. Callbacks block: acquisition
  cannot continue until they return, and the documentation recommends moving heavy work to another
  process over a message queue [31].
- **(c)** A stream of event documents; scalar scan parameters; awaited fits.
- **(d)** `createStreamFold` accumulates points; an async memo fits them and a superseded fit is
  cancelled, so a slow fit does not hold up acquisition. **Fit: strong in shape.** The documented
  blocking is the problem that flight cancellation removes.
- **(e) Tools in use today.** Bluesky's own callbacks [31]. The run engine is Python ⚠️
  **unverified** [32]. A consumer in another language would attach where the documentation sends
  documents to other processes [31]; whether that message format is readable outside Python is a
  *check*.

### P5. Computational steering of simulations

- **(a)** Simulation scientists inspect a running job and change its parameters.
- **(b)** In situ processing exists because writing full output to disk is too costly at scale ⚠️
  **unverified** (abstract) [33]. No source found reports stale or wholly recomputed results as the
  problem.
- **(c)** Scalar parameters in; large array fields out.
- **(d)** Parameters are signals and the run is an async or stream memo. **Fit: poor.** The work is
  inside an MPI solver, and a restart from the seed (*local guide*: `streams.md`) discards hours of
  state.
- **(e) Tools in use today.** In situ frameworks ⚠️ [33].

### P6. Conjunction assessment

- **(a)** Satellite operators assess predicted close approaches and decide whether to manoeuvre.
- **(b)** Protected assets are screened against the catalogue at least once and up to three times a
  day; three a day is the minimum for orbits with perigee below 500 km. Mitigation is recommended
  when the probability of collision exceeds 1E-04. A manoeuvre often creates new conjunctions or
  raises the probability of existing ones, and the handbook asks operators to confirm that it does
  not leave the overall situation worse [34].
- **(c)** A keyed collection of conjunction events, each revised by every screening; scalar
  thresholds; a candidate manoeuvre as a what-if input.
- **(d)** A row per conjunction, the probability as a row memo, `countBy` for events above the
  threshold and `tryMaxBy` for the worst. A candidate manoeuvre is a signal that every row reads.
  **Fit: strong.** The workspace's demonstration app already has a Conjunction Assessment page
  (*from documentation*: `AstroShowcase/README.md`, "Pages"), with a 3D scene on the WebGPU layer.
- **(e) Tools in use today.** Agency and commercial screening services (*general knowledge*).
  **Limit:** operations are closed to a small team; analysis tools and teaching are open.

### P7. Mission analysis and concurrent design budgets

- **(a)** Spacecraft design teams keep mass, power and delta-V budgets consistent across disciplines.
- **(b)** ESA developed the Open Concurrent Design Tool in-house to replace its Integrated Design
  Model; the tool is an Excel add-in over a database [35]. The earlier model was a set of Excel
  workbooks, one per discipline, with a data-exchange file ⚠️ **unverified** (conference abstract)
  [36].
- **(c)** Scalar parameters with owners; keyed collections of equipment; roll-ups by subsystem.
- **(d)** Signals, memos, `groupBy` with `Grouping.aggregate` for per-subsystem totals.
  **Fit: strong.** It is a spreadsheet's dependency graph with types.
- **(e) Tools in use today.** OCDT [35] and COMET, whose SDK is published as C# libraries ⚠️
  **unverified** (repository listing) [37]. A .NET library could read the standard design model
  through that SDK (an inference made here; *check*).

---

## §3. Chemistry and biology

### C1. Reaction kinetics and equilibrium calculations

- **(a)** Enzymologists and physical chemists simulate a mechanism, fit rate constants, and compute
  speciation.
- **(b)** A 2009 kinetics program was published on the strength of dynamic simulation: the user
  scrolls rate constants and starting concentrations and watches the curves change, which helps
  find starting values for regression ⚠️ **unverified** (abstract, read through a summary) [38].
  That a journal paper was built on this behaviour shows that it is valued.
- **(c)** Scalar rate constants and concentrations; a time course as an array; awaited global fits.
- **(d)** A memo integrates the rate equations when they are cheap; an async memo with
  `CancelPrevious` runs a global fit. **Fit: strong for exploration.** An equilibrium is a set of
  simultaneous equations, so the solver is one node, not a graph of nodes (§8 item 9).
- **(e) Tools in use today.** KinTek Explorer ⚠️ [38] and free simulators (*general knowledge*).

### C2. Spectroscopy and chromatography: peak fitting and calibration

- **(a)** Analytical chemists fit overlapping peaks, integrate them, and convert areas to
  concentrations through a calibration curve.
- **(b)** In Origin, an analysis has a Recalculate mode of None, Auto or Manual, and Manual is the
  default: after the input changes, the result waits for the user to trigger an update. Auto
  updates the result when the input changes [39]. Stale results are thus the documented default of
  a widely used tool.
- **(c)** A spectrum as an array; a keyed collection of peaks; a keyed collection of standards and
  one of samples.
- **(d)** The calibration is a strong fit: least-squares coefficients are functions of sums with
  inverses (*general knowledge*), so `foldGroup` over the standards feeds a per-sample row memo, and
  an edit to one standard updates every concentration. **Fit: partial for the peak fit itself.**
  Overlapping peaks share residuals, so the fit is one async memo over the whole spectrum.
- **(e) Tools in use today.** Origin [39], Fityk ⚠️ [40] and vendor data systems (*general
  knowledge*).

### C3. On-the-fly cryo-EM processing

- **(a)** Structural biologists judge data quality while the microscope collects.
- **(b)** Warp's authors state that preprocessing was generally done after collection and that the
  operator needs to monitor quality continuously ⚠️ **unverified** (paper, read through a summary)
  [42]. CryoSPARC Live recommends at least 4 GPUs to keep up with collection; its preprocessing
  workers handle each movie as it arrives, and its 2D and 3D jobs run indefinitely on new particles
  [43].
- **(c)** A stream of movies; a keyed collection of micrographs with quality metrics; scalar
  thresholds; awaited GPU jobs.
- **(d)** Rows per micrograph, `filter` driven by threshold signals, `countBy` and `tryAverageBy` for
  the session summary. **Fit: partial.** The reactive layer is the dashboard and the selection. The
  motion correction, picking and classification are GPU kernels.
- **(e) Tools in use today.** CryoSPARC Live [43], Warp ⚠️ [42], RELION and Scipion (*general
  knowledge*). Warp's desktop program is C# ⚠️ **unverified** (fork README).

### C4. Real-time nanopore sequencing analysis

- **(a)** Genomics groups enrich targets by rejecting unwanted molecules during the run, and watch
  yield and coverage.
- **(b)** readfish base-calls, maps and acts within about 1 s of a read starting, using 0.4 s chunks
  of about 180 bases, across up to 512 channels and at least 100 read fragments per second. The
  earlier signal-matching method did not scale to gigabase references [44].
- **(c)** A stream of read chunks keyed by channel; a keyed collection of targets with coverage.
- **(d)** Coverage per target is `foldGroup` over reads grouped by target, and a memo flips a target
  from enrich to deplete at a depth. **Fit: partial.** Run monitoring fits. The per-read decision is
  one function of one chunk; no derived value is shared between reads, so a graph has nothing to
  save there.
- **(e) Tools in use today.** readfish [44] and minoTour ⚠️ [45], both Python.

### C5. Mass spectrometry: real-time acquisition and run QC

- **(a)** Proteomics facilities monitor instrument performance run by run, and method developers
  steer acquisition scan by scan.
- **(b)** A cloud QC system was published to give laboratories automated, longitudinal assessment
  of instrument performance ⚠️ **unverified** (abstract) [47]. Thermo's instrument API repository
  ships C# examples and requires a separate licence agreement for use [46]. Skyline, a main
  quantitative tool, is written in C# ⚠️ **unverified** (paper, read through a summary) [48].
- **(c)** A stream of scans; keyed collections of precursors, peptides and runs; scalar tolerances.
- **(d)** `createStreamFold` per run for totals; a keyed collection by precursor with `groupBy`
  peptide; row memos for mass error and retention-time drift; `countBy` for values out of tolerance.
  **Fit: partial to strong.** This is the one scientific instrument ecosystem found where .NET is the
  vendor's own language. **Limit:** the evidence of pain is a tool paper's abstract, and the
  instrument API needs a licence agreement [46] and access to an instrument.
- **(e) Tools in use today.** QCloud ⚠️ [47] and similar pipelines; custom acquisition logic written
  against the .NET API [46].

### C6. Plate-reader assays

- **(a)** Screening and pharmacology groups turn well readings into normalised responses, plate
  quality metrics and dose-response fits.
- **(b)** No primary source was found that measures the pain. An R package for plate data exists to
  move plate-shaped spreadsheet layouts into tidy tables ⚠️ **unverified** (paper, read through a
  summary) [49]. The spreadsheet evidence in X2 applies. GraphPad Prism recomputes results and
  graphs automatically when data are corrected or replaced [41], which shows that users of this
  workflow expect linked results to update.
- **(c)** A keyed collection by well; groups by compound and by control type; scalar exclusions.
- **(d)** Control means and standard deviations by `foldGroup`; the plate's Z′ as a memo over them;
  `groupBy` compound with a fit per group. Excluding one well refits one compound.
  **Fit: strong.** A plate is small, so the gain is consistency, types and tests, not speed.
- **(e) Tools in use today.** Prism [41], vendor software and Excel templates (*general knowledge*).

### C7. Laboratory automation

- **(a)** Automation engineers and self-driving-laboratory groups schedule instruments and choose
  the next experiment from earlier results.
- **(b)** An orchestration system for autonomous discovery describes a central workflow manager
  that designs experiments on the fly from those already run ⚠️ **unverified** (paper, read through
  a summary) [50].
- **(c)** Awaited experiment results arriving over hours; a keyed collection of experiments; a
  model refit after each.
- **(d)** Results as an async source per row; the surrogate model as an async memo over settled
  rows. **Fit: partial.** Inputs can be restored from a store, but derived values are not cached
  durably (§8 item 4), and device control is imperative.
- **(e) Tools in use today.** Dedicated orchestrators ⚠️ [50].

---

## §4. Mathematics and teaching

### M1. Computer algebra sessions

- **(a)** Mathematicians, physicists and students derive and manipulate expressions step by step.
- **(b)** No primary source of pain was found. The Wolfram Language tracks dependencies
  automatically so that controls and outputs update when values change [51], which shows that the
  behaviour is wanted in this setting.
- **(c)** Symbolic expressions; scalar parameters.
- **(d)** A derived expression is a memo with structural equality. **Fit: poor in practice.** The
  workflow needs an algebra system, which the family does not have.
- **(e) Tools in use today.** Mathematica's `Dynamic` and `Manipulate` [51].

### M2. Explorable explanations and interactive textbooks

- **(a)** Authors of interactive articles and course texts, and the students who read them.
- **(b)** The journal built for such articles paused in July 2021, citing volunteer burnout, and
  stated that "the primary bottleneck is the amount of effort it takes to produce these articles"
  [52]. Later toolkits target the authoring cost directly ⚠️ **unverified** [53][54].
- **(c)** Scalar parameters bound to controls; derived values; small tables.
- **(d)** Signals and memos, compiled to JavaScript. **Fit: strong.** The repository's own guide
  pages already embed live signal maps (*local guide*: `memos.md`, `streams.md`). The documented
  pain is authoring effort, and the family's hot reload host and single source for window and
  browser bear on that directly.
- **(e) Tools in use today.** Observable, Idyll, Living Papers ⚠️ [54], Pluto.jl, GeoGebra and PhET,
  whose simulations use their own observable-property library ⚠️ **unverified** [56]. What this
  family adds is typed, unit-checked formulas and the pending channel.

### M3. Lecture demonstrations

- **(a)** Lecturers show a model responding to its parameters in front of a class.
- **(b)** No primary source of pain was found. A reactive notebook written for teaching is in use
  for a university course ⚠️ **unverified** (project pages, read through a summary) [5], which
  shows demand.
- **(c)** Scalars and time; a simulation that advances continuously.
- **(d)** Signals, memos and a clock stream. **Fit: strong.** The workspace's rule that models are
  live by default (*from documentation*: workspace `AGENTS.md`) is this workflow. The demonstration
  app's 29 pages build for the native window and the browser from the same sources (see "What the
  family reaches").
- **(e) Tools in use today.** Pluto.jl, Mathematica, GeoGebra, Desmos (*general knowledge*).

---

## §5. Cross-cutting

### X1. Notebook hidden state and reproducibility

- **(a)** Nearly every computational scientist.
- **(b)** Of 863,878 attempted executions of valid notebooks from GitHub, 24.11% ran without errors
  and 4.03% produced the same results; among 912,343 notebooks with unambiguous execution order,
  36.36% had cells out of order [1]. Of 27,271 notebooks from 2,660 repositories tied to 3,467
  biomedical publications, 22,578 were Python, 15,817 declared dependencies and were attempted,
  1,203 ran without errors and 879 gave identical results [2], which is 5.6% of those attempted
  (*calculated here*: 879 / 15,817). Interviews (n = 20) and a survey (n = 156) of data scientists
  catalogue nine pain points, among them re-running everything after a small change and reaching a
  state that differs from the notebook's linear order [3].
- **(c)** A graph of cells: scalars, tables, awaited loads.
- **(d)** Each cell is a memo; a slow cell is an async memo that reads as `Pending`; a cell whose
  result is unchanged cuts off its dependents. **Fit: strong.** **Limit of the evidence:** hidden
  state is not the main cause of failure. In [2], 6,588 executions (41.65%) failed on missing
  modules or imports. A reactive runtime does not repair an environment; a host would have to pin
  one. §6 lists the parts that exist and those that do not.
- **(e) Tools in use today.** Jupyter. marimo re-runs the cells that depend on a changed cell ⚠️
  **unverified** (project pages, read through a summary) [5]; Pluto.jl and Observable do the same
  (*general knowledge*); a Jupyter kernel flags stale cells ⚠️ **unverified** [4]. No statement was
  found on whether .NET's Polyglot Notebooks re-run dependents ⚠️ **unverified** [69].

### X2. Spreadsheet errors in research

- **(a)** Laboratory scientists who keep data and calculations in Excel.
- **(b)** Among 3,597 papers with supplementary Excel gene lists in 18 journals from 2005 to 2015,
  704 (19.6%) held gene names converted to dates or numbers [8]. A second scan for 2014 to 2020
  found errors in 30.9% (3,436 of 11,117) [9]. In October 2020, 15,841 positive COVID-19 results
  were left out of England's daily figures after a file exceeded a size limit ⚠️ **unverified**
  (news reports of the agency's statement) [10].
- **(c)** Keyed rows, formula columns, scalar constants.
- **(d)** Row projections, `map` views and aggregates. **Fit: strong.** **What the evidence shows:**
  the documented errors are silent type coercion and size limits, not stale results; a spreadsheet
  already recalculates its dependents. A typed formula graph removes coercion by construction and
  adds units and tests. Scientists need a grid to use it; no data-grid component is among the parts
  listed for the family (*check*).
- **(e) Tools in use today.** Excel and other spreadsheets.

### X3. Parameter sweeps

- **(a)** Modellers who run one model at many parameter points.
- **(b)** Workflow tools state their purpose as skipping work that is already up to date ⚠️
  **unverified** (package description, read through a summary) [60][61]. That is evidence of the
  pain.
- **(c)** A keyed collection of parameter points; an awaited result per point.
- **(d)** A row per point with an async value; adding a point computes one row; a shared upstream
  constant invalidates all rows. An example in the guide already bounds concurrency (*local guide*:
  `examples.md`). **Fit: strong for points that take milliseconds to seconds; poor for hours**,
  where the result must outlive the process and derived values are not cached durably (§8 item 4).
- **(e) Tools in use today.** Snakemake, targets and Nextflow, which keep results on disk ⚠️
  [60][61].

### X4. Sensitivity and uncertainty analysis

- **(a)** Modellers and metrologists who must state how uncertain an output is and which inputs
  drive it.
- **(b)** In a systematic review of highly cited papers, 42% failed to explore the input space
  properly. Of the sensitivity analyses, 34% moved one factor at a time, 41% were global and 25%
  were unclear. In ten dimensions a one-at-a-time design leaves over 99.75% of the input space
  unexplored [57]. NIST's Uncertainty Machine takes a measurement model y = f(x₁,…,xₙ) and applies
  the GUM's approximate method and a Monte Carlo method ⚠️ **unverified** (NIST pages, read through a
  summary) [58][59]. It runs on request.
- **(c)** Scalar inputs with distributions; a formula graph; a keyed collection of sampled scenarios.
- **(d)** Because inputs are declared, a library can enumerate them and build a global design
  without the user naming them again. First-order propagation is a memo over values and
  sensitivities. Monte Carlo is `createStreamFold` over batches of draws, which restarts when an
  input distribution changes and reads as `Pending` until the first batch. Scenarios as rows work
  for hundreds to thousands. **Fit: strong, with one gap:** no API evaluates one graph over many
  input vectors (§8 item 3).
- **(e) Tools in use today.** The NIST tool ⚠️ [58], commercial budget software and Python packages
  (*general knowledge*). None was found that keeps the budget live.

### X5. Units handling

- **(a)** Everyone who combines quantities from different sources.
- **(b)** The Mars Climate Orbiter was lost because English units were not converted to metric in
  ground navigation software [62]. A survey condensed about 3,700 search results into 38 unit
  libraries ⚠️ **unverified** (abstract) [64]. That many libraries exist and the error class
  persists is an inference made here, not a finding of the paper.
- **(c)** Scalars and columns with dimensions.
- **(d)** F# checks units at compile time at no run-time cost [63]. **Fit: strong in F#.** Units are
  erased at run time [63], so C# callers, the JavaScript target and formulas entered by a user get
  no check (§8 item 8).
- **(e) Tools in use today.** Run-time unit libraries in Python and Julia (*general knowledge*).

### X6. Provenance

- **(a)** Researchers, reviewers and auditors who ask where a number came from.
- **(b)** Covered by X1: an unrecorded execution order is a provenance failure [1][3].
- **(c)** The dependency graph and the history of inputs.
- **(d)** The journal records every external input and replays it; `Trace.why` walks from a value
  back to the write that caused it (*local guide*: `journal.md`, `tracing.md`). **Fit: partial.**
  Tracing is compiled out of untraced builds, and both features are marked as research (§8 item 10).
- **(e) Tools in use today.** Workflow engines and version control (*general knowledge*).

### X7. Executable and living papers

- **(a)** Authors and publishers who want figures regenerated from code and data.
- **(b)** eLife's executable articles let a reader edit a figure's code and re-run it ⚠️
  **unverified** (publisher pages, read through a summary) [55]. A 2023 toolkit emits print,
  interactive and computational outputs from one source ⚠️ **unverified** [54].
- **(c)** As M2, with awaited data loads.
- **(d)** As M2. **Fit: strong.** A paper reaches readers through a publisher's pipeline, so a live
  document also needs a static export (*general knowledge*); the view layer's server HTML is the
  part that exists for it.
- **(e) Tools in use today.** Stencila ⚠️ [55], Quarto, MyST (*general knowledge*), Living Papers ⚠️
  [54].

### X8. Public and laboratory dashboards

- **(a)** Agencies and laboratories that publish live figures.
- **(b)** Streamlit reruns the script from top to bottom whenever a user interacts with a widget;
  caching and fragments are the documented remedies [6].
- **(c)** Keyed records that are appended and revised; grouped aggregates; filters as scalars.
- **(d)** A keyed collection with `groupBy` and `Grouping.aggregate`; an effect per chart. A reader
  of two derived values sees both at the same write (*local guide*: `memos.md`), so a page never
  mixes old and new figures. **Fit: strong.** The delivery parts exist: server HTML with hydration,
  a charting library, stores for the input records and a duplex wire channel (see "What the family
  reaches"), and a Blazor Server guide page (*local guide*: `blazor-server.md`, listed in
  `index.md`; not read for this survey).
- **(e) Tools in use today.** Streamlit [6]; Shiny, whose reactive expressions are lazy and cached
  ⚠️ **unverified** (documentation, read through a summary) [7]. What this family adds is per-row
  incremental aggregates and consistent reads.

### X9. Digital twins

- **(a)** Engineers and clinicians who keep a model synchronised with a physical system.
- **(b)** The National Academies define a digital twin as dynamically updated with data from its
  physical counterpart, predictive, and bidirectional [65]. The report is about research gaps, not
  about a tool that fails.
- **(c)** Sensor streams; state estimates; awaited simulations; commands out.
- **(d)** A recursive filter is a stream fold (*general knowledge*); effects send commands.
  **Fit: partial.** It suits lumped models; field-scale models are array kernels on clusters.
- **(e) Tools in use today.** Vendor platforms (*general knowledge*).

### X10. Clinical-trial monitoring

- **(a)** Data monitoring committees and central monitors.
- **(b)** Regulators stress that unblinded interim results stay inside the committee ⚠️
  **unverified** (draft guidance, read through a summary) [19]. No source found reports a
  computational problem.
- **(c)** Keyed by participant and by site; aggregates per site and arm.
- **(d)** As S1 and X8. **Fit: partial.** **Limit:** the setting is regulated. The requirements are
  access control, validation and audit, none of which a reactive core provides (§8 item 13).
- **(e) Tools in use today.** Validated commercial systems (*general knowledge*).

### X11. Epidemiological nowcasting

- **(a)** Public-health modellers who correct recent counts for reporting delay.
- **(b)** In Germany's hospitalisation data, same-day values held only about 50–60% of the cases
  eventually reported, and values took weeks or months to stabilise. Eight teams produced nowcasts
  every day from 22 November 2021 to 29 April 2022, each for the current day and the 28 before it,
  with fully automated pipelines [66].
- **(c)** A reporting triangle keyed by event date and report date, revised daily; an awaited model fit.
- **(d)** Empirical delay factors are ratios of column sums and suit `foldGroup` (*general
  knowledge*). A Bayesian model refit is one async memo. **Fit: partial for the computation, strong
  for the display** (X8). **Limit:** data arrive once a day, so the computation is not pressed for
  time; the gain is a display that revises every dependent figure together.
- **(e) Tools in use today.** R packages with scheduled runs ⚠️ [67].

---

## §6. Ranking

Ranked on three axes only: strength of the evidence of pain, fit of the model, and reach through
the targets and parts listed under "What the family reaches".

| Rank | Workflow | Pain | Fit | Reach |
| --- | --- | --- | --- | --- |
| 1 | Always-valid monitoring and online estimation (S2, S3, S4) | Formal results ⚠️ [13][15][16] | Exact: folds over running sums | Library code on every target |
| 2 | Notebook hidden state and live documents (X1, X7) | Strongest in the survey [1][2][3] | Each cell a memo | Parts exist; a host must be built |
| 3 | Dashboards over live, revised data (X8, X11) | Documented rerun [6]; measured revisions [66] | Keyed collection with grouped aggregates | Delivery parts exist |
| 4 | Assay and calibration calculations (C6, C2, X2) | Measured for spreadsheets [8][9]; none found for plates | The collection model unchanged | View, charts, window and browser exist; grid and run-time units to build |
| 5 | Teaching demonstrations and explorables (M2, M3, C1) | Authoring effort [52]; none found for lectures | Signals and memos | Shown: 29 pages on two targets |
| 6 | Uncertainty and sensitivity (X4) | Systematic review [57] | Strong, with one gap | Scenario forking to build |
| 7 | Conjunction assessment and mission budgets (P6, P7) | Handbook and agency pages [34][35] | Strong | A demonstrator page exists; operations are closed |
| 8 | Living and cumulative meta-analysis (S6) | Measured staleness ⚠️ [11] | Exact in shape | Library code; human work dominates the pain |

1. **Always-valid monitoring and online estimation (S2, S3, S4).** The pain is a formal result, not
   an opinion: looking repeatedly breaks fixed-sample inference ⚠️ [13][15]. The algorithms are folds
   over a few sums, small enough to verify against published tables, and they need nothing beyond
   library code. It is also the one place where the library's model and the statistics need each
   other. Its sources were read through summaries and should be opened before it is built on.
2. **Notebook hidden state and live documents (X1, X7).** No other workflow has evidence this
   strong: two studies, of 863,878 attempted executions and of 15,817 attempted notebooks, both
   read directly [1][2]. The model fits
   without adaptation. It ranks second and not first because it is the largest build, described
   below, and because cells would be written in F#, which 0.40% of surveyed research software
   engineers use [68].
3. **Dashboards over live, revised data (X8, X11).** A widely used tool documents the exact pain in
   the brief, recomputing everything after one interaction [6], and revision of published figures
   is measured [66]. The parts needed to deliver one exist. Nothing in the core is missing for it,
   subject to the cost of key changes (§8 item 5).
4. **Assay and calibration calculations (C6, C2, X2).** The data are keyed rows with grouped
   aggregates, and spreadsheet failure in biology is measured [8][9]. The documented failure is
   type coercion, which a typed graph removes; staleness is not the documented failure. It ranks
   below dashboards because a grid and run-time units have to be built first.
5. **Teaching demonstrations and explorables (M2, M3, C1).** Reach is the strongest of any item:
   one application of 29 pages already builds for window and browser, with hot reload. The
   documented pain is authoring effort [52], which those parts address. It ranks fifth because the
   evidence of pain is one editorial statement and tool motivations.
6. **Uncertainty and sensitivity over a declared formula graph (X4).** Declared inputs are the
   idea's defining feature, and here they pay directly: the library can vary every input together.
   The evidence is a systematic review read directly [57], though it measures poor method choice
   more than stale results. It needs scenario forking, which the source does not have.
7. **Conjunction assessment and mission-design budgets (P6, P7).** Both are parameter graphs over
   revised keyed data, with handbook-level evidence [34][35] and a 3D layer and a demonstrator page
   already present. Operations are closed, so the reach is analysis and teaching tools.
8. **Living and cumulative meta-analysis (S6).** The shape is exact and review staleness is measured
   ⚠️ [11]. It ranks last of the eight because the cost of a living review is human work [12]. It is
   a good first demonstrator of item 1 on real data.

**Next in line.** Mass-spectrometry run monitoring (C5) has the platform on its side, since the
vendor API is C# [46], but the thinnest evidence of pain in the survey and a licensed API. MCMC
monitoring (S5) has a language-neutral input and waits on sublinear order statistics. Beamline live
fitting (P4) has documented pain [31] and waits on a *check* of the document stream's format.

**A notebook or live-document host: what exists and what does not.**

- *Exists.* A language service for F# on the same graph, which re-checks at the level of one
  declaration and cuts off when a signature is unchanged; a text-editor component; a hot reload
  host for the native window; a view layer with server HTML; charts; stores for inputs (*from
  documentation*: `ranvier-fsharp/README.md` and the family's READMEs). The journal records inputs
  for replay (*local guide*: `journal.md`).
- *To build.* A document model of cells and their outputs. An evaluator that runs a changed cell
  and its dependents as graph nodes: the language service checks code, and nothing read here says it
  executes it (*check*). Rendering and storage of outputs. A file format with a static export. A
  pinned environment per document, which is what the 41.65% of failures on missing imports call for
  [2]. Durable caching of slow cells (§8 item 4).

---

## §7. Poor fits

Each row rests on the fit of the model or on a missing capability.

| Workflow | Why the model does not fit |
| --- | --- |
| Broker core of a survey alert stream (P1) | The broker decodes, cross-matches and redistributes 82 KB packets at up to 5.4 Gbps [25]. That is I/O and array work spread over machines, and one fold runs serially under its lock (*local guide*: `threading.md`). Running statistics over the stream do fit, and so does a downstream filter. |
| Event-level histogram filling in HEP (P3) | Filling happens inside the experiments' distributed reconstruction processes ⚠️ [30], where a .NET or JavaScript graph is not present. Merging and comparing the histograms would fit. |
| Computational steering (P5) | The work is inside a parallel solver; a restart from the seed discards the run. |
| Batch array computation: motion correction, base-calling, 2D classification (C3, C4) | A vectorised or GPU kernel does the work. The graph can only call it as one node. |
| Pipelines whose steps take hours (X3) | A result that takes hours must be kept on disk and computed on other machines. Derived values are not cached durably (*from documentation*: `Ranvier.Persistence/README.md`), so a memo is lost with the process (§8 item 4). |
| MCMC sampling and full Bayesian refits (S5, X11) | A sampler is one long computation. Only its monitoring is incremental. |
| Per-read adaptive sampling decisions (C4) | Each decision is one function of one chunk, made within about 1 s [44]. No derived value is shared between reads, so a graph has nothing to save. |
| Global fits with shared residuals (C2) | Every parameter touches every residual, so no row-level locality exists to exploit. |
| Monte Carlo with 10⁶ samples as rows (X4) | One set of nodes per sample is heavier than one array. Use a stream fold over batches. |
| Computer algebra (M1) | Needs an algebra system, which the family does not have. |
| Regulated trial monitoring (X10, S1) | Looks are few and scheduled, so nothing is incremental; the requirements are validation, audit and access control. |

---

## §8. Requirements the primitives do not meet

Each item names the capability and the workflows that need it. The list was drawn from nine guide
pages. The coordinating session then checked items 1 to 6 against source, and the lines cited from
`Ranvier/src/Ranvier/Combinators.fs` were re-read for this revision unless marked otherwise. Two
searches were made here: a case-insensitive search of `Ranvier/src/**/*.fs` for
`quantile|median|percentile|sliding|Kahan|compensat|Neumaier|tumbling|topK|sketch`, and one of
`Ranvier/src/Ranvier/*.fs` for `tolerance|epsilon|approxEqual|withinTolerance`. Both returned no
matches (*measured here*, with the Grep tool).

1. **Robust inverse folds for floating point. Confirmed.** `foldGroup` requires that `subtract`
   invert `add` (*source*: line 1774). IEEE addition does not satisfy this exactly (*general
   knowledge*), so a long-lived sum that adds and subtracts drifts, and one-pass moment formulas on
   raw power sums are unstable ⚠️ [22]. `sumBy` re-folds from the cached values only when a
   difference is infinite or NaN (*source*: lines 1858–1873). No compensated accumulator and no
   scheduled re-fold ships. `foldGroup` accepts any state type (*source*: line 1786), so a
   compensated accumulator can be passed as state today. No tolerance comparer for a float cut-off
   ships either; the comparer overloads would accept one (*local guide*: `timing.md` names a typed
   comparer). Affects S2, S3, S6, C2, C6.
2. **Sublinear order statistics.** Median, quantiles, ranks and trimmed means have no inverse. They
   are composable today as `Projection.sortBy` then `tryItem`, at O(N) per order change (*source*:
   lines 1636 and 1941; *local guide*: `aggregates.fsx`). `tryMinBy` and `tryMaxBy` cost O(1) unless
   the holder's row is removed or edited, then an O(N) re-fold (*source*: line 2216). No general
   fold with a fallible `subtract` is exposed (*source* check by the coordinating session; not
   re-read here). Missing: a balanced-tree or sketch structure. Affects S3, S4, S5, C6.
3. **Scenario forking. Not found.** No API evaluates one graph over many input vectors without
   writing to the live signals (*source* search by the coordinating session; not re-run here).
   Affects X4, X3, P6 and every what-if question.
4. **Durable caching of derived values and long flights. Confirmed.** The persistence package
   restores keyed inputs, signals and keyed collections, from a store; derived values are not
   cached durably (*from documentation*: `Ranvier.Persistence/README.md`, as checked by the
   coordinating session). A way to run a flight on another machine with progress was not found in
   the guide pages (*check*). Affects X3, X1, C7, P5.
5. **Cost of a key change. Unsettled; a *check*.** The guide says a key added or removed costs an
   O(N) key diff (*local guide*: `aggregates.fsx`, line 71). The doc comment on `foldGroup` says a
   key change costs O(Δ) up to max(64, N) changes, with an O(N) key diff on a larger burst
   (*source*: lines 1774–1775). The guide also says filters, sorts and groupings scan keys on
   membership changes (*local guide*: `collection-views.fsx`). Instrument streams whose rows must
   stay addressable need the answer. Affects P1, C4, C5, X8.
6. **Windowed and time-decayed folds.** Positional `take`, `skip` and `sub` views exist for
   projections (*source*: lines 1680, 1696 and 1717), so a count-based window over a keyed
   collection is composable. No windowed or decayed fold exists for streams, and a stream fold
   never evicts. Affects S3, C5, X9.
7. **Replayable streams.** A change to a value read by `start` restarts the fold from the seed
   (*local guide*: `streams.md`), so changing a threshold discards what an instrument already sent.
   Needed: a source that retains items, or rows kept in a collection subject to item 5.
8. **Run-time units.** F# units are erased at run time [63]. C# callers, the JavaScript target and
   user-entered formulas need a dimension check that exists at run time. Affects X5 and all of §3.
9. **Simultaneous equations.** A traced build raises when nodes keep writing each other's
   dependencies in one flush (*local guide*: `tracing.md`), so a cycle of formulas cannot be left
   to iterate through the graph. Equilibria and implicit models need a fixed-point solver as one
   node, with its own convergence state. Affects C1, P7.
10. **Lineage in release builds.** Tracing is absent from untraced builds, and the journal records
    inputs, not derivations. A reviewer's question about where a number came from needs an
    exportable derivation record. Affects X6, X7, X10.
11. **Derivatives across nodes.** First-order uncertainty and gradient-based fits need
    sensitivities carried through the graph. Not found in the guide pages read (*check*). Affects
    X4, C1, C2.
12. **Array-valued nodes.** An equality cut-off on a large array costs a full comparison, and no
    partial invalidation inside an array was found in the guide pages read (*check*). Affects C2,
    C3, P5.
13. **Access control and blinding.** Outside a reactive core; named because X10 cannot proceed
    without it.

---

## §9. Sources

Opened and read for this survey unless marked *summary*, which means the claim rests on a
search-result summary or index-page abstract and is flagged ⚠️ where used.

1. Pimentel, Murta, Braganholo, Freire. A Large-scale Study about Quality and Reproducibility of Jupyter Notebooks. MSR 2019. https://leomurta.github.io/papers/pimentel2019a.pdf
2. Samuel, Mietchen. Computational reproducibility of Jupyter notebooks from biomedical publications. GigaScience 2024. https://pmc.ncbi.nlm.nih.gov/articles/PMC10783158/
3. Chattopadhyay, Prasad, Henley, Sarma, Barik. What's Wrong with Computational Notebooks? CHI 2020. https://www.microsoft.com/en-us/research/uploads/prod/2020/03/chi20c-sub8173-cam-i16.pdf
4. Macke et al. Fine-Grained Lineage for Safer Notebook Interactions. VLDB 2021. https://arxiv.org/abs/2012.06981 (*summary*)
5. marimo project pages. https://marimo.io/for-researchers (*summary*)
6. Streamlit documentation, Optimizing app performance. https://docs.streamlit.io/develop/concepts/design/performance
7. Shiny documentation, Reactivity: an overview. https://shiny.posit.co/articles/reactivity-overview.html (*summary*)
8. Ziemann, Eren, El-Osta. Gene name errors are widespread in the scientific literature. Genome Biology 2016. https://pmc.ncbi.nlm.nih.gov/articles/PMC4994289/
9. Abeysooriya et al. Gene name errors: Lessons not learned. PLoS Computational Biology 2021. https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1008984
10. UKAuthority. PHE confirms file size issue caused COVID-19 undercounting. https://ukauthority.com/articles/phe-confirms-file-size-issue-caused-covid-19-undercounting/ (*summary*; secondary)
11. Shojania et al. How quickly do systematic reviews go out of date? Annals of Internal Medicine 2007. https://pubmed.ncbi.nlm.nih.gov/17638714/ (*summary*)
12. Elliott et al. Living systematic review: 1. Introduction. Journal of Clinical Epidemiology 2017. https://doi.org/10.1016/j.jclinepi.2017.08.010 (*summary*)
13. Simmonds et al. Living systematic reviews: 3. Statistical methods for updating meta-analyses. Journal of Clinical Epidemiology 2017. https://doi.org/10.1016/j.jclinepi.2017.08.008 (*summary*)
14. Lau et al. Cumulative meta-analysis of therapeutic trials for myocardial infarction. NEJM 1992. https://doi.org/10.1056/NEJM199207233270406 (*summary*, via R dataset documentation)
15. Johari, Koomen, Pekelis, Walsh. Always Valid Inference: Continuous Monitoring of A/B Tests. Operations Research 2022. https://arxiv.org/abs/1512.04922 (*summary*)
16. Howard, Ramdas, McAuliffe, Sekhon. Time-uniform, nonparametric, nonasymptotic confidence sequences. Annals of Statistics 2021. https://arxiv.org/abs/1810.08240 (*summary*)
17. Ramdas, Grünwald, Vovk, Shafer. Game-Theoretic Statistics and Safe Anytime-Valid Inference. Statistical Science 2023. https://arxiv.org/abs/2210.01948 (*summary*)
18. FDA. Adaptive Designs for Clinical Trials of Drugs and Biologics. 2019. https://www.fda.gov/media/78495/download (*summary*)
19. FDA. Use of Data Monitoring Committees in Clinical Trials, draft. 2024. https://www.fda.gov/media/176107/download (*summary*)
20. Chamandy, Muralidharan, Najmi, Naidu. Estimating Uncertainty for Massive Data Streams. Google, 2012. https://storage.googleapis.com/gweb-research2023-media/pubtools/pdf/43157.pdf
21. Day, Zhou. OnlineStats.jl: A Julia package for statistics on data streams. JOSS 2020. https://joss.theoj.org/papers/10.21105/joss.01816 (*summary*)
22. Pébay. Formulas for robust, one-pass parallel computation of covariances and arbitrary-order statistical moments. SAND2008-6212. https://www.osti.gov/biblio/1028931 (*summary*)
23. Soules. MCMC Monitor: online monitoring of Stan runs. Stan Discourse, 30 May 2023. https://discourse.mc-stan.org/t/mcmc-monitor-online-monitoring-of-stan-runs/31568
24. Stan Reference Manual, Posterior Analysis. https://mc-stan.org/docs/reference-manual/analysis.html (*summary*)
25. Rubin Observatory. DMTN-102: LSST Alerts, Key Numbers. https://dmtn-102.lsst.io/DMTN-102.pdf
26. Rubin Observatory. Alerts and brokers. https://rubinobservatory.org/for-scientists/data-products/alerts-and-brokers
27. Möller et al. Fink, a new generation of broker for the LSST community. MNRAS 2021. https://arxiv.org/abs/2009.10185 (*summary*)
28. Chaudhary et al. Low-latency gravitational wave alert products and their performance at the time of the fourth LIGO-Virgo-KAGRA observing run. https://arxiv.org/abs/2308.04545
29. GWCelery documentation, Design and anatomy. https://gwcelery.readthedocs.io/en/latest/design.html (*summary*)
30. Azzolini et al. The data quality monitoring software for the CMS experiment at the LHC. EPJ Web of Conferences 2019. https://www.epj-conferences.org/10.1051/epjconf/201921402003 (*summary*)
31. Bluesky documentation, Documents and Live Visualization and Processing. https://blueskyproject.io/bluesky/main/documents.html and https://blueskyproject.io/bluesky/main/callbacks.html
32. Allan, Caswell, Campbell, Rakitin. Bluesky's Ahead. Synchrotron Radiation News 2019. https://doi.org/10.1080/08940886.2019.1608121 (*summary*)
33. Bauer et al. In Situ Methods, Infrastructures, and Applications on High Performance Computing Platforms. Computer Graphics Forum 2016. https://doi.org/10.1111/cgf.12930 (*summary*)
34. NASA. Spacecraft Conjunction Assessment and Collision Avoidance Best Practices Handbook, Volume 1, NASA-SP-20205011318 Rev 2. https://ntrs.nasa.gov/api/citations/20260000453/downloads/CA_Handbook_Volume1-NASA-SP-20205011318_REV2-VOL1.pdf
35. ESA. OCDT tutorial now available. https://www.esa.int/Enabling_Support/Space_Engineering_Technology/CDF/OCDT_tutorial_now_available
36. The ESA Data Model for Concurrent Design of Space Systems. IAC-10. https://iafastro.directory/iac/archive/browse/IAC-10/D1/3/8229/ (*summary*)
37. COMET SDK Community Edition. https://github.com/STARIONGROUP/COMET-SDK-Community-Edition (*summary*)
38. Johnson, Simpson, Blom. Global Kinetic Explorer. Analytical Biochemistry 2009. https://doi.org/10.1016/j.ab.2008.12.024 (*summary*)
39. OriginLab. Recalculating Analysis Results. https://docs.originlab.com/origin-help/analydialog-recalculate/
40. Wojdyr. Fityk: a general-purpose peak fitting program. Journal of Applied Crystallography 2010. https://doi.org/10.1107/S0021889810030499 (*summary*)
41. GraphPad Prism User Guide, Everything is hot-linked. https://graphpad.com/guides/prism/latest/user-guide/everything_is_hot_linked.htm
42. Tegunov, Cramer. Real-time cryo-electron microscopy data preprocessing with Warp. Nature Methods 2019. https://pmc.ncbi.nlm.nih.gov/articles/PMC6858868 (*summary*)
43. CryoSPARC Guide, Prerequisites and compute resources setup. https://guide.cryosparc.com/live/prerequisites-and-compute-resources-setup
44. Payne et al. Readfish enables targeted nanopore sequencing of gigabase-sized genomes. Nature Biotechnology 2021. https://pmc.ncbi.nlm.nih.gov/articles/PMC7610616/
45. Munro et al. minoTour, real-time monitoring and analysis for nanopore sequencers. Bioinformatics 2022. https://pmc.ncbi.nlm.nih.gov/articles/PMC8796371/ (*summary*)
46. Thermo Fisher Scientific. Instrument Application Programming Interface. https://github.com/thermofisherlsms/iapi
47. Chiva et al. QCloud: A cloud-based quality control system for mass spectrometry-based proteomics laboratories. PLoS ONE 2018. https://doi.org/10.1371/journal.pone.0189209 (*summary*)
48. MacLean et al. Skyline: an open source document editor for creating and analyzing targeted proteomics experiments. Bioinformatics 2010. https://pmc.ncbi.nlm.nih.gov/articles/PMC2844992 (*summary*)
49. Hughes. plater: Read, Tidy, and Display Data from Microtiter Plates. JOSS 2016. https://joss.theoj.org/papers/10.21105/joss.00106 (*summary*)
50. Roch et al. ChemOS: An orchestration software to democratize autonomous discovery. PLoS ONE 2020. https://pmc.ncbi.nlm.nih.gov/articles/PMC7161969/ (*summary*)
51. Wolfram Language documentation, Dynamic Interactivity Language. https://reference.wolfram.com/language/guide/DynamicInteractivityLanguage.html
52. Distill editorial team. Distill Hiatus. 2 July 2021. https://distill.pub/2021/distill-hiatus/
53. Hohman, Conlen, Heer, Chau. Communicating with Interactive Articles. Distill 2020. https://distill.pub/2020/communicating-with-interactive-articles/ (*summary*)
54. Heer et al. Living Papers: A Language Toolkit for Augmented Scholarly Communication. UIST 2023. https://idl.uw.edu/papers/living-papers (*summary*)
55. eLife. Welcome to a new ERA of reproducible publishing. https://elifesciences.org/labs/dc5acbde/welcome-to-a-new-era-of-reproducible-publishing (*summary*)
56. PhET. Software design patterns. https://github.com/phetsims/phet-info/blob/master/doc/phet-software-design-patterns.md (*summary*)
57. Saltelli et al. Why so many published sensitivity analyses are false. Environmental Modelling & Software 2019. https://pure.au.dk/ws/files/193871031/1_s2.0_S1364815218302822_main.pdf
58. NIST Uncertainty Machine. https://uncertainty.nist.gov/ (*summary*)
59. JCGM 101:2008. Propagation of distributions using a Monte Carlo method. https://www.bipm.org/doi/10.59161/JCGM101-2008 (*summary*)
60. Landau. The targets R package. JOSS 2021. https://joss.theoj.org/papers/10.21105/joss.02959 (*summary*)
61. Snakemake documentation, Between workflow caching. https://snakemake.readthedocs.io/en/stable/executing/caching.html (*summary*)
62. NASA. Mars Climate Orbiter Failure Board Releases Report. 10 November 1999. https://nssdc.gsfc.nasa.gov/planetary/text/mco_pr_19991110.txt
63. Microsoft Learn. Units of Measure (F#). https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure
64. McKeever, Bennich-Björkman, Salah. Unit of measurement libraries, their popularity and suitability. Software: Practice and Experience 2021. https://doi.org/10.1002/spe.2926 (*summary*)
65. National Academies. Foundational Research Gaps and Future Directions for Digital Twins. 2024. https://www.nationalacademies.org/read/26894/chapter/4
66. Wolffram et al. Collaborative nowcasting of COVID-19 hospitalization incidences in Germany. PLoS Computational Biology 2023. https://pmc.ncbi.nlm.nih.gov/articles/PMC10446237/
67. EpiNow2 package README. https://cloud.r-project.org/web/packages/EpiNow2/readme/README.html (*summary*)
68. Software Sustainability Institute. International RSE Survey 2022, Tools and programming languages. https://softwaresaved.github.io/international-survey-2022/section/tools-and-programming-languages/
69. Visual Studio Code documentation, Polyglot Notebooks. https://code.visualstudio.com/docs/languages/polyglot (*summary*; no statement found either way)
