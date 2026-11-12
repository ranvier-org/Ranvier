From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.

Open Scope nat_scope.

(**md # Collection delta coalescing and ProjectionReader contracts

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
     no intervening changes reports `pd_is_empty = true`.
4. **Delta-driven `ProjectionFold` membership sync and non-invertible reorder completeness (`Combinators.fs` lines 1004–1085)**:
   - `fold_delta_sync_refines_diff`: applying two-phase `KcRemoved`/`KcReplaced` detach and
     `KcAdded`/`KcReplaced` attach over any `NoDup` enumeration of `pd_changes` yields the exact
     upstream `cur_entries` map in $O(\Delta)$, matching the $O(N)$ `Diff` fallback used when
     `pd_is_reset = true`.
   - `fold_pure_reorder_invariants`: on a pure upstream reorder (`pd_is_reset = false`, `pairs = []`,
     `pd_order_changed = true`), `foldGroup` (`invertible = true`) performs zero row-map or fold updates,
     whereas `fold` (`invertible = false`) marks `full = true` so `Recompute` re-folds in the new key order.
5. **`CollectionProjection` tail-append/remove order preservation on cancelled batches (`Collections.fs` lines 38–60)**:
   - `append_remove_empty_changes_preserves_order`: after any sequence of tail-append `ColAdd` and
     arbitrary-position `ColRemove` operations on a `NoDup` key list `init`, if the coalesced `KeyChanges`
     map is `∅`, then the resulting key list is identically equal to `init` — certifying the zero-allocation
     `changes.Size = 0` fast path in `CollectionProjection.ReadDelta`. *)

(** ** 1. KeyChange and KeyChanges.Record coalescing algebra *)

Inductive key_change :=
  | KcAdded
  | KcRemoved
  | KcReplaced
  | KcChanged.

Global Instance key_change_eq_dec : EqDecision key_change.
Proof. solve_decision. Defined.

(**md Pure transition for a single key's pending entry in `KeyChanges.Record` (`Deltas.fs` lines 37–69):
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

(**md Map-level `KeyChanges.Record` over `kc : gmap nat key_change`. *)
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

(**md Models the dense `pairs` array swap-remove in `KeyChanges.Record` (`Deltas.fs` lines 47–55):
    when `k` is at index `idx` of `pairs ++ [last_pair]`, moving `last_pair` into `idx` and dropping the
    last element produces `<[idx := last_pair]> pairs`. *)
Definition swap_remove_at {A : Type} (idx : nat) (prefix : list A) (last_elem : A) : list A :=
  <[idx := last_elem]> prefix.

(**md
**Replacing index `idx` of `prefix` (where `prefix !! idx = Some (k, c)`) with `last_pair` and dropping the tail element refines `delete k (list_to_map (prefix ++ [last_pair]))` and preserves `NoDup`.**

Certifies the `Deltas.fs` lines 47–55 optimization: `KeyChanges<'K>` avoids `O(N)` shifting on
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

(** ** 3. ProjectionReader bounded cap, reset recovery, and cursor advancement *)

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

(**md `ProjectionReader.Record` (`Deltas.fs` lines 187–194): records `(k, c)` while `not reset`, and resets
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

(**md `ProjectionReader.Read` (`Deltas.fs` lines 207–236): returns a `ProjectionDelta` and advances
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

(** ** 4. Delta-driven ProjectionFold membership sync and non-invertible reorder completeness *)

Definition fold_detach_one {E : Type} (m : gmap nat E) (kc : nat * key_change) : gmap nat E :=
  match kc.2 with
  | KcRemoved | KcReplaced => delete kc.1 m
  | KcAdded | KcChanged => m
  end.

Definition fold_attach_one {E : Type} (cur_entries : gmap nat E)
    (m : gmap nat E) (kc : nat * key_change) : gmap nat E :=
  match kc.2 with
  | KcAdded | KcReplaced =>
      match cur_entries !! kc.1 with
      | Some e => <[kc.1 := e]> m
      | None => m
      end
  | KcRemoved | KcChanged => m
  end.

Definition fold_apply_delta {E : Type} (cur_entries : gmap nat E)
    (pairs : list (nat * key_change)) (rows : gmap nat E) : gmap nat E :=
  let m1 := foldl fold_detach_one rows pairs in
  foldl (fold_attach_one cur_entries) m1 pairs.

Definition fold_sync_membership {E : Type}
    (invertible had_keys : bool) (cur_entries : gmap nat E)
    (pairs : list (nat * key_change)) (d : proj_delta_state)
    (rows : gmap nat E) (full : bool) : gmap nat E * bool :=
  let rows' :=
    if pd_is_reset d then cur_entries
    else if decide (length pairs = 0) then rows
    else fold_apply_delta cur_entries pairs rows in
  let full' :=
    if negb invertible && had_keys && pd_order_changed d then true else full in
  (rows', full').

Lemma fold_detach_lookup {E : Type}
    (pairs : list (nat * key_change)) (rows : gmap nat E) (q : nat) :
  NoDup (fst <$> pairs) ->
  foldl fold_detach_one rows pairs !! q =
    match (list_to_map pairs : gmap nat key_change) !! q with
    | Some KcRemoved | Some KcReplaced => None
    | _ => rows !! q
    end.
Proof.
  revert rows.
  induction pairs as [|[k c] rest IH]; intros rows Hnd; simpl in *.
  - by rewrite lookup_empty.
  - inversion Hnd as [|x l Hnin Hnd_rest]; subst x l.
    rewrite (IH _ Hnd_rest).
    destruct (decide (q = k)) as [->|Hne].
    + rewrite lookup_insert_eq.
      rewrite (not_elem_of_list_to_map_1 rest k Hnin).
      unfold fold_detach_one; simpl.
      destruct c; simpl; try reflexivity; by rewrite lookup_delete_eq.
    + rewrite lookup_insert_ne; [|symmetry; exact Hne].
      assert (Hq : fold_detach_one rows (k, c) !! q = rows !! q).
      { unfold fold_detach_one; simpl.
        destruct c; try reflexivity; apply lookup_delete_ne; symmetry; exact Hne. }
      by rewrite Hq.
Qed.

Lemma fold_attach_lookup {E : Type}
    (cur_entries : gmap nat E) (pairs : list (nat * key_change)) (m1 : gmap nat E) (q : nat) :
  NoDup (fst <$> pairs) ->
  foldl (fold_attach_one cur_entries) m1 pairs !! q =
    match (list_to_map pairs : gmap nat key_change) !! q with
    | Some KcAdded | Some KcReplaced =>
        match cur_entries !! q with
        | Some e => Some e
        | None => m1 !! q
        end
    | _ => m1 !! q
    end.
Proof.
  revert m1.
  induction pairs as [|[k c] rest IH]; intros m1 Hnd; simpl in *.
  - by rewrite lookup_empty.
  - inversion Hnd as [|x l Hnin Hnd_rest]; subst x l.
    rewrite (IH _ Hnd_rest).
    destruct (decide (q = k)) as [->|Hne].
    + rewrite lookup_insert_eq.
      rewrite (not_elem_of_list_to_map_1 rest k Hnin).
      unfold fold_attach_one; simpl.
      destruct c; simpl; try reflexivity;
        destruct (cur_entries !! k) as [e|]; try reflexivity;
        by rewrite lookup_insert_eq.
    + rewrite lookup_insert_ne; [|symmetry; exact Hne].
      assert (Hq : fold_attach_one cur_entries m1 (k, c) !! q = m1 !! q).
      { unfold fold_attach_one; simpl.
        destruct c; try reflexivity;
          destruct (cur_entries !! k) as [e|]; try reflexivity;
          apply lookup_insert_ne; symmetry; exact Hne. }
      by rewrite Hq.
Qed.

(**md
**`ProjectionFold.Sync` refines `upstream.Entries` in $O(\Delta)$ on normal deltas (for any `NoDup` enumeration order of `pd_changes`) and via `Diff` when `pd_is_reset = true` (`Combinators.fs` lines 1004–1085).**

- [E:] the upstream row-entry type (`RowEntry<'K, 'V>`).
- [invertible:] `true` for `foldGroup`, `false` for `fold`.
- [had_keys:] `true` after the fold's initial read.
- [cur_entries:] the current `upstream.Entries` map.
- [rows:] the fold's `rows` map from the previous read.
- [pairs:] the dense `KeyChanges` array entries enumerated by `0 .. delta.ChangeCount - 1`.
- [d:] the `ProjectionDelta` returned by `keyReader.Read()`.
- [full:] the `full` recompute flag entering `Sync`.
*)
Theorem fold_delta_sync_refines_diff {E : Type}
    (invertible had_keys : bool) (cur_entries rows : gmap nat E)
    (pairs : list (nat * key_change)) (d : proj_delta_state) (full : bool) :
  (pd_is_reset d = false ->
   NoDup (fst <$> pairs) /\
   pd_changes d = list_to_map pairs /\
   (forall q,
      (pd_changes d !! q = Some KcRemoved -> cur_entries !! q = None) /\
      (pd_changes d !! q = Some KcAdded -> is_Some (cur_entries !! q)) /\
      (pd_changes d !! q = Some KcReplaced -> is_Some (cur_entries !! q)) /\
      (pd_changes d !! q = None \/ pd_changes d !! q = Some KcChanged ->
       cur_entries !! q = rows !! q))) ->
  fst (fold_sync_membership invertible had_keys cur_entries pairs d rows full) = cur_entries.
Proof.
  intros Hcoh.
  unfold fold_sync_membership; simpl.
  destruct (pd_is_reset d) eqn:Eres; [reflexivity|].
  destruct (Hcoh eq_refl) as (Hnd & Hmap & Hkeys).
  destruct (decide (length pairs = 0)) as [Hlen|Hlen].
  - apply nil_length_inv in Hlen; subst pairs.
    simpl in Hmap.
    apply map_eq; intros q.
    destruct (Hkeys q) as (_ & _ & _ & Hsame).
    symmetry. apply Hsame. left. rewrite Hmap. apply lookup_empty.
  - unfold fold_apply_delta.
    apply map_eq; intros q.
    rewrite (fold_attach_lookup cur_entries pairs _ q Hnd).
    rewrite (fold_detach_lookup pairs rows q Hnd).
    rewrite <- Hmap.
    destruct (Hkeys q) as (Hrem & Hadd & Hrep & Hsame).
    destruct (pd_changes d !! q) as [c|] eqn:Eqc.
    + destruct c.
      * destruct (Hadd eq_refl) as [e He]. by rewrite He.
      * symmetry. exact (Hrem eq_refl).
      * destruct (Hrep eq_refl) as [e He]. by rewrite He.
      * symmetry. apply Hsame. by right.
    + symmetry. apply Hsame. by left.
Qed.

(**md
**On a pure upstream reorder (`pd_is_reset d = false`, `pairs = []`, `had_keys = true`, `pd_order_changed d = true`), `foldGroup` (`invertible = true`) leaves `(rows, full)` untouched, whereas `fold` (`invertible = false`) sets `full = true` so `Recompute` folds in the new key order (`Combinators.fs` lines 1072–1085).**

- [E:] the upstream row-entry type.
- [cur_entries:] the current `upstream.Entries` map.
- [rows:] the fold's `rows` map.
- [d:] a pure-reorder `ProjectionDelta`.
- [full:] the `full` recompute flag entering `Sync`.
*)
Theorem fold_pure_reorder_invariants {E : Type}
    (cur_entries rows : gmap nat E) (d : proj_delta_state) (full : bool) :
  pd_is_reset d = false ->
  pd_order_changed d = true ->
  fold_sync_membership true true cur_entries [] d rows full = (rows, full) /\
  fold_sync_membership false true cur_entries [] d rows full = (rows, true).
Proof.
  intros Hres Hord.
  unfold fold_sync_membership.
  rewrite Hres, Hord; simpl.
  split; reflexivity.
Qed.

(** ** 5. CollectionProjection tail-append/remove order preservation on cancelled batches *)

Inductive col_op :=
  | ColAdd (k : nat)
  | ColRemove (k : nat).

Definition col_step (st : list nat * gmap nat key_change) (op : col_op)
    : list nat * gmap nat key_change :=
  let (cur, kc) := st in
  match op with
  | ColAdd k =>
      if decide (k ∈ cur) then (cur, kc)
      else (cur ++ [k], kc_record k KcAdded kc)
  | ColRemove k =>
      if decide (k ∈ cur) then (filter (fun x => x <> k) cur, kc_record k KcRemoved kc)
      else (cur, kc)
  end.

Definition col_inv (init cur : list nat) (kc : gmap nat key_change) : Prop :=
  NoDup cur /\
  (forall x, x ∉ init -> x ∈ cur -> kc !! x = Some KcAdded \/ kc !! x = Some KcReplaced) /\
  (forall x, x ∈ init -> kc !! x <> Some KcAdded) /\
  (forall x, x ∈ init -> x ∉ cur -> kc !! x = Some KcRemoved) /\
  ((forall x, x ∈ init -> kc !! x = None) ->
   exists sfx, cur = init ++ sfx /\ (forall y, y ∈ sfx -> y ∉ init)).

Lemma filter_ne_not_elem (k : nat) (l : list nat) :
  k ∉ l ->
  filter (fun x => x <> k) l = l.
Proof.
  induction l as [|x rest IH]; intros Hnin; [reflexivity|].
  apply not_elem_of_cons in Hnin as [Hne Hnin_rest].
  rewrite filter_cons_True; [|symmetry; exact Hne].
  f_equal. exact (IH Hnin_rest).
Qed.

Lemma elem_of_filter_ne_iff (k x : nat) (l : list nat) :
  x ∈ filter (fun y => y <> k) l <-> x <> k /\ x ∈ l.
Proof.
  induction l as [|y rest IH]; [set_solver|].
  destruct (decide (y <> k)) as [Hne|Hne].
  - rewrite filter_cons_True by exact Hne. set_solver.
  - rewrite filter_cons_False by exact Hne.
    assert (y = k) by lia; subst y. set_solver.
Qed.

Lemma col_step_preserves_inv (init cur : list nat) (kc : gmap nat key_change) (op : col_op) :
  col_inv init cur kc ->
  let (cur', kc') := col_step (cur, kc) op in
  col_inv init cur' kc'.
Proof.
  intros (Hnd & Hfresh & Hno_add & Hrem_init & Hord).
  destruct op as [k | k]; simpl.
  - destruct (decide (k ∈ cur)) as [Hin|Hnin].
    + exact (conj Hnd (conj Hfresh (conj Hno_add (conj Hrem_init Hord)))).
    + split_and!.
      * apply NoDup_app; split_and!; [exact Hnd| |apply NoDup_singleton].
        set_solver.
      * intros x Hx_ninit Hx_cur'.
        apply elem_of_app in Hx_cur' as [Hx_cur | Hxk].
        -- destruct (decide (x = k)) as [->|Hne]; [contradiction|].
           unfold kc_record. destruct (kc_merge_opt (kc !! k) KcAdded) as [nxt|].
           ++ rewrite lookup_insert_ne; [|symmetry; exact Hne].
              exact (Hfresh x Hx_ninit Hx_cur).
           ++ rewrite lookup_delete_ne; [|symmetry; exact Hne].
              exact (Hfresh x Hx_ninit Hx_cur).
        -- assert (x = k) by set_solver; subst x.
           unfold kc_record.
           destruct (kc !! k) as [p|] eqn:Ekc; simpl.
           ++ destruct p; simpl; rewrite lookup_insert_eq; [by left | by right | by left | by left].
           ++ rewrite lookup_insert_eq. by left.
      * intros x Hx_init.
        destruct (decide (x = k)) as [->|Hne].
        -- specialize (Hrem_init k Hx_init Hnin).
           unfold kc_record. rewrite Hrem_init; simpl.
           rewrite lookup_insert_eq. discriminate.
        -- unfold kc_record. destruct (kc_merge_opt (kc !! k) KcAdded) as [nxt|].
           ++ rewrite lookup_insert_ne; [|symmetry; exact Hne].
              exact (Hno_add x Hx_init).
           ++ rewrite lookup_delete_ne; [|symmetry; exact Hne].
              exact (Hno_add x Hx_init).
      * intros x Hx_init Hx_ncur'.
        assert (Hne : x <> k) by set_solver.
        assert (Hx_ncur : x ∉ cur) by set_solver.
        unfold kc_record. destruct (kc_merge_opt (kc !! k) KcAdded) as [nxt|].
        -- rewrite lookup_insert_ne; [|symmetry; exact Hne].
           exact (Hrem_init x Hx_init Hx_ncur).
        -- rewrite lookup_delete_ne; [|symmetry; exact Hne].
           exact (Hrem_init x Hx_init Hx_ncur).
      * intros Hall_none.
        assert (Hk_ninit : k ∉ init).
        { intros Hk_init.
          pose proof (Hrem_init k Hk_init Hnin) as Hkr.
          pose proof (Hall_none k Hk_init) as Hkn.
          unfold kc_record in Hkn. rewrite Hkr in Hkn; simpl in Hkn.
          rewrite lookup_insert_eq in Hkn. discriminate. }
        assert (Hall_prev : forall x, x ∈ init -> kc !! x = None).
        { intros x Hx_init.
          assert (Hne : x <> k) by (intros ->; contradiction).
          pose proof (Hall_none x Hx_init) as Hxn.
          unfold kc_record in Hxn.
          destruct (kc_merge_opt (kc !! k) KcAdded) as [nxt|].
          - rewrite lookup_insert_ne in Hxn; [exact Hxn | symmetry; exact Hne].
          - rewrite lookup_delete_ne in Hxn; [exact Hxn | symmetry; exact Hne]. }
        destruct (Hord Hall_prev) as (sfx & Hcur_eq & Hsfx).
        exists (sfx ++ [k]).
        split.
        -- rewrite Hcur_eq, <- app_assoc. reflexivity.
        -- intros y Hy. apply elem_of_app in Hy as [Hy | Hy].
           ++ exact (Hsfx y Hy).
           ++ assert (y = k) by set_solver; subst y; exact Hk_ninit.
  - destruct (decide (k ∈ cur)) as [Hin|Hnin].
    + assert (Hrem_step : forall q, q <> k -> kc_record k KcRemoved kc !! q = kc !! q).
      { intros q Hne. unfold kc_record.
        destruct (kc_merge_opt (kc !! k) KcRemoved) as [nxt|].
        - apply lookup_insert_ne; symmetry; exact Hne.
        - apply lookup_delete_ne; symmetry; exact Hne. }
      assert (Hrem_init_k : k ∈ init -> kc_record k KcRemoved kc !! k = Some KcRemoved).
      { intros Hk_init. pose proof (Hno_add k Hk_init) as Hna.
        unfold kc_record. destruct (kc !! k) as [p|] eqn:Ep; simpl.
        - destruct p; [exfalso; exact (Hna eq_refl) | | |]; simpl; by rewrite lookup_insert_eq.
        - by rewrite lookup_insert_eq. }
      split_and!.
      * apply NoDup_filter, Hnd.
      * intros x Hx_ninit Hx_cur'.
        apply elem_of_filter_ne_iff in Hx_cur' as [Hne Hx_cur].
        rewrite (Hrem_step x Hne).
        exact (Hfresh x Hx_ninit Hx_cur).
      * intros x Hx_init.
        destruct (decide (x = k)) as [->|Hne].
        -- rewrite (Hrem_init_k Hx_init). discriminate.
        -- rewrite (Hrem_step x Hne). exact (Hno_add x Hx_init).
      * intros x Hx_init Hx_ncur'.
        destruct (decide (x = k)) as [->|Hne].
        -- exact (Hrem_init_k Hx_init).
        -- rewrite (Hrem_step x Hne). apply Hrem_init; [exact Hx_init|].
           intros Hx_cur. apply Hx_ncur', elem_of_filter_ne_iff. split; [exact Hne | exact Hx_cur].
      * intros Hall_none.
        assert (Hk_ninit : k ∉ init).
        { intros Hk_init.
          pose proof (Hrem_init_k Hk_init) as Hkr.
          pose proof (Hall_none k Hk_init) as Hkn. congruence. }
        assert (Hall_prev : forall x, x ∈ init -> kc !! x = None).
        { intros x Hx_init.
          assert (Hne : x <> k) by (intros ->; contradiction).
          rewrite <- (Hrem_step x Hne). exact (Hall_none x Hx_init). }
        destruct (Hord Hall_prev) as (sfx & Hcur_eq & Hsfx).
        exists (filter (fun x => x <> k) sfx).
        split.
        -- rewrite Hcur_eq, filter_app, (filter_ne_not_elem k init Hk_ninit). reflexivity.
        -- intros y Hy. apply elem_of_filter_ne_iff in Hy as [_ Hy]. exact (Hsfx y Hy).
    + exact (conj Hnd (conj Hfresh (conj Hno_add (conj Hrem_init Hord)))).
Qed.

(**md
**After any sequence of tail-append `ColAdd` and arbitrary-position `ColRemove` operations on a `NoDup` key list `init`, if the coalesced `KeyChanges` map is `∅`, the resulting key list equals `init` (`Collections.fs` lines 38–60).**

- [init:] the ordered key list `previous` from the prior `ReadDelta`.
- [ops:] any sequence of `AddOrUpdate` (new-key append) and `Remove` operations within a batch.
*)
Theorem append_remove_empty_changes_preserves_order (init : list nat) (ops : list col_op) :
  NoDup init ->
  let (cur, kc) := foldl col_step (init, ∅) ops in
  kc = ∅ ->
  cur = init.
Proof.
  intros Hnd.
  assert (Hinv0 : col_inv init init ∅).
  { split_and!.
    - exact Hnd.
    - intros x Hni Hi; contradiction.
    - intros x _. rewrite lookup_empty. discriminate.
    - intros x Hi Hni; contradiction.
    - intros _. exists []. rewrite app_nil_r. split; [reflexivity | intros y Hy; inversion Hy]. }
  assert (Hinv : forall st ops',
            col_inv init st.1 st.2 ->
            let (cur, kc) := foldl col_step st ops' in
            col_inv init cur kc).
  { intros st ops'. revert st.
    induction ops' as [|op rest IH]; intros [c m] Hinv_cm; cbn [foldl].
    - exact Hinv_cm.
    - pose proof (col_step_preserves_inv init c m op Hinv_cm) as Hnext.
      destruct (col_step (c, m) op) as [c' m'].
      exact (IH (c', m') Hnext). }
  specialize (Hinv (init, ∅) ops Hinv0).
  destruct (foldl col_step (init, ∅) ops) as [cur kc].
  intros Hkc_emp. subst kc.
  destruct Hinv as (_ & Hfresh & _ & _ & Hord).
  assert (Hall_none : forall x, x ∈ init -> (∅ : gmap nat key_change) !! x = None).
  { intros x _. apply lookup_empty. }
  destruct (Hord Hall_none) as (sfx & Hcur_eq & Hsfx).
  destruct sfx as [|y sfx'].
  - rewrite app_nil_r in Hcur_eq. exact Hcur_eq.
  - exfalso.
    assert (Hy_sfx : y ∈ y :: sfx') by left.
    pose proof (Hsfx y Hy_sfx) as Hy_ninit.
    assert (Hy_cur : y ∈ cur).
    { rewrite Hcur_eq. apply elem_of_app; right; exact Hy_sfx. }
    destruct (Hfresh y Hy_ninit Hy_cur) as [Hy1 | Hy2];
      [rewrite lookup_empty in Hy1; discriminate | rewrite lookup_empty in Hy2; discriminate].
Qed.
