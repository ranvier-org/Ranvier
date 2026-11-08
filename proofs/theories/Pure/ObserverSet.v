From stdpp Require Import prelude.
From Ranvier.Base Require Import Prelude.

(** [EdgeHandle.ScanLimit]. *)
Definition scan_limit : nat := 8.

(** An observer set. [oitems] holds each observer with the slot passed back in its [MovedIn], and a
    position is an edge's handle. *)
Record obs := { oitems : list (nid * nat); olatest : nat }.

Definition empty_obs : obs := {| oitems := []; olatest := 0 |}.

(** [TUnlinked] and [TUnknown] are [EdgeHandle.Unlinked] and [EdgeHandle.Unknown]. *)
Inductive tried := TAdded (h : nat) | TUnlinked | TUnknown.

Definition obs_view (o : obs) : list nid := fst <$> oitems o.

Definition obs_add (c : nid) (slot : nat) (o : obs) : obs * nat :=
  let h := length (oitems o) in
  ({| oitems := oitems o ++ [(c, slot)]; olatest := h |}, h).

Definition obs_try_add (c : nid) (slot : nat) (o : obs) : obs * tried :=
  let add := fun _ : unit => let '(o', h) := obs_add c slot o in (o', TAdded h) in
  if decide (length (oitems o) = 0) then add ()
  else if decide (fst <$> oitems o !! olatest o = Some c) then (o, TUnlinked)
  else if decide (length (oitems o) <= scan_limit) then
    if decide (c ∈ obs_view o) then (o, TUnlinked) else add ()
  else (o, TUnknown).

(** Swap-removal. The result's notice is (observer, slot, new handle) for the observer moved. [None]
    as the whole result is the [InvalidOperationException] of a handle holding another observer. *)
Definition obs_remove_at (c : nid) (h : nat) (o : obs) : option (obs * option (nid * nat * nat)) :=
  let last := pred (length (oitems o)) in
  match oitems o !! h with
  | Some (c', _) =>
    if decide (c' = c) then
      if decide (h = last) then
        Some ({| oitems := take last (oitems o); olatest := olatest o |}, None)
      else
        match oitems o !! last with
        | Some (m, ms) =>
          Some ({| oitems := <[h := (m, ms)]> (take last (oitems o));
                   olatest := if decide (olatest o = last) then h else olatest o |},
                Some (m, ms, h))
        | None => None
        end
    else None
  | None => None
  end.

(** A mutation of the observer set between two visits of a walk. *)
Inductive walk_mut := WAdd (c : nid) (slot : nat) | WRemove (c : nid) (h : nat).

Definition apply_mut (m : walk_mut) (o : obs) : option obs :=
  match m with
  | WAdd c slot => Some (obs_add c slot o).1
  | WRemove c h => fst <$> obs_remove_at c h o
  end.

Fixpoint apply_muts (ms : list walk_mut) (o : obs) : option obs :=
  match ms with
  | [] => Some o
  | m :: r => apply_mut m o ≫= apply_muts r
  end.

(** The walk of [NotifyDirty], [NotifyDirtyExcept] and [NotifyCheck]. The first argument is the
    cursor plus one, and starts as the observer count. Each step re-clamps it to the count, visits the
    observer below it, then applies that visit's mutations. *)
Inductive walk : nat -> obs -> list (list walk_mut) -> list nid -> obs -> Prop :=
  | walk_end n o :
      min n (length (oitems o)) = 0 ->
      walk n o [] [] o
  | walk_visit n i o c slot ms o1 rest vis o' :
      min n (length (oitems o)) = S i ->
      oitems o !! i = Some (c, slot) ->
      apply_muts ms o = Some o1 ->
      walk i o1 rest vis o' ->
      walk n o (ms :: rest) (c :: vis) o'.

(** The observer set at every point of a mutation sequence: the start, then after each mutation. *)
Fixpoint mut_points (ms : list walk_mut) (o : obs) : list obs :=
  o :: match ms with
       | [] => []
       | m :: r =>
         match apply_mut m o with
         | Some o1 => mut_points r o1
         | None => []
         end
       end.

(** [c] occupies exactly one position. *)
Definition held_once (c : nid) (o : obs) : Prop :=
  c ∈ obs_view o /\ forall i j, obs_view o !! i = Some c -> obs_view o !! j = Some c -> i = j.

Lemma view_lookup o i c : obs_view o !! i = Some c <-> exists s, oitems o !! i = Some (c, s).
Proof.
  unfold obs_view. rewrite list_lookup_fmap. split.
  - destruct (oitems o !! i) as [[c' s]|]; simpl; intros H; simplify_eq; eauto.
  - intros [s ->]. reflexivity.
Qed.

Lemma view_elem o c : c ∈ obs_view o <-> exists i s, oitems o !! i = Some (c, s).
Proof.
  rewrite list_elem_of_lookup. split.
  - intros [i Hi]. apply view_lookup in Hi as [s Hs]. eauto.
  - intros (i & s & Hs). exists i. apply view_lookup. eauto.
Qed.

(** ** Add and try-add *)

Theorem obs_add_position c slot o :
  (obs_add c slot o).2 = length (oitems o) /\
  oitems (obs_add c slot o).1 !! length (oitems o) = Some (c, slot) /\
  obs_view (obs_add c slot o).1 = obs_view o ++ [c] /\
  olatest (obs_add c slot o).1 = length (oitems o).
Proof.
  unfold obs_add, obs_view. simpl. rewrite fmap_app. split_and!; try done.
  rewrite lookup_app_r by lia. rewrite Nat.sub_diag. done.
Qed.

(** Every answer of [obs_try_add], with its effect. *)
Lemma obs_try_add_spec c slot o :
  (obs_try_add c slot o = (o, TUnlinked) /\ c ∈ obs_view o) \/
  (exists h, obs_try_add c slot o = ((obs_add c slot o).1, TAdded h) /\
             h = (obs_add c slot o).2 /\ c ∉ obs_view o) \/
  (obs_try_add c slot o = (o, TUnknown) /\ scan_limit < length (oitems o) /\
   fst <$> oitems o !! olatest o <> Some c).
Proof.
  unfold obs_try_add. destruct (decide (length (oitems o) = 0)) as [H0|H0].
  { right; left. eexists. split_and!; [reflexivity|reflexivity|].
    rewrite view_elem. intros (i & s & Hi). apply lookup_lt_Some in Hi. lia. }
  destruct (decide (fst <$> oitems o !! olatest o = Some c)) as [Hl|Hl].
  { left. split; [done|]. rewrite view_elem.
    destruct (oitems o !! olatest o) as [[c' s]|] eqn:E; simplify_eq/=. eauto. }
  destruct (decide (length (oitems o) <= scan_limit)) as [Hs|Hs].
  - destruct (decide (c ∈ obs_view o)) as [Hc|Hc]; [by left|].
    right; left. eexists. split_and!; [reflexivity|reflexivity|done].
  - right; right. split_and!; [done|lia|done].
Qed.

Theorem obs_try_add_cases c slot o :
  length (oitems o) <= scan_limit ->
  (obs_try_add c slot o).2 <> TUnknown /\
  ((obs_try_add c slot o).2 = TUnlinked <-> c ∈ obs_view o).
Proof.
  intros Hlen.
  destruct (obs_try_add_spec c slot o) as [[-> Hc]|[(h & -> & _ & Hc)|(_ & Hb & _)]]; simpl.
  - split; [done|tauto].
  - split; [done|]. split; [done|tauto].
  - unfold scan_limit in *. lia.
Qed.

(** ** Remove *)

Theorem obs_remove_mismatch c h o :
  fst <$> oitems o !! h <> Some c -> obs_remove_at c h o = None.
Proof.
  unfold obs_remove_at. destruct (oitems o !! h) as [[c' s]|]; simpl; intros Hne; [|done].
  rewrite decide_False; [done|]. congruence.
Qed.

(** The last observer fills the hole, and the notice names it. Removing the last gives no notice. *)
Theorem obs_remove_at_swap c s m ms pre mid latest :
  obs_remove_at c (length pre)
    {| oitems := pre ++ (c, s) :: mid ++ [(m, ms)]; olatest := latest |} =
    Some ({| oitems := pre ++ (m, ms) :: mid;
             olatest := if decide (latest = length pre + S (length mid))
                        then length pre else latest |},
          Some (m, ms, length pre)) /\
  obs_remove_at c (length pre) {| oitems := pre ++ [(c, s)]; olatest := latest |} =
    Some ({| oitems := pre; olatest := latest |}, None).
Proof.
  split.
  - unfold obs_remove_at. simpl.
    assert (Hlen : pred (length (pre ++ (c, s) :: mid ++ [(m, ms)])) = length pre + S (length mid)).
    { rewrite !length_app. simpl. rewrite length_app. simpl. lia. }
    rewrite Hlen.
    rewrite lookup_app_r, Nat.sub_diag by lia. simpl.
    rewrite decide_True by done. rewrite decide_False by lia.
    rewrite lookup_app_r by lia.
    replace (length pre + S (length mid) - length pre) with (S (length mid)) by lia. simpl.
    rewrite lookup_app_r, Nat.sub_diag by lia. simpl.
    assert (Htake : take (length pre + S (length mid)) (pre ++ (c, s) :: mid ++ [(m, ms)]) =
                    pre ++ (c, s) :: mid).
    { replace (pre ++ (c, s) :: mid ++ [(m, ms)]) with ((pre ++ (c, s) :: mid) ++ [(m, ms)])
        by (rewrite <- app_assoc; reflexivity).
      apply take_app_length'. rewrite length_app. simpl. lia. }
    rewrite Htake.
    rewrite insert_app_r_alt, Nat.sub_diag by lia. reflexivity.
  - unfold obs_remove_at. simpl.
    rewrite length_app. simpl. replace (pred (length pre + 1)) with (length pre) by lia.
    rewrite lookup_app_r, Nat.sub_diag by lia. simpl.
    rewrite decide_True by done. rewrite decide_True by done.
    rewrite take_app_length. reflexivity.
Qed.

(** A successful removal, by position. [last] is the position of the last observer before it. *)
Lemma obs_remove_at_spec c h o o' n (last := pred (length (oitems o))) :
  obs_remove_at c h o = Some (o', n) ->
  (exists s, oitems o !! h = Some (c, s)) /\
  (forall q, q <> h -> q < last -> oitems o' !! q = oitems o !! q) /\
  (forall q, last <= q -> oitems o' !! q = None) /\
  ((h = last /\ n = None) \/
   (exists m ms, h < last /\ oitems o !! last = Some (m, ms) /\ n = Some (m, ms, h) /\
                 oitems o' !! h = Some (m, ms))).
Proof.
  unfold obs_remove_at. fold last.
  destruct (oitems o !! h) as [[c' s]|] eqn:Eh; [|done].
  pose proof (lookup_lt_Some _ _ _ Eh) as Hlt.
  assert (Hlen : h <= last) by (subst last; lia).
  destruct (decide (c' = c)) as [->|]; [|done].
  destruct (decide (h = last)) as [->|Hne].
  - intros [= <- <-]. split_and!; [eauto|..]; simpl.
    + intros q _ Hq. by rewrite lookup_take_lt.
    + intros q Hq. by rewrite lookup_take_ge.
    + left. done.
  - destruct (oitems o !! last) as [[m ms]|] eqn:El; [|done].
    assert (Hhl : h < last) by lia.
    assert (Htl : length (take last (oitems o)) = last).
    { apply length_take_le. subst last; lia. }
    intros [= <- <-]. split_and!; [eauto|..]; simpl.
    + intros q Hq Hql. rewrite list_lookup_insert_ne by done. by rewrite lookup_take_lt.
    + intros q Hq. rewrite list_lookup_insert_ne by lia. by rewrite lookup_take_ge.
    + right. eexists _, _. split_and!; [done|done|done|].
      rewrite list_lookup_insert_eq by lia. done.
Qed.

(** ** Walk coverage *)

Lemma apply_muts_app ms1 ms2 o :
  apply_muts (ms1 ++ ms2) o = apply_muts ms1 o ≫= apply_muts ms2.
Proof.
  revert o. induction ms1 as [|m r IH]; intros o; simpl; [done|].
  destruct (apply_mut m o) as [o1|]; simpl; [apply IH|done].
Qed.

Lemma mut_points_app (P : obs -> Prop) ms r o o1 :
  apply_muts ms o = Some o1 ->
  Forall P (mut_points (ms ++ r) o) ->
  Forall P (mut_points ms o) /\ Forall P (mut_points r o1).
Proof.
  revert o. induction ms as [|m ms' IH]; intros o Ha HP; simpl in *.
  - simplify_eq. split; [|done]. constructor; [|constructor].
    destruct r; simpl in HP; inversion HP; done.
  - destruct (apply_mut m o) as [o2|] eqn:E; simpl in *; [|done].
    inversion HP as [|?? Ho HPt]; subst. destruct (IH _ Ha HPt) as [Hm Hr].
    split; [constructor; done|done].
Qed.

(** A held-once observer that stays present never moves to a higher position. *)
Lemma mut_keeps_below m o o1 c p :
  apply_mut m o = Some o1 ->
  held_once c o ->
  c ∈ obs_view o1 ->
  obs_view o !! p = Some c ->
  exists p', p' <= p /\ obs_view o1 !! p' = Some c.
Proof.
  intros Hm [_ Hu] Hin1 Hp. destruct m as [d slot|d h]; simpl in Hm.
  - simplify_eq. exists p. split; [done|].
    apply view_lookup in Hp as [s Hs]. apply view_lookup. exists s.
    apply lookup_app_l_Some. done.
  - destruct (obs_remove_at d h o) as [[o2 n]|] eqn:Er; simpl in Hm; [|done]. simplify_eq.
    destruct (obs_remove_at_spec _ _ _ _ _ Er) as (_ & Hsame & Hgone & Hnotice).
    apply view_elem in Hin1 as (q & s & Hq).
    destruct (decide (q = h)) as [->|Hqh].
    + destruct Hnotice as [[-> ->]|(m' & ms & Hhl & Hl & -> & Hh)].
      * rewrite Hgone in Hq; [done|lia].
      * rewrite Hh in Hq. simplify_eq.
        assert (pred (length (oitems o)) = p) as <-.
        { apply Hu; [apply view_lookup; eauto|done]. }
        exists h. split; [lia|]. apply view_lookup. eauto.
    + destruct (decide (q < pred (length (oitems o)))) as [Hql|Hql].
      * rewrite Hsame in Hq by done.
        assert (q = p) as <-.
        { apply Hu; [apply view_lookup; eauto|done]. }
        exists q. split; [done|]. apply view_lookup. exists s.
        rewrite Hsame by done. done.
      * rewrite Hgone in Hq; [done|lia].
Qed.

Lemma muts_keep_below ms o o1 c p :
  apply_muts ms o = Some o1 ->
  Forall (held_once c) (mut_points ms o) ->
  obs_view o !! p = Some c ->
  exists p', p' <= p /\ obs_view o1 !! p' = Some c.
Proof.
  revert o p. induction ms as [|m r IH]; intros o p Ha HP Hp; simpl in *.
  - simplify_eq. eauto.
  - destruct (apply_mut m o) as [o2|] eqn:E; simpl in *; [|done].
    inversion HP as [|?? Ho HP']; subst.
    destruct r as [|m' r'] eqn:Er.
    all: assert (Ho2 : held_once c o2)
      by (subst; simpl in HP'; inversion HP'; done).
    all: destruct (mut_keeps_below _ _ _ _ _ E Ho (proj1 Ho2) Hp) as (p1 & Hle & Hp1).
    all: destruct (IH _ _ Ha HP' Hp1) as (p2 & Hle2 & Hp2).
    all: exists p2; split; [lia|done].
Qed.

Lemma walk_covers_from n o muts vis o' c p :
  walk n o muts vis o' ->
  Forall (held_once c) (mut_points (concat muts) o) ->
  obs_view o !! p = Some c ->
  p < n ->
  c ∈ vis.
Proof.
  intros Hw. revert p.
  induction Hw as [n o Hmin|n i o c0 slot ms o1 rest vis o' Hmin Hi Ha Hw IH];
    intros p HP Hp Hpn.
  - apply view_lookup in Hp as [s Hs]. apply lookup_lt_Some in Hs. lia.
  - apply view_lookup in Hp as [s Hs]. pose proof (lookup_lt_Some _ _ _ Hs) as Hplt.
    destruct (decide (p = i)) as [->|Hne].
    { rewrite Hi in Hs. simplify_eq. constructor. }
    constructor. simpl in HP.
    destruct (mut_points_app _ _ _ _ _ Ha HP) as [H1 H2].
    destruct (muts_keep_below _ _ _ _ _ Ha H1 (proj2 (view_lookup _ _ _) ltac:(eauto)))
      as (p' & Hle & Hp').
    eapply IH; [done|done|lia].
Qed.

(** An observer held exactly once at every point of a walk is visited. Held twice, it can be missed:
    [walk_covers_duplicate_observer] in [Pure/Witnesses.v]. *)
Theorem walk_covers o muts vis o' c :
  walk (length (oitems o)) o muts vis o' ->
  Forall (held_once c) (mut_points (concat muts) o) ->
  c ∈ vis.
Proof.
  intros Hw HP.
  assert (Ho : held_once c o).
  { destruct (concat muts) as [|m r]; simpl in HP; inversion HP; done. }
  destruct Ho as [Hin _]. apply list_elem_of_lookup in Hin as [p Hp].
  eapply walk_covers_from; [done|done|done|].
  apply view_lookup in Hp as [s Hs]. by eapply lookup_lt_Some.
Qed.

(** The [k]th observer visited was present after the mutations of the [k] visits before it. *)
Theorem walk_visits_only_seen n o muts vis o' :
  walk n o muts vis o' ->
  forall k c, vis !! k = Some c ->
  exists ok, apply_muts (concat (take k muts)) o = Some ok /\ c ∈ obs_view ok.
Proof.
  intros Hw. induction Hw as [|n i o c0 slot ms o1 rest vis o' Hmin Hi Ha Hw IH];
    intros k c Hk; [done|].
  destruct k as [|k]; simpl in Hk.
  - simplify_eq. exists o. split; [done|]. apply view_elem. eauto.
  - destruct (IH _ _ Hk) as (ok & Hok & Hc). exists ok. split; [|done].
    simpl. rewrite apply_muts_app, Ha. done.
Qed.
