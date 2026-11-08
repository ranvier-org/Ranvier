# Ranvier core proofs

Rocq/Iris development over the synchronous reactive core in `src/Ranvier/Core.fs`, pinned at
commit `d701e6c5`.

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`

Both run from any directory. See `VERSIONS.md` for the toolchain.

## Layout

- `theories/Base/Prelude.v`: identifiers and the freshness order.
- `theories/Base/Syntax.v`: the command language.
- `theories/Reference/Semantics.v`: the meaning of a program and an input history, observations and
  the refinement relation.
- `theories/Reference/Properties.v`: determinism, glitch freedom, refinement, redundancy, examples.
- `theories/Pure/ObserverSet.v`: `ObserverSet`, and the backward re-clamped walk.
- `theories/Pure/SourceList.v`: `SourceList` over both ends of every edge, and the edge invariants.
- `theories/Pure/Witnesses.v`: checked counterexamples and behaviour witnesses.
- `theories/Audit.v`: one `Print Assumptions` per theorem; `check.sh` reads it.
- `BUGS.md`: defect ledger. `CORRESPONDENCE.md`: model-to-F# map.

## Theorem index

| Theorem | File | Claim |
|---|---|---|
| `rtrace_deterministic` | `Reference/Properties.v` | Agreeing orders and any two sufficient fuels give one trace. |
| `memo_value_extensional` | `Reference/Properties.v` | A memo's value depends only on the signals its evaluation discovers. |
| `glitch_free_by_definition` | `Reference/Properties.v` | Each `EvLog` belongs to one run. A run that writes no signal has its reads and events computed from one signal snapshot and its cleanups' captured environments, which can hold values from an earlier snapshot. |
| `reference_run_redundant` | `Reference/Properties.v` | Witness: the reference tier has a redundant run when an effect restores a value mid-round. |
| `untracked_value_not_redundant` | `Reference/Properties.v` | A run whose untracked read moved is not redundant. |
| `untracked_branch_not_redundant` | `Reference/Properties.v` | A run whose reads are a subset of the previous run's, selected by an untracked read, is not redundant. |
| `zero_divisor_later_effects_run` | `Reference/Properties.v` | A zero divisor fails its effect, and later effects run. |
| `obs_settled_coarser` | `Reference/Properties.v` | `obs_full` equality implies `obs_settled` equality. |
| `refines_full_implies_settled` | `Reference/Properties.v` | An engine refining `obs_full` refines `obs_settled`. |
| `reference_refines_itself` | `Reference/Properties.v` | The reference under a valid order and any fuel refines `obs_full`, at every batch prefix, with extending traces. |
| `rtrace_answers` | `Reference/Properties.v` | The reference indexed by its fuel satisfies `engine_answers` under a valid order. |
| `final_batch_engine_not_refines` | `Reference/Properties.v` | An engine answering only a full history does not refine `obs_settled`, though its full-history view matches. |
| `last_run_traces_match_every_prefix` | `Reference/Properties.v` | Traces keeping only each prefix's last run match `obs_settled` at every prefix of a history. |
| `last_run_engine_not_refines` | `Reference/Properties.v` | An engine answering those traces does not refine `obs_settled`: its traces do not extend. |
| `rtrace_extends` | `Reference/Properties.v` | A history's trace extends each batch prefix's trace, under the same order and fuel. |
| `redundant_failing_cleanup_changes_settled` | `Reference/Properties.v` | Witness: removing a redundant run whose cleanup fails changes `obs_settled`. |
| `logging_cleanup_drop_unobserved` | `Reference/Properties.v` | Witness: `obs_settled` admits dropping a re-run's non-failing logging cleanup. |
| `host_read_sees_earlier_write` | `Reference/Properties.v` | A host read answers at its position in the batch. |
| `host_read_then_dispose_memo` | `Reference/Properties.v` | A memo read and then disposed in one batch keeps the value the read answered. |
| `dropped_write_unobserved_before_read` | `Reference/Properties.v` | Dropping a write to a signal no effect logs leaves `obs_settled` unchanged. |
| `dropped_write_observed_by_host_read` | `Reference/Properties.v` | A later host read makes that dropped write observable under `obs_settled`. |
| `silent_engine_not_refines` | `Reference/Properties.v` | An engine that emits nothing does not refine `obs_settled`. |
| `none_engine_refines` | `Reference/Properties.v` | An engine with no traces refines every observation. |
| `none_engine_not_answers` | `Reference/Properties.v` | That engine fails `engine_answers`. |
| `failing_round_later_rounds_run` | `Reference/Properties.v` | A failed run emits `EvFail`, and later rounds run. |
| `dispose_runs_cleanups_once` | `Reference/Properties.v` | Disposing an effect runs its cleanups once and ends its runs. |
| `disposed_memo_answers_last_value` | `Reference/Properties.v` | A disposed memo answers reads with its last value. |
| `order_varies_by_round` | `Reference/Properties.v` | The effect order can differ between rounds of a history. |
| `redundant_run_settled_equal` | `Reference/Properties.v` | On the cleanup-free `resetting` program, traces differing by a redundant run have equal `obs_settled`. |
| `redundant_run_full_differs` | `Reference/Properties.v` | The same traces have unequal `obs_full`. |
| `obs_add_position` | `Pure/ObserverSet.v` | The handle is the position, the view appends, and `olatest` is the handle. |
| `obs_remove_at_swap` | `Pure/ObserverSet.v` | The last observer moves into the hole and the notice names it. Removing the last gives no notice. |
| `obs_remove_mismatch` | `Pure/ObserverSet.v` | A handle holding another observer, or none, gives `None`. |
| `obs_try_add_cases` | `Pure/ObserverSet.v` | At or below `scan_limit`, `TUnknown` is impossible and `TUnlinked` holds exactly when the observer is held. |
| `walk_covers` | `Pure/ObserverSet.v` | An observer held exactly once at every point of a walk is visited, under any mutations. |
| `walk_visits_only_seen` | `Pure/ObserverSet.v` | Each visited observer was present at its visit. |
| `sl_add_positional` | `Pure/SourceList.v` | A read matching the cursor's slot advances the cursor and leaves every edge unchanged. |
| `sl_add_repeat` | `Pure/SourceList.v` | A read of the source just matched leaves the world unchanged. |
| `sl_add_truncates` | `Pure/SourceList.v` | A miss drops every slot from the cursor and the edges they held, then appends. |
| `sl_no_duplicate_edge` | `Pure/SourceList.v` | In a reachable world, a slot holds an edge exactly when it is its source's first slot. |
| `observer_held_once` | `Pure/SourceList.v` | In a reachable world, an observer occupies at most one position of a source's observers. |
| `trim_moves_only_others` | `Pure/SourceList.v` | With one edge per source, a removal moves only another computation's edge. |
| `sl_trim_moves_own_edge` | `Pure/SourceList.v` | Review Focus 4: for A, B, A with two A edges, the later handle is read after the move, and a clear removes every edge. |
| `sl_add_unknown_no_duplicate` | `Pure/SourceList.v` | Review Focus 5: above the limit with a stale `olatest`, the answer is `TUnknown` and the source ends with one edge. |
| `unknown_held`, `unknown_unheld` | `Pure/SourceList.v` | Runs reaching that case, with and without an earlier slot. |
| `aba_reachable_trim` | `Pure/SourceList.v` | A, B, A from a run: a trim moves another observer's edge, and its slot follows. |
| `aba_world_premises` | `Pure/SourceList.v` | A concrete world meets the premises of `sl_trim_moves_own_edge`. |
| `snapshot_trim_fails` | `Pure/SourceList.v` | Handles read before the removals fail on that world. |
| `walk_covers_duplicate_observer` | `Pure/Witnesses.v` | An observer held twice can be missed though present throughout. |
| `walk_visits_moved_twice` | `Pure/Witnesses.v` | A moved observer is visited twice. |
| `walk_visits_added_observer` | `Pure/Witnesses.v` | An observer added during a walk is visited when a later removal moves it below the cursor. |

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
| Thm 4.3, Thm B.13 | Both relate one evaluation to its from-scratch evaluation: a result and a store. | A trace over a history, compared per effect by `obs_settled` or whole by `obs_full`, at every batch prefix, with the engine's traces extending across prefixes. | Kept. `obs_settled` is the analogue of comparing terminal results. `obs_full` has no analogue in the statements read, which leave trace structure free. |
| Thm 4.3 | The final store is compared. | Signal writes are not events, so a signal's value is compared only through logged reads and host reads. | Partly adopted: `RRead` makes a signal's value observable at any batch position, in both observations. A value the history never reads stays unobserved. |
| Thm 3.2, Section 2.1 | The inner layer cannot write. Writes come from the outer layer between evaluations. | Memos match this (`well_behaved_memo`). Effects write during a flush, and they read those writes. | Kept. The statements read give effects no meaning, and they do not justify any weaker statement about effects. |
| CS-TR-5027 abstract, Section 5 | Demand drives evaluation, and patching skips what is not relevant. | A memo's value is defined over current signals, whether or not it is demanded. Observers are effects and host reads. | Kept: the from-scratch value is what `memo_value` already defines. |
| Thm 4.3, Thm B.13 | Consistency holds for any well-formed prior knowledge `K`, and Theorem B.13 carries a whole-graph invariant through its induction. | `refines` compares traces and names no engine state. | Not applied in this tier: engine state lives below it. The factoring is a suggested shape for the engine invariant: every cached memo value equals `memo_value` over current signals, with disposed memos as the exception below. |
| Statements read | No disposal in them. | A disposed memo answers its last value, so it is deliberately not from-scratch consistent. | Kept, as `Core.fs` documents. `rs_frozen` makes `memo_value` the from-scratch value relative to the disposed memos' answers. |
| Thm B.13, Section 4.2.3 | Name checks and allocation (`P1`, `W`). | A static node set with fixed identities. | No counterpart. |
| CS-TR-5027 Section 5 | The order of patching steps is a source of nondeterminism in the declarative semantics. | The order of effects is observable. `refines` takes one valid round-indexed order per history. | Kept: `obs_full` is order-sensitive, and an effect's writes can make settled values depend on the order. |
| Statements read | No failures in them. | A failure is an outcome, compared by identity. | No counterpart. |
| Thm B.13 | A written proof, and the parts read mention no mechanisation. | Proofs are checked by Rocq. | Kept. |

No literature result here is used to weaken a statement.
