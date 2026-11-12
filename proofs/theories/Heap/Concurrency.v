From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Engine Scheduler Concurrency.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run Scheduler.

Open Scope nat_scope.

(** * Heap concurrency, thread affinity, and inbox contracts

This module lifts the concurrency, `Serialised` reentrant holder, inbox handover, and `PendingDispatch`
contracts of `Pure/Concurrency.v` to HeapLang separation logic:
1. `guarded_enter_spec`: `Guarded` thread-affinity gate (`AssertOnGraphThread` / `Enter`).
2. `serialised_acquire_spec` and `serialised_release_spec`: `Serialised` reentrant holder lock (`CAS` on
   `holder`, context check, and `Release` with non-empty inbox drain re-post).
3. `post_queued_spec`: cross-thread inbox enqueue (`PostQueued`) and drain wake post.
4. `pending_dispatch_claim_spec` and `pending_dispatch_close_spec`: atomic `CAS` claim (`TryClaim`) and
   cancellation/abandon (`TryClose`) on `PendingDispatch`. *)

(** ** HeapLang code for concurrency, holder lock, inbox, and `PendingDispatch` *)

(** Layout of a 6-cell concurrency control block `cs`:
    - `cs +ₗ 0`: affinity (`0` = `Guarded`, `1` = `Unchecked`, `2` = `Serialised`)
    - `cs +ₗ 1`: `ownerThread`
    - `cs +ₗ 2`: `context`
    - `cs +ₗ 3`: `holder` (`0` when free, `me > 0` when held)
    - `cs +ₗ 4`: `inbox` (HeapLang list of work item ids)
    - `cs +ₗ 5`: `pumpPosted` (boolean) *)

Definition affinity_val (aff : thread_affinity) : val :=
  match aff with
  | AffGuarded => #0
  | AffUnchecked => #1
  | AffSerialised => #2
  end.

Definition guarded_enter_code : val :=
  λ: "cs" "me",
    let: "owner" := !("cs" +ₗ #1) in
    if: "me" = "owner" then SOME #false
    else NONE.

Definition serialised_enter_code : val :=
  λ: "cs" "me" "ctx",
    let: "h" := !("cs" +ₗ #3) in
    if: "h" = "me" then SOME #false
    else
      let: "c" := !("cs" +ₗ #2) in
      if: "ctx" = "c" then
        if: CAS ("cs" +ₗ #3) #0 "me" then SOME #true
        else NONE
      else NONE.

Definition serialised_release_code : val :=
  λ: "cs",
    ("cs" +ₗ #3) <- #0;;
    let: "inb" := !("cs" +ₗ #4) in
    match: "inb" with
      NONE => #()
    | SOME <> => ("cs" +ₗ #5) <- #true
    end.

Definition post_queued_code : val :=
  λ: "cs" "w",
    let: "inb" := !("cs" +ₗ #4) in
    ("cs" +ₗ #4) <- vlist_snoc_code "inb" "w";;
    ("cs" +ₗ #5) <- #true.

Definition pd_try_claim_code : val :=
  λ: "pd",
    CAS "pd" #0 #1.

Definition pd_try_close_code : val :=
  λ: "pd",
    CAS "pd" #0 #2.

Section heap_concurrency.
Context `{!heapGS Σ, !graphG Σ}.

Definition is_concurrency_state (cs_loc : loc) (cs : concurrency_state) : iProp Σ :=
  cs_loc ↦ affinity_val (cs_affinity cs) ∗
  (cs_loc +ₗ 1) ↦ #(Z.of_nat (cs_owner_thread cs)) ∗
  (cs_loc +ₗ 2) ↦ #(Z.of_nat (cs_context cs)) ∗
  (cs_loc +ₗ 3) ↦ #(Z.of_nat (cs_holder cs)) ∗
  (cs_loc +ₗ 4) ↦ list_val (cs_inbox cs) ∗
  (cs_loc +ₗ 5) ↦ #(cs_pump_posted cs).

Definition enter_res_val (res : option (concurrency_state * bool)) : val :=
  match res with
  | None => NONEV
  | Some (_, b) => SOMEV #b
  end.

(**md
**`guarded_enter_code` checks `me = cs_owner_thread` without mutating `is_concurrency_state`, returning `SOME #false` on the owner thread and `NONE` off-thread.**

- [cs_loc:] the concurrency control block location.
- [me:] the calling thread id.
- [ctx:] the calling context id.
- [cs:] the concurrency state with `cs_affinity cs = AffGuarded`.
*)
Theorem guarded_enter_spec (cs_loc : loc) (me ctx : nat) (cs : concurrency_state) :
  cs_affinity cs = AffGuarded ->
  {{{ is_concurrency_state cs_loc cs }}}
    guarded_enter_code #cs_loc #(Z.of_nat me)
  {{{ RET enter_res_val (graph_enter me ctx cs); is_concurrency_state cs_loc cs }}}.
Proof.
  intros Haff.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /guarded_enter_code /graph_enter Haff.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (me = cs_owner_thread cs)) as [->|Hne].
  - rewrite bool_decide_eq_true_2 //. wp_pures. iModIntro.
    iApply "HΦ". iFrame.
  - rewrite bool_decide_eq_false_2; [|intros Heq; apply Hne; lia].
    wp_pures. iModIntro. iApply "HΦ". iFrame.
Qed.

(**md
**`serialised_enter_code` implements reentrant `Serialised` acquisition via `CAS` on `holder` (`cs_loc +ₗ 3`) and context verification (`cs_loc +ₗ 2`).**

Covers all three outcomes of `graph_enter` under `AffSerialised`:
- Reentrant entry (`cs_holder cs = me`): returns `SOME #false` and preserves `cs`.
- Outermost entry (`cs_holder cs = 0` and `ctx = cs_context cs`): `CAS` transitions `holder` from `0` to
  `me` and returns `SOME #true`.
- Contended or wrong-context entry: returns `NONE` and leaves `cs` untouched.

- [cs_loc:] the concurrency control block location.
- [me:] the calling thread id (`me > 0`).
- [ctx:] the calling thread's `SynchronizationContext` id.
- [cs:] the concurrency state with `cs_affinity cs = AffSerialised`.
*)
Theorem serialised_acquire_spec (cs_loc : loc) (me ctx : nat) (cs : concurrency_state) :
  cs_affinity cs = AffSerialised ->
  {{{ is_concurrency_state cs_loc cs }}}
    serialised_enter_code #cs_loc #(Z.of_nat me) #(Z.of_nat ctx)
  {{{ RET enter_res_val (graph_enter me ctx cs);
      is_concurrency_state cs_loc
        (match graph_enter me ctx cs with Some (cs', _) => cs' | None => cs end) }}}.
Proof.
  intros Haff.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /serialised_enter_code /graph_enter Haff.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (cs_holder cs = me)) as [Hhold|Hne].
  - rewrite Hhold bool_decide_eq_true_2 //. wp_pures. iModIntro.
    iApply "HΦ". rewrite /is_concurrency_state Hhold. iFrame.
  - rewrite bool_decide_eq_false_2; [|intros Heq; apply Hne; lia].
    wp_pures. wp_load. wp_pures.
    destruct (decide (ctx = cs_context cs /\ cs_holder cs = 0)) as [[Hctx Hfree]|Hbad].
    + rewrite Hctx bool_decide_eq_true_2 //. wp_pures.
      rewrite Hfree /=.
      wp_cmpxchg_suc. wp_pures. iModIntro.
      iApply "HΦ". rewrite /is_concurrency_state /=. iFrame.
    + destruct (decide (ctx = cs_context cs)) as [Hctx|Hctx_ne].
      * assert (Hnh : cs_holder cs ≠ 0) by (intros H0; apply Hbad; split; done).
        rewrite Hctx bool_decide_eq_true_2 //. wp_pures.
        wp_cmpxchg_fail; [intros Heq; apply Hnh; lia|].
        wp_pures. iModIntro. iApply "HΦ". iFrame.
      * rewrite bool_decide_eq_false_2; [|intros Heq; apply Hctx_ne; lia].
        wp_pures. iModIntro. iApply "HΦ". iFrame.
Qed.

(**md
**`serialised_release_code` clears `holder` to `0` and sets `pumpPosted = true` whenever `cs_inbox` is non-empty (`graph_release`).**

- [cs_loc:] the concurrency control block location.
- [cs:] the concurrency state before `Release`.
*)
Theorem serialised_release_spec (cs_loc : loc) (cs : concurrency_state) :
  {{{ is_concurrency_state cs_loc cs }}}
    serialised_release_code #cs_loc
  {{{ RET #(); is_concurrency_state cs_loc (graph_release cs) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /serialised_release_code /graph_release.
  wp_lam. wp_pures. wp_store. wp_load.
  destruct (cs_inbox cs) as [|w ws] eqn:Einb; simpl; wp_pures.
  - iModIntro. iApply "HΦ". rewrite /is_concurrency_state /= Einb /=. iFrame.
  - wp_store. iModIntro. iApply "HΦ". rewrite /is_concurrency_state /= Einb /=. iFrame.
Qed.

(**md
**`post_queued_code` appends work item `w` to the FIFO inbox and sets `pumpPosted = true` (`post_queued`).**

- [cs_loc:] the concurrency control block location.
- [w:] the work item id enqueued.
- [cs:] the concurrency state before `PostQueued`.
*)
Theorem post_queued_spec (cs_loc : loc) (w : nat) (cs : concurrency_state) :
  {{{ is_concurrency_state cs_loc cs }}}
    post_queued_code #cs_loc #(Z.of_nat w)
  {{{ RET #();
      ⌜drain_guaranteed (post_queued w cs)⌝ ∗
      is_concurrency_state cs_loc (post_queued w cs) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /post_queued_code /post_queued.
  wp_lam. wp_pures. wp_load.
  rewrite (list_val_eq (cs_inbox cs)).
  wp_apply (vlist_snoc_spec (fun k : nat => #(Z.of_nat k)) (cs_inbox cs) w); [done|].
  iIntros "_". wp_store. wp_store. iModIntro.
  iApply "HΦ". iSplit.
  - iPureIntro. apply (proj1 (serialised_release_reposts_nonempty_inbox w cs)).
  - rewrite /is_concurrency_state /= (list_val_eq (cs_inbox cs ++ [w])). iFrame.
Qed.

(**md
**`pd_try_claim_code` atomically transitions a `PendingDispatch` cell from `0` (`Waiting`) to `1` (`Claimed`) via `CAS` (`pd_try_claim`).**

- [pd:] the `PendingDispatch` state location.
- [st:] the current state (`0` = waiting, `1` = claimed, `2` = closed).
*)
Theorem pending_dispatch_claim_spec (pd : loc) (st : pd_state) :
  {{{ pd ↦ #(Z.of_nat st) }}}
    pd_try_claim_code #pd
  {{{ RET #(snd (pd_try_claim st)); pd ↦ #(Z.of_nat (fst (pd_try_claim st))) }}}.
Proof.
  iIntros (Φ) "Hpd HΦ".
  rewrite /pd_try_claim_code /pd_try_claim.
  wp_lam.
  destruct (decide (st = 0)) as [->|Hne].
  - wp_cmpxchg_suc. wp_pures. iModIntro. by iApply "HΦ".
  - wp_cmpxchg_fail; [intros Heq; apply Hne; lia|].
    wp_pures. iModIntro. by iApply "HΦ".
Qed.

(**md
**`pd_try_close_code` atomically transitions a `PendingDispatch` cell from `0` (`Waiting`) to `2` (`Closed`) via `CAS` (`pd_try_close`).**

- [pd:] the `PendingDispatch` state location.
- [st:] the current state (`0` = waiting, `1` = claimed, `2` = closed).
*)
Theorem pending_dispatch_close_spec (pd : loc) (st : pd_state) :
  {{{ pd ↦ #(Z.of_nat st) }}}
    pd_try_close_code #pd
  {{{ RET #(snd (pd_try_close st)); pd ↦ #(Z.of_nat (fst (pd_try_close st))) }}}.
Proof.
  iIntros (Φ) "Hpd HΦ".
  rewrite /pd_try_close_code /pd_try_close.
  wp_lam.
  destruct (decide (st = 0)) as [->|Hne].
  - wp_cmpxchg_suc. wp_pures. iModIntro. by iApply "HΦ".
  - wp_cmpxchg_fail; [intros Heq; apply Hne; lia|].
    wp_pures. iModIntro. by iApply "HΦ".
Qed.

End heap_concurrency.
