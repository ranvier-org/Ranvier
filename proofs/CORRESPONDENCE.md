# Correspondence

Each row maps a symbol of the model to the F# in `src/Ranvier/Core.fs` (pinned at commit
`d701e6c5`), and lists every simplification with its reason.

| Model symbol | Core.fs symbol | Simplification and reason |
|---|---|---|
| `nid` | node identity | `nat`; node ids are opaque. |
| `oid` | owner identity | `nat`; owner ids are opaque. |
| `val` | node value | `Z`; the model fixes one value type. |
| `fresh` | node freshness state | `Clean`, `Check`, `Dirty`; ordered `Clean <= Check <= Dirty`. |

## Reference tier

The reference tier defines meaning for any engine, so it has no Core.fs symbol for marks, freshness
or the queue. Task 12 relates it to the engine. These rows record its fragment and where its
choices come from.

| Model symbol | Core.fs symbol | Simplification and reason |
|---|---|---|
| `val := Z` | generic `'T` | Unbounded integers against Core.fs's fixed-width integers in user bodies. Overflow is outside the tier, so a body whose arithmetic wraps in F# has a different value here. |
| `rprog` | a graph built by user code | A static node set. A program whose effect creates or disposes nodes is outside the fragment and keeps only the general engine theorems. Author's call, from the plan. |
| `rmemos`, `well_behaved_memo` | `Memo` | No previous-value argument, ownership, cleanups, writes, disposal, effect creation or untracked read. A memo has no comparer here: cutoff compares outcomes by equality, as `CmpEq`. `CmpNever` and `CmpThrows` are outside the fragment. |
| `memos_acyclic` | none | A rank that decreases along every read, for every answer the reads can give. A cycle or self-read that some answers would reach is outside the fragment, whether or not the program's signals reach it. |
| `reffects`, `well_behaved_effect` | `Effect` | Bodies use `CRet`, `CThrow`, `CLet`, `CIf`, `CRead`, `CTryRead`, `CReadU`, `CWrite`, `CCleanup`, `CLog`, `CBatch`, and no `EPrev`: an effect has no previous value. `rwf` rejects `CSignal`, `CMemo`, `CEffect`, `CDispose`. |
| `exec` on `CIf` | `if` in a body | A zero condition selects the else branch. |
| `exec` on `CTryRead` | `TryValue` | `ok` binds 1 or 0, and `x` binds the value or the failure's identity. |
| `exec` on `CRead` | `Value` | A failed memo ends the body with its failure, and the read counts as tracked. |
| `exec` on `CBatch` | `RunBatch` | Sequences its parts. Writes apply immediately, and a flush cannot nest in a round. |
| `eval_expr` on `EDiv`, `div_zero_failure` | `DivideByZeroException`, `Effect.RunBody` | A zero divisor is the failure `div_zero_failure`, the body's outcome, as `RunBody` records the exception. Later effects run. The identity is one fixed value, so it has the difference in the `Fail f` row: F# throws a new exception per zero divisor, so a memo that fails this way on consecutive recomputes has moved in F# and has not moved here. `CThrow (-1)` denotes the same failure. |
| `Fail f`, `outcome` in read comparison | `Failure.Moved`, `Graph.FailureOf` | `f` denotes an exception instance. Outcomes compare by `f`, as `Moved` compares instances by reference. `CThrow f` is a body throwing the same stored instance on every evaluation, so an unmoved failure wakes nothing. A body throwing a new exception per recompute has no fixed `CThrow` form: `FailureOf` allocates a new `Failure`, the failure has moved and wakes readers, whereas `CThrow 7` on re-evaluation is the same outcome and wakes nothing. Tasks 4 and 12 rely on this row. |
| `EvRun` | `Effect.RunBody`, `runs` | Emitted once per effect run, after its cleanups. Memo-body evaluation is not an event, so the observable number of runs is the number of effect runs. A memo's run count belongs to the engine. |
| `EvLog` in a memo | a user side effect in a memo body | Discarded in this tier: its count would be a memo's run count. |
| `run_cleanups` | `Graph.Discharge`, `Owner.DisposeScope` | Newest first, before the body, with reads untracked. A cleanup registered by a cleanup joins the new scope, as `Owner.AddCleanup` does. A failed cleanup ends only itself, as `RecordError` does. Whether that failure then escapes the flush is engine behaviour, examined by scenario `S09b`. |
| `run_effect` on a failed body | `Effect.RunBody`, `FailureOf` | `RunBody` records the failure. `run_effect` drops the outcome, and `run` has no outcome field, so a failure leaves no event and no record in this tier. Task 3a adds failure events. Later effects run, and the reads and cleanups reached are kept. |
| `run_all_reads` | none | Every read of a run in order, tracked and untracked, cleanups included. `redundant` compares it. The run set uses the tracked `run_reads` only. |
| `computed_from_snapshot` | closures in cleanups | The inputs are one signal snapshot and the captured environments of the cleanups the run ran. A captured environment comes from the run that registered the cleanup, so a cleanup can log a value from an earlier snapshot, as an F# closure does: `cleanups_first`. Every read of the run answers from the one snapshot. |
| `rruns` initial round | `Effect.Start` | The history starts with one round in which every effect runs, in `effect_order`'s order. |
| `run_set`, `settle` | `RunFlush` | A round's run set comes from a snapshot: each effect's last-run tracked reads compared with the graph at the round's start. `CReadU` reads wake nothing. Writes by an effect apply immediately, so a later effect of the round reads them, and effects they wake run in the next round unless their last-run reads are already current. |
| `effect_order` | the observer positions and queue | A parameter of the tier. Task 12 characterises the engine's order. |
| `fuel` | none | Bounds the rounds of each settling. A flush that keeps waking effects has no reference meaning: `rtrace` returns `None`, and the spec states flush theorems conditionally on a returned flush. |
| `None` from `exec` | none | A stuck body: an unbound variable, a negative node id, a read of an unknown node or an effect, a write to a non-signal. These have no F# counterpart, since F# bodies hold variables by scope and nodes by reference. A zero divisor is a failure, not a stuck body. |
