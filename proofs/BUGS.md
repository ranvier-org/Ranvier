# Defect ledger

Only Confirmed entries count as bugs. Where `src/Ranvier/Core.fs` documents a behaviour as
intended, the entry closes as "spec corrected".

## Suspected

## Modelled

## Confirmed

- `nested_self_recompute_resets_cursor` (scenario `nested_self_run`; `theories/Pure/Witnesses.v`:
  `nested_self_run_ends_clean_and_wrong`): a run reads `a`, writes `flip`, which marks it, and reads itself. The nested run
  (Core.fs 4209) takes another branch and its `EndRun` trims `a`'s edge, and the outer run then stores the value it computed
  from `a`. After `flip <- 2`, the memo is clean with value 10, its body now answers 20, `a` has no observers and `b`
  has one. After `a <- 11` the memo is still clean at 10. The effect is clean, nothing is queued or running, and an explicit
  `Flush` changes nothing. The harness asserts each of these and the witness states them. F# trace: `[EvRun 5; EvLog 5 10; EvRun 5; EvLog 5 10; EvHostRead 4 (Ok 10)]`. Unlike `self_marking_wake_lost`, the
  memo is clean, so no marked state records the difference. Core.fs 4205-4206 covers a self-read's overflow and 4178-4181 a
  cleanup's nested run, and neither states this. A self-read and a write put it outside the well-behaved fragment. The
  glitch-free claim excepts the self-dirtying case.
  **Theorems.** This falsifies `marks_survive_run_statement` and `run_end_edges_are_reads_statement`: `marks_survive_run_falsified`, `run_end_edges_are_reads_falsified`. The nested run clears a mark the
  outer run received, and the outer run read `a` with no slot for it. Both are Open below. `marks_survive_body_end` covers the tail of a run from the body's return. `run_end_edges_are_latest_reads` states the source
  list against the reads since the latest run start, and `run_end_edges_no_nested_run` against the run's reads when no nested self-run started. A form of the first with a mark at any point and a premise excluding the
  clearing paths (a nested run, a self-read's `ResolveCheck`, the discharge window) was not attempted.

- `self_read_check_overflow` (scenarios `self_read_check_overflow`, `mutual_read_check_overflow`, run by `--fatal`;
  `theories/Pure/Witnesses.v`: `self_read_check_resolves_itself`, `self_read_second_resolves_itself`,
  `mutual_read_check_resolves_itself`): a memo reads itself and also reads another memo `a` over a signal. Two reads leave it
  clean with itself among its sources, and a write to the signal marks `a` dirty, which tells the memo `Check`. The next read
  overflows the stack, which ends the process: no caller can catch it. Program:
  `(signal 1 (k 0) (memo 2 pure eq (read 10 (v 1) (ret (v 10))) (memo 3 pure eq (read 10 (v 3) (read 11 (v 2) (ret (v 11)))) (read 20 (v 3) (read 21 (v 3) (write (v 1) (k 1) (read 22 (v 3) (ret (k 0)))))))))`.
  The harness asserts `Clean`, then `Check`, then that the memo observes itself, before the fatal read.
  **The shape.** The other source has to be a memo, since that is what delivers `Check`. A memo reading itself and a signal runs
  fine, because the write marks it `Dirty`, and `Dirty` runs the body with no walk. The self-slot need not be first. With the
  self-read second the walk reaches it when `a` holds its value (`a` as `lt s 100`, `self_read_second_resolves_itself`), and stops
  short of it when `a` moves, which runs fine. Two memos reading each other overflow the same way, by constant id in the command
  language: `(signal 1 (k 0) (memo 2 pure eq (read 10 (v 1) (ret (v 10))) (memo 3 pure eq (read 11 (k 4) (read 12 (v 2) (ret (v 12)))) (memo 4 pure eq (read 13 (k 3) (ret (v 13))) (tryread 20 21 (k 3) (tryread 22 23 (k 4) (tryread 24 25 (k 3) (tryread 26 27 (k 4) (write (v 1) (k 1) (tryread 28 29 (k 3) (ret (k 0))))))))))))`.
  Without its final read that program returns on both sides, with the two sides agreeing and memo 3 in `Check`.
  Path: `TrackCurrent` (Core.fs 4334), then for a host read `Pull` (4336, 4349-4353), to `Refresh` (4360) to `ResolveCheck` (4304),
  whose walk calls `UpdateIfNecessary` (4309) on the memo itself, which is still in `Check`, so `EnsureCurrent` (4318) enters
  `Refresh` again, and no body runs between the frames. Mark and body guards never see it. `Generate.fsx`'s step and depth budgets
  count bodies, and do not see it either. The model has the same walk: `resolve_walk` spends fuel and changes nothing, so the read
  has no result at the fuels the witnesses state, where the same program without that read returns.
  **Against the documents.** Core.fs 4205-4206 says marking clean before the body "turns a memo that reads itself from a stack
  overflow into a default value". That holds for the path it names, `Run`, and the walk in `ResolveCheck` is a second path with no
  such guard. The test "a memo that reads itself yields a default rather than overflowing" (`tests/Ranvier.Tests/Invalidation.fs:66`)
  says of a self-read: "Not a supported thing to write, but a cycle must fail as a value, not as a stack overflow the caller cannot
  catch." That test reads through `Peek`, so it takes no edge to itself and does not reach this path.
  **The contract is the owner's call.** A self-reading memo may be outside the contract, and the owner decides that. No document
  found says this outcome is intended, which is why the entry is Confirmed and not closed as spec corrected.
  **Theorems.** `glitch_free_read` (Task 11) and `self_dirtying` exclude this program: a read of a node, or of itself, later than the reader makes the reader self-dirtying, for every answer of its earlier reads: `self_dirtying_covers_known_cases`. `convergence_all_statement` takes `memos_ranked` for the same reason. No proved theorem is falsified, and none is
  weakened. `well_formed_program` includes `memos_acyclic`, which rejects this program, so Task 12's `engine_of_answers_statement` and `weak_refinement_statement` keep
  their form with that premise. `well_behaved_memo` alone accepts a self-read and is no substitute for it. Any statement that `read`
  or `update_if_necessary` returns needs `memos_acyclic` as a premise, and Task 8's `resolve_clean_no_move_statement` and Task 9's
  `engine_inv_preserved_statement` hold over results, which this read has none of. In the differential check this program is `fatal` on the
  F# side and has no result in the model, which counts as a both-sides skip and closes nothing.

## Awaiting ruling

Each entry of Task 12 is a difference between the engine and the reference, with a checked `Example` in `theories/Pure/Witnesses.v` holding both traces. `weak_refinement_except` or `exact_refinement_except` takes each as a named premise until the owner rules. All of them are outside `src/Ranvier`'s documents unless the entry says so.

## Closed

- `self_marking_wake_lost` (scenarios `self_marking_cleanup`, `self_marking_body`, `self_marking_through_memo`, `stale_source_reader`; `theories/Pure/Witnesses.v`: `self_marking_cleanup_left_behind`, `self_marking_body_left_behind`, `self_marking_through_memo_left_behind`, `resolve_clean_no_stale_source_falsified`), closed as fixed (`SourceList.HasStaleSource`, `Memo.ResolveCheck`, `Effect.Execute`, `EffectOn.Execute`, `AsyncMemo.Refresh`, `Boundary.Refresh` in Core.fs, and `resolve_walk` / `oresolve_walk` in `theories/Pure/Engine.v` and `Optimised.v`). A check walk repeats from slot 0 while the node remains in `Check` and any live source in its `SourceList` is stale (`HasStaleSource` / `slot_holds stale_live`), so a source dirtied during its own recomputation or by a later source's run is re-pulled before the reader can resolve its `Check` to `Clean`.
- `walk_passed_source_marked` (scenarios `walk_passed_source_marked`, `walk_passed_source_cleanup`; `theories/Pure/Witnesses.v`: `walk_passed_source_marked`, `walk_passed_source_cleanup`, `walk_passed_stays_deaf`, `no_lost_wakeups_falsified`, `engine_inv_preserved_falsified`), closed as fixed under the same `resolve_walk` / `HasStaleSource` fix as `self_marking_wake_lost`. When a later source in a check walk marks an already-passed source `Dirty`, `HasStaleSource` restarts the walk from slot 0 and pulls the dirtied source before resolving `Check`.
- `cmp_never_passes_source` (scenarios `cmp_never_passes_source`, `cmp_never_lost_update`, `fresh_record_lost_update`; `theories/Pure/Witnesses.v`: `cmp_never_passes_source`, `never_lost_update`, `convergence_all_falsified`), closed as fixed under the same `resolve_walk` / `HasStaleSource` fix as `self_marking_wake_lost` and `walk_passed_source_marked`. When a later source's check walk recomputes a shared upstream memo that moves (including memos returning fresh reference values under `ReferenceComparer` / `CmpNever`) and marks an earlier source `Dirty`, the outer check walk restarts and pulls the dirtied earlier source instead of resolving `Check` to `Clean` over a stale source.
- `check_resolves_over_moved_source` (scenarios `check_over_moved`, `check_two_reads`; `theories/Pure/Witnesses.v`: `check_resolves_over_moved_source`, `check_two_reads_over_moved_source`, `resolve_clean_no_move_falsified`), closed as fixed (`IComputation.HasReadSource`, `SourceList.ContainsBeforeCursor`, `ObserverSet.NotifyDirtyExcept` in Core.fs, and `read_prefix` / `mark_except` in `theories/Pure/Edges.v` and `Marking.v`). `NotifyDirtyExcept` skips `CurrentComputation` only when the source is not in the current computation's `read_prefix` (`slot < cursor`), so if a source already read earlier in the current run moves during a later source's check walk, the running computation is marked `Dirty`.
- `effect_resolves_over_moved_source` (scenario of the same name; `theories/Pure/Witnesses.v`: `effect_resolves_over_moved_source`, `effects_only_read_needed`), closed as fixed under the same `NotifyDirtyExcept` / `HasReadSource` (`read_prefix`) fix as `check_resolves_over_moved_source`. An effect that reads `a`, writes `a`'s source, and reads `b` (whose check walk recomputes `a` to a new value) is marked `Dirty` and re-queued when `a` moves because `a` is already in the effect's `read_prefix`.
- `effect_restored_source_loop` (`#315`; scenario `effect_restored_source_reruns`; `theories/Pure/Witnesses.v`: `restored_source_no_answer`, `restored_source_never`, `engine_of_answers_falsified`), closed as accepted and documented under the ruling on effect writes (`Effect.freshness`, Core.fs 4639-4640, and `effects_read_only` in `theories/Pure/Refinement.v`). An effect that writes a signal it has already read marks itself `Dirty` on the write, and a `Dirty` effect runs its body without comparing its previous reads, so writing and restoring a tracked signal inside an effect body re-queues the effect on each run (`effects_read_only` excludes effect writes from `engine_of_answers_on` and `weak_refinement_except`).

- `read_settles_writer` (`#316`; scenario of the same name; `theories/Pure/Witnesses.v`, same name), closed as fixed (`Memo.ReadCurrent`, `AsyncMemo.ReadCurrent`, `Boundary.ReadCurrent` in Core.fs, and `read_node` / `pull` / `refresh_or_pull` in `theories/Pure/Engine.v` and `Optimised.v`). A read captures the node's status, failure and value before `ExitPull` settles an owed flush, so the read answers what the pull computed before any effects woken or drained by the flush mutate upstream signals.
- `quiescent_read_answers_past_flush` (scenarios `quiescent_read_answers_past_flush`, `quiescent_read_answers_past_flush_by_cleanup`; `theories/Pure/Witnesses.v`: `read_past_flush`, `read_past_flush_by_cleanup`, `read_quiescent_falsified`), closed as fixed under the same fix as `read_settles_writer` (`#316`). At quiescence on `read_past_flush`, the read captures memo 5's outcome (`Ok 1`) before `ExitPull` settles the flush that writes 9.
- `read_move_leaves_effect_queued` (scenario `self_marking_cleanup`, closing read; `theories/Pure/Witnesses.v`, same name), closed as fixed (`Graph.OweFlush`, `Memo.Run`, `Boundary.Run` in Core.fs, and `run_memo` / `orun_memo` in `theories/Pure/Engine.v` and `Optimised.v`). Moving a memo or boundary value calls `OweFlush ()` after `NotifyDirtyExcept`, so a stale read whose recomputation moves a value owes a flush and drains woken effects on `ExitPull`.

- `disposed_window_keeps_unannounced` (`#317`; scenario `disposed_self_window`; `theories/Pure/Witnesses.v`, same name), closed as fixed (`Memo.RecomputeScoped`, `AsyncMemo.Start`, `Boundary.Recompute` in Core.fs, and `recompute` / `orecompute` in `theories/Pure/Engine.v` and `Optimised.v`). When `Discharge` disposes the computation and leaves `freshness = Clean`, the window clears `unannounced <- false` before returning, so `ci_unannounced` and `resolve_clears_unannounced` (`theories/Pure/Check.v`) hold unconditionally without a liveness premise.
- `disposed_running_observer_stays_clean` (`#318`; scenario of the same name; `theories/Pure/Witnesses.v`, same name, and `check_disposed_window` in `theories/Conformance/Check.v`), closed as fixed (`IComputation.AddSource` in Core.fs, and `track` / `otrack` via `live_current` in `theories/Pure/Engine.v` and `Optimised.v`). A disposed computation ignores `AddSource`, so a body that disposes its own computation and keeps reading links no transient source edges during the remainder of the run.

- `disposed_unread_memo_value` (`#309`; scenario of the same name; `theories/Reference/Properties.v` and `theories/Pure/Witnesses.v`, same name), closed as accepted and documented (`Memo.Dispose`, Core.fs 4372-4373). A disposed memo answers reads with its last computed outcome, or the type's default when it has never run (`disposed_memo_answers`, `disposed_unread_answers_default`), while the engine-independent reference evaluates over the signals at disposal. `weak_refinement_except` carries `no_memo_disposal` for this difference, and `weak_refinement_falsified` records it.
- `write_then_dispose_memo_current_value` (`#310`; scenario of the same name; `theories/Reference/Properties.v` and `theories/Pure/Witnesses.v`, same name), closed as accepted and documented under the same ruling as `disposed_unread_memo_value` (Core.fs 4372-4373). Disposing a memo does not pull it, so a memo disposed after a source write in a batch keeps its last computed outcome and does not wake its readers.
- `batch_restored_value_reruns` (`#311`; scenario `S07`; `theories/Pure/Witnesses.v`, same name, and `batch_restored_redundant`), closed as accepted and documented (`Effect.freshness`, Core.fs 4639-4640). A `Dirty` effect runs its body without comparing its previous reads, so a batch that writes and restores a direct signal source causes one redundant run that preserves `observe_settled` (`weak_refinement_except`) and is excluded from `exact_refinement_except` by `runs_agree` (`exact_refinement_falsified`).
- `diamond_marks_running_effect` (`#312`; scenario `S15`; `theories/Pure/Witnesses.v`, same name), closed as accepted and documented under the same ruling as `batch_restored_value_reruns` (Core.fs 4639-4640). A memo moving during an effect's run marks the effect `Dirty` through its old edge when another memo is the current computation, causing one redundant run on equal reads.
- `requeue_runs_after_later_entry` (`#313`; scenario of the same name; `theories/Pure/Witnesses.v`, same name, on `requeue_order`), closed as accepted and documented under the same ruling as `diamond_marks_running_effect`. An effect re-marked during its own run is re-queued behind later entries, which refutes the start-of-flush queue as the flush order (`engine_order_exact_falsified`) while `engine_order_exact_except` proves that runs follow the drain and `history_order` covers the whole history.
- `redundant_run_reads_untracked` (`#314`; scenario of the same name; `theories/Pure/Witnesses.v`, same name), closed as accepted and documented under the same ruling as `batch_restored_value_reruns`. Because a `Dirty` effect runs without comparing its tracked reads, a redundant run can observe a new value on an untracked read; `weak_refinement_except` excludes untracked effect reads through `effects_read_only`, while `convergence` (`untracked_read_reached`) covers them.
- `reference_run_redundant` (`#319`; `theories/Reference/Properties.v`), closed as accepted and documented (`run_set`, `theories/Reference/Semantics.v`). The reference tier's `run_set` is fixed from the snapshot at the start of a round, so an earlier effect in the round restoring a signal does not remove a later effect from that round's run set.
- `redundant_failing_cleanup_changes_settled` (`#320`; `theories/Reference/Properties.v`), closed as accepted and documented (`observe_settled`, `theories/Reference/Semantics.v`, and `redundant_run_settled_equal`, `theories/Reference/Properties.v`). Removing a redundant run preserves `observe_settled` on cleanup-free programs, whereas a redundant run whose discharged cleanup raises the history's only failure is observed in `sv_failures`.
- `logging_cleanup_drop_unobserved` (`#321`; `theories/Reference/Properties.v`), closed as accepted and documented (`observe_settled`, `theories/Reference/Semantics.v`). `observe_settled` retains logs after each node's last `EvRun`, so non-failing cleanup logs of a re-run are cleared by that `EvRun`; cleanup execution is proved separately by `optimised_cleanups` (`theories/Pure/Optimised.v`) and `theories/Pure/Ownership.v`.
- `failed_batch_leaves_queue` (`#322`; scenario `failed_batch`; `theories/Pure/Witnesses.v`, same name), closed as accepted and documented (`docs/content/guide/batch.md:83-84`, `Graph.RequestFlush` in Core.fs 3159-3160, and `tests/Ranvier.Tests/Reentrancy.fs:176`). A batch whose body throws decrements `batchDepth` without calling `RequestFlush`, leaving effects woken by its writes queued until the next flush.

- `moved_past_list_clears` (`theories/Heap/SourceList.v`, with `moved_past_list_incoherent`), closed as spec corrected
  (Task 14). The heap source-list specifications as "the pure function succeeds, so the code leaves its world" are false
  on an incoherent world: observer 2 recorded at slot 3 of a source, with no slots. The pure `sources_clear` of the
  other observer succeeds, moving 2, and its `MovedIn` past the end leaves the world as it is; the heap `MovedIn`, like
  `SourceList.MovedIn` (Core.fs 599-607, 715), writes `rest[2]` unchecked, into a blank cell or past the array. The
  correspondence row of `sources_moved_in` documents that a coherent world reaches neither. The specifications take
  `edge_match`, which every reachable world has (`reachable_well_formed`). Not reproducible on F#: unreachable.
- `cleanup_registered_in_teardown_runs_late` (`theories/Pure/Optimised.v`), reproduced and closed as spec corrected. A
  cleanup that registers a cleanup with the scope it is torn down from: the second cleanup joins the emptied scope and
  runs at the next teardown, after the host's next run. Core.fs 1130-1132 documents this: "A cleanup or child
  registered while this teardown runs joins the emptied scope and outlives the teardown." Scenario
  `S18_cleanup_in_teardown`, F# trace `R2; L2 0; C2; R2; L2 1; C2; C2; L2 7; R2; L2 2`, equal to the model's. It
  falsifies Task 13a's `cleanups_before_replacement` read over every registration; the corrected statement is under
  Unproved obligations.
- `walk_covers_duplicate_observer` (`theories/Pure/Witnesses.v`), reproduced and closed as spec corrected. The test
  "a walk can miss an observer held at two positions" (`tests/Ranvier.Tests/Observers.fs`) runs the witness on the F#
  with a stub `IComputation`: `NotifyDirty` visits the observers at positions 1 and 0, and misses the one held at both
  through the walk. `ObserverSet` documents each observer as held at most once (Core.fs 190), and `ISource.AddObserver`
  requires "an observer the caller holds no edge for" (Core.fs 65), so the walk's coverage claim at Core.fs 360-369 holds
  under that premise, and `walk_covers` takes it as `held_once`. The callers that have to keep it are
  `SourceList.Append` (Core.fs 744, 769), where `observer_held_once` proves it, and `Bindings.fs:51`,
  `Combinators.fs:686`, `Combinators.fs:944`, `Projections.fs:676` and `ValueReaders.fs:100`, argued from reading in
  `CORRESPONDENCE.md`.
- `in_cleanup_observer_stays_clean` (`theories/Pure/Witnesses.v`), reproduced and closed as spec corrected. Core.fs
  4651-4671 documents the suppression as intended and not lossy. A clean memo's observer that is an effect in its
  cleanups stays clean, so `mark_forwards_on_clean` and `mark_forwards_on_unannounced` conclude "ends stale" for each
  observer in `takes_marks`. Scenario `in_cleanup_observer_stays_clean`: the cleanup's write moves the memo, no extra run
  follows, and the body reads the written value 100.
- `walk_visits_added_observer` (`theories/Pure/Witnesses.v`), closed for `Memo` and `Effect` marks: a walk's
  mutations are empty, so a mark cannot add the observer the witness visits. `mark_preserves_edges` holds for
  all five marking operations, and `notify_walk_unmutated` states each walk as `walk` with no mutations, so the
  doc at Core.fs 360-362 holds for walks whose visits are these two marks. The witness stays as a fact about
  `walk`, whose mutations over-approximate a mark. No theorem covers the other implementors of `MarkDirty` and
  `MarkCheck`, and they are outside the core nodes modelled here. From reading only, each writes its own fields,
  queues, forwards, or notifies, and none adds or removes an edge: `changed`, which is `touch ()`
  (`Combinators.fs:794, 799`), `touched` (`Combinators.fs:801`, `ValueReaders.fs:64`), `touchSummary`
  (`Projections.fs:441`), `RowWatch` (`Projections.fs:55`), `Binding` (`Bindings.fs:133`), the projection at
  `Projections.fs:1229`, `Streams.fs:504`, `Timed.fs:293`, `ForwardingEdge` (462) and the three other computations
  of `Core.fs` (5160, 5916, 6532). A change to any of these can reopen the entry.

- `disposed_stale_source_read` (scenario of the same name; `theories/Pure/Witnesses.v`, same name), reproduced and closed as spec corrected. A memo disposed
  while stale, then read by an effect, links an edge with no mark (Core.fs 4335): the source is `Dirty`, with no announcement, over a clean idle observer, so the first
  statement of `wake_ok` is false. F# trace: `[EvHostRead 2 (Ok 0); EvDispose 2; EvRun 3; EvLog 3 0; EvHostRead 2 (Ok 0)]`. The harness asserts `Dirty`, one observer, the
  effect's status and an empty queue. Core.fs 4372-4376 documents it: a disposed memo "still answers reads, with the last value it computed" and "stops being woken". No wake-up
  is owed, so `wake_ok`, `wake_told` and `later_mark_forwarded` cover live sources, and `resolve_over_disposed_dirty_source` is the same case through a walk.

- `self_writing_memo_ends_stale` (scenario `S14`), reproduced and closed as spec corrected (Task 13). A memo writes its own
  source, and an effect reads it. The memo's run ends `Dirty` holding `n_unannounced`, and the effect resolves its `Check` to
  `Clean`. The memo's body answers 0 for every value of the signal, so the effect's 0 is what the memo computes. The harness
  asserts the signal at 2, the memo `Stale` and the effect clear. F# trace:
  `[EvRun 3; EvLog 2 0; EvLog 3 0; EvLog 2 1; EvLog 2 2; EvLog 2 3; EvHostRead 2 (Ok 0); EvHostRead 1 (Ok 3)]`, and `check_S14`
  (`theories/Conformance/Check.v`) gives the model the same trace. `ordinary_programs_nothing_passed` has `gh_passed` empty at
  the end, so `no_lost_wakeups_when_nothing_passed` covers every edge from the stale memo: the memo holds its announcement.
  Core.fs 4200-4204 documents a run that invalidates its own source ending `Dirty`, and 4071-4072 documents the flag as the
  next mark's notice. The form with a wrong value is a memo whose next run changes its value: `self_marking_wake_lost` under
  Confirmed.
- `disposed_memo_keeps_observers` (`theories/Pure/Ledger.v`, same name), reproduced and closed as spec corrected (Task 13).
  `Memo.Dispose` clears the memo's sources and leaves its observers (Core.fs 4377-4398), so a reader keeps its edge to the
  disposed memo. Program: an effect reads memo `m` over `s`, the host disposes `m`, then writes `s <- 5`. The model ends with
  `m` disposed at 0 and observed by the effect, and the effect `Clean`, unqueued, with one run; trace
  `[EvRun 3; EvLog 3 0; EvDispose 2]`. F#, scenario `disposed_memo_keeps_observers` of `Scenarios.fsx`, with
  `check_disposed_memo_keeps_observers` in `Conformance/Check.v`: `m.Peek` 0, `s.Peek` 5, `ObserverCount` 1, one effect run, `queued` false, `Clean`, and the same events. Core.fs
  4372-4376 documents both halves: a disposed memo "still answers reads, with the last value it computed" and "stops being
  woken". The reader holds that value, which no later write moves, so no mark is owed.
- `glitch_free_read_statement` (`theories/Pure/WholeGraph.v`), closed as spec corrected. A disposed memo answers what it stored, as Core.fs 4372-4376 documents, as `disposed_stale_source_read` closed the same form of `wake_ok`. `glitch_free_read_falsified` is the record. `glitch_free_read` is the form proved. The form over the node alone is `read_settles_writer` under Awaiting ruling, and at quiescence `quiescent_read_answers_past_flush` under Confirmed.

## Rulings

- Scaffolding (M6, Task 11 fix round 1): `theories/Pure/WholeGraph.v` keeps its internal names public this round. Task 12 uses qualified imports if a name collides.
- `read_quiescent_batch_free_statement` (Task 11 fix round 2): a `Definition` with no proof and no `Admitted`. No proof is attempted: Task 12 does not use it, and the reviewer estimates 1000 to 1500 lines. Evidence, fuzz over the lost wake-up's shape, 3000 programs per run: batches allowed, 640 wrong answers in about 21000 reads, so the harness can see the bug; batch-free cones, 0 in 12970; no batch anywhere, 0 in 21032.
- The premises of `convergence_all_statement` cover the whole program, not an effect's cone (Task 11 fix round 3). A memo nobody reads, with an untracked read, takes `self_marking_cleanup` out of the target while the bug stays (computed), and the same applies to `memos_no_prev`, `memos_no_creation` and `memos_no_throwing_comparer`. The premises keep this scope. It narrows the target's reach and leaves the target correct: a fix owes the behaviour regardless of an unrelated memo.
- Forms 4 and 5 of `convergence`, effect cleanups and child effects (Task 11 fix round 2): they stay excluded, on the proof's reach. They are unproved by choice of scope, with no proof attempted, and the owner has been told. The reviewer's estimates are 800 to 1500 and 1500 to 2500 lines, and its search of 4000 programs per form found no counterexample.

## Definition changes

Changes to `redundant` from its first statement in the plan.

- `redundant : program -> list event -> node_id -> nat -> Prop` became
  `redundant : program -> list run -> node_id -> nat -> Prop`. A `list event` holds no read values, and
  neither does `program`, since the values depend on the history. The prose definition, "every value
  it read is equal to its previous run's", needs them. A `run` holds the reads and the events of one
  run, and `reference_trace` is the concatenation of `run_events` over `reference_runs`.
- "Equal to its previous run's" reads as: `run_all_reads` of the run equals `run_all_reads` of the
  previous run, as lists. `run_all_reads` holds every read of the run, tracked and untracked,
  cleanups included, in order. The initial run has no previous run and is not redundant. A run with
  no reads is redundant when its previous run also had none.
- Replaced at the first review: inclusion of the tracked `run_reads` in the previous run's. That form
  held for runs that read different values and produced different output. `untracked_value` logs an
  untracked read that moved from 0 to 42. `untracked_branch` selects its branch by an untracked read
  and logs for the first time with no tracked reads. Both are now checked as not redundant. The run
  set still depends on tracked reads only.
- `runs_of` keeps effect runs only: `reference_runs` now also holds disposal records, which are not runs.
- `redundant` compares reads, not events. A rerun runs the cleanups its previous run registered, so
  its events can differ from the previous run's on equal reads: `redundant_run_with_cleanup`.

## Unproved obligations

Statements Task 12 leaves unproved, with no `Admitted` behind them. None is a difference between the engine and the reference, and none carries a question for the owner.

- `runs_agree_bridge` (`theories/Pure/Refinement.v`, the premise of `exact_refinement_except`): no theorem derives `runs_agree` from the absence of redundant runs. `runs_agree` excludes a redundant run, a missed run and a reordering: `runs_agree_has_content`. The bridge wants: on a refinable program, each batch's runs in the engine and the reference's run set of that batch are equal as sets, each effect once, then `runs_agree` holds at `history_order`. `history_order_reached` meets the premise on one program by computation.
- `queue_order_through_memos` (`theories/Pure/Refinement.v`, beside `notify_queues_observers_reversed`): the walk's order is proved for a source's direct effect observers, and across a swap-removal in `notify_after_swap`. A memo among the observers forwards through `mark_check`, depth first, under a freshness test that reads the state as the walk changes it. The smallest open lemma: `notify_dirty` appends the effects of a depth-first walk of `observer_items`, from the last position, entering a memo only when `Clean` or owing an announcement, and skipping an effect that is queued, disposed or in a cleanup. `history_order` takes its rounds from the engine's runs, so no theorem computes it from observer positions.
- `answers_where_reference_settles` (`theories/Pure/Refinement.v`, the premise `bodies_total` of `engine_of_answers_on`): `refinable` plus the reference's success, with no `bodies_total`. The inputs are done: `inputs_total_of_runs` derives them from the reference's success. **The bodies stop here.** Measure: `mu` is unaffected, and it needs no premise on bodies. Step: `exec_tot`, at each `on_value`, `on_node` and read, which takes `body_ends` over all answers because the layer's post-condition `kept` says nothing of a read's answer. Two lemmas are open, and each is the smallest I can state. `read_answers_denotation`: from `gd` under a flush or batch, a read ending in `tot` answers `memo_value` over the current signals. `Aread` proves this of a completed call under `rd_pre`, so the layer would carry `aS` as well as `gd`, with `run_begin` at each body's start, and `Aexec` takes `body_eval A e cm = Some r0` as a premise, which the reference's success has to supply for each body the engine starts. `engine_evaluates_within_reference`: each memo body the engine runs at given signals, and each effect body at given reads, is one whose evaluation by the reference's `memo_value`, `reads_moved` or `run_round` at those signals gave a result, which couples `resolve_walk`'s stop at the first moved source to `reads_moved`'s stop at the first difference. Not a counterexample: by reading, the engine's walk evaluates sources in slot order and stops as the reference does, and `bodies_conservative` shows the premise is stricter than needed.
- `memos_below_stuck_paths` (`theories/Pure/Refinement.v`, the field `memos_below` of `refinable`): `memos_below` takes a rank from the class `Rank`, and every theorem over `refinable` holds for every rank, so the id order is gone (proved; `reads_higher_refinable` is a memo reading a higher id inside the theorems). What remains is the stuck path: `memos_below` asks for a lower rank on every path of a memo's body, and `memos_acyclic` only on the paths that return. Not attempted. Where it stops: `tbl_pure` builds `pure_at`, which covers every path, from `memos_below`, so a premise over the paths that return needs a `pure_at` over those paths through `WholeGraph.v`. No counterexample: a read on a stuck path has no effect on either side's answer, by reading.

- `cleanups_before_replacement` (Task 13a, `theories/Pure/Optimised.v`): each cleanup registered outside its host's
  teardown has its `EvCleanup` before the host's next `EvRun` or its `EvDispose`. Not attempted: it is a statement of
  the present engine, it needs an invariant tying a host's scope (`n_scope`, `o_host`) to the cleanups registered with
  it, and the optimised engine inherits it unchanged through `optimised_trace_eq`. What is proved: the optimised trace's
  `EvCleanup` events are the present engine's, at most one per registration, and each teardown runs each detached
  entry once (`optimised_cleanups`).

## Open statements

Statements of what the fix phase owes, each a `Definition` of type `Prop` that the present engine falsifies. Each entry
names the statement, its proved `_falsified` witness and the proved weaker form later layers use. `check.sh` rejects every `Admitted`, `admit`, `give_up`, `Axiom`, `Parameter` and `Conjecture` under
`theories/`.

- `resolve_clean_no_move_statement` (`theories/Pure/Check.v`): `check_resolves_over_moved_source` falsifies it on the present engine, and
  `resolve_clean_no_move_falsified` proves that. Weaker form: `resolve_clean_unmoved`, under `reads_settled`.
- `resolve_clean_no_stale_source_statement` (`theories/Pure/Check.v`): `self_marking_wake_lost` falsifies it on the present engine, and
  `resolve_clean_no_stale_source_falsified` proves that. It covers live sources, since a disposed source stays stale without this
  bug: `resolve_over_disposed_dirty_source`. No weaker form is proved.
- `no_lost_wakeups_statement` (`theories/Pure/Runs.v`): `walk_passed_source_marked` falsifies it on the present engine, and `no_lost_wakeups_falsified`
  proves that. Weaker forms: `no_lost_wakeups_when_nothing_passed`, `no_lost_wakeups_unless_passed` and `no_wake_lost_unless_resolved`.
- `engine_inv_preserved_statement` (`theories/Pure/Runs.v`): the same counterexample, and `engine_inv_preserved_falsified` proves that.
  Weaker form: `engine_settled_preserved`.
- `marks_survive_run_statement` (`theories/Pure/Runs.v`): `nested_self_recompute_resets_cursor` falsifies it on the present engine, and `marks_survive_run_falsified`
  proves that. Weaker form: `marks_survive_body_end`.
- `run_end_edges_are_reads_statement` (`theories/Pure/Runs.v`): the same Confirmed bug, and `run_end_edges_are_reads_falsified` proves that.
  Weaker forms: `run_end_edges_are_latest_reads` and `run_end_edges_no_nested_run`.
- `weak_refinement_statement` (`theories/Pure/Refinement.v`): `disposed_unread_memo_value` falsifies it on the present engine and reference, and `weak_refinement_falsified` proves that. A ruling on that entry, a fix of `redundant_run_reads_untracked` and of `effect_resolves_over_moved_source`, the unattempted proofs for effect cleanups and child effects, and `memos_below_stuck_paths` under Unproved obligations are what it needs. `memos_below` takes any rank, so a memo reading a higher id is inside `weak_refinement_except` (proved: `reads_higher_refinable`; computed: `memo_reads_higher_id` gives identical `eng` and `rtrace` traces over a two-batch history). Weaker form: `weak_refinement_except`.
- `exact_refinement_statement` (`theories/Pure/Refinement.v`): `batch_restored_value_reruns` falsifies it, and `exact_refinement_falsified` proves that. Weaker form: `exact_refinement_except`.
- `engine_order_exact_statement` (`theories/Pure/Refinement.v`): this statement takes the queue at a flush's start as the flush's order. `requeue_runs_after_later_entry` falsifies that definition of the order, and `engine_order_exact_falsified` proves that. It says nothing of `history_order`. A fix of `diamond_marks_running_effect` that stops marking a running effect removes this requeue, by reading. Weaker form: `engine_order_exact_except`, which delivers "runs follow the drain".
- `engine_of_answers_statement` (`theories/Pure/Refinement.v`): `effect_restored_source_loop` falsifies it at every fuel, and `engine_of_answers_falsified` proves that. A ruling on effect writes decides what it should say. Weaker form: `engine_of_answers_on`, on a narrower fragment: `refinable`, `bodies_total` and the reference's success. See `answers_where_reference_settles`.
- `read_quiescent_statement` (`theories/Pure/WholeGraph.v`): `quiescent_read_answers_past_flush` falsifies it on the present engine, and `read_quiescent_falsified` proves that. Weaker form: `glitch_free_read`.
- `convergence_all_statement` (`theories/Pure/WholeGraph.v`): `self_marking_wake_lost` and the lost update of `cmp_never_passes_source` falsify it on the present engine, and `convergence_all_falsified` proves that, with each premise proved on both. Weaker form: `convergence`.
  **The fix phase's target.** Inside it, as the bugs the fix owes: effect writes, memo writes reaching no later read of the body, memo cleanups, `CmpNever`, and effect cleanups, child effects and disposals.
  **What it is not asked to fix through this statement.** Each premise, with what it excludes. Each field is checked over a memo's whole body, whichever path runs, and over every live memo of the program, so each one also excludes programs a fix may reach. `memos_no_throwing_comparer` is checked on the comparer, whether or not it fires.
  On a path that reaches it, a memo cycle, a self-read, an untracked read, `EPrev` or an excluded created node has no `den_all` answer. A throwing comparer is a failure `den_all` does not model, once the comparer fires.
  A memo reading `v`, then `if v<100 ret v else <excluded form>`, read by an effect, with the signal written to 5, has a false premise, `den_all` answers `Ok 5` and the conclusion holds: `untracked_conservative`, `prev_conservative` and `creation_conservative`. With a `CmpThrows` memo that ran once and a comparer never called, the premise is false, `den_all` answers `Ok 0` and the conclusion holds: `comparer_conservative`. All four are proved in `Witnesses.v`. `memos_ranked` has the same gap by reading, through a self-read on a branch that never runs.
  `memos_ranked` and `memos_read_before_write` are the notable cases, below.
  - `memos_ranked`: a memo cycle or self-read has no denotation on a path that reaches it. Conservatively, also a computed target with no bound, since the rank quantifies over every answer, as `memos_acyclic` does.
  - `memos_no_untracked`: a reader keeps the value of the read's run, which is what an untracked read means. `row_memo_untracked_excluded`; F#: signal 5, memo 0, `Clean`.
  - `memos_no_throwing_comparer`: the comparer's failure is outside the body's computation. `row_throwing_comparer_excluded`; F#: logs 0 then 7. Modelling it in the denotation is the alternative the ruling left open.
  - `memos_no_prev`: `EPrev` answers from the memo's history. `row_memo_prev_excluded`.
  - `memos_no_creation`: a created node's id varies by run. A signal in any memo, and a memo or effect in an `Owning` memo. `row_memo_creates_excluded`. The ruling names the `Pure` case. This premise covers the rest, where `erase` has no value.
  - `memos_read_before_write`: a read in a memo's body after a write on the same path that reaches its target. `row_read_between_writes_excluded`: every run answers 1 and `den_all` answers 2; F#: signal 2, memo 1.
    **Conservative.** It also excludes the shape of `check_resolves_over_moved_source`: `check_over_moved_excluded` (proved). There the effect saw 0, `den_all` answers 1 and the memo is `Clean`: `check_over_moved_left_behind` (computed). Reasoned: the memo's next run answers 1 and is stable, so a fix can reach this shape. That bug stays owed through the Open `resolve_clean_no_move_statement`. A narrower form tried in review left 30 unclassified candidates (fuzz, seed 73), so no narrower form is shown.
  **Forms the statement holds, proved on the present engine:** `row_child_effect_holds`, `row_effect_disposes_holds` (the logs clause has a gate for an effect creating or disposing a node), `row_pure_cleanup_holds`, `row_pure_creation_holds` (each denotes `pure_scope_failure`), `row_failing_write_holds` (`erase` keeps a write's value failure). `converged_all` covers effects with an owner: `row_pure_creation_holds` found that `exec` adds an effect created under `Pure` and then fails, leaving a node that never ran, where F# raises before the effect exists.
  Disposal and cycles need no further premise. `den_all` answers a disposed memo's stored view, and a body with no end fails the `run_top` premise. That is reasoned. Fuzz on the present engine, over programs passing a sufficient test of the premises: 0 hits in 15930 programs with effect cleanups, writes, child effects, disposals, batches and untracked reads; 694 hits in 1494 on the lost wake-up's template, the shortest a memo cleanup writing, so the harness sees the bug. The test admits no memo body that writes.
  **Programs with no fixed point.** Two memos writing each other's sources, `s1 := s2<2` and `s2 := s1+s3`, with `s3` at 1, have no fixed point, and the present engine ends on them. The pair is the shortest of 3 oscillating programs of 3904 passing the premises in review's fuzz, seed 71, and review inspected only the shortest. Reasoned: a correct engine does not end on a program with no fixed point, so the statement holds on that program only because such a fix hangs, which turns a program that ends today into a hang. Ten further `or_diverge` hits from the same search are unclassified.
  **Whole-program premises.** The premises cover the whole program, not an effect's cone: `## Rulings` records this.
