From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler Ownership.
From iris.heap_lang Require Import adequacy lang proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run Scheduler Ownership.

Open Scope nat_scope.

(** * Closed-program adequacy for the HeapLang reactive core

`heap_refines_pure` closes the Iris ghost state (`ranvierΣ`) via `heap_adequacy`, proving that any
operational execution of `run_top_code (compile_cmd c)` from `init_heap` that terminates in a value
`v` produces `outcome_val r` whenever the pure interpreter `run_top fuel c (init current_flags)`
returns `Some (s', r)`. `heap_no_lost_wakeups` combines this refinement with `no_lost_wakeups` and
`engine_inv_init` from `Pure/Runs.v` to guarantee `wake_ok s'` at the final state. *)

Definition ranvierΣ : gFunctors := #[heapΣ; graphΣ].

(** ** Closed-program refinement and no lost wake-ups *)

(**md
**A terminating HeapLang run of a compiled command produces the outcome of the pure interpreter.**

Closes `run_top_spec` over `ranvierΣ` via `heap_adequacy`, with no Iris resources in the statement.

- [c:] the command executed from a fresh graph.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the pure outcome of the run.
- [σ':] the final HeapLang state.
- [v:] the final HeapLang value.
*)
Theorem heap_refines_pure (c : cmd) (fuel : nat) (s' : State.state) (r : outcome) (σ' : heap_lang.state) (v : val) :
  run_top fuel c (init current_flags) = Some (s', r) ->
  rtc erased_step ([run_top_code (compile_cmd c)], init_heap) ([of_val v], σ') ->
  v = outcome_val r.
Proof.
  intros Hrun Hsteps.
  cut (adequate NotStuck (run_top_code (compile_cmd c)) init_heap (fun v0 _ => v0 = outcome_val r)).
  - intros Had. by eapply (adequate_result _ _ _ _ Had).
  - apply (heap_adequacy ranvierΣ NotStuck (run_top_code (compile_cmd c)) init_heap (fun v0 => v0 = outcome_val r)).
    iIntros (Hinv) "_".
    wp_apply (run_top_spec c fuel s' r Hrun); [done|].
    iIntros (γ g) "_". done.
Qed.

(**md
**Every terminating HeapLang run of a compiled command produces the pure outcome and leaves `wake_ok` holding.**

Combines `heap_refines_pure` with `no_lost_wakeups` and `engine_inv_init` from `Pure/Runs.v`: in the
final state `s'`, every observer with an edge from a live, stale source is itself stale, queued or
running, or the source holds its announcement flag.

- [c:] the command executed from a fresh graph.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the pure outcome of the run.
- [σ':] the final HeapLang state.
- [v:] the final HeapLang value.
*)
Corollary heap_no_lost_wakeups (c : cmd) (fuel : nat) (s' : State.state) (r : outcome) (σ' : heap_lang.state) (v : val) :
  run_top fuel c (init current_flags) = Some (s', r) ->
  rtc erased_step ([run_top_code (compile_cmd c)], init_heap) ([of_val v], σ') ->
  v = outcome_val r /\ wake_ok s'.
Proof.
  intros Hrun Hsteps. split.
  - eapply (heap_refines_pure c fuel s' r σ' v Hrun Hsteps).
  - exact (no_lost_wakeups fuel c (init current_flags) s' r engine_inv_init Hrun).
Qed.
