From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run.

Open Scope nat_scope.

(**md # Heap scheduler specifications

This module proves the HeapLang specifications for `request_flush_code`, `settle_flush_code`,
`run_flush_code`, and `drain_code`, and lifts the pure scheduler invariants (`sched_inv` and
`drain_no_escape` from `Pure/Scheduler.v`) to the heap tier. *)

Section heap_scheduler.
Context `{!heapGS Σ, !graphG Σ}.

(** ** Flush and drain specifications *)

(**md
**`request_flush_code` refines `request_flush` (`Graph.RequestFlush`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem request_flush_spec γ g (s : State.state) (fuel : nat) (s' : State.state) :
  request_flush fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    request_flush_code g
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_request_flush _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`settle_flush_code` refines `settle_flush` (`Graph.SettleFlush`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem settle_flush_spec γ g (s : State.state) (fuel : nat) (s' : State.state) :
  settle_flush fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    settle_flush_code g
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_settle_flush _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`run_flush_code` refines `run_flush` (`Graph.RunFlush`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem run_flush_spec γ g (s : State.state) (fuel : nat) (s' : State.state) :
  run_flush fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    run_flush_code g
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_run_flush _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`drain_code` refines `drain` (the queue drain loop of `Graph.RunFlush`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem drain_spec γ g (s : State.state) (fuel : nat) (s' : State.state) :
  drain fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    drain_code g
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_drain _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**A heap flush started outside a flush returns with `g_flushing` false, an empty live queue, and head `0`.**

Combines `run_flush_spec` with `drain_no_escape` from `Pure/Scheduler.v`.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the flush, with `g_flushing s = false`.
- [fuel:] the pure step budget.
- [s':] the state after the flush.
*)
Corollary heap_flush_returns_clean γ g (s : State.state) (fuel : nat) (s' : State.state) :
  g_flushing s = false ->
  run_flush fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    run_flush_code g
  {{{ RET #(); is_graph γ g s' ∗ ⌜g_flushing s' = false /\ pending s' = [] /\ g_queue_head s' = 0⌝ }}}.
Proof.
  intros Hfl Hrun. iIntros (Φ) "Hg HΦ".
  wp_apply (run_flush_spec _ _ s fuel s' Hrun with "Hg"). iIntros "Hg".
  iApply "HΦ". iFrame "Hg". iPureIntro.
  by eapply drain_no_escape.
Qed.

(**md
**A top-level heap run preserves the scheduler invariant `sched_inv None`.**

In the resulting graph state `s'`, each node has at most one pending queue entry, its `n_queued` flag
agrees with membership in `pending s'`, every live stale effect is queued, and all owners satisfy `acq_ok`.

- [c:] the top-level command.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the returned outcome.
*)
Corollary heap_sched_inv_top (c : cmd) (fuel : nat) (s' : State.state) (r : outcome) :
  run_top fuel c (init current_flags) = Some (s', r) ->
  {{{ True }}}
    run_top_code (compile_cmd c)
  {{{ γ g, RET outcome_val r; is_graph γ g s' ∗ ⌜sched_inv None s'⌝ }}}.
Proof.
  intros Hrun. iIntros (Φ) "_ HΦ".
  wp_apply (run_top_spec c fuel s' r Hrun); [done|]. iIntros (γ g) "Hg".
  iApply ("HΦ" $! γ g). iFrame "Hg". iPureIntro.
  exact (sched_inv_run_top fuel c (init current_flags) s' r (sched_inv_init current_flags) Hrun).
Qed.

End heap_scheduler.
