From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Deltas.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost.

Open Scope nat_scope.

(**md # Heap collection delta coalescing and ProjectionReader contracts

This module lifts the `KeyChanges.Record` coalescing algebra and `ProjectionReader` cap/cursor contracts
of `Pure/Deltas.v` to HeapLang separation logic:

1. `key_change_merge_spec`: HeapLang execution of `key_change_merge_code` over a key's pending change slot
   (`NONEV` or `SOMEV #prev`), refining `kc_merge_opt`.
2. `projection_reader_record_spec`: HeapLang execution of `projection_reader_record_code`, enforcing the
   `max 64 live_count` overflow reset cap (`pr_record`).
3. `projection_reader_read_spec`: HeapLang execution of `projection_reader_read_code`, clearing `reset` and
   `changes` and advancing `cursor` to `current_keys` (`pr_read`). *)

(** ** HeapLang code for KeyChanges.Record and ProjectionReader *)

Definition key_change_code_nat (c : key_change) : nat :=
  match c with
  | KcAdded => 0
  | KcRemoved => 1
  | KcReplaced => 2
  | KcChanged => 3
  end.

Definition key_change_val (c : key_change) : val :=
  #(Z.of_nat (key_change_code_nat c)).

Definition opt_kc_val (oc : option key_change) : val :=
  match oc with
  | None => NONEV
  | Some c => SOMEV (key_change_val c)
  end.

(**md Single-key pending slot transition (`Deltas.fs` lines 37–69):

- `0` = `Added`, `1` = `Removed`, `2` = `Replaced`, `3` = `Changed`.
- `Added` (`#0`) followed by `Removed` (`#1`) clears the slot to `NONE`.
- `Removed` (`#1`) followed by `Added` (`#0`) coalesces to `SOME #2` (`Replaced`).
- `Changed` (`#3`) preserves any existing membership change (`#0`, `#1`, `#2`).
*)
Definition key_change_merge_code : val :=
  λ: "slot" "c",
    match: !"slot" with
      NONE => "slot" <- SOME "c"
    | SOME "prev" =>
        if: ("c" = #1) && ("prev" = #0) then
          "slot" <- NONE
        else if: ("c" = #0) && ("prev" = #1) then
          "slot" <- SOME #2
        else if: "c" = #3 then
          if: "prev" = #3 then "slot" <- SOME #3
          else #()
        else
          "slot" <- SOME "c"
    end.

(**md Layout of a 3-cell `ProjectionReader` control block `pr`:

- `pr +ₗ 0`: `changes_count` (integer size of pending `KeyChanges`)
- `pr +ₗ 1`: `cursor` (integer version / key-list snapshot id)
- `pr +ₗ 2`: `reset` (boolean)
*)
Definition projection_reader_record_code : val :=
  λ: "pr" "new_size" "cap",
    if: !("pr" +ₗ #2) then #()
    else if: "cap" < "new_size" then
      "pr" <- #0;;
      ("pr" +ₗ #2) <- #true
    else
      "pr" <- "new_size".

Definition projection_reader_read_code : val :=
  λ: "pr" "current",
    let: "was_reset" := !("pr" +ₗ #2) in
    let: "prev" := !("pr" +ₗ #1) in
    let: "cnt" := if: "was_reset" then #0 else !"pr" in
    "pr" <- #0;;
    ("pr" +ₗ #1) <- "current";;
    ("pr" +ₗ #2) <- #false;;
    ("was_reset", ("cnt", "prev")).

Section heap_deltas.
Context `{!heapGS Σ, !graphG Σ}.

(**md
**`key_change_merge_code` updates `slot ↦ opt_kc_val prev` to `slot ↦ opt_kc_val (kc_merge_opt prev c)` for every combination of `prev` and `c`.**

- [slot:] the heap location holding the key's pending `KeyChange`.
- [prev:] the previous pending change (`None` or `Some p`).
- [c:] the newly recorded `KeyChange`.
*)
Theorem key_change_merge_spec (slot : loc) (prev : option key_change) (c : key_change) :
  {{{ slot ↦ opt_kc_val prev }}}
    key_change_merge_code #slot (key_change_val c)
  {{{ RET #(); slot ↦ opt_kc_val (kc_merge_opt prev c) }}}.
Proof.
  iIntros (Φ) "Hslot HΦ".
  rewrite /key_change_merge_code.
  wp_lam. wp_pures. wp_load.
  destruct prev as [p|].
  - destruct p, c; simpl; wp_pures; try wp_store; iModIntro; by iApply "HΦ".
  - simpl. wp_pures. wp_store. iModIntro. by iApply "HΦ".
Qed.

(**md
**`projection_reader_record_code` preserves the reset state when `reset = true`, flags a reset and clears `changes_count` to `0` when `cap < new_size`, and otherwise records `new_size`.**

- [pr:] the `ProjectionReader` control block location.
- [cnt:] the previous pending change count.
- [cursor:] the reader's cursor snapshot id.
- [reset:] whether the reader is currently flagged for a reset.
- [new_size:] the size of `changes` after merging the incoming key change.
- [cap:] the reader's threshold `Nat.max 64 live_count`.
*)
Theorem projection_reader_record_spec
    (pr : loc) (cnt cursor new_size cap : nat) (reset : bool) :
  {{{ pr ↦ #(Z.of_nat cnt) ∗ (pr +ₗ 1) ↦ #(Z.of_nat cursor) ∗ (pr +ₗ 2) ↦ #reset }}}
    projection_reader_record_code #pr #(Z.of_nat new_size) #(Z.of_nat cap)
  {{{ RET #();
      let reset' := if reset then true else bool_decide (cap < new_size) in
      let cnt' := if reset then cnt else if bool_decide (cap < new_size) then 0 else new_size in
      pr ↦ #(Z.of_nat cnt') ∗ (pr +ₗ 1) ↦ #(Z.of_nat cursor) ∗ (pr +ₗ 2) ↦ #reset' }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /projection_reader_record_code.
  wp_lam. wp_pures. wp_load.
  destruct reset; wp_pures.
  - iModIntro. iApply "HΦ". iFrame.
  - destruct (decide (cap < new_size)) as [Hgt|Hle].
    + rewrite (bool_decide_eq_true_2 (Z.of_nat cap < Z.of_nat new_size)%Z); [|lia].
      wp_pures. wp_store. wp_store. iModIntro. iApply "HΦ".
      rewrite (bool_decide_eq_true_2 (cap < new_size) Hgt) /=. iFrame.
    + rewrite (bool_decide_eq_false_2 (Z.of_nat cap < Z.of_nat new_size)%Z); [|lia].
      wp_pures. wp_store. iModIntro. iApply "HΦ".
      rewrite (bool_decide_eq_false_2 (cap < new_size) Hle) /=. iFrame.
Qed.

(**md
**`projection_reader_read_code` returns `(reset, (reported_count, cursor))`, clears `changes_count` to `0` and `reset` to `false`, and advances `cursor` to `current`.**

- [pr:] the `ProjectionReader` control block location.
- [cnt:] the pending change count before `Read()`.
- [cursor:] the cursor snapshot id from the previous `Read()`.
- [current:] the current key-set snapshot id returned by `host.ReadKeys()`.
- [reset:] whether the reader was flagged for a reset before `Read()`.
*)
Theorem projection_reader_read_spec
    (pr : loc) (cnt cursor current : nat) (reset : bool) :
  {{{ pr ↦ #(Z.of_nat cnt) ∗ (pr +ₗ 1) ↦ #(Z.of_nat cursor) ∗ (pr +ₗ 2) ↦ #reset }}}
    projection_reader_read_code #pr #(Z.of_nat current)
  {{{ RET (#reset, (#(Z.of_nat (if reset then 0%nat else cnt)), #(Z.of_nat cursor)));
      pr ↦ #0 ∗ (pr +ₗ 1) ↦ #(Z.of_nat current) ∗ (pr +ₗ 2) ↦ #false }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /projection_reader_read_code.
  wp_lam. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
  destruct reset; wp_pures.
  - wp_store. wp_store. wp_store. wp_pures. iModIntro.
    iApply "HΦ". iFrame.
  - wp_load. wp_pures. wp_store. wp_store. wp_store. wp_pures. iModIntro.
    iApply "HΦ". iFrame.
Qed.

End heap_deltas.
