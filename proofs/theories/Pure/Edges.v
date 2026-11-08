From stdpp Require Import prelude gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Pure Require Import ObserverSet SourceList State.

(** Slot [i] of [c] holds [(s, Some h)] exactly when position [h] of [s]'s observers holds [(c, i)].
    A coherent world can hold two edges per source, as [aba_world] does. [observer_held_once] gives
    the [held_once] of [walk_covers]. *)
Definition coherent (w : EdgeWorld) : Prop := edge_match w.

Lemma coherent_empty : coherent empty_world.
Proof.
  intros c i s h. unfold slot_at, obs_of, src_of. simpl. rewrite !lookup_empty. simpl.
  rewrite !lookup_nil. done.
Qed.

Theorem coherent_begin c w : coherent w -> coherent (sl_begin c w).
Proof.
  intros Hm c' i s h. unfold sl_begin. rewrite slot_at_set_src, obs_of_set_src.
  destruct (decide (c' = c)) as [->|]; apply Hm.
Qed.

Theorem coherent_add self s w :
  coherent w -> exists w', sl_add self s w = Some w' /\ coherent w'.
Proof.
  intros Hm. unfold sl_add.
  destruct (decide (fst <$> sslots (src_of w self) !! scursor (src_of w self) = Some s)) as [Hp|Hp].
  - eexists. split; [reflexivity|]. intros c i s' h.
    rewrite slot_at_advance, obs_of_set_src. apply Hm.
  - destruct (decide (0 < scursor (src_of w self) /\
                      fst <$> sslots (src_of w self) !! pred (scursor (src_of w self)) = Some s));
      [by eexists|].
    destruct (sl_end_ok self w Hm) as (w1 & -> & Hm1 & _). simpl.
    eexists. split; [reflexivity|]. by apply edge_match_link.
Qed.

Theorem coherent_end self w :
  coherent w -> exists w', sl_end self w = Some w' /\ coherent w'.
Proof. intros Hm. destruct (sl_end_ok self w Hm) as (w' & Hr & Hm' & _). eauto. Qed.

Theorem coherent_clear self w :
  coherent w -> exists w', sl_clear self w = Some w' /\ coherent w'.
Proof. intros Hm. destruct (sl_clear_ok self w Hm) as (w' & Hr & Hm' & _). eauto. Qed.

(** Removing a slot's edge returns a coherent world. When the removal moves another observer, that
    world is the removal, the move and the blanked slot, and the moved slot holds its new handle. *)
Theorem coherent_moved self i s h w :
  coherent w ->
  slot_at w self i = Some (s, Some h) ->
  exists w', sl_remove_slot self i w = Some w' /\ coherent w' /\
    forall o' m ms h',
      obs_remove_at self h (obs_of w s) = Some (o', Some (m, ms, h')) ->
      w' = set_handle self i None (sl_moved_in m ms h' (set_obs s o' w)) /\
      slot_at w' m ms = Some (s, Some h').
Proof.
  intros Hm Hs.
  destruct (remove_step self i s h w Hm Hs) as (w' & Hr & Hm' & _).
  exists w'. split_and!; [done|done|].
  intros o' m ms h' Hrm.
  unfold sl_remove_slot, remove_edge in Hr. rewrite Hs, Hrm in Hr. simpl in Hr. simplify_eq.
  split; [done|].
  destruct (obs_remove_at_spec _ _ _ _ _ Hrm)
    as (_ & _ & _ & [[_ ?]|(m' & ms' & _ & _ & Hn & Hhole)]); [done|].
  simplify_eq.
  apply Hm'. unfold sl_moved_in. rewrite !obs_of_set_handle, obs_of_set_obs, decide_True by done.
  done.
Qed.

(** A move to a different handle, with no removal, breaks coherence. *)
Theorem moved_alone_breaks_coherent c slot s h h' w :
  coherent w ->
  slot_at w c slot = Some (s, Some h) ->
  h <> h' ->
  ~ coherent (sl_moved_in c slot h' w).
Proof.
  intros Hm Hs Hne Hm'.
  assert (Hmoved : slot_at (sl_moved_in c slot h' w) c slot = Some (s, Some h')).
  { unfold sl_moved_in. rewrite slot_at_set_handle, decide_True by done. rewrite Hs. done. }
  apply Hm' in Hmoved. unfold sl_moved_in in Hmoved. rewrite obs_of_set_handle in Hmoved.
  apply Hm in Hmoved. rewrite Hs in Hmoved. simplify_eq.
Qed.

(** In a coherent world either side of an edge implies the other. *)
Theorem edge_iff_both_sides s src c :
  coherent (g_edges s) ->
  (edge s src c <-> c ∈ obs_view (obs_of (g_edges s) src)) /\
  (edge s src c <-> exists i h, slot_at (g_edges s) c i = Some (src, Some h)).
Proof.
  intros Hm. unfold edge. split.
  - rewrite view_elem. split.
    + intros (i & h & _ & Hp). eauto.
    + intros (h & i & Hp). exists i, h. split; [by apply Hm|done].
  - split.
    + intros (i & h & Hs & _). eauto.
    + intros (i & h & Hs). exists i, h. split; [done|by apply Hm].
Qed.
