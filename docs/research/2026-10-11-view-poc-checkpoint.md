# DOM PoC checkpoint — 2026-10-11

This status describes the work preserved from the DOM worktree's uncommitted changes, based on `ca01abf`. The earlier comparison with Ripple described that committed base and missed the subsequent keyed-list and application work.

## Working experimentally

The Foundation renderer now supports keyed lists in addition to reusable views, typed components/props, owned ranges and child-first cleanup. Retained keys keep their row mount and DOM identity, while a reader supplies the latest item value. Reordering moves existing ranges; removal releases row listeners/resources; reinsertion creates a fresh row. Duplicate snapshots are rejected before reconciliation mutates the host. Tree and Program can both render keyed snapshots as escaped HTML.

The playground compiles Foundation sources directly and mounts an add/reverse keyed-row panel alongside the original DOM PoC. The browser test exercises this consumer and verifies retained node identity. This is application integration of experimental source, not a public package or a complete application framework.

The solution includes the portable representation-check project. The relative SageFs project setting selects the solution without embedding a machine-specific path. No daemon/session configuration was changed; the existing daemon reports a separate home-directory watcher limitation.

## Review and repairs

A fresh reviewer found that top-level keyed regions bypassed the outer range's root bookkeeping. A new regression failed with `before::afteronetwo` instead of `before:onetwo:after`. The range now records a keyed span, moves it with the outer mount and updates retained rows' placement after movement.

The reviewer then identified two issues in that fix. Each was reproduced by a failing regression: foreign nodes inserted inside keyed rows were accepted during outer movement, and a failed start-marker removal skipped the end marker. Region validation now checks child ranges before movement; endpoint releases are attempted independently. All three regressions pass in the complete Node and Chromium runs.

## Verification

- Complete playground acceptance: 113 Node tests and 16 Chromium tests passed.
- Release DomTests build: zero warnings/errors; fresh scoped FCS check: zero errors/warnings.
- Portable .NET representation executable: all six groups passed.
- Playground production bundle: build passed.
- XML comment audit of Representation/Mounting: zero findings after wrapping one overlong comment; staged formatting/comment checks run during commit.

These are scoped DOM/portable gates. Whole-solution acceptance and production API freeze remain open.

## Remaining work

Follow-on: the [keyed correctness repair](2026-10-11-keyed-correctness.md) adds coverage and fixes for the failure/reentry/nested-composition gaps below. The verification counts above describe the original checkpoint; consult the follow-on for its newer results.

This was a useful keyed-list correctness checkpoint, not completion of framework Task 6. Its duplicate-update reporting, failed live reconciliation, reentrant disposal and nested-composition gaps are addressed by the follow-on within the tested contracts. Branch selection and broader comparer/focus cases remain open. The implementation uses quadratic duplicate checks, retained-key lookup and removed-entry filtering; it has no list throughput or allocation budget yet. Large-list performance must be measured and optimized before claiming the intended performance target.

Bindings still need explicit pending/error/status policies and scheduling integration in Foundation. Broad typed HTML/SVG coverage, generated scaffolding, server request ownership, hydration and full ergonomic sugar remain unfinished. The instruction tree is provisional; no Ripple/Solid speed ranking is established by these tests.
