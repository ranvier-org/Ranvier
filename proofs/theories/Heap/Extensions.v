From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Engine Extensions.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run.

Open Scope nat_scope.

(** * Heap `Ranvier.Query` and `Ranvier.Elmish` boundary contracts

This module lifts the `Ranvier.Query` and `Ranvier.Elmish` boundary contracts of `Pure/Extensions.v` to
HeapLang separation logic:
1. `query_start_refresh_spec` and `query_publish_fenced_spec`: HeapLang execution of `QueryEntry.start`
   and `QueryEntry.publish` over a 7-cell `QueryEntry` control block (`is_query_entry`).
2. `query_lease_dispose_spec`: HeapLang execution of `QueryLease.Dispose` (`is_query_lease`) with
   idempotent reference-count decrement and last-lease eviction.
3. `query_commit_staging_spec`: HeapLang execution of `QueryClient.commit`'s two-phase `staging` fence.
4. `mvu_apply_spec` and `mvu_run_commands_stopped_spec`: HeapLang execution of `Mvu.Apply` and `Mvu.Run`
   over an `Mvu` control block (`is_mvu`). *)

(** ** HeapLang code for `QueryEntry`, `QueryLease`, `QueryClient.commit`, and `Mvu` *)

Definition fetch_status_val (fs : fetch_status) : val :=
  match fs with
  | FsIdle => #0
  | FsFetching => #1
  end.

Definition opt_z_val (oz : option Z) : val :=
  match oz with
  | None => NONEV
  | Some z => SOMEV #z
  end.

(** Layout of a 7-cell `QueryEntry` control block `qe`:
    - `qe +ₗ 0`: `data` (`NONEV` or `SOMEV #v`)
    - `qe +ₗ 1`: `status` (`#0` = `FsIdle`, `#1` = `FsFetching`)
    - `qe +ₗ 2`: `error` (`NONEV` or `SOMEV #(Z.of_nat err)`)
    - `qe +ₗ 3`: `stale` (boolean)
    - `qe +ₗ 4`: `generation` (integer)
    - `qe +ₗ 5`: `disposed` (boolean)
    - `qe +ₗ 6`: `gate_faulted` (boolean) *)

Definition query_start_code : val :=
  λ: "qe",
    if: !("qe" +ₗ #5) then #()
    else
      ("qe" +ₗ #1) <- #1;;
      ("qe" +ₗ #2) <- NONE;;
      ("qe" +ₗ #3) <- #true;;
      ("qe" +ₗ #4) <- !("qe" +ₗ #4) + #1;;
      ("qe" +ₗ #6) <- #false.

Definition query_publish_ok_code : val :=
  λ: "qe" "cur" "v",
    if: (~ !("qe" +ₗ #5)) && (!("qe" +ₗ #4) = "cur") then
      "qe" <- SOME "v";;
      ("qe" +ₗ #1) <- #0;;
      ("qe" +ₗ #2) <- NONE;;
      ("qe" +ₗ #3) <- #false;;
      ("qe" +ₗ #6) <- #false
    else #().

Definition query_publish_err_code : val :=
  λ: "qe" "cur" "err",
    if: (~ !("qe" +ₗ #5)) && (!("qe" +ₗ #4) = "cur") then
      ("qe" +ₗ #1) <- #0;;
      ("qe" +ₗ #2) <- SOME "err";;
      ("qe" +ₗ #3) <- #true;;
      match: !"qe" with
        NONE => ("qe" +ₗ #6) <- #true
      | SOME <> => ("qe" +ₗ #6) <- #false
      end
    else #().

(** Layout of a 3-cell `QueryLease` + family refcount block `ql`:
    - `ql +ₗ 0`: `lease_disposed` (boolean)
    - `ql +ₗ 1`: `ref_count` (integer)
    - `ql +ₗ 2`: `entry_alive` (boolean) *)
Definition query_lease_dispose_code : val :=
  λ: "ql",
    if: !"ql" then #()
    else
      "ql" <- #true;;
      let: "c" := !("ql" +ₗ #1) - #1 in
      ("ql" +ₗ #1) <- "c";;
      if: #0 < "c" then #()
      else ("ql" +ₗ #2) <- #false.

(** Layout of a 2-cell `QueryClient` staging header `qc`:
    - `qc +ₗ 0`: `staging` (boolean)
    - `qc +ₗ 1`: `cache_val` (integer snapshot) *)
Definition query_commit_staging_code : val :=
  λ: "qc" "prepared",
    "qc" <- #true;;
    "qc" <- #false;;
    match: "prepared" with
      NONE => #()
    | SOME "v" => ("qc" +ₗ #1) <- "v"
    end.

(** Layout of a 3-cell `Mvu` control block `mvu`:
    - `mvu +ₗ 0`: `model` (integer)
    - `mvu +ₗ 1`: `disposed` (boolean)
    - `mvu +ₗ 2`: `last_cmd_seen_model` (`NONEV` or `SOMEV #m`) *)
Definition mvu_apply_code : val :=
  λ: "mvu" "next_model",
    if: !("mvu" +ₗ #1) then #()
    else
      "mvu" <- "next_model";;
      if: !("mvu" +ₗ #1) then #()
      else ("mvu" +ₗ #2) <- SOME !"mvu".

Section heap_extensions.
Context `{!heapGS Σ, !graphG Σ}.

Definition is_query_entry (qe_loc : loc) (qe : query_entry_state) : iProp Σ :=
  qe_loc ↦ opt_z_val (qe_data qe) ∗
  (qe_loc +ₗ 1) ↦ fetch_status_val (qe_status qe) ∗
  (qe_loc +ₗ 2) ↦ opt_nat_val (qe_error qe) ∗
  (qe_loc +ₗ 3) ↦ #(qe_stale qe) ∗
  (qe_loc +ₗ 4) ↦ #(Z.of_nat (qe_gen qe)) ∗
  (qe_loc +ₗ 5) ↦ #(qe_disposed qe) ∗
  (qe_loc +ₗ 6) ↦ #(qe_gate_faulted qe).

(**md
**`query_start_code` transitions `is_query_entry` along `qe_start`, advancing `qe_gen` and retaining `qe_data`.**

- [qe_loc:] the `QueryEntry` control block location.
- [qe:] the pure `query_entry_state` before `start`.
*)
Theorem query_start_refresh_spec (qe_loc : loc) (qe : query_entry_state) :
  {{{ is_query_entry qe_loc qe }}}
    query_start_code #qe_loc
  {{{ RET #(); is_query_entry qe_loc (qe_start qe) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5 & H6) HΦ".
  rewrite /query_start_code /qe_start.
  wp_lam. wp_pures. wp_load.
  destruct (qe_disposed qe) eqn:Edisp; wp_pures.
  - iModIntro. iApply "HΦ". rewrite /is_query_entry Edisp. iFrame.
  - wp_store. wp_store. wp_store. wp_load. wp_pures. wp_store. wp_store.
    iModIntro. iApply "HΦ".
    rewrite /is_query_entry /= Nat2Z.inj_succ Z.add_1_r. iFrame.
Qed.

(**md
**`query_publish_ok_code` and `query_publish_err_code` transition `is_query_entry` along `qe_publish`, fencing out stale generations and retaining cached data on refresh error.**

- [qe_loc:] the `QueryEntry` control block location.
- [flight_gen:] the generation of the completing request.
- [qe:] the pure `query_entry_state` before `publish`.
*)
Theorem query_publish_fenced_spec (qe_loc : loc) (flight_gen : nat) (qe : query_entry_state) :
  (forall (v : Z),
     {{{ is_query_entry qe_loc qe }}}
       query_publish_ok_code #qe_loc #(Z.of_nat flight_gen) #v
     {{{ RET #(); is_query_entry qe_loc (qe_publish flight_gen (inl v) qe) }}}) /\
  (forall (err : nat),
     {{{ is_query_entry qe_loc qe }}}
       query_publish_err_code #qe_loc #(Z.of_nat flight_gen) #(Z.of_nat err)
     {{{ RET #(); is_query_entry qe_loc (qe_publish flight_gen (inr err) qe) }}}).
Proof.
  split.
  - intros v.
    iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5 & H6) HΦ".
    rewrite /query_publish_ok_code.
    wp_lam. wp_pures. wp_load.
    destruct (qe_disposed qe) eqn:Edisp; wp_pures.
    + iModIntro. iApply "HΦ".
      rewrite /qe_publish decide_False; [|intros [Hfalse _]; congruence].
      rewrite /is_query_entry Edisp. iFrame.
    + wp_load. wp_pures.
      destruct (decide (flight_gen = qe_gen qe)) as [Hgen|Hne].
      * rewrite Hgen bool_decide_eq_true_2 //.
        wp_pures. wp_store. wp_store. wp_store. wp_store. wp_store.
        iModIntro. iApply "HΦ".
        rewrite /qe_publish decide_True; [|split; [exact Edisp|reflexivity]].
        rewrite /is_query_entry /=. iFrame.
      * rewrite bool_decide_eq_false_2; [|intros Heq; injection Heq as Heq; apply Hne; lia].
        wp_pures. iModIntro. iApply "HΦ".
        rewrite /qe_publish decide_False; [|intros [_ Heq]; exact (Hne Heq)].
        rewrite /is_query_entry Edisp. iFrame.
  - intros err.
    iIntros (Φ) "(H0 & H1 & H2 & H3 & H4 & H5 & H6) HΦ".
    rewrite /query_publish_err_code.
    wp_lam. wp_pures. wp_load.
    destruct (qe_disposed qe) eqn:Edisp; wp_pures.
    + iModIntro. iApply "HΦ".
      rewrite /qe_publish decide_False; [|intros [Hfalse _]; congruence].
      rewrite /is_query_entry Edisp. iFrame.
    + wp_load. wp_pures.
      destruct (decide (flight_gen = qe_gen qe)) as [Hgen|Hne].
      * rewrite Hgen bool_decide_eq_true_2 //.
        wp_pures. wp_store. wp_store. wp_store. wp_load.
        destruct (qe_data qe) as [v0|] eqn:Edata; simpl; wp_pures; wp_store; iModIntro; iApply "HΦ";
          (rewrite /qe_publish decide_True; [|split; [exact Edisp|reflexivity]]);
          rewrite /is_query_entry /= Edata /=; iFrame.
      * rewrite bool_decide_eq_false_2; [|intros Heq; injection Heq as Heq; apply Hne; lia].
        wp_pures. iModIntro. iApply "HΦ".
        rewrite /qe_publish decide_False; [|intros [_ Heq]; exact (Hne Heq)].
        rewrite /is_query_entry Edisp. iFrame.
Qed.

Definition is_query_lease (ql_loc : loc) (st : query_lease_state) : iProp Σ :=
  ql_loc ↦ #(ql_lease_disposed st) ∗
  (ql_loc +ₗ 1) ↦ #(Z.of_nat (ql_ref_count st)) ∗
  (ql_loc +ₗ 2) ↦ #(ql_entry_alive st).

(**md
**`query_lease_dispose_code` transitions `is_query_lease` along `query_lease_dispose`, decrementing `ql_ref_count` once and evicting the entry iff the count reaches `0`.**

- [ql_loc:] the `QueryLease` control block location.
- [st:] the lease and family reference-count state.
*)
Theorem query_lease_dispose_spec (ql_loc : loc) (st : query_lease_state) :
  (ql_lease_disposed st = false -> 0 < ql_ref_count st /\ ql_entry_alive st = true) ->
  {{{ is_query_lease ql_loc st }}}
    query_lease_dispose_code #ql_loc
  {{{ RET #(); is_query_lease ql_loc (query_lease_dispose st) }}}.
Proof.
  intros Hinv.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /query_lease_dispose_code.
  wp_lam. wp_pures. wp_load.
  destruct (ql_lease_disposed st) eqn:Edisp; wp_pures.
  - iModIntro. iApply "HΦ". rewrite /query_lease_dispose Edisp /is_query_lease Edisp. iFrame.
  - destruct (Hinv eq_refl) as [Hpos Halive].
    wp_store. wp_load. wp_pures. wp_store. wp_pures.
    assert (Hz : (Z.of_nat (ql_ref_count st) - 1)%Z = Z.of_nat (ql_ref_count st - 1)) by lia.
    rewrite Hz.
    destruct (decide (0 < ql_ref_count st - 1)) as [Hgt|Hle].
    + rewrite bool_decide_eq_true_2; [|lia].
      wp_pures. iModIntro. iApply "HΦ".
      rewrite /query_lease_dispose Edisp /is_query_lease /= (bool_decide_eq_true_2 (0 < ql_ref_count st - 1) Hgt) Halive /=.
      iFrame.
    + rewrite bool_decide_eq_false_2; [|lia].
      wp_pures. wp_store. iModIntro. iApply "HΦ".
      rewrite /query_lease_dispose Edisp /is_query_lease /= (bool_decide_eq_false_2 (0 < ql_ref_count st - 1) Hle) /=.
      iFrame.
Qed.

(**md
**`query_commit_staging_code` brackets `Prepare` with `staging = true -> false` and updates the cache iff `prepared = Some v` (`commit_staging`).**

- [qc:] the `QueryClient` staging header location.
- [prepared:] the result of staging (`None` on validation failure, `Some v` on success).
- [cache:] the initial cache value.
*)
Theorem query_commit_staging_spec (qc : loc) (prepared : option Z) (cache : Z) :
  {{{ qc ↦ #false ∗ (qc +ₗ 1) ↦ #cache }}}
    query_commit_staging_code #qc (opt_z_val prepared)
  {{{ RET #(); qc ↦ #false ∗ (qc +ₗ 1) ↦ #(commit_staging prepared cache) }}}.
Proof.
  iIntros (Φ) "[H0 H1] HΦ".
  rewrite /query_commit_staging_code /commit_staging.
  wp_lam. wp_pures. wp_store. wp_store.
  destruct prepared as [v|]; simpl; wp_pures.
  - wp_store. iModIntro. iApply "HΦ". iFrame.
  - iModIntro. iApply "HΦ". iFrame.
Qed.

Definition is_mvu (mvu_loc : loc) (model : Z) (disposed : bool) (seen : option Z) : iProp Σ :=
  mvu_loc ↦ #model ∗
  (mvu_loc +ₗ 1) ↦ #disposed ∗
  (mvu_loc +ₗ 2) ↦ opt_z_val seen.

(**md
**`mvu_apply_code` drops dispatches when `disposed = true`, and when `disposed = false` writes `next_model` to `mvu_loc` before running the command so the command reads `Some next_model`.**

- [mvu_loc:] the `Mvu` control block location.
- [model:] the model value before `Dispatch`.
- [next_model:] the new model value computed by `update`/`step`.
- [disposed:] whether the owning scope has been torn down.
- [seen:] the previous command-observed model snapshot.
*)
Theorem mvu_apply_spec (mvu_loc : loc) (model next_model : Z) (disposed : bool) (seen : option Z) :
  {{{ is_mvu mvu_loc model disposed seen }}}
    mvu_apply_code #mvu_loc #next_model
  {{{ RET #();
      is_mvu mvu_loc
        (if disposed then model else next_model)
        disposed
        (if disposed then seen else Some next_model) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /mvu_apply_code.
  wp_lam. wp_pures. wp_load.
  destruct disposed; wp_pures.
  - iModIntro. iApply "HΦ". rewrite /is_mvu. iFrame.
  - wp_store. wp_load. wp_pures. wp_load. wp_pures. wp_store.
    iModIntro. iApply "HΦ". rewrite /is_mvu /=. iFrame.
Qed.

End heap_extensions.
