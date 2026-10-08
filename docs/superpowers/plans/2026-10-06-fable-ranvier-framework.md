# Fable.Ranvier framework implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** deliver a plugin-free, explicit F#/Fable renderer with fine-grained DOM updates and a proven path to server rendering.

**Architecture:** opaque reusable views instantiate into owned DOM ranges; components run setup once; explicit readers drive bindings and keyed regions; a small renderer boundary separates portable definitions from browser capabilities.

**Tech stack:** F#, Ranvier, Fable, browser DOM, existing Expecto/jsdom/Playwright infrastructure. Validate Debug and Release behavior; performance runs use Release output.

**Spec:** [framework brief](../specs/2026-10-06-fable-ranvier-framework-brief.md). **Tradeoffs and risk IDs:** [variants and gotchas](../specs/2026-10-06-fable-ranvier-framework-variants-and-risks.md).

Status: proposed work, not executed. File names below are proposed seams; verify existing symbols before choosing final names. API descriptions are design sketches, not compiler-validated signatures.

## Global constraints

- Preserve explicit core policies and plugin independence. Do not add silent scheduling, ownership inference, implicit async retention or automatic memoization.
- Follow repository AGENTS.md. Use fslangmcp `check` before symbol queries and `fcs_refactor_impact` before public-signature changes. If required tools are missing, follow the repository setup-permission instructions.
- Read the SageFs and XML documentation skills before applicable F# changes. Do not restart the previously stopped SageFs daemon without user authorization. Use the CLI for final acceptance.
- Keep microtask implementation separate. Preserve legacy behavior only through documented explicit compatibility configuration or a named adapter.
- Do not claim Solid parity before equivalent production benchmarks. Do not let optimization change observable identity or timing.
- Run the full repository acceptance suite at release gates, not just filtered tests. Determine the current build-runner command from its help before execution.

## Review focus

The highest-risk seams are child-first cleanup over current core semantics, status propagation through split effects, portable component setup, and key equality across .NET/Fable. Public API freeze depends on those proofs. Scheduling, list performance and compiler sugar follow correctness.

## Proposed module boundaries

- `src/Fable.Ranvier/Types/View.fs`: opaque definitions, component/prop contracts and backend-neutral composition.
- `src/Fable.Ranvier/Types/Policies.fs`: explicit binding, outcome, scheduling and reporting policy types.
- `src/Fable.Ranvier/Runtime/Scope.fs`: lifetime state machine, child registration, cleanup and capability validation.
- `src/Fable.Ranvier/Runtime/Range.fs`: empty/single/multiple-node insertion, movement and removal.
- `src/Fable.Ranvier/Runtime/Binding.fs`: tracked read/untracked write, committed equality and status handling.
- `src/Fable.Ranvier/Runtime/Branch.fs` and `Runtime/KeyedList.fs`: retained identity and structural updates.
- Existing `Types/DomScheduling.fs` and `Types/DomQueue.Microtask.fs`: scheduler interface and optional queue strategy, evolving after audit.
- Existing `Types/Component.fs`, `Dom.fs` and `Mount.fs`: audit and migrate existing responsibilities; avoid parallel competing component APIs.
- `src/Fable.Ranvier/Dom/Attributes.fs`, `Properties.fs`, `Events.fs`, `Lifecycle.fs`: typed browser surface over the runtime.
- Future `src/Ranvier.View/` and `src/Ranvier.Html/`: possible extraction of portable contracts and server interpreter, only after the portability experiment justifies the package split.
- `examples/Fable.Ranvier.Playground/tests/`: expand the existing tests, registering new F# files in `DomTests.fsproj` and generated test entry points in existing JS runners.

The implementation may consolidate small modules; these names identify responsibility and dependency direction, not a requirement for one file per abstraction.

## Task 1 — Audit the PoC and establish the acceptance harness

Contracts: C01–C10. Risks: G03, G09, G12. No public API changes in this task.

- [ ] Read current project instructions and manifests; confirm the working tree and isolate implementation work as appropriate.
- [ ] Run fslangmcp `check`, then inspect mount, component, scheduler, owner disposal and status/effect APIs semantically. Record existing signatures and public API snapshots.
- [ ] Compare the implementation with `src/Fable.Ranvier/README.md`; identify where its documented behavior has drifted.
- [ ] Establish current full .NET and browser test results. Run existing `test:dom`, `test:browser` and Release build scripts from the playground.
- [ ] Record legacy behavior requiring migration: ambient batched events, retained successful DOM on pending/error, initial versus deferred setter failures, and single-node mounting.

**Exit:** a baseline report appended to this plan with actual commands/results and an API migration list. Existing failures are attributed before feature work begins.

## Task 2 — Prove disposal and portable view representation

Contracts: C01, C03, C09. Variants: V01, V03. Risks: G03, G11, G12, G15. This is a blocking experiment.

- [ ] Write lifecycle trace tests first: nested children clean up before parent while nodes remain attached; a throwing child does not prevent other cleanup/removal; disposal repeats are harmless.
- [ ] Run the same traces through mount disposal, graph disposal, failed setup and owner disposal during reactive reruns. Include two mounts sharing one graph.
- [ ] Prototype the renderer child registry. If any entry point bypasses the ordering guarantee, document the failure and evaluate a core extension with refactor-impact analysis; do not conceal it behind only mount-level tests.
- [ ] Prototype a small portable instruction definition and a backend-neutral executable definition for the same component, fragment, text binding and keyed branch.
- [ ] Interpret a static selection on .NET into escaped HTML and instantiate the equivalent definition in Fable. Prove browser refs cannot accidentally be required by the portable setup surface.
- [ ] Measure definition/instance allocations and mount cost in Release JS; inspect generated closure/DU dispatch. Choose A or B from V01 and record the reason.

**Exit:** both lifecycle and portability proofs pass. If not, revise the proposed representation before public API freeze. The HTML proof is not yet a production SSR renderer.

## Task 3 — Implement scopes, ranges and explicit mount policies

Contracts: C01, C03, C07. Risks: G03, G04, G11, G15.

Proposed operation: `mount(graph, parentScope, policies, host, view) -> MountedView`. `MountedView` exposes disposal and explicit mounted capabilities, not arbitrary mutation of its range internals.

- [ ] Add failing tests for empty, one-node and fragment views; sibling preservation; failed attachment; reparented owned nodes; borrowed-node release; and post-disposal operations.
- [ ] Implement a scope state machine that distinguishes setup, attached and disposed states and tracks children/cleanup deterministically.
- [ ] Implement ranges with stable insertion boundaries, explicit movement and idempotent removal. Validate boundaries before destructive structural operations.
- [ ] Make partial mount failure unwind created listeners, bindings, children and inserted nodes while preserving unrelated host content.
- [ ] Aggregate disposal errors through the supplied reporter; guard reporter failure so it cannot interrupt resource release.
- [ ] Extend the playground's existing mount tests and run the full DOM/browser test scripts.

**Exit:** mount ownership is independent of graph ownership and every lifecycle entry point follows Task 2's proven order.

## Task 4 — Implement components and props

Contracts: C02, C10. Risks: G01, G02, G14.

Proposed operation: `component(setup, props) -> View`, where setup receives its explicit child scope at instantiation. `Prop.read` evaluates constants/readers; it never memoizes or creates a subscription by itself.

- [ ] Add tests demonstrating setup runs once, untracked; two mounts have distinct instance state; captured state remains shared; static snapshots stay static; reactive props remain live.
- [ ] Add compile-time API examples for typed props, explicit callback writes, read-only adapters and reusable child views.
- [ ] Implement component instantiation using Task 3's child scope; normalize prop adapters once where useful.
- [ ] Reject use of disposed setup capabilities; test component setup failure and cleanup registration before failure.
- [ ] Document instance state versus captured state with one minimal example of each.

**Exit:** no plugin, dynamic invocation trick or props proxy is required for a typed component example.

## Task 5 — Bindings, status handling and scheduling

Contracts: C04, C06. Variants: V02, V05. Risks: G06–G09.

Proposed binding input: reader, setter, equality, outcome policy, scope and scheduler. Proposed committed cache changes only after a successful setter. Final status adapter design depends on the semantic audit of Ranvier's read APIs.

- [ ] Add tests separating source equality, memo equality and binding equality, including always-write and a setter that reads another signal without subscribing to it.
- [ ] Add explicit initial-pending, later-pending, initial-error, later-error and recovery cases. Verify status transitions are observable when the ordinary split-effect action would otherwise be skipped.
- [ ] Implement policy-driven status handling and consistent reporting for initial and deferred writes.
- [ ] Implement synchronous commits first, then adapt the separate microtask module to the same contracts.
- [ ] Test coalescing, snapshot waves, manual flush ticket invalidation, recursive flush rejection, disposal midway through a snapshot and stale queued values after replacement.
- [ ] Test setter failure under both commit failure policies. Define whether remaining entries are canceled or rescheduled under fail-fast; make the choice explicit before shipping.
- [ ] Verify all policy combinations claimed as supported; do not silently convert a sync choice into a microtask to avoid reentrancy.

**Exit:** scheduling and rendering outcomes are independently selectable and no unselected PoC retention behavior leaks into the core.

## Task 6 — Keyed branches and lists

Contracts: C05. Variant: V04. Risks: G05, G10, G15.

Proposed branch input: selection reader, key selector, equality, factory receiving a current-value reader, and explicit outcome policy. List input additionally supplies key lookup semantics, including compatible hashing if required by the implementation.

- [ ] Test equal-key data replacement without setup rerun; changed-key replacement; variant-discriminated keys; duplicate keys; remove/reinsert; and cleanup during replacement.
- [ ] Implement keyed branches using owned ranges; avoid capturing only the initial selected value in retained children.
- [ ] Implement snapshot keyed lists, prevalidating duplicate keys before DOM mutation. Cover prepend, append, reverse, random reorder, multi-node rows and empty rows.
- [ ] Test state and listener identity through moves, and focus/selection in real browsers.
- [ ] Measure large-list lookup complexity under each supported key comparer. Do not substitute JS identity for structural equality.
- [ ] Add a separately named positional API only if needed. Defer incremental adapters and LIS optimization until baseline measurements show a useful improvement.

**Exit:** stable keys retain mounted instances, changed keys recreate them, and removal cancels all owned work.

## Task 7 — Typed DOM, events, lifecycle and owned async helpers

Contracts: C07, C08. Risks: G08, G10, G11, G13.

- [ ] Implement distinct static/reactive attribute and property operations, with typed event registration and explicit batch/scheduler context.
- [ ] Test boolean attributes, null/removal, `value`, `checked`, selection and IME behavior. Add browser-only tests where jsdom cannot represent behavior adequately.
- [ ] Add mounted access hooks and explicit owned/borrowed integrations; document raw HTML as a separate unsafe-content boundary.
- [ ] Implement cooperative owned-task cancellation with generation/liveness checks; test success after disposal, replacement races and noncooperative completion.
- [ ] Add explicit portable metadata/server fallback for browser-only operations rather than pretending arbitrary setters serialize.
- [ ] Migrate one realistic playground screen using only the public plugin-free API. Keep compatibility helpers clearly named and outside the minimal core.

**Exit:** the first usable renderer demonstrates interactive forms, keyed content, async status and reliable cleanup without compiler sugar.

## Task 8 — Performance and release acceptance

Contracts: all first-release contracts. Variants: V01–V05. No predetermined parity claim.

- [ ] Extend `examples/Fable.Ranvier.Playground/benchmarks/` with mount, update, reorder and disposal workloads; preserve the scheduling benchmark as a baseline.
- [ ] Include shared-graph fan-out, independent mounts, constant-heavy views, cheap/expensive setters and long-lived mount/unmount cycles.
- [ ] Record browser/Node/Fable versions, hardware, production settings, warmup, sample count, independent runs, medians and tail behavior. Measure retained heap after settled disposal and bundle size separately.
- [ ] Compare pinned Solid implementations with equivalent state, DOM, event and scheduling behavior; report differences that cannot be normalized.
- [ ] Optimize only demonstrated hot paths: constant elimination, adapter allocation, list lookup/moves, and scheduler storage. Re-run affected correctness suites after each change.
- [ ] Run whole-solution .NET acceptance and complete playground tests/build in Release-compatible configuration. Test additional browsers where supported; explicitly record missing coverage.
- [ ] Update public docs, migration examples and API snapshots. Report benchmark improvements and regressions without hiding unfavorable workloads.

**Exit:** evidence supports a usable release and documents its limits. Set numeric performance budgets from the measured baseline rather than inventing percentages now.

## Task 9 — Server HTML and hydration follow-on

Contracts: C09. Risks: G02, G09, G11–G13. Depends on portable proof and stable identity semantics.

- [ ] Extract portable contracts only if the chosen representation requires a separate project; keep browser dependencies out of the server package.
- [ ] Implement escaped text/attributes, portable property serialization, deterministic identity and request-local scopes; define explicit server pending/error behavior.
- [ ] Test two concurrent requests for accidental shared state, scope release on failure, and absence of browser lifecycle callbacks.
- [ ] Define hydration markers, state transfer, escaping and mismatch policy. Test empty/fragment/keyed regions, forms, malformed HTML normalization and server/client differing selections.
- [ ] Add first-content and hydration benchmarks. Treat streaming, event replay and selective hydration as separate later proposals.

**Exit:** real server HTML plus tested hydration; client-only rendering is no longer relied on for initial indexable content.

## Task 10 — Optional sugar and specialization

Contracts: C10. Variant: V06. Risk: G14. Depends on settled runtime contracts.

- [ ] Add optional ergonomic wrappers that select clearly documented policies and expand to explicit operations.
- [ ] Prototype CE control flow using actual F# compiler behavior; verify nested selection and continuations rather than relying on surface syntax assumptions.
- [ ] Add Fable plugin golden-output and behavioral-equivalence tests for setup count, prop reads, nested branches, cleanup, pending/error outcomes and timing.
- [ ] Measure static-template specialization and generated bundle size against the plugin-free baseline. Keep a complete no-plugin test application permanently.

**Exit:** sugar improves authoring or measured performance without becoming necessary for correctness.

## Contract coverage and validation commands

C01/C03: Tasks 2–3; C02: Task 4; C04/C06: Task 5; C05: Task 6; C07/C08: Task 7; C09: Tasks 2 and 9; C10: Tasks 4, 7 and 10. Task 8 validates the integrated first release.

Existing playground scripts, to run from the repository root during implementation:

```powershell
rtk proxy npm --prefix examples/Fable.Ranvier.Playground run test:dom
rtk proxy npm --prefix examples/Fable.Ranvier.Playground run test:browser
rtk proxy npm --prefix examples/Fable.Ranvier.Playground run build
rtk proxy npm --prefix examples/Fable.Ranvier.Playground run bench:dom
rtk proxy dotnet build Ranvier.slnx --configuration Release
rtk proxy dotnet fsi build.fsx -- --help
```

Use the build runner's verified full-test command for the whole .NET suite; a successful build is not a substitute. New test modules must be included in both project compilation and relevant runners. These commands are proposed implementation checks, not results of this documentation task.
