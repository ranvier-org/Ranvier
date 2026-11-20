# Science market: who would use reactive scientific computations on Ranvier, and who would pay

**Status.** Research survey, 2026-11-19. Revised the same day under the evaluation rule below: the
verdicts, the segment ranking and the risks were re-derived, and four workspace facts were added
(§4.6, §4.7). Every figure, source, date and label from the first version is unchanged. No code
changed.

**Question.** Is there a viable market for a library that packages scientific and academic
algorithms (statistics, physics, chemistry, mathematics) as reactive computations on Ranvier's
primitives, and which user segment, if any, could sustain a small maintainer team?

**Evaluation rule (set by the owner, 2026-11-19).** The existence of a competitor, an incumbent or
an earlier implementation, in-house builds included, is not counted as a reason against the idea.
Where people have adopted or paid for another tool, that counts as evidence that the need is
real. Reasons of other kinds are kept and weighed on their merits: who can author in the
language, funding eligibility, the absence of direct demand evidence, validation and maintenance
cost, and the state of .NET numerical libraries. A gap in the evidence is reported as a gap and
is not read as support.

**Scope.** Seven questions, answered from primary sources where they exist.

| Group | Sources read |
| --- | --- |
| Population surveys | Stack Overflow Developer Survey 2025 and 2026, JetBrains Developer Ecosystem 2025 and State of .NET 2025, Python Developers Survey 2024, Julia User & Developer Survey 2025, RSE International Survey 2022, SSI UK Research Software Survey 2014 |
| Company outcomes | Company announcements, SEC filings and exhibits, company fact sheets, press coverage where no primary source exists |
| Funders | Programme pages of CZI / Renaissance Philanthropy, NSF, NASA, Sovereign Tech Agency, NumFOCUS, SSI / UKRI, Sloan (via grantee announcements) |
| Package and repository counts | NuGet search API and gallery pages, pypistats API, GitHub REST API |
| Workspace documentation (added in the revision) | READMEs across the repository family, one counter benchmark report, and the science application's README and action plan |

**Conventions.** Every figure carries a source number in square brackets (see §8) and a date.
Labels follow the workspace rule:

- **read** = read here on 2026-11-19. NuGet totals are the `totalDownloads` field of the NuGet
  search API; PyPI figures are the `last_month` field of the pypistats `recent` endpoint; GitHub
  stars are `stargazers_count` from the GitHub REST API.
- **doc** = taken from the cited document or page.
- **ws** = from documentation in this workspace, at the path given in §8 [64]–[67]. Benchmark
  figures there were measured by their authors on the dates stated; this survey did not re-run
  them. Each is named with the one thing it times, and two figures are compared only when both
  come from the same benchmark.
- **press** = taken from press coverage; no primary document was opened.
- **calc** = my arithmetic on two cited figures.
- **analyst** = an analyst-firm estimate. Low trust.
- **gk** = general knowledge, not checked.
- ⚠️ **unverified** = the claim could not be confirmed in its primary source. It may come from a
  press aggregator, a single search-result summary, or a page that failed to load.

A source marked "found by search" in §8 was read through a search summary that quoted it; the
page itself was not opened. Its figures are labelled **doc** or **press** when the summary quoted
the primary document and other outlets agreed, and ⚠️ unverified otherwise. Sources without that
mark were opened and read directly.

**Signal strength.** Download counts are inflated by build servers in every ecosystem, so they are
used here only as ratios within one registry or as order-of-magnitude comparisons between
registries. Stack Overflow and JetBrains survey developers, not scientists. Only two surveys in
this report sample research programmers directly (the RSE survey [8] and the Julia survey [7]),
and their samples are about 1,000 and 721. No survey found measures how many scientists program
in .NET; the figures in §1 and §4 are the nearest proxies.

---

## §0. Executive summary

| # | Finding | Strongest evidence | Bearing on the idea |
| --- | --- | --- | --- |
| 1 | Research programmers work in Python, C++ and R. .NET is marginal among them | RSE survey 2022: Python 71.0%, C# 4.1%, F# 0.4% (4 respondents) [8] | **Limits who can author.** Few research programmers could write computations in F# or C# today. It does not limit who can use a finished application, which reaches the browser from the same sources (§4.6) |
| 2 | The .NET scientific library base is small and partly stalled | numpy had 13 times more downloads in the last month than Math.NET Numerics has had in total [39][40]; Math.NET's last stable release is 2022-04-03 | **A cost, and a second limit on authors.** Few .NET developers do numerical work, and the library would have to supply or validate numerics that the platform's own libraries no longer maintain |
| 3 | Microsoft left .NET notebooks | .NET Interactive deprecated 2026-04-24, repository archived 2026-04-27 [26] | A platform signal with no stated reason. The owner's family supplies its own surfaces (native window, browser, terminal), so nothing here depends on that notebook |
| 4 | In the comparables, value is captured by an environment or by distribution | Environment: MathWorks, $1.5 billion [11]. Distribution: Anaconda, over $150 million ARR [20]. App and notebook tools were bought by platforms (Streamlit, marimo, Noteable) | The owner's family has the components of an environment (§4.6). The formula library would be content for it, as toolboxes are for MATLAB. **Unproven:** adoption of that environment outside the owner, and payment for it |
| 5 | Grant programmes fund established software | CZI EOSS has ended; its successor requires demonstrated adoption [30]. NSF POSE does not fund product development [31]. NASA's two software elements are not solicited [36] | **Against** grants as a starting route |
| 6 | .NET is present near science in applied engineering | NuGet totals: FHIR SDK 18.1M, OPC UA 6.1M, DICOM 6.0M, QLNet 2.9M, RhinoCommon 1.8M, Excel-DNA 1.6M [39] | **For** an audience that can author in C# today: .NET product teams |
| 7 | Reactive notebooks have real adoption in Python | marimo: 2.95M downloads in the last month, 23.6k stars, acquired by CoreWeave [15][40][41] | **Evidence that the need is real:** scientists adopt tools that recompute when inputs change. The adoption is of free software and of reactivity between notebook cells; demand for formula-level computations was not measured |
| 8 | Interactive science teaching has very large usage; the income found comes from grants and curriculum sales | PhET: over 250 million simulation runs in 2025 [49]; Distill stopped in 2021 citing volunteer burnout [52] | **For** reach through browser delivery. No evidence was found that usage of this kind brings income |
| 9 | Libraries that crossed ecosystems became installable in the scientists' own language | Polars, DuckDB, Arrow [28][29][40] | Authoring from Python is not available today: Fable's Python target is beta. Delivery of finished applications to the browser is available |
| 10 | The reactive core already carries a heavy incremental workload that is not a user interface | An F# language server on the same graph: edit p50/p95 of 41.3/104.5 ms against 87.5/291.1 and 74.9/261.0 for the incumbent server, `mid-body` scenario [67] (**ws**) | Supports the engineering premise. Says nothing about demand |

**Verdict in one paragraph.** The evidence supports three statements. People who do science and
engineering adopt tools that recompute when inputs change, and in industry they pay for them (§2,
§5). The value in this market is captured by environments, and the owner's family has the
components of one: a science application of 29 pages already builds for the native window and for
the browser from the same sources (§4.6). Using such an application needs no .NET; writing
computations does, and few research programmers write F# or C# (§1). The evidence does not show
three things: direct demand for formula-level reactive computations from any segment, willingness
to pay for them, or adoption of the environment by anyone outside the owner. The reasons against
that remain are the small supply of authors, the funding routes closed to a new project (§3), and
the cost of validating and maintaining scientific routines on a platform whose own numerical
libraries are stalled (§4.2). Read together, the idea is not contradicted as the content layer of
the owner's environment, written by .NET authors and delivered as applications. It is unproven as
a business. §7 ranks the segments and lists what would settle the open questions.

---

## §1. Who would use it

### 1.1 Developers in general

| Language | Stack Overflow 2025, all respondents (n = 31,771) [1] | Stack Overflow 2026, all respondents (n = 14,099) [2] |
| --- | --- | --- |
| Python | 57.9% | 58.0% |
| C# | 27.8% | 27.1% |
| C++ | 23.5% | 24.3% |
| R | 4.9% | 4.5% |
| MATLAB | 3.9% | 3.6% |
| Fortran | 1.4% | 1.7% |
| Julia | not in the main chart | 1.5% |
| F# | 1.3% | 1.2% (172 respondents) |

All figures **doc**, read from the survey pages on 2026-11-19. The 2026 language question had
14,099 respondents; a search summary of the survey site gives 30,903 total responses
⚠️ unverified [3]. In 2026 F# was "admired" by 48.3% of its users and "desired" by 2.3% of
respondents ⚠️ unverified (search summary of [2]). F# is the least used of the five
science-adjacent languages in this table.

JetBrains surveyed 24,534 developers between April and June 2025 [4] (**doc**). Its .NET report
covers more than 3,800 of them [5] (**doc**): 98% use C# in .NET projects, 5% use Python and 3%
use F#. The workloads it lists are web (68%), backend services (50%), desktop (41%), console
tools (30%), libraries (29%), Unity game development (19%) and cloud (18%). The visible list has
no scientific, numerical or data-science category.

### 1.2 Python users

The Python Developers Survey 2024 was fielded in October and November 2024 and reports more than
25,000 responses after cleaning [6] (**doc**). 51% of respondents work on data exploration and
processing. Academic research is the single main use of Python for 8% of respondents (9% in
2023). Among respondents whose main language is Python, 28% use it for academic research, by my
reading of the page's main-and-secondary breakdown. 7% work in the science sector and 6% in
education. Among those doing data work, 80% use pandas and 75% use
NumPy.

### 1.3 Research programmers

The RSE International Survey 2022 [8] (**doc**) asked research software engineers in eight
countries which languages they use at work. The world table:

| Language | Respondents | Share |
| --- | --- | --- |
| Python | 708 | 71.0% |
| C++ | 301 | 30.2% |
| R | 269 | 27.0% |
| JavaScript | 246 | 24.7% |
| C | 193 | 19.4% |
| Fortran | 170 | 17.1% |
| Java | 143 | 14.3% |
| Matlab | 130 | 13.0% |
| Julia | 65 | 6.5% |
| C# | 41 | 4.1% |
| VB.NET | 6 | 0.6% |
| F# | 4 | 0.4% |

The base is about 997 respondents (**calc**: 708 / 0.7101). This is the most direct measure found
of .NET among people who write research software. It bears on authoring: few of these people
could write computations with the library today. It does not bear on who can use an application
built with it (§4.6). JavaScript, at 24.7%, is used by six times as many research software
engineers as C#.

The Julia User & Developer Survey 2025 [7] (**doc**; 721 interviews, 2025-06-18 to 2025-07-08,
margin of error ±3.65 points) describes the community closest in spirit to the idea: a language
built for scientific computing with reactive notebooks. The slides are charts, so the percentages
below were read from extracted text and matched to their labels by position:

- 75% use Julia for research; 15% use it in production for a business-critical task.
- 54% describe themselves as academic and 48% as professional.
- 57% also use Python regularly, 14% MATLAB, 7% R, 6% Fortran and 4% C#.
- The largest non-technical complaint, chosen by 63%, is that there are not enough Julia users in
  the respondent's field or industry. The slide's own title names it the biggest non-technical
  problem. It was 58% in 2023 and 64% in 2024 by the same reading.

Julia has 49.3k GitHub stars (**read** [41]) and its main commercial sponsor has raised about
$106.6 million ⚠️ unverified (Sacra [21]). After that investment, most of its own users still
name lack of peers as the main obstacle. A new library in a language with a smaller scientific
footprint should expect the same obstacle, at greater strength, wherever it asks scientists to
write code in that language.

### 1.4 Researchers who use software

The only survey found of researchers at large is the Software Sustainability Institute's UK
survey of 2014 [9] (**doc**; 417 responses from 15 Russell Group universities). 92% used research
software, 69% said their research would not be practical without it, 56% developed their own,
and 21% of those developers had no training in software development. No newer survey of the same
kind was found.

UNESCO counted 8.854 million full-time-equivalent researchers worldwide in 2018 ⚠️ unverified
(search summary of [10]). MathWorks states that MATLAB has more than 5 million users [11]
(**doc**, fact sheet dated March 2026). SlashData estimated 10.1 million Python developers and
6.5 million C# developers in 2021 [61] (**analyst**, and five years old).

### 1.5 What can be said about size

No source gives the number of scientists who program in .NET. Three proxies bound it:

- F# is 3% of .NET developers [5] and 1.2% of Stack Overflow respondents [2].
- C# is 4.1% and F# 0.4% of research software engineers [8]; C# is 4% of Julia users [7].
- FSharp.Stats, the main F# statistics library, has 259,781 downloads in its lifetime [39]
  (**read**). Math.NET Numerics has 83.9 million [39] (**read**). FSharp.Stats is 0.3% of
  Math.NET Numerics (**calc**).

**Bearing on the idea.** The scientist population is large, and it writes its code elsewhere. A
library that must be programmed in .NET can be authored by a few per cent of research programmers
at most, and in F# by a fraction of one per cent. This limits the supply of authors. It does not
limit the audience for what they build (§4.6).

---

## §2. Commercial comparables and their outcomes

| Company | What it sells | Figures | Outcome |
| --- | --- | --- | --- |
| MathWorks | Proprietary environment and toolboxes | $1.5 billion annual revenue, over 6,500 staff, over 5 million MATLAB users, privately held, profitable every year since founding [11] (**doc**, March 2026) | Independent and dominant |
| Wolfram Research | Proprietary environment | No official figures. Aggregators give $70 million to $155 million revenue and 640 to 760 staff [59] ⚠️ unverified | Independent, private |
| NI (LabVIEW) | Test hardware with dataflow software | Acquired by Emerson for $8.2 billion equity value, closed 2023-10-11; NI revenue $1.66 billion in 2022; about 20% of sales in software [12] (**doc**). Emerson's Test & Measurement segment: $1,464 million in fiscal 2024, $1,486 million in fiscal 2025 [13] (**doc**) | Absorbed. Segment sales are flat |
| Ansys, Altair | Simulation software | Synopsys closed its Ansys purchase on 2025-07-17 [27] (**doc**), reported at $35 billion. Siemens bought Altair at about $10 billion enterprise value ⚠️ unverified (search summary of a Siemens statement) | Absorbed into larger engineering vendors |
| Anaconda | Python distribution, governance, security | Over $150 million Series C on 2025-07-31; company states over $150 million ARR and profitability [20] (**doc**, press). Valuation about $1.5 billion ⚠️ unverified (Bloomberg, sourced) | Independent. It monetises distribution, not algorithms |
| Posit (RStudio) | Open core: IDE and publishing servers | Public benefit corporation. Revenue is not disclosed; third-party estimates conflict [60] ⚠️ unverified. Its 2025 benefit report could not be loaded | Independent |
| Streamlit | Python app framework, hosted service | Raised $62 million; Snowflake agreed in March 2022 to buy it for about $800 million [14] (**doc**, Snowflake 10-K). 46.4k stars, 24.0M PyPI downloads in the last month (**read** [40][41]) | Absorbed after about three years. The open-source project continues |
| marimo | Reactive Python notebook | CoreWeave announced the acquisition on 2025-10-30, terms undisclosed [15] (**doc**). Team of eight at the time. Prior funding about $5 million ⚠️ unverified | Absorbed after about two years |
| Observable | JavaScript reactive notebooks | $35.6 million Series B on 2022-01-13, $46.1 million in total [16] (**doc**). Moved its emphasis to Observable Framework in February 2024, announced the deprecation of Observable Cloud on 2025-04-15 ⚠️ unverified (the release note [17] returned an error; only its title was read), previewed a rewritten notebook format in July 2025 and shipped the rewrite in September 2026 [17] | Pivoted repeatedly. No acquisition or revenue figure found |
| Hex | Hosted data notebooks for analytics teams | $70 million Series C on 2025-05-28 [18] (press). About $172 million in total ⚠️ unverified | Growing, and positioned at business analytics, not science |
| Deepnote | Hosted data notebooks | $20 million Series A on 2022-01-31 [19] (**doc**). Open-sourced its notebook in November 2025 [19]. 3.0k stars (**read** [41]) | Changed model to open source; no new round found |
| Noteable | Hosted Jupyter notebooks | Ceased operations; the team joined Confluent in February 2024, terms undisclosed [25] | Stalled, then absorbed |
| Plotly | Open core: Dash Enterprise | Aggregators disagree on funding, from $8.3 million to $21.6 million [22] ⚠️ unverified. Dash has 24.5k stars (**read** [41]) | Independent and small relative to its usage |
| JuliaHub | Julia platform, now industrial modelling | $65 million Series B on 2026-04-30 [21] (**doc**). Earlier rounds of $24 million (2021) and $13 million (2023) ⚠️ unverified (Sacra). The Series B announcement describes the product, Dyad, as an agentic AI platform for hardware and industrial engineering [21] | Pivoted from general scientific computing towards industrial digital twins |
| Quansight | Consulting that cross-subsidises open-source work | Consulting revenue funds its Labs division, with sponsors and CZI grants [23] (**doc**). A reported divestiture of the consulting business in May 2025 is ⚠️ unverified | Independent; restructuring |
| Enthought | Formerly a Python distribution; now consulting | Describes itself as digital-transformation consulting for science R&D, with a five-year agreement with Tokyo Electron announced in January 2023 [24] (**doc**) | Pivoted from product to services |
| Polars | Dataframe library, hosted distributed engine | €18 million Series A on 2025-09-29 [28] (**doc**) | Independent, early |
| DuckDB / MotherDuck | Embedded analytics database, hosted service | MotherDuck: $52.5 million Series B in September 2023, $100 million in total [29] (press) | Independent |
| Modelon | Modelica-based system simulation | No funding, revenue or acquisition figure was found | Not assessed |

Three smaller data points belong beside the table. Microsoft deprecated Polyglot Notebooks on
2026-03-27 and .NET Interactive on 2026-04-24, and archived the repository on 2026-04-27 [26]
(**doc**). The announcement gives no reason beyond "stepping back from further investment in this
area" [26]. JetBrains is reported to be ending Kotlin Notebook ⚠️ unverified [63]. Commercial
.NET numerical libraries exist and are small by download count: Extreme.Numerics 1.82M lifetime
downloads, CenterSpace.NMath 182K, Numerics.NET 168K and ILNumerics.Core 62K [39] (**read**).
Their revenues are not public.

**What the outcomes have in common.**

1. The durable businesses sell a complete environment to industry engineers (MathWorks) or sell
   distribution and compliance around open-source code (Anaconda). In both cases the payment is
   for the environment or the distribution.
2. Notebook and app-framework companies were bought by data and AI infrastructure vendors
   (Snowflake, CoreWeave, Confluent) within two to three years, or they pivoted (Observable,
   Deepnote). Their adoption is evidence of need (§5), and the purchases show that platform
   vendors value that adoption. None in the sample became a durable independent business on
   notebooks alone, which is a fact about that business model.
3. The companies that began in general scientific computing and survived moved towards industry:
   JuliaHub to digital twins, Enthought to consulting for R&D laboratories. That is where the
   payment was found.
4. The products closest in kind to the idea (LabVIEW, Simulink, Dyad) join a dataflow model to
   hardware, code generation or solvers and are sold to engineering organisations. Under the
   evaluation rule their existence is evidence that the need is real. Their shape suggests that
   the unit which is bought is an environment. No comparable was found that sells a library of
   scientific algorithms on a reactive runtime by itself.

**Bearing on the idea.** In these outcomes the money went to environments, to distribution and to
platforms. The owner's family has the components of an environment (§4.6), and the formula
library would be content for it. It has no distribution to speak of, and this survey could cite
no adoption outside the owner. The outcomes show where payment has occurred. They do not show
that it would occur here.

---

## §3. Funding for open-source scientific software

| Programme | Status on 2026-11-19 | Award size | Open to a new library by a small team? |
| --- | --- | --- | --- |
| CZI Essential Open Source Software for Science | Six cycles, $58 million, over 230 projects [30] (**doc**). No new cycle; the last call is reported to have closed on 2023-10-17 ⚠️ unverified. CZI has made no closing statement that was found | About $252,000 per project on average (**calc**: $58 million / 230) | No longer available |
| Open Source for Science Fund (Renaissance Philanthropy; Biohub, Wellcome, Kavli, ReSA) | Launched 2026-05-04 with $20 million of anchor funding, designed by the former EOSS team; letters of intent from 2026-05-11 [30] (**doc**) | Track 1 up to $250,000; Track 2 up to $1,000,000 | No. Track 1 requires demonstrated adoption in the life sciences; Track 2 targets broadly used libraries |
| NSF POSE | Solicitation NSF 24-606 is archived and replaced by NSF 26-506, whose terms were not read ⚠️ unverified [31] | Phase I up to $300,000 for one year; Phase II up to $1.5 million for two years [31] (**doc**, earlier solicitation) | No. The programme page says it is "not intended to fund the development of open-source products" [31] and requires an existing product with external users |
| NASA ROSES open-source elements | F.7 (tools, frameworks and libraries) and F.14 (high-priority open-source science) are both marked not currently solicited [36] (**doc**) | F.14 was about $100,000 for one year and accepted new tools; F.7 funded existing tools for three to five years | Not at present |
| Sloan Foundation, Technology programme | Active. Recent grants fund university open-source programme offices at about $720,000 to $750,000 each [32] (**doc**, grantee announcements) | Most projects $50,000 to $600,000 ⚠️ unverified (a university notice [32]) | Unlikely. The focus is institutions and economic models, not single libraries |
| Schmidt Sciences (Virtual Institute for Scientific Software) | $40 million committed in 2022 to four university centres for five years [33] (press). No open call for external projects was found | Not applicable | No. It funds engineering teams inside universities |
| NumFOCUS Small Development Grants | Active, two or three rounds a year [34] (**doc**) | Up to $10,000 per proposal | Only for NumFOCUS fiscally sponsored or affiliated projects |
| Sovereign Tech Fund (Sovereign Tech Agency, Germany) | Active; applications through its portal [35] (**doc**). About €23.5 million invested in over 60 projects by late 2024 ⚠️ unverified | Project cost above €50,000 | No. Criteria include prevalence and vulnerability of existing base technology, and the page says prototypes are not financed |
| UKRI / SSI Research Software Maintenance Fund | Round 2 funded 19 of 229 applications [37] (**doc**); 8.3% success (**calc**). Total pot £4.8 million in the terms and conditions, £5.8 million on another SSI page | Up to £150,000 for up to 12 months | No. The lead must be at a UK research organisation, and the fund maintains existing software |
| Wellcome | Anchor funder of the Open Source for Science Fund [30]. No separate software call was found | See above | See above |
| Gordon and Betty Moore Foundation | No current software programme was found | Not assessed | Not assessed |

Two further observations. The United States launched the Genesis Mission with more than
$5 billion of federal commitments and 278 selected projects, announced on 2026-07-22 [38]
(**doc**); the release does not mention software libraries or open-source tooling. PhET, the
largest interactive-simulation project, is reported to have lost an NSF POSE Phase II grant on
2025-04-25 ⚠️ unverified (a LinkedIn post found by search), which suggests that even established
recipients cannot treat US federal software funding as stable.

**Bearing on the idea.** Every active programme funds software that is already widely used, or
requires an institutional home, or both. The one element that accepted new tools (NASA F.14) is
not being solicited. Grants become available after adoption, most plausibly through an academic
collaborator in the life sciences; they cannot pay for the first two years.

---

## §4. .NET and F# in science: evidence for and against

### 4.1 NuGet download counts

Totals are the NuGet search API's `totalDownloads`, **read** on 2026-11-19 [39]. The per-day
figure is the one the gallery page displays; the page does not state its averaging window, so it
is shown only for comparison between rows. Stars and last-push dates are from the GitHub API
[41].

| Package | Total downloads | Per day (gallery) | Latest stable release | Repository |
| --- | --- | --- | --- | --- |
| MathNet.Numerics | 83,939,607 | 42.9K | 5.0.0, 2022-04-03 (a 6.0.0 beta dates from 2025-03-02) | 3,772 stars; last push 2025-03-03 |
| Microsoft.ML | 18,793,676 | 18.2K | 6.0.0, 2026-11-11 | 9,364 stars; active |
| Deedle | 2,935,448 | 1.5K | 8.1.0, 2026-08-24 | 1,006 stars; active |
| Plotly.NET | 1,616,597 | 881 | 6.0.0, 2026-10-29 | 866 stars; active |
| DiffSharp.Core | 313,860 | 179 | 1.0.7, 2022-03-26 | 616 stars; no release since 2022 |
| FSharp.Stats | 259,781 | 120 | 0.6.0, 2025-01-07 (a 1.0.0 preview dates from 2025-10-21) | 228 stars; active |

For scale, from the same API on the same day: Newtonsoft.Json 9.88 billion, System.Reactive
467.8M, FSharp.Core 151.6M, Apache.Arrow 59.7M, UnitsNet 27.0M, MathNet.Numerics.FSharp 12.6M,
Microsoft.Data.Analysis 12.0M, Accord.Math 10.5M (repository archived, last push 2020-11-18),
OxyPlot.Core 10.4M, ScottPlot 6.9M, NumSharp 6.2M, TorchSharp 1.59M, MathNet.Symbolics 0.81M,
AngouriMath 0.35M.

For comparison with Python, pypistats `last_month` on 2026-11-19 [40] (**read**): numpy 1,113.2M,
pandas 710.3M, pyarrow 437.8M, scipy 392.1M, sympy 169.9M, duckdb 81.2M, polars 68.7M, jupyterlab
34.7M, streamlit 24.0M, pint 8.6M, marimo 2.95M.

- numpy was downloaded 13.3 times more in one month than Math.NET Numerics in its lifetime
  (**calc**). scipy's month is 4.7 times Math.NET's lifetime (**calc**).
- sympy's month is about 210 times the lifetime total of MathNet.Symbolics (**calc**).
- The physical-units library is the exception: UnitsNet has 27.0M lifetime downloads against 8.6M
  a month for Python's pint. Units matter to .NET engineering code.

The two registries count differently and both include build servers, so these ratios show order
of magnitude only. On the gallery's per-day figure, Math.NET Numerics runs at about 1.3 million
downloads a month (**calc**: 42.9K × 30), which would put numpy about 850 times higher. The
window behind the per-day figure is not stated, so the safe statement is that the gap is between
two and three orders of magnitude.

### 4.2 Evidence against a large authoring population on .NET

- Research software engineers: C# 4.1%, F# 0.4% [8]. Julia users: C# 4% [7].
- The most used .NET numerical library has had no stable release since April 2022 [39]. The
  automatic-differentiation library has had none since March 2022 [39]. Accord.NET is archived
  [41].
- Microsoft withdrew its own notebook for .NET in 2026 [26].
- .NET developers reach for Python (5%) more often than F# (3%) as a second language [5].
- The F# statistics library has a quarter of a million downloads in total [39].

These points concern who writes numerical code on .NET and what the platform maintains. The
second is also a cost: scientific routines would be written or validated here, because the
platform's libraries cannot be relied on to keep pace. None of the points concerns who can use an
application once it is built (§4.6).

### 4.3 Where .NET is present near science

NuGet lifetime totals, **read** 2026-11-19 [39]:

| Area | Package | Total downloads | Note |
| --- | --- | --- | --- |
| Healthcare data | Hl7.Fhir.R4 | 18,052,902 | Clinical data interchange |
| Medical imaging | fo-dicom | 6,005,499 | 1,220 stars [41] |
| Industrial automation | OPCFoundation.NetStandard.Opc.Ua | 6,080,545 | Plant and device telemetry |
| Quantitative finance | ParquetSharp | 3,177,455 | Maintained by G-Research, a quantitative research firm that also funds F# compiler work [44] |
| Quantitative finance | QLNet | 2,888,081 | QuantLib port |
| Quantitative finance | ExcelDna.AddIn | 1,553,857 | .NET functions inside Excel |
| Quantitative finance | QuantConnect.Lean | 486,597 | 22.6k stars [41]; a C# engine |
| Engineering design | RhinoCommon, Grasshopper | 1,802,261 and 1,259,885 | Plug-in SDKs. Grasshopper is itself a dataflow graph editor (**gk**) |
| Test and measurement | OpenTAP | 759,743 | Test automation; 262 stars [41] |
| Test and measurement | NationalInstruments.Visa | 48,122 | NI documents native .NET APIs for its drivers [42] |
| Laboratory automation | Tecan.Sila2 | 255,281 | SiLA 2 laboratory device standard |
| Neuroscience experiments | Bonsai.Core | 230,853 | A visual reactive language built on Rx.NET; 201 stars [41] |
| Mass spectrometry | Thermo Raw File Reader | not on NuGet | Described by PNNL as a .NET wrapper for Thermo's reader [43] |
| Simulation in Unity | ml-agents | not applicable | 19.9k stars [41]; 19% of .NET developers work on Unity games [5] |

Unity reported $503 million revenue in the fourth quarter of fiscal 2025 and does not break out
industry or digital-twin revenue [45] (press copy of the release). An older article puts Unity's
industry business at about $58 million in 2023 ⚠️ unverified [45].

Python's bridge into .NET is used: pythonnet had 2.55M PyPI downloads in the last month [40] and
3.52M NuGet downloads in total [39] (**read**). The most likely reason is Python scripts calling
.NET vendor SDKs (**gk**).

No primary evidence was found for .NET's share of LIMS or medical-device software. The common
claim that both lean on .NET is ⚠️ unverified (**gk**).

Bonsai is the nearest existing precedent to the idea: a reactive dataflow tool on .NET, adopted
by experimental neuroscience laboratories. It is evidence that the combination meets a real need
in at least one scientific field. Its counts (231K downloads, 201 stars) are the only measure
found of such adoption. They describe one tool in one field and set no ceiling.

### 4.4 Routes beyond .NET

| Route | State | Evidence |
| --- | --- | --- |
| JavaScript through Fable | Stable | Fable.Core 6.47M downloads [39]; Fable repository 3,170 stars, pushed 2026-11-18 [41]. 24.7% of research software engineers use JavaScript at work [8] |
| WebAssembly | Available through the .NET runtime | No adoption figure was found for scientific use. Payload size relative to plain JavaScript was not measured (**gk**) |
| Python through pythonnet | Works, and requires a .NET runtime on the user's machine | 2.55M PyPI downloads a month [40] |
| Python through Fable | Beta | Fable's documentation describes the Python target as beta, with breaking changes possible between minor versions ⚠️ unverified (search summary of [46]). Fable.Python has 157,720 downloads in total [39] |

### 4.5 Precedent for crossing ecosystems

| Library | Core | Monthly PyPI downloads [40] | .NET uptake [39] | Funding |
| --- | --- | --- | --- | --- |
| Arrow | Format specification with C++ and other implementations | pyarrow 437.8M | Apache.Arrow 59.7M total | Apache project |
| DuckDB | C++, in-process | 81.2M | DuckDB.NET.Data 1.03M total | MotherDuck $100 million [29] |
| Polars | Rust | 68.7M | Polars.NET 10,650 total | €18 million Series A [28] |

What these three share (**gk**, drawn from their designs, not from a study):

1. A native core with a C interface and no managed runtime, so the host installs one ordinary
   package.
2. Delivery through the host ecosystem's own package manager, with an API written in the host's
   idiom.
3. A pain the host's users already felt and could measure: dataframe speed and memory, analytics
   without a server, copies at language boundaries.
4. Direction. All three moved into Python, R and JavaScript, where the users already were. None
   moved outward from a managed runtime.

Polars states that its monthly usage grew from 250 thousand to over 23 million between its seed
and Series A rounds [28] (**doc**; the company calls these users, while PyPI counts downloads).

These precedents concern libraries that scientists program against. On that measure a .NET
library fails the first test unless it is compiled to something else. Compiled to JavaScript it
passes the first two for JavaScript hosts. Python authors are not reached until the Fable Python
target matures (§4.4). Delivery of a finished application is a different case and does not face
these tests (§4.6).

The third test is open, and it is a gap in the evidence. No direct sign was found, for or
against, that scientists feel a pain which formula-level reactivity, stream inputs or incremental
aggregates would relieve. The adoption of reactive notebooks (§5) shows that recomputation on
change is wanted. It was measured between notebook cells, and nothing found measures the finer
level.

### 4.6 Delivery reach: what the owner's family already provides

The idea was framed as one library. The workspace holds a family of packages around the reactive
core [64] (**ws**; verified by the coordinating session from project files and READMEs across the
family, and not re-read in full for this survey):

- a reactive core with a C# facade;
- a typed view layer with server HTML and hydration;
- a native window host built on WebKit's WebCore and JavaScriptCore, packaged for win-x64,
  win-arm64, linux-x64, linux-arm64 and osx-arm64 in GPU and CPU flavours;
- a WebGPU 3D layer, native on six runtime identifiers, and in the browser on WebGPU with a
  WebGL2 fallback;
- a charting and visualisation library, a terminal UI for Windows, Linux and macOS, a text-editor
  component, typed CSS, animation and keyboard handling;
- persistence to file, browser storage, PostgreSQL and an event store, JSON and binary wire
  codecs, and remote queries;
- graph tracing, an input journal, and machine-checked proofs of the synchronous core.

The targets are .NET 10, .NET 8, .NET Standard 2.1, NativeAOT-compatible packages, and JavaScript
for the browser and Node.js [64] (**ws**).

One science application of 29 pages builds from the same sources for the native window and for
the browser [65] (**ws**: `AstroShowcase.Web/README.md` states that the same sources build the
native window; `AstroShowcase/README.md` and `AstroShowcase/ACTION-PLAN.md` give the page count).

Two consequences follow, and they must be kept apart.

1. **Delivery is not bound by the language surveys.** A person with a browser, or with a desktop
   on one of the packaged platforms, can use what is built without installing .NET. The audiences
   of §5 and the 24.7% of research software engineers who use JavaScript [8] can be reached as
   users.
2. **Authoring still is bound by them.** Computations are written in F# or C#. The figures of §1
   and §4.1 describe the supply of people who can do that today, and this section does not change
   them. Every application for a scientist has to be written by someone who knows .NET.

In the terms of §2 this is the shape of an environment: a computation model, a display layer,
hosts and storage. What the comparables had in addition was distribution and users. No figure for
adoption of the family outside the owner was available to this survey.

### 4.7 What is known about the engine

Two workspace measurements bear on whether the core can carry scientific workloads. Neither bears
on demand.

Counter benchmark `e13f159`, run on 2026-09-30 on .NET 10.0.12, reports retired instructions per
operation [66] (**ws**; all figures are from this one benchmark):

| Operation timed | This core | FSharp.Data.Adaptive 1.2.27 | R3 1.3.1 |
| --- | ---: | ---: | ---: |
| create: one root of 1000 rows | 2,239,733 | 12,687,324 | 1,572,257 |
| update: write every 10th of 1000 rows | 63,408 | 874,969 | 26,406 |
| chain: write the source of 4 memos, read the tail | 2,047 | 8,419 | 267 |
| cutoff: write an equal value to an observed source | 51.9 | 736.5 | 43.0 |
| dispose: one root of 1000 rows | 396,898 | 3,625,429 | 512,466 |

FSharp.Data.Adaptive takes between 4.1 and 14.2 times the instructions of this core across the
five operations (**calc**). R3 takes fewer instructions than this core in four of the five. The
report notes that R3 pushes each write to its subscribers without batching or glitch-free
ordering, and that its chain holds no cached value [66].

An F# language server built on the same graph reports, for its `mid-body` scenario, edit p50/p95
of 41.3/104.5 ms against 87.5/291.1 and 74.9/261.0 for two configurations of the incumbent
server, and a peak working set of 422.8 MB against 762.7 and 811.8 [67] (**ws**; the passage read
gives no measurement date). Its README presents it as a replacement server for several editors
and as an analysis tool for coding agents, and the coordinating session reports that it is in
use. It shows the graph carrying a large incremental workload that is not a user interface.

**Bearing on the idea.** .NET's weight near science is in applied engineering software, and that
is where authors can be found today. The audience for what they build is wider than .NET, because
delivery reaches the browser and the desktop. What remains against is the small supply of
numerical authors on .NET and the state of its numerical libraries.

---

## §5. Adjacent demand signals

**Reactive notebooks.** marimo has 23,621 stars and 2.95M PyPI downloads in the last month
[40][41] (**read**); at its acquisition on 2025-10-30 it reported over 1 million monthly
downloads and 16.6k stars [15] (**doc**). Its monthly downloads are 8.5% of jupyterlab's
(**calc**). Pluto.jl has 5,409 stars [41] (**read**) and was used frequently by about 19% of
Julia survey respondents in 2025, down from about 25% in 2024 ⚠️ unverified (the percentages were
read from extracted chart text and matched to labels by position [7]). Microsoft and reportedly
JetBrains withdrew their notebooks in 2026 [26][63], and Observable has rebuilt its notebook
twice [17]. Press coverage of the Microsoft decision reports objections from users, whose number
was not found [63] (**press**).

Under the evaluation rule this adoption is evidence that scientists want computations that update
when inputs change. Two limits apply to it. The software adopted is free, so it shows need and
not payment. And it measures reactivity between notebook cells, not between formulas.

**AI for science.** The Genesis Mission carries more than $5 billion of US federal commitments
[38] (**doc**). Venture rounds reported for autonomous-laboratory companies include $300 million
for Periodic Labs and $235 million to $550 million for Lila Sciences ⚠️ unverified [62].
JuliaHub's $65 million round was raised on a pitch of agentic AI for industrial engineering [21].
The money found is going to compute, laboratories and models. No source showed spending on
libraries of scientific formulas.

**Digital twins.** Analyst estimates of the 2025 market are $21.1 billion (MarketsandMarkets),
$29.1 billion (The Business Research Company) and $36.2 billion (Mordor Intelligence) [48]
(**analyst**, low trust; the spread suggests the term has no agreed boundary). The firmer
evidence is transactions: Ansys at a reported $35 billion [27], Altair at about $10 billion
⚠️ unverified, JuliaHub at $65 million [21]. The spending is real, and the buyers are large
engineering organisations. No evidence was found on how such buyers assess a small supplier.

**Laboratory automation.** Analyst estimates for 2025 run from $7.3 billion (Knowledge Sourcing
Intelligence) to $18.0 billion (DIResearch) [48] (**analyst**, low trust). The one audited figure
found for the neighbouring test-and-measurement business is Emerson's segment, which grew 2% in
fiscal 2025 [13] (**doc**).

**Real-time analytics.** IBM completed its purchase of Confluent on 2026-03-17 at an enterprise
value of about $11 billion; Confluent reported more than 6,500 enterprise customers [47]
(**doc**). Streaming data is established infrastructure. This supports the stream-memo primitive
as relevant to industry and says nothing about scientific formulas.

**Interactive educational material.** PhET delivered more than 250 million simulation runs to
more than 50 million learners in 2025, and more than 1.8 billion runs since 2002 [49] (**doc**,
impact report, marked draft). Its budget is about $4 million a year, of which grants and
donations are reported to be 85% ⚠️ unverified. Desmos sold its curriculum business to Amplify in
May 2022 and kept its calculators in a public benefit corporation [50] (**doc**). GeoGebra was
bought by BYJU'S in December 2021 for a reported $100 million that neither company confirmed [51]
⚠️ unverified. Distill, the journal for interactive explanations of machine learning, went on
hiatus on 2021-07-02 and cited volunteer burnout ⚠️ unverified (the wording was read from a
mirror of [52]). Usage is very large; the income comes from grants, curriculum sales and
acquirers.

**Bearing on the idea.** Each neighbour of the idea shows a real need: reactive notebooks, live
industrial models, streaming data and interactive teaching. Under the evaluation rule the tools
people have adopted and the sums they have paid count as evidence of that need. Payment was found
for live industrial models and for streaming infrastructure. It was not found among scientists
and teachers, whose tools are free or grant-funded. None of the signals is a direct measurement
of demand for formula-level reactive computations.

---

## §6. Sustainability models open to a small maintainer team

| Model | Where it worked | Where it did not | Realistic outcome here |
| --- | --- | --- | --- |
| Grants | NumPy, SciPy and conda-forge work funded through CZI at Quansight Labs [23]; PhET over two decades [49] | EOSS ended after six cycles [30]; NASA elements not solicited [36]; Distill ran on volunteers and stopped [52] | Nothing for the first years (§3). Possible later through an academic partner |
| Open core | Posit, Plotly (Dash Enterprise), Polars (hosted distributed engine) [22][28] | Works only when enterprises need a deployment, governance or scale layer that the library lacks | Not applicable until organisations run the library in production |
| Hosted service | Streamlit and marimo built hosted tiers and were then acquired [14][15] | Noteable ceased operations [25]; Observable announced the end of its hosting product ⚠️ unverified [17] | Technically open: the family has server rendering, remote queries and persistence (§4.6). Hosting needs capital and operations staff, and no demand for a hosted form was found |
| Consulting | Quansight [23]; Enthought, which left product for consulting [24] | Income scales with hours. Quansight is restructuring its consulting arm ⚠️ unverified | The most available route for a very small team with domain depth |
| Dual licence or relicensing | ImageSharp (split licence since 2022) [56]; AutoMapper and MediatR (commercial editions from 2025-07-02) [54]; MassTransit (commercial from version 9) [54] | FluentAssertions moved to a paid licence in January 2025 at a reported $129.95 per developer per year and drew a strong backlash [53]; Moq's SponsorLink in August 2023 damaged trust in the library [55] | Not available. Each case relied on an installed base built over a decade under a permissive licence |
| Sponsorship | Avalonia: $3 million over three years from Devolutions, announced 2025-07-01 and conditional on the MIT licence [57] | Tidelift, which paid maintainers from subscription income, was sold to Sonar in December 2024 [58]. General sponsorship income for small libraries was not measured here | Plausible only from a company whose product depends on the library, as in the Avalonia case |
| Proprietary numerical library | Extreme Optimization, CenterSpace and ILNumerics have sold .NET numerics for years | Not known: revenues are not public, and the download counts are in §2 | Size of the paying market unknown. It would give up the MIT licence |

The licence and sponsorship cases in this table come from press coverage and project
announcements found by search [53]–[58]; none of those pages was opened directly.

The Avalonia arrangement is the most relevant positive example in .NET. One company whose main
product is built on the framework pays for the framework's open development. The equivalent here
is a vendor of instrument, laboratory or engineering software that builds on Ranvier and needs
the domain packages to exist.

**Bearing on the idea.** For a team of this size the workable combinations are consulting, with
the family and its science applications as the showcase, or one anchor sponsor with a product
dependency. The other models need either an installed base or capital.

---

## §7. Risks and a verdict

### 7.1 Segment ranking

The ranking uses four tests. Can the segment author in C# or F#, or would it use what others
build? Is there evidence that the need exists, counting adoption of and payment for other tools as
such evidence? Is there evidence of payment in the segment? What limits remain that are not about
other products? No segment has direct evidence of demand for this idea, so that test does not
separate them.

| Rank | Segment | Role | Evidence of need | Evidence of payment | What limits it |
| --- | --- | --- | --- | --- | --- |
| 1 | .NET product teams near instruments, devices and engineering tools (test and measurement, laboratory automation, healthcare and imaging software, design-tool plug-ins) | Authors, in C# | Moderate. Dataflow tools for instruments are bought and used: Emerson's Test & Measurement segment had sales of $1,486 million in fiscal 2025 [13], and Bonsai is used in neuroscience laboratories [39]. .NET is established in these fields (§4.3) | Moderate for the category. None for this idea | No direct demand signal. The validation cost of regulated uses was not estimated |
| 2 | Digital twins and industrial monitoring | Authors in industrial .NET teams; users on the native and browser hosts | Strong. Ansys at a reported $35 billion [27], JuliaHub's $65 million round [21], Confluent at about $11 billion [47]. Stream inputs, live models and 3D display match the family's components (§4.6) | Strong for the category. None for this idea | No direct demand signal. No evidence on how large buyers assess a small supplier. Validation cost |
| 3 | Quantitative finance teams on .NET | Authors, in F# and C# | Moderate, and partly general knowledge. Recalculation over a dependency graph is built in-house and in the open: Jane Street's Incremental has 1,550 stars [41] (**read**). Excel-DNA, QLNet and ParquetSharp show .NET in the work [39] | None found | The need rests on package counts and general knowledge. No buyer was identified |
| 4 | Scientists, students and teachers as users of delivered applications | Users. Authoring falls to the owner or to another .NET author | Strong for use: PhET's 250 million runs in 2025 [49], 92% of researchers using research software [9], and the adoption of reactive notebooks (§5). Delivery to the browser is shown by the 29-page application [65] | Weak. The payers found are grant-makers and curriculum buyers [49][50] | Grant routes stay closed until there is adoption (§3). Each application needs an author who knows .NET |
| 5 | Reactive-notebook users as authors | Authors, who write Python, Julia or JavaScript today | Strong that they want recomputation on change: marimo at 2.95M downloads a month [40], Pluto used by about a fifth of Julia respondents [7] | None. The tools adopted are free | Language: authoring means F# or C#, or a Python target that is in beta. Whether they want reactivity at the level of formulas was not measured |
| 6 | Other research programmers as authors | Authors | Weak for this idea. Their number is large (§1) | None found | Language: C# 4.1%, F# 0.4% [8]. The 4.1% who use C# are the likeliest authors of applications for segment 4 |

Grants are a funding route, not a segment, and are closed to a new project (§3).

Three changes from the first version of this ranking. Digital twins rose from fourth to second,
and reactive-notebook users from sixth to fifth: the reasons that held them down were that large
vendors and a free tool already serve them, and those reasons no longer count. Research
scientists were one segment and are now two, split by role. As users of delivered applications
they rank fourth. As authors they stay last, on language grounds alone.

### 7.2 Risks, in order of weight

1. **Authors are scarce.** Every population measure agrees that research programmers write
   Python, C++ and R (§1, §4.1). Julia's own users, after far greater investment in their
   language, still name the lack of peers in their field as their main obstacle [7]. Delivery to
   the browser and the desktop removes this limit for users of finished applications (§4.6). It
   leaves the limit in place for anyone expected to write computations, so the supply of
   applications depends on a small group.
2. **No direct demand has been observed.** No request, interview, issue or purchase was found
   that asks for formula-level reactive computations. Everything in §5 is evidence of a
   neighbouring need.
3. **No evidence of payment.** The adoption found among scientists is of free software. Payment
   was found for environments and for live industrial models (§2, §5). The owner's family has the
   components of an environment. Whether it can be sold or sponsored as one is untested, and it
   has no distribution yet.
4. **The first years cannot be funded from outside.** Grants require adoption, relicensing
   requires an installed base, and hosted services require capital (§3, §6).
5. **The platform's numerical base is thin.** Microsoft ended .NET Interactive, and the leading
   community numerical libraries are stalled (§4.2). The family supplies its own display and host
   surfaces, so the exposure is to numerics: routines would be written or validated here.
6. **Correctness liability.** Scientific and statistical routines need validation against
   reference implementations and sustained maintenance. Math.NET and DiffSharp show what happens
   to that maintenance when it depends on volunteers [39]. Uses in instruments and devices
   (segment 1) raise the cost. No cost estimate was made here.

### 7.3 What the evidence supports, and what it does not

The evidence supports:

- **The need is real.** Scientists adopt tools that recompute on change (marimo, Pluto), learners
  run interactive simulations in very large numbers (PhET), and industry pays for dataflow
  environments, live models and streaming data (§2, §5).
- **The unit that is bought is an environment, and the owner has the components of one** (§2,
  §4.6).
- **Delivery reaches people who never write .NET.** One 29-page science application builds for
  the native window and the browser from the same sources [65].
- **The engine carries heavy incremental work.** The measurements in §4.7 are from the workspace
  and concern engineering only.

The evidence does not show:

- direct demand for formula-level reactive computations, from any segment;
- willingness to pay for them;
- adoption of the environment by anyone outside the owner, apart from the reported use of the
  language server (§4.7);
- that scientists would write computations in F# or C#;
- that reactivity finer than a notebook cell matters to scientists. This was not measured in
  either direction.

Reasons against that remain, none of which rests on another product:

- **Authoring demographics.** C# 4.1% and F# 0.4% among research software engineers [8].
- **Funding eligibility.** Every programme found funds established software or requires an
  institutional home (§3).
- **Validation and maintenance cost,** on a platform whose own numerical libraries are stalled
  (§4.2).

Taken by reading:

- As content for the owner's environment, written by .NET authors and delivered as applications
  to engineers, scientists and students: supported on need and on delivery, unproven on demand
  and on payment.
- As a library that scientists adopt and program against: the language evidence is against it
  until authoring from Python is possible.
- As a business: there is evidence of category spending in segments 1 and 2 and no evidence
  either way for this idea.

### 7.4 What would settle it

| Open question | What would answer it |
| --- | --- |
| Is there direct demand, and from whom? | Interviews with five to ten teams in segments 1 to 3. This survey found population evidence and need evidence, and no demand evidence |
| Would anyone pay? | One instrument, laboratory-software or engineering vendor committing as an anchor sponsor or customer, on the Avalonia model [57] |
| Can someone outside the owner build with it? | A pilot in which one outside .NET team builds a computation and a screen on the packages. It would also give a first figure for validation cost |
| Do scientists and students use what is delivered? | Publishing the browser build of a science application and measuring its use |
| How much does the language cost an author from outside .NET? | One such author completing a computation, with the time recorded |
| Can scientists author from Python? | The Fable Python target leaving beta, and one domain package that Python users install on its merits. This would reopen segments 5 and 6 |
| Can grants fund it later? | An academic collaborator with an adopted use in the life sciences, which would open the first track of the Open Source for Science Fund [30] |

### 7.5 Not found

Modelon's finances; a current Moore Foundation software programme; the terms of NSF 26-506;
EPSRC research-software fellowships; JetBrains' all-respondent language shares for 2025; primary
evidence on .NET in LIMS and medical devices; Posit's revenue and open-source spending; any
survey of researchers' software use newer than 2014.

---

## §8. Sources

All pages were read on 2026-11-19 unless a note says otherwise.

1. Stack Overflow Developer Survey 2025, Technology. https://survey.stackoverflow.co/2025/technology
2. Stack Overflow Developer Survey 2026, languages data. https://survey.stackoverflow.co/2026/technology/data/languages
3. Stack Overflow blog, results of the 2026 survey (found by search, not opened). https://stackoverflow.blog/2026/10/06/the-results-of-the-2026-developer-survey-are-here/
4. JetBrains, State of Developer Ecosystem 2025. https://devecosystem-2025.jetbrains.com/
5. JetBrains, The State of .NET 2025. https://lp.jetbrains.com/the-state-of-dotnet-2025/
6. Python Software Foundation and JetBrains, Python Developers Survey 2024. https://lp.jetbrains.com/python-developers-survey-2024/
7. Julia User & Developer Survey 2025 (slides; text extracted locally from the PDF). https://julialang.org/assets/2025-julia-user-developer-survey.pdf
8. RSE International Survey 2022, tools and programming languages. https://softwaresaved.github.io/international-survey-2022/section/tools-and-programming-languages
9. Software Sustainability Institute, UK Research Software Survey 2014. https://www.software.ac.uk/blog/its-impossible-conduct-research-without-software-say-7-out-10-uk-researchers
10. UNESCO Science Report, figures (found by search, not opened). https://en.unesco.org/unesco_science_report/figures
11. MathWorks company fact sheet, March 2026 (text extracted locally from the PDF). https://ch.mathworks.com/content/dam/mathworks/fact-sheet/2026-company-factsheet-8-5x11-8282v27.pdf and https://www.mathworks.com/company.html
12. Emerson, completion of the NI acquisition, 2023-10-11. https://www.emerson.com/en-us/news/2023/emerson-completes-ni-acquisition
13. Emerson, fourth quarter and full year 2025 results, SEC exhibit 99.1. https://www.sec.gov/Archives/edgar/data/32604/000003260425000079/a2025q4release_ex991.htm
14. Snowflake Form 10-K for fiscal 2022 (found by search). https://www.sec.gov/Archives/edgar/data/1640147/000164014722000023/snow-20220131.htm and TechCrunch, 2022-03-02. https://techcrunch.com/2022/03/02/snowflake-acquires-streamlit-for-800m-to-help-customers-build-data-based-apps
15. marimo, joining CoreWeave, 2025-10-30. https://marimo.io/blog/joining-coreweave and CoreWeave investor release. https://investors.coreweave.com/news/news-details/2025/CoreWeave-Acquires-Marimo-to-Unify-the-Generative-AI-Developer-Workflow/default.aspx
16. Observable, Series B announcement, 2022-01-13 (found by search). https://www.businesswire.com/news/home/20220113005245/en/Observable-Announces-35.6-Million-Series-B-Funding
17. Observable: Observable 2.0. https://observablehq.com/blog/observable-2-0 ; deprecating Observable Cloud (returned HTTP 429). https://observablehq.com/release-notes/2025-04-15-deprecating-observable-cloud ; Notebooks 2.0 preview. https://observablehq.com/notebook-kit/ ; FlowingData, 2026-09-02. https://flowingdata.com/2026/09/02/observable-notebooks-get-a-rewrite/
18. SiliconANGLE, Hex raises $70 million, 2025-05-28 (found by search). https://siliconangle.com/2025/05/28/hex-raises-70m-expand-ai-powered-data-analytics-platform/
19. Deepnote Series A, 2022-01-31 (found by search). https://www.businesswire.com/news/home/20220131005531/en and The New Stack on its open-source release. https://thenewstack.io/deepnote-a-successor-to-jupyter-notebook-goes-open-source/
20. SiliconANGLE, Anaconda raises $150 million, 2025-07-31 (found by search). https://siliconangle.com/2025/07/31/anaconda-raises-150m-speed-python-based-ai-projects/
21. JuliaHub, Series B and Dyad 3.0. https://juliahub.com/blog/juliahub-raises-65m-series-b-launches-dyad-3.0 ; earlier rounds from Sacra (found by search). https://sacra.com/c/juliahub
22. Plotly, Dash Enterprise announcement. https://plotly.com/news/plotly-announces-dash-enterprise-building-smarter-safer-data-apps-ai-era/ ; funding aggregator (found by search). https://www.cbinsights.com/company/plotly/financials
23. Quansight Labs, funding model (found by search). https://labs.quansight.org/blog/community-opensource-funded-development
24. Enthought, agreement with Tokyo Electron (found by search). https://www.enthought.com/news/enthought-secures-five-year-agreement-to-accelerate-tokyo-electrons-digital-transformation-initiatives/
25. Confluent, welcoming Noteable (found by search). https://www.confluent.io/blog/welcoming-noteable-to-confluent/
26. dotnet/interactive repository and deprecation announcement. https://github.com/dotnet/interactive and https://github.com/dotnet/interactive/issues/4163
27. Synopsys, completion of the Ansys acquisition, 2025-07-17 (found by search). https://news.synopsys.com/2025-07-17-Synopsys-Completes-Acquisition-of-Ansys
28. Polars, Series A, 2025-09-29. https://pola.rs/posts/series_a/
29. TechCrunch, MotherDuck Series B, 2023-09-20 (found by search). https://techcrunch.com/2023/09/20/database-startup-motherduck-lands-52-5m-to-grow-its-duckdb-based-platform
30. Renaissance Philanthropy, Open Source for Science Fund, 2026-05-04. https://www.renaissancephilanthropy.org/insights/open-source-for-science-fund-launches and CZI EOSS. https://chanzuckerberg.com/eoss/
31. NSF, Pathways to Enable Open-Source Ecosystems. https://www.nsf.gov/funding/opportunities/pose-pathways-enable-open-source-ecosystems and https://www.nsf.gov/tip/updates/nsf-announces-solicitation-open-source-ecosystems
32. Sloan-funded open-source programme offices (found by search). https://news.syr.edu/2025/12/03/open-source-program-office-secures-719k-grant and https://csde.washington.edu/news-events/new-alfred-p-sloan-foundation-technology-program-rolling/
33. Virtual Institute for Scientific Software, 2022 (found by search). https://cc.gatech.edu/news/major-philanthropic-grant-will-create-new-center-advance-open-source-software and https://media.nature.com/original/magazine-assets/d41586-022-01901-x/d41586-022-01901-x.pdf
34. NumFOCUS Small Development Grants. https://numfocus.org/programs/small-development-grants
35. Sovereign Tech Fund. https://www.sovereign.tech/programs/fund ; EU case study (found by search). https://interoperable-europe.ec.europa.eu/collection/open-source-observatory-osor/document/funding-open-source-case-study-sovereign-tech-fund
36. NASA Open Science funding opportunities. https://science.nasa.gov/open-science/nasa-open-science-funding-opportunities/
37. SSI Research Software Maintenance Fund, Round 2 (found by search). https://www.software.ac.uk/research-software-maintenance-fund/round-2 and https://www.software.ac.uk/news/announcing-19-projects-funded-through-research-software-maintenance-fund-round-2
38. The White House, Genesis Mission release, 2026-07-22. https://www.whitehouse.gov/releases/2026/07/45502/
39. NuGet search API and gallery, for example https://azuresearch-usnc.nuget.org/query?q=packageid:MathNet.Numerics and https://www.nuget.org/packages/MathNet.Numerics
40. pypistats API, for example https://pypistats.org/api/packages/numpy/recent
41. GitHub REST API, for example https://api.github.com/repos/marimo-team/marimo
42. NI, .NET resources for NI hardware and software (found by search). https://www.ni.com/en/support/documentation/supplemental/13/national-instruments--net-support.html
43. PNNL, Thermo Raw File Reader (found by search). https://www.osti.gov/doecode/biblio/8636
44. G-Research, work on the F# compiler (found by search). https://www.gresearch.com/news/going-15-percent-faster-with-graph-based-type-checking-part-one/
45. Unity fiscal 2025 fourth-quarter release, third-party copy (found by search). https://gameindustrylibrary.com/documents/unity-software-fy2025-q4-press-release ; Constellation Research on Unity Industries. https://www.constellationr.com/insights/news/unity-software-scales-digital-twin-business
46. Fable 5 release candidate and Python compatibility page (found by search). https://fable.io/blog/2026/2026-02-27-Fable_5_release_candidate.html and https://fable.io/docs/python/compatibility.html
47. IBM completes acquisition of Confluent, 2026-03-17 (found by search). https://www.confluent.io/pt-br/press-release/ibm-completes-acquisition-of-confluent/
48. Analyst estimates (found by search). https://www.marketsandmarkets.com/Market-Reports/geography/digital-twin-market/rest-of-europe ; https://www.startus-insights.com/?p=40373 ; https://www.knowledge-sourcing.com/report/lab-automation-market ; https://www.marketresearch.com/Deep-Insights-Research-Co-DIR-v4285/Global-Lab-Automation-Research-Competitive-41778104
49. PhET Impact Report 2025 (found by search). https://phet.colorado.edu/publications/PhET_Impact_Report_2025.pdf
50. Amplify acquires the Desmos curriculum, 2022-05-18 (found by search). https://amplify.com/news/amplify-acquires-desmos-curriculum-to-build-the-future-of-math-instruction-desmos-calculators-to-remain-independent-and-free-to-all/
51. BYJU'S acquires GeoGebra, 2021-12-08 (found by search). https://www.businesswire.com/news/home/20211208005873/en/BYJUS-Acquires-Austrian-Company-GeoGebra-Aims-To-Make-Math-Learning-Even-More-Visual-And-Interactive
52. Distill hiatus, 2021-07-02 (found by search). https://distill.pub/2021/distill-hiatus/
53. InfoQ on the Fluent Assertions licence (found by search). https://www.infoq.com/news/2025/01/fluent-assertions-v8-license
54. AutoMapper and MediatR commercial editions; MassTransit version 9 (found by search). https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/ and https://masstransit.io/introduction/v9-announcement
55. DevClass on Moq and SponsorLink (found by search). https://devclass.com/2023/08/10/moq-testing-framework-under-fire-for-phone-home-sponsorlink-monetization/
56. Six Labors licence documentation and the .NET Foundation's October 2022 note (found by search). https://docs.sixlabors.com/articles/imagesharp/ and https://old.dotnetfoundation.org/blog/2022/10
57. Avalonia and Devolutions sponsorship (found by search). https://avaloniaui.net/blog/three-year-sponsorship-accelerates-avalonia-s-open-source-roadmap and https://www.globenewswire.com/news-release/2025/07/01/3108491/0/en/Devolutions-Backs-Avalonia-Fuels-Native-Future-of-Remote-Desktop-Manager-with-3M-Investment.html
58. Sonar to acquire Tidelift, 2024-12-17 (found by search). https://sonarsource.com/company/press-releases/sonar-to-acquire-tidelift
59. Wolfram Research estimates from aggregators (found by search). https://www.reveliolabs.com/companies/wolfram-research/employees/ and https://growjo.com/company/Wolfram_Research
60. Posit benefit corporation report 2025 (too large to load; not read). https://posit.co/wp-content/uploads/2025/05/Posit-PBCReport2025.pdf
61. The Register on SlashData's 2021 estimates (found by search). https://www.theregister.com/2021/04/26/report_developers_slashdata/
62. Commentary on autonomous-laboratory funding (found by search). https://ml4sci.substack.com/p/venture-capital-is-subsidizing-us and https://www.theneuron.ai/explainer-articles/ai-just-got-a-300m-lab-coat-and-its-finally-ready-to-do-real-science
63. The Register on the Polyglot Notebooks deprecation, and a report of the Kotlin Notebook sunset (found by search). https://theregister.com/2026/02/12/polyglot_notebooks_deprecation and https://thenewstack.io/?p=22828264

Workspace sources (paths relative to `C:\wk\ranvier-release`), added in the revision:

64. Project files and READMEs across the repository family: `Ranvier`, `Ranvier.View`, `Ranvier.WebCore`, `Ranvier.Wgpu`, `Ranvier.D5`, `Ranvier.Terminal`, `Ranvier.Plume`, `Ranvier.Css`, `Ranvier.Anime`, `Ranvier.Keyboard`, `Ranvier.Persistence`, `Ranvier.BAREWire`, `ranvier-fsharp`. The component and target list in §4.6 was verified by the coordinating session. This survey confirmed only that the repositories are present.
65. `AstroShowcase.Web/README.md` (line 3: the same sources build the native window); `AstroShowcase/README.md` (the soak visits "each of the 29 pages"); `AstroShowcase/ACTION-PLAN.md` (all 29 pages draw in Chromium on WebGPU). Lines read for this revision.
66. `Ranvier/docs/.ai/benchmarks/counters/e13f159.md`, counter benchmark dated 2026-09-30. Read for this revision.
67. `ranvier-fsharp/README.md`, "Benchmark Highlights". Read for this revision.
