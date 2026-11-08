# Defect ledger

Only Confirmed entries count as bugs. Where `src/Ranvier/Core.fs` documents a behaviour as
intended, the entry closes as "spec corrected".

## Suspected

- `disposed_unread_memo_value` (`theories/Reference/Properties.v`): which value a disposed memo keeps.
  The reference keeps the memo's value over the signals at the start of the disposal's batch, a value
  that depends on no engine's demand. From reading Core.fs, not from running it: a memo starts `Dirty` and
  `Uninitialized` with a default value (4063-4065), `Create` does not compute it (4144-4147), the read
  paths do not refresh a disposed memo (4323, 4335), and `Value` then answers the stored value
  (4539-4548), as the doc at 4372 states. So in `unread_memo` F# would answer the type's default where
  the reference answers 5. The two also differ when something pulls the memo earlier in the batch: a
  cleanup, or a host read (`host_read_then_dispose_memo`, where the reference answers 6 and then 1, and F#
  would answer 6 both times). The question for the owner: does the reference keep this engine-independent
  value, or does it model demand as Core.fs has it? Modelling demand would put one engine's laziness in
  the tier every engine proves against. Task 13 runs it against the F#.

## Modelled

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
- `last_run_traces_match_every_prefix` (`theories/Reference/Properties.v`): `refines` compares each batch
  prefix with the reference and relates no two prefixes' engine traces. For `one_read` and
  `[[RWrite 0 1]; [RWrite 0 2]]`, the traces `[R9;L0]`, `[R9;L1]`, `[R9;L2]` match `obs_settled` at each
  prefix, though no trace extends another. It rejects an engine that is silent at a prefix
  (`final_batch_engine_not_refines`), and not one that recomputes a shortened trace for each prefix. The
  question for the owner: does `refines` also require each prefix's engine trace to extend the previous
  one? Only this one history is checked; no such engine is proved to refine in general.

## Closed

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
