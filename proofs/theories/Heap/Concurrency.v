From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Engine Scheduler Concurrency.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run Scheduler.

Open Scope nat_scope.

(**md # Heap concurrency, thread affinity, and inbox contracts

This module lifts the concurrency, `Serialised` reentrant holder, inbox handover, and `PendingDispatch`
contracts of `Pure/Concurrency.v` to HeapLang separation logic:

1. `guarded_enter_spec`: `Guarded` thread-affinity gate (`AssertOnGraphThread` / `Enter`).
2. `serialised_acquire_spec` and `serialised_release_spec`: `Serialised` reentrant holder lock (`CAS` on
   `holder`, context check, and `Release` with non-empty inbox drain re-post).
3. `post_queued_spec`: cross-thread inbox enqueue (`PostQueued`) and drain wake post.
4. `pending_dispatch_claim_spec` and `pending_dispatch_close_spec`: atomic `CAS` claim (`TryClaim`) and
   cancellation/abandon (`TryClose`) on `PendingDispatch`.
5. `read_memo_fast_spec`, `signal_set_fast_split_spec`, `default_comparer_null_sentinel_spec`, and
   `fast_write_token_spec`: `.NET` hot/cold fast-path specifications for `Memo.Value`, `Signal.Value`,
   `ComparerSentinel`, and `Graph.fastWriteToken` / `OwnerThreadToken`. *)

(** ** HeapLang code for concurrency, holder lock, inbox, and PendingDispatch *)

(**md Layout of a 6-cell concurrency control block `cs`:

- `cs +ₗ 0`: affinity (`0` = `Guarded`, `1` = `Unchecked`, `2` = `Serialised`)
- `cs +ₗ 1`: `ownerThread`
- `cs +ₗ 2`: `context`
- `cs +ₗ 3`: `holder` (`0` when free, `me > 0` when held)
- `cs +ₗ 4`: `inbox` (HeapLang list of work item ids)
- `cs +ₗ 5`: `pumpPosted` (boolean)
*)

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

Definition aff_enter_code : val :=
  λ: "cs" "me" "ctx",
    let: "aff" := !"cs" in
    if: "aff" = #0 then guarded_enter_code "cs" "me"
    else if: "aff" = #1 then SOME #false
    else serialised_enter_code "cs" "me" "ctx".

Definition read_memo_fast_code : val :=
  λ: "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => read_code "g" "n" #true
    | SOME "d" =>
        let: "k" := Fst (Fst (Fst (Fst "d"))) in
        if: "k" = #1 then
          let: "fr" := Fst (Fst (Snd (Fst "d"))) in
          let: "st" := Snd (Snd (Fst (Fst "d"))) in
          if: (BinOp OrOp "fr" (BinOp AndOp "st" #3)) = #0 then
            track_code "g" "n";;
            InjL (Snd (Fst (Fst (Fst "d"))))
          else
            read_code "g" "n" #true
        else
          read_code "g" "n" #true
    end.

Definition signal_set_after_acquire_code : val :=
  λ: "cs" "g" "n" "v",
    match: graph_get_node_code "g" "n" with
      NONE => serialised_release_code "cs";; NONE
    | SOME "d" =>
        let: "cur" := Snd (Fst (Fst (Fst "d"))) in
        if: "cur" = "v" then
          serialised_release_code "cs";; SOME #()
        else
          write_signal_code "g" "n" "v";;
          serialised_release_code "cs";;
          SOME #()
    end.

Definition signal_set_fast_split_code : val :=
  λ: "cs" "me" "ctx" "g" "n" "v",
    match: aff_enter_code "cs" "me" "ctx" with
      NONE => NONE
    | SOME "must_rel" =>
        if: "must_rel" then
          signal_set_after_acquire_code "cs" "g" "n" "v"
        else
          match: graph_get_node_code "g" "n" with
            NONE => NONE
          | SOME "d" =>
              let: "cur" := Snd (Fst (Fst (Fst "d"))) in
              if: "cur" = "v" then SOME #()
              else
                write_signal_code "g" "n" "v";;
                SOME #()
          end
    end.

Definition null_comparer_code : val :=
  λ: <> <>, InjR (#0, #0).

Definition encode_comparer_code : val :=
  λ: "is_val_type" "is_val_def" "is_ref_id" "opt_cmp",
    match: "opt_cmp" with
      NONE => SOME null_comparer_code
    | SOME "f" =>
        if: "is_val_type" then
          (if: "is_val_def" then NONE else SOME "f")
        else
          (if: "is_ref_id" then NONE else SOME "f")
    end.

Definition eval_comparer_sentinel_code : val :=
  λ: "is_val_type" "enc" "a" "b",
    if: "is_val_type" then
      match: "enc" with
        NONE => InjL ("a" = "b")
      | SOME "cmp" => "cmp" "a" "b"
      end
    else
      match: "enc" with
        NONE => InjL ("a" = "b")
      | SOME "cmp" => "cmp" "a" "b"
      end.

Definition check_fast_write_token_code : val :=
  λ: "tok_loc" "caller_tok",
    let: "tok" := !"tok_loc" in
    if: #0 ≤ "tok" then
      (if: "tok" = #0 then #true else "tok" = "caller_tok")
    else #false.

Definition signal_set_token_fast_code : val :=
  λ: "tok_loc" "caller_tok" "cs" "me" "ctx" "g" "n" "v",
    if: check_fast_write_token_code "tok_loc" "caller_tok" then
      match: graph_get_node_code "g" "n" with
        NONE => NONE
      | SOME "d" =>
          let: "cur" := Snd (Fst (Fst (Fst "d"))) in
          if: "cur" = "v" then SOME #()
          else
            write_signal_code "g" "n" "v";;
            SOME #()
      end
    else
      signal_set_fast_split_code "cs" "me" "ctx" "g" "n" "v".

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
  rewrite /guarded_enter_code /graph_enter Haff /=.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (me = cs_owner_thread cs)) as [->|Hne].
  - rewrite bool_decide_eq_true_2 //. wp_pures. iModIntro.
    iApply "HΦ". rewrite /is_concurrency_state Haff /=. iFrame.
  - rewrite bool_decide_eq_false_2; [|intros Heq; injection Heq as Heq; apply Hne; lia].
    wp_pures. iModIntro.
    iApply "HΦ". rewrite /is_concurrency_state Haff /=. iFrame.
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
  rewrite /serialised_enter_code /graph_enter Haff /=.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (cs_holder cs = me)) as [Hhold|Hne].
  - rewrite Hhold bool_decide_eq_true_2 //. wp_pures. iModIntro.
    iApply "HΦ". rewrite /is_concurrency_state Haff Hhold /=. iFrame.
  - rewrite bool_decide_eq_false_2; [|intros Heq; injection Heq as Heq; apply Hne; lia].
    wp_pures. wp_load. wp_pures.
    destruct (decide (ctx = cs_context cs /\ cs_holder cs = 0)) as [[Hctx Hfree]|Hbad].
    + rewrite Hctx bool_decide_eq_true_2 //. wp_pures.
      rewrite Hfree.
      wp_cmpxchg_suc; [done|]. wp_pures. iModIntro.
      iApply "HΦ". rewrite /is_concurrency_state /=. iFrame.
    + destruct (decide (ctx = cs_context cs)) as [Hctx|Hctx_ne].
      * assert (Hnh : cs_holder cs ≠ 0) by (intros H0; apply Hbad; split; done).
        rewrite Hctx bool_decide_eq_true_2 //. wp_pures.
        wp_cmpxchg_fail; [intros Heq; injection Heq as Heq; apply Hnh; lia|].
        wp_pures. iModIntro. iApply "HΦ". rewrite /is_concurrency_state Haff /=. iFrame.
      * rewrite bool_decide_eq_false_2; [|intros Heq; injection Heq as Heq; apply Hctx_ne; lia].
        wp_pures. iModIntro. iApply "HΦ". rewrite /is_concurrency_state Haff /=. iFrame.
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
  - iModIntro. iApply "HΦ". rewrite /is_concurrency_state /=. iFrame.
  - wp_store. iModIntro. iApply "HΦ". rewrite /is_concurrency_state /=. iFrame.
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
  wp_lam. wp_pures. wp_load. wp_pures.
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
  - wp_cmpxchg_suc; [done|]. wp_pures. iModIntro. by iApply "HΦ".
  - wp_cmpxchg_fail; [intros Heq; injection Heq as Heq; apply Hne; lia|].
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
  - wp_cmpxchg_suc; [done|]. wp_pures. iModIntro. by iApply "HΦ".
  - wp_cmpxchg_fail; [intros Heq; injection Heq as Heq; apply Hne; lia|].
    wp_pures. iModIntro. by iApply "HΦ".
Qed.

(** ** Hot/cold fast-path specifications for Memo.Value and Signal.Value *)

Lemma aff_enter_spec (cs_loc : loc) (me ctx : nat) (cs : concurrency_state) :
  {{{ is_concurrency_state cs_loc cs }}}
    aff_enter_code #cs_loc #(Z.of_nat me) #(Z.of_nat ctx)
  {{{ RET enter_res_val (graph_enter me ctx cs);
      is_concurrency_state cs_loc
        (match graph_enter me ctx cs with Some (cs', _) => cs' | None => cs end) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /aff_enter_code. wp_lam. wp_pures. wp_load. wp_pures.
  destruct (cs_affinity cs) eqn:Haff; simpl; wp_pures.
  - wp_apply (guarded_enter_spec cs_loc me ctx cs Haff with "[H0 H1 H2 H3 H4 H5]").
    + rewrite /is_concurrency_state Haff /=. iFrame.
    + iIntros "Hcs".
      destruct (guarded_enter_iff_owner me ctx cs Haff) as [[_ Hsome] [Hne _]].
      destruct (decide (me = cs_owner_thread cs)) as [Heq|Hneq].
      * assert (Hge : graph_enter me ctx cs = Some (cs, false)) by (by apply Hsome).
        rewrite Hge. by iApply "HΦ".
      * assert (Hge : graph_enter me ctx cs = None) by (by apply Hne).
        rewrite Hge. by iApply "HΦ".
  - rewrite /graph_enter Haff /=. iModIntro.
    iApply "HΦ". rewrite /is_concurrency_state Haff /=. iFrame.
  - wp_apply (serialised_acquire_spec cs_loc me ctx cs Haff with "[H0 H1 H2 H3 H4 H5]").
    + rewrite /is_concurrency_state Haff /=. iFrame.
    + done.
Qed.

(**md
**`read_memo_fast_code` inspects `freshness` and `status &&& 3` inline to track and return clean cached memo values directly while matching `read_code` across all states.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the target node id.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the read outcome.
*)
Theorem read_memo_fast_spec γ g (n : node_id) (s : State.state) (fuel : nat) (s' : State.state) (r : outcome) :
  read fuel n true s = Some (s', r) ->
  {{{ is_graph γ g s }}}
    read_memo_fast_code g #(Z.of_nat n)
  {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros Hread. iIntros (Φ) "Hg HΦ".
  rewrite /read_memo_fast_code. wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  destruct (g_nodes s !! n) as [d|] eqn:Hnode; simpl.
  - rewrite /node_val /node_val_a /node_val_b /node_val_c /node_val_d.
    wp_pures.
    destruct (n_kind d) eqn:Hk; simpl; wp_pures.
    + wp_apply (read_spec _ _ n true s fuel s' r Hread with "Hg"). iExact "HΦ".
    + destruct (n_freshness d) eqn:Hfr, (n_status d) eqn:Hst; simpl; wp_pures.
      1: {
        destruct fuel as [|fuel']; [done|].
        destruct fuel' as [|f]; [done|].
        pose proof (proj2 (memo_read_fast_split_eq f n s)) as Hsplit.
        rewrite Hsplit /read_memo_fast in Hread.
        unfold m_bind, m_return, get_node in Hread. simpl in Hread.
        rewrite Hnode Hk Hfr Hst in Hread.
        assert (Hc : is_clean_and_ready_bits Clean StNone false = true) by reflexivity.
        rewrite Hc in Hread.
        destruct (track n s) as [[s1 []]|] eqn:Htr; [|discriminate Hread].
        injection Hread as -> <-.
        assert (Hndom : n ∈ dom (g_nodes s)) by (by apply elem_of_dom_2 in Hnode).
        wp_apply (track_spec _ _ n s s' Hndom Htr with "Hg"). iIntros "Hg".
        wp_pures. by iApply "HΦ".
      }
      all: wp_apply (read_spec _ _ n true s fuel s' r Hread with "Hg"); iExact "HΦ".
    + wp_apply (read_spec _ _ n true s fuel s' r Hread with "Hg"). iExact "HΦ".
  - wp_pures.
    wp_apply (read_spec _ _ n true s fuel s' r Hread with "Hg"). iExact "HΦ".
Qed.

(**md
**`signal_set_fast_split_code` enters affinity first (`aff_enter_code`), short-circuits unjournaled equal writes without `write_signal_code` or `serialised_release_code` when `must_rel = false`, and releases acquired `Serialised` locks cleanly.**

- [γ:] the ghost name.
- [g:] the graph value.
- [cs_loc:] the concurrency control block location.
- [me:] the calling thread id.
- [ctx:] the calling thread's `SynchronizationContext` id.
- [f:] inner pure step budget for `write_signal (S f)`.
- [n:] the signal node id.
- [v:] the written integer value.
- [cs:] the initial concurrency state.
- [s:] the initial pure graph state.
- [s_w:] the pure state produced by `write_signal (S f) n v s`.
- [d:] the signal node record at `n`.
*)
Theorem signal_set_fast_split_spec
    γ g (cs_loc : loc) (me ctx f : nat) (n : node_id) (v : Z)
    (cs : concurrency_state) (s s_w : State.state) (d : node) :
  g_nodes s !! n = Some d ->
  n_kind d = KSignal ->
  write_signal (S f) n v s = Some (s_w, tt) ->
  {{{ is_concurrency_state cs_loc cs ∗ is_graph γ g s }}}
    signal_set_fast_split_code #cs_loc #(Z.of_nat me) #(Z.of_nat ctx) g #(Z.of_nat n) #v
  {{{ RET match signal_set_fast_split me ctx (S f) n v cs None s with
          | Some _ => SOMEV #()
          | None => NONEV
          end;
      match signal_set_fast_split me ctx (S f) n v cs None s with
      | Some (cs', _, s') => is_concurrency_state cs_loc cs' ∗ is_graph γ g s'
      | None => is_concurrency_state cs_loc cs ∗ is_graph γ g s
      end }}}.
Proof.
  intros Hnode Hk Hws.
  iIntros (Φ) "(Hcs & Hg) HΦ".
  rewrite /signal_set_fast_split_code. wp_lam. wp_pures.
  wp_apply (aff_enter_spec cs_loc me ctx cs with "Hcs"). iIntros "Hcs".
  destruct (graph_enter me ctx cs) as [[cs1 []]|] eqn:Hge; simpl; wp_pures.
  - rewrite /signal_set_after_acquire_code. wp_lam. wp_pures.
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hnode /node_val /node_val_a /node_val_b /node_val_c /node_val_d /=.
    wp_pures.
    destruct (decide (n_value d = v)) as [Heq|Hne].
    + rewrite Heq bool_decide_eq_true_2 //. wp_pures.
      wp_apply (serialised_release_spec cs_loc cs1 with "Hcs"). iIntros "Hcs".
      wp_pures. iModIntro.
      rewrite /signal_set_fast_split /signal_set_after_acquire Hge Hnode Hk decide_True //.
      iApply "HΦ". iFrame.
    + rewrite bool_decide_eq_false_2; [|intros Hc; injection Hc as Hc; apply Hne; lia].
      wp_pures.
      wp_apply (write_signal_spec γ g n v s (S f) s_w Hws with "Hg"). iIntros "Hg".
      wp_pures.
      wp_apply (serialised_release_spec cs_loc cs1 with "Hcs"). iIntros "Hcs".
      wp_pures. iModIntro.
      rewrite /signal_set_fast_split /signal_set_after_acquire Hge Hnode Hk decide_False // Hws /=.
      iApply "HΦ". iFrame.
  - wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hnode /node_val /node_val_a /node_val_b /node_val_c /node_val_d /=.
    wp_pures.
    destruct (decide (n_value d = v)) as [Heq|Hne].
    + rewrite Heq bool_decide_eq_true_2 //. wp_pures. iModIntro.
      rewrite /signal_set_fast_split Hge Hnode Hk decide_True //.
      iApply "HΦ". iFrame.
    + rewrite bool_decide_eq_false_2; [|intros Hc; injection Hc as Hc; apply Hne; lia].
      wp_pures.
      wp_apply (write_signal_spec γ g n v s (S f) s_w Hws with "Hg"). iIntros "Hg".
      wp_pures. iModIntro.
      rewrite /signal_set_fast_split Hge Hnode Hk decide_False // Hws /=.
      iApply "HΦ". iFrame.
  - rewrite /signal_set_fast_split Hge.
    iModIntro. iApply "HΦ". iFrame.
Qed.

Definition cmp_outcome_val (out : cmp_outcome) : val :=
  match out with
  | CmpBool b => InjLV #b
  | CmpNullRef => InjRV (#0, #0)
  | CmpThrow err => InjRV (#1, #err)
  end.

Definition eq_prim_code : val :=
  λ: "x" "y", InjL ("x" = "y").

Definition nullable_comparer_args (c : nullable_comparer) (fv : val) : val * val * val :=
  match c with
  | CmpNullInput => (#false, #false, NONEV)
  | CmpValDefault => (#true, #false, SOMEV eq_prim_code)
  | CmpRefIdentity => (#false, #true, SOMEV eq_prim_code)
  | CmpCustomInput _ => (#false, #false, SOMEV fv)
  end.

(**md
**`encode_comparer_code` followed by `eval_comparer_sentinel_code` encodes value-type `EqualityComparer<'T>.Default` and reference-type `ReferenceComparer<'T>` as `NONE` (`null`) evaluated via primitive `=`, raises `CmpNullRef` via `null_comparer_code` when the input comparer was `NONE`, and invokes other comparers unchanged.**

- [is_val_type:] `true` when `typeof<'T>.IsValueType`, `false` otherwise.
- [c:] the input `nullable_comparer` (`CmpNullInput`, `CmpValDefault`, `CmpRefIdentity`, or `CmpCustomInput f`).
- [fv:] the HeapLang closure representing a custom comparer when `c = CmpCustomInput f`.
- [a:] the previous integer value.
- [b:] the candidate integer value.
*)
Theorem default_comparer_null_sentinel_spec
    (is_val_type : bool) (c : nullable_comparer) (fv : val) (a b : Z) :
  (forall f, c = CmpCustomInput f ->
    {{{ True }}} fv #a #b {{{ RET cmp_outcome_val (f a b); True }}}) ->
  let '(is_val_def, is_ref_id, opt_cmp) := nullable_comparer_args c fv in
  {{{ True }}}
    let: "enc" := encode_comparer_code #is_val_type is_val_def is_ref_id opt_cmp in
    eval_comparer_sentinel_code #is_val_type "enc" #a #b
  {{{ RET cmp_outcome_val (eval_nullable_comparer Z.eqb Z.eqb c a b); True }}}.
Proof.
  intros Hcustom.
  destruct is_val_type, c as [| | |f]; simpl; iIntros (Φ) "_ HΦ".
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code /null_comparer_code.
    wp_pures. iModIntro. by iApply "HΦ".
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code.
    wp_pures. iModIntro.
    replace (bool_decide (#a = #b)) with (Z.eqb a b).
    + by iApply "HΦ".
    + symmetry. destruct (Z.eqb_spec a b) as [->|Hne].
      * by rewrite bool_decide_eq_true_2.
      * rewrite bool_decide_eq_false_2 //; intros [= H]; exact (Hne H).
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code /eq_prim_code.
    wp_pures. iModIntro.
    replace (bool_decide (#a = #b)) with (Z.eqb a b).
    + by iApply "HΦ".
    + symmetry. destruct (Z.eqb_spec a b) as [->|Hne].
      * by rewrite bool_decide_eq_true_2.
      * rewrite bool_decide_eq_false_2 //; intros [= H]; exact (Hne H).
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code.
    wp_pures.
    wp_apply (Hcustom f eq_refl with "[//]").
    iIntros "_". by iApply "HΦ".
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code /null_comparer_code.
    wp_pures. iModIntro. by iApply "HΦ".
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code /eq_prim_code.
    wp_pures. iModIntro.
    replace (bool_decide (#a = #b)) with (Z.eqb a b).
    + by iApply "HΦ".
    + symmetry. destruct (Z.eqb_spec a b) as [->|Hne].
      * by rewrite bool_decide_eq_true_2.
      * rewrite bool_decide_eq_false_2 //; intros [= H]; exact (Hne H).
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code.
    wp_pures. iModIntro.
    replace (bool_decide (#a = #b)) with (Z.eqb a b).
    + by iApply "HΦ".
    + symmetry. destruct (Z.eqb_spec a b) as [->|Hne].
      * by rewrite bool_decide_eq_true_2.
      * rewrite bool_decide_eq_false_2 //; intros [= H]; exact (Hne H).
  - rewrite /encode_comparer_code /eval_comparer_sentinel_code.
    wp_pures.
    wp_apply (Hcustom f eq_refl with "[//]").
    iIntros "_". by iApply "HΦ".
Qed.

Lemma check_fast_write_token_spec (tok_loc : loc) (tok caller_tok : Z) :
  {{{ tok_loc ↦ #tok }}}
    check_fast_write_token_code #tok_loc #caller_tok
  {{{ RET #(check_fast_write_token tok caller_tok); tok_loc ↦ #tok }}}.
Proof.
  iIntros (Φ) "Htok HΦ".
  rewrite /check_fast_write_token_code /check_fast_write_token.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (0 <= tok)%Z) as [Hnn|Hneg].
  - rewrite !(bool_decide_eq_true_2 (0 <= tok)%Z) //=.
    wp_pures.
    destruct (decide (tok = 0%Z)) as [->|Hnt0].
    + rewrite (bool_decide_eq_true_2 (0%Z = 0%Z)) //=.
      wp_pures. iModIntro. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (tok = 0%Z)) //=.
      rewrite (bool_decide_eq_false_2 (#tok = #0)); [|intros [= H]; exact (Hnt0 H)].
      wp_pures.
      destruct (decide (tok = caller_tok)) as [->|Hntc].
      * rewrite (bool_decide_eq_true_2 (caller_tok = caller_tok)) //.
        rewrite (bool_decide_eq_true_2 (#caller_tok = #caller_tok)) //.
        iModIntro. by iApply "HΦ".
      * rewrite (bool_decide_eq_false_2 (tok = caller_tok)) //.
        rewrite (bool_decide_eq_false_2 (#tok = #caller_tok)); [|intros [= H]; exact (Hntc H)].
        iModIntro. by iApply "HΦ".
  - rewrite !(bool_decide_eq_false_2 (0 <= tok)%Z) //=.
    wp_pures. iModIntro. by iApply "HΦ".
Qed.

(**md
**`signal_set_token_fast_code` loads `fastWriteToken` (`tok_loc`) and compares against `0` or `caller_tok` to bypass `aff_enter_code` on unjournaled `Unchecked` and owner-thread `Guarded` writes while refining `signal_set_entered` identically across all affinities and threads.**

- [γ:] the ghost name.
- [g:] the graph value.
- [tok_loc:] the immutable `fastWriteToken` cell location.
- [cs_loc:] the concurrency control block location.
- [owner_tok:] the positive token (`>= 1`) assigned to `cs_owner_thread cs`.
- [caller_tok:] `OwnerThreadToken.Current` on the calling thread (`>= 0`).
- [me:] the calling thread id.
- [ctx:] the calling thread's `SynchronizationContext` id.
- [f:] inner pure step budget for `write_signal (S f)`.
- [n:] the signal node id.
- [v:] the written integer value.
- [cs:] the initial concurrency state.
- [s:] the initial pure graph state.
- [s_w:] the pure state produced by `write_signal (S f) n v s`.
- [d:] the signal node record at `n`.
*)
Theorem fast_write_token_spec
    γ g (tok_loc cs_loc : loc) (owner_tok caller_tok : Z)
    (me ctx f : nat) (n : node_id) (v : Z)
    (cs : concurrency_state) (s s_w : State.state) (d : node) :
  (1 <= owner_tok)%Z ->
  (0 <= caller_tok)%Z ->
  (caller_tok = owner_tok <-> me = cs_owner_thread cs) ->
  g_nodes s !! n = Some d ->
  n_kind d = KSignal ->
  write_signal (S f) n v s = Some (s_w, tt) ->
  let tok := compute_fast_write_token (cs_affinity cs) false owner_tok in
  {{{ tok_loc ↦ #tok ∗ is_concurrency_state cs_loc cs ∗ is_graph γ g s }}}
    signal_set_token_fast_code #tok_loc #caller_tok #cs_loc #(Z.of_nat me) #(Z.of_nat ctx) g #(Z.of_nat n) #v
  {{{ RET match signal_set_entered me ctx (S f) n v cs None s with
          | Some _ => SOMEV #()
          | None => NONEV
          end;
      tok_loc ↦ #tok ∗
      match signal_set_entered me ctx (S f) n v cs None s with
      | Some (cs', _, s') => is_concurrency_state cs_loc cs' ∗ is_graph γ g s'
      | None => is_concurrency_state cs_loc cs ∗ is_graph γ g s
      end }}}.
Proof.
  intros Hown Hcaller Htok_eq Hnode Hk Hws. cbv zeta.
  set (tok := compute_fast_write_token (cs_affinity cs) false owner_tok).
  destruct (fast_write_token_iff owner_tok caller_tok me ctx f n v cs None s Hown Hcaller Htok_eq)
    as (_ & _ & Href).
  assert (Hbody :
    signal_set_entered me ctx (S f) n v cs None s =
    if check_fast_write_token tok caller_tok then
      if decide (n_value d = v) then Some (cs, None, s) else Some (cs, None, s_w)
    else
      signal_set_fast_split me ctx (S f) n v cs None s).
  { rewrite <- Href. unfold signal_set_token_fast.
    rewrite Hnode. cbv beta iota zeta.
    rewrite Hk Hws. reflexivity. }
  rewrite Hbody.
  iIntros (Φ) "(Htok & Hcs & Hg) HΦ".
  rewrite /signal_set_token_fast_code. wp_lam. wp_pures.
  wp_apply (check_fast_write_token_spec tok_loc tok caller_tok with "Htok").
  iIntros "Htok".
  destruct (check_fast_write_token tok caller_tok) eqn:Hchk; wp_pures.
  - wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hnode /node_val /node_val_a /node_val_b /node_val_c /node_val_d /=.
    wp_pures.
    destruct (decide (n_value d = v)) as [Heq|Hne].
    + rewrite Heq bool_decide_eq_true_2 //. wp_pures. iModIntro.
      iApply "HΦ". iFrame.
    + rewrite bool_decide_eq_false_2; [|intros [= Hc]; exact (Hne Hc)].
      wp_pures.
      wp_apply (write_signal_spec γ g n v s (S f) s_w Hws with "Hg"). iIntros "Hg".
      wp_pures. iModIntro.
      iApply "HΦ". iFrame.
  - wp_apply (signal_set_fast_split_spec γ g cs_loc me ctx f n v cs s s_w d Hnode Hk Hws with "[$Hcs $Hg]").
    iIntros "Hpost".
    iApply "HΦ". iFrame.
Qed.

End heap_concurrency.
