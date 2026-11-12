From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.

Open Scope nat_scope.

(** * Collection delta coalescing and `ProjectionReader` contracts

`Deltas.fs` implements `KeyChange`, `KeyChanges<'K>`, `ProjectionDelta<'K>`, `ProjectionReader<'K>`, and
`KeyLog<'K>` for incremental membership, order, and settled-value observation over projections and
editable keyed collections. To keep the underlying dense-array and hash-table representation open to
optimization while guaranteeing the documented consumer contracts, this module proves the exact minimum
specifications required by delta readers:

1. **`KeyChanges.Record` coalescing algebra (`Deltas.fs` lines 7–69)**:
   - `key_changes_added_removed_cancels`: recording `KcAdded` on an absent key followed by `KcRemoved` on
     the same key cancels out and removes the key (`delete k`), restoring the original map.
   - `key_changes_removed_added_replaces`: recording `KcRemoved` followed by `KcAdded` on the same key
     coalesces into `KcReplaced`.
   - `key_changes_membership_precedes_changed`: recording `KcChanged` on a key that already has a pending
     membership change (`KcAdded`, `KcRemoved`, or `KcReplaced`) preserves the membership change, while
     recording `KcChanged` on an absent key or a `KcChanged` key yields `KcChanged`.
2. **Dense swap-remove optimization refinement (`Deltas.fs` lines 47–55)**:
   - `key_changes_swap_remove_refines_delete`: the dense `ResizeArray` swap-remove implementation of
     `Added`-then-`Removed` cancellation preserves key uniqueness (`NoDup`) and refines map deletion
     (`list_to_map`), justifying why `ProjectionDelta.Changes` enumerates in an unspecified order.
3. **`ProjectionReader` bounded cap, reset recovery, and cursor advancement (`Deltas.fs` lines 160–236)**:
   - `projection_reader_cap_resets`: `pr_record` bounds `size (pr_changes pr) <= Nat.max 64 live_count`;
     exceeding the cap clears `pr_changes` to `∅` and sets `pr_reset = true`, so the next read reports
     `pd_is_reset = true` with `pd_changes = ∅`.
   - `projection_reader_read_advances_cursor`: `pr_read` delivers `(current_keys, pr_cursor)`, clears
     `pr_reset` and `pr_changes`, and advances `pr_cursor` to `current_keys`, so a subsequent read with
     no intervening changes reports `pd_is_empty = true`. *)

(** ** 1. `KeyChange` and `KeyChanges.Record` coalescing algebra *)

Inductive key_change :=
  | KcAdded
  | KcRemoved
  | KcReplaced
  | KcChanged.

Global Instance key_change_eq_dec : EqDecision key_change.
Proof. solve_decision. Defined.

(** Pure transition for a single key's pending entry in `KeyChanges.Record` (`Deltas.fs` lines 37–69):
    returns `None` when `KcAdded` followed by `KcRemoved` cancels out, and `Some next` otherwise. *)
Definition kc_merge_opt (prev : option key_change) (c : key_change) : option key_change :=
  match prev with
  | None => Some c
  | Some p =>
      if decide (c = KcRemoved /\ p = KcAdded) then None
      else if decide (c = KcAdded /\ p = KcRemoved) then Some KcReplaced
      else if decide (c = KcChanged) then
        if decide (p = KcChanged) then Some KcChanged else Some p
      else Some c
  end.

(** Map-level `KeyChanges.Record` over `kc : gmap nat key_change`. *)
Definition kc_record (k : nat) (c : key_change) (kc : gmap nat key_change) : gmap nat key_change :=
  match kc_merge_opt (kc !! k) c with
  | None => delete k kc
  | Some next => <[k := next]> kc
  end.

(**md
**Recording `KcAdded` on an absent key `k` followed by `KcRemoved` on `k` cancels out and restores `kc` (`Deltas.fs` lines 43–55).**

- [k:] the key added and then removed between two reads.
- [kc:] the pending changes map with `kc !! k = None`.
*)
Theorem key_changes_added_removed_cancels (k : nat) (kc : gmap nat key_change) :
  kc !! k = None ->
  kc_record k KcRemoved (kc_record k KcAdded kc) = kc /\
  kc_record k KcRemoved (kc_record k KcAdded kc) !! k = None.
Proof.
  intros Habs.
  unfold kc_record.
  rewrite Habs. simpl.
  rewrite lookup_insert_eq. simpl.
  rewrite delete_insert_id; [|exact Habs].
  split; [reflexivity | exact Habs].
Qed.

(**md
**Recording `KcRemoved` followed by `KcAdded` on the same key coalesces to `Some KcReplaced` (`Deltas.fs` lines 57–60).**

- [k:] the key removed and re-added between two reads.
- [kc:] the pending changes map ( provided `kc !! k <> Some KcAdded`, as a live key before removal is either absent from `kc` or not freshly `KcAdded`).
*)
Theorem key_changes_removed_added_replaces (k : nat) (kc : gmap nat key_change) :
  kc !! k <> Some KcAdded ->
  kc_record k KcAdded (kc_record k KcRemoved kc) !! k = Some KcReplaced /\
  (forall k', k' <> k -> kc_record k KcAdded (kc_record k KcRemoved kc) !! k' = kc !! k').
Proof.
  intros Hnot_add.
  unfold kc_record.
  assert (Hstep1 : kc_merge_opt (kc !! k) KcRemoved = Some KcRemoved).
  { destruct (kc !! k) as [p|]; simpl; [|done].
    rewrite decide_False; [|intros [_ Hp]; exact (Hnot_add (f_equal Some Hp))].
    simpl. reflexivity. }
  rewrite Hstep1.
  rewrite lookup_insert_eq. simpl.
  rewrite insert_insert_eq.
  split.
  - by rewrite lookup_insert_eq.
  - intros k' Hne. by rewrite lookup_insert_ne.
Qed.

(**md
**Membership changes (`KcAdded`, `KcRemoved`, `KcReplaced`) take precedence over `KcChanged`, while `KcChanged` on an absent or `KcChanged` key records `Some KcChanged` (`Deltas.fs` lines 61–63).**

- [k:] the key whose row value settled to a new value.
- [kc:] the pending changes map.
*)
Theorem key_changes_membership_precedes_changed (k : nat) (kc : gmap nat key_change) :
  (kc !! k = None -> kc_record k KcChanged kc !! k = Some KcChanged) /\
  (forall p,
     p = KcAdded \/ p = KcRemoved \/ p = KcReplaced ->
     kc !! k = Some p ->
     kc_record k KcChanged kc = kc) /\
  (kc !! k = Some KcChanged ->
   kc_record k KcChanged kc = kc).
Proof.
  split_and!.
  - intros Habs. unfold kc_record. rewrite Habs. simpl. by rewrite lookup_insert_eq.
  - intros p Hp Hk. unfold kc_record. rewrite Hk. simpl.
    destruct Hp as [->|[->| ->]]; simpl; apply insert_id, Hk.
  - intros Hk. unfold kc_record. rewrite Hk. simpl.
    apply insert_id, Hk.
Qed.

(** ** 2. Dense swap-remove optimization refinement *)

(** Models the dense `pairs` array swap-remove in `KeyChanges.Record` (`Deltas.fs` lines 47–55):
    when `k` is at index `idx` of `pairs ++ [last_pair]`, moving `last_pair` into `idx` and dropping the
    last element produces `<[idx := last_pair]> pairs`. *)
Definition swap_remove_at {A : Type} (idx : nat) (prefix : list A) (last_elem : A) : list A :=
  <[idx := last_elem]> prefix.

(**md
**Replacing index `idx` of `prefix` (where `prefix !! idx = Some (k, c)`) with `last_pair` and dropping the tail element refines `delete k (list_to_map (prefix ++ [last_pair]))` and preserves `NoDup`.**

Certifies the `Deltas.fs` lines 47–55 optimization: `KeyChanges<'K>` avoids $O(N)$ shifting on
`Added`-then-`Removed` cancellation by overwriting slot `idx` with the final entry `last_pair` and
truncating the `ResizeArray`, which permutes enumeration order while preserving the exact `gmap` of changes.

- [k:] the cancelled key at `idx`.
- [c:] the previous change at `idx`.
- [last_pair:] the final `(k_last, c_last)` pair in `pairs` before truncation.
- [prefix:] the prefix of `pairs` before `last_pair`.
- [idx:] the index of `(k, c)` in `prefix`.
*)
Theorem key_changes_swap_remove_refines_delete
    (k : nat) (c : key_change) (last_pair : nat * key_change)
    (prefix : list (nat * key_change)) (idx : nat) :
  NoDup (fst <$> (prefix ++ [last_pair])) ->
  prefix !! idx = Some (k, c) ->
  NoDup (fst <$> swap_remove_at idx prefix last_pair) /\
  (list_to_map (swap_remove_at idx prefix last_pair) : gmap nat key_change) =
    delete k (list_to_map (prefix ++ [last_pair])).
Proof.
  intros Hnd Hidx.
  unfold swap_remove_at.
  pose proof (lookup_lt_Some _ _ _ Hidx) as Hlt.
  apply take_drop_middle in Hidx.
  set (before := take idx prefix) in *.
  set (after := drop (S idx) prefix) in *.
  assert (Hlen : length before = idx).
  { subst before. apply length_take_le. lia. }
  rewrite <- Hidx in Hnd |- *.
  rewrite <- app_assoc in Hnd |- *.
  rewrite <- app_comm_cons in Hnd |- *.
  rewrite insert_app_r_alt; [|lia].
  rewrite Hlen, Nat.sub_diag. simpl.
  rewrite !fmap_app, !fmap_cons, !fmap_app, !fmap_cons in Hnd.
  rewrite fmap_app, fmap_cons.
  simpl in Hnd |- *.
  apply NoDup_app in Hnd as (Hnd_b & Hdisj_b & Hnd_rest).
  inversion Hnd_rest as [|x l Hk_nin Hnd_after]; subst x l.
  apply NoDup_app in Hnd_after as (Hnd_a & Hdisj_a & _).
  split.
  - apply NoDup_app. split_and!; [exact Hnd_b| |].
    + intros x Hxb Hxrest.
      apply elem_of_cons in Hxrest as [->|Hxa].
      * apply (Hdisj_b last_pair.1 Hxb).
        apply elem_of_cons. right. apply elem_of_app. right. by left.
      * apply (Hdisj_b x Hxb).
        apply elem_of_cons. right. apply elem_of_app. by left.
    + constructor.
      * intros Hlast_a. apply (Hdisj_a last_pair.1 Hlast_a). by left.
      * exact Hnd_a.
  - apply map_eq. intros q.
    destruct (decide (q = k)) as [->|Hqk].
    + rewrite lookup_delete_eq.
      apply not_elem_of_list_to_map_1.
      rewrite fmap_app, fmap_cons. simpl.
      intros Hk_in.
      apply elem_of_app in Hk_in as [Hkb|Hkrest].
      * apply (Hdisj_b k Hkb). by left.
      * apply elem_of_cons in Hkrest as [Hklast|Hka]; apply Hk_nin; apply elem_of_app.
        -- right. rewrite <- Hklast. by left.
        -- by left.
    + rewrite lookup_delete_ne; [|symmetry; exact Hqk].
      rewrite !list_to_map_app. simpl. rewrite list_to_map_app. simpl.
      destruct ((list_to_map before : gmap nat key_change) !! q) as [vq|] eqn:Eb.
      * rewrite !(lookup_union_Some_l _ _ _ _ Eb). reflexivity.
      * rewrite !lookup_union_r; [|exact Eb | exact Eb].
        simpl. rewrite (lookup_insert_ne _ k q c); [|symmetry; exact Hqk].
        destruct (decide (q = last_pair.1)) as [->|Hql].
        -- rewrite lookup_insert_eq.
           assert (Hla : (list_to_map after : gmap nat key_change) !! last_pair.1 = None).
           { apply not_elem_of_list_to_map_1. intros Hla.
             apply (Hdisj_a last_pair.1 Hla). by left. }
           rewrite lookup_union_r; [|exact Hla].
           rewrite lookup_insert_eq. reflexivity.
        -- rewrite lookup_insert_ne; [|symmetry; exact Hql].
           destruct ((list_to_map after : gmap nat key_change) !! q) as [va|] eqn:Ea.
           ++ rewrite (lookup_union_Some_l _ _ _ _ Ea). reflexivity.
           ++ rewrite lookup_union_r; [|exact Ea].
              rewrite lookup_insert_ne; [|symmetry; exact Hql].
              rewrite lookup_empty. reflexivity.
Qed.

(** ** 3. `ProjectionReader` bounded cap, reset recovery, and cursor advancement *)

Record proj_reader_state := {
  pr_changes : gmap nat key_change;
  pr_cursor : list nat;
  pr_reset : bool;
}.

Record proj_delta_state := {
  pd_changes : gmap nat key_change;
  pd_keys : list nat;
  pd_prev_keys : list nat;
  pd_is_reset : bool;
  pd_order_changed : bool;
  pd_is_empty : bool;
}.

(** `ProjectionReader.Record` (`Deltas.fs` lines 187–194): records `(k, c)` while `not reset`, and resets
    `changes` to `∅` with `reset := true` if `size changes > max 64 live_count`. *)
Definition pr_record (live_count : nat) (k : nat) (c : key_change) (pr : proj_reader_state)
    : proj_reader_state :=
  if pr_reset pr then pr
  else
    let kc' := kc_record k c (pr_changes pr) in
    if decide ( Nat.max 64 live_count < size kc' ) then
      {| pr_changes := ∅; pr_cursor := pr_cursor pr; pr_reset := true |}
    else
      {| pr_changes := kc'; pr_cursor := pr_cursor pr; pr_reset := false |}.

(** `ProjectionReader.Read` (`Deltas.fs` lines 207–236): returns a `ProjectionDelta` and advances
    `pr_cursor` to `current_keys`, clearing `pr_reset` and `pr_changes`. *)
Definition pr_read (current_keys : list nat) (pr : proj_reader_state)
    : proj_delta_state * proj_reader_state :=
  let ord_changed := bool_decide (current_keys <> pr_cursor pr) in
  let next_pr := {| pr_changes := ∅; pr_cursor := current_keys; pr_reset := false |} in
  if pr_reset pr then
    ({| pd_changes := ∅;
        pd_keys := current_keys;
        pd_prev_keys := pr_cursor pr;
        pd_is_reset := true;
        pd_order_changed := ord_changed;
        pd_is_empty := false |},
     next_pr)
  else
    let emp := bool_decide (size (pr_changes pr) = 0) && negb ord_changed in
    ({| pd_changes := pr_changes pr;
        pd_keys := current_keys;
        pd_prev_keys := pr_cursor pr;
        pd_is_reset := false;
        pd_order_changed := ord_changed;
        pd_is_empty := emp |},
     next_pr).

(**md
**`pr_record` keeps `size (pr_changes pr) <= Nat.max 64 live_count`, and when a new change exceeds that cap it clears `pr_changes` to `∅` and flags `pr_reset = true` so the next `pr_read` reports a reset.**

Proves the bounded-memory contract of `ProjectionReader` (`Deltas.fs` lines 150–152, 187–194, 213–219).

- [live_count:] the current live key count `host.LiveCount`.
- [k:] the key being recorded.
- [c:] the change being recorded.
- [current_keys:] the key array at the next read.
- [pr:] the reader state before `pr_record`.
*)
Theorem projection_reader_cap_resets
    (live_count k : nat) (c : key_change) (current_keys : list nat) (pr : proj_reader_state) :
  size (pr_changes pr) <= Nat.max 64 live_count ->
  let pr' := pr_record live_count k c pr in
  size (pr_changes pr') <= Nat.max 64 live_count /\
  (pr_reset pr = false ->
   Nat.max 64 live_count < size (kc_record k c (pr_changes pr)) ->
   pr_reset pr' = true /\
   pr_changes pr' = ∅ /\
   pd_is_reset (fst (pr_read current_keys pr')) = true /\
   pd_changes (fst (pr_read current_keys pr')) = ∅).
Proof.
  intros Hle. simpl.
  unfold pr_record.
  destruct (pr_reset pr) eqn:Eres.
  - split; [exact Hle | intros Hfalse; congruence].
  - destruct (decide (Nat.max 64 live_count < size (kc_record k c (pr_changes pr)))) as [Hgt|Hng].
    + simpl. split.
      * rewrite map_size_empty. lia.
      * intros _ _. repeat split; reflexivity.
    + simpl. split; [lia|].
      intros _ Hgt. contradiction.
Qed.

(**md
**`pr_read` delivers `(current_keys, pr_cursor pr)`, clears `pr_reset` and `pr_changes`, and advances `pr_cursor` to `current_keys`; an immediate second read with no intervening changes reports `pd_is_empty = true`.**

Proves the cursor-advancement and idle-read contracts of `ProjectionReader.Read` (`Deltas.fs` lines 200–236).

- [current_keys:] the key array returned by `host.ReadKeys()`.
- [pr:] the reader state before the first `Read()`.
*)
Theorem projection_reader_read_advances_cursor
    (current_keys : list nat) (pr : proj_reader_state) :
  let (d1, pr1) := pr_read current_keys pr in
  let (d2, pr2) := pr_read current_keys pr1 in
  pd_keys d1 = current_keys /\
  pd_prev_keys d1 = pr_cursor pr /\
  pr_cursor pr1 = current_keys /\
  pr_reset pr1 = false /\
  pr_changes pr1 = ∅ /\
  pd_is_reset d2 = false /\
  pd_changes d2 = ∅ /\
  pd_order_changed d2 = false /\
  pd_is_empty d2 = true /\
  pr2 = pr1.
Proof.
  unfold pr_read.
  destruct (pr_reset pr); simpl;
    rewrite (bool_decide_eq_false_2 (current_keys ≠ current_keys) (fun H => H eq_refl));
    rewrite map_size_empty; simpl;
    repeat split; reflexivity.
Qed.
