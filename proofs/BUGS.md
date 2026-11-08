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

## Modelled

- `walk_covers_duplicate_observer` (`theories/Pure/Witnesses.v`): an observer held at two positions can be
  missed by a walk though present at every point. `walk_covers` takes `held_once` at every point as its
  named premise. `ISource.AddObserver` documents "an observer the caller holds no edge for" (Core.fs 65),
  and `ObserverSet` documents each observer as held at most once (Core.fs 190). The callers that have to
  keep this are `SourceList.Append` (Core.fs 744, 769), `Bindings.fs:51`, `Combinators.fs:686`,
  `Combinators.fs:944`, `Projections.fs:676` and `ValueReaders.fs:100`. For `SourceList`,
  `observer_held_once` proves the premise. The other five are argued in `CORRESPONDENCE.md`. Task 7 runs
  the witness on the F#, as a `Ranvier.Tests` case with a stub `IComputation` whose `MarkDirty` mutates the
  set (`Types.fs:654` makes both visible).
- `in_cleanup_observer_stays_clean` (`theories/Pure/Witnesses.v`): a clean memo's observer that is an effect
  in its cleanups stays clean, so "every member of `os` ends stale" as the plan states it is false.
  `mark_forwards_on_clean` and `mark_forwards_on_unannounced` conclude it for each observer in `takes_marks`.
  Reachable: `Effect.Run` keeps its edges through `Discharge` (Core.fs 4813-4825). Core.fs 4651-4671 documents
  the suppression as intended and not lossy, since the body runs next and reads the written value, so the entry
  is expected to close as "spec corrected". Task 7 runs the witness against the F# in `Scenarios.fsx`: an effect
  reads a memo over a signal, and its cleanup writes that signal.
- `disposed_running_observer_stays_clean` (`theories/Pure/Witnesses.v`): a disposed effect can hold an edge and
  ignore the mark, so `takes_marks` excepts it too. An effect that disposes itself mid-body keeps running:
  `Track` (3420) and `AddSource` (4737) have no disposed test, so a later read links an edge, and the `finally` at
  4854-4855 clears it. This matches the spec's "once its run has ended". The case is covered by the running
  disjunct of "stale, queued or running", and not by `takes_marks`. Task 7 runs the witness against the F# in
  `Scenarios.fsx`: an effect disposes itself, reads a memo over a signal, and writes that signal.

## Confirmed

## Awaiting ruling

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
