From stdpp Require Import prelude gmap.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost.

Open Scope nat_scope.

(** * Heap specifications of the five marking operations

The five marking operations ([mark_check], [notify_check], [mark_dirty], [notify_dirty],
[notify_dirty_except]) run on the heap graph without fuel at runtime. Each specification connects a pure
marking run [op fuel ... s = Some s'] to a HeapLang triple transitioning [is_graph γ g s] to
[is_graph γ g s']. Every observer walk uses [walk_cursor_spec] with [is_graph_observers_acc], and every node
write steps the epoch table through [graph_set_node_spec]. *)

Definition node_marked_code : val :=
  λ: "f" "u" "q" "d",
    let: "ab" := Fst "d" in
    let: "cd" := Snd "d" in
    let: "b" := Snd "ab" in
    let: "c" := Fst "cd" in
    ((Fst "ab", (("f", "u"), Snd "b")),
     (((Fst (Fst "c"), "q"), Snd "c"), Snd "cd")).

Definition announce_due_code : val :=
  λ: "g" "d",
    let: "fu" := Fst (Snd (Snd (graph_get_ctrl_code "g"))) in
    let: "du" := Snd (Fst (Snd (Fst "d"))) in
    if: "fu" then "du" else #false.

Definition effect_marked_code : val :=
  λ: "g" "f" "n" "d",
    let: "c" := Fst (Snd "d") in
    let: "disp" := Fst (Fst "c") in
    let: "q" := Snd (Fst "c") in
    let: "cl" := Fst (Snd "c") in
    let: "u" := Snd (Fst (Snd (Fst "d"))) in
    if: (if: "disp" then #true else "cl") then #()
    else
      let: "d'" := node_marked_code "f" "u" #true "d" in
      graph_set_node_code "g" "n" "d'";;
      if: "q" then #() else graph_enqueue_code "g" "n".

Definition notify_with_code : val :=
  λ: "mark" "g" "src",
    match: graph_get_node_code "g" "src" with
      NONE => #()
    | SOME <> =>
        let: "lo" := Fst ((! "g") "src") in
        observers_walk_code "lo" (λ: "c", "mark" "g" "c")
    end.

Definition mark_check_code : val :=
  rec: "mark_check" "g" "n" :=
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        let: "k" := Fst (Fst (Fst (Fst "d"))) in
        let: "b" := Snd (Fst "d") in
        let: "f" := Fst (Fst "b") in
        let: "u" := Snd (Fst "b") in
        let: "q" := Snd (Fst (Fst (Snd "d"))) in
        if: "k" = #1 then
          if: "f" = #0 then
            let: "d'" := node_marked_code #1 "u" "q" "d" in
            graph_set_node_code "g" "n" "d'";;
            notify_with_code "mark_check" "g" "n"
          else if: announce_due_code "g" "d" then
            let: "d'" := node_marked_code "f" #false "q" "d" in
            graph_set_node_code "g" "n" "d'";;
            notify_with_code "mark_check" "g" "n"
          else #()
        else if: "k" = #2 then
          let: "f'" := if: "f" = #0 then #1 else "f" in
          effect_marked_code "g" "f'" "n" "d"
        else #()
    end.

Definition notify_check_code : val :=
  λ: "g" "src", notify_with_code mark_check_code "g" "src".

Definition mark_dirty_code : val :=
  λ: "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        let: "k" := Fst (Fst (Fst (Fst "d"))) in
        let: "b" := Snd (Fst "d") in
        let: "f" := Fst (Fst "b") in
        let: "u" := Snd (Fst "b") in
        let: "q" := Snd (Fst (Fst (Snd "d"))) in
        if: "k" = #1 then
          if: "f" = #0 then
            let: "d'" := node_marked_code #2 "u" "q" "d" in
            graph_set_node_code "g" "n" "d'";;
            notify_with_code mark_check_code "g" "n"
          else if: announce_due_code "g" "d" then
            let: "d'" := node_marked_code #2 #false "q" "d" in
            graph_set_node_code "g" "n" "d'";;
            notify_with_code mark_check_code "g" "n"
          else if: "f" = #2 then #()
          else
            let: "d'" := node_marked_code #2 "u" "q" "d" in
            graph_set_node_code "g" "n" "d'"
        else if: "k" = #2 then
          effect_marked_code "g" #2 "n" "d"
        else #()
    end.

Definition notify_dirty_code : val :=
  λ: "g" "src", notify_with_code mark_dirty_code "g" "src".

Definition mark_except_code : val :=
  λ: "src" "r" "g" "c",
    let: "skip" :=
      match: "r" with
        NONE => #false
      | SOME "rc" =>
          if: "rc" = "c" then ~ (graph_held_in_prefix_code "g" "c" "src")
          else #false
      end in
    if: "skip" then #() else mark_dirty_code "g" "c".

Definition notify_dirty_except_code : val :=
  λ: "g" "src" "r", notify_with_code (mark_except_code "src" "r") "g" "src".

Lemma walk_marks_clamp mark src n s :
  walk_marks mark src n s =
  walk_marks mark src (min n (length (observer_items (observers_of (g_edges s) src)))) s.
Proof.
  induction n as [|i IH]; [done|].
  set (k := length (observer_items (observers_of (g_edges s) src))).
  destruct (decide (k <= i)) as [Hle|Hlt].
  - replace (min (S i) k) with (min i k) by lia.
    simpl. rewrite decide_True // -IH //.
  - replace (min (S i) k) with (S i) by lia. done.
Qed.

Section marking.
Context `{!heapGS Σ, !graphG Σ}.

Lemma node_marked_spec (f : freshness) (u q : bool) (d : node) :
  {{{ True }}}
    node_marked_code (freshness_val f) #u #q (node_val d)
  {{{ RET node_val (node_marked f u q d); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. rewrite /node_val /node_val_a /node_val_b /node_val_c /node_val_d.
  wp_pures. by iApply "HΦ".
Qed.
Global Opaque node_marked_code.

Lemma announce_due_spec γ g s (d : node) :
  {{{ is_graph γ g s }}}
    announce_due_code g (node_val d)
  {{{ RET #(announce_due s d); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  rewrite /ctrl_val /ctrl_val_a /ctrl_val_b /ctrl_val_c /node_val /node_val_a /node_val_b /announce_due.
  wp_pures. destruct (f_unannounced (g_flags s)); wp_pures; by iApply "HΦ".
Qed.
Global Opaque announce_due_code.

Lemma effect_marked_spec γ g s (f : freshness) (n : node_id) (d : node) :
  g_nodes s !! n = Some d ->
  {{{ is_graph γ g s }}}
    effect_marked_code g (freshness_val f) #(Z.of_nat n) (node_val d)
  {{{ RET #(); is_graph γ g (effect_marked f n d s) }}}.
Proof.
  intros Hn. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  rewrite /effect_marked.
  destruct (n_disposed d) eqn:Hdisp; wp_pures.
  - by iApply "HΦ".
  - destruct (n_in_cleanup d) eqn:Hcl; wp_pures.
    + by iApply "HΦ".
    + wp_apply (node_marked_spec f (n_unannounced d) true d); [done|]. iIntros "_". wp_pures.
      wp_apply (graph_set_node_spec _ _ _ n (node_marked f (n_unannounced d) true d) with "Hg"); [by eexists|]. iIntros "Hg". wp_pures.
      destruct (n_queued d) eqn:Hq; wp_pures.
      * by iApply "HΦ".
      * wp_apply (graph_enqueue_spec with "Hg"). iExact "HΦ".
Qed.
Global Opaque effect_marked_code.

Lemma notify_with_spec γ g (mark : node_id -> State.state -> option State.state) (mark_v : val)
    (src : node_id) (s s' : State.state) :
  □ (∀ (c : node_id) (s0 s1 : State.state),
      ⌜mark c s0 = Some s1⌝ -∗
      {{{ is_graph γ g s0 }}} mark_v g #(Z.of_nat c) {{{ RET #(); is_graph γ g s1 }}}) -∗
  ⌜notify_with mark src s = Some s'⌝ -∗
  {{{ is_graph γ g s }}}
    notify_with_code mark_v g #(Z.of_nat src)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  iIntros "#Hmark %Hnw" (Φ) "!> Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  destruct (g_nodes s !! src) as [d0|] eqn:Hsrc.
  - wp_pures.
    assert (Hdom : src ∈ dom (g_nodes s)) by (by apply elem_of_dom_2 in Hsrc).
    wp_apply (is_graph_resolve_spec _ _ _ src Hdom with "Hg").
    iIntros (lo ls) "[Hg #Hloc]". wp_pures.
    iDestruct (is_graph_observers_acc with "Hg Hloc") as "[Ho Hclose]".
    set (I := (fun (k : nat) (o : observer_set) =>
      ∃ s0 : State.state,
        ⌜o = observers_of (g_edges s0) src⌝ ∗
        ⌜walk_marks mark src k s0 = Some s'⌝ ∗
        (is_observers lo o -∗ is_graph γ g s0))%I).
    wp_apply (walk_cursor_spec lo (λ: "c", mark_v g "c") I (observers_of (g_edges s) src)
                with "[Ho Hclose]").
    + iFrame "Ho". iSplitR "Hclose".
      * iIntros (n i o c slot Φv) "!> (%Hmin & %Hi & Ho & %s0 & -> & %Hw & Hcl) HΦv".
        iDestruct ("Hcl" with "Ho") as "Hg".
        rewrite walk_marks_clamp Hmin /= in Hw.
        rewrite decide_False in Hw; [|lia].
        rewrite Hi in Hw.
        destruct (mark c s0) as [s1|] eqn:Hmc; simpl in Hw; [|done].
        wp_pures. wp_apply ("Hmark" $! c s0 s1 Hmc with "Hg"). iIntros "Hg1".
        iDestruct (is_graph_observers_acc with "Hg1 Hloc") as "[Ho1 Hcl1]".
        iApply "HΦv". iFrame "Ho1". iExists s1. by iFrame.
      * iExists s. by iFrame.
    + iIntros (k o') "(%Hmin & Ho' & %s0 & -> & %Hw & Hcl)".
      iDestruct ("Hcl" with "Ho'") as "Hg".
      rewrite walk_marks_clamp Hmin /= in Hw. simplify_eq.
      by iApply "HΦ".
  - wp_pures. iDestruct (is_graph_absent_observers _ _ _ src Hsrc with "Hg") as %Hempty.
    rewrite /notify_with Hempty /= in Hnw. simplify_eq.
    by iApply "HΦ".
Qed.
Global Opaque notify_with_code.

(**md
**`mark_check_code` transitions the heap graph along `mark_check`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the fuel of the pure run.
- [n:] the node marked.
- [s:] the state before marking.
- [s':] the state after marking.
*)
Theorem mark_check_spec γ g (fuel : nat) (n : node_id) (s s' : State.state) :
  mark_check fuel n s = Some s' ->
  {{{ is_graph γ g s }}}
    mark_check_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  revert n s s'. induction fuel as [|f IH]; intros n s s' Hm; [done|].
  iIntros (Φ) "Hg HΦ". wp_rec. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  simpl in Hm. destruct (g_nodes s !! n) as [d|] eqn:Hn; [|simplify_eq; wp_pures; by iApply "HΦ"].
  wp_pures.
  assert (Hih : ⊢ □ ∀ (c : node_id) (s0 s1 : State.state),
    ⌜mark_check f c s0 = Some s1⌝ -∗
    {{{ is_graph γ g s0 }}} mark_check_code g #(Z.of_nat c) {{{ RET #(); is_graph γ g s1 }}}).
  { iModIntro. iIntros (c s0 s1 Hc Φc) "!> Hg HΦc". by wp_apply (IH c s0 s1 Hc with "Hg"). }
  iPoseProof Hih as "#Hmark".
  destruct (n_kind d) eqn:Hk; simpl.
  - simplify_eq. wp_pures. by iApply "HΦ".
  - wp_pures. destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
    + rewrite Hc /=. wp_pures.
      wp_apply (node_marked_spec Check (n_unannounced d) (n_queued d) d); [done|]. iIntros "_". wp_pures.
      wp_apply (graph_set_node_spec _ _ _ n (node_marked Check (n_unannounced d) (n_queued d) d) with "Hg"); [by eexists|]. iIntros "Hg". wp_pures.
      wp_apply (notify_with_spec _ _ (mark_check f) mark_check_code n _ s' with "Hmark [//] Hg").
      iExact "HΦ".
    + assert (Hfc : bool_decide (freshness_val (n_freshness d) = #0) = false)
        by (destruct (n_freshness d); done).
      rewrite bool_decide_eq_false_2; [|by destruct (n_freshness d)]. wp_pures.
      wp_apply (announce_due_spec _ _ _ d with "Hg"). iIntros "Hg".
      destruct (announce_due s d) eqn:Hdu; wp_pures.
      * wp_apply (node_marked_spec (n_freshness d) false (n_queued d) d); [done|]. iIntros "_". wp_pures.
        wp_apply (graph_set_node_spec _ _ _ n (node_marked (n_freshness d) false (n_queued d) d) with "Hg"); [by eexists|]. iIntros "Hg". wp_pures.
        wp_apply (notify_with_spec _ _ (mark_check f) mark_check_code n _ s' with "Hmark [//] Hg").
        iExact "HΦ".
      * simplify_eq. by iApply "HΦ".
  - simplify_eq. wp_pures.
    destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
    + rewrite Hc /=. wp_pures.
      wp_apply (effect_marked_spec _ _ _ Check n d Hn with "Hg"). iExact "HΦ".
    + rewrite bool_decide_eq_false_2; [|by destruct (n_freshness d)]. wp_pures.
      wp_apply (effect_marked_spec _ _ _ (n_freshness d) n d Hn with "Hg"). iExact "HΦ".
Qed.
Global Opaque mark_check_code.

(**md
**`notify_check_code` transitions the heap graph along `notify_check`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the fuel of the pure run.
- [src:] the source whose observers are marked.
- [s:] the state before marking.
- [s':] the state after marking.
*)
Theorem notify_check_spec γ g (fuel : nat) (src : node_id) (s s' : State.state) :
  notify_check fuel src s = Some s' ->
  {{{ is_graph γ g s }}}
    notify_check_code g #(Z.of_nat src)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hn. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (notify_with_spec _ _ (mark_check fuel) mark_check_code src s s' with "[] [//] Hg"); [|iExact "HΦ"].
  iModIntro. iIntros (c s0 s1 Hc Φc) "!> Hg HΦc".
  by wp_apply (mark_check_spec _ _ fuel c s0 s1 Hc with "Hg").
Qed.
Global Opaque notify_check_code.

(**md
**`mark_dirty_code` transitions the heap graph along `mark_dirty`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the fuel of the pure run.
- [n:] the node marked.
- [s:] the state before marking.
- [s':] the state after marking.
*)
Theorem mark_dirty_spec γ g (fuel : nat) (n : node_id) (s s' : State.state) :
  mark_dirty fuel n s = Some s' ->
  {{{ is_graph γ g s }}}
    mark_dirty_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  destruct fuel as [|f]; intros Hm; [done|].
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  simpl in Hm. destruct (g_nodes s !! n) as [d|] eqn:Hn; [|simplify_eq; wp_pures; by iApply "HΦ"].
  wp_pures.
  assert (Hmc : ⊢ □ ∀ (c : node_id) (s0 s1 : State.state),
    ⌜mark_check f c s0 = Some s1⌝ -∗
    {{{ is_graph γ g s0 }}} mark_check_code g #(Z.of_nat c) {{{ RET #(); is_graph γ g s1 }}}).
  { iModIntro. iIntros (c s0 s1 Hc Φc) "!> Hg HΦc". by wp_apply (mark_check_spec _ _ f c s0 s1 Hc with "Hg"). }
  iPoseProof Hmc as "#Hmark".
  destruct (n_kind d) eqn:Hk; simpl.
  - simplify_eq. wp_pures. by iApply "HΦ".
  - wp_pures. destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
    + rewrite Hc /=. wp_pures.
      wp_apply (node_marked_spec Dirty (n_unannounced d) (n_queued d) d); [done|]. iIntros "_". wp_pures.
      wp_apply (graph_set_node_spec _ _ _ n (node_marked Dirty (n_unannounced d) (n_queued d) d) with "Hg"); [by eexists|]. iIntros "Hg". wp_pures.
      wp_apply (notify_with_spec _ _ (mark_check f) mark_check_code n _ s' with "Hmark [//] Hg").
      iExact "HΦ".
    + rewrite bool_decide_eq_false_2; [|by destruct (n_freshness d)]. wp_pures.
      wp_apply (announce_due_spec _ _ _ d with "Hg"). iIntros "Hg".
      destruct (announce_due s d) eqn:Hdu; wp_pures.
      * wp_apply (node_marked_spec Dirty false (n_queued d) d); [done|]. iIntros "_". wp_pures.
        wp_apply (graph_set_node_spec _ _ _ n (node_marked Dirty false (n_queued d) d) with "Hg"); [by eexists|]. iIntros "Hg". wp_pures.
        wp_apply (notify_with_spec _ _ (mark_check f) mark_check_code n _ s' with "Hmark [//] Hg").
        iExact "HΦ".
      * destruct (decide (n_freshness d = Dirty)) as [Hd|Hd].
        -- simplify_eq. rewrite Hd /=. wp_pures. by iApply "HΦ".
        -- simplify_eq. rewrite bool_decide_eq_false_2; [|by destruct (n_freshness d)]. wp_pures.
           wp_apply (node_marked_spec Dirty (n_unannounced d) (n_queued d) d); [done|]. iIntros "_". wp_pures.
           wp_apply (graph_set_node_spec _ _ _ n (node_marked Dirty (n_unannounced d) (n_queued d) d) with "Hg"); [by eexists|]. iExact "HΦ".
  - simplify_eq. wp_pures.
    wp_apply (effect_marked_spec _ _ _ Dirty n d Hn with "Hg"). iExact "HΦ".
Qed.
Global Opaque mark_dirty_code.

(**md
**`notify_dirty_code` transitions the heap graph along `notify_dirty`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the fuel of the pure run.
- [src:] the source whose observers are marked.
- [s:] the state before marking.
- [s':] the state after marking.
*)
Theorem notify_dirty_spec γ g (fuel : nat) (src : node_id) (s s' : State.state) :
  notify_dirty fuel src s = Some s' ->
  {{{ is_graph γ g s }}}
    notify_dirty_code g #(Z.of_nat src)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hn. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (notify_with_spec _ _ (mark_dirty fuel) mark_dirty_code src s s' with "[] [//] Hg"); [|iExact "HΦ"].
  iModIntro. iIntros (c s0 s1 Hc Φc) "!> Hg HΦc".
  by wp_apply (mark_dirty_spec _ _ fuel c s0 s1 Hc with "Hg").
Qed.
Global Opaque notify_dirty_code.

Lemma mark_except_spec γ g (fuel : nat) (src : node_id) (r : option node_id) (c : node_id) (s s' : State.state) :
  mark_except fuel src r c s = Some s' ->
  {{{ is_graph γ g s }}}
    mark_except_code #(Z.of_nat src) (opt_nat_val r) g #(Z.of_nat c)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hm. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  rewrite /mark_except in Hm.
  destruct r as [rc|]; simpl.
  - wp_pures. case_bool_decide as Heq.
    + assert (rc = c) as -> by (injection Heq; lia). wp_pures.
      wp_apply (graph_held_in_prefix_spec with "Hg"). iIntros "Hg".
      case_bool_decide as Hin; wp_pures.
      * rewrite decide_False in Hm; [|intros [_ Hnin]; done].
        by wp_apply (mark_dirty_spec _ _ fuel c s s' Hm with "Hg").
      * rewrite decide_True in Hm; [|split; done]. simplify_eq.
        by iApply "HΦ".
    + assert (rc ≠ c) by (intros ->; done). wp_pures.
      rewrite decide_False in Hm; [|intros [[= ->] _]; done].
      by wp_apply (mark_dirty_spec _ _ fuel c s s' Hm with "Hg").
  - wp_pures. simpl in Hm.
    by wp_apply (mark_dirty_spec _ _ fuel c s s' Hm with "Hg").
Qed.

(**md
**`notify_dirty_except_code` transitions the heap graph along `notify_dirty_except`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the fuel of the pure run.
- [src:] the source whose observers are marked.
- [r:] the current computation skipped unless `src` is in its read prefix, or `None`.
- [s:] the state before marking.
- [s':] the state after marking.
*)
Theorem notify_dirty_except_spec γ g (fuel : nat) (src : node_id) (r : option node_id) (s s' : State.state) :
  notify_dirty_except fuel src r s = Some s' ->
  {{{ is_graph γ g s }}}
    notify_dirty_except_code g #(Z.of_nat src) (opt_nat_val r)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hn. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_bind (mark_except_code _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = (λ: "g" "c",
    let: "skip" :=
      match: opt_nat_val r with
        NONE => #false
      | SOME "rc" =>
          if: "rc" = "c" then ~ graph_held_in_prefix_code "g" "c" #(Z.of_nat src) else #false
      end in
    if: "skip" then #() else mark_dirty_code "g" "c")%V⌝)%I).
  { wp_lam. by wp_pures. }
  iIntros (v ->).
  wp_apply (notify_with_spec _ _ (mark_except fuel src r) _ src s s' with "[] [//] Hg"); [|iExact "HΦ"].
  iModIntro. iIntros (c s0 s1 Hc Φc) "!> Hg HΦc".
  change ((λ: "g" "c",
    let: "skip" :=
      match: opt_nat_val r with
        NONE => #false
      | SOME "rc" =>
          if: "rc" = "c" then ~ graph_held_in_prefix_code "g" "c" #(Z.of_nat src) else #false
      end in
    if: "skip" then #() else mark_dirty_code "g" "c")%V g #(Z.of_nat c))
    with ((λ: "g" "c",
    let: "skip" :=
      match: opt_nat_val r with
        NONE => #false
      | SOME "rc" =>
          if: "rc" = "c" then ~ graph_held_in_prefix_code "g" "c" #(Z.of_nat src) else #false
      end in
    if: "skip" then #() else mark_dirty_code "g" "c")%V g #(Z.of_nat c)).
  wp_pures.
  rewrite /mark_except in Hc.
  destruct r as [rc|]; simpl.
  - wp_pures. case_bool_decide as Heq.
    + assert (rc = c) as -> by (injection Heq; lia). wp_pures.
      wp_apply (graph_held_in_prefix_spec with "Hg"). iIntros "Hg".
      case_bool_decide as Hin; wp_pures.
      * rewrite decide_False in Hc; [|intros [_ Hnin]; done].
        by wp_apply (mark_dirty_spec _ _ fuel c s0 s1 Hc with "Hg").
      * rewrite decide_True in Hc; [|split; done]. simplify_eq.
        by iApply "HΦc".
    + assert (Hne : rc ≠ c) by (intros ->; done). wp_pures.
      rewrite decide_False in Hc; [|intros [[= ->] _]; done].
      by wp_apply (mark_dirty_spec _ _ fuel c s0 s1 Hc with "Hg").
  - wp_pures. simpl in Hc.
    by wp_apply (mark_dirty_spec _ _ fuel c s0 s1 Hc with "Hg").
Qed.
Global Opaque notify_dirty_except_code.

End marking.
