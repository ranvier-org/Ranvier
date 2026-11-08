From stdpp Require Import prelude gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Pure Require Import ObserverSet.

(** A computation's source list. A slot holds a source and its edge handle, [None] for
    [EdgeHandle.Unlinked]. *)
Record src := { sslots : list (nid * option nat); scursor : nat }.

(** Both ends of every edge. A node absent from a map has an empty set or list. *)
Record EdgeWorld := { ew_obs : gmap nid obs; ew_src : gmap nid src }.

Definition empty_src : src := {| sslots := []; scursor := 0 |}.
Definition empty_world : EdgeWorld := {| ew_obs := ∅; ew_src := ∅ |}.

Definition obs_of (w : EdgeWorld) (s : nid) : obs := default empty_obs (ew_obs w !! s).
Definition src_of (w : EdgeWorld) (c : nid) : src := default empty_src (ew_src w !! c).
Definition slot_at (w : EdgeWorld) (c : nid) (i : nat) : option (nid * option nat) :=
  sslots (src_of w c) !! i.

Definition set_obs (s : nid) (o : obs) (w : EdgeWorld) : EdgeWorld :=
  {| ew_obs := <[s := o]> (ew_obs w); ew_src := ew_src w |}.
Definition set_src (c : nid) (l : src) (w : EdgeWorld) : EdgeWorld :=
  {| ew_obs := ew_obs w; ew_src := <[c := l]> (ew_src w) |}.

Definition sl_begin (c : nid) (w : EdgeWorld) : EdgeWorld :=
  set_src c {| sslots := sslots (src_of w c); scursor := 0 |} w.

Definition set_handle (c : nid) (i : nat) (mh : option nat) (w : EdgeWorld) : EdgeWorld :=
  let l := src_of w c in
  set_src c {| sslots := alter (fun p => (p.1, mh)) i (sslots l); scursor := scursor l |} w.

Definition sl_moved_in (c : nid) (slot h : nat) (w : EdgeWorld) : EdgeWorld :=
  set_handle c slot (Some h) w.

(** [RemoveObserverAt], with the [MovedIn] of the observer it moves. *)
Definition remove_edge (self s h : nid) (w : EdgeWorld) : option EdgeWorld :=
  match obs_remove_at self h (obs_of w s) with
  | Some (o', n) =>
    let w1 := set_obs s o' w in
    Some match n with
         | None => w1
         | Some (m, ms, h') => sl_moved_in m ms h' w1
         end
  | None => None
  end.

(** One iteration of [trimFrom]. The handle comes from the world current at this slot. *)
Definition sl_remove_slot (self : nid) (i : nat) (w : EdgeWorld) : option EdgeWorld :=
  match slot_at w self i with
  | Some (s, Some h) => set_handle self i None <$> remove_edge self s h w
  | _ => Some w
  end.

Fixpoint trim_loop (self : nid) (i n : nat) (w : EdgeWorld) : option EdgeWorld :=
  match n with
  | 0 => Some w
  | S n' => sl_remove_slot self i w ≫= trim_loop self (S i) n'
  end.

(** [trimFrom]. *)
Definition sl_trim (self : nid) (from : nat) (w : EdgeWorld) : option EdgeWorld :=
  trim_loop self from (length (sslots (src_of w self)) - from) w ≫= fun w1 =>
  let l1 := src_of w1 self in
  Some (set_src self {| sslots := take from (sslots l1); scursor := scursor l1 |} w1).

Definition sl_end (self : nid) (w : EdgeWorld) : option EdgeWorld :=
  let l := src_of w self in
  if decide (scursor l < length (sslots l)) then sl_trim self (scursor l) w else Some w.

Definition sl_clear (self : nid) (w : EdgeWorld) : option EdgeWorld :=
  sl_trim self 0 w ≫= fun w1 =>
  Some (set_src self {| sslots := sslots (src_of w1 self); scursor := 0 |} w1).

Definition sl_append (self s : nid) (mh : option nat) (o : obs) (w : EdgeWorld) : EdgeWorld :=
  let l := src_of w self in
  set_obs s o
    (set_src self {| sslots := sslots l ++ [(s, mh)]; scursor := S (length (sslots l)) |} w).

(** The slot and edge of [Append], after its trim. Slot 0 adds unconditionally. [SourceList.held] is
    abstracted to membership in the slots. *)
Definition sl_link (self s : nid) (w : EdgeWorld) : EdgeWorld :=
  let l := src_of w self in
  let slot := length (sslots l) in
  let o := obs_of w s in
  let add := fun _ : unit => sl_append self s (Some (obs_add self slot o).2) (obs_add self slot o).1 w in
  if decide (slot = 0) then add ()
  else
    match obs_try_add self slot o with
    | (o', TAdded h) => sl_append self s (Some h) o' w
    | (o', TUnlinked) => sl_append self s None o' w
    | (o', TUnknown) =>
      if decide (s ∈ fst <$> sslots l) then sl_append self s None o' w else add ()
    end.

(** [SourceList.Add]: a match at the cursor, a repeat of the source just matched, or a miss. *)
Definition sl_add (self s : nid) (w : EdgeWorld) : option EdgeWorld :=
  let l := src_of w self in
  let cur := scursor l in
  if decide (fst <$> sslots l !! cur = Some s) then
    Some (set_src self {| sslots := sslots l; scursor := S cur |} w)
  else if decide (0 < cur /\ fst <$> sslots l !! pred cur = Some s) then Some w
  else sl_end self w ≫= fun w1 => Some (sl_link self s w1).

(** Slot [i] of [c] holds [(s, Some h)] exactly when position [h] of [s]'s observers holds [(c, i)]. *)
Definition edge_match (w : EdgeWorld) : Prop :=
  forall c i s h, slot_at w c i = Some (s, Some h) <-> oitems (obs_of w s) !! h = Some (c, i).

(** A slot holds an edge exactly when it is its source's first slot. *)
Definition first_owns (w : EdgeWorld) (c : nid) : Prop :=
  forall i s mh, slot_at w c i = Some (s, mh) ->
    (is_Some mh <-> forall j, j < i -> fst <$> slot_at w c j <> Some s).

Definition ew_wf (w : EdgeWorld) : Prop := edge_match w /\ forall c, first_owns w c.

(** Every world the source-list operations build from nothing, in any interleaving per node. *)
Inductive ew_reachable : EdgeWorld -> Prop :=
  | er_empty : ew_reachable empty_world
  | er_begin c w : ew_reachable w -> ew_reachable (sl_begin c w)
  | er_add c s w w' : ew_reachable w -> sl_add c s w = Some w' -> ew_reachable w'
  | er_end c w w' : ew_reachable w -> sl_end c w = Some w' -> ew_reachable w'
  | er_clear c w w' : ew_reachable w -> sl_clear c w = Some w' -> ew_reachable w'.

(** ** Reading updated worlds *)

Lemma obs_of_set_obs s o w s' :
  obs_of (set_obs s o w) s' = if decide (s' = s) then o else obs_of w s'.
Proof.
  unfold obs_of, set_obs. simpl. destruct (decide (s' = s)) as [->|Hne].
  - by rewrite lookup_insert_eq.
  - by rewrite lookup_insert_ne by congruence.
Qed.

Lemma src_of_set_src c l w c' :
  src_of (set_src c l w) c' = if decide (c' = c) then l else src_of w c'.
Proof.
  unfold src_of, set_src. simpl. destruct (decide (c' = c)) as [->|Hne].
  - by rewrite lookup_insert_eq.
  - by rewrite lookup_insert_ne by congruence.
Qed.

Lemma obs_of_set_src c l w s : obs_of (set_src c l w) s = obs_of w s.
Proof. reflexivity. Qed.

Lemma src_of_set_obs s o w c : src_of (set_obs s o w) c = src_of w c.
Proof. reflexivity. Qed.

Lemma slot_at_set_obs s o w c i : slot_at (set_obs s o w) c i = slot_at w c i.
Proof. reflexivity. Qed.

Lemma slot_at_set_src c l w c' i :
  slot_at (set_src c l w) c' i = if decide (c' = c) then sslots l !! i else slot_at w c' i.
Proof. unfold slot_at. rewrite src_of_set_src. by destruct (decide (c' = c)). Qed.

Lemma obs_of_set_handle c i mh w s : obs_of (set_handle c i mh w) s = obs_of w s.
Proof. reflexivity. Qed.

Lemma cursor_set_handle c i mh w c' :
  scursor (src_of (set_handle c i mh w) c') = scursor (src_of w c').
Proof.
  unfold set_handle. rewrite src_of_set_src. destruct (decide (c' = c)) as [->|]; done.
Qed.

Lemma slot_at_set_handle c i mh w c' j :
  slot_at (set_handle c i mh w) c' j =
    if decide (c' = c /\ j = i) then (fun p => (p.1, mh)) <$> slot_at w c i else slot_at w c' j.
Proof.
  unfold set_handle. rewrite slot_at_set_src.
  destruct (decide (c' = c)) as [->|Hne]; simpl.
  - destruct (decide (c = c /\ j = i)) as [[_ ->]|Hne].
    + apply list_lookup_alter_eq.
    + apply list_lookup_alter_ne. intros ->. tauto.
  - rewrite decide_False by tauto. done.
Qed.

Lemma fst_set_handle c i mh w c' j :
  fst <$> slot_at (set_handle c i mh w) c' j = fst <$> slot_at w c' j.
Proof.
  rewrite slot_at_set_handle. destruct (decide (c' = c /\ j = i)) as [[-> ->]|]; [|done].
  by destruct (slot_at w c i).
Qed.

Definition owns (w : EdgeWorld) (c : nid) (j : nat) : Prop :=
  exists s h, slot_at w c j = Some (s, Some h).

Lemma owns_is_Some w c i s mh :
  slot_at w c i = Some (s, mh) -> owns w c i <-> is_Some mh.
Proof.
  unfold owns. intros Hs. rewrite Hs. split.
  - intros (s' & h & ?). simplify_eq. eauto.
  - intros [h ->]. eauto.
Qed.

Lemma owns_set_handle_ne c i mh w c' j :
  ~ (c' = c /\ j = i) -> owns (set_handle c i mh w) c' j <-> owns w c' j.
Proof. intros Hne. unfold owns. rewrite slot_at_set_handle, decide_False by done. done. Qed.

Lemma owns_set_handle_none c i w : ~ owns (set_handle c i None w) c i.
Proof.
  unfold owns. rewrite slot_at_set_handle, decide_True by done.
  intros (s & h & Hs). by destruct (slot_at w c i).
Qed.

(** ** Removing a slot's edge *)

(** What a stretch of [trim_loop] does to slots. *)
Definition trim_rel (self : nid) (from upto : nat) (w w' : EdgeWorld) : Prop :=
  (forall c j, fst <$> slot_at w' c j = fst <$> slot_at w c j) /\
  (forall c j, (c <> self \/ j < from \/ upto <= j) -> owns w' c j <-> owns w c j) /\
  (forall j, from <= j < upto -> ~ owns w' self j) /\
  (forall c, scursor (src_of w' c) = scursor (src_of w c)).

Lemma obs_remove_at_ok c s h o :
  oitems o !! h = Some (c, s) -> exists r, obs_remove_at c h o = Some r.
Proof.
  intros Hh. unfold obs_remove_at. rewrite Hh, decide_True by done.
  pose proof (lookup_lt_Some _ _ _ Hh) as Hlt.
  destruct (decide (h = pred (length (oitems o)))) as [|Hne]; [by eexists|].
  destruct (lookup_lt_is_Some_2 (oitems o) (pred (length (oitems o))) ltac:(lia)) as [[m ms] ->].
  by eexists.
Qed.

Lemma remove_step self i s h w :
  edge_match w ->
  slot_at w self i = Some (s, Some h) ->
  exists w', sl_remove_slot self i w = Some w' /\ edge_match w' /\ trim_rel self i (S i) w w'.
Proof.
  intros Hm Hs. red in Hm.
  assert (Hh : oitems (obs_of w s) !! h = Some (self, i)) by (by apply Hm).
  pose proof (lookup_lt_Some _ _ _ Hh) as Hhlen.
  destruct (obs_remove_at_ok _ _ _ _ Hh) as [[o' n] Hr].
  destruct (obs_remove_at_spec _ _ _ _ _ Hr) as (_ & Hsame & Hgone & Hnotice).
  unfold sl_remove_slot, remove_edge. rewrite Hs, Hr. simpl.
  destruct Hnotice as [[Hl ->]|(m & ms & Hhl & Hlast & -> & Hhole)].
  - eexists. split; [reflexivity|]. split.
    + intros c j s' q.
      rewrite slot_at_set_handle, obs_of_set_handle, obs_of_set_obs.
      destruct (decide (c = self /\ j = i)) as [[-> ->]|Hni].
      * rewrite slot_at_set_obs, Hs. simpl. split; [congruence|]. intros Hq.
        destruct (decide (s' = s)) as [->|Hs'].
        -- destruct (decide (q = h)) as [->|Hqh].
           ++ rewrite Hgone in Hq by lia. done.
           ++ destruct (decide (q < pred (length (oitems (obs_of w s))))) as [Hql|Hql].
              ** rewrite Hsame in Hq by done. apply Hm in Hq. congruence.
              ** rewrite Hgone in Hq by lia. done.
        -- apply Hm in Hq. congruence.
      * rewrite slot_at_set_obs.
        destruct (decide (s' = s)) as [->|Hs']; [|apply Hm]. rewrite Hm. split.
        -- intros Hq. pose proof (lookup_lt_Some _ _ _ Hq) as Hqlt.
           destruct (decide (q = h)) as [->|Hqh]; [rewrite Hh in Hq; simplify_eq; tauto|].
           rewrite Hsame by lia. done.
        -- intros Hq. destruct (decide (q < pred (length (oitems (obs_of w s))))) as [Hql|Hql].
           ++ destruct (decide (q = h)) as [->|Hqh]; [lia|]. rewrite Hsame in Hq by done. done.
           ++ rewrite Hgone in Hq by lia. done.
    + split_and!.
      * intros c j. rewrite fst_set_handle, slot_at_set_obs. done.
      * intros c j Hcj. rewrite owns_set_handle_ne by (intros [-> ->]; lia).
        unfold owns. setoid_rewrite slot_at_set_obs. done.
      * intros j Hj. assert (j = i) as -> by lia. apply owns_set_handle_none.
      * intros c. rewrite cursor_set_handle. done.
  - assert (Hms : slot_at w m ms = Some (s, Some (pred (length (oitems (obs_of w s))))))
      by (by apply Hm).
    assert (Hne : ~ (m = self /\ ms = i)).
    { intros [-> ->]. rewrite Hs in Hms. simplify_eq. lia. }
    eexists. split; [reflexivity|]. split.
    + intros c j s' q.
      rewrite slot_at_set_handle, obs_of_set_handle. unfold sl_moved_in.
      rewrite obs_of_set_handle, obs_of_set_obs.
      destruct (decide (c = self /\ j = i)) as [[-> ->]|Hni].
      * rewrite slot_at_set_handle, decide_False by (intros [-> ->]; tauto).
        rewrite slot_at_set_obs, Hs. simpl. split; [congruence|]. intros Hq.
        destruct (decide (s' = s)) as [->|Hs'].
        -- destruct (decide (q = h)) as [->|Hqh].
           ++ rewrite Hhole in Hq. simplify_eq. tauto.
           ++ destruct (decide (q < pred (length (oitems (obs_of w s))))) as [Hql|Hql].
              ** rewrite Hsame in Hq by done. apply Hm in Hq. congruence.
              ** rewrite Hgone in Hq by lia. done.
        -- apply Hm in Hq. congruence.
      * rewrite slot_at_set_handle.
        destruct (decide (c = m /\ j = ms)) as [[-> ->]|Hnm].
        -- rewrite slot_at_set_obs, Hms. simpl. split.
           ++ intros Heq. simplify_eq. rewrite decide_True by done. done.
           ++ intros Hq. destruct (decide (s' = s)) as [->|Hs'].
              ** destruct (decide (q = h)) as [->|Hqh]; [done|].
                 destruct (decide (q < pred (length (oitems (obs_of w s))))) as [Hql|Hql].
                 --- rewrite Hsame in Hq by done. apply Hm in Hq. rewrite Hms in Hq.
                     simplify_eq. lia.
                 --- rewrite Hgone in Hq by lia. done.
              ** apply Hm in Hq. congruence.
        -- rewrite slot_at_set_obs.
           destruct (decide (s' = s)) as [->|Hs']; [|apply Hm]. rewrite Hm. split.
           ++ intros Hq. pose proof (lookup_lt_Some _ _ _ Hq) as Hqlt.
              destruct (decide (q = h)) as [->|Hqh]; [rewrite Hh in Hq; simplify_eq; tauto|].
              destruct (decide (q = pred (length (oitems (obs_of w s))))) as [->|Hql];
                [rewrite Hlast in Hq; simplify_eq; tauto|].
              rewrite Hsame by lia. done.
           ++ intros Hq. destruct (decide (q = h)) as [->|Hqh].
              ** rewrite Hhole in Hq. simplify_eq. tauto.
              ** destruct (decide (q < pred (length (oitems (obs_of w s))))) as [Hql|Hql].
                 --- rewrite Hsame in Hq by done. done.
                 --- rewrite Hgone in Hq by lia. done.
    + split_and!.
      * intros c j. rewrite fst_set_handle. unfold sl_moved_in. rewrite fst_set_handle.
        rewrite slot_at_set_obs. done.
      * intros c j Hcj. rewrite owns_set_handle_ne by (intros [-> ->]; lia).
        unfold sl_moved_in.
        destruct (decide (c = m /\ j = ms)) as [[-> ->]|Hnm].
        -- unfold owns. rewrite slot_at_set_handle, decide_True by done.
           rewrite slot_at_set_obs, Hms. simpl. split; eauto.
        -- rewrite owns_set_handle_ne by done. unfold owns.
           setoid_rewrite slot_at_set_obs. done.
      * intros j Hj. assert (j = i) as -> by lia. apply owns_set_handle_none.
      * intros c. rewrite cursor_set_handle. unfold sl_moved_in.
        rewrite cursor_set_handle. done.
Qed.

Lemma remove_slot_ok self i w :
  edge_match w ->
  exists w', sl_remove_slot self i w = Some w' /\ edge_match w' /\ trim_rel self i (S i) w w'.
Proof.
  intros Hm. destruct (slot_at w self i) as [[s [h|]]|] eqn:Hs.
  - by eapply remove_step.
  - exists w. unfold sl_remove_slot. rewrite Hs. split_and!; [done|done|].
    unfold trim_rel. split_and!; try done.
    intros j Hj. assert (j = i) as -> by lia. unfold owns. rewrite Hs.
    intros (s' & h & ?). simplify_eq.
  - exists w. unfold sl_remove_slot. rewrite Hs. split_and!; [done|done|].
    unfold trim_rel. split_and!; try done.
    intros j Hj. assert (j = i) as -> by lia. unfold owns. rewrite Hs.
    intros (s' & h & ?). simplify_eq.
Qed.

Lemma trim_loop_ok self n i w :
  edge_match w ->
  exists w', trim_loop self i n w = Some w' /\ edge_match w' /\ trim_rel self i (i + n) w w'.
Proof.
  revert i w. induction n as [|n IH]; intros i w Hm; simpl.
  - exists w. split_and!; [done|done|]. unfold trim_rel. split_and!; try done. intros j Hj. lia.
  - destruct (remove_slot_ok self i w Hm) as (w1 & -> & Hm1 & Hfst1 & Hown1 & Hno1 & Hcur1). simpl.
    destruct (IH (S i) w1 Hm1) as (w' & -> & Hm' & Hfst' & Hown' & Hno' & Hcur').
    exists w'. split_and!; [done|done|]. unfold trim_rel. split_and!.
    + intros c j. rewrite Hfst', Hfst1. done.
    + intros c j Hcj. rewrite Hown' by (destruct Hcj as [|[|]]; [tauto|right; left; lia|right; right; lia]).
      apply Hown1. destruct Hcj as [|[|]]; [tauto|right; left; lia|right; right; lia].
    + intros j Hj. destruct (decide (j = i)) as [->|Hne].
      * rewrite Hown' by (right; left; lia). apply Hno1. lia.
      * apply Hno'. lia.
    + intros c. rewrite Hcur', Hcur1. done.
Qed.

(** What a trim from [from] does to slots. *)
Definition trimmed (self : nid) (from : nat) (w w' : EdgeWorld) : Prop :=
  (forall j, j < from ->
     fst <$> slot_at w' self j = fst <$> slot_at w self j /\ (owns w' self j <-> owns w self j)) /\
  (forall j, from <= j -> slot_at w' self j = None) /\
  (forall c j, c <> self ->
     fst <$> slot_at w' c j = fst <$> slot_at w c j /\ (owns w' c j <-> owns w c j)) /\
  (forall c, scursor (src_of w' c) = scursor (src_of w c)).

Lemma sl_trim_ok self from w :
  edge_match w ->
  exists w', sl_trim self from w = Some w' /\ edge_match w' /\ trimmed self from w w'.
Proof.
  intros Hm. unfold sl_trim.
  destruct (trim_loop_ok self (length (sslots (src_of w self)) - from) from w Hm)
    as (w1 & -> & Hm1 & Hfst & Hown & Hno & Hcur). simpl.
  assert (Hnot : forall j, from <= j -> ~ owns w1 self j).
  { intros j Hj Ho. destruct (decide (j < length (sslots (src_of w self)))) as [Hl|Hl].
    - apply (Hno j); [lia|done].
    - apply Hown in Ho; [|right; right; lia].
      destruct Ho as (s & h & Hs). unfold slot_at in Hs. apply lookup_lt_Some in Hs. lia. }
  eexists. split; [reflexivity|]. split; [|unfold trimmed; split_and!].
  - intros c j s q. rewrite slot_at_set_src, obs_of_set_src.
    destruct (decide (c = self)) as [->|Hne]; simpl; [|apply Hm1].
    destruct (decide (j < from)) as [Hj|Hj].
    + rewrite lookup_take_lt by done. apply Hm1.
    + rewrite lookup_take_ge by lia. split; [done|]. intros Hq.
      apply Hm1 in Hq. exfalso. eapply (Hnot j); [lia|]. eexists _, _. exact Hq.
  - intros j Hj. unfold owns. rewrite !slot_at_set_src, decide_True by done. simpl.
    rewrite lookup_take_lt by done. split; [apply Hfst|]. apply Hown. tauto.
  - intros j Hj. rewrite slot_at_set_src, decide_True by done. simpl.
    apply lookup_take_ge. done.
  - intros c j Hne. unfold owns. rewrite !slot_at_set_src, decide_False by done.
    split; [apply Hfst|]. apply Hown. tauto.
  - intros c. rewrite src_of_set_src.
    destruct (decide (c = self)) as [->|]; simpl; apply Hcur.
Qed.

Lemma sl_end_ok self w :
  edge_match w ->
  exists w', sl_end self w = Some w' /\ edge_match w' /\
             trimmed self (scursor (src_of w self)) w w'.
Proof.
  intros Hm. unfold sl_end.
  destruct (decide (scursor (src_of w self) < length (sslots (src_of w self)))) as [Hl|Hl].
  - by apply sl_trim_ok.
  - exists w. split_and!; [done|done|]. unfold trimmed. split_and!; try done.
    intros j Hj. apply lookup_ge_None_2. lia.
Qed.

(** ** Slots, source by source *)

Lemma first_owns_transfer w w1 c from :
  (forall j, j < from ->
     fst <$> slot_at w1 c j = fst <$> slot_at w c j /\ (owns w1 c j <-> owns w c j)) ->
  (forall j, from <= j -> slot_at w1 c j = None) ->
  first_owns w c -> first_owns w1 c.
Proof.
  intros Hsame Hnone Hf i s mh Hs.
  assert (Hi : i < from).
  { destruct (decide (i < from)) as [|Hge]; [done|]. rewrite Hnone in Hs by lia. done. }
  destruct (Hsame i Hi) as [Hfst Hown].
  destruct (slot_at w c i) as [[s0 mh0]|] eqn:Hs0; rewrite Hs in Hfst; simpl in Hfst; [|done].
  simplify_eq.
  rewrite <- (owns_is_Some _ _ _ _ _ Hs), Hown, (owns_is_Some _ _ _ _ _ Hs0), (Hf _ _ _ Hs0).
  split.
  - intros Hall j Hj. rewrite (proj1 (Hsame j ltac:(lia))). by apply Hall.
  - intros Hall j Hj. rewrite <- (proj1 (Hsame j ltac:(lia))). by apply Hall.
Qed.

Lemma first_owns_trimmed self from w w' :
  trimmed self from w w' ->
  (forall c, first_owns w c) -> forall c, first_owns w' c.
Proof.
  intros (Hsame & Hnone & Hother & _) Hf c.
  destruct (decide (c = self)) as [->|Hne].
  - eapply first_owns_transfer; [apply Hsame|apply Hnone|apply Hf].
  - eapply first_owns_transfer with (from := length (sslots (src_of w' c))).
    + intros j _. by apply Hother.
    + intros j Hj. by apply lookup_ge_None_2.
    + apply Hf.
Qed.

Lemma slot_at_append self s mh o w c j :
  slot_at (sl_append self s mh o w) c j =
    if decide (c = self) then (sslots (src_of w self) ++ [(s, mh)]) !! j else slot_at w c j.
Proof.
  unfold sl_append. rewrite slot_at_set_obs, slot_at_set_src.
  destruct (decide (c = self)); done.
Qed.

Lemma first_source s (l : list (nid * option nat)) :
  s ∈ fst <$> l ->
  exists i mh, l !! i = Some (s, mh) /\ forall j, j < i -> fst <$> l !! j <> Some s.
Proof.
  induction l as [|[s0 mh0] l IH]; simpl; intros Hin.
  - by apply not_elem_of_nil in Hin.
  - destruct (decide (s0 = s)) as [->|Hne].
    + exists 0, mh0. split; [done|]. intros j Hj. lia.
    + apply elem_of_cons in Hin as [->|Hin]; [done|].
      destruct (IH Hin) as (i & mh & Hi & Hb). exists (S i), mh. split; [done|].
      intros [|j] Hj; simpl; [congruence|]. apply Hb. lia.
Qed.

Lemma first_owns_edge w c s :
  first_owns w c ->
  s ∈ fst <$> sslots (src_of w c) ->
  exists i h, slot_at w c i = Some (s, Some h).
Proof.
  intros Hf Hin. destruct (first_source _ _ Hin) as (i & mh & Hi & Hb).
  destruct (proj2 (Hf i s mh Hi) Hb) as [h ->]. eauto.
Qed.

Lemma slot_source w c i s mh :
  slot_at w c i = Some (s, mh) -> s ∈ fst <$> sslots (src_of w c).
Proof.
  intros Hs. apply list_elem_of_fmap. exists (s, mh). split; [done|].
  by eapply list_elem_of_lookup_2.
Qed.

(** [c] is in [s]'s observers exactly when [s] has a slot in [c]'s list. *)
Lemma held_iff_slot w c s :
  ew_wf w -> c ∈ obs_view (obs_of w s) <-> s ∈ fst <$> sslots (src_of w c).
Proof.
  intros [Hm Hf]. rewrite view_elem. split.
  - intros (q & i & Hq). apply Hm in Hq. by eapply slot_source.
  - intros Hin. destruct (first_owns_edge _ _ _ (Hf c) Hin) as (i & h & Hs).
    exists h, i. by apply Hm.
Qed.

(** Two slots of one source holding edges are one slot. *)
Lemma first_owns_unique w c s i j hi hj :
  first_owns w c ->
  slot_at w c i = Some (s, Some hi) -> slot_at w c j = Some (s, Some hj) -> i = j.
Proof.
  intros Hf Hi Hj. destruct (decide (i = j)) as [|Hne]; [done|]. exfalso.
  destruct (decide (i < j)) as [Hlt|Hge].
  - eapply (proj1 (Hf _ _ _ Hj) ltac:(eauto) i Hlt). by rewrite Hi.
  - eapply (proj1 (Hf _ _ _ Hi) ltac:(eauto) j ltac:(lia)). by rewrite Hj.
Qed.

Lemma wf_held_once w c s :
  ew_wf w -> s ∈ fst <$> sslots (src_of w c) -> held_once c (obs_of w s).
Proof.
  intros Hw Hin. pose proof Hw as [Hm Hf]. split; [by apply held_iff_slot|].
  intros i j Hi Hj.
  apply view_lookup in Hi as [a Ha]. apply view_lookup in Hj as [b Hb].
  apply Hm in Ha. apply Hm in Hb.
  assert (a = b) as -> by (eapply first_owns_unique; [apply Hf|exact Ha|exact Hb]).
  rewrite Ha in Hb. by simplify_eq.
Qed.

Lemma lookup_append_lt (l : list (nid * option nat)) x j :
  j < length l -> (l ++ [x]) !! j = l !! j.
Proof. apply lookup_app_l. Qed.

Lemma lookup_append_eq (l : list (nid * option nat)) x : (l ++ [x]) !! length l = Some x.
Proof. rewrite lookup_app_r, Nat.sub_diag by lia. done. Qed.

Lemma lookup_append_gt (l : list (nid * option nat)) x j :
  length l < j -> (l ++ [x]) !! j = None.
Proof. intros Hj. apply lookup_ge_None_2. rewrite length_app. simpl. lia. Qed.

(** ** Appending *)

Lemma sl_link_cases self s w :
  (sl_link self s w = sl_append self s None (obs_of w s) w /\
   (self ∈ obs_view (obs_of w s) \/ s ∈ fst <$> sslots (src_of w self))) \/
  (sl_link self s w =
     sl_append self s (Some (length (oitems (obs_of w s))))
       (obs_add self (length (sslots (src_of w self))) (obs_of w s)).1 w /\
   (sslots (src_of w self) = [] \/ self ∉ obs_view (obs_of w s) \/
    s ∉ fst <$> sslots (src_of w self))).
Proof.
  unfold sl_link.
  destruct (decide (length (sslots (src_of w self)) = 0)) as [H0|H0].
  { right. split; [reflexivity|]. left. by apply nil_length_inv. }
  destruct (obs_try_add_spec self (length (sslots (src_of w self))) (obs_of w s))
    as [[-> Hc]|[(h & -> & -> & Hc)|(-> & _ & _)]].
  - left. split; [reflexivity|]. by left.
  - right. split; [reflexivity|]. by right; left.
  - destruct (decide (s ∈ fst <$> sslots (src_of w self))) as [Hin|Hin].
    + left. split; [reflexivity|]. by right.
    + right. split; [reflexivity|]. by right; right.
Qed.

Lemma edge_match_append_none self s w :
  edge_match w -> edge_match (sl_append self s None (obs_of w s) w).
Proof.
  intros Hm c j s' q. rewrite slot_at_append.
  unfold sl_append. rewrite obs_of_set_obs, obs_of_set_src.
  assert (Hobs : (if decide (s' = s) then obs_of w s else obs_of w s') = obs_of w s').
  { by destruct (decide (s' = s)) as [->|]. }
  rewrite Hobs.
  destruct (decide (c = self)) as [->|Hne]; [|apply Hm].
  destruct (decide (j < length (sslots (src_of w self)))) as [Hl|Hl].
  - rewrite lookup_append_lt by done. apply Hm.
  - split.
    + destruct (decide (j = length (sslots (src_of w self)))) as [->|Hj].
      * rewrite lookup_append_eq. done.
      * rewrite lookup_append_gt by lia. done.
    + intros Hq. apply Hm in Hq. apply lookup_lt_Some in Hq. lia.
Qed.

Lemma edge_match_append_add self s w :
  edge_match w ->
  edge_match
    (sl_append self s (Some (length (oitems (obs_of w s))))
       (obs_add self (length (sslots (src_of w self))) (obs_of w s)).1 w).
Proof.
  intros Hm c j s' q. red in Hm. rewrite slot_at_append.
  unfold sl_append. rewrite obs_of_set_obs, obs_of_set_src.
  set (slot := length (sslots (src_of w self))).
  set (len := length (oitems (obs_of w s))).
  assert (Hnoslot : forall z, oitems (obs_of w z) !! q <> Some (self, slot)).
  { intros z Hq. apply Hm in Hq. apply lookup_lt_Some in Hq. subst slot. lia. }
  assert (Hlenq : forall c' j', oitems (obs_of w s) !! len <> Some (c', j')).
  { intros c' j'. subst len. rewrite lookup_ge_None_2 by lia. done. }
  destruct (decide (s' = s)) as [->|Hs']; simpl.
  - assert (Hitems : forall x, (oitems (obs_of w s) ++ [(self, slot)]) !! q = Some x <->
              (q < len /\ oitems (obs_of w s) !! q = Some x) \/ (q = len /\ x = (self, slot))).
    { intros x. rewrite lookup_app_Some. subst len. split.
      - intros [Hq|[Hge Hq]].
        + left. split; [eapply lookup_lt_Some; done|done].
        + right. destruct (q - length (oitems (obs_of w s))) as [|d] eqn:Hd; simpl in Hq.
          * simplify_eq. split; [lia|done].
          * by rewrite lookup_nil in Hq.
      - intros [[_ Hq]|[-> ->]]; [by left|]. right. split; [lia|].
        rewrite Nat.sub_diag. done. }
    rewrite Hitems.
    destruct (decide (c = self)) as [->|Hne].
    + destruct (decide (j < slot)) as [Hl|Hl].
      * rewrite lookup_append_lt by done. fold (slot_at w self j). rewrite Hm. split.
        -- intros Hq. left. split; [|done]. by eapply lookup_lt_Some.
        -- intros [[_ Hq]|[-> Heq]]; [done|]. simplify_eq. lia.
      * destruct (decide (j = slot)) as [->|Hj].
        -- unfold slot; rewrite lookup_append_eq; fold slot. split.
           ++ intros Heq. simplify_eq. by right.
           ++ intros [[_ Hq]|[-> _]]; [|done]. by apply Hnoslot in Hq.
        -- rewrite lookup_append_gt by lia. split; [done|].
           intros [[_ Hq]|[_ Heq]]; [|simplify_eq; tauto].
           apply Hm in Hq. apply lookup_lt_Some in Hq. lia.
    + rewrite Hm. split.
      * intros Hq. left. split; [|done]. by eapply lookup_lt_Some.
      * intros [[_ Hq]|[_ Heq]]; [done|]. simplify_eq.
  - destruct (decide (c = self)) as [->|Hne]; [|apply Hm].
    destruct (decide (j < slot)) as [Hl|Hl].
    + rewrite lookup_append_lt by done. apply Hm.
    + split.
      * destruct (decide (j = slot)) as [->|Hj].
        -- unfold slot; rewrite lookup_append_eq; fold slot. intros Heq. simplify_eq.
        -- rewrite lookup_append_gt by lia. done.
      * intros Hq. apply Hm in Hq. apply lookup_lt_Some in Hq. lia.
Qed.

Lemma edge_match_link self s w : edge_match w -> edge_match (sl_link self s w).
Proof.
  intros Hm. destruct (sl_link_cases self s w) as [[-> _]|[-> _]].
  - by apply edge_match_append_none.
  - by apply edge_match_append_add.
Qed.

Lemma first_owns_append self s mh o w :
  first_owns w self ->
  (is_Some mh <-> s ∉ fst <$> sslots (src_of w self)) ->
  first_owns (sl_append self s mh o w) self.
Proof.
  intros Hf Hmh i s' mh'. rewrite !slot_at_append, decide_True by done.
  setoid_rewrite slot_at_append. setoid_rewrite (decide_True _ _ (eq_refl self)).
  destruct (decide (i < length (sslots (src_of w self)))) as [Hl|Hl].
  - rewrite lookup_append_lt by done. intros Hi. rewrite (Hf _ _ _ Hi). split.
    + intros Hall j Hj. rewrite lookup_append_lt by lia. by apply Hall.
    + intros Hall j Hj. specialize (Hall j Hj). rewrite lookup_append_lt in Hall by lia. done.
  - destruct (decide (i = length (sslots (src_of w self)))) as [->|Hi].
    + rewrite lookup_append_eq. intros Heq. simplify_eq. rewrite Hmh. split.
      * intros Hnot j Hj. rewrite lookup_append_lt by lia. intros Hs.
        apply Hnot. apply list_elem_of_fmap.
        destruct (sslots (src_of w self) !! j) as [[s0 m0]|] eqn:E; simplify_eq/=.
        exists (s', m0). split; [done|]. by eapply list_elem_of_lookup_2.
      * intros Hall Hin. apply list_elem_of_lookup in Hin as [j Hj].
        rewrite list_lookup_fmap in Hj.
        destruct (sslots (src_of w self) !! j) as [[s0 m0]|] eqn:E; simplify_eq/=.
        pose proof (lookup_lt_Some _ _ _ E) as Hjl.
        apply (Hall j Hjl). rewrite lookup_append_lt by done. rewrite E. done.
    + rewrite lookup_append_gt by lia. done.
Qed.

Lemma first_owns_append_other self s mh o w c :
  c <> self -> first_owns w c -> first_owns (sl_append self s mh o w) c.
Proof.
  intros Hne Hf i s' mh'. rewrite slot_at_append, decide_False by done.
  setoid_rewrite slot_at_append. setoid_rewrite (decide_False _ _ Hne). apply Hf.
Qed.

Lemma wf_link self s w : ew_wf w -> ew_wf (sl_link self s w).
Proof.
  intros Hw. pose proof Hw as [Hm Hf]. split; [by apply edge_match_link|]. intros c.
  destruct (decide (c = self)) as [->|Hne].
  - destruct (sl_link_cases self s w) as [[-> Hc]|[-> Hc]].
    + apply first_owns_append; [done|]. split; [by intros []|].
      intros Hnot. exfalso. apply Hnot.
      destruct Hc as [Hc|Hc]; [by apply (held_iff_slot _ _ _ Hw) in Hc|done].
    + apply first_owns_append; [done|]. split; [|eauto]. intros _.
      destruct Hc as [->|[Hc|Hc]]; [|by rewrite (held_iff_slot _ _ _ Hw) in Hc|done].
      simpl. apply not_elem_of_nil.
  - destruct (sl_link_cases self s w) as [[-> _]|[-> _]];
      by apply first_owns_append_other.
Qed.

(** ** Preservation *)

Lemma wf_end self w :
  ew_wf w -> exists w', sl_end self w = Some w' /\ ew_wf w' /\
                        trimmed self (scursor (src_of w self)) w w'.
Proof.
  intros [Hm Hf]. destruct (sl_end_ok self w Hm) as (w' & Hr & Hm' & Ht).
  exists w'. split_and!; [done|split; [done|]|done]. by eapply first_owns_trimmed.
Qed.

Lemma sl_clear_ok self w :
  edge_match w ->
  exists w', sl_clear self w = Some w' /\ edge_match w' /\
             sslots (src_of w' self) = [] /\ scursor (src_of w' self) = 0 /\
             (forall c j, c <> self ->
                fst <$> slot_at w' c j = fst <$> slot_at w c j /\ (owns w' c j <-> owns w c j)) /\
             (forall s, self ∉ obs_view (obs_of w' s)).
Proof.
  intros Hm. destruct (sl_trim_ok self 0 w Hm) as (w1 & Hr & Hm1 & _ & Hnone & Hother & _).
  assert (Hslots : sslots (src_of w1 self) = []).
  { destruct (sslots (src_of w1 self)) as [|x l] eqn:E; [done|].
    exfalso. specialize (Hnone 0 ltac:(lia)). unfold slot_at in Hnone. rewrite E in Hnone. done. }
  unfold sl_clear. rewrite Hr. simpl.
  eexists. split; [reflexivity|].
  assert (Hm' : edge_match (set_src self {| sslots := sslots (src_of w1 self); scursor := 0 |} w1)).
  { intros c j s q. rewrite slot_at_set_src, obs_of_set_src.
    destruct (decide (c = self)) as [->|]; [|apply Hm1]. simpl. apply Hm1. }
  split_and!; [done|..].
  - rewrite src_of_set_src, decide_True by done. simpl. done.
  - rewrite src_of_set_src, decide_True by done. done.
  - intros c j Hne. unfold owns. rewrite !slot_at_set_src, decide_False by done.
    apply Hother. done.
  - intros s. rewrite view_elem. intros (q & i & Hq). apply Hm' in Hq.
    unfold slot_at in Hq. rewrite src_of_set_src, decide_True in Hq by done.
    simpl in Hq. rewrite Hslots in Hq. done.
Qed.

Lemma wf_clear self w :
  ew_wf w -> exists w', sl_clear self w = Some w' /\ ew_wf w'.
Proof.
  intros [Hm Hf]. destruct (sl_clear_ok self w Hm) as (w' & Hr & Hm' & Hs & _ & Hother & _).
  exists w'. split; [done|]. split; [done|]. intros c.
  destruct (decide (c = self)) as [->|Hne].
  - intros i s mh Hi. unfold slot_at in Hi. rewrite Hs in Hi. done.
  - eapply first_owns_transfer with (from := length (sslots (src_of w' c))).
    + intros j _. by apply Hother.
    + intros j Hj. by apply lookup_ge_None_2.
    + apply Hf.
Qed.

Lemma wf_begin c w : ew_wf w -> ew_wf (sl_begin c w).
Proof.
  intros [Hm Hf]. unfold sl_begin.
  assert (Hslot : forall c' i,
    slot_at (set_src c {| sslots := sslots (src_of w c); scursor := 0 |} w) c' i = slot_at w c' i).
  { intros c' i. rewrite slot_at_set_src. destruct (decide (c' = c)) as [->|]; done. }
  split.
  - intros c' i s h. rewrite Hslot, obs_of_set_src. apply Hm.
  - intros c' i s mh. rewrite !Hslot. setoid_rewrite Hslot. apply Hf.
Qed.

Lemma slot_at_advance self c j w :
  slot_at (set_src self {| sslots := sslots (src_of w self);
                           scursor := S (scursor (src_of w self)) |} w) c j = slot_at w c j.
Proof.
  rewrite slot_at_set_src. destruct (decide (c = self)) as [->|]; done.
Qed.

Lemma wf_add self s w :
  ew_wf w -> exists w', sl_add self s w = Some w' /\ ew_wf w'.
Proof.
  intros Hw. pose proof Hw as [Hm Hf]. unfold sl_add.
  destruct (decide (fst <$> sslots (src_of w self) !! scursor (src_of w self) = Some s)) as [Hp|Hp].
  - eexists. split; [reflexivity|]. split.
    + intros c i s' h. rewrite slot_at_advance, obs_of_set_src. apply Hm.
    + intros c i s' mh. rewrite !slot_at_advance. setoid_rewrite slot_at_advance. apply Hf.
  - destruct (decide (0 < scursor (src_of w self) /\
                      fst <$> sslots (src_of w self) !! pred (scursor (src_of w self)) = Some s));
      [by eexists|].
    destruct (wf_end self w Hw) as (w1 & -> & Hw1 & _). simpl.
    eexists. split; [reflexivity|]. by apply wf_link.
Qed.

Lemma reachable_wf w : ew_reachable w -> ew_wf w.
Proof.
  induction 1 as [|c w _ IH|c s w w' _ IH Ha|c w w' _ IH He|c w w' _ IH Hc].
  - split.
    + intros c i s h. unfold slot_at, obs_of, src_of. simpl. rewrite !lookup_empty. simpl.
      rewrite !lookup_nil. done.
    + intros c i s mh. unfold slot_at, src_of. simpl. rewrite lookup_empty. simpl.
      rewrite lookup_nil. done.
  - by apply wf_begin.
  - destruct (wf_add c s w IH) as (w'' & Ha' & Hw). congruence.
  - destruct (wf_end c w IH) as (w'' & He' & Hw & _). congruence.
  - destruct (wf_clear c w IH) as (w'' & Hc' & Hw). congruence.
Qed.

(** ** The source-list theorems *)

(** A read matching the cursor's slot advances the cursor and leaves every edge unchanged. *)
Theorem sl_add_positional self s w :
  fst <$> slot_at w self (scursor (src_of w self)) = Some s ->
  exists w', sl_add self s w = Some w' /\
    ew_obs w' = ew_obs w /\
    sslots (src_of w' self) = sslots (src_of w self) /\
    scursor (src_of w' self) = S (scursor (src_of w self)) /\
    forall c, c <> self -> src_of w' c = src_of w c.
Proof.
  intros Hp. unfold sl_add. unfold slot_at in Hp. rewrite decide_True by done.
  eexists. split; [reflexivity|]. split_and!; [done|..].
  - rewrite src_of_set_src, decide_True by done. done.
  - rewrite src_of_set_src, decide_True by done. done.
  - intros c Hne. rewrite src_of_set_src, decide_False by done. done.
Qed.

(** A read of the source just matched leaves the world unchanged. *)
Theorem sl_add_repeat self s w :
  fst <$> slot_at w self (scursor (src_of w self)) <> Some s ->
  0 < scursor (src_of w self) ->
  fst <$> slot_at w self (pred (scursor (src_of w self))) = Some s ->
  sl_add self s w = Some w.
Proof.
  intros Hp Hc Hr. unfold slot_at in *. unfold sl_add.
  rewrite decide_False by done. rewrite decide_True by done. done.
Qed.

Lemma slots_fst_trimmed self from w w' :
  trimmed self from w w' ->
  fst <$> sslots (src_of w' self) = take from (fst <$> sslots (src_of w self)).
Proof.
  intros (Hsame & Hnone & _ & _). apply list_eq. intros j.
  rewrite list_lookup_fmap.
  destruct (decide (j < from)) as [Hj|Hj].
  - rewrite lookup_take_lt by done. rewrite list_lookup_fmap. apply Hsame. done.
  - rewrite lookup_take_ge by lia. fold (slot_at w' self j). rewrite Hnone by lia. done.
Qed.

(** A read matching neither drops every slot from the cursor, unlinks the edges they held, then
    appends. *)
Theorem sl_add_truncates self s w :
  edge_match w ->
  fst <$> slot_at w self (scursor (src_of w self)) <> Some s ->
  ~ (0 < scursor (src_of w self) /\ fst <$> slot_at w self (pred (scursor (src_of w self))) = Some s) ->
  exists w', sl_add self s w = Some w' /\
    fst <$> sslots (src_of w' self) =
      take (scursor (src_of w self)) (fst <$> sslots (src_of w self)) ++ [s] /\
    scursor (src_of w' self) = length (sslots (src_of w' self)) /\
    edge_match w' /\
    forall s', s' ∉ fst <$> sslots (src_of w' self) -> self ∉ obs_view (obs_of w' s').
Proof.
  intros Hm Hp Hr. unfold slot_at in *. unfold sl_add.
  rewrite decide_False by done. rewrite decide_False by done.
  destruct (sl_end_ok self w Hm) as (w1 & -> & Hm1 & Ht). simpl.
  assert (Hm' := edge_match_link self s w1 Hm1).
  assert (Hshape : exists mh, sslots (src_of (sl_link self s w1) self) =
                     sslots (src_of w1 self) ++ [(s, mh)] /\
                   scursor (src_of (sl_link self s w1) self) = S (length (sslots (src_of w1 self)))).
  { destruct (sl_link_cases self s w1) as [[-> _]|[-> _]]; unfold sl_append;
      rewrite src_of_set_obs, src_of_set_src, decide_True by done; simpl; eauto. }
  destruct Hshape as (mh & Hsl & Hcur).
  eexists. split; [reflexivity|]. split_and!.
  - rewrite Hsl, fmap_app. simpl. rewrite (slots_fst_trimmed _ _ _ _ Ht). done.
  - rewrite Hcur, Hsl, length_app. simpl. lia.
  - done.
  - intros s' Hnot. rewrite view_elem. intros (q & i & Hq). apply Hm' in Hq.
    apply Hnot. by eapply slot_source.
Qed.

(** At most one slot of a source holds an edge, and that slot is the source's first. *)
Theorem sl_no_duplicate_edge w :
  ew_reachable w ->
  (forall c s i j hi hj,
     slot_at w c i = Some (s, Some hi) -> slot_at w c j = Some (s, Some hj) -> i = j) /\
  (forall c i s mh, slot_at w c i = Some (s, mh) ->
     (is_Some mh <-> forall j, j < i -> fst <$> slot_at w c j <> Some s)).
Proof.
  intros Hr. destruct (reachable_wf w Hr) as [_ Hf]. split.
  - intros c s i j hi hj. by apply first_owns_unique.
  - intros c. apply Hf.
Qed.

(** An observer occupies at most one position of a source's observers. *)
Theorem observer_held_once w s c i j :
  ew_reachable w ->
  obs_view (obs_of w s) !! i = Some c -> obs_view (obs_of w s) !! j = Some c -> i = j.
Proof.
  intros Hr Hi Hj. pose proof (reachable_wf w Hr) as Hw.
  assert (Hin : c ∈ obs_view (obs_of w s)) by (by eapply list_elem_of_lookup_2).
  apply (held_iff_slot _ _ _ Hw) in Hin.
  by apply (proj2 (wf_held_once w c s Hw Hin)).
Qed.

(** With one edge per source, a removal moves only another computation's edge. *)
Theorem trim_moves_only_others w self i s h o' m ms h' :
  ew_wf w ->
  slot_at w self i = Some (s, Some h) ->
  obs_remove_at self h (obs_of w s) = Some (o', Some (m, ms, h')) ->
  m <> self.
Proof.
  intros [Hm Hf] Hs Hr ->.
  destruct (obs_remove_at_spec _ _ _ _ _ Hr) as (_ & _ & _ & [[_ ?]|(m' & ms' & Hhl & Hl & Hn & _)]);
    [done|].
  simplify_eq.
  apply Hm in Hl.
  assert (ms' = i) as -> by (eapply first_owns_unique; [apply Hf|exact Hl|exact Hs]).
  rewrite Hs in Hl. simplify_eq. lia.
Qed.

(** Review Focus 4. A list reading A, B, A with an edge in each A slot, and the later A edge last in
    A's observers. The first removal moves it, and the later slot reads the moved handle. A clear then
    removes every edge. [trim_moves_only_others] puts this list outside [ew_reachable], and
    [aba_reachable_trim] is the corresponding run. *)
Theorem sl_trim_moves_own_edge w self A B ha hb ha' :
  edge_match w ->
  sslots (src_of w self) = [(A, Some ha); (B, Some hb); (A, Some ha')] ->
  S ha' = length (oitems (obs_of w A)) ->
  (exists w1, sl_remove_slot self 0 w = Some w1 /\ slot_at w1 self 2 = Some (A, Some ha)) /\
  (exists w', sl_clear self w = Some w' /\ sslots (src_of w' self) = [] /\
     forall s, self ∉ obs_view (obs_of w' s)).
Proof.
  intros Hm Hsl Hlast. split.
  - assert (H0 : slot_at w self 0 = Some (A, Some ha)) by (unfold slot_at; rewrite Hsl; done).
    assert (H2 : slot_at w self 2 = Some (A, Some ha')) by (unfold slot_at; rewrite Hsl; done).
    pose proof (proj1 (Hm _ _ _ _) H0) as Hh0.
    pose proof (proj1 (Hm _ _ _ _) H2) as Hh2.
    assert (Hne : ha <> ha') by (intros ->; congruence).
    destruct (obs_remove_at_ok _ _ _ _ Hh0) as [[o' n] Hr].
    destruct (obs_remove_at_spec _ _ _ _ _ Hr) as (_ & _ & _ & Hn).
    rewrite <- Hlast in Hn. simpl in Hn.
    destruct Hn as [[? _]|(m & ms & _ & Hl & -> & _)]; [done|].
    rewrite Hh2 in Hl. simplify_eq.
    unfold sl_remove_slot, remove_edge. rewrite H0, Hr. simpl.
    eexists. split; [reflexivity|].
    rewrite slot_at_set_handle, decide_False by lia.
    unfold sl_moved_in. rewrite slot_at_set_handle, decide_True by done.
    rewrite slot_at_set_obs, H2. done.
  - destruct (sl_clear_ok self w Hm) as (w' & Hr & _ & Hs & _ & _ & Hgone). eauto.
Qed.

(** Review Focus 5. A read missing its slot, with the source's observers above the scan limit and
    [olatest] holding another observer: [TUnknown], and one edge for the source afterwards, whether or
    not the source already had a slot. *)
Theorem sl_add_unknown_no_duplicate self s w w1 :
  ew_wf w ->
  fst <$> slot_at w self (scursor (src_of w self)) <> Some s ->
  ~ (0 < scursor (src_of w self) /\ fst <$> slot_at w self (pred (scursor (src_of w self))) = Some s) ->
  sl_end self w = Some w1 ->
  scan_limit < length (oitems (obs_of w1 s)) ->
  fst <$> oitems (obs_of w1 s) !! olatest (obs_of w1 s) <> Some self ->
  (obs_try_add self (length (sslots (src_of w1 self))) (obs_of w1 s)).2 = TUnknown /\
  exists w', sl_add self s w = Some w' /\ ew_wf w' /\ held_once self (obs_of w' s).
Proof.
  intros Hw Hp Hr He Habove Hstale. unfold slot_at in *. split.
  - unfold obs_try_add. rewrite decide_False by (unfold scan_limit in *; lia).
    rewrite decide_False by done. rewrite decide_False by lia. done.
  - unfold sl_add. rewrite decide_False by done. rewrite decide_False by done.
    rewrite He. simpl.
    destruct (wf_end self w Hw) as (w1' & He' & Hw1 & _).
    assert (w1' = w1) as -> by congruence.
    eexists. split; [reflexivity|].
    pose proof (wf_link self s w1 Hw1) as Hw'. split; [done|].
    apply wf_held_once; [done|].
    destruct (sl_link_cases self s w1) as [[-> _]|[-> _]]; unfold sl_append;
      rewrite src_of_set_obs, src_of_set_src, decide_True by done; simpl;
      rewrite fmap_app; apply elem_of_app; right; simpl; apply list_elem_of_singleton; done.
Qed.

(** ** Checked runs *)

Definition reads (rs : list (nid * nid)) (w : EdgeWorld) : option EdgeWorld :=
  foldl (fun mw r => mw ≫= sl_add r.1 r.2) (Some w) rs.

(** Observers 1 to 9, each reading source 5. *)
Definition nine_readers : list (nid * nid) :=
  [(1,5); (2,5); (3,5); (4,5); (5,5); (6,5); (7,5); (8,5); (9,5)].

(** 100 has a slot for 5, and reads 5 again with [olatest] on observer 9. *)
Example unknown_held :
  ((fun w => (obs_view (obs_of w 5), sslots (src_of w 100))) <$>
     reads ([(100,5); (100,6)] ++ nine_readers ++ [(100,5)]) empty_world) =
  Some ([100; 1; 2; 3; 4; 5; 6; 7; 8; 9], [(5, Some 0); (6, Some 0); (5, None)]).
Proof. vm_compute. reflexivity. Qed.

(** 100 has no slot for 5: the same answer, and 100 gets its one edge. *)
Example unknown_unheld :
  ((fun w => (obs_view (obs_of w 5), sslots (src_of w 100))) <$>
     reads (nine_readers ++ [(100,6); (100,5)]) empty_world) =
  Some ([1; 2; 3; 4; 5; 6; 7; 8; 9; 100], [(6, Some 0); (5, Some 9)]).
Proof. vm_compute. reflexivity. Qed.

(** A, B, A from a run, with 101 also observing A. A trim moves 101's edge, and 101's slot follows. *)
Example aba_reachable_trim :
  ((fun w => (obs_view (obs_of w 5), obs_view (obs_of w 6), sslots (src_of w 100),
              sslots (src_of w 101))) <$>
     (reads [(100,5); (100,6); (100,5); (101,5)] empty_world ≫= fun w =>
      sl_end 100 (sl_begin 100 w))) =
  Some ([101], [], [], [(5, Some 0)]).
Proof. vm_compute. reflexivity. Qed.

(** The list of [sl_trim_moves_own_edge], with A = 5 and B = 6. *)
Definition aba_world : EdgeWorld :=
  set_src 100 {| sslots := [(5, Some 0); (6, Some 0); (5, Some 1)]; scursor := 0 |}
    (set_obs 5 {| oitems := [(100, 0); (100, 2)]; olatest := 1 |}
       (set_obs 6 {| oitems := [(100, 1)]; olatest := 0 |} empty_world)).

Lemma aba_world_match : edge_match aba_world.
Proof.
  intros c i s h. unfold aba_world.
  rewrite slot_at_set_src, obs_of_set_src, slot_at_set_obs, slot_at_set_obs.
  rewrite !obs_of_set_obs.
  assert (Hempty : forall c' i', slot_at empty_world c' i' = None).
  { intros c' i'. unfold slot_at, src_of. simpl. rewrite lookup_empty. simpl.
    rewrite lookup_nil. done. }
  assert (Hobs : forall z q, oitems (obs_of empty_world z) !! q = None).
  { intros z q. unfold obs_of. simpl. rewrite lookup_empty. simpl. apply lookup_nil. }
  rewrite Hempty.
  destruct (decide (s = 5)) as [->|Hs5]; [|destruct (decide (s = 6)) as [->|Hs6]]; simpl.
  - destruct (decide (c = 100)) as [->|Hc]; simpl.
    + destruct i as [|[|[|i]]], h as [|[|h]]; simpl; split; intros Heq; simplify_eq; done.
    + destruct h as [|[|h]]; simpl; split; intros Heq; simplify_eq; done.
  - destruct (decide (c = 100)) as [->|Hc]; simpl.
    + destruct i as [|[|[|i]]], h as [|h]; simpl; split; intros Heq; simplify_eq; done.
    + destruct h as [|h]; simpl; split; intros Heq; simplify_eq; done.
  - rewrite Hobs. destruct (decide (c = 100)) as [->|Hc]; simpl; [|done].
    destruct i as [|[|[|i]]]; simpl; split; intros Heq; simplify_eq; done.
Qed.

(** [aba_world] meets the premises of [sl_trim_moves_own_edge]. *)
Example aba_world_premises :
  edge_match aba_world /\
  sslots (src_of aba_world 100) = [(5, Some 0); (6, Some 0); (5, Some 1)] /\
  S 1 = length (oitems (obs_of aba_world 5)).
Proof. split; [apply aba_world_match|]. vm_compute. auto. Qed.

(** [trimFrom] reading every handle before its first removal. *)
Fixpoint snapshot_trim (self : nid) (l : list (nid * option nat)) (w : EdgeWorld) : option EdgeWorld :=
  match l with
  | [] => Some w
  | (s, Some h) :: r => remove_edge self s h w ≫= snapshot_trim self r
  | (_, None) :: r => snapshot_trim self r w
  end.

(** Handles read before the removals fail on that list: the later A slot holds a stale handle. *)
Example snapshot_trim_fails :
  (fun _ => ()) <$> snapshot_trim 100 (sslots (src_of aba_world 100)) aba_world = None.
Proof. vm_compute. reflexivity. Qed.
