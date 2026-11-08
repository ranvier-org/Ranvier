# Defect ledger

Only Confirmed entries count as bugs. Where `src/Ranvier/Core.fs` documents a behaviour as
intended, the entry closes as "spec corrected".

## Suspected

- `disposed_unread_memo_value` (`theories/Reference/Properties.v`): which value a disposed memo keeps
  when it has had no demand since its sources moved. By the owner's ruling, the reference keeps the
  memo's value over the signals current at the disposal's position in the batch. From reading Core.fs,
  not from running it: a memo starts `Dirty` and `Uninitialized` with a default value (4063-4065),
  `Create` does not compute it (4144-4147), the read paths do not refresh a disposed memo (4323, 4335),
  and `Value` then answers the stored value (4539-4548), as the doc at 4372 states. So in `unread_memo`
  F# would answer the type's default where the reference answers 5. By the same reading,
  `write_then_dispose_memo_current_value` also differs: F# leaves the written memo unpulled and keeps 1,
  and its reader does not run, where the reference answers 6 and runs the reader. The question for the
  owner: does the reference keep this engine-independent value, or does it model demand as Core.fs has
  it? Modelling demand would put one engine's laziness in the tier every engine proves against. Task 13
  runs it against the F#.
- `batch_restored_value_reruns` (scenario `S07`): a batch writes 0, 1, 0 under an effect that reads the signal
  directly. The first write marks the effect `Dirty`, a `Dirty` effect runs with no comparison of its reads, and
  it logs the value of its previous run. F# trace: `[EvRun 2; EvLog 2 0; EvRun 2; EvLog 2 0]`. A redundant run
  by `redundant`, for the owner's ruling at Task 12.
- `self_writing_memo_ends_stale` (scenario `S14`): a memo writes its own source, and an effect reads it. The memo's
  value does not change, so the effect resolves its `Check` to `Clean` while the memo is `Dirty` with no further mark
  due. The flush ends with the signal at 2 and the memo's last run over 1, which the harness asserts. F# trace:
  `[EvRun 3; EvLog 2 0; EvLog 3 0; EvLog 2 1; EvLog 2 2; EvLog 2 3; EvHostRead 2 (Ok 0); EvHostRead 1 (Ok 3)]`. `EvLog 2 2`
  is the host read's pull, whose write queues the effect, and `EvLog 2 3` is the walk in the flush that pull settles.
  Here the memo's value stays 0, so the effect's read stays right. When the next run changes the value, the effect is
  left behind: `self_marking_wake_lost` under Confirmed.
- `diamond_marks_running_effect` (scenario `S15`): the effect reads `[h, x, y]` and `x` reads `[h, y]`, and a batch
  writes `h` and `y`'s source. Under the effect's run, `y`'s run moves and marks the effect through its old edge,
  since `NotifyDirtyExcept` skips only `x`, the current computation. The effect runs again on equal reads. F# trace:
  `[EvRun 5; EvLog 3 0; EvLog 4 0; EvLog 5 0; EvRun 5; EvLog 3 1; EvLog 4 2; EvLog 5 4; EvRun 5; EvLog 5 4]`.
- `recompute_scoped_keeps_unannounced` (from reading, no trace): `RecomputeScoped` sets `Clean` before `Discharge`
  (Core.fs 4191-4192) and does not clear `unannounced`. Verified from reading, not run: the flag from before the window
  survives only when the memo is disposed in the window. With the memo live, `runs = before` takes `Run`, which clears it
  (4208), and otherwise a nested `Run` has cleared it, and any later value comes from that run's end (4289-4290). A mark in
  the window takes the `Clean` branch and leaves the flag. Whether the disposed case can miss or gain a notice is open.
- `disposed_memo_keeps_observers` (from reading, no trace): `Memo.Dispose` clears the memo's sources and leaves its
  observers (Core.fs 4377-4398). A reader keeps an edge to the disposed memo, which marks no one again once its sources
  are gone, except for the marks of a run in progress when it was disposed.

## Modelled

## Confirmed

- `self_marking_wake_lost` (scenarios `self_marking_cleanup`, `self_marking_body`, `self_marking_through_memo`;
  `theories/Pure/Witnesses.v`: `self_marking_cleanup_left_behind`, `self_marking_body_left_behind`,
  `self_marking_through_memo_left_behind`): a run marked during itself ends stale with an unchanged value, its reader's
  `Check` resolves to `Clean`, and the reader keeps a value the memo's next run would change. No mark is due. F# trace of
  `self_marking_cleanup` before its closing read: `R6; L5 0; L6 0; C4; L5 0`. The signal holds 50, `x` is `Dirty` with value
  0, and the effect is clean and saw only 0, with an empty queue. An explicit `Flush` adds no event and changes no status.
  The harness asserts each of these, and the closing host read answers 50. `self_marking_body` is the same end state from a
  memo writing its own source, with the signal at 2, the memo at 0 and its next run answering 2.
  Path: `Effect.Execute`'s walk (Core.fs 4792) pulls `x`. A write during `x`'s run marks `x` from `Clean` (4483-4498), and
  the notice reaches an effect already `Check`. `x`'s value does not move, so it does not mark the effect dirty, and it ends
  with `unannounced` (4289). The walk ends and the effect goes `Clean` (4798-4799). `MarkReaderCheck` (4339) is on the body
  read path only. `Memo.ResolveCheck` (4314-4316) has the same gap, through a reading memo: `self_marking_through_memo`.
  The nearest docs: 4071-4072 describe the flag as the next mark's notice and say nothing of a reader that resolves in the
  meantime, and 4301-4302 say "Reaching the end still in `Check` means nothing above moved", which
  `self_marking_through_memo` contradicts: the reading memo goes clean over a source whose next run moves. 3438-3441 and the
  doc of `lateLinkConverges` (`AsyncEdges.fs`, 2e8f9690) point the same way: a reader is told when the node ends a run stale.
  The spec names this as its known starting candidate.
  **`wake_ok` holds.** Every stale node in each end state holds `n_unann` (`stale_all_unann`), so the edge from the stale
  node to the clean, unqueued, idle observer is covered by the last disjunct alone, and `no_lost_wakeups` is not falsified:
  the theorem does not claim a later mark will arrive. **The property that fails:** when a flush returns with an empty live
  queue and nothing running, every effect's last run read, from each source, the value that source's body computes from the
  current signals. A form over each source's stored value passes on these witnesses, since the effect read the stored 0.
  These programs are outside the well-behaved fragment, since `well_behaved_memo` rejects `CWrite` and `CCleanup`, so the
  fragment's convergence theorem and refinement are not falsified. Task 11 owns the statement of that property over all
  programs, so no open theorem is named here. Task 8's `resolve_clean_no_move` meets this shape.
  **Contrary evidence, by shape.** `tests/Ranvier.Tests/Invalidation.fs:33-35` says "A memo that writes is outside the
  contract" and "The same shape arises legitimately from a source that settles inline". `self_marking_body` and
  `self_marking_through_memo` are memos that write, so that comment puts them outside the contract. `self_marking_cleanup` is
  the strongest case: no body writes, only a cleanup writes a source, and Core.fs 4178-4181 documents a cleanup that writes a
  source as a behaviour. The inline settle is `S03`'s shape, which async nodes reach and which this entry has not run on them.
- `read_move_leaves_effect_queued` (scenario `self_marking_cleanup`, closing read; `theories/Pure/Witnesses.v`, same name): a
  stale read whose run moves its value queues a clean observer, and no flush follows. After `x.Value`, the effect is `Dirty`
  and queued, the queue holds 1, `flushOwed` is false and `Runs` is 1. An explicit `Flush` then runs it and logs 50. The
  harness asserts each of these. The read's move goes through `NotifyDirtyExcept` (Core.fs 4283), and only a write calls
  `RequestFlush` (3822), so `ExitPull` finds nothing owed (3243). The doc at 3159-3161 says effects "run once the outermost
  body or read has finished". The only route found starts from the left-behind state above: an effect observing a stale
  memo is otherwise already queued by the mark that made it stale. Outside the well-behaved fragment by that route.
- `nested_self_recompute_resets_cursor` (scenario `nested_self_run`; `theories/Pure/Witnesses.v`:
  `nested_self_run_ends_clean_and_wrong`): a run reads `a`, writes `flip`, which marks it, and reads itself. The nested run
  (Core.fs 4209) takes another branch and its `EndRun` trims `a`'s edge, and the outer run then stores the value it computed
  from `a`. After `flip <- 2`, the memo is clean with value 10, its body now answers 20, `a` has no observers and `b`
  has one. After `a <- 11` the memo is still clean at 10. The effect is clean, nothing is queued or running, and an explicit
  `Flush` changes nothing. The harness asserts each of these and the witness states them. F# trace: `[EvRun 5; EvLog 5 10; EvRun 5; EvLog 5 10; EvHostRead 4 (Ok 10)]`. Unlike `self_marking_wake_lost`, the
  memo is clean, so no marked state records the difference. Core.fs 4205-4206 covers a self-read's overflow and 4178-4181 a
  cleanup's nested run, and neither states this. A self-read and a write put it outside the well-behaved fragment. The
  glitch-free claim excepts the self-dirtying case, and Task 9's `run_end_edges_are_reads` meets this shape: the outer run
  read `a`, and its source list holds no slot for it.

- `self_read_check_overflow` (scenarios `self_read_check_overflow`, `mutual_read_check_overflow`, run by `--fatal`;
  `theories/Pure/Witnesses.v`: `self_read_check_resolves_itself`): a memo reads itself, then a memo `a` over a signal. Two reads
  leave it clean with itself as its first source, and a write to the signal marks `a` dirty, which tells the memo `Check`. The
  next read overflows the stack, which ends the process: no caller can catch it. Program:
  `(signal 1 (k 0) (memo 2 pure eq (read 10 (v 1) (ret (v 10))) (memo 3 pure eq (read 10 (v 3) (read 11 (v 2) (ret (v 11)))) (read 20 (v 3) (read 21 (v 3) (write (v 1) (k 1) (read 22 (v 3) (ret (k 0)))))))))`.
  The harness asserts `Clean`, then `Check`, then that the memo observes itself, before the fatal read.
  Path: `TrackCurrent` (Core.fs 4334) to `Refresh` (4360) to `ResolveCheck` (4304), whose walk calls `SourceAt(0).UpdateIfNecessary()`
  (4309) on the memo itself, which is still in `Check`, so `EnsureCurrent` (4319) enters `Refresh` again, and no body runs
  between the frames. Mark and body guards never see it. `Generate.fsx`'s step and depth budgets count bodies, and do not see it
  either. The model has the same walk: `resolve_walk` spends fuel and changes nothing, so the read has no result at fuel 500 or
  2000, where the same program without that read returns.
  **Against the documents.** Core.fs 4205-4206 says marking clean before the body "turns a memo that reads itself from a stack
  overflow into a default value". That holds for the path it names, `Run`, and the walk in `ResolveCheck` is a second path with no
  such guard. The test "a memo that reads itself yields a default rather than overflowing" (`tests/Ranvier.Tests/Invalidation.fs:66`)
  says of a self-read: "Not a supported thing to write, but a cycle must fail as a value, not as a stack overflow the caller cannot
  catch." That test reads through `Peek`, so it takes no edge to itself and does not reach this path. The cycle of two memos overflows
  the same way (`mutual_read_check_overflow`, F# only, since the command language has no forward reference).
  **The contract is the owner's call.** A self-reading memo may be outside the contract, and the owner decides that. No document
  found says this outcome is intended, which is why the entry is Confirmed and not closed as spec corrected.
  **Theorems.** No stated theorem is falsified, and none is weakened. `rwf` includes `memos_acyclic`, which rejects this program, so
  Task 12's `engine_of_answers` and `weak_refinement` keep their form with that premise. `well_behaved_memo` alone accepts a self-read
  and is no substitute for it. Any statement that `read` or `update_if_necessary` returns needs `memos_acyclic` as a premise, and
  Task 8's `resolve_clean_no_move` and Task 9's `engine_inv_preserved` hold over results, which this read has none of. So no open
  theorem is named here. In the differential check this program is `fatal` on the F# side and has no result in the model, which
  counts as a both-sides skip and closes nothing.

## Awaiting ruling

- `disposed_running_observer_stays_clean` (`theories/Pure/Witnesses.v`, scenario of the same name): an effect that disposes
  itself mid-body keeps running, a later read links an edge, its write leaves it clean and unqueued, and the `finally` at
  Core.fs 4854-4855 clears the edge. The harness asserts each step inside the body: the memo has 0 observers after the
  disposal and 1 after the read, the effect's status is `None` with `queued` false after the write, and 0 observers at the
  end. `check_disposed_window` states the window of the model. No Core.fs doc covers this window. `Memo.Dispose`'s doc at 4373 ("stops
  tracking and stops being woken") and `Effect.Dispose`'s at 4882 ("Detaches from every source") lean the other way. Nothing is lost: the effect is
  disposed, so no later run owes a value, and the edge ends with the run. The question for the owner: is the window intended?
  The case falls under the running disjunct of `later_mark_forwarded`, not under `takes_marks`.
- `reference_run_redundant` (`theories/Reference/Properties.v`): the reference tier itself has a
  redundant run. A round's run set comes from its starting snapshot, and an earlier effect of the
  round can restore the value a later one read. Under `t_rev`, the `resetting` program re-runs effect
  8 on the value of its previous run. The definition follows the ruling for Task 3. The question for
  the owner: does a run set stay fixed at the round's start, or does a run re-compare when its turn
  comes? Not a Core.fs defect.
- `redundant_failing_cleanup_changes_settled` (`theories/Reference/Properties.v`): removing a redundant
  run can change `obs_settled`. In `resetting_failing_cleanup` under `t_rev` and history
  `[[RWrite 0 1; RWrite 1 1]]`, run 1 of effect 8 is redundant, and the only `EvFail 8 7` of the trace
  comes from the cleanup it discharges. `sv_failures` holds the distinct failures of the history, so a
  removed run takes out a failure that occurs only in its cleanups. The design's claim that removing a
  redundant run preserves `obs_settled` holds on the cleanup-free example `redundant_run_settled_equal`
  and is false here. `obs_settled` and `redundant` are unchanged, pending the owner's ruling.
- `logging_cleanup_drop_unobserved` (`theories/Reference/Properties.v`): `obs_settled` admits an engine
  that drops a non-failing logging cleanup of a re-run: `[R9;C9;L100;C9;L0;R9]` and `[R9;R9]` have equal
  views. Task 13a's `optimised_cleanups` covers this for the worked optimisation.

## Closed

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

## Definition changes

Changes to `redundant` from its first statement in the plan.

- `redundant : rprog -> list event -> nid -> nat -> Prop` became
  `redundant : rprog -> list run -> nid -> nat -> Prop`. A `list event` holds no read values, and
  neither does `rprog`, since the values depend on the history. The prose definition, "every value
  it read is equal to its previous run's", needs them. A `run` holds the reads and the events of one
  run, and `rtrace` is the concatenation of `run_events` over `rruns`.
- "Equal to its previous run's" reads as: `run_all_reads` of the run equals `run_all_reads` of the
  previous run, as lists. `run_all_reads` holds every read of the run, tracked and untracked,
  cleanups included, in order. The initial run has no previous run and is not redundant. A run with
  no reads is redundant when its previous run also had none.
- Replaced at the first review: inclusion of the tracked `run_reads` in the previous run's. That form
  held for runs that read different values and produced different output. `untracked_value` logs an
  untracked read that moved from 0 to 42. `untracked_branch` selects its branch by an untracked read
  and logs for the first time with no tracked reads. Both are now checked as not redundant. The run
  set still depends on tracked reads only.
- `runs_of` keeps effect runs only: `rruns` now also holds disposal records, which are not runs.
- `redundant` compares reads, not events. A rerun runs the cleanups its previous run registered, so
  its events can differ from the previous run's on equal reads: `redundant_run_with_cleanup`.

## Open theorems

Names of theorems that may contain `Admitted`, `admit` or an `Axiom`, one per line as a list item.
`check.sh` compares its findings against this section.
