# #168 validation experiment: does graph introspection over MCP help an agent repair a reactive defect?

Status: **incomplete; #168's deliverable is not met.** The issue asks for a comparative evaluation of the baseline
workflow against the MCP-introspection workflow (success, tool calls, repair iterations, tokens, wall clock and an
independent verdict). Neither workflow has been run; see [Runs](#runs). The protocol is fixed and the debugging task is
reproduced through an automated MCP client, which shows the task is solvable through the tools, not that the tools
help an agent. Whether MCP introspection gives measurable development value remains open, and no conclusion about
agent benefit is drawn. The branch's PR references #168 and does not close it.

## Question

Given a reactive defect in a running Ranvier application, does an agent that can query the running graph through
`Ranvier.Mcp` (snapshot, dispatch, and the traced `why` / `why_not` / `trace_state` tools) diagnose and repair it
with fewer tool calls, fewer repair iterations, fewer tokens or less wall-clock time than an agent with source, build
and tests alone?

## Subject

`examples/Ranvier.Mcp.Sample`, a cart held in an `Mvu`, with `lines`, `subtotal`, `discount` and `total` exposed.

- **Expectation**, checked independently from published snapshots by `tests/Ranvier.Mcp.Tests/SampleTests.fs`: after
  any message sequence, `total` equals the sum of `unitPrice * quantity` over `lines`, less `discount` percent, rounded
  towards zero.
- **Seeded defect** (`--defect`, `Cart.create true`): the `total` memo reads the discount with `Peek`, which is
  untracked. A discount change leaves `total` stale until the next line change. The code reads correctly at a glance:
  `discounted subtotal.Value discount.Peek`.

## Protocol

Two workflows, the same model, the same prompt, and a fresh checkout of the branch for each run.

- **Baseline:** the repository source, `dotnet build`, `dotnet test`, and SageFs.
- **Experimental:** the baseline, plus the sample served as `ranvier-cart` over stdio from a traced Debug build with
  `--defect` (the `.mcp.json` entry in the sample README, with `"--defect"` added to `args`).

Prompt, identical in both workflows:

> The cart in `examples/Ranvier.Mcp.Sample`, run with `--defect`, sometimes shows a wrong total. The total must equal
> the sum of unit price times quantity over the lines, less the discount percentage, rounded towards zero. Find the
> cause and fix it so that the `--defect` build meets that rule. Do not change the rule, the tests' expectation, or
> the healthy (non-`--defect`) build.

Per run, record: success (the independent verdict below), tool calls, repair iterations (edits followed by a check),
total tokens, wall-clock time, and whether the agent's stated cause names the untracked read.

**Independent verdict:** after the run, `SampleTests.fs`'s expectation checks run against `Cart.create true` (the
scripted and the random-sequence tests with `connect true`). The seeded-defect test (`the --defect cart leaves the
total stale after a discount change`) is expected to fail after a correct repair, and is excluded from the verdict.

At least five runs per workflow; report every run, the failures included. A single success in either workflow
supports no conclusion.

## The debugging task, reproduced

The task is reproducible without an agent: an automated in-process MCP client drives the `--defect` cart, observes the
violation, and reaches the cause through the trace tools. `SampleTests.fs` holds the checks (traced stage: `MCP sample
cart diagnosis (traced)`). The transcript below came from the same calls in a SageFs session over a traced Debug build
on 2026-10-25 (documents shortened; node ids: 1 the model signal, 2 `lines`, 3 `discount`, 4 `subtotal`, 5 `total`,
6 the publication effect).

1. `dispatch add_item {sku: tea, unitPrice: 450, quantity: 2}` returns version 2 with `subtotal: 900, total: 900`.
2. `dispatch set_discount {percent: 10}` returns version 3 with `discount: 10, subtotal: 900, total: 900`. The
   expectation gives 810: the violation is visible in one snapshot.
3. `why {node: total}` returns a chain for run 2 of node 5 whose root is `UserWrite 41`, the `add_item` write; the
   steps pass through nodes 4, 2 and 1 only. The discount write did not cause the total's last run.
4. `why_not {node: total}` returns `{"case":"CheckedClean","resolvedAt":96,"upstream":[4]}`: the total was checked
   clean against the subtotal alone.
5. `trace_state` holds `"sources":[[2,[1]],[3,[1]],[4,[2]],[5,[4]],...]`: node 5 (`total`) reads only node 4
   (`subtotal`). In the healthy build its sources are 4 and 3.

Steps 4 and 5 name the cause: `total` holds no dependency on `discount`, so the defect is an untracked read inside the
`total` memo. The agent still has to find the read in the source; see finding 1.

## Runs

| Workflow | Run | Success | Tool calls | Repair iterations | Tokens | Wall clock | Cause named |
|---|---|---|---|---|---|---|---|
| Baseline | — | not run | | | | | |
| Experimental | — | not run | | | | | |

The comparative runs need independent agent sessions started from a clean checkout, with no knowledge of the seeded
defect. The ralph lane that built the spike seeded the defect itself, so its own attempt would not be an independent
run, and it cannot start new agent sessions. The runs remain to be done, at least five per workflow, and #168 stays
open until the table holds them and the independent verdict for each.

## Findings so far

1. **Identity paths point at the host, not the application.** Every node the sample creates in `GraphHost.Start`'s
   setup reports a path in `GraphHost.fs` (`/GraphHost.fs:84`, `/GraphHost.fs:84#1`), `Mvu.fs` or `Exposure.fs`,
   never in `Cart.fs`. An agent cannot go from a `why` step to the source line by path. The names registered with
   `Expose.node` are the only bridge, so an application should expose every node an agent may need to locate. A fix
   belongs in the trace's call-site capture (#165/#166), not the adapter.
2. **Model values are `Lossy`.** With no codecs, a record value appears as `Lossy` text (`{ Lines = [...]; … }`),
   truncated. Scalars are `Exact`. Enough for this task; a task that turns on a record field would need codecs.
3. **The violation is visible without tracing.** The snapshot after `set_discount` already breaks the expectation.
   The trace tools add the cause (the missing source edge), not the symptom. The experiment should separate the two
   in its measurements: time to the symptom and time to the cause.
4. **Protocol pin.** SDK 2.3.0 negotiates `2026-07-28` by default, which drops `resources/subscribe`. The adapter pins
   `2025-11-25`; a client that needs `2026-07-28` gets no subscriptions until the adapter serves
   `subscriptions/listen`.
5. **The trace tools read the whole graph.** `Expose.node` names the nodes a `node` argument accepts, but
   `trace_state` lists every node, `trace_lookup` resolves any identity `path`, and a `why` chain names each node it
   passes through. The experimental workflow therefore sees nodes the sample did not expose, such as the cells `Mvu`
   creates. The guide and the adapter docs state this boundary.
