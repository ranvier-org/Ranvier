# Ranvier core proofs

Rocq/Iris development over the synchronous reactive core in `src/Ranvier/Core.fs`, pinned at
commit `d701e6c5`.

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`
- Differential check: `bash proofs/differential/run.sh [count] [seed]`
- Documentation: `bash proofs/docs.sh`, which prints where the pages are. Start at the Guarantees page.

All four run from any directory. See `VERSIONS.md` for the toolchain. The audit and the differential check also need the
.NET SDK, since they rebuild `src/Ranvier`. Under WSL they run the Windows `dotnet.exe`.

## The link to the F#

Rocq cannot read F#. The theorems are about the model, and they reach `Core.fs` only through `CORRESPONDENCE.md`. The
differential check is testing evidence for that table, and it is not a proof: agreement says the model and the F# gave one
trace on the programs generated, and says nothing of other programs. Agreement also says nothing of whether the behaviour
is right. A program can reach a Confirmed entry of `BUGS.md` and agree, since the model has that bug too.

`differential/run.sh` generates programs, runs each on real `Signal`, `Memo` and `Effect` objects (`Generate.fsx`) and on
the extracted interpreter (`driver.ml`), and compares the traces and the end states. It exits nonzero on the first
difference, printing the program, both sides and the seed, and also when 5% or more of the programs are skipped, when more
than 1% are excluded, or when the compared programs fall below a coverage floor.

The end states are `observed` in `theories/Extract.v` and `Interpreter.View` in `Generate.fsx`: each node's value, status,
failure, freshness, `unannounced`, runs, disposal, queued flag, and source and observer counts, with the queue length and the
root's error count. They are compared at the program's end and again after a closing that reads every memo and flushes. The
first keeps a stale node visible, and the second shows what it was hiding. **The view reads private fields by name, so
refactoring one of these fields in `Core.fs` means updating the view.** `.github/workflows/proofs-differential.yml`
runs it when `proofs/`, `src/Ranvier/Core.fs` or the files compiled before it change, with the run number as the seed, so a
run tries new programs. A change the generated programs do not distinguish passes, so the check narrows drift and does not
eliminate it. The watchdog covers the F# side only: the workflow's `timeout-minutes` bounds the model driver.

What the comparison leaves out, each reported by `run.sh`:

- **Pure-scope violations.** A pure memo's body creates no node or cleanup, by construction: the F# raises a new exception
  each time, and the model has one failure identity. `run.sh` prints how many draws this drops.
- **Repeated zero divisors.** The same reason applies: a program raising more than one is excluded when it runs, up to a cap
  of 1%.
- **Programs that diverge on both sides.** The model has no result, and the F# passed its budget or ended its process
  by a stack overflow or the watchdog (`fatal`). Any other exit of the F# process fails the check. Neither has a
  complete trace to compare. `self_read_check_overflow` in `BUGS.md` is how a generated program overflows. One side
  diverging alone fails the check, as does a stuck program.

The generated programs have no `createRoot`, no owner API, no teardown of the root, no comparer API wrappers, no async nodes
and no second thread, since the command language has none. `Graph.Flush` appears only in the closing. Writes from bodies keep
to a higher creation index than the body's reads, or sit under a guard that stops changing the value, apart from a template
whose effect cleanup writes its own source under the core's `inCleanup` guard.

After changing the interpreter, copy the generated files: `bash proofs/build.sh`, then
`cp proofs/engine.ml proofs/engine.mli proofs/differential/`. `check.sh` fails until they match. CI compiles the committed
copy, so it needs no Rocq.

## Layout

- `theories/Base/Prelude.v`: identifiers and the freshness order.
- `theories/Base/Syntax.v`: the command language.
- `theories/Reference/Semantics.v`: the meaning of a program and an input history, observations and
  the refinement relation.
- `theories/Reference/Properties.v`: determinism, glitch freedom, refinement, redundancy, examples.
- `theories/Pure/ObserverSet.v`: `ObserverSet`, and the backward re-clamped walk.
- `theories/Pure/SourceList.v`: `SourceList` over both ends of every edge, and the edge invariants.
- `theories/Pure/Witnesses.v`: checked counterexamples and behaviour witnesses.
- `theories/Pure/State.v`: the graph state, and `edge`.
- `theories/Pure/Edges.v`: edge coherence over the source-list operations.
- `theories/Pure/Marking.v`: freshness marks and the walks that spread them.
- `theories/Pure/Check.v`: check resolution, over an invariant held by every engine function.
- `theories/Pure/Engine.v`: the executable interpreter of the command language over the graph state.
- `theories/Pure/Runs.v`: the wake-up statement, the invariant every engine function keeps, and what a memo's run guarantees.
- `theories/Pure/Scheduler.v`: the scheduler's invariant over every engine function, the queue, and flushes.
- `theories/Pure/Ownership.v`: teardown order, each cleanup once, disposal and late registrations.
- `conformance/Harness.fsx`: the graph with its trace, and reflection over the core's private state.
- `conformance/Scenarios.fsx`: the scenarios on real `Signal`, `Memo` and `Effect` objects.
- `theories/Conformance/Expected.v`: their traces, generated by `Scenarios.fsx`.
- `theories/Conformance/Check.v`: the scenarios as programs, each checked against its trace.
- `theories/Extract.v`: the end-state view and the closing, and the extraction of `trace_of`, `observed` and `current_flags` to OCaml.
- `differential/engine.ml`, `differential/engine.mli`: the committed extraction.
- `differential/driver.ml`: reads programs and runs the extracted `observed`.
- `differential/Generate.fsx`: the program generator, the text form, and the interpreter over real objects.
- `differential/run.sh`: the comparison.
- `theories/Audit.v`: one `Print Assumptions` per theorem; `check.sh` reads it.
- `docs.sh`, `docs/guarantees.py`, `docs/ranvier.css`, `docs/ranvier.js`: the pages, the Guarantees overview and their checks.
- `BUGS.md`: defect ledger. `CORRESPONDENCE.md`: model-to-F# map.

## Theorem index

| Theorem | File | Claim |
|---|---|---|
| `reference_trace_deterministic` | `Reference/Properties.v` | Agreeing orders and any two sufficient fuels give one trace. |
| `memo_value_extensional` | `Reference/Properties.v` | With the same disposed memos, a memo's value depends only on the signals its evaluation discovers. |
| `glitch_free_by_definition` | `Reference/Properties.v` | The run holding an `EvLog`, when it writes no signal, has its reads and events computed from one signal snapshot and its cleanups' captured environments, which can hold values from an earlier snapshot. |
| `reference_run_redundant` | `Reference/Properties.v` | Witness: the reference tier has a redundant run when an effect restores a value mid-round. |
| `untracked_value_not_redundant` | `Reference/Properties.v` | A run whose untracked read moved is not redundant. |
| `untracked_branch_not_redundant` | `Reference/Properties.v` | A run whose reads are a subset of the previous run's, selected by an untracked read, is not redundant. |
| `zero_divisor_later_effects_run` | `Reference/Properties.v` | A zero divisor fails its effect, and later effects run. |
| `observe_settled_coarser` | `Reference/Properties.v` | `observe_full` equality implies `observe_settled` equality. |
| `refines_full_implies_settled` | `Reference/Properties.v` | An engine refining `observe_full` refines `observe_settled`. |
| `reference_refines_itself` | `Reference/Properties.v` | The reference under a valid order and any fuel refines `observe_full`, at every batch prefix, with extending traces. |
| `reference_trace_answers` | `Reference/Properties.v` | The reference indexed by its fuel satisfies `engine_answers` under a valid order. |
| `final_batch_engine_not_refines` | `Reference/Properties.v` | An engine answering only a full history does not refine `observe_settled`, though its full-history view matches. |
| `last_run_traces_match_every_prefix` | `Reference/Properties.v` | Traces keeping only each prefix's last run match `observe_settled` at every prefix of a history. |
| `last_run_engine_not_refines` | `Reference/Properties.v` | An engine answering those traces does not refine `observe_settled`: its traces do not extend. |
| `reference_trace_extends` | `Reference/Properties.v` | A history's trace extends each batch prefix's trace, under the same order and fuel. |
| `redundant_failing_cleanup_changes_settled` | `Reference/Properties.v` | Witness: removing a redundant run whose cleanup fails changes `observe_settled`. |
| `logging_cleanup_drop_unobserved` | `Reference/Properties.v` | Witness: `observe_settled` admits dropping a re-run's non-failing logging cleanup. |
| `host_read_sees_earlier_write` | `Reference/Properties.v` | A host read answers at its position in the batch. |
| `host_read_then_dispose_memo` | `Reference/Properties.v` | A memo read and then disposed in one batch keeps the value the read answered. |
| `dropped_write_unobserved_before_read` | `Reference/Properties.v` | Dropping a write to a signal no effect logs leaves `observe_settled` unchanged. |
| `dropped_write_observed_by_host_read` | `Reference/Properties.v` | A later host read makes that dropped write observable under `observe_settled`. |
| `silent_engine_not_refines` | `Reference/Properties.v` | An engine that emits nothing does not refine `observe_settled`. |
| `none_engine_refines` | `Reference/Properties.v` | An engine with no traces refines every observation. |
| `none_engine_not_answers` | `Reference/Properties.v` | That engine fails `engine_answers`. |
| `failing_round_later_rounds_run` | `Reference/Properties.v` | A failed run emits `EvFail`, and later rounds run. |
| `dispose_runs_cleanups_once` | `Reference/Properties.v` | Disposing an effect runs its cleanups once and ends its runs. |
| `disposed_memo_answers_last_value` | `Reference/Properties.v` | A disposed memo answers reads with its last value. |
| `order_varies_by_round` | `Reference/Properties.v` | The effect order can differ between rounds of a history. |
| `redundant_run_settled_equal` | `Reference/Properties.v` | On the cleanup-free `resetting` program, traces differing by a redundant run have equal `observe_settled`. |
| `redundant_run_full_differs` | `Reference/Properties.v` | The same traces have unequal `observe_full`. |
| `observers_add_position` | `Pure/ObserverSet.v` | The handle is the position, the view appends, and `observer_latest` is the handle. |
| `observers_remove_at_swap` | `Pure/ObserverSet.v` | The last observer moves into the hole and the notice names it. Removing the last gives no notice. |
| `observers_remove_mismatch` | `Pure/ObserverSet.v` | A handle holding another observer, or none, gives `None`. |
| `observers_try_add_cases` | `Pure/ObserverSet.v` | At or below `scan_limit`, `TUnknown` is impossible and `TUnlinked` holds exactly when the observer is held. |
| `walk_covers` | `Pure/ObserverSet.v` | An observer held exactly once at every point of a walk is visited, under any mutations. |
| `walk_visits_only_seen` | `Pure/ObserverSet.v` | Each visited observer was present at its visit. |
| `sources_add_positional` | `Pure/SourceList.v` | A read matching the cursor's slot advances the cursor and leaves every edge unchanged. |
| `sources_add_repeat` | `Pure/SourceList.v` | A read of the source just matched leaves the world unchanged. |
| `sources_add_truncates` | `Pure/SourceList.v` | A miss drops every slot from the cursor and the edges they held, then appends. |
| `sources_no_duplicate_edge` | `Pure/SourceList.v` | In a reachable world, a slot holds an edge exactly when it is its source's first slot. |
| `trim_moves_only_others` | `Pure/SourceList.v` | Over `edge_match` and `slots_unique`, a removal moves only another computation's edge. |
| `trim_every_removal` | `Pure/SourceList.v` | `trim_loop` keeps both invariants at every removal, with each observer held at most once and only others' edges moved. |
| `observer_held_once` | `Pure/SourceList.v` | In a reachable world or between two removals of its trim, a present observer is `held_once`: the premise of `walk_covers`. |
| `sources_trim_moves_own_edge` | `Pure/SourceList.v` | Review Focus 4: for A, B, A with two A edges, the later handle is read after the move, and a clear removes every edge. |
| `sources_add_unknown_no_duplicate` | `Pure/SourceList.v` | Review Focus 5: above the limit with a stale `observer_latest`, the answer is `TUnknown` and the source ends with one edge. |
| `unknown_held`, `unknown_unheld` | `Pure/SourceList.v` | Runs reaching that case, with and without an earlier slot. |
| `aba_reachable_trim` | `Pure/SourceList.v` | A, B, A from a run: a trim moves another observer's edge, and its slot follows. |
| `aba_world_premises` | `Pure/SourceList.v` | A concrete world meets the premises of `sources_trim_moves_own_edge`. |
| `snapshot_trim_fails` | `Pure/SourceList.v` | Handles read before the removals fail on that world. |
| `walk_covers_duplicate_observer` | `Pure/Witnesses.v` | An observer held twice can be missed though present throughout. Closed in `BUGS.md`. |
| `walk_visits_moved_twice` | `Pure/Witnesses.v` | A moved observer is visited twice. |
| `walk_visits_added_observer` | `Pure/Witnesses.v` | An observer added during a walk is visited when a later removal moves it below the cursor. Closed in `BUGS.md`. |
| `coherent_begin` | `Pure/Edges.v` | `sources_begin_run` preserves `coherent`. |
| `coherent_add`, `coherent_end`, `coherent_clear` | `Pure/Edges.v` | From a coherent world, each returns `Some`, and the result is coherent. |
| `coherent_empty` | `Pure/Edges.v` | The empty world is coherent. |
| `coherent_moved` | `Pure/Edges.v` | Removing a slot's edge returns a coherent world, of a stated shape with and without a move. A moved observer takes the removed handle, and its slot holds it. |
| `moved_alone_breaks_coherent` | `Pure/Edges.v` | A move to a different handle with no removal breaks coherence. |
| `remove_edge_alone_breaks_coherent` | `Pure/Edges.v` | A removal with no blanking of the removed slot breaks coherence. |
| `edge_iff_both_sides` | `Pure/Edges.v` | In a coherent world, an edge holds exactly when `c` is in `src`'s observers, and exactly when a slot of `c` holds `src` with a handle. |
| `edge_iff_read` | `Pure/Edges.v` | In a well-formed world, an edge holds exactly when `src` is in `c`'s slots. |
| `read_without_edge` | `Pure/Edges.v` | A coherent world where a computation read a source and has no edge to it, so `first_owns` fails. |
| `check_invariant_init`, `check_invariant_run_top`, `check_invariant_update` | `Pure/Check.v` | `check_invariant` holds of a new graph and after every function of the engine, for all programs and fuel. `engine_ok_all` is the induction. |
| `unmoved_short_of_dirty` | `Pure/Check.v` | Under `check_invariant` and `reads_settled`, a memo in `Clean` or `Check` has `moved_since_last_run` false. |
| `resolve_clean_unmoved` | `Pure/Check.v` | `update_if_necessary` taking a memo from `Check` to `Clean` leaves no moved source, under `reads_settled`. It follows from `check_invariant`'s preservation and `unmoved_short_of_dirty`, without using the walk. |
| `resolve_clean_no_move` | `Pure/Check.v` | **Open.** The unqualified statement, false on the present engine: `resolve_clean_no_move_falsified`. |
| `resolve_clean_no_stale_source` | `Pure/Check.v` | **Open.** A memo resolved `Clean` has no live stale source, false on the present engine: `resolve_clean_no_stale_source_falsified`. A disposed source stays stale: `resolve_over_disposed_dirty_source`. |
| `resolve_dirty_stops_walk`, `resolve_walk_step` | `Pure/Check.v` | A node out of `Check` ends the walk with the state unchanged. In `Check`, the walk updates the slot's source and moves to the next slot. |
| `resolve_clears_unannounced` | `Pure/Check.v` | `update_if_necessary` taking a memo from `Check` to `Clean` ends a live memo's `n_unannounced`, on every path. A disposed memo can keep it: `disposed_window_keeps_unannounced`. |
| `resolve_walk_check_clears` | `Pure/Check.v` | A walk ending in `Check` makes `refresh` return the node `Clean` with `n_unannounced` false and no run. |
| `signal_update_noop` | `Pure/Check.v` | `update_if_necessary` on a signal returns the state unchanged. |
| `disposed_memo_no_refresh` | `Pure/Check.v` | A read of a disposed memo keeps every node, and answers the stored value or failure. |
| `check_resolves_over_moved_source`, `resolve_clean_no_move_falsified` | `Pure/Witnesses.v` | A memo resolves to `Clean` over a source that moved after its read. Confirmed. |
| `check_two_reads_over_moved_source` | `Pure/Witnesses.v` | The same skip through a second read of the source. |
| `resolve_clean_no_stale_source_falsified`, `disposed_window_keeps_unannounced` | `Pure/Witnesses.v` | A reader resolves `Clean` over a `Dirty` source. A memo disposed in `RecomputeScoped`'s window ends `Clean` with `n_unannounced`. |
| `check_cutoff_settled`, `self_marking_through_memo_unmoved` | `Pure/Witnesses.v` | `reads_settled` holds on an ordinary resolution, and on the reader of `self_marking_wake_lost`, which goes clean over its source's stored value. |
| `mark_terminates`, `notify_terminates` | `Pure/Marking.v` | Fuel above `mark_measure` gives a result for every mark and walk, on any graph, cycles included. |
| `mark_monotone` | `Pure/Marking.v` | Freshness only rises, for every node, under all five marking operations. |
| `dirty_not_lost` | `Pure/Marking.v` | A `Dirty` node stays `Dirty` under `mark_check`. |
| `mark_preserves_edges` | `Pure/Marking.v` | All five marking operations leave `g_edges` unchanged. Covers `Memo` and `Effect` marks. |
| `notify_walk_unmutated` | `Pure/Marking.v` | Each marking walk is a `walk` with empty mutations, folding its mark over the observers at its start. |
| `notify_stales` | `Pure/Marking.v` | Each walk leaves each observer in `takes_marks` stale, apart from the skipped computation. |
| `mark_forwards_on_clean` | `Pure/Marking.v` | A clean memo goes stale, and each observer in `takes_marks` ends stale. |
| `mark_forwards_on_unannounced` | `Pure/Marking.v` | With the flag set, a stale memo holding an announcement does the same, and spends it. |
| `effect_mark_queues` | `Pure/Marking.v` | An effect outside disposal and cleanups ends queued, with at most one pending entry. |
| `mark_keeps_queue_once` | `Pure/Marking.v` | All five marking operations keep `queue_once`, for every node. |
| `disposed_effect_ignores_marks`, `in_cleanup_ignores_marks` | `Pure/Marking.v` | Such an effect's marks return the state unchanged. |
| `mark_dirty_ends_dirty` | `Pure/Marking.v` | `mark_dirty` leaves a memo or an effect in `takes_marks` `Dirty`, an already-queued effect included. |
| `notify_dirty_ends_dirty` | `Pure/Marking.v` | `notify_dirty` and `notify_dirty_except` leave each observer in `takes_marks` `Dirty`, apart from the skipped computation. |
| `mark_check_keeps_check` | `Pure/Marking.v` | `mark_check` and `notify_check` raise no node to `Dirty`. |
| `memo_mark_dirty_keeps_others_short` | `Pure/Marking.v` | `mark_dirty` of a memo raises no other node to `Dirty`. |
| `clean_memo_keeps_unannounced` | `Pure/Marking.v` | The `Clean` transition starts its walk with `n_unannounced` unchanged. |
| `in_cleanup_observer_stays_clean` | `Pure/Witnesses.v` | A memo's in-cleanup observer, with a coherent edge, stays clean, which `takes_marks` excepts. Closed in `BUGS.md`. |
| `disposed_running_observer_stays_clean` | `Pure/Witnesses.v` | An effect disposed mid-body holds an edge while running, and ignores the mark. Awaiting ruling in `BUGS.md`. |
| `self_marking_cleanup_left_behind` | `Pure/Witnesses.v` | A memo's cleanup marks its reader during the reader's run: the reader ends stale, its effect clean and idle, and a flush changes nothing. Confirmed in `BUGS.md`. |
| `self_marking_body_left_behind` | `Pure/Witnesses.v` | The same end state from a memo writing its own source. Confirmed in `BUGS.md`. |
| `self_marking_through_memo_left_behind` | `Pure/Witnesses.v` | The same end state through a reading memo. Confirmed in `BUGS.md`. |
| `read_move_leaves_effect_queued` | `Pure/Witnesses.v` | A read of the left-behind memo queues its effect and owes no flush. Confirmed in `BUGS.md`. |
| `nested_self_run_ends_clean_and_wrong` | `Pure/Witnesses.v` | A nested self-run trims an edge the outer run read, and the memo ends clean with the outer value. Confirmed in `BUGS.md`. |
| `self_read_check_resolves_itself` | `Pure/Witnesses.v` | A self-reading memo in `Check` has itself as its first slot, and a read of it has no result at fuel 500 or 2000. Confirmed in `BUGS.md`. |
| `self_read_second_resolves_itself` | `Pure/Witnesses.v` | The same with the self-read second, behind a source that holds its value. |
| `mutual_read_check_resolves_itself` | `Pure/Witnesses.v` | The same through two memos reading each other by constant id. |
| `requeue_holds_two_entries` | `Pure/Witnesses.v` | A re-queued effect has two entries in `g_queue` and one in `pending`. |
| `engine_settled_preserved` | `Pure/Runs.v` | Every engine function, at every fuel, keeps `engine_settled`: the check invariant, stack discipline, id bounds, cleanup lists, in-cleanup effects on the stack, and the told record. |
| `engine_settled_init`, `engine_inv_init` | `Pure/Runs.v` | A new graph under the current flags satisfies both invariants. |
| `no_lost_wakeups_unless_passed` | `Pure/Runs.v` | After a program, an observer of a live stale source is covered or in `gh_passed`, the record of a walk ending over a stale source with no announcement. |
| `no_lost_wakeups_when_nothing_passed` | `Pure/Runs.v` | With `gh_passed` empty at the end, `wake_ok` holds. |
| `no_wake_lost_unless_resolved` | `Pure/Runs.v` | After a program, an observer of a live stale source is stale, queued, running or in `gh_over`: the source's flag excuses nothing. |
| `no_lost_wakeups` | `Pure/Runs.v` | **Open.** The unqualified statement, false on the present engine: `no_lost_wakeups_falsified`. |
| `engine_inv_preserved` | `Pure/Runs.v` | **Open.** Every engine function keeps the invariant with `wake_ok`, false on the present engine: `engine_inv_preserved_falsified`. |
| `later_mark_forwarded` | `Pure/Runs.v` | From `wake_ok`, a later `mark_check` or `mark_dirty` of a live memo leaves every observer stale, queued or running. |
| `marks_survive_run` | `Pure/Runs.v` | **Open.** A memo stale at a point in its body is stale at its run's end, holding `n_unannounced`, false on the present engine: `marks_survive_run_falsified`. |
| `marks_survive_body_end` | `Pure/Runs.v` | The tail of a run: a memo stale when its body returns is stale when its run returns, holding `n_unannounced`. |
| `cutoff` | `Pure/Runs.v` | A `CmpEq` run whose stored view holds still adds no mark to any node and nothing to the queue. |
| `moved_marks_except_running` | `Pure/Runs.v` | A moved run leaves every observer that takes marks `Dirty`, apart from the computation current at its start. |
| `notice_skips_current`, `moved_run_skips_current` | `Pure/Runs.v` | That computation stays short of `Dirty` if it was. |
| `run_end_edges_are_reads` | `Pure/Runs.v` | **Open.** After a run, a live computation's source list is `read_list` of all its run's reads, false on the present engine: `run_end_edges_are_reads_falsified`. |
| `run_end_edges_are_latest_reads` | `Pure/Runs.v` | The same over the reads since its latest run start. |
| `run_end_edges_no_nested_run` | `Pure/Runs.v` | The same over the run's reads, when no nested self-run started. |
| `disposed_stale_source_read` | `Pure/Witnesses.v` | A disposed stale memo's reader is uncovered, which `wake_ok`'s live premise excepts. Closed in `BUGS.md`. |
| `walk_passed_source_marked`, `walk_passed_source_cleanup` | `Pure/Witnesses.v` | A walk passes a source, a later source's run marks it, and the effect ends clean and idle over it with no flag. Confirmed in `BUGS.md`. |
| `no_lost_wakeups_falsified`, `engine_inv_preserved_falsified` | `Pure/Witnesses.v` | Both Open statements above are false, from that program. |
| `walk_passed_stays_deaf` | `Pure/Witnesses.v` | After the walk, another write of the source wakes nothing. |
| `marks_survive_run_falsified`, `run_end_edges_are_reads_falsified` | `Pure/Witnesses.v` | Both Open statements are false, from a nested self-run. |
| `skip_removed_fires` | `Pure/Witnesses.v` | With the skip of `NotifyDirtyExcept` removed, the current computation ends `Dirty`. |
| `ordinary_programs_nothing_passed`, `ordinary_stale_edge_covered`, `flagged_stale_edge_covered` | `Conformance/Check.v` | `gh_passed` is empty on the ordinary conformance programs, and `no_lost_wakeups_when_nothing_passed` applies across a live stale edge. |
| `effect_resolves_over_moved_source` | `Pure/Witnesses.v` | An effect reads a source, writes it, reads a memo over it, and never runs again with the new value. Confirmed in `BUGS.md`. |
| `diamond_ends_memo_stale` | `Pure/Witnesses.v` | With no write in any body, a memo's run ends `Dirty` holding its flag. |
| `reader_mark_off_fires` | `Pure/Witnesses.v` | With `MarkReaderCheck` off, `wake_told` fails and `wake_ok` still holds. |
| `unannounced_off_fires` | `Pure/Witnesses.v` | With `n_unannounced` never set, `wake_ok` fails. |
| `sched_ok_all` | `Pure/Scheduler.v` | Every engine function, at every fuel, keeps `sched_inv`, under the premise `sched_ok` lists for it: a memo function needs a memo and, from `refresh` down, an open stale read or a running flush; `execute_effect` takes `n` in progress to none, with `n` an effect during a flush; `run_effect` the same with none in progress; `run_effect_body` also a live effect; `discharge` an open stale read or a running flush; `teardown` and `run_cleanup` an existing owner, with an allowance per cleanup handed to them; `drain` a running flush; `settle_flush` the invariant without its owed clause. Each returns the context fields as it found them. Nodes keep kinds, disposal only rises, a disposed effect's runs stay put, and a host's cleanup events rise by at most its registrations. |
| `sched_inv_init`, `sched_inv_run_top` | `Pure/Scheduler.v` | A new graph satisfies `sched_inv`, and a whole program keeps it. |
| `queued_at_most_once`, `queued_at_most_once_new_graph` | `Pure/Scheduler.v` | Each node has at most one live queue entry, and `queued` is set exactly when it has one or it is the effect mid-`Execute`. `queue_once` follows. |
| `flush_no_nest` | `Pure/Scheduler.v` | `run_flush` with `g_flushing` set returns the state it was given. |
| `owed_settled` | `Pure/Scheduler.v` | A program from no batch, pull or current computation ends with no flush owed. |
| `flush_returns_clean` | `Pure/Scheduler.v` | A flush that returns has an empty queue, and every live effect `Clean` and unqueued, with or without a failing body. `Clean` is a mark. |
| `flush_reruns_dirty` | `Pure/Scheduler.v` | A live effect `Dirty` at its dequeue, or raised to `Dirty` by its walk, has its `EvRun` count rise, or ends disposed. |
| `body_failure_contained`, `cleanup_failure_contained`, `late_cleanup_contained`, `memo_failure_contained`, `comparer_throw_contained` | `Pure/Scheduler.v` | A sub-body ending in `Fail`, or a throwing comparer, leaves the enclosing function returning, with the failure recorded where the F# records it. |
| `memo_scope_violation_returns`, `memo_comparer_replaces_failure` | `Pure/Scheduler.v` | The two other ways a memo's run records a failure: a scope violation, and a throwing comparer over an unchanged failure. |
| `drain_continues`, `teardown_continues` | `Pure/Scheduler.v` | The drain and the teardown go on from the state each step returned. |
| `drain_no_escape` | `Pure/Scheduler.v` | A flush that returns leaves `g_flushing` false and an empty live queue. `flush_survives_body`, `flush_survives_cleanup`, `flush_survives_child_release`, `flush_survives_memo_run` and `flush_survives_comparer` run each failure with an effect behind it. |
| `dispose_reverse_order` | `Pure/Ownership.v` | `dispose_scope` is one merged list of the detached children and cleanups, each once, with acquisitions strictly falling. |
| `owners_in_order` | `Pure/Ownership.v` | After a program on a new graph, every owner's lists are in acquisition order. |
| `cleanup_once`, `cleanup_once_new_graph` | `Pure/Ownership.v` | A host's `EvCleanup` events, with the cleanups its owners hold, are at most its registrations; and each list entry takes one step of `teardown`, or one `run_cleanup` on the late path. Structural, per entry, since a registration carries no id. |
| `disposed_no_edges`, `disposed_no_edges_new_graph` | `Pure/Ownership.v` | A disposed node off the running stack has no slots, no edge and no observer position. |
| `disposed_effect_never_runs` | `Pure/Ownership.v` | From a disposed effect, a program, a flush, a `drain` or its dequeue adds no run and no `EvRun`, and the dequeue changes only `queued`. `queued_effect_disposed_skipped` has a disposal mid-flush. |
| `disposed_memo_answers` | `Pure/Ownership.v` | A read of a disposed memo keeps nodes, trace and queue, and answers what it stored. `disposed_unread_answers_default` has it answer a default. |
| `late_registration_immediate`, `late_attach_disposed` | `Pure/Ownership.v` | On a disposed scope, a cleanup runs before the body goes on, and a node is disposed at once. |
| `stale_link_detach_noop` | `Pure/Ownership.v` | `unlink` of a link from an earlier generation returns the state it was given, which pins the model's check and no reachable behaviour. `stale_link_reached` reaches it. |
| `teardown_runs_in_reverse`, `late_registrations_run_at_once` | `Pure/Ownership.v` | The two orders on a program, against the F# scenarios of the same programs. |
| `failed_batch_leaves_queue` | `Pure/Witnesses.v` | A batch whose body fails leaves its effect queued and `Dirty` with no flush owed. Awaiting ruling in `BUGS.md`. |

`Pure/Marking.v` guards the forwarding half of #302 only: a memo holding an announcement tells its observers. Never setting `unannounced` falls to `wake_ok` and `marks_survive_body_end`, and removing `MarkReaderCheck` to `wake_told`: `unannounced_off_fires`, `reader_mark_off_fires`. `wake_ok` holds with `MarkReaderCheck` removed, since the source holds its flag.

## Literature

The from-scratch consistency results of the Adapton papers, compared with the reference tier. Each
claim below is scoped to the parts listed here.

**Sources read.**

- Hammer, Khoo Yit Phang, Hicks, Foster, *Adapton: Composable, Demand-Driven Incremental Computation*,
  technical report CS-TR-5027, University of Maryland, July 2013
  ([hdl.handle.net/1903/14708](http://hdl.handle.net/1903/14708)). Read: the abstract, the
  introduction's summary of the formalism, the meta-theory statements of Section 3 (Theorems 3.1 to
  3.3), the rule prose at the end of Section 4.1, Section 4.2's statements (Theorems 4.1 to 4.3), the
  opening of Section 5 and the statement of Theorem 5.2, and the conclusion's consistency sentence. Not
  read: the rule figures in detail, and any proofs.
- Hammer, Dunfield, Headley, Labich, Foster, Hicks, Van Horn, *Incremental Computation with Names*,
  OOPSLA 2015, extended version ([arXiv:1503.07792](https://arxiv.org/abs/1503.07792)). Read from the PDF
  text: the introduction's statement of the property, Section 4.2's description of the reference system,
  Section 4.3, Appendix B's account of the invariant, and the statement of Theorem B.13. Read only
  through a summarising fetch of the ar5iv rendering: Section 2.1 on the inner layer, Section 4.2.3 on
  legal modifications and the name check, and Definitions B.9 and B.10. Not checked: the proof of
  Theorem B.13.
- Hammer, Dunfield, Economou, Narasimhamurthy, *Refinement types for precisely named cache locations*
  ([arXiv:1610.00097](https://arxiv.org/abs/1610.00097), title and authors as on the arXiv page; version 1
  was titled *Typed Adapton* and listed Hammer and Dunfield). Read: the abstract of version 1 only, so
  nothing here is claimed about its results.

**Not accessed.** The PLDI 2014 paper. The ACM page returned 403, and the copy at
`people.mpi-sws.org/~hammer/adapton-pldi2014.pdf` returned 404. CS-TR-5027 has the same title and authors.
Nothing here is claimed about the PLDI text's theorem numbering or wording.

**Statements.**

- CS-TR-5027 Theorem 4.3 (Soundness): for well-formed prior knowledge `K` and a well-typed store, an
  incremental reduction `K; S1 |- e ⇓ S2; T` holds if and only if a basic reduction
  `S1 |- e ⇓ S2; trm(T)` holds. The report calls this extensional consistency: the final store and the
  terminal expression agree, and the trace structure is free. Theorems 3.2 and 3.3 make the inner layer
  read-only and deterministic.
- arXiv:1503.07792 Theorem B.13: given an incremental derivation `H1 |- e ⇓ H2; t` that is satisfactory,
  a pointer set `P1` that covers its reads and is apart from its writes, and a from-scratch consistent
  `H1`, there is a non-incremental derivation over the restricted graphs with the same `t`, the
  restricted graphs grow, `P2 = P1 ∪ dom(W)`, and `H2` is from-scratch consistent.

**Differences, and what was adopted.**

| Source | Difference | Reference tier | Decision |
|---|---|---|---|
| CS-TR-5027 Thm 4.3 | The soundness theorem is an equivalence: each direction gives a reduction. | `refines` has one direction, so an engine with no traces refines everything: `none_engine_refines`. | Adopted as `engine_answers`, stated separately so that `refines` keeps its form. **An engine proof owes both `refines` and `engine_answers`.** |
| Thm 4.3, Thm B.13 | Both relate one evaluation to its from-scratch evaluation: a result and a store. | A trace over a history, compared per effect by `observe_settled` or whole by `observe_full`, at every batch prefix, with the engine's traces extending across prefixes. | Kept. `observe_settled` is the analogue of comparing terminal results. `observe_full` has no analogue in the statements read, which leave trace structure free. |
| Thm 4.3 | The final store is compared. | Signal writes are not events, so a signal's value is compared only through logged reads and host reads. | Partly adopted: `RRead` makes a signal's value observable at any batch position, in both observations. A value the history never reads stays unobserved. |
| Thm 3.2, Section 2.1 | The inner layer cannot write. Writes come from the outer layer between evaluations. | Memos match this (`well_behaved_memo`). Effects write during a flush, and they read those writes. | Kept. The statements read give effects no meaning, and they do not justify any weaker statement about effects. |
| CS-TR-5027 abstract, Section 5 | Demand drives evaluation, and patching skips what is not relevant. | A memo's value is defined over current signals, whether or not it is demanded. Observers are effects and host reads. | Kept: the from-scratch value is what `memo_value` already defines. |
| Thm 4.3, Thm B.13 | Consistency holds for any well-formed prior knowledge `K`, and Theorem B.13 carries a whole-graph invariant through its induction. | `refines` compares traces and names no engine state. | Not applied in this tier: engine state lives below it. The factoring is a suggested shape for the engine invariant: every cached memo value equals `memo_value` over current signals, with disposed memos as the exception below. |
| Statements read | No disposal in them. | A disposed memo answers its last value, so it is deliberately not from-scratch consistent. | Kept, as `Core.fs` documents. `rs_frozen` makes `memo_value` the from-scratch value relative to the disposed memos' answers. |
| Thm B.13, Section 4.2.3 | Name checks and allocation (`P1`, `W`). | A static node set with fixed identities. | No counterpart. |
| CS-TR-5027 Section 5 | The order of patching steps is a source of nondeterminism in the declarative semantics. | The order of effects is observable. `refines` takes one valid round-indexed order per history. | Kept: `observe_full` is order-sensitive, and an effect's writes can make settled values depend on the order. |
| Statements read | No failures in them. | A failure is an outcome, compared by identity. | No counterpart. |
| Thm B.13 | A written proof, and the parts read mention no mechanisation. | Proofs are checked by Rocq. | Kept. |

No literature result here is used to weaken a statement.
