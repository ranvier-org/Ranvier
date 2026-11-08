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
| `reference_refines_itself` | `Reference/Properties.v` | The reference under a valid order and any fuel refines `obs_full`. |
| `silent_engine_not_refines` | `Reference/Properties.v` | An engine that emits nothing does not refine `obs_settled`. |
| `none_engine_refines` | `Reference/Properties.v` | An engine with no traces refines every observation. |
| `none_engine_not_answers` | `Reference/Properties.v` | That engine fails `engine_answers`. |
| `failing_round_later_rounds_run` | `Reference/Properties.v` | A failed run emits `EvFail`, and later rounds run. |
| `dispose_runs_cleanups_once` | `Reference/Properties.v` | Disposing an effect runs its cleanups once and ends its runs. |
| `disposed_memo_answers_last_value` | `Reference/Properties.v` | A disposed memo answers reads with its last value. |
| `order_varies_by_round` | `Reference/Properties.v` | The effect order can differ between rounds of a history. |
| `redundant_run_settled_equal` | `Reference/Properties.v` | Traces differing by a redundant run have equal `obs_settled`. |
| `redundant_run_full_differs` | `Reference/Properties.v` | The same traces have unequal `obs_full`. |

## Literature

The from-scratch consistency results of the Adapton papers, compared with the reference tier.

**Sources read.**

- Hammer, Khoo Yit Phang, Hicks, Foster, *Adapton: Composable, Demand-Driven Incremental Computation*, technical
  report CS-TR-5027, University of Maryland, July 2013 ([hdl.handle.net/1903/14708](http://hdl.handle.net/1903/14708)).
  It has the title and authors of the PLDI 2014 paper. The PLDI text itself was not accessible, so
  nothing here is claimed about its theorem numbering or wording.
- Hammer, Dunfield, Headley, Labich, Foster, Hicks, Van Horn, *Incremental Computation with Names*,
  OOPSLA 2015, extended version ([arXiv:1503.07792](https://arxiv.org/abs/1503.07792)), Section 4.3
  and Theorem B.13. The theorem's statement was read. Its case-by-case proof was not checked.
- Hammer, Dunfield, *Typed Adapton* ([arXiv:1610.00097](https://arxiv.org/abs/1610.00097)), abstract
  only. It enforces the side conditions of the 2015 paper by types and claims no stronger consistency
  statement.

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

| Difference | Reference tier | Decision |
|---|---|---|
| The soundness theorem is an equivalence: each direction gives a reduction. | `refines` has one direction, with the engine's trace as a premise, so an engine with no traces refines everything: `none_engine_refines`. | Adopted as `engine_answers`, stated separately so that `refines` keeps the brief's form. An engine proof owes both. |
| Both results relate one evaluation to its from-scratch evaluation: a result and a store. | A trace over a history, compared per effect by `obs_settled` or whole by `obs_full`. | Kept. `obs_settled` is the analogue of comparing terminal results. `obs_full` has no analogue, since these papers leave trace structure free. A redundant run is outside what they can state. |
| The final store is compared. | Final signals are compared only through logged reads. Signal writes are not events. | Kept, as the brief defines the observations. Recorded as a concern: an unlogged final signal value is unobserved, and the history has no host-read input. |
| The inner layer cannot write. Writes come from the outer layer between evaluations. | Memos match this (`well_behaved_memo`). Effects write during a flush, and they read those writes. | Kept. The papers give effects no meaning: the outer layer is their closest counterpart, and it has no consistency claim of its own. They do not justify any weaker statement about effects. |
| Demand drives evaluation, and an unread thunk need not be repaired. | A memo's value is defined over current signals, whether or not it is demanded. Only effects are observers. | Kept: the from-scratch value is what `memo_value` already defines. |
| Consistency holds for any prior knowledge `K`, and Theorem B.13 carries a whole-graph invariant through the induction. | `refines` compares traces and names no engine state. | Not applied in this tier: engine state lives below it. The factoring is the suggested shape of the engine invariant: every cached memo value equals `memo_value` over current signals. Disposed memos are the exception below. |
| No disposal. | A disposed memo answers its last value, so it is deliberately not from-scratch consistent. | Kept, as `Core.fs` documents. `rs_frozen` makes `memo_value` the from-scratch value relative to the disposed memos' answers. |
| Name-precision conditions and allocation (`P1`, `W`). | A static node set with fixed identities. | No counterpart. |
| Evaluation order is a source of nondeterminism in patching, and the result is independent of it. | The order of effects is observable. `refines` quantifies over valid round-indexed orders. | Kept: `obs_full` is order-sensitive, and an effect's writes can make the settled values depend on the order. |
| No failures in the calculi. | A failure is an outcome, compared by identity. | No counterpart. |
| Theorem B.13 has a written proof, and the sources read mention no mechanisation. | Proofs are checked by Rocq. | Kept. |

No literature result here is used to weaken a statement.
