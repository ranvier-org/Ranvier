From stdpp Require Import prelude.
From Ranvier.Base Require Import Prelude.
From Ranvier.Pure Require Import ObserverSet.
From iris.heap_lang Require Import proofmode notation.
From iris.heap_lang.lib Require Import array assert.

(** * The observer set in HeapLang

An observer set is a block of five fields: the cell of position 0 (field 0, F#'s [first] and [firstSlot]),
the overflow array of the later positions (field 1, [rest]), [count] (field 2), [latest] (field 3), and the
overflow array's capacity (field 4, [rest.Length] in F#). A cell is the pair of the observer's id and its
slot, the .NET [ObserverSlot] struct. Cells past [count] are blank. *)

(** The cell of a blanked position: no observer and [EdgeHandle.Unlinked]. *)
Definition blank_cell : val := (#(-1), #(-1)).

Definition observer_cell (p : node_id * nat) : val := (#(Z.of_nat p.1), #(Z.of_nat p.2)).

(**md
The cell of position `j` of the observers `xs`, blank past the end.
*)
Definition obs_fun (xs : list (node_id * nat)) (j : nat) : val :=
  default blank_cell (observer_cell <$> xs !! j).

(** [f] with position [i] replaced by [v]. *)
Definition obs_fupd (f : nat -> val) (i : nat) (v : val) (j : nat) : val :=
  if decide (j = i) then v else f j.

(** [TryAdd]'s answer: the handle, [EdgeHandle.Unlinked] or [EdgeHandle.Unknown]. *)
Definition tried_val (t : tried) : val :=
  match t with TAdded h => #(Z.of_nat h) | TUnlinked => #(-1) | TUnknown => #(-2) end.

(** The [MovedIn] notice of a removal: the moved observer, its slot and its new handle. *)
Definition moved_val (m : option (node_id * nat * nat)) : val :=
  match m with
  | None => NONEV
  | Some (c, s, h) => SOMEV (#(Z.of_nat c), #(Z.of_nat s), #(Z.of_nat h))
  end.

(** Reading and writing cell [i]: position 0 sits inline at [l], later positions sit in [rest] at [i - 1]. *)
Definition observers_cell_code : val :=
  λ: "l" "i", if: "i" = #0 then ! "l" else ! (! ("l" +ₗ #1) +ₗ ("i" - #1)).

Definition observers_set_cell_code : val :=
  λ: "l" "i" "v", if: "i" = #0 then "l" <- "v" else (! ("l" +ₗ #1) +ₗ ("i" - #1)) <- "v".

(** [new ObserverSet ()]: position 0 blank and an overflow array of capacity 0. *)
Definition observers_new_code : val :=
  λ: <>,
    let: "l" := AllocN #5 blank_cell in
    ("l" +ₗ #1) <- "l";; ("l" +ₗ #2) <- #0;; ("l" +ₗ #3) <- #0;; ("l" +ₗ #4) <- #0;; "l".

(** [growRest]: the overflow capacity after position 0 doubles, to at least 4. *)
Definition observers_grow_code : val :=
  λ: "l",
    let: "cap" := ! ("l" +ₗ #4) in
    let: "size" := if: #4 < "cap" * #2 then "cap" * #2 else #4 in
    let: "b" := AllocN "size" blank_cell in
    array_copy_to "b" (! ("l" +ₗ #1)) "cap";;
    ("l" +ₗ #1) <- "b";;
    ("l" +ₗ #4) <- "size".

(** [Add]. *)
Definition observers_add_code : val :=
  λ: "l" "c" "slot",
    let: "h" := ! ("l" +ₗ #2) in
    if: "h" = #0 then
      "l" <- ("c", "slot");;
      ("l" +ₗ #2) <- #1;;
      ("l" +ₗ #3) <- #0;;
      #0
    else
      (if: "h" - #1 = ! ("l" +ₗ #4) then observers_grow_code "l" else #());;
      observers_set_cell_code "l" "h" ("c", "slot");;
      ("l" +ₗ #2) <- "h" + #1;;
      ("l" +ₗ #3) <- "h";;
      "h".

(** [TryAdd]'s scan: whether a position from [i] up to [n] holds [c]. *)
Definition observers_scan_code : val :=
  rec: "scan" "a" "i" "n" "c" :=
    if: "i" < "n" then
      if: Fst (! ("a" +ₗ "i")) = "c" then #true else "scan" "a" ("i" + #1) "n" "c"
    else #false.

(** [TryAdd]. *)
Definition observers_try_add_code : val :=
  λ: "l" "c" "slot",
    let: "n" := ! ("l" +ₗ #2) in
    if: "n" = #0 then observers_add_code "l" "c" "slot"
    else
      let: "lt" := ! ("l" +ₗ #3) in
      if: ("lt" < "n") && (Fst (observers_cell_code "l" "lt") = "c") then #(-1)
      else if: "n" = #1 then
        if: Fst (! "l") = "c" then #(-1) else observers_add_code "l" "c" "slot"
      else if: "n" ≤ #(Z.of_nat scan_limit) then
        if: Fst (! "l") = "c" then #(-1)
        else if: observers_scan_code (! ("l" +ₗ #1)) #0 ("n" - #1) "c" then #(-1)
        else observers_add_code "l" "c" "slot"
      else #(-2).

(** [RemoveAt]. A handle holding another observer fails the assertion, the [InvalidOperationException]. The
    result is the [MovedIn] notice, which the caller delivers. *)
Definition observers_remove_at_code : val :=
  λ: "l" "c" "h",
    let: "n" := ! ("l" +ₗ #2) in
    (assert: ("h" < "n") && (Fst (observers_cell_code "l" "h") = "c"));;
    let: "last" := "n" - #1 in
    if: "h" = "last" then
      observers_set_cell_code "l" "last" blank_cell;;
      ("l" +ₗ #2) <- "last";;
      NONE
    else
      let: "mv" := observers_cell_code "l" "last" in
      observers_set_cell_code "l" "h" "mv";;
      observers_set_cell_code "l" "last" blank_cell;;
      ("l" +ₗ #2) <- "last";;
      (if: ! ("l" +ₗ #3) = "last" then ("l" +ₗ #3) <- "h" else #());;
      SOME (Fst "mv", Snd "mv", "h").

(** The walk of [NotifyDirty], [NotifyDirtyExcept] and [NotifyCheck] from cursor [n], the F# [i + 1]. Each
    step re-clamps the cursor to the count and calls [visit] on the observer below it. *)
Definition observers_walk_loop : val :=
  rec: "loop" "l" "visit" "n" :=
    let: "k" := ! ("l" +ₗ #2) in
    let: "m" := if: "n" ≤ "k" then "n" else "k" in
    if: "m" = #0 then #()
    else
      let: "i" := "m" - #1 in
      "visit" (Fst (observers_cell_code "l" "i"));;
      "loop" "l" "visit" "i".

Definition observers_walk_code : val :=
  λ: "l" "visit",
    let: "n" := ! ("l" +ₗ #2) in
    if: "n" = #1 then
      "visit" (Fst (! "l"));; #()
    else if: #1 < "n" then
      observers_walk_loop "l" "visit" "n"
    else #().

(** ** Cells *)

Lemma obs_fun_lookup xs j p : xs !! j = Some p -> obs_fun xs j = observer_cell p.
Proof. intros Hj. rewrite /obs_fun Hj //. Qed.

Lemma obs_fun_ge xs j : length xs <= j -> obs_fun xs j = blank_cell.
Proof. intros Hj. rewrite /obs_fun lookup_ge_None_2 //. Qed.

Lemma obs_rest_lookup (f : nat -> val) cap j :
  j < cap -> ((fun k => f (S k)) <$> seq 0 cap) !! j = Some (f (S j)).
Proof. intros Hj. rewrite list_lookup_fmap lookup_seq_lt //. Qed.

Lemma obs_rest_ext (f g : nat -> val) cap :
  (forall j, j < cap -> f (S j) = g (S j)) ->
  (fun k => f (S k)) <$> seq 0 cap = (fun k => g (S k)) <$> seq 0 cap.
Proof. intros H. apply list_fmap_ext. intros i x Hx. apply lookup_seq in Hx as [-> ?]. apply H. lia. Qed.

Lemma obs_rest_insert (f : nat -> val) cap j v :
  j < cap -> <[j := v]> ((fun k => f (S k)) <$> seq 0 cap) = (fun k => obs_fupd f (S j) v (S k)) <$> seq 0 cap.
Proof.
  intros Hj. apply list_eq. intros i. rewrite list_lookup_fmap.
  destruct (decide (i < cap)) as [Hi|Hi].
  - rewrite lookup_seq_lt //=. rewrite /obs_fupd. destruct (decide (i = j)) as [->|Hne].
    + rewrite list_lookup_insert_eq; [|rewrite length_fmap length_seq; lia]. rewrite decide_True //.
    + rewrite list_lookup_insert_ne // obs_rest_lookup // decide_False //. lia.
  - rewrite lookup_seq_ge; [|lia]. rewrite lookup_ge_None_2 //. rewrite length_insert length_fmap length_seq. lia.
Qed.

Lemma obs_fun_snoc xs p j :
  obs_fun (xs ++ [p]) j = obs_fupd (obs_fun xs) (length xs) (observer_cell p) j.
Proof.
  rewrite /obs_fupd /obs_fun. destruct (decide (j = length xs)) as [->|Hne].
  - rewrite lookup_app_r // Nat.sub_diag //.
  - destruct (decide (j < length xs)) as [Hlt|Hge].
    + rewrite lookup_app_l //.
    + rewrite !lookup_ge_None_2 // ?length_app /=; lia.
Qed.

Lemma obs_fun_pop xs k j :
  length xs = S k ->
  obs_fun (take k xs) j = obs_fupd (obs_fun xs) k blank_cell j.
Proof.
  intros Hlen. rewrite /obs_fupd /obs_fun. destruct (decide (j = k)) as [->|Hne].
  - rewrite lookup_ge_None_2 // length_take; lia.
  - destruct (decide (j < k)) as [Hlt|Hge].
    + rewrite lookup_take_lt //.
    + rewrite !lookup_ge_None_2 // ?length_take; lia.
Qed.

Lemma obs_fun_insert xs i p j :
  i < length xs ->
  obs_fun (<[i := p]> xs) j = obs_fupd (obs_fun xs) i (observer_cell p) j.
Proof.
  intros Hi. rewrite /obs_fupd /obs_fun. destruct (decide (j = i)) as [->|Hne].
  - rewrite list_lookup_insert_eq //.
  - rewrite list_lookup_insert_ne //.
Qed.

Lemma obs_fun_ext_eq xs ys :
  (forall j, obs_fun xs j = obs_fun ys j) ->
  xs = ys.
Proof.
  intros H. apply list_eq. intros j. specialize (H j). rewrite /obs_fun in H.
  destruct (xs !! j) as [px|] eqn:Ex, (ys !! j) as [py|] eqn:Ey; simpl in *; try done.
  - destruct px, py. injection H. intros. subst. f_equal. f_equal; lia.
  - unfold observer_cell, blank_cell in H. destruct px. injection H. lia.
  - unfold observer_cell, blank_cell in H. destruct py. injection H. lia.
Qed.

(** ** Mutation points *)

Lemma mutation_points_join (P : observer_set -> Prop) ms r o o1 :
  apply_mutations ms o = Some o1 ->
  Forall P (mutation_points ms o) -> Forall P (mutation_points r o1) ->
  Forall P (mutation_points (ms ++ r) o).
Proof.
  revert o. induction ms as [|m ms IH]; intros o Ha H1 H2; simpl in *.
  - by simplify_eq.
  - destruct (apply_mutation m o) as [o2|] eqn:E; simpl in *; [|done].
    inversion H1 as [|?? Ho H1']; subst. constructor; [done|]. by eapply IH.
Qed.

Section heap.
Context `{!heapGS Σ}.

(**md
The fields of an observer set at `l`: position `j`'s cell is `f j` for `j` up to `cap`, with `count`
and `latest`.

- [l:] the location of the observer set's fields.
- [f:] the cell of each position.
- [cap:] the capacity of the overflow array after position 0.
- [count:] the `count` field.
- [latest:] the `latest` field.
*)
Definition obs_rep (l : loc) (f : nat -> val) (cap count latest : nat) : iProp Σ :=
  ∃ a : loc,
    l ↦ f 0 ∗ (l +ₗ 1) ↦ #a ∗ (l +ₗ 2) ↦ #(Z.of_nat count) ∗
    (l +ₗ 3) ↦ #(Z.of_nat latest) ∗ (l +ₗ 4) ↦ #(Z.of_nat cap) ∗
    a ↦∗ ((fun k => f (S k)) <$> seq 0 cap).

(**md
The observer set at `l` holds the observers of `o` in order, with `observer_latest o` as `latest`. The
overflow array's capacity covers positions past 0, and the cells past the count are blank.

- [l:] the location of the observer set's fields.
- [o:] the observer set represented.
*)
Definition is_observers (l : loc) (o : observer_set) : iProp Σ :=
  ∃ (cap : nat),
    ⌜length (observer_items o) <= S cap⌝ ∗
    obs_rep l (obs_fun (observer_items o)) cap (length (observer_items o)) (observer_latest o).

Lemma fields4 l v0 v1 v2 v3 :
  l ↦∗ [v0; v1; v2; v3] ⊣⊢ l ↦ v0 ∗ (l +ₗ 1) ↦ v1 ∗ (l +ₗ 2) ↦ v2 ∗ (l +ₗ 3) ↦ v3.
Proof. rewrite !array_cons array_nil !Loc.add_assoc /=. by rewrite right_id. Qed.

Local Lemma fields5 l v0 v1 v2 v3 v4 :
  l ↦∗ [v0; v1; v2; v3; v4] ⊣⊢ l ↦ v0 ∗ (l +ₗ 1) ↦ v1 ∗ (l +ₗ 2) ↦ v2 ∗ (l +ₗ 3) ↦ v3 ∗ (l +ₗ 4) ↦ v4.
Proof. rewrite !array_cons array_nil !Loc.add_assoc /=. by rewrite right_id. Qed.

Local Lemma observers_cell_spec (l a : loc) (f : nat -> val) (cap i : nat) :
  i < S cap ->
  {{{ l ↦ f 0 ∗ (l +ₗ 1) ↦ #a ∗ a ↦∗ ((fun k => f (S k)) <$> seq 0 cap) }}}
    observers_cell_code #l #(Z.of_nat i)
  {{{ RET f i; l ↦ f 0 ∗ (l +ₗ 1) ↦ #a ∗ a ↦∗ ((fun k => f (S k)) <$> seq 0 cap) }}}.
Proof.
  intros Hi. iIntros (Φ) "(H0 & H1 & Ha) HΦ". wp_lam. wp_pures.
  destruct i as [|j].
  - wp_pures. wp_load. iModIntro. iApply "HΦ". by iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures. wp_load. wp_pures.
    rewrite (_ : (Z.of_nat (S j) - 1)%Z = Z.of_nat j); [|lia].
    wp_apply (wp_load_offset with "Ha"); [apply obs_rest_lookup; lia|].
    iIntros "Ha". iApply "HΦ". by iFrame.
Qed.

Local Lemma observers_set_cell_spec (l a : loc) (f : nat -> val) (cap i : nat) (v : val) :
  i < S cap ->
  {{{ l ↦ f 0 ∗ (l +ₗ 1) ↦ #a ∗ a ↦∗ ((fun k => f (S k)) <$> seq 0 cap) }}}
    observers_set_cell_code #l #(Z.of_nat i) v
  {{{ RET #(); l ↦ obs_fupd f i v 0 ∗ (l +ₗ 1) ↦ #a ∗ a ↦∗ ((fun k => obs_fupd f i v (S k)) <$> seq 0 cap) }}}.
Proof.
  intros Hi. iIntros (Φ) "(H0 & H1 & Ha) HΦ". wp_lam. wp_pures.
  destruct i as [|j].
  - wp_pures. wp_store. iModIntro. iApply "HΦ".
    rewrite (_ : obs_fupd f 0 v 0 = v) //. by iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures. wp_load. wp_pures.
    rewrite (_ : (Z.of_nat (S j) - 1)%Z = Z.of_nat j); [|lia].
    wp_apply (wp_store_offset with "Ha").
    { apply lookup_lt_is_Some. rewrite length_fmap length_seq; lia. }
    iIntros "Ha". rewrite obs_rest_insert; [|lia].
    iApply "HΦ". rewrite (_ : obs_fupd f (S j) v 0 = f 0) //. by iFrame.
Qed.

(**md
**`new ObserverSet ()` is the empty set.**

- [l:] the location returned.
*)
Lemma observers_new_spec :
  {{{ True }}} observers_new_code #() {{{ l, RET #l; is_observers l empty_observer_set }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_alloc l as "Hl"; [lia|].
  rewrite (_ : Z.to_nat 5 = 5) // /=. iDestruct (fields5 with "Hl") as "(H0 & H1 & H2 & H3 & H4)".
  wp_pures. wp_store. wp_store. wp_store. wp_store. iModIntro. iApply "HΦ".
  iExists 0. iSplit; [iPureIntro; simpl; lia|].
  iExists l. rewrite /obs_fun /= array_nil. by iFrame.
Qed.

(**md
**`growRest` moves the overflow observers into a larger array.**

Called when `count - 1` equals the overflow capacity. The new capacity is greater than the old capacity,
and its cells past the old capacity are blank.

- [l:] the observer set's fields.
- [a:] the full overflow array.
- [f:] the cells currently held.
- [cap:] the current overflow capacity.
*)
Lemma observers_grow_spec (l a : loc) (f : nat -> val) (cap : nat) :
  {{{ (l +ₗ 1) ↦ #a ∗ (l +ₗ 4) ↦ #(Z.of_nat cap) ∗ a ↦∗ ((fun k => f (S k)) <$> seq 0 cap) }}}
    observers_grow_code #l
  {{{ (b : loc) (cap' : nat), RET #();
      ⌜cap < cap'⌝ ∗ (l +ₗ 1) ↦ #b ∗ (l +ₗ 4) ↦ #(Z.of_nat cap') ∗
      b ↦∗ ((fun k => if decide (k < cap) then f (S k) else blank_cell) <$> seq 0 cap') }}}.
Proof.
  iIntros (Φ) "(H1 & H4 & Ha) HΦ". wp_lam. wp_load. wp_pures.
  set (size := max 4 (cap * 2)).
  wp_bind (If _ _ _). iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat size)⌝)%I).
  { case_bool_decide as H4; wp_pures; iPureIntro; subst size; f_equal; f_equal; lia. }
  iIntros (v ->). wp_pures.
  wp_alloc b as "Hb"; [subst size; lia|].
  rewrite Nat2Z.id. wp_pures. wp_load.
  assert (Hsz : cap < size) by (subst size; lia).
  rewrite (_ : replicate size blank_cell =
               replicate cap blank_cell ++ replicate (size - cap) blank_cell);
    [|rewrite -replicate_add; f_equal; lia].
  iDestruct (array_app with "Hb") as "[Hb1 Hb2]".
  wp_apply (wp_array_copy_to with "[$Hb1 $Ha]");
    [rewrite length_replicate //|rewrite length_fmap length_seq //|].
  iIntros "[Hb1 _]". wp_store. wp_store. iModIntro.
  iApply "HΦ". iFrame. iSplit; [done|].
  rewrite (_ : ((fun k => if decide (k < cap) then f (S k) else blank_cell) <$> seq 0 size) =
               ((fun k => f (S k)) <$> seq 0 cap) ++ replicate (size - cap) blank_cell);
    [iApply array_app; rewrite length_fmap length_seq length_replicate; iFrame|symmetry].
  apply list_eq. intros j. destruct (decide (j < cap)) as [Hj|Hj].
  - rewrite lookup_app_l; [|rewrite length_fmap length_seq //].
    rewrite !list_lookup_fmap !lookup_seq_lt //; [|lia].
    simpl. rewrite decide_True //.
  - rewrite lookup_app_r; [|rewrite length_fmap length_seq; lia].
    rewrite length_fmap length_seq.
    destruct (decide (j < size)) as [Hjs|Hjs].
    + rewrite list_lookup_fmap lookup_seq_lt //= decide_False //.
      rewrite lookup_replicate_2 //; lia.
    + rewrite lookup_ge_None_2; [|rewrite length_replicate; lia].
      rewrite list_lookup_fmap lookup_seq_ge //; lia.
Qed.

(**md
**`Add` appends the observer at the handle it returns.**

The handle is the old count, and the set after it is `observers_add`'s.

- [l:] the observer set.
- [o:] the observers before the add.
- [c:] the observer added.
- [slot:] the slot recorded with it.
*)
Lemma observers_add_spec (l : loc) (o : observer_set) (c : node_id) (slot : nat) :
  {{{ is_observers l o }}}
    observers_add_code #l #(Z.of_nat c) #(Z.of_nat slot)
  {{{ RET #(Z.of_nat (observers_add c slot o).2); is_observers l (observers_add c slot o).1 }}}.
Proof.
  iIntros (Φ) "Ho HΦ". iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)".
  destruct o as [xs latest]; simpl in *.
  wp_lam. wp_pures. wp_load. wp_pures.
  destruct (decide (length xs = 0)) as [Hz|Hnz].
  - destruct xs as [|??]; [|done]. simpl in *.
    wp_pures. wp_store. wp_pures. wp_store. wp_pures. wp_store. iModIntro.
    rewrite /observers_add /=. iApply "HΦ".
    iExists cap. iSplit; [iPureIntro; simpl; lia|].
    iExists a. simpl. rewrite /obs_fun /=. by iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures. wp_load. wp_pures.
    wp_bind (If _ _ _).
    iApply (wp_wand _ _ _ (fun _ => ∃ (b : loc) (cap' : nat),
               ⌜length xs < S cap'⌝ ∗ (l +ₗ 1) ↦ #b ∗ (l +ₗ 4) ↦ #(Z.of_nat cap') ∗
               b ↦∗ ((fun k => obs_fun xs (S k)) <$> seq 0 cap'))%I
             with "[H1 H4 Ha]").
    { case_bool_decide as Heq; wp_pures.
      - assert (cap = length xs - 1) as -> by (injection Heq; lia).
        wp_apply (observers_grow_spec with "[$H1 $H4 $Ha]").
        iIntros (b cap') "(%Hlt & H1 & H4 & Hb)". iExists b, cap'. iFrame.
        iSplit; [iPureIntro; lia|].
        rewrite (_ : ((fun k => obs_fun xs (S k)) <$> seq 0 cap') =
                     ((fun k => if decide (k < length xs - 1) then obs_fun xs (S k) else blank_cell) <$> seq 0 cap'));
          [iFrame|].
        apply list_fmap_ext. intros k x Hkx. apply lookup_seq in Hkx as [-> _].
        case_decide; [done|]. rewrite obs_fun_ge //; lia.
      - iModIntro. iExists a, cap. iFrame. iPureIntro.
        assert (cap ≠ length xs - 1) by (intros E; apply Heq; do 2 f_equal; lia). lia. }
    iIntros (?) "(%b & %cap' & %Hlt' & H1 & H4 & Hb)". wp_pures.
    wp_apply (observers_set_cell_spec l b (obs_fun xs) cap' (length xs) (#(Z.of_nat c), #(Z.of_nat slot))
                with "[$H0 $H1 $Hb]"); [done|].
    iIntros "(H0 & H1 & Hb)". wp_pures. wp_store. wp_pures. wp_store. iModIntro.
    rewrite /observers_add /=. iApply "HΦ". iExists cap'. rewrite length_app /=.
    iSplit; [iPureIntro; lia|].
    iExists b. rewrite Nat2Z.inj_add.
    have Hsnoc : forall j, obs_fupd (obs_fun xs) (length xs) (observer_cell (c, slot)) j = obs_fun (xs ++ [(c, slot)]) j.
    { intros j. symmetry. apply obs_fun_snoc. }
    rewrite (Hsnoc 0) (obs_rest_ext _ _ _ (fun k _ => Hsnoc (S k))). by iFrame.
Qed.

(**md
**`TryAdd`'s scan answers whether an observer from position `i` on is `c`.**

- [a:] the array.
- [cells:] its contents, holding a cell per observer of `xs`.
- [xs:] the observers.
- [c:] the observer looked for.
- [i:] the first position scanned.
*)
Lemma observers_scan_spec (a : loc) (cells : list val) (xs : list (node_id * nat)) (c i : nat) :
  (forall j p, xs !! j = Some p -> cells !! j = Some (observer_cell p)) ->
  {{{ a ↦∗ cells }}}
    observers_scan_code #a #(Z.of_nat i) #(Z.of_nat (length xs)) #(Z.of_nat c)
  {{{ RET #(bool_decide (c ∈ fst <$> drop i xs)); a ↦∗ cells }}}.
Proof.
  intros Hcells. iIntros (Φ) "Ha HΦ". iLöb as "IH" forall (i).
  wp_rec. wp_pures. destruct (decide (i < length xs)) as [Hi'|Hi].
  - rewrite (bool_decide_eq_true_2 (Z.of_nat i < Z.of_nat (length xs))%Z); [|lia]. wp_pures.
    destruct (lookup_lt_is_Some_2 xs i Hi') as [[c' s'] Hp].
    wp_apply (wp_load_offset with "Ha"); [by apply Hcells|]. iIntros "Ha". wp_pures.
    rewrite (drop_S _ _ _ Hp) /=.
    destruct (decide (c' = c)) as [->|He].
    + rewrite (bool_decide_eq_true_2 (#(Z.of_nat c) = #(Z.of_nat c))) //. wp_pures.
      iModIntro. rewrite bool_decide_eq_true_2; [by iApply "HΦ"|]. apply elem_of_cons. by left.
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat c') = #(Z.of_nat c))); [|intros [= ?]; lia]. wp_pures.
      rewrite (_ : (Z.of_nat i + 1)%Z = Z.of_nat (S i)); [|lia].
      iApply ("IH" with "Ha"). iNext.
      rewrite (bool_decide_ext (c ∈ c' :: _) (c ∈ fst <$> drop (S i) xs)); [done|].
      rewrite elem_of_cons. split; [|by right]. intros [->|]; [done|done].
  - rewrite (bool_decide_eq_false_2 (Z.of_nat i < Z.of_nat (length xs))%Z); [|lia]. wp_pures.
    iModIntro. rewrite drop_ge; [|lia]. by iApply "HΦ".
Qed.

(**md
**`TryAdd` answers as `observers_try_add` does.**

The answer is the new handle, `EdgeHandle.Unlinked` or `EdgeHandle.Unknown`, and the set after it is
`observers_try_add`'s.

- [l:] the observer set.
- [o:] the observers before the call.
- [c:] the observer offered.
- [slot:] the slot recorded if it is added.
*)
Lemma observers_try_add_spec (l : loc) (o : observer_set) (c : node_id) (slot : nat) :
  {{{ is_observers l o }}}
    observers_try_add_code #l #(Z.of_nat c) #(Z.of_nat slot)
  {{{ RET tried_val (observers_try_add c slot o).2; is_observers l (observers_try_add c slot o).1 }}}.
Proof.
  iIntros (Φ) "Ho HΦ". wp_lam. wp_pures.
  iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)".
  destruct o as [xs latest]; unfold observers_try_add, observer_view; simpl in *.
  wp_load. wp_pures.
  destruct (decide (length xs = 0)) as [Hz|Hnz].
  { rewrite bool_decide_eq_true_2; [|by rewrite Hz]. wp_pures.
    wp_apply (observers_add_spec _ {| observer_items := xs; observer_latest := latest |} with "[H0 H1 H2 H3 H4 Ha]").
    { iExists cap. iSplit; [done|]. iExists a. by iFrame. }
    iIntros "Ho". by iApply "HΦ". }
  rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures. wp_load. wp_pures.
  wp_bind (If _ _ #false).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #(bool_decide (fst <$> xs !! latest = Some c))⌝ ∗
             l ↦ obs_fun xs 0 ∗ (l +ₗ 1) ↦ #a ∗
             a ↦∗ ((fun k => obs_fun xs (S k)) <$> seq 0 cap))%I with "[H0 H1 Ha]").
  { destruct (decide (latest < length xs)) as [Hlt'|Hlt'].
    - rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
      destruct (lookup_lt_is_Some_2 xs latest) as [[c' s'] Hp]; [lia|].
      wp_apply (observers_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
      iIntros "(H0 & H1 & Ha)". rewrite (obs_fun_lookup _ _ _ Hp). wp_pures. iFrame. iPureIntro. rewrite Hp /=.
      destruct (decide (c' = c)) as [->|Hne].
      + rewrite !bool_decide_eq_true_2 //.
      + rewrite !bool_decide_eq_false_2 //; intros [= ?]; lia.
    - rewrite bool_decide_eq_false_2; [|lia]. wp_pures.
      iFrame. iPureIntro. rewrite lookup_ge_None_2 /=; [|lia]. done. }
  iIntros (v) "(-> & H0 & H1 & Ha)".
  destruct (decide (fst <$> xs !! latest = Some c)) as [Hl|Hl].
  { rewrite bool_decide_eq_true_2 //. wp_pures. iModIntro. iApply "HΦ".
    iExists cap. iSplit; [done|]. iExists a. by iFrame. }
  rewrite bool_decide_eq_false_2 //. wp_pures.
  destruct (lookup_lt_is_Some_2 xs 0) as [[c0 s0] Hp0]; [lia|].
  destruct (decide (length xs = 1)) as [H1'|H1'].
  - rewrite bool_decide_eq_true_2; [|rewrite H1' //]. wp_pures.
    wp_load. rewrite (obs_fun_lookup _ 0 _ Hp0). wp_pures.
    destruct xs as [|[c0' s0'][|??]]; simpl in *; try done.
    injection Hp0 as -> ->.
    have Hview : (c ∈ [c0]) <-> (#(Z.of_nat c0) = #(Z.of_nat c)).
    { rewrite elem_of_cons elem_of_nil. split; [intros [->|[]]; done | intros [= ?]; left; lia]. }
    destruct (decide (c ∈ [c0])) as [Hin|Hin].
    + rewrite bool_decide_eq_true_2; [|by apply Hview]. wp_pures.
      iModIntro. iApply "HΦ". iExists cap. iSplit; [done|].
      iExists a. simpl. rewrite /obs_fun /=. by iFrame.
    + rewrite bool_decide_eq_false_2; [|by rewrite -Hview]. wp_pures.
      wp_apply (observers_add_spec _ {| observer_items := [(c0, s0)]; observer_latest := latest |} with "[H0 H1 H2 H3 H4 Ha]").
      { iExists cap. iSplit; [done|]. iExists a. simpl. rewrite /obs_fun /=. by iFrame. }
      iIntros "Ho". simpl. by iApply "HΦ".
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    destruct (decide (length xs <= scan_limit)) as [Hs|Hs].
    + rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
      wp_load. rewrite (obs_fun_lookup _ 0 _ Hp0). wp_pures.
      destruct xs as [|[c0' s0'] tl]; simpl in *; try done.
      injection Hp0 as -> ->.
      destruct (decide (c0 = c)) as [->|Hne].
      * rewrite bool_decide_eq_true_2 //. wp_pures.
        rewrite decide_True; [|apply elem_of_cons; by left].
        iModIntro. iApply "HΦ". iExists cap. iSplit; [done|].
        iExists a. simpl. rewrite /obs_fun /=. by iFrame.
      * rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures. wp_load. wp_pures.
        rewrite (_ : (Z.of_nat (S (length tl)) - 1)%Z = Z.of_nat (length tl)); [|lia].
        wp_apply (observers_scan_spec a _ tl c 0 with "Ha").
        { intros j p Hj. rewrite obs_rest_lookup; [|apply lookup_lt_Some in Hj; lia].
          by rewrite /obs_fun /= Hj. }
        iIntros "Ha". rewrite drop_0.
        have Hview : (c ∈ c0 :: (fst <$> tl)) <-> (c ∈ fst <$> tl).
        { rewrite elem_of_cons. split; [intros [->|?]; done | by right]. }
        destruct (decide (c ∈ fst <$> tl)) as [Hin|Hin].
        { rewrite bool_decide_eq_true_2 //. wp_pures.
          rewrite decide_True; [|by apply Hview]. iModIntro. iApply "HΦ".
          iExists cap. iSplit; [done|]. iExists a. simpl. rewrite /obs_fun /=. by iFrame. }
        { rewrite bool_decide_eq_false_2 //. wp_pures.
          rewrite decide_False; [|by intros ?%Hview].
          wp_apply (observers_add_spec _ {| observer_items := (c0, s0) :: tl; observer_latest := latest |} with "[H0 H1 H2 H3 H4 Ha]").
          { iExists cap. iSplit; [done|]. iExists a. simpl. rewrite /obs_fun /=. by iFrame. }
          iIntros "Ho". simpl. by iApply "HΦ". }
    + rewrite bool_decide_eq_false_2; [|lia]. wp_pures. iModIntro. iApply "HΦ".
      iExists cap. iSplit; [done|]. iExists a. by iFrame.
Qed.

(**md
**`RemoveAt` swap-removes as `observers_remove_at` does.**

When the handle holds `c`, the code returns the `MovedIn` notice of the pure result and leaves the set it
names. Cells past the new count are blank.

- [l:] the observer set.
- [o:] the observers before the removal.
- [c:] the observer removed.
- [h:] its handle.
- [o':] the set after the removal.
- [m:] the notice: the moved observer, its slot and its new handle.
*)
Lemma observers_remove_at_spec (l : loc) (o : observer_set) (c : node_id) (h : nat) (o' : observer_set)
    (m : option (node_id * nat * nat)) :
  observers_remove_at c h o = Some (o', m) ->
  {{{ is_observers l o }}}
    observers_remove_at_code #l #(Z.of_nat c) #(Z.of_nat h)
  {{{ RET moved_val m; is_observers l o' }}}.
Proof.
  intros Hr. iIntros (Φ) "Ho HΦ". wp_lam. wp_pures.
  iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)".
  destruct o as [xs latest]. unfold observers_remove_at in Hr. simpl in *.
  destruct (xs !! h) as [[c' s]|] eqn:Eh; [|done].
  destruct (decide (c' = c)) as [->|]; [|done].
  pose proof (lookup_lt_Some _ _ _ Eh) as Hh.
  wp_load. wp_pures.
  wp_apply wp_assert. wp_pures. rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
  wp_apply (observers_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
  iIntros "(H0 & H1 & Ha)". rewrite (obs_fun_lookup _ _ _ Eh).
  wp_pures. rewrite bool_decide_eq_true_2 //. iModIntro. iSplit; [done|]. iNext. wp_pures.
  remember (pred (length xs)) as last eqn:Elast. assert (Hlen : length xs = S last) by lia.
  rewrite (_ : (Z.of_nat (length xs) - 1)%Z = Z.of_nat last); [|lia].
  destruct (decide (h = last)) as [->|Hne].
  - simplify_eq. set (k := pred (length xs)) in *. rewrite bool_decide_eq_true_2 //. wp_pures.
    wp_apply (observers_set_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". wp_pures. wp_store. wp_pures. iModIntro. iApply "HΦ".
    iExists cap. rewrite /= length_take. iSplit; [iPureIntro; lia|].
    iExists a. rewrite (_ : k `min` length xs = k); [|lia].
    have Hpop : forall j, obs_fupd (obs_fun xs) k blank_cell j = obs_fun (take k xs) j.
    { intros j. symmetry. by apply obs_fun_pop. }
    rewrite (Hpop 0) (obs_rest_ext _ _ _ (fun j _ => Hpop (S j))). by iFrame.
  - destruct (xs !! last) as [[mc ms]|] eqn:El; [|done]. simplify_eq.
    set (last := pred (length xs)) in *.
    rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    wp_apply (observers_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". rewrite (obs_fun_lookup _ _ _ El). wp_pures.
    wp_apply (observers_set_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". wp_pures.
    wp_apply (observers_set_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". wp_pures. wp_store. wp_load. wp_pures.
    have Hrep : forall j,
      obs_fupd (obs_fupd (obs_fun xs) h (observer_cell (mc, ms))) last blank_cell j =
      obs_fun (<[h := (mc, ms)]> (take last xs)) j.
    { intros j. rewrite obs_fun_insert; [|rewrite length_take; lia].
      rewrite /obs_fupd. destruct (decide (j = last)) as [->|Hj1].
      - rewrite decide_False; [|lia]. rewrite obs_fun_pop // /obs_fupd decide_True //.
      - destruct (decide (j = h)) as [->|Hj2]; [done|].
        rewrite obs_fun_pop // /obs_fupd decide_False //. }
    rewrite (Hrep 0) (obs_rest_ext _ _ _ (fun j _ => Hrep (S j))).
    case_bool_decide as Hle; wp_pures.
    + wp_store. wp_pures. iModIntro. iApply "HΦ". iExists cap. rewrite /= length_insert length_take.
      rewrite decide_True; [|injection Hle; lia].
      rewrite (_ : last `min` length xs = last); [|lia].
      iSplit; [iPureIntro; lia|]. iExists a. by iFrame.
    + iModIntro. iApply "HΦ". iExists cap. rewrite /= length_insert length_take.
      rewrite decide_False; [|intros E; apply Hle; do 2 f_equal; lia].
      rewrite (_ : last `min` length xs = last); [|lia].
      iSplit; [iPureIntro; lia|]. iExists a. by iFrame.
Qed.

(** ** The walk *)

(**md
The visitor of a walk over the set at `l`. Each call receives the set, the resource `I`, and the position and
slot at which the set holds the observer visited. It may mutate the
set by `observers_add` and `observers_remove_at`, keeps `P` at every point it passes through, and returns
`V` of the observer visited.

- [l:] the observer set walked.
- [visit:] the closure called on each observer.
- [I:] the resource the visitor needs besides the set.
- [P:] the property of the set kept at every mutation point.
- [V:] what a visit returns of its observer.
*)
Definition visitor_spec (l : loc) (visit : val) (I : observer_set -> iProp Σ) (P : observer_set -> Prop)
    (V : node_id -> iProp Σ) : iProp Σ :=
  ∀ (o : observer_set) (d : node_id) (i slot : nat),
    {{{ is_observers l o ∗ I o ∗ ⌜P o⌝ ∗ ⌜observer_items o !! i = Some (d, slot)⌝ }}} visit #(Z.of_nat d)
    {{{ ms o1, RET #(); ⌜apply_mutations ms o = Some o1⌝ ∗ ⌜Forall P (mutation_points ms o)⌝ ∗
        is_observers l o1 ∗ I o1 ∗ V d }}}.

(**md
**The walk's loop from cursor `n` is the pure walk from `n`.**

- [l:] the observer set walked.
- [visit:] the visitor.
- [I:] the visitor's resource.
- [P:] the property kept at every point.
- [V:] what a visit returns.
- [o:] the set at the loop's start.
- [n:] the cursor, the F# index plus one.
*)
Lemma observers_walk_loop_spec (l : loc) (visit : val) I P V (o : observer_set) (n : nat) :
  {{{ visitor_spec l visit I P V ∗ is_observers l o ∗ I o ∗ ⌜P o⌝ }}}
    observers_walk_loop #l visit #(Z.of_nat n)
  {{{ muts vis o', RET #(); ⌜walk n o muts vis o'⌝ ∗ ⌜Forall P (mutation_points (concat muts) o)⌝ ∗
      is_observers l o' ∗ I o' ∗ [∗ list] d ∈ vis, V d }}}.
Proof.
  iIntros (Φ) "(#Hvis & Ho & HI & %HP) HΦ". iLöb as "IH" forall (n o HP).
  wp_rec. wp_pures. iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)".
  wp_load. wp_pures.
  set (k := length (observer_items o)).
  wp_bind (If _ _ _). iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (min n k))⌝)%I).
  { case_bool_decide; wp_pures; iPureIntro; f_equal; f_equal; lia. }
  iIntros (v ->). wp_pures.
  destruct (min n k) as [|i] eqn:Hmin.
  - wp_pures. iModIntro. iApply ("HΦ" $! [] [] o). iSplit; [iPureIntro; by constructor|].
    iSplit; [iPureIntro; by constructor|]. iSplitR "HI"; [iExists cap; iSplit; [done|]; iExists a; by iFrame|].
    by iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    rewrite (_ : (Z.of_nat (S i) - 1)%Z = Z.of_nat i); [|lia].
    destruct (lookup_lt_is_Some_2 (observer_items o) i) as [[d slot] Hd]; [subst k; lia|].
    wp_apply (observers_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". rewrite (obs_fun_lookup _ _ _ Hd). wp_pures.
    wp_apply ("Hvis" with "[H0 H1 H2 H3 H4 Ha HI]").
    { iFrame "HI". iSplitL; [iExists cap; iSplit; [done|]; iExists a; by iFrame|]. iSplit; [done|].
      iPureIntro. exact Hd. }
    iIntros (ms o1) "(%Hms & %HPms & Ho1 & HI1 & HV)". wp_pures.
    assert (HP1 : P o1).
    { clear -Hms HPms. revert o Hms HPms. induction ms as [|m r IH]; intros o Hms HPms; simpl in *.
      - simplify_eq. by inversion HPms.
      - destruct (apply_mutation m o) as [o2|]; simpl in *; [|done].
        inversion HPms; subst. by eapply IH. }
    iApply ("IH" $! i o1 HP1 with "Ho1 HI1"). iNext.
    iIntros (muts vis o') "(%Hw & %HPw & Ho' & HI' & HVs)".
    iApply ("HΦ" $! (ms :: muts) (d :: vis) o'). iFrame. iPureIntro. split.
    + econstructor; [done|done|done|done].
    + simpl. by eapply mutation_points_join.
Qed.

(**md
**A walk is the pure walk.**

Each visit leaves `P` at every point it passes through. The walk relates the starting count and set to the
mutations of each visit, the observers visited and the final set, and every visited observer's `V` is returned.

- [l:] the observer set walked.
- [visit:] the visitor.
- [I:] the visitor's resource.
- [P:] the property kept at every point.
- [V:] what a visit returns.
- [o:] the set at the start of the walk.
*)
Lemma walk_trace_spec (l : loc) (visit : val) I P V (o : observer_set) :
  {{{ visitor_spec l visit I P V ∗ is_observers l o ∗ I o ∗ ⌜P o⌝ }}}
    observers_walk_code #l visit
  {{{ muts vis o', RET #(); ⌜walk (length (observer_items o)) o muts vis o'⌝ ∗
      ⌜Forall P (mutation_points (concat muts) o)⌝ ∗ is_observers l o' ∗ I o' ∗ [∗ list] d ∈ vis, V d }}}.
Proof.
  iIntros (Φ) "(#Hvis & Ho & HI & %HP) HΦ". wp_lam. wp_pures.
  iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)". wp_load. wp_pures.
  destruct (decide (length (observer_items o) = 1)) as [H1|Hn1].
  - rewrite bool_decide_eq_true_2; [|rewrite H1 //]. wp_pures.
    destruct (lookup_lt_is_Some_2 (observer_items o) 0) as [[d slot] Hd]; [lia|].
    wp_load. rewrite (obs_fun_lookup _ 0 _ Hd). wp_pures. rewrite -(obs_fun_lookup _ 0 _ Hd).
    wp_apply ("Hvis" with "[H0 H1 H2 H3 H4 Ha HI]").
    { iFrame "HI". iSplitL; [iExists cap; iSplit; [done|]; iExists a; by iFrame|].
      iSplit; [done|]. iPureIntro. exact Hd. }
    iIntros (ms o1) "(%Hms & %HPms & Ho1 & HI1 & HV)". wp_pures. iModIntro.
    iApply ("HΦ" $! [ms] [d] o1). iFrame.
    iSplit; [|iSplitL; [iPureIntro; simpl; rewrite app_nil_r //|done]].
    iPureIntro. econstructor; [rewrite H1 /=; reflexivity|done|done|].
    econstructor. simpl. lia.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    destruct (decide (1 < length (observer_items o))) as [Hgt|Hle].
    + rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
      wp_apply (observers_walk_loop_spec with "[H0 H1 H2 H3 H4 Ha HI]").
      { iFrame "#". iFrame "HI". iSplitL; [iExists cap; iSplit; [done|]; iExists a; by iFrame|done]. }
      done.
    + rewrite bool_decide_eq_false_2; [|lia]. wp_pures.
      assert (length (observer_items o) = 0) as Hz by lia.
      iModIntro. iApply ("HΦ" $! [] [] o).
      iSplit; [iPureIntro; rewrite Hz; by constructor|].
      iSplit; [iPureIntro; by constructor|].
      iSplitR "HI"; [iExists cap; iSplit; [done|]; iExists a; by iFrame|by iFrame].
Qed.

(**md
**A walk visits every observer held exactly once throughout.**

The heap form of [`walk_covers`](Ranvier.Pure.ObserverSet.html#walk_covers). When each visit keeps `c` in
exactly one position at every point it passes through, and `c` starts in exactly one position, the walk calls
the visitor on `c`, so `V c` is among the results.

- [l:] the observer set walked.
- [visit:] the visitor, whose mutations keep `held_once c`.
- [I:] the visitor's resource.
- [V:] what a visit returns.
- [o:] the set at the start of the walk.
- [c:] the observer held once throughout.
*)
Theorem walk_spec (l : loc) (visit : val) I V (o : observer_set) (c : node_id) :
  {{{ visitor_spec l visit I (held_once c) V ∗ is_observers l o ∗ I o ∗ ⌜held_once c o⌝ }}}
    observers_walk_code #l visit
  {{{ vis o', RET #(); ⌜c ∈ vis⌝ ∗ is_observers l o' ∗ I o' ∗ [∗ list] d ∈ vis, V d }}}.
Proof.
  iIntros (Φ) "H HΦ". wp_apply (walk_trace_spec with "H").
  iIntros (muts vis o') "(%Hw & %HP & Ho' & HI' & HV)". iApply "HΦ". iFrame. iPureIntro.
  by eapply walk_covers.
Qed.

(**md
The visitor of a cursor-indexed walk over the set at `l`. Each call receives the set at cursor `n`, where
`min n (length (observer_items o)) = S i`, the resource `I n o`, and the observer `d` at position `i`, and
returns the updated set `o1` with `I i o1`.

- [l:] the observer set walked.
- [visit:] the closure called on each observer.
- [I:] the cursor-indexed resource carried through the walk.
*)
Definition cursor_visitor_spec (l : loc) (visit : val) (I : nat -> observer_set -> iProp Σ) : iProp Σ :=
  ∀ (n i : nat) (o : observer_set) (d : node_id) (slot : nat),
    {{{ ⌜min n (length (observer_items o)) = S i⌝ ∗ ⌜observer_items o !! i = Some (d, slot)⌝ ∗
        is_observers l o ∗ I n o }}}
      visit #(Z.of_nat d)
    {{{ o1, RET #(); is_observers l o1 ∗ I i o1 }}}.

(**md
**The walk's loop from cursor `n` steps `I` through each visited index down to an empty prefix.**

- [l:] the observer set walked.
- [visit:] the visitor.
- [I:] the cursor-indexed resource.
- [o:] the set at the loop's start.
- [n:] the cursor, the F# index plus one.
*)
Lemma observers_walk_loop_cursor_spec (l : loc) (visit : val) (I : nat -> observer_set -> iProp Σ)
    (o : observer_set) (n : nat) :
  {{{ cursor_visitor_spec l visit I ∗ is_observers l o ∗ I n o }}}
    observers_walk_loop #l visit #(Z.of_nat n)
  {{{ k o', RET #(); ⌜min k (length (observer_items o')) = 0⌝ ∗ is_observers l o' ∗ I k o' }}}.
Proof.
  iIntros (Φ) "(#Hvis & Ho & HI) HΦ". iLöb as "IH" forall (n o).
  wp_rec. wp_pures. iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)".
  wp_load. wp_pures.
  set (k := length (observer_items o)).
  wp_bind (If _ _ _). iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (min n k))⌝)%I).
  { case_bool_decide; wp_pures; iPureIntro; f_equal; f_equal; lia. }
  iIntros (v ->). wp_pures.
  destruct (min n k) as [|i] eqn:Hmin.
  - wp_pures. iModIntro. iApply ("HΦ" $! n o). iSplit; [done|]. iFrame "HI".
    iExists cap. iSplit; [done|]. iExists a. by iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    rewrite (_ : (Z.of_nat (S i) - 1)%Z = Z.of_nat i); [|lia].
    destruct (lookup_lt_is_Some_2 (observer_items o) i) as [[d slot] Hd]; [subst k; lia|].
    wp_apply (observers_cell_spec with "[$H0 $H1 $Ha]"); [lia|].
    iIntros "(H0 & H1 & Ha)". rewrite (obs_fun_lookup _ _ _ Hd). wp_pures.
    wp_apply ("Hvis" $! n i o d slot with "[H0 H1 H2 H3 H4 Ha HI]").
    { iFrame "HI". iSplit; [done|]. iSplit; [done|]. iExists cap. iSplit; [done|]. iExists a. by iFrame. }
    iIntros (o1) "[Ho1 HI1]". wp_pures.
    by iApply ("IH" $! i o1 with "Ho1 HI1").
Qed.

(**md
**A cursor-indexed walk starts at the observer count and ends at an empty prefix.**

- [l:] the observer set walked.
- [visit:] the visitor.
- [I:] the cursor-indexed resource.
- [o:] the set at the start of the walk.
*)
Lemma walk_cursor_spec (l : loc) (visit : val) (I : nat -> observer_set -> iProp Σ) (o : observer_set) :
  {{{ cursor_visitor_spec l visit I ∗ is_observers l o ∗ I (length (observer_items o)) o }}}
    observers_walk_code #l visit
  {{{ k o', RET #(); ⌜min k (length (observer_items o')) = 0⌝ ∗ is_observers l o' ∗ I k o' }}}.
Proof.
  iIntros (Φ) "(#Hvis & Ho & HI) HΦ". wp_lam. wp_pures.
  iDestruct "Ho" as (cap Hcap) "Hrep".
  iDestruct "Hrep" as (a) "(H0 & H1 & H2 & H3 & H4 & Ha)". wp_load. wp_pures.
  destruct (decide (length (observer_items o) = 1)) as [H1|Hn1].
  - rewrite bool_decide_eq_true_2; [|rewrite H1 //]. wp_pures.
    destruct (lookup_lt_is_Some_2 (observer_items o) 0) as [[d slot] Hd]; [lia|].
    wp_load. rewrite (obs_fun_lookup _ 0 _ Hd). wp_pures. rewrite -(obs_fun_lookup _ 0 _ Hd).
    wp_apply ("Hvis" $! (length (observer_items o)) 0 o d slot with "[H0 H1 H2 H3 H4 Ha HI]").
    { iFrame "HI". iSplit; [iPureIntro; rewrite H1 /=; reflexivity|]. iSplit; [done|].
      iExists cap. iSplit; [done|]. iExists a. by iFrame. }
    iIntros (o1) "[Ho1 HI1]". wp_pures. iModIntro.
    iApply ("HΦ" $! 0 o1). iSplit; [done|]. iFrame.
  - rewrite bool_decide_eq_false_2; [|intros [= ?]; lia]. wp_pures.
    destruct (decide (1 < length (observer_items o))) as [Hgt|Hle].
    + rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
      wp_apply (observers_walk_loop_cursor_spec with "[H0 H1 H2 H3 H4 Ha HI]").
      { iFrame "#". iFrame "HI". iExists cap. iSplit; [done|]. iExists a. by iFrame. }
      done.
    + rewrite bool_decide_eq_false_2; [|lia]. wp_pures. iModIntro.
      iApply ("HΦ" $! (length (observer_items o)) o).
      iSplit; [iPureIntro; lia|].
      iFrame "HI". iExists cap. iSplit; [done|]. iExists a. by iFrame.
Qed.

End heap.
