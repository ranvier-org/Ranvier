From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Reference Require Export Semantics.

(** * Determinism *)

Lemma settle_mono o1 o2 p :
  (forall l, o1 l = o2 l) ->
  forall f1 f2 st r,
    (f1 <= f2)%nat ->
    settle f1 o1 p st = Some r ->
    settle f2 o2 p st = Some r.
Proof.
  intros Hext f1. induction f1 as [|f1 IH]; intros f2 st r Hle Hs.
  - destruct f2; simpl in *;
      destruct (run_set p st (effect_ids p)) as [[|n ns]|]; simpl in *; congruence.
  - destruct f2 as [|f2]; [lia|]. simpl in *.
    destruct (run_set p st (effect_ids p)) as [[|n ns]|]; simpl in *; try congruence.
    rewrite <- Hext.
    destruct (run_round p (o1 (n :: ns)) st) as [[st1 r1]|]; simpl in *; [|discriminate].
    destruct (settle f1 o1 p st1) as [[st2 r2]|] eqn:E; simpl in *; [|discriminate].
    rewrite (IH f2 st1 (st2, r2)); [done|lia|done].
Qed.

Lemma run_history_mono o1 o2 p f1 f2 :
  (forall l, o1 l = o2 l) ->
  (f1 <= f2)%nat ->
  forall h st r,
    run_history f1 o1 p h st = Some r ->
    run_history f2 o2 p h st = Some r.
Proof.
  intros Hext Hle h. induction h as [|b h IH]; intros st r Hs; simpl in *; [done|].
  destruct (apply_batch p b (rs_sigs st)) as [sigs|]; simpl in *; [|discriminate].
  destruct (settle f1 o1 p _) as [[st1 r1]|] eqn:E1; simpl in *; [|discriminate].
  rewrite (settle_mono o1 o2 p Hext f1 f2 _ _ Hle E1). simpl.
  destruct (run_history f1 o1 p h st1) as [[st2 r2]|] eqn:E2; simpl in *; [|discriminate].
  rewrite (IH st1 (st2, r2) E2). done.
Qed.

Lemma rruns_mono o1 o2 p f1 f2 h rs :
  (forall l, o1 l = o2 l) ->
  (f1 <= f2)%nat ->
  rruns f1 o1 p h = Some rs ->
  rruns f2 o2 p h = Some rs.
Proof.
  intros Hext Hle. unfold rruns. rewrite <- Hext.
  destruct (run_round _ _ _) as [[st1 r1]|]; simpl; [|discriminate].
  destruct (settle f1 o1 p st1) as [[st2 r2]|] eqn:E1; simpl; [|discriminate].
  rewrite (settle_mono o1 o2 p Hext f1 f2 _ _ Hle E1). simpl.
  destruct (run_history f1 o1 p h st2) as [[st3 r3]|] eqn:E2; simpl; [|discriminate].
  rewrite (run_history_mono o1 o2 p f1 f2 Hext Hle h st2 (st3, r3) E2). done.
Qed.

(** Two orders that agree on every list, and any two sufficient fuels, give one trace. *)
Theorem rtrace_deterministic f1 f2 o1 o2 p h t1 t2 :
  (forall l, o1 l = o2 l) ->
  rtrace f1 o1 p h = Some t1 ->
  rtrace f2 o2 p h = Some t2 ->
  t1 = t2.
Proof.
  intros Hext. unfold rtrace.
  destruct (rruns f1 o1 p h) as [r1|] eqn:E1; simpl; [|discriminate].
  destruct (rruns f2 o2 p h) as [r2|] eqn:E2; simpl; [|discriminate].
  intros [= <-] [= <-].
  destruct (Nat.le_total f1 f2) as [Hle|Hle].
  - rewrite (rruns_mono o1 o2 p f1 f2 h r1 Hext Hle E1) in E2. congruence.
  - rewrite (rruns_mono o2 o1 p f2 f1 h r2 (fun l => eq_sym (Hext l)) Hle E2) in E1. congruence.
Qed.

(** * Memo values *)

#[local] Arguments memo_value : simpl never.
#[local] Arguments live_read : simpl never.
#[local] Arguments frozen_read : simpl never.

#[local] Ltac bind_in H :=
  repeat match type of H with
  | context [?x ≫= _] =>
      let E := fresh "E" in destruct x eqn:E; simpl in H; [|discriminate H]
  end.

Lemma exec_reads_mono p rd n c :
  forall g st o st' x,
    exec p rd n g c st = Some (o, st') ->
    x ∈ breads st ->
    x ∈ breads st'.
Proof.
  induction c; intros g st o' st' x' Hex Hx; simpl in Hex; bind_in Hex;
    repeat case_match; simplify_eq/=; try done;
    try (eapply IHc; [exact Hex|]; simpl; set_solver);
    try (eapply IHc1; [exact Hex|]; done);
    try (eapply IHc2; [exact Hex|]; done);
    try (simpl; set_solver).
  - destruct p0; simplify_eq/=. eapply IHc2; [exact Hex|]. eapply IHc1; [exact E|done].
Qed.

Lemma exec_ext p (rd1 rd2 : nid -> option outcome) n c :
  forall g st o st',
    well_behaved_memo c = true ->
    exec p (fun _ => rd1) n g c st = Some (o, st') ->
    (forall t u, (t, u) ∈ breads st' -> rd1 t = Some u -> rd2 t = Some u) ->
    exec p (fun _ => rd2) n g c st = Some (o, st').
Proof.
  induction c; intros g st o' st' Hwb Hex Hag; simpl in Hwb, Hex |- *;
    rewrite ?andb_true_iff in Hwb; try discriminate Hwb; bind_in Hex; simplify_eq/=.
  - done.
  - done.
  - simpl. destruct Hwb. eauto.
  - simpl. destruct Hwb as [[_ ?] ?]. repeat case_match; eauto.
  - simpl. destruct Hwb. rewrite (Hag n1 o).
    + simpl. destruct o; simplify_eq/=; [eauto|].
      set_solver.
    + destruct o; simplify_eq/=.
      * eapply exec_reads_mono; [exact Hex|]. simpl. set_solver.
      * set_solver.
    + done.
  - simpl. destruct Hwb. rewrite (Hag n1 o).
    + simpl. destruct o; simplify_eq/=; eauto.
    + destruct o; (eapply exec_reads_mono; [exact Hex|]); simpl; set_solver.
    + done.
  - destruct Hwb. rewrite (IHc1 g st p0.1 p0.2); [|done| |].
    + simpl. destruct p0 as [o1 st1]; simpl in *. destruct o1; simplify_eq/=; eauto.
    + destruct p0; exact E.
    + intros t u Ht. apply Hag. destruct p0 as [o1 st1]; simpl in *. destruct o1; simplify_eq/=.
      * eapply exec_reads_mono; [exact Hex|done].
      * done.
  - simpl. destruct Hwb. eauto.
Qed.

Lemma node_value_ext p s1 s2 :
  map_Forall (fun _ c => well_behaved_memo c = true) (rmemos p) ->
  forall k n o,
    node_value k p s1 n = Some o ->
    (forall s, s ∈ node_deps k p s1 n -> s1 !! s = s2 !! s) ->
    node_value k p s2 n = Some o.
Proof.
  intros Hwb k. induction k as [|k IH]; intros n o Hv Hdeps; simpl in *; [discriminate|].
  destruct (rsignals p !! n) as [v|] eqn:Es.
  - rewrite <- (Hdeps n); [done|set_solver].
  - destruct (rmemos p !! n) as [c|] eqn:Em; [|discriminate].
    destruct (exec p (fun _ => node_value k p s1) n [] c (bs_start ∅)) as [[o1 st1]|] eqn:Ex;
      simpl in Hv; [|discriminate]. simplify_eq.
    erewrite (exec_ext p _ (node_value k p s2) n c); [done|by eapply Hwb|exact Ex|].
    intros t u Hin Ht. apply (IH t u Ht). intros s Hs. apply Hdeps.
    apply list_elem_of_In, in_flat_map. exists (t, u). split.
    + by apply list_elem_of_In.
    + by apply list_elem_of_In.
Qed.

(** A memo's value over [s1] is its value over any [s2] that agrees on the signals discovered by
    the evaluation over [s1]. *)
Theorem memo_value_extensional p s1 s2 n o :
  rwf p ->
  memo_value p s1 n = Some o ->
  (forall s, s ∈ memo_deps p s1 n -> s1 !! s = s2 !! s) ->
  memo_value p s2 n = Some o.
Proof.
  intros (_ & _ & _ & Hwb & _). apply node_value_ext. exact Hwb.
Qed.

(** * Glitch freedom *)

(** The run's reads and events are those of a run with every read answered from [run_snapshot r].
    A cleanup's environment, and so a value it logs, can date from the run that registered it. *)
Definition computed_from_snapshot (p : rprog) (r : run) : Prop :=
  exists body bs,
    reffects p !! run_node r = Some body /\
    run_effect p (frozen_read p (run_snapshot r)) (run_node r) body
      (run_ran_cleanups r) (run_snapshot r) = Some bs /\
    run_reads r = breads bs /\
    run_events r = bevs bs.

Lemma exec_writes_len p rd n c :
  forall g st o st',
    exec p rd n g c st = Some (o, st') ->
    (length (bwrites st) <= length (bwrites st'))%nat.
Proof.
  induction c; intros g st o' st' Hex; simpl in Hex; bind_in Hex;
    repeat case_match; simplify_eq/=; try done;
    try (eapply IHc; exact Hex).
  all: try (destruct p0; simplify_eq/=).
  all: try (etrans; [eapply IHc1; exact E|eapply IHc2; exact Hex]).
  all: try (eapply IHc1; exact E).
  all: try (eapply IHc1; exact Hex).
  all: try (eapply IHc2; exact Hex).
  all: try apply IHc in Hex; try apply IHc2 in Hex.
  all: unfold bs_track, bs_write, bs_cleanup, bs_emit in Hex; simpl in Hex;
    rewrite ?length_app in Hex; simpl in Hex; lia.
Qed.

Lemma exec_frozen p n c :
  forall g st o st',
    exec p (live_read p) n g c st = Some (o, st') ->
    length (bwrites st') = length (bwrites st) ->
    exec p (frozen_read p (bsigs st)) n g c st = Some (o, st') /\ bsigs st' = bsigs st.
Proof.
  induction c; intros g st o' st' Hex Hlen; simpl in Hex |- *; bind_in Hex; simplify_eq/=.
  - done.
  - done.
  - eauto.
  - repeat case_match; eauto.
  - unfold frozen_read, live_read in *. rewrite E0. simpl.
    destruct o; simplify_eq/=; [|done].
    apply (IHc _ (bs_track _ _ st)); done.
  - unfold frozen_read, live_read in *. rewrite E0. simpl.
    destruct o; simplify_eq/=.
    + apply (IHc _ (bs_track _ _ st)); done.
    + apply (IHc _ (bs_track _ _ st)); done.
  - unfold frozen_read, live_read in *. rewrite E0. simpl.
    destruct o; simplify_eq/=; [|done].
    apply IHc; done.
  - exfalso. destruct (decide _); [|discriminate Hex].
    apply exec_writes_len in Hex. unfold bs_write in Hex. simpl in Hex.
    rewrite length_app in Hex. simpl in Hex. lia.
  - apply (IHc2 _ (bs_cleanup _ _ st)); done.
  - destruct p0 as [o1 st1]; simpl in *.
    pose proof (exec_writes_len _ _ _ _ _ _ _ _ E) as Hl1.
    destruct o1; simplify_eq/=.
    + pose proof (exec_writes_len _ _ _ _ _ _ _ _ Hex) as Hl2.
      destruct (IHc1 _ _ _ _ E ltac:(lia)) as [Hf Hs]. rewrite Hf. simpl.
      destruct (IHc2 _ _ _ _ Hex ltac:(lia)) as [Hf' Hs']. rewrite Hs in Hf'.
      split; [done|congruence].
    + destruct (IHc1 _ _ _ _ E ltac:(lia)) as [Hf Hs]. rewrite Hf. simpl. done.
  - apply (IHc _ (bs_emit (EvLog n v) st)); done.
Qed.

Lemma run_cleanups_writes_len p rd n cs :
  forall st st',
    run_cleanups p rd n cs st = Some st' ->
    (length (bwrites st) <= length (bwrites st'))%nat.
Proof.
  induction cs as [|[b g] cs IH]; intros st st' Hr; simpl in Hr; simplify_eq/=; [done|].
  bind_in Hr. destruct p0 as [o1 st1]; simpl in *.
  pose proof (exec_writes_len _ _ _ _ _ _ _ _ E). simpl in *. pose proof (IH _ _ Hr). lia.
Qed.

Lemma run_cleanups_frozen p n cs :
  forall st st',
    run_cleanups p (live_read p) n cs st = Some st' ->
    length (bwrites st') = length (bwrites st) ->
    run_cleanups p (frozen_read p (bsigs st)) n cs st = Some st' /\ bsigs st' = bsigs st.
Proof.
  induction cs as [|[b g] cs IH]; intros st st' Hr Hlen; simpl in *; simplify_eq/=; [done|].
  bind_in Hr. destruct p0 as [o1 st1]; simpl in *.
  pose proof (exec_writes_len _ _ _ _ _ _ _ _ E) as Hl1. simpl in Hl1.
  pose proof (run_cleanups_writes_len _ _ _ _ _ _ Hr) as Hl2.
  destruct (exec_frozen _ _ _ _ _ _ _ E ltac:(simpl; lia)) as [He Hs]. simpl in He, Hs.
  rewrite He. simpl.
  destruct (IH _ _ Hr ltac:(lia)) as [Hr' Hs']. rewrite Hs in Hr'.
  split; [done|congruence].
Qed.

Lemma run_effect_frozen p n body cs sigs bs :
  run_effect p (live_read p) n body cs sigs = Some bs ->
  bwrites bs = [] ->
  run_effect p (frozen_read p sigs) n body cs sigs = Some bs.
Proof.
  unfold run_effect. intros Hr Hw. bind_in Hr. destruct p0 as [o1 st1]; simplify_eq/=.
  pose proof (run_cleanups_writes_len _ _ _ _ _ _ E) as Hl1.
  pose proof (exec_writes_len _ _ _ _ _ _ _ _ E0) as Hl2. simpl in Hl1, Hl2.
  rewrite Hw in Hl2. simpl in Hl2.
  destruct (run_cleanups_frozen _ _ _ _ _ E ltac:(simpl; lia)) as [Hc Hs]. simpl in Hc, Hs.
  rewrite Hc. simpl.
  destruct (exec_frozen _ _ _ _ _ _ _ E0 ltac:(simpl; rewrite Hw; simpl; lia)) as [He _].
  simpl in He. rewrite Hs in He. rewrite He. done.
Qed.

(** The record [r] comes from a run of its effect. *)
Definition run_of (p : rprog) (r : run) : Prop :=
  exists body bs,
    reffects p !! run_node r = Some body /\
    run_effect p (live_read p) (run_node r) body (run_ran_cleanups r) (run_snapshot r) = Some bs /\
    run_reads r = breads bs /\
    run_events r = bevs bs /\
    run_writes r = bwrites bs.

Lemma run_round_runs p ns :
  forall st st' rs,
    run_round p ns st = Some (st', rs) ->
    Forall (run_of p) rs.
Proof.
  induction ns as [|n ns IH]; intros st st' rs Hr; simpl in Hr; [by simplify_eq|].
  bind_in Hr. destruct p0 as [st1 rs1]; simplify_eq/=.
  constructor; [|by eapply IH].
  exists c, b. done.
Qed.

Lemma settle_runs p fuel order :
  forall st st' rs,
    settle fuel order p st = Some (st', rs) ->
    Forall (run_of p) rs.
Proof.
  induction fuel as [|fuel IH]; intros st st' rs Hs; simpl in Hs; bind_in Hs;
    destruct l as [|m ns]; simplify_eq/=; try done.
  bind_in Hs. destruct p0 as [st1 r1]; simpl in Hs. bind_in Hs.
  destruct p0 as [st2 r2]; simplify_eq/=.
  apply Forall_app. split; [by eapply run_round_runs|by eapply IH].
Qed.

Lemma run_history_runs p fuel order h :
  forall st st' rs,
    run_history fuel order p h st = Some (st', rs) ->
    Forall (run_of p) rs.
Proof.
  induction h as [|b h IH]; intros st st' rs Hr; simpl in Hr; [by simplify_eq|].
  bind_in Hr. destruct p0 as [st1 r1]; simpl in Hr. bind_in Hr.
  destruct p0 as [st2 r2]; simplify_eq/=.
  apply Forall_app. split; [by eapply settle_runs|by eapply IH].
Qed.

Lemma rruns_runs fuel order p h rs :
  rruns fuel order p h = Some rs ->
  Forall (run_of p) rs.
Proof.
  intros Hr. unfold rruns in Hr. bind_in Hr.
  destruct p0 as [st1 r1]; simpl in Hr. bind_in Hr.
  destruct p0 as [st2 r2]; simpl in Hr. bind_in Hr.
  destruct p0 as [st3 r3]; simplify_eq/=.
  apply Forall_app. split; [by eapply run_round_runs|].
  apply Forall_app. split; [by eapply settle_runs|by eapply run_history_runs].
Qed.

Lemma run_of_snapshot p r :
  run_of p r -> run_writes r = [] -> computed_from_snapshot p r.
Proof.
  intros (body & bs & Hb & Hr & Hreads & Hev & Hw) Hnone.
  exists body, bs. split_and!; try done.
  apply run_effect_frozen; [done|congruence].
Qed.

Lemma exec_log_tag p rd n c :
  forall g st o st',
    exec p rd n g c st = Some (o, st') ->
    (forall m v, EvLog m v ∈ bevs st -> m = n) ->
    forall m v, EvLog m v ∈ bevs st' -> m = n.
Proof.
  induction c; intros g st o' st' Hex Htag; simpl in Hex; bind_in Hex;
    repeat case_match; simplify_eq/=; try done;
    try (eapply IHc; [exact Hex|]; done).
  all: try (destruct p0; simplify_eq/=).
  all: try (eapply IHc2; [exact Hex|]; eapply IHc1; [exact E|done]).
  all: try (eapply IHc1; [exact E|done]).
  all: try (eapply IHc1; [exact Hex|done]).
  all: try (eapply IHc2; [exact Hex|done]).
  eapply IHc; [exact Hex|]. intros m v'. simpl. rewrite elem_of_app, list_elem_of_singleton.
  intros [?|Heq]; [by eapply Htag|by simplify_eq].
Qed.

Lemma run_cleanups_log_tag p rd n cs :
  forall st st',
    run_cleanups p rd n cs st = Some st' ->
    (forall m v, EvLog m v ∈ bevs st -> m = n) ->
    forall m v, EvLog m v ∈ bevs st' -> m = n.
Proof.
  induction cs as [|[b g] cs IH]; intros st st' Hr Htag; simpl in Hr; simplify_eq/=; [done|].
  bind_in Hr. destruct p0 as [o1 st1]; simpl in *.
  eapply IH; [exact Hr|]. eapply exec_log_tag; [exact E|].
  intros m v. simpl. rewrite elem_of_app, list_elem_of_singleton.
  intros [?|Heq]; [by eapply Htag|discriminate].
Qed.

Lemma run_of_log_tag p r m v :
  run_of p r -> EvLog m v ∈ run_events r -> m = run_node r.
Proof.
  intros (body & bs & _ & Hr & _ & -> & _). unfold run_effect in Hr. bind_in Hr.
  destruct p0 as [o1 st1]; simplify_eq/=.
  eapply exec_log_tag; [exact E0|].
  intros m' v'. simpl. rewrite elem_of_app, list_elem_of_singleton.
  intros [Hin|Heq]; [|discriminate].
  eapply run_cleanups_log_tag; [exact E| |exact Hin].
  intros ? ? Hin'. simpl in Hin'. by apply not_elem_of_nil in Hin'.
Qed.

(** Each [EvLog] of a trace belongs to one effect run. A run that writes no signal has its reads and
    events computed from one snapshot of the signals. *)
Theorem glitch_free_by_definition fuel order p h evs n v :
  rtrace fuel order p h = Some evs ->
  EvLog n v ∈ evs ->
  exists rs r,
    rruns fuel order p h = Some rs /\
    evs = concat (run_events <$> rs) /\
    r ∈ rs /\
    run_node r = n /\
    EvLog n v ∈ run_events r /\
    (run_writes r = [] -> computed_from_snapshot p r).
Proof.
  intros Htr Hin. unfold rtrace in Htr.
  destruct (rruns fuel order p h) as [rs|] eqn:Er; simpl in Htr; [|discriminate]. simplify_eq.
  apply list_elem_of_In, in_concat in Hin as (evs & Hevs & Hin).
  apply list_elem_of_In in Hevs, Hin.
  apply list_elem_of_fmap in Hevs as (r & -> & Hr).
  pose proof (proj1 (Forall_forall _ _) (rruns_runs _ _ _ _ _ Er) r Hr) as Hrun.
  exists rs, r. split_and!; try done.
  - symmetry. by eapply run_of_log_tag.
  - by apply run_of_snapshot.
Qed.

(** * Examples *)

Definition t_id : effect_order := fun l => l.
Definition t_rev : effect_order := fun l => reverse l.

Lemma t_id_order : is_effect_order t_id.
Proof. intros l. done. Qed.
Lemma t_rev_order : is_effect_order t_rev.
Proof. intros l. apply reverse_Permutation. Qed.

Local Notation ret := (CRet (EConst 0)).
Local Notation s0 := (EConst 0).
Local Notation s1 := (EConst 1).

#[local] Arguments map_Forall : simpl never.
#[local] Arguments EvRun _%_nat.
#[local] Arguments EvLog _%_nat _.
#[local] Arguments EvCleanup _%_nat.
#[local] Arguments memo_value _ _ _%_nat.
#[local] Arguments memo_deps _ _ _%_nat.
#[local] Arguments redundant _ _ _%_nat _%_nat.

#[local] Ltac solve_rank :=
  let rd := fresh "rd" in
  let Hex := fresh "Hex" in
  let Hin := fresh "Hin" in
  intros rd ? ? ? ? Hex Hin _;
  vm_compute in Hex;
  repeat match type of Hex with
  | context [rd ?k] => destruct (rd k) as [[?|?]|]; vm_compute in Hex
  end;
  simplify_eq;
  apply list_elem_of_In in Hin; simpl in Hin; intuition (simplify_eq; simpl; lia).

#[local] Ltac solve_forall tac :=
  repeat (apply map_Forall_insert_2; [cbv beta; tac|]);
  apply map_Forall_empty.

#[local] Ltac solve_rwf :=
  refine (conj _ (conj _ (conj _ (conj _ (conj _ _)))));
  [ simpl; rewrite ?dom_insert_L, ?dom_singleton_L; set_solver
  | simpl; rewrite ?dom_insert_L, ?dom_singleton_L; set_solver
  | simpl; rewrite ?dom_insert_L, ?dom_singleton_L; set_solver
  | simpl; solve_forall ltac:(reflexivity)
  | simpl; solve_forall ltac:(reflexivity)
  | exists (fun n => n); simpl; solve_forall ltac:(solve_rank) ].

(** A diamond: one signal, two memos over it, a memo over both, an effect over that. *)
Definition diamond : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := {[ 1%nat := CRead 0 s0 (CRet (EAdd (EVar 0) (EConst 1)));
               2%nat := CRead 0 s0 (CRet (EMul (EVar 0) (EConst 2)));
               3%nat := CRead 0 s1 (CRead 1 (EConst 2) (CRet (EAdd (EVar 0) (EVar 1)))) ]};
  reffects := {[ 9%nat := CRead 0 (EConst 3) (CLog (EVar 0) ret) ]};
|}.

Lemma diamond_rwf : rwf diamond.
Proof. solve_rwf. Qed.

Example diamond_one_run_per_batch :
  rtrace 5 t_id diamond [[(0%nat, 1)]; [(0%nat, 2)]] =
    Some [EvRun 9; EvLog 9 1; EvRun 9; EvLog 9 4; EvRun 9; EvLog 9 7].
Proof. vm_compute. reflexivity. Qed.

Example diamond_any_order :
  rtrace 5 t_rev diamond [[(0%nat, 1)]] = rtrace 5 t_id diamond [[(0%nat, 1)]].
Proof. vm_compute. reflexivity. Qed.

Example diamond_value : memo_value diamond {[ 0%nat := 1 ]} 3 = Some (Ok 4).
Proof. vm_compute. reflexivity. Qed.

(** A memo's logs are discarded, and an effect's are events. *)
Definition logging_memo : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := {[ 1%nat := CRead 0 s0 (CLog (EConst 100) (CRet (EVar 0))) ]};
  reffects := {[ 9%nat := CRead 0 s1 (CLog (EVar 0) ret) ]};
|}.

Lemma logging_memo_rwf : rwf logging_memo.
Proof. solve_rwf. Qed.

Example memo_logs_discarded :
  rtrace 5 t_id logging_memo [] = Some [EvRun 9; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

(** A memo of nesting depth [size (rmemos p)] has a value. *)
Definition chain : rprog := {|
  rsignals := {[ 0%nat := 5 ]};
  rmemos := {[ 1%nat := CRead 0 s0 (CRet (EVar 0));
               2%nat := CRead 0 s1 (CRet (EVar 0));
               3%nat := CRead 0 (EConst 2) (CRet (EVar 0)) ]};
  reffects := ∅;
|}.

Example chain_value : memo_value chain (rsignals chain) 3 = Some (Ok 5).
Proof. vm_compute. reflexivity. Qed.

(** A cutoff: a memo over a moving signal, with an unmoved value. *)
Definition cutoff : rprog := {|
  rsignals := {[ 0%nat := 1 ]};
  rmemos := {[ 1%nat := CRead 0 s0 (CRet (ELt (EVar 0) (EConst 10))) ]};
  reffects := {[ 9%nat := CRead 0 s1 (CLog (EVar 0) ret) ]};
|}.

Lemma cutoff_rwf : rwf cutoff.
Proof. solve_rwf. Qed.

Example cutoff_no_wake :
  rtrace 5 t_id cutoff [[(0%nat, 2)]] = Some [EvRun 9; EvLog 9 1].
Proof. vm_compute. reflexivity. Qed.

Example cutoff_moved_memo_wakes :
  rtrace 5 t_id cutoff [[(0%nat, 20)]] = Some [EvRun 9; EvLog 9 1; EvRun 9; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

(** A batch that restores a signal's value. *)
Definition one_read : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat := CRead 0 s0 (CLog (EVar 0) ret) ]};
|}.

Example batch_restored_no_run :
  rtrace 5 t_id one_read [[(0%nat, 1); (0%nat, 0)]] = Some [EvRun 9; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

Example batch_moved_runs :
  rtrace 5 t_id one_read [[(0%nat, 0); (0%nat, 1)]] =
    Some [EvRun 9; EvLog 9 0; EvRun 9; EvLog 9 1].
Proof. vm_compute. reflexivity. Qed.

Example write_to_absent_signal_is_stuck :
  rtrace 5 t_id one_read [[(3%nat, 1)]] = None.
Proof. vm_compute. reflexivity. Qed.

(** Cleanups run before the body, newest first, with the environment of their registration. *)
Definition cleanups : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat :=
    CRead 0 s0 (CCleanup (CLog (EVar 0) ret) (CCleanup (CLog (EConst 100) ret) ret)) ]};
|}.

Example cleanups_first :
  rtrace 5 t_id cleanups [[(0%nat, 1)]] =
    Some [EvRun 9; EvCleanup 9; EvLog 9 100; EvCleanup 9; EvLog 9 0; EvRun 9].
Proof. vm_compute. reflexivity. Qed.

(** A failed body keeps its reads and leaves later effects running. *)
Definition failing : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 8%nat := CRead 0 s0 (CThrow 7);
                 9%nat := CRead 0 s0 (CLog (EVar 0) ret) ]};
|}.

Example failed_effect_later_effects_run :
  rtrace 5 t_id failing [[(0%nat, 1)]] =
    Some [EvRun 8; EvRun 9; EvLog 9 0; EvRun 8; EvRun 9; EvLog 9 1].
Proof. vm_compute. reflexivity. Qed.

(** A failure is a value: an unmoved failure wakes nothing, and [CTryRead] observes it. *)
Definition failing_memo : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := {[ 1%nat :=
    CRead 0 s0 (CIf (ELt (EVar 0) (EConst 10)) (CThrow 7) (CThrow 8)) ]};
  reffects := {[ 9%nat := CTryRead 0 1 s1 (CLog (EVar 0) (CLog (EVar 1) ret)) ]};
|}.

Example failure_unmoved_no_wake :
  rtrace 5 t_id failing_memo [[(0%nat, 1)]] = Some [EvRun 9; EvLog 9 7; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

Example failure_moved_wakes :
  rtrace 5 t_id failing_memo [[(0%nat, 10)]] =
    Some [EvRun 9; EvLog 9 7; EvLog 9 0; EvRun 9; EvLog 9 8; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

Definition untracked : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat := CReadU 0 s0 (CLog (EVar 0) ret) ]};
|}.

Example untracked_read_no_wake :
  rtrace 5 t_id untracked [[(0%nat, 1)]] = Some [EvRun 9; EvLog 9 0].
Proof. vm_compute. reflexivity. Qed.

(** Effect 8 copies signal 0 to signal 1. Effect 9 reads both. *)
Definition writer : rprog := {|
  rsignals := {[ 0%nat := 0; 1%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 8%nat := CRead 0 s0 (CWrite s1 (EVar 0) ret);
                 9%nat := CRead 0 s0 (CRead 1 s1 (CLog (EAdd (EVar 0) (EVar 1)) ret)) ]};
|}.

Lemma writer_rwf : rwf writer.
Proof.
  refine (conj _ (conj _ (conj _ (conj _ (conj _ _)))));
    [ simpl; rewrite ?dom_empty_L; set_solver
    | simpl; rewrite ?dom_insert_L, ?dom_singleton_L; set_solver
    | simpl; rewrite ?dom_empty_L; set_solver
    | simpl; apply map_Forall_empty
    | simpl; solve_forall ltac:(reflexivity)
    | exists (fun n => n); simpl; apply map_Forall_empty ].
Qed.

(** A later effect of a round reads the writes of an earlier one, and has nothing left to do. *)
Example write_read_later_in_round :
  rtrace 5 t_id writer [[(0%nat, 1)]] =
    Some [EvRun 8; EvRun 9; EvLog 9 0; EvRun 8; EvRun 9; EvLog 9 2].
Proof. vm_compute. reflexivity. Qed.

(** An effect that ran before the write runs again in the next round. *)
Example write_wakes_next_round :
  rtrace 5 t_rev writer [[(0%nat, 1)]] =
    Some [EvRun 9; EvLog 9 0; EvRun 8; EvRun 9; EvLog 9 1; EvRun 8; EvRun 9; EvLog 9 2].
Proof. vm_compute. reflexivity. Qed.

Definition climbing : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat :=
    CRead 0 s0 (CIf (ELt (EVar 0) (EConst 2)) (CWrite s0 (EAdd (EVar 0) (EConst 1)) ret) ret) ]};
|}.

Example self_write_settles :
  rtrace 5 t_id climbing [] = Some [EvRun 9; EvRun 9; EvRun 9].
Proof. vm_compute. reflexivity. Qed.

Definition diverging : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat := CRead 0 s0 (CWrite s0 (EAdd (EVar 0) (EConst 1)) ret) ]};
|}.

Example self_write_exhausts_fuel : rtrace 50 t_id diverging [] = None.
Proof. vm_compute. reflexivity. Qed.

(** The [run_writes r = []] premise of [glitch_free_by_definition] is needed: a run that writes
    between two reads logs values from two snapshots. *)
Definition two_snapshots : rprog := {|
  rsignals := {[ 0%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 9%nat :=
    CRead 0 s0 (CLog (EVar 0) (CWrite s0 (EConst 5) (CRead 1 s0 (CLog (EVar 1) ret)))) ]};
|}.

Example write_between_reads_live :
  rtrace 5 t_id two_snapshots [] =
    Some [EvRun 9; EvLog 9 0; EvLog 9 5; EvRun 9; EvLog 9 5; EvLog 9 5].
Proof. vm_compute. reflexivity. Qed.

Lemma write_between_reads_not_from_snapshot :
  exists rs r, rruns 5 t_id two_snapshots [] = Some rs /\ r ∈ rs /\
    ~ computed_from_snapshot two_snapshots r.
Proof.
  eexists _, _. split; [vm_compute; reflexivity|]. split; [left|].
  intros (body & bs & Hb & Hr & _ & Hev).
  vm_compute in Hb. injection Hb as <-. vm_compute in Hr. injection Hr as <-.
  vm_compute in Hev. discriminate Hev.
Qed.

(** The dependencies of a memo are those of its branch taken. *)
Definition branching : rprog := {|
  rsignals := {[ 0%nat := 0; 1%nat := 10; 2%nat := 20 ]};
  rmemos := {[ 3%nat :=
    CRead 0 s0 (CIf (EVar 0) (CRead 1 s1 (CRet (EVar 1))) (CRead 1 (EConst 2) (CRet (EVar 1)))) ]};
  reffects := ∅;
|}.

Example branching_deps : memo_deps branching (rsignals branching) 3 = [0%nat; 2%nat].
Proof. vm_compute. reflexivity. Qed.

Example branching_ignores_other_branch :
  memo_value branching (<[1%nat := 99]> (rsignals branching)) 3 = Some (Ok 20).
Proof. vm_compute. reflexivity. Qed.

(** A dependency cycle is outside the fragment, and has no value. *)
Definition cyclic : rprog := {|
  rsignals := ∅;
  rmemos := {[ 1%nat := CRead 0 (EConst 2) ret; 2%nat := CRead 0 s1 ret ]};
  reffects := ∅;
|}.

Lemma cyclic_not_rwf : ~ rwf cyclic.
Proof.
  intros (_ & _ & _ & _ & _ & rank & Hr).
  assert (rmemos cyclic !! 1%nat = Some (CRead 0 (EConst 2) ret)) as H1 by (vm_compute; reflexivity).
  assert (rmemos cyclic !! 2%nat = Some (CRead 0 s1 ret)) as H2 by (vm_compute; reflexivity).
  assert (rank 2%nat < rank 1%nat)%nat as Hlt1.
  { eapply (Hr _ _ H1 (fun _ => Some (Ok 0)));
      [vm_compute; reflexivity | simpl; left | vm_compute; eauto]. }
  assert (rank 1%nat < rank 2%nat)%nat as Hlt2.
  { eapply (Hr _ _ H2 (fun _ => Some (Ok 0)));
      [vm_compute; reflexivity | simpl; left | vm_compute; eauto]. }
  lia.
Qed.

Example cyclic_no_value : memo_value cyclic ∅ 1 = None.
Proof. vm_compute. reflexivity. Qed.

Example wbe_accepts :
  well_behaved_effect
    (CLet 0 (EConst 1) (CIf (EVar 0)
      (CRead 1 s0 (CTryRead 2 3 s0 (CReadU 4 s0 (CWrite s0 (EVar 1)
        (CCleanup ret (CBatch ret (CLog (EVar 1) ret)))))))
      (CThrow 1))) = true.
Proof. reflexivity. Qed.
Example wbe_rejects_signal : well_behaved_effect (CSignal 0 (EConst 1) ret) = false.
Proof. reflexivity. Qed.
Example wbe_rejects_memo : well_behaved_effect (CMemo 0 Owning CmpEq ret ret) = false.
Proof. reflexivity. Qed.
Example wbe_rejects_effect : well_behaved_effect (CEffect 0 ret ret) = false.
Proof. reflexivity. Qed.
Example wbe_rejects_dispose : well_behaved_effect (CDispose s0 ret) = false.
Proof. reflexivity. Qed.
Example wbe_rejects_in_cleanup : well_behaved_effect (CCleanup (CDispose s0 ret) ret) = false.
Proof. reflexivity. Qed.
Example wbe_rejects_prev : well_behaved_effect (CRet EPrev) = false.
Proof. reflexivity. Qed.

(** ** Redundant runs *)

Definition diamond_runs := default [] (rruns 5 t_id diamond [[(0%nat, 1)]]).

Lemma diamond_run_not_redundant : ~ redundant diamond diamond_runs 9 1.
Proof.
  intros (_ & j & prev & cur & Hk & Hp & Hc & Hsub). simplify_eq.
  vm_compute in Hp, Hc. simplify_eq.
  specialize (Hsub 3%nat (Ok 4)). simpl in Hsub.
  assert ((3%nat, Ok 4) ∈ [(3%nat, Ok 1)]) as Hin by (apply Hsub; left).
  apply list_elem_of_singleton in Hin. discriminate Hin.
Qed.

Lemma initial_run_not_redundant : ~ redundant diamond diamond_runs 9 0.
Proof. intros (_ & j & prev & cur & Hk & _). discriminate Hk. Qed.

(** Effect 8 reads signal 0. Effect 9 reads signal 1 and resets signal 0. *)
Definition resetting : rprog := {|
  rsignals := {[ 0%nat := 0; 1%nat := 0 ]};
  rmemos := ∅;
  reffects := {[ 8%nat := CRead 0 s0 (CLog (EVar 0) ret);
                 9%nat := CRead 0 s1 (CWrite s0 (EConst 0) ret) ]};
|}.

Definition resetting_runs := default [] (rruns 5 t_rev resetting [[(0%nat, 1); (1%nat, 1)]]).

Example resetting_trace :
  rtrace 5 t_rev resetting [[(0%nat, 1); (1%nat, 1)]] =
    Some [EvRun 9; EvRun 8; EvLog 8 0; EvRun 9; EvRun 8; EvLog 8 0].
Proof. vm_compute. reflexivity. Qed.

(** The reference tier itself has a redundant run. Effect 8 is in the round's run set from the
    round's snapshot, and an earlier effect of the round restores the value effect 8 read. *)
Lemma reference_run_redundant : redundant resetting resetting_runs 8 1.
Proof.
  split; [vm_compute; eauto|].
  eexists 0%nat, _, _. split; [reflexivity|].
  split; [vm_compute; reflexivity|]. split; [vm_compute; reflexivity|].
  intros t o Hin. exact Hin.
Qed.
