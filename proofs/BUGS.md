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
  `disposed_memo_answers` (Task 10) states the behaviour of the doc at 4372: a read answers what the memo stored, with no run. `disposed_unread_answers_default` has `unread_memo`'s
  shape in the model, answering 0. That doc's "the last value it computed" does not describe a memo that never computed one.
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
  **`wake_ok` holds here.** Every stale node in each end state holds `n_unannounced` (`stale_all_unannounced`), so the edge from the stale
  node to the clean, unqueued, idle observer is covered by the last disjunct alone: `wake_ok` does not claim a later mark will arrive.
  `wake_told` fails on these states unless the observer is in `gh_over`, and these observers are. `walk_passed_source_marked` is what
  falsifies `no_lost_wakeups_statement`. **The property that fails:** when a flush returns with an empty live
  queue and nothing running, every effect's last run read, from each source, the value that source's body computes from the
  current signals. A form over each source's stored value passes on these witnesses, since the effect read the stored 0.
  These programs are outside the well-behaved fragment, since `well_behaved_memo` rejects `CWrite` and `CCleanup`, so the
  fragment's convergence theorem and refinement are not falsified. Task 11 owns the statement of that property over all
  programs. `resolve_clean_no_stale_source` (Task 8) is the Open form at a single resolution, falsified through `stale_source_reader`: the
  reader answers 0 with the signal at 2 and its source `Dirty`, and the source's next run answers 2. The harness asserts each of these.
  **Contrary evidence, by shape.** `tests/Ranvier.Tests/Invalidation.fs:33-35` says "A memo that writes is outside the
  contract" and "The same shape arises legitimately from a source that settles inline". `self_marking_body` and
  `self_marking_through_memo` are memos that write, so that comment puts them outside the contract. `self_marking_cleanup` is
  the strongest case: no body writes, only a cleanup writes a source. Core.fs 4178-4181 describes a cleanup that writes a source and
  then reads the memo, and this cleanup does not read it, so no document covers this form. The inline settle is `S03`'s shape, which async nodes reach and which this entry has not run on them.
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
  **Theorems.** `glitch_free_read` (Task 11) is affected: for a node that is not `self_dirtying`, a read returns what its body
  computes, and this read returns nothing. Task 11's `self_dirtying` or `well_behaved_program` must exclude memo cycles, self-reads
  included, or this program falsifies the theorem. The plan's Task 11 carries that. No proved theorem is falsified, and none is
  weakened. `well_formed_program` includes `memos_acyclic`, which rejects this program, so Task 12's `engine_of_answers` and `weak_refinement` keep
  their form with that premise. `well_behaved_memo` alone accepts a self-read and is no substitute for it. Any statement that `read`
  or `update_if_necessary` returns needs `memos_acyclic` as a premise, and Task 8's `resolve_clean_no_move` and Task 9's
  `engine_inv_preserved` hold over results, which this read has none of. In the differential check this program is `fatal` on the
  F# side and has no result in the model, which counts as a both-sides skip and closes nothing.

- `check_resolves_over_moved_source` (scenario `check_over_moved`; `theories/Pure/Witnesses.v`, same name, and
  `resolve_clean_no_move_falsified`): a memo `x` reads `a`, writes `a`'s source, and reads `b`, a memo over `a` that has run
  before. The write marks `a` `Dirty`, and `x` and `b` `Check`. `b`'s `ResolveCheck` runs `a` with `x` as the current
  computation, and `NotifyDirtyExcept` (Core.fs 4283) skips `x`. `x`'s run ends in `Check` with its value from the old `a`.
  The next read walks `x`'s sources, both now clean, and `x` goes `Clean` with no run: `x` answers 0 with `a` at 1. F# trace:
  `[EvHostRead 3 (Ok 0); EvLog 4 0; EvHostRead 4 (Ok 0); EvHostRead 4 (Ok 0)]`. The harness asserts `Check`, the answer 0, `Clean`
  and `a` at 1. Against the documents: Core.fs 4301-4302 says "Reaching the end still in `Check` means nothing above moved", and
  here `a` moved. The doc of `NotifyDirtyExcept` (Core.fs 386-393) gives the skip's reason: the running computation "receives the
  new value from that very read". This reader read the source earlier in its run, and takes no new value from the walk.
  `well_behaved_memo` rejects this program, since it writes.
  **Variant, same cause:** `check_two_reads`, witness `check_two_reads_over_moved_source`. The body reads `a`, writes the source,
  reads `a` again and answers the first read. F# trace: `[EvHostRead 2 (Ok 0); EvLog 3 0; EvHostRead 3 (Ok 0); EvHostRead 3 (Ok 0)]`.
  The harness asserts `Check`, the answer 0, `Clean`, and `a` at 1. `gh_seen` keeps every view of a source read in a run, so
  `moved_since_last_run` sees the first read.
  **Contrary evidence.** `tests/Ranvier.Tests/Invalidation.fs:33-35` says "A memo that writes is outside the contract", and this
  memo writes. From reading, not run: a cleanup that writes under `x`'s run reaches the same skip. Core.fs 4178-4181 describes a cleanup
  that writes a source and then reads the memo, so no document covers a cleanup that only writes. **Theorems.** `resolve_clean_no_move` is false on this engine, and is the Open theorem below.
  `resolve_clean_unmoved` proves it under `reads_settled`, from `check_invariant`'s preservation and `unmoved_short_of_dirty`, without using the walk: the memo is not in `gh_unnoticed`, and neither it
  nor a source is running. `gh_unnoticed` records four events: this skip, a read of a running node, the `Dirty` cleared at the start
  of `RecomputeScoped` (Core.fs 4191), and a run with no move ending on a view other than the published one, which takes a nested run.
  `check_cutoff_settled` shows the premise holding on an ordinary resolution. From `Clean` or `Check`, the conclusion holds of any
  memo: `unmoved_short_of_dirty`.
  **The other Confirmed entries.** `self_marking_wake_lost` does not falsify `resolve_clean_no_move`, since the reader read the
  stored value: `self_marking_through_memo_unmoved`. It falsifies `resolve_clean_no_stale_source`, a separate Open theorem, so a fix
  of the skip alone leaves that one open: `resolve_clean_no_stale_source_falsified`, scenario `stale_source_reader`. `read_move_leaves_effect_queued` changes no memo's reads. `nested_self_recompute_resets_cursor`
  starts from `Dirty`, and its lost read has no slot, so `moved_since_last_run` does not see it. `self_read_check_overflow` has no
  result to conclude from, and the theorem's premise is conditional on one: `self_read_check_resolves_itself` has the read at no result.

- `walk_passed_source_marked` (scenarios `walk_passed_source_marked`, `walk_passed_source_cleanup`; `theories/Pure/Witnesses.v`, same names):
  a check walk passes a source, and a later source's run marks it again. The effect reads `a` then `b`, and `b` writes `a`'s source, in its
  body or in a cleanup. A write to `b`'s source queues the effect. `Effect.Execute`'s walk (Core.fs 4792) finds `a` clean and runs `b`. The write marks
  `a` `Dirty` from `Clean`, and `a`'s notice finds the effect already in `Check`, so it adds nothing. `b`'s value holds, and the walk ends with the
  effect `Check`, which goes `Clean` (4798-4799). F# trace: `[EvRun 5; EvLog 5 0; EvHostRead 3 (Ok 2)]`, and with the cleanup
  `[EvRun 5; EvLog 5 0; EvCleanup 4; EvHostRead 3 (Ok 2)]`. Before the closing read the harness asserts the signal at 1, `a` at 0 and `Dirty` with
  `unannounced` false, one effect run, status `None` for the effect, an empty queue, and an explicit `Flush` changing nothing. The effect logged 0, and
  no mark is due: no disjunct of `wake_ok` covers the edge, so this is a lost wake-up in the sense of the design, and not only a stale value.
  **The effect stays deaf.** The harness then writes the signal again, to 2, and the effect still has one run: `a` is `Dirty` with no announcement, so it tells no observer. The effect
  wakes when another source of it moves, or when `a` is read and a flush follows. `walk_passed_stays_deaf` states this in the model.
  `ResolveCheck` (4308-4316) has the same walk. Against the documents: 4301-4302 says "Reaching the end still in `Check` means nothing above moved",
  and a source here is `Dirty` at the end. 4178-4181 describes a cleanup that writes a source and then reads the memo, and this cleanup does not read it, so no document covers this form.
  `tests/Ranvier.Tests/Invalidation.fs:33-35` puts a memo that writes outside the contract. In the cleanup form no memo body writes, so that comment does not reach this form.
  **Theorems.** `no_lost_wakeups_statement` and `engine_inv_preserved_statement` are false: `no_lost_wakeups_falsified`, `engine_inv_preserved_falsified`.
  `no_lost_wakeups_unless_passed` proves the exception is the only one: an observer outside `gh_passed` is covered. `gh_passed` is reached only by a walk
  ending over a live, stale source with no announcement, and it is per computation: once an observer has passed one source, the exception covers all its sources until its next run.
  `ordinary_programs_nothing_passed` checks `gh_passed` empty on the ordinary conformance programs. `well_behaved_nothing_passed` proves it empty on any program whose memos are outside the self-dirtying case and whose effects only read.
  `diamond_ends_memo_stale` ends a memo's run stale with no write in any body. `CmpNever` is the counterexample on a program with no write: `cmp_never_passes_source`.

- `cmp_never_passes_source` (scenario of the same name; `theories/Pure/Witnesses.v`, same name, on the program `cmp_never_source`): a walk passes a source with no body writing. Memos 3 and 4 hold `CmpNever`, and every run of them counts as a move. A write queues
  the effect, which reads `a` (memo 5, over the signal and memo 4), then `b` (memo 8, at the end of a chain from memo 4). `Effect.Execute`'s walk (Core.fs 4792) runs `a`, which holds its value. Reaching `b` runs the chain, and memo 4's run counts as a move, so its notice marks `a` `Dirty` from `Clean`.
  `a`'s notice finds the effect already in `Check`, and the effect goes `Clean`. F# trace: `[EvRun 9; EvLog 9 1; EvHostRead 5 (Ok 1)]`. Before the closing read the harness asserts `a` at 1 and `Dirty` with `unannounced` false, one effect run, status `None` for the effect, an empty queue, and an explicit `Flush` changing nothing.
  No disjunct of `wake_ok` covers the edge. **No value is lost on this program:** `a`'s next run answers 1, and the next write reaches the effect through `b`. So this is the lost wake-up of `walk_passed_source_marked` by another route, and the entry claims no stale read.
  `CmpNever` is the model's name for a comparer whose `Equals` answers false. The public `createMemoWithComparer` (`Api.fs` 257) takes such a comparer, and the harness calls the `Memo.CreateWithComparer` behind it by reflection, as for `CmpThrows`.
  **Theorems.** `well_behaved_program` excludes `CmpNever`, so `well_behaved_nothing_passed` does not reach this program.

- `effect_resolves_over_moved_source` (scenario of the same name; `theories/Pure/Witnesses.v`, same name): the effect form of
  `check_resolves_over_moved_source`. An effect reads `a`, writes `a`'s source, and reads `b`, a memo over `a`. The write marks `a` `Dirty` and the
  running effect `Check` and queued. `b`'s walk runs `a` with the effect as the current computation, and `NotifyDirtyExcept` (Core.fs 4283) skips the effect.
  The next execution walks `a` and `b`, both clean, and the effect goes `Clean` with no run. F# trace: `[EvHostRead 3 (Ok 0); EvRun 4; EvLog 4 0; EvRun 4; EvLog 4 2; EvRun 4; EvLog 4 1]`. The harness
  asserts `a` at 1 and `Clean`, one effect run, status `None`, an empty queue and a `Flush` changing nothing: the effect's body used 0 and `a` holds 1. The next write of the source wakes it, and the harness asserts the runs that follow, three in all.
  **Same cause as `check_resolves_over_moved_source`:** one fix, a different victim.
  Against the documents: 386-393 give the skip's reason, that the computation "receives the new value from that very read", and this read is of `b`. Unlike a memo,
  an effect that writes is inside the contract: `well_behaved_memo` restricts memos only. `wake_ok` holds, since `a` is `Clean`. So this falsifies no theorem of this layer.
  **`convergence`.** `effects_only_read_needed` proves the conclusion of `convergence` false on this program, whose memos are all well behaved. `convergence` takes `effects_only_read` as a named premise, which excludes every effect that writes. The premise is provisional: the ruling on this entry decides whether it stays or narrows.
  `moved_run_skips_current` states the skip, and `skip_removed_fires` shows it fails with the skip removed.

## Awaiting ruling

- `disposed_window_keeps_unannounced` (scenario `disposed_self_window`; `theories/Pure/Witnesses.v`, same name): `RecomputeScoped`
  sets `Clean` before `Discharge` (Core.fs 4191-4192) and does not clear `unannounced`. A nested run ends stale, so the flag is set,
  and registers a cleanup that disposes the memo. An untracked self-read then enters the window, and the cleanup disposes the memo:
  it ends `Clean`, disposed, with the flag set, from a read that started in `Check`. F# trace:
  `[EvHostRead 4 (Ok 0); EvCleanup 4; EvDispose 4; EvHostRead 4 (Ok 0)]`, and the harness asserts `Clean`, the flag and the disposal
  from the private fields. Only private fields differ, Core.fs 4071-4073 is honoured, and no document covers the flag on a disposed
  memo. `resolve_clears_unannounced` takes `n_disposed d' = false` for this witness, and proves every path of a live memo. The question
  for the owner: should disposal or the window clear the flag?
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
  round can restore the value a later one read. Under `order_reversed`, the `resetting` program re-runs effect
  8 on the value of its previous run. The definition follows the ruling for Task 3. The question for
  the owner: does a run set stay fixed at the round's start, or does a run re-compare when its turn
  comes? Not a Core.fs defect.
- `redundant_failing_cleanup_changes_settled` (`theories/Reference/Properties.v`): removing a redundant
  run can change `observe_settled`. In `resetting_failing_cleanup` under `order_reversed` and history
  `[[RWrite 0 1; RWrite 1 1]]`, run 1 of effect 8 is redundant, and the only `EvFail 8 7` of the trace
  comes from the cleanup it discharges. `sv_failures` holds the distinct failures of the history, so a
  removed run takes out a failure that occurs only in its cleanups. The design's claim that removing a
  redundant run preserves `observe_settled` holds on the cleanup-free example `redundant_run_settled_equal`
  and is false here. `observe_settled` and `redundant` are unchanged, pending the owner's ruling.
- `logging_cleanup_drop_unobserved` (`theories/Reference/Properties.v`): `observe_settled` admits an engine
  that drops a non-failing logging cleanup of a re-run: `[R9;C9;L100;C9;L0;R9]` and `[R9;R9]` have equal
  views. Task 13a's `optimised_cleanups` covers this for the worked optimisation.

- `failed_batch_leaves_queue` (scenario `failed_batch`; `theories/Pure/Witnesses.v`, same name): a batch whose body fails applies its write and
  skips `RequestFlush` (Core.fs 3303-3310), so the effect it woke stays `Dirty` and queued with `flushOwed` false, until something else flushes. The harness asserts the queue holds
  1, `flushOwed` false, one run and `Dirty`, and an explicit `Flush` runs it. F# trace: `[EvRun 2; EvLog 2 0; EvRun 2; EvLog 2 1]`.
  **The documents.** `docs/content/guide/batch.md:83-84` documents this behaviour: effects queued by the failed batch's writes run at the next flush. Other sources read differently.
  `tests/Ranvier.Tests/Reentrancy.fs:176` has the title "a batch that throws still flushes the writes it made", and its body asserts the write happened and that a later write runs effects.
  `tests/Ranvier.Tests/Batching.fs:203`, "a batch that throws is not left open", asserts a later write flushes. Core.fs 3159 says effects inside a batch "run when the batch ends".
  `owed_settled` holds here: it says no flush is owed, and leaves the queue open.
  **The question for the owner:** the guide documents this; does the owner accept it as the contract? `read_move_leaves_effect_queued` is Confirmed with the same end state and has no such documentation, so the two can be ruled differently.

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

- `disposed_stale_source_read` (scenario of the same name; `theories/Pure/Witnesses.v`, same name), reproduced and closed as spec corrected. A memo disposed
  while stale, then read by an effect, links an edge with no mark (Core.fs 4335): the source is `Dirty`, with no announcement, over a clean idle observer, so the first
  statement of `wake_ok` is false. F# trace: `[EvHostRead 2 (Ok 0); EvDispose 2; EvRun 3; EvLog 3 0; EvHostRead 2 (Ok 0)]`. The harness asserts `Dirty`, one observer, the
  effect's status and an empty queue. Core.fs 4372-4376 documents it: a disposed memo "still answers reads, with the last value it computed" and "stops being woken". No wake-up
  is owed, so `wake_ok`, `wake_told` and `later_mark_forwarded` cover live sources, and `resolve_over_disposed_dirty_source` is the same case through a walk.

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

## Open theorems

Names of theorems that may contain `Admitted`, `admit` or an `Axiom`, one per line as a list item.
`check.sh` compares its findings against this section.

- `resolve_clean_no_move` (`theories/Pure/Check.v`): `check_resolves_over_moved_source` falsifies it on the present engine, and
  `resolve_clean_no_move_falsified` proves that. It states what the fix owes, and `check.sh` fails any other reference to it under
  `theories/` and any other theorem in `Audit.v` resting on it. Use `resolve_clean_unmoved` until the fix.
- `resolve_clean_no_stale_source` (`theories/Pure/Check.v`): `self_marking_wake_lost` falsifies it on the present engine, and
  `resolve_clean_no_stale_source_falsified` proves that. It covers live sources, since a disposed source stays stale without this
  bug: `resolve_over_disposed_dirty_source`. `check.sh` holds it to the same rule.
- `no_lost_wakeups` (`theories/Pure/Runs.v`): `walk_passed_source_marked` falsifies it on the present engine, and `no_lost_wakeups_falsified`
  proves that. Use `no_lost_wakeups_when_nothing_passed`, `no_lost_wakeups_unless_passed` or `no_wake_lost_unless_resolved` until the fix.
- `engine_inv_preserved` (`theories/Pure/Runs.v`): the same counterexample, and `engine_inv_preserved_falsified` proves that. Use
  `engine_settled_preserved` until the fix.
- `marks_survive_run` (`theories/Pure/Runs.v`): `nested_self_recompute_resets_cursor` falsifies it on the present engine, and `marks_survive_run_falsified`
  proves that. Use `marks_survive_body_end` until the fix.
- `run_end_edges_are_reads` (`theories/Pure/Runs.v`): the same Confirmed bug, and `run_end_edges_are_reads_falsified` proves that. Use
  `run_end_edges_are_latest_reads` or `run_end_edges_no_nested_run` until the fix.
- `glitch_free_read` (`theories/Pure/WholeGraph.v`): a disposed memo answers what it stored, as Core.fs 4372-4376 documents, so the body's answer over the current signals is the wrong statement for it. `disposed_stale_source_read`
  is the counterexample, and `glitch_free_read_falsified` proves that. This statement is a specification correction waiting for the owner to drop it or restate it, and no engine fix is owed. Use `glitch_free_read_stored`.
