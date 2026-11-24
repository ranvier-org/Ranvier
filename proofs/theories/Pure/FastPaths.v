From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler Ownership Concurrency.

Open Scope nat_scope.

(**md # Stage B fast-path refinement theorems (pure tier)

This module formalizes and verifies the Stage B fast-path refinement theorems in `Pure/`:

1. **Theorem B1** (`flushing_implies_no_flush_owed`, `deferring_with_flushing_eq`): `g_flushing = true` implies `g_flush_owed = false` under `sched_inv`, and adding `g_flushing` to `deferring` yields identical states, values, and traces across all programs and pulls.
2. **Theorem B2** (`disposed_empty_sources`, `track_slot0_fast_eq`, `track_slot1_fast_eq`): reachable disposed computations always have empty `source_slots` (even on the running stack), refining slot-0 and slot-1 hits in `track` to a direct cursor assignment.
3. **Theorem B3** (`leaf_clean_memo_mark_dirty_eq`, `notify_moved_single_check_memo_eq`, `iterative_mark_check_chain_eq`): leaf clean memo direct dirty write, sole `Check` memo promotion, and tail-recursive single-memo `mark_check` chain traversal.
4. **Theorem B4** (`iterative_chain_refresh_eq`): explicit-stack iterative single-memo `Check`-chain refresh with LIFO `g_checking` frames and repeat-check unwinding equals `refresh` on all states.
5. **Theorem B5** (`fused_single_signal_effect_eq`, `fused_single_memo_effect_flush_eq`): idle-graph fused `S -> E` and `S -> M -> E` execution equals `write_signal` with bit-for-bit identical final states and traces.
6. **Theorem B6** (`memo_published_ready_cutoff_eq`): ready-published memo outcome comparison simplifies to value comparison without status/failure checks. *)

(** ** 0. Fuel monotonicity and state invariants across Engine *)

Lemma bind_mono {A B} (m1 m2 : M A) (k1 k2 : A -> M B) s r :
  (forall s1 a, m1 s = Some (s1, a) -> m2 s = Some (s1, a)) ->
  (forall a s1 r1, m1 s = Some (s1, a) -> k1 a s1 = Some r1 -> k2 a s1 = Some r1) ->
  m_bind m1 k1 s = Some r -> m_bind m2 k2 s = Some r.
Proof.
  intros Hm Hk Hb. unfold m_bind in *.
  destruct (m1 s) as [[s1 a]|] eqn:E; [|discriminate].
  rewrite (Hm _ _ eq_refl). by eapply Hk.
Qed.

Lemma hosted_mono {A} n (b1 b2 : M A) s r :
  (forall s0 r0, b1 s0 = Some r0 -> b2 s0 = Some r0) ->
  hosted n b1 s = Some r -> hosted n b2 s = Some r.
Proof.
  intros Hb H. unfold hosted in *.
  eapply bind_mono; [intros ?? E; exact E| |exact H].
  intros [] s1 r1 _.
  eapply bind_mono; [intros ?? E; by apply Hb|intros a s2 r2 _; exact id].
Qed.

Lemma untracked_mono {A} (b1 b2 : M A) s r :
  (forall s0 r0, b1 s0 = Some r0 -> b2 s0 = Some r0) ->
  untracked b1 s = Some r -> untracked b2 s = Some r.
Proof.
  intros Hb H. unfold untracked in *.
  eapply bind_mono; [intros ?? E; exact E| |exact H].
  intros [] s1 r1 _.
  eapply bind_mono; [intros ?? E; by apply Hb|intros a s2 r2 _; exact id].
Qed.

Lemma detached_mono o (b1 b2 : M unit) s r :
  (forall s0 r0, b1 s0 = Some r0 -> b2 s0 = Some r0) ->
  detached o b1 s = Some r -> detached o b2 s = Some r.
Proof.
  intros Hb H. unfold detached in *.
  eapply bind_mono; [intros ?? E; exact E| |exact H].
  intros [] s1 r1 _.
  eapply bind_mono; [intros ?? E; by apply Hb|intros [] s2 r2 _; exact id].
Qed.

Lemma walk_marks_mono mark1 mark2 src k s s' :
  (forall c s0 s1, mark1 c s0 = Some s1 -> mark2 c s0 = Some s1) ->
  walk_marks mark1 src k s = Some s' -> walk_marks mark2 src k s = Some s'.
Proof.
  intros Hm. revert s. induction k as [|i IH]; intros s Hw; simpl in *; [exact Hw|].
  destruct (decide _); [by apply IH|].
  destruct (observer_items (observers_of (g_edges s) src) !! i) as [[c ?]|]; [|discriminate].
  destruct (mark1 c s) as [s1|] eqn:E; simpl in *; [|discriminate].
  rewrite (Hm _ _ _ E). simpl. by apply IH.
Qed.

Lemma mark_check_mono f f' n s s' :
  f <= f' -> mark_check f n s = Some s' -> mark_check f' n s = Some s'.
Proof.
  revert f' n s s'. induction f as [|f IH]; intros f' n s s' Hle Hm; simpl in *; [discriminate|].
  destruct f' as [|f']; [lia|]. simpl.
  destruct (g_nodes s !! n) as [d|]; [|exact Hm].
  destruct (n_kind d); [exact Hm| |exact Hm].
  destruct (decide (n_freshness d = Clean)).
  - eapply walk_marks_mono; [|exact Hm]. intros c s0 s1. apply IH. lia.
  - destruct (announce_due s d); [|exact Hm].
    eapply walk_marks_mono; [|exact Hm]. intros c s0 s1. apply IH. lia.
Qed.

Lemma mark_dirty_mono f f' n s s' :
  f <= f' -> mark_dirty f n s = Some s' -> mark_dirty f' n s = Some s'.
Proof.
  intros Hle Hm. destruct f as [|f]; simpl in *; [discriminate|].
  destruct f' as [|f']; [lia|]. simpl.
  destruct (g_nodes s !! n) as [d|]; [|exact Hm].
  destruct (n_kind d); [exact Hm| |exact Hm].
  destruct (decide (n_freshness d = Clean)).
  - eapply walk_marks_mono; [|exact Hm]. intros c s0 s1. apply mark_check_mono. lia.
  - destruct (announce_due s d); [|exact Hm].
    eapply walk_marks_mono; [|exact Hm]. intros c s0 s1. apply mark_check_mono. lia.
Qed.

Lemma notify_dirty_mono f f' src s s' :
  f <= f' -> notify_dirty f src s = Some s' -> notify_dirty f' src s = Some s'.
Proof.
  intros Hle Hn. unfold notify_dirty, notify_with in *.
  eapply walk_marks_mono; [|exact Hn]. intros c s0 s1. by apply mark_dirty_mono.
Qed.

Lemma notify_dirty_except_mono f f' src r s s' :
  f <= f' -> notify_dirty_except f src r s = Some s' -> notify_dirty_except f' src r s = Some s'.
Proof.
  intros Hle Hn. unfold notify_dirty_except, notify_with in *.
  eapply walk_marks_mono; [|exact Hn]. intros c s0 s1.
  unfold mark_except. destruct (decide _); [exact id|by apply mark_dirty_mono].
Qed.

Lemma m_step_mark_dirty_mono f1 f2 n s r :
  f1 <= f2 -> m_step (mark_dirty f1 n) s = Some r -> m_step (mark_dirty f2 n) s = Some r.
Proof.
  intros Hle H. unfold m_step in H |- *.
  destruct (mark_dirty f1 n s) eqn:Em; simplify_eq.
  erewrite mark_dirty_mono; [reflexivity|exact Hle|exact Em].
Qed.

Lemma m_step_mark_check_mono f1 f2 n s r :
  f1 <= f2 -> m_step (mark_check f1 n) s = Some r -> m_step (mark_check f2 n) s = Some r.
Proof.
  intros Hle H. unfold m_step in H |- *.
  destruct (mark_check f1 n s) eqn:Em; simplify_eq.
  erewrite mark_check_mono; [reflexivity|exact Hle|exact Em].
Qed.

Lemma m_step_notify_dirty_mono f1 f2 src s r :
  f1 <= f2 -> m_step (notify_dirty f1 src) s = Some r -> m_step (notify_dirty f2 src) s = Some r.
Proof.
  intros Hle H. unfold m_step in H |- *.
  destruct (notify_dirty f1 src s) eqn:Em; simplify_eq.
  erewrite notify_dirty_mono; [reflexivity|exact Hle|exact Em].
Qed.

Lemma m_step_notify_dirty_except_mono f1 f2 src cur s r :
  f1 <= f2 -> m_step (notify_dirty_except f1 src cur) s = Some r -> m_step (notify_dirty_except f2 src cur) s = Some r.
Proof.
  intros Hle H. unfold m_step in H |- *.
  destruct (notify_dirty_except f1 src cur s) eqn:Em; simplify_eq.
  erewrite notify_dirty_except_mono; [reflexivity|exact Hle|exact Em].
Qed.

Lemma mark_reader_mono f1 f2 s r :
  f1 <= f2 -> mark_reader f1 s = Some r -> mark_reader f2 s = Some r.
Proof.
  intros Hle H. unfold mark_reader in H |- *.
  destruct (g_current s), (f_reader_mark (g_flags s)); try exact H.
  destruct (mark_check f1 _ s) eqn:Em; simplify_eq.
  erewrite mark_check_mono; [reflexivity|exact Hle|exact Em].
Qed.

Record engine_mono_at (f : nat) : Prop := {
  Mexec : forall f' e c s r, f <= f' -> exec f e c s = Some r -> exec f' e c s = Some r;
  Mread : forall f' n b s r, f <= f' -> read f n b s = Some r -> read f' n b s = Some r;
  Mread_node : forall f' n s r, f <= f' -> read_node f n s = Some r -> read_node f' n s = Some r;
  Mupdate : forall f' n s r, f <= f' -> update_if_necessary f n s = Some r -> update_if_necessary f' n s = Some r;
  Mrop : forall f' n s r, f <= f' -> refresh_or_pull f n s = Some r -> refresh_or_pull f' n s = Some r;
  Mpull : forall f' n s r, f <= f' -> pull f n s = Some r -> pull f' n s = Some r;
  Mrefresh : forall f' n s r, f <= f' -> refresh f n s = Some r -> refresh f' n s = Some r;
  Mwalk : forall f' n i s r, f <= f' -> resolve_walk f n i s = Some r -> resolve_walk f' n i s = Some r;
  Mrecompute : forall f' n s r, f <= f' -> recompute f n s = Some r -> recompute f' n s = Some r;
  Mrun_memo : forall f' n s r, f <= f' -> run_memo f n s = Some r -> run_memo f' n s = Some r;
  Mexecute : forall f' n s r, f <= f' -> execute_effect f n s = Some r -> execute_effect f' n s = Some r;
  Mrun_effect : forall f' n s r, f <= f' -> run_effect f n s = Some r -> run_effect f' n s = Some r;
  Mbody : forall f' n s r, f <= f' -> run_effect_body f n s = Some r -> run_effect_body f' n s = Some r;
  Mdischarge : forall f' o s r, f <= f' -> discharge f o s = Some r -> discharge f' o s = Some r;
  Mretire : forall f' o s r, f <= f' -> retire f o s = Some r -> retire f' o s = Some r;
  Mdispose_scope : forall f' o s r, f <= f' -> dispose_scope f o s = Some r -> dispose_scope f' o s = Some r;
  Mteardown : forall f' o ls cs s r, f <= f' -> teardown f o ls cs s = Some r -> teardown f' o ls cs s = Some r;
  Mcleanup : forall f' o b e s r, f <= f' -> run_cleanup f o b e s = Some r -> run_cleanup f' o b e s = Some r;
  Mrelease : forall f' n s r, f <= f' -> release f n s = Some r -> release f' n s = Some r;
  Mdispose : forall f' n s r, f <= f' -> dispose_node f n s = Some r -> dispose_node f' n s = Some r;
  Mattach : forall f' n o s r, f <= f' -> attach f n o s = Some r -> attach f' n o s = Some r;
  Mwrite : forall f' n v s r, f <= f' -> write_signal f n v s = Some r -> write_signal f' n v s = Some r;
  Mrequest : forall f' s r, f <= f' -> request_flush f s = Some r -> request_flush f' s = Some r;
  Msettle : forall f' s r, f <= f' -> settle_flush f s = Some r -> settle_flush f' s = Some r;
  Mflush : forall f' s r, f <= f' -> run_flush f s = Some r -> run_flush f' s = Some r;
  Mdrain : forall f' s r, f <= f' -> drain f s = Some r -> drain f' s = Some r
}.

Local Ltac mono_step :=
  repeat match goal with
  | |- m_bind _ _ _ = Some _ => eapply bind_mono; [intros ?? ?; repeat mono_step|intros ??? ?]
  | |- hosted _ _ _ = Some _ => eapply hosted_mono; [intros ?? ?; repeat mono_step|]
  | |- untracked _ _ = Some _ => eapply untracked_mono; [intros ?? ?; repeat mono_step|]
  | |- detached _ _ _ = Some _ => eapply detached_mono; [intros ?? ?; repeat mono_step|]
  | H : m_step _ ?s = Some (?s', tt) |- m_step _ ?s = Some (?s', tt) =>
      unfold m_step in H |- *; destruct (_ ?s) eqn:?E; simplify_eq;
      first [erewrite mark_dirty_mono; [reflexivity|lia|exact E]
            |erewrite mark_check_mono; [reflexivity|lia|exact E]
            |erewrite notify_dirty_mono; [reflexivity|lia|exact E]
            |erewrite notify_dirty_except_mono; [reflexivity|lia|exact E]
            |idtac]
  | |- context[match ?x with _ => _ end] => destruct x eqn:?
  | |- (if ?b then _ else _) _ = Some _ => destruct b eqn:?
  end; try (eauto with lia; fail).

Lemma engine_mono_all f : engine_mono_at f.
Proof.
  induction f as [|f IH].
  - split; intros; try discriminate.
  - assert (Hle : forall f', S f <= f' -> exists g, f' = S g /\ f <= g).
    { intros [|g] H; [lia|]. exists g. split; [reflexivity|lia]. }
    split.
    + intros f' e c s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [exec] in H |- *.
      destruct c; repeat match goal with
      | |- context [on_node] => unfold on_node in H |- *
      | |- context [on_value] => unfold on_value in H |- *
      | |- context [match eval_expr ?e ?p ?x with _ => _ end] => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | |- (if decide _ then _ else stuck) _ = Some _ => destruct (decide _); try discriminate
      end; try exact H; try (by apply (Mexec _ IH)); try (destruct (decide _); by apply (Mexec _ IH)); revert H.
      * eapply bind_mono; [intros ??; by apply (Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; [by apply (Mexec _ IH)|exact id]].
      * eapply bind_mono; [intros ??; by apply (Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; by apply (Mexec _ IH)].
      * eapply bind_mono; [intros ??; by apply (Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; [by apply (Mexec _ IH)|exact id]].
      * eapply bind_mono; [intros ??; by apply (Mwrite _ IH)|intros [] ?? _; by apply (Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ??; by apply (Mattach _ IH)|intros [] ?? _; by apply (Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ??; by apply (Mattach _ IH)|intros [] ?? _].
        eapply bind_mono; [|intros [] ?? _].
        { intros ?? E. unfold m_step in E |- *. destruct (mark_dirty f _ _) eqn:Em; simplify_eq.
          erewrite mark_dirty_mono; [reflexivity|exact Hg|exact Em]. }
        eapply bind_mono; [intros ??; by apply (Mrequest _ IH)|intros [] ?? _; by apply (Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [|intros [] ?? _; by apply (Mexec _ IH)].
        intros ??; destruct (o_disposed x); [|exact id].
        eapply bind_mono; [intros ?? E; eapply detached_mono; [intros ??; by apply (Mcleanup _ IH)|exact E]|intros [] ?? _; by apply (Mrequest _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
        destruct (n_kind d); try discriminate;
          (eapply bind_mono; [intros ??; by apply (Mdispose _ IH)|intros [] ?? _];
           eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (Mexec _ IH)]).
      * eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (Mexec _ IH)|intros [|] ?? _];
          (eapply bind_mono; [intros ?? E; exact E|intros [] ?? _]);
          [eapply bind_mono; [intros ??; by apply (Mrequest _ IH)|intros [] ?? _; by apply (Mexec _ IH)]|exact id].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (Mexec _ IH)].
    + intros f' n b s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [read] in H |- *. destruct b; [by apply (Mread_node _ IH)|eapply untracked_mono; [intros ??; by apply (Mread_node _ IH)|exact H]].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [read_node] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [exact id| |exact id].
      eapply bind_mono; [|intros d' ?? _; exact id].
      intros ??; destruct (stale_live d); [|exact id].
      eapply bind_mono; [intros ??; by apply (Mrop _ IH)|intros d' ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d'' ?? _].
      destruct (stale_live d''); [|exact id].
      intros E; eapply mark_reader_mono; [exact Hg|exact E].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [update_if_necessary] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [exact id| |exact id].
      destruct (stale_live d); [|exact id].
      eapply bind_mono; [intros ??; by apply (Mrop _ IH)|intros ??? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [refresh_or_pull] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (deferring st0); [|by apply (Mpull _ IH)].
      eapply bind_mono; [intros ??; by apply (Mrefresh _ IH)|intros ??? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [pull] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (Mrefresh _ IH)|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      eapply bind_mono; [|intros ??? _; exact id].
      intros ??; destruct (_ && _); [by apply (Msettle _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [refresh] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (decide (n ∈ g_checking st0)).
      { intros E; eapply m_step_mark_dirty_mono; [exact Hg|exact E]. }
      destruct (decide (n ∈ g_running st0)); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (decide (n_freshness d = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (Mwalk _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        destruct (decide (n_freshness d' = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Mrecompute _ IH)|exact id].
    + intros f' n i s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [resolve_walk] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (decide (n_freshness d = Check)); [|exact id].
      destruct (source_slots (sources_of (g_edges st0) n) !! i) as [[src _]|].
      * eapply bind_mono; [intros ??; by apply (Mupdate _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d_src ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros st1 ?? _].
        eapply bind_mono; [|intros [] ?? _; by apply (Mwalk _ IH)].
        intros st2 a0; destruct (decide _); [|exact id]. intros E; eapply m_step_mark_dirty_mono; [exact Hg|exact E].
      * destruct (slot_holds stale_live st0 n); [by apply (Mwalk _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [recompute] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_scope d) as [o|]; [|by apply (Mrun_memo _ IH)].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (Mdischarge _ IH)|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      destruct (n_disposed d'); [exact id|].
      destruct (decide (n_runs d' = n_runs d)); [by apply (Mrun_memo _ IH)|].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Mrecompute _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [run_memo] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; eapply hosted_mono; [intros ??; by apply (Mexec _ IH)|exact E]|intros o ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d1 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d2 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros moved ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; destruct moved; [|exact id].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros st2 a0 E. eapply m_step_notify_dirty_except_mono; [exact Hg|exact E].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [execute_effect] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_disposed d); [exact id|].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (decide (n_freshness d = Check)); [|exact id].
        eapply bind_mono; [intros ??; by apply (Mwalk _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        destruct (decide (n_freshness d' = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Mrun_effect _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [run_effect] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (n_scope d) as [o|]; [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (Mdischarge _ IH)|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; destruct (n_disposed d'); [exact id|by apply (Mbody _ IH)].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [run_effect_body] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; eapply hosted_mono; [intros ??; by apply (Mexec _ IH)|exact E]|intros o ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [discharge] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; eapply detached_mono; [intros ??; by apply (Mdispose_scope _ IH)|exact E]|intros [] ?? _; exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [retire] in H |- *. revert H.
      eapply bind_mono; [|intros [] ?? _].
      { intros ?? E0. eapply detached_mono; [|exact E0]. intros st0 r0.
        eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
        destruct (o_disposed x); [exact id|].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (Mdispose_scope _ IH)]. }
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (live_queue st0); [by apply (Mrequest _ IH)|exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [dispose_scope] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (Mteardown _ IH)].
    + intros f' o ls cs s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [teardown] in H |- *. revert H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [exact id|..].
      * eapply bind_mono; [intros ??; by apply (Mcleanup _ IH)|intros [] ?? _; by apply (Mteardown _ IH)].
      * eapply bind_mono; [intros ??; by apply (Mrelease _ IH)|intros [] ?? _; by apply (Mteardown _ IH)].
      * destruct (decide (a < l_acquired l)).
        -- eapply bind_mono; [intros ??; by apply (Mrelease _ IH)|intros [] ?? _; by apply (Mteardown _ IH)].
        -- eapply bind_mono; [intros ??; by apply (Mcleanup _ IH)|intros [] ?? _; by apply (Mteardown _ IH)].
    + intros f' o b e s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [run_cleanup] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (Mexec _ IH)|intros [|] ?? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [release] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (Mdispose _ IH)].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [dispose_node] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_disposed d); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      destruct (n_scope d); [by apply (Mretire _ IH)|exact id].
    + intros f' n o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [attach] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      destruct (o_disposed x); [|exact id].
      eapply bind_mono; [intros ??; by apply (Mrelease _ IH)|intros [] ?? _; exact id].
    + intros f' n v s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [write_signal] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [|exact id|exact id].
      destruct (decide (n_value d = v)); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [|intros [] ?? _; by apply (Mrequest _ IH)].
      intros st2 a0 E. eapply m_step_notify_dirty_mono; [exact Hg|exact E].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [request_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (_ && _); [|exact id].
      destruct (outside_bodies st0); [by apply (Mflush _ IH)|exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [settle_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (_ && _); [by apply (Mflush _ IH)|exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [run_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (g_flushing st0); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (Mdrain _ IH)|intros [] ?? _; exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [drain] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (g_queue st0 !! g_queue_head st0); [|exact id].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (Mexecute _ IH)|intros [] ?? _; by apply (Mdrain _ IH)].
Qed.

Definition same_depths (s s' : state) : Prop :=
  g_pull_depth s' = g_pull_depth s /\ g_flushing s' = g_flushing s.

Lemma same_depths_refl s : same_depths s s.
Proof. split; reflexivity. Qed.

Lemma same_depths_trans s1 s2 s3 : same_depths s1 s2 -> same_depths s2 s3 -> same_depths s1 s3.
Proof. intros [A B] [C D]. split; congruence. Qed.

Record depths_kept_at (f : nat) : Prop := {
  Dexec : forall e c s s' r, exec f e c s = Some (s', r) -> same_depths s s';
  Dread : forall n b s s' r, read f n b s = Some (s', r) -> same_depths s s';
  Dread_node : forall n s s' r, read_node f n s = Some (s', r) -> same_depths s s';
  Dupdate : forall n s s' r, update_if_necessary f n s = Some (s', r) -> same_depths s s';
  Drop : forall n s s' r, refresh_or_pull f n s = Some (s', r) -> same_depths s s';
  Dpull : forall n s s' r, pull f n s = Some (s', r) -> same_depths s s';
  Drefresh : forall n s s' r, refresh f n s = Some (s', r) -> same_depths s s';
  Dwalk : forall n i s s' r, resolve_walk f n i s = Some (s', r) -> same_depths s s';
  Drecompute : forall n s s' r, recompute f n s = Some (s', r) -> same_depths s s';
  Drun_memo : forall n s s' r, run_memo f n s = Some (s', r) -> same_depths s s';
  Dexecute : forall n s s' r, execute_effect f n s = Some (s', r) -> same_depths s s';
  Drun_effect : forall n s s' r, run_effect f n s = Some (s', r) -> same_depths s s';
  Dbody : forall n s s' r, run_effect_body f n s = Some (s', r) -> same_depths s s';
  Ddischarge : forall o s s' r, discharge f o s = Some (s', r) -> same_depths s s';
  Dretire : forall o s s' r, retire f o s = Some (s', r) -> same_depths s s';
  Ddispose_scope : forall o s s' r, dispose_scope f o s = Some (s', r) -> same_depths s s';
  Dteardown : forall o ls cs s s' r, teardown f o ls cs s = Some (s', r) -> same_depths s s';
  Dcleanup : forall o b e s s' r, run_cleanup f o b e s = Some (s', r) -> same_depths s s';
  Drelease : forall n s s' r, release f n s = Some (s', r) -> same_depths s s';
  Ddispose : forall n s s' r, dispose_node f n s = Some (s', r) -> same_depths s s';
  Dattach : forall n o s s' r, attach f n o s = Some (s', r) -> same_depths s s';
  Dwrite : forall n v s s' r, write_signal f n v s = Some (s', r) -> same_depths s s';
  Drequest : forall s s' r, request_flush f s = Some (s', r) -> same_depths s s';
  Dsettle : forall s s' r, settle_flush f s = Some (s', r) -> same_depths s s';
  Dflush : forall s s' r, run_flush f s = Some (s', r) -> same_depths s s';
  Ddrain : forall s s' r, drain f s = Some (s', r) -> same_depths s s'
}.

Lemma bind_depths {A B} (m : M A) (k : A -> M B) s s' b :
  (forall s1 a, m s = Some (s1, a) -> same_depths s s1) ->
  (forall a s1, m s = Some (s1, a) -> k a s1 = Some (s', b) -> same_depths s1 s') ->
  m_bind m k s = Some (s', b) -> same_depths s s'.
Proof.
  intros Hm Hk Hb. unfold m_bind in Hb.
  destruct (m s) as [[s1 a]|] eqn:E; [|discriminate].
  eapply same_depths_trans; [by eapply Hm|by eapply Hk].
Qed.

Lemma bind_depths_of {A B} {m : M A} {k : A -> M B} {s s' b}
    (Hb : m_bind m k s = Some (s', b)) :
  (forall s1 a, m s = Some (s1, a) -> same_depths s s1) ->
  (forall a s1, m s = Some (s1, a) -> k a s1 = Some (s', b) -> same_depths s1 s') ->
  same_depths s s'.
Proof. intros Hm Hk. exact (bind_depths m k s s' b Hm Hk Hb). Qed.

Lemma marks_same_depths s s' : rtc mark_prim s s' -> same_depths s s'.
Proof.
  induction 1 as [s|s1 s2 s3 Hp _ IH]; [apply same_depths_refl|].
  eapply same_depths_trans; [|exact IH]. destruct Hp; split; reflexivity.
Qed.

Lemma hosted_depths {A} n (b : M A) s s' r :
  (forall s0 s1 a, b s0 = Some (s1, a) -> same_depths s0 s1) ->
  hosted n b s = Some (s', r) -> same_depths s s'.
Proof.
  intros Hb H. unfold hosted in H.
  eapply (bind_depths_of H); [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity| ].
  intros [] s1 E1. eapply bind_depths; [intros ?? E2; by eapply Hb|].
  intros a s2 E2 H3. unfold m_bind, m_modify, m_return in H3. simplify_eq. split; reflexivity.
Qed.

Lemma untracked_depths {A} (b : M A) s s' r :
  (forall s0 s1 a, b s0 = Some (s1, a) -> same_depths s0 s1) ->
  untracked b s = Some (s', r) -> same_depths s s'.
Proof.
  intros Hb H. unfold untracked in H.
  eapply (bind_depths_of H); [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity| ].
  intros [] s1 E1. eapply bind_depths; [intros ?? E2; by eapply Hb|].
  intros a s2 E2 H3. unfold m_bind, m_modify, m_return in H3. simplify_eq. split; reflexivity.
Qed.

Lemma detached_depths o (b : M unit) s s' r :
  (forall s0 s1 a, b s0 = Some (s1, a) -> same_depths s0 s1) ->
  detached o b s = Some (s', r) -> same_depths s s'.
Proof.
  intros Hb H. unfold detached in H.
  eapply (bind_depths_of H); [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity| ].
  intros [] s1 E1. eapply bind_depths; [intros ?? E2; by eapply Hb|].
  intros [] s2 E2 H3. unfold m_bind, m_modify in H3. simplify_eq. split; reflexivity.
Qed.

Lemma log_label_depths s s' a : log_label s = Some (s', a) -> same_depths s s'.
Proof.
  intro H. unfold log_label in H.
  destruct (g_current s), (g_current_owner s); simplify_eq; apply same_depths_refl.
Qed.

Lemma mark_reader_depths f s s' a : mark_reader f s = Some (s', a) -> same_depths s s'.
Proof.
  intro H. unfold mark_reader in H.
  destruct (g_current s), (f_reader_mark (g_flags s)); try (simplify_eq; apply same_depths_refl).
  destruct (mark_check f _ s) eqn:Em; simplify_eq. by eapply marks_same_depths, mark_check_rtc.
Qed.

Lemma engine_depths_kept f : depths_kept_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - assert (Htrack : forall n s s' a, track n s = Some (s', a) -> same_depths s s').
    { intros n s s' a Ht. unfold track in Ht. destruct (live_current s); [|simplify_eq; apply same_depths_refl].
      destruct (sources_add _ _ _); [|discriminate]. simplify_eq. unfold ghost_read. destruct (g_nodes (with_edges _ _) !! n); split; reflexivity. }
    assert (Hcur : forall s s' a, current_owner s = Some (s', a) -> same_depths s s').
    { intros s s' a Hc. unfold current_owner in Hc.
      destruct (g_current_owner s), (g_current s); try discriminate; try (simplify_eq; apply same_depths_refl).
      destruct (g_nodes s !! _); [|discriminate]. destruct (n_scope _); [simplify_eq; split; reflexivity|].
      destruct (match n_kind _, n_mode _ with KMemo, Pure => true | _, _ => false end); simplify_eq; split; reflexivity. }
    assert (Hunlink : forall l s s' a, unlink l s = Some (s', a) -> same_depths s s').
    { intros l s s' a Hu. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return in Hu.
      destruct (g_owners s !! l_owner l); [|simplify_eq]. destruct (decide _); simplify_eq; split; reflexivity. }
    assert (Hrec : forall o y s s' a, record_error o y s = Some (s', a) -> same_depths s s').
    { intros o y s s' a Hr. unfold record_error, m_bind, get_owner, put_owner, m_modify in Hr.
      destruct (g_owners s !! o) as [x|]; [|simplify_eq]. destruct (o_sink x) as [p|]; [destruct (g_owners s !! p)|]; simplify_eq; split; reflexivity. }
    assert (Hghost : forall n s s' a, (let* d3 := get_node n in if decide (n_freshness d3 = Clean) then m_return tt else announce n) s = Some (s', a) -> same_depths s s').
    { intros n s s' a Hg. unfold m_bind, get_node, announce, modify_node, m_return in Hg.
      destruct (g_nodes s !! n); [|discriminate]. destruct (decide _); [simplify_eq; split; reflexivity|].
      destruct (f_unannounced _); [destruct (g_nodes s !! n); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity]. }
    split.
    + intros e c s s' r H. cbn [exec] in H.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0
      | H0 : context [on_value] |- _ => unfold on_value in H0
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (unfold m_return in H; simplify_eq; apply same_depths_refl); try (simplify_eq; apply same_depths_refl).
      * eapply (Dexec _ IH); exact H.
      * destruct (decide _); eapply (Dexec _ IH); exact H.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; [eapply (Dexec _ IH); exact H3|unfold m_return in H3; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; eapply (Dexec _ IH); exact H3.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; [eapply (Dexec _ IH); exact H3|unfold m_return in H3; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dwrite _ IH); exact H__d|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d]].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ??; eapply bind_depths; [intros ??; apply Hcur|intros [o|] ??; [eapply bind_depths; [intros ??; intro H__d; eapply (Dattach _ IH); exact H__d|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d]|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]]]].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ??; eapply bind_depths; [intros ??; apply Hcur|intros [o|] ??; [|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]]]].
        eapply bind_depths; [intros ??; intro H__d; eapply (Dattach _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold m_step in E; destruct (mark_dirty f _ _) eqn:Em; simplify_eq; by eapply marks_same_depths, mark_dirty_rtc|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (Drequest _ IH); exact H__d|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ??; apply Hcur|intros [o|] ??; [|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]].
        eapply bind_depths; [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ??].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d].
        intros ??; destruct (o_disposed x); [|intros E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity].
        eapply bind_depths; [intros ??; eapply detached_depths; intros ???; intro H__d; eapply (Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (Drequest _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! _); simplify_eq; apply same_depths_refl|intros d ?? ].
        destruct (n_kind d); try discriminate;
          (eapply bind_depths; [intros ??; intro H__d; eapply (Ddispose _ IH); exact H__d|intros [] ??];
           eapply bind_depths; [intros ?? E; destruct (n_disposed d); unfold emit, m_modify, m_return in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d]).
      * eapply (bind_depths_of H); [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? ].
        eapply bind_depths; [intros ??; intro H__d; eapply (Dexec _ IH); exact H__d|intros o ??].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        destruct o; [eapply bind_depths; [intros ??; intro H__d; eapply (Drequest _ IH); exact H__d|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d]|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; apply log_label_depths|intros ??? ].
        eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Dexec _ IH); exact H__d].
    + intros n b s s' r H. cbn [read] in H. destruct b; [by eapply (Dread_node _ IH)|eapply untracked_depths; [intros ???; intro H__d; eapply (Dread_node _ IH); exact H__d|exact H]].
    + intros n s s' r H. cbn [read_node] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [eapply bind_depths; [intros ??; apply Htrack|intros [] ?? E; unfold m_return in E; simplify_eq; apply same_depths_refl]| |discriminate].
      eapply bind_depths; [|intros d' ?? E; destruct (n_status d'); unfold m_return in E; simplify_eq; apply same_depths_refl].
      intros ??; destruct (stale_live d).
      * eapply bind_depths; [intros ??; intro H__d; eapply (Drop _ IH); exact H__d|intros d' ??].
        eapply bind_depths; [|intros [] ?? E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        intros ??; eapply bind_depths; [intros ??; apply Htrack|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d'' ??].
        destruct (stale_live d''); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        apply mark_reader_depths.
      * eapply bind_depths; [intros ??; apply Htrack|intros [] ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [update_if_necessary] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl| |intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (stale_live d); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      eapply bind_depths; [intros ??; intro H__d; eapply (Drop _ IH); exact H__d|intros ??? E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [refresh_or_pull] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros s0 ?? ].
      destruct (deferring s0); [|by apply (Dpull _ IH)].
      eapply bind_depths; [intros ??; intro H__d; eapply (Drefresh _ IH); exact H__d|intros [] ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [pull] in H.
      eapply bind_inv in H as (s0 & [] & E0 & H). unfold m_modify in E0; simplify_eq.
      eapply bind_inv in H as (s1 & [] & Er & H).
      eapply bind_inv in H as (s2 & d & Eg & H). unfold get_node in Eg. destruct (g_nodes s1 !! n); [|discriminate]. injection Eg as <- <-.
      eapply bind_inv in H as (s3 & [] & E3 & H). unfold m_modify in E3; simplify_eq.
      rewrite bind_get in H.
      eapply bind_inv in H as (s4 & [] & Es & H). unfold m_return in H; simplify_eq.
      pose proof (Drefresh _ IH n _ _ _ Er) as [Hp1 Hf1].
      set (s3 := with_pull_depth (pred (g_pull_depth s1)) s1) in *.
      assert (Hp3 : g_pull_depth s3 = g_pull_depth s) by (unfold s3; simpl; rewrite Hp1; simpl; lia).
      assert (Hf3 : g_flushing s3 = g_flushing s) by (unfold s3; simpl; exact Hf1).
      destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3).
      * pose proof (Dsettle _ IH _ _ _ Es) as [Hp4 Hf4]. split; congruence.
      * unfold m_return in Es; simplify_eq. split; assumption.
    + intros n s s' r H. cbn [refresh] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros s0 ?? ].
      destruct (decide (n ∈ g_checking s0)).
      { intros E. unfold m_step in E. destruct (mark_dirty f n _) eqn:Em; simplify_eq. by eapply marks_same_depths, mark_dirty_rtc. }
      destruct (decide (n ∈ g_running s0)); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ??].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold push_checking, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (Dwalk _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold pop_checking, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        destruct (decide (n_freshness d' = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Drecompute _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n i s s' r H. cbn [resolve_walk] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (source_slots (sources_of (g_edges st0) n) !! i) as [[src _]|].
      * eapply bind_depths; [intros ??; intro H__d; eapply (Dupdate _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! src); simplify_eq; apply same_depths_refl|intros d_src ??].
        eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st1 ??].
        eapply bind_depths; [|intros [] ?? H__d; eapply (Dwalk _ IH); exact H__d].
        intros st_ [] E; destruct (decide _); [|unfold m_return in E; simplify_eq; apply same_depths_refl].
        unfold m_step in E. destruct (mark_dirty f n _) eqn:Em; simplify_eq. by eapply marks_same_depths, mark_dirty_rtc.
      * destruct (slot_holds stale_live st0 n); [by apply (Dwalk _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [recompute] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_scope d) as [o|]; [|by apply (Drun_memo _ IH)].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (Ddischarge _ IH); exact H__d|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      destruct (n_disposed d').
      { destruct (decide (n_freshness d' = Clean)); [intros E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl]. }
      destruct (decide (n_runs d' = n_runs d)); [by apply (Drun_memo _ IH)|].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Drecompute _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [run_memo] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold push_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; eapply hosted_depths; intros ???; intro H__d; eapply (Dexec _ IH); exact H__d|intros o ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d1 ??].
      eapply bind_depths; [intros ?? E; destruct (if n_violated d1 then Fail pure_scope_failure else o); unfold modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (_ (g_edges _)); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold pop_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; destruct (if n_violated d1 then Fail pure_scope_failure else o); unfold modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d2 ??].
      eapply bind_depths; [intros ?? E; destruct (decide _); [unfold m_return in E; simplify_eq; apply same_depths_refl|destruct (n_comparer d2); unfold m_bind, modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]|intros mv ??].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      eapply bind_depths; [intros ?? E; unfold m_modify, ghost_run_end in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [|intros [] ??; intro Hg_; eapply Hghost; exact Hg_].
      intros ??; destruct mv; [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      eapply bind_depths; [intros st_ [] E; unfold m_step in E; destruct (notify_dirty_except f n (g_current st0) _) eqn:Em; simplify_eq; by eapply marks_same_depths, (any_mark_rtc f n (g_current st0)), or_intror, or_intror, or_intror, or_intror|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st1 ??].
      destruct (_ && _); [intros E; unfold m_modify in E; simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [execute_effect] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_disposed d); [intros E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ??; intro H__d; eapply (Dwalk _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        destruct (decide (n_freshness d' = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      destruct (decide (n_freshness d' = Dirty)); [by apply (Drun_effect _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [run_effect] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold push_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? ].
      eapply bind_depths; [intros ?? E; unfold edges, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ??].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (n_scope d) as [o|]; [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (Ddischarge _ IH); exact H__d|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [|intros [] ?? E; unfold pop_running, m_modify in E; simplify_eq; split; reflexivity].
      intros ??; destruct (n_disposed d'); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|by apply (Dbody _ IH)].
    + intros n s s' r H. cbn [run_effect_body] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; eapply hosted_depths; intros ???; intro H__d; eapply (Dexec _ IH); exact H__d|intros o ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (_ (g_edges _)); simplify_eq; split; reflexivity|intros [] ??].
      destruct o; [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity].
    + intros o s s' r H. cbn [discharge] in H.
      eapply (bind_depths_of H); [intros ??; eapply detached_depths; intros ???; intro H__d; eapply (Ddispose_scope _ IH); exact H__d|intros [] ?? ].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (_ && _); [intros E; unfold m_modify in E; simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros o s s' r H. cbn [retire] in H.
      eapply (bind_depths_of H); [|intros [] ?? ].
      { intros ?? E. eapply detached_depths; [|exact E]. intros ???.
        eapply bind_depths; [intros ?? E1; unfold get_owner in E1; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ??].
        destruct (o_disposed x); [intros E1; unfold m_return in E1; simplify_eq; apply same_depths_refl|].
        eapply bind_depths; [intros ?? E1; unfold put_owner, m_modify in E1; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Ddispose_scope _ IH); exact H__d]. }
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (live_queue st0); [by apply (Drequest _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros o s s' r H. cbn [dispose_scope] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      eapply bind_depths; [intros ?? E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Dteardown _ IH); exact H__d].
    + intros o ls cs s s' r H. cbn [teardown] in H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs'].
      * unfold m_return in H. simplify_eq. apply same_depths_refl.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (Dteardown _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Drelease _ IH); exact H__d|intros [] ?? H__d; eapply (Dteardown _ IH); exact H__d].
      * destruct (decide _); [eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Drelease _ IH); exact H__d|intros [] ?? H__d; eapply (Dteardown _ IH); exact H__d]|eapply (bind_depths_of H); [intros ??; intro H__d; eapply (Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (Dteardown _ IH); exact H__d]].
    + intros o b e s s' r H. cbn [run_cleanup] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (Dexec _ IH); exact H__d|intros [|] ??].
      * intros E; unfold m_return in E; simplify_eq; apply same_depths_refl.
      * eapply bind_depths; [intros ??; apply Hrec|intros [] ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity].
    + intros n s s' r H. cbn [release] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (Ddispose _ IH); exact H__d].
    + intros n s s' r H. cbn [dispose_node] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_disposed d); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; destruct (n_link d); [apply Hunlink|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl]|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (sources_clear _ _); simplify_eq; split; reflexivity|intros [] ??].
      destruct (n_scope d); [by apply (Dretire _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n o s s' r H. cbn [attach] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      destruct (o_disposed x).
      * eapply bind_depths; [intros ??; intro H__d; eapply (Drelease _ IH); exact H__d|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
      * eapply bind_depths; [intros ?? E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
    + intros n v s s' r H. cbn [write_signal] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify, ghost_publish in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_step in E; destruct (notify_dirty f n _) eqn:Em; simplify_eq; by eapply marks_same_depths, (any_mark_rtc f n None), or_intror, or_intror, or_intror, or_introl|intros [] ?? H__d; eapply (Drequest _ IH); exact H__d].
    + intros s s' r H. cbn [request_flush] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (_ && _); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (outside_bodies st0); [by apply (Dflush _ IH)|].
      destruct (g_flushing st0); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|intros E; unfold m_modify in E; simplify_eq; split; reflexivity].
    + intros s s' r H. cbn [settle_flush] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (_ && _); [by apply (Dflush _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros s s' r H. cbn [run_flush] in H.
      rewrite bind_get in H.
      destruct (g_flushing s) eqn:Ef; [unfold m_return in H; simplify_eq; apply same_depths_refl|].
      eapply bind_inv in H as (st1 & [] & E1 & H). unfold m_modify in E1; simplify_eq.
      eapply bind_inv in H as (s2 & [] & Ed & H). unfold m_modify in H; simplify_eq.
      pose proof (Ddrain _ IH _ _ _ Ed) as [Hp2 _]. split; [exact Hp2|simpl; symmetry; exact Ef].
    + intros s s' r H. cbn [drain] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (g_queue st0 !! g_queue_head st0); [|intros E; unfold m_modify in E; simplify_eq; split; reflexivity].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (Dexecute _ IH); exact H__d|intros [] ?? H__d; eapply (Ddrain _ IH); exact H__d].
Qed.

(** ** B1. Flushing implies no flush owed and Deferring equivalence *)

(**md
**Whenever `g_flushing` is `true` under `sched_inv` (and hence after any `run_top` from `init`), `g_flush_owed` is `false`.**

`run_flush` clears `g_flush_owed` to `false` before setting `g_flushing := true`, and `request_flush`, `run_memo`, and `discharge` only set `g_flush_owed := true` when `g_flushing = false`. Consequently `sc_owed` in `sched_inv` rules out any owed flush while a flush is running.

- [x:] optional in-progress effect for `sched_inv`.
- [s:] any state satisfying `sched_inv x s`.
- [f:] fuel for `run_top`.
- [fl:] initial graph flags.
- [c:] whole program executed from `init fl`.
- [s':] final state after `run_top`.
- [r:] outcome of `run_top`.
*)
Theorem flushing_implies_no_flush_owed f fl c s' r :
  (forall x s, sched_inv x s -> g_flushing s = true -> g_flush_owed s = false) /\
  (run_top f c (init fl) = Some (s', r) -> g_flushing s' = true -> g_flush_owed s' = false).
Proof.
  assert (Hinv : forall x s, sched_inv x s -> g_flushing s = true -> g_flush_owed s = false).
  { intros x s (_ & [_ Howed _] & _) Hfl.
    destruct (g_flush_owed s) eqn:Eo; [|reflexivity].
    pose proof (Howed eq_refl). congruence. }
  split; [exact Hinv|].
  intros Hrun Hfl.
  exact (Hinv None s' (sched_inv_run_top f c _ s' r (sched_inv_init fl) Hrun) Hfl).
Qed.

(**md
`Graph.Deferring` with `g_flushing` included: defers nested flushes when a flush is active, a pull is active, or a computation body is current.
*)
Definition deferring_b1 (s : state) : bool := g_flushing s || deferring s.

(** Engine interpreter identical to `Engine.v` except `b1_refresh_or_pull` uses `deferring_b1`. *)
Fixpoint b1_exec (fuel : nat) (e : env) (c : cmd) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    match c with
    | CRet x => on_value e x (fun v => m_return (Ok v))
    | CThrow y => m_return (Fail y)
    | CLet x v k => on_value e v (fun z => b1_exec f ((x, z) :: e) k)
    | CIf v a b => on_value e v (fun z => if decide (z = 0%Z) then b1_exec f e b else b1_exec f e a)
    | CRead x n k =>
      on_node e n (fun t =>
        let* o := b1_read f t true in
        let* _ := note_host_read t o in
        match o with Ok v => b1_exec f ((x, v) :: e) k | Fail y => m_return (Fail y) end)
    | CTryRead x ok n k =>
      on_node e n (fun t =>
        let* o := b1_read f t true in
        let* _ := note_host_read t o in
        match o with
        | Ok v => b1_exec f ((ok, 1%Z) :: (x, v) :: e) k
        | Fail y => b1_exec f ((ok, 0%Z) :: (x, y) :: e) k
        end)
    | CReadUntracked x n k =>
      on_node e n (fun t =>
        let* o := b1_read f t false in
        let* _ := note_host_read t o in
        match o with Ok v => b1_exec f ((x, v) :: e) k | Fail y => m_return (Fail y) end)
    | CWrite n v k =>
      on_node e n (fun t => on_value e v (fun z => let* _ := b1_write_signal f t z in b1_exec f e k))
    | CSignal x v k =>
      on_value e v (fun z =>
        let* n := next_id in
        let* _ := add_node n (signal_node z) in
        b1_exec f ((x, Z.of_nat n) :: e) k)
    | CMemo x m q b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (memo_node m q b e') in
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o => let* _ := b1_attach f n o in b1_exec f e' k
      end
    | CEffect x b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (effect_node b e') in
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o =>
        let* _ := b1_attach f n o in
        let* _ := m_step (mark_dirty f n) in
        let* _ := b1_request_flush f in
        b1_exec f e' k
      end
    | CCleanup b k =>
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o =>
        let* x := get_owner o in
        let* _ := m_modify (ghost_registered (o_host x)) in
        let* _ :=
          if o_disposed x then let* _ := detached o (b1_run_cleanup f o b e) in b1_request_flush f
          else put_owner o (owner_with_cleanup b e x) in
        b1_exec f e k
      end
    | CDispose n k =>
      on_node e n (fun t =>
        let* d := get_node t in
        match n_kind d with
        | KSignal => stuck
        | _ =>
          let* _ := b1_dispose_node f t in
          let* _ := if n_disposed d then m_return tt else emit (EvDispose t) in
          b1_exec f e k
        end)
    | CBatch b k =>
      let* _ := m_modify (fun s => with_batch_depth (S (g_batch_depth s)) s) in
      let* r := b1_exec f e b in
      let* _ := m_modify (fun s => with_batch_depth (pred (g_batch_depth s)) s) in
      match r with
      | Ok _ => let* _ := b1_request_flush f in b1_exec f e k
      | Fail y => m_return (Fail y)
      end
    | CLog v k =>
      on_value e v (fun z => let* l := log_label in let* _ := emit (EvLog l z) in b1_exec f e k)
    end
  end

with b1_read (fuel : nat) (n : node_id) (tracked : bool) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f => if tracked then b1_read_node f n else untracked (b1_read_node f n)
  end

with b1_read_node (fuel : nat) (n : node_id) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal => let* _ := track n in m_return (Ok (n_value d))
    | KEffect => stuck
    | KMemo =>
      let* d' :=
        if stale_live d then
          let* d' := b1_refresh_or_pull f n in
          let* _ :=
            let* _ := track n in
            let* d'' := get_node n in
            if stale_live d'' then mark_reader f else m_return tt in
          m_return d'
        else let* _ := track n in get_node n in
      match n_status d' with
      | StError => m_return (Fail (default 0%Z (n_failure d')))
      | _ => m_return (Ok (n_value d'))
      end
    end
  end

with b1_update_if_necessary (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KMemo => if stale_live d then let* _ := b1_refresh_or_pull f n in m_return tt else m_return tt
    | _ => m_return tt
    end
  end

with b1_refresh_or_pull (fuel : nat) (n : node_id) {struct fuel} : M node :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if deferring_b1 s then let* _ := b1_refresh f n in get_node n else b1_pull f n
  end

with b1_pull (fuel : nat) (n : node_id) {struct fuel} : M node :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := m_modify (fun s => with_pull_depth (S (g_pull_depth s)) s) in
    let* _ := b1_refresh f n in
    let* d := get_node n in
    let* _ := m_modify (fun s => with_pull_depth (pred (g_pull_depth s)) s) in
    let* s := m_get in
    let* _ := if bool_decide (g_pull_depth s = 0%nat) && g_flush_owed s then b1_settle_flush f else m_return tt in
    m_return d
  end

with b1_refresh (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s0 := m_get in
    if decide (n ∈ g_checking s0) then m_step (mark_dirty f n)
    else if decide (n ∈ g_running s0) then m_return tt
    else
      let* d := get_node n in
      let* _ :=
        if decide (n_freshness d = Check) then
          let* _ := push_checking n in
          let* _ := b1_resolve_walk f n 0 in
          let* _ := pop_checking in
          let* d' := get_node n in
          if decide (n_freshness d' = Check) then
            let* _ := m_modify (ghost_resolved n) in
            modify_node n (fun x => node_with_unannounced false (node_with_freshness Clean x))
          else m_return tt
        else m_return tt in
      let* d' := get_node n in
      if decide (n_freshness d' = Dirty) then b1_recompute f n else m_return tt
  end

with b1_resolve_walk (fuel : nat) (n : node_id) (i : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* s := m_get in
    if decide (n_freshness d = Check) then
      match source_slots (sources_of (g_edges s) n) !! i with
      | Some (src, _) =>
        let* _ := b1_update_if_necessary f src in
        let* d' := get_node n in
        let* d_src := get_node src in
        let* s1 := m_get in
        let* _ :=
          if decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s1 \/ src ∈ g_checking s1))
          then m_step (mark_dirty f n) else m_return tt in
        b1_resolve_walk f n (S i)
      | None => if slot_holds stale_live s n then b1_resolve_walk f n 0 else m_return tt
      end
    else m_return tt
  end

with b1_recompute (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_scope d with
    | None => b1_run_memo f n
    | Some o =>
      let* _ := modify_node n (node_with_freshness Clean) in
      let* _ := m_modify (ghost_dirty_cleared n (n_runs d)) in
      let* _ := b1_discharge f o in
      let* d' := get_node n in
      if n_disposed d' then
        (if decide (n_freshness d' = Clean) then modify_node n (node_with_unannounced false) else m_return tt)
      else if decide (n_runs d' = n_runs d) then b1_run_memo f n
      else if decide (n_freshness d' = Dirty) then b1_recompute f n
      else m_return tt
    end
  end

with b1_run_memo (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let previous := n_value d in
    let previous_status := n_status d in
    let previous_failure := n_failure d in
    let* _ := modify_node n (fun x =>
      node_with_runs (S (n_runs x)) (node_with_failure None (node_with_status StNone
        (node_with_unannounced false (node_with_freshness Clean x))))) in
    let* _ := edges (sources_begin_run n) in
    let* _ := m_modify (ghost_run_start n) in
    let* _ := push_running n in
    let body_env := if n_published d then (prev_var, previous) :: n_env d else without_prev (n_env d) in
    let* r := hosted n (b1_exec f body_env (n_body d)) in
    let* d1 := get_node n in
    let result := if n_violated d1 then Fail pure_scope_failure else r in
    let* _ :=
      match result with
      | Ok v => modify_node n (fun x => node_with_published true (node_with_value v x))
      | Fail _ => m_return tt
      end in
    let* _ := edges_opt (if n_disposed d1 then sources_clear n else sources_end_run n) in
    let* _ := pop_running in
    let* _ :=
      match result with
      | Ok _ => m_return tt
      | Fail y => modify_node n (fun x => node_with_violated false (node_with_status StError (node_with_failure (Some y) x)))
      end in
    let* d2 := get_node n in
    let* moved :=
      if decide (n_status d2 <> previous_status \/ n_failure d2 <> previous_failure) then m_return true
      else
        match n_comparer d2 with
        | CmpEq => m_return (bool_decide (n_value d2 <> previous))
        | CmpNever => m_return true
        | CmpThrows y =>
          let* _ := modify_node n (fun x => node_with_status StError (node_with_failure (Some y) (node_with_value previous x))) in
          m_return true
        end in
    let* s := m_get in
    let* _ := m_modify (ghost_run_end n (g_current s) moved) in
    let* _ :=
      if moved then
        let* _ := m_step (notify_dirty_except f n (g_current s)) in
        let* s' := m_get in
        if bool_decide (g_batch_depth s' = 0%nat) && negb (g_flushing s') && live_queue s'
        then m_modify (with_flush_owed true) else m_return tt
      else m_return tt in
    let* d3 := get_node n in
    if decide (n_freshness d3 = Clean) then m_return tt else announce n
  end

with b1_execute_effect (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then modify_node n (node_with_queued false)
    else
      let* _ :=
        if decide (n_freshness d = Check) then
          let* _ := b1_resolve_walk f n 0 in
          let* d' := get_node n in
          if decide (n_freshness d' = Check) then
            let* _ := m_modify (ghost_resolved n) in modify_node n (node_with_freshness Clean)
          else m_return tt
        else m_return tt in
      let* d' := get_node n in
      let* _ := modify_node n (fun x => node_with_freshness Clean (node_with_queued false x)) in
      if decide (n_freshness d' = Dirty) then b1_run_effect f n else m_return tt
  end

with b1_run_effect (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := push_running n in
    let* _ := edges (sources_begin_run n) in
    let* _ := m_modify (ghost_effect_start n) in
    let* d := get_node n in
    let* _ :=
      match n_scope d with
      | Some o =>
        let* _ := modify_node n (node_with_in_cleanup true) in
        let* _ := b1_discharge f o in
        modify_node n (node_with_in_cleanup false)
      | None => m_return tt
      end in
    let* d' := get_node n in
    let* _ := if n_disposed d' then m_return tt else b1_run_effect_body f n in
    pop_running
  end

with b1_run_effect_body (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* _ := modify_node n (fun x => node_with_runs (S (n_runs x)) (node_with_failure None (node_with_status StNone x))) in
    let* _ := emit (EvRun n) in
    let* r := hosted n (b1_exec f (n_env d) (n_body d)) in
    let* d' := get_node n in
    let* _ := edges_opt (if n_disposed d' then sources_clear n else sources_end_run n) in
    match r with
    | Ok _ => m_return tt
    | Fail y =>
      let* _ := modify_node n (fun x => node_with_status StError (node_with_failure (Some y) x)) in
      emit (EvFail n y)
    end
  end

with b1_discharge (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (b1_dispose_scope f o) in
    let* s := m_get in
    if bool_decide (g_batch_depth s = 0%nat) && negb (g_flushing s) && live_queue s
    then m_modify (with_flush_owed true) else m_return tt
  end

with b1_retire (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (
      let* x := get_owner o in
      if o_disposed x then m_return tt
      else let* _ := put_owner o (owner_retired x) in b1_dispose_scope f o) in
    let* s := m_get in
    if live_queue s then b1_request_flush f else m_return tt
  end

with b1_dispose_scope (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := put_owner o (owner_detached x) in
    b1_teardown f o (rev (o_children x)) (owner_cleanups x)
  end

with b1_teardown (fuel : nat) (o : owner_id) (ls : list link) (cs : list (cmd * env * nat)) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    match ls, cs with
    | [], [] => m_return tt
    | l :: ls', [] => let* _ := b1_release f (l_child l) in b1_teardown f o ls' []
    | [], (b, e, _) :: cs' => let* _ := b1_run_cleanup f o b e in b1_teardown f o [] cs'
    | l :: ls', (b, e, a) :: cs' =>
      if decide (a < l_acquired l)%nat then
        let* _ := b1_release f (l_child l) in b1_teardown f o ls' cs
      else let* _ := b1_run_cleanup f o b e in b1_teardown f o ls cs'
    end
  end

with b1_run_cleanup (fuel : nat) (o : owner_id) (b : cmd) (e : env) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := emit (EvCleanup (o_host x)) in
    let* r := b1_exec f e b in
    match r with
    | Ok _ => m_return tt
    | Fail y => let* _ := record_error o y in emit (EvFail (o_host x) y)
    end
  end

with b1_release (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f => let* _ := modify_node n (node_with_link None) in b1_dispose_node f n
  end

with b1_dispose_node (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then m_return tt
    else
      let* _ := modify_node n (fun x => node_with_link None (node_with_disposed true x)) in
      let* _ := match n_link d with Some l => unlink l | None => m_return tt end in
      let* _ := edges_opt (sources_clear n) in
      match n_scope d with Some o => b1_retire f o | None => m_return tt end
  end

with b1_attach (fuel : nat) (n : node_id) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    if o_disposed x then
      let* _ := b1_release f n in
      modify_node n (node_with_link (Some {| l_child := n; l_owner := o; l_generation := None; l_acquired := 0 |}))
    else
      let l := {| l_child := n; l_owner := o; l_generation := Some (o_generation x); l_acquired := o_acquisitions x |} in
      let* _ := put_owner o (owner_with_child l x) in
      modify_node n (node_with_link (Some l))
  end

with b1_write_signal (fuel : nat) (n : node_id) (v : val) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      if decide (n_value d = v) then m_return tt
      else
        let* _ := modify_node n (node_with_value v) in
        let* _ := m_modify (ghost_publish n) in
        let* _ := m_step (notify_dirty f n) in
        b1_request_flush f
    | _ => stuck
    end
  end

with b1_request_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if live_queue s && bool_decide (g_batch_depth s = 0%nat) then
      if outside_bodies s then b1_run_flush f
      else if g_flushing s then m_return tt
      else m_modify (with_flush_owed true)
    else m_return tt
  end

with b1_settle_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_flush_owed s && bool_decide (g_batch_depth s = 0%nat) && outside_bodies s then b1_run_flush f else m_return tt
  end

with b1_run_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_flushing s then m_return tt
    else
      let* _ := m_modify (fun s => with_flush_owed false (with_flushing true s)) in
      let* _ := b1_drain f in
      m_modify (with_flushing false)
  end

with b1_drain (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    match g_queue s !! g_queue_head s with
    | Some n =>
      let* _ := m_modify (with_queue_head (S (g_queue_head s))) in
      let* _ := b1_execute_effect f n in
      b1_drain f
    | None => m_modify (fun s => with_queue [] (with_queue_head 0 s))
    end
  end.

Definition b1_run_top (fuel : nat) (c : cmd) : M outcome := b1_exec fuel [] c.

Record b1_mono_at (f : nat) : Prop := {
  B1Mexec : forall f' e c s r, f <= f' -> b1_exec f e c s = Some r -> b1_exec f' e c s = Some r;
  B1Mread : forall f' n b s r, f <= f' -> b1_read f n b s = Some r -> b1_read f' n b s = Some r;
  B1Mread_node : forall f' n s r, f <= f' -> b1_read_node f n s = Some r -> b1_read_node f' n s = Some r;
  B1Mupdate : forall f' n s r, f <= f' -> b1_update_if_necessary f n s = Some r -> b1_update_if_necessary f' n s = Some r;
  B1Mrop : forall f' n s r, f <= f' -> b1_refresh_or_pull f n s = Some r -> b1_refresh_or_pull f' n s = Some r;
  B1Mpull : forall f' n s r, f <= f' -> b1_pull f n s = Some r -> b1_pull f' n s = Some r;
  B1Mrefresh : forall f' n s r, f <= f' -> b1_refresh f n s = Some r -> b1_refresh f' n s = Some r;
  B1Mwalk : forall f' n i s r, f <= f' -> b1_resolve_walk f n i s = Some r -> b1_resolve_walk f' n i s = Some r;
  B1Mrecompute : forall f' n s r, f <= f' -> b1_recompute f n s = Some r -> b1_recompute f' n s = Some r;
  B1Mrun_memo : forall f' n s r, f <= f' -> b1_run_memo f n s = Some r -> b1_run_memo f' n s = Some r;
  B1Mexecute : forall f' n s r, f <= f' -> b1_execute_effect f n s = Some r -> b1_execute_effect f' n s = Some r;
  B1Mrun_effect : forall f' n s r, f <= f' -> b1_run_effect f n s = Some r -> b1_run_effect f' n s = Some r;
  B1Mbody : forall f' n s r, f <= f' -> b1_run_effect_body f n s = Some r -> b1_run_effect_body f' n s = Some r;
  B1Mdischarge : forall f' o s r, f <= f' -> b1_discharge f o s = Some r -> b1_discharge f' o s = Some r;
  B1Mretire : forall f' o s r, f <= f' -> b1_retire f o s = Some r -> b1_retire f' o s = Some r;
  B1Mdispose_scope : forall f' o s r, f <= f' -> b1_dispose_scope f o s = Some r -> b1_dispose_scope f' o s = Some r;
  B1Mteardown : forall f' o ls cs s r, f <= f' -> b1_teardown f o ls cs s = Some r -> b1_teardown f' o ls cs s = Some r;
  B1Mcleanup : forall f' o b e s r, f <= f' -> b1_run_cleanup f o b e s = Some r -> b1_run_cleanup f' o b e s = Some r;
  B1Mrelease : forall f' n s r, f <= f' -> b1_release f n s = Some r -> b1_release f' n s = Some r;
  B1Mdispose : forall f' n s r, f <= f' -> b1_dispose_node f n s = Some r -> b1_dispose_node f' n s = Some r;
  B1Mattach : forall f' n o s r, f <= f' -> b1_attach f n o s = Some r -> b1_attach f' n o s = Some r;
  B1Mwrite : forall f' n v s r, f <= f' -> b1_write_signal f n v s = Some r -> b1_write_signal f' n v s = Some r;
  B1Mrequest : forall f' s r, f <= f' -> b1_request_flush f s = Some r -> b1_request_flush f' s = Some r;
  B1Msettle : forall f' s r, f <= f' -> b1_settle_flush f s = Some r -> b1_settle_flush f' s = Some r;
  B1Mflush : forall f' s r, f <= f' -> b1_run_flush f s = Some r -> b1_run_flush f' s = Some r;
  B1Mdrain : forall f' s r, f <= f' -> b1_drain f s = Some r -> b1_drain f' s = Some r
}.

Lemma b1_mono_all f : b1_mono_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - assert (Hle : forall f', S f <= f' -> exists g, f' = S g /\ f <= g).
    { intros [|g] H; [lia|]. exists g. split; [reflexivity|lia]. }
    split.
    + intros f' e c s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_exec] in H |- *.
      destruct c; repeat match goal with
      | |- context [on_node] => unfold on_node in H |- *
      | |- context [on_value] => unfold on_value in H |- *
      | |- context [match eval_expr ?e ?p ?x with _ => _ end] => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | |- (if decide _ then _ else stuck) _ = Some _ => destruct (decide _); try discriminate
      end; try exact H; try (by apply (B1Mexec _ IH)); try (destruct (decide _); by apply (B1Mexec _ IH)); revert H.
      * eapply bind_mono; [intros ??; by apply (B1Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; [by apply (B1Mexec _ IH)|exact id]].
      * eapply bind_mono; [intros ??; by apply (B1Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; by apply (B1Mexec _ IH)].
      * eapply bind_mono; [intros ??; by apply (B1Mread _ IH)|intros o ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; destruct o; [by apply (B1Mexec _ IH)|exact id]].
      * eapply bind_mono; [intros ??; by apply (B1Mwrite _ IH)|intros [] ?? _; by apply (B1Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ??; by apply (B1Mattach _ IH)|intros [] ?? _; by apply (B1Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ??; by apply (B1Mattach _ IH)|intros [] ?? _].
        eapply bind_mono; [|intros [] ?? _].
        { intros ?? E. unfold m_step in E |- *. destruct (mark_dirty f _ _) eqn:Em; simplify_eq.
          erewrite mark_dirty_mono; [reflexivity|exact Hg|exact Em]. }
        eapply bind_mono; [intros ??; by apply (B1Mrequest _ IH)|intros [] ?? _; by apply (B1Mexec _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros [o|] ?? _; [|exact id]].
        eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [|intros [] ?? _; by apply (B1Mexec _ IH)].
        intros ??; destruct (o_disposed x); [|exact id].
        eapply bind_mono; [intros ?? E; eapply detached_mono; [intros ??; by apply (B1Mcleanup _ IH)|exact E]|intros [] ?? _; by apply (B1Mrequest _ IH)].
      * eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
        destruct (n_kind d); try discriminate;
          (eapply bind_mono; [intros ??; by apply (B1Mdispose _ IH)|intros [] ?? _];
           eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (B1Mexec _ IH)]).
      * eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (B1Mexec _ IH)|intros [|] ?? _];
          (eapply bind_mono; [intros ?? E; exact E|intros [] ?? _]);
          [eapply bind_mono; [intros ??; by apply (B1Mrequest _ IH)|intros [] ?? _; by apply (B1Mexec _ IH)]|exact id].
      * eapply bind_mono; [intros ?? E; exact E|intros ??? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (B1Mexec _ IH)].
    + intros f' n b s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_read] in H |- *. destruct b; [by apply (B1Mread_node _ IH)|eapply untracked_mono; [intros ??; by apply (B1Mread_node _ IH)|exact H]].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_read_node] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [exact id| |exact id].
      eapply bind_mono; [|intros d' ?? _; exact id].
      intros ??; destruct (stale_live d); [|exact id].
      eapply bind_mono; [intros ??; by apply (B1Mrop _ IH)|intros d' ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d'' ?? _].
      destruct (stale_live d''); [|exact id].
      intros E; eapply mark_reader_mono; [exact Hg|exact E].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_update_if_necessary] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [exact id| |exact id].
      destruct (stale_live d); [|exact id].
      eapply bind_mono; [intros ??; by apply (B1Mrop _ IH)|intros ??? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_refresh_or_pull] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (deferring_b1 st0); [|by apply (B1Mpull _ IH)].
      eapply bind_mono; [intros ??; by apply (B1Mrefresh _ IH)|intros ??? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_pull] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (B1Mrefresh _ IH)|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      eapply bind_mono; [|intros ??? _; exact id].
      intros ??; destruct (_ && _); [by apply (B1Msettle _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_refresh] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (decide (n ∈ g_checking st0)).
      { intros E; eapply m_step_mark_dirty_mono; [exact Hg|exact E]. }
      destruct (decide (n ∈ g_running st0)); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (decide (n_freshness d = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (B1Mwalk _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        destruct (decide (n_freshness d' = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Mrecompute _ IH)|exact id].
    + intros f' n i s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_resolve_walk] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (decide (n_freshness d = Check)); [|exact id].
      destruct (source_slots (sources_of (g_edges st0) n) !! i) as [[src _]|].
      * eapply bind_mono; [intros ??; by apply (B1Mupdate _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d_src ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros st1 ?? _].
        eapply bind_mono; [|intros [] ?? _; by apply (B1Mwalk _ IH)].
        intros st2 a0; destruct (decide _); [|exact id]. intros E; eapply m_step_mark_dirty_mono; [exact Hg|exact E].
      * destruct (slot_holds stale_live st0 n); [by apply (B1Mwalk _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_recompute] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_scope d) as [o|]; [|by apply (B1Mrun_memo _ IH)].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (B1Mdischarge _ IH)|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      destruct (n_disposed d'); [exact id|].
      destruct (decide (n_runs d' = n_runs d)); [by apply (B1Mrun_memo _ IH)|].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Mrecompute _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_run_memo] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; eapply hosted_mono; [intros ??; by apply (B1Mexec _ IH)|exact E]|intros o ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d1 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d2 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros moved ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; destruct moved; [|exact id].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros st2 a0 E. eapply m_step_notify_dirty_except_mono; [exact Hg|exact E].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_execute_effect] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_disposed d); [exact id|].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (decide (n_freshness d = Check)); [|exact id].
        eapply bind_mono; [intros ??; by apply (B1Mwalk _ IH)|intros [] ?? _].
        eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
        destruct (decide (n_freshness d' = Check)); [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Mrun_effect _ IH)|exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_run_effect] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [|intros [] ?? _].
      { intros ??; destruct (n_scope d) as [o|]; [|exact id].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
        eapply bind_mono; [intros ??; by apply (B1Mdischarge _ IH)|intros [] ?? _; exact id]. }
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [|intros [] ?? _; exact id].
      intros ??; destruct (n_disposed d'); [exact id|by apply (B1Mbody _ IH)].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_run_effect_body] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; eapply hosted_mono; [intros ??; by apply (B1Mexec _ IH)|exact E]|intros o ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_discharge] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; eapply detached_mono; [intros ??; by apply (B1Mdispose_scope _ IH)|exact E]|intros [] ?? _; exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_retire] in H |- *. revert H.
      eapply bind_mono; [|intros [] ?? _].
      { intros ?? E0. eapply detached_mono; [|exact E0]. intros st0 r0.
        eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
        destruct (o_disposed x); [exact id|].
        eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (B1Mdispose_scope _ IH)]. }
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (live_queue st0); [by apply (B1Mrequest _ IH)|exact id].
    + intros f' o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_dispose_scope] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (B1Mteardown _ IH)].
    + intros f' o ls cs s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_teardown] in H |- *. revert H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [exact id|..].
      * eapply bind_mono; [intros ??; by apply (B1Mcleanup _ IH)|intros [] ?? _; by apply (B1Mteardown _ IH)].
      * eapply bind_mono; [intros ??; by apply (B1Mrelease _ IH)|intros [] ?? _; by apply (B1Mteardown _ IH)].
      * destruct (decide (a < l_acquired l)).
        -- eapply bind_mono; [intros ??; by apply (B1Mrelease _ IH)|intros [] ?? _; by apply (B1Mteardown _ IH)].
        -- eapply bind_mono; [intros ??; by apply (B1Mcleanup _ IH)|intros [] ?? _; by apply (B1Mteardown _ IH)].
    + intros f' o b e s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_run_cleanup] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (B1Mexec _ IH)|intros [|] ?? _; exact id].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_release] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _; by apply (B1Mdispose _ IH)].
    + intros f' n s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_dispose_node] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_disposed d); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      destruct (n_scope d); [by apply (B1Mretire _ IH)|exact id].
    + intros f' n o s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_attach] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros x ?? _].
      destruct (o_disposed x); [|exact id].
      eapply bind_mono; [intros ??; by apply (B1Mrelease _ IH)|intros [] ?? _; exact id].
    + intros f' n v s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_write_signal] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros d ?? _].
      destruct (n_kind d); [|exact id|exact id].
      destruct (decide (n_value d = v)); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [|intros [] ?? _; by apply (B1Mrequest _ IH)].
      intros st2 a0 E. eapply m_step_notify_dirty_mono; [exact Hg|exact E].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_request_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (_ && _); [|exact id].
      destruct (outside_bodies st0); [by apply (B1Mflush _ IH)|exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_settle_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (_ && _); [by apply (B1Mflush _ IH)|exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_run_flush] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (g_flushing st0); [exact id|].
      eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (B1Mdrain _ IH)|intros [] ?? _; exact id].
    + intros f' s r Hle' H. destruct (Hle _ Hle') as (g & -> & Hg).
      cbn [b1_drain] in H |- *. revert H.
      eapply bind_mono; [intros ?? E; exact E|intros st0 ?? _].
      destruct (g_queue st0 !! g_queue_head st0); [|exact id].
      eapply bind_mono; [intros ?? E; unfold m_modify in *; exact E|intros [] ?? _].
      eapply bind_mono; [intros ??; by apply (B1Mexecute _ IH)|intros [] ?? _; by apply (B1Mdrain _ IH)].
Qed.

Record b1_depths_kept_at (f : nat) : Prop := {
  B1Dexec : forall e c s s' r, b1_exec f e c s = Some (s', r) -> same_depths s s';
  B1Dread : forall n b s s' r, b1_read f n b s = Some (s', r) -> same_depths s s';
  B1Dread_node : forall n s s' r, b1_read_node f n s = Some (s', r) -> same_depths s s';
  B1Dupdate : forall n s s' r, b1_update_if_necessary f n s = Some (s', r) -> same_depths s s';
  B1Drop : forall n s s' r, b1_refresh_or_pull f n s = Some (s', r) -> same_depths s s';
  B1Dpull : forall n s s' r, b1_pull f n s = Some (s', r) -> same_depths s s';
  B1Drefresh : forall n s s' r, b1_refresh f n s = Some (s', r) -> same_depths s s';
  B1Dwalk : forall n i s s' r, b1_resolve_walk f n i s = Some (s', r) -> same_depths s s';
  B1Drecompute : forall n s s' r, b1_recompute f n s = Some (s', r) -> same_depths s s';
  B1Drun_memo : forall n s s' r, b1_run_memo f n s = Some (s', r) -> same_depths s s';
  B1Dexecute : forall n s s' r, b1_execute_effect f n s = Some (s', r) -> same_depths s s';
  B1Drun_effect : forall n s s' r, b1_run_effect f n s = Some (s', r) -> same_depths s s';
  B1Dbody : forall n s s' r, b1_run_effect_body f n s = Some (s', r) -> same_depths s s';
  B1Ddischarge : forall o s s' r, b1_discharge f o s = Some (s', r) -> same_depths s s';
  B1Dretire : forall o s s' r, b1_retire f o s = Some (s', r) -> same_depths s s';
  B1Ddispose_scope : forall o s s' r, b1_dispose_scope f o s = Some (s', r) -> same_depths s s';
  B1Dteardown : forall o ls cs s s' r, b1_teardown f o ls cs s = Some (s', r) -> same_depths s s';
  B1Dcleanup : forall o b e s s' r, b1_run_cleanup f o b e s = Some (s', r) -> same_depths s s';
  B1Drelease : forall n s s' r, b1_release f n s = Some (s', r) -> same_depths s s';
  B1Ddispose : forall n s s' r, b1_dispose_node f n s = Some (s', r) -> same_depths s s';
  B1Dattach : forall n o s s' r, b1_attach f n o s = Some (s', r) -> same_depths s s';
  B1Dwrite : forall n v s s' r, b1_write_signal f n v s = Some (s', r) -> same_depths s s';
  B1Drequest : forall s s' r, b1_request_flush f s = Some (s', r) -> same_depths s s';
  B1Dsettle : forall s s' r, b1_settle_flush f s = Some (s', r) -> same_depths s s';
  B1Dflush : forall s s' r, b1_run_flush f s = Some (s', r) -> same_depths s s';
  B1Ddrain : forall s s' r, b1_drain f s = Some (s', r) -> same_depths s s'
}.

Lemma b1_depths_kept f : b1_depths_kept_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - assert (Htrack : forall n s s' a, track n s = Some (s', a) -> same_depths s s').
    { intros n s s' a Ht. unfold track in Ht. destruct (live_current s); [|simplify_eq; apply same_depths_refl].
      destruct (sources_add _ _ _); [|discriminate]. simplify_eq. unfold ghost_read. destruct (g_nodes (with_edges _ _) !! n); split; reflexivity. }
    assert (Hcur : forall s s' a, current_owner s = Some (s', a) -> same_depths s s').
    { intros s s' a Hc. unfold current_owner in Hc.
      destruct (g_current_owner s), (g_current s); try discriminate; try (simplify_eq; apply same_depths_refl).
      destruct (g_nodes s !! _); [|discriminate]. destruct (n_scope _); [simplify_eq; split; reflexivity|].
      destruct (match n_kind _, n_mode _ with KMemo, Pure => true | _, _ => false end); simplify_eq; split; reflexivity. }
    assert (Hunlink : forall l s s' a, unlink l s = Some (s', a) -> same_depths s s').
    { intros l s s' a Hu. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return in Hu.
      destruct (g_owners s !! l_owner l); [|simplify_eq]. destruct (decide _); simplify_eq; split; reflexivity. }
    assert (Hrec : forall o y s s' a, record_error o y s = Some (s', a) -> same_depths s s').
    { intros o y s s' a Hr. unfold record_error, m_bind, get_owner, put_owner, m_modify in Hr.
      destruct (g_owners s !! o) as [x|]; [|simplify_eq]. destruct (o_sink x) as [p|]; [destruct (g_owners s !! p)|]; simplify_eq; split; reflexivity. }
    assert (Hghost : forall n s s' a, (let* d3 := get_node n in if decide (n_freshness d3 = Clean) then m_return tt else announce n) s = Some (s', a) -> same_depths s s').
    { intros n s s' a Hg. unfold m_bind, get_node, announce, modify_node, m_return in Hg.
      destruct (g_nodes s !! n); [|discriminate]. destruct (decide _); [simplify_eq; split; reflexivity|].
      destruct (f_unannounced _); [destruct (g_nodes s !! n); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity]. }
    split.
    + intros e c s s' r H. cbn [b1_exec] in H.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0
      | H0 : context [on_value] |- _ => unfold on_value in H0
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (unfold m_return in H; simplify_eq; apply same_depths_refl); try (simplify_eq; apply same_depths_refl).
      * eapply (B1Dexec _ IH); exact H.
      * destruct (decide _); eapply (B1Dexec _ IH); exact H.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; [eapply (B1Dexec _ IH); exact H3|unfold m_return in H3; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; eapply (B1Dexec _ IH); exact H3.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dread _ IH); exact H__d| ].
        intros o s1 E1. eapply bind_depths; [intros ?? E2; unfold note_host_read in E2; destruct (g_current s1), (g_current_owner s1); try destruct (decide _); unfold emit, m_modify in E2; simplify_eq; split; reflexivity|].
        intros [] s2 E2 H3. destruct o; [eapply (B1Dexec _ IH); exact H3|unfold m_return in H3; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dwrite _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d]].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ??; eapply bind_depths; [intros ??; apply Hcur|intros [o|] ??; [eapply bind_depths; [intros ??; intro H__d; eapply (B1Dattach _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d]|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]]]].
      * eapply (bind_depths_of H); [intros ?? E; unfold next_id in E; simplify_eq; split; reflexivity|intros ???; eapply bind_depths; [intros ?? E; unfold add_node, m_modify, ghost_publish in E; destruct (g_nodes _ !! _); simplify_eq; split; reflexivity|intros [] ??; eapply bind_depths; [intros ??; apply Hcur|intros [o|] ??; [|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]]]].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Dattach _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold m_step in E; destruct (mark_dirty f _ _) eqn:Em; simplify_eq; by eapply marks_same_depths, mark_dirty_rtc|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Drequest _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ??; apply Hcur|intros [o|] ??; [|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl]].
        eapply bind_depths; [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ??].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d].
        intros ??; destruct (o_disposed x); [|intros E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity].
        eapply bind_depths; [intros ??; eapply detached_depths; intros ???; intro H__d; eapply (B1Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (B1Drequest _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! _); simplify_eq; apply same_depths_refl|intros d ?? ].
        destruct (n_kind d); try discriminate;
          (eapply bind_depths; [intros ??; intro H__d; eapply (B1Ddispose _ IH); exact H__d|intros [] ??];
           eapply bind_depths; [intros ?? E; destruct (n_disposed d); unfold emit, m_modify, m_return in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d]).
      * eapply (bind_depths_of H); [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? ].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Dexec _ IH); exact H__d|intros o ??].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        destruct o; [eapply bind_depths; [intros ??; intro H__d; eapply (B1Drequest _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d]|intros Hm; unfold m_return in Hm; simplify_eq; apply same_depths_refl].
      * eapply (bind_depths_of H); [intros ??; apply log_label_depths|intros ??? ].
        eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Dexec _ IH); exact H__d].
    + intros n b s s' r H. cbn [b1_read] in H. destruct b; [by eapply (B1Dread_node _ IH)|eapply untracked_depths; [intros ???; intro H__d; eapply (B1Dread_node _ IH); exact H__d|exact H]].
    + intros n s s' r H. cbn [b1_read_node] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [eapply bind_depths; [intros ??; apply Htrack|intros [] ?? E; unfold m_return in E; simplify_eq; apply same_depths_refl]| |discriminate].
      eapply bind_depths; [|intros d' ?? E; destruct (n_status d'); unfold m_return in E; simplify_eq; apply same_depths_refl].
      intros ??; destruct (stale_live d).
      * eapply bind_depths; [intros ??; intro H__d; eapply (B1Drop _ IH); exact H__d|intros d' ??].
        eapply bind_depths; [|intros [] ?? E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        intros ??; eapply bind_depths; [intros ??; apply Htrack|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d'' ??].
        destruct (stale_live d''); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        apply mark_reader_depths.
      * eapply bind_depths; [intros ??; apply Htrack|intros [] ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_update_if_necessary] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl| |intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (stale_live d); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      eapply bind_depths; [intros ??; intro H__d; eapply (B1Drop _ IH); exact H__d|intros ??? E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_refresh_or_pull] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros s0 ?? ].
      destruct (deferring_b1 s0); [|by apply (B1Dpull _ IH)].
      eapply bind_depths; [intros ??; intro H__d; eapply (B1Drefresh _ IH); exact H__d|intros [] ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_pull] in H.
      eapply bind_inv in H as (s0 & [] & E0 & H). unfold m_modify in E0; simplify_eq.
      eapply bind_inv in H as (s1 & [] & Er & H).
      eapply bind_inv in H as (s2 & d & Eg & H). unfold get_node in Eg. destruct (g_nodes s1 !! n); [|discriminate]. injection Eg as <- <-.
      eapply bind_inv in H as (s3 & [] & E3 & H). unfold m_modify in E3; simplify_eq.
      rewrite bind_get in H.
      eapply bind_inv in H as (s4 & [] & Es & H). unfold m_return in H; simplify_eq.
      pose proof (B1Drefresh _ IH n _ _ _ Er) as [Hp1 Hf1].
      set (s3 := with_pull_depth (pred (g_pull_depth s1)) s1) in *.
      assert (Hp3 : g_pull_depth s3 = g_pull_depth s) by (unfold s3; simpl; rewrite Hp1; simpl; lia).
      assert (Hf3 : g_flushing s3 = g_flushing s) by (unfold s3; simpl; exact Hf1).
      destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3).
      * pose proof (B1Dsettle _ IH _ _ _ Es) as [Hp4 Hf4]. split; congruence.
      * unfold m_return in Es; simplify_eq. split; assumption.
    + intros n s s' r H. cbn [b1_refresh] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros s0 ?? ].
      destruct (decide (n ∈ g_checking s0)).
      { intros E. unfold m_step in E. destruct (mark_dirty f n _) eqn:Em; simplify_eq. by eapply marks_same_depths, mark_dirty_rtc. }
      destruct (decide (n ∈ g_running s0)); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ??].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold push_checking, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Dwalk _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold pop_checking, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        destruct (decide (n_freshness d' = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Drecompute _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n i s s' r H. cbn [b1_resolve_walk] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (source_slots (sources_of (g_edges st0) n) !! i) as [[src _]|].
      * eapply bind_depths; [intros ??; intro H__d; eapply (B1Dupdate _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! src); simplify_eq; apply same_depths_refl|intros d_src ??].
        eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st1 ??].
        eapply bind_depths; [|intros [] ?? H__d; eapply (B1Dwalk _ IH); exact H__d].
        intros st_ [] E; destruct (decide _); [|unfold m_return in E; simplify_eq; apply same_depths_refl].
        unfold m_step in E. destruct (mark_dirty f n _) eqn:Em; simplify_eq. by eapply marks_same_depths, mark_dirty_rtc.
      * destruct (slot_holds stale_live st0 n); [by apply (B1Dwalk _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_recompute] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_scope d) as [o|]; [|by apply (B1Drun_memo _ IH)].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (B1Ddischarge _ IH); exact H__d|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      destruct (n_disposed d').
      { destruct (decide (n_freshness d' = Clean)); [intros E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl]. }
      destruct (decide (n_runs d' = n_runs d)); [by apply (B1Drun_memo _ IH)|].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Drecompute _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_run_memo] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold push_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; eapply hosted_depths; intros ???; intro H__d; eapply (B1Dexec _ IH); exact H__d|intros o ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d1 ??].
      eapply bind_depths; [intros ?? E; destruct (if n_violated d1 then Fail pure_scope_failure else o); unfold modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (_ (g_edges _)); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold pop_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; destruct (if n_violated d1 then Fail pure_scope_failure else o); unfold modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d2 ??].
      eapply bind_depths; [intros ?? E; destruct (decide _); [unfold m_return in E; simplify_eq; apply same_depths_refl|destruct (n_comparer d2); unfold m_bind, modify_node, m_return in E; try destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]|intros mv ??].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      eapply bind_depths; [intros ?? E; unfold m_modify, ghost_run_end in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [|intros [] ??; intro Hg_; eapply Hghost; exact Hg_].
      intros ??; destruct mv; [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      eapply bind_depths; [intros st_ [] E; unfold m_step in E; destruct (notify_dirty_except f n (g_current st0) _) eqn:Em; simplify_eq; by eapply marks_same_depths, (any_mark_rtc f n (g_current st0)), or_intror, or_intror, or_intror, or_intror|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st1 ??].
      destruct (_ && _); [intros E; unfold m_modify in E; simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_execute_effect] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_disposed d); [intros E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (decide (n_freshness d = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Dwalk _ IH); exact H__d|intros [] ??].
        eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
        destruct (decide (n_freshness d' = Check)); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      destruct (decide (n_freshness d' = Dirty)); [by apply (B1Drun_effect _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n s s' r H. cbn [b1_run_effect] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold push_running, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? ].
      eapply bind_depths; [intros ?? E; unfold edges, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ??].
      eapply bind_depths; [|intros [] ??].
      { intros ??; destruct (n_scope d) as [o|]; [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
        eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
        eapply bind_depths; [intros ??; intro H__d; eapply (B1Ddischarge _ IH); exact H__d|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity]. }
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [|intros [] ?? E; unfold pop_running, m_modify in E; simplify_eq; split; reflexivity].
      intros ??; destruct (n_disposed d'); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|by apply (B1Dbody _ IH)].
    + intros n s s' r H. cbn [b1_run_effect_body] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; eapply hosted_depths; intros ???; intro H__d; eapply (B1Dexec _ IH); exact H__d|intros o ??].
      eapply bind_depths; [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d' ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (_ (g_edges _)); simplify_eq; split; reflexivity|intros [] ??].
      destruct o; [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity].
    + intros o s s' r H. cbn [b1_discharge] in H.
      eapply (bind_depths_of H); [intros ??; eapply detached_depths; intros ???; intro H__d; eapply (B1Ddispose_scope _ IH); exact H__d|intros [] ?? ].
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (_ && _); [intros E; unfold m_modify in E; simplify_eq; split; reflexivity|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros o s s' r H. cbn [b1_retire] in H.
      eapply (bind_depths_of H); [|intros [] ?? ].
      { intros ?? E. eapply detached_depths; [|exact E]. intros ???.
        eapply bind_depths; [intros ?? E1; unfold get_owner in E1; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ??].
        destruct (o_disposed x); [intros E1; unfold m_return in E1; simplify_eq; apply same_depths_refl|].
        eapply bind_depths; [intros ?? E1; unfold put_owner, m_modify in E1; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Ddispose_scope _ IH); exact H__d]. }
      eapply bind_depths; [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ??].
      destruct (live_queue st0); [by apply (B1Drequest _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros o s s' r H. cbn [b1_dispose_scope] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      eapply bind_depths; [intros ?? E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Dteardown _ IH); exact H__d].
    + intros o ls cs s s' r H. cbn [b1_teardown] in H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs'].
      * unfold m_return in H. simplify_eq. apply same_depths_refl.
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dteardown _ IH); exact H__d].
      * eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Drelease _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dteardown _ IH); exact H__d].
      * destruct (decide _); [eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Drelease _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dteardown _ IH); exact H__d]|eapply (bind_depths_of H); [intros ??; intro H__d; eapply (B1Dcleanup _ IH); exact H__d|intros [] ?? H__d; eapply (B1Dteardown _ IH); exact H__d]].
    + intros o b e s s' r H. cbn [b1_run_cleanup] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      eapply bind_depths; [intros ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (B1Dexec _ IH); exact H__d|intros [|] ??].
      * intros E; unfold m_return in E; simplify_eq; apply same_depths_refl.
      * eapply bind_depths; [intros ??; apply Hrec|intros [] ?? E; unfold emit, m_modify in E; simplify_eq; split; reflexivity].
    + intros n s s' r H. cbn [b1_release] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ?? H__d; eapply (B1Ddispose _ IH); exact H__d].
    + intros n s s' r H. cbn [b1_dispose_node] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_disposed d); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; destruct (n_link d); [apply Hunlink|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl]|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold edges_opt in E; destruct (sources_clear _ _); simplify_eq; split; reflexivity|intros [] ??].
      destruct (n_scope d); [by apply (B1Dretire _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros n o s s' r H. cbn [b1_attach] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_owner in E; destruct (g_owners _ !! o); simplify_eq; apply same_depths_refl|intros x ?? ].
      destruct (o_disposed x).
      * eapply bind_depths; [intros ??; intro H__d; eapply (B1Drelease _ IH); exact H__d|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
      * eapply bind_depths; [intros ?? E; unfold put_owner, m_modify in E; simplify_eq; split; reflexivity|intros [] ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
    + intros n v s s' r H. cbn [b1_write_signal] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold get_node in E; destruct (g_nodes _ !! n); simplify_eq; apply same_depths_refl|intros d ?? ].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|].
      eapply bind_depths; [intros ?? E; unfold modify_node in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_modify, ghost_publish in E; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ?? E; unfold m_step in E; destruct (notify_dirty f n _) eqn:Em; simplify_eq; by eapply marks_same_depths, (any_mark_rtc f n None), or_intror, or_intror, or_intror, or_introl|intros [] ?? H__d; eapply (B1Drequest _ IH); exact H__d].
    + intros s s' r H. cbn [b1_request_flush] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (_ && _); [|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
      destruct (outside_bodies st0); [by apply (B1Dflush _ IH)|].
      destruct (g_flushing st0); [intros E; unfold m_return in E; simplify_eq; apply same_depths_refl|intros E; unfold m_modify in E; simplify_eq; split; reflexivity].
    + intros s s' r H. cbn [b1_settle_flush] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (_ && _); [by apply (B1Dflush _ IH)|intros E; unfold m_return in E; simplify_eq; apply same_depths_refl].
    + intros s s' r H. cbn [b1_run_flush] in H.
      rewrite bind_get in H.
      destruct (g_flushing s) eqn:Ef; [unfold m_return in H; simplify_eq; apply same_depths_refl|].
      eapply bind_inv in H as (st1 & [] & E1 & H). unfold m_modify in E1; simplify_eq.
      eapply bind_inv in H as (s2 & [] & Ed & H). unfold m_modify in H; simplify_eq.
      pose proof (B1Ddrain _ IH _ _ _ Ed) as [Hp2 _]. split; [exact Hp2|simpl; symmetry; exact Ef].
    + intros s s' r H. cbn [b1_drain] in H.
      eapply (bind_depths_of H); [intros ?? E; unfold m_get in E; simplify_eq; apply same_depths_refl|intros st0 ?? ].
      destruct (g_queue st0 !! g_queue_head st0); [|intros E; unfold m_modify in E; simplify_eq; split; reflexivity].
      eapply bind_depths; [intros ?? E; unfold m_modify in E; simplify_eq; split; reflexivity|intros [] ??].
      eapply bind_depths; [intros ??; intro H__d; eapply (B1Dexecute _ IH); exact H__d|intros [] ?? H__d; eapply (B1Ddrain _ IH); exact H__d].
Qed.

Definition b1_state_rel (s sb : state) : Prop :=
  with_pull_depth 0 s = with_pull_depth 0 sb /\
  (g_flushing sb = false -> g_pull_depth s = g_pull_depth sb) /\
  (g_flushing sb = true -> g_pull_depth sb <= g_pull_depth s).

Lemma b1_state_rel_refl s : b1_state_rel s s.
Proof. split; [reflexivity|split; auto]. Qed.

Lemma b1_state_rel_eq s sb :
  b1_state_rel s sb -> g_pull_depth s = g_pull_depth sb -> s = sb.
Proof.
  intros [Hw _] Hpd.
  destruct s, sb; simpl in *.
  unfold with_pull_depth in Hw. injection Hw. intros. subst. reflexivity.
Qed.

Lemma b1_rel_fields s sb :
  b1_state_rel s sb ->
  g_flags s = g_flags sb /\ g_edges s = g_edges sb /\ g_nodes s = g_nodes sb /\
  g_owners s = g_owners sb /\ g_queue s = g_queue sb /\ g_queue_head s = g_queue_head sb /\
  g_flushing s = g_flushing sb /\ g_flush_owed s = g_flush_owed sb /\
  g_batch_depth s = g_batch_depth sb /\ g_current s = g_current sb /\
  g_untracked s = g_untracked sb /\ g_current_owner s = g_current_owner sb /\
  g_next_id s = g_next_id sb /\ g_trace s = g_trace sb /\ g_running s = g_running sb /\
  g_checking s = g_checking sb /\ g_ghost s = g_ghost sb /\ g_registered s = g_registered sb.
Proof.
  intros [Hw _]. unfold with_pull_depth in Hw. destruct s, sb; simpl in *.
  injection Hw; intros; repeat split; assumption.
Qed.

Lemma b1_rel_update (upd : state -> state) s sb :
  (forall st, g_pull_depth (upd st) = g_pull_depth st /\
              g_flushing (upd st) = g_flushing st /\
              with_pull_depth 0 (upd st) = upd (with_pull_depth 0 st)) ->
  b1_state_rel s sb -> b1_state_rel (upd s) (upd sb).
Proof.
  intros Hupd [Hw [Hf Ht]].
  destruct (Hupd s) as [Hp1 [Hfl1 Hw1]].
  destruct (Hupd sb) as [Hp2 [Hfl2 Hw2]].
  split; [congruence|split].
  - rewrite Hfl2, Hp1, Hp2. exact Hf.
  - rewrite Hfl2, Hp1, Hp2. exact Ht.
Qed.

Local Ltac b1_rel_simple :=
  match goal with
  | H : b1_state_rel ?s ?sb |- b1_state_rel _ _ =>
    let Hw := fresh "Hw" in let Hf := fresh "Hf" in let Ht := fresh "Ht" in
    destruct H as [Hw [Hf Ht]];
    split; [unfold with_pull_depth in *; destruct s, sb; simpl in *; injection Hw; intros; subst; reflexivity|simpl; split; assumption]
  end.

Lemma b1_rel_set_node n d s sb :
  b1_state_rel s sb -> b1_state_rel (set_node n d s) (set_node n d sb).
Proof. intro H. b1_rel_simple. Qed.

Lemma b1_rel_with_pull_S s sb :
  b1_state_rel s sb -> b1_state_rel (with_pull_depth (S (g_pull_depth s)) s) (with_pull_depth (S (g_pull_depth sb)) sb).
Proof.
  intros [Hw [Hf Ht]]. split; [unfold with_pull_depth in *; destruct s, sb; simpl in *; injection Hw; intros; subst; reflexivity|].
  simpl. split; [intros H; rewrite (Hf H); reflexivity|intros H; pose proof (Ht H); lia].
Qed.

Lemma b1_rel_with_pull_S_left s sb :
  b1_state_rel s sb -> g_flushing sb = true ->
  b1_state_rel (with_pull_depth (S (g_pull_depth s)) s) sb.
Proof.
  intros [Hw [Hf Ht]] Hfl. split.
  - rewrite <- Hw. unfold with_pull_depth. destruct s; reflexivity.
  - simpl. split; [intro H; congruence|intro H; pose proof (Ht H); lia].
Qed.

Lemma b1_rel_with_pull_pred s sb :
  b1_state_rel s sb -> b1_state_rel (with_pull_depth (pred (g_pull_depth s)) s) (with_pull_depth (pred (g_pull_depth sb)) sb).
Proof.
  intros [Hw [Hf Ht]]. split; [unfold with_pull_depth in *; destruct s, sb; simpl in *; injection Hw; intros; subst; reflexivity|].
  simpl. split; [intros H; rewrite (Hf H); reflexivity|intros H; pose proof (Ht H); lia].
Qed.

Lemma request_flush_flushing f s s' a :
  g_flushing s = true -> request_flush f s = Some (s', a) -> s' = s /\ a = tt.
Proof.
  intros Hf H. destruct f as [|f]; [discriminate|].
  cbn [request_flush] in H. rewrite bind_get in H.
  destruct (_ && _); [|unfold m_return in H; simplify_eq; done].
  destruct (outside_bodies s).
  - destruct f as [|f]; [discriminate|]. cbn [run_flush] in H. rewrite bind_get, Hf in H.
    unfold m_return in H. by simplify_eq.
  - rewrite Hf in H. unfold m_return in H. by simplify_eq.
Qed.

Lemma settle_flush_flushing f s s' a :
  g_flushing s = true -> settle_flush f s = Some (s', a) -> s' = s /\ a = tt.
Proof.
  intros Hf H. destruct f as [|f]; [discriminate|].
  cbn [settle_flush] in H. rewrite bind_get in H.
  destruct (_ && _); [|unfold m_return in H; simplify_eq; done].
  destruct f as [|f]; [discriminate|]. cbn [run_flush] in H. rewrite bind_get, Hf in H.
  unfold m_return in H. by simplify_eq.
Qed.

Lemma b1_request_flush_flushing f s s' a :
  g_flushing s = true -> b1_request_flush f s = Some (s', a) -> s' = s /\ a = tt.
Proof.
  intros Hf H. destruct f as [|f]; [discriminate|].
  cbn [b1_request_flush] in H. rewrite bind_get in H.
  destruct (_ && _); [|unfold m_return in H; simplify_eq; done].
  destruct (outside_bodies s).
  - destruct f as [|f]; [discriminate|]. cbn [b1_run_flush] in H. rewrite bind_get, Hf in H.
    unfold m_return in H. by simplify_eq.
  - rewrite Hf in H. unfold m_return in H. by simplify_eq.
Qed.

Lemma b1_settle_flush_flushing f s s' a :
  g_flushing s = true -> b1_settle_flush f s = Some (s', a) -> s' = s /\ a = tt.
Proof.
  intros Hf H. destruct f as [|f]; [discriminate|].
  cbn [b1_settle_flush] in H. rewrite bind_get in H.
  destruct (_ && _); [|unfold m_return in H; simplify_eq; done].
  destruct f as [|f]; [discriminate|]. cbn [b1_run_flush] in H. rewrite bind_get, Hf in H.
  unfold m_return in H. by simplify_eq.
Qed.

Definition sim_fwd_step {A} (m mb : nat -> M A) : Prop :=
  forall f s sb s' a, b1_state_rel s sb -> m f s = Some (s', a) ->
    exists fb sb', mb fb sb = Some (sb', a) /\ b1_state_rel s' sb'.

Definition sim_bwd_step {A} (m mb : nat -> M A) : Prop :=
  forall fb sb sb' a s, b1_state_rel s sb -> mb fb sb = Some (sb', a) ->
    exists f s', m f s = Some (s', a) /\ b1_state_rel s' sb'.

Lemma sim_bind {A B} (m1 mb1 : nat -> M A) (k1 kb1 : A -> nat -> M B)
    (mono1 : forall f1 f2 s r, f1 <= f2 -> mb1 f1 s = Some r -> mb1 f2 s = Some r)
    (mono2 : forall a f1 f2 s r, f1 <= f2 -> kb1 a f1 s = Some r -> kb1 a f2 s = Some r) :
  sim_fwd_step m1 mb1 ->
  (forall a, sim_fwd_step (k1 a) (kb1 a)) ->
  forall f s sb s' b, b1_state_rel s sb -> m_bind (m1 f) (fun a => k1 a f) s = Some (s', b) ->
    exists fb sb', m_bind (mb1 fb) (fun a => kb1 a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof.
  intros H1 H2 f s sb s' b Hrel Hb.
  unfold m_bind in Hb. destruct (m1 f s) as [[s1 a]|] eqn:E1; [|discriminate].
  destruct (H1 f s sb s1 a Hrel E1) as (fb1 & sb1 & Emb1 & Hrel1).
  destruct (H2 a f s1 sb1 s' b Hrel1 Hb) as (fb2 & sb' & Ekb2 & Hrel2).
  exists (Nat.max fb1 fb2), sb'. split; [|exact Hrel2].
  unfold m_bind.
  rewrite (mono1 fb1 (Nat.max fb1 fb2) sb (sb1, a) ltac:(lia) Emb1).
  exact (mono2 a fb2 (Nat.max fb1 fb2) sb1 (sb', b) ltac:(lia) Ekb2).
Qed.

Lemma sim_pure_bind {A B} (m : M A) (k1 kb1 : A -> nat -> M B)
    (Hm : forall s sb s1 a, b1_state_rel s sb -> m s = Some (s1, a) ->
      exists sb1, m sb = Some (sb1, a) /\ b1_state_rel s1 sb1) :
  (forall a, sim_fwd_step (k1 a) (kb1 a)) ->
  forall f s sb s' b, b1_state_rel s sb -> m_bind m (fun a => k1 a f) s = Some (s', b) ->
    exists fb sb', m_bind m (fun a => kb1 a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof.
  intros Hk f s sb s' b Hrel Hb.
  unfold m_bind in Hb. destruct (m s) as [[s1 a]|] eqn:E1; [|discriminate].
  destruct (Hm s sb s1 a Hrel E1) as (sb1 & Emb1 & Hrel1).
  destruct (Hk a f s1 sb1 s' b Hrel1 Hb) as (fb & sb' & Ekb & Hrel2).
  exists fb, sb'. split; [|exact Hrel2]. unfold m_bind. by rewrite Emb1.
Qed.

Lemma b1_rel_hosted {A} n (b bb : nat -> M A)
    (Hsim : sim_fwd_step b bb) :
  sim_fwd_step (fun f => hosted n (b f)) (fun fb => hosted n (bb fb)).
Proof.
  intros f s sb s' a Hrel H. unfold hosted in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_current_owner None (with_current (Some n) s))
                               (with_current_owner None (with_current (Some n) sb))).
  { b1_rel_simple. }
  destruct (Hsim f _ _ _ _ Hrel1 E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_current_owner (g_current_owner sb) (with_current (g_current sb) sb2)).
  split.
  - unfold hosted, m_bind, m_modify, m_return. rewrite Ebb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_hosted_bwd {A} n (b bb : nat -> M A)
    (Hsim : sim_bwd_step b bb) :
  sim_bwd_step (fun f => hosted n (b f)) (fun fb => hosted n (bb fb)).
Proof.
  intros fb sb sb' a s Hrel H. unfold hosted in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_current_owner None (with_current (Some n) s))
                               (with_current_owner None (with_current (Some n) sb))).
  { b1_rel_simple. }
  destruct (Hsim fb _ _ _ _ Hrel1 E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_current_owner (g_current_owner s) (with_current (g_current s) s2)).
  split.
  - unfold hosted, m_bind, m_modify, m_return. rewrite Eb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_untracked {A} (b bb : nat -> M A)
    (Hsim : sim_fwd_step b bb) :
  sim_fwd_step (fun f => untracked (b f)) (fun fb => untracked (bb fb)).
Proof.
  intros f s sb s' a Hrel H. unfold untracked in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&Hu&_).
  assert (Hflag : match g_current s with Some _ => true | None => g_untracked s end =
                  match g_current sb with Some _ => true | None => g_untracked sb end) by (by rewrite Hc, Hu).
  assert (Hrel1 : b1_state_rel (with_untracked (match g_current s with Some _ => true | None => g_untracked s end) (with_current None s))
                               (with_untracked (match g_current sb with Some _ => true | None => g_untracked sb end) (with_current None sb))).
  { rewrite Hflag. b1_rel_simple. }
  destruct (Hsim f _ _ _ _ Hrel1 E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_untracked (g_untracked sb) (with_current (g_current sb) sb2)).
  split.
  - unfold untracked, m_bind, m_modify, m_return. rewrite Ebb. reflexivity.
  - destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Hu.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_untracked_bwd {A} (b bb : nat -> M A)
    (Hsim : sim_bwd_step b bb) :
  sim_bwd_step (fun f => untracked (b f)) (fun fb => untracked (bb fb)).
Proof.
  intros fb sb sb' a s Hrel H. unfold untracked in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&Hu&_).
  assert (Hflag : match g_current s with Some _ => true | None => g_untracked s end =
                  match g_current sb with Some _ => true | None => g_untracked sb end) by (by rewrite Hc, Hu).
  assert (Hrel1 : b1_state_rel (with_untracked (match g_current s with Some _ => true | None => g_untracked s end) (with_current None s))
                               (with_untracked (match g_current sb with Some _ => true | None => g_untracked sb end) (with_current None sb))).
  { rewrite Hflag. b1_rel_simple. }
  destruct (Hsim fb _ _ _ _ Hrel1 E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_untracked (g_untracked s) (with_current (g_current s) s2)).
  split.
  - unfold untracked, m_bind, m_modify, m_return. rewrite Eb. reflexivity.
  - destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Hu.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_detached o (b bb : nat -> M unit)
    (Hsim : sim_fwd_step b bb) :
  sim_fwd_step (fun f => detached o (b f)) (fun fb => detached o (bb fb)).
Proof.
  intros f s sb s' [] Hrel H. unfold detached in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & [] & E2 & H). unfold m_modify in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s)))
                               (with_batch_depth (S (g_batch_depth sb)) (with_current_owner (Some o) (with_current None sb)))).
  { destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hbd&_). rewrite Hbd. b1_rel_simple. }
  destruct (Hsim f _ _ _ _ Hrel1 E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_batch_depth (pred (g_batch_depth sb2)) (with_current_owner (g_current_owner sb) (with_current (g_current sb) sb2))).
  split.
  - unfold detached, m_bind, m_modify. rewrite Ebb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_detached_bwd o (b bb : nat -> M unit)
    (Hsim : sim_bwd_step b bb) :
  sim_bwd_step (fun f => detached o (b f)) (fun fb => detached o (bb fb)).
Proof.
  intros fb sb sb' [] s Hrel H. unfold detached in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & [] & E2 & H). unfold m_modify in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s)))
                               (with_batch_depth (S (g_batch_depth sb)) (with_current_owner (Some o) (with_current None sb)))).
  { destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hbd&_). rewrite Hbd. b1_rel_simple. }
  destruct (Hsim fb _ _ _ _ Hrel1 E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_batch_depth (pred (g_batch_depth s2)) (with_current_owner (g_current_owner s) (with_current (g_current s) s2))).
  split.
  - unfold detached, m_bind, m_modify. rewrite Eb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_rel_destruct s sb :
  b1_state_rel s sb ->
  sb = with_pull_depth (g_pull_depth sb) s /\
  s = with_pull_depth (g_pull_depth s) sb /\
  g_flushing s = g_flushing sb /\
  (g_flushing sb = false -> g_pull_depth s = g_pull_depth sb) /\
  (g_flushing sb = true -> g_pull_depth sb <= g_pull_depth s).
Proof.
  intros [Hw [Hf Ht]].
  assert (Hsb : sb = with_pull_depth (g_pull_depth sb) s).
  { unfold with_pull_depth in *. destruct s, sb; simpl in *. injection Hw; intros; subst; reflexivity. }
  assert (Hs : s = with_pull_depth (g_pull_depth s) sb).
  { unfold with_pull_depth in *. destruct s, sb; simpl in *. injection Hw; intros; subst; reflexivity. }
  repeat split; try assumption.
  destruct (b1_rel_fields _ _ (conj Hw (conj Hf Ht))) as (_&_&_&_&_&_&Hfl&_). exact Hfl.
Qed.

Lemma b1_rel_pure_step {A} (m : M A)
    (Hwp : forall p s, m (with_pull_depth p s) = option_map (fun '(s', a) => (with_pull_depth p s', a)) (m s))
    (Hdp : forall s s' a, m s = Some (s', a) -> g_flushing s' = g_flushing s /\ g_pull_depth s' = g_pull_depth s)
    s sb s' a :
  b1_state_rel s sb -> m s = Some (s', a) ->
  exists sb', m sb = Some (sb', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hm.
  destruct (b1_rel_destruct _ _ Hrel) as (Hsb & _ & Hfl & Hf & Ht).
  exists (with_pull_depth (g_pull_depth sb) s').
  rewrite Hsb, Hwp, Hm. simpl.
  destruct (Hdp _ _ _ Hm) as [Hfl' Hpd'].
  split; [reflexivity|].
  split; [unfold with_pull_depth; destruct s'; reflexivity|].
  simpl. split.
  - intro H. rewrite Hfl', Hfl in H. rewrite Hpd'. exact (Hf H).
  - intro H. rewrite Hfl', Hfl in H. rewrite Hpd'. exact (Ht H).
Qed.

Lemma b1_rel_pure_step_bwd {A} (m : M A)
    (Hwp : forall p s, m (with_pull_depth p s) = option_map (fun '(s', a) => (with_pull_depth p s', a)) (m s))
    (Hdp : forall s s' a, m s = Some (s', a) -> g_flushing s' = g_flushing s /\ g_pull_depth s' = g_pull_depth s)
    s sb sb' a :
  b1_state_rel s sb -> m sb = Some (sb', a) ->
  exists s', m s = Some (s', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hm.
  destruct (b1_rel_destruct _ _ Hrel) as (_ & Hs & Hfl & Hf & Ht).
  exists (with_pull_depth (g_pull_depth s) sb').
  rewrite Hs, Hwp, Hm. simpl.
  destruct (Hdp _ _ _ Hm) as [Hfl' Hpd'].
  split; [reflexivity|].
  split; [unfold with_pull_depth; destruct sb'; reflexivity|].
  simpl. split.
  - intro H. rewrite Hfl' in H. rewrite Hpd'. exact (Hf H).
  - intro H. rewrite Hfl' in H. rewrite Hpd'. exact (Ht H).
Qed.

Lemma set_node_with_pull n d p s : set_node n d (with_pull_depth p s) = with_pull_depth p (set_node n d s).
Proof. destruct s; reflexivity. Qed.

Lemma enqueue_with_pull n p s : enqueue n (with_pull_depth p s) = with_pull_depth p (enqueue n s).
Proof. destruct s; reflexivity. Qed.

Lemma walk_marks_with_pull mark src k p s :
  (forall c p0 s0, mark c (with_pull_depth p0 s0) = option_map (with_pull_depth p0) (mark c s0)) ->
  walk_marks mark src k (with_pull_depth p s) = option_map (with_pull_depth p) (walk_marks mark src k s).
Proof.
  intros Hm. revert s. induction k as [|k IH]; intros s; simpl; [reflexivity|].
  destruct (decide _); [apply IH|].
  destruct (observer_items (observers_of (g_edges s) src) !! k) as [[c _]|]; [|reflexivity].
  rewrite Hm. destruct (mark c s) as [s1|]; [simpl; apply IH|reflexivity].
Qed.

Lemma mark_check_with_pull f n p s :
  mark_check f n (with_pull_depth p s) = option_map (with_pull_depth p) (mark_check f n s).
Proof.
  revert p n s. induction f as [|f IH]; intros p n s; simpl; [reflexivity|].
  destruct (g_nodes s !! n) as [d|]; [|reflexivity].
  destruct (n_kind d); [reflexivity| |].
  - destruct (decide _).
    + unfold notify_with; simpl. rewrite set_node_with_pull. apply walk_marks_with_pull. intros; apply IH.
    + unfold announce_due; simpl. destruct (f_unannounced (g_flags s) && n_unannounced d); [|reflexivity].
      unfold notify_with; simpl. rewrite set_node_with_pull. apply walk_marks_with_pull. intros; apply IH.
  - simpl. unfold effect_marked.
    destruct (n_disposed d || n_in_cleanup d); [reflexivity|].
    destruct (n_queued d); rewrite set_node_with_pull; [reflexivity|].
    by rewrite enqueue_with_pull.
Qed.

Lemma mark_dirty_with_pull f n p s :
  mark_dirty f n (with_pull_depth p s) = option_map (with_pull_depth p) (mark_dirty f n s).
Proof.
  destruct f as [|f]; simpl; [reflexivity|].
  destruct (g_nodes s !! n) as [d|]; [|reflexivity].
  destruct (n_kind d); [reflexivity| |].
  - destruct (decide _).
    + unfold notify_with; simpl. rewrite set_node_with_pull. apply walk_marks_with_pull. intros; apply mark_check_with_pull.
    + unfold announce_due; simpl. destruct (f_unannounced (g_flags s) && n_unannounced d).
      * unfold notify_with; simpl. rewrite set_node_with_pull. apply walk_marks_with_pull. intros; apply mark_check_with_pull.
      * destruct (decide _); [reflexivity|]. by rewrite set_node_with_pull.
  - simpl. unfold effect_marked.
    destruct (n_disposed d || n_in_cleanup d); [reflexivity|].
    destruct (n_queued d); rewrite set_node_with_pull; [reflexivity|].
    by rewrite enqueue_with_pull.
Qed.

Lemma notify_dirty_with_pull f src p s :
  notify_dirty f src (with_pull_depth p s) = option_map (with_pull_depth p) (notify_dirty f src s).
Proof.
  unfold notify_dirty, notify_with; simpl.
  apply walk_marks_with_pull. intros; apply mark_dirty_with_pull.
Qed.

Lemma notify_dirty_except_with_pull f src cur p s :
  notify_dirty_except f src cur (with_pull_depth p s) = option_map (with_pull_depth p) (notify_dirty_except f src cur s).
Proof.
  unfold notify_dirty_except, notify_with; simpl.
  apply walk_marks_with_pull.
  intros c p0 s0. unfold mark_except; simpl.
  destruct (decide _); [reflexivity|apply mark_dirty_with_pull].
Qed.

Lemma b1_fwd_bind {A B} (m : M A) (mb : nat -> M A) (k : A -> M B) (kb : A -> nat -> M B) s sb s' b :
  (forall f1 f2 s0 r0, f1 <= f2 -> mb f1 s0 = Some r0 -> mb f2 s0 = Some r0) ->
  (forall a f1 f2 s0 r0, f1 <= f2 -> kb a f1 s0 = Some r0 -> kb a f2 s0 = Some r0) ->
  (forall s1 a, m s = Some (s1, a) ->
    exists fb1 sb1, mb fb1 sb = Some (sb1, a) /\ b1_state_rel s1 sb1) ->
  (forall a s1 sb1, m s = Some (s1, a) -> b1_state_rel s1 sb1 -> k a s1 = Some (s', b) ->
    exists fb2 sb', kb a fb2 sb1 = Some (sb', b) /\ b1_state_rel s' sb') ->
  m_bind m k s = Some (s', b) ->
  exists fb sb', m_bind (mb fb) (fun a => kb a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof.
  intros Hm1 Hk1 H1 H2 Hb.
  unfold m_bind in Hb. destruct (m s) as [[s1 a]|] eqn:E1; [|discriminate].
  destruct (H1 _ _ eq_refl) as (fb1 & sb1 & Emb1 & Hrel1).
  destruct (H2 _ _ _ eq_refl Hrel1 Hb) as (fb2 & sb' & Ekb2 & Hrel2).
  exists (Nat.max fb1 fb2), sb'. split; [|exact Hrel2].
  unfold m_bind.
  rewrite (Hm1 fb1 (Nat.max fb1 fb2) sb (sb1, a) ltac:(lia) Emb1).
  exact (Hk1 a fb2 (Nat.max fb1 fb2) sb1 (sb', b) ltac:(lia) Ekb2).
Qed.

Lemma b1_fwd_bind_of {A B m k s s' b} (Hb : m_bind m k s = Some (s', b))
    (mb : nat -> M A) (kb : A -> nat -> M B) sb :
  (forall f1 f2 s0 r0, f1 <= f2 -> mb f1 s0 = Some r0 -> mb f2 s0 = Some r0) ->
  (forall a f1 f2 s0 r0, f1 <= f2 -> kb a f1 s0 = Some r0 -> kb a f2 s0 = Some r0) ->
  (forall s1 a, m s = Some (s1, a) ->
    exists fb1 sb1, mb fb1 sb = Some (sb1, a) /\ b1_state_rel s1 sb1) ->
  (forall a s1 sb1, m s = Some (s1, a) -> b1_state_rel s1 sb1 -> k a s1 = Some (s', b) ->
    exists fb2 sb', kb a fb2 sb1 = Some (sb', b) /\ b1_state_rel s' sb') ->
  exists fb sb', m_bind (mb fb) (fun a => kb a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof. intros Hm1 Hk1 H1 H2. exact (b1_fwd_bind m mb k kb s sb s' b Hm1 Hk1 H1 H2 Hb). Qed.

Lemma b1_bwd_bind {A B} (m : nat -> M A) (mb : M A) (k : A -> nat -> M B) (kb : A -> M B) s sb sb' b :
  (forall f1 f2 s0 r0, f1 <= f2 -> m f1 s0 = Some r0 -> m f2 s0 = Some r0) ->
  (forall a f1 f2 s0 r0, f1 <= f2 -> k a f1 s0 = Some r0 -> k a f2 s0 = Some r0) ->
  (forall sb1 a, mb sb = Some (sb1, a) ->
    exists f1 s1, m f1 s = Some (s1, a) /\ b1_state_rel s1 sb1) ->
  (forall a sb1 s1, mb sb = Some (sb1, a) -> b1_state_rel s1 sb1 -> kb a sb1 = Some (sb', b) ->
    exists f2 s', k a f2 s1 = Some (s', b) /\ b1_state_rel s' sb') ->
  m_bind mb kb sb = Some (sb', b) ->
  exists f s', m_bind (m f) (fun a => k a f) s = Some (s', b) /\ b1_state_rel s' sb'.
Proof.
  intros Hm1 Hk1 H1 H2 Hb.
  unfold m_bind in Hb. destruct (mb sb) as [[sb1 a]|] eqn:E1; [|discriminate].
  destruct (H1 _ _ eq_refl) as (f1 & s1 & Em1 & Hrel1).
  destruct (H2 _ _ _ eq_refl Hrel1 Hb) as (f2 & s' & Ek2 & Hrel2).
  exists (Nat.max f1 f2), s'. split; [|exact Hrel2].
  unfold m_bind.
  rewrite (Hm1 f1 (Nat.max f1 f2) s (s1, a) ltac:(lia) Em1).
  exact (Hk1 a f2 (Nat.max f1 f2) s1 (s', b) ltac:(lia) Ek2).
Qed.

Lemma b1_bwd_bind_of {A B mb kb sb sb' b} (Hb : m_bind mb kb sb = Some (sb', b))
    (m : nat -> M A) (k : A -> nat -> M B) s :
  (forall f1 f2 s0 r0, f1 <= f2 -> m f1 s0 = Some r0 -> m f2 s0 = Some r0) ->
  (forall a f1 f2 s0 r0, f1 <= f2 -> k a f1 s0 = Some r0 -> k a f2 s0 = Some r0) ->
  (forall sb1 a, mb sb = Some (sb1, a) ->
    exists f1 s1, m f1 s = Some (s1, a) /\ b1_state_rel s1 sb1) ->
  (forall a sb1 s1, mb sb = Some (sb1, a) -> b1_state_rel s1 sb1 -> kb a sb1 = Some (sb', b) ->
    exists f2 s', k a f2 s1 = Some (s', b) /\ b1_state_rel s' sb') ->
  exists f s', m_bind (m f) (fun a => k a f) s = Some (s', b) /\ b1_state_rel s' sb'.
Proof. intros Hm1 Hk1 H1 H2. exact (b1_bwd_bind m mb k kb s sb sb' b Hm1 Hk1 H1 H2 Hb). Qed.

Lemma b1_fwd_bind_pure_l {A B} (m : M A) (k : A -> M B) (kb : A -> nat -> M B) s sb s' b :
  b1_state_rel s sb ->
  (forall s0 sb0 s1 a, b1_state_rel s0 sb0 -> m s0 = Some (s1, a) ->
    exists sb1, m sb0 = Some (sb1, a) /\ b1_state_rel s1 sb1) ->
  (forall a s1 sb1, m s = Some (s1, a) -> m sb = Some (sb1, a) -> b1_state_rel s1 sb1 ->
    k a s1 = Some (s', b) -> exists fb sb', kb a fb sb1 = Some (sb', b) /\ b1_state_rel s' sb') ->
  m_bind m k s = Some (s', b) ->
  exists fb sb', m_bind m (fun a => kb a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hm Hk Hb.
  unfold m_bind in Hb. destruct (m s) as [[s1 a]|] eqn:E1; [|discriminate].
  destruct (Hm _ _ _ _ Hrel E1) as (sb1 & Emb1 & Hrel1).
  destruct (Hk _ _ _ eq_refl Emb1 Hrel1 Hb) as (fb & sb' & Ekb & Hrel2).
  exists fb, sb'. split; [|exact Hrel2]. unfold m_bind. by rewrite Emb1.
Qed.

Lemma b1_bwd_bind_pure_l {A B} (m : M A) (k : A -> nat -> M B) (kb : A -> M B) s sb sb' b :
  b1_state_rel s sb ->
  (forall s0 sb0 sb1 a, b1_state_rel s0 sb0 -> m sb0 = Some (sb1, a) ->
    exists s1, m s0 = Some (s1, a) /\ b1_state_rel s1 sb1) ->
  (forall a sb1 s1, m sb = Some (sb1, a) -> m s = Some (s1, a) -> b1_state_rel s1 sb1 ->
    kb a sb1 = Some (sb', b) -> exists f s', k a f s1 = Some (s', b) /\ b1_state_rel s' sb') ->
  m_bind m kb sb = Some (sb', b) ->
  exists f s', m_bind m (fun a => k a f) s = Some (s', b) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hm Hk Hb.
  unfold m_bind in Hb. destruct (m sb) as [[sb1 a]|] eqn:E1; [|discriminate].
  destruct (Hm _ _ _ _ Hrel E1) as (s1 & Em1 & Hrel1).
  destruct (Hk _ _ _ eq_refl Em1 Hrel1 Hb) as (f & s' & Ek & Hrel2).
  exists f, s'. split; [|exact Hrel2]. unfold m_bind. by rewrite Em1.
Qed.
Lemma b1_fwd_bind_pure_l_of {A B m k s sb s' b}
    (Hrel : b1_state_rel s sb) (Hb : m_bind m k s = Some (s', b)) (kb : A -> nat -> M B) :
  (forall s0 sb0 s1 a, b1_state_rel s0 sb0 -> m s0 = Some (s1, a) ->
    exists sb1, m sb0 = Some (sb1, a) /\ b1_state_rel s1 sb1) ->
  (forall a s1 sb1, m s = Some (s1, a) -> m sb = Some (sb1, a) -> b1_state_rel s1 sb1 ->
    k a s1 = Some (s', b) -> exists fb sb', kb a fb sb1 = Some (sb', b) /\ b1_state_rel s' sb') ->
  exists fb sb', m_bind m (fun a => kb a fb) sb = Some (sb', b) /\ b1_state_rel s' sb'.
Proof. intros Hm Hk. exact (b1_fwd_bind_pure_l m k kb s sb s' b Hrel Hm Hk Hb). Qed.

Lemma b1_bwd_bind_pure_l_of {A B m kb sb s sb' b}
    (Hrel : b1_state_rel s sb) (Hb : m_bind m kb sb = Some (sb', b)) (k : A -> nat -> M B) :
  (forall s0 sb0 sb1 a, b1_state_rel s0 sb0 -> m sb0 = Some (sb1, a) ->
    exists s1, m s0 = Some (s1, a) /\ b1_state_rel s1 sb1) ->
  (forall a sb1 s1, m sb = Some (sb1, a) -> m s = Some (s1, a) -> b1_state_rel s1 sb1 ->
    kb a sb1 = Some (sb', b) -> exists f s', k a f s1 = Some (s', b) /\ b1_state_rel s' sb') ->
  exists f s', m_bind m (fun a => k a f) s = Some (s', b) /\ b1_state_rel s' sb'.
Proof. intros Hm Hk. exact (b1_bwd_bind_pure_l m k kb s sb sb' b Hrel Hm Hk Hb). Qed.

Lemma b1_hosted_fwd {A} n (b : M A) (bb : nat -> M A) s sb s' a :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall s1 a1, b s0 = Some (s1, a1) ->
    exists fb sb1, bb fb sb0 = Some (sb1, a1) /\ b1_state_rel s1 sb1) ->
  hosted n b s = Some (s', a) ->
  exists fb sb', hosted n (bb fb) sb = Some (sb', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. unfold hosted in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_current_owner None (with_current (Some n) s))
                               (with_current_owner None (with_current (Some n) sb))) by b1_rel_simple.
  destruct (Hb _ _ Hrel1 _ _ E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_current_owner (g_current_owner sb) (with_current (g_current sb) sb2)).
  split.
  - unfold hosted, m_bind, m_modify, m_return. rewrite Ebb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_hosted_bwd {A} n (b : nat -> M A) (bb : M A) s sb sb' a :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall sb1 a1, bb sb0 = Some (sb1, a1) ->
    exists f s1, b f s0 = Some (s1, a1) /\ b1_state_rel s1 sb1) ->
  hosted n bb sb = Some (sb', a) ->
  exists f s', hosted n (b f) s = Some (s', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. unfold hosted in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_current_owner None (with_current (Some n) s))
                               (with_current_owner None (with_current (Some n) sb))) by b1_rel_simple.
  destruct (Hb _ _ Hrel1 _ _ E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_current_owner (g_current_owner s) (with_current (g_current s) s2)).
  split.
  - unfold hosted, m_bind, m_modify, m_return. rewrite Eb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_untracked_fwd {A} (b : M A) (bb : nat -> M A) s sb s' a :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall s1 a1, b s0 = Some (s1, a1) ->
    exists fb sb1, bb fb sb0 = Some (sb1, a1) /\ b1_state_rel s1 sb1) ->
  untracked b s = Some (s', a) ->
  exists fb sb', untracked (bb fb) sb = Some (sb', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. unfold untracked in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&Hu&_).
  assert (Hflag : match g_current s with Some _ => true | None => g_untracked s end =
                  match g_current sb with Some _ => true | None => g_untracked sb end) by (by rewrite Hc, Hu).
  assert (Hrel1 : b1_state_rel (with_untracked (match g_current s with Some _ => true | None => g_untracked s end) (with_current None s))
                               (with_untracked (match g_current sb with Some _ => true | None => g_untracked sb end) (with_current None sb))).
  { rewrite Hflag. b1_rel_simple. }
  destruct (Hb _ _ Hrel1 _ _ E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_untracked (g_untracked sb) (with_current (g_current sb) sb2)).
  split.
  - unfold untracked, m_bind, m_modify, m_return. rewrite Ebb. reflexivity.
  - destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Hu.
    + simpl. split; assumption.
Qed.

Lemma b1_untracked_bwd {A} (b : nat -> M A) (bb : M A) s sb sb' a :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall sb1 a1, bb sb0 = Some (sb1, a1) ->
    exists f s1, b f s0 = Some (s1, a1) /\ b1_state_rel s1 sb1) ->
  untracked bb sb = Some (sb', a) ->
  exists f s', untracked (b f) s = Some (s', a) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. unfold untracked in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & a' & E2 & H).
  unfold m_bind, m_modify, m_return in H. simplify_eq.
  destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&Hu&_).
  assert (Hflag : match g_current s with Some _ => true | None => g_untracked s end =
                  match g_current sb with Some _ => true | None => g_untracked sb end) by (by rewrite Hc, Hu).
  assert (Hrel1 : b1_state_rel (with_untracked (match g_current s with Some _ => true | None => g_untracked s end) (with_current None s))
                               (with_untracked (match g_current sb with Some _ => true | None => g_untracked sb end) (with_current None sb))).
  { rewrite Hflag. b1_rel_simple. }
  destruct (Hb _ _ Hrel1 _ _ E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_untracked (g_untracked s) (with_current (g_current s) s2)).
  split.
  - unfold untracked, m_bind, m_modify, m_return. rewrite Eb. reflexivity.
  - destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Hu.
    + simpl. split; assumption.
Qed.

Lemma b1_detached_fwd o (b : M unit) (bb : nat -> M unit) s sb s' u :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall s1 a1, b s0 = Some (s1, a1) ->
    exists fb sb1, bb fb sb0 = Some (sb1, a1) /\ b1_state_rel s1 sb1) ->
  detached o b s = Some (s', u) ->
  exists fb sb', detached o (bb fb) sb = Some (sb', u) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. destruct u. unfold detached in H.
  eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (s2 & [] & E2 & H). unfold m_modify in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s)))
                               (with_batch_depth (S (g_batch_depth sb)) (with_current_owner (Some o) (with_current None sb)))).
  { destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hbd&_). rewrite Hbd. b1_rel_simple. }
  destruct (Hb _ _ Hrel1 _ _ E2) as (fb & sb2 & Ebb & Hrel2).
  exists fb, (with_batch_depth (pred (g_batch_depth sb2)) (with_current_owner (g_current_owner sb) (with_current (g_current sb) sb2))).
  split.
  - unfold detached, m_bind, m_modify. rewrite Ebb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

Lemma b1_detached_bwd o (b : nat -> M unit) (bb : M unit) s sb sb' u :
  b1_state_rel s sb ->
  (forall s0 sb0, b1_state_rel s0 sb0 -> forall sb1 a1, bb sb0 = Some (sb1, a1) ->
    exists f s1, b f s0 = Some (s1, a1) /\ b1_state_rel s1 sb1) ->
  detached o bb sb = Some (sb', u) ->
  exists f s', detached o (b f) s = Some (s', u) /\ b1_state_rel s' sb'.
Proof.
  intros Hrel Hb H. destruct u. unfold detached in H.
  eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
  eapply bind_inv in H as (sb2 & [] & E2 & H). unfold m_modify in H. simplify_eq.
  assert (Hrel1 : b1_state_rel (with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s)))
                               (with_batch_depth (S (g_batch_depth sb)) (with_current_owner (Some o) (with_current None sb)))).
  { destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hbd&_). rewrite Hbd. b1_rel_simple. }
  destruct (Hb _ _ Hrel1 _ _ E2) as (f & s2 & Eb & Hrel2).
  exists f, (with_batch_depth (pred (g_batch_depth s2)) (with_current_owner (g_current_owner s) (with_current (g_current s) s2))).
  split.
  - unfold detached, m_bind, m_modify. rewrite Eb. reflexivity.
  - destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&Hc&_&Ho&_).
    destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
    + unfold with_pull_depth in *. destruct s2, sb2; simpl in *. injection Hw2; intros; subst.
      by rewrite Hc, Ho.
    + simpl. split; assumption.
Qed.

(** Generic monotonicity tactics for `b1_*` and `Engine` steps. *)
Local Ltac b1_mono_tac :=
  intros;
  repeat (cbv beta in *; match goal with
  | H0 : m_bind ?m1 ?k1 ?s = Some ?r |- m_bind _ _ ?s = Some ?r =>
    eapply bind_mono; [..|exact H0]; [intros ?? ?|intros ???? ?]
  | H0 : hosted ?n ?b1 ?s = Some ?r |- hosted ?n _ ?s = Some ?r =>
    eapply hosted_mono; [..|exact H0]; intros ???
  | H0 : untracked ?b1 ?s = Some ?r |- untracked _ ?s = Some ?r =>
    eapply untracked_mono; [..|exact H0]; intros ???
  | H0 : detached ?o ?b1 ?s = Some ?r |- detached ?o _ ?s = Some ?r =>
    eapply detached_mono; [..|exact H0]; intros ???
  | |- (if ?b then _ else _) _ = Some _ => destruct b
  | |- match ?x with _ => _ end _ = Some _ => destruct x
  | H : ?f1 <= ?f2 |- b1_exec ?f2 _ _ _ = Some _ => by eapply (B1Mexec _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_read ?f2 _ _ _ = Some _ => by eapply (B1Mread _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_read_node ?f2 _ _ = Some _ => by eapply (B1Mread_node _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_update_if_necessary ?f2 _ _ = Some _ => by eapply (B1Mupdate _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_refresh_or_pull ?f2 _ _ = Some _ => by eapply (B1Mrop _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_pull ?f2 _ _ = Some _ => by eapply (B1Mpull _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_refresh ?f2 _ _ = Some _ => by eapply (B1Mrefresh _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_resolve_walk ?f2 _ _ _ = Some _ => by eapply (B1Mwalk _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_recompute ?f2 _ _ = Some _ => by eapply (B1Mrecompute _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_run_memo ?f2 _ _ = Some _ => by eapply (B1Mrun_memo _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_execute_effect ?f2 _ _ = Some _ => by eapply (B1Mexecute _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_run_effect ?f2 _ _ = Some _ => by eapply (B1Mrun_effect _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_run_effect_body ?f2 _ _ = Some _ => by eapply (B1Mbody _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_discharge ?f2 _ _ = Some _ => by eapply (B1Mdischarge _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_retire ?f2 _ _ = Some _ => by eapply (B1Mretire _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_dispose_scope ?f2 _ _ = Some _ => by eapply (B1Mdispose_scope _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_teardown ?f2 _ _ _ _ = Some _ => by eapply (B1Mteardown _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_run_cleanup ?f2 _ _ _ _ = Some _ => by eapply (B1Mcleanup _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_release ?f2 _ _ = Some _ => by eapply (B1Mrelease _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_dispose_node ?f2 _ _ = Some _ => by eapply (B1Mdispose _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_attach ?f2 _ _ _ = Some _ => by eapply (B1Mattach _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_write_signal ?f2 _ _ _ = Some _ => by eapply (B1Mwrite _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_request_flush ?f2 _ = Some _ => by eapply (B1Mrequest _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_settle_flush ?f2 _ = Some _ => by eapply (B1Msettle _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_run_flush ?f2 _ = Some _ => by eapply (B1Mflush _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- b1_drain ?f2 _ = Some _ => by eapply (B1Mdrain _ (b1_mono_all f1))
  | H : ?f1 <= ?f2 |- mark_reader ?f2 _ = Some _ => by eapply mark_reader_mono
  | H : ?f1 <= ?f2 |- m_step (mark_dirty ?f2 _) _ = Some _ => by eapply m_step_mark_dirty_mono
  | H : ?f1 <= ?f2 |- m_step (mark_check ?f2 _) _ = Some _ => by eapply m_step_mark_check_mono
  | H : ?f1 <= ?f2 |- m_step (notify_dirty ?f2 _) _ = Some _ => by eapply m_step_notify_dirty_mono
  | H : ?f1 <= ?f2 |- m_step (notify_dirty_except ?f2 _ _) _ = Some _ => by eapply m_step_notify_dirty_except_mono
  | H0 : ?m ?s = Some ?r |- ?m ?s = Some ?r => exact H0
  end).

Local Ltac eng_mono_tac :=
  intros;
  repeat (cbv beta in *; match goal with
  | H0 : m_bind ?m1 ?k1 ?s = Some ?r |- m_bind _ _ ?s = Some ?r =>
    eapply bind_mono; [..|exact H0]; [intros ?? ?|intros ???? ?]
  | H0 : hosted ?n ?b1 ?s = Some ?r |- hosted ?n _ ?s = Some ?r =>
    eapply hosted_mono; [..|exact H0]; intros ???
  | H0 : untracked ?b1 ?s = Some ?r |- untracked _ ?s = Some ?r =>
    eapply untracked_mono; [..|exact H0]; intros ???
  | H0 : detached ?o ?b1 ?s = Some ?r |- detached ?o _ ?s = Some ?r =>
    eapply detached_mono; [..|exact H0]; intros ???
  | |- (if ?b then _ else _) _ = Some _ => destruct b
  | |- match ?x with _ => _ end _ = Some _ => destruct x
  | H : ?f1 <= ?f2 |- exec ?f2 _ _ _ = Some _ => by eapply (Mexec _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- read ?f2 _ _ _ = Some _ => by eapply (Mread _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- read_node ?f2 _ _ = Some _ => by eapply (Mread_node _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- update_if_necessary ?f2 _ _ = Some _ => by eapply (Mupdate _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- refresh_or_pull ?f2 _ _ = Some _ => by eapply (Mrop _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- pull ?f2 _ _ = Some _ => by eapply (Mpull _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- refresh ?f2 _ _ = Some _ => by eapply (Mrefresh _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- resolve_walk ?f2 _ _ _ = Some _ => by eapply (Mwalk _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- recompute ?f2 _ _ = Some _ => by eapply (Mrecompute _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- run_memo ?f2 _ _ = Some _ => by eapply (Mrun_memo _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- execute_effect ?f2 _ _ = Some _ => by eapply (Mexecute _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- run_effect ?f2 _ _ = Some _ => by eapply (Mrun_effect _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- run_effect_body ?f2 _ _ = Some _ => by eapply (Mbody _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- discharge ?f2 _ _ = Some _ => by eapply (Mdischarge _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- retire ?f2 _ _ = Some _ => by eapply (Mretire _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- dispose_scope ?f2 _ _ = Some _ => by eapply (Mdispose_scope _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- teardown ?f2 _ _ _ _ = Some _ => by eapply (Mteardown _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- run_cleanup ?f2 _ _ _ _ = Some _ => by eapply (Mcleanup _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- release ?f2 _ _ = Some _ => by eapply (Mrelease _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- dispose_node ?f2 _ _ = Some _ => by eapply (Mdispose _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- attach ?f2 _ _ _ = Some _ => by eapply (Mattach _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- write_signal ?f2 _ _ _ = Some _ => by eapply (Mwrite _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- request_flush ?f2 _ = Some _ => by eapply (Mrequest _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- settle_flush ?f2 _ = Some _ => by eapply (Msettle _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- run_flush ?f2 _ = Some _ => by eapply (Mflush _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- drain ?f2 _ = Some _ => by eapply (Mdrain _ (engine_mono_all f1))
  | H : ?f1 <= ?f2 |- mark_reader ?f2 _ = Some _ => by eapply mark_reader_mono
  | H : ?f1 <= ?f2 |- m_step (mark_dirty ?f2 _) _ = Some _ => by eapply m_step_mark_dirty_mono
  | H : ?f1 <= ?f2 |- m_step (mark_check ?f2 _) _ = Some _ => by eapply m_step_mark_check_mono
  | H : ?f1 <= ?f2 |- m_step (notify_dirty ?f2 _) _ = Some _ => by eapply m_step_notify_dirty_mono
  | H : ?f1 <= ?f2 |- m_step (notify_dirty_except ?f2 _ _) _ = Some _ => by eapply m_step_notify_dirty_except_mono
  | H0 : ?m ?s = Some ?r |- ?m ?s = Some ?r => exact H0
  end).

Local Ltac b1_pure_solve :=
  intros s0 sb0 s1 a Hrel0 Hm; eapply b1_rel_pure_step; [..|exact Hrel0|exact Hm];
  [intros p st; destruct st; try reflexivity
  |intros st st' a0 Hst; destruct st; try (simplify_eq; split; reflexivity)].

Local Ltac b1_pure_solve_bwd :=
  intros s0 sb0 sb1 a Hrel0 Hm; eapply b1_rel_pure_step_bwd; [..|exact Hrel0|exact Hm];
  [intros p st; destruct st; try reflexivity
  |intros st st' a0 Hst; destruct st; try (simplify_eq; split; reflexivity)].

Lemma b1_pure_m_return {A} (a : A) s0 sb0 s1 a1 :
  b1_state_rel s0 sb0 -> m_return a s0 = Some (s1, a1) ->
  exists sb1, m_return a sb0 = Some (sb1, a1) /\ b1_state_rel s1 sb1.
Proof. intros Hrel H. unfold m_return in H |- *. simplify_eq. eauto. Qed.

Lemma b1_pure_m_return_bwd {A} (a : A) s0 sb0 sb1 a1 :
  b1_state_rel s0 sb0 -> m_return a sb0 = Some (sb1, a1) ->
  exists s1, m_return a s0 = Some (s1, a1) /\ b1_state_rel s1 sb1.
Proof. intros Hrel H. unfold m_return in H |- *. simplify_eq. eauto. Qed.

Lemma b1_pure_get_node n s0 sb0 s1 d :
  b1_state_rel s0 sb0 -> get_node n s0 = Some (s1, d) ->
  exists sb1, get_node n sb0 = Some (sb1, d) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step; [intros p []; unfold get_node; simpl; destruct (g_nodes !! n); reflexivity|intros ??? H; unfold get_node in H; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_get_node_bwd n s0 sb0 sb1 d :
  b1_state_rel s0 sb0 -> get_node n sb0 = Some (sb1, d) ->
  exists s1, get_node n s0 = Some (s1, d) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd; [intros p []; unfold get_node; simpl; destruct (g_nodes !! n); reflexivity|intros ??? H; unfold get_node in H; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_modify_node n u s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> modify_node n u s0 = Some (s1, r) ->
  exists sb1, modify_node n u sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step; [intros p []; unfold modify_node; simpl; destruct (g_nodes !! n); reflexivity|intros ??? H; unfold modify_node in H; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_modify_node_bwd n u s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> modify_node n u sb0 = Some (sb1, r) ->
  exists s1, modify_node n u s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd; [intros p []; unfold modify_node; simpl; destruct (g_nodes !! n); reflexivity|intros ??? H; unfold modify_node in H; destruct (g_nodes _ !! n); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_get_owner o s0 sb0 s1 x :
  b1_state_rel s0 sb0 -> get_owner o s0 = Some (s1, x) ->
  exists sb1, get_owner o sb0 = Some (sb1, x) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step; [intros p []; unfold get_owner; simpl; destruct (g_owners !! o); reflexivity|intros ??? H; unfold get_owner in H; destruct (g_owners _ !! o); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_get_owner_bwd o s0 sb0 sb1 x :
  b1_state_rel s0 sb0 -> get_owner o sb0 = Some (sb1, x) ->
  exists s1, get_owner o s0 = Some (s1, x) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd; [intros p []; unfold get_owner; simpl; destruct (g_owners !! o); reflexivity|intros ??? H; unfold get_owner in H; destruct (g_owners _ !! o); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_edges_opt u s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> edges_opt u s0 = Some (s1, r) ->
  exists sb1, edges_opt u sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step; [intros p []; unfold edges_opt; simpl; destruct (u _); reflexivity|intros ??? H; unfold edges_opt in H; destruct (u _); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_edges_opt_bwd u s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> edges_opt u sb0 = Some (sb1, r) ->
  exists s1, edges_opt u s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd; [intros p []; unfold edges_opt; simpl; destruct (u _); reflexivity|intros ??? H; unfold edges_opt in H; destruct (u _); simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_current_owner s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> current_owner s0 = Some (s1, r) ->
  exists sb1, current_owner sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold current_owner. simpl.
    destruct g_current_owner, g_current; try reflexivity.
    destruct (g_nodes !! n) as [d|]; [|reflexivity].
    destruct (n_scope d); [reflexivity|].
    destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end); reflexivity.
  - intros s s' a H. unfold current_owner in H.
    destruct (g_current_owner s), (g_current s); simplify_eq; try (split; reflexivity).
    destruct (g_nodes s !! _) as [d|]; [|simplify_eq].
    destruct (n_scope d); simplify_eq; try (split; reflexivity).
    destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_current_owner_bwd s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> current_owner sb0 = Some (sb1, r) ->
  exists s1, current_owner s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold current_owner. simpl.
    destruct g_current_owner, g_current; try reflexivity.
    destruct (g_nodes !! n) as [d|]; [|reflexivity].
    destruct (n_scope d); [reflexivity|].
    destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end); reflexivity.
  - intros s s' a H. unfold current_owner in H.
    destruct (g_current_owner s), (g_current s); simplify_eq; try (split; reflexivity).
    destruct (g_nodes s !! _) as [d|]; [|simplify_eq].
    destruct (n_scope d); simplify_eq; try (split; reflexivity).
    destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_note_host_read n o s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> note_host_read n o s0 = Some (s1, r) ->
  exists sb1, note_host_read n o sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold note_host_read, emit, m_modify. simpl.
    destruct g_current, g_current_owner; try reflexivity.
    destruct (decide _); reflexivity.
  - intros s s' a H. unfold note_host_read, emit, m_modify in H.
    destruct (g_current s), (g_current_owner s); simplify_eq; try (split; reflexivity).
    destruct (decide _); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_note_host_read_bwd n o s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> note_host_read n o sb0 = Some (sb1, r) ->
  exists s1, note_host_read n o s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold note_host_read, emit, m_modify. simpl.
    destruct g_current, g_current_owner; try reflexivity.
    destruct (decide _); reflexivity.
  - intros s s' a H. unfold note_host_read, emit, m_modify in H.
    destruct (g_current s), (g_current_owner s); simplify_eq; try (split; reflexivity).
    destruct (decide _); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_track n s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> track n s0 = Some (s1, r) ->
  exists sb1, track n sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold track, live_current. simpl.
    destruct g_current; [|reflexivity].
    destruct (g_nodes !! n0) as [d|]; [|reflexivity].
    destruct (n_disposed d); [reflexivity|].
    destruct (sources_add _ _ _); [|reflexivity].
    unfold ghost_read; simpl. destruct (g_nodes !! n); reflexivity.
  - intros s s' a H. unfold track, live_current in H.
    destruct (g_current s); simplify_eq; try (split; reflexivity).
    destruct (g_nodes s !! _) as [d|]; [|simplify_eq; split; reflexivity].
    destruct (n_disposed d); simplify_eq; [split; reflexivity|].
    destruct (sources_add _ _ _); simplify_eq; unfold ghost_read; simpl; try destruct (g_nodes s !! n); split; reflexivity.
Qed.

Lemma b1_pure_track_bwd n s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> track n sb0 = Some (sb1, r) ->
  exists s1, track n s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold track, live_current. simpl.
    destruct g_current; [|reflexivity].
    destruct (g_nodes !! n0) as [d|]; [|reflexivity].
    destruct (n_disposed d); [reflexivity|].
    destruct (sources_add _ _ _); [|reflexivity].
    unfold ghost_read; simpl. destruct (g_nodes !! n); reflexivity.
  - intros s s' a H. unfold track, live_current in H.
    destruct (g_current s); simplify_eq; try (split; reflexivity).
    destruct (g_nodes s !! _) as [d|]; [|simplify_eq; split; reflexivity].
    destruct (n_disposed d); simplify_eq; [split; reflexivity|].
    destruct (sources_add _ _ _); simplify_eq; unfold ghost_read; simpl; try destruct (g_nodes s !! n); split; reflexivity.
Qed.

Lemma b1_pure_mark_reader f s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> mark_reader f s0 = Some (s1, r) ->
  exists sb1, mark_reader f sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p s. unfold mark_reader; simpl.
    destruct (g_current s), (f_reader_mark (g_flags s)); try reflexivity.
    rewrite mark_check_with_pull. destruct (mark_check _ _ _); reflexivity.
  - intros s s' a H. unfold mark_reader in H.
    destruct (g_current s), (f_reader_mark (g_flags s)); simplify_eq; try (split; reflexivity).
    destruct (mark_check f _ s) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f _ None), or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_mark_reader_bwd f s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> mark_reader f sb0 = Some (sb1, r) ->
  exists s1, mark_reader f s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p s. unfold mark_reader; simpl.
    destruct (g_current s), (f_reader_mark (g_flags s)); try reflexivity.
    rewrite mark_check_with_pull. destruct (mark_check _ _ _); reflexivity.
  - intros s s' a H. unfold mark_reader in H.
    destruct (g_current s), (f_reader_mark (g_flags s)); simplify_eq; try (split; reflexivity).
    destruct (mark_check f _ s) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f _ None), or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_announce n s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> announce n s0 = Some (s1, r) ->
  exists sb1, announce n sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold announce, modify_node. simpl.
    destruct (f_unannounced g_flags); [destruct (g_nodes !! n); reflexivity|reflexivity].
  - intros ??? H. unfold announce, modify_node in H.
    destruct (f_unannounced _); [destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_announce_bwd n s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> announce n sb0 = Some (sb1, r) ->
  exists s1, announce n s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold announce, modify_node. simpl.
    destruct (f_unannounced g_flags); [destruct (g_nodes !! n); reflexivity|reflexivity].
  - intros ??? H. unfold announce, modify_node in H.
    destruct (f_unannounced _); [destruct (g_nodes _ !! n); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_unlink l s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> unlink l s0 = Some (s1, r) ->
  exists sb1, unlink l sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return. simpl.
    destruct (g_owners !! l_owner l); [|reflexivity]. destruct (decide _); reflexivity.
  - intros ??? H. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return in H.
    destruct (g_owners _ !! l_owner l); [|simplify_eq]. destruct (decide _); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_unlink_bwd l s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> unlink l sb0 = Some (sb1, r) ->
  exists s1, unlink l s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return. simpl.
    destruct (g_owners !! l_owner l); [|reflexivity]. destruct (decide _); reflexivity.
  - intros ??? H. unfold unlink, m_bind, get_owner, put_owner, m_modify, m_return in H.
    destruct (g_owners _ !! l_owner l); [|simplify_eq]. destruct (decide _); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_record_error o y s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> record_error o y s0 = Some (s1, r) ->
  exists sb1, record_error o y sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold record_error, m_bind, get_owner, put_owner, m_modify. simpl.
    destruct (g_owners !! o) as [x|]; simpl; [|reflexivity].
    destruct (o_sink x) as [os|]; simpl; [destruct (g_owners !! os); simpl; reflexivity|reflexivity].
  - intros ??? H. unfold record_error, m_bind, get_owner, put_owner, m_modify in H.
    destruct (g_owners _ !! o) as [x|]; [|simplify_eq].
    destruct (o_sink x) as [os|]; [destruct (g_owners _ !! os); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_record_error_bwd o y s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> record_error o y sb0 = Some (sb1, r) ->
  exists s1, record_error o y s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold record_error, m_bind, get_owner, put_owner, m_modify. simpl.
    destruct (g_owners !! o) as [x|]; simpl; [|reflexivity].
    destruct (o_sink x) as [os|]; simpl; [destruct (g_owners !! os); simpl; reflexivity|reflexivity].
  - intros ??? H. unfold record_error, m_bind, get_owner, put_owner, m_modify in H.
    destruct (g_owners _ !! o) as [x|]; [|simplify_eq].
    destruct (o_sink x) as [os|]; [destruct (g_owners _ !! os); simplify_eq; split; reflexivity|simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_log_label s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> log_label s0 = Some (s1, r) ->
  exists sb1, log_label sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold log_label. simpl. destruct g_current, g_current_owner; reflexivity.
  - intros s s' a H. unfold log_label in H. destruct (g_current s), (g_current_owner s); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_log_label_bwd s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> log_label sb0 = Some (sb1, r) ->
  exists s1, log_label s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold log_label. simpl. destruct g_current, g_current_owner; reflexivity.
  - intros s s' a H. unfold log_label in H. destruct (g_current s), (g_current_owner s); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_mark_dirty f n s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> m_step (mark_dirty f n) s0 = Some (s1, r) ->
  exists sb1, m_step (mark_dirty f n) sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p s. unfold m_step. rewrite mark_dirty_with_pull. destruct (mark_dirty _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (mark_dirty _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f n None), or_intror, or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_mark_dirty_bwd f n s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> m_step (mark_dirty f n) sb0 = Some (sb1, r) ->
  exists s1, m_step (mark_dirty f n) s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p s. unfold m_step. rewrite mark_dirty_with_pull. destruct (mark_dirty _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (mark_dirty _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f n None), or_intror, or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_notify_dirty f src s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> m_step (notify_dirty f src) s0 = Some (s1, r) ->
  exists sb1, m_step (notify_dirty f src) sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p s. unfold m_step. rewrite notify_dirty_with_pull. destruct (notify_dirty _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (notify_dirty _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f src None), or_intror, or_intror, or_intror, or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_notify_dirty_bwd f src s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> m_step (notify_dirty f src) sb0 = Some (sb1, r) ->
  exists s1, m_step (notify_dirty f src) s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p s. unfold m_step. rewrite notify_dirty_with_pull. destruct (notify_dirty _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (notify_dirty _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f src None), or_intror, or_intror, or_intror, or_introl, Em|split; reflexivity].
Qed.

Lemma b1_pure_notify_dirty_except f src cur s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> m_step (notify_dirty_except f src cur) s0 = Some (s1, r) ->
  exists sb1, m_step (notify_dirty_except f src cur) sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p s. unfold m_step. rewrite notify_dirty_except_with_pull. destruct (notify_dirty_except _ _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (notify_dirty_except _ _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f src cur), or_intror, or_intror, or_intror, or_intror, Em|split; reflexivity].
Qed.

Lemma b1_pure_notify_dirty_except_bwd f src cur s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> m_step (notify_dirty_except f src cur) sb0 = Some (sb1, r) ->
  exists s1, m_step (notify_dirty_except f src cur) s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p s. unfold m_step. rewrite notify_dirty_except_with_pull. destruct (notify_dirty_except _ _ _ _); reflexivity.
  - intros ??? H. unfold m_step in H. destruct (notify_dirty_except _ _ _ _) eqn:Em; simplify_eq.
    edestruct marks_same_depths as [-> ->]; [eapply (any_mark_rtc f src cur), or_intror, or_intror, or_intror, or_intror, Em|split; reflexivity].
Qed.

Record b1_fwd_at (f : nat) : Prop := {
  Fexec1 : forall e c s sb s' r, b1_state_rel s sb -> exec f e c s = Some (s', r) ->
    exists fb sb', b1_exec fb e c sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fread1 : forall n b s sb s' r, b1_state_rel s sb -> read f n b s = Some (s', r) ->
    exists fb sb', b1_read fb n b sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fread_node1 : forall n s sb s' r, b1_state_rel s sb -> read_node f n s = Some (s', r) ->
    exists fb sb', b1_read_node fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fupdate1 : forall n s sb s' r, b1_state_rel s sb -> update_if_necessary f n s = Some (s', r) ->
    exists fb sb', b1_update_if_necessary fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frop1 : forall n s sb s' r, b1_state_rel s sb -> refresh_or_pull f n s = Some (s', r) ->
    exists fb sb', b1_refresh_or_pull fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fpull1 : forall n s sb s' r, b1_state_rel s sb -> pull f n s = Some (s', r) ->
    exists fb sb', b1_pull fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frefresh1 : forall n s sb s' r, b1_state_rel s sb -> refresh f n s = Some (s', r) ->
    exists fb sb', b1_refresh fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fwalk1 : forall n i s sb s' r, b1_state_rel s sb -> resolve_walk f n i s = Some (s', r) ->
    exists fb sb', b1_resolve_walk fb n i sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frecompute1 : forall n s sb s' r, b1_state_rel s sb -> recompute f n s = Some (s', r) ->
    exists fb sb', b1_recompute fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frun_memo1 : forall n s sb s' r, b1_state_rel s sb -> run_memo f n s = Some (s', r) ->
    exists fb sb', b1_run_memo fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fexecute1 : forall n s sb s' r, b1_state_rel s sb -> execute_effect f n s = Some (s', r) ->
    exists fb sb', b1_execute_effect fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frun_effect1 : forall n s sb s' r, b1_state_rel s sb -> run_effect f n s = Some (s', r) ->
    exists fb sb', b1_run_effect fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fbody1 : forall n s sb s' r, b1_state_rel s sb -> run_effect_body f n s = Some (s', r) ->
    exists fb sb', b1_run_effect_body fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fdischarge1 : forall o s sb s' r, b1_state_rel s sb -> discharge f o s = Some (s', r) ->
    exists fb sb', b1_discharge fb o sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fretire1 : forall o s sb s' r, b1_state_rel s sb -> retire f o s = Some (s', r) ->
    exists fb sb', b1_retire fb o sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fdispose_scope1 : forall o s sb s' r, b1_state_rel s sb -> dispose_scope f o s = Some (s', r) ->
    exists fb sb', b1_dispose_scope fb o sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fteardown1 : forall o ls cs s sb s' r, b1_state_rel s sb -> teardown f o ls cs s = Some (s', r) ->
    exists fb sb', b1_teardown fb o ls cs sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fcleanup1 : forall o b e s sb s' r, b1_state_rel s sb -> run_cleanup f o b e s = Some (s', r) ->
    exists fb sb', b1_run_cleanup fb o b e sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frelease1 : forall n s sb s' r, b1_state_rel s sb -> release f n s = Some (s', r) ->
    exists fb sb', b1_release fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fdispose1 : forall n s sb s' r, b1_state_rel s sb -> dispose_node f n s = Some (s', r) ->
    exists fb sb', b1_dispose_node fb n sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fattach1 : forall n o s sb s' r, b1_state_rel s sb -> attach f n o s = Some (s', r) ->
    exists fb sb', b1_attach fb n o sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fwrite1 : forall n v s sb s' r, b1_state_rel s sb -> write_signal f n v s = Some (s', r) ->
    exists fb sb', b1_write_signal fb n v sb = Some (sb', r) /\ b1_state_rel s' sb';
  Frequest1 : forall s sb s' r, b1_state_rel s sb -> request_flush f s = Some (s', r) ->
    exists fb sb', b1_request_flush fb sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fsettle1 : forall s sb s' r, b1_state_rel s sb -> settle_flush f s = Some (s', r) ->
    exists fb sb', b1_settle_flush fb sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fflush1 : forall s sb s' r, b1_state_rel s sb -> run_flush f s = Some (s', r) ->
    exists fb sb', b1_run_flush fb sb = Some (sb', r) /\ b1_state_rel s' sb';
  Fdrain1 : forall s sb s' r, b1_state_rel s sb -> drain f s = Some (s', r) ->
    exists fb sb', b1_drain fb sb = Some (sb', r) /\ b1_state_rel s' sb'
}.

Lemma b1_pure_modify upd
    (Hupd : forall p s, upd (with_pull_depth p s) = with_pull_depth p (upd s) /\
                        g_flushing (upd s) = g_flushing s /\ g_pull_depth (upd s) = g_pull_depth s)
    s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> m_modify upd s0 = Some (s1, r) ->
  exists sb1, m_modify upd sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p s. unfold m_modify. destruct (Hupd p s) as [Hw _]. by rewrite Hw.
  - intros s s' [] H. unfold m_modify in H. simplify_eq. by destruct (Hupd 0 s) as [_ ?].
Qed.

Lemma b1_pure_modify_bwd upd
    (Hupd : forall p s, upd (with_pull_depth p s) = with_pull_depth p (upd s) /\
                        g_flushing (upd s) = g_flushing s /\ g_pull_depth (upd s) = g_pull_depth s)
    s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> m_modify upd sb0 = Some (sb1, r) ->
  exists s1, m_modify upd s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p s. unfold m_modify. destruct (Hupd p s) as [Hw _]. by rewrite Hw.
  - intros s s' [] H. unfold m_modify in H. simplify_eq. by destruct (Hupd 0 s) as [_ ?].
Qed.

Lemma b1_pure_next_id s0 sb0 s1 r :
  b1_state_rel s0 sb0 -> next_id s0 = Some (s1, r) ->
  exists sb1, next_id sb0 = Some (sb1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step; [intros p []; unfold next_id; reflexivity|intros ??? H; unfold next_id in H; simplify_eq; split; reflexivity].
Qed.

Lemma b1_pure_next_id_bwd s0 sb0 sb1 r :
  b1_state_rel s0 sb0 -> next_id sb0 = Some (sb1, r) ->
  exists s1, next_id s0 = Some (s1, r) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd; [intros p []; unfold next_id; reflexivity|intros ??? H; unfold next_id in H; simplify_eq; split; reflexivity].
Qed.

Lemma wrap_ex_S (P : nat -> Prop) : (exists f, P (S f)) -> exists f, P f.
Proof. intros [f H]. exists (S f). exact H. Qed.

Local Ltac upd_pull_tac :=
  let st := fresh "st" in
  intros ?p st;
  unfold ghost_publish, ghost_run_end, ghost_run_start, ghost_effect_start,
    ghost_dirty_cleared, ghost_resolved, ghost_registered, ghost_read,
    run_end_unnoticed, slot_holds, add_if, set_node,
    with_batch_depth, with_flush_owed, with_queue_head, with_queue,
    with_owners, with_edges, with_trace, with_running, with_checking,
    with_ghost, with_registered, with_flushing,
    with_current, with_current_owner, with_untracked, with_next_id;
  destruct st; simpl;
  repeat match goal with
  | |- context [match ?m !! ?k with _ => _ end] => destruct (m !! k)
  end;
  repeat split; reflexivity.

Lemma b1_pure_cmp_throws {A} n u (v : A) s0 sb0 s1 a :
  b1_state_rel s0 sb0 -> (let* _ := modify_node n u in m_return v) s0 = Some (s1, a) ->
  exists sb1, (let* _ := modify_node n u in m_return v) sb0 = Some (sb1, a) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step.
  - intros p []. unfold m_bind, modify_node, m_return. simpl. destruct (g_nodes !! n); reflexivity.
  - intros ??? H. unfold m_bind, modify_node, m_return in H. destruct (g_nodes _ !! n); simplify_eq; split; reflexivity.
Qed.

Lemma b1_pure_cmp_throws_bwd {A} n u (v : A) s0 sb0 sb1 a :
  b1_state_rel s0 sb0 -> (let* _ := modify_node n u in m_return v) sb0 = Some (sb1, a) ->
  exists s1, (let* _ := modify_node n u in m_return v) s0 = Some (s1, a) /\ b1_state_rel s1 sb1.
Proof.
  eapply b1_rel_pure_step_bwd.
  - intros p []. unfold m_bind, modify_node, m_return. simpl. destruct (g_nodes !! n); reflexivity.
  - intros ??? H. unfold m_bind, modify_node, m_return in H. destruct (g_nodes _ !! n); simplify_eq; split; reflexivity.
Qed.

Local Ltac b1_mod_fwd :=
  intros;
  try unfold add_node, put_owner, emit, edges, push_running, pop_running, push_checking, pop_checking in *;
  eapply b1_pure_modify; [| |eassumption]; [|eassumption]; upd_pull_tac.

Local Ltac b1_mod_bwd :=
  intros;
  try unfold add_node, put_owner, emit, edges, push_running, pop_running, push_checking, pop_checking in *;
  eapply b1_pure_modify_bwd; [| |eassumption]; [|eassumption]; upd_pull_tac.

Local Ltac b1_pure_fwd :=
  try unfold add_node, put_owner, emit, edges, push_running, pop_running, push_checking, pop_checking;
  first
    [ eapply b1_pure_m_return
    | eapply b1_pure_cmp_throws
    | eapply b1_pure_get_node
    | eapply b1_pure_modify_node
    | eapply b1_pure_get_owner
    | eapply b1_pure_edges_opt
    | eapply b1_pure_current_owner
    | eapply b1_pure_note_host_read
    | eapply b1_pure_track
    | eapply b1_pure_mark_reader
    | eapply b1_pure_announce
    | eapply b1_pure_unlink
    | eapply b1_pure_record_error
    | eapply b1_pure_log_label
    | eapply b1_pure_mark_dirty
    | eapply b1_pure_notify_dirty
    | eapply b1_pure_notify_dirty_except
    | eapply b1_pure_next_id
    | b1_mod_fwd
    ].

Local Ltac b1_pure_bwd :=
  try unfold add_node, put_owner, emit, edges, push_running, pop_running, push_checking, pop_checking;
  first
    [ eapply b1_pure_m_return_bwd
    | eapply b1_pure_cmp_throws_bwd
    | eapply b1_pure_get_node_bwd
    | eapply b1_pure_modify_node_bwd
    | eapply b1_pure_get_owner_bwd
    | eapply b1_pure_edges_opt_bwd
    | eapply b1_pure_current_owner_bwd
    | eapply b1_pure_note_host_read_bwd
    | eapply b1_pure_track_bwd
    | eapply b1_pure_mark_reader_bwd
    | eapply b1_pure_announce_bwd
    | eapply b1_pure_unlink_bwd
    | eapply b1_pure_record_error_bwd
    | eapply b1_pure_log_label_bwd
    | eapply b1_pure_mark_dirty_bwd
    | eapply b1_pure_notify_dirty_bwd
    | eapply b1_pure_notify_dirty_except_bwd
    | eapply b1_pure_next_id_bwd
    | b1_mod_bwd
    ].

Lemma b1_fwd_ret {A} (a : A) s sb s' r :
  b1_state_rel s sb -> m_return a s = Some (s', r) ->
  exists (fb : nat) sb', m_return a sb = Some (sb', r) /\ b1_state_rel s' sb'.
Proof. intros Hr H. unfold m_return in H |- *. simplify_eq. exists 0%nat, sb. auto. Qed.

Lemma b1_bwd_ret {A} (a : A) s sb sb' r :
  b1_state_rel s sb -> m_return a sb = Some (sb', r) ->
  exists (f : nat) s', m_return a s = Some (s', r) /\ b1_state_rel s' sb'.
Proof. intros Hr H. unfold m_return in H |- *. simplify_eq. exists 0%nat, s. auto. Qed.

Lemma b1_fwd_all f : b1_fwd_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - split.
    + intros e c s sb s' r Hrel H.
      apply wrap_ex_S. cbn [b1_exec]. cbn [exec] in H.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0 |- *
      | H0 : context [on_value] |- _ => unfold on_value in H0 |- *
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (unfold m_return in H; simplify_eq; exists 0%nat, sb; split; [reflexivity|exact Hrel]);
      try (simplify_eq; exists 0%nat, sb; split; [reflexivity|exact Hrel]).
      * by eapply (Fexec1 _ IH).
      * destruct (decide _); by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; [by eapply (Fexec1 _ IH)|by eapply b1_fwd_ret].
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; [by eapply (Fexec1 _ IH)|by eapply b1_fwd_ret].
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fwrite1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros ?? sb1 _ _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros ?? sb2 _ _ Hrel2 Hk2].
        by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros ?? sb1 _ _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros ?? sb2 _ _ Hrel2 Hk2].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [o|] ?? _ _ Hrel3 Hk3; [|by eapply b1_fwd_ret]].
        eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fattach1 _ IH)|intros [] ?? _ Hrel4 Hk4].
        by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros ?? sb1 _ _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros ?? sb2 _ _ Hrel2 Hk2].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [o|] ?? _ _ Hrel3 Hk3; [|by eapply b1_fwd_ret]].
        eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fattach1 _ IH)|intros [] ?? _ Hrel4 Hk4].
        eapply (b1_fwd_bind_of Hk4); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; exists f; by eapply b1_pure_mark_dirty|intros [] ?? _ Hrel5 Hk5].
        eapply (b1_fwd_bind_of Hk5); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frequest1 _ IH)|intros [] ?? _ Hrel6 Hk6].
        by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros [o|] ?? _ _ Hrel1 Hk; [|by eapply b1_fwd_ret]].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros x ?? _ _ Hrel2 Hk2].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
        destruct (o_disposed x).
        -- eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel4 Hk4].
           ++ intros s1' [] Hm. eapply (b1_fwd_bind_of Hm); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel4' Hk4'].
              ** intros s2' [] Hd. by eapply b1_detached_fwd; [exact Hrel3|intros ??? Hrel0 ??; by eapply (Fcleanup1 _ IH)|exact Hd].
              ** by eapply (Frequest1 _ IH).
           ++ by eapply (Fexec1 _ IH).
        -- eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros [] ?? _ _ Hrel4 Hk4].
           by eapply (Fexec1 _ IH).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
        destruct (n_kind d); [discriminate| |];
          (eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fdispose1 _ IH)|intros [] ?? _ Hrel2 Hk2];
           eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [destruct (n_disposed d); b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3];
           by eapply (Fexec1 _ IH)).
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros [] ?? _ _ Hrel1 Hk].
        eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fexec1 _ IH)|intros r0 ?? _ Hrel2 Hk2].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
        destruct r0 as [|].
        -- eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frequest1 _ IH)|intros [] ?? _ Hrel4 Hk4].
           by eapply (Fexec1 _ IH).
        -- by eapply b1_fwd_ret.
      * eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros ?? sb1 _ _ Hrel1 Hk].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        by eapply (Fexec1 _ IH).
    + intros n b s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_read]. cbn [read] in H.
      destruct b; [by eapply (Fread_node1 _ IH)|eapply b1_untracked_fwd; [exact Hrel|intros ??? Hrel0 ??; by eapply (Fread_node1 _ IH)|exact H]].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_read_node]. cbn [read_node] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [| |discriminate].
      * eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        by eapply b1_fwd_ret.
      * eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros d' ?? _ Hrel2 Hk2].
        -- intros s1a d'a Hm. destruct (stale_live d).
           ++ eapply (b1_fwd_bind_of Hm); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frop1 _ IH)|intros d'0 ?? _ Hrel2a Hk2a].
              eapply (b1_fwd_bind_of Hk2a); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel3a Hk3a].
              ** intros s2a [] Hm2.
                 eapply (b1_fwd_bind_pure_l_of Hrel2a Hm2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3b Hk3b].
                 eapply (b1_fwd_bind_pure_l_of Hrel3b Hk3b _); [b1_pure_fwd|intros d'' ?? _ _ Hrel4b Hk4b].
                 destruct (stale_live d''); [destruct (b1_pure_mark_reader _ _ _ _ _ Hrel4b Hk4b) as (sb' & E & Hr); exists f, sb'; exact (conj E Hr)|by eapply b1_fwd_ret].
              ** by eapply b1_fwd_ret.
           ++ eapply (b1_fwd_bind_pure_l_of Hrel1 Hm _); [b1_pure_fwd|intros [] ?? _ _ Hrel2b Hk2b].
              exists 0%nat; by eapply b1_pure_get_node.
        -- destruct (n_status d'); by eapply b1_fwd_ret.
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_update_if_necessary]. cbn [update_if_necessary] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [by eapply b1_fwd_ret| |by eapply b1_fwd_ret].
      destruct (stale_live d); [|by eapply b1_fwd_ret].
      eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frop1 _ IH)|intros ??? _ Hrel2 Hk2].
      by eapply b1_fwd_ret.
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_refresh_or_pull]. cbn [refresh_or_pull] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (g_flushing sb) eqn:Efl.
      * unfold deferring_b1. rewrite Efl. simpl.
        destruct (deferring s) eqn:Edef.
        -- eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frefresh1 _ IH)|intros [] ?? _ Hrel1 Hk].
           destruct (b1_pure_get_node _ _ _ _ _ Hrel1 Hk) as (sb' & E & Hr); exists 0%nat, sb'; exact (conj E Hr).
        -- destruct f as [|f']; [discriminate|]. cbn [pull] in H.
           eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1. simplify_eq.
           eapply bind_inv in H as (s2 & [] & E2 & H).
           eapply bind_inv in H as (s3 & d & E3 & H). unfold get_node in E3.
           destruct (g_nodes s2 !! n) as [d0|] eqn:En; [|discriminate]. simplify_eq.
           eapply bind_inv in H as (s4 & [] & E4 & H). unfold m_modify in E4. simplify_eq.
           rewrite bind_get in H.
           eapply bind_inv in H as (s5 & [] & E5 & H). unfold m_return in H. simplify_eq.
           assert (Hrel1 : b1_state_rel (with_pull_depth (S (g_pull_depth s)) s) sb)
             by (by apply b1_rel_with_pull_S_left).
           pose proof (Mrefresh _ (engine_mono_all f') (S f') n (with_pull_depth (S (g_pull_depth s)) s) (s3, ()) ltac:(lia) E2) as E2'.
           destruct (Frefresh1 _ IH n _ _ _ _ Hrel1 E2') as (fb & sb2 & Eb2 & Hrel2).
           destruct (engine_depths_kept f') as [_ _ _ _ _ _ Dref _].
           pose proof (Dref n _ _ _ E2) as [Hp2 Hfl2].
           destruct (b1_depths_kept fb) as [_ _ _ _ _ _ B1Dref _].
           pose proof (B1Dref n _ _ _ Eb2) as [Hpb2 Hflb2].
           assert (Hfls4 : g_flushing (with_pull_depth (pred (g_pull_depth s3)) s3) = true).
           { simpl. rewrite Hfl2. simpl. destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hf&_). congruence. }
           assert (Hs' : s' = with_pull_depth (pred (g_pull_depth s3)) s3).
           { destruct (_ && _); [by destruct (settle_flush_flushing _ _ _ _ Hfls4 E5)|unfold m_return in E5; by simplify_eq]. }
           subst s'.
           assert (Hrel5 : b1_state_rel (with_pull_depth (pred (g_pull_depth s3)) s3) sb2).
           { destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfs&_&Hts).
             destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
             - rewrite <- Hw2. unfold with_pull_depth. destruct s3; reflexivity.
             - simpl. split; [intro; congruence|]. intro Hfl0. rewrite Hp2. simpl.
               pose proof (Hts Efl) as Hle. rewrite Hpb2. lia. }
           assert (Eget0 : get_node n (with_pull_depth (pred (g_pull_depth s3)) s3) = Some (with_pull_depth (pred (g_pull_depth s3)) s3, r))
             by (unfold get_node, with_pull_depth; simpl; by rewrite En).
           destruct (b1_pure_get_node _ _ _ _ _ Hrel5 Eget0) as (sb' & Eget & Hrel').
           exists fb, sb'. split; [|exact Hrel'].
           unfold m_bind. rewrite Eb2. simpl. exact Eget.
      * destruct (b1_rel_destruct _ _ Hrel) as (_ & _ & Hfs & Hfp & _).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        unfold deferring_b1. rewrite Efl. simpl.
        destruct (deferring s).
        -- eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frefresh1 _ IH)|intros [] ?? _ Hrel1 Hk].
           exists 0%nat; by eapply b1_pure_get_node.
        -- by eapply (Fpull1 _ IH).
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_pull]. cbn [pull] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _);
        [intros sa0 sba0 sa1 [] Hr0 Hm; unfold m_modify in Hm; simplify_eq; eexists; split; [unfold m_modify; reflexivity|by apply b1_rel_with_pull_S]|intros [] ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frefresh1 _ IH)|intros [] ?? _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros d ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _);
        [intros s_p2 sb_p2 s_p3 [] Hr0 Hm; unfold m_modify in Hm; simplify_eq; eexists; split; [unfold m_modify; reflexivity|by apply b1_rel_with_pull_pred]|intros [] s3 sb4 _ _ Hrel4 Hk4].
      rewrite bind_get in Hk4. setoid_rewrite bind_get.
      eapply (b1_fwd_bind_of Hk4); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel5 Hk5; by eapply b1_fwd_ret].
      intros s_q a Hm.
      destruct (g_flushing s3) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel4) as (_&_&Hfl&_).
        assert (Hflb : g_flushing sb4 = true) by congruence.
        assert (Hs1 : s_q = s3 /\ a = tt).
        { destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3).
          - by eapply settle_flush_flushing.
          - unfold m_return in Hm. by simplify_eq. }
        destruct Hs1 as [-> ->].
        exists 2%nat, sb4. split; [|exact Hrel4].
        destruct (bool_decide (g_pull_depth sb4 = 0) && g_flush_owed sb4); [|reflexivity].
        cbn [b1_settle_flush]. rewrite bind_get.
        destruct (g_flush_owed sb4 && bool_decide (g_batch_depth sb4 = 0) && outside_bodies sb4); [|reflexivity].
        cbn [b1_run_flush]. rewrite bind_get, Hflb. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel4) as (_&_&Hfl&Hfp&_).
        assert (Hsb4 : s3 = sb4) by (apply b1_state_rel_eq; [exact Hrel4|apply Hfp; congruence]).
        subst sb4.
        destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3); [by eapply (Fsettle1 _ IH)|by eapply b1_fwd_ret].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_refresh]. cbn [refresh] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
      destruct (decide (n ∈ g_checking s)), (decide (n ∈ g_checking sb)); try (exfalso; rewrite <- Hchk in *; contradiction).
      { unfold m_step in H |- *. destruct (mark_dirty f n s) eqn:Em; simplify_eq.
        destruct (b1_pure_mark_dirty f n s sb s' tt Hrel ltac:(unfold m_step; by rewrite Em)) as (sb1 & Em1 & Hrel1).
        unfold m_step in Em1. destruct (mark_dirty f n sb) eqn:Emb; simplify_eq.
        exists f, sb1. split; [by rewrite Emb|exact Hrel1]. }
      destruct (decide (n ∈ g_running s)), (decide (n ∈ g_running sb)); try (exfalso; rewrite <- Hrun in *; contradiction); [by eapply b1_fwd_ret|].
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel2 Hk2].
      * intros s_ref [] Hm. destruct (decide (n_freshness d = Check)); [|by eapply b1_fwd_ret].
        eapply (b1_fwd_bind_pure_l_of Hrel1 Hm _); [b1_pure_fwd|intros [] ?? _ _ Hrel2a Hk2a].
        eapply (b1_fwd_bind_of Hk2a); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fwalk1 _ IH)|intros [] ?? _ Hrel3a Hk3a].
        eapply (b1_fwd_bind_pure_l_of Hrel3a Hk3a _); [b1_pure_fwd|intros [] ?? _ _ Hrel4a Hk4a].
        eapply (b1_fwd_bind_pure_l_of Hrel4a Hk4a _); [b1_pure_fwd|intros d' ?? _ _ Hrel5a Hk5a].
        destruct (decide (n_freshness d' = Check)); [|by eapply b1_fwd_ret].
        eapply (b1_fwd_bind_pure_l_of Hrel5a Hk5a _); [b1_pure_fwd|intros [] ?? _ _ Hrel6a Hk6a].
        exists 0%nat; by eapply b1_pure_modify_node.
      * eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros d' ?? _ _ Hrel3 Hk3].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Frecompute1 _ IH)|by eapply b1_fwd_ret].
    + intros n i s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_resolve_walk]. cbn [resolve_walk] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d s1 sb1 _ _ Hrel1 Hk].
      rewrite bind_get in Hk. setoid_rewrite bind_get.
      destruct (decide (n_freshness d = Check)); [|by eapply b1_fwd_ret].
      destruct (b1_rel_fields _ _ Hrel1) as (_&He1&_).
      destruct (source_slots (sources_of (g_edges s1) n) !! i) as [[src ?]|] eqn:Es1,
               (source_slots (sources_of (g_edges sb1) n) !! i) as [[srcb ?]|] eqn:Esb1;
        try (exfalso; rewrite <- He1, Es1 in Esb1; discriminate).
      * assert (srcb = src) by (rewrite <- He1, Es1 in Esb1; congruence); subst srcb.
        eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fupdate1 _ IH)|intros [] ?? _ Hrel2 Hk2].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros d' ?? _ _ Hrel3 Hk3].
        eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros d_src s4 sb4 _ _ Hrel4 Hk4].
        rewrite bind_get in Hk4. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel4) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hrun4&Hchk4&_).
        eapply (b1_fwd_bind_of Hk4); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel5 Hk5; by eapply (Fwalk1 _ IH)].
        intros s5 [] Hm.
        destruct (decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s4 \/ src ∈ g_checking s4))) as [Hd|Hd];
          destruct (decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running sb4 \/ src ∈ g_checking sb4))) as [Hdb|Hdb];
          try (exfalso; rewrite <- Hrun4, <- Hchk4 in *; tauto).
        -- unfold m_step in Hm |- *. destruct (mark_dirty f n s4) eqn:Em; simplify_eq.
           destruct (b1_pure_mark_dirty f n s4 sb4 s5 tt Hrel4 ltac:(unfold m_step; by rewrite Em)) as (sb5 & Em5 & Hrel5).
           unfold m_step in Em5. destruct (mark_dirty f n sb4) eqn:Emb; simplify_eq.
           exists f, sb5. split; [by rewrite Emb|exact Hrel5].
        -- by eapply b1_fwd_ret.
      * assert (Hsh : slot_holds stale_live s1 n = slot_holds stale_live sb1 n).
        { destruct (b1_rel_fields _ _ Hrel1) as (_&He'&Hn'&_). unfold slot_holds. by rewrite He', Hn'. }
        destruct (slot_holds stale_live s1 n) eqn:Esh1, (slot_holds stale_live sb1 n) eqn:Esh2;
          try (exfalso; congruence); [by eapply (Fwalk1 _ IH)|by eapply b1_fwd_ret].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_recompute]. cbn [recompute] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_scope d) as [o|]; [|by eapply (Frun_memo1 _ IH)].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fdischarge1 _ IH)|intros [] ?? _ Hrel4 Hk4].
      eapply (b1_fwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_fwd|intros d' ?? _ _ Hrel5 Hk5].
      destruct (n_disposed d').
      * destruct (decide (n_freshness d' = Clean)); [exists 0%nat; by eapply b1_pure_modify_node|by eapply b1_fwd_ret].
      * destruct (decide (n_runs d' = n_runs d)); [by eapply (Frun_memo1 _ IH)|].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Frecompute1 _ IH)|by eapply b1_fwd_ret].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_run_memo]. cbn [run_memo] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros [] ?? _ _ Hrel4 Hk4].
      eapply (b1_fwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_fwd|intros [] ?? _ _ Hrel5 Hk5].
      eapply (b1_fwd_bind_of Hk5); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros r0 ?? _ Hrel6 Hk6].
      { intros s6 r0a Hm. by eapply b1_hosted_fwd; [exact Hrel5|intros ??? Hrel0 ??; by eapply (Fexec1 _ IH)|exact Hm]. }
      eapply (b1_fwd_bind_pure_l_of Hrel6 Hk6 _); [b1_pure_fwd|intros d1 ?? _ _ Hrel7 Hk7].
      eapply (b1_fwd_bind_pure_l_of Hrel7 Hk7 _); [destruct (if n_violated d1 then Fail pure_scope_failure else r0); b1_pure_fwd|intros [] ?? _ _ Hrel8 Hk8].
      eapply (b1_fwd_bind_pure_l_of Hrel8 Hk8 _); [b1_pure_fwd|intros [] ?? _ _ Hrel9 Hk9].
      eapply (b1_fwd_bind_pure_l_of Hrel9 Hk9 _); [b1_pure_fwd|intros [] ?? _ _ Hrel10 Hk10].
      eapply (b1_fwd_bind_pure_l_of Hrel10 Hk10 _); [destruct (if n_violated d1 then Fail pure_scope_failure else r0); b1_pure_fwd|intros [] ?? _ _ Hrel11 Hk11].
      eapply (b1_fwd_bind_pure_l_of Hrel11 Hk11 _); [b1_pure_fwd|intros d2 ?? _ _ Hrel12 Hk12].
      eapply (b1_fwd_bind_pure_l_of Hrel12 Hk12 _);
        [destruct (decide _); [b1_pure_fwd|destruct (n_comparer d2); b1_pure_fwd]|intros moved s13 sb13 _ _ Hrel13 Hk13].
      rewrite bind_get in Hk13. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel13) as (_&_&_&_&_&_&_&_&_&Hc13&_).
      destruct (g_current s13) as [c13|] eqn:Ecs1, (g_current sb13) as [cb13|] eqn:Ecs2;
        try (exfalso; rewrite Hc13 in Ecs1; congruence);
        [assert (cb13 = c13) by congruence; subst cb13|];
        (eapply (b1_fwd_bind_pure_l_of Hrel13 Hk13 _); [b1_pure_fwd|intros [] ?? _ _ Hrel14 Hk14];
         eapply (b1_fwd_bind_of Hk14); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel15 Hk15];
         [intros s15 [] Hm; destruct moved; [|by eapply b1_fwd_ret];
          eapply (b1_fwd_bind_of Hm); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; exists f; by eapply b1_pure_notify_dirty_except|intros [] s16 sb16 _ Hrel16 Hk16];
          rewrite bind_get in Hk16; setoid_rewrite bind_get;
          destruct (b1_rel_fields _ _ Hrel16) as (_&_&_&_&Hq16&Hqh16&Hfl16&_&Hbd16&_);
          assert (Hcond : bool_decide (g_batch_depth s16 = 0%nat) && negb (g_flushing s16) && live_queue s16 =
                          bool_decide (g_batch_depth sb16 = 0%nat) && negb (g_flushing sb16) && live_queue sb16)
            by (unfold live_queue; by rewrite Hbd16, Hfl16, Hq16, Hqh16);
          destruct (bool_decide (g_batch_depth s16 = 0%nat) && negb (g_flushing s16) && live_queue s16) eqn:Ec1,
                   (bool_decide (g_batch_depth sb16 = 0%nat) && negb (g_flushing sb16) && live_queue sb16) eqn:Ec2;
            try (exfalso; congruence); [exists 0%nat; b1_mod_fwd|by eapply b1_fwd_ret]
         |eapply (b1_fwd_bind_pure_l_of Hrel15 Hk15 _); [b1_pure_fwd|intros d3 ?? _ _ Hrel16 Hk16];
          destruct (decide (n_freshness d3 = Clean)); [by eapply b1_fwd_ret|exists 0%nat; by eapply b1_pure_announce]]).
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_execute_effect]. cbn [execute_effect] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_disposed d); [exists 0%nat; by eapply b1_pure_modify_node|].
      eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel2 Hk2].
      * intros s2 [] Hm. destruct (decide (n_freshness d = Check)); [|by eapply b1_fwd_ret].
        eapply (b1_fwd_bind_of Hm); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fwalk1 _ IH)|intros [] ?? _ Hrel3 Hk3].
        eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros d' ?? _ _ Hrel4 Hk4].
        destruct (decide (n_freshness d' = Check)); [|by eapply b1_fwd_ret].
        eapply (b1_fwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_fwd|intros [] ?? _ _ Hrel5 Hk5].
        exists 0%nat; by eapply b1_pure_modify_node.
      * eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros d' ?? _ _ Hrel3 Hk3].
        eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros [] ?? _ _ Hrel4 Hk4].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Frun_effect1 _ IH)|by eapply b1_fwd_ret].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_run_effect]. cbn [run_effect] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros [] ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros d ?? _ _ Hrel4 Hk4].
      eapply (b1_fwd_bind_of Hk4); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel5 Hk5].
      * intros s5 [] Hm. destruct (n_scope d) as [o|]; [|by eapply b1_fwd_ret].
        eapply (b1_fwd_bind_pure_l_of Hrel4 Hm _); [b1_pure_fwd|intros [] ?? _ _ Hrel5a Hk5a].
        eapply (b1_fwd_bind_of Hk5a); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fdischarge1 _ IH)|intros [] ?? _ Hrel6a Hk6a].
        exists 0%nat; by eapply b1_pure_modify_node.
      * eapply (b1_fwd_bind_pure_l_of Hrel5 Hk5 _); [b1_pure_fwd|intros d' ?? _ _ Hrel6 Hk6].
        eapply (b1_fwd_bind_of Hk6); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] ?? _ Hrel7 Hk7; exists 0%nat; b1_mod_fwd].
        intros s7 [] Hm. destruct (n_disposed d'); [by eapply b1_fwd_ret|by eapply (Fbody1 _ IH)].
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_run_effect_body]. cbn [run_effect_body] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros r0 ?? _ Hrel4 Hk4].
      { intros s4 r0a Hm. by eapply b1_hosted_fwd; [exact Hrel3|intros ??? Hrel0 ??; by eapply (Fexec1 _ IH)|exact Hm]. }
      eapply (b1_fwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_fwd|intros d' ?? _ _ Hrel5 Hk5].
      eapply (b1_fwd_bind_pure_l_of Hrel5 Hk5 _); [b1_pure_fwd|intros [] ?? _ _ Hrel6 Hk6].
      destruct r0; [by eapply b1_fwd_ret|].
      eapply (b1_fwd_bind_pure_l_of Hrel6 Hk6 _); [b1_pure_fwd|intros [] ?? _ _ Hrel7 Hk7].
      exists 0%nat; b1_mod_fwd.
    + intros o s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_discharge]. cbn [discharge] in H.
      eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] s1 sb1 _ Hrel1 Hk].
      * intros s1 [] Hm. by eapply b1_detached_fwd; [exact Hrel|intros ??? Hrel0 ??; by eapply (Fdispose_scope1 _ IH)|exact Hm].
      * rewrite bind_get in Hk. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel1) as (_&_&_&_&Hq1&Hqh1&Hfl1&_&Hbd1&_).
        assert (Hcond : bool_decide (g_batch_depth s1 = 0%nat) && negb (g_flushing s1) && live_queue s1 =
                        bool_decide (g_batch_depth sb1 = 0%nat) && negb (g_flushing sb1) && live_queue sb1)
          by (unfold live_queue; by rewrite Hbd1, Hfl1, Hq1, Hqh1).
        destruct (bool_decide (g_batch_depth s1 = 0%nat) && negb (g_flushing s1) && live_queue s1) eqn:Ec1,
                 (bool_decide (g_batch_depth sb1 = 0%nat) && negb (g_flushing sb1) && live_queue sb1) eqn:Ec2;
          try (exfalso; congruence); [exists 0%nat; b1_mod_fwd|by eapply b1_fwd_ret].
    + intros o s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_retire]. cbn [retire] in H.
      eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac| |intros [] s1 sb1 _ Hrel1 Hk].
      * intros s1' [] Hm. eapply b1_detached_fwd; [exact Hrel| |exact Hm].
        intros s0 sb0 Hrel0 s1'' r' Hm0.
        eapply (b1_fwd_bind_pure_l_of Hrel0 Hm0 _); [b1_pure_fwd|intros x ?? _ _ Hrel2 Hk2].
        destruct (o_disposed x); [by eapply b1_fwd_ret|].
        eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
        by eapply (Fdispose_scope1 _ IH).
      * rewrite bind_get in Hk. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel1) as (_&_&_&_&Hq1&Hqh1&_).
        assert (Hlq : live_queue s1 = live_queue sb1) by (unfold live_queue; by rewrite Hq1, Hqh1).
        destruct (live_queue s1) eqn:El1, (live_queue sb1) eqn:El2;
          try (exfalso; congruence); [by eapply (Frequest1 _ IH)|by eapply b1_fwd_ret].
    + intros o s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_dispose_scope]. cbn [dispose_scope] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros x ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      by eapply (Fteardown1 _ IH).
    + intros o ls cs s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_teardown]. cbn [teardown] in H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [by eapply b1_fwd_ret|..].
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fcleanup1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Fteardown1 _ IH).
      * eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frelease1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Fteardown1 _ IH).
      * destruct (decide (a < l_acquired l)).
        -- eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frelease1 _ IH)|intros [] ?? _ Hrel1 Hk].
           by eapply (Fteardown1 _ IH).
        -- eapply (b1_fwd_bind_of H); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fcleanup1 _ IH)|intros [] ?? _ Hrel1 Hk].
           by eapply (Fteardown1 _ IH).
    + intros o b e s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_run_cleanup]. cbn [run_cleanup] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros x ?? _ _ Hrel1 Hk].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_of Hk2); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Fexec1 _ IH)|intros r0 ?? _ Hrel3 Hk3].
      destruct r0; [by eapply b1_fwd_ret|].
      eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros [] ?? _ _ Hrel4 Hk4].
      exists 0%nat; b1_mod_fwd.
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_release]. cbn [release] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros [] ?? _ _ Hrel1 Hk].
      by eapply (Fdispose1 _ IH).
    + intros n s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_dispose_node]. cbn [dispose_node] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_disposed d); [by eapply b1_fwd_ret|].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [destruct (n_link d); b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_fwd|intros [] ?? _ _ Hrel4 Hk4].
      destruct (n_scope d); [by eapply (Fretire1 _ IH)|by eapply b1_fwd_ret].
    + intros n o s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_attach]. cbn [attach] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros x ?? _ _ Hrel1 Hk].
      destruct (o_disposed x).
      * eapply (b1_fwd_bind_of Hk); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; by eapply (Frelease1 _ IH)|intros [] ?? _ Hrel2 Hk2].
        exists 0%nat; by eapply b1_pure_modify_node.
      * eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
        exists 0%nat; by eapply b1_pure_modify_node.
    + intros n v s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_write_signal]. cbn [write_signal] in H.
      eapply (b1_fwd_bind_pure_l_of Hrel H _); [b1_pure_fwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [by eapply b1_fwd_ret|].
      eapply (b1_fwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_fwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_fwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_fwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_fwd_bind_of Hk3); [intros ????; b1_mono_tac|intros ?????; b1_mono_tac|intros ???; exists f; by eapply b1_pure_notify_dirty|intros [] ?? _ Hrel4 Hk4].
      by eapply (Frequest1 _ IH).
    + intros s sb s' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        assert (Hfls : g_flushing s = true) by congruence.
        destruct (request_flush_flushing _ _ _ _ Hfls H) as [-> ->].
        exists 2%nat, sb. split; [|exact Hrel].
        cbn [b1_request_flush]. rewrite bind_get.
        destruct (live_queue sb && bool_decide (g_batch_depth sb = 0)); [|reflexivity].
        destruct (outside_bodies sb); [|by rewrite Efl].
        cbn [b1_run_flush]. rewrite bind_get, Efl. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        apply wrap_ex_S. cbn [b1_request_flush]. cbn [request_flush] in H.
        rewrite bind_get in H. setoid_rewrite bind_get.
        destruct (live_queue s && bool_decide (g_batch_depth s = 0));
          [|unfold m_return in H; simplify_eq; exists 0%nat, s'; split; [reflexivity|apply b1_state_rel_refl]].
        destruct (outside_bodies s); [by eapply (Fflush1 _ IH)|].
        rewrite Efl in H. unfold m_modify in H; simplify_eq.
        exists 0%nat, (with_flush_owed true s); split; [rewrite Efl; reflexivity|apply b1_state_rel_refl].
    + intros s sb s' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        assert (Hfls : g_flushing s = true) by congruence.
        destruct (settle_flush_flushing _ _ _ _ Hfls H) as [-> ->].
        exists 2%nat, sb. split; [|exact Hrel].
        cbn [b1_settle_flush]. rewrite bind_get.
        destruct (g_flush_owed sb && bool_decide (g_batch_depth sb = 0) && outside_bodies sb); [|reflexivity].
        cbn [b1_run_flush]. rewrite bind_get, Efl. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        apply wrap_ex_S. cbn [b1_settle_flush]. cbn [settle_flush] in H.
        rewrite bind_get in H. setoid_rewrite bind_get.
        destruct (g_flush_owed s && bool_decide (g_batch_depth s = 0) && outside_bodies s);
          [by eapply (Fflush1 _ IH)|unfold m_return in H; simplify_eq; exists 0%nat, s'; split; [reflexivity|apply b1_state_rel_refl]].
    + intros s sb s' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        cbn [run_flush] in H. rewrite bind_get in H.
        assert (Hsfl : g_flushing s = true) by congruence. rewrite Hsfl in H.
        unfold m_return in H. simplify_eq.
        exists 1%nat, sb. split; [cbn [b1_run_flush]; rewrite bind_get, Efl; reflexivity|exact Hrel].
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        cbn [run_flush] in H. rewrite bind_get, Efl in H.
        unfold m_bind, m_modify in H.
        set (s1 := with_flush_owed false (with_flushing true s)) in H.
        destruct (drain f s1) as [[s2 []]|] eqn:Ed; simpl in H; [|discriminate].
        simplify_eq.
        destruct (Fdrain1 _ IH _ _ _ _ (b1_state_rel_refl s1) Ed) as (fb & sb2 & Ebd & Hrel2).
        destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Ddrain].
        destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Ddrain].
        pose proof (Ddrain _ _ _ Ed) as [Hp2 _].
        pose proof (B1Ddrain _ _ _ Ebd) as [Hpb2 _].
        assert (Hs2 : s2 = sb2) by (apply b1_state_rel_eq; [exact Hrel2|congruence]). subst sb2.
        exists (S fb), (with_flushing false s2). split.
        -- cbn [b1_run_flush]. unfold m_bind, m_get, m_modify. rewrite Efl. fold s1. rewrite Ebd. reflexivity.
        -- apply b1_state_rel_refl.
    + intros s sb s' r Hrel H. apply wrap_ex_S. cbn [b1_drain]. cbn [drain] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&Hq&Hqh&_).
      destruct (g_queue s !! g_queue_head s) as [n|] eqn:Eq1,
               (g_queue sb !! g_queue_head sb) as [nb|] eqn:Eq2;
        try (exfalso; rewrite <- Hq, <- Hqh, Eq1 in Eq2; discriminate).
      * assert (nb = n) by (rewrite <- Hq, <- Hqh, Eq1 in Eq2; congruence); subst nb.
        eapply bind_inv in H as (s1 & [] & E1 & H). unfold m_modify in E1; simplify_eq.
        eapply bind_inv in H as (s2 & [] & E2 & E3).
        assert (Hrel1 : b1_state_rel (with_queue_head (S (g_queue_head s)) s) (with_queue_head (S (g_queue_head sb)) sb))
          by (rewrite Hqh; b1_rel_simple).
        destruct (Fexecute1 _ IH n _ _ _ _ Hrel1 E2) as (f1 & sb2 & Eb2 & Hrel2).
        destruct (Fdrain1 _ IH _ _ _ _ Hrel2 E3) as (f2 & sb3 & Eb3 & Hrel3).
        exists (Nat.max f1 f2), sb3. split; [|exact Hrel3].
        unfold m_bind, m_modify.
        rewrite (B1Mexecute _ (b1_mono_all f1) (Nat.max f1 f2) _ _ _ (Nat.le_max_l f1 f2) Eb2).
        exact (B1Mdrain _ (b1_mono_all f2) (Nat.max f1 f2) _ _ (Nat.le_max_r f1 f2) Eb3).
      * exists 0%nat; eapply b1_pure_modify; [|exact Hrel|exact H]; upd_pull_tac.
Qed.

Record b1_bwd_at (fb : nat) : Prop := {
  Bexec1 : forall e c s sb sb' r, b1_state_rel s sb -> b1_exec fb e c sb = Some (sb', r) ->
    exists f s', exec f e c s = Some (s', r) /\ b1_state_rel s' sb';
  Bread1 : forall n b s sb sb' r, b1_state_rel s sb -> b1_read fb n b sb = Some (sb', r) ->
    exists f s', read f n b s = Some (s', r) /\ b1_state_rel s' sb';
  Bread_node1 : forall n s sb sb' r, b1_state_rel s sb -> b1_read_node fb n sb = Some (sb', r) ->
    exists f s', read_node f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bupdate1 : forall n s sb sb' r, b1_state_rel s sb -> b1_update_if_necessary fb n sb = Some (sb', r) ->
    exists f s', update_if_necessary f n s = Some (s', r) /\ b1_state_rel s' sb';
  Brop1 : forall n s sb sb' r, b1_state_rel s sb -> b1_refresh_or_pull fb n sb = Some (sb', r) ->
    exists f s', refresh_or_pull f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bpull1 : forall n s sb sb' r, b1_state_rel s sb -> b1_pull fb n sb = Some (sb', r) ->
    exists f s', pull f n s = Some (s', r) /\ b1_state_rel s' sb';
  Brefresh1 : forall n s sb sb' r, b1_state_rel s sb -> b1_refresh fb n sb = Some (sb', r) ->
    exists f s', refresh f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bwalk1 : forall n i s sb sb' r, b1_state_rel s sb -> b1_resolve_walk fb n i sb = Some (sb', r) ->
    exists f s', resolve_walk f n i s = Some (s', r) /\ b1_state_rel s' sb';
  Brecompute1 : forall n s sb sb' r, b1_state_rel s sb -> b1_recompute fb n sb = Some (sb', r) ->
    exists f s', recompute f n s = Some (s', r) /\ b1_state_rel s' sb';
  Brun_memo1 : forall n s sb sb' r, b1_state_rel s sb -> b1_run_memo fb n sb = Some (sb', r) ->
    exists f s', run_memo f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bexecute1 : forall n s sb sb' r, b1_state_rel s sb -> b1_execute_effect fb n sb = Some (sb', r) ->
    exists f s', execute_effect f n s = Some (s', r) /\ b1_state_rel s' sb';
  Brun_effect1 : forall n s sb sb' r, b1_state_rel s sb -> b1_run_effect fb n sb = Some (sb', r) ->
    exists f s', run_effect f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bbody1 : forall n s sb sb' r, b1_state_rel s sb -> b1_run_effect_body fb n sb = Some (sb', r) ->
    exists f s', run_effect_body f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bdischarge1 : forall o s sb sb' r, b1_state_rel s sb -> b1_discharge fb o sb = Some (sb', r) ->
    exists f s', discharge f o s = Some (s', r) /\ b1_state_rel s' sb';
  Bretire1 : forall o s sb sb' r, b1_state_rel s sb -> b1_retire fb o sb = Some (sb', r) ->
    exists f s', retire f o s = Some (s', r) /\ b1_state_rel s' sb';
  Bdispose_scope1 : forall o s sb sb' r, b1_state_rel s sb -> b1_dispose_scope fb o sb = Some (sb', r) ->
    exists f s', dispose_scope f o s = Some (s', r) /\ b1_state_rel s' sb';
  Bteardown1 : forall o ls cs s sb sb' r, b1_state_rel s sb -> b1_teardown fb o ls cs sb = Some (sb', r) ->
    exists f s', teardown f o ls cs s = Some (s', r) /\ b1_state_rel s' sb';
  Bcleanup1 : forall o b e s sb sb' r, b1_state_rel s sb -> b1_run_cleanup fb o b e sb = Some (sb', r) ->
    exists f s', run_cleanup f o b e s = Some (s', r) /\ b1_state_rel s' sb';
  Brelease1 : forall n s sb sb' r, b1_state_rel s sb -> b1_release fb n sb = Some (sb', r) ->
    exists f s', release f n s = Some (s', r) /\ b1_state_rel s' sb';
  Bdispose1 : forall n s sb sb' r, b1_state_rel s sb -> b1_dispose_node fb n sb = Some (sb', r) ->
    exists f s', dispose_node f n s = Some (s', r) /\ b1_state_rel s' sb';
  Battach1 : forall n o s sb sb' r, b1_state_rel s sb -> b1_attach fb n o sb = Some (sb', r) ->
    exists f s', attach f n o s = Some (s', r) /\ b1_state_rel s' sb';
  Bwrite1 : forall n v s sb sb' r, b1_state_rel s sb -> b1_write_signal fb n v sb = Some (sb', r) ->
    exists f s', write_signal f n v s = Some (s', r) /\ b1_state_rel s' sb';
  Brequest1 : forall s sb sb' r, b1_state_rel s sb -> b1_request_flush fb sb = Some (sb', r) ->
    exists f s', request_flush f s = Some (s', r) /\ b1_state_rel s' sb';
  Bsettle1 : forall s sb sb' r, b1_state_rel s sb -> b1_settle_flush fb sb = Some (sb', r) ->
    exists f s', settle_flush f s = Some (s', r) /\ b1_state_rel s' sb';
  Bflush1 : forall s sb sb' r, b1_state_rel s sb -> b1_run_flush fb sb = Some (sb', r) ->
    exists f s', run_flush f s = Some (s', r) /\ b1_state_rel s' sb';
  Bdrain1 : forall s sb sb' r, b1_state_rel s sb -> b1_drain fb sb = Some (sb', r) ->
    exists f s', drain f s = Some (s', r) /\ b1_state_rel s' sb'
}.

Lemma b1_bwd_all fb : b1_bwd_at fb.
Proof.
  induction fb as [|fb IH].
  - split; intros; discriminate.
  - split.
    + intros e c s sb sb' r Hrel H.
      apply wrap_ex_S. cbn [exec]. cbn [b1_exec] in H.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0 |- *
      | H0 : context [on_value] |- _ => unfold on_value in H0 |- *
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (unfold m_return in H; simplify_eq; exists 0%nat, s; split; [reflexivity|exact Hrel]);
      try (simplify_eq; exists 0%nat, s; split; [reflexivity|exact Hrel]).
      * by eapply (Bexec1 _ IH).
      * destruct (decide _); by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; [by eapply (Bexec1 _ IH)|by eapply b1_bwd_ret].
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bread1 _ IH)|intros o ?? _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        destruct o; [by eapply (Bexec1 _ IH)|by eapply b1_bwd_ret].
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bwrite1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros ?? s1 _ _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros ?? s2 _ _ Hrel2 Hk2].
        by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros ?? s1 _ _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros ?? s2 _ _ Hrel2 Hk2].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [o|] ?? _ _ Hrel3 Hk3; [|by eapply b1_bwd_ret]].
        eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Battach1 _ IH)|intros [] ?? _ Hrel4 Hk4].
        by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros ?? s1 _ _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros ?? s2 _ _ Hrel2 Hk2].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [o|] ?? _ _ Hrel3 Hk3; [|by eapply b1_bwd_ret]].
        eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Battach1 _ IH)|intros [] ?? _ Hrel4 Hk4].
        eapply (b1_bwd_bind_of Hk4); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; exists fb; by eapply b1_pure_mark_dirty_bwd|intros [] ?? _ Hrel5 Hk5].
        eapply (b1_bwd_bind_of Hk5); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brequest1 _ IH)|intros [] ?? _ Hrel6 Hk6].
        by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros [o|] ?? _ _ Hrel1 Hk; [|by eapply b1_bwd_ret]].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros x ?? _ _ Hrel2 Hk2].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
        destruct (o_disposed x).
        -- eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel4 Hk4].
           ++ intros sb1' [] Hm. eapply (b1_bwd_bind_of Hm); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel4' Hk4'].
              ** intros sb2' [] Hd. by eapply b1_detached_bwd; [exact Hrel3|intros ??? Hrel0 ??; by eapply (Bcleanup1 _ IH)|exact Hd].
              ** by eapply (Brequest1 _ IH).
           ++ by eapply (Bexec1 _ IH).
        -- eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros [] ?? _ _ Hrel4 Hk4].
           by eapply (Bexec1 _ IH).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
        destruct (n_kind d); [discriminate| |];
          (eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bdispose1 _ IH)|intros [] ?? _ Hrel2 Hk2];
           eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [destruct (n_disposed d); b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3];
           by eapply (Bexec1 _ IH)).
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros [] ?? _ _ Hrel1 Hk].
        eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bexec1 _ IH)|intros r0 ?? _ Hrel2 Hk2].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
        destruct r0 as [|].
        -- eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brequest1 _ IH)|intros [] ?? _ Hrel4 Hk4].
           by eapply (Bexec1 _ IH).
        -- by eapply b1_bwd_ret.
      * eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros ?? s1 _ _ Hrel1 Hk].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        by eapply (Bexec1 _ IH).
    + intros n b s sb sb' r Hrel H. apply wrap_ex_S. cbn [read]. cbn [b1_read] in H.
      destruct b; [by eapply (Bread_node1 _ IH)|eapply b1_untracked_bwd; [exact Hrel|intros ??? Hrel0 ??; by eapply (Bread_node1 _ IH)|exact H]].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [read_node]. cbn [b1_read_node] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [| |discriminate].
      * eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        by eapply b1_bwd_ret.
      * eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros d' ?? _ Hrel2 Hk2].
        -- intros sb1a d'a Hm. destruct (stale_live d).
           ++ eapply (b1_bwd_bind_of Hm); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brop1 _ IH)|intros d'0 ?? _ Hrel2a Hk2a].
              eapply (b1_bwd_bind_of Hk2a); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel3a Hk3a].
              ** intros sb2a [] Hm2.
                 eapply (b1_bwd_bind_pure_l_of Hrel2a Hm2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3b Hk3b].
                 eapply (b1_bwd_bind_pure_l_of Hrel3b Hk3b _); [b1_pure_bwd|intros d'' ?? _ _ Hrel4b Hk4b].
                 destruct (stale_live d''); [destruct (b1_pure_mark_reader_bwd _ _ _ _ _ Hrel4b Hk4b) as (s' & E & Hr); exists fb, s'; exact (conj E Hr)|by eapply b1_bwd_ret].
              ** by eapply b1_bwd_ret.
           ++ eapply (b1_bwd_bind_pure_l_of Hrel1 Hm _); [b1_pure_bwd|intros [] ?? _ _ Hrel2b Hk2b].
              exists 0%nat; by eapply b1_pure_get_node_bwd.
        -- destruct (n_status d'); by eapply b1_bwd_ret.
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [update_if_necessary]. cbn [b1_update_if_necessary] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [by eapply b1_bwd_ret| |by eapply b1_bwd_ret].
      destruct (stale_live d); [|by eapply b1_bwd_ret].
      eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brop1 _ IH)|intros ??? _ Hrel2 Hk2].
      by eapply b1_bwd_ret.
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [refresh_or_pull]. cbn [b1_refresh_or_pull] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (g_flushing sb) eqn:Efl.
      * unfold deferring_b1 in H. rewrite Efl in H. simpl in H.
        destruct (deferring s) eqn:Edef.
        -- eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brefresh1 _ IH)|intros [] ?? _ Hrel1 Hk].
           destruct (b1_pure_get_node_bwd _ _ _ _ _ Hrel1 Hk) as (s' & E & Hr); exists 0%nat, s'; exact (conj E Hr).
        -- eapply bind_inv in H as (sb2 & [] & Eb2 & H). unfold get_node in H.
           destruct (g_nodes sb2 !! n) as [d0|] eqn:En; simplify_eq.
           assert (Hrel1 : b1_state_rel (with_pull_depth (S (g_pull_depth s)) s) sb)
             by (by apply b1_rel_with_pull_S_left).
           destruct (Brefresh1 _ IH n _ _ _ _ Hrel1 Eb2) as (f1 & s2 & E2 & Hrel2).
           set (f2 := Nat.max f1 2).
           pose proof (Mrefresh _ (engine_mono_all f1) f2 n _ _ (Nat.le_max_l f1 2) E2) as E2'.
           destruct (engine_depths_kept f2) as [_ _ _ _ _ _ Dref _].
           pose proof (Dref n _ _ _ E2') as [Hp2 Hfl2].
           destruct (b1_depths_kept fb) as [_ _ _ _ _ _ B1Dref _].
           pose proof (B1Dref n _ _ _ Eb2) as [Hpb2 Hflb2].
           set (s3 := with_pull_depth (pred (g_pull_depth s2)) s2).
           assert (Hfls3 : g_flushing s3 = true).
           { unfold s3. simpl. rewrite Hfl2. simpl. destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hf&_). congruence. }
           assert (Hsettle : (if bool_decide (g_pull_depth s3 = 0%nat) && g_flush_owed s3 then settle_flush f2 else m_return tt) s3 = Some (s3, tt)).
           { destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3); [|reflexivity].
             assert (Hf2 : f2 >= 2) by (unfold f2; lia).
             destruct f2 as [|[|k]]; [lia|lia|].
             cbn [settle_flush]. rewrite bind_get.
             destruct (g_flush_owed s3 && bool_decide (g_batch_depth s3 = 0) && outside_bodies s3); [|reflexivity].
             cbn [run_flush]. rewrite bind_get, Hfls3. reflexivity. }
           assert (Hrel3 : b1_state_rel s3 sb').
           { unfold s3. destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfs&_&Hts).
             destruct Hrel2 as [Hw2 [Hf2 Ht2]]. split.
             - rewrite <- Hw2. unfold with_pull_depth. destruct s2; reflexivity.
             - simpl. split; [intro; congruence|]. intro Hfl0. rewrite Hp2. simpl.
               pose proof (Hts Efl) as Hle. rewrite Hpb2. lia. }
           destruct (b1_rel_fields _ _ Hrel2) as (_&_&Hn2&_).
           exists (S f2), s3. split; [|exact Hrel3].
           cbn [pull]. unfold m_bind, m_modify, m_get, get_node.
           rewrite E2', Hn2, En.
           change ((if bool_decide (g_pull_depth (with_pull_depth (pred (g_pull_depth s2)) s2) = 0%nat) && g_flush_owed (with_pull_depth (pred (g_pull_depth s2)) s2) then settle_flush f2 else m_return tt) (with_pull_depth (pred (g_pull_depth s2)) s2))
             with ((if bool_decide (g_pull_depth s3 = 0%nat) && g_flush_owed s3 then settle_flush f2 else m_return tt) s3).
           rewrite Hsettle. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel) as (_ & _ & Hfs & Hfp & _).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        unfold deferring_b1 in H. rewrite Efl in H. simpl in H.
        destruct (deferring s).
        -- eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brefresh1 _ IH)|intros [] ?? _ Hrel1 Hk].
           exists 0%nat; by eapply b1_pure_get_node_bwd.
        -- by eapply (Bpull1 _ IH).
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [pull]. cbn [b1_pull] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _);
        [intros sa0 sba0 sba1 [] Hr0 Hm; unfold m_modify in Hm; simplify_eq; eexists; split; [unfold m_modify; reflexivity|by apply b1_rel_with_pull_S]|intros [] ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brefresh1 _ IH)|intros [] ?? _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros d ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _);
        [intros s_p2 sb_p2 s_p3 [] Hr0 Hm; unfold m_modify in Hm; simplify_eq; eexists; split; [unfold m_modify; reflexivity|by apply b1_rel_with_pull_pred]|intros [] sb3 s3 _ _ Hrel4 Hk4].
      rewrite bind_get in Hk4. setoid_rewrite bind_get.
      eapply (b1_bwd_bind_of Hk4); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel5 Hk5; by eapply b1_bwd_ret].
      intros sb_q a Hm.
      destruct (g_flushing sb3) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel4) as (_&_&Hfl&_).
        assert (Hfls : g_flushing s3 = true) by congruence.
        assert (Hsb1 : sb_q = sb3 /\ a = tt).
        { destruct (bool_decide (g_pull_depth sb3 = 0) && g_flush_owed sb3).
          - by eapply b1_settle_flush_flushing.
          - unfold m_return in Hm. by simplify_eq. }
        destruct Hsb1 as [-> ->].
        exists 2%nat, s3. split; [|exact Hrel4].
        destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3); [|reflexivity].
        cbn [settle_flush]. rewrite bind_get.
        destruct (g_flush_owed s3 && bool_decide (g_batch_depth s3 = 0) && outside_bodies s3); [|reflexivity].
        cbn [run_flush]. rewrite bind_get, Hfls. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel4) as (_&_&_&Hfp&_).
        assert (Hsb4 : s3 = sb3) by (apply b1_state_rel_eq; [exact Hrel4|apply Hfp; congruence]).
        subst sb3.
        destruct (bool_decide (g_pull_depth s3 = 0) && g_flush_owed s3); [by eapply (Bsettle1 _ IH)|by eapply b1_bwd_ret].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [refresh]. cbn [b1_refresh] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
      destruct (decide (n ∈ g_checking sb)), (decide (n ∈ g_checking s)); try (exfalso; rewrite Hchk in *; contradiction).
      { unfold m_step in H |- *. destruct (mark_dirty fb n sb) eqn:Em; simplify_eq.
        destruct (b1_pure_mark_dirty_bwd fb n s sb sb' tt Hrel ltac:(unfold m_step; by rewrite Em)) as (s1 & Em1 & Hrel1).
        unfold m_step in Em1. destruct (mark_dirty fb n s) eqn:Ems; simplify_eq.
        exists fb, s1. split; [by rewrite Ems|exact Hrel1]. }
      destruct (decide (n ∈ g_running sb)), (decide (n ∈ g_running s)); try (exfalso; rewrite Hrun in *; contradiction); [by eapply b1_bwd_ret|].
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel2 Hk2].
      * intros sb_ref [] Hm. destruct (decide (n_freshness d = Check)); [|by eapply b1_bwd_ret].
        eapply (b1_bwd_bind_pure_l_of Hrel1 Hm _); [b1_pure_bwd|intros [] ?? _ _ Hrel2a Hk2a].
        eapply (b1_bwd_bind_of Hk2a); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bwalk1 _ IH)|intros [] ?? _ Hrel3a Hk3a].
        eapply (b1_bwd_bind_pure_l_of Hrel3a Hk3a _); [b1_pure_bwd|intros [] ?? _ _ Hrel4a Hk4a].
        eapply (b1_bwd_bind_pure_l_of Hrel4a Hk4a _); [b1_pure_bwd|intros d' ?? _ _ Hrel5a Hk5a].
        destruct (decide (n_freshness d' = Check)); [|by eapply b1_bwd_ret].
        eapply (b1_bwd_bind_pure_l_of Hrel5a Hk5a _); [b1_pure_bwd|intros [] ?? _ _ Hrel6a Hk6a].
        exists 0%nat; by eapply b1_pure_modify_node_bwd.
      * eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros d' ?? _ _ Hrel3 Hk3].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Brecompute1 _ IH)|by eapply b1_bwd_ret].
    + intros n i s sb sb' r Hrel H. apply wrap_ex_S. cbn [resolve_walk]. cbn [b1_resolve_walk] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d sb1 s1 _ _ Hrel1 Hk].
      rewrite bind_get in Hk. setoid_rewrite bind_get.
      destruct (decide (n_freshness d = Check)); [|by eapply b1_bwd_ret].
      destruct (b1_rel_fields _ _ Hrel1) as (_&He1&_).
      destruct (source_slots (sources_of (g_edges sb1) n) !! i) as [[src ?]|] eqn:Esb1,
               (source_slots (sources_of (g_edges s1) n) !! i) as [[srcs ?]|] eqn:Es1;
        try (exfalso; rewrite He1, Esb1 in Es1; discriminate).
      * assert (srcs = src) by (rewrite He1, Esb1 in Es1; congruence); subst srcs.
        eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bupdate1 _ IH)|intros [] ?? _ Hrel2 Hk2].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros d' ?? _ _ Hrel3 Hk3].
        eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros d_src sb4 s4 _ _ Hrel4 Hk4].
        rewrite bind_get in Hk4. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel4) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hrun4&Hchk4&_).
        eapply (b1_bwd_bind_of Hk4); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel5 Hk5; by eapply (Bwalk1 _ IH)].
        intros sb5 [] Hm.
        destruct (decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running sb4 \/ src ∈ g_checking sb4))) as [Hdb|Hdb];
          destruct (decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s4 \/ src ∈ g_checking s4))) as [Hd|Hd];
          try (exfalso; rewrite Hrun4, Hchk4 in *; tauto).
        -- unfold m_step in Hm |- *. destruct (mark_dirty fb n sb4) eqn:Em; simplify_eq.
           destruct (b1_pure_mark_dirty_bwd fb n s4 sb4 sb5 tt Hrel4 ltac:(unfold m_step; by rewrite Em)) as (s5 & Em5 & Hrel5).
           unfold m_step in Em5. destruct (mark_dirty fb n s4) eqn:Ems; simplify_eq.
           exists fb, s5. split; [by rewrite Ems|exact Hrel5].
        -- by eapply b1_bwd_ret.
      * assert (Hsh : slot_holds stale_live s1 n = slot_holds stale_live sb1 n).
        { destruct (b1_rel_fields _ _ Hrel1) as (_&He'&Hn'&_). unfold slot_holds. by rewrite He', Hn'. }
        destruct (slot_holds stale_live sb1 n) eqn:Esh1, (slot_holds stale_live s1 n) eqn:Esh2;
          try (exfalso; congruence); [by eapply (Bwalk1 _ IH)|by eapply b1_bwd_ret].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [recompute]. cbn [b1_recompute] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_scope d) as [o|]; [|by eapply (Brun_memo1 _ IH)].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bdischarge1 _ IH)|intros [] ?? _ Hrel4 Hk4].
      eapply (b1_bwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_bwd|intros d' ?? _ _ Hrel5 Hk5].
      destruct (n_disposed d').
      * destruct (decide (n_freshness d' = Clean)); [exists 0%nat; by eapply b1_pure_modify_node_bwd|by eapply b1_bwd_ret].
      * destruct (decide (n_runs d' = n_runs d)); [by eapply (Brun_memo1 _ IH)|].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Brecompute1 _ IH)|by eapply b1_bwd_ret].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [run_memo]. cbn [b1_run_memo] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros [] ?? _ _ Hrel4 Hk4].
      eapply (b1_bwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_bwd|intros [] ?? _ _ Hrel5 Hk5].
      eapply (b1_bwd_bind_of Hk5); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros r0 ?? _ Hrel6 Hk6].
      { intros sb6 r0a Hm. by eapply b1_hosted_bwd; [exact Hrel5|intros ??? Hrel0 ??; by eapply (Bexec1 _ IH)|exact Hm]. }
      eapply (b1_bwd_bind_pure_l_of Hrel6 Hk6 _); [b1_pure_bwd|intros d1 ?? _ _ Hrel7 Hk7].
      eapply (b1_bwd_bind_pure_l_of Hrel7 Hk7 _); [destruct (if n_violated d1 then Fail pure_scope_failure else r0); b1_pure_bwd|intros [] ?? _ _ Hrel8 Hk8].
      eapply (b1_bwd_bind_pure_l_of Hrel8 Hk8 _); [b1_pure_bwd|intros [] ?? _ _ Hrel9 Hk9].
      eapply (b1_bwd_bind_pure_l_of Hrel9 Hk9 _); [b1_pure_bwd|intros [] ?? _ _ Hrel10 Hk10].
      eapply (b1_bwd_bind_pure_l_of Hrel10 Hk10 _); [destruct (if n_violated d1 then Fail pure_scope_failure else r0); b1_pure_bwd|intros [] ?? _ _ Hrel11 Hk11].
      eapply (b1_bwd_bind_pure_l_of Hrel11 Hk11 _); [b1_pure_bwd|intros d2 ?? _ _ Hrel12 Hk12].
      eapply (b1_bwd_bind_pure_l_of Hrel12 Hk12 _);
        [destruct (decide _); [b1_pure_bwd|destruct (n_comparer d2); b1_pure_bwd]|intros moved sb13 s13 _ _ Hrel13 Hk13].
      rewrite bind_get in Hk13. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel13) as (_&_&_&_&_&_&_&_&_&Hc13&_).
      destruct (g_current sb13) as [cb13|] eqn:Ecs1, (g_current s13) as [c13|] eqn:Ecs2;
        try (exfalso; rewrite Hc13 in Ecs2; congruence);
        [assert (c13 = cb13) by congruence; subst c13|];
        (eapply (b1_bwd_bind_pure_l_of Hrel13 Hk13 _); [b1_pure_bwd|intros [] ?? _ _ Hrel14 Hk14];
         eapply (b1_bwd_bind_of Hk14); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel15 Hk15];
         [intros sb15 [] Hm; destruct moved; [|by eapply b1_bwd_ret];
          eapply (b1_bwd_bind_of Hm); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; exists fb; by eapply b1_pure_notify_dirty_except_bwd|intros [] sb16 s16 _ Hrel16 Hk16];
          rewrite bind_get in Hk16; setoid_rewrite bind_get;
          destruct (b1_rel_fields _ _ Hrel16) as (_&_&_&_&Hq16&Hqh16&Hfl16&_&Hbd16&_);
          assert (Hcond : bool_decide (g_batch_depth s16 = 0%nat) && negb (g_flushing s16) && live_queue s16 =
                          bool_decide (g_batch_depth sb16 = 0%nat) && negb (g_flushing sb16) && live_queue sb16)
            by (unfold live_queue; by rewrite Hbd16, Hfl16, Hq16, Hqh16);
          destruct (bool_decide (g_batch_depth sb16 = 0%nat) && negb (g_flushing sb16) && live_queue sb16) eqn:Ec1,
                   (bool_decide (g_batch_depth s16 = 0%nat) && negb (g_flushing s16) && live_queue s16) eqn:Ec2;
            try (exfalso; congruence); [exists 0%nat; b1_mod_bwd|by eapply b1_bwd_ret]
         |eapply (b1_bwd_bind_pure_l_of Hrel15 Hk15 _); [b1_pure_bwd|intros d3 ?? _ _ Hrel16 Hk16];
          destruct (decide (n_freshness d3 = Clean)); [by eapply b1_bwd_ret|exists 0%nat; by eapply b1_pure_announce_bwd]]).
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [execute_effect]. cbn [b1_execute_effect] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_disposed d); [exists 0%nat; by eapply b1_pure_modify_node_bwd|].
      eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel2 Hk2].
      * intros sb2 [] Hm. destruct (decide (n_freshness d = Check)); [|by eapply b1_bwd_ret].
        eapply (b1_bwd_bind_of Hm); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bwalk1 _ IH)|intros [] ?? _ Hrel3 Hk3].
        eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros d' ?? _ _ Hrel4 Hk4].
        destruct (decide (n_freshness d' = Check)); [|by eapply b1_bwd_ret].
        eapply (b1_bwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_bwd|intros [] ?? _ _ Hrel5 Hk5].
        exists 0%nat; by eapply b1_pure_modify_node_bwd.
      * eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros d' ?? _ _ Hrel3 Hk3].
        eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros [] ?? _ _ Hrel4 Hk4].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (Brun_effect1 _ IH)|by eapply b1_bwd_ret].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [run_effect]. cbn [b1_run_effect] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros [] ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros d ?? _ _ Hrel4 Hk4].
      eapply (b1_bwd_bind_of Hk4); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel5 Hk5].
      * intros sb5 [] Hm. destruct (n_scope d) as [o|]; [|by eapply b1_bwd_ret].
        eapply (b1_bwd_bind_pure_l_of Hrel4 Hm _); [b1_pure_bwd|intros [] ?? _ _ Hrel5a Hk5a].
        eapply (b1_bwd_bind_of Hk5a); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bdischarge1 _ IH)|intros [] ?? _ Hrel6a Hk6a].
        exists 0%nat; by eapply b1_pure_modify_node_bwd.
      * eapply (b1_bwd_bind_pure_l_of Hrel5 Hk5 _); [b1_pure_bwd|intros d' ?? _ _ Hrel6 Hk6].
        eapply (b1_bwd_bind_of Hk6); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] ?? _ Hrel7 Hk7; exists 0%nat; b1_mod_bwd].
        intros sb7 [] Hm. destruct (n_disposed d'); [by eapply b1_bwd_ret|by eapply (Bbody1 _ IH)].
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [run_effect_body]. cbn [b1_run_effect_body] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros r0 ?? _ Hrel4 Hk4].
      { intros sb4 r0a Hm. by eapply b1_hosted_bwd; [exact Hrel3|intros ??? Hrel0 ??; by eapply (Bexec1 _ IH)|exact Hm]. }
      eapply (b1_bwd_bind_pure_l_of Hrel4 Hk4 _); [b1_pure_bwd|intros d' ?? _ _ Hrel5 Hk5].
      eapply (b1_bwd_bind_pure_l_of Hrel5 Hk5 _); [b1_pure_bwd|intros [] ?? _ _ Hrel6 Hk6].
      destruct r0; [by eapply b1_bwd_ret|].
      eapply (b1_bwd_bind_pure_l_of Hrel6 Hk6 _); [b1_pure_bwd|intros [] ?? _ _ Hrel7 Hk7].
      exists 0%nat; b1_mod_bwd.
    + intros o s sb sb' r Hrel H. apply wrap_ex_S. cbn [discharge]. cbn [b1_discharge] in H.
      eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] sb1 s1 _ Hrel1 Hk].
      * intros sb1 [] Hm. by eapply b1_detached_bwd; [exact Hrel|intros ??? Hrel0 ??; by eapply (Bdispose_scope1 _ IH)|exact Hm].
      * rewrite bind_get in Hk. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel1) as (_&_&_&_&Hq1&Hqh1&Hfl1&_&Hbd1&_).
        assert (Hcond : bool_decide (g_batch_depth s1 = 0%nat) && negb (g_flushing s1) && live_queue s1 =
                        bool_decide (g_batch_depth sb1 = 0%nat) && negb (g_flushing sb1) && live_queue sb1)
          by (unfold live_queue; by rewrite Hbd1, Hfl1, Hq1, Hqh1).
        destruct (bool_decide (g_batch_depth sb1 = 0%nat) && negb (g_flushing sb1) && live_queue sb1) eqn:Ec1,
                 (bool_decide (g_batch_depth s1 = 0%nat) && negb (g_flushing s1) && live_queue s1) eqn:Ec2;
          try (exfalso; congruence); [exists 0%nat; b1_mod_bwd|by eapply b1_bwd_ret].
    + intros o s sb sb' r Hrel H. apply wrap_ex_S. cbn [retire]. cbn [b1_retire] in H.
      eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac| |intros [] sb1 s1 _ Hrel1 Hk].
      * intros sb1' [] Hm. eapply b1_detached_bwd; [exact Hrel| |exact Hm].
        intros s0 sb0 Hrel0 sb1'' r' Hm0.
        eapply (b1_bwd_bind_pure_l_of Hrel0 Hm0 _); [b1_pure_bwd|intros x ?? _ _ Hrel2 Hk2].
        destruct (o_disposed x); [by eapply b1_bwd_ret|].
        eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
        by eapply (Bdispose_scope1 _ IH).
      * rewrite bind_get in Hk. setoid_rewrite bind_get.
        destruct (b1_rel_fields _ _ Hrel1) as (_&_&_&_&Hq1&Hqh1&_).
        assert (Hlq : live_queue s1 = live_queue sb1) by (unfold live_queue; by rewrite Hq1, Hqh1).
        destruct (live_queue sb1) eqn:El1, (live_queue s1) eqn:El2;
          try (exfalso; congruence); [by eapply (Brequest1 _ IH)|by eapply b1_bwd_ret].
    + intros o s sb sb' r Hrel H. apply wrap_ex_S. cbn [dispose_scope]. cbn [b1_dispose_scope] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros x ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      by eapply (Bteardown1 _ IH).
    + intros o ls cs s sb sb' r Hrel H. apply wrap_ex_S. cbn [teardown]. cbn [b1_teardown] in H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [by eapply b1_bwd_ret|..].
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bcleanup1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Bteardown1 _ IH).
      * eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brelease1 _ IH)|intros [] ?? _ Hrel1 Hk].
        by eapply (Bteardown1 _ IH).
      * destruct (decide (a < l_acquired l)).
        -- eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brelease1 _ IH)|intros [] ?? _ Hrel1 Hk].
           by eapply (Bteardown1 _ IH).
        -- eapply (b1_bwd_bind_of H); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bcleanup1 _ IH)|intros [] ?? _ Hrel1 Hk].
           by eapply (Bteardown1 _ IH).
    + intros o b e s sb sb' r Hrel H. apply wrap_ex_S. cbn [run_cleanup]. cbn [b1_run_cleanup] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros x ?? _ _ Hrel1 Hk].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_of Hk2); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Bexec1 _ IH)|intros r0 ?? _ Hrel3 Hk3].
      destruct r0; [by eapply b1_bwd_ret|].
      eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros [] ?? _ _ Hrel4 Hk4].
      exists 0%nat; b1_mod_bwd.
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [release]. cbn [b1_release] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros [] ?? _ _ Hrel1 Hk].
      by eapply (Bdispose1 _ IH).
    + intros n s sb sb' r Hrel H. apply wrap_ex_S. cbn [dispose_node]. cbn [b1_dispose_node] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_disposed d); [by eapply b1_bwd_ret|].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [destruct (n_link d); b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_pure_l_of Hrel3 Hk3 _); [b1_pure_bwd|intros [] ?? _ _ Hrel4 Hk4].
      destruct (n_scope d); [by eapply (Bretire1 _ IH)|by eapply b1_bwd_ret].
    + intros n o s sb sb' r Hrel H. apply wrap_ex_S. cbn [attach]. cbn [b1_attach] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros x ?? _ _ Hrel1 Hk].
      destruct (o_disposed x).
      * eapply (b1_bwd_bind_of Hk); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; by eapply (Brelease1 _ IH)|intros [] ?? _ Hrel2 Hk2].
        exists 0%nat; by eapply b1_pure_modify_node_bwd.
      * eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
        exists 0%nat; by eapply b1_pure_modify_node_bwd.
    + intros n v s sb sb' r Hrel H. apply wrap_ex_S. cbn [write_signal]. cbn [b1_write_signal] in H.
      eapply (b1_bwd_bind_pure_l_of Hrel H _); [b1_pure_bwd|intros d ?? _ _ Hrel1 Hk].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [by eapply b1_bwd_ret|].
      eapply (b1_bwd_bind_pure_l_of Hrel1 Hk _); [b1_pure_bwd|intros [] ?? _ _ Hrel2 Hk2].
      eapply (b1_bwd_bind_pure_l_of Hrel2 Hk2 _); [b1_pure_bwd|intros [] ?? _ _ Hrel3 Hk3].
      eapply (b1_bwd_bind_of Hk3); [intros ????; eng_mono_tac|intros ?????; eng_mono_tac|intros ???; exists fb; by eapply b1_pure_notify_dirty_bwd|intros [] ?? _ Hrel4 Hk4].
      by eapply (Brequest1 _ IH).
    + intros s sb sb' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        destruct (b1_request_flush_flushing _ _ _ _ Efl H) as [-> ->].
        exists 2%nat, s. split; [|exact Hrel].
        cbn [request_flush]. rewrite bind_get.
        destruct (live_queue s && bool_decide (g_batch_depth s = 0)); [|reflexivity].
        destruct (outside_bodies s); [|by rewrite Hfl, Efl].
        cbn [run_flush]. rewrite bind_get, Hfl, Efl. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        apply wrap_ex_S. cbn [request_flush]. cbn [b1_request_flush] in H.
        rewrite bind_get in H. setoid_rewrite bind_get.
        destruct (live_queue s && bool_decide (g_batch_depth s = 0));
          [|unfold m_return in H; simplify_eq; exists 0%nat, sb'; split; [reflexivity|apply b1_state_rel_refl]].
        destruct (outside_bodies s); [by eapply (Bflush1 _ IH)|].
        rewrite Efl in H. unfold m_modify in H; simplify_eq.
        exists 0%nat, (with_flush_owed true s); split; [rewrite Efl; reflexivity|apply b1_state_rel_refl].
    + intros s sb sb' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        destruct (b1_settle_flush_flushing _ _ _ _ Efl H) as [-> ->].
        exists 2%nat, s. split; [|exact Hrel].
        cbn [settle_flush]. rewrite bind_get.
        destruct (g_flush_owed s && bool_decide (g_batch_depth s = 0) && outside_bodies s); [|reflexivity].
        cbn [run_flush]. rewrite bind_get, Hfl, Efl. reflexivity.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        apply wrap_ex_S. cbn [settle_flush]. cbn [b1_settle_flush] in H.
        rewrite bind_get in H. setoid_rewrite bind_get.
        destruct (g_flush_owed s && bool_decide (g_batch_depth s = 0) && outside_bodies s);
          [by eapply (Bflush1 _ IH)|unfold m_return in H; simplify_eq; exists 0%nat, sb'; split; [reflexivity|apply b1_state_rel_refl]].
    + intros s sb sb' r Hrel H.
      destruct (g_flushing sb) eqn:Efl.
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&Hfl&_).
        cbn [b1_run_flush] in H. rewrite bind_get, Efl in H.
        unfold m_return in H. simplify_eq.
        exists 1%nat, s. split; [cbn [run_flush]; rewrite bind_get, Hfl, Efl; reflexivity|exact Hrel].
      * destruct (b1_rel_destruct _ _ Hrel) as (_&_&_&Hfp&_).
        assert (Hs : s = sb) by (apply b1_state_rel_eq; [exact Hrel|auto]). subst sb.
        cbn [b1_run_flush] in H. rewrite bind_get, Efl in H.
        unfold m_bind, m_modify in H.
        set (s1 := with_flush_owed false (with_flushing true s)) in H.
        destruct (b1_drain fb s1) as [[sb2 []]|] eqn:Ed; simpl in H; [|discriminate].
        simplify_eq.
        destruct (Bdrain1 _ IH _ _ _ _ (b1_state_rel_refl s1) Ed) as (f & s2 & Ebd & Hrel2).
        destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Ddrain].
        destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Ddrain].
        pose proof (Ddrain _ _ _ Ebd) as [Hp2 _].
        pose proof (B1Ddrain _ _ _ Ed) as [Hpb2 _].
        assert (Hs2 : s2 = sb2) by (apply b1_state_rel_eq; [exact Hrel2|congruence]). subst sb2.
        exists (S f), (with_flushing false s2). split.
        -- cbn [run_flush]. unfold m_bind, m_get, m_modify. rewrite Efl. fold s1. rewrite Ebd. reflexivity.
        -- apply b1_state_rel_refl.
    + intros s sb sb' r Hrel H. apply wrap_ex_S. cbn [drain]. cbn [b1_drain] in H.
      rewrite bind_get in H. setoid_rewrite bind_get.
      destruct (b1_rel_fields _ _ Hrel) as (_&_&_&_&Hq&Hqh&_).
      destruct (g_queue sb !! g_queue_head sb) as [n|] eqn:Eq1,
               (g_queue s !! g_queue_head s) as [ns|] eqn:Eq2;
        try (exfalso; rewrite Hq, Hqh, Eq1 in Eq2; discriminate).
      * assert (ns = n) by (rewrite Hq, Hqh, Eq1 in Eq2; congruence); subst ns.
        eapply bind_inv in H as (sb1 & [] & E1 & H). unfold m_modify in E1; simplify_eq.
        eapply bind_inv in H as (sb2 & [] & E2 & E3).
        assert (Hrel1 : b1_state_rel (with_queue_head (S (g_queue_head s)) s) (with_queue_head (S (g_queue_head sb)) sb))
          by (rewrite Hqh; b1_rel_simple).
        destruct (Bexecute1 _ IH n _ _ _ _ Hrel1 E2) as (f1 & s2 & Eb2 & Hrel2).
        destruct (Bdrain1 _ IH _ _ _ _ Hrel2 E3) as (f2 & s3 & Eb3 & Hrel3).
        exists (Nat.max f1 f2), s3. split; [|exact Hrel3].
        unfold m_bind, m_modify.
        rewrite (Mexecute _ (engine_mono_all f1) (Nat.max f1 f2) _ _ _ (Nat.le_max_l f1 f2) Eb2).
        exact (Mdrain _ (engine_mono_all f2) (Nat.max f1 f2) _ _ (Nat.le_max_r f1 f2) Eb3).
      * exists 0%nat; eapply b1_pure_modify_bwd; [|exact Hrel|exact H]; upd_pull_tac.
Qed.

(**md
**Including `g_flushing` in `deferring` (`deferring_b1`) preserves exact program outcomes, states, and traces, and makes `pull` identical to `refresh` when `g_flushing = true`, `g_pull_depth = 0`, and `g_current = None`.**

Every engine primitive except `refresh_or_pull` is identical between `Engine` and `Engine_B1`, and whenever `g_flushing = true`, any `pull` leaves `g_flushing = true`, restores `g_pull_depth`, and skips `settle_flush`. Consequently whole-program execution from any non-flushing state (including `init fl`) and single-node pulls during a flush reach identical final states (hence identical `g_trace`).

- [c:] program command executed by `run_top` / `b1_run_top`.
- [s:] starting state.
- [n:] target node for `pull` / `refresh`.
- [s':] final state after execution.
- [r:] command outcome (`Ok` or `Fail`).
- [d:] read node snapshot returned by `pull` or `get_node`.
*)
Theorem deferring_with_flushing_eq c s n s' r d :
  (g_flushing s = false ->
    ((exists f, run_top f c s = Some (s', r)) <-> (exists fb, b1_run_top fb c s = Some (s', r)))) /\
  (g_flushing s = true -> g_pull_depth s = 0 -> g_current s = None ->
    ((exists f, pull f n s = Some (s', d)) <->
     (exists fb, (let* _ := b1_refresh fb n in get_node n) s = Some (s', d))) /\
    ((exists f, pull f n s = Some (s', d)) <->
     (exists f', (let* _ := refresh f' n in get_node n) s = Some (s', d)))).
Proof.
  split.
  - intros Hfl. split.
    + intros [f Hf].
      destruct (Fexec1 _ (b1_fwd_all f) [] c s s s' r (b1_state_rel_refl s) Hf) as (fb & sb' & Efb & Hrel).
      destruct (engine_depths_kept f) as [Dexec _].
      destruct (b1_depths_kept fb) as [B1Dexec _].
      pose proof (Dexec [] c s s' r Hf) as [Hp1 _].
      pose proof (B1Dexec [] c s sb' r Efb) as [Hp2 _].
      assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
      subst sb'. exists fb. exact Efb.
    + intros [fb Hfb].
      destruct (Bexec1 _ (b1_bwd_all fb) [] c s s s' r (b1_state_rel_refl s) Hfb) as (f & s0' & Ef & Hrel).
      destruct (engine_depths_kept f) as [Dexec _].
      destruct (b1_depths_kept fb) as [B1Dexec _].
      pose proof (Dexec [] c s s0' r Ef) as [Hp1 _].
      pose proof (B1Dexec [] c s s' r Hfb) as [Hp2 _].
      assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
      subst s0'. exists f. exact Ef.
  - intros Hfl Hpd Hcur.
    assert (Hmid : (exists f, pull f n s = Some (s', d)) <->
                   (exists fb, (let* _ := b1_refresh fb n in get_node n) s = Some (s', d))).
    { split.
      - intros [f Hf].
        assert (Hrop : refresh_or_pull (S f) n s = Some (s', d)).
        { cbn [refresh_or_pull]. rewrite bind_get. unfold deferring. rewrite Hcur, Hpd. simpl. exact Hf. }
        destruct (Frop1 _ (b1_fwd_all (S f)) n s s s' d (b1_state_rel_refl s) Hrop) as (fb & sb' & Efb & Hrel).
        destruct fb as [|fb']; [discriminate|].
        cbn [b1_refresh_or_pull] in Efb. rewrite bind_get in Efb.
        unfold deferring_b1 in Efb. rewrite Hfl in Efb. simpl in Efb.
        eapply bind_inv in Efb as (sb1 & [] & Eb1 & Eg). unfold get_node in Eg.
        destruct (g_nodes sb1 !! n) eqn:En; [|discriminate]. simplify_eq.
        destruct (engine_depths_kept f) as [_ _ _ _ _ Dpull _].
        pose proof (Dpull n s s' d Hf) as [Hp1 _].
        destruct (b1_depths_kept fb') as [_ _ _ _ _ _ B1Dref _].
        pose proof (B1Dref n s sb' tt Eb1) as [Hp2 _].
        assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
        subst sb'. exists fb'. unfold m_bind. rewrite Eb1. simpl. unfold get_node.
        by rewrite En.
      - intros [fb Hfb].
        eapply bind_inv in Hfb as (sb1 & [] & Eb1 & Eg). unfold get_node in Eg.
        destruct (g_nodes sb1 !! n) eqn:En; [|discriminate]. simplify_eq.
        assert (Hbrop : b1_refresh_or_pull (S fb) n s = Some (s', d)).
        { cbn [b1_refresh_or_pull]. rewrite bind_get. unfold deferring_b1. rewrite Hfl. simpl.
          unfold m_bind. rewrite Eb1. simpl. unfold get_node. by rewrite En. }
        destruct (Brop1 _ (b1_bwd_all (S fb)) n s s s' d (b1_state_rel_refl s) Hbrop) as (f & s0' & Ef & Hrel).
        destruct f as [|f']; [discriminate|].
        cbn [refresh_or_pull] in Ef. rewrite bind_get in Ef.
        unfold deferring in Ef. rewrite Hcur, Hpd in Ef. simpl in Ef.
        destruct (engine_depths_kept f') as [_ _ _ _ _ Dpull _].
        pose proof (Dpull n s s0' d Ef) as [Hp1 _].
        destruct (b1_depths_kept fb) as [_ _ _ _ _ _ B1Dref _].
        pose proof (B1Dref n s s' tt Eb1) as [Hp2 _].
        assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
        subst s0'. exists f'. exact Ef. }
    split; [exact Hmid|].
    rewrite Hmid. split.
    + intros [fb Hfb].
      eapply bind_inv in Hfb as (s1 & [] & Eb1 & Eg). unfold get_node in Eg.
      destruct (g_nodes s1 !! n) eqn:En; [|discriminate]. simplify_eq.
      destruct (Brefresh1 _ (b1_bwd_all fb) n s s s' tt (b1_state_rel_refl s) Eb1) as (f' & s0' & Ef & Hrel).
      destruct (engine_depths_kept f') as [_ _ _ _ _ _ Dref _].
      pose proof (Dref n s s0' tt Ef) as [Hp1 _].
      destruct (b1_depths_kept fb) as [_ _ _ _ _ _ B1Dref _].
      pose proof (B1Dref n s s' tt Eb1) as [Hp2 _].
      assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]). subst s0'.
      exists f'. unfold m_bind. rewrite Ef. simpl. unfold get_node. by rewrite En.
    + intros [f' Hf'].
      eapply bind_inv in Hf' as (s1 & [] & Ef1 & Eg). unfold get_node in Eg.
      destruct (g_nodes s1 !! n) eqn:En; [|discriminate]. simplify_eq.
      destruct (Frefresh1 _ (b1_fwd_all f') n s s s' tt (b1_state_rel_refl s) Ef1) as (fb & sb' & Eb & Hrel).
      destruct (engine_depths_kept f') as [_ _ _ _ _ _ Dref _].
      pose proof (Dref n s s' tt Ef1) as [Hp1 _].
      destruct (b1_depths_kept fb) as [_ _ _ _ _ _ B1Dref _].
      pose proof (B1Dref n s sb' tt Eb) as [Hp2 _].
      assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]). subst sb'.
      exists fb. unfold m_bind. rewrite Eb. simpl. unfold get_node. by rewrite En.
Qed.

(** ** B2. Disposed empty sources & Track slot-0 / slot-1 cursor-bump fast paths *)

Definition set_cursor (cur : nat) (self : node_id) (w : EdgeWorld) : EdgeWorld :=
  let l := sources_of w self in
  set_sources self {| source_slots := source_slots l; source_cursor := cur |} w.

Lemma edge_match_empty : edge_match empty_world.
Proof.
  intros c i s h. unfold slot_at, sources_of, observers_of, empty_world. simpl.
  rewrite !lookup_empty. simpl. rewrite !lookup_nil. split; discriminate.
Qed.

Lemma edge_match_begin c w :
  edge_match w -> edge_match (sources_begin_run c w).
Proof.
  intros Hm c' i s h. unfold sources_begin_run.
  rewrite slot_at_set_sources, observers_of_set_sources.
  destruct (decide (c' = c)) as [->|]; apply Hm.
Qed.

Lemma slot_at_none_nil w c :
  (forall i, fst <$> slot_at w c i = None) -> source_slots (sources_of w c) = [].
Proof.
  intros H. destruct (source_slots (sources_of w c)) as [|p ps] eqn:Es; [reflexivity|].
  specialize (H 0%nat). unfold slot_at in H. rewrite Es in H. discriminate.
Qed.

Lemma edge_match_add c src w w' :
  edge_match w -> sources_add c src w = Some w' -> edge_match w'.
Proof.
  intros Hm Hadd. unfold sources_add in Hadd.
  destruct (decide _).
  - simplify_eq. intros c' i s h.
    rewrite slot_at_set_sources, observers_of_set_sources.
    destruct (decide (c' = c)) as [->|]; apply Hm.
  - destruct (decide _); [by simplify_eq|].
    destruct (sources_end_run c w) as [w1|] eqn:Eend; [|discriminate].
    simpl in Hadd. simplify_eq.
    destruct (sources_end_run_ok c w Hm) as (w1' & E1 & Hm1 & _).
    rewrite Eend in E1. injection E1 as <-.
    by apply edge_match_link.
Qed.

Lemma sources_add_keeps_empty c src w w' c' :
  edge_match w -> c' <> c ->
  source_slots (sources_of w c') = [] ->
  sources_add c src w = Some w' ->
  source_slots (sources_of w' c') = [].
Proof.
  intros Hm Hne Hempty Hadd. unfold sources_add in Hadd.
  destruct (decide _).
  - simplify_eq. rewrite sources_of_set_sources, decide_False by exact Hne. exact Hempty.
  - destruct (decide _); [simplify_eq; exact Hempty|].
    destruct (sources_end_run c w) as [w1|] eqn:Eend; [|discriminate].
    simpl in Hadd. simplify_eq.
    destruct (sources_end_run_ok c w Hm) as (w1' & E1 & _ & (_&_&Hother&_)).
    rewrite Eend in E1. injection E1 as <-.
    assert (H1empty : source_slots (sources_of w1 c') = []).
    { apply slot_at_none_nil. intros i. destruct (Hother c' i Hne) as [-> _].
      unfold slot_at. rewrite Hempty, lookup_nil. reflexivity. }
    unfold sources_link, sources_append.
    repeat match goal with
    | |- context [if decide _ then _ else _] => destruct (decide _)
    | |- context [match observers_try_add _ _ _ with _ => _ end] => destruct (observers_try_add _ _ _) as [? []]
    end; rewrite sources_of_set_observers, sources_of_set_sources, decide_False by exact Hne; exact H1empty.
Qed.

Lemma sources_end_run_keeps_empty n w w' c :
  edge_match w ->
  source_slots (sources_of w c) = [] ->
  sources_end_run n w = Some w' ->
  source_slots (sources_of w' c) = [].
Proof.
  intros Hm Hempty Hend.
  destruct (sources_end_run_ok n w Hm) as (w1 & E1 & _ & (_&Hnone&Hother&_)).
  rewrite Hend in E1. injection E1 as <-.
  destruct (decide (c = n)) as [->|Hne].
  - unfold sources_end_run in Hend.
    destruct (decide (source_cursor (sources_of w n) < length (source_slots (sources_of w n)))) as [Hl|Hl].
    + rewrite Hempty in Hl. simpl in Hl. lia.
    + by simplify_eq.
  - apply slot_at_none_nil. intros i. destruct (Hother c i Hne) as [-> _].
    unfold slot_at. rewrite Hempty, lookup_nil. reflexivity.
Qed.

Lemma sources_clear_keeps_empty n w w' c :
  edge_match w ->
  (c = n \/ source_slots (sources_of w c) = []) ->
  sources_clear n w = Some w' ->
  source_slots (sources_of w' c) = [].
Proof.
  intros Hm Hc Hclr.
  destruct (sources_clear_ok n w Hm) as (w1 & E1 & _ & Hnempty & _ & Hother & _).
  rewrite Hclr in E1. injection E1 as <-.
  destruct (decide (c = n)) as [->|Hne]; [exact Hnempty|].
  destruct Hc as [Heq|Hempty]; [contradiction|].
  apply slot_at_none_nil. intros i. destruct (Hother c i Hne) as [-> _].
  unfold slot_at. rewrite Hempty, lookup_nil. reflexivity.
Qed.

Definition b2_inv (s : state) : Prop :=
  edge_match (g_edges s) /\
  forall c d, g_nodes s !! c = Some d -> n_disposed d = true ->
    source_slots (sources_of (g_edges s) c) = [].

Lemma b2_inv_init fl : b2_inv (init fl).
Proof.
  split; [exact edge_match_empty|].
  intros c d Hd. unfold init in Hd. simpl in Hd. by rewrite lookup_empty in Hd.
Qed.

Lemma mark_prim_b2_inv s s' :
  mark_prim s s' -> b2_inv s -> b2_inv s'.
Proof.
  intros Hp [Hm Hdisp].
  assert (He : g_edges s' = g_edges s) by (by apply prim_edges).
  split; [by rewrite He|].
  intros c d Hd Hdz. rewrite He.
  destruct Hp as [s0 n d0 f u Hnd Hk Hf Hu|s0 n d0 f Hnd Hk Hq Hf|s0 n d0 f Hnd Hk Hq Hf]; simpl in *.
  all: destruct (decide (c = n)) as [->|Hne];
       [rewrite lookup_insert_eq in Hd; simplify_eq; eapply Hdisp; [exact Hnd|exact Hdz]
       |rewrite lookup_insert_ne in Hd by congruence; eapply Hdisp; [exact Hd|exact Hdz]].
Qed.

Lemma marks_b2_inv s s' :
  rtc mark_prim s s' -> b2_inv s -> b2_inv s'.
Proof. induction 1; [exact id|intros Hb; apply IHrtc; by eapply mark_prim_b2_inv]. Qed.

Lemma b2_inv_any_mark f n r s s' :
  any_mark f n r s s' -> b2_inv s -> b2_inv s'.
Proof. intros H. eapply marks_b2_inv, (any_mark_rtc f n r _ _ H). Qed.

Lemma b2_bind_of {A B : Type} {m : M A} {k : A -> M B} {s s' : state} {b : B}
    (Hb : m_bind m k s = Some (s', b)) :
  (forall s1 a, b2_inv s -> m s = Some (s1, a) -> b2_inv s1) ->
  (forall a s1, m s = Some (s1, a) -> b2_inv s1 -> k a s1 = Some (s', b) -> b2_inv s') ->
  b2_inv s -> b2_inv s'.
Proof.
  intros H1 H2 Hs. unfold m_bind in Hb. destruct (m s) as [[s1 a]|] eqn:Em; [|discriminate].
  eapply (H2 a s1 eq_refl); [by eapply H1|exact Hb].
Qed.

Lemma b2_m_return {A} (a : A) s s' r :
  m_return a s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof. intros H Hs. unfold m_return in H. by simplify_eq. Qed.

Lemma b2_m_modify upd s s' r :
  m_modify upd s = Some (s', r) -> b2_inv s ->
  (forall st, g_nodes (upd st) = g_nodes st /\ g_edges (upd st) = g_edges st) ->
  b2_inv s'.
Proof.
  intros H [Hm Hd] Hupd. unfold m_modify in H. simplify_eq.
  destruct (Hupd s) as [Hn He]. split; [by rewrite He|].
  intros c d Hc Hdz. rewrite He. eapply Hd; [rewrite <- Hn; exact Hc|exact Hdz].
Qed.

Lemma b2_get_node n s s' d :
  get_node n s = Some (s', d) -> b2_inv s -> b2_inv s'.
Proof. intros H Hs. unfold get_node in H. destruct (g_nodes s !! n); simplify_eq; exact Hs. Qed.

Lemma b2_get_owner o s s' x :
  get_owner o s = Some (s', x) -> b2_inv s -> b2_inv s'.
Proof. intros H Hs. unfold get_owner in H. destruct (g_owners s !! o); simplify_eq; exact Hs. Qed.

Lemma b2_modify_node n u s s' r :
  modify_node n u s = Some (s', r) -> b2_inv s ->
  (forall x, n_disposed (u x) = n_disposed x) ->
  b2_inv s'.
Proof.
  intros H [Hm Hd] Hu. unfold modify_node in H.
  destruct (g_nodes s !! n) as [d0|] eqn:En; [|discriminate].
  simplify_eq. split; [exact Hm|].
  intros c d Hc Hdz. simpl in *.
  destruct (decide (c = n)) as [->|Hne].
  - rewrite lookup_insert_eq in Hc. simplify_eq. rewrite Hu in Hdz. eapply Hd; [exact En|exact Hdz].
  - rewrite lookup_insert_ne in Hc by congruence. eapply Hd; [exact Hc|exact Hdz].
Qed.

Lemma b2_add_node n d s s' r :
  add_node n d s = Some (s', r) -> b2_inv s -> n_disposed d = false -> b2_inv s'.
Proof.
  intros H [Hm Hd] Hnd. unfold add_node, m_modify in H. simplify_eq.
  unfold ghost_publish, set_node. simpl.
  destruct (<[n := d]> (g_nodes s) !! n) eqn:E;
    (split; simpl; [exact Hm|];
     intros c d0 Hc Hdz;
     destruct (decide (c = n)) as [->|Hne];
     [rewrite lookup_insert_eq in Hc; simplify_eq; congruence
     |rewrite lookup_insert_ne in Hc by congruence; eapply Hd; [exact Hc|exact Hdz]]).
Qed.

Lemma b2_current_owner s s' r :
  current_owner s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H [Hm Hd]. unfold current_owner in H.
  destruct (g_current_owner s), (g_current s); simplify_eq; try (split; assumption).
  destruct (g_nodes s !! n) as [d0|] eqn:En; [|discriminate].
  destruct (n_scope d0); simplify_eq; try (split; assumption).
  destruct (match n_kind d0, n_mode d0 with KMemo, Pure => true | _, _ => false end); simplify_eq;
    (split; simpl; [exact Hm|];
     intros c d Hc Hdz; destruct (decide (c = n)) as [->|Hne];
     [rewrite lookup_insert_eq in Hc; simplify_eq; eapply Hd; [exact En|exact Hdz]
     |rewrite lookup_insert_ne in Hc by congruence; eapply Hd; [exact Hc|exact Hdz]]).
Qed.

Lemma b2_track n s s' r :
  track n s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H [Hm Hd]. unfold track, live_current in H.
  destruct (g_current s) as [c0|]; [|simplify_eq; split; assumption].
  destruct (g_nodes s !! c0) as [dc|] eqn:Edc; [|simplify_eq; split; assumption].
  destruct (n_disposed dc) eqn:Edcz; [simplify_eq; split; assumption|].
  destruct (sources_add c0 n (g_edges s)) as [w'|] eqn:Eadd; [|discriminate].
  simplify_eq. unfold ghost_read; simpl.
  destruct (g_nodes s !! n) as [dn|]; simpl;
    (split; [by eapply edge_match_add|];
     intros c d Hc Hdz; simpl in Hc;
     destruct (decide (c = c0)) as [->|Hne];
     [rewrite Edc in Hc; simplify_eq; congruence
     |eapply sources_add_keeps_empty; [exact Hm|exact Hne|eapply Hd; [exact Hc|exact Hdz]|exact Eadd]]).
Qed.

Lemma b2_mark_reader f s s' r :
  mark_reader f s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold mark_reader in H.
  destruct (g_current s), (f_reader_mark (g_flags s)); simplify_eq; try exact Hb.
  destruct (mark_check f _ s) eqn:Em; simplify_eq.
  eapply (b2_inv_any_mark _ _ None); [left; exact Em|exact Hb].
Qed.

Lemma b2_announce n s s' r :
  announce n s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold announce in H. destruct (f_unannounced (g_flags s)); [|by simplify_eq].
  eapply b2_modify_node; [exact H|exact Hb|done].
Qed.

Lemma b2_unlink l s s' r :
  unlink l s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold unlink in H. eapply (b2_bind_of H); [intros ?? Hs Hg; by eapply b2_get_owner| |exact Hb].
  intros x s1 _ Hs1 H1. destruct (decide _); [eapply b2_m_modify; [exact H1|exact Hs1|]|by eapply b2_m_return].
  intro st; split; reflexivity.
Qed.

Lemma b2_record_error o y s s' r :
  record_error o y s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold record_error in H. eapply (b2_bind_of H); [intros ?? Hs Hg; by eapply b2_get_owner| |exact Hb].
  intros x s1 _ Hs1 H1. destruct (o_sink x).
  - eapply (b2_bind_of H1); [intros ?? Hs Hg; by eapply b2_get_owner| |exact Hs1].
    intros y0 s2 _ Hs2 H2. eapply b2_m_modify; [exact H2|exact Hs2|]. intro st; split; reflexivity.
  - eapply b2_m_modify; [exact H1|exact Hs1|]. intro st; split; reflexivity.
Qed.

Lemma b2_begin_run n s s' r :
  edges (sources_begin_run n) s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H [Hm Hd]. unfold edges, m_modify in H. simplify_eq.
  split; simpl; [by apply edge_match_begin|].
  intros c d Hc Hdz. unfold sources_begin_run. rewrite sources_of_set_sources.
  destruct (decide (c = n)) as [->|]; eapply Hd; [exact Hc|exact Hdz|exact Hc|exact Hdz].
Qed.

Lemma b2_end_or_clear n (b : bool) s s' r :
  edges_opt (if b then sources_clear n else sources_end_run n) s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H [Hm Hd]. unfold edges_opt in H.
  destruct b.
  - destruct (sources_clear n (g_edges s)) as [w'|] eqn:E; [|discriminate].
    simplify_eq. destruct (sources_clear_ok n _ Hm) as (w1 & E1 & Hm1 & Hempty & _ & _).
    rewrite E in E1. injection E1 as <-. split; [exact Hm1|].
    intros c d Hc Hdz. simpl. destruct (decide (c = n)) as [->|Hne]; [exact Hempty|].
    eapply sources_clear_keeps_empty; [exact Hm|right; eapply Hd; [exact Hc|exact Hdz]|exact E].
  - destruct (sources_end_run n (g_edges s)) as [w'|] eqn:E; [|discriminate].
    simplify_eq. destruct (sources_end_run_ok n _ Hm) as (w1 & E1 & Hm1 & _).
    rewrite E in E1. injection E1 as <-. split; [exact Hm1|].
    intros c d Hc Hdz. simpl. eapply sources_end_run_keeps_empty; [exact Hm|eapply Hd; [exact Hc|exact Hdz]|exact E].
Qed.

Lemma b2_hosted {A} n (b : M A) s s' r :
  hosted n b s = Some (s', r) ->
  (forall s0 s1 a, b s0 = Some (s1, a) -> b2_inv s0 -> b2_inv s1) ->
  b2_inv s -> b2_inv s'.
Proof.
  intros H Hb Hs. unfold hosted in H.
  eapply (b2_bind_of H); [intros ?? Hs0 Hm; eapply b2_m_modify; [exact Hm|exact Hs0|]; intro st; split; reflexivity| |exact Hs].
  intros [] s1 _ Hs1 Hk1.
  eapply (b2_bind_of Hk1); [intros ?? Hs0 Hm; by eapply Hb| |exact Hs1].
  intros a s2 _ Hs2 Hk2.
  eapply (b2_bind_of Hk2); [intros ?? Hs0 Hm; eapply b2_m_modify; [exact Hm|exact Hs0|]; intro st; split; reflexivity| |exact Hs2].
  intros [] s3 _ Hs3 Hm. by eapply b2_m_return.
Qed.

Lemma b2_untracked {A} (b : M A) s s' r :
  untracked b s = Some (s', r) ->
  (forall s0 s1 a, b s0 = Some (s1, a) -> b2_inv s0 -> b2_inv s1) ->
  b2_inv s -> b2_inv s'.
Proof.
  intros H Hb Hs. unfold untracked in H.
  eapply (b2_bind_of H); [intros ?? Hs0 Hm; eapply b2_m_modify; [exact Hm|exact Hs0|]; intro st; split; reflexivity| |exact Hs].
  intros [] s1 _ Hs1 Hk1.
  eapply (b2_bind_of Hk1); [intros ?? Hs0 Hm; by eapply Hb| |exact Hs1].
  intros a s2 _ Hs2 Hk2.
  eapply (b2_bind_of Hk2); [intros ?? Hs0 Hm; eapply b2_m_modify; [exact Hm|exact Hs0|]; intro st; split; reflexivity| |exact Hs2].
  intros [] s3 _ Hs3 Hm. by eapply b2_m_return.
Qed.

Lemma b2_detached o b s s' r :
  detached o b s = Some (s', r) ->
  (forall s0 s1 a, b s0 = Some (s1, a) -> b2_inv s0 -> b2_inv s1) ->
  b2_inv s -> b2_inv s'.
Proof.
  intros H Hb Hs. unfold detached in H.
  eapply (b2_bind_of H); [intros ?? Hs0 Hm; eapply b2_m_modify; [exact Hm|exact Hs0|]; intro st; split; reflexivity| |exact Hs].
  intros [] s1 _ Hs1 Hk1.
  eapply (b2_bind_of Hk1); [intros ?? Hs0 Hm; by eapply Hb| |exact Hs1].
  intros [] s2 _ Hs2 Hm. eapply b2_m_modify; [exact Hm|exact Hs2|]. intro st; split; reflexivity.
Qed.

Lemma b2_dispose_prefix n d s s1 s2 s3 :
  b2_inv s ->
  modify_node n (fun x => node_with_link None (node_with_disposed true x)) s = Some (s1, tt) ->
  match n_link d with Some l => unlink l | None => m_return tt end s1 = Some (s2, tt) ->
  edges_opt (sources_clear n) s2 = Some (s3, tt) ->
  b2_inv s3.
Proof.
  intros [Hm Hd] E1 E2 E3.
  unfold modify_node in E1. destruct (g_nodes s !! n) as [d0|] eqn:En; [|discriminate]. simplify_eq.
  assert (Hn2 : g_nodes s2 = <[n := node_with_link None (node_with_disposed true d0)]> (g_nodes s))
    by (destruct (n_link d); [unfold unlink in E2; eapply bind_inv in E2 as (sx & x & Ex & Eu); destruct (decide _); unfold get_owner, put_owner, m_modify, m_return in *; destruct (g_owners _ !! _); simplify_eq; reflexivity|unfold m_return in E2; by simplify_eq]).
  assert (He2 : g_edges s2 = g_edges s)
    by (destruct (n_link d); [unfold unlink in E2; eapply bind_inv in E2 as (sx & x & Ex & Eu); destruct (decide _); unfold get_owner, put_owner, m_modify, m_return in *; destruct (g_owners _ !! _); simplify_eq; reflexivity|unfold m_return in E2; by simplify_eq]).
  unfold edges_opt in E3. destruct (sources_clear n (g_edges s2)) as [w'|] eqn:Eclr; [|discriminate].
  simplify_eq. rewrite He2 in Eclr.
  destruct (sources_clear_ok n _ Hm) as (w1 & Eclr1 & Hm1 & Hempty & _).
  rewrite Eclr in Eclr1. injection Eclr1 as <-.
  split; [exact Hm1|].
  intros c dc Hc Hdz. simpl in *. rewrite Hn2 in Hc.
  destruct (decide (c = n)) as [->|Hne]; [exact Hempty|].
  rewrite lookup_insert_ne in Hc by congruence.
  eapply sources_clear_keeps_empty; [exact Hm|right; eapply Hd; [exact Hc|exact Hdz]|exact Eclr].
Qed.

Lemma b2_next_id s s' r :
  next_id s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof. intros H Hb. unfold next_id in H. by simplify_eq. Qed.

Lemma b2_note_host_read n o s s' r :
  note_host_read n o s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold note_host_read in H.
  destruct (g_current s), (g_current_owner s); simplify_eq; try exact Hb.
  destruct (decide _); [unfold emit, m_modify in H; by simplify_eq|by simplify_eq].
Qed.

Lemma b2_log_label s s' r :
  log_label s = Some (s', r) -> b2_inv s -> b2_inv s'.
Proof.
  intros H Hb. unfold log_label in H.
  destruct (g_current s), (g_current_owner s); by simplify_eq.
Qed.

Local Ltac b2_pure_tac :=
  try unfold add_node, put_owner, emit, edges, push_running, pop_running,
    push_checking, pop_checking, m_step in *;
  first
    [ by eapply b2_m_return
    | by eapply b2_get_node
    | by eapply b2_get_owner
    | by eapply b2_current_owner
    | by eapply b2_next_id
    | by eapply b2_note_host_read
    | by eapply b2_log_label
    | by eapply b2_track
    | by eapply b2_mark_reader
    | by eapply b2_announce
    | by eapply b2_unlink
    | by eapply b2_record_error
    | by eapply b2_begin_run
    | by eapply b2_end_or_clear
    | by (eapply b2_add_node; [eassumption|eassumption|reflexivity])
    | by (eapply b2_modify_node; [eassumption|eassumption|intro; reflexivity])
    | by (eapply b2_m_modify; [eassumption|eassumption|let st := fresh "st" in intro st; unfold ghost_publish, ghost_run_end, ghost_run_start, ghost_effect_start, ghost_dirty_cleared, ghost_resolved, ghost_registered, run_end_unnoticed, slot_holds, add_if, set_node; destruct st; simpl; repeat match goal with | |- context [match ?m !! ?k with _ => _ end] => destruct (m !! k) end; split; reflexivity])
    ].

Record b2_inv_at (f : nat) : Prop := {
  B2exec : forall e c s s' r, exec f e c s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2read : forall n b s s' r, read f n b s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2read_node : forall n s s' r, read_node f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2update : forall n s s' r, update_if_necessary f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2rop : forall n s s' r, refresh_or_pull f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2pull : forall n s s' r, pull f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2refresh : forall n s s' r, refresh f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2walk : forall n i s s' r, resolve_walk f n i s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2recompute : forall n s s' r, recompute f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2run_memo : forall n s s' r, run_memo f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2execute : forall n s s' r, execute_effect f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2run_effect : forall n s s' r, run_effect f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2body : forall n s s' r, run_effect_body f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2discharge : forall o s s' r, discharge f o s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2retire : forall o s s' r, retire f o s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2dispose_scope : forall o s s' r, dispose_scope f o s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2teardown : forall o ls cs s s' r, teardown f o ls cs s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2cleanup : forall o b e s s' r, run_cleanup f o b e s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2release : forall n s s' r, release f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2dispose : forall n s s' r, dispose_node f n s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2attach : forall n o s s' r, attach f n o s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2write : forall n v s s' r, write_signal f n v s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2request : forall s s' r, request_flush f s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2settle : forall s s' r, settle_flush f s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2flush : forall s s' r, run_flush f s = Some (s', r) -> b2_inv s -> b2_inv s';
  B2drain : forall s s' r, drain f s = Some (s', r) -> b2_inv s -> b2_inv s'
}.

Lemma b2_inv_all f : b2_inv_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - split.
    + intros e c s s' r H Hs. cbn [exec] in H.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0
      | H0 : context [on_value] |- _ => unfold on_value in H0
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (eapply b2_m_return; [exact H|exact Hs]); try (simplify_eq; exact Hs).
      * by eapply (B2exec _ IH).
      * destruct (decide _); by eapply (B2exec _ IH).
      * eapply (b2_bind_of H); [intros ????; by eapply (B2read _ IH)|intros o s1 _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] s2 _ Hs2 Hk2|exact Hs1].
        destruct o; [by eapply (B2exec _ IH)|by eapply b2_m_return].
      * eapply (b2_bind_of H); [intros ????; by eapply (B2read _ IH)|intros o s1 _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] s2 _ Hs2 Hk2|exact Hs1].
        destruct o; by eapply (B2exec _ IH).
      * eapply (b2_bind_of H); [intros ????; by eapply (B2read _ IH)|intros o s1 _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] s2 _ Hs2 Hk2|exact Hs1].
        destruct o; [by eapply (B2exec _ IH)|by eapply b2_m_return].
      * eapply (b2_bind_of H); [intros ????; by eapply (B2write _ IH)|intros [] s1 _ Hs1 Hk; by eapply (B2exec _ IH)|exact Hs].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros ?? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; by eapply (B2exec _ IH)|exact Hs1].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros ?? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
        eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [o|] ? _ Hs3 Hk3; [|by eapply b2_m_return]|exact Hs2].
        eapply (b2_bind_of Hk3); [intros ????; by eapply (B2attach _ IH)|intros [] ? _ Hs4 Hk4; by eapply (B2exec _ IH)|exact Hs3].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros ?? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
        eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [o|] ? _ Hs3 Hk3; [|by eapply b2_m_return]|exact Hs2].
        eapply (b2_bind_of Hk3); [intros ????; by eapply (B2attach _ IH)|intros [] ? _ Hs4 Hk4|exact Hs3].
        eapply (b2_bind_of Hk4); [intros ?? Hs_ Hm_; unfold m_step in Hm_; destruct (mark_dirty f _ _) eqn:Em; simplify_eq; eapply (b2_inv_any_mark _ _ None); [right; left; exact Em|exact Hs_]|intros [] ? _ Hs5 Hk5|exact Hs4].
        eapply (b2_bind_of Hk5); [intros ????; by eapply (B2request _ IH)|intros [] ? _ Hs6 Hk6; by eapply (B2exec _ IH)|exact Hs5].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [o|] ? _ Hs1 Hk; [|by eapply b2_m_return]|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros x ? _ Hs2 Hk2|exact Hs1].
        eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
        destruct (o_disposed x).
        -- eapply (b2_bind_of Hk3); [|intros [] ? _ Hs4 Hk4; by eapply (B2exec _ IH)|exact Hs3].
           intros s4 [] _ Hm. eapply (b2_bind_of Hm); [|intros [] ? _ Hs5 Hreq; by eapply (B2request _ IH)|exact Hs3].
           intros s5 [] _ Hd. by eapply b2_detached; [exact Hd|intros ?????; by eapply (B2cleanup _ IH)|exact Hs3].
        -- eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4; by eapply (B2exec _ IH)|exact Hs3].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
        destruct (n_kind d); [discriminate| |];
          (eapply (b2_bind_of Hk); [intros ????; by eapply (B2dispose _ IH)|intros [] ? _ Hs2 Hk2|exact Hs1];
           eapply (b2_bind_of Hk2); [intros ????; destruct (n_disposed d); b2_pure_tac|intros [] ? _ Hs3 Hk3; by eapply (B2exec _ IH)|exact Hs2]).
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; by eapply (B2exec _ IH)|intros r0 ? _ Hs2 Hk2|exact Hs1].
        eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
        destruct r0 as [|]; [|by eapply b2_m_return].
        eapply (b2_bind_of Hk3); [intros ????; by eapply (B2request _ IH)|intros [] ? _ Hs4 Hk4; by eapply (B2exec _ IH)|exact Hs3].
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros ?? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; by eapply (B2exec _ IH)|exact Hs1].
    + intros n b s s' r H Hs. cbn [read] in H.
      destruct b; [by eapply (B2read_node _ IH)|eapply b2_untracked; [exact H|intros ?????; by eapply (B2read_node _ IH)|exact Hs]].
    + intros n s s' r H Hs. cbn [read_node] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      destruct (n_kind d); [| |discriminate].
      * eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; by eapply b2_m_return|exact Hs1].
      * eapply (b2_bind_of Hk); [|intros d' ? _ Hs2 Hk2; destruct (n_status d'); by eapply b2_m_return|exact Hs1].
        intros s2 d' _ Hm. destruct (stale_live d).
        -- eapply (b2_bind_of Hm); [intros ????; by eapply (B2rop _ IH)|intros d0 ? _ Hs2 Hk2|exact Hs1].
           eapply (b2_bind_of Hk2); [|intros [] ? _ Hs3 Hk3; by eapply b2_m_return|exact Hs2].
           intros s3 [] _ Hm2.
           eapply (b2_bind_of Hm2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
           eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros d'' ? _ Hs4 Hk4|exact Hs3].
           destruct (stale_live d''); [b2_pure_tac|by eapply b2_m_return].
        -- eapply (b2_bind_of Hm); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; b2_pure_tac|exact Hs1].
    + intros n s s' r H Hs. cbn [update_if_necessary] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      destruct (n_kind d); [by eapply b2_m_return| |by eapply b2_m_return].
      destruct (stale_live d); [|by eapply b2_m_return].
      eapply (b2_bind_of Hk); [intros ????; by eapply (B2rop _ IH)|intros ?? _ Hs2 Hk2; by eapply b2_m_return|exact Hs1].
    + intros n s s' r H Hs. cbn [refresh_or_pull] in H. rewrite bind_get in H.
      destruct (deferring s); [|by eapply (B2pull _ IH)].
      eapply (b2_bind_of H); [intros ????; by eapply (B2refresh _ IH)|intros [] ? _ Hs1 Hk; b2_pure_tac|exact Hs].
    + intros n s s' r H Hs. cbn [pull] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; by eapply (B2refresh _ IH)|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros d ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4|exact Hs3].
      rewrite bind_get in Hk4.
      eapply (b2_bind_of Hk4); [intros s5 ? Hs4' Hm; destruct (bool_decide _ && _); [by eapply (B2settle _ IH)|by eapply b2_m_return]|intros [] ? _ Hs5 Hk5; by eapply b2_m_return|exact Hs4].
    + intros n s s' r H Hs. cbn [refresh] in H. rewrite bind_get in H.
      destruct (decide (n ∈ g_checking s)).
      { unfold m_step in H. destruct (mark_dirty f n s) eqn:Em; simplify_eq. eapply (b2_inv_any_mark _ _ None); [right; left; exact Em|exact Hs]. }
      destruct (decide (n ∈ g_running s)); [by eapply b2_m_return|].
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [|intros [] ? _ Hs2 Hk2; eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros d' ? _ Hs3 Hk3; destruct (decide (n_freshness d' = Dirty)); [by eapply (B2recompute _ IH)|by eapply b2_m_return]|exact Hs2]|exact Hs1].
      intros s2 [] _ Hm. destruct (decide (n_freshness d = Check)); [|by eapply b2_m_return].
      eapply (b2_bind_of Hm); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; by eapply (B2walk _ IH)|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4|exact Hs3].
      eapply (b2_bind_of Hk4); [intros ????; b2_pure_tac|intros d' ? _ Hs5 Hk5|exact Hs4].
      destruct (decide (n_freshness d' = Check)); [|by eapply b2_m_return].
      eapply (b2_bind_of Hk5); [intros ????; b2_pure_tac|intros [] ? _ Hs6 Hk6; b2_pure_tac|exact Hs5].
    + intros n i s s' r H Hs. cbn [resolve_walk] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      rewrite bind_get in Hk. destruct (decide (n_freshness d = Check)); [|by eapply b2_m_return].
      destruct (source_slots (sources_of (g_edges s1) n) !! i) as [[src _]|].
      * eapply (b2_bind_of Hk); [intros ????; by eapply (B2update _ IH)|intros [] ? _ Hs2 Hk2|exact Hs1].
        eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros d' ? _ Hs3 Hk3|exact Hs2].
        eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros d_src ? _ Hs4 Hk4|exact Hs3].
        rewrite bind_get in Hk4.
        eapply (b2_bind_of Hk4); [|intros [] ? _ Hs5 Hk5; by eapply (B2walk _ IH)|exact Hs4].
        intros s5 [] _ Hm. destruct (decide _); [|by eapply b2_m_return].
        unfold m_step in Hm. destruct (mark_dirty f n _) eqn:Em; simplify_eq.
        eapply (b2_inv_any_mark _ _ None); [right; left; exact Em|exact Hs4].
      * destruct (slot_holds stale_live s1 n); [by eapply (B2walk _ IH)|by eapply b2_m_return].
    + intros n s s' r H Hs. cbn [recompute] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      destruct (n_scope d) as [o|]; [|by eapply (B2run_memo _ IH)].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; by eapply (B2discharge _ IH)|intros [] ? _ Hs4 Hk4|exact Hs3].
      eapply (b2_bind_of Hk4); [intros ????; b2_pure_tac|intros d' ? _ Hs5 Hk5|exact Hs4].
      destruct (n_disposed d').
      * destruct (decide (n_freshness d' = Clean)); [b2_pure_tac|by eapply b2_m_return].
      * destruct (decide (n_runs d' = n_runs d)); [by eapply (B2run_memo _ IH)|].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (B2recompute _ IH)|by eapply b2_m_return].
    + intros n s s' r H Hs. cbn [run_memo] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4|exact Hs3].
      eapply (b2_bind_of Hk4); [intros ????; b2_pure_tac|intros [] ? _ Hs5 Hk5|exact Hs4].
      eapply (b2_bind_of Hk5); [intros ????; eapply b2_hosted; [eassumption|intros ?????; by eapply (B2exec _ IH)|eassumption]|intros r0 ? _ Hs6 Hk6|exact Hs5].
      eapply (b2_bind_of Hk6); [intros ????; b2_pure_tac|intros d1 ? _ Hs7 Hk7|exact Hs6].
      eapply (b2_bind_of Hk7); [intros ????; destruct (if n_violated d1 then Fail pure_scope_failure else r0); b2_pure_tac|intros [] ? _ Hs8 Hk8|exact Hs7].
      eapply (b2_bind_of Hk8); [intros ????; b2_pure_tac|intros [] ? _ Hs9 Hk9|exact Hs8].
      eapply (b2_bind_of Hk9); [intros ????; b2_pure_tac|intros [] ? _ Hs10 Hk10|exact Hs9].
      eapply (b2_bind_of Hk10); [intros ????; destruct (if n_violated d1 then Fail pure_scope_failure else r0); b2_pure_tac|intros [] ? _ Hs11 Hk11|exact Hs10].
      eapply (b2_bind_of Hk11); [intros ????; b2_pure_tac|intros d2 ? _ Hs12 Hk12|exact Hs11].
      eapply (b2_bind_of Hk12); [intros s13 moved Hs12' Hcmp; destruct (decide _); [by eapply b2_m_return|destruct (n_comparer d2); [by eapply b2_m_return|by eapply b2_m_return|eapply (b2_bind_of Hcmp); [intros ????; b2_pure_tac|intros [] ? _ Hs_ Hm_; by eapply b2_m_return|exact Hs12']]]|intros moved ? _ Hs13 Hk13|exact Hs12].
      rewrite bind_get in Hk13.
      eapply (b2_bind_of Hk13); [intros ????; b2_pure_tac|intros [] ? _ Hs14 Hk14|exact Hs13].
      eapply (b2_bind_of Hk14); [|intros [] ? _ Hs15 Hk15; eapply (b2_bind_of Hk15); [intros ????; b2_pure_tac|intros d3 ? _ Hs16 Hk16; destruct (decide (n_freshness d3 = Clean)); [by eapply b2_m_return|b2_pure_tac]|exact Hs15]|exact Hs14].
      intros s15 [] _ Hm. destruct moved; [|by eapply b2_m_return].
      eapply (b2_bind_of Hm); [intros ?? Hs_ Hm_; unfold m_step in Hm_; destruct (notify_dirty_except f n _ _) eqn:Em; simplify_eq; eapply b2_inv_any_mark; [do 4 right; exact Em|exact Hs_]|intros [] s16 _ Hs16 Hk16; rewrite bind_get in Hk16; destruct (bool_decide _ && _ && _); [b2_pure_tac|by eapply b2_m_return]|exact Hs14].
    + intros n s s' r H Hs. cbn [execute_effect] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      destruct (n_disposed d); [b2_pure_tac|].
      eapply (b2_bind_of Hk); [|intros [] ? _ Hs2 Hk2|exact Hs1].
      * intros s2 [] _ Hm. destruct (decide (n_freshness d = Check)); [|by eapply b2_m_return].
        eapply (b2_bind_of Hm); [intros ????; by eapply (B2walk _ IH)|intros [] ? _ Hs3 Hk3|exact Hs1].
        eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros d' ? _ Hs4 Hk4|exact Hs3].
        destruct (decide (n_freshness d' = Check)); [|by eapply b2_m_return].
        eapply (b2_bind_of Hk4); [intros ????; b2_pure_tac|intros [] ? _ Hs5 Hk5; b2_pure_tac|exact Hs4].
      * eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros d' ? _ Hs3 Hk3|exact Hs2].
        eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4; destruct (decide (n_freshness d' = Dirty)); [by eapply (B2run_effect _ IH)|by eapply b2_m_return]|exact Hs3].
    + intros n s s' r H Hs. cbn [run_effect] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros d ? _ Hs4 Hk4|exact Hs3].
      eapply (b2_bind_of Hk4); [|intros [] ? _ Hs5 Hk5|exact Hs4].
      * intros s5 [] _ Hm. destruct (n_scope d) as [o|]; [|by eapply b2_m_return].
        eapply (b2_bind_of Hm); [intros ????; b2_pure_tac|intros [] ? _ Hs5 Hk5|exact Hs4].
        eapply (b2_bind_of Hk5); [intros ????; by eapply (B2discharge _ IH)|intros [] ? _ Hs6 Hk6; b2_pure_tac|exact Hs5].
      * eapply (b2_bind_of Hk5); [intros ????; b2_pure_tac|intros d' ? _ Hs6 Hk6|exact Hs5].
        eapply (b2_bind_of Hk6); [intros s7 [] _ Hm; destruct (n_disposed d'); [by eapply b2_m_return|by eapply (B2body _ IH)]|intros [] ? _ Hs7 Hk7; b2_pure_tac|exact Hs6].
    + intros n s s' r H Hs. cbn [run_effect_body] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ????; eapply b2_hosted; [eassumption|intros ?????; by eapply (B2exec _ IH)|eassumption]|intros r0 ? _ Hs4 Hk4|exact Hs3].
      eapply (b2_bind_of Hk4); [intros ????; b2_pure_tac|intros d' ? _ Hs5 Hk5|exact Hs4].
      eapply (b2_bind_of Hk5); [intros ????; b2_pure_tac|intros [] ? _ Hs6 Hk6; destruct r0; [by eapply b2_m_return|eapply (b2_bind_of Hk6); [intros ????; b2_pure_tac|intros [] ? _ Hs7 Hk7; b2_pure_tac|exact Hs6]]|exact Hs5].
    + intros o s s' r H Hs. cbn [discharge] in H.
      eapply (b2_bind_of H); [intros ????; eapply b2_detached; [eassumption|intros ?????; by eapply (B2dispose_scope _ IH)|eassumption]|intros [] ? _ Hs1 Hk; rewrite bind_get in Hk; destruct (bool_decide _ && _ && _); [b2_pure_tac|by eapply b2_m_return]|exact Hs].
    + intros o s s' r H Hs. cbn [retire] in H.
      eapply (b2_bind_of H); [|intros [] ? _ Hs1 Hk; rewrite bind_get in Hk; destruct (live_queue s1); [by eapply (B2request _ IH)|by eapply b2_m_return]|exact Hs].
      intros s1 [] _ Hm. eapply b2_detached; [exact Hm| |exact Hs].
      intros s0 s2 r2 Hm0 Hs0.
      eapply (b2_bind_of Hm0); [intros ????; b2_pure_tac|intros x ? _ Hs2 Hk2; destruct (o_disposed x); [by eapply b2_m_return|eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3; by eapply (B2dispose_scope _ IH)|exact Hs2]]|exact Hs0].
    + intros o s s' r H Hs. cbn [dispose_scope] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros x ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; by eapply (B2teardown _ IH)|exact Hs1].
    + intros o ls cs s s' r H Hs. cbn [teardown] in H.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [by eapply b2_m_return|..].
      * eapply (b2_bind_of H); [intros ????; by eapply (B2cleanup _ IH)|intros [] ? _ Hs1 Hk; by eapply (B2teardown _ IH)|exact Hs].
      * eapply (b2_bind_of H); [intros ????; by eapply (B2release _ IH)|intros [] ? _ Hs1 Hk; by eapply (B2teardown _ IH)|exact Hs].
      * destruct (decide (a < l_acquired l)).
        -- eapply (b2_bind_of H); [intros ????; by eapply (B2release _ IH)|intros [] ? _ Hs1 Hk; by eapply (B2teardown _ IH)|exact Hs].
        -- eapply (b2_bind_of H); [intros ????; by eapply (B2cleanup _ IH)|intros [] ? _ Hs1 Hk; by eapply (B2teardown _ IH)|exact Hs].
    + intros o b e s s' r H Hs. cbn [run_cleanup] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros x ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; by eapply (B2exec _ IH)|intros r0 ? _ Hs3 Hk3|exact Hs2].
      destruct r0; [by eapply b2_m_return|].
      eapply (b2_bind_of Hk3); [intros ????; b2_pure_tac|intros [] ? _ Hs4 Hk4; b2_pure_tac|exact Hs3].
    + intros n s s' r H Hs. cbn [release] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk; by eapply (B2dispose _ IH)|exact Hs].
    + intros n s s' r H Hs. cbn [dispose_node] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d s1 _ Hs1 Hk|exact Hs].
      destruct (n_disposed d); [by eapply b2_m_return|].
      eapply bind_inv in Hk as (s1' & [] & E1' & Hk').
      eapply bind_inv in Hk' as (s2' & [] & E2' & Hk'').
      eapply bind_inv in Hk'' as (s3' & [] & E3' & Hk''').
      assert (Hs3' : b2_inv s3') by (eapply b2_dispose_prefix; [exact Hs1|exact E1'|exact E2'|exact E3']).
      destruct (n_scope d); [by eapply (B2retire _ IH)|by eapply b2_m_return].
    + intros n o s s' r H Hs. cbn [attach] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros x ? _ Hs1 Hk|exact Hs].
      destruct (o_disposed x).
      * eapply (b2_bind_of Hk); [intros ????; by eapply (B2release _ IH)|intros [] ? _ Hs2 Hk2; b2_pure_tac|exact Hs1].
      * eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2; b2_pure_tac|exact Hs1].
    + intros n v s s' r H Hs. cbn [write_signal] in H.
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros d ? _ Hs1 Hk|exact Hs].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [by eapply b2_m_return|].
      eapply (b2_bind_of Hk); [intros ????; b2_pure_tac|intros [] ? _ Hs2 Hk2|exact Hs1].
      eapply (b2_bind_of Hk2); [intros ????; b2_pure_tac|intros [] ? _ Hs3 Hk3|exact Hs2].
      eapply (b2_bind_of Hk3); [intros ?? Hs_ Hm_; unfold m_step in Hm_; destruct (notify_dirty f n _) eqn:Em; simplify_eq; eapply (b2_inv_any_mark _ _ None); [do 3 right; left; exact Em|exact Hs_]|intros [] ? _ Hs4 Hk4; by eapply (B2request _ IH)|exact Hs3].
    + intros s s' r H Hs. cbn [request_flush] in H. rewrite bind_get in H.
      destruct (live_queue s && bool_decide (g_batch_depth s = 0)); [|by eapply b2_m_return].
      destruct (outside_bodies s); [by eapply (B2flush _ IH)|].
      destruct (g_flushing s); [by eapply b2_m_return|b2_pure_tac].
    + intros s s' r H Hs. cbn [settle_flush] in H. rewrite bind_get in H.
      destruct (g_flush_owed s && bool_decide (g_batch_depth s = 0) && outside_bodies s); [by eapply (B2flush _ IH)|by eapply b2_m_return].
    + intros s s' r H Hs. cbn [run_flush] in H. rewrite bind_get in H.
      destruct (g_flushing s); [by eapply b2_m_return|].
      eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk|exact Hs].
      eapply (b2_bind_of Hk); [intros ????; by eapply (B2drain _ IH)|intros [] ? _ Hs2 Hk2; b2_pure_tac|exact Hs1].
    + intros s s' r H Hs. cbn [drain] in H. rewrite bind_get in H.
      destruct (g_queue s !! g_queue_head s).
      * eapply (b2_bind_of H); [intros ????; b2_pure_tac|intros [] ? _ Hs1 Hk|exact Hs].
        eapply (b2_bind_of Hk); [intros ????; by eapply (B2execute _ IH)|intros [] ? _ Hs2 Hk2; by eapply (B2drain _ IH)|exact Hs1].
      * b2_pure_tac.
Qed.

(**md
**Every disposed computation reachable from a new graph holds empty `source_slots` at all points (including on the running stack), and off-stack disposed nodes have no edges in either map.**

Strengthens [`Ownership.disposed_no_edges`] to hold even during an in-flight computation (`dispose_node` clears `sources` via `sources_clear` and `#318` `live_current` blocks `track` on a disposed current node), guaranteeing that reading any slot `k` during `track` witnesses `n_disposed d = false`.

- [f:] fuel bound.
- [fl:] initial flags.
- [c:] program command executed from `init fl`.
- [s':] reachable intermediate or final state (`b2_inv` preserved across all 26 engine functions).
- [r:] command outcome.
- [n:] disposed node id.
- [d:] node record at `n`.
- [k:] slot index observed in `sources_of`.
- [p:] slot pair `(src, handle)`.
*)
Theorem disposed_empty_sources f fl c s' r n d :
  run_top f c (init fl) = Some (s', r) ->
  g_nodes s' !! n = Some d -> n_disposed d = true ->
  source_slots (sources_of (g_edges s') n) = [] /\
  (n ∉ g_running s' ->
    (forall src, ~ edge s' src n) /\
    (forall src, n ∉ observer_view (observers_of (g_edges s') src))) /\
  (forall k p, source_slots (sources_of (g_edges s') n) !! k = Some p -> False).
Proof.
  intros Hrun Hn Hdz.
  pose proof (B2exec _ (b2_inv_all f) [] c _ _ _ Hrun (b2_inv_init fl)) as [Hm Hd].
  pose proof (Hd n d Hn Hdz) as Hslots.
  split_and!; [exact Hslots| |intros k p Hp; rewrite Hslots, lookup_nil in Hp; discriminate].
  intros Hnr. split.
  - intros src (i & h & Hsl & _). unfold slot_at in Hsl. rewrite Hslots in Hsl. discriminate.
  - intros src Hin. apply view_elem in Hin as (h & i & Hobs).
    pose proof (proj2 (Hm n i src h) Hobs) as Hsl.
    unfold slot_at in Hsl. rewrite Hslots, lookup_nil in Hsl. discriminate.
Qed.

(**md
**When `source_cursor = 0` and slot `0` matches `src`, `track` refines to `n_disposed = false` and setting `source_cursor := 1`.**

Refines `Graph.Track` + `SourceList.Add` under `b2_inv` (and for any state reachable from `init` or satisfying `engine_inv` + on-stack cleared disposed slots): the presence of slot `0` rules out disposal without an explicit branch, `#318` makes `live_current s = Some c`, and `sources_add c src` hits slot `0` and sets `source_cursor := 1`.

- [s:] current engine state satisfying `b2_inv s`.
- [c:] `g_current s` computation id.
- [d:] node record at `c`.
- [src:] source id read.
- [h:] handle stored at slot `0`.
*)
Theorem track_slot0_fast_eq s c d src h :
  b2_inv s ->
  g_current s = Some c ->
  g_nodes s !! c = Some d ->
  source_cursor (sources_of (g_edges s) c) = 0 ->
  source_slots (sources_of (g_edges s) c) !! 0 = Some (src, h) ->
  n_disposed d = false /\
  live_current s = Some c /\
  sources_add c src (g_edges s) = Some (set_cursor 1 c (g_edges s)) /\
  track src s = Some (ghost_read c src (read_prefix (g_edges s) c) (with_edges (set_cursor 1 c (g_edges s)) s), tt).
Proof.
  intros [_ Hd] Hcur Hc Hcursor Hslot0.
  assert (Hdz : n_disposed d = false).
  { destruct (n_disposed d) eqn:E; [|reflexivity].
    rewrite (Hd c d Hc E), lookup_nil in Hslot0. discriminate. }
  assert (Hlive : live_current s = Some c)
    by (unfold live_current; by rewrite Hcur, Hc, Hdz).
  assert (Hadd : sources_add c src (g_edges s) = Some (set_cursor 1 c (g_edges s))).
  { unfold sources_add, set_cursor. rewrite Hcursor, Hslot0. simpl.
    by rewrite decide_True. }
  split_and!; [exact Hdz|exact Hlive|exact Hadd|].
  unfold track. by rewrite Hlive, Hadd.
Qed.

(**md
**When `source_cursor = 1` and slot `1` matches `src`, `track` refines to `n_disposed = false` and setting `source_cursor := 2`.**

Refines `Graph.Track` + `SourceList.Add` under `b2_inv`: reading slot `1` establishes `n_disposed d = false` (`disposed_empty_sources`), so `live_current s = Some c` and `sources_add c src` bumps `source_cursor` from `1` to `2`.

- [s:] current engine state satisfying `b2_inv s`.
- [c:] `g_current s` computation id.
- [d:] node record at `c`.
- [src:] source id read.
- [h:] handle stored at slot `1`.
*)
Theorem track_slot1_fast_eq s c d src h :
  b2_inv s ->
  g_current s = Some c ->
  g_nodes s !! c = Some d ->
  source_cursor (sources_of (g_edges s) c) = 1 ->
  source_slots (sources_of (g_edges s) c) !! 1 = Some (src, h) ->
  n_disposed d = false /\
  live_current s = Some c /\
  sources_add c src (g_edges s) = Some (set_cursor 2 c (g_edges s)) /\
  track src s = Some (ghost_read c src (read_prefix (g_edges s) c) (with_edges (set_cursor 2 c (g_edges s)) s), tt).
Proof.
  intros [_ Hd] Hcur Hc Hcursor Hslot1.
  assert (Hdz : n_disposed d = false).
  { destruct (n_disposed d) eqn:E; [|reflexivity].
    rewrite (Hd c d Hc E), lookup_nil in Hslot1. discriminate. }
  assert (Hlive : live_current s = Some c)
    by (unfold live_current; by rewrite Hcur, Hc, Hdz).
  assert (Hadd : sources_add c src (g_edges s) = Some (set_cursor 2 c (g_edges s))).
  { unfold sources_add, set_cursor. rewrite Hcursor, Hslot1. simpl.
    by rewrite decide_True. }
  split_and!; [exact Hdz|exact Hlive|exact Hadd|].
  unfold track. by rewrite Hlive, Hadd.
Qed.


(** * B3: Marking fast paths — leaf clean memo, moved single-check memo, iterative check spine *)

Lemma notify_with_nil mark src s :
  observer_items (observers_of (g_edges s) src) = [] ->
  notify_with mark src s = Some s.
Proof.
  intros Hobs. unfold notify_with. rewrite Hobs. reflexivity.
Qed.

Lemma notify_with_singleton mark src c slot s :
  observer_items (observers_of (g_edges s) src) = [(c, slot)] ->
  notify_with mark src s = mark c s.
Proof.
  intros Hobs. unfold notify_with. rewrite Hobs. simpl.
  rewrite Hobs. destruct (decide _); [simpl in *; lia|].
  simpl. destruct (mark c s); reflexivity.
Qed.

(**md
**Marking a `Clean` leaf memo dirty updates `n_freshness := Dirty` directly while preserving `n_unannounced` and `g_edges`.**

Refines the leaf-clean fast path in `NodeOps.MarkDirty`: because `observer_items` of `m` is empty, `notify_with (mark_check f)` terminates immediately without touching `g_edges` or `n_unannounced`.

- [f:] remaining recursion fuel.
- [m:] leaf memo node id.
- [d:] memo record at `m` before marking.
- [s:] engine state before marking.
*)
Theorem leaf_clean_memo_mark_dirty_eq f m d s :
  g_nodes s !! m = Some d ->
  n_kind d = KMemo ->
  n_freshness d = Clean ->
  observer_items (observers_of (g_edges s) m) = [] ->
  let d' := node_marked Dirty (n_unannounced d) (n_queued d) d in
  let s' := set_node m d' s in
  mark_dirty (S f) m s = Some s' /\
  n_unannounced d' = n_unannounced d /\
  g_edges s' = g_edges s.
Proof.
  intros Hm Hk Hc Hobs. simpl. rewrite Hm, Hk, decide_True by exact Hc.
  rewrite notify_with_nil; [|simpl; exact Hobs].
  repeat split; reflexivity.
Qed.

(**md
**`notify_dirty_except` on a source with sole observer `[m]` (`KMemo`, `Check`, `n_unannounced = false`) promotes `m` directly to `Dirty`.**

Refines `NotifyMoved` / `NotifyMovedTop` fast path (`r = None`): since `m` is already `Check` and unflagged for deferred announcement, `mark_dirty` updates `n_freshness m := Dirty` in one write without propagating `mark_check` downstream.

- [f:] remaining recursion fuel.
- [src:] moved source node id.
- [m:] sole memo observer id.
- [slot:] edge slot index stored on `m`.
- [dm:] node record at `m`.
- [s:] engine state before notification.
*)
Theorem notify_moved_single_check_memo_eq f src m slot dm s :
  observer_items (observers_of (g_edges s) src) = [(m, slot)] ->
  g_nodes s !! m = Some dm ->
  n_kind dm = KMemo ->
  n_freshness dm = Check ->
  n_unannounced dm = false ->
  notify_dirty_except (S (S f)) src None s =
    Some (set_node m (node_marked Dirty false (n_queued dm) dm) s).
Proof.
  intros Hobs Hm Hk Hc Hu. unfold notify_dirty_except.
  erewrite notify_with_singleton; [|exact Hobs].
  unfold mark_except. rewrite decide_False by (intros [H _]; discriminate).
  simpl. rewrite Hm, Hk, decide_False by congruence.
  unfold announce_due. rewrite Hu, andb_false_r.
  rewrite decide_False by congruence.
  reflexivity.
Qed.

(** Tail-recursive single-memo `Check` spine walk mirroring `NotifyCheckSpine`, falling back to `notify_check` on fan-out > 1 or non-memo observers. *)
Fixpoint iter_mark_check_chain (fuel : nat) (n : node_id) (s : state) {struct fuel} : option state :=
  match fuel with
  | 0 => None
  | S f =>
    match g_nodes s !! n with
    | Some d =>
      match n_kind d with
      | KMemo =>
        if bool_decide (n_freshness d = Clean) || announce_due s d then
          let nf := if decide (n_freshness d = Clean) then Check else n_freshness d in
          let nu := if decide (n_freshness d = Clean) then n_unannounced d else false in
          let s1 := set_node n (node_marked nf nu (n_queued d) d) s in
          match observer_items (observers_of (g_edges s1) n) with
          | [] => Some s1
          | [(next, _)] =>
            match g_nodes s1 !! next with
            | Some dn =>
              match n_kind dn with
              | KMemo => iter_mark_check_chain f next s1
              | _ => notify_check f n s1
              end
            | None => notify_check f n s1
            end
          | _ => notify_check f n s1
          end
        else Some s
      | KEffect =>
        Some (effect_marked (if decide (n_freshness d = Clean) then Check else n_freshness d) n d s)
      | KSignal => Some s
      end
    | None => Some s
    end
  end.

(**md
**The iterative single-memo spine walk `iter_mark_check_chain` is extensionally equal to `mark_check` on all inputs and states.**

Refines `NotifyCheckSpine`: tail-recursing down single `KMemo` observer edges and falling back to `notify_check` on empty, non-memo, or multi-observer edges produces the exact same `option state` as recursive `mark_check` (and in particular along any clean single-memo chain).

- [fuel:] fuel bound.
- [n:] starting node id.
- [s:] starting engine state.
*)
Theorem iterative_mark_check_chain_eq fuel n s :
  iter_mark_check_chain fuel n s = mark_check fuel n s.
Proof.
  revert n s. induction fuel as [|f IH]; intros n s; simpl; [reflexivity|].
  destruct (g_nodes s !! n) as [d|]; [|reflexivity].
  destruct (n_kind d) eqn:Hk; [reflexivity| |reflexivity].
  destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
  - rewrite bool_decide_true by exact Hc. simpl.
    destruct (observer_items (observers_of (g_edges s) n)) as [|[next slot] [|p2 rest]] eqn:Hobs.
    + symmetry. apply notify_with_nil. simpl. exact Hobs.
    + destruct (<[n := node_marked Check (n_unannounced d) (n_queued d) d]> (g_nodes s) !! next) as [dn|];
        [|by unfold notify_check].
      destruct (n_kind dn); [by unfold notify_check| |by unfold notify_check].
      rewrite IH. symmetry. eapply notify_with_singleton. simpl. exact Hobs.
    + by unfold notify_check.
  - rewrite bool_decide_false by exact Hc. simpl.
    destruct (announce_due s d) eqn:Hann; [|reflexivity].
    destruct (observer_items (observers_of (g_edges s) n)) as [|[next slot] [|p2 rest]] eqn:Hobs.
    + symmetry. apply notify_with_nil. simpl. exact Hobs.
    + destruct (<[n := node_marked (n_freshness d) false (n_queued d) d]> (g_nodes s) !! next) as [dn|];
        [|by unfold notify_check].
      destruct (n_kind dn); [by unfold notify_check| |by unfold notify_check].
      rewrite IH. symmetry. eapply notify_with_singleton. simpl. exact Hobs.
    + by unfold notify_check.
Qed.

(** * B4: Explicit-stack iterative single-memo `Check`-chain refresh *)

Lemma b4_bind_assoc {A B C} (m : M A) (k1 : A -> M B) (k2 : B -> M C) s :
  m_bind (m_bind m k1) k2 s = m_bind m (fun a => m_bind (k1 a) k2) s.
Proof.
  unfold m_bind. destruct (m s) as [[s1 a]|]; reflexivity.
Qed.

Lemma b4_bind_ret_l {A B} (a : A) (k : A -> M B) s :
  m_bind (m_return a) k s = k a s.
Proof. reflexivity. Qed.

Lemma b4_bind_ext {A B} (m : M A) (k1 k2 : A -> M B) s :
  (forall a s1, k1 a s1 = k2 a s1) ->
  m_bind m k1 s = m_bind m k2 s.
Proof.
  intros Hk. unfold m_bind. destruct (m s) as [[s1 a]|]; [apply Hk|reflexivity].
Qed.

(** Single-memo `Check` descent guard matching `RefreshChain`'s linear spine check. *)
Definition single_memo_check_slot (s : state) (cur : node_id) : option node_id :=
  if deferring s then
    if decide (cur ∉ g_checking s /\ cur ∉ g_running s) then
      match g_nodes s !! cur with
      | Some d =>
        if decide (n_freshness d = Check) then
          match source_slots (sources_of (g_edges s) cur) with
          | [(src, _)] =>
            if decide (src <> cur /\ src ∉ g_checking s /\ src ∉ g_running s) then
              match g_nodes s !! src with
              | Some d_src =>
                match n_kind d_src with
                | KMemo =>
                  if decide (n_freshness d_src = Check /\ n_disposed d_src = false)
                  then Some src else None
                | _ => None
                end
              | None => None
              end
            else None
          | _ => None
          end
        else None
      | None => None
      end
    else None
  else None.

Definition refresh_after_src (f_md f_rw f_rec : nat) (cur src : node_id) : M unit :=
  let* _ := get_node src in
  let* d' := get_node cur in
  let* d_src' := get_node src in
  let* s1 := m_get in
  let* _ :=
    if decide (n_freshness d' = Check /\ stale_live d_src' = true /\ (src ∈ g_running s1 \/ src ∈ g_checking s1))
    then m_step (mark_dirty f_md cur) else m_return tt in
  let* _ := resolve_walk f_rw cur 1 in
  let* _ := pop_checking in
  let* d'' := get_node cur in
  let* _ :=
    if decide (n_freshness d'' = Check)
    then let* _ := m_modify (ghost_resolved cur) in
         modify_node cur (fun x => node_with_unannounced false (node_with_freshness Clean x))
    else m_return tt in
  let* d''' := get_node cur in
  if decide (n_freshness d''' = Dirty) then recompute f_rec cur else m_return tt.

Definition iter_unwind_one (fuel : nat) (cur src : node_id) : M unit :=
  refresh_after_src fuel fuel fuel cur src.

Fixpoint iter_unwind (fuel : nat) (stk : list (node_id * node_id)) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    match stk with
    | [] => m_return tt
    | (cur, src) :: rest =>
      let* _ := iter_unwind_one f cur src in
      iter_unwind f rest
    end
  end.

Fixpoint iter_descend (fuel : nat) (stk : list (node_id * node_id)) (cur : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    match single_memo_check_slot s cur with
    | Some src =>
      let* _ := push_checking cur in
      iter_descend f ((cur, src) :: stk) src
    | None =>
      let* _ := refresh f cur in
      iter_unwind f stk
    end
  end.

Definition iter_chain_refresh (fuel : nat) (n : node_id) : M unit :=
  iter_descend fuel [] n.

Lemma refresh_after_src_mono f1 f1' f2 f2' f3 f3' cur src s r :
  f1 <= f1' -> f2 <= f2' -> f3 <= f3' ->
  refresh_after_src f1 f2 f3 cur src s = Some r ->
  refresh_after_src f1' f2' f3' cur src s = Some r.
Proof.
  intros H1 H2 H3 H. unfold refresh_after_src in H |- *. revert H.
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [|intros [] ?? _].
  { intros ?? E. destruct (decide _); [by eapply m_step_mark_dirty_mono|exact E]. }
  eapply bind_mono; [intros ??; by eapply (Mwalk _ (engine_mono_all f2))|intros [] ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [intros ?? E; exact E|intros d'' ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros d''' ?? _].
  destruct (decide (n_freshness d''' = Dirty)); [by eapply (Mrecompute _ (engine_mono_all f3))|exact id].
Qed.

Lemma iter_unwind_mono f f' stk s r :
  f <= f' ->
  iter_unwind f stk s = Some r ->
  iter_unwind f' stk s = Some r.
Proof.
  revert f' stk s r. induction f as [|f IH]; intros f' stk s r Hle H.
  - cbn in H. discriminate.
  - destruct f' as [|f']; [lia|].
    assert (Hle' : f <= f') by lia.
    cbn [iter_unwind] in H |- *. destruct stk as [|[cur src] rest]; [exact H|].
    revert H. eapply bind_mono.
    + intros ?? E. eapply refresh_after_src_mono; [exact Hle'|exact Hle'|exact Hle'|exact E].
    + intros [] ?? _ E. eapply IH; [exact Hle'|exact E].
Qed.

Lemma single_memo_check_slot_inv s cur src :
  single_memo_check_slot s cur = Some src ->
  exists d h d_src,
    deferring s = true /\
    cur ∉ g_checking s /\ cur ∉ g_running s /\
    g_nodes s !! cur = Some d /\ n_freshness d = Check /\
    source_slots (sources_of (g_edges s) cur) = [(src, h)] /\
    src <> cur /\ src ∉ g_checking s /\ src ∉ g_running s /\
    g_nodes s !! src = Some d_src /\ n_kind d_src = KMemo /\
    n_freshness d_src = Check /\ n_disposed d_src = false.
Proof.
  unfold single_memo_check_slot.
  destruct (deferring s) eqn:Edef; [|discriminate].
  destruct (decide (cur ∉ g_checking s /\ cur ∉ g_running s)) as [[Hc1 Hc2]|]; [|discriminate].
  destruct (g_nodes s !! cur) as [d|] eqn:Ecur; [|discriminate].
  destruct (decide (n_freshness d = Check)) as [Hfc|]; [|discriminate].
  destruct (source_slots (sources_of (g_edges s) cur)) as [|[s0 h] [|p2 rest]] eqn:Esl; try discriminate.
  destruct (decide (s0 <> cur /\ s0 ∉ g_checking s /\ s0 ∉ g_running s)) as [[Hne [Hs1 Hs2]]|]; [|discriminate].
  destruct (g_nodes s !! s0) as [d_src|] eqn:Esrc; [|discriminate].
  destruct (n_kind d_src) eqn:Hk; try discriminate.
  destruct (decide (n_freshness d_src = Check /\ n_disposed d_src = false)) as [[Hfs Hdz]|]; [|discriminate].
  intros [= <-]. exists d, h, d_src. repeat split; assumption.
Qed.

Lemma deferring_with_checking ch s :
  deferring (with_checking ch s) = deferring s.
Proof. reflexivity. Qed.

Lemma resolve_walk_0_step f cur src s d h :
  g_nodes s !! cur = Some d ->
  n_freshness d = Check ->
  source_slots (sources_of (g_edges s) cur) = [(src, h)] ->
  resolve_walk (S f) cur 0 s =
  (let* _ := update_if_necessary f src in
   let* d' := get_node cur in
   let* d_src := get_node src in
   let* s1 := m_get in
   let* _ :=
     if decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s1 \/ src ∈ g_checking s1))
     then m_step (mark_dirty f cur) else m_return tt in
   resolve_walk f cur 1) s.
Proof.
  intros Hcur Hfc Hsl. cbn [resolve_walk].
  unfold get_node at 1. unfold m_bind at 1. rewrite Hcur.
  rewrite bind_get, decide_True by exact Hfc.
  simpl. rewrite Hsl. reflexivity.
Qed.

Lemma b4_bind_arg {A B} (m1 m2 : M A) (k : A -> M B) s :
  m1 s = m2 s -> m_bind m1 k s = m_bind m2 k s.
Proof. intros Hm. unfold m_bind. rewrite Hm. reflexivity. Qed.

Lemma refresh_single_slot_unfold f cur src s :
  single_memo_check_slot s cur = Some src ->
  refresh (S (S (S (S f)))) cur s =
  m_bind (refresh f src)
    (fun _ => refresh_after_src (S (S f)) (S (S f)) (S (S (S f))) cur src)
    (with_checking (cur :: g_checking s) s).
Proof.
  intros Hslot.
  destruct (single_memo_check_slot_inv _ _ _ Hslot)
    as (d & h & d_src & Hdef & Hc1 & Hc2 & Hcur & Hfc & Hsl & _ & _ & _ & Hsrc & Hks & Hfs & Hdz).
  cbn [refresh]. rewrite bind_get.
  rewrite decide_False by exact Hc1.
  rewrite decide_False by exact Hc2.
  unfold get_node at 1. unfold m_bind at 1. rewrite Hcur.
  rewrite decide_True by exact Hfc.
  rewrite b4_bind_assoc. unfold push_checking, m_modify, m_bind at 1. simpl.
  set (s1 := with_checking (cur :: g_checking s) s).
  rewrite b4_bind_assoc.
  transitivity
    (m_bind (resolve_walk (S (S (S f))) cur 0)
       (fun _ =>
          let* _ := pop_checking in
          let* d'' := get_node cur in
          let* _ :=
            if decide (n_freshness d'' = Check)
            then let* _ := m_modify (ghost_resolved cur) in
                 modify_node cur (fun x => node_with_unannounced false (node_with_freshness Clean x))
            else m_return tt in
          let* d''' := get_node cur in
          if decide (n_freshness d''' = Dirty) then recompute (S (S (S f))) cur else m_return tt)
       s1).
  { apply b4_bind_ext. intros [] st. rewrite ?b4_bind_assoc. apply b4_bind_ext. intros [] st'.
    rewrite ?b4_bind_assoc. apply b4_bind_ext. intros d'' st''. reflexivity. }
  erewrite b4_bind_arg; [|erewrite resolve_walk_0_step; [reflexivity|simpl; exact Hcur|exact Hfc|simpl; exact Hsl]].
  rewrite b4_bind_assoc.
  cbn [update_if_necessary]. rewrite b4_bind_assoc.
  unfold get_node at 1. unfold m_bind at 1. simpl. rewrite Hsrc. simpl.
  rewrite Hks.
  assert (Hstl : stale_live d_src = true) by (unfold stale_live; by rewrite Hfs, Hdz).
  rewrite Hstl. rewrite b4_bind_assoc.
  cbn [refresh_or_pull]. rewrite ?b4_bind_assoc, bind_get.
  unfold s1 at 1. rewrite deferring_with_checking, Hdef.
  rewrite ?b4_bind_assoc.
  apply b4_bind_ext. intros [] st.
  unfold refresh_after_src.
  apply b4_bind_ext. intros _ st0.
  rewrite b4_bind_ret_l.
  rewrite ?b4_bind_assoc. apply b4_bind_ext. intros d' st1.
  rewrite ?b4_bind_assoc. apply b4_bind_ext. intros ds' st2.
  rewrite ?b4_bind_assoc. apply b4_bind_ext. intros s2 st3.
  rewrite ?b4_bind_assoc. apply b4_bind_ext. intros [] st4.
  reflexivity.
Qed.

Lemma b4_bind_push_checking {A} cur (k : unit -> M A) s :
  m_bind (push_checking cur) k s = k tt (with_checking (cur :: g_checking s) s).
Proof. reflexivity. Qed.

Lemma refresh_under_4_none f1 cur src s :
  f1 < 4 ->
  single_memo_check_slot s cur = Some src ->
  refresh f1 cur s = None.
Proof.
  intros Hlt Hslot.
  destruct (single_memo_check_slot_inv _ _ _ Hslot)
    as (d & h & d_src & Hdef & Hc1 & Hc2 & Hcur & Hfc & Hsl & _ & _ & _ & Hsrc & Hks & Hfs & Hdz).
  assert (Hstl : stale_live d_src = true) by (unfold stale_live; by rewrite Hfs, Hdz).
  destruct f1 as [|[|[|[|f']]]]; [reflexivity| | | |lia].
  - cbn [refresh]. rewrite bind_get, decide_False, decide_False by assumption.
    unfold get_node at 1. unfold m_bind at 1. rewrite Hcur.
    rewrite decide_True by exact Hfc. rewrite b4_bind_assoc.
    rewrite b4_bind_push_checking. reflexivity.
  - cbn [refresh]. rewrite bind_get, decide_False, decide_False by assumption.
    unfold get_node at 1. unfold m_bind at 1. rewrite Hcur.
    rewrite decide_True by exact Hfc. rewrite b4_bind_assoc.
    rewrite b4_bind_push_checking, b4_bind_assoc.
    erewrite b4_bind_arg; [|erewrite resolve_walk_0_step; [reflexivity|simpl; exact Hcur|exact Hfc|simpl; exact Hsl]].
    rewrite b4_bind_assoc. cbn [update_if_necessary]. reflexivity.
  - cbn [refresh]. rewrite bind_get, decide_False, decide_False by assumption.
    unfold get_node at 1. unfold m_bind at 1. rewrite Hcur.
    rewrite decide_True by exact Hfc. rewrite b4_bind_assoc.
    rewrite b4_bind_push_checking, b4_bind_assoc.
    erewrite b4_bind_arg; [|erewrite resolve_walk_0_step; [reflexivity|simpl; exact Hcur|exact Hfc|simpl; exact Hsl]].
    rewrite b4_bind_assoc. cbn [update_if_necessary]. rewrite b4_bind_assoc.
    unfold get_node at 1. unfold m_bind at 1. simpl. rewrite Hsrc. simpl.
    rewrite Hks, Hstl. rewrite b4_bind_assoc. cbn [refresh_or_pull]. reflexivity.
Qed.

Lemma iter_descend_fwd f stk n s s' :
  iter_descend f stk n s = Some (s', tt) ->
  exists f1 f2, m_bind (refresh f1 n) (fun _ => iter_unwind f2 stk) s = Some (s', tt).
Proof.
  revert stk n s s'. induction f as [|f IH]; intros stk n s s' H.
  - cbn in H. discriminate.
  - cbn [iter_descend] in H. rewrite bind_get in H.
    destruct (single_memo_check_slot s n) as [src|] eqn:Eslot.
    + unfold m_bind, push_checking, m_modify in H. simpl in H.
      destruct (IH _ _ _ _ H) as (f1 & f2 & Hm).
      unfold m_bind in Hm. destruct (refresh f1 src (with_checking (n :: g_checking s) s)) as [[s2 []]|] eqn:Er1; [|discriminate].
      destruct f2 as [|f2']; [cbn in Hm; discriminate|].
      cbn [iter_unwind] in Hm. unfold m_bind in Hm.
      destruct (iter_unwind_one f2' n src s2) as [[s3 []]|] eqn:Eu1; [|discriminate].
      set (m := max f1 f2').
      assert (Er' : refresh m src (with_checking (n :: g_checking s) s) = Some (s2, tt))
        by (eapply (Mrefresh _ (engine_mono_all f1)); [lia|exact Er1]).
      assert (Eu' : refresh_after_src (S (S m)) (S (S m)) (S (S (S m))) n src s2 = Some (s3, tt))
        by (eapply (refresh_after_src_mono f2' _ f2' _ f2' _); [lia|lia|lia|exact Eu1]).
      assert (Href : refresh (S (S (S (S m)))) n s = Some (s3, tt)).
      { erewrite refresh_single_slot_unfold; [|exact Eslot].
        unfold m_bind. rewrite Er'. exact Eu'. }
      exists (S (S (S (S m)))), f2'.
      unfold m_bind. rewrite Href. exact Hm.
    + exists f, f. exact H.
Qed.

Lemma iter_descend_bwd f1 f2 stk n s s' :
  m_bind (refresh f1 n) (fun _ => iter_unwind f2 stk) s = Some (s', tt) ->
  exists f, iter_descend f stk n s = Some (s', tt).
Proof.
  revert f2 stk n s s'. induction f1 as [f1 IH] using lt_wf_ind; intros f2 stk n s s' H.
  destruct (single_memo_check_slot s n) as [src|] eqn:Eslot.
  - destruct f1 as [|[|[|[|f1']]]].
    1,2,3,4: unfold m_bind in H; erewrite (refresh_under_4_none _ n src s) in H; [discriminate|lia|exact Eslot].
    unfold m_bind in H. destruct (refresh (S (S (S (S f1')))) n s) as [[s2 []]|] eqn:Href; [|discriminate].
    erewrite refresh_single_slot_unfold in Href by exact Eslot.
    unfold m_bind in Href. destruct (refresh f1' src (with_checking (n :: g_checking s) s)) as [[sm []]|] eqn:Er'; [|discriminate].
    set (fu := max (S (S (S f1'))) f2).
    assert (Eu1 : iter_unwind_one fu n src sm = Some (s2, tt))
      by (eapply (refresh_after_src_mono (S (S f1')) _ (S (S f1')) _ (S (S (S f1'))) _); [lia|lia|lia|exact Href]).
    assert (Eu2 : iter_unwind fu stk s2 = Some (s', tt))
      by (eapply (iter_unwind_mono f2); [lia|exact H]).
    assert (Hm' : m_bind (refresh f1' src) (fun _ => iter_unwind (S fu) ((n, src) :: stk)) (with_checking (n :: g_checking s) s) = Some (s', tt)).
    { unfold m_bind. rewrite Er'. cbn [iter_unwind]. unfold m_bind. rewrite Eu1. exact Eu2. }
    destruct (IH f1' ltac:(lia) (S fu) ((n, src) :: stk) src (with_checking (n :: g_checking s) s) s' Hm') as [f_desc Hdesc].
    exists (S f_desc). cbn [iter_descend]. rewrite bind_get, Eslot.
    unfold m_bind, push_checking, m_modify. simpl. exact Hdesc.
  - unfold m_bind in H. destruct (refresh f1 n s) as [[s1 []]|] eqn:Er1; [|discriminate].
    set (fm := max f1 f2).
    assert (Er' : refresh fm n s = Some (s1, tt)) by (eapply (Mrefresh _ (engine_mono_all f1)); [lia|exact Er1]).
    assert (Eu' : iter_unwind fm stk s1 = Some (s', tt)) by (eapply (iter_unwind_mono f2); [lia|exact H]).
    exists (S fm). cbn [iter_descend]. rewrite bind_get, Eslot.
    unfold m_bind. rewrite Er'. exact Eu'.
Qed.

(**md
**The explicit-stack iterative `Check`-chain refresh `iter_chain_refresh` produces the exact same states as recursive `refresh`.**

Refines `RefreshChain`: descending down single-memo `Check` edges while pushing `g_checking` and `checkStack` frames and unwinding in LIFO order with the repeat check, `resolve_walk ... 1`, and `recompute` agrees extensionally with `refresh` on every state.

- [n:] target memo node id.
- [s:] initial engine state.
- [s':] resulting engine state.
*)
Theorem iterative_chain_refresh_eq n s s' :
  (exists f, iter_chain_refresh f n s = Some (s', tt)) <->
  (exists f, refresh f n s = Some (s', tt)).
Proof.
  split.
  - intros [f H]. destruct (iter_descend_fwd _ _ _ _ _ H) as (f1 & f2 & Hm).
    unfold m_bind in Hm. destruct (refresh f1 n s) as [[s1 []]|] eqn:Er1; [|discriminate].
    destruct f2 as [|f2']; [cbn in Hm; discriminate|].
    cbn [iter_unwind] in Hm. unfold m_return in Hm. simplify_eq. by exists f1.
  - intros [f H]. apply (iter_descend_bwd f 1 [] n s s').
    unfold m_bind. rewrite H. reflexivity.
Qed.

(** * B5: Queue equivalence and idle-graph fused `S -> E` / `S -> M -> E` execution *)

Definition queue_rel (s1 s2 : state) : Prop :=
  with_queue_head 0 (with_queue [] s1) = with_queue_head 0 (with_queue [] s2) /\
  pending s1 = pending s2 /\
  g_queue_head s1 <= length (g_queue s1) /\
  g_queue_head s2 <= length (g_queue s2).

Lemma queue_rel_refl s :
  g_queue_head s <= length (g_queue s) ->
  queue_rel s s.
Proof. intros Hle. repeat split; assumption. Qed.

Lemma queue_rel_sym s1 s2 : queue_rel s1 s2 -> queue_rel s2 s1.
Proof. intros (Hw & Hp & H1 & H2). repeat split; congruence || assumption. Qed.

Lemma queue_rel_reset_eq s1 s2 :
  queue_rel s1 s2 ->
  with_queue [] (with_queue_head 0 s1) = with_queue [] (with_queue_head 0 s2).
Proof.
  intros (Hw & _). destruct s1, s2; simpl in *; simplify_eq; reflexivity.
Qed.

Lemma queue_rel_drained_eq s1 s2 :
  queue_rel s1 s2 ->
  g_queue s1 = [] -> g_queue_head s1 = 0 ->
  g_queue s2 = [] -> g_queue_head s2 = 0 ->
  s1 = s2.
Proof.
  intros (Hw & _) Eq1 Eh1 Eq2 Eh2.
  destruct s1, s2; simpl in *; subst; simplify_eq; reflexivity.
Qed.

Lemma queue_rel_live_queue s1 s2 :
  queue_rel s1 s2 -> live_queue s1 = live_queue s2.
Proof.
  intros (_ & Hp & H1 & H2). unfold live_queue, pending in *.
  pose proof (f_equal length Hp) as Hlen. rewrite !length_drop in Hlen.
  destruct (bool_decide (g_queue_head s1 < length (g_queue s1))) eqn:E1,
           (bool_decide (g_queue_head s2 < length (g_queue s2))) eqn:E2; try reflexivity.
  - apply bool_decide_eq_true in E1. apply bool_decide_eq_false in E2. lia.
  - apply bool_decide_eq_false in E1. apply bool_decide_eq_true in E2. lia.
Qed.

Lemma queue_rel_head_lookup s1 s2 :
  queue_rel s1 s2 -> g_queue s1 !! g_queue_head s1 = g_queue s2 !! g_queue_head s2.
Proof.
  intros (_ & Hp & _). unfold pending in Hp.
  pose proof (f_equal (fun l => l !! 0) Hp) as H0.
  rewrite !lookup_drop, !Nat.add_0_r in H0. exact H0.
Qed.

Lemma queue_rel_fields s1 s2 :
  queue_rel s1 s2 ->
  g_nodes s1 = g_nodes s2 /\
  g_edges s1 = g_edges s2 /\
  g_owners s1 = g_owners s2 /\
  g_flags s1 = g_flags s2 /\
  g_flushing s1 = g_flushing s2 /\
  g_flush_owed s1 = g_flush_owed s2 /\
  g_batch_depth s1 = g_batch_depth s2 /\
  g_pull_depth s1 = g_pull_depth s2 /\
  g_current s1 = g_current s2 /\
  g_untracked s1 = g_untracked s2 /\
  g_current_owner s1 = g_current_owner s2 /\
  g_running s1 = g_running s2 /\
  g_checking s1 = g_checking s2 /\
  live_queue s1 = live_queue s2 /\
  outside_bodies s1 = outside_bodies s2 /\
  deferring_b1 s1 = deferring_b1 s2 /\
  g_queue s1 !! g_queue_head s1 = g_queue s2 !! g_queue_head s2.
Proof.
  intros Hrel.
  pose proof (queue_rel_live_queue _ _ Hrel) as Hlq.
  pose proof (queue_rel_head_lookup _ _ Hrel) as Hhd.
  destruct Hrel as (Hw & _).
  destruct s1, s2; simpl in *; simplify_eq.
  unfold outside_bodies, deferring_b1 in *; simpl in *.
  repeat split; assumption || reflexivity.
Qed.

Lemma queue_rel_pop s1 s2 n :
  queue_rel s1 s2 ->
  g_queue s1 !! g_queue_head s1 = Some n ->
  queue_rel (with_queue_head (S (g_queue_head s1)) s1)
            (with_queue_head (S (g_queue_head s2)) s2).
Proof.
  intros Hrel Hn1.
  pose proof (queue_rel_head_lookup _ _ Hrel) as Hhd.
  rewrite Hn1 in Hhd. symmetry in Hhd.
  destruct Hrel as (Hw & Hp & H1 & H2).
  pose proof (lookup_lt_Some _ _ _ Hn1) as Hlt1.
  pose proof (lookup_lt_Some _ _ _ Hhd) as Hlt2.
  unfold queue_rel, pending in *. simpl in *.
  repeat split; [destruct s1, s2; simpl in *; simplify_eq; reflexivity| |lia|lia].
  rewrite (drop_S _ n (g_queue_head s1) Hn1) in Hp.
  rewrite (drop_S _ n (g_queue_head s2) Hhd) in Hp.
  by inversion Hp.
Qed.

Lemma queue_rel_enqueue n s1 s2 :
  queue_rel s1 s2 -> queue_rel (enqueue n s1) (enqueue n s2).
Proof.
  intros (Hw & Hp & H1 & H2). unfold queue_rel, pending, enqueue in *; simpl in *.
  repeat split; [destruct s1, s2; simpl in *; simplify_eq; reflexivity|..].
  - rewrite !drop_app_le by assumption. congruence.
  - rewrite length_app; simpl; lia.
  - rewrite length_app; simpl; lia.
Qed.

Lemma queue_rel_set_node n d s1 s2 :
  queue_rel s1 s2 -> queue_rel (set_node n d s1) (set_node n d s2).
Proof.
  intros (Hw & Hp & H1 & H2). unfold pending, set_node in *.
  destruct s1, s2; simpl in *; simplify_eq. by repeat split.
Qed.

Lemma queue_rel_effect_marked f n d s1 s2 :
  queue_rel s1 s2 -> queue_rel (effect_marked f n d s1) (effect_marked f n d s2).
Proof.
  intros Hrel. unfold effect_marked.
  destruct (n_disposed d || n_in_cleanup d); [exact Hrel|].
  destruct (n_queued d); [by apply queue_rel_set_node|apply queue_rel_enqueue, queue_rel_set_node; exact Hrel].
Qed.

Lemma qrel_walk_marks m1 m2 src k s1 s2 s1' :
  (forall c x y x', queue_rel x y -> m1 c x = Some x' -> exists y', m2 c y = Some y' /\ queue_rel x' y') ->
  queue_rel s1 s2 ->
  walk_marks m1 src k s1 = Some s1' ->
  exists s2', walk_marks m2 src k s2 = Some s2' /\ queue_rel s1' s2'.
Proof.
  intros Hm. revert s1 s2 s1'. induction k as [|k IH]; intros s1 s2 s1' Hrel H.
  - cbn in H |- *. simplify_eq. by exists s2.
  - cbn [walk_marks] in H |- *.
    destruct (queue_rel_fields _ _ Hrel) as (_ & He & _).
    rewrite <- He.
    destruct (decide (length (observer_items (observers_of (g_edges s1) src)) <= k));
      [by eapply IH|].
    destruct (observer_items (observers_of (g_edges s1) src) !! k) as [[c slot]|]; [|discriminate].
    unfold m_bind, option_bind in *.
    destruct (m1 c s1) as [sx1|] eqn:Em1; [|discriminate].
    destruct (Hm c s1 s2 sx1 Hrel Em1) as (sx2 & Em2 & Hrel1).
    rewrite Em2. simpl in *. by eapply IH.
Qed.

Lemma qrel_mark_check f n s1 s2 s1' :
  queue_rel s1 s2 ->
  mark_check f n s1 = Some s1' ->
  exists s2', mark_check f n s2 = Some s2' /\ queue_rel s1' s2'.
Proof.
  revert n s1 s2 s1'. induction f as [|f IH]; intros n s1 s2 s1' Hrel H.
  - cbn in H. discriminate.
  - cbn [mark_check] in H |- *.
    destruct (queue_rel_fields _ _ Hrel) as (Hn & He & Ho & Hfl & _).
    rewrite <- Hn.
    destruct (g_nodes s1 !! n) as [d|]; [|simplify_eq; by exists s2].
    destruct (n_kind d); [simplify_eq; by exists s2| |].
    + destruct (decide (n_freshness d = Clean)).
      * unfold notify_with in H |- *. simpl in H |- *. rewrite <- He.
        eapply qrel_walk_marks; [intros c x y x' Hxy Hm; by eapply IH| |exact H].
        by apply queue_rel_set_node.
      * assert (Hann : announce_due s1 d = announce_due s2 d)
          by (unfold announce_due; by rewrite Hfl).
        rewrite <- Hann. destruct (announce_due s1 d).
        -- unfold notify_with in H |- *. simpl in H |- *. rewrite <- He.
           eapply qrel_walk_marks; [intros c x y x' Hxy Hm; by eapply IH| |exact H].
           by apply queue_rel_set_node.
        -- simplify_eq. by exists s2.
    + simplify_eq. eexists; split; [reflexivity|].
      by apply queue_rel_effect_marked.
Qed.

Lemma qrel_mark_dirty f n s1 s2 s1' :
  queue_rel s1 s2 ->
  mark_dirty f n s1 = Some s1' ->
  exists s2', mark_dirty f n s2 = Some s2' /\ queue_rel s1' s2'.
Proof.
  intros Hrel H. destruct f as [|f]; [cbn in H; discriminate|].
  cbn [mark_dirty] in H |- *.
  destruct (queue_rel_fields _ _ Hrel) as (Hn & He & Ho & Hfl & _).
  rewrite <- Hn.
  destruct (g_nodes s1 !! n) as [d|]; [|simplify_eq; by exists s2].
  destruct (n_kind d); [simplify_eq; by exists s2| |].
  - destruct (decide (n_freshness d = Clean)).
    + unfold notify_with in H |- *. simpl in H |- *. rewrite <- He.
      eapply qrel_walk_marks; [intros c x y x' Hxy Hm; by eapply qrel_mark_check| |exact H].
      by apply queue_rel_set_node.
    + assert (Hann : announce_due s1 d = announce_due s2 d) by (unfold announce_due; by rewrite Hfl).
      rewrite <- Hann. destruct (announce_due s1 d).
      * unfold notify_with in H |- *. simpl in H |- *. rewrite <- He.
        eapply qrel_walk_marks; [intros c x y x' Hxy Hm; by eapply qrel_mark_check| |exact H].
        by apply queue_rel_set_node.
      * destruct (decide (n_freshness d = Dirty)); simplify_eq; [by exists s2|].
        eexists; split; [reflexivity|by apply queue_rel_set_node].
  - simplify_eq. eexists; split; [reflexivity|by apply queue_rel_effect_marked].
Qed.

Lemma qrel_notify_dirty f n s1 s2 s1' :
  queue_rel s1 s2 ->
  notify_dirty f n s1 = Some s1' ->
  exists s2', notify_dirty f n s2 = Some s2' /\ queue_rel s1' s2'.
Proof.
  intros Hrel H. unfold notify_dirty, notify_with in H |- *.
  destruct (queue_rel_fields _ _ Hrel) as (_ & He & _).
  rewrite <- He.
  eapply qrel_walk_marks; [intros c x y x' Hxy Hm; by eapply qrel_mark_dirty|exact Hrel|exact H].
Qed.

Lemma qrel_notify_dirty_except f n r s1 s2 s1' :
  queue_rel s1 s2 ->
  notify_dirty_except f n r s1 = Some s1' ->
  exists s2', notify_dirty_except f n r s2 = Some s2' /\ queue_rel s1' s2'.
Proof.
  intros Hrel H. unfold notify_dirty_except, notify_with in H |- *.
  destruct (queue_rel_fields _ _ Hrel) as (_ & He & _).
  rewrite <- He.
  eapply qrel_walk_marks; [|exact Hrel|exact H].
  intros c x y x' Hxy Hm. unfold mark_except in Hm |- *.
  destruct (queue_rel_fields _ _ Hxy) as (_ & Hey & _).
  rewrite <- Hey.
  destruct (decide _); [simplify_eq; by exists y|by eapply qrel_mark_dirty].
Qed.

Lemma qrel_bind_of {A B} {m : M A} {k : A -> M B} {s1 s2 s1' : state} {b : B}
    (Hb : m_bind m k s1 = Some (s1', b)) (Hrel : queue_rel s1 s2) :
  (forall sb1 a, m s1 = Some (sb1, a) -> queue_rel s1 s2 ->
    exists sb2, m s2 = Some (sb2, a) /\ queue_rel sb1 sb2) ->
  (forall a sb1 sb2, queue_rel sb1 sb2 -> k a sb1 = Some (s1', b) ->
    exists s2', k a sb2 = Some (s2', b) /\ queue_rel s1' s2') ->
  exists s2', m_bind m k s2 = Some (s2', b) /\ queue_rel s1' s2'.
Proof.
  intros Hm Hk. unfold m_bind in *.
  destruct (m s1) as [[sb1 a]|] eqn:Em1; [|discriminate].
  destruct (Hm _ _ eq_refl Hrel) as (sb2 & Em2 & Hrel1).
  rewrite Em2. eapply Hk; [exact Hrel1|exact Hb].
Qed.

Lemma qrel_m_return {A} (a : A) s1 s2 s1' r :
  m_return a s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', m_return a s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof. intros H Hrel. unfold m_return in *. simplify_eq. by exists s2. Qed.

Lemma qrel_m_modify upd s1 s2 s1' r :
  m_modify upd s1 = Some (s1', r) -> queue_rel s1 s2 ->
  (forall x y, queue_rel x y -> queue_rel (upd x) (upd y)) ->
  exists s2', m_modify upd s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel Hupd. unfold m_modify in *. simplify_eq.
  eexists; split; [reflexivity|by apply Hupd].
Qed.

Lemma qrel_get_node n s1 s2 s1' d :
  get_node n s1 = Some (s1', d) -> queue_rel s1 s2 ->
  exists s2', get_node n s2 = Some (s2', d) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. destruct (queue_rel_fields _ _ Hrel) as (Hn & _).
  unfold get_node in *. rewrite <- Hn.
  destruct (g_nodes s1 !! n); [simplify_eq; by exists s2|discriminate].
Qed.

Lemma qrel_modify_node n u s1 s2 s1' r :
  modify_node n u s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', modify_node n u s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. destruct (queue_rel_fields _ _ Hrel) as (Hn & _).
  unfold modify_node in *. rewrite <- Hn.
  destruct (g_nodes s1 !! n); [|discriminate].
  simplify_eq. eexists; split; [reflexivity|by apply queue_rel_set_node].
Qed.

Lemma qrel_get_owner o s1 s2 s1' x :
  get_owner o s1 = Some (s1', x) -> queue_rel s1 s2 ->
  exists s2', get_owner o s2 = Some (s2', x) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. destruct (queue_rel_fields _ _ Hrel) as (_ & _ & Ho & _).
  unfold get_owner in *. rewrite <- Ho.
  destruct (g_owners s1 !! o); [simplify_eq; by exists s2|discriminate].
Qed.

Local Ltac qrel_upd_tac :=
  let x := fresh "x" in let y := fresh "y" in let Hxy := fresh "Hxy" in
  intros x y Hxy; destruct Hxy as (?&?&?&?);
  unfold queue_rel, pending,
    set_node, ghost_read, ghost_run_start, ghost_effect_start, ghost_publish,
    ghost_run_end, ghost_dirty_cleared, ghost_resolved, ghost_registered,
    run_end_unnoticed, slot_holds, add_if in *;
  destruct x, y; simpl in *; simplify_eq;
  repeat match goal with
  | |- context [match ?m !! ?k with _ => _ end] => destruct (m !! k)
  end;
  simpl in *;
  repeat split; try assumption; try reflexivity.

Lemma qrel_current_owner s1 s2 s1' r :
  current_owner s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', current_owner s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. destruct Hrel as (Hw & Hp & H1 & H2).
  unfold current_owner, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  destruct g_current_owner0, g_current0; simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (g_nodes0 !! n); [|discriminate].
  destruct (n_scope n0); simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (match n_kind n0, n_mode n0 with KMemo, Pure => true | _, _ => false end); simplify_eq;
    (eexists; split; [reflexivity|by repeat split]).
Qed.

Lemma qrel_next_id s1 s2 s1' r :
  next_id s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', next_id s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H (Hw & Hp & H1 & H2). unfold next_id, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  eexists; split; [reflexivity|by repeat split].
Qed.

Lemma qrel_note_host_read n o s1 s2 s1' r :
  note_host_read n o s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', note_host_read n o s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H (Hw & Hp & H1 & H2). unfold note_host_read, emit, m_modify, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  destruct g_current0, g_current_owner0; simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (decide _); simplify_eq; (eexists; split; [reflexivity|by repeat split]).
Qed.

Lemma qrel_log_label s1 s2 s1' r :
  log_label s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', log_label s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H (Hw & Hp & H1 & H2). unfold log_label, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  destruct g_current0, g_current_owner0; simplify_eq; (eexists; split; [reflexivity|by repeat split]).
Qed.

Lemma qrel_track n s1 s2 s1' r :
  track n s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', track n s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H (Hw & Hp & H1 & H2). unfold track, live_current, ghost_read, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  destruct g_current0; simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (g_nodes0 !! n0); simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (n_disposed n1); simplify_eq; try (eexists; split; [reflexivity|by repeat split]).
  destruct (sources_add n0 n g_edges0); [|discriminate]. simplify_eq.
  destruct (g_nodes0 !! n); (eexists; split; [reflexivity|by repeat split]).
Qed.

Lemma qrel_mark_reader f s1 s2 s1' r :
  mark_reader f s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', mark_reader f s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel.
  destruct (queue_rel_fields _ _ Hrel) as (_ & _ & _ & Hfl & _ & _ & _ & _ & Hcur & _).
  unfold mark_reader in *. rewrite <- Hcur, <- Hfl.
  destruct (g_current s1), (f_reader_mark (g_flags s1)); simplify_eq; try (by exists s2).
  destruct (mark_check f n s1) eqn:Em; simplify_eq.
  destruct (qrel_mark_check _ _ _ _ _ Hrel Em) as (s2' & Em2 & Hrel').
  rewrite Em2. by exists s2'.
Qed.

Lemma qrel_announce n s1 s2 s1' r :
  announce n s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', announce n s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel.
  destruct (queue_rel_fields _ _ Hrel) as (_ & _ & _ & Hfl & _).
  unfold announce in *. rewrite <- Hfl.
  destruct (f_unannounced (g_flags s1)); [by eapply qrel_modify_node|simplify_eq; by exists s2].
Qed.

Lemma qrel_unlink l s1 s2 s1' r :
  unlink l s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', unlink l s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. unfold unlink in *.
  eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; by eapply qrel_get_owner|].
  intros x sb1 sb2 Hxy Hk.
  destruct (decide _); [eapply qrel_m_modify; [exact Hk|exact Hxy|qrel_upd_tac]|by eapply qrel_m_return].
Qed.

Lemma qrel_record_error o y s1 s2 s1' r :
  record_error o y s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', record_error o y s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel. unfold record_error in *.
  eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; by eapply qrel_get_owner|].
  intros x sb1 sb2 Hxy Hk. destruct (o_sink x).
  - eapply (qrel_bind_of Hk Hxy); [intros ?? Hm Hxy'; by eapply qrel_get_owner|].
    intros y0 sb1' sb2' Hxy' Hk'. eapply qrel_m_modify; [exact Hk'|exact Hxy'|qrel_upd_tac].
  - eapply qrel_m_modify; [exact Hk|exact Hxy|qrel_upd_tac].
Qed.

Lemma qrel_edges_opt u s1 s2 s1' r :
  edges_opt u s1 = Some (s1', r) -> queue_rel s1 s2 ->
  exists s2', edges_opt u s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H (Hw & Hp & H1 & H2). unfold edges_opt, pending in *.
  destruct s1, s2; simpl in *; simplify_eq.
  destruct (u g_edges0); [|discriminate]. simplify_eq.
  eexists; split; [reflexivity|by repeat split].
Qed.

Lemma qrel_hosted {A} n (b : M A) s1 s2 s1' r :
  hosted n b s1 = Some (s1', r) -> queue_rel s1 s2 ->
  (forall x1 x2 x1' a, b x1 = Some (x1', a) -> queue_rel x1 x2 ->
    exists x2', b x2 = Some (x2', a) /\ queue_rel x1' x2') ->
  exists s2', hosted n b s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel Hb. unfold hosted in *.
  destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hcur&_&Hcown&_).
  rewrite <- Hcur, <- Hcown.
  eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; eapply qrel_m_modify; [exact Hm|exact Hxy|qrel_upd_tac]|].
  intros [] sb1 sb2 Hxy Hk.
  eapply (qrel_bind_of Hk Hxy); [intros ?? Hm' Hxy'; by eapply Hb|].
  intros a sb1' sb2' Hxy' Hk'.
  eapply (qrel_bind_of Hk' Hxy'); [intros ?? Hm Hxy''; eapply qrel_m_modify; [exact Hm|exact Hxy''|qrel_upd_tac]|].
  intros [] sb1'' sb2'' Hxy'' Hk''. by eapply qrel_m_return.
Qed.

Lemma qrel_untracked {A} (b : M A) s1 s2 s1' r :
  untracked b s1 = Some (s1', r) -> queue_rel s1 s2 ->
  (forall x1 x2 x1' a, b x1 = Some (x1', a) -> queue_rel x1 x2 ->
    exists x2', b x2 = Some (x2', a) /\ queue_rel x1' x2') ->
  exists s2', untracked b s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel Hb. unfold untracked in *.
  destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hcur&Hunt&_).
  rewrite <- Hcur, <- Hunt.
  eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; eapply qrel_m_modify; [exact Hm|exact Hxy|qrel_upd_tac]|].
  intros [] sb1 sb2 Hxy Hk.
  eapply (qrel_bind_of Hk Hxy); [intros ?? Hm' Hxy'; by eapply Hb|].
  intros a sb1' sb2' Hxy' Hk'.
  eapply (qrel_bind_of Hk' Hxy'); [intros ?? Hm Hxy''; eapply qrel_m_modify; [exact Hm|exact Hxy''|qrel_upd_tac]|].
  intros [] sb1'' sb2'' Hxy'' Hk''. by eapply qrel_m_return.
Qed.

Lemma qrel_detached o b s1 s2 s1' r :
  detached o b s1 = Some (s1', r) -> queue_rel s1 s2 ->
  (forall x1 x2 x1' a, b x1 = Some (x1', a) -> queue_rel x1 x2 ->
    exists x2', b x2 = Some (x2', a) /\ queue_rel x1' x2') ->
  exists s2', detached o b s2 = Some (s2', r) /\ queue_rel s1' s2'.
Proof.
  intros H Hrel Hb. unfold detached in *.
  destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&Hcur&_&Hcown&_).
  rewrite <- Hcur, <- Hcown.
  eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; eapply qrel_m_modify; [exact Hm|exact Hxy|qrel_upd_tac]|].
  intros [] sb1 sb2 Hxy Hk.
  eapply (qrel_bind_of Hk Hxy); [intros ?? Hm' Hxy'; by eapply Hb|].
  intros [] sb1' sb2' Hxy' Hk'.
  eapply qrel_m_modify; [exact Hk'|exact Hxy'|qrel_upd_tac].
Qed.

Local Ltac qrel_pure_tac :=
  try unfold add_node, put_owner, emit, edges, push_running, pop_running,
    push_checking, pop_checking, m_step in *;
  first
    [ by eapply qrel_m_return
    | by eapply qrel_get_node
    | by eapply qrel_modify_node
    | by eapply qrel_get_owner
    | by eapply qrel_current_owner
    | by eapply qrel_next_id
    | by eapply qrel_note_host_read
    | by eapply qrel_log_label
    | by eapply qrel_track
    | by eapply qrel_mark_reader
    | by eapply qrel_announce
    | by eapply qrel_unlink
    | by eapply qrel_record_error
    | by eapply qrel_edges_opt
    | by (eapply qrel_m_modify; [eassumption|eassumption|qrel_upd_tac])
    ].

Record b1_qrel_at (f : nat) : Prop := {
  B1Qexec : forall e c s1 s2 s1' r, b1_exec f e c s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_exec f e c s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qread : forall n b s1 s2 s1' r, b1_read f n b s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_read f n b s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qread_node : forall n s1 s2 s1' r, b1_read_node f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_read_node f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qupdate : forall n s1 s2 s1' r, b1_update_if_necessary f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_update_if_necessary f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrop : forall n s1 s2 s1' r, b1_refresh_or_pull f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_refresh_or_pull f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qpull : forall n s1 s2 s1' r, b1_pull f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_pull f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrefresh : forall n s1 s2 s1' r, b1_refresh f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_refresh f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qwalk : forall n i s1 s2 s1' r, b1_resolve_walk f n i s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_resolve_walk f n i s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrecompute : forall n s1 s2 s1' r, b1_recompute f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_recompute f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrun_memo : forall n s1 s2 s1' r, b1_run_memo f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_run_memo f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qexecute : forall n s1 s2 s1' r, b1_execute_effect f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_execute_effect f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrun_effect : forall n s1 s2 s1' r, b1_run_effect f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_run_effect f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qbody : forall n s1 s2 s1' r, b1_run_effect_body f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_run_effect_body f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qdischarge : forall o s1 s2 s1' r, b1_discharge f o s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_discharge f o s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qretire : forall o s1 s2 s1' r, b1_retire f o s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_retire f o s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qdispose_scope : forall o s1 s2 s1' r, b1_dispose_scope f o s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_dispose_scope f o s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qteardown : forall o ls cs s1 s2 s1' r, b1_teardown f o ls cs s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_teardown f o ls cs s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qcleanup : forall o b e s1 s2 s1' r, b1_run_cleanup f o b e s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_run_cleanup f o b e s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrelease : forall n s1 s2 s1' r, b1_release f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_release f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qdispose : forall n s1 s2 s1' r, b1_dispose_node f n s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_dispose_node f n s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qattach : forall n o s1 s2 s1' r, b1_attach f n o s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_attach f n o s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qwrite : forall n v s1 s2 s1' r, b1_write_signal f n v s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_write_signal f n v s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qrequest : forall s1 s2 s1' r, b1_request_flush f s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_request_flush f s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qsettle : forall s1 s2 s1' r, b1_settle_flush f s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_settle_flush f s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qflush : forall s1 s2 s1' r, b1_run_flush f s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_run_flush f s2 = Some (s2', r) /\ queue_rel s1' s2';
  B1Qdrain : forall s1 s2 s1' r, b1_drain f s1 = Some (s1', r) -> queue_rel s1 s2 ->
    exists s2', b1_drain f s2 = Some (s2', r) /\ queue_rel s1' s2'
}.

Lemma b1_qrel_all f : b1_qrel_at f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - split.
    + intros e c s1 s2 s1' r H Hrel. cbn [b1_exec] in H |- *.
      destruct c; repeat match goal with
      | H0 : context [on_node] |- _ => unfold on_node in H0 |- *
      | H0 : context [on_value] |- _ => unfold on_value in H0 |- *
      | H0 : context [match eval_expr ?e ?p ?x with _ => _ end] |- _ => destruct (eval_expr e p x) as [[|]|]; try discriminate
      | H0 : (if decide _ then _ else stuck) _ = Some _ |- _ => destruct (decide _); try discriminate
      end; try (eapply qrel_m_return; [exact H|exact Hrel]); try (simplify_eq; by exists s2).
      * by eapply (B1Qexec _ IH).
      * destruct (decide _); by eapply (B1Qexec _ IH).
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qread _ IH)|intros o sb1 sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] sb1' sb2' Hrel2 Hk2].
        destruct o; [by eapply (B1Qexec _ IH)|by eapply qrel_m_return].
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qread _ IH)|intros o sb1 sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] sb1' sb2' Hrel2 Hk2].
        destruct o; by eapply (B1Qexec _ IH).
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qread _ IH)|intros o sb1 sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] sb1' sb2' Hrel2 Hk2].
        destruct o; [by eapply (B1Qexec _ IH)|by eapply qrel_m_return].
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qwrite _ IH)|intros [] sb1 sb2 Hrel1 Hk; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros ?? sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros ?? sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [o|] ? sb2'' Hrel3 Hk3; [|by eapply qrel_m_return]].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; by eapply (B1Qattach _ IH)|intros [] ? sb2''' Hrel4 Hk4; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros ?? sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [o|] ? sb2'' Hrel3 Hk3; [|by eapply qrel_m_return]].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; by eapply (B1Qattach _ IH)|intros [] ? sb2''' Hrel4 Hk4].
        eapply (qrel_bind_of Hk4 Hrel4); [intros ?? Hm Hxy; unfold m_step in *; destruct (mark_dirty f _ _) eqn:Em; simplify_eq; destruct (qrel_mark_dirty _ _ _ _ _ Hxy Em) as (y' & Em2 & Hxy'); rewrite Em2; by exists y'|intros [] ? sb2'''' Hrel5 Hk5].
        eapply (qrel_bind_of Hk5 Hrel5); [intros ????; by eapply (B1Qrequest _ IH)|intros [] ? sb2''''' Hrel6 Hk6; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [o|] ? sb2 Hrel1 Hk; [|by eapply qrel_m_return]].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros x ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
        destruct (o_disposed x).
        -- eapply (qrel_bind_of Hk3 Hrel3); [|intros [] ? sb2''' Hrel4 Hk4; by eapply (B1Qexec _ IH)].
           intros s4 [] Hm Hxy. eapply (qrel_bind_of Hm Hxy); [|intros [] ? sb2'''' Hrel5 Hreq; by eapply (B1Qrequest _ IH)].
           intros s5 [] Hd Hxy'. by eapply qrel_detached; [exact Hd|exact Hxy'|intros ????; by eapply (B1Qcleanup _ IH)].
        -- eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
        destruct (n_kind d); [discriminate| |];
          (eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qdispose _ IH)|intros [] ? sb2' Hrel2 Hk2];
           eapply (qrel_bind_of Hk2 Hrel2); [intros ????; destruct (n_disposed d); qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3; by eapply (B1Qexec _ IH)]).
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [] ? sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qexec _ IH)|intros r0 ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
        destruct r0 as [|]; [|by eapply qrel_m_return].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; by eapply (B1Qrequest _ IH)|intros [] ? sb2''' Hrel4 Hk4; by eapply (B1Qexec _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros ?? sb2 Hrel1 Hk].
        eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; by eapply (B1Qexec _ IH)].
    + intros n b s1 s2 s1' r H Hrel. cbn [b1_read] in H |- *.
      destruct b; [by eapply (B1Qread_node _ IH)|eapply qrel_untracked; [exact H|exact Hrel|intros ????; by eapply (B1Qread_node _ IH)]].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_read_node] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_kind d); [| |discriminate].
      * eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; by eapply qrel_m_return].
      * eapply (qrel_bind_of Hk Hrel1); [|intros d' ? sb2' Hrel2 Hk2; destruct (n_status d'); by eapply qrel_m_return].
        intros sb1' d' Hm Hxy. destruct (stale_live d).
        -- eapply (qrel_bind_of Hm Hxy); [intros ????; by eapply (B1Qrop _ IH)|intros d0 ? sb2' Hrel2 Hk2].
           eapply (qrel_bind_of Hk2 Hrel2); [|intros [] ? sb2'' Hrel3 Hk3; by eapply qrel_m_return].
           intros sb1'' [] Hm2 Hxy'.
           eapply (qrel_bind_of Hm2 Hxy'); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
           eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros d'' ? sb2''' Hrel4 Hk4].
           destruct (stale_live d''); [qrel_pure_tac|by eapply qrel_m_return].
        -- eapply (qrel_bind_of Hm Hxy); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; qrel_pure_tac].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_update_if_necessary] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_kind d); [by eapply qrel_m_return| |by eapply qrel_m_return].
      destruct (stale_live d); [|by eapply qrel_m_return].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qrop _ IH)|intros ?? sb2' Hrel2 Hk2; by eapply qrel_m_return].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_refresh_or_pull] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hdef&_).
      rewrite <- Hdef.
      destruct (deferring_b1 s1); [|by eapply (B1Qpull _ IH)].
      eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qrefresh _ IH)|intros [] ? sb2 Hrel1 Hk; qrel_pure_tac].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_pull] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [] ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qrefresh _ IH)|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros d ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4].
      rewrite !bind_get in Hk4 |- *.
      destruct (queue_rel_fields _ _ Hrel4) as (_&_&_&_&_&Hfo&_&Hpd&_).
      rewrite <- Hfo, <- Hpd.
      eapply (qrel_bind_of Hk4 Hrel4); [|intros [] ? sb2'''' Hrel5 Hk5; by eapply qrel_m_return].
      intros s5 ? Hm Hxy. destruct (bool_decide _ && _); [by eapply (B1Qsettle _ IH)|by eapply qrel_m_return].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_refresh] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
      rewrite <- Hchk, <- Hrun.
      destruct (decide (n ∈ g_checking s1)).
      { unfold m_step in *. destruct (mark_dirty f n s1) eqn:Em; simplify_eq.
        destruct (qrel_mark_dirty _ _ _ _ _ Hrel Em) as (y' & Em2 & Hxy'). rewrite Em2. by exists y'. }
      destruct (decide (n ∈ g_running s1)); [by eapply qrel_m_return|].
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [|intros [] ? sb2' Hrel2 Hk2; eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros d' ? sb2'' Hrel3 Hk3; destruct (decide (n_freshness d' = Dirty)); [by eapply (B1Qrecompute _ IH)|by eapply qrel_m_return]]].
      intros s2' [] Hm Hxy. destruct (decide (n_freshness d = Check)); [|by eapply qrel_m_return].
      eapply (qrel_bind_of Hm Hxy); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; by eapply (B1Qwalk _ IH)|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4].
      eapply (qrel_bind_of Hk4 Hrel4); [intros ????; qrel_pure_tac|intros d' ? sb2'''' Hrel5 Hk5].
      destruct (decide (n_freshness d' = Check)); [|by eapply qrel_m_return].
      eapply (qrel_bind_of Hk5 Hrel5); [intros ????; qrel_pure_tac|intros [] ? sb2''''' Hrel6 Hk6; qrel_pure_tac].
    + intros n i s1 s2 s1' r H Hrel. cbn [b1_resolve_walk] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d sb1 sb2 Hrel1 Hk].
      rewrite !bind_get in Hk |- *.
      destruct (queue_rel_fields _ _ Hrel1) as (Hn1&He1&_). rewrite <- He1.
      destruct (decide (n_freshness d = Check)); [|by eapply qrel_m_return].
      destruct (source_slots (sources_of (g_edges sb1) n) !! i) as [[src _]|].
      * eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qupdate _ IH)|intros [] ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros d' ? sb2'' Hrel3 Hk3].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros d_src ? sb2''' Hrel4 Hk4].
        rewrite !bind_get in Hk4 |- *.
        destruct (queue_rel_fields _ _ Hrel4) as (_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
        rewrite <- Hrun, <- Hchk.
        eapply (qrel_bind_of Hk4 Hrel4); [|intros [] ? sb2'''' Hrel5 Hk5; by eapply (B1Qwalk _ IH)].
        intros s5 [] Hm Hxy. destruct (decide _); [|by eapply qrel_m_return].
        unfold m_step in *. destruct (mark_dirty f n _) eqn:Em; simplify_eq.
        destruct (qrel_mark_dirty _ _ _ _ _ Hxy Em) as (y' & Em2 & Hxy'). rewrite Em2. by exists y'.
      * assert (Hsh : slot_holds stale_live sb1 n = slot_holds stale_live sb2 n)
          by (unfold slot_holds; by rewrite Hn1, He1).
        rewrite <- Hsh. destruct (slot_holds stale_live sb1 n); [by eapply (B1Qwalk _ IH)|by eapply qrel_m_return].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_recompute] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_scope d) as [o|]; [|by eapply (B1Qrun_memo _ IH)].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; by eapply (B1Qdischarge _ IH)|intros [] ? sb2''' Hrel4 Hk4].
      eapply (qrel_bind_of Hk4 Hrel4); [intros ????; qrel_pure_tac|intros d' ? sb2'''' Hrel5 Hk5].
      destruct (n_disposed d').
      * destruct (decide (n_freshness d' = Clean)); [qrel_pure_tac|by eapply qrel_m_return].
      * destruct (decide (n_runs d' = n_runs d)); [by eapply (B1Qrun_memo _ IH)|].
        destruct (decide (n_freshness d' = Dirty)); [by eapply (B1Qrecompute _ IH)|by eapply qrel_m_return].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_run_memo] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4].
      eapply (qrel_bind_of Hk4 Hrel4); [intros ????; qrel_pure_tac|intros [] ? sb2'''' Hrel5 Hk5].
      eapply (qrel_bind_of Hk5 Hrel5); [intros ?? Hm Hxy; eapply qrel_hosted; [exact Hm|exact Hxy|intros ????; by eapply (B1Qexec _ IH)]|intros r0 ? sb2''''' Hrel6 Hk6].
      eapply (qrel_bind_of Hk6 Hrel6); [intros ????; qrel_pure_tac|intros d1 ? sb2_7 Hrel7 Hk7].
      eapply (qrel_bind_of Hk7 Hrel7); [intros ????; destruct (if n_violated d1 then Fail pure_scope_failure else r0); qrel_pure_tac|intros [] ? sb2_8 Hrel8 Hk8].
      eapply (qrel_bind_of Hk8 Hrel8); [intros ????; qrel_pure_tac|intros [] ? sb2_9 Hrel9 Hk9].
      eapply (qrel_bind_of Hk9 Hrel9); [intros ????; qrel_pure_tac|intros [] ? sb2_10 Hrel10 Hk10].
      eapply (qrel_bind_of Hk10 Hrel10); [intros ????; destruct (if n_violated d1 then Fail pure_scope_failure else r0); qrel_pure_tac|intros [] ? sb2_11 Hrel11 Hk11].
      eapply (qrel_bind_of Hk11 Hrel11); [intros ????; qrel_pure_tac|intros d2 ? sb2_12 Hrel12 Hk12].
      eapply (qrel_bind_of Hk12 Hrel12); [intros ?? Hm Hxy; destruct (decide _); [by eapply qrel_m_return|destruct (n_comparer d2); [by eapply qrel_m_return|by eapply qrel_m_return|eapply (qrel_bind_of Hm Hxy); [intros ????; qrel_pure_tac|intros [] ? sb2_x Hxy' Hk'; by eapply qrel_m_return]]]|intros moved ? sb2_13 Hrel13 Hk13].
      rewrite !bind_get in Hk13 |- *.
      destruct (queue_rel_fields _ _ Hrel13) as (_&_&_&_&_&_&_&_&Hcur&_).
      rewrite <- Hcur.
      eapply (qrel_bind_of Hk13 Hrel13); [intros ?? Hm Hxy; eapply qrel_m_modify; [exact Hm|exact Hxy|qrel_upd_tac]|intros [] ? sb2_14 Hrel14 Hk14].
      eapply (qrel_bind_of Hk14 Hrel14); [|intros [] ? sb2_15 Hrel15 Hk15; eapply (qrel_bind_of Hk15 Hrel15); [intros ????; qrel_pure_tac|intros d3 ? sb2_16 Hrel16 Hk16; destruct (decide (n_freshness d3 = Clean)); [by eapply qrel_m_return|qrel_pure_tac]]].
      intros s15 [] Hm Hxy. destruct moved; [|by eapply qrel_m_return].
      eapply (qrel_bind_of Hm Hxy); [intros ?? Hm' Hxy'; unfold m_step in *; destruct (notify_dirty_except f n _ _) eqn:Em; simplify_eq; destruct (qrel_notify_dirty_except _ _ _ _ _ _ Hxy' Em) as (y' & Em2 & Hxy''); rewrite Em2; by exists y'|].
      intros [] sb16 sb2_16 Hrel16 Hk16. rewrite !bind_get in Hk16 |- *.
      destruct (queue_rel_fields _ _ Hrel16) as (_&_&_&_&Hfl&_&Hbd&_&_&_&_&_&_&Hlq&_).
      rewrite <- Hbd, <- Hfl, <- Hlq.
      destruct (bool_decide _ && _ && _); [qrel_pure_tac|by eapply qrel_m_return].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_execute_effect] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_disposed d); [qrel_pure_tac|].
      eapply (qrel_bind_of Hk Hrel1); [|intros [] ? sb2' Hrel2 Hk2].
      * intros s2' [] Hm Hxy. destruct (decide (n_freshness d = Check)); [|by eapply qrel_m_return].
        eapply (qrel_bind_of Hm Hxy); [intros ????; by eapply (B1Qwalk _ IH)|intros [] ? sb2' Hrel2 Hk2].
        eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros d' ? sb2'' Hrel3 Hk3].
        destruct (decide (n_freshness d' = Check)); [|by eapply qrel_m_return].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4; qrel_pure_tac].
      * eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros d' ? sb2'' Hrel3 Hk3].
        eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4; destruct (decide (n_freshness d' = Dirty)); [by eapply (B1Qrun_effect _ IH)|by eapply qrel_m_return]].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_run_effect] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [] ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros d ? sb2''' Hrel4 Hk4].
      eapply (qrel_bind_of Hk4 Hrel4); [|intros [] ? sb2'''' Hrel5 Hk5].
      * intros s5 [] Hm Hxy. destruct (n_scope d) as [o|]; [|by eapply qrel_m_return].
        eapply (qrel_bind_of Hm Hxy); [intros ????; qrel_pure_tac|intros [] ? sb2'''' Hrel5 Hk5].
        eapply (qrel_bind_of Hk5 Hrel5); [intros ????; by eapply (B1Qdischarge _ IH)|intros [] ? sb2''''' Hrel6 Hk6; qrel_pure_tac].
      * eapply (qrel_bind_of Hk5 Hrel5); [intros ????; qrel_pure_tac|intros d' ? sb2''''' Hrel6 Hk6].
        eapply (qrel_bind_of Hk6 Hrel6); [intros s7 [] Hm Hxy; destruct (n_disposed d'); [by eapply qrel_m_return|by eapply (B1Qbody _ IH)]|intros [] ? sb2_7 Hrel7 Hk7; qrel_pure_tac].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_run_effect_body] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ?? Hm Hxy; eapply qrel_hosted; [exact Hm|exact Hxy|intros ????; by eapply (B1Qexec _ IH)]|intros r0 ? sb2''' Hrel4 Hk4].
      eapply (qrel_bind_of Hk4 Hrel4); [intros ????; qrel_pure_tac|intros d' ? sb2'''' Hrel5 Hk5].
      eapply (qrel_bind_of Hk5 Hrel5); [intros ????; qrel_pure_tac|intros [] ? sb2''''' Hrel6 Hk6; destruct r0; [by eapply qrel_m_return|eapply (qrel_bind_of Hk6 Hrel6); [intros ????; qrel_pure_tac|intros [] ? sb2_7 Hrel7 Hk7; qrel_pure_tac]]].
    + intros o s1 s2 s1' r H Hrel. cbn [b1_discharge] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ?? Hm Hxy; eapply qrel_detached; [exact Hm|exact Hxy|intros ????; by eapply (B1Qdispose_scope _ IH)]|].
      intros [] sb1 sb2 Hrel1 Hk. rewrite !bind_get in Hk |- *.
      destruct (queue_rel_fields _ _ Hrel1) as (_&_&_&_&Hfl&_&Hbd&_&_&_&_&_&_&Hlq&_).
      rewrite <- Hbd, <- Hfl, <- Hlq.
      destruct (bool_decide _ && _ && _); [qrel_pure_tac|by eapply qrel_m_return].
    + intros o s1 s2 s1' r H Hrel. cbn [b1_retire] in H |- *.
      eapply (qrel_bind_of H Hrel); [|intros [] sb1 sb2 Hrel1 Hk; rewrite !bind_get in Hk |- *; destruct (queue_rel_fields _ _ Hrel1) as (_&_&_&_&_&_&_&_&_&_&_&_&_&Hlq&_); rewrite <- Hlq; destruct (live_queue sb1); [by eapply (B1Qrequest _ IH)|by eapply qrel_m_return]].
      intros s1'0 [] Hm Hxy. eapply qrel_detached; [exact Hm|exact Hxy|].
      intros x0 x2 x1' r2 Hm0 Hxy0.
      eapply (qrel_bind_of Hm0 Hxy0); [intros ????; qrel_pure_tac|intros x ? sb2 Hrel2 Hk2].
      destruct (o_disposed x); [by eapply qrel_m_return|eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel3 Hk3; by eapply (B1Qdispose_scope _ IH)]].
    + intros o s1 s2 s1' r H Hrel. cbn [b1_dispose_scope] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros x ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; by eapply (B1Qteardown _ IH)].
    + intros o ls cs s1 s2 s1' r H Hrel. cbn [b1_teardown] in H |- *.
      destruct ls as [|l ls'], cs as [|[[b e] a] cs']; [by eapply qrel_m_return|..].
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qcleanup _ IH)|intros [] ? sb2 Hrel1 Hk; by eapply (B1Qteardown _ IH)].
      * eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qrelease _ IH)|intros [] ? sb2 Hrel1 Hk; by eapply (B1Qteardown _ IH)].
      * destruct (decide (a < l_acquired l)).
        -- eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qrelease _ IH)|intros [] ? sb2 Hrel1 Hk; by eapply (B1Qteardown _ IH)].
        -- eapply (qrel_bind_of H Hrel); [intros ????; by eapply (B1Qcleanup _ IH)|intros [] ? sb2 Hrel1 Hk; by eapply (B1Qteardown _ IH)].
    + intros o b e s1 s2 s1' r H Hrel. cbn [b1_run_cleanup] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros x ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; by eapply (B1Qexec _ IH)|intros r0 ? sb2'' Hrel3 Hk3].
      destruct r0; [by eapply qrel_m_return|].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4; qrel_pure_tac].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_release] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [] ? sb2 Hrel1 Hk; by eapply (B1Qdispose _ IH)].
    + intros n s1 s2 s1' r H Hrel. cbn [b1_dispose_node] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_disposed d); [by eapply qrel_m_return|].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; destruct (n_link d); qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ????; qrel_pure_tac|intros [] ? sb2''' Hrel4 Hk4].
      destruct (n_scope d); [by eapply (B1Qretire _ IH)|by eapply qrel_m_return].
    + intros n o s1 s2 s1' r H Hrel. cbn [b1_attach] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros x ? sb2 Hrel1 Hk].
      destruct (o_disposed x).
      * eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qrelease _ IH)|intros [] ? sb2' Hrel2 Hk2; qrel_pure_tac].
      * eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2; qrel_pure_tac].
    + intros n v s1 s2 s1' r H Hrel. cbn [b1_write_signal] in H |- *.
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros d ? sb2 Hrel1 Hk].
      destruct (n_kind d); [|discriminate|discriminate].
      destruct (decide (n_value d = v)); [by eapply qrel_m_return|].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; qrel_pure_tac|intros [] ? sb2' Hrel2 Hk2].
      eapply (qrel_bind_of Hk2 Hrel2); [intros ????; qrel_pure_tac|intros [] ? sb2'' Hrel3 Hk3].
      eapply (qrel_bind_of Hk3 Hrel3); [intros ?? Hm Hxy; unfold m_step in *; destruct (notify_dirty f n _) eqn:Em; simplify_eq; destruct (qrel_notify_dirty _ _ _ _ _ Hxy Em) as (y' & Em2 & Hxy'); rewrite Em2; by exists y'|intros [] ? sb2''' Hrel4 Hk4; by eapply (B1Qrequest _ IH)].
    + intros s1 s2 s1' r H Hrel. cbn [b1_request_flush] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&Hfl&_&Hbd&_&_&_&_&_&_&Hlq&Hob&_).
      rewrite <- Hlq, <- Hbd, <- Hob, <- Hfl.
      destruct (live_queue s1 && bool_decide (g_batch_depth s1 = 0)); [|by eapply qrel_m_return].
      destruct (outside_bodies s1); [by eapply (B1Qflush _ IH)|].
      destruct (g_flushing s1); [by eapply qrel_m_return|qrel_pure_tac].
    + intros s1 s2 s1' r H Hrel. cbn [b1_settle_flush] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&Hfo&Hbd&_&_&_&_&_&_&_&Hob&_).
      rewrite <- Hfo, <- Hbd, <- Hob.
      destruct (g_flush_owed s1 && bool_decide (g_batch_depth s1 = 0) && outside_bodies s1); [by eapply (B1Qflush _ IH)|by eapply qrel_m_return].
    + intros s1 s2 s1' r H Hrel. cbn [b1_run_flush] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&Hfl&_). rewrite <- Hfl.
      destruct (g_flushing s1); [by eapply qrel_m_return|].
      eapply (qrel_bind_of H Hrel); [intros ????; qrel_pure_tac|intros [] ? sb2 Hrel1 Hk].
      eapply (qrel_bind_of Hk Hrel1); [intros ????; by eapply (B1Qdrain _ IH)|intros [] ? sb2' Hrel2 Hk2; qrel_pure_tac].
    + intros s1 s2 s1' r H Hrel. cbn [b1_drain] in H |- *. rewrite !bind_get in H |- *.
      destruct (queue_rel_fields _ _ Hrel) as (_&_&_&_&_&_&_&_&_&_&_&_&_&_&_&_&Hhd).
      rewrite <- Hhd.
      destruct (g_queue s1 !! g_queue_head s1) as [n|] eqn:En.
      * eapply bind_inv in H as (sb1 & [] & Em & Hk). unfold m_modify in Em; simplify_eq.
        assert (Hrel1 : queue_rel (with_queue_head (S (g_queue_head s1)) s1)
                                  (with_queue_head (S (g_queue_head s2)) s2))
          by by eapply queue_rel_pop.
        eapply bind_inv in Hk as (sb1' & [] & Hexe & Hdr).
        destruct (B1Qexecute _ IH _ _ _ _ _ Hexe Hrel1) as (sb2' & Hexe2 & Hrel2).
        destruct (B1Qdrain _ IH _ _ _ _ Hdr Hrel2) as (s2' & Hdr2 & Hrel3).
        exists s2'; split; [|exact Hrel3].
        unfold m_bind at 1, m_modify at 1; simpl.
        unfold m_bind at 1; rewrite Hexe2; simpl; exact Hdr2.
      * unfold m_modify in *. simplify_eq.
        eexists; split; [reflexivity|].
        rewrite <- (queue_rel_reset_eq _ _ Hrel).
        apply queue_rel_refl. simpl. lia.
Qed.

Lemma b1_drain_resets f s s' :
  b1_drain f s = Some (s', tt) ->
  g_queue s' = [] /\ g_queue_head s' = 0.
Proof.
  revert s s'. induction f as [|f IH]; intros s s' H; [cbn in H; discriminate|].
  cbn [b1_drain] in H. rewrite bind_get in H.
  destruct (g_queue s !! g_queue_head s) as [n|].
  - eapply bind_inv in H as (s1 & [] & _ & H1).
    eapply bind_inv in H1 as (s2 & [] & _ & H2).
    by eapply IH.
  - unfold m_modify in H. simplify_eq. split; reflexivity.
Qed.

Lemma drain_resets f s s' :
  drain f s = Some (s', tt) ->
  g_queue s' = [] /\ g_queue_head s' = 0.
Proof.
  revert s s'. induction f as [|f IH]; intros s s' H; [cbn in H; discriminate|].
  cbn [drain] in H. rewrite bind_get in H.
  destruct (g_queue s !! g_queue_head s) as [n|].
  - eapply bind_inv in H as (s1 & [] & _ & H1).
    eapply bind_inv in H1 as (s2 & [] & _ & H2).
    by eapply IH.
  - unfold m_modify in H. simplify_eq. split; reflexivity.
Qed.

Lemma b1_write_signal_eq n v s s' :
  (exists f, write_signal f n v s = Some (s', tt)) <->
  (exists fb, b1_write_signal fb n v s = Some (s', tt)).
Proof.
  split.
  - intros [f Hf].
    destruct (Fwrite1 _ (b1_fwd_all f) n v s s s' tt (b1_state_rel_refl s) Hf) as (fb & sb' & Efb & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Dwr _ _ _ _].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Dwr _ _ _ _].
    pose proof (Dwr n v s s' tt Hf) as [Hp1 _].
    pose proof (B1Dwr n v s sb' tt Efb) as [Hp2 _].
    assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst sb'. by exists fb.
  - intros [fb Hfb].
    destruct (Bwrite1 _ (b1_bwd_all fb) n v s s s' tt (b1_state_rel_refl s) Hfb) as (f & s0' & Ef & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Dwr _ _ _ _].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Dwr _ _ _ _].
    pose proof (Dwr n v s s0' tt Ef) as [Hp1 _].
    pose proof (B1Dwr n v s s' tt Hfb) as [Hp2 _].
    assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst s0'. by exists f.
Qed.

Lemma b1_run_effect_eq n s s' :
  (exists f, run_effect f n s = Some (s', tt)) <->
  (exists fb, b1_run_effect fb n s = Some (s', tt)).
Proof.
  split.
  - intros [f Hf].
    destruct (Frun_effect1 _ (b1_fwd_all f) n s s s' tt (b1_state_rel_refl s) Hf) as (fb & sb' & Efb & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ Dre _ _ _ _ _ _ _ _ _ _ _ _ _ _].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ B1Dre _ _ _ _ _ _ _ _ _ _ _ _ _ _].
    pose proof (Dre n s s' tt Hf) as [Hp1 _].
    pose proof (B1Dre n s sb' tt Efb) as [Hp2 _].
    assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst sb'. by exists fb.
  - intros [fb Hfb].
    destruct (Brun_effect1 _ (b1_bwd_all fb) n s s s' tt (b1_state_rel_refl s) Hfb) as (f & s0' & Ef & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ Dre _ _ _ _ _ _ _ _ _ _ _ _ _ _].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ B1Dre _ _ _ _ _ _ _ _ _ _ _ _ _ _].
    pose proof (Dre n s s0' tt Ef) as [Hp1 _].
    pose proof (B1Dre n s s' tt Hfb) as [Hp2 _].
    assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst s0'. by exists f.
Qed.

Lemma b1_drain_eq s s' :
  (exists f, drain f s = Some (s', tt)) <->
  (exists fb, b1_drain fb s = Some (s', tt)).
Proof.
  split.
  - intros [f Hf].
    destruct (Fdrain1 _ (b1_fwd_all f) s s s' tt (b1_state_rel_refl s) Hf) as (fb & sb' & Efb & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Ddr].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Ddr].
    pose proof (Ddr s s' tt Hf) as [Hp1 _].
    pose proof (B1Ddr s sb' tt Efb) as [Hp2 _].
    assert (s' = sb') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst sb'. by exists fb.
  - intros [fb Hfb].
    destruct (Bdrain1 _ (b1_bwd_all fb) s s s' tt (b1_state_rel_refl s) Hfb) as (f & s0' & Ef & Hrel).
    destruct (engine_depths_kept f) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ Ddr].
    destruct (b1_depths_kept fb) as [_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ B1Ddr].
    pose proof (Ddr s s0' tt Ef) as [Hp1 _].
    pose proof (B1Ddr s s' tt Hfb) as [Hp2 _].
    assert (s0' = s') by (apply b1_state_rel_eq; [exact Hrel|congruence]).
    subst s0'. by exists f.
Qed.

(** Idle-graph fused execution of a signal whose sole observer is an idle effect (`S -> E`). *)
Definition fused_single_signal_effect (f : nat) (src eff : node_id) (v : val) : M unit :=
  let* d := get_node src in
  match n_kind d with
  | KSignal =>
    if decide (n_value d = v) then m_return tt
    else
      let* _ := modify_node src (node_with_value v) in
      let* _ := m_modify (ghost_publish src) in
      let* _ := m_modify (fun st => with_flush_owed false (with_flushing true st)) in
      let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
      let* _ := run_effect f eff in
      let* _ := drain f in
      m_modify (with_flushing false)
  | _ => stuck
  end.

Lemma node_clean_unqueued_marked_dirty de :
  node_with_freshness Clean (node_with_queued false (node_marked Dirty (n_unannounced de) true de)) =
  node_with_freshness Clean (node_with_queued false de).
Proof.
  destruct de; reflexivity.
Qed.

Lemma m_bind_get_node {A} n (k : node -> M A) d s :
  g_nodes s !! n = Some d -> (let* x := get_node n in k x) s = k d s.
Proof. intros H. unfold m_bind, get_node. by rewrite H. Qed.

Lemma m_bind_modify_node {A} n u (k : M A) d s :
  g_nodes s !! n = Some d -> (let* _ := modify_node n u in k) s = k (set_node n (u d) s).
Proof. intros H. unfold m_bind, modify_node. by rewrite H. Qed.

Lemma m_bind_modify {A} u (k : M A) s :
  (let* _ := m_modify u in k) s = k (u s).
Proof. reflexivity. Qed.

Lemma m_bind_step {A} step (k : M A) s s' :
  step s = Some s' -> (let* _ := m_step step in k) s = k s'.
Proof. intros H. unfold m_bind, m_step. by rewrite H. Qed.

Lemma m_bind_get {A} (k : state -> M A) s :
  (let* s0 := m_get in k s0) s = k s s.
Proof. reflexivity. Qed.

Lemma if_true_eq {A} (x y : A) : (if true then x else y) = x.
Proof. reflexivity. Qed.

Lemma if_false_eq {A} (x y : A) : (if false then x else y) = y.
Proof. reflexivity. Qed.

Lemma bool_decide_nat_0 : bool_decide (0%nat = 0%nat) = true.
Proof. by apply bool_decide_eq_true. Qed.

Lemma reset_queue_set_node n d s :
  with_queue_head 0 (with_queue [] (set_node n d s)) = set_node n d (with_queue_head 0 (with_queue [] s)).
Proof. destruct s; reflexivity. Qed.

Lemma reset_queue_enqueue n s :
  with_queue_head 0 (with_queue [] (enqueue n s)) = with_queue_head 0 (with_queue [] s).
Proof. destruct s; reflexivity. Qed.

Lemma reset_queue_head h s :
  with_queue_head 0 (with_queue [] (with_queue_head h s)) = with_queue_head 0 (with_queue [] s).
Proof. destruct s; reflexivity. Qed.

Lemma reset_queue_flush_owed b s :
  with_queue_head 0 (with_queue [] (with_flush_owed b s)) = with_flush_owed b (with_queue_head 0 (with_queue [] s)).
Proof. destruct s; reflexivity. Qed.

Lemma reset_queue_flushing b s :
  with_queue_head 0 (with_queue [] (with_flushing b s)) = with_flushing b (with_queue_head 0 (with_queue [] s)).
Proof. destruct s; reflexivity. Qed.

Lemma set_node_with_flushing n d b s :
  set_node n d (with_flushing b s) = with_flushing b (set_node n d s).
Proof. destruct s; reflexivity. Qed.

Lemma set_node_with_flush_owed n d b s :
  set_node n d (with_flush_owed b s) = with_flush_owed b (set_node n d s).
Proof. destruct s; reflexivity. Qed.

Lemma queue_rel_fused_signal (x : state) eff d_eff :
  g_queue_head x = length (g_queue x) ->
  queue_rel
    (set_node eff (node_with_freshness Clean (node_with_queued false d_eff))
       (with_flush_owed false (with_flushing true x)))
    (set_node eff (node_with_freshness Clean (node_with_queued false (node_marked Dirty (n_unannounced d_eff) true d_eff)))
       (with_queue_head (S (g_queue_head (enqueue eff (set_node eff (node_marked Dirty (n_unannounced d_eff) true d_eff) x))))
          (with_flush_owed false (with_flushing true (enqueue eff (set_node eff (node_marked Dirty (n_unannounced d_eff) true d_eff) x)))))).
Proof.
  intros Hqhd. rewrite node_clean_unqueued_marked_dirty.
  unfold queue_rel, pending. repeat split.
  - rewrite !reset_queue_set_node, !reset_queue_head, !reset_queue_flush_owed, !reset_queue_flushing, !reset_queue_enqueue, !reset_queue_set_node.
    rewrite <- set_node_with_flushing, <- set_node_with_flush_owed, set_node_twice.
    reflexivity.
  - cbn [g_queue g_queue_head enqueue with_queue with_queue_head with_flush_owed with_flushing set_node].
    rewrite Hqhd, drop_all.
    replace (S (length (g_queue x))) with (length (g_queue x ++ [eff])) by (rewrite length_app; simpl; lia).
    by rewrite drop_all.
  - cbn [g_queue g_queue_head with_flush_owed with_flushing set_node]. lia.
  - cbn [g_queue g_queue_head enqueue with_queue with_queue_head with_flush_owed with_flushing set_node].
    rewrite length_app; simpl; lia.
Qed.

Lemma queue_rel_fused_memo (x : state) memo eff dm' de' :
  g_queue_head x = length (g_queue x) ->
  queue_rel
    (with_flush_owed false (with_flushing true (set_node eff de' (set_node memo dm' x))))
    (with_queue_head (S (g_queue_head (enqueue eff (set_node eff de' (set_node memo dm' x)))))
       (with_flush_owed false (with_flushing true (enqueue eff (set_node eff de' (set_node memo dm' x)))))).
Proof.
  intros Hqhd. unfold queue_rel, pending. split; [reflexivity|repeat split].
  - cbn [g_queue g_queue_head enqueue with_queue with_queue_head with_flush_owed with_flushing set_node].
    rewrite Hqhd, drop_all.
    replace (S (length (g_queue x))) with (length (g_queue x ++ [eff])) by (rewrite length_app; simpl; lia).
    by rewrite drop_all.
  - cbn [g_queue g_queue_head with_flush_owed with_flushing set_node]. lia.
  - cbn [g_queue g_queue_head enqueue with_queue with_queue_head with_flush_owed with_flushing set_node].
    rewrite length_app; simpl; lia.
Qed.

(**md
**On an idle graph, fused `S -> E` execution without intermediate enqueue/dequeue produces the exact same final state and trace as `write_signal`.**

Refines the idle `S -> E` branch of `NotifySingleFromIdleSignal`: setting `g_flushing := true`, clearing `eff` to `Clean` / `n_queued := false`, running `run_effect eff`, and draining any reentrant effects gives the identical state `s'` (`g_trace`, `g_nodes`, `g_queue`) as `write_signal`.

- [src:] signal node id.
- [d_src:] signal node record in `s`.
- [eff:] sole observer effect node id.
- [slot:] observer edge slot on `eff`.
- [de:] effect node record in `s`.
- [v:] new value written to `src`.
- [s:] idle starting engine state.
- [s':] final engine state after flush completes.
*)
Theorem fused_single_signal_effect_eq src d_src eff slot de v s s' :
  g_nodes s !! src = Some d_src ->
  n_kind d_src = KSignal ->
  observer_items (observers_of (g_edges s) src) = [(eff, slot)] ->
  g_nodes s !! eff = Some de ->
  n_kind de = KEffect ->
  n_disposed de = false ->
  n_in_cleanup de = false ->
  n_queued de = false ->
  g_batch_depth s = 0 ->
  g_pull_depth s = 0 ->
  g_flushing s = false ->
  g_current s = None ->
  g_untracked s = false ->
  g_flush_owed s = false ->
  pending s = [] ->
  g_queue_head s <= length (g_queue s) ->
  (exists f, fused_single_signal_effect f src eff v s = Some (s', tt)) <->
  (exists f, write_signal f src v s = Some (s', tt)).
Proof.
  intros Hsrc Hks Hobs Heff Hke Hde Hclean Hnq Hbd Hpd Hfl Hcur Hunt Hfo Hpend Hqle.
  assert (Hne : eff <> src) by (intros ->; rewrite Hsrc in Heff; simplify_eq; congruence).
  assert (Hqhd : g_queue_head s = length (g_queue s)).
  { unfold pending in Hpend. pose proof (f_equal length Hpend) as Hlen. rewrite length_drop in Hlen. simpl in Hlen. lia. }
  rewrite b1_write_signal_eq.
  destruct (decide (n_value d_src = v)) as [Heqv|Hnev].
  - split; intros [f Hf].
    + unfold fused_single_signal_effect in Hf. unfold get_node, m_bind at 1 in Hf.
      rewrite Hsrc, Hks, decide_True in Hf by exact Heqv. unfold m_return in Hf. simplify_eq.
      exists 1. cbn [b1_write_signal]. unfold get_node, m_bind at 1.
      rewrite Hsrc, Hks, decide_True by reflexivity. reflexivity.
    + destruct f as [|f]; [cbn in Hf; discriminate|].
      cbn [b1_write_signal] in Hf. unfold get_node, m_bind at 1 in Hf.
      rewrite Hsrc, Hks, decide_True in Hf by exact Heqv. unfold m_return in Hf. simplify_eq.
      exists 0. unfold fused_single_signal_effect. unfold get_node, m_bind at 1.
      rewrite Hsrc, Hks, decide_True by reflexivity. reflexivity.
  - set (sa := ghost_publish src (set_node src (node_with_value v d_src) s)).
    assert (Ha_nodes : g_nodes sa = <[src := node_with_value v d_src]> (g_nodes s))
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_eff : g_nodes sa !! eff = Some de) by (rewrite Ha_nodes, lookup_insert_ne by congruence; exact Heff).
    assert (Ha_edges : g_edges sa = g_edges s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_q : g_queue sa = g_queue s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_qhd : g_queue_head sa = g_queue_head s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_bd : g_batch_depth sa = g_batch_depth s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_pd : g_pull_depth sa = g_pull_depth s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_fl : g_flushing sa = g_flushing s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_cur : g_current sa = g_current s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_unt : g_untracked sa = g_untracked s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_obs : observer_items (observers_of (g_edges sa) src) = [(eff, slot)]) by (rewrite Ha_edges; exact Hobs).
    assert (Hpre : forall f,
      fused_single_signal_effect f src eff v s =
      (let* _ := run_effect f eff in let* _ := drain f in m_modify (with_flushing false))
        (set_node eff (node_with_freshness Clean (node_with_queued false de))
           (with_flush_owed false (with_flushing true sa)))).
    { intros f. unfold fused_single_signal_effect.
      rewrite (m_bind_get_node _ _ _ _ Hsrc).
      rewrite Hks, decide_False by exact Hnev.
      rewrite (m_bind_modify_node _ _ _ _ _ Hsrc).
      rewrite m_bind_modify. fold sa.
      rewrite m_bind_modify.
      assert (Hwf_eff : g_nodes (with_flush_owed false (with_flushing true sa)) !! eff = Some de) by exact Ha_eff.
      rewrite (m_bind_modify_node _ _ _ _ _ Hwf_eff).
      reflexivity. }
    set (sf0 := set_node eff (node_with_freshness Clean (node_with_queued false de))
                  (with_flush_owed false (with_flushing true sa))).
    assert (Hmark : forall f,
      notify_dirty (S f) src sa =
      Some (enqueue eff (set_node eff (node_marked Dirty (n_unannounced de) true de) sa))).
    { intros f. unfold notify_dirty. erewrite notify_with_singleton; [|exact Ha_obs].
      cbn [mark_dirty]. rewrite Ha_eff, Hke.
      unfold effect_marked. rewrite Hde, Hclean, Hnq. reflexivity. }
    set (sa_enq := enqueue eff (set_node eff (node_marked Dirty (n_unannounced de) true de) sa)).
    set (sw0 := with_queue_head (S (g_queue_head sa_enq)) (with_flush_owed false (with_flushing true sa_enq))).
    assert (Hrel_0 : queue_rel sf0 (set_node eff (node_with_freshness Clean (node_with_queued false (node_marked Dirty (n_unannounced de) true de))) sw0))
      by (unfold sf0, sw0, sa_enq; apply queue_rel_fused_signal; rewrite Ha_qhd, Ha_q; exact Hqhd).
    assert (Hlq_enq : live_queue sa_enq = true)
      by (unfold live_queue, sa_enq, enqueue, set_node; simpl; rewrite Ha_q, Ha_qhd, Hqhd; apply bool_decide_eq_true; rewrite length_app; simpl; lia).
    assert (Hbd_enq : g_batch_depth sa_enq = 0)
      by (unfold sa_enq, enqueue, set_node; simpl; rewrite Ha_bd; exact Hbd).
    assert (Hob_enq : outside_bodies sa_enq = true)
      by (unfold outside_bodies, sa_enq, enqueue, set_node; simpl; by rewrite Ha_pd, Ha_cur, Ha_unt, Hpd, Hcur, Hunt).
    assert (Hfl_enq : g_flushing sa_enq = false)
      by (unfold sa_enq, enqueue, set_node; simpl; rewrite Ha_fl; exact Hfl).
    assert (Hlu_enq : (g_queue sa ++ [eff]) !! g_queue_head sa = Some eff)
      by (rewrite Ha_q, Ha_qhd, Hqhd, lookup_app_r, Nat.sub_diag by lia; reflexivity).
    assert (Hsw0_eff : g_nodes sw0 !! eff = Some (node_marked Dirty (n_unannounced de) true de))
      by (unfold sw0, sa_enq, enqueue, set_node; simpl; rewrite lookup_insert_eq; reflexivity).
    assert (Hwrite_prefix : forall k,
      b1_write_signal (5 + k) src v s =
      (let* _ := b1_run_effect k eff in
       let* _ := b1_drain (S k) in
       m_modify (with_flushing false))
        (set_node eff (node_with_freshness Clean (node_with_queued false (node_marked Dirty (n_unannounced de) true de))) sw0)).
    { intros k. change (5 + k) with (S (S (S (S (S k))))).
      cbn [b1_write_signal].
      rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
      rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
      rewrite (m_bind_step _ _ _ _ (Hmark (S (S (S k))))).
      cbn [b1_request_flush]. rewrite m_bind_get. fold sa_enq.
      rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
      cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
      cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
      change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
              g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
        with ((g_queue sa ++ [eff]) !! g_queue_head sa).
      rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify.
      change (g_queue_head (with_flush_owed false (with_flushing true sa_enq))) with (g_queue_head sa_enq).
      fold sw0.
      cbn [b1_execute_effect]. rewrite ?b4_bind_assoc, (m_bind_get_node _ _ _ _ Hsw0_eff).
      change (n_disposed (node_marked Dirty (n_unannounced de) true de)) with (n_disposed de).
      rewrite Hde, decide_False by discriminate.
      rewrite ?b4_bind_assoc, b4_bind_ret_l, ?b4_bind_assoc.
      rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
      rewrite (m_bind_modify_node _ _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
      change (n_freshness (node_marked Dirty (n_unannounced de) true de)) with Dirty.
      rewrite decide_True by reflexivity.
      reflexivity. }
    split.
    + intros [f Hf]. rewrite Hpre in Hf.
      eapply bind_inv in Hf as (s_re & [] & Hre & Hd).
      eapply bind_inv in Hd as (s_dr & [] & Hdr & Hfl_end).
      unfold m_modify in Hfl_end. simplify_eq.
      assert (Hre_ex : exists fb1, b1_run_effect fb1 eff sf0 = Some (s_re, tt))
        by (rewrite <- b1_run_effect_eq; exists f; exact Hre).
      destruct Hre_ex as [fb1 Hre1].
      assert (Hdr_ex : exists fb2, b1_drain fb2 s_re = Some (s_dr, tt))
        by (rewrite <- b1_drain_eq; exists f; exact Hdr).
      destruct Hdr_ex as [fb2 Hdr2].
      set (fb := max fb1 fb2).
      assert (Hre' : b1_run_effect fb eff sf0 = Some (s_re, tt))
        by (eapply (B1Mrun_effect _ (b1_mono_all fb1)); [lia|exact Hre1]).
      assert (Hdr' : b1_drain fb s_re = Some (s_dr, tt))
        by (eapply (B1Mdrain _ (b1_mono_all fb2)); [lia|exact Hdr2]).
      destruct (B1Qrun_effect _ (b1_qrel_all fb) eff _ _ _ _ Hre' Hrel_0) as (sw1 & Hre_w & Hrel1).
      destruct (B1Qdrain _ (b1_qrel_all fb) _ _ _ _ Hdr' Hrel1) as (sw2 & Hdr_w & Hrel2).
      destruct (b1_drain_resets _ _ _ Hdr') as [Hq1 Hh1].
      destruct (b1_drain_resets _ _ _ Hdr_w) as [Hq2 Hh2].
      assert (Heq : s_dr = sw2) by (apply queue_rel_drained_eq; assumption).
      subst sw2.
      exists (5 + fb). rewrite Hwrite_prefix.
      unfold m_bind at 1. rewrite Hre_w.
      assert (Hdr_w' : b1_drain (S fb) sw1 = Some (s_dr, tt))
        by (eapply (B1Mdrain _ (b1_mono_all fb)); [lia|exact Hdr_w]).
      cbn [option_bind]. unfold m_bind at 1. rewrite Hdr_w'. cbn [option_bind]. reflexivity.
    + intros [f Hf].
      assert (Hunder5 : f < 5 -> b1_write_signal f src v s = None).
      { intros Hlt. destruct f as [|[|[|[|[|f']]]]]; try lia.
        - reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc).
          rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          unfold m_bind, m_step, notify_dirty. erewrite notify_with_singleton by exact Ha_obs.
          cbn [walk_marks mark_dirty]. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc).
          rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 0)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc).
          rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 1)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc).
          rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 2)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
          change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
                  g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
            with ((g_queue sa ++ [eff]) !! g_queue_head sa).
          rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify. reflexivity. }
      destruct f as [|[|[|[|[|f']]]]]; [rewrite Hunder5 in Hf by lia; discriminate..|].
      replace (S (S (S (S (S f'))))) with (5 + f') in Hf by lia.
      rewrite Hwrite_prefix in Hf.
      eapply bind_inv in Hf as (sw1 & [] & Hre_w & Hrest).
      eapply bind_inv in Hrest as (sw2 & [] & Hdr_w & Hfl_end).
      unfold m_modify in Hfl_end. simplify_eq.
      pose proof (queue_rel_sym _ _ Hrel_0) as Hrel_0'.
      destruct (B1Qrun_effect _ (b1_qrel_all f') eff _ _ _ _ Hre_w Hrel_0') as (sf1 & Hre_f1 & Hrel1).
      destruct (B1Qdrain _ (b1_qrel_all (S f')) _ _ _ _ Hdr_w Hrel1) as (sf2 & Hdr_f1 & Hrel2).
      destruct (b1_drain_resets _ _ _ Hdr_w) as [Hq1 Hh1].
      destruct (b1_drain_resets _ _ _ Hdr_f1) as [Hq2 Hh2].
      assert (Heq : sw2 = sf2) by (apply queue_rel_drained_eq; assumption).
      subst sf2.
      assert (Hre_eng : exists fe1, run_effect fe1 eff sf0 = Some (sf1, tt))
        by (rewrite b1_run_effect_eq; by exists f').
      assert (Hdr_eng : exists fe2, drain fe2 sf1 = Some (sw2, tt))
        by (rewrite b1_drain_eq; by exists (S f')).
      destruct Hre_eng as [fe1 Hre_e], Hdr_eng as [fe2 Hdr_e].
      set (fe := max fe1 fe2).
      assert (Hre_e' : run_effect fe eff sf0 = Some (sf1, tt))
        by (eapply (Mrun_effect _ (engine_mono_all fe1)); [lia|exact Hre_e]).
      assert (Hdr_e' : drain fe sf1 = Some (sw2, tt))
        by (eapply (Mdrain _ (engine_mono_all fe2)); [lia|exact Hdr_e]).
      exists fe. rewrite Hpre. fold sf0. unfold m_bind at 1. rewrite Hre_e'.
      cbn [option_bind]. unfold m_bind at 1. rewrite Hdr_e'. cbn [option_bind]. reflexivity.
Qed.

(** Idle-graph fused execution of `S -> M -> E`, setting `E` to `Check` + `queued := true` and `g_flushing := true` while refreshing `M` inline via `b1_refresh`, then settling/running `E` and draining any newly queued effects. *)
Definition fused_single_memo_effect (f : nat) (src memo eff : node_id) (v : val) : M unit :=
  let* d := get_node src in
  match n_kind d with
  | KSignal =>
    if decide (n_value d = v) then m_return tt
    else
      let* _ := modify_node src (node_with_value v) in
      let* _ := m_modify (ghost_publish src) in
      let* _ := modify_node memo (fun x => node_marked Dirty (n_unannounced x) (n_queued x) x) in
      let* _ := modify_node eff (fun x => node_marked Check (n_unannounced x) true x) in
      let* _ := m_modify (fun st => with_flush_owed false (with_flushing true st)) in
      let* _ := b1_refresh f memo in
      let* _ := get_node memo in
      let* d' := get_node eff in
      let* d_m := get_node memo in
      let* s1 := m_get in
      let* _ :=
        if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running s1 \/ memo ∈ g_checking s1))
        then m_step (mark_dirty f eff) else m_return tt in
      let* _ := b1_resolve_walk f eff 1 in
      let* d'' := get_node eff in
      let* _ :=
        if decide (n_freshness d'' = Check)
        then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
        else m_return tt in
      let* d''' := get_node eff in
      let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
      let* _ := if decide (n_freshness d''' = Dirty) then b1_run_effect f eff else m_return tt in
      let* _ := b1_drain f in
      m_modify (with_flushing false)
  | _ => stuck
  end.

Lemma fused_single_memo_effect_tail_mono f1 f2 memo eff :
  f1 <= f2 ->
  forall st r,
    (let* _ := b1_refresh f1 memo in
     let* _ := get_node memo in
     let* d' := get_node eff in
     let* d_m := get_node memo in
     let* s1 := m_get in
     let* _ :=
       if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running s1 \/ memo ∈ g_checking s1))
       then m_step (mark_dirty f1 eff) else m_return tt in
     let* _ := b1_resolve_walk f1 eff 1 in
     let* d'' := get_node eff in
     let* _ :=
       if decide (n_freshness d'' = Check)
       then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
       else m_return tt in
     let* d''' := get_node eff in
     let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
     let* _ := if decide (n_freshness d''' = Dirty) then b1_run_effect f1 eff else m_return tt in
     let* _ := b1_drain f1 in
     m_modify (with_flushing false)) st = Some r ->
    (let* _ := b1_refresh f2 memo in
     let* _ := get_node memo in
     let* d' := get_node eff in
     let* d_m := get_node memo in
     let* s1 := m_get in
     let* _ :=
       if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running s1 \/ memo ∈ g_checking s1))
       then m_step (mark_dirty f2 eff) else m_return tt in
     let* _ := b1_resolve_walk f2 eff 1 in
     let* d'' := get_node eff in
     let* _ :=
       if decide (n_freshness d'' = Check)
       then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
       else m_return tt in
     let* d''' := get_node eff in
     let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
     let* _ := if decide (n_freshness d''' = Dirty) then b1_run_effect f2 eff else m_return tt in
     let* _ := b1_drain f2 in
     m_modify (with_flushing false)) st = Some r.
Proof.
  intros Hle st r H. revert H.
  eapply bind_mono; [intros ??; by eapply (B1Mrefresh _ (b1_mono_all f1))|intros [] ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros ??? _].
  eapply bind_mono; [intros ?? E; exact E|intros d' ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros dm ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros st1 ?? _].
  eapply bind_mono; [intros ?? E; destruct (decide _); [by eapply m_step_mark_dirty_mono|exact E]|intros [] ?? _].
  eapply bind_mono; [intros ??; by eapply (B1Mwalk _ (b1_mono_all f1))|intros [] ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros d'' ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros d''' ?? _].
  eapply bind_mono; [intros ?? E; exact E|intros [] ?? _].
  eapply bind_mono; [intros ?? E; destruct (decide (n_freshness d''' = Dirty)); [by eapply (B1Mrun_effect _ (b1_mono_all f1))|exact E]|intros [] ?? _].
  eapply bind_mono; [intros ??; by eapply (B1Mdrain _ (b1_mono_all f1))|intros [] ?? _; exact id].
Qed.

(**md
**On an idle graph, fused `S -> M -> E` execution with `E` marked `Check`/`queued` and `g_flushing = true` yields the exact same final state and trace as `write_signal`.**

Refines the idle `S -> M -> E` branch of `NotifySingleFromIdleSignal` together with Section B1 (`flushing` in `Deferring`): refreshing `M` inline via `b1_refresh` under `g_flushing = true` and then checking/running `E` and draining reentrant effects produces the identical final state and trace as `write_signal`.

- [src:] signal node id.
- [d_src:] signal node record in `s`.
- [memo:] sole memo observer id.
- [slot_m:] observer edge slot on `memo`.
- [dm:] memo node record in `s`.
- [eff:] sole effect observer id of `memo`.
- [slot_e:] observer edge slot on `eff`.
- [de:] effect node record in `s`.
- [h_e:] handle of slot `0` in `sources_of eff`.
- [v:] new value written to `src`.
- [s:] idle starting engine state.
- [s':] final engine state after flush completes.
*)
Theorem fused_single_memo_effect_flush_eq src d_src memo slot_m dm eff slot_e de h_e v s s' :
  g_nodes s !! src = Some d_src ->
  n_kind d_src = KSignal ->
  observer_items (observers_of (g_edges s) src) = [(memo, slot_m)] ->
  g_nodes s !! memo = Some dm ->
  n_kind dm = KMemo ->
  n_freshness dm = Clean ->
  n_disposed dm = false ->
  observer_items (observers_of (g_edges s) memo) = [(eff, slot_e)] ->
  g_nodes s !! eff = Some de ->
  n_kind de = KEffect ->
  n_freshness de = Clean ->
  n_disposed de = false ->
  n_in_cleanup de = false ->
  n_queued de = false ->
  source_slots (sources_of (g_edges s) eff) !! 0 = Some (memo, h_e) ->
  g_batch_depth s = 0 ->
  g_pull_depth s = 0 ->
  g_flushing s = false ->
  g_current s = None ->
  g_untracked s = false ->
  g_flush_owed s = false ->
  pending s = [] ->
  g_queue_head s <= length (g_queue s) ->
  (exists f, fused_single_memo_effect f src memo eff v s = Some (s', tt)) <->
  (exists f, write_signal f src v s = Some (s', tt)).
Proof.
  intros Hsrc Hks Hobs_m Hmemo Hkm Hcm Hdzm Hobs_e Heff Hke Hce Hdze Hclean Hnq Hsl0
         Hbd Hpd Hfl Hcur Hunt Hfo Hpend Hqle.
  assert (Hsm : memo <> src) by (intros ->; rewrite Hsrc in Hmemo; simplify_eq; congruence).
  assert (Hse : eff <> src) by (intros ->; rewrite Hsrc in Heff; simplify_eq; congruence).
  assert (Hme : eff <> memo) by (intros ->; rewrite Hmemo in Heff; simplify_eq; congruence).
  assert (Hqhd : g_queue_head s = length (g_queue s)).
  { unfold pending in Hpend. pose proof (f_equal length Hpend) as Hlen. rewrite length_drop in Hlen. simpl in Hlen. lia. }
  rewrite b1_write_signal_eq.
  destruct (decide (n_value d_src = v)) as [Heqv|Hnev].
  - split; intros [f Hf].
    + unfold fused_single_memo_effect in Hf. unfold get_node, m_bind at 1 in Hf.
      rewrite Hsrc, Hks, decide_True in Hf by exact Heqv. unfold m_return in Hf. simplify_eq.
      exists 1. cbn [b1_write_signal]. unfold get_node, m_bind at 1.
      rewrite Hsrc, Hks, decide_True by reflexivity. reflexivity.
    + destruct f as [|f]; [cbn in Hf; discriminate|].
      cbn [b1_write_signal] in Hf. unfold get_node, m_bind at 1 in Hf.
      rewrite Hsrc, Hks, decide_True in Hf by exact Heqv. unfold m_return in Hf. simplify_eq.
      exists 0. unfold fused_single_memo_effect. unfold get_node, m_bind at 1.
      rewrite Hsrc, Hks, decide_True by reflexivity. reflexivity.
  - set (sa := ghost_publish src (set_node src (node_with_value v d_src) s)).
    assert (Ha_nodes : g_nodes sa = <[src := node_with_value v d_src]> (g_nodes s))
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_memo : g_nodes sa !! memo = Some dm) by (rewrite Ha_nodes, lookup_insert_ne by congruence; exact Hmemo).
    assert (Ha_eff : g_nodes sa !! eff = Some de) by (rewrite Ha_nodes, lookup_insert_ne by congruence; exact Heff).
    assert (Ha_edges : g_edges sa = g_edges s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_q : g_queue sa = g_queue s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_qhd : g_queue_head sa = g_queue_head s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_bd : g_batch_depth sa = g_batch_depth s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_pd : g_pull_depth sa = g_pull_depth s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_fl : g_flushing sa = g_flushing s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_cur : g_current sa = g_current s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_unt : g_untracked sa = g_untracked s)
      by (unfold sa, ghost_publish; destruct (g_nodes (set_node src (node_with_value v d_src) s) !! src); reflexivity).
    assert (Ha_obs_m : observer_items (observers_of (g_edges sa) src) = [(memo, slot_m)])
      by (rewrite Ha_edges; exact Hobs_m).
    assert (Ha_obs_e : observer_items (observers_of (g_edges (set_node memo (node_marked Dirty (n_unannounced dm) (n_queued dm) dm) sa)) memo) = [(eff, slot_e)])
      by (unfold set_node; simpl; rewrite Ha_edges; exact Hobs_e).
    assert (Hmark : forall f,
      notify_dirty (S (S f)) src sa =
      Some (enqueue eff (set_node eff (node_marked Check (n_unannounced de) true de)
               (set_node memo (node_marked Dirty (n_unannounced dm) (n_queued dm) dm) sa)))).
    { intros f. unfold notify_dirty. erewrite notify_with_singleton; [|exact Ha_obs_m].
      cbn [mark_dirty]. rewrite Ha_memo, Hkm, decide_True by exact Hcm.
      erewrite notify_with_singleton; [|exact Ha_obs_e].
      cbn [mark_check]. unfold set_node at 1. simpl.
      rewrite lookup_insert_ne by congruence. rewrite Ha_eff, Hke.
      unfold effect_marked. rewrite Hdze, Hclean, Hnq, decide_True by exact Hce. reflexivity. }
    set (dm' := node_marked Dirty (n_unannounced dm) (n_queued dm) dm).
    set (de' := node_marked Check (n_unannounced de) true de).
    set (sa_enq := enqueue eff (set_node eff de' (set_node memo dm' sa))).
    set (sf0 := with_flush_owed false (with_flushing true (set_node eff de' (set_node memo dm' sa)))).
    set (sw0 := with_queue_head (S (g_queue_head sa_enq)) (with_flush_owed false (with_flushing true sa_enq))).
    assert (Hpre_f : forall f,
      fused_single_memo_effect f src memo eff v s =
      (let* _ := b1_refresh f memo in
       let* _ := get_node memo in
       let* d' := get_node eff in
       let* d_m := get_node memo in
       let* s1 := m_get in
       let* _ :=
         if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running s1 \/ memo ∈ g_checking s1))
         then m_step (mark_dirty f eff) else m_return tt in
       let* _ := b1_resolve_walk f eff 1 in
       let* d'' := get_node eff in
       let* _ :=
         if decide (n_freshness d'' = Check)
         then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
         else m_return tt in
       let* d''' := get_node eff in
       let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
       let* _ := if decide (n_freshness d''' = Dirty) then b1_run_effect f eff else m_return tt in
       let* _ := b1_drain f in
       m_modify (with_flushing false)) sf0).
    { intros f. unfold fused_single_memo_effect.
      rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
      rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
      rewrite (m_bind_modify_node _ _ _ _ _ Ha_memo).
      assert (Hmem_eff : g_nodes (set_node memo (node_marked Dirty (n_unannounced dm) (n_queued dm) dm) sa) !! eff = Some de)
        by (unfold set_node; simpl; rewrite lookup_insert_ne by congruence; exact Ha_eff).
      rewrite (m_bind_modify_node _ _ _ _ _ Hmem_eff).
      fold dm' de'. rewrite m_bind_modify. reflexivity. }
    assert (Hrel_0 : queue_rel sf0 sw0)
      by (unfold sf0, sw0, sa_enq; apply queue_rel_fused_memo; rewrite Ha_qhd, Ha_q; exact Hqhd).
    assert (Hstl_dm : stale_live dm' = true) by (unfold stale_live, dm'; simpl; by rewrite Hdzm).
    assert (Hsw0_memo : g_nodes sw0 !! memo = Some dm')
      by (unfold sw0, sa_enq, enqueue, set_node; simpl; rewrite Ha_nodes, lookup_insert_ne by congruence; rewrite lookup_insert_eq; reflexivity).
    assert (Hsw0_eff : g_nodes sw0 !! eff = Some de')
      by (unfold sw0, sa_enq, enqueue, set_node; simpl; rewrite lookup_insert_eq; reflexivity).
    assert (Hsw0_sl : source_slots (sources_of (g_edges sw0) eff) !! 0 = Some (memo, h_e))
      by (unfold sw0, sa_enq, enqueue, set_node; simpl; rewrite Ha_edges; exact Hsl0).
    assert (Hsw0_def : deferring_b1 sw0 = true) by (unfold deferring_b1, sw0; simpl; reflexivity).
    assert (Hlq_enq : live_queue sa_enq = true)
      by (unfold live_queue, sa_enq, enqueue, set_node; simpl; rewrite Ha_q, Ha_qhd, Hqhd; apply bool_decide_eq_true; rewrite length_app; simpl; lia).
    assert (Hbd_enq : g_batch_depth sa_enq = 0)
      by (unfold sa_enq, enqueue, set_node; simpl; rewrite Ha_bd; exact Hbd).
    assert (Hob_enq : outside_bodies sa_enq = true)
      by (unfold outside_bodies, sa_enq, enqueue, set_node; simpl; by rewrite Ha_pd, Ha_cur, Ha_unt, Hpd, Hcur, Hunt).
    assert (Hfl_enq : g_flushing sa_enq = false)
      by (unfold sa_enq, enqueue, set_node; simpl; rewrite Ha_fl; exact Hfl).
    assert (Hlu_enq : (g_queue sa ++ [eff]) !! g_queue_head sa = Some eff)
      by (rewrite Ha_q, Ha_qhd, Hqhd, lookup_app_r, Nat.sub_diag by lia; reflexivity).
    assert (Hstep8 : forall k,
      b1_write_signal (8 + k) src v s =
      (let* _ := b1_refresh k memo in
       let* _ := get_node memo in
       let* _ := m_return tt in
       let* d' := get_node eff in
       let* d_m := get_node memo in
       let* s1 := m_get in
       let* _ :=
         if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running s1 \/ memo ∈ g_checking s1))
         then m_step (mark_dirty (2 + k) eff) else m_return tt in
       let* _ := b1_resolve_walk (2 + k) eff 1 in
       let* d'' := get_node eff in
       let* _ :=
         if decide (n_freshness d'' = Check)
         then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
         else m_return tt in
       let* d''' := get_node eff in
       let* _ := modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) in
       let* _ := if decide (n_freshness d''' = Dirty) then b1_run_effect (3 + k) eff else m_return tt in
       let* _ := b1_drain (4 + k) in
       m_modify (with_flushing false)) sw0).
    { intros k. change (8 + k) with (S (S (S (S (S (S (S (S k)))))))).
      cbn [b1_write_signal].
      rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
      rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
      change (S (S (S (S (S (S (S k))))))) with (S (S (5 + k))).
      rewrite (m_bind_step _ _ _ _ (Hmark (5 + k))).
      cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
      rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
      cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
      change (5 + k) with (S (4 + k)).
      cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
      change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
              g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
        with ((g_queue sa ++ [eff]) !! g_queue_head sa).
      rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify.
      change (g_queue_head (with_flush_owed false (with_flushing true sa_enq))) with (g_queue_head sa_enq).
      fold sw0.
      change (4 + k) with (S (3 + k)) at 1.
      cbn [b1_execute_effect]. rewrite ?b4_bind_assoc.
      rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
      change (n_disposed de') with (n_disposed de).
      change (n_freshness de') with Check.
      rewrite Hdze, decide_True by reflexivity.
      rewrite ?b4_bind_assoc.
      change (3 + k) with (S (2 + k)) at 1.
      cbn [b1_resolve_walk]. rewrite ?b4_bind_assoc.
      rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
      rewrite m_bind_get.
      change (n_freshness de') with Check.
      rewrite decide_True by reflexivity.
      rewrite Hsw0_sl, ?b4_bind_assoc.
      change (2 + k) with (S (1 + k)) at 1.
      cbn [b1_update_if_necessary]. rewrite ?b4_bind_assoc.
      rewrite (m_bind_get_node _ _ _ _ Hsw0_memo).
      change (n_kind dm') with (n_kind dm).
      rewrite Hkm, Hstl_dm, ?b4_bind_assoc.
      change (1 + k) with (S k).
      cbn [b1_refresh_or_pull]. rewrite ?b4_bind_assoc.
      rewrite m_bind_get, Hsw0_def, ?b4_bind_assoc.
      repeat (first [ rewrite b4_bind_assoc | apply b4_bind_ext; intros ? ? ]).
      reflexivity. }
    split.
    + intros [f Hf]. rewrite Hpre_f in Hf.
      eapply bind_inv in Hf as (sf1 & [] & Href & Hrest).
      eapply bind_inv in Hrest as (sf1' & d_m0 & Hgetm & Hrest).
      eapply bind_inv in Hrest as (sf2 & d' & Hget_d' & Hrest).
      eapply bind_inv in Hrest as (sf3 & d_m & Hget_dm & Hrest).
      eapply bind_inv in Hrest as (sf4 & s1_ & Hget_s1 & Hrest).
      eapply bind_inv in Hrest as (sf5 & [] & Hmd & Hrest).
      eapply bind_inv in Hrest as (sf6 & [] & Hrw & Hrest).
      eapply bind_inv in Hrest as (sf7 & d'' & Hget_d'' & Hrest).
      eapply bind_inv in Hrest as (sf8 & [] & Hres & Hrest).
      eapply bind_inv in Hrest as (sf9 & d''' & Hget_d''' & Hrest).
      eapply bind_inv in Hrest as (sf10 & [] & Hclr & Hrest).
      eapply bind_inv in Hrest as (sf11 & [] & Hre & Hrest).
      eapply bind_inv in Hrest as (sf12 & [] & Hdr & Hfl_end).
      unfold m_modify in Hfl_end. simplify_eq.
      assert (Href' : b1_refresh f memo sf0 = Some (sf1, tt)) by exact Href.
      destruct (B1Qrefresh _ (b1_qrel_all f) memo _ _ _ _ Href' Hrel_0) as (sw1 & Href_w & Hrel1).
      destruct (qrel_get_node memo _ _ _ _ Hgetm Hrel1) as (sw1' & Hgetm_w & Hrel1').
      destruct (qrel_get_node eff _ _ _ _ Hget_d' Hrel1') as (sw2 & Hget_d'_w & Hrel2).
      destruct (qrel_get_node memo _ _ _ _ Hget_dm Hrel2) as (sw3 & Hget_dm_w & Hrel3).
      unfold m_get in Hget_s1. injection Hget_s1 as <- <-.
      assert (Hmd_w : exists sw5,
        (if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running sw3 \/ memo ∈ g_checking sw3))
         then m_step (mark_dirty (2 + f) eff) else m_return tt) sw3 = Some (sw5, tt) /\
        queue_rel sf5 sw5).
      { destruct (queue_rel_fields _ _ Hrel3) as (_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
        rewrite <- Hrun, <- Hchk.
        destruct (decide _); [|unfold m_return in Hmd; simplify_eq; by exists sw3].
        unfold m_step in Hmd |- *. destruct (mark_dirty f eff sf3) eqn:Em; simplify_eq.
        assert (Em' : mark_dirty (2 + f) eff sf3 = Some sf5) by (eapply (mark_dirty_mono f); [lia|exact Em]).
        destruct (qrel_mark_dirty _ _ _ _ _ Hrel3 Em') as (sw5 & Em2 & Hrel5). rewrite Em2. by exists sw5. }
      destruct Hmd_w as (sw5 & Hmd_w & Hrel5).
      assert (Hrw' : b1_resolve_walk (2 + f) eff 1 sf5 = Some (sf6, tt))
        by (eapply (B1Mwalk _ (b1_mono_all f)); [lia|exact Hrw]).
      destruct (B1Qwalk _ (b1_qrel_all (2 + f)) eff 1 _ _ _ _ Hrw' Hrel5) as (sw6 & Hrw_w & Hrel6).
      destruct (qrel_get_node eff _ _ _ _ Hget_d'' Hrel6) as (sw7 & Hget_d''_w & Hrel7).
      assert (Hres_w : exists sw8,
        (if decide (n_freshness d'' = Check)
         then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
         else m_return tt) sw7 = Some (sw8, tt) /\ queue_rel sf8 sw8).
      { destruct (decide (n_freshness d'' = Check)); [|unfold m_return in Hres; simplify_eq; by exists sw7].
        eapply (qrel_bind_of Hres Hrel7); [intros ????; qrel_pure_tac|intros [] ????; qrel_pure_tac]. }
      destruct Hres_w as (sw8 & Hres_w & Hrel8).
      destruct (qrel_get_node eff _ _ _ _ Hget_d''' Hrel8) as (sw9 & Hget_d'''_w & Hrel9).
      destruct (qrel_modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) _ _ _ _ Hclr Hrel9) as (sw10 & Hclr_w & Hrel10).
      assert (Hre_w : exists sw11,
        (if decide (n_freshness d''' = Dirty) then b1_run_effect (3 + f) eff else m_return tt) sw10 = Some (sw11, tt) /\
        queue_rel sf11 sw11).
      { destruct (decide (n_freshness d''' = Dirty)); [|unfold m_return in Hre; simplify_eq; by exists sw10].
        assert (Hre' : b1_run_effect (3 + f) eff sf10 = Some (sf11, tt))
          by (eapply (B1Mrun_effect _ (b1_mono_all f)); [lia|exact Hre]).
        by eapply (B1Qrun_effect _ (b1_qrel_all _)). }
      destruct Hre_w as (sw11 & Hre_w & Hrel11).
      assert (Hdr' : b1_drain (4 + f) sf11 = Some (sf12, tt))
        by (eapply (B1Mdrain _ (b1_mono_all f)); [lia|exact Hdr]).
      destruct (B1Qdrain _ (b1_qrel_all _) _ _ _ _ Hdr' Hrel11) as (sw12 & Hdr_w & Hrel12).
      destruct (b1_drain_resets _ _ _ Hdr') as [Hq1 Hh1].
      destruct (b1_drain_resets _ _ _ Hdr_w) as [Hq2 Hh2].
      assert (Heq : sf12 = sw12) by (apply queue_rel_drained_eq; assumption).
      subst sw12.
      exists (8 + f). rewrite Hstep8.
      unfold m_bind at 1. rewrite Href_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hgetm_w. cbn [option_bind].
      rewrite b4_bind_ret_l.
      unfold m_bind at 1. rewrite Hget_d'_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_dm_w. cbn [option_bind].
      rewrite m_bind_get.
      unfold m_bind at 1. rewrite Hmd_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hrw_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_d''_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hres_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_d'''_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hclr_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hre_w. cbn [option_bind].
      unfold m_bind at 1. rewrite Hdr_w. cbn [option_bind].
      reflexivity.
    + intros [f Hf].
      assert (Hunder9 : f < 9 -> b1_write_signal f src v s = None).
      { intros Hlt.
        destruct f as [|[|[|[|[|[|[|[|[|f']]]]]]]]]; try lia.
        - reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          unfold m_bind, m_step, notify_dirty. erewrite notify_with_singleton by exact Ha_obs_m.
          cbn [walk_marks mark_dirty]. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          unfold m_bind, m_step, notify_dirty. erewrite notify_with_singleton by exact Ha_obs_m.
          cbn [walk_marks mark_dirty]. rewrite Ha_memo, Hkm, decide_True by exact Hcm.
          erewrite notify_with_singleton by exact Ha_obs_e.
          cbn [walk_marks mark_check]. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 0)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 1)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
          change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
                  g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
            with ((g_queue sa ++ [eff]) !! g_queue_head sa).
          rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 2)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
          change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
                  g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
            with ((g_queue sa ++ [eff]) !! g_queue_head sa).
          rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify.
      change (g_queue_head (with_flush_owed false (with_flushing true sa_enq))) with (g_queue_head sa_enq).
      fold sw0.
          cbn [b1_execute_effect]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
          change (n_disposed de') with (n_disposed de).
          change (n_freshness de') with Check.
          rewrite Hdze, decide_True by reflexivity.
          rewrite ?b4_bind_assoc. reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 3)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
          change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
                  g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
            with ((g_queue sa ++ [eff]) !! g_queue_head sa).
          rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify.
      change (g_queue_head (with_flush_owed false (with_flushing true sa_enq))) with (g_queue_head sa_enq).
      fold sw0.
          cbn [b1_execute_effect]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
          change (n_disposed de') with (n_disposed de).
          change (n_freshness de') with Check.
          rewrite Hdze, decide_True by reflexivity.
          rewrite ?b4_bind_assoc.
          cbn [b1_resolve_walk]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
          rewrite m_bind_get. change (n_freshness de') with Check.
          rewrite decide_True by reflexivity. rewrite Hsw0_sl, ?b4_bind_assoc.
          reflexivity.
        - cbn [b1_write_signal]. rewrite (m_bind_get_node _ _ _ _ Hsrc). rewrite Hks, decide_False by exact Hnev.
          rewrite (m_bind_modify_node _ _ _ _ _ Hsrc). rewrite m_bind_modify. fold sa.
          rewrite (m_bind_step _ _ _ _ (Hmark 4)).
          cbn [b1_request_flush]. rewrite m_bind_get. fold dm' de'. fold sa_enq.
          rewrite Hlq_enq, Hbd_enq, Hob_enq, bool_decide_nat_0. cbn [andb].
          cbn [b1_run_flush]. rewrite m_bind_get, Hfl_enq, m_bind_modify.
          cbn [b1_drain]. rewrite ?b4_bind_assoc, m_bind_get.
          change (g_queue (with_flush_owed false (with_flushing true sa_enq)) !!
                  g_queue_head (with_flush_owed false (with_flushing true sa_enq)))
            with ((g_queue sa ++ [eff]) !! g_queue_head sa).
          rewrite Hlu_enq, ?b4_bind_assoc, m_bind_modify.
      change (g_queue_head (with_flush_owed false (with_flushing true sa_enq))) with (g_queue_head sa_enq).
      fold sw0.
          cbn [b1_execute_effect]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
          change (n_disposed de') with (n_disposed de).
          change (n_freshness de') with Check.
          rewrite Hdze, decide_True by reflexivity.
          rewrite ?b4_bind_assoc.
          cbn [b1_resolve_walk]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_eff), ?b4_bind_assoc.
          rewrite m_bind_get. change (n_freshness de') with Check.
          rewrite decide_True by reflexivity. rewrite Hsw0_sl, ?b4_bind_assoc.
          cbn [b1_update_if_necessary]. rewrite ?b4_bind_assoc.
          rewrite (m_bind_get_node _ _ _ _ Hsw0_memo).
          change (n_kind dm') with (n_kind dm).
          rewrite Hkm, Hstl_dm, ?b4_bind_assoc.
          reflexivity.
        - change 8 with (8 + 0). rewrite Hstep8. reflexivity. }
      destruct f as [|[|[|[|[|[|[|[|[|f']]]]]]]]];
        [rewrite Hunder9 in Hf by lia; discriminate..|].
      replace (S (S (S (S (S (S (S (S (S f'))))))))) with (8 + S f') in Hf by lia.
      rewrite Hstep8 in Hf.
      eapply bind_inv in Hf as (sw1 & [] & Href_w & Hrest).
      eapply bind_inv in Hrest as (sw1' & d_m0 & Hgetm_w & Hrest).
      rewrite b4_bind_ret_l in Hrest.
      eapply bind_inv in Hrest as (sw2 & d' & Hget_d'_w & Hrest).
      eapply bind_inv in Hrest as (sw3 & d_m & Hget_dm_w & Hrest).
      eapply bind_inv in Hrest as (sw4 & s1_ & Hget_s1_w & Hrest).
      eapply bind_inv in Hrest as (sw5 & [] & Hmd_w & Hrest).
      eapply bind_inv in Hrest as (sw6 & [] & Hrw_w & Hrest).
      eapply bind_inv in Hrest as (sw7 & d'' & Hget_d''_w & Hrest).
      eapply bind_inv in Hrest as (sw8 & [] & Hres_w & Hrest).
      eapply bind_inv in Hrest as (sw9 & d''' & Hget_d'''_w & Hrest).
      eapply bind_inv in Hrest as (sw10 & [] & Hclr_w & Hrest).
      eapply bind_inv in Hrest as (sw11 & [] & Hre_w & Hrest).
      eapply bind_inv in Hrest as (sw12 & [] & Hdr_w & Hfl_end).
      unfold m_modify in Hfl_end. simplify_eq.
      pose proof (queue_rel_sym _ _ Hrel_0) as Hrel_0'.
      set (fmax := S (S (S (S (S (S f')))))).
      assert (Href_w' : b1_refresh fmax memo sw0 = Some (sw1, tt))
        by (eapply (B1Mrefresh _ (b1_mono_all (S f'))); [lia|exact Href_w]).
      destruct (B1Qrefresh _ (b1_qrel_all fmax) memo _ _ _ _ Href_w' Hrel_0') as (sf1 & Href_f & Hrel1).
      destruct (qrel_get_node memo _ _ _ _ Hgetm_w Hrel1) as (sf1' & Hgetm_f & Hrel1').
      destruct (qrel_get_node eff _ _ _ _ Hget_d'_w Hrel1') as (sf2 & Hget_d'_f & Hrel2).
      destruct (qrel_get_node memo _ _ _ _ Hget_dm_w Hrel2) as (sf3 & Hget_dm_f & Hrel3).
      unfold m_get in Hget_s1_w. injection Hget_s1_w as <- <-.
      assert (Hmd_f : exists sf5,
        (if decide (n_freshness d' = Check /\ stale_live d_m = true /\ (memo ∈ g_running sf3 \/ memo ∈ g_checking sf3))
         then m_step (mark_dirty fmax eff) else m_return tt) sf3 = Some (sf5, tt) /\
        queue_rel sw5 sf5).
      { destruct (queue_rel_fields _ _ Hrel3) as (_&_&_&_&_&_&_&_&_&_&_&Hrun&Hchk&_).
        rewrite <- Hrun, <- Hchk.
        destruct (decide _); [|unfold m_return in Hmd_w; simplify_eq; by exists sf3].
        unfold m_step in Hmd_w |- *. destruct (mark_dirty (2 + S f') eff sw3) eqn:Em; simplify_eq.
        assert (Em' : mark_dirty fmax eff sw3 = Some sw5) by (eapply (mark_dirty_mono (2 + S f')); [lia|exact Em]).
        destruct (qrel_mark_dirty _ _ _ _ _ Hrel3 Em') as (sf5 & Em2 & Hrel5). rewrite Em2. by exists sf5. }
      destruct Hmd_f as (sf5 & Hmd_f & Hrel5).
      assert (Hrw_w' : b1_resolve_walk fmax eff 1 sw5 = Some (sw6, tt))
        by (eapply (B1Mwalk _ (b1_mono_all (2 + S f'))); [lia|exact Hrw_w]).
      destruct (B1Qwalk _ (b1_qrel_all fmax) eff 1 _ _ _ _ Hrw_w' Hrel5) as (sf6 & Hrw_f & Hrel6).
      destruct (qrel_get_node eff _ _ _ _ Hget_d''_w Hrel6) as (sf7 & Hget_d''_f & Hrel7).
      assert (Hres_f : exists sf8,
        (if decide (n_freshness d'' = Check)
         then let* _ := m_modify (ghost_resolved eff) in modify_node eff (node_with_freshness Clean)
         else m_return tt) sf7 = Some (sf8, tt) /\ queue_rel sw8 sf8).
      { destruct (decide (n_freshness d'' = Check)); [|unfold m_return in Hres_w; simplify_eq; by exists sf7].
        eapply (qrel_bind_of Hres_w Hrel7); [intros ????; qrel_pure_tac|intros [] ????; qrel_pure_tac]. }
      destruct Hres_f as (sf8 & Hres_f & Hrel8).
      destruct (qrel_get_node eff _ _ _ _ Hget_d'''_w Hrel8) as (sf9 & Hget_d'''_f & Hrel9).
      destruct (qrel_modify_node eff (fun x => node_with_freshness Clean (node_with_queued false x)) _ _ _ _ Hclr_w Hrel9) as (sf10 & Hclr_f & Hrel10).
      assert (Hre_f : exists sf11,
        (if decide (n_freshness d''' = Dirty) then b1_run_effect fmax eff else m_return tt) sf10 = Some (sf11, tt) /\
        queue_rel sw11 sf11).
      { destruct (decide (n_freshness d''' = Dirty)); [|unfold m_return in Hre_w; simplify_eq; by exists sf10].
        assert (Hre_w' : b1_run_effect fmax eff sw10 = Some (sw11, tt))
          by (eapply (B1Mrun_effect _ (b1_mono_all (3 + S f'))); [lia|exact Hre_w]).
        by eapply (B1Qrun_effect _ (b1_qrel_all fmax)). }
      destruct Hre_f as (sf11 & Hre_f & Hrel11).
      assert (Hdr_w' : b1_drain fmax sw11 = Some (sw12, tt))
        by (eapply (B1Mdrain _ (b1_mono_all (4 + S f'))); [lia|exact Hdr_w]).
      destruct (B1Qdrain _ (b1_qrel_all fmax) _ _ _ _ Hdr_w' Hrel11) as (sf12 & Hdr_f & Hrel12).
      destruct (b1_drain_resets _ _ _ Hdr_w') as [Hq1 Hh1].
      destruct (b1_drain_resets _ _ _ Hdr_f) as [Hq2 Hh2].
      assert (Heq : sw12 = sf12) by (apply queue_rel_drained_eq; assumption).
      subst sf12.
      exists fmax. rewrite Hpre_f. try fold sf0.
      unfold m_bind at 1. rewrite Href_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hgetm_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_d'_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_dm_f. cbn [option_bind].
      rewrite m_bind_get.
      unfold m_bind at 1. rewrite Hmd_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hrw_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_d''_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hres_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hget_d'''_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hclr_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hre_f. cbn [option_bind].
      unfold m_bind at 1. rewrite Hdr_f. cbn [option_bind].
      reflexivity.
Qed.

(** * B6: Published ready-memo outcome cutoff refinement *)

Definition outcome_moved
    (prev_st : status) (prev_fail : option val) (prev_val : val)
    (next_st : status) (next_fail : option val) (next_val : val)
    (cmp : comparer) (n : node_id) : M bool :=
  if decide (next_st <> prev_st \/ next_fail <> prev_fail) then m_return true
  else
    match cmp with
    | CmpEq => m_return (bool_decide (next_val <> prev_val))
    | CmpNever => m_return true
    | CmpThrows y =>
      let* _ := modify_node n (fun x =>
        node_with_status StError (node_with_failure (Some y) (node_with_value prev_val x))) in
      m_return true
    end.

Definition ready_value_moved
    (prev_val next_val : val) (cmp : comparer) (n : node_id) : M bool :=
  match cmp with
  | CmpEq => m_return (bool_decide (next_val <> prev_val))
  | CmpNever => m_return true
  | CmpThrows y =>
    let* _ := modify_node n (fun x =>
      node_with_status StError (node_with_failure (Some y) (node_with_value prev_val x))) in
    m_return true
  end.

(**md
**When previous and next memo states are both ready (`StNone`, `failure = None`), `outcome_moved` simplifies directly to value comparison without status or failure checks.**

Refines `RunTopCore` / `Memo.Recompute` steady-state cutoff on ready-published memos: the status/failure difference guard is identically false, reducing `outcome_moved` to `ready_value_moved` (and for `CmpEq`, to `bool_decide (next_val <> prev_val)`).

- [prev_st:] previous memo status.
- [prev_fail:] previous memo failure.
- [prev_val:] previous memo value.
- [next_st:] newly computed memo status.
- [next_fail:] newly computed memo failure.
- [next_val:] newly computed memo value.
- [cmp:] memo equality comparer.
- [n:] memo node id.
- [s:] engine state.
*)
Theorem memo_published_ready_cutoff_eq
    prev_st prev_fail prev_val next_st next_fail next_val cmp n s :
  prev_st = StNone ->
  prev_fail = None ->
  next_st = StNone ->
  next_fail = None ->
  outcome_moved prev_st prev_fail prev_val next_st next_fail next_val cmp n s =
    ready_value_moved prev_val next_val cmp n s /\
  (cmp = CmpEq ->
    outcome_moved prev_st prev_fail prev_val next_st next_fail next_val cmp n s =
      Some (s, bool_decide (next_val <> prev_val))).
Proof.
  intros -> -> -> ->. unfold outcome_moved, ready_value_moved.
  rewrite decide_False by (intros [H|H]; exact (H eq_refl)).
  split; [reflexivity|].
  intros ->. reflexivity.
Qed.
