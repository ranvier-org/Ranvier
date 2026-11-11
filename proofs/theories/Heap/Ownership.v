From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith Sorted.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler Ownership.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run Scheduler.

Open Scope nat_scope.

(** * Heap ownership: doubly-linked `OwnerLink` chains and whole-graph disposal

This module provides:
1. The separation-logic representation and specifications for the doubly-linked `OwnerLink` list of
   `Owner` (`Core.fs` lines 955–1226) with unbounded integer generations (`is_owner_links`,
   `owner_links_new_spec`, `owner_links_append_spec`, `owner_links_detach_spec`,
   `owner_links_walk_prev_spec`, and `owner_links_stale_unlink_spec`).
2. The whole-graph HeapLang specifications for `dispose_node_code`, `discharge_code`, `retire_code`,
   `dispose_scope_code`, `teardown_code`, `run_cleanup_code`, `release_code`, and `attach_code`,
   together with corollaries lifting `Pure/Ownership.v`'s theorems (`owners_in_order`,
   `cleanup_once_new_graph`, `disposed_no_edges_new_graph`, `disposed_effect_never_runs`) to the
   heap tier. *)

(** ** Doubly-linked `OwnerLink` heap representation *)

Definition opt_loc_val (ml : option loc) : val :=
  match ml with None => NONEV | Some l => SOMEV #l end.

Definition prev_loc_of (rentries : list (loc * link)) : option loc :=
  match rentries with [] => None | (lk, _) :: _ => Some lk end.

Definition head_loc_of (rentries : list (loc * link)) : option loc :=
  fst <$> last rentries.

Definition owner_links_new_code : val :=
  λ: "disp",
    let: "ol" := AllocN #4 #0 in
    "ol" <- NONE;;
    ("ol" +ₗ #1) <- NONE;;
    ("ol" +ₗ #2) <- (if: "disp" then #1 else #0);;
    ("ol" +ₗ #3) <- #0;;
    "ol".

Definition owner_links_append_code : val :=
  λ: "ol" "o" "child",
    let: "gen" := !("ol" +ₗ #2) in
    let: "acq" := !("ol" +ₗ #3) in
    ("ol" +ₗ #3) <- ("acq" + #1);;
    let: "lk" := AllocN #5 #0 in
    let: "tl" := !("ol" +ₗ #1) in
    "lk" <- "tl";;
    ("lk" +ₗ #1) <- NONE;;
    ("lk" +ₗ #2) <- ("child", "o");;
    ("lk" +ₗ #3) <- SOME "gen";;
    ("lk" +ₗ #4) <- "acq";;
    (match: "tl" with
       NONE =>
         "ol" <- SOME "lk";;
         ("ol" +ₗ #1) <- SOME "lk"
     | SOME "t" =>
         ("t" +ₗ #1) <- SOME "lk";;
         ("ol" +ₗ #1) <- SOME "lk"
     end);;
    "lk".

Definition owner_links_detach_code : val :=
  λ: "ol",
    let: "tl" := !("ol" +ₗ #1) in
    let: "gen" := !("ol" +ₗ #2) in
    "ol" <- NONE;;
    ("ol" +ₗ #1) <- NONE;;
    ("ol" +ₗ #2) <- ("gen" + #1);;
    ("ol" +ₗ #3) <- #0;;
    "tl".

Definition owner_links_walk_prev_code : val :=
  rec: "walk" "cur" :=
    match: "cur" with
      NONE => NONE
    | SOME "lk" =>
        let: "prev" := !"lk" in
        let: "co" := !("lk" +ₗ #2) in
        let: "gen" := !("lk" +ₗ #3) in
        let: "acq" := !("lk" +ₗ #4) in
        let: "lval" := (Fst "co", Snd "co", "gen", "acq") in
        let: "rest" := "walk" "prev" in
        SOME ("lval", "rest")
    end.

Definition owner_links_unlink_code : val :=
  λ: "ol" "lk",
    let: "gen" := !("ol" +ₗ #2) in
    match: !("lk" +ₗ #3) with
      NONE => #()
    | SOME "lgen" =>
        if: "lgen" = "gen" then
          ("lk" +ₗ #3) <- NONE;;
          let: "prev" := !"lk" in
          let: "next" := !("lk" +ₗ #1) in
          (match: "prev" with
             NONE => "ol" <- "next"
           | SOME "p" => ("p" +ₗ #1) <- "next"
           end);;
          (match: "next" with
             NONE => ("ol" +ₗ #1) <- "prev"
           | SOME "n" => "n" <- "prev"
           end);;
          "lk" <- NONE;;
          ("lk" +ₗ #1) <- NONE
        else #()
    end.

Section owner_links.
Context `{!heapGS Σ}.

Definition is_link_node (lk : loc) (prev next : option loc) (l : link) : iProp Σ :=
  lk ↦ opt_loc_val prev ∗
  (lk +ₗ 1) ↦ opt_loc_val next ∗
  (lk +ₗ 2) ↦ (#(Z.of_nat (l_child l)), #(Z.of_nat (l_owner l)))%V ∗
  (lk +ₗ 3) ↦ opt_nat_val (l_generation l) ∗
  (lk +ₗ 4) ↦ #(Z.of_nat (l_acquired l)).

Fixpoint is_dchain_rev (next : option loc) (rentries : list (loc * link)) : iProp Σ :=
  match rentries with
  | [] => True
  | (lk, l) :: r' =>
      is_link_node lk (prev_loc_of r') next l ∗
      is_dchain_rev (Some lk) r'
  end.

Definition is_owner_links (ol : loc) (o : owner_id) (entries : list (loc * link)) (gen acq : nat) : iProp Σ :=
  let rentries := rev entries in
  ol ↦ opt_loc_val (head_loc_of rentries) ∗
  (ol +ₗ 1) ↦ opt_loc_val (prev_loc_of rentries) ∗
  (ol +ₗ 2) ↦ #(Z.of_nat gen) ∗
  (ol +ₗ 3) ↦ #(Z.of_nat acq) ∗
  is_dchain_rev None rentries.

Local Lemma fields4_ol (l : loc) (v : val) :
  l ↦∗ replicate 4 v ⊣⊢
  l ↦ v ∗ (l +ₗ 1) ↦ v ∗ (l +ₗ 2) ↦ v ∗ (l +ₗ 3) ↦ v.
Proof.
  rewrite /= !array_cons array_nil !Loc.add_assoc /=.
  by rewrite right_id.
Qed.

Local Lemma fields5_lk (l : loc) (v : val) :
  l ↦∗ replicate 5 v ⊣⊢
  l ↦ v ∗ (l +ₗ 1) ↦ v ∗ (l +ₗ 2) ↦ v ∗ (l +ₗ 3) ↦ v ∗ (l +ₗ 4) ↦ v.
Proof.
  rewrite /= !array_cons array_nil !Loc.add_assoc /=.
  by rewrite right_id.
Qed.

(**md
**`owner_links_new_code` allocates an empty doubly-linked owner header with initial generation `0` or `1`.**

- [o:] the owner id.
- [disp:] whether the owner starts disposed (`generation = 1`) or live (`generation = 0`).
*)
Theorem owner_links_new_spec (o : owner_id) (disp : bool) :
  {{{ True }}}
    owner_links_new_code #disp
  {{{ ol, RET #ol; is_owner_links ol o [] (if disp then 1 else 0) 0 }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_alloc ol as "Hol"; [lia|].
  rewrite (_ : Z.to_nat 4 = 4%nat) // /=.
  iDestruct (fields4_ol with "Hol") as "(H0 & H1 & H2 & H3)".
  wp_pures. wp_store. wp_pures. wp_store. wp_pures.
  destruct disp; wp_pures; wp_store; wp_pures; wp_store;
    iModIntro; iApply "HΦ"; rewrite /is_owner_links /=; iFrame; done.
Qed.

(**md
**`owner_links_append_code` appends a new `OwnerLink` at the tail in constant time and increments `acquisitions`.**

- [ol:] the owner link-header location.
- [o:] the owner id.
- [child:] the attached child node id.
- [entries:] the existing `(loc * link)` entries in acquisition order.
- [gen:] the current generation.
- [acq:] the current acquisition count.
*)
Theorem owner_links_append_spec (ol : loc) (o : owner_id) (child : node_id)
    (entries : list (loc * link)) (gen acq : nat) :
  let l := {| l_child := child; l_owner := o; l_generation := Some gen; l_acquired := acq |} in
  {{{ is_owner_links ol o entries gen acq }}}
    owner_links_append_code #ol #(Z.of_nat o) #(Z.of_nat child)
  {{{ lk, RET #lk; is_owner_links ol o (entries ++ [(lk, l)]) gen (S acq) }}}.
Proof.
  intros l. iIntros (Φ) "(H0 & H1 & H2 & H3 & Hch) HΦ".
  wp_lam. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
  rewrite (_ : (Z.of_nat acq + 1)%Z = Z.of_nat (S acq)); [|lia].
  wp_store. wp_pures. wp_alloc lk as "Hlk"; [lia|].
  rewrite (_ : Z.to_nat 5 = 5%nat) // /=.
  iDestruct (fields5_lk with "Hlk") as "(L0 & L1 & L2 & L3 & L4)".
  wp_pures. wp_load.
  wp_store. wp_pures. wp_store. wp_pures. wp_store. wp_pures. wp_store. wp_pures. wp_store. wp_pures.
  destruct (rev entries) as [|[t lt] r'] eqn:Hrev; simpl.
  - wp_pures. wp_store. wp_pures. wp_store. iModIntro. iApply "HΦ".
    rewrite /is_owner_links rev_unit Hrev /is_link_node /=. by iFrame.
  - wp_pures. iDestruct "Hch" as "[(T0 & T1 & T2 & T3 & T4) Hr']".
    wp_store. wp_pures. wp_store. iModIntro. iApply "HΦ".
    rewrite /is_owner_links rev_unit Hrev /head_loc_of last_cons_cons /is_link_node /=. iFrame.
Qed.

(**md
**`owner_links_detach_code` detaches the doubly-linked list in constant time and advances `generation`.**

- [ol:] the owner link-header location.
- [o:] the owner id.
- [entries:] the entries detached by the teardown.
- [gen:] the generation before detaching.
- [acq:] the acquisition count before detaching.
*)
Theorem owner_links_detach_spec (ol : loc) (o : owner_id)
    (entries : list (loc * link)) (gen acq : nat) :
  {{{ is_owner_links ol o entries gen acq }}}
    owner_links_detach_code #ol
  {{{ RET opt_loc_val (prev_loc_of (rev entries));
      is_owner_links ol o [] (S gen) 0 ∗ is_dchain_rev None (rev entries) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2 & H3 & Hch) HΦ".
  wp_lam. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
  wp_store. wp_pures. wp_store. wp_pures.
  rewrite (_ : (Z.of_nat gen + 1)%Z = Z.of_nat (S gen)); [|lia].
  wp_store. wp_pures. wp_store. iModIntro. iApply "HΦ".
  rewrite /is_owner_links /=. by iFrame.
Qed.

(**md
**`owner_links_walk_prev_code` walks `Prev` pointers from the detached tail and returns `links_val (snd <$> rentries)`.**

When applied to `rentries = rev entries` after `owner_links_detach_code`, it yields `links_val (rev (snd <$> entries))`,
matching `rev (o_children x)` in `dispose_scope`.

- [next:] the forward pointer of the current tail node.
- [rentries:] the reversed entry chain from tail to head.
*)
Theorem owner_links_walk_prev_spec (next : option loc) (rentries : list (loc * link)) :
  {{{ is_dchain_rev next rentries }}}
    owner_links_walk_prev_code (opt_loc_val (prev_loc_of rentries))
  {{{ RET links_val (snd <$> rentries); is_dchain_rev next rentries }}}.
Proof.
  induction rentries as [|[lk l] r' IH] in next |- *; iIntros (Φ) "Hch HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - iDestruct "Hch" as "[(L0 & L1 & L2 & L3 & L4) Hr']".
    wp_load. wp_pures. wp_load. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
    wp_apply (IH (Some lk) with "Hr'"). iIntros "Hr'". wp_pures.
    iModIntro.
    change (SOMEV ((#(Z.of_nat (l_child l)), #(Z.of_nat (l_owner l)), opt_nat_val (l_generation l), #(Z.of_nat (l_acquired l)))%V,
                   links_val (snd <$> r')))
      with (links_val (l :: (snd <$> r'))).
    iApply "HΦ". rewrite /is_link_node. iFrame.
Qed.

(**md
**`owner_links_unlink_code` is a no-op when called on a link from a detached or unlinked generation.**

- [ol:] the owner link-header location.
- [o:] the owner id.
- [entries:] the current live entries of `ol`.
- [gen:] the current generation of `ol`.
- [acq:] the current acquisition count of `ol`.
- [lk:] the link location being unlinked.
- [prev:] the `Prev` field of `lk`.
- [next:] the `Next` field of `lk`.
- [l:] the pure link value of `lk`, with `l_generation l ≠ Some gen`.
*)
Theorem owner_links_stale_unlink_spec (ol : loc) (o : owner_id)
    (entries : list (loc * link)) (gen acq : nat)
    (lk : loc) (prev next : option loc) (l : link) :
  l_generation l ≠ Some gen ->
  {{{ is_owner_links ol o entries gen acq ∗ is_link_node lk prev next l }}}
    owner_links_unlink_code #ol #lk
  {{{ RET #(); is_owner_links ol o entries gen acq ∗ is_link_node lk prev next l }}}.
Proof.
  intros Hne. iIntros (Φ) "[(H0 & H1 & H2 & H3 & Hch) (L0 & L1 & L2 & L3 & L4)] HΦ".
  wp_lam. wp_pures. wp_load. wp_pures. wp_load.
  destruct (l_generation l) as [g0|] eqn:Hgen; simpl; wp_pures.
  - rewrite (bool_decide_eq_false_2 (#(Z.of_nat g0) = #(Z.of_nat gen))%V);
      [|intros [= ?]; apply Hne; f_equal; lia].
    wp_pures. iModIntro. iApply "HΦ". rewrite /is_owner_links /is_link_node Hgen /=. iFrame.
  - iModIntro. iApply "HΦ". rewrite /is_owner_links /is_link_node Hgen /=. iFrame.
Qed.

End owner_links.

(** ** Whole-graph ownership specifications and corollaries *)

Section heap_ownership.
Context `{!heapGS Σ, !graphG Σ}.

(**md
**`dispose_node_code` refines `dispose_node` (`Memo.Dispose` and `Effect.Dispose`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the node id disposed.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem dispose_node_spec γ g (n : node_id) (s : State.state) (fuel : nat) (s' : State.state) :
  dispose_node fuel n s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    dispose_node_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_dispose_node _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`discharge_code` refines `discharge` (`Graph.Discharge`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [o:] the owner id discharged.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem discharge_spec γ g (o : owner_id) (s : State.state) (fuel : nat) (s' : State.state) :
  discharge fuel o s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    discharge_code g #(Z.of_nat o)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_discharge _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`retire_code` refines `retire` (`Graph.Retire` and `Owner.Dispose`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [o:] the owner id retired.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem retire_spec γ g (o : owner_id) (s : State.state) (fuel : nat) (s' : State.state) :
  retire fuel o s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    retire_code g #(Z.of_nat o)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_retire _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`dispose_scope_code` refines `dispose_scope` (`Owner.DisposeScope`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [o:] the owner id whose scope is torn down.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem dispose_scope_spec γ g (o : owner_id) (s : State.state) (fuel : nat) (s' : State.state) :
  dispose_scope fuel o s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    dispose_scope_code g #(Z.of_nat o)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_dispose_scope _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`teardown_code` refines `teardown` (the reverse-acquisition merge loop of `Owner.DisposeScope`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [o:] the owner id being torn down.
- [ls:] the remaining child links in reverse acquisition order.
- [cs:] the remaining cleanups in reverse acquisition order.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem teardown_spec γ g (o : owner_id) (ls : list link) (cs : list (cmd * env * nat))
    (s : State.state) (fuel : nat) (s' : State.state) :
  teardown fuel o ls cs s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    teardown_code g #(Z.of_nat o) (links_val ls) (cleanups_val cs)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_teardown _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`run_cleanup_code` refines `run_cleanup` over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [o:] the owner id whose cleanup runs.
- [b:] the cleanup body command.
- [e:] the cleanup environment.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem run_cleanup_spec γ g (o : owner_id) (b : cmd) (e : env)
    (s : State.state) (fuel : nat) (s' : State.state) :
  run_cleanup fuel o b e s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    run_cleanup_code g #(Z.of_nat o) (compile_cmd b) (env_val e)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_run_cleanup _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`release_code` refines `release` (`IOwned.Release`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the released node id.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem release_spec γ g (n : node_id) (s : State.state) (fuel : nat) (s' : State.state) :
  release fuel n s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    release_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_release _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`attach_code` refines `attach` (`Owner.AttachLinked`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the child node id.
- [o:] the owner id.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem attach_spec γ g (n : node_id) (o : owner_id) (s : State.state) (fuel : nat) (s' : State.state) :
  attach fuel n o s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    attach_code g #(Z.of_nat n) #(Z.of_nat o)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_attach _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**After any top-level heap program, every owner's children and cleanups are in acquisition order, each host's cleanup events are bounded by its registrations, and every non-running disposed node holds no source edges.**

Combines `run_top_spec` with `owners_in_order`, `cleanup_once_new_graph`, and `disposed_no_edges_new_graph`
from `Pure/Ownership.v`.

- [c:] the top-level command.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the returned outcome.
*)
Corollary heap_ownership_guarantees (c : cmd) (fuel : nat) (s' : State.state) (r : outcome) :
  run_top fuel c (init current_flags) = Some (s', r) ->
  {{{ True }}}
    run_top_code (compile_cmd c)
  {{{ γ g, RET outcome_val r;
      is_graph γ g s' ∗
      ⌜(∀ o y, g_owners s' !! o = Some y -> acq_ok y) /\
       (∀ h, count_cleanups h (g_trace s') <= registered h s') /\
       (∀ n d, g_nodes s' !! n = Some d -> n_disposed d = true -> n ∉ g_running s' ->
          source_slots (sources_of (g_edges s') n) = [] /\
          (∀ src, ~ edge s' src n) /\
          (∀ src, n ∉ observer_view (observers_of (g_edges s') src)))⌝ }}}.
Proof.
  intros Hrun. iIntros (Φ) "_ HΦ".
  wp_apply (run_top_spec c fuel s' r Hrun); [done|]. iIntros (γ g) "Hg".
  iApply ("HΦ" $! γ g). iFrame "Hg". iPureIntro. split_and!.
  - intros o y Hy. exact (owners_in_order fuel current_flags c s' r o y Hrun Hy).
  - intros h. exact (proj1 (cleanup_once_new_graph fuel current_flags) c s' r h Hrun).
  - intros n d Hd Hdisp Hnr. exact (disposed_no_edges_new_graph fuel c s' r n d Hrun Hd Hdisp Hnr).
Qed.

End heap_ownership.
