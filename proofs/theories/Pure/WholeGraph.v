From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler.

Open Scope nat_scope.

Local Arguments slot_view : simpl never.
Local Arguments read_prefix : simpl never.

(** ** What a body computes *)

(** What a read of a node with this view answers. *)
Definition view_answer (v : read_view) : outcome :=
  match v with
  | (x, st, fl) => match st with StError => Fail (default 0%Z fl) | _ => Ok x end
  end.

(** An expression independent of the previous value. *)
Fixpoint expr_plain (x : expr) : bool :=
  match x with
  | EConst _ => true
  | EVar y => negb (Nat.eqb y prev_var)
  | EPrev => false
  | EAdd a b | ESub a b | EMul a b | EDiv a b | ELt a b | EEq a b => expr_plain a && expr_plain b
  end.

Definition ev (e : env) (x : expr) : option outcome := eval_expr e None x.

(** The result of a body whose reads are answered by [ans]. [None] is a stuck body. *)
Fixpoint body_eval (ans : node_id -> option outcome) (e : env) (c : cmd) : option outcome :=
  match c with
  | CRet x => ev e x
  | CThrow y => Some (Fail y)
  | CLet x v k =>
    match ev e v with Some (Ok z) => body_eval ans ((x, z) :: e) k | r => r end
  | CIf v a b =>
    match ev e v with
    | Some (Ok z) => if decide (z = 0%Z) then body_eval ans e b else body_eval ans e a
    | r => r
    end
  | CRead x n k =>
    match ev e n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match ans (Z.to_nat z) with
        | Some (Ok v) => body_eval ans ((x, v) :: e) k
        | r => r
        end
      else None
    | r => r
    end
  | CTryRead x ok n k =>
    match ev e n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match ans (Z.to_nat z) with
        | Some (Ok v) => body_eval ans ((ok, 1%Z) :: (x, v) :: e) k
        | Some (Fail y) => body_eval ans ((ok, 0%Z) :: (x, y) :: e) k
        | None => None
        end
      else None
    | r => r
    end
  | CLog v k =>
    match ev e v with Some (Ok _) => body_eval ans e k | r => r end
  | CBatch b k =>
    match body_eval ans e b with Some (Ok _) => body_eval ans e k | r => r end
  | _ => None
  end.

(** A body built from returns, failures, lets, branches, tracked reads, batches and logs, independent of the previous
    value, whose reads satisfy [Q] for every answer the earlier reads can give. *)
Fixpoint body_pure (Q : node_id -> Prop) (e : env) (c : cmd) : Prop :=
  match c with
  | CRet x => expr_plain x = true
  | CThrow _ => True
  | CLet x v k => expr_plain v = true /\ forall z, ev e v = Some (Ok z) -> body_pure Q ((x, z) :: e) k
  | CIf v a b =>
    expr_plain v = true /\
    forall z, ev e v = Some (Ok z) -> (z = 0%Z -> body_pure Q e b) /\ (z <> 0%Z -> body_pure Q e a)
  | CRead x n k =>
    expr_plain n = true /\ (forall z, ev e n = Some (Ok z) -> (0 <= z)%Z -> Q (Z.to_nat z)) /\
    forall v, body_pure Q ((x, v) :: e) k
  | CTryRead x ok n k =>
    expr_plain n = true /\ (forall z, ev e n = Some (Ok z) -> (0 <= z)%Z -> Q (Z.to_nat z)) /\
    (forall v, body_pure Q ((ok, 1%Z) :: (x, v) :: e) k) /\ (forall y, body_pure Q ((ok, 0%Z) :: (x, y) :: e) k)
  | CLog v k => expr_plain v = true /\ body_pure Q e k
  | CBatch b k => body_pure Q e b /\ body_pure Q e k
  | _ => False
  end.

(** The logs of a body whose reads are answered by [ans], up to its result. [None] is a stuck body. *)
Fixpoint body_logs (ans : node_id -> option outcome) (e : env) (c : cmd) : option (list val) :=
  match c with
  | CRet x => match ev e x with Some _ => Some [] | None => None end
  | CThrow y => Some []
  | CLet x v k =>
    match ev e v with Some (Ok z) => body_logs ans ((x, z) :: e) k | Some (Fail _) => Some [] | None => None end
  | CIf v a b =>
    match ev e v with
    | Some (Ok z) => if decide (z = 0%Z) then body_logs ans e b else body_logs ans e a
    | Some (Fail _) => Some []
    | None => None
    end
  | CRead x n k =>
    match ev e n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match ans (Z.to_nat z) with
        | Some (Ok v) => body_logs ans ((x, v) :: e) k
        | Some (Fail _) => Some []
        | None => None
        end
      else None
    | Some (Fail _) => Some []
    | None => None
    end
  | CTryRead x ok n k =>
    match ev e n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match ans (Z.to_nat z) with
        | Some (Ok v) => body_logs ans ((ok, 1%Z) :: (x, v) :: e) k
        | Some (Fail y) => body_logs ans ((ok, 0%Z) :: (x, y) :: e) k
        | None => None
        end
      else None
    | Some (Fail _) => Some []
    | None => None
    end
  | CLog v k =>
    match ev e v with
    | Some (Ok z) => cons z <$> body_logs ans e k
    | Some (Fail _) => Some []
    | None => None
    end
  | CBatch b k =>
    match body_eval ans e b, body_logs ans e b with
    | Some (Ok _), Some l => app l <$> body_logs ans e k
    | Some (Fail _), Some l => Some l
    | _, _ => None
    end
  | _ => None
  end.

(** The node fields constant across runs. *)
Definition shape_of (d : node) : kind * comparer * env * cmd := (n_kind d, n_comparer d, n_env d, n_body d).

Definition shapes (s : state) : gmap node_id (kind * comparer * env * cmd) := shape_of <$> g_nodes s.

(** A signal or a memo: a read of anything else has no result. *)
Definition readable (S : gmap node_id (kind * comparer * env * cmd)) (t : node_id) : Prop :=
  match S !! t with
  | Some (KSignal, _, _, _) | Some (KMemo, _, _, _) => True
  | _ => False
  end.

(** [k] bounds the depth of reads. A node is pure when it is a signal, or a memo with [CmpEq] and a pure body that reads,
    for every answer of its earlier reads, only nodes created earlier and pure. *)
Fixpoint pure_at (S : gmap node_id (kind * comparer * env * cmd)) (k : nat) (n : node_id) : Prop :=
  match k with
  | 0 => False
  | Datatypes.S k' =>
    match S !! n with
    | Some (KSignal, _, _, _) => True
    | Some (KMemo, CmpEq, e, b) => body_pure (fun t => readable S t -> t < n /\ pure_at S k' t) e b
    | _ => False
    end
  end.

Definition pure_in (S : gmap node_id (kind * comparer * env * cmd)) (n : node_id) : Prop := pure_at S (Datatypes.S n) n.

(** [n] is a signal, or a memo with [CmpEq] and a pure body that reads, for every answer of its earlier reads, only nodes
    of the same kind, created earlier. A cycle, a self-read, a write, a cleanup, an untracked read, a node created in the
    body, the previous value, a read that can reach an impure or later node, or a comparer other than [CmpEq] anywhere [n]
    can read makes it self-dirtying. A memo returning a fresh reference value under the default policy is [CmpNever], so
    it is self-dirtying. An effect is self-dirtying: a read of an effect is stuck. *)
Definition self_dirtying (s : state) (n : node_id) : Prop := ~ pure_in (shapes s) n.

(** Every memo of the graph is outside the self-dirtying case. A default memo returning a record is not well behaved: it is a
    [CmpNever] memo, as [CORRESPONDENCE.md] records.

    Exclusions resting on a counterexample, with the entry of [BUGS.md]: a write or cleanup in a memo's body:
    [self_marking_wake_lost], [walk_passed_source_marked], [check_resolves_over_moved_source] and
    [nested_self_recompute_resets_cursor]; a read reaching itself: [self_read_check_overflow]; a [CmpNever] comparer:
    [cmp_never_passes_source].

    Exclusions resting on the proof's reach, each with no counterexample known: a [CmpThrows] comparer, an untracked read or
    a node created in a memo's body, and a read of a later node that closes no cycle. *)
Definition well_behaved_program (s : state) : Prop :=
  forall n d, g_nodes s !! n = Some d -> n_kind d = KMemo -> pure_in (shapes s) n.

(** The body of an effect built from returns, failures, lets, branches, tracked reads, batches and logs, whose reads reach
    only nodes outside the self-dirtying case. The targets can be computed. *)
Definition quiet_effect (s : state) (c : node_id) (d : node) : Prop :=
  n_kind d = KEffect /\ body_pure (fun t => readable (shapes s) t -> pure_in (shapes s) t) (n_env d) (n_body d).

(** Named premise of [convergence]. The exclusions from an effect's body:

    Resting on a counterexample: a write, by [effect_resolves_over_moved_source], pending the owner's ruling.

    An untracked read: the logs clause of [converged] is false over it, since the source can move with no run, and what it
    is entitled to is unstated. Unproved in a weaker form, with no counterexample.

    Resting on the proof's reach, unproved with no counterexample: a cleanup, and an effect or other node created in the
    body. *)
Definition effects_only_read (s : state) : Prop :=
  forall c d, g_nodes s !! c = Some d -> n_kind d = KEffect -> quiet_effect s c d.

Definition quiescent (s : state) : Prop := pending s = [] /\ g_running s = [].

(** What a node answers from the current signals: a signal its value, a live memo its body, with the fuel as a bound
    on the depth of reads, and a disposed memo what it stored, as Core.fs 4372-4376 documents. *)
Fixpoint den (k : nat) (s : state) (n : node_id) : option outcome :=
  match k with
  | 0 => None
  | S k' =>
    match g_nodes s !! n with
    | None => None
    | Some d =>
      match n_kind d with
      | KSignal => Some (view_answer (read_view_of d))
      | KEffect => None
      | KMemo =>
        if n_disposed d then Some (view_answer (read_view_of d))
        else body_eval (den k' s) (n_env d) (n_body d)
      end
    end
  end.

Definition denotes (s : state) (n : node_id) (o : outcome) : Prop := exists k, den k s n = Some o.

(** The latest [EvLog] of [n]. *)
Definition last_log (n : node_id) (tr : list event) : option val :=
  foldl (fun acc ev => match ev with EvLog m v => if decide (m = n) then Some v else acc | _ => acc end) None tr.

(** An effect's latest log is what a source it observes computes from the current signals. It is existential, naming one
    effect and one source, so it fits a witness with a single source. [converged] is the statement over every effect. *)
Definition effect_ran_for_last_write (s : state) : Prop :=
  exists c d t v, g_nodes s !! c = Some d /\ n_kind d = KEffect /\
    t ∈ (slot_view (g_edges s) c).1 /\ denotes s t (Ok v) /\ last_log c (g_trace s) = Some v.

(** A node's runs and logs in a trace. *)
Definition view_ev (c : node_id) (ev : event) : bool :=
  match ev with EvRun m | EvLog m _ => bool_decide (m = c) | _ => false end.

Definition trace_view (c : node_id) (tr : list event) : list event :=
  filter (fun ev => view_ev c ev = true) tr.

(** The logs of [c] since its latest [EvRun]. *)
Definition last_run_logs (c : node_id) (tr : list event) : list val :=
  foldl (fun acc ev => match ev with
    | EvRun m => if decide (m = c) then [] else acc
    | EvLog m v => if decide (m = c) then acc ++ [v] else acc
    | _ => acc end) [] tr.

(** ** Bodies *)

Lemma trace_view_app c l l' : trace_view c (l ++ l') = trace_view c l ++ trace_view c l'.
Proof. apply filter_app. Qed.

Lemma trace_view_log c l z : trace_view c (l ++ [EvLog c z]) = trace_view c l ++ [EvLog c z].
Proof.
  rewrite trace_view_app. unfold trace_view at 2. rewrite filter_cons_True, filter_nil; [done|].
  simpl. by apply bool_decide_eq_true_2.
Qed.

Lemma trace_view_other c l e :
  match e with EvRun m | EvLog m _ => m <> c | _ => True end -> trace_view c (l ++ [e]) = trace_view c l.
Proof.
  intros H. rewrite trace_view_app. unfold trace_view at 2. rewrite filter_cons_False, filter_nil, app_nil_r; [done|].
  destruct e; simpl in *; try done; rewrite bool_decide_eq_false_2 by done; done.
Qed.

Lemma last_run_logs_foldl c acc l :
  foldl (fun acc ev => match ev with
    | EvRun m => if decide (m = c) then [] else acc
    | EvLog m v => if decide (m = c) then acc ++ [v] else acc
    | _ => acc end) acc (trace_view c l) =
  foldl (fun acc ev => match ev with
    | EvRun m => if decide (m = c) then [] else acc
    | EvLog m v => if decide (m = c) then acc ++ [v] else acc
    | _ => acc end) acc l.
Proof.
  revert acc. induction l as [|x l IH]; intros acc; [done|]. unfold trace_view in *. rewrite filter_cons.
  destruct (decide _) as [H|H]; simpl; [apply IH|].
  destruct x; simpl in *; try apply IH; rewrite decide_False; try apply IH; intros K; apply H; by apply bool_decide_eq_true_2.
Qed.

(** What a run's start and its logs make of a view. *)
Lemma last_run_logs_view c pre l tr :
  trace_view c tr = pre ++ EvRun c :: (EvLog c <$> l) -> last_run_logs c tr = l.
Proof.
  intros H. unfold last_run_logs. rewrite <- last_run_logs_foldl, H, foldl_app. simpl. rewrite decide_True by done.
  set (ff := fun (acc : list val) (ev : event) => match ev with
    | EvRun m => if decide (m = c) then [] else acc
    | EvLog m v => if decide (m = c) then acc ++ [v] else acc
    | _ => acc end).
  assert (K : forall acc l', foldl ff acc (EvLog c <$> l') = acc ++ l').
  { intros acc l'. revert acc. induction l' as [|z l' IH]; intros acc; simpl; [by rewrite app_nil_r|].
    rewrite IH. subst ff. simpl. rewrite decide_True by done. by rewrite <- app_assoc. }
  apply K.
Qed.

Lemma body_eval_mono (A B : node_id -> option outcome) e c r :
  (forall t o, A t = Some o -> B t = Some o) -> body_eval A e c = Some r -> body_eval B e c = Some r.
Proof.
  intros H. revert e r. induction c; intros g r0; simpl; try done.
  - destruct (ev g e) as [[z|y]|]; [apply IHc|done|done].
  - destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (H _ _ E); apply IHc|intros [=<-]; by rewrite (H _ _ E)|done].
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (H _ _ E); apply IHc|rewrite (H _ _ E); apply IHc|done].
  - destruct (body_eval A g c1) as [[z|y]|] eqn:E; [rewrite (IHc1 _ _ E); apply IHc2|intros [=<-]; by rewrite (IHc1 _ _ E)|done].
  - destruct (ev g e) as [[z|y]|]; [apply IHc|done|done].
Qed.

Lemma body_logs_mono (A B : node_id -> option outcome) e c r :
  (forall t o, A t = Some o -> B t = Some o) -> body_logs A e c = Some r -> body_logs B e c = Some r.
Proof.
  intros H. revert e r. induction c; intros g r0; simpl; try done.
  - destruct (ev g e) as [[z|y]|]; [apply IHc|done|done].
  - destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (H _ _ E); apply IHc|rewrite (H _ _ E); done|done].
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (H _ _ E); apply IHc|rewrite (H _ _ E); apply IHc|done].
  - destruct (body_eval A g c1) as [[z|y]|] eqn:E1; [|destruct (body_logs A g c1) as [l|] eqn:E2; [|done]|done].
    + destruct (body_logs A g c1) as [l|] eqn:E2; [|done]. rewrite (body_eval_mono A B _ _ _ H E1), (IHc1 _ _ E2).
      destruct (body_logs A g c2) as [l2|] eqn:E3; simpl; [|done]. rewrite (IHc2 _ _ E3). done.
    + rewrite (body_eval_mono A B _ _ _ H E1), (IHc1 _ _ E2). done.
  - destruct (ev g e) as [[z|y]|]; [|done|done].
    destruct (body_logs A g c) as [l|] eqn:E; simpl; [|done]. rewrite (IHc _ _ E). done.
Qed.

(** Answers that agree wherever both exist give one result. *)
Lemma body_eval_agree (A B : node_id -> option outcome) e c r r' :
  (forall t x y, A t = Some x -> B t = Some y -> x = y) ->
  body_eval A e c = Some r -> body_eval B e c = Some r' -> r = r'.
Proof.
  intros H. revert e r r'. induction c; intros g r0 r1; simpl; try congruence.
  - destruct (ev g e) as [[z|y]|]; [apply IHc|congruence|done].
  - destruct (ev g e) as [[z|y]|]; [|congruence|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|congruence|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E1; destruct (B _) as [[w|y']|] eqn:E2; try done;
      pose proof (H _ _ _ E1 E2) as Heq; try discriminate Heq; injection Heq as <-; [apply IHc|congruence].
  - destruct (ev g n) as [[z|y]|]; [|congruence|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|] eqn:E1; destruct (B _) as [[w|y']|] eqn:E2; try done;
      pose proof (H _ _ _ E1 E2) as Heq; try discriminate Heq; injection Heq as <-; apply IHc.
  - destruct (body_eval A g c1) as [x1|] eqn:E1; [|done]. destruct (body_eval B g c1) as [x2|] eqn:E2; [|done].
    pose proof (IHc1 _ _ _ E1 E2) as ->. destruct x2 as [z|y]; [apply IHc2|congruence].
  - destruct (ev g e) as [[z|y]|]; [apply IHc|congruence|done].
Qed.

Lemma body_pure_mono (P Q : node_id -> Prop) e c :
  (forall t, P t -> Q t) -> body_pure P e c -> body_pure Q e c.
Proof.
  intros H. revert e. induction c; intros g; simpl; try done.
  - intros [A B]. split; [done|]. intros z Hz. apply IHc, B, Hz.
  - intros [A B]. split; [done|]. intros z Hz. destruct (B z Hz) as [C D]. split; intros K; [apply IHc2, C, K|apply IHc1, D, K].
  - intros (A&B&C). split_and!; [done|intros z Hz Hp; apply H, B; done|intros v; apply IHc, C].
  - intros (A&B&C&D). split_and!; [done|intros z Hz Hp; apply H, B; done|intros v; apply IHc, C|intros v; apply IHc, D].
  - intros [A B]. split; [apply IHc1, A|apply IHc2, B].
  - intros [A B]. split; [done|]. apply IHc, B.
Qed.

Lemma den_S k s n o : den k s n = Some o -> den (S k) s n = Some o.
Proof.
  revert n o. induction k as [|k IH]; intros n o; [done|]. simpl (den (S k)). intros Hd.
  remember (S k) as k0. simpl. rewrite Heqk0 in *. simpl in Hd.
  destruct (g_nodes s !! n) as [d|]; [|done].
  destruct (n_kind d); try done. destruct (n_disposed d); [done|].
  revert Hd. apply body_eval_mono. intros t x. apply IH.
Qed.

Lemma den_mono k k' s n o : k <= k' -> den k s n = Some o -> den k' s n = Some o.
Proof. induction 1; [done|]. intros Hd. apply den_S. auto. Qed.

Lemma denotes_unique s n o o' : denotes s n o -> denotes s n o' -> o = o'.
Proof.
  intros [k H] [k' H']. pose proof (den_mono k (k `max` k') s n o ltac:(lia) H) as A.
  pose proof (den_mono k' (k `max` k') s n o' ltac:(lia) H') as B. congruence.
Qed.

(** ** The environments of a run *)

Definition env_agree (e1 e2 : env) : Prop :=
  forall y, y <> prev_var -> env_lookup e1 y = env_lookup e2 y.

Lemma env_agree_refl e : env_agree e e.
Proof. intros ??. done. Qed.

Lemma env_agree_cons x v e1 e2 : env_agree e1 e2 -> env_agree ((x, v) :: e1) ((x, v) :: e2).
Proof. intros H y Hy. simpl. destruct (Nat.eqb y x); [done|by apply H]. Qed.

Lemma env_agree_prev v e : env_agree ((prev_var, v) :: e) e.
Proof.
  intros y Hy. simpl. destruct (Nat.eqb y prev_var) eqn:E; [|done]. apply Nat.eqb_eq in E. done.
Qed.

Lemma env_lookup_filter e y :
  y <> prev_var -> env_lookup (without_prev e) y = env_lookup e y.
Proof.
  intros Hy. induction e as [|[x v] e IH]; [done|]. unfold without_prev in *.
  change (filter (fun p : var * val => p.1 <> prev_var) ((x, v) :: e)) with
    (if decide (x <> prev_var) then (x, v) :: filter (fun p : var * val => p.1 <> prev_var) e
     else filter (fun p : var * val => p.1 <> prev_var) e).
  destruct (decide (x <> prev_var)) as [Hx|Hx]; simpl.
  - destruct (Nat.eqb y x); [done|apply IH].
  - apply dec_stable in Hx. subst x. destruct (Nat.eqb y prev_var) eqn:E; [apply Nat.eqb_eq in E; done|apply IH].
Qed.

Lemma env_agree_without e : env_agree (without_prev e) e.
Proof. intros y Hy. by apply env_lookup_filter. Qed.

Lemma env_agree_run d : env_agree (run_env d) (n_env d).
Proof. unfold run_env. destruct (n_published d); [apply env_agree_prev|apply env_agree_without]. Qed.

Lemma eval_agree e1 e2 p1 p2 x :
  env_agree e1 e2 -> expr_plain x = true -> eval_expr e1 p1 x = eval_expr e2 p2 x.
Proof.
  intros H. induction x; simpl; try done; intros Hp.
  { rewrite H; [done|]. apply negb_true_iff, Nat.eqb_neq in Hp. done. }
  all: apply andb_true_iff in Hp as [Ha Hb]; by rewrite (IHx1 Ha), (IHx2 Hb).
Qed.

Lemma ev_of e1 e2 x :
  env_agree e1 e2 -> expr_plain x = true -> eval_expr e1 (env_lookup e1 prev_var) x = ev e2 x.
Proof. intros. by apply eval_agree. Qed.

(** ** Purity *)

Lemma shapes_lookup s n d : g_nodes s !! n = Some d -> shapes s !! n = Some (shape_of d).
Proof. intros H. unfold shapes. by rewrite lookup_fmap, H. Qed.

Lemma readable_sub S S' t : (forall x v, S !! x = Some v -> S' !! x = Some v) -> readable S t -> readable S' t.
Proof.
  intros H. unfold readable. destruct (S !! t) as [v|] eqn:E; [|done]. rewrite (H _ _ E). done.
Qed.

Lemma readable_node s t : readable (shapes s) t -> is_Some (g_nodes s !! t).
Proof.
  unfold readable, shapes. rewrite lookup_fmap. destruct (g_nodes s !! t) as [d|]; [by eexists|done].
Qed.

Lemma pure_at_unfold Sh k (n : node_id) :
  pure_at Sh (Datatypes.S k) n =
    match Sh !! n with
    | Some (KSignal, _, _, _) => True
    | Some (KMemo, CmpEq, e, b) => body_pure (fun t => readable Sh t -> t < n /\ pure_at Sh k t) e b
    | _ => False
    end.
Proof. reflexivity. Qed.

Lemma pure_at_S Sh k n : pure_at Sh k n -> pure_at Sh (Datatypes.S k) n.
Proof.
  revert n. induction k as [|k IH]; intros n; [done|]. intros H.
  rewrite pure_at_unfold in H |- *.
  destruct (Sh !! n) as [[[[[] [| |]] e] b]|]; try done.
  revert H. apply body_pure_mono. intros t Ht Hr. destruct (Ht Hr) as [H1 H2]. split; [done|]. apply IH. exact H2.
Qed.

Lemma pure_at_mono Sh k k' n : k <= k' -> pure_at Sh k n -> pure_at Sh k' n.
Proof. induction 1 as [|k0 Hle IH]; [done|]. intros H0. apply pure_at_S. auto. Qed.

Lemma pure_at_least Sh : forall n k, pure_at Sh k n -> pure_in Sh n.
Proof.
  intros n. induction n as [n IH] using lt_wf_ind. intros k H. destruct k as [|k]; [done|].
  unfold pure_in. rewrite pure_at_unfold in H |- *. destruct (Sh !! (n : node_id)) as [[[[[] [| |]] e] b]|]; try done.
  revert H. apply body_pure_mono. intros t Ht Hr. destruct (Ht Hr) as [Hlt H2]. split; [done|].
  pose proof (IH t Hlt k H2) as Hp. unfold pure_in in Hp. eapply pure_at_mono; [|exact Hp]. lia.
Qed.

Lemma pure_body Sh n k q e b :
  pure_in Sh n -> Sh !! n = Some (k, q, e, b) ->
  k = KSignal \/ (k = KMemo /\ q = CmpEq /\ body_pure (fun t => readable Sh t -> t < n /\ pure_in Sh t) e b).
Proof.
  unfold pure_in. rewrite pure_at_unfold. intros H Hs. rewrite Hs in H. destruct k; [by left|right|done].
  destruct q; try done. split_and!; [done|done|].
  revert H. apply body_pure_mono. intros t Ht Hr. destruct (Ht Hr) as [H1 H2]. split; [done|]. by eapply pure_at_least.
Qed.

Lemma pure_node s n d :
  pure_in (shapes s) n -> g_nodes s !! n = Some d ->
  n_kind d = KSignal \/ (n_kind d = KMemo /\ n_comparer d = CmpEq /\
    body_pure (fun t => readable (shapes s) t -> t < n /\ pure_in (shapes s) t) (n_env d) (n_body d)).
Proof.
  intros Hp Hd. exact (pure_body _ n _ _ _ _ Hp (shapes_lookup s n d Hd)).
Qed.

Lemma pure_exists s n : pure_in (shapes s) n -> is_Some (g_nodes s !! n).
Proof.
  unfold pure_in. rewrite pure_at_unfold. unfold shapes. rewrite lookup_fmap.
  destruct (g_nodes s !! n); [by eexists|done].
Qed.

(** A node pure in a larger graph is pure in the smaller. *)
Lemma pure_at_sub S S' : (forall x v, S !! x = Some v -> S' !! x = Some v) ->
  forall k (t : node_id), is_Some (S !! t) -> pure_at S' k t -> pure_at S k t.
Proof.
  intros Hsub. induction k as [|k IH]; intros t [sh Hs]; [done|]. rewrite !pure_at_unfold. rewrite (Hsub _ _ Hs), Hs.
  destruct sh as [[[[] [| |]] e] b]; try done.
  apply body_pure_mono. intros u Hu Hr. destruct (Hu (readable_sub _ _ _ Hsub Hr)) as [H1 H2]. split; [done|].
  apply IH; [|done]. unfold readable in Hr. destruct (S !! u) as [v|]; [by eexists|done].
Qed.

Lemma pure_in_sub S S' t : (forall x v, S !! x = Some v -> S' !! x = Some v) -> is_Some (S !! t) -> pure_in S' t -> pure_in S t.
Proof. intros H1 H2. apply pure_at_sub; done. Qed.

(** ** A decision for concrete states *)

(** What is known of a body's variables ahead of its run: the captured environment, and [None] for a variable bound to
    an answer. *)
Definition static_env := list (var * option val).

Fixpoint static_lookup (g : static_env) (x : var) : option val :=
  match g with
  | [] => None
  | (y, v) :: g' => if Nat.eqb x y then v else static_lookup g' x
  end.

Definition static_of (e : env) : static_env := (fun p : var * val => (p.1, Some p.2)) <$> e.

(** The values an expression can take, when they can be listed ahead of the run. *)
Fixpoint targets (g : static_env) (n : expr) : option (list val) :=
  match n with
  | EConst z => Some [z]
  | EVar x => match static_lookup g x with Some v => Some [v] | None => None end
  | EAdd a b =>
    match targets g a, targets g b with
    | Some la, Some lb => Some (u ← la; v ← lb; [(u + v)%Z])
    | _, _ => None
    end
  | ELt _ _ | EEq _ _ => Some [0%Z; 1%Z]
  | _ => None
  end.

(** A sufficient test of [body_pure]: each read's target lies in a list known ahead of the run, and [Q] holds of the list. *)
Fixpoint body_ok (Q : node_id -> bool) (g : static_env) (c : cmd) : bool :=
  match c with
  | CRet x => expr_plain x
  | CThrow _ => true
  | CLet x v k => expr_plain v && body_ok Q ((x, None) :: g) k
  | CIf v a b => expr_plain v && body_ok Q g a && body_ok Q g b
  | CRead x n k =>
    expr_plain n &&
    match targets g n with
    | Some l => forallb (fun z => bool_decide (z < 0)%Z || Q (Z.to_nat z)) l && body_ok Q ((x, None) :: g) k
    | None => false
    end
  | CTryRead x ok n k =>
    expr_plain n &&
    match targets g n with
    | Some l => forallb (fun z => bool_decide (z < 0)%Z || Q (Z.to_nat z)) l && body_ok Q ((ok, None) :: (x, None) :: g) k
    | None => false
    end
  | CLog v k => expr_plain v && body_ok Q g k
  | CBatch b k => body_ok Q g b && body_ok Q g k
  | _ => false
  end.


Definition static_agrees (g : static_env) (e : env) : Prop :=
  forall x z, static_lookup g x = Some z -> env_lookup e x = Some z.

Lemma static_agrees_of e : static_agrees (static_of e) e.
Proof.
  induction e as [|[y v] e IH]; intros x z; simpl; [done|].
  destruct (Nat.eqb x y); [done|apply IH].
Qed.

Lemma static_agrees_none x v g e : static_agrees g e -> static_agrees ((x, None) :: g) ((x, v) :: e).
Proof. intros H y z. simpl. destruct (Nat.eqb y x); [done|apply H]. Qed.

Lemma targets_ev g e n l w :
  static_agrees g e -> targets g n = Some l -> ev e n = Some (Ok w) -> In w l.
Proof.
  intros H. unfold ev. revert l w. induction n; intros l w Ht Hv; simpl in *; try done.
  - injection Ht as <-. injection Hv as <-. by left.
  - destruct (static_lookup g x) as [v|] eqn:E; [|done]. injection Ht as <-. rewrite (H _ _ E) in Hv. simpl in Hv. injection Hv as <-. by left.
  - destruct (targets g n1) as [la|] eqn:E1; [|done]. destruct (targets g n2) as [lb|] eqn:E2; [|done]. injection Ht as <-.
    destruct (eval_expr e None n1) as [[u|y]|] eqn:V1; try done. destruct (eval_expr e None n2) as [[v|y]|] eqn:V2; try done.
    injection Hv as <-. apply list_elem_of_In. apply list_elem_of_bind. exists u. split; [|apply list_elem_of_In; exact (IHn1 _ _ eq_refl eq_refl)].
    apply list_elem_of_bind. exists v. split; [apply list_elem_of_singleton; reflexivity|apply list_elem_of_In; exact (IHn2 _ _ eq_refl eq_refl)].
  - injection Ht as <-. destruct (eval_expr e None n1) as [[u|y]|]; try done. destruct (eval_expr e None n2) as [[v|y]|]; try done.
    injection Hv as <-. unfold bool_val. destruct (Z.ltb u v); simpl; auto.
  - injection Ht as <-. destruct (eval_expr e None n1) as [[u|y]|]; try done. destruct (eval_expr e None n2) as [[v|y]|]; try done.
    injection Hv as <-. unfold bool_val. destruct (Z.eqb u v); simpl; auto.
Qed.

Lemma body_ok_pure (Q : node_id -> bool) (P : node_id -> Prop) g e c :
  (forall t, Q t = true -> P t) -> static_agrees g e -> body_ok Q g c = true -> body_pure P e c.
Proof.
  intros HQ. revert g e. induction c; intros g0 e0 Hg; simpl; try done; intros H.
  - apply andb_true_iff in H as [H1 H2]. split; [done|]. intros z _. eapply IHc; [|exact H2]. by apply static_agrees_none.
  - apply andb_true_iff in H as [H H2]. apply andb_true_iff in H as [H1 H3]. split; [done|]. intros z _. split; intros _; [by eapply IHc2|by eapply IHc1].
  - apply andb_true_iff in H as [H1 H]. destruct (targets g0 n) as [l|] eqn:Et; [|done].
    apply andb_true_iff in H as [H2 H3]. rewrite forallb_forall in H2. split_and!; [done| |].
    + intros z' Hv Hz. pose proof (H2 z' (targets_ev _ _ _ _ _ Hg Et Hv)) as H4. apply orb_true_iff in H4 as [H4|H4]; [apply bool_decide_eq_true_1 in H4; lia|by apply HQ].
    + intros v. eapply IHc; [|exact H3]. by apply static_agrees_none.
  - apply andb_true_iff in H as [H1 H]. destruct (targets g0 n) as [l|] eqn:Et; [|done].
    apply andb_true_iff in H as [H2 H3]. rewrite forallb_forall in H2. split_and!; [done| | |].
    + intros z' Hv Hz. pose proof (H2 z' (targets_ev _ _ _ _ _ Hg Et Hv)) as H4. apply orb_true_iff in H4 as [H4|H4]; [apply bool_decide_eq_true_1 in H4; lia|by apply HQ].
    + intros v. eapply IHc; [|exact H3]. by apply static_agrees_none, static_agrees_none.
    + intros y. eapply IHc; [|exact H3]. by apply static_agrees_none, static_agrees_none.
  - apply andb_true_iff in H as [H1 H2]. split; [by eapply IHc1|by eapply IHc2].
  - apply andb_true_iff in H as [H1 H2]. split; [done|]. by eapply IHc.
Qed.

(** A sufficient test of [pure_at], over reads with constant or captured targets. *)
Fixpoint pure_b (S : gmap node_id (kind * comparer * env * cmd)) (k : nat) (n : node_id) : bool :=
  match k with
  | 0 => false
  | Datatypes.S k' =>
    match S !! n with
    | Some (KSignal, _, _, _) => true
    | Some (KMemo, CmpEq, e, b) => body_ok (fun t => bool_decide (t < n) && pure_b S k' t) (static_of e) b
    | _ => false
    end
  end.

Lemma pure_b_sound S : forall k n, pure_b S k n = true -> pure_at S k n.
Proof.
  induction k as [|k IH]; intros n; [done|]. simpl. destruct (S !! n) as [[[[[] [| |]] e] b]|]; try done.
  intros H. eapply body_ok_pure; [|apply static_agrees_of|exact H].
  intros t Ht _. apply andb_true_iff in Ht as [H1 H2]. apply bool_decide_eq_true_1 in H1. split; [done|]. by apply IH.
Qed.

(** ** The answer of a memo left quiet *)

Definition quiet (d : node) : Prop := n_freshness d = Clean \/ n_unannounced d = true.

(** The node's stored view answers what its latest run read. *)
Definition settled (d : node) : Prop := n_freshness d <> Dirty \/ n_unannounced d = true.

Definition quiet_or_gone (s : state) (t : node_id) : Prop :=
  exists e, g_nodes s !! t = Some e /\ (quiet e \/ n_disposed e = true).

Definition rd_ok (s : state) (c t : node_id) : Prop :=
  seen_of (g_ghost s) c t <> [] /\
  exists e, g_nodes s !! t = Some e /\ forall v, v ∈ seen_of (g_ghost s) c t -> v = read_view_of e.

Definition seen_ans (s : state) (c t : node_id) : option outcome :=
  if decide (t ∈ read_prefix (g_edges s) c)
  then view_answer <$> head (seen_of (g_ghost s) c t)
  else None.

Definition view_matches (d : node) (r : outcome) : Prop :=
  match r with
  | Ok v => n_value d = v /\ n_status d = StNone /\ n_failure d = None
  | Fail y => n_status d = StError /\ n_failure d = Some y
  end.

Lemma view_answer_matches d r : view_matches d r -> view_answer (read_view_of d) = r.
Proof.
  destruct r as [v|y]; unfold view_answer, read_view_of; simpl.
  - intros (H1&H2&_). rewrite H2. simpl. by rewrite H1.
  - intros (H1&H2). rewrite H1, H2. done.
Qed.

Definition value_ok (s : state) (c : node_id) (d : node) : Prop :=
  exists r, body_eval (seen_ans s c) (n_env d) (n_body d) = Some r /\ view_matches d r.

(** A memo that is not self-dirtying, or an effect that only reads. *)
Definition qc (s : state) (c : node_id) (d : node) : Prop :=
  (pure_in (shapes s) c /\ n_kind d = KMemo) \/ quiet_effect s c d.

(** The name of [well_behaved_program] in the invariant's [w_passed]. *)
Definition all_pure (s : state) : Prop := well_behaved_program s.

(** The invariant of the whole-graph theorems. Besides the memos' fields:

    - Each slot of a memo or quiet effect holds an earlier, pure node.
    - A live one outside any run, and not [Dirty] unless it holds its flag, read each source's stored view.
    - Such a memo stores what its body answers over those reads.
    - A live memo outside any run, [Clean] or holding its flag, has only quiet or disposed sources. *)
Record whole (s : state) : Prop := {
  w_signal : forall n d, g_nodes s !! n = Some d -> n_kind d = KSignal -> n_status d = StNone /\ n_failure d = None;
  w_memo : forall c d, g_nodes s !! c = Some d -> n_kind d = KMemo -> pure_in (shapes s) c ->
    n_scope d = None /\ n_violated d = false /\ (n_freshness d = Clean -> n_unannounced d = false);
  w_effect : forall c d, g_nodes s !! c = Some d -> quiet_effect s c d -> n_scope d = None /\ n_unannounced d = false;
  w_slots : forall c d, g_nodes s !! c = Some d -> qc s c d ->
    forall t, t ∈ (slot_view (g_edges s) c).1 -> (n_kind d = KMemo -> t < c) /\ pure_in (shapes s) t;
  w_reads : forall c d, g_nodes s !! c = Some d -> qc s c d -> n_disposed d = false -> c ∉ g_running s -> settled d ->
    forall t, t ∈ (slot_view (g_edges s) c).1 -> t ∉ g_running s -> rd_ok s c t;
  w_value : forall c d, g_nodes s !! c = Some d -> n_kind d = KMemo -> pure_in (shapes s) c -> n_disposed d = false ->
    c ∉ g_running s -> settled d -> value_ok s c d;
  w_quiet : forall c d, g_nodes s !! c = Some d -> n_kind d = KMemo -> pure_in (shapes s) c -> n_disposed d = false ->
    c ∉ g_running s -> quiet d -> forall t, t ∈ (slot_view (g_edges s) c).1 -> quiet_or_gone s t;
  w_passed : forall c, c ∈ gh_passed (g_ghost s) -> ~ pure_in (shapes s) c /\ ~ all_pure s
}.

Lemma seen_ans_view s c t e :
  t ∈ read_prefix (g_edges s) c -> rd_ok s c t -> g_nodes s !! t = Some e ->
  seen_ans s c t = Some (view_answer (read_view_of e)).
Proof.
  intros Hin [Hne (e' & He' & Hall)] He. rewrite He in He'. injection He' as <-.
  unfold seen_ans. rewrite decide_True by done.
  destruct (seen_of (g_ghost s) c t) as [|v l] eqn:Es; [done|].
  simpl. rewrite (Hall v); [done|]. left.
Qed.

Lemma seen_ans_prefix s c t o : seen_ans s c t = Some o -> t ∈ read_prefix (g_edges s) c.
Proof. unfold seen_ans. destruct (decide _); done. Qed.

(** Quiet nodes answer from the signals, once no pure node is running. *)
Lemma quiet_denotes s :
  whole s -> run_core s -> (forall m, m ∈ g_running s -> ~ pure_in (shapes s) m) ->
  forall c e, g_nodes s !! c = Some e -> pure_in (shapes s) c -> quiet e \/ n_disposed e = true ->
  den (S c) s c = Some (view_answer (read_view_of e)).
Proof.
  intros Hw Hc Hnp.
  induction c as [c IH] using lt_wf_ind. intros e He Hp Hq.
  cbn [den]. rewrite He.
  destruct (pure_node s c e Hp He) as [Hk|(Hk&_&_)]; [by rewrite Hk|].
  rewrite Hk. destruct (n_disposed e) eqn:Ed; [done|].
  destruct Hq as [Hq|Hq]; [|done].
  assert (Hnr : c ∉ g_running s) by (intros Hin; exact (Hnp c Hin Hp)).
  assert (Hset : settled e).
  { destruct Hq as [Hq|Hq]; [left; congruence|by right]. }
  destruct (w_value _ Hw c e He Hk Hp Ed Hnr Hset) as (r & Hb & Hm).
  rewrite (view_answer_matches e r Hm).
  revert Hb. apply body_eval_mono. intros t o Ho.
  pose proof (seen_ans_prefix _ _ _ _ Ho) as Hpre.
  assert (Hsl : t ∈ (slot_view (g_edges s) c).1).
  { rewrite read_prefix_view in Hpre. by apply elem_take in Hpre. }
  destruct (w_slots _ Hw c e He (or_introl (conj Hp Hk)) t Hsl) as [Hlt0 Hpt]. pose proof (Hlt0 Hk) as Hlt.
  assert (Hnrt : t ∉ g_running s) by (intros Hin; exact (Hnp t Hin Hpt)).
  pose proof (w_reads _ Hw c e He (or_introl (conj Hp Hk)) Ed Hnr Hset t Hsl Hnrt) as Hrd.
  destruct (w_quiet _ Hw c e He Hk Hp Ed Hnr Hq t Hsl) as (et & Het & Hqt).
  rewrite (seen_ans_view s c t et Hpre Hrd Het) in Ho. injection Ho as <-.
  eapply den_mono; [|exact (IH t Hlt et Het Hpt Hqt)]. lia.
Qed.

(** ** What marks change *)

(** What a mark keeps of a node, and how it moves the node's freshness and flag. *)
Definition rise_node (d d' : node) : Prop :=
  shape_of d' = shape_of d /\ read_view_of d' = read_view_of d /\ n_disposed d' = n_disposed d /\
  n_in_cleanup d' = n_in_cleanup d /\ n_runs d' = n_runs d /\ n_scope d' = n_scope d /\ n_violated d' = n_violated d /\
  freshness_le (n_freshness d) (n_freshness d') /\ (n_unannounced d' = true -> n_unannounced d = true).

Lemma rise_node_refl d : rise_node d d.
Proof. split_and!; first [done|apply freshness_le_refl]. Qed.

Lemma rise_node_trans d1 d2 d3 : rise_node d1 d2 -> rise_node d2 d3 -> rise_node d1 d3.
Proof.
  intros (A1&A2&A3&A4&A5&A6&A7&A8&A9) (B1&B2&B3&B4&B5&B6&B7&B8&B9).
  split_and!; try congruence; [by eapply freshness_le_trans|auto].
Qed.

Lemma rise_kind d d' : rise_node d d' -> n_kind d' = n_kind d.
Proof. intros (H&_). unfold shape_of in H. by injection H. Qed.

Lemma rise_comparer d d' : rise_node d d' -> n_comparer d' = n_comparer d.
Proof. intros (H&_). unfold shape_of in H. by injection H. Qed.

Lemma rise_settled d d' : rise_node d d' -> settled d' -> settled d.
Proof.
  intros (_&_&_&_&_&_&_&H8&H9) [H|H]; [left|right; auto].
  intros Hd. rewrite Hd in H8. apply H. destruct (n_freshness d'); unfold freshness_le in H8; simpl in H8; try lia; done.
Qed.

Lemma rise_quiet d d' : rise_node d d' -> quiet d' -> quiet d.
Proof.
  intros (_&_&_&_&_&_&_&H8&H9) [H|H]; [left|right; auto].
  rewrite H in H8. destruct (n_freshness d); unfold freshness_le in H8; simpl in H8; try lia; done.
Qed.

(** The marks change the nodes and the queue, and the rest of the state stays. *)
Record rise (s s' : state) : Prop := {
  rs_edges : g_edges s' = g_edges s;
  rs_ghost : g_ghost s' = g_ghost s;
  rs_running : g_running s' = g_running s;
  rs_current : g_current s' = g_current s;
  rs_flags : g_flags s' = g_flags s;
  rs_owed : g_flush_owed s' = g_flush_owed s;
  rs_batch : g_batch_depth s' = g_batch_depth s;
  rs_pull : g_pull_depth s' = g_pull_depth s;
  rs_untracked : g_untracked s' = g_untracked s;
  rs_flushing : g_flushing s' = g_flushing s;
  rs_fwd : forall m d, g_nodes s !! m = Some d -> exists d', g_nodes s' !! m = Some d' /\ rise_node d d';
  rs_back : forall m d', g_nodes s' !! m = Some d' -> is_Some (g_nodes s !! m);
  rs_trace : g_trace s' = g_trace s;
  rs_owners : g_owners s' = g_owners s
}.

Lemma rise_refl s : rise s s.
Proof. split; try done. intros m d Hd. exists d. split; [done|apply rise_node_refl]. Qed.

Lemma rise_trans s1 s2 s3 : rise s1 s2 -> rise s2 s3 -> rise s1 s3.
Proof.
  intros [H1 H2 H3 H4 H5 H6 H7 H8 H9 H10 H11 H12 H13 H14] [J1 J2 J3 J4 J5 J6 J7 J8 J9 J10 J11 J12 J13 J14]. split; try congruence.
  - intros m d Hd. destruct (H11 m d Hd) as (d2&Hd2&A). destruct (J11 m d2 Hd2) as (d3&Hd3&B).
    exists d3. split; [done|by eapply rise_node_trans].
  - intros m d' Hd'. destruct (J12 m d' Hd') as [d2 Hd2]. exact (H12 m d2 Hd2).
Qed.

Lemma rise_shapes s s' : rise s s' -> shapes s' = shapes s.
Proof.
  intros [_ _ _ _ _ _ _ _ _ _ F B]. apply map_eq. intros m. unfold shapes. rewrite !lookup_fmap.
  destruct (g_nodes s !! m) as [d|] eqn:E.
  - destruct (F m d E) as (d'&Hd'&H&_). rewrite Hd'. simpl. by rewrite H.
  - destruct (g_nodes s' !! m) as [d'|] eqn:E'; [|done]. destruct (B m d' E') as [x Hx]. congruence.
Qed.

Lemma rise_set s n d f u q :
  g_nodes s !! n = Some d -> freshness_le (n_freshness d) f -> (u = n_unannounced d \/ u = false) ->
  rise s (set_node n (node_marked f u q d) s).
Proof.
  intros Hd Hf Hu. split; try done; simpl.
  - intros m e He. destruct (decide (m = n)) as [->|Hne].
    + rewrite lookup_insert_eq. rewrite Hd in He. injection He as <-. eexists. split; [done|].
      split_and!; try done. simpl. intros ->. destruct Hu as [H|H]; [by rewrite <- H|done].
    + rewrite lookup_insert_ne by done. exists e. split; [done|apply rise_node_refl].
  - intros m e. destruct (decide (m = n)) as [->|Hne]; [eauto|]. rewrite lookup_insert_ne by done. eauto.
Qed.

Lemma prim_rise s s' : mark_prim s s' -> rise s s'.
Proof.
  intros [s0 n d f u Hd _ Hf Hu|s0 n d f Hd _ _ Hf|s0 n d f Hd _ _ Hf].
  - by apply rise_set.
  - apply rise_set; auto.
  - pose proof (rise_set s0 n d f (n_unannounced d) true Hd Hf ltac:(by left)) as [A B C D E F G H I J K L].
    split; try done.
Qed.

Lemma rtc_rise s s' : rtc mark_prim s s' -> rise s s'.
Proof.
  induction 1 as [s|s1 s2 s3 Hp _ IH]; [apply rise_refl|]. eapply rise_trans; [|exact IH]. by apply prim_rise.
Qed.

Lemma any_mark_rise f n r s s' : any_mark f n r s s' -> rise s s'.
Proof. intros H. by apply rtc_rise, (any_mark_rtc f n r). Qed.

(** ** Marks from outside the quiet nodes *)

Definition un (d : node) : Prop := stale (n_freshness d) = true /\ n_unannounced d = false.

Lemma un_rises d d' : rise_node d d' -> un d -> un d'.
Proof.
  intros (_&_&_&_&_&_&_&H8&H9) [H1 H2]. split; [by eapply stale_rises|].
  destruct (n_unannounced d') eqn:E; [|done]. rewrite (H9 eq_refl) in H2. done.
Qed.

Lemma un_not_quiet d : un d -> quiet d -> False.
Proof. intros [H1 H2] [H|H]; [by rewrite H in H1|congruence]. Qed.

(** A node open to a mark from outside the quiet nodes: impure, in [T], or stale with no flag. *)
Definition ysrc (T : node_id -> Prop) (s : state) (n : node_id) : Prop := pure_in (shapes s) n -> T n.

Definition iny (T : node_id -> Prop) (s : state) (n : node_id) : Prop :=
  pure_in (shapes s) n -> T n \/ exists d, g_nodes s !! n = Some d /\ un d.

Definition ytouch (T : node_id -> Prop) (s s0 : state) : Prop :=
  rise s s0 /\ forall m, pure_in (shapes s) m -> ~ T m -> ~ (exists d, g_nodes s !! m = Some d /\ un d) ->
    g_nodes s0 !! m = g_nodes s !! m.

Lemma walk_in (mark : node_id -> state -> option state) (P : state -> Prop) src k s s' :
  (forall c s0 s1, c ∈ observer_view (observers_of (g_edges s0) src) -> P s0 -> mark c s0 = Some s1 -> P s1) ->
  P s -> walk_marks mark src k s = Some s' -> P s'.
Proof.
  intros Hm. revert s. induction k as [|i IH]; intros s Hs Hw; simpl in Hw.
  - by simplify_eq.
  - destruct (decide _); [by eapply IH|].
    destruct (observer_items (observers_of (g_edges s) src) !! i) as [[c x]|] eqn:Ei; [|done].
    destruct (mark c s) as [s1|] eqn:E; simpl in Hw; [|done]. eapply IH; [|exact Hw].
    eapply Hm; [|exact Hs|exact E]. apply view_elem. eauto.
Qed.

Section Touch.
  Variable T : node_id -> Prop.
  Variable s : state.
  (** Observers of an impure node or a member of [T] can be touched too. *)
  Hypothesis HC : forall x y, ysrc T s x -> y ∈ observer_view (observers_of (g_edges s) x) -> iny T s y.

  Lemma ytouch_set s0 n d f u q :
    ytouch T s s0 -> iny T s n -> g_nodes s0 !! n = Some d -> freshness_le (n_freshness d) f ->
    (u = n_unannounced d \/ u = false) ->
    ytouch T s (set_node n (node_marked f u q d) s0).
  Proof.
    intros [R H] Hn Hd Hf Hu. split; [eapply rise_trans; [exact R|by apply rise_set]|].
    intros m Hp HT Hy. simpl. rewrite lookup_insert_ne; [auto|]. intros ->. destruct (Hn Hp) as [K|K]; [done|by apply Hy].
  Qed.

  (** A memo that forwards is impure or in [T]. *)
  Lemma iny_forward s0 n d :
    ytouch T s s0 -> iny T s n -> g_nodes s0 !! n = Some d -> quiet d -> ysrc T s n.
  Proof.
    intros [R _] Hn Hd Hq Hp. destruct (Hn Hp) as [H|(e&He&Hu)]; [done|exfalso].
    destruct (rs_fwd _ _ R n e He) as (e'&He'&Hr). rewrite Hd in He'. injection He' as <-.
    exact (un_not_quiet _ (un_rises _ _ Hr Hu) Hq).
  Qed.

  Lemma ytouch_forward f s0 s1 n :
    (forall c s2 s3, ytouch T s s2 -> iny T s c -> mark_check f c s2 = Some s3 -> ytouch T s s3) ->
    ysrc T s n -> ytouch T s s0 -> notify_with (mark_check f) n s0 = Some s1 -> ytouch T s s1.
  Proof.
    intros IH Hn H0 Hw. refine (walk_in _ _ _ _ _ _ _ H0 Hw).
    intros c s2 s3 Hin Hs2 Hm. refine (IH c s2 s3 Hs2 _ Hm).
    apply (HC n c Hn). by rewrite <- (rs_edges _ _ (proj1 Hs2)).
  Qed.

  Lemma ytouch_effect f n d s0 :
    ytouch T s s0 -> iny T s n -> g_nodes s0 !! n = Some d -> n_kind d = KEffect ->
    freshness_le (n_freshness d) f -> ytouch T s (effect_marked f n d s0).
  Proof.
    intros H Hn Hd Hk Hf. unfold effect_marked. destruct (n_disposed d || n_in_cleanup d); [done|].
    assert (H1 := ytouch_set s0 n d f (n_unannounced d) true H Hn Hd Hf ltac:(by left)).
    destruct (n_queued d); [done|]. destruct H1 as [[A B C D E F G I J K L M] H2].
    split; [split; try done|done].
  Qed.

  Lemma ytouch_check f : forall c s0 s1, ytouch T s s0 -> iny T s c -> mark_check f c s0 = Some s1 -> ytouch T s s1.
  Proof.
    induction f as [|f IH]; intros n s0 s1 H0 Hn Hm; simpl in Hm; [done|].
    destruct (g_nodes s0 !! n) as [d|] eqn:Hd; [|by simplify_eq].
    destruct (n_kind d) eqn:Hk.
    - by simplify_eq.
    - destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
      + eapply ytouch_forward; [exact IH| |eapply ytouch_set; [exact H0|done|done| |by left]|exact Hm].
        * eapply iny_forward; [exact H0|done|done|by left].
        * rewrite Hc. apply freshness_le_clean.
      + destruct (announce_due s0 d) eqn:Ea; [|by simplify_eq].
        unfold announce_due in Ea. apply andb_true_iff in Ea as [_ Ea].
        eapply ytouch_forward; [exact IH| |eapply ytouch_set; [exact H0|done|done|apply freshness_le_refl|by right]|exact Hm].
        eapply iny_forward; [exact H0|done|done|by right].
    - simplify_eq. apply ytouch_effect; [done|done|done|done|].
      destruct (decide _) as [->|_]; [apply freshness_le_clean|apply freshness_le_refl].
  Qed.

  Lemma ytouch_dirty f c s0 s1 : ytouch T s s0 -> iny T s c -> mark_dirty f c s0 = Some s1 -> ytouch T s s1.
  Proof.
    rename c into n. intros H0 Hn Hm. destruct f as [|f]; simpl in Hm; [done|].
    destruct (g_nodes s0 !! n) as [d|] eqn:Hd; [|by simplify_eq].
    destruct (n_kind d) eqn:Hk.
    - by simplify_eq.
    - destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
      + eapply ytouch_forward; [apply ytouch_check| |eapply ytouch_set; [exact H0|done|done|apply freshness_le_dirty|by left]|exact Hm].
        eapply iny_forward; [exact H0|done|done|by left].
      + destruct (announce_due s0 d) eqn:Ea.
        * unfold announce_due in Ea. apply andb_true_iff in Ea as [_ Ea].
          eapply ytouch_forward; [apply ytouch_check| |eapply ytouch_set; [exact H0|done|done|apply freshness_le_dirty|by right]|exact Hm].
          eapply iny_forward; [exact H0|done|done|by right].
        * destruct (decide (n_freshness d = Dirty)); [by simplify_eq|]. simplify_eq.
          eapply ytouch_set; [exact H0|done|done|apply freshness_le_dirty|by left].
    - simplify_eq. apply ytouch_effect; [done|done|done|done|apply freshness_le_dirty].
  Qed.

  Lemma ytouch_refl : ytouch T s s.
  Proof. split; [apply rise_refl|done]. Qed.

  (** A notice from [n] whose observers can be touched, apart from the skipped [r]. *)
  Lemma ytouch_notify_except f n r s' :
    (forall y, y ∈ observer_view (observers_of (g_edges s) n) -> r = Some y \/ iny T s y) ->
    notify_dirty_except f n r s = Some s' -> ytouch T s s'.
  Proof.
    intros Hobs Hw. refine (walk_in _ _ _ _ _ _ _ ytouch_refl Hw).
    intros c s2 s3 Hin Hs2 Hm. unfold mark_except in Hm.
    destruct (decide _) as [_|Hne]; [by simplify_eq|].
    rewrite (rs_edges _ _ (proj1 Hs2)) in Hin.
    destruct (Hobs c Hin) as [Hr|Hy]; [done|]. eapply ytouch_dirty; [exact Hs2|exact Hy|exact Hm].
  Qed.
End Touch.

(** ** Runs below a stack *)

(** The computations in reach of a call below [lim]: pure nodes below [lim], and the current computation [x]. *)
Definition rn (s : state) (lim : node_id) (x : option node_id) (m : node_id) : Prop :=
  (pure_in (shapes s) m /\ m < lim) \/ x = Some m.

(** The stack around a call. [T] holds the running nodes that were stale with no flag when they started. Observers of
    those are in [T] or stale with no flag. *)
Record stk (T : node_id -> Prop) (lim : node_id) (s : state) : Prop := {
  st_low : forall m, m ∈ g_running s -> pure_in (shapes s) m -> lim <= m;
  st_T : forall x, T x -> x ∈ g_running s;
  st_obs : forall x y dy, T x -> g_nodes s !! y = Some dy -> n_kind dy = KMemo -> pure_in (shapes s) y ->
    x ∈ (slot_view (g_edges s) y).1 -> T y \/ (y ∉ g_running s /\ un dy);
  st_A : forall x dx, x ∈ g_running s -> g_nodes s !! x = Some dx -> n_kind dx = KMemo -> pure_in (shapes s) x -> ~ T x ->
    forall t, t ∈ (slot_view (g_edges s) x).1 -> quiet_or_gone s t;
  st_read : forall x dx, x ∈ g_running s -> g_nodes s !! x = Some dx -> qc s x dx ->
    forall t, t ∈ read_prefix (g_edges s) x -> t ∉ g_running s -> rd_ok s x t /\ quiet_or_gone s t;
  st_live : forall x dx, x ∈ g_running s -> g_nodes s !! x = Some dx -> qc s x dx -> n_disposed dx = false;
  st_defer : deferring s = true \/ g_flushing s = true;
  st_dec : forall x, T x \/ ~ T x
}.

(** What a call below [lim] does to the rest of the state. *)
Record atom (T : node_id -> Prop) (lim : node_id) (x : option node_id) (s s' : state) : Prop := {
  at_shapes : shapes s' = shapes s;
  at_running : g_running s' = g_running s;
  at_current : g_current s' = g_current s;
  at_flags : g_flags s' = g_flags s;
  at_owed : g_flush_owed s = true -> g_flush_owed s' = true;
  at_batch : g_batch_depth s' = g_batch_depth s;
  at_pull : g_pull_depth s' = g_pull_depth s;
  at_untracked : g_untracked s' = g_untracked s;
  at_flushing : g_flushing s' = g_flushing s;
  at_passed : forall c, c ∈ gh_passed (g_ghost s') -> c ∈ gh_passed (g_ghost s);
  at_any : forall m d, g_nodes s !! m = Some d -> exists d', g_nodes s' !! m = Some d' /\ n_disposed d' = n_disposed d;
  at_back : forall m d', g_nodes s' !! m = Some d' -> is_Some (g_nodes s !! m);
  at_rise : forall m d, ~ rn s lim None m -> g_nodes s !! m = Some d -> exists d', g_nodes s' !! m = Some d' /\ rise_node d d';
  at_slots : forall c, ~ rn s lim x c -> slot_view (g_edges s') c = slot_view (g_edges s) c /\
    forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y;
  at_keep : forall n d, g_nodes s !! n = Some d -> pure_in (shapes s) n -> ~ T n -> quiet d \/ n_disposed d = true ->
    exists d', g_nodes s' !! n = Some d' /\ read_view_of d' = read_view_of d /\ (quiet d' \/ n_disposed d' = true) /\
      (n_freshness d = Clean -> n_freshness d' = Clean);
  at_frozen : forall n d, ~ rn s lim None n -> g_nodes s !! n = Some d -> pure_in (shapes s) n -> ~ T n -> quiet d ->
    g_nodes s' !! n = Some d;
  at_view : forall m, ~ rn s lim x m -> trace_view m (g_trace s') = trace_view m (g_trace s);
  at_owners : g_owners s' = g_owners s
}.

Lemma atom_refl T lim x s : atom T lim x s s.
Proof.
  split; try done.
  - intros m d Hd. exists d. done.
  - intros m d _ Hd. exists d. split; [done|apply rise_node_refl].
  - intros n d Hd _ _ Hq. exists d. split_and!; done.
Qed.

Lemma rn_mono s s' lim lim' x m :
  shapes s' = shapes s -> lim <= lim' -> ~ rn s lim' x m -> ~ rn s' lim x m.
Proof.
  intros Hs Hl Hn H. apply Hn. unfold rn in *. rewrite Hs in H. destruct H as [[H1 H2]|H]; [left; split; [done|lia]|by right].
Qed.

Lemma atom_trans T lim x s1 s2 s3 : atom T lim x s1 s2 -> atom T lim x s2 s3 -> atom T lim x s1 s3.
Proof.
  intros A B. pose proof (at_shapes _ _ _ _ _ A) as Hs.
  assert (Hrn : forall x' m, ~ rn s1 lim x' m -> ~ rn s2 lim x' m) by (intros x' m; apply rn_mono; [done|lia]).
  split.
  - rewrite (at_shapes _ _ _ _ _ B). done.
  - rewrite (at_running _ _ _ _ _ B). apply A.
  - rewrite (at_current _ _ _ _ _ B). apply A.
  - rewrite (at_flags _ _ _ _ _ B). apply A.
  - intros K. apply (at_owed _ _ _ _ _ B), (at_owed _ _ _ _ _ A), K.
  - rewrite (at_batch _ _ _ _ _ B). apply A.
  - rewrite (at_pull _ _ _ _ _ B). apply A.
  - rewrite (at_untracked _ _ _ _ _ B). apply A.
  - rewrite (at_flushing _ _ _ _ _ B). apply A.
  - intros c Hc. apply (at_passed _ _ _ _ _ A), (at_passed _ _ _ _ _ B), Hc.
  - intros m d Hd. destruct (at_any _ _ _ _ _ A m d Hd) as (d2&Hd2&E). destruct (at_any _ _ _ _ _ B m d2 Hd2) as (d3&Hd3&E').
    exists d3. split; [done|congruence].
  - intros m d' Hd'. destruct (at_back _ _ _ _ _ B m d' Hd') as [d2 Hd2]. exact (at_back _ _ _ _ _ A m d2 Hd2).
  - intros m d Hm Hd. destruct (at_rise _ _ _ _ _ A m d Hm Hd) as (d2&Hd2&R1).
    destruct (at_rise _ _ _ _ _ B m d2 (Hrn None m Hm) Hd2) as (d3&Hd3&R2). exists d3. split; [done|by eapply rise_node_trans].
  - intros c Hc. destruct (at_slots _ _ _ _ _ A c Hc) as [S1 E1]. destruct (at_slots _ _ _ _ _ B c (Hrn x c Hc)) as [S2 E2].
    split; [congruence|]. intros y. by rewrite E2, E1.
  - intros n d Hd Hp HT Hq. destruct (at_keep _ _ _ _ _ A n d Hd Hp HT Hq) as (d2&Hd2&V1&Q1&C1).
    assert (Hp2 : pure_in (shapes s2) n) by (by rewrite Hs).
    destruct (at_keep _ _ _ _ _ B n d2 Hd2 Hp2 HT Q1) as (d3&Hd3&V2&Q2&C2).
    exists d3. split_and!; [done|congruence|done|auto].
  - intros n d Hn Hd Hp HT Hq. pose proof (at_frozen _ _ _ _ _ A n d Hn Hd Hp HT Hq) as H2.
    refine (at_frozen _ _ _ _ _ B n d (Hrn None n Hn) H2 _ HT Hq). by rewrite Hs.
  - intros m Hm. rewrite (at_view _ _ _ _ _ B m (Hrn x m Hm)). exact (at_view _ _ _ _ _ A m Hm).
  - rewrite (at_owners _ _ _ _ _ B). apply A.
Qed.

Lemma atom_mono T lim lim' x x' s s' :
  lim <= lim' -> (forall m, x = Some m -> x' = Some m \/ (pure_in (shapes s) m /\ m < lim')) ->
  atom T lim x s s' -> atom T lim' x' s s'.
Proof.
  intros Hl Hx A.
  assert (Hrn : forall m, ~ rn s lim' x' m -> ~ rn s lim x m).
  { intros m Hm H. apply Hm. destruct H as [[H1 H2]|H]; [left; split; [done|lia]|].
    destruct (Hx m H) as [K|K]; [by right|by left]. }
  assert (Hrn0 : forall m, ~ rn s lim' None m -> ~ rn s lim None m).
  { intros m Hm H. apply Hm. destruct H as [[H1 H2]|H]; [left; split; [done|lia]|done]. }
  destruct A. split; try done.
  - intros m d Hm. eauto.
  - intros c Hc. auto.
  - intros n d Hn. eauto.
  - intros m Hm. auto.
Qed.

Lemma stk_lim T lim lim' s : lim' <= lim -> stk T lim s -> stk T lim' s.
Proof. intros Hl [A B C D E F G H]. split; try done. intros m Hm Hp. pose proof (A m Hm Hp). lia. Qed.

(** ** The same state, as the invariants read it *)

Definition same4 (s s' : state) : Prop :=
  g_nodes s' = g_nodes s /\ g_edges s' = g_edges s /\ g_ghost s' = g_ghost s /\ g_running s' = g_running s.

Lemma same4_shapes s s' : same4 s s' -> shapes s' = shapes s.
Proof. intros (H&_). unfold shapes. by rewrite H. Qed.

Lemma qog_same s s' t : same4 s s' -> quiet_or_gone s t -> quiet_or_gone s' t.
Proof. intros (H&_) Hq. unfold quiet_or_gone in *. by rewrite H. Qed.

Lemma rd_same s s' c t : same4 s s' -> rd_ok s c t -> rd_ok s' c t.
Proof. intros (H1&_&H3&_) Hq. unfold rd_ok in *. by rewrite H1, H3. Qed.

Lemma seen_ans_same s s' c : same4 s s' -> forall t, seen_ans s' c t = seen_ans s c t.
Proof. intros (_&H2&H3&_) t. unfold seen_ans. by rewrite H2, H3. Qed.

Lemma qc_same s s' c d : same4 s s' -> qc s c d -> qc s' c d.
Proof. intros H Hq. unfold qc, quiet_effect in *. by rewrite (same4_shapes _ _ H). Qed.

Lemma value_same s s' c d : same4 s s' -> value_ok s c d -> value_ok s' c d.
Proof.
  intros H (r&Hb&Hm). exists r. split; [|done]. revert Hb. apply body_eval_mono. intros t o. by rewrite (seen_ans_same _ _ _ H).
Qed.

Lemma all_pure_same s s' : same4 s s' -> all_pure s -> all_pure s'.
Proof.
  intros H Ha n d Hd Hk. rewrite (same4_shapes _ _ H). refine (Ha n d _ Hk). by rewrite (proj1 H) in Hd.
Qed.

Lemma whole_same s s' : same4 s s' -> whole s -> whole s'.
Proof.
  intros H Hw. pose proof H as H0. pose proof (same4_shapes _ _ H) as Hs. destruct H as (H1&H2&H3&H4).
  assert (Hback : same4 s' s).
  { split_and!; congruence. }
  destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Hs, ?H1, ?H2, ?H3, ?H4.
  - done.
  - done.
  - intros c d Hd Hq. apply (W3 c d Hd). unfold quiet_effect in *. by rewrite Hs in Hq.
  - intros c d Hd Hq. apply (W4 c d Hd). exact (qc_same _ _ _ _ Hback Hq).
  - intros c d Hd Hq Hl Hr Hse t Ht Hrt. apply (rd_same _ _ _ _ H0). exact (W5 c d Hd (qc_same _ _ _ _ Hback Hq) Hl Hr Hse t Ht Hrt).
  - intros c d Hd Hk Hp Hl Hr Hse. apply (value_same _ _ _ _ H0). exact (W6 c d Hd Hk Hp Hl Hr Hse).
  - intros c d Hd Hk Hp Hl Hr Hq t Ht. apply (qog_same _ _ _ H0). exact (W7 c d Hd Hk Hp Hl Hr Hq t Ht).
  - intros c Hc. destruct (W8 c Hc) as [A B]. split; [done|]. intros Ha. exact (B (all_pure_same _ _ Hback Ha)).
Qed.

Lemma stk_same T lim s s' :
  same4 s s' -> (deferring s = true \/ g_flushing s = true -> deferring s' = true \/ g_flushing s' = true) ->
  stk T lim s -> stk T lim s'.
Proof.
  intros H Hd Hw. pose proof H as H0. pose proof (same4_shapes _ _ H) as Hs. destruct H as (H1&H2&H3&H4).
  assert (Hback : same4 s' s) by (split_and!; congruence).
  destruct Hw as [S1 S2 S3 S4 S5 S6 S7 S8]. split; rewrite ?Hs, ?H1, ?H2, ?H3, ?H4; try done; auto.
  - intros x dx Hx Hdx Hk Hp HT t Ht. apply (qog_same _ _ _ H0). exact (S4 x dx Hx Hdx Hk Hp HT t Ht).
  - intros x dx Hx Hdx Hq t Ht Hnr. destruct (S5 x dx Hx Hdx (qc_same _ _ _ _ Hback Hq) t Ht Hnr) as [A B].
    split; [exact (rd_same _ _ _ _ H0 A)|exact (qog_same _ _ _ H0 B)].
  - intros x dx Hx Hdx Hq. exact (S6 x dx Hx Hdx (qc_same _ _ _ _ Hback Hq)).
Qed.

Lemma atom_same T lim x s s' :
  same4 s s' -> g_current s' = g_current s -> g_flags s' = g_flags s -> (g_flush_owed s = true -> g_flush_owed s' = true) ->
  g_batch_depth s' = g_batch_depth s -> g_pull_depth s' = g_pull_depth s -> g_untracked s' = g_untracked s ->
  g_flushing s' = g_flushing s -> (forall m, ~ rn s lim x m -> trace_view m (g_trace s') = trace_view m (g_trace s)) -> g_owners s' = g_owners s -> atom T lim x s s'.
Proof.
  intros H C F O B P U Fl V Ow. pose proof (same4_shapes _ _ H) as Hs. destruct H as (H1&H2&H3&H4).
  split; rewrite ?H1, ?H2, ?H3, ?H4; try done.
  - intros m d Hd. exists d. done.
  - intros m d _ Hd. exists d. split; [done|apply rise_node_refl].
  - intros n d Hd _ _ Hq. exists d. split_and!; done.
Qed.

(** ** Marks under a stack *)

Lemma pure_kind s n d : pure_in (shapes s) n -> g_nodes s !! n = Some d -> n_kind d <> KEffect.
Proof. intros Hp Hd. destruct (pure_node s n d Hp Hd) as [H|(H&_)]; rewrite H; done. Qed.

Lemma stk_HC T lim s :
  run_base s -> whole s -> stk T lim s ->
  forall x y, ysrc T s x -> y ∈ observer_view (observers_of (g_edges s) x) -> iny T s y.
Proof.
  intros Hb Hw St x y Hx Hin.
  assert (Hslot : x ∈ (slot_view (g_edges s) y).1).
  { apply (held_iff_slot (g_edges s) y x (reachable_well_formed _ (me_reachable _ (b_env _ Hb)))). done. }
  intros Hp. destruct (pure_exists s y Hp) as [dy Hdy].
  destruct (pure_node s y dy Hp Hdy) as [Hk|(Hk&_&_)].
  { rewrite (me_signal _ (b_env _ Hb) y dy Hdy Hk) in Hslot. by apply not_elem_of_nil in Hslot. }
  pose proof (Hx (proj2 (w_slots _ Hw y dy Hdy (or_introl (conj Hp Hk)) x Hslot))) as HTx.
  destruct (st_obs _ _ _ St x y dy HTx Hdy Hk Hp Hslot) as [H|[_ H]]; [by left|right; eauto].
Qed.

(** A slot in [T] makes a pure node's reader touchable. *)
Lemma quiet_untouched T s s' t e :
  ytouch T s s' -> g_nodes s !! t = Some e -> pure_in (shapes s) t -> ~ T t -> quiet e -> g_nodes s' !! t = Some e.
Proof.
  intros [_ H] He Hp HT Hq. rewrite H; [done|done|done|]. intros (e'&He'&Hu).
  rewrite He in He'. injection He' as <-. exact (un_not_quiet _ Hu Hq).
Qed.

Lemma qog_touch T s s' t :
  ytouch T s s' -> pure_in (shapes s) t -> ~ T t -> quiet_or_gone s t -> quiet_or_gone s' t.
Proof.
  intros Ht Hp HT (e&He&Hq). destruct (rs_fwd _ _ (proj1 Ht) t e He) as (e'&He'&R).
  destruct (decide (n_disposed e = true)) as [Hdi|Hdi].
  - exists e'. split; [done|right]. destruct R as (_&_&->&_). done.
  - destruct Hq as [Hq|Hq]; [|done]. exists e. split; [|by left]. eapply quiet_untouched; eauto.
Qed.

Lemma rd_rise s s' c t :
  rise s s' -> rd_ok s c t -> rd_ok s' c t.
Proof.
  intros R (Hne&e&He&Hall). unfold rd_ok. rewrite (rs_ghost _ _ R). split; [done|].
  destruct (rs_fwd _ _ R t e He) as (e'&He'&_&Hv&_). exists e'. split; [done|]. intros v Hv'. rewrite Hv. auto.
Qed.

Lemma seen_ans_rise s s' c : rise s s' -> forall t, seen_ans s' c t = seen_ans s c t.
Proof. intros R t. unfold seen_ans. by rewrite (rs_edges _ _ R), (rs_ghost _ _ R). Qed.

Lemma rise_back s s' m d' : rise s s' -> g_nodes s' !! m = Some d' -> exists d, g_nodes s !! m = Some d /\ rise_node d d'.
Proof.
  intros R Hd'. destruct (rs_back _ _ R m d' Hd') as [d Hd]. destruct (rs_fwd _ _ R m d Hd) as (e&He&Hr).
  rewrite Hd' in He. injection He as <-. eauto.
Qed.

Lemma all_pure_rise s s' : rise s s' -> all_pure s' -> all_pure s.
Proof.
  intros R Ha n d Hd Hk. destruct (rs_fwd _ _ R n d Hd) as (d'&Hd'&Hr).
  rewrite <- (rise_shapes _ _ R). refine (Ha n d' Hd' _). by rewrite (rise_kind _ _ Hr).
Qed.

Lemma view_matches_view d d' r : read_view_of d' = read_view_of d -> view_matches d r -> view_matches d' r.
Proof.
  unfold read_view_of. intros H. injection H as H1 H2 H3. destruct r; simpl; rewrite ?H1, ?H2, ?H3; done.
Qed.

Lemma value_rise s s' c d d' : rise s s' -> rise_node d d' -> value_ok s c d -> value_ok s' c d'.
Proof.
  intros R Hr (r&Hb&Hm). exists r. pose proof Hr as (Hsh&_). unfold shape_of in Hsh. injection Hsh as _ _ E B. rewrite E, B. split.
  - revert Hb. apply body_eval_mono. intros t o. by rewrite (seen_ans_rise _ _ _ R).
  - eapply view_matches_view; [apply Hr|done].
Qed.

Lemma qc_rise s s' c d d' : rise s s' -> rise_node d d' -> qc s' c d' -> qc s c d.
Proof.
  intros R Hr Hq. pose proof (rise_shapes _ _ R) as Hs. destruct Hr as (Hsh&_).
  unfold shape_of in Hsh. injection Hsh as K Q E B.
  unfold qc, quiet_effect in *. rewrite Hs in Hq. destruct Hq as [[H1 H2]|[H1 H2]]; [left; split; congruence|right].
  rewrite E, B in H2. split; congruence.
Qed.

Lemma whole_touch T lim s s' :
  whole s -> stk T lim s -> ytouch T s s' -> whole s' /\ stk T lim s'.
Proof.
  intros Hw St Ht. pose proof Ht as [R Hun]. pose proof (rise_shapes _ _ R) as Hs.
  pose proof (rs_edges _ _ R) as HE. pose proof (rs_ghost _ _ R) as HG. pose proof (rs_running _ _ R) as HR.
  assert (Hqs : forall c dc t, g_nodes s' !! c = Some dc -> n_kind dc = KMemo -> pure_in (shapes s) c -> c ∉ g_running s -> quiet dc ->
            t ∈ (slot_view (g_edges s) c).1 -> pure_in (shapes s) t -> ~ T t).
  { intros c dc t Hc Hk Hp Hnr Hq Hin Hpt HT.
    destruct (rise_back _ _ _ _ R Hc) as (d0&Hd0&Hr).
    destruct (st_obs _ _ _ St t c d0 HT Hd0 ltac:(rewrite <- (rise_kind _ _ Hr); done) Hp Hin) as [K|[_ K]].
    - exact (Hnr (st_T _ _ _ St c K)).
    - exact (un_not_quiet _ (un_rises _ _ Hr K) Hq). }
  split.
  - destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?HE, ?HG, ?HR, ?Hs.
    + intros n d Hd Hk. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hr).
      destruct (W1 n d0 Hd0 ltac:(rewrite <- (rise_kind _ _ Hr); done)) as [A B].
      destruct Hr as (_&Hv&_). unfold read_view_of in Hv. injection Hv as _ X Y. split; congruence.
    + intros c d Hd Hk Hp. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hr).
      destruct (W2 c d0 Hd0 ltac:(rewrite <- (rise_kind _ _ Hr); done) Hp) as (A&B&C).
      pose proof Hr as (_&_&_&_&_&Hsc&Hvi&Hf&Hfl). split_and!; [congruence|congruence|].
      intros Hcl. rewrite Hcl in Hf. destruct (n_unannounced d) eqn:E; [|done].
      rewrite C in Hfl; [naive_solver|]. destruct (n_freshness d0); unfold freshness_le in Hf; simpl in Hf; try lia; done.
    + intros c d Hd Hq. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hr). destruct Hr as (Hsh&_&_&_&_&Hsc&_&_&Hfl').
      assert (Hq1 : quiet_effect s c d0).
      { unfold shape_of in Hsh. injection Hsh as K Q E B.
        unfold quiet_effect in *. rewrite Hs in Hq. destruct Hq as [H1 H2]. rewrite E, B in H2. split; [congruence|done]. }
      destruct (W3 c d0 Hd0 Hq1) as [A B']. rewrite Hsc. split; [done|].
      destruct (n_unannounced d) eqn:Ef; [|done]. rewrite Hfl' in B'; [done|done].
    + intros c d Hd Hq. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hr).
      rewrite (rise_kind _ _ Hr). apply (W4 c d0 Hd0). by eapply qc_rise.
    + intros c d Hd Hq Hl Hr Hse t Ht' Hrt. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hrn).
      assert (Hl0 : n_disposed d0 = false) by (destruct Hrn as (_&_&X&_); congruence).
      refine (rd_rise _ _ _ _ R (W5 c d0 Hd0 _ Hl0 Hr _ t Ht' Hrt)); [by eapply qc_rise|by eapply rise_settled].
    + intros c d Hd Hk Hp Hl Hr Hse. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hrn).
      assert (Hl0 : n_disposed d0 = false) by (destruct Hrn as (_&_&X&_); congruence).
      refine (value_rise _ _ _ _ _ R Hrn (W6 c d0 Hd0 _ Hp Hl0 Hr _)); [by rewrite <- (rise_kind _ _ Hrn)|by eapply rise_settled].
    + intros c d Hd Hk Hp Hl Hr Hq t Ht'. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hrn).
      assert (Hl0 : n_disposed d0 = false) by (destruct Hrn as (_&_&X&_); congruence).
      assert (Hk0 : n_kind d0 = KMemo) by (by rewrite <- (rise_kind _ _ Hrn)).
      pose proof (W7 c d0 Hd0 Hk0 Hp Hl0 Hr (rise_quiet _ _ Hrn Hq) t Ht') as Hqg.
      assert (Hpt : pure_in (shapes s) t) by exact (proj2 (W4 c d0 Hd0 (or_introl (conj Hp Hk0)) t Ht')).
      refine (qog_touch T s s' t Ht Hpt _ Hqg). exact (Hqs c d t Hd Hk Hp Hr Hq Ht' Hpt).
    + intros c Hc. destruct (W8 c Hc) as [A B]. split; [done|]. intros Ha. exact (B (all_pure_rise _ _ R Ha)).
  - destruct St as [S1 S2 S3 S4 S5 S6 S7 S8]. split; rewrite ?HE, ?HG, ?HR, ?Hs; try done.
    + intros x y dy Hx Hdy Hk Hp Hin. destruct (rise_back _ _ _ _ R Hdy) as (d0&Hd0&Hr).
      destruct (S3 x y d0 Hx Hd0 ltac:(rewrite <- (rise_kind _ _ Hr); done) Hp Hin) as [K|[K1 K2]]; [by left|right].
      split; [done|by eapply un_rises].
    + intros x dx Hx Hdx Hk Hp HT t Ht'. destruct (rise_back _ _ _ _ R Hdx) as (d0&Hd0&Hr).
      assert (Hk0 : n_kind d0 = KMemo) by (by rewrite <- (rise_kind _ _ Hr)).
      pose proof (S4 x d0 Hx Hd0 Hk0 Hp HT t Ht') as Hqg.
      assert (Hpt : pure_in (shapes s) t) by exact (proj2 (w_slots _ Hw x d0 Hd0 (or_introl (conj Hp Hk0)) t Ht')).
      refine (qog_touch T s s' t Ht Hpt _ Hqg). intros HTt.
      destruct (S3 t x d0 HTt Hd0 Hk0 Hp Ht') as [K|[K _]]; [done|done].
    + intros x dx Hx Hdx Hq t Ht' Hnr. destruct (rise_back _ _ _ _ R Hdx) as (d0&Hd0&Hr).
      assert (Hq0 : qc s x d0) by (by eapply qc_rise).
      destruct (S5 x d0 Hx Hd0 Hq0 t Ht' Hnr) as [A B].
      split; [exact (rd_rise _ _ _ _ R A)|].
      assert (Hin' : t ∈ (slot_view (g_edges s) x).1) by (rewrite read_prefix_view in Ht'; by apply elem_take in Ht').
      assert (Hpt : pure_in (shapes s) t) by exact (proj2 (w_slots _ Hw x d0 Hd0 Hq0 t Hin')).
      refine (qog_touch T s s' t Ht Hpt _ B). intros HTt. exact (Hnr (S2 t HTt)).
    + intros x dx Hx Hdx Hq. destruct (rise_back _ _ _ _ R Hdx) as (d0&Hd0&Hr).
      pose proof Hr as (_&_&X&_). rewrite X. refine (S6 x d0 Hx Hd0 _). exact (qc_rise _ _ _ _ _ R Hr Hq).
    + unfold deferring. rewrite (rs_current _ _ R), (rs_pull _ _ R), (rs_flushing _ _ R). exact S7.
Qed.

Lemma atom_touch T lim x s s' : ytouch T s s' -> atom T lim x s s'.
Proof.
  intros Ht. pose proof Ht as [R Hun]. pose proof (rise_shapes _ _ R) as Hs.
  split; try apply R; try done.
  - intros K. by rewrite (rs_owed _ _ R).
  - intros c Hc. by rewrite (rs_ghost _ _ R) in Hc.
  - intros m d Hd. destruct (rs_fwd _ _ R m d Hd) as (d'&Hd'&Hr). exists d'. split; [done|apply Hr].
  - intros m d _ Hd. exact (rs_fwd _ _ R m d Hd).
  - intros c _. split; [by rewrite (rs_edges _ _ R)|]. intros y. by rewrite (rs_ghost _ _ R).
  - intros n d Hd Hp HT Hq. destruct (rs_fwd _ _ R n d Hd) as (d'&Hd'&Hr).
    destruct (decide (n_disposed d = true)) as [Hdi|Hdi].
    + destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
      * exists d. split_and!; [|done|by left; left|done].
        eapply quiet_untouched; [exact Ht|exact Hd|exact Hp|exact HT|by left].
      * exists d'. pose proof Hr as (_&Hv&Hx&_). split_and!; [done|done|right; congruence|done].
    + destruct Hq as [Hq|Hq]; [|done]. exists d. split_and!; [|done|by left|done].
      eapply quiet_untouched; eauto.
  - intros n d _ Hd Hp HT Hq. eapply quiet_untouched; eauto.
  - intros m _. by rewrite (rs_trace _ _ R).
Qed.

Lemma read_step_keeps acc x y : y ∈ acc -> y ∈ read_step acc x.
Proof.
  unfold read_step. destruct (decide _); [done|]. rewrite elem_of_app. auto.
Qed.

Lemma rd_ok_ext s s' c t :
  g_nodes s' = g_nodes s -> seen_of (g_ghost s') c t = seen_of (g_ghost s) c t -> rd_ok s c t -> rd_ok s' c t.
Proof. intros H1 H2 Hr. unfold rd_ok in *. by rewrite H1, H2. Qed.

Lemma value_ext s s' c d :
  read_prefix (g_edges s') c = read_prefix (g_edges s) c -> (forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) ->
  value_ok s c d -> value_ok s' c d.
Proof.
  intros H1 H2 (r&Hb&Hm). exists r. split; [|done]. revert Hb. apply body_eval_mono. intros t o.
  unfold seen_ans. rewrite H1, H2. done.
Qed.

Lemma qog_ext s s' t : g_nodes s' = g_nodes s -> quiet_or_gone s t -> quiet_or_gone s' t.
Proof. intros H Hq. unfold quiet_or_gone in *. by rewrite H. Qed.

Lemma qc_ext s s' c d : shapes s' = shapes s -> qc s' c d -> qc s c d.
Proof. intros H Hq. unfold qc, quiet_effect in *. by rewrite H in Hq. Qed.

Lemma all_pure_ext s s' : g_nodes s' = g_nodes s -> all_pure s' -> all_pure s.
Proof. intros H Ha n d Hd Hk. assert (Hs : shapes s = shapes s') by (unfold shapes; by rewrite H). rewrite Hs. refine (Ha n d _ Hk). by rewrite H. Qed.

Lemma track_trace n s s' : track n s = Some (s', tt) -> g_trace s' = g_trace s.
Proof.
  unfold track. destruct (g_current s) as [c|]; [|by intros [=<-]].
  destruct (sources_add c n (g_edges s)); [|done]. intros H. injection H as <-. unfold ghost_read. repeat case_match; reflexivity.
Qed.

Lemma A_track (T : node_id -> Prop) (lim t : node_id) s s' e :
  run_extras s -> whole s -> stk T lim s -> t < lim -> pure_in (shapes s) t -> g_nodes s !! t = Some e ->
  quiet e \/ n_disposed e = true ->
  (forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc -> n_kind dc = KMemo -> t < c) ->
  track t s = Some (s', tt) ->
  run_base s' /\ whole s' /\ stk T lim s' /\ atom T lim (g_current s) s s' /\
  forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc ->
    t ∈ read_prefix (g_edges s') c /\ rd_ok s' c t /\
    (forall y, y ∈ read_prefix (g_edges s) c -> y ∈ read_prefix (g_edges s') c).
Proof.
  intros Hx Hw St Hlt Hp He Hq Hbelow Hm. unfold track in Hm.
  destruct (g_current s) as [c|] eqn:Ec.
  2: { simplify_eq. split_and!; [apply Hx|done|done|apply atom_refl|done]. }
  destruct (sources_add c t (g_edges s)) as [w|] eqn:Ea; [|done]. simplify_eq.
  assert (Hkne : n_kind e <> KEffect) by exact (pure_kind s t e Hp He).
  destruct (extras_track s c t w e Hx Ec Ea He Hkne) as (Hb' & _ & Hinw).
  pose proof (proj1 Hx) as Hb.
  destruct (slot_view_add _ _ _ _ (b_cursor _ Hb c) Ea) as (Hoth & Hle & Hsl & Hpre).
  assert (Hrunc : c ∈ g_running s) by exact (b_current _ Hb c Ec).
  assert (Hnrt : t ∉ g_running s).
  { intros Hin. pose proof (st_low _ _ _ St t Hin Hp). lia. }
  assert (HTt : ~ T t) by (intros K; exact (Hnrt (st_T _ _ _ St t K))).
  assert (Hqt : quiet_or_gone s t) by (exists e; done).
  set (g := g_ghost s) in *.
  set (G := {| gh_published := gh_published g; gh_seen := <[(c, t) := read_view_of e :: (if decide (t ∈ read_prefix (g_edges s) c) then default [] (gh_seen g !! (c, t)) else [])]> (gh_seen g);
              gh_unnoticed := if decide (t ∈ g_running s) then {[c]} ∪ gh_unnoticed g else gh_unnoticed g;
              gh_window := gh_window g; gh_reads := <[c := default [] (gh_reads g !! c) ++ [t]]> (gh_reads g);
              gh_passed := gh_passed g; gh_over := gh_over g;
              gh_run_reads := <[c := default [] (gh_run_reads g !! c) ++ [t]]> (gh_run_reads g);
              gh_nested := gh_nested g |}).
  assert (Hs1 : ghost_read c t (read_prefix (g_edges s) c) (with_edges w s) = with_ghost G (with_edges w s)).
  { unfold ghost_read. match goal with |- match ?x with _ => _ end = _ => assert (Hmx : x = Some e) by exact He; rewrite Hmx end. reflexivity. }
  rewrite Hs1 in Hb' |- *.
  set (s1 := with_ghost G (with_edges w s)) in *.
  assert (Hn : g_nodes s1 = g_nodes s) by done.
  assert (Hr : g_running s1 = g_running s) by done.
  assert (Hg : g_ghost s1 = G) by done.
  assert (Hw1 : g_edges s1 = w) by done.
  assert (Hsh : shapes s1 = shapes s) by (unfold shapes; by rewrite Hn).
  assert (Hso : forall c' y, (c', y) <> (c, t) -> seen_of (g_ghost s1) c' y = seen_of g c' y).
  { intros c' y Hne. unfold seen_of. rewrite Hg. subst G. simpl. by rewrite lookup_insert_ne. }
  assert (Hpo : forall c', c' <> c -> read_prefix w c' = read_prefix (g_edges s) c').
  { intros c' Hne. by rewrite !read_prefix_view, Hoth. }
  assert (Hrd : forall c' y, c' <> c -> rd_ok s c' y -> rd_ok s1 c' y).
  { intros c' y Hne. apply rd_ok_ext; [done|]. apply Hso. congruence. }
  assert (Hrd2 : forall y, y <> t -> rd_ok s c y -> rd_ok s1 c y).
  { intros y Hne. apply rd_ok_ext; [done|]. apply Hso. congruence. }
  assert (Hnew : forall v, v ∈ seen_of (g_ghost s1) c t -> v = read_view_of e \/ (t ∈ read_prefix (g_edges s) c /\ v ∈ seen_of g c t)).
  { intros v. unfold seen_of. rewrite Hg. subst G. simpl. rewrite lookup_insert_eq. simpl.
    rewrite elem_of_cons. intros [H|H]; [by left|right].
    destruct (decide _) as [Hin|Hout]; [done|]. by apply not_elem_of_nil in H. }
  assert (Hrdt : forall dc, g_nodes s !! c = Some dc -> qc s c dc -> rd_ok s1 c t).
  { intros dc Hdc Hqc. split.
    - unfold seen_of. rewrite Hg. subst G. simpl. rewrite lookup_insert_eq. simpl. done.
    - exists e. split; [done|]. intros v Hv. destruct (Hnew v Hv) as [->|[Hin Hv']]; [done|].
      destruct (st_read _ _ _ St c dc Hrunc Hdc Hqc t Hin Hnrt) as [(_&e'&He'&Hall) _].
      rewrite He in He'. injection He' as <-. auto. }
  assert (Hrwp : forall dc, g_nodes s !! c = Some dc -> qc s c dc -> forall x, x ∈ (slot_view w c).1 -> x <> t -> x ∈ (slot_view (g_edges s) c).1).
  { intros dc _ _ x Hin Hne. destruct (Hsl x Hin) as [H|H]; done. }
  split_and!.
  - exact Hb'.
  - destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Hn, ?Hr, ?Hsh, ?Hw1, ?Hg.
    + done.
    + done.
    + intros c0 d Hd Hk. apply (W3 c0 d Hd). unfold quiet_effect in *. done.
    + intros c0 d Hd Hk y Hy. destruct (decide (c0 = c)) as [->|Hne].
      * destruct (Hsl y Hy) as [H| ->]; [exact (W4 c d Hd (qc_ext _ s1 _ _ Hsh Hk) y H)|].
        split; [intros Kk; exact (Hbelow c d eq_refl Hd (qc_ext _ s1 _ _ Hsh Hk) Kk)|done].
      * rewrite Hoth in Hy by done. exact (W4 c0 d Hd (qc_ext _ s1 _ _ Hsh Hk) y Hy).
    + intros c0 d Hd Hk Hl Hnr Hse y Hy Hny. assert (Hne : c0 <> c) by (by intros ->).
      rewrite Hoth in Hy by done. apply Hrd; [done|]. exact (W5 c0 d Hd (qc_ext _ s1 _ _ Hsh Hk) Hl Hnr Hse y Hy Hny).
    + intros c0 d Hd Hk Hp0 Hl Hnr Hse. assert (Hne : c0 <> c) by (by intros ->).
      refine (value_ext s s1 c0 d (Hpo c0 Hne) _ (W6 c0 d Hd Hk Hp0 Hl Hnr Hse)).
      intros y. apply Hso. congruence.
    + intros c0 d Hd Hk Hp0 Hl Hnr Hq0 y Hy. assert (Hne : c0 <> c) by (by intros ->).
      rewrite Hoth in Hy by done. apply (qog_ext s s1 y Hn). exact (W7 c0 d Hd Hk Hp0 Hl Hnr Hq0 y Hy).
    + intros c0 Hc0. destruct (W8 c0 Hc0) as [A B]. split; [done|]. intros Ha. exact (B (all_pure_ext _ _ Hn Ha)).
  - destruct St as [S1 S2 S3 S4 S5 S6 S7 S8]. split; rewrite ?Hn, ?Hr, ?Hsh, ?Hw1, ?Hg; try done.
    + intros x y dy Hx0 Hdy Hk Hp0 Hin. destruct (decide (y = c)) as [->|Hne].
      * destruct (Hsl x Hin) as [H| ->]; [exact (S3 x c dy Hx0 Hdy Hk Hp0 H)|by destruct HTt].
      * rewrite Hoth in Hin by done. exact (S3 x y dy Hx0 Hdy Hk Hp0 Hin).
    + intros x dx Hx0 Hdx Hk Hp0 HT0 y Hy. apply (qog_ext s s1 y Hn). destruct (decide (x = c)) as [->|Hne].
      * destruct (Hsl y Hy) as [H| ->]; [exact (S4 c dx Hx0 Hdx Hk Hp0 HT0 y H)|done].
      * rewrite Hoth in Hy by done. exact (S4 x dx Hx0 Hdx Hk Hp0 HT0 y Hy).
    + intros x dx Hx0 Hdx Hqc y Hy Hny. assert (Hqc0 : qc s x dx) by exact (qc_ext _ s1 _ _ Hsh Hqc).
      destruct (decide (x = c)) as [->|Hne].
      * destruct (decide (y = t)) as [->|Hyt].
        -- split; [exact (Hrdt dx Hdx Hqc0)|exact (qog_ext s s1 t Hn Hqt)].
        -- destruct (Hpre y Hy) as [H|H]; [|done].
           destruct (S5 c dx Hx0 Hdx Hqc0 y H Hny) as [A B].
           split; [exact (Hrd2 y Hyt A)|exact (qog_ext s s1 y Hn B)].
      * rewrite Hpo in Hy by done. destruct (S5 x dx Hx0 Hdx Hqc0 y Hy Hny) as [A B].
        split; [exact (Hrd x y Hne A)|exact (qog_ext s s1 y Hn B)].
  - split; rewrite ?Hn, ?Hr, ?Hsh, ?Hw1, ?Hg; try done.
    + intros m d Hd. exists d. done.
    + intros m d _ Hd. exists d. split; [done|apply rise_node_refl].
    + intros c0 Hc0. assert (Hne : c0 <> c) by (intros ->; apply Hc0; by right).
      split; [by rewrite Hoth|]. intros y. apply Hso. congruence.
    + intros n d Hd _ _ Hq0. exists d. split_and!; done.
  - intros c0 dc Hc0 Hdc Hqc. injection Hc0 as <-. split_and!.
    + exact Hinw.
    + exact (Hrdt dc Hdc Hqc).
    + intros y Hy. rewrite Hw1.
      assert (Hl : n_disposed dc = false) by exact (st_live _ _ _ St c dc Hrunc Hdc Hqc).
      rewrite (b_reads _ Hb c dc Hdc Hl) in Hy.
      pose proof (b_reads _ Hb' c dc ltac:(by rewrite Hn) Hl) as Hr'. rewrite Hw1 in Hr'. rewrite Hr'.
      unfold reads_of. rewrite Hg. subst G. simpl. rewrite lookup_insert_eq. simpl.
      rewrite read_list_snoc. apply read_step_keeps. exact Hy.
Qed.

Lemma shapes_set s n d d' :
  g_nodes s !! n = Some d -> shape_of d' = shape_of d -> shapes (set_node n d' s) = shapes s.
Proof.
  intros Hd Hsh. unfold shapes. simpl. rewrite fmap_insert, Hsh.
  apply insert_id. by rewrite lookup_fmap, Hd.
Qed.

Lemma slot_holds_false p s n :
  (forall src e, src ∈ (slot_view (g_edges s) n).1 -> g_nodes s !! src = Some e -> p e = false) ->
  slot_holds p s n = false.
Proof.
  intros H. destruct (slot_holds p s n) eqn:E; [|done]. unfold slot_holds in E.
  apply existsb_exists in E as (src & Hin & Hp). destruct (g_nodes s !! src) as [e|] eqn:He; [|done].
  rewrite (H src e) in Hp; [done| |done]. by apply list_elem_of_In in Hin.
Qed.

Lemma qog_not_passing s t :
  quiet_or_gone s t -> forall e, g_nodes s !! t = Some e -> stale_live e && negb (n_unannounced e) = false.
Proof.
  intros (e0&He0&Hq) e He. rewrite He in He0. injection He0 as <-. unfold stale_live.
  destruct Hq as [[H|H]|H]; rewrite H; simpl; [done|apply andb_false_r|].
  rewrite andb_false_r. done.
Qed.

(** The state a walk ending in [Check] over quiet sources leaves a memo in, [Clean]. *)
Lemma resolve_step (T : node_id -> Prop) (lim t : node_id) s d :
  run_base s -> whole s -> stk T lim s -> t < lim -> pure_in (shapes s) t -> g_nodes s !! t = Some d -> n_kind d = KMemo ->
  n_disposed d = false -> n_freshness d = Check -> t ∉ g_running s ->
  (forall u, u ∈ (slot_view (g_edges s) t).1 -> quiet_or_gone s u) ->
  let s' := set_node t (node_with_unannounced false (node_with_freshness Clean d)) (ghost_resolved t s) in
  whole s' /\ stk T lim s' /\ atom T lim None s s'.
Proof.
  intros Hb Hw St Hlt Hp Hd Hk Hl Hf Hnr Hq s'.
  destruct (ghost_resolved_fields t s) as (F1&F2&F3&F4&F5&F6&F7&_).
  assert (Hslots : forall u, u ∈ (slot_view (g_edges s) t).1 -> u < t /\ pure_in (shapes s) u).
  { intros u Hu. destruct (w_slots _ Hw t d Hd (or_introl (conj Hp Hk)) u Hu) as [A B]. split; [exact (A Hk)|exact B]. }
  assert (Hpass : gh_passed (g_ghost (ghost_resolved t s)) = gh_passed (g_ghost s)).
  { unfold ghost_resolved. simpl. rewrite slot_holds_false; [done|].
    intros src e Hin He. exact (qog_not_passing s src (Hq src Hin) e He). }
  assert (Hsn : g_nodes s' = <[t := node_with_unannounced false (node_with_freshness Clean d)]> (g_nodes s)) by done.
  assert (He' : g_edges s' = g_edges s) by done.
  assert (Hrn' : g_running s' = g_running s) by done.
  assert (Hsee : forall c y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) by done.
  assert (Hpas' : gh_passed (g_ghost s') = gh_passed (g_ghost s)) by exact Hpass.
  assert (Hsh : shapes s' = shapes s).
  { unfold shapes. rewrite Hsn, fmap_insert. assert (Hx : shape_of (node_with_unannounced false (node_with_freshness Clean d)) = shape_of d) by done.
    rewrite Hx. apply insert_id. by rewrite lookup_fmap, Hd. }
  assert (Ht1 : g_nodes s' !! t = Some (node_with_unannounced false (node_with_freshness Clean d))) by (rewrite Hsn; apply lookup_insert_eq).
  assert (Hne1 : forall m, m <> t -> g_nodes s' !! m = g_nodes s !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hrd : forall c y, rd_ok s c y -> rd_ok s' c y).
  { intros c y (H1&e&He&H2). split; [done|]. destruct (decide (y = t)) as [->|Hyt].
    - rewrite Hd in He. injection He as <-. eexists. split; [exact Ht1|]. intros v Hv. rewrite (H2 v); [done|exact Hv].
    - exists e. split; [by rewrite Hne1|]. exact H2. }
  assert (Hqg : forall y, quiet_or_gone s y -> quiet_or_gone s' y).
  { intros y (e&He&H). destruct (decide (y = t)) as [->|Hyt].
    - eexists. split; [exact Ht1|]. left. by left.
    - exists e. split; [by rewrite Hne1|done]. }
  assert (Hback : forall m d', g_nodes s' !! m = Some d' -> (m = t /\ d' = node_with_unannounced false (node_with_freshness Clean d)) \/ (m <> t /\ g_nodes s !! m = Some d')).
  { intros m d' Hm. destruct (decide (m = t)) as [->|Hm']; [left|right].
    - rewrite Ht1 in Hm. injection Hm as <-. done.
    - rewrite Hne1 in Hm by done. done. }
  assert (Hval : forall c d0, value_ok s c d0 -> value_ok s' c d0).
  { intros c d0. apply value_ext; [by rewrite He'|done]. }
  split_and!.
  - destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Hsh, ?He', ?Hrn', ?Hpas'.
    + intros n d0 Hd0 Hk0. destruct (Hback n d0 Hd0) as [[-> ->]|[_ H]]; [simpl in Hk0; congruence|by apply (W1 n d0)].
    + intros c d0 Hd0 Hk0 Hp0. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * simpl. destruct (W2 t d Hd Hk Hp) as (A&B&_). split_and!; done.
      * by apply (W2 c d0).
    + intros c d0 Hd0 Hq0. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * exfalso. destruct Hq0 as [K _]. simpl in K. congruence.
      * apply (W3 c d0 H). unfold quiet_effect in *. by rewrite Hsh in Hq0.
    + intros c d0 Hd0 Hq0. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * exact (W4 t d Hd (or_introl (conj Hp Hk))).
      * exact (W4 c d0 H Hq1).
    + intros c d0 Hd0 Hq0 Hl0 Hnr0 Hse u Hu Hnu. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. apply Hrd.
      destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * refine (W5 t d Hd (or_introl (conj Hp Hk)) Hl Hnr _ u Hu Hnu). left. congruence.
      * exact (W5 c d0 H Hq1 Hl0 Hnr0 Hse u Hu Hnu).
    + intros c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hse. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * destruct (Hval t d (W6 t d Hd Hk Hp Hl Hnr ltac:(left; congruence))) as (r&Hb0&Hm0). exists r. split; [done|].
        eapply view_matches_view; [|exact Hm0]. done.
      * apply Hval. exact (W6 c d0 H Hk0 Hp0 Hl0 Hnr0 Hse).
    + intros c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hq0 u Hu. apply Hqg. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
      * by apply Hq.
      * exact (W7 c d0 H Hk0 Hp0 Hl0 Hnr0 Hq0 u Hu).
    + intros c Hc. destruct (W8 c Hc) as [A B]. split; [done|]. intros Ha. apply B.
      intros n d0 Hd0 Hk0. rewrite <- Hsh. destruct (decide (n = t)) as [->|Hn]; [refine (Ha t _ Ht1 _); done|].
      refine (Ha n d0 _ Hk0). by rewrite Hne1.
  - destruct St as [S1 S2 S3 S4 S5 S6 S7 S8]. split; rewrite ?Hsh, ?He', ?Hrn'.
    + done.
    + done.
    + intros x y dy Hx Hdy Hk0 Hp0 Hin. destruct (Hback y dy Hdy) as [[-> ->]|[_ H]].
      * exfalso. destruct (Hslots x Hin) as [Hlx Hpx]. pose proof (S1 x (S2 x Hx) Hpx). lia.
      * exact (S3 x y dy Hx H Hk0 Hp0 Hin).
    + intros x dx Hx Hdx Hk0 Hp0 HT u Hu. apply Hqg. destruct (Hback x dx Hdx) as [[-> ->]|[_ H]]; [done|].
      exact (S4 x dx Hx H Hk0 Hp0 HT u Hu).
    + intros x dx Hx Hdx Hq0 u Hu Hnu. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. destruct (Hback x dx Hdx) as [[-> ->]|[_ H]]; [done|].
      destruct (S5 x dx Hx H Hq1 u Hu Hnu) as [A B]. split; [by apply Hrd|by apply Hqg].
    + intros x dx Hx Hdx Hq0. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. destruct (Hback x dx Hdx) as [[-> ->]|[_ H]]; [done|].
      exact (S6 x dx Hx H Hq1).
    + exact S7.
    + exact S8.
  - split; rewrite ?Hsh, ?He', ?Hrn', ?Hpas'; try done.
    + intros m d0 Hd0. destruct (decide (m = t)) as [->|Hm].
      * rewrite Hd in Hd0. injection Hd0 as <-. eexists. split; [exact Ht1|done].
      * exists d0. rewrite Hne1 by done. done.
    + intros m d' Hm. destruct (Hback m d' Hm) as [[-> ->]|[_ H]]; [rewrite Hd; eauto|rewrite H; eauto].
    + intros m d0 Hm Hd0. assert (Hne : m <> t) by (intros ->; apply Hm; by left; split). rewrite Hne1 by done.
      exists d0. split; [done|apply rise_node_refl].
    + intros n d0 Hd0 Hp0 HT0 Hq0. destruct (decide (n = t)) as [->|Hn].
      * rewrite Hd in Hd0. injection Hd0 as <-. eexists. split_and!; [exact Ht1|done|left; by left|done].
      * exists d0. rewrite Hne1 by done. split_and!; done.
    + intros n d0 Hn Hd0 _ _ _. assert (Hne : n <> t) by (intros ->; apply Hn; by left; split). by rewrite Hne1.
Qed.

(** ** A call below the stack *)

Definition aS (T : node_id -> Prop) (lim : node_id) (s : state) : Prop := run_core s /\ whole s /\ stk T lim s.

(** Answers a guide holds: the current answers of quiet nodes. *)
Definition guide (T : node_id -> Prop) (s : state) (A : node_id -> option outcome) : Prop :=
  forall t o, A t = Some o -> pure_in (shapes s) t /\ ~ T t /\
    exists e, g_nodes s !! t = Some e /\ (quiet e \/ n_disposed e = true) /\ view_answer (read_view_of e) = o.

(** The node ends quiet or disposed, and a node that started quiet ends [Clean] or disposed. *)
Definition done_at (t : node_id) (s s' : state) : Prop :=
  quiet_or_gone s' t /\
  forall d, g_nodes s !! t = Some d -> quiet d -> exists d', g_nodes s' !! t = Some d' /\ (n_freshness d' = Clean \/ n_disposed d' = true).

Definition rd_pre (T : node_id -> Prop) (lim t : node_id) (s : state) : Prop :=
  aS T lim s /\ t < lim /\ pure_in (shapes s) t /\
  (forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc -> n_kind dc = KMemo -> t < c) /\
  (forall c, g_current s = Some c -> ysrc T s c \/ quiet_or_gone s t).

Definition rd_post (T : node_id -> Prop) (lim t : node_id) (s s' : state) (o : outcome) : Prop :=
  aS T lim s' /\ atom T lim (g_current s) s s' /\
  (exists e, g_nodes s' !! t = Some e /\ o = view_answer (read_view_of e) /\ (quiet e \/ n_disposed e = true)) /\
  (forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc ->
    t ∈ read_prefix (g_edges s') c /\ rd_ok s' c t /\
    (forall y, y ∈ read_prefix (g_edges s) c -> y ∈ read_prefix (g_edges s') c)) /\
  forall m, ~ (pure_in (shapes s) m /\ m < lim) -> trace_view m (g_trace s') = trace_view m (g_trace s).

(** A memo's reads reach nodes below it. An effect's reach every node, and [L] is above them all. *)
Definition below_or_all (L c : node_id) (d : node) (s : state) : Prop :=
  (n_kind d = KMemo /\ L = c) \/ (n_kind d = KEffect /\ forall m, is_Some (g_nodes s !! m) -> m < L).

Lemma below_shapes L c d d' s s' :
  shapes s' = shapes s -> g_nodes s !! c = Some d -> g_nodes s' !! c = Some d' -> below_or_all L c d s -> below_or_all L c d' s'.
Proof.
  intros Hs Hd Hd' H.
  assert (Hk : n_kind d' = n_kind d).
  { pose proof (shapes_lookup s c d Hd) as A. pose proof (shapes_lookup s' c d' Hd') as B. rewrite Hs, A in B. unfold shape_of in B. injection B as K _ _ _. congruence. }
  destruct H as [[K ->]|[K H]]; [left; split; [congruence|done]|right; split; [congruence|]].
  intros m [x Hx]. apply H. pose proof (shapes_lookup s' m x Hx) as B. rewrite Hs in B.
  assert (B2 : is_Some (shapes s !! m)) by (eexists; exact B). unfold shapes in B2. rewrite lookup_fmap in B2.
  by apply fmap_is_Some in B2.
Qed.

Lemma read_readable g t s s' o : read g t true s = Some (s', o) -> readable (shapes s) t.
Proof.
  intros H. destruct g as [|g]; [done|]. cbn [read] in H. destruct g as [|g]; [done|]. cbn [read_node] in H.
  unfold readable, shapes. rewrite lookup_fmap. unfold m_bind, get_node in H. destruct (g_nodes s !! t) as [d|] eqn:E; [|done].
  simpl. unfold shape_of. destruct (n_kind d); done.
Qed.

Record A_ok (f : nat) : Prop := {
  Aexec : forall (T : node_id -> Prop) (L c : node_id) d e' e cm s s' r A r0,
    aS T L s -> g_current s = Some c -> g_nodes s !! c = Some d -> qc s c d -> env_agree e' e ->
    below_or_all L c d s ->
    body_pure (fun t => readable (shapes s) t -> t < L /\ pure_in (shapes s) t) e cm ->
    (~ T c -> body_eval A e cm = Some r0 /\ guide T s A) ->
    (T c \/ (~ T c /\ pure_in (shapes s) c)) ->
    exec f e' cm s = Some (s', r) ->
    aS T L s' /\ atom T L (Some c) s s' /\ body_eval (seen_ans s' c) e cm = Some r /\ (~ T c -> r = r0) /\
    (forall t o, seen_ans s c t = Some o -> seen_ans s' c t = Some o) /\
    (exists l, body_logs (seen_ans s' c) e cm = Some l /\ trace_view c (g_trace s') = trace_view c (g_trace s) ++ (EvLog c <$> l));
  Aread : forall (T : node_id -> Prop) (lim t : node_id) s s' o, rd_pre T lim t s -> read f t true s = Some (s', o) -> rd_post T lim t s s' o;
  Anode : forall (T : node_id -> Prop) (lim t : node_id) s s' o, rd_pre T lim t s -> read_node f t s = Some (s', o) -> rd_post T lim t s s' o;
  Aupd : forall (T : node_id -> Prop) (lim t : node_id) s s' a, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    update_if_necessary f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s';
  Arop : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    refresh_or_pull f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s';
  Apull : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> g_flushing s = true -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    pull f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s';
  Aref : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    refresh f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s';
  Awalk : forall (T : node_id -> Prop) (lim c : node_id) i s s' a, aS T lim s -> ~ rn s lim None c -> c ∉ g_running s ->
    (forall u, u ∈ (slot_view (g_edges s) c).1 -> u < lim /\ pure_in (shapes s) u) ->
    resolve_walk f c i s = Some (s', a) ->
    aS T lim s' /\ atom T lim None s s' /\
    forall dc j u mh, g_nodes s' !! c = Some dc -> n_freshness dc = Check -> slot_at (g_edges s') c j = Some (u, mh) -> i <= j -> quiet_or_gone s' u;
  Arec : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    recompute f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s';
  Arun : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    run_memo f t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'
}.

Lemma A_ok_0 : A_ok 0.
Proof. split; intros; done. Qed.

Lemma slot_at_in (w : EdgeWorld) c j u mh : slot_at w c j = Some (u, mh) -> u ∈ (slot_view w c).1.
Proof.
  intros H. apply list_elem_of_lookup. exists j. unfold slot_view, slot_at in *. simpl. rewrite list_lookup_fmap, H. done.
Qed.

Lemma qog_atom (T : node_id -> Prop) lim x s s' t :
  atom T lim x s s' -> pure_in (shapes s) t -> ~ T t -> quiet_or_gone s t -> quiet_or_gone s' t.
Proof.
  intros A Hp HT (e&He&Hq). destruct (at_keep _ _ _ _ _ A t e He Hp HT Hq) as (e'&He'&_&Hq'&_). exists e'. done.
Qed.

Lemma rise_not_check d d' : rise_node d d' -> n_freshness d = Check -> n_freshness d' <> Check -> n_freshness d' = Dirty.
Proof.
  intros (_&_&_&_&_&_&_&H&_) H1 H2. rewrite H1 in H. destruct (n_freshness d'); unfold freshness_le in H; simpl in H; try lia; done.
Qed.

Section Step.
  Variable f : nat.
  Hypothesis IH : A_ok f.

  Lemma Arec_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    recompute (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' a d Ha Hlt Hp Hd Hk Hl Hm. cbn [recompute] in Hm. rewrite (bind_get_node _ _ _ _ Hd) in Hm.
    destruct Ha as (Hc&Hw&St) eqn:Eq.
    rewrite (proj1 (w_memo _ Hw t d Hd Hk Hp)) in Hm. exact (Arun _ IH T lim t s s' a d Ha Hlt Hp Hd Hk Hl Hm).
  Qed.

  Lemma Aref_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    refresh (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' a d Ha Hlt Hp Hd Hk Hl Hm0.
    pose proof Ha as (Hc&Hw&St).
    assert (Hmono : forall s1 s2, atom T t None s1 s2 -> atom T lim None s1 s2).
    { intros s1 s2. apply atom_mono; [lia|done]. }
    assert (Hnr : t ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St t Hin Hp); lia).
    pose proof (Rrefresh _ (runs_ok_all (S f)) t _ _ _ Hc ltac:(by exists d) Hm0) as [Hc' _].
    pose proof Hm0 as Hm. cbn [refresh] in Hm. rewrite (bind_get_node _ _ _ _ Hd) in Hm.
    (* The last step, from the state a walk or nothing leaves. *)
    assert (Hlast : forall s2 d2, aS T lim s2 -> atom T lim None s s2 -> g_nodes s2 !! t = Some d2 -> n_kind d2 = KMemo -> n_disposed d2 = false ->
              n_freshness d2 <> Check -> (quiet d -> n_freshness d2 <> Dirty \/ quiet d2) ->
              (let* d' := get_node t in if decide (n_freshness d' = Dirty) then recompute f t else m_return tt) s2 = Some (s', a) ->
              aS T lim s' /\ atom T lim None s s' /\ done_at t s s').
    { intros s2 d2 Ha2 A2 Hd2 Hk2 Hl2 Hnc Hqq Hm2. rewrite (bind_get_node _ _ _ _ Hd2) in Hm2.
      assert (Hp2 : pure_in (shapes s2) t) by (by rewrite (at_shapes _ _ _ _ _ A2)).
      destruct (decide (n_freshness d2 = Dirty)) as [Hdirty|Hnd].
      - pose proof (Arec _ IH T lim t s2 s' a d2 Ha2 Hlt Hp2 Hd2 Hk2 Hl2 Hm2) as (Ha3&A3&D3).
        split_and!; [done|by eapply atom_trans|]. destruct D3 as [D1 D2]. split; [done|].
        intros d0 Hd0 Hq0. rewrite Hd in Hd0. injection Hd0 as <-.
        destruct (Hqq Hq0) as [K|K]; [done|]. apply (D2 d2 Hd2 K).
      - unfold m_return in Hm2. injection Hm2 as <- <-.
        split_and!; [done|done|split].
        + exists d2. split; [done|left]. destruct (n_freshness d2) eqn:E; [by left|done|done].
        + intros d0 Hd0 Hq0. eexists. split; [exact Hd2|left]. destruct (n_freshness d2); done. }
    destruct (decide (n_freshness d = Check)) as [Hch|Hch].
    - apply bind_inv in Hm as (s2 & [] & H1 & Hm2). apply bind_inv in H1 as (s1 & [] & Hwk & H1).
      assert (Hrnt : ~ rn s t None t) by (intros [[_ K]|K]; [lia|done]).
      destruct (Awalk _ IH T t t 0 s s1 tt (conj Hc (conj Hw (stk_lim T lim t s ltac:(lia) St))) Hrnt Hnr
                 (fun u Hu => conj (proj1 (w_slots _ Hw t d Hd (or_introl (conj Hp Hk)) u Hu) Hk) (proj2 (w_slots _ Hw t d Hd (or_introl (conj Hp Hk)) u Hu))) Hwk) as ((Hc1&Hw1&St1)&A1&Hall).
      destruct (at_rise _ _ _ _ _ A1 t d Hrnt Hd) as (d1&Hd1&Hr1).
      assert (Ha1 : aS T lim s1).
      { split_and!; [done|done|]. destruct St1 as [S1 S2 S3 S4 S5 S6 S7 S8]. pose proof (st_low _ _ _ St) as Sl. split; try done.
        intros m Hm0' Hpm. rewrite (at_running _ _ _ _ _ A1) in Hm0'. rewrite (at_shapes _ _ _ _ _ A1) in Hpm. auto. }
      rewrite (bind_get_node _ _ _ _ Hd1) in H1.
      assert (Hk1 : n_kind d1 = KMemo) by (by rewrite (rise_kind _ _ Hr1)).
      assert (Hl1 : n_disposed d1 = false) by (pose proof Hr1 as (_&_&X&_); congruence).
      assert (Hp1 : pure_in (shapes s1) t) by (by rewrite (at_shapes _ _ _ _ _ A1)).
      assert (Hnr1 : t ∉ g_running s1) by (by rewrite (at_running _ _ _ _ _ A1)).
      destruct (decide (n_freshness d1 = Check)) as [Hc1'|Hc1'].
      + rewrite bind_m_modify in H1. unfold modify_node in H1.
        destruct (ghost_resolved_fields t s1) as (_&F2&_). rewrite F2, Hd1 in H1. injection H1 as <-.
        assert (Hq1 : forall u, u ∈ (slot_view (g_edges s1) t).1 -> quiet_or_gone s1 u).
        { intros u Hu. apply list_elem_of_lookup in Hu as [j Hj]. unfold slot_view in Hj. simpl in Hj.
          rewrite list_lookup_fmap in Hj. destruct (source_slots (sources_of (g_edges s1) t) !! j) as [[v mh]|] eqn:Es; [|done].
          simpl in Hj. injection Hj as ->. exact (Hall d1 j u mh Hd1 Hc1' Es ltac:(lia)). }
        pose proof Ha1 as (_&_&St1').
        destruct (resolve_step T lim t s1 d1 (proj1 (proj2 Hc1)) Hw1 St1' Hlt Hp1 Hd1 Hk1 Hl1 Hc1' Hnr1 Hq1) as (Hw2&St2&A2).
        set (s2' := set_node t _ _) in *.
        rewrite <- ?Hm2 in *.
        assert (Hd2 : g_nodes s2' !! t = Some (node_with_unannounced false (node_with_freshness Clean d1))) by (subst s2'; simpl; apply lookup_insert_eq).
        rewrite (bind_get_node _ _ _ _ Hd2) in Hm2. simpl in Hm2. unfold m_return in Hm2. injection Hm2 as <- <-.
        split_and!; [split_and!; done|eapply atom_trans; [apply Hmono; exact A1|exact A2]|split].
        * eexists. split; [exact Hd2|left; by left].
        * intros d0 Hd0 Hq0. eexists. split; [exact Hd2|left; done].
      + unfold m_return in H1. injection H1 as <-.
        refine (Hlast s1 d1 Ha1 (Hmono _ _ A1) Hd1 Hk1 Hl1 Hc1' _ Hm2).
        intros Hq0. exfalso. apply Hc1'. destruct Hq0 as [Kq|Kq]; [congruence|].
        assert (Hfz := at_frozen _ _ _ _ _ A1 t d Hrnt Hd Hp (fun K => Hnr (st_T _ _ _ St t K)) (or_intror Kq)).
        rewrite Hfz in Hd1. injection Hd1 as <-. done.
    - rewrite bind_m_return in Hm.
      assert (Hnc : n_freshness d <> Check) by done.
      refine (Hlast s d Ha (atom_refl _ _ _ _) Hd Hk Hl Hnc _ Hm). intros Hq0. by right.
  Qed.

  Lemma slot_view_lookup (w : EdgeWorld) c j u mh : slot_at w c j = Some (u, mh) -> (slot_view w c).1 !! j = Some u.
  Proof. intros H. unfold slot_view, slot_at in *. simpl. by rewrite list_lookup_fmap, H. Qed.

  Lemma Awalk_S : forall (T : node_id -> Prop) (lim c : node_id) i s s' a, aS T lim s -> ~ rn s lim None c -> c ∉ g_running s ->
    (forall u, u ∈ (slot_view (g_edges s) c).1 -> u < lim /\ pure_in (shapes s) u) ->
    resolve_walk (S f) c i s = Some (s', a) ->
    aS T lim s' /\ atom T lim None s s' /\
    forall dc j u mh, g_nodes s' !! c = Some dc -> n_freshness dc = Check -> slot_at (g_edges s') c j = Some (u, mh) -> i <= j -> quiet_or_gone s' u.
  Proof.
    intros T lim c i s s' a Ha Hrn Hnr Hsl Hm. pose proof Ha as (Hc&Hw&St).
    cbn [resolve_walk] in Hm.
    destruct (g_nodes s !! c) as [d|] eqn:Hd; [|unfold m_bind, get_node in Hm; by rewrite Hd in Hm].
    rewrite (bind_get_node _ _ _ _ Hd), bind_get in Hm.
    destruct (decide (n_freshness d = Check)) as [Hch|Hch].
    - destruct (source_slots (sources_of (g_edges s) c) !! i) as [[src mh]|] eqn:Es.
      + apply bind_inv in Hm as (s1 & a1 & Hu & Hm).
        assert (Hsrc : src ∈ (slot_view (g_edges s) c).1) by (exact (slot_at_in _ _ _ _ _ Es)).
        destruct (Hsl src Hsrc) as [Hlt Hps].
        destruct (Aupd _ IH T lim src s s1 a1 Ha Hlt Hps Hu) as (Ha1&A1&D1).
        pose proof (at_shapes _ _ _ _ _ A1) as Hsh1.
        assert (Hrn1 : ~ rn s1 lim None c) by (apply (rn_mono s s1 lim lim None c Hsh1); [lia|done]).
        destruct (at_slots _ _ _ _ _ A1 c Hrn) as [Hv1 _].
        destruct (Awalk _ IH T lim c (S i) s1 s' a Ha1 Hrn1 ltac:(by rewrite (at_running _ _ _ _ _ A1))
                   ltac:(intros u Hu'; rewrite Hv1 in Hu'; rewrite Hsh1; auto) Hm) as (Ha2&A2&Hall).
        split_and!; [done|by eapply atom_trans|].
        intros dc j u mh' Hdc Hfc Hsj Hij. destruct (decide (j = i)) as [->|Hj]; [|apply (Hall dc j u mh' Hdc Hfc Hsj); lia].
        destruct (at_slots _ _ _ _ _ A2 c Hrn1) as [Hv2 _].
        pose proof (slot_view_lookup _ _ _ _ _ Hsj) as Hl1. rewrite Hv2, Hv1 in Hl1.
        rewrite (slot_view_lookup _ _ _ _ _ Es) in Hl1. injection Hl1 as <-.
        pose proof Ha1 as (_&_&St1).
        assert (HTs : ~ T src).
        { intros K. pose proof (st_low _ _ _ St1 src (st_T _ _ _ St1 src K) ltac:(by rewrite Hsh1)). lia. }
        refine (qog_atom T lim None s1 s' src A2 ltac:(by rewrite Hsh1) HTs (proj1 D1)).
      + unfold m_return in Hm. injection Hm as <- <-. split_and!; [done|apply atom_refl|].
        intros dc j u mh Hdc Hfc Hsj Hij. unfold slot_at in Hsj.
        rewrite (lookup_ge_None_2 (source_slots (sources_of (g_edges s) c)) j) in Hsj; [done|].
        apply lookup_ge_None_1 in Es. lia.
    - unfold m_return in Hm. injection Hm as <- <-. split_and!; [done|apply atom_refl|].
      intros dc j u mh Hdc Hfc. rewrite Hd in Hdc. injection Hdc as <-. done.
  Qed.

  Lemma Aupd_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    update_if_necessary (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' a Ha Hlt Hp Hm. pose proof Ha as (Hc&Hw&St).
    destruct (pure_exists s t Hp) as [d Hd].
    cbn [update_if_necessary] in Hm. rewrite (bind_get_node _ _ _ _ Hd) in Hm.
    assert (Hquiet : stale_live d = false -> done_at t s s).
    { intros El. split.
      - exists d. split; [done|]. unfold stale_live in El. destruct (stale (n_freshness d)) eqn:Es; simpl in El.
        + right. destruct (n_disposed d); done.
        + left. left. destruct (n_freshness d); done.
      - intros d0 Hd0 _. rewrite Hd in Hd0. injection Hd0 as <-. exists d. split; [done|].
        unfold stale_live in El. destruct (stale (n_freshness d)) eqn:Es; simpl in El.
        + right. destruct (n_disposed d); done.
        + left. destruct (n_freshness d); done. }
    destruct (n_kind d) eqn:Ek.
    - unfold m_return in Hm. injection Hm as <- <-. split_and!; [done|apply atom_refl|apply Hquiet].
      destruct (b_window _ (proj1 (proj2 Hc))) as (_&_&W&_). unfold stale_live. rewrite (W t d Hd Ek). done.
    - destruct (stale_live d) eqn:El.
      + assert (Hl : n_disposed d = false) by (unfold stale_live in El; destruct (n_disposed d); [by rewrite andb_false_r in El|done]).
        exact (Arop _ IH T lim t s s' a d Ha Hlt Hp Hd Ek Hl Hm).
      + unfold m_return in Hm. injection Hm as <- <-. split_and!; [done|apply atom_refl|by apply Hquiet].
    - exfalso. exact (pure_kind s t d Hp Hd Ek).
  Qed.

  Lemma Arop_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    refresh_or_pull (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' a d Ha Hlt Hp Hd Hk Hl Hm. cbn [refresh_or_pull] in Hm. rewrite bind_get in Hm.
    pose proof Ha as (_&_&St).
    destruct (deferring s) eqn:Ed.
    - exact (Aref _ IH T lim t s s' a d Ha Hlt Hp Hd Hk Hl Hm).
    - destruct (st_defer _ _ _ St) as [K|K]; [congruence|].
      exact (Apull _ IH T lim t s s' a d Ha K Hlt Hp Hd Hk Hl Hm).
  Qed.

  Lemma settle_flushing g s s' a : g_flushing s = true -> settle_flush g s = Some (s', a) -> s' = s.
  Proof.
    intros Hf Hm. destruct g as [|g]; [done|]. cbn [settle_flush] in Hm. rewrite bind_get in Hm.
    destruct (_ && _ && _).
    - destruct g as [|g]; [done|]. cbn [run_flush] in Hm. rewrite bind_get, Hf in Hm. unfold m_return in Hm. by injection Hm as <-.
    - unfold m_return in Hm. by injection Hm as <-.
  Qed.

  Lemma core_batch s v : run_core s -> run_core (with_batch_depth v s).
  Proof.
    intros [Hi Hx]. split.
    - eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity.
    - eapply extras_same; [|exact Hx]. split; reflexivity.
  Qed.

  Lemma core_pull s v : run_core s -> run_core (with_pull_depth v s).
  Proof.
    intros [Hi Hx]. split.
    - eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity.
    - eapply extras_same; [|exact Hx]. split; reflexivity.
  Qed.

  Lemma atom_wrap (T : node_id -> Prop) lim x s s0 s1 s2 :
    atom T lim x s0 s1 -> same4 s s0 -> same4 s1 s2 -> g_trace s0 = g_trace s -> g_trace s2 = g_trace s1 -> g_owners s0 = g_owners s -> g_owners s2 = g_owners s1 ->
    g_current s2 = g_current s -> g_flags s2 = g_flags s -> (g_flush_owed s = true -> g_flush_owed s2 = true) ->
    g_batch_depth s2 = g_batch_depth s -> g_pull_depth s2 = g_pull_depth s -> g_untracked s2 = g_untracked s ->
    g_flushing s2 = g_flushing s -> atom T lim x s s2.
  Proof.
    intros A H0 H1 Tr0 Tr1 Ow0 Ow1 C F O B P U Fl.
    pose proof (same4_shapes _ _ H0) as Hs0. pose proof (same4_shapes _ _ H1) as Hs1.
    destruct H0 as (N0&E0&G0&R0). destruct H1 as (N1&E1&G1&R1).
    assert (Hrn : forall m, rn s lim x m <-> rn s0 lim x m) by (intros m; unfold rn; by rewrite Hs0).
    destruct A as [A1 A2 A3 A4 A5 A6 A7 A8 A9 A10 A11 A12 A13 A14 A15 A16 A17 A18].
    split; try done.
    - by rewrite Hs1, A1, Hs0.
    - by rewrite R1, A2, R0.
    - intros c Hc. rewrite <- G0. rewrite G1 in Hc. auto.
    - intros m d Hd. rewrite <- N0 in Hd. destruct (A11 m d Hd) as (d'&H&K). exists d'. rewrite N1. done.
    - intros m d' Hd'. rewrite N1 in Hd'. rewrite <- N0. exact (A12 m d' Hd').
    - intros m d Hm Hd. rewrite <- N0 in Hd. rewrite N1. refine (A13 m d _ Hd). unfold rn in *. by rewrite Hs0.
    - intros c Hc. rewrite E1, G1, <- E0, <- G0. refine (A14 c _). by rewrite <- Hrn.
    - intros n d Hd Hp HT Hq. rewrite <- N0 in Hd. rewrite <- Hs0 in Hp. rewrite N1. exact (A15 n d Hd Hp HT Hq).
    - intros n d Hn Hd Hp HT Hq. rewrite <- N0 in Hd. rewrite <- Hs0 in Hp. rewrite N1. refine (eq_trans (A16 n d _ Hd Hp HT Hq) _); [unfold rn in *; by rewrite Hs0|done].
    - intros m Hm. rewrite Tr1, <- Tr0. refine (A17 m _). by rewrite <- Hrn.
    - by rewrite Ow1, A18, Ow0.
  Qed.

  Lemma Apull_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> g_flushing s = true -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    pull (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' a d (Hc&Hw&St) Hfl Hlt Hp Hd Hk Hl Hm. cbn [pull] in Hm. rewrite bind_m_modify in Hm.
    set (s0 := with_pull_depth (S (g_pull_depth s)) s) in Hm.
    assert (H0 : same4 s s0) by done.
    apply bind_inv in Hm as (s1 & a1 & Hr & Hm). rewrite bind_m_modify, bind_get in Hm.
    assert (Ha0 : aS T lim s0).
    { split_and!; [by apply core_pull|exact (whole_same _ _ H0 Hw)|].
      refine (stk_same T lim s s0 H0 _ St). intros _. right. done. }
    destruct (Aref _ IH T lim t s0 s1 a1 d Ha0 Hlt ltac:(by rewrite (same4_shapes _ _ H0)) Hd Hk Hl Hr) as ((Hc1&Hw1&St1)&A1&D1).
    set (s2 := with_pull_depth (pred (g_pull_depth s1)) s1) in Hm.
    assert (H1 : same4 s1 s2) by done.
    assert (Hfl2 : g_flushing s2 = true) by (change (g_flushing s1 = true); rewrite (at_flushing _ _ _ _ _ A1); done).
    assert (Hend : s' = s2).
    { destruct (_ && _).
      - symmetry. eapply (eq_sym (settle_flushing f s2 s' a Hfl2 Hm)).
      - unfold m_return in Hm. by injection Hm as <-. }
    subst s'.
    assert (Hpl : g_pull_depth s2 = g_pull_depth s).
    { change (pred (g_pull_depth s1) = g_pull_depth s). rewrite (at_pull _ _ _ _ _ A1). done. }
    assert (Hend2 : aS T lim s2).
    { split_and!; [by apply core_pull|exact (whole_same _ _ H1 Hw1)|].
      refine (stk_same T lim s1 s2 H1 _ St1). intros _. by right. }
    split_and!; [done| |].
    - eapply atom_wrap; [exact A1|exact H0|exact H1|reflexivity|reflexivity|reflexivity|reflexivity|..]; (try exact Hpl); simpl.
      all: first [intros Ho0; exact (at_owed _ _ _ _ _ A1 Ho0)|rewrite (at_current _ _ _ _ _ A1); done|rewrite (at_flags _ _ _ _ _ A1); done|rewrite (at_owed _ _ _ _ _ A1); done|rewrite (at_batch _ _ _ _ _ A1); done|rewrite (at_untracked _ _ _ _ _ A1); done|rewrite (at_flushing _ _ _ _ _ A1); done].
    - destruct D1 as [D2 D3]. split; [exact (qog_same _ _ _ H1 D2)|].
      intros d0 Hd0 Hq0. destruct (D3 d0 Hd0 Hq0) as (d'&Hd'&K). exists d'. split; [|done]. done.
  Qed.

  Lemma qc_shapes s s' c d d' :
    shapes s' = shapes s -> g_nodes s !! c = Some d -> g_nodes s' !! c = Some d' -> qc s' c d' -> qc s c d.
  Proof.
    intros Hs Hd Hd' Hq.
    assert (Hsh : shape_of d' = shape_of d).
    { pose proof (shapes_lookup s c d Hd) as A. pose proof (shapes_lookup s' c d' Hd') as B. rewrite Hs, A in B. congruence. }
    unfold shape_of in Hsh. injection Hsh as K Q E B. unfold qc, quiet_effect in *. rewrite Hs in Hq.
    destruct Hq as [[H1 H2]|[H1 H2]]; [left; split; congruence|right]. rewrite E, B in H2. split; congruence.
  Qed.

  Lemma A_tail (T : node_id -> Prop) (lim t : node_id) s s' e g :
    run_extras s -> whole s -> stk T lim s -> t < lim -> pure_in (shapes s) t -> g_nodes s !! t = Some e ->
    quiet e \/ n_disposed e = true ->
    (forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc -> n_kind dc = KMemo -> t < c) ->
    (forall c, g_current s = Some c -> ysrc T s c \/ n_freshness e = Clean \/ n_disposed e = true) ->
    (let* _ := track t in let* d' := get_node t in if stale_live d' then mark_reader g else m_return tt) s = Some (s', tt) ->
    whole s' /\ stk T lim s' /\ atom T lim (g_current s) s s' /\
    (exists e', g_nodes s' !! t = Some e' /\ read_view_of e' = read_view_of e /\ (quiet e' \/ n_disposed e' = true)) /\
    (forall c dc, g_current s = Some c -> g_nodes s !! c = Some dc -> qc s c dc ->
      t ∈ read_prefix (g_edges s') c /\ rd_ok s' c t /\
      (forall y, y ∈ read_prefix (g_edges s) c -> y ∈ read_prefix (g_edges s') c)) /\
    g_trace s' = g_trace s.
  Proof.
    intros Hx Hw St Hlt Hp He Hq Hbelow Hmode Hm.
    apply bind_inv in Hm as (s1 & [] & Htr & Hm).
    destruct (A_track T lim t s s1 e Hx Hw St Hlt Hp He Hq Hbelow Htr) as (Hb1&Hw1&St1&A1&Rf).
    pose proof (track_nodes _ _ _ Htr) as Hn1.
    assert (He1 : g_nodes s1 !! t = Some e) by (by rewrite Hn1).
    assert (HTt : ~ T t).
    { intros K. pose proof (st_low _ _ _ St t (st_T _ _ _ St t K) Hp). lia. }
    rewrite (bind_get_node _ _ _ _ He1) in Hm.
    destruct (stale_live e) eqn:El.
    - unfold mark_reader in Hm. rewrite (at_current _ _ _ _ _ A1) in Hm.
      destruct (g_current s) as [c|] eqn:Ec.
      + rewrite (b_flag_r _ Hb1) in Hm. destruct (mark_check g c s1) as [s2|] eqn:Em; [|done]. injection Hm as <-.
        assert (Hys : ysrc T s1 c).
        { destruct (Hmode c eq_refl) as [K|[K|K]].
          - intros Hpc. apply K. by rewrite <- (at_shapes _ _ _ _ _ A1).
          - exfalso. unfold stale_live in El. rewrite K in El. done.
          - exfalso. unfold stale_live in El. rewrite K, andb_false_r in El. done. }
        assert (Htc : ytouch T s1 s2).
        { refine (ytouch_check T s1 (stk_HC T lim s1 Hb1 Hw1 St1) g c s1 s2 (ytouch_refl T s1) _ Em).
          intros Hpc. left. by apply Hys. }
        destruct (whole_touch T lim s1 s2 Hw1 St1 Htc) as [Hw2 St2].
        pose proof (atom_touch T lim (Some c) s1 s2 Htc) as A2.
        assert (Htr1 : g_trace s1 = g_trace s) by (rewrite (track_trace _ _ _ Htr); done).
        split_and!; [done|done|by eapply atom_trans|..].
        * destruct (at_keep _ _ _ _ _ A2 t e He1 ltac:(by rewrite (at_shapes _ _ _ _ _ A1)) HTt Hq) as (e'&He'&V&Q&_).
          exists e'. done.
        * intros c0 dc Hc0 Hdc Hqc. destruct (Rf c0 dc Hc0 Hdc Hqc) as (R1&R2&R3). pose proof (proj1 Htc) as R.
          rewrite (rs_edges _ _ R). split_and!; [done|exact (rd_rise _ _ _ _ R R2)|done].
        * rewrite (rs_trace _ _ (proj1 Htc)). exact Htr1.
      + unfold m_bind in Hm. injection Hm as <-. split_and!; [done|done|done| |done|exact (track_trace _ _ _ Htr)]. exists e. split_and!; done.
    - unfold m_return in Hm. injection Hm as <-. split_and!; [done|done|done| |done|exact (track_trace _ _ _ Htr)]. exists e. split_and!; done.
  Qed.

  Lemma read_answer d s s' o :
    (match n_status d with
     | StError => m_return (Fail (default 0%Z (n_failure d)))
     | _ => m_return (Ok (n_value d))
     end) s = Some (s', o) -> s' = s /\ o = view_answer (read_view_of d).
  Proof. unfold m_return, view_answer, read_view_of. simpl. destruct (n_status d); intros H; by injection H as <- <-. Qed.

  Lemma Anode_S : forall (T : node_id -> Prop) (lim t : node_id) s s' o, rd_pre T lim t s -> read_node (S f) t s = Some (s', o) -> rd_post T lim t s s' o.
  Proof.
    intros T lim t s s' o (Ha&Hlt&Hp&Hbelow&Hmode) Hm0. pose proof Ha as (Hc&Hw&St).
    pose proof (Rread_node _ (runs_ok_all (S f)) t _ _ _ Hc I Hm0) as [Hc' _].
    destruct (pure_exists s t Hp) as [d Hd].
    pose proof Hm0 as Hm. cbn [read_node] in Hm. rewrite (bind_get_node _ _ _ _ Hd) in Hm.
    assert (HTt : ~ T t).
    { intros K. pose proof (st_low _ _ _ St t (st_T _ _ _ St t K) Hp). lia. }
    destruct (n_kind d) eqn:Ek.
    - apply bind_inv in Hm as (s1 & [] & Htr & Hm). unfold m_return in Hm. injection Hm as <- <-.
      destruct (b_window _ (proj1 (proj2 Hc))) as (_&_&W&_).
      destruct (A_track T lim t s s1 d (proj2 Hc) Hw St Hlt Hp Hd ltac:(left; left; exact (W t d Hd Ek)) Hbelow Htr) as (_&Hw1&St1&A1&Rf).
      split_and!; [split_and!; done|done| |done|intros m _; by rewrite (track_trace _ _ _ Htr)].
      exists d. rewrite (track_nodes _ _ _ Htr). split_and!; [done| |left; left; exact (W t d Hd Ek)].
      unfold view_answer, read_view_of. simpl. rewrite (proj1 (w_signal _ Hw t d Hd Ek)). done.
    - apply bind_inv in Hm as (s2 & [] & H1 & Hm).
      assert (Hfin : forall e2, g_nodes s2 !! t = Some e2 -> s' = s2 /\ o = view_answer (read_view_of e2)).
      { intros e2 He2. rewrite (bind_get_node _ _ _ _ He2) in Hm. exact (read_answer _ _ _ _ Hm). }
      destruct (stale_live d) eqn:El.
      + assert (Hl : n_disposed d = false) by (unfold stale_live in El; destruct (n_disposed d); [by rewrite andb_false_r in El|done]).
        apply bind_inv in H1 as (s1 & a1 & Hrp & H1).
        destruct (Arop _ IH T lim t s s1 a1 d Ha Hlt Hp Hd Ek Hl Hrp) as ((Hc1&Hw1&St1)&A1&[D1 D2]).
        destruct D1 as (e1&He1&Hq1).
        pose proof (at_shapes _ _ _ _ _ A1) as Hsh.
        assert (Hcr : g_current s1 = g_current s) by exact (at_current _ _ _ _ _ A1).
        assert (Hrunc : forall c, g_current s = Some c -> c ∈ g_running s) by exact (b_current _ (proj1 (proj2 Hc))).
        assert (Hex : forall c, g_current s = Some c -> exists d0, g_nodes s !! c = Some d0).
        { intros c Hcc. destruct (ci_running' s c (Hrunc c Hcc) (b_running_kind _ (proj1 (proj2 Hc)))) as [d0 Hd0]. eauto. }
        assert (Hbelow1 : forall c dc, g_current s1 = Some c -> g_nodes s1 !! c = Some dc -> qc s1 c dc -> n_kind dc = KMemo -> t < c).
        { intros c dc Hcc Hdc Hq Hkc. rewrite Hcr in Hcc. destruct (Hex c Hcc) as [d0 Hd0].
          pose proof (shapes_lookup s c d0 Hd0) as Y1. pose proof (shapes_lookup s1 c dc Hdc) as Y2. rewrite Hsh, Y1 in Y2. unfold shape_of in Y2. injection Y2 as Y4 _ _ _.
          refine (Hbelow c d0 Hcc Hd0 (qc_shapes s s1 c d0 dc Hsh Hd0 Hdc Hq) _). congruence. }
        assert (Hmode1 : forall c, g_current s1 = Some c -> ysrc T s1 c \/ n_freshness e1 = Clean \/ n_disposed e1 = true).
        { intros c Hcc. rewrite Hcr in Hcc. destruct (Hmode c Hcc) as [K|K].
          - left. intros Hpc. apply K. by rewrite <- Hsh.
          - right. destruct K as (e0&He0&Hq0). rewrite Hd in He0. injection He0 as <-.
            destruct Hq0 as [Q|Q]; [|congruence].
            destruct (D2 d Hd Q) as (d'&Hd'&K2). rewrite He1 in Hd'. injection Hd' as <-. exact K2. }
        destruct (A_tail T lim t s1 s2 e1 f (proj2 Hc1) Hw1 St1 Hlt ltac:(by rewrite Hsh) He1 Hq1 Hbelow1 Hmode1 H1) as (Hw2&St2&A2&(e2&He2&V2&Q2)&Rf&Ht2).
        destruct (Hfin e2 He2) as [-> ->].
        split_and!.
        * split_and!; done.
        * eapply atom_trans; [apply (atom_mono T lim lim None (g_current s) s s1); [lia|intros m Hm1; discriminate Hm1|exact A1]|]. by rewrite Hcr in A2.
        * exists e2. done.
        * intros c dc Hcc Hdc Hq.
          assert (Hrnc : ~ rn s lim None c).
          { intros [[Hpc Hlc]|K]; [|done]. pose proof (st_low _ _ _ St c (Hrunc c Hcc) Hpc). lia. }
          destruct (at_rise _ _ _ _ _ A1 c dc Hrnc Hdc) as (dc1&Hdc1&_).
          destruct (at_slots _ _ _ _ _ A1 c Hrnc) as [Hv1 _].
          destruct (Rf c dc1 ltac:(by rewrite Hcr) Hdc1 (qc_shapes s1 s c dc1 dc (eq_sym Hsh) Hdc1 Hdc Hq)) as (R1&R2&R3).
          split_and!; [done|done|]. intros y Hy. apply R3. rewrite read_prefix_view, Hv1. by rewrite read_prefix_view in Hy.
        * intros m Hm1. rewrite Ht2. refine (at_view _ _ _ _ _ A1 m _). intros [K|K]; [exact (Hm1 K)|done].
      + unfold stale_live in El.
        assert (Hq0 : quiet d \/ n_disposed d = true).
        { destruct (stale (n_freshness d)) eqn:Es; simpl in El.
          - right. destruct (n_disposed d); done.
          - left. left. destruct (n_freshness d); done. }
        destruct (A_track T lim t s s2 d (proj2 Hc) Hw St Hlt Hp Hd Hq0 Hbelow H1) as (_&Hw2&St2&A2&Rf).
        destruct (Hfin d ltac:(by rewrite (track_nodes _ _ _ H1))) as [-> ->].
        split_and!; [split_and!; done|done| |done|intros m _; by rewrite (track_trace _ _ _ H1)].
        exists d. rewrite (track_nodes _ _ _ H1). split_and!; done.
    - exfalso. exact (pure_kind s t d Hp Hd Ek).
  Qed.

  Lemma Aread_S : forall (T : node_id -> Prop) (lim t : node_id) s s' o, rd_pre T lim t s -> read (S f) t true s = Some (s', o) -> rd_post T lim t s s' o.
  Proof. intros T lim t s s' o H Hm. cbn [read] in Hm. exact (Anode _ IH T lim t s s' o H Hm). Qed.

  Lemma guide_atom (T : node_id -> Prop) lim x s s' A : atom T lim x s s' -> guide T s A -> guide T s' A.
  Proof.
    intros At Hg t o Ho. destruct (Hg t o Ho) as (Hp&HT&e&He&Hq&Hv).
    destruct (at_keep _ _ _ _ _ At t e He Hp HT Hq) as (e'&He'&V&Q&_).
    split_and!; [by rewrite (at_shapes _ _ _ _ _ At)|done|]. exists e'. split_and!; [done|done|by rewrite V].
  Qed.

  (** A read of [c] that went through [read_node] or a log keeps what [c] has read. *)
  Lemma seen_keep (T : node_id -> Prop) (L c : node_id) x s s1 d d1 :
    aS T L s -> aS T L s1 -> atom T L x s s1 -> c ∈ g_running s -> g_nodes s !! c = Some d -> g_nodes s1 !! c = Some d1 -> qc s c d ->
    below_or_all L c d s ->
    (forall y, y ∈ read_prefix (g_edges s) c -> y ∈ read_prefix (g_edges s1) c) ->
    forall t o, seen_ans s c t = Some o -> seen_ans s1 c t = Some o.
  Proof.
    intros (Hc&Hw&St) (Hc1&Hw1&St1) A Hrun Hd Hd1 Hq HL Hgrow t o Ho.
    pose proof (at_shapes _ _ _ _ _ A) as Hsh.
    pose proof (seen_ans_prefix _ _ _ _ Ho) as Hpre.
    assert (Hsl : t ∈ (slot_view (g_edges s) c).1) by (rewrite read_prefix_view in Hpre; by apply elem_take in Hpre).
    destruct (w_slots _ Hw c d Hd Hq t Hsl) as [Hlt0 Hpt].
    assert (Hlt : t < L).
    { destruct HL as [[Hk ->]|[_ Ha]]; [exact (Hlt0 Hk)|exact (Ha t (pure_exists s t Hpt))]. }
    assert (Hnr : t ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St t Hin Hpt); lia).
    destruct (st_read _ _ _ St c d Hrun Hd Hq t Hpre Hnr) as [Hrd (e&He&Hqe)].
    rewrite (seen_ans_view s c t e Hpre Hrd He) in Ho. injection Ho as <-.
    assert (HT : ~ T t) by (intros K; exact (Hnr (st_T _ _ _ St t K))).
    destruct (at_keep _ _ _ _ _ A t e He Hpt HT Hqe) as (e1&He1&V&_).
    assert (Hq1 : qc s1 c d1) by exact (qc_shapes s1 s c d1 d (eq_sym Hsh) Hd1 Hd Hq).
    assert (Hrun1 : c ∈ g_running s1) by (by rewrite (at_running _ _ _ _ _ A)).
    assert (Hpre1 := Hgrow t Hpre).
    destruct (st_read _ _ _ St1 c d1 Hrun1 Hd1 Hq1 t Hpre1 ltac:(by rewrite (at_running _ _ _ _ _ A))) as [Hrd1 _].
    rewrite (seen_ans_view s1 c t e1 Hpre1 Hrd1 He1), V. done.
  Qed.

  (** One read of a body, up to its result. *)
  Lemma exec_read (T : node_id -> Prop) (L c : node_id) d s s1 o (t : node_id) :
    aS T L s -> g_current s = Some c -> g_nodes s !! c = Some d -> qc s c d -> below_or_all L c d s -> t < L -> pure_in (shapes s) t ->
    (T c \/ quiet_or_gone s t) ->
    read f t true s = Some (s1, o) ->
    aS T L s1 /\ atom T L (Some c) s s1 /\ g_current s1 = Some c /\
    (exists d1, g_nodes s1 !! c = Some d1 /\ qc s1 c d1 /\ below_or_all L c d1 s1) /\
    seen_ans s1 c t = Some o /\
    (forall u x, seen_ans s c u = Some x -> seen_ans s1 c u = Some x) /\
    (forall e, g_nodes s !! t = Some e -> quiet e \/ n_disposed e = true -> ~ T t -> view_answer (read_view_of e) = o) /\
    trace_view c (g_trace s1) = trace_view c (g_trace s).
  Proof.
    intros Ha Hcur Hd Hq HL Hlt Hp Hor Hrd.
    assert (Hpre : rd_pre T L t s).
    { split_and!; [done|done|done|intros c0 dc Hc0; rewrite Hcur in Hc0; injection Hc0 as <-; intros Hdc _ Hkc; rewrite Hd in Hdc; injection Hdc as <-; destruct HL as [[_ ->]|[K _]]; [done|congruence]|].
      intros c0 Hc0. rewrite Hcur in Hc0. injection Hc0 as <-. destruct Hor as [K|K]; [left; intros _; done|by right]. }
    destruct (Aread _ IH T L t s s1 o Hpre Hrd) as (Ha1&A1&(e1&He1&Ho&Hq1)&Rf&Vk).
    rewrite Hcur in A1.
    pose proof (at_shapes _ _ _ _ _ A1) as Hsh.
    destruct (Rf c d Hcur Hd Hq) as (R1&R2&R3).
    destruct (at_any _ _ _ _ _ A1 c d Hd) as (d1&Hd1&_).
    pose proof Ha as (Hc&Hw&St).
    assert (Hrun : c ∈ g_running s) by exact (b_current _ (proj1 (proj2 Hc)) c Hcur).
    split_and!.
    - done.
    - done.
    - rewrite (at_current _ _ _ _ _ A1). done.
    - exists d1. split_and!; [done|exact (qc_shapes s1 s c d1 d (eq_sym Hsh) Hd1 Hd Hq)|exact (below_shapes L c d d1 s s1 Hsh Hd Hd1 HL)].
    - rewrite (seen_ans_view s1 c t e1 R1 R2 He1). by rewrite Ho.
    - exact (seen_keep T L c (Some c) s s1 d d1 Ha Ha1 A1 Hrun Hd Hd1 Hq HL R3).
    - intros e He Hqe HT. destruct (at_keep _ _ _ _ _ A1 t e He Hp HT Hqe) as (e'&He'&V&_).
      rewrite He1 in He'. injection He' as <-. rewrite Ho, V. done.
    - apply Vk. intros [K1 K2]. destruct HL as [[_ ->]|[K3 _]]; [lia|exact (pure_kind s c d K1 Hd K3)].
  Qed.

  Lemma note_body t o s : (exists c, g_current s = Some c) -> note_host_read t o s = Some (s, tt).
  Proof. intros [c Hc]. unfold note_host_read. rewrite Hc. done. Qed.

  Lemma guide_same (T : node_id -> Prop) s s' A : same4 s s' -> guide T s A -> guide T s' A.
  Proof.
    intros H Hg t o Ho. pose proof (same4_shapes _ _ H) as Hs. destruct H as (N&_). rewrite Hs, N. exact (Hg t o Ho).
  Qed.

  Lemma req_body g c s s' a :
    g_current s = Some c -> request_flush g s = Some (s', a) -> s' = s \/ s' = with_flush_owed true s.
  Proof.
    intros Hc E. destruct g as [|g]; [done|]. cbn [request_flush] in E. rewrite bind_get in E.
    destruct (live_queue s && _); [|unfold m_return in E; injection E as <- _; by left].
    assert (Ho : outside_bodies s = false) by (unfold outside_bodies; rewrite Hc; apply andb_false_r || (rewrite !andb_false_iff; left; right; done)).
    rewrite Ho in E. destruct (g_flushing s); unfold m_return, m_modify in E; injection E as <- _; [by left|by right].
  Qed.

  Lemma Aexec_S : forall (T : node_id -> Prop) (L c : node_id) d e' e cm s s' r A r0,
    aS T L s -> g_current s = Some c -> g_nodes s !! c = Some d -> qc s c d -> env_agree e' e ->
    below_or_all L c d s ->
    body_pure (fun t => readable (shapes s) t -> t < L /\ pure_in (shapes s) t) e cm ->
    (~ T c -> body_eval A e cm = Some r0 /\ guide T s A) ->
    (T c \/ (~ T c /\ pure_in (shapes s) c)) ->
    exec (S f) e' cm s = Some (s', r) ->
    aS T L s' /\ atom T L (Some c) s s' /\ body_eval (seen_ans s' c) e cm = Some r /\ (~ T c -> r = r0) /\
    (forall t o, seen_ans s c t = Some o -> seen_ans s' c t = Some o) /\
    (exists l, body_logs (seen_ans s' c) e cm = Some l /\ trace_view c (g_trace s') = trace_view c (g_trace s) ++ (EvLog c <$> l)).
  Proof.
    intros T L c d e' e cm s s' r A r0 Ha Hcur Hd Hq Hag HL Hbp Hg Hor Hm.
    assert (Hdone : s' = s -> body_eval A e cm = Some r \/ T c -> body_eval (seen_ans s c) e cm = Some r ->
              body_logs (seen_ans s c) e cm = Some [] ->
              aS T L s' /\ atom T L (Some c) s s' /\ body_eval (seen_ans s' c) e cm = Some r /\ (~ T c -> r = r0) /\
              (forall t o, seen_ans s c t = Some o -> seen_ans s' c t = Some o) /\
              (exists l, body_logs (seen_ans s' c) e cm = Some l /\ trace_view c (g_trace s') = trace_view c (g_trace s) ++ (EvLog c <$> l))).
    { intros -> Hr0 Hb Hl. split_and!; [done|apply atom_refl|done| |done|].
      - intros HT. destruct (Hg HT) as [H1 _]. destruct Hr0 as [H2|H2]; [congruence|done].
      - exists []. split; [done|]. by rewrite app_nil_r. }
    destruct cm as [x|y|x v k|v a b|x n k|x ok n k|x n k|n v k|x v k|x mo q b k|x b k|b k|n k|b k|v k]; cbn [exec] in Hm; simpl in Hbp.
    - unfold on_value in Hm. rewrite (ev_of _ _ _ Hag Hbp) in Hm.
      destruct (ev e x) as [[z|y]|] eqn:Ev; unfold m_return in Hm; try done; injection Hm as <- <-.
      + apply Hdone; [done| |simpl; done|simpl; rewrite Ev; done]. destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done.
      + apply Hdone; [done| |simpl; done|simpl; rewrite Ev; done]. destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done.
    - unfold m_return in Hm. injection Hm as <- <-. apply Hdone; [done| |simpl; done|simpl; done].
      destruct Hor as [K|K]; [by right|]. left. done.
    - destruct Hbp as [Hv Hk]. unfold on_value in Hm. rewrite (ev_of _ _ _ Hag Hv) in Hm.
      destruct (ev e v) as [[z|y]|] eqn:Ev; try done.
      + destruct (Aexec _ IH T L c d _ _ k s s' r A r0 Ha Hcur Hd Hq (env_agree_cons x z _ _ Hag) HL (Hk z eq_refl)
                   ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev in H1; by split) Hor Hm) as (R1&R2&R3&R4&R5&R6).
        split_and!; try done; simpl; rewrite Ev; done.
      + injection Hm as <- <-. apply Hdone; [done| |simpl; rewrite Ev; done|simpl; rewrite Ev; done].
        destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done.
    - destruct Hbp as (Hv&Hab). unfold on_value in Hm. rewrite (ev_of _ _ _ Hag Hv) in Hm.
      destruct (ev e v) as [[z|y]|] eqn:Ev; try done.
      + destruct (Hab z eq_refl) as [Hb' Ha'].
        destruct (decide (z = 0%Z)) as [Hz|Hz].
        * destruct (Aexec _ IH T L c d _ _ b s s' r A r0 Ha Hcur Hd Hq Hag HL (Hb' Hz)
                     ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev, decide_True in H1 by done; by split) Hor Hm) as (R1&R2&R3&R4&R5&R6).
          split_and!; try done; simpl; rewrite Ev, decide_True by done; done.
        * destruct (Aexec _ IH T L c d _ _ a s s' r A r0 Ha Hcur Hd Hq Hag HL (Ha' Hz)
                     ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev, decide_False in H1 by done; by split) Hor Hm) as (R1&R2&R3&R4&R5&R6).
          split_and!; try done; simpl; rewrite Ev, decide_False by done; done.
      + injection Hm as <- <-. apply Hdone; [done| |simpl; rewrite Ev; done|simpl; rewrite Ev; done].
        destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done.
    - (* a tracked read *)
      destruct Hbp as (Hn&Ht&Hk). unfold on_node, on_value in Hm. rewrite (ev_of _ _ _ Hag Hn) in Hm.
      destruct (ev e n) as [[z|y]|] eqn:Ev; try done.
      2: { injection Hm as <- <-. apply Hdone; [done| |simpl; rewrite Ev; done|simpl; rewrite Ev; done].
           destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done. }
      destruct (decide (0 <= z)%Z) as [Hz|Hz]; [|done].
      apply bind_inv in Hm as (s1 & o & Hrd & Hm).
      destruct (Ht z eq_refl Hz (read_readable _ _ _ _ _ Hrd)) as [Hlt Hpt]. set (t := Z.to_nat z) in *.
      (* the answer the guide holds *)
      assert (Hguide : ~ T c -> exists o0, A t = Some o0 /\ guide T s A /\ pure_in (shapes s) t /\ ~ T t /\
                exists et, g_nodes s !! t = Some et /\ (quiet et \/ n_disposed et = true) /\ view_answer (read_view_of et) = o0).
      { intros K. destruct (Hg K) as [H1 H2]. simpl in H1. rewrite Ev, decide_True in H1 by done. fold t in H1.
        destruct (A t) as [o0|] eqn:EA; [|done]. exists o0. split_and!; [done|done|exact (proj1 (H2 _ _ EA))|exact (proj1 (proj2 (H2 _ _ EA)))|exact (proj2 (proj2 (H2 _ _ EA)))]. }
      assert (Hor1 : T c \/ quiet_or_gone s t).
      { destruct Hor as [K|[K _]]; [by left|right]. destruct (Hguide K) as (o0&_&_&_&_&et&He&Hq0&_). exists et. done. }
      destruct (exec_read T L c d s s1 o t Ha Hcur Hd Hq HL Hlt Hpt Hor1 Hrd) as (Ha1&A1&Hcur1&(d1&Hd1&Hq1&HL1)&Hse&Hkeep&Hans&Hv1).
      erewrite bind_some in Hm; [|exact (note_body t o s1 ltac:(eauto))].
      pose proof (at_shapes _ _ _ _ _ A1) as Hsh.
      assert (Ho0 : ~ T c -> A t = Some o).
      { intros K. destruct (Hguide K) as (o0&EA&_&_&HTt&et&He&Hq0&Hv). rewrite (Hans et He Hq0 HTt) in Hv. by rewrite <- Hv in EA. }
      destruct o as [v|y].
      + assert (Hbp1 : body_pure (fun t0 => readable (shapes s1) t0 -> t0 < L /\ pure_in (shapes s1) t0) ((x, v) :: e) k).
        { rewrite Hsh. apply Hk. }
        destruct (Aexec _ IH T L c d1 _ _ k s1 s' r A r0 Ha1 Hcur1 Hd1 Hq1 (env_agree_cons x v _ _ Hag) HL1 Hbp1
                   ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev, decide_True in H1 by done; fold t in H1; rewrite (Ho0 K) in H1; split; [done|exact (guide_atom _ _ _ _ _ _ A1 H2)])
                   ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Hsh]]) Hm) as (R1&R2&R3&R4&R5&(l&R6&R7)).
        split_and!; [done|eapply atom_trans; [exact A1|exact R2]| |done|auto|].
        * simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R3.
        * exists l. split; [|by rewrite R7, Hv1]. simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R6.
      + unfold m_return in Hm. injection Hm as <- <-. split_and!; [done|done| |intros K; destruct (Hg K) as [H1 _]; simpl in H1; rewrite Ev, decide_True in H1 by done; fold t in H1; rewrite (Ho0 K) in H1; congruence|done|].
        * simpl. rewrite Ev, decide_True by done. fold t. rewrite Hse. done.
        * exists []. split; [simpl; rewrite Ev, decide_True by done; fold t; rewrite Hse; done|by rewrite app_nil_r].
    - (* a tracked read that keeps a failure *)
      destruct Hbp as (Hn&Ht&Hk1&Hk2). unfold on_node, on_value in Hm. rewrite (ev_of _ _ _ Hag Hn) in Hm.
      destruct (ev e n) as [[z|y]|] eqn:Ev; try done.
      2: { injection Hm as <- <-. apply Hdone; [done| |simpl; rewrite Ev; done|simpl; rewrite Ev; done].
           destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done. }
      destruct (decide (0 <= z)%Z) as [Hz|Hz]; [|done].
      apply bind_inv in Hm as (s1 & o & Hrd & Hm).
      destruct (Ht z eq_refl Hz (read_readable _ _ _ _ _ Hrd)) as [Hlt Hpt]. set (t := Z.to_nat z) in *.
      assert (Hguide : ~ T c -> exists o0, A t = Some o0 /\ guide T s A /\ pure_in (shapes s) t /\ ~ T t /\
                exists et, g_nodes s !! t = Some et /\ (quiet et \/ n_disposed et = true) /\ view_answer (read_view_of et) = o0).
      { intros K. destruct (Hg K) as [H1 H2]. simpl in H1. rewrite Ev, decide_True in H1 by done. fold t in H1.
        destruct (A t) as [o0|] eqn:EA; [|done]. exists o0. split_and!; [done|done|exact (proj1 (H2 _ _ EA))|exact (proj1 (proj2 (H2 _ _ EA)))|exact (proj2 (proj2 (H2 _ _ EA)))]. }
      assert (Hor1 : T c \/ quiet_or_gone s t).
      { destruct Hor as [K|[K _]]; [by left|right]. destruct (Hguide K) as (o0&_&_&_&_&et&He&Hq0&_). exists et. done. }
      destruct (exec_read T L c d s s1 o t Ha Hcur Hd Hq HL Hlt Hpt Hor1 Hrd) as (Ha1&A1&Hcur1&(d1&Hd1&Hq1&HL1)&Hse&Hkeep&Hans&Hv1).
      erewrite bind_some in Hm; [|exact (note_body t o s1 ltac:(eauto))].
      pose proof (at_shapes _ _ _ _ _ A1) as Hsh.
      assert (Ho0 : ~ T c -> A t = Some o).
      { intros K. destruct (Hguide K) as (o0&EA&_&_&HTt&et&He&Hq0&Hv). rewrite (Hans et He Hq0 HTt) in Hv. by rewrite <- Hv in EA. }
      destruct o as [v|y].
      + assert (Hbp1 : body_pure (fun t0 => readable (shapes s1) t0 -> t0 < L /\ pure_in (shapes s1) t0) ((ok, 1%Z) :: (x, v) :: e) k).
        { rewrite Hsh. apply Hk1. }
        destruct (Aexec _ IH T L c d1 _ _ k s1 s' r A r0 Ha1 Hcur1 Hd1 Hq1 (env_agree_cons ok _ _ _ (env_agree_cons x v _ _ Hag)) HL1 Hbp1
                   ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev, decide_True in H1 by done; fold t in H1; rewrite (Ho0 K) in H1; split; [done|exact (guide_atom _ _ _ _ _ _ A1 H2)])
                   ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Hsh]]) Hm) as (R1&R2&R3&R4&R5&(l&R6&R7)).
        split_and!; [done|eapply atom_trans; [exact A1|exact R2]| |done|auto|].
        * simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R3.
        * exists l. split; [|by rewrite R7, Hv1]. simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R6.
      + assert (Hbp1 : body_pure (fun t0 => readable (shapes s1) t0 -> t0 < L /\ pure_in (shapes s1) t0) ((ok, 0%Z) :: (x, y) :: e) k).
        { rewrite Hsh. apply Hk2. }
        destruct (Aexec _ IH T L c d1 _ _ k s1 s' r A r0 Ha1 Hcur1 Hd1 Hq1 (env_agree_cons ok _ _ _ (env_agree_cons x y _ _ Hag)) HL1 Hbp1
                   ltac:(intros K; destruct (Hg K) as [H1 H2]; simpl in H1; rewrite Ev, decide_True in H1 by done; fold t in H1; rewrite (Ho0 K) in H1; split; [done|exact (guide_atom _ _ _ _ _ _ A1 H2)])
                   ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Hsh]]) Hm) as (R1&R2&R3&R4&R5&(l&R6&R7)).
        split_and!; [done|eapply atom_trans; [exact A1|exact R2]| |done|auto|].
        * simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R3.
        * exists l. split; [|by rewrite R7, Hv1]. simpl. rewrite Ev, decide_True by done. fold t. rewrite (R5 _ _ Hse). exact R6.
    - done.
    - done.
    - done.
    - done.
    - done.
    - done.
    - done.
    - (* a batch in the body *)
      destruct Hbp as [Hbb Hbk]. rewrite bind_m_modify in Hm. set (s0 := with_batch_depth (S (g_batch_depth s)) s) in Hm.
      assert (H0 : same4 s s0) by done.
      assert (Hsh0 : shapes s0 = shapes s) by exact (same4_shapes _ _ H0).
      assert (Ha0 : aS T L s0).
      { destruct Ha as (Hc&Hw&St). split_and!; [by apply core_batch|exact (whole_same _ _ H0 Hw)|]. refine (stk_same T L s s0 H0 _ St). intros K. exact K. }
      apply bind_inv in Hm as (s1 & r1 & Hb1 & Hm). rewrite bind_m_modify in Hm.
      set (s2 := with_batch_depth (pred (g_batch_depth s1)) s1) in Hm.
      set (r01 := default (Ok 0%Z) (body_eval A e b)).
      assert (Hg0 : ~ T c -> body_eval A e b = Some r01 /\ guide T s0 A).
      { intros K. destruct (Hg K) as [H1 H2]. simpl in H1. split; [|exact (guide_same _ _ _ _ H0 H2)]. subst r01.
        destruct (body_eval A e b) as [x0|]; done. }
      destruct (Aexec _ IH T L c d _ _ b s0 s1 r1 A r01 Ha0 Hcur Hd (qc_same _ _ _ _ (ltac:(split_and!; done) : same4 s0 s) Hq) Hag (below_shapes L c d d s s0 Hsh0 Hd Hd HL)
                 ltac:(by rewrite Hsh0)
                 Hg0 ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Hsh0]]) Hb1) as (R1&R2&R3&R4&R5&(l1&R6&R7)).
      pose proof (at_shapes _ _ _ _ _ R2) as Sh1.
      assert (H1 : same4 s1 s2) by done.
      assert (Hcur2 : g_current s2 = Some c) by exact (eq_trans (at_current _ _ _ _ _ R2) Hcur).
      assert (Hb2 : g_batch_depth s2 = g_batch_depth s).
      { subst s2. simpl. rewrite (at_batch _ _ _ _ _ R2). reflexivity. }
      destruct R1 as (Hc1&Hw1&St1).
      assert (Ha2 : aS T L s2).
      { split_and!; [by apply core_batch|exact (whole_same _ _ H1 Hw1)|]. refine (stk_same T L s1 s2 H1 _ St1). intros K. exact K. }
      assert (A2 : atom T L (Some c) s s2).
      { eapply atom_wrap; [exact R2|exact H0|exact H1|reflexivity|reflexivity|reflexivity|reflexivity|..]; try exact Hb2.
        all: first [exact (at_current _ _ _ _ _ R2)|intros Ho0; exact (at_owed _ _ _ _ _ R2 Ho0)|exact (at_flags _ _ _ _ _ R2)|exact (at_pull _ _ _ _ _ R2)|exact (at_untracked _ _ _ _ _ R2)|exact (at_flushing _ _ _ _ _ R2)]. }
      assert (Hse2 : forall t o, seen_ans s1 c t = Some o -> seen_ans s2 c t = Some o) by (intros t o Ho; by rewrite (seen_ans_same _ _ _ H1)).
      assert (Hse0 : forall t o, seen_ans s c t = Some o -> seen_ans s0 c t = Some o) by (intros t o Ho; by rewrite (seen_ans_same _ _ _ H0)).
      assert (Hv2 : trace_view c (g_trace s2) = trace_view c (g_trace s) ++ (EvLog c <$> l1)) by exact R7.
      destruct r1 as [v|y].
      + (* the batch answered, and the body goes on *)
        cbv iota in Hm. apply bind_inv in Hm as (s3 & [] & Er & Hm).
        pose proof (req_body f c s2 s3 tt Hcur2 Er) as Hs3.
        assert (H3 : same4 s2 s3) by (destruct Hs3 as [ -> | -> ]; split_and!; done).
        assert (Hcur3 : g_current s3 = Some c) by (destruct Hs3 as [ -> | -> ]; exact Hcur2).
        assert (Hc3 : run_core s3) by exact (proj1 (Rrequest _ (runs_ok_all f) s2 s3 tt (proj1 Ha2) I Er)).
        assert (Ha3 : aS T L s3).
        { destruct Ha2 as (_&Hw2&St2). split_and!; [done|exact (whole_same _ _ H3 Hw2)|]. refine (stk_same T L s2 s3 H3 _ St2). intros _. left. unfold deferring. by rewrite Hcur3. }
        assert (A3 : atom T L (Some c) s2 s3).
        { destruct Hs3 as [ -> | -> ]; [apply atom_refl|]. apply atom_same; try done. }
        pose proof (atom_trans _ _ _ _ _ _ A2 A3) as A4. pose proof (at_shapes _ _ _ _ _ A4) as Sh4.
        destruct (at_any _ _ _ _ _ A4 c d Hd) as (d3&Hd3&_).
        assert (Hq3 : qc s3 c d3) by exact (qc_shapes s3 s c d3 d (eq_sym Sh4) Hd3 Hd Hq).
        assert (HL3 : below_or_all L c d3 s3) by exact (below_shapes L c d d3 s s3 Sh4 Hd Hd3 HL).
        assert (Hse3 : forall t o, seen_ans s2 c t = Some o -> seen_ans s3 c t = Some o) by (intros t o Ho; by rewrite (seen_ans_same _ _ _ H3)).
        destruct (Aexec _ IH T L c d3 _ _ k s3 s' r A r0 Ha3 Hcur3 Hd3 Hq3 Hag HL3 ltac:(by rewrite Sh4)
                   ltac:(intros K; destruct (Hg K) as [X3 X4]; simpl in X3; pose proof (R4 K) as X5; subst r01;
                         destruct (body_eval A e b) as [[v0|y0]|]; simpl in X5;
                         [split; [exact X3|exact (guide_atom _ _ _ _ _ _ A4 X4)]|discriminate X5|discriminate X3])
                   ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Sh4]]) Hm) as (Q1&Q2&Q3&Q4&Q5&(l2&Q6&Q7)).
        assert (Hup : forall t o, seen_ans s1 c t = Some o -> seen_ans s' c t = Some o) by (intros t o Ho; exact (Q5 _ _ (Hse3 _ _ (Hse2 _ _ Ho)))).
        pose proof (body_eval_mono _ _ _ _ _ Hup R3) as E1. pose proof (body_logs_mono _ _ _ _ _ Hup R6) as E2.
        split_and!; [done|eapply atom_trans; [exact A4|exact Q2]| |done|intros t o Ho; exact (Hup _ _ (R5 _ _ (Hse0 _ _ Ho)))|].
        * cbn [body_eval]. rewrite E1. exact Q3.
        * exists (l1 ++ l2). split; [cbn [body_logs]; rewrite E1, E2, Q6; reflexivity|].
          rewrite Q7, fmap_app, app_assoc. destruct Hs3 as [ -> | -> ]; exact (f_equal (fun x => x ++ (EvLog c <$> l2)) Hv2).
      + cbv iota in Hm. unfold m_return in Hm. injection Hm as <- <-.
        pose proof (body_eval_mono _ _ _ _ _ Hse2 R3) as E1. pose proof (body_logs_mono _ _ _ _ _ Hse2 R6) as E2.
        split_and!; [done|done| | |intros t o Ho; exact (Hse2 _ _ (R5 _ _ (Hse0 _ _ Ho)))|].
        * cbn [body_eval]. rewrite E1. reflexivity.
        * intros K. destruct (Hg K) as [X3 _]. simpl in X3. pose proof (R4 K) as X5. subst r01.
          destruct (body_eval A e b) as [[v0|y0]|]; simpl in X5; [discriminate X5|congruence|discriminate X3].
        * exists l1. split; [cbn [body_logs]; rewrite E1, E2; reflexivity|exact Hv2].
    - (* a log *)
      destruct Hbp as [Hv Hk]. unfold on_value in Hm. rewrite (ev_of _ _ _ Hag Hv) in Hm.
      destruct (ev e v) as [[z|y]|] eqn:Ev; try done.
      + unfold m_bind, log_label in Hm. rewrite Hcur in Hm. simpl in Hm.
        set (s1 := with_trace (g_trace s ++ [EvLog c z]) s) in Hm.
        assert (H1 : same4 s s1) by done.
        pose proof Ha as (Hc&Hw&St).
        assert (Hc1 : run_core s1).
        { refine (proj1 (ok2_emit (fun _ => True) (EvLog c z) s s1 tt Hc I _)). done. }
        assert (Ha1 : aS T L s1).
        { split_and!; [done|exact (whole_same _ _ H1 Hw)|exact (stk_same _ _ _ _ H1 ltac:(done) St)]. }
        assert (A1 : atom T L (Some c) s s1).
        { apply atom_same; try done. intros m Hm0. subst s1. simpl. apply trace_view_other. intros ->. apply Hm0. by right. }
        pose proof (same4_shapes _ _ H1) as Hsh.
        destruct (Aexec _ IH T L c d _ _ k s1 s' r A r0 Ha1 ltac:(done) ltac:(done) ltac:(exact (qc_same _ _ _ _ H1 Hq)) Hag (below_shapes L c d d s s1 Hsh Hd Hd HL)
                   ltac:(by rewrite Hsh)
                   ltac:(intros K; destruct (Hg K) as [H3 H2]; simpl in H3; rewrite Ev in H3; split; [done|exact (guide_atom _ _ _ _ _ _ A1 H2)])
                   ltac:(destruct Hor as [K|[K K']]; [by left|right; split; [done|by rewrite Hsh]]) Hm) as (R1&R2&R3&R4&R5&(l&R6&R7)).
        split_and!; [done|eapply atom_trans; [exact A1|exact R2]| |done| |].
        * simpl. rewrite Ev. done.
        * intros t o Ho. apply R5. by rewrite (seen_ans_same _ _ _ H1).
        * exists (z :: l). split; [simpl; rewrite Ev, R6; done|].
          rewrite R7. subst s1. simpl. rewrite trace_view_log, <- app_assoc. done.
      + injection Hm as <- <-. apply Hdone; [done| |simpl; rewrite Ev; done|simpl; rewrite Ev; done].
        destruct Hor as [K|K]; [by right|]. left. simpl. rewrite Ev. done.
  Qed.

  Lemma quiet_dec d : quiet d \/ ~ quiet d.
  Proof.
    unfold quiet. destruct (n_freshness d); destruct (n_unannounced d); try (left; by left); try (left; by right); right; intros [H|H]; done.
  Qed.

  (** [T] with a run started stale. *)
  Definition tw (T : node_id -> Prop) (t : node_id) (d : node) (x : node_id) : Prop := T x \/ (x = t /\ ~ quiet d).

  Lemma run_begin (T : node_id -> Prop) (lim t : node_id) s d :
    aS T lim s -> t < lim -> pure_in (shapes s) t -> g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    aS (tw T t d) t (with_current_owner None (with_current (Some t) (run_start t d s))).
  Proof.
    intros (Hc&Hw&St) Hlt Hp Hd Hk Hl.
    set (s0 := with_current_owner None (with_current (Some t) (run_start t d s))).
    destruct Hc as [Hi Hx].
    assert (Hnr : t ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St t Hin Hp); lia).
    assert (Hc0 : run_core s0).
    { pose proof (invariant_run_begin t run_node d s Hi Hd eq_refl eq_refl (le_S _ _ (le_n _))) as Hi4.
      destruct (extras_run_begin t run_node d s Hx Hd Hk Hk eq_refl eq_refl eq_refl eq_refl (le_S _ _ (le_n _))) as [Hx4 _].
      change (check_invariant (run_start t d s)) in Hi4. change (run_extras (run_start t d s)) in Hx4.
      assert (Hmem : n_in_cleanup (run_node d) = false) by exact (proj2 (b_memo _ (proj1 Hx) t d Hd Hk)).
      split.
      - eapply invariant_irrelevant; [|apply invariant_with_current; [|exact Hi4]].
        + unfold irrelevant. simpl. split_and!; reflexivity.
        + intros c [=<-]. simpl. left.
      - eapply extras_same; [|apply extras_current; [exact Hx4|]].
        + split; reflexivity.
        + intros c [=<-]. split; [simpl; left|]. intros e He. simpl in He. rewrite lookup_insert_eq in He. by injection He as <-. }
    pose proof (proj1 Hx) as Hb.
    assert (Hsn : g_nodes s0 = <[t := run_node d]> (g_nodes s)) by done.
    assert (He0 : g_edges s0 = sources_begin_run t (g_edges s)) by done.
    assert (Hr0 : g_running s0 = t :: g_running s) by done.
    assert (Hsee : forall c y, seen_of (g_ghost s0) c y = seen_of (g_ghost s) c y) by done.
    assert (Hsh : shapes s0 = shapes s).
    { unfold shapes. rewrite Hsn, fmap_insert. assert (Hx0 : shape_of (run_node d) = shape_of d) by done.
      rewrite Hx0. apply insert_id. by rewrite lookup_fmap, Hd. }
    assert (Ht1 : g_nodes s0 !! t = Some (run_node d)) by (rewrite Hsn; apply lookup_insert_eq).
    assert (Hne1 : forall m, m <> t -> g_nodes s0 !! m = g_nodes s !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
    assert (Hv : forall c, c <> t -> slot_view (g_edges s0) c = slot_view (g_edges s) c).
    { intros c Hne. rewrite He0, slot_view_begin. by rewrite decide_False. }
    assert (Hvt : slot_view (g_edges s0) t = ((slot_view (g_edges s) t).1, 0)).
    { rewrite He0, slot_view_begin. by rewrite decide_True. }
    assert (Hback : forall m d', g_nodes s0 !! m = Some d' -> (m = t /\ d' = run_node d) \/ (m <> t /\ g_nodes s !! m = Some d')).
    { intros m d' Hm. destruct (decide (m = t)) as [->|Hm']; [left|right].
      - rewrite Ht1 in Hm. injection Hm as <-. done.
      - rewrite Hne1 in Hm by done. done. }
    assert (Hslots : forall u, u ∈ (slot_view (g_edges s) t).1 -> u < t /\ pure_in (shapes s) u).
    { intros u Hu. destruct (w_slots _ Hw t d Hd (or_introl (conj Hp Hk)) u Hu) as [A B]. split; [exact (A Hk)|exact B]. }
    assert (Hrd : forall c y, y <> t -> rd_ok s c y -> rd_ok s0 c y).
    { intros c y Hy (H1&e&He&H2). split; [done|]. exists e. split; [by rewrite Hne1|]. exact H2. }
    assert (Hqg : forall y, quiet_or_gone s y -> quiet_or_gone s0 y).
    { intros y (e&He&H). destruct (decide (y = t)) as [->|Hyt].
      - eexists. split; [exact Ht1|]. left. by left.
      - exists e. split; [by rewrite Hne1|done]. }
    assert (Hval : forall c d0, c <> t -> value_ok s c d0 -> value_ok s0 c d0).
    { intros c d0 Hne. apply value_ext; [|done]. by rewrite !read_prefix_view, Hv. }
    split_and!; [done| |].
    - destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Hsh.
      + intros n d0 Hd0 Hk0. destruct (Hback n d0 Hd0) as [[-> ->]|[_ H]]; [simpl in Hk0; congruence|by apply (W1 n d0)].
      + intros c d0 Hd0 Hk0 Hp0. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
        * simpl. destruct (W2 t d Hd Hk Hp) as (A&B&_). split_and!; done.
        * by apply (W2 c d0).
      + intros c d0 Hd0 Hq0. destruct (Hback c d0 Hd0) as [[-> ->]|[_ H]].
        * exfalso. destruct Hq0 as [K _]. simpl in K. congruence.
        * apply (W3 c d0 H). unfold quiet_effect in *. by rewrite Hsh in Hq0.
      + intros c d0 Hd0 Hq0 u Hu. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. destruct (Hback c d0 Hd0) as [[-> ->]|[Hne H]].
        * rewrite Hvt in Hu. exact (W4 t d Hd (or_introl (conj Hp Hk)) u Hu).
        * rewrite Hv in Hu by done. exact (W4 c d0 H Hq1 u Hu).
      + intros c d0 Hd0 Hq0 Hl0 Hnr0 Hse u Hu Hnu. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1.
        rewrite Hr0 in Hnr0, Hnu. apply not_elem_of_cons in Hnr0 as [Hct Hnr0]. apply not_elem_of_cons in Hnu as [Hut Hnu].
        rewrite Hv in Hu by done. rewrite Hne1 in Hd0 by done. apply Hrd; [done|]. exact (W5 c d0 Hd0 Hq1 Hl0 Hnr0 Hse u Hu Hnu).
      + intros c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hse. rewrite Hr0 in Hnr0. apply not_elem_of_cons in Hnr0 as [Hct Hnr0].
        rewrite Hne1 in Hd0 by done. apply Hval; [done|]. exact (W6 c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hse).
      + intros c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hq0 u Hu. rewrite Hr0 in Hnr0. apply not_elem_of_cons in Hnr0 as [Hct Hnr0].
        rewrite Hne1 in Hd0 by done. rewrite Hv in Hu by done. apply Hqg. exact (W7 c d0 Hd0 Hk0 Hp0 Hl0 Hnr0 Hq0 u Hu).
      + intros c Hc1. assert (Hc2 : c ∈ gh_passed (g_ghost s)).
        { simpl in Hc1. apply elem_of_difference in Hc1. tauto. }
        destruct (W8 c Hc2) as [A B]. split; [done|]. intros Ha. apply B.
        intros n d0 Hd0 Hk0. rewrite <- Hsh. destruct (decide (n = t)) as [->|Hn]; [refine (Ha t _ Ht1 _); done|].
        refine (Ha n d0 _ Hk0). by rewrite Hne1.
    - destruct St as [S1 S2 S3 S4 S5 S6 S7 S8]. split; rewrite ?Hsh, ?Hr0.
      + intros m Hm Hpm. apply elem_of_cons in Hm as [->|Hm]; [lia|]. pose proof (S1 m Hm Hpm). lia.
      + intros x [K|[-> _]]; [right; by apply S2|left].
      + intros x y dy [K|[-> Knq]] Hdy Hk0 Hp0 Hin.
        * destruct (Hback y dy Hdy) as [[-> ->]|[Hne H]].
          -- exfalso. rewrite Hvt in Hin. destruct (Hslots x Hin) as [Hlx Hpx]. pose proof (S1 x (S2 x K) Hpx). lia.
          -- rewrite Hv in Hin by done. destruct (S3 x y dy K H Hk0 Hp0 Hin) as [L|[L1 L2]]; [left; by left|right].
             split; [|done]. apply not_elem_of_cons. done.
        * destruct (Hback y dy Hdy) as [[-> ->]|[Hne H]].
          -- exfalso. rewrite Hvt in Hin. destruct (Hslots t Hin) as [Hlx _]. lia.
          -- rewrite Hv in Hin by done.
             assert (Hnq : ~ quiet_or_gone s t).
             { intros (e&He&[Q|Q]); rewrite Hd in He; injection He as <-; [done|congruence]. }
             destruct (decide (y ∈ g_running s)) as [Hyr|Hyr].
             ++ destruct (S8 y) as [L|L]; [left; by left|]. exfalso. exact (Hnq (S4 y dy Hyr H Hk0 Hp0 L t Hin)).
             ++ right. split; [apply not_elem_of_cons; done|].
                destruct (n_disposed dy) eqn:Edi.
                ** exfalso. rewrite (b_disposed _ Hb y dy H Edi Hyr) in Hin. by apply not_elem_of_nil in Hin.
                ** destruct (quiet_dec dy) as [Q|Q].
                   --- exfalso. exact (Hnq (w_quiet _ Hw y dy H Hk0 Hp0 Edi Hyr Q t Hin)).
                   --- split.
                       +++ apply stale_of_not_clean. intros Hcl. apply Q. by left.
                       +++ destruct (n_unannounced dy) eqn:Ef; [|done]. exfalso. apply Q. by right.
      + intros x dx Hx0 Hdx Hk0 Hp0 HT u Hu. apply elem_of_cons in Hx0 as [->|Hx0].
        * destruct (Hback t dx Hdx) as [[_ ->]|[K _]]; [|done].
          assert (Qd : quiet d).
          { destruct (quiet_dec d) as [Q|Q]; [done|]. exfalso. apply HT. right. done. }
          rewrite Hvt in Hu. apply Hqg. exact (w_quiet _ Hw t d Hd Hk Hp Hl Hnr Qd u Hu).
        * assert (Hxt : x <> t) by (intros ->; done).
          rewrite Hne1 in Hdx by done. rewrite Hv in Hu by done. apply Hqg.
          exact (S4 x dx Hx0 Hdx Hk0 Hp0 (fun K => HT (or_introl K)) u Hu).
      + intros x dx Hx0 Hdx Hq0 u Hu Hnu. apply not_elem_of_cons in Hnu as [Hut Hnu]. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1.
        apply elem_of_cons in Hx0 as [->|Hx0].
        * exfalso. rewrite read_prefix_view, Hvt in Hu. simpl in Hu. rewrite take_0 in Hu. by apply not_elem_of_nil in Hu.
        * assert (Hxt : x <> t) by (intros ->; done).
          rewrite Hne1 in Hdx by done. rewrite read_prefix_view, Hv, <- read_prefix_view in Hu by done.
          destruct (S5 x dx Hx0 Hdx Hq1 u Hu Hnu) as [A B]. split; [by apply Hrd|by apply Hqg].
      + intros x dx Hx0 Hdx Hq0. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. apply elem_of_cons in Hx0 as [->|Hx0].
        * rewrite Ht1 in Hdx. injection Hdx as <-. done.
        * assert (Hxt : x <> t) by (intros ->; done). rewrite Hne1 in Hdx by done. exact (S6 x dx Hx0 Hdx Hq1).
      + left. done.
      + intros x. destruct (S8 x) as [K|K]; [left; by left|].
        destruct (decide (x = t)) as [->|Hxt].
        * destruct (quiet_dec d) as [Q|Q]; [right; intros [L|[_ L]]; done|left; right; done].
        * right. intros [L|[L _]]; done.
  Qed.

  Lemma run_memo_parts g n s s' d :
    g_nodes s !! n = Some d -> run_memo (S g) n s = Some (s', tt) ->
    exists s2 r, hosted n (exec g (run_env d) (n_body d)) (run_start n d s) = Some (s2, r) /\
      forall d1, g_nodes s2 !! n = Some d1 -> n_violated d1 = false -> n_disposed d1 = false -> n_comparer d1 = CmpEq ->
      exists w d10 mv,
        sources_end_run n (g_edges s2) = Some w /\
        d10 = match r with
              | Ok v => node_with_published true (node_with_value v d1)
              | Fail y => node_with_violated false (node_with_status StError (node_with_failure (Some y) d1))
              end /\
        (mv = false <-> read_view_of d10 = read_view_of d) /\
        run_finish g n mv (with_running (tail (g_running s2)) (with_edges w (set_node n d10 s2))) = Some (s', tt).
  Proof.
    intros Hd Hm. cbn [run_memo] in Hm.
    rewrite (bind_get_node _ _ _ _ Hd), (bind_modify_node _ _ _ _ _ Hd) in Hm.
    unfold edges, push_running in Hm. rewrite !bind_m_modify in Hm.
    apply bind_inv in Hm as (s2 & r & Eh & Hm).
    exists s2, r. split; [exact Eh|]. intros d1 Hd1 Hv Hdi Hcq.
    rewrite (bind_get_node _ _ _ _ Hd1) in Hm. rewrite Hv, Hdi in Hm. cbv iota in Hm.
    destruct r as [v|y].
    - rewrite (bind_modify_node _ _ _ _ _ Hd1) in Hm.
      apply bind_edges_opt in Hm as (w & Hw & Hm). cbv iota in Hw.
      unfold pop_running in Hm. rewrite bind_m_modify, bind_m_return in Hm.
      set (d10 := node_with_published true (node_with_value v d1)) in Hm.
      set (s10 := with_running (tail (g_running s2)) (with_edges w (set_node n d10 s2))) in Hm.
      assert (Hd10 : g_nodes s10 !! n = Some d10) by (subst s10; simpl; apply lookup_insert_eq).
      rewrite (bind_get_node _ _ _ _ Hd10) in Hm.
      assert (Ec : n_comparer d10 = CmpEq) by done.
      exists w, d10. destruct (decide _) as [Hne|Hsame].
      + rewrite bind_m_return in Hm. exists true. split_and!; [exact Hw|done| |exact Hm].
        split; [done|]. intros Hv'. unfold read_view_of in Hv'. injection Hv' as _ H1 H2. exfalso. destruct Hne as [Hn|Hn]; by apply Hn.
      + rewrite Ec, bind_m_return in Hm. eexists. split_and!; [exact Hw|done| |exact Hm].
        assert (Hs1 : n_status d10 = n_status d).
        { destruct (decide (n_status d10 = n_status d)); [done|]. exfalso. apply Hsame. by left. }
        assert (Hs2 : n_failure d10 = n_failure d).
        { destruct (decide (n_failure d10 = n_failure d)); [done|]. exfalso. apply Hsame. by right. }
        split.
        * intros Hb. apply bool_decide_eq_false in Hb. unfold read_view_of. apply dec_stable in Hb. by rewrite Hb, Hs1, Hs2.
        * intros Hv'. apply bool_decide_eq_false. unfold read_view_of in Hv'. injection Hv' as H0 _ _. by intros Hx.
    - rewrite bind_m_return in Hm.
      apply bind_edges_opt in Hm as (w & Hw & Hm). cbv iota in Hw.
      unfold pop_running in Hm. rewrite bind_m_modify in Hm.
      set (s8 := with_running _ (with_edges w s2)) in Hm.
      assert (Hd8 : g_nodes s8 !! n = Some d1) by exact Hd1.
      rewrite (bind_modify_node _ _ _ _ _ Hd8) in Hm.
      set (d10 := node_with_violated false (node_with_status StError (node_with_failure (Some y) d1))) in Hm.
      set (s10 := set_node n d10 s8) in Hm.
      assert (Hd10 : g_nodes s10 !! n = Some d10) by (subst s10; simpl; apply lookup_insert_eq).
      rewrite (bind_get_node _ _ _ _ Hd10) in Hm.
      assert (Ec : n_comparer d10 = CmpEq) by done.
      exists w, d10. destruct (decide _) as [Hne|Hsame].
      + rewrite bind_m_return in Hm. exists true. split_and!; [exact Hw|done| |exact Hm].
        split; [done|]. intros Hv'. unfold read_view_of in Hv'. injection Hv' as _ H1 H2. exfalso. destruct Hne as [Hn|Hn]; by apply Hn.
      + rewrite Ec, bind_m_return in Hm. eexists. split_and!; [exact Hw|done| |exact Hm].
        assert (Hs1 : n_status d10 = n_status d).
        { destruct (decide (n_status d10 = n_status d)); [done|]. exfalso. apply Hsame. by left. }
        assert (Hs2 : n_failure d10 = n_failure d).
        { destruct (decide (n_failure d10 = n_failure d)); [done|]. exfalso. apply Hsame. by right. }
        split.
        * intros Hb. apply bool_decide_eq_false in Hb. unfold read_view_of. apply dec_stable in Hb. by rewrite Hb, Hs1, Hs2.
        * intros Hv'. apply bool_decide_eq_false. unfold read_view_of in Hv'. injection Hv' as H0 _ _. by intros Hx.
  Qed.

  (** The end of a pure memo's run re-establishes the invariant, from what its body left. *)
  Lemma memo_fields_rise dm dn :
    rise_node dm dn -> n_scope dm = None /\ n_violated dm = false /\ (n_freshness dm = Clean -> n_unannounced dm = false) ->
    n_scope dn = None /\ n_violated dn = false /\ (n_freshness dn = Clean -> n_unannounced dn = false).
  Proof.
    intros (_&_&_&_&_&Hsc&Hvi&Hf&Hfl) (A&B&C). split_and!; [congruence|congruence|].
    intros Hcl. rewrite Hcl in Hf. destruct (n_unannounced dn) eqn:E; [|done].
    rewrite C in Hfl; [naive_solver|]. destruct (n_freshness dm); unfold freshness_le in Hf; simpl in Hf; try lia; done.
  Qed.

  Lemma qe_shapes s s' c d d' :
    shapes s' = shapes s -> rise_node d d' -> quiet_effect s' c d' -> quiet_effect s c d.
  Proof.
    intros Hs (Hsh&_) Hq. unfold shape_of in Hsh. injection Hsh as K Q E B.
    unfold quiet_effect in *. rewrite Hs in Hq. destruct Hq as [H1 H2]. rewrite E, B in H2. split; [congruence|done].
  Qed.

  Lemma finish_ok (T : node_id -> Prop) (lim t : node_id) s s0 s2 s' d d2 dE r :
    whole s -> stk T lim s -> t < lim -> pure_in (shapes s) t -> g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    (* the start of the body *)
    shapes s0 = shapes s -> g_running s0 = t :: g_running s ->
    (forall m, m <> t -> g_nodes s0 !! m = g_nodes s !! m) ->
    (forall c, c <> t -> slot_view (g_edges s0) c = slot_view (g_edges s) c) ->
    (forall c y, seen_of (g_ghost s0) c y = seen_of (g_ghost s) c y) ->
    (forall c, c ∈ gh_passed (g_ghost s0) -> c ∈ gh_passed (g_ghost s)) ->
    (* the end of the body *)
    whole s2 -> stk (tw T t d) t s2 -> atom (tw T t d) t (Some t) s0 s2 -> g_nodes s2 !! t = Some d2 -> n_kind d2 = KMemo ->
    body_eval (seen_ans s2 t) (n_env d) (n_body d) = Some r ->
    (* the end of the run *)
    shapes s' = shapes s2 -> g_running s' = g_running s ->
    (forall c, c <> t -> slot_view (g_edges s') c = slot_view (g_edges s2) c) ->
    (slot_view (g_edges s') t).1 = read_prefix (g_edges s2) t ->
    read_prefix (g_edges s') t = read_prefix (g_edges s2) t ->
    (forall c y, seen_of (g_ghost s') c y = seen_of (g_ghost s2) c y) ->
    gh_passed (g_ghost s') = gh_passed (g_ghost s2) ->
    (forall m dm, m <> t -> g_nodes s2 !! m = Some dm -> exists dm', g_nodes s' !! m = Some dm' /\ rise_node dm dm') ->
    (forall m dm', m <> t -> g_nodes s' !! m = Some dm' -> exists dm, g_nodes s2 !! m = Some dm /\ rise_node dm dm') ->
    (forall m, m <> t -> pure_in (shapes s2) m -> ~ tw T t d m -> ~ (exists e, g_nodes s2 !! m = Some e /\ un e) -> g_nodes s' !! m = g_nodes s2 !! m) ->
    g_nodes s' !! t = Some dE -> shape_of dE = shape_of d -> view_matches dE r -> n_disposed dE = false -> quiet dE ->
    n_scope dE = None -> n_violated dE = false -> (n_freshness dE = Clean -> n_unannounced dE = false) ->
    (read_view_of dE <> read_view_of d -> forall c dc, g_nodes s' !! c = Some dc -> c ∉ g_running s' -> n_disposed dc = false ->
       t ∈ (slot_view (g_edges s') c).1 -> qc s' c dc -> ~ settled dc) ->
    (quiet d -> read_view_of dE = read_view_of d) ->
    (quiet d -> n_freshness dE = Clean) ->
    (deferring s' = deferring s) -> g_flushing s' = g_flushing s ->
    whole s' /\ stk T lim s' /\
    (forall c, c ∈ gh_passed (g_ghost s') -> c ∈ gh_passed (g_ghost s)) /\
    (forall m dm, g_nodes s !! m = Some dm -> exists dm', g_nodes s' !! m = Some dm' /\ n_disposed dm' = n_disposed dm) /\
    (forall m dm', g_nodes s' !! m = Some dm' -> is_Some (g_nodes s !! m)) /\
    (forall m dm, ~ rn s lim None m -> g_nodes s !! m = Some dm -> exists dm', g_nodes s' !! m = Some dm' /\ rise_node dm dm') /\
    (forall c, ~ rn s lim None c -> slot_view (g_edges s') c = slot_view (g_edges s) c /\ forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) /\
    (forall n dn, g_nodes s !! n = Some dn -> pure_in (shapes s) n -> ~ T n -> quiet dn \/ n_disposed dn = true ->
       exists dn', g_nodes s' !! n = Some dn' /\ read_view_of dn' = read_view_of dn /\ (quiet dn' \/ n_disposed dn' = true) /\
         (n_freshness dn = Clean -> n_freshness dn' = Clean)) /\
    (forall n dn, ~ rn s lim None n -> g_nodes s !! n = Some dn -> pure_in (shapes s) n -> ~ T n -> quiet dn -> g_nodes s' !! n = Some dn).
  Proof.
    intros Hw St Hlt Hp Hd Hk Hl Sh0 Run0 Nd0 Sv0 Se0 Pa0 Hw2 St2 A Hd2 Hk2 Hbe Sh' Run' Sv' SvT PreT Se' Pa' F1 F1b F2
      HdE ShE VmE LiE QE ScE ViE CfE Hmv Hqv Hqc Hdf Hfl.
    assert (Hsh : shapes s' = shapes s).
    { rewrite Sh', (at_shapes _ _ _ _ _ A). done. }
    assert (Hsh2 : shapes s2 = shapes s) by (rewrite (at_shapes _ _ _ _ _ A); done).
    assert (Run2 : g_running s2 = t :: g_running s) by (rewrite (at_running _ _ _ _ _ A); done).
    assert (Hnr : t ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St t Hin Hp); lia).
    assert (Hkind : n_kind dE = KMemo) by (unfold shape_of in ShE; injection ShE as K _ _ _; congruence).
    assert (Hp2 : pure_in (shapes s2) t) by (by rewrite Hsh2).
    assert (Hqc2 : qc s2 t d2) by (left; split; done).
    assert (Hslots : forall u, u ∈ (slot_view (g_edges s2) t).1 -> u < t /\ pure_in (shapes s2) u).
    { intros u Hu. destruct (w_slots _ Hw2 t d2 Hd2 Hqc2 u Hu) as [A9 B9]. split; [exact (A9 Hk2)|exact B9]. }
    assert (Hpsl : forall u, u ∈ read_prefix (g_edges s2) t -> u ∈ (slot_view (g_edges s2) t).1).
    { intros u Hu. rewrite read_prefix_view in Hu. by apply elem_take in Hu. }
    assert (Hntw : forall u, u ∉ g_running s2 -> ~ tw T t d u).
    { intros u Hnu [K|[-> _]]; [apply Hnu; exact (st_T _ _ _ St2 u (or_introl K))|apply Hnu; rewrite Run2; left]. }
    assert (Hlow : forall u, u < t -> pure_in (shapes s2) u -> u ∉ g_running s2).
    { intros u Hu Hpu Hin. pose proof (st_low _ _ _ St2 u Hin Hpu). lia. }
    assert (Hrd : forall c u, u <> t -> rd_ok s2 c u -> rd_ok s' c u).
    { intros c u Hu (H1&e&He&H2). unfold rd_ok. rewrite Se'. split; [done|].
      destruct (F1 u e Hu He) as (e'&He'&_&V&_). exists e'. split; [done|]. intros v Hv. rewrite V. auto. }
    assert (Hqf : forall u, u <> t -> pure_in (shapes s2) u -> ~ tw T t d u -> quiet_or_gone s2 u -> quiet_or_gone s' u).
    { intros u Hu Hpu HT (e&He&Q). destruct (quiet_dec e) as [Qe|Qe].
      - exists e. rewrite (F2 u Hu Hpu HT); [split; [done|by left]|]. intros (e0&He0&Hun). rewrite He in He0. injection He0 as <-.
        exact (un_not_quiet _ Hun Qe).
      - destruct Q as [Q|Q]; [done|]. destruct (F1 u e Hu He) as (e'&He'&_&_&X&_). exists e'. split; [done|right; congruence]. }
    assert (Hqt : quiet_or_gone s' t) by (exists dE; split; [done|by left]).
    assert (Hval : forall c dm, c <> t -> value_ok s2 c dm -> value_ok s' c dm).
    { intros c dm Hc. apply value_ext; [by rewrite !read_prefix_view, Sv'|done]. }
    assert (Hup : forall c, c <> t -> ~ rn s0 t (Some t) c -> slot_view (g_edges s') c = slot_view (g_edges s) c /\ forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y).
    { intros c Hc Hn. destruct (at_slots _ _ _ _ _ A c Hn) as [X Y]. split.
      - rewrite Sv' by done. rewrite X. by apply Sv0.
      - intros y. rewrite Se', Y. apply Se0. }
    (* a reader of [t] outside the run, or on the stack, that read its view at the start *)
    assert (Hrdt : forall c, rd_ok s c t -> slot_view (g_edges s') c = slot_view (g_edges s) c -> (forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) ->
              read_view_of dE = read_view_of d -> rd_ok s' c t).
    { intros c (H1&e&He&H2) _ Hse Hv. unfold rd_ok. rewrite Hse. split; [done|]. rewrite Hd in He. injection He as <-.
      exists dE. split; [done|]. intros v Hv'. rewrite Hv. auto. }
    assert (Hmemo : forall c dm dc, g_nodes s2 !! c = Some dm -> rise_node dm dc -> qc s' c dc -> qc s2 c dm).
    { intros c dm dc Hdm Hr Hq. destruct Hq as [[Y1 Y2]|Y]; [left; split; [by rewrite <- Sh'|by rewrite <- (rise_kind _ _ Hr)]|right]. eapply qe_shapes; [exact Sh'|exact Hr|exact Y]. }
    assert (HstartNode : forall c dm, c <> t -> t < c \/ ~ pure_in (shapes s) c -> g_nodes s2 !! c = Some dm -> exists dcs, g_nodes s !! c = Some dcs /\ rise_node dcs dm).
    { intros c dm Hc Hlt' Hdm. destruct (at_back _ _ _ _ _ A c dm Hdm) as [x Hx]. rewrite Nd0 in Hx by done. exists x. split; [done|].
      assert (Hnr0 : ~ rn s0 t None c).
      { intros [[H1 H2]|H]; [|done]. destruct Hlt' as [L|L]; [lia|]. apply L. by rewrite <- Sh0. }
      destruct (at_rise _ _ _ _ _ A c x Hnr0 ltac:(by rewrite Nd0)) as (y&Hy&Hr). rewrite Hdm in Hy. injection Hy as <-. done. }
    split_and!.
    - pose proof Hw2 as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Sh', ?Run', ?Pa'.
      + intros n dn Hn Hk0. destruct (decide (n = t)) as [->|Hnt]; [rewrite HdE in Hn; injection Hn as <-; congruence|].
        destruct (F1b n dn Hnt Hn) as (dm&Hdm&Hr). destruct (W1 n dm Hdm ltac:(by rewrite <- (rise_kind _ _ Hr))) as [A1 B1].
        destruct Hr as (_&Hv&_). unfold read_view_of in Hv. injection Hv as _ X Y. split; congruence.
      + intros c dc Hc Hk0 Hp0. destruct (decide (c = t)) as [->|Hct]; [rewrite HdE in Hc; injection Hc as <-; by split_and!|].
        destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr).
        exact (memo_fields_rise dm dc Hr (W2 c dm Hdm ltac:(by rewrite <- (rise_kind _ _ Hr)) Hp0)).
      + intros c dc Hc Hq. destruct (decide (c = t)) as [->|Hct].
        { exfalso. rewrite HdE in Hc. injection Hc as <-. destruct Hq as [Y _]. congruence. }
        destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr). pose proof (qe_shapes _ _ _ _ _ Sh' Hr Hq) as Hq0.
        destruct (W3 c dm Hdm Hq0) as [X Y]. pose proof Hr as (_&_&_&_&_&Hsc&_&_&Hfl'). rewrite Hsc. split; [done|].
        destruct (n_unannounced dc) eqn:Ef; [|done]. rewrite Hfl' in Y; done.
      + intros c dc Hc Hq u Hu. destruct (decide (c = t)) as [->|Hct].
        * rewrite SvT in Hu. destruct (Hslots u (Hpsl u Hu)) as [X9 Y9]. split; [intros _; exact X9|exact Y9].
        * rewrite Sv' in Hu by done. destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr). rewrite (rise_kind _ _ Hr). exact (W4 c dm Hdm (Hmemo c dm dc Hdm Hr Hq) u Hu).
      + intros c dc Hc Hq Hl0 Hnr0 Hse u Hu Hnu. destruct (decide (c = t)) as [->|Hct].
        * rewrite SvT in Hu. destruct (Hslots u (Hpsl u Hu)) as [Hut Hpu]. assert (Hu' : u <> t) by lia.
          exact (Hrd t u Hu' (proj1 (st_read _ _ _ St2 t d2 ltac:(rewrite Run2; left) Hd2 Hqc2 u Hu (Hlow u Hut Hpu)))).
        * rewrite Sv' in Hu by done. destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr). pose proof (Hmemo c dm dc Hdm Hr Hq) as Hq2.
          assert (Hl2 : n_disposed dm = false) by (pose proof Hr as (_&_&X&_); congruence).
          assert (Hnr2 : c ∉ g_running s2) by (rewrite Run2; apply not_elem_of_cons; done).
          destruct (decide (u = t)) as [->|Hut].
          -- destruct (decide (read_view_of dE = read_view_of d)) as [Hv|Hv].
             2: { exfalso. refine (Hmv Hv c dc Hc ltac:(by rewrite Run') Hl0 _ Hq Hse). by rewrite Sv'. }
             destruct (W4 c dm Hdm Hq2 t Hu) as [Htc0 _].
             assert (Htc : t < c \/ ~ pure_in (shapes s) c).
             { destruct Hq2 as [[_ Km]|[Ke _]]; [left; exact (Htc0 Km)|right].
               rewrite <- Hsh2. intros K9. exact (pure_kind s2 c dm K9 Hdm Ke). }
             assert (Hnrn : ~ rn s0 t (Some t) c).
             { intros [[K0 K]|K]; [|congruence]. destruct Htc as [L9|L9]; [lia|]. apply L9. by rewrite <- Sh0. }
             destruct (Hup c Hct Hnrn) as [X Y].
             destruct (HstartNode c dm Hct Htc Hdm) as (dcs&Hdcs&Hr0).
             assert (Hqs : qc s c dcs) by exact (qc_shapes s s2 c dcs dm Hsh2 Hdcs Hdm Hq2).
             assert (Hl3 : n_disposed dcs = false) by (pose proof Hr0 as (_&_&Z&_); congruence).
             assert (Hse3 : settled dcs) by exact (rise_settled _ _ Hr0 (rise_settled _ _ Hr Hse)).
             assert (Hin : t ∈ (slot_view (g_edges s) c).1).
             { rewrite <- X. by rewrite Sv'. }
             exact (Hrdt c (w_reads _ Hw c dcs Hdcs Hqs Hl3 Hnr0 Hse3 t Hin Hnr) X Y Hv).
          -- apply Hrd; [done|]. refine (W5 c dm Hdm Hq2 Hl2 Hnr2 (rise_settled _ _ Hr Hse) u Hu _). rewrite Run2. apply not_elem_of_cons. done.
      + intros c dc Hc Hk0 Hp0 Hl0 Hnr0 Hse. destruct (decide (c = t)) as [->|Hct].
        * rewrite HdE in Hc. injection Hc as <-. exists r. split; [|done].
          unfold shape_of in ShE. injection ShE as _ _ E B. rewrite E, B. revert Hbe. apply body_eval_mono. intros y o.
          unfold seen_ans. rewrite PreT, Se'. done.
        * destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr).
          assert (Hl2 : n_disposed dm = false) by (pose proof Hr as (_&_&X&_); congruence).
          assert (Hnr2 : c ∉ g_running s2) by (rewrite Run2; apply not_elem_of_cons; done).
          destruct (Hval c dm Hct (W6 c dm Hdm ltac:(by rewrite <- (rise_kind _ _ Hr)) Hp0 Hl2 Hnr2 (rise_settled _ _ Hr Hse))) as (r0&Hb0&Hm0).
          pose proof Hr as (Hsh0&Hv&_). unfold shape_of in Hsh0. injection Hsh0 as _ _ E B.
          exists r0. rewrite E, B. split; [done|]. exact (view_matches_view _ _ _ Hv Hm0).
      + intros c dc Hc Hk0 Hp0 Hl0 Hnr0 Hq u Hu. destruct (decide (c = t)) as [->|Hct].
        * rewrite SvT in Hu. destruct (Hslots u (Hpsl u Hu)) as [Hut Hpu]. assert (Hu' : u <> t) by lia.
          pose proof (Hlow u Hut Hpu) as Hnu.
          exact (Hqf u Hu' Hpu (Hntw u Hnu) (proj2 (st_read _ _ _ St2 t d2 ltac:(rewrite Run2; left) Hd2 Hqc2 u Hu Hnu))).
        * rewrite Sv' in Hu by done. destruct (F1b c dc Hct Hc) as (dm&Hdm&Hr).
          assert (Hk2' : n_kind dm = KMemo) by (by rewrite <- (rise_kind _ _ Hr)).
          assert (Hl2 : n_disposed dm = false) by (pose proof Hr as (_&_&X&_); congruence).
          assert (Hnr2 : c ∉ g_running s2) by (rewrite Run2; apply not_elem_of_cons; done).
          pose proof (rise_quiet _ _ Hr Hq) as Hq2.
          pose proof (W7 c dm Hdm Hk2' Hp0 Hl2 Hnr2 Hq2 u Hu) as Hqg.
          destruct (decide (u = t)) as [->|Hut]; [done|].
          destruct (W4 c dm Hdm (or_introl (conj Hp0 Hk2')) u Hu) as [_ Hpu].
          refine (Hqf u Hut Hpu _ Hqg). intros HTu.
          destruct (st_obs _ _ _ St2 u c dm HTu Hdm Hk2' Hp0 Hu) as [K|[_ K]].
          -- apply Hnr2. exact (st_T _ _ _ St2 c K).
          -- exact (un_not_quiet _ K Hq2).
      + intros c Hc. destruct (W8 c Hc) as [X Y]. split; [done|]. intros Ha. apply Y.
        intros n dn Hn Hk0. rewrite <- Sh'. destruct (decide (n = t)) as [->|Hnt]; [exact (Ha t dE HdE Hkind)|].
        destruct (F1 n dn Hnt Hn) as (dn'&Hdn'&Hr). refine (Ha n dn' Hdn' _). by rewrite (rise_kind _ _ Hr).
    - pose proof St as [S1 S2 S3 S4 S5 S6 S7 S8]. pose proof St2 as [J1 J2 J3 J4 J5 J6 J7 J8].
      split; rewrite ?Hsh, ?Run'.
      + done.
      + done.
      + intros x y dy Hx Hdy Hk0 Hp0 Hin. destruct (decide (y = t)) as [->|Hyt].
        * exfalso. rewrite SvT in Hin. destruct (Hslots x (Hpsl x Hin)) as [Hlx Hpx].
          pose proof (S1 x (S2 x Hx) ltac:(by rewrite <- Hsh2)). lia.
        * rewrite Sv' in Hin by done. destruct (F1b y dy Hyt Hdy) as (dm&Hdm&Hr).
          destruct (J3 x y dm (or_introl Hx) Hdm ltac:(by rewrite <- (rise_kind _ _ Hr)) ltac:(by rewrite Hsh2) Hin) as [[K|[K _]]|[K1 K2]]; [by left|done|right].
          split; [|by eapply un_rises]. rewrite Run2 in K1. apply not_elem_of_cons in K1. tauto.
      + intros x dx Hx Hdx Hk0 Hp0 HT u Hu.
        assert (Hxt : x <> t) by (intros ->; done). rewrite Sv' in Hu by done.
        destruct (F1b x dx Hxt Hdx) as (dm&Hdm&Hr).
        assert (Hnt : ~ tw T t d x) by (intros [K|[K _]]; done).
        assert (Hrx : x ∈ g_running s2) by (rewrite Run2; by right).
        assert (Hk2' : n_kind dm = KMemo) by (by rewrite <- (rise_kind _ _ Hr)).
        assert (Hp2' : pure_in (shapes s2) x) by (by rewrite Hsh2).
        pose proof (J4 x dm Hrx Hdm Hk2' Hp2' Hnt u Hu) as Hqg.
        destruct (decide (u = t)) as [->|Hut]; [done|].
        destruct (w_slots _ Hw2 x dm Hdm (or_introl (conj Hp2' Hk2')) u Hu) as [_ Hpu].
        refine (Hqf u Hut Hpu _ Hqg). intros HTu.
        destruct (J3 u x dm HTu Hdm Hk2' Hp2' Hu) as [K|[K _]]; done.
      + intros x dx Hx Hdx Hq u Hu Hnu.
        assert (Hxt : x <> t) by (intros ->; done).
        assert (Hrx : x ∈ g_running s2) by (rewrite Run2; by right).
        destruct (F1b x dx Hxt Hdx) as (dm&Hdm&Hr). pose proof (Hmemo x dm dx Hdm Hr Hq) as Hq2.
        assert (Hu2 : u ∈ read_prefix (g_edges s2) x).
        { rewrite read_prefix_view, <- Sv', <- read_prefix_view by done. exact Hu. }
        destruct (decide (u = t)) as [->|Hut].
        * assert (HnotLow : t < x \/ ~ pure_in (shapes s) x).
          { destruct Hq2 as [[Y1 Y2]|[Y1 _]].
            - left. pose proof (S1 x Hx ltac:(by rewrite <- Hsh2)). lia.
            - right. intros Hpx. exact (pure_kind s2 x dm ltac:(by rewrite Hsh2) Hdm Y1). }
          assert (Hnrn : ~ rn s0 t (Some t) x).
          { intros [[H1 H2]|K]; [|congruence]. destruct HnotLow as [L|L]; [lia|]. apply L. by rewrite <- Sh0. }
          destruct (Hup x Hxt Hnrn) as [X Y].
          destruct (HstartNode x dm Hxt HnotLow Hdm) as (dcs&Hdcs&Hr0).
          assert (Hqs : qc s x dcs) by exact (qc_shapes s s2 x dcs dm Hsh2 Hdcs Hdm Hq2).
          assert (Hpre0 : t ∈ read_prefix (g_edges s) x).
          { rewrite read_prefix_view, <- X, <- read_prefix_view. exact Hu. }
          destruct (S5 x dcs Hx Hdcs Hqs t Hpre0 Hnr) as [Hrs Hqs0].
          assert (Qd : quiet d).
          { destruct Hqs0 as (e&He&[Q|Q]); rewrite Hd in He; injection He as <-; [done|congruence]. }
          split; [exact (Hrdt x Hrs X Y (Hqv Qd))|exact Hqt].
        * assert (Hnu2 : u ∉ g_running s2) by (rewrite Run2; apply not_elem_of_cons; done).
          destruct (J5 x dm Hrx Hdm Hq2 u Hu2 Hnu2) as [A1 B1].
          assert (Hsl2 : u ∈ (slot_view (g_edges s2) x).1) by (rewrite read_prefix_view in Hu2; by apply elem_take in Hu2).
          destruct (w_slots _ Hw2 x dm Hdm Hq2 u Hsl2) as [_ Hpu].
          split; [exact (Hrd x u Hut A1)|exact (Hqf u Hut Hpu (Hntw u Hnu2) B1)].
      + intros x dx Hx Hdx Hq.
        assert (Hxt : x <> t) by (intros ->; done).
        assert (Hrx : x ∈ g_running s2) by (rewrite Run2; by right).
        destruct (F1b x dx Hxt Hdx) as (dm&Hdm&Hr). pose proof (J6 x dm Hrx Hdm (Hmemo x dm dx Hdm Hr Hq)) as K.
        pose proof Hr as (_&_&X&_). congruence.
      + rewrite Hdf, Hfl. exact S7.
      + exact S8.
    - intros c Hc. rewrite Pa' in Hc. apply Pa0, (at_passed _ _ _ _ _ A), Hc.
    - intros m dm Hdm. destruct (decide (m = t)) as [->|Hmt].
      + rewrite Hd in Hdm. injection Hdm as <-. exists dE. split; [done|congruence].
      + destruct (at_any _ _ _ _ _ A m dm ltac:(by rewrite Nd0)) as (e2&He2&E2).
        destruct (F1 m e2 Hmt He2) as (e3&He3&_&_&E3&_). exists e3. split; [done|congruence].
    - intros m dm Hdm. destruct (decide (m = t)) as [->|Hmt]; [exact (mk_is_Some _ _ Hd)|].
      destruct (F1b m dm Hmt Hdm) as (e2&He2&_). rewrite <- Nd0 by done. exact (at_back _ _ _ _ _ A m e2 He2).
    - intros m dm Hm Hdm. assert (Hmt : m <> t) by (intros ->; apply Hm; left; split; done).
      destruct (at_rise _ _ _ _ _ A m dm (rn_mono s s0 t lim None m Sh0 ltac:(lia) Hm) ltac:(by rewrite Nd0)) as (e2&He2&R1).
      destruct (F1 m e2 Hmt He2) as (e3&He3&R2). exists e3. split; [done|by eapply rise_node_trans].
    - intros c Hc. assert (Hct : c <> t) by (intros ->; apply Hc; left; split; done).
      apply (Hup c Hct). intros [[H1 H2]|K]; [|congruence]. apply Hc. left. split; [by rewrite <- Sh0|lia].
    - intros n dn Hdn Hpn HTn Hq. destruct (decide (n = t)) as [->|Hnt].
      + rewrite Hd in Hdn. injection Hdn as <-.
        assert (Qd : quiet d) by (destruct Hq as [Q|Q]; [done|congruence]).
        exists dE. split_and!; [done|exact (Hqv Qd)|by left|intros _; exact (Hqc Qd)].
      + assert (Hnt' : ~ tw T t d n) by (intros [K|[K _]]; done).
        destruct (at_keep _ _ _ _ _ A n dn ltac:(by rewrite Nd0) ltac:(by rewrite Sh0) Hnt' Hq) as (e2&He2&V&Q&C).
        destruct (quiet_dec e2) as [Qe|Qe].
        * exists e2. rewrite (F2 n Hnt ltac:(by rewrite Hsh2) Hnt').
          { split_and!; [done|done|by left|done]. }
          intros (e0&He0&Hun). rewrite He2 in He0. injection He0 as <-. exact (un_not_quiet _ Hun Qe).
        * destruct Q as [Q|Q]; [done|]. destruct (F1 n e2 Hnt He2) as (e3&He3&_&V3&X3&_).
          exists e3. split_and!; [done|congruence|right; congruence|].
          intros Hcl. exfalso. apply Qe. left. by apply C.
    - intros n dn Hn Hdn Hpn HTn Hq.
      assert (Hnt : n <> t) by (intros ->; apply Hn; left; split; done).
      assert (Hnt' : ~ tw T t d n) by (intros [K|[K _]]; done).
      pose proof (at_frozen _ _ _ _ _ A n dn (rn_mono s s0 t lim None n Sh0 ltac:(lia) Hn) ltac:(by rewrite Nd0) ltac:(by rewrite Sh0) Hnt' Hq) as Hf.
      rewrite (F2 n Hnt ltac:(by rewrite Hsh2) Hnt'); [done|].
      intros (e0&He0&Hun). rewrite Hf in He0. injection He0 as <-. exact (un_not_quiet _ Hun Hq).
  Qed.

  Lemma run_finish_parts g n mv s10 s' :
    run_finish g n mv s10 = Some (s', tt) ->
    let sg := ghost_run_end n (g_current s10) mv s10 in
    exists s11 d4,
      ((mv = false /\ s11 = sg) \/ (mv = true /\ notify_dirty_except g n (g_current s10) sg = Some s11)) /\
      g_nodes s11 !! n = Some d4 /\
      (s' = s11 \/ (n_freshness d4 <> Clean /\ s' = set_node n (node_with_unannounced true d4) s11)) /\
      (n_freshness d4 <> Clean -> f_unannounced (g_flags s11) = true -> s' = set_node n (node_with_unannounced true d4) s11).
  Proof.
    intros Hm sg. unfold run_finish in Hm. rewrite bind_get, bind_m_modify in Hm. cbv beta in Hm. fold sg in Hm.
    assert (Hs : exists s11, ((mv = false /\ s11 = sg) \/ (mv = true /\ notify_dirty_except g n (g_current s10) sg = Some s11)) /\
        (let* d3 := get_node n in if decide (n_freshness d3 = Clean) then m_return tt else announce n) s11 = Some (s', tt)).
    { destruct mv.
      - apply bind_step in Hm as (s11 & Hn & Hm). exists s11. split; [by right|exact Hm].
      - rewrite bind_m_return in Hm. exists sg. split; [by left|exact Hm]. }
    destruct Hs as (s11 & Hc & Hm2). exists s11.
    destruct (g_nodes s11 !! n) as [d4|] eqn:Hd4; [|unfold m_bind, get_node in Hm2; by rewrite Hd4 in Hm2].
    exists d4. rewrite (bind_get_node _ _ _ _ Hd4) in Hm2.
    split_and!; [done|done| |].
    - destruct (decide _) as [Hcl|Hcl].
      + left. unfold m_return in Hm2. by injection Hm2.
      + unfold announce in Hm2. destruct (f_unannounced (g_flags s11)).
        * right. split; [done|]. unfold modify_node in Hm2. rewrite Hd4 in Hm2. by injection Hm2.
        * left. by injection Hm2.
    - intros Hcl Hfl. rewrite decide_False in Hm2 by done. unfold announce in Hm2. rewrite Hfl in Hm2.
      unfold modify_node in Hm2. rewrite Hd4 in Hm2. by injection Hm2.
  Qed.

  Lemma hosted_parts {A} n (body : M A) s0 s2 r :
    hosted n body s0 = Some (s2, r) ->
    exists s1, body (with_current_owner None (with_current (Some n) s0)) = Some (s1, r) /\
      s2 = with_current_owner (g_current_owner s0) (with_current (g_current s0) s1).
  Proof.
    unfold hosted, m_bind, m_modify, m_return. destruct (body _) as [[s1 x]|]; [|done]. intros Hm. injection Hm as <- <-. eauto.
  Qed.

  Definition flds (x : state) := (g_current x, g_flags x, g_batch_depth x, g_pull_depth x, g_untracked x, g_flushing x).

  Lemma Arun_S : forall (T : node_id -> Prop) (lim t : node_id) s s' a d, aS T lim s -> t < lim -> pure_in (shapes s) t ->
    g_nodes s !! t = Some d -> n_kind d = KMemo -> n_disposed d = false ->
    run_memo (S f) t s = Some (s', a) -> aS T lim s' /\ atom T lim None s s' /\ done_at t s s'.
  Proof.
    intros T lim t s s' [] d Ha Hlt Hp Hd Hk Hl Hm0. pose proof Ha as (Hc&Hw&St).
    pose proof (Rrun_memo _ (runs_ok_all (S f)) t _ _ _ Hc ltac:(by exists d) Hm0) as [Hc' _].
    destruct (run_memo_parts f t s s' d Hd Hm0) as (s2 & r & Eh & Hrest).
    pose proof (run_begin T lim t s d Ha Hlt Hp Hd Hk Hl) as Ha0.
    set (T' := tw T t d) in *.
    set (s0 := with_current_owner None (with_current (Some t) (run_start t d s))) in *.
    assert (Hnr : t ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St t Hin Hp); lia).
    assert (HTt : ~ T t) by (intros K; exact (Hnr (st_T _ _ _ St t K))).
    (* the state at the body's start *)
    assert (Sh0 : shapes s0 = shapes s) by exact (shapes_set s t d (run_node d) Hd eq_refl).
    assert (Run0 : g_running s0 = t :: g_running s) by done.
    assert (Nd0 : forall m, m <> t -> g_nodes s0 !! m = g_nodes s !! m).
    { intros m Hm. change (<[t := run_node d]> (g_nodes s) !! m = g_nodes s !! m). by apply lookup_insert_ne. }
    assert (Ht0 : g_nodes s0 !! t = Some (run_node d)).
    { change (<[t := run_node d]> (g_nodes s) !! t = Some (run_node d)). apply lookup_insert_eq. }
    assert (Sv0 : forall c, c <> t -> slot_view (g_edges s0) c = slot_view (g_edges s) c).
    { intros c Hne. change (slot_view (sources_begin_run t (g_edges s)) c = slot_view (g_edges s) c). rewrite slot_view_begin. by rewrite decide_False. }
    assert (Se0 : forall c y, seen_of (g_ghost s0) c y = seen_of (g_ghost s) c y) by done.
    assert (Pa0 : forall c, c ∈ gh_passed (g_ghost s0) -> c ∈ gh_passed (g_ghost s)).
    { intros c Hc0. change (c ∈ gh_passed (g_ghost s) ∖ {[t]}) in Hc0. apply elem_of_difference in Hc0. tauto. }
    assert (Hcur0 : g_current s0 = Some t) by done.
    destruct (pure_node s t d Hp Hd) as [Hk'|(_&Hcq&Hbp)]; [congruence|].
    assert (Hp0 : pure_in (shapes s0) t) by (by rewrite Sh0).
    assert (Hqc0 : qc s0 t (run_node d)) by (left; split; done).
    (* the guide of a run that started quiet *)
    assert (Hgd : exists r0, quiet d -> body_eval (seen_ans s t) (n_env d) (n_body d) = Some r0 /\ view_matches d r0).
    { destruct (quiet_dec d) as [Q|Q].
      - assert (Hset : settled d) by (destruct Q as [Q|Q]; [left; congruence|by right]).
        destruct (w_value _ Hw t d Hd Hk Hp Hl Hnr Hset) as (r0&X&Y). exists r0. intros _. done.
      - exists (Ok 0%Z). intros K. done. }
    destruct Hgd as [r0 Hr0].
    assert (HnT : ~ T' t -> quiet d).
    { intros K. destruct (quiet_dec d) as [Q|Q]; [done|]. exfalso. apply K. by right. }
    assert (Hguide : ~ T' t -> body_eval (seen_ans s t) (n_env d) (n_body d) = Some r0 /\ guide T' s0 (seen_ans s t)).
    { intros K. pose proof (HnT K) as Q. split; [exact (proj1 (Hr0 Q))|].
      intros u o Ho. pose proof (seen_ans_prefix _ _ _ _ Ho) as Hpre.
      assert (Hsl : u ∈ (slot_view (g_edges s) t).1) by (rewrite read_prefix_view in Hpre; by apply elem_take in Hpre).
      destruct (w_slots _ Hw t d Hd (or_introl (conj Hp Hk)) u Hsl) as [Hu0 Hpu]. pose proof (Hu0 Hk) as Hu.
      assert (Hnu : u ∉ g_running s) by (intros Hin; pose proof (st_low _ _ _ St u Hin Hpu); lia).
      assert (Hset : settled d) by (destruct Q as [Q0|Q0]; [left; congruence|by right]).
      pose proof (w_reads _ Hw t d Hd (or_introl (conj Hp Hk)) Hl Hnr Hset u Hsl Hnu) as Hrd.
      destruct (w_quiet _ Hw t d Hd Hk Hp Hl Hnr Q u Hsl) as (e&He&Hqe).
      rewrite (seen_ans_view s t u e Hpre Hrd He) in Ho. injection Ho as <-.
      split_and!.
      - by rewrite Sh0.
      - intros [L|[L _]]; [exact (Hnu (st_T _ _ _ St u L))|lia].
      - exists e. rewrite Nd0 by lia. split_and!; done. }
    assert (Hor : T' t \/ (~ T' t /\ pure_in (shapes s0) t)).
    { destruct (quiet_dec d) as [Q|Q]; [right|left; by right]. split; [|done]. intros [L|[_ L]]; done. }
    (* the body *)
    destruct (hosted_parts _ _ _ _ _ Eh) as (s1 & Ex & Es2). fold s0 in Ex.
    assert (Hbp0 : body_pure (fun u => readable (shapes s0) u -> u < t /\ pure_in (shapes s0) u) (n_env d) (n_body d)) by (by rewrite Sh0).
    destruct (Aexec _ IH T' t t (run_node d) (run_env d) (n_env d) (n_body d) s0 s1 r (seen_ans s t) r0 Ha0 Hcur0 Ht0 Hqc0
                (env_agree_run d) (or_introl (conj Hk eq_refl)) Hbp0 Hguide Hor Ex) as ((Hc1&Hw1&St1)&A1&Hev&Hrr&_).
    assert (Hrnt : ~ rn s0 t None t) by (intros [[_ K]|K]; [lia|done]).
    destruct (at_rise _ _ _ _ _ A1 t (run_node d) Hrnt Ht0) as (d1&Hd1&R1).
    pose proof R1 as (Sh1&Vw1&Di1&_&_&Sc1&Vi1&Fr1&Fl1).
    destruct (w_memo _ Hw t d Hd Hk Hp) as (Hsc&Hvi&_).
    assert (Hd1s2 : g_nodes s2 !! t = Some d1) by (subst s2; exact Hd1).
    assert (Hk1 : n_kind d1 = KMemo) by (rewrite (rise_kind _ _ R1); done).
    destruct (Hrest d1 Hd1s2 ltac:(rewrite Vi1; done) ltac:(rewrite Di1; done) ltac:(rewrite (rise_comparer _ _ R1); done)) as (w&d10&mv&Hwe&Ed10&Hmv&Hfin).
    set (s10 := with_running (tail (g_running s2)) (with_edges w (set_node t d10 s2))) in Hfin.
    destruct (run_finish_parts f t mv s10 s' Hfin) as (s11&d4&Hnot&Hd4&Hs1'&Hs2').
    set (sg := ghost_run_end t (g_current s10) mv s10) in *.
    (* the state ahead of the notice *)
    assert (Hd10 : g_nodes s10 !! t = Some d10) by (subst s10; simpl; apply lookup_insert_eq).
    assert (Hsg : g_nodes sg = g_nodes s10 /\ g_edges sg = w /\ (forall c y, seen_of (g_ghost sg) c y = seen_of (g_ghost s1) c y) /\
              gh_passed (g_ghost sg) = gh_passed (g_ghost s1) /\ flds sg = flds s10 /\ g_running sg = g_running s10).
    { subst sg. unfold ghost_run_end. rewrite Hd10. subst s10 s2. split_and!; done. }
    destruct Hsg as (Ng&Eg&Seg&Pag&Fg&Rg).
    assert (Hdg : g_nodes sg !! t = Some d10) by (by rewrite Ng).
    assert (Hshd10 : shape_of d10 = shape_of d).
    { rewrite Ed10. destruct r; exact Sh1. }
    assert (Hviewd1 : read_view_of d1 = (n_value d, StNone, None)) by (rewrite Vw1; done).
    unfold read_view_of in Hviewd1. injection Hviewd1 as Vv1 Vs1 Vf1.
    assert (Hvm : view_matches d10 r).
    { rewrite Ed10. destruct r; simpl; split_and!; done. }
    assert (Hflag1 : n_unannounced d1 = false).
    { destruct (n_unannounced d1) eqn:E; [|done]. pose proof (Fl1 eq_refl) as K. done. }
    assert (Hflag10 : n_unannounced d10 = false) by (rewrite Ed10; destruct r; done).
    assert (Hfr10 : n_freshness d10 = n_freshness d1) by (rewrite Ed10; destruct r; done).
    assert (Hdi10 : n_disposed d10 = false) by (rewrite Ed10; destruct r; simpl; rewrite Di1; done).
    assert (Hsc10 : n_scope d10 = None) by (rewrite Ed10; destruct r; simpl; rewrite Sc1; done).
    assert (Hvi10 : n_violated d10 = false) by (rewrite Ed10; destruct r; simpl; [rewrite Vi1|]; done).
    assert (Hval10 : forall y, r = Fail y -> n_value d10 = n_value d) by (intros y ->; rewrite Ed10; simpl; done).
    assert (Rg1 : g_running s10 = g_running s).
    { subst s10 s2. simpl. rewrite (at_running _ _ _ _ _ A1), Run0. done. }
    (* a run that started quiet answers as it did *)
    pose proof (proj1 (proj2 Hc1)) as Hb1.
    assert (Hqview : quiet d -> read_view_of d10 = read_view_of d).
    { intros Q. destruct (Hr0 Q) as [_ Hm0d].
      assert (HnotT : ~ T' t) by (intros [L|[_ L]]; done).
      rewrite (Hrr HnotT) in Hvm.
      unfold read_view_of. destruct r0 as [v|y]; simpl in Hvm, Hm0d.
      - destruct Hvm as (A1'&A2'&A3'). destruct Hm0d as (B1'&B2'&B3'). rewrite A1', A2', A3', B1', B2', B3'. done.
      - destruct Hvm as (A2'&A3'). destruct Hm0d as (B2'&B3').
        pose proof (Hrr HnotT) as Er. rewrite A2', A3', B2', B3', (Hval10 y Er). done. }
    assert (Hmvt : mv = true -> T' t).
    { intros Hmt. destruct (quiet_dec d) as [Q|Q]; [|by right]. exfalso.
      pose proof (proj2 Hmv (Hqview Q)). congruence. }
    (* the marks of the notice *)
    assert (Hsh10 : shapes sg = shapes s1).
    { unfold shapes. rewrite Ng. subst s10 s2. change (shape_of <$> <[t := d10]> (g_nodes s1) = shape_of <$> g_nodes s1).
      rewrite fmap_insert.
      assert (Hx : shape_of d10 = shape_of d1) by (rewrite Ed10; destruct r; reflexivity). rewrite Hx.
      apply insert_id. by rewrite lookup_fmap, Hd1. }
    assert (Hne10 : forall m, m <> t -> g_nodes sg !! m = g_nodes s1 !! m).
    { intros m Hm. rewrite Ng. subst s10 s2. change (<[t := d10]> (g_nodes s1) !! m = g_nodes s1 !! m). by apply lookup_insert_ne. }
    assert (Hv10 : forall c, c <> t -> slot_view w c = slot_view (g_edges s1) c).
    { intros c Hne. assert (Hw1' : sources_end_run t (g_edges s1) = Some w) by (subst s2; exact Hwe).
      rewrite (slot_view_end t _ w c Hw1' (b_cursor _ Hb1 t)). by rewrite decide_False. }
    assert (Hvt10 : slot_view w t = (take (slot_view (g_edges s1) t).2 (slot_view (g_edges s1) t).1, (slot_view (g_edges s1) t).2)).
    { assert (Hw1' : sources_end_run t (g_edges s1) = Some w) by (subst s2; exact Hwe).
      rewrite (slot_view_end t _ w t Hw1' (b_cursor _ Hb1 t)). by rewrite decide_True. }
    assert (Hedges' : g_edges s' = w).
    { destruct Hs1' as [->|[_ ->]]; destruct Hnot as [[_ ->]|[_ Hn]]; try done.
      all: pose proof (any_mark_rise f t (g_current s10) sg s11 ltac:(right; right; right; right; exact Hn)) as R0;
           rewrite ?set_node_edges, (rs_edges _ _ R0); done. }
    assert (Hrw : world_reachable w).
    { rewrite <- Hedges'. exact (me_reachable _ (b_env _ (proj1 (proj2 Hc')))). }
    assert (Hslot_obs : forall x y, y ∈ observer_view (observers_of w x) -> x ∈ (slot_view w y).1).
    { intros x y Hin. exact (proj1 (held_iff_slot w y x (reachable_well_formed _ Hrw)) Hin). }
    assert (HC : T' t -> forall x y, ysrc T' sg x -> y ∈ observer_view (observers_of (g_edges sg) x) -> iny T' sg y).
    { intros HTt' x y Hx Hin. rewrite Eg in Hin. intros Hpy. pose proof (Hslot_obs x y Hin) as Hsl.
      assert (Hpy1 : pure_in (shapes s1) y) by (by rewrite <- Hsh10).
      destruct (decide (y = t)) as [->|Hyt]; [by left|].
      rewrite Hv10 in Hsl by done.
      destruct (pure_exists s1 y Hpy1) as [dy Hdy].
      assert (Hdyg : g_nodes sg !! y = Some dy) by (by rewrite Hne10).
      destruct (pure_node s1 y dy Hpy1 Hdy) as [Hks|(Hkm&_&_)].
      { exfalso. rewrite (me_signal _ (b_env _ Hb1) y dy Hdy Hks) in Hsl. by apply not_elem_of_nil in Hsl. }
      destruct (w_slots _ Hw1 y dy Hdy (or_introl (conj Hpy1 Hkm)) x Hsl) as [_ Hpx].
      pose proof (Hx ltac:(by rewrite Hsh10)) as HTx.
      destruct (st_obs _ _ _ St1 x y dy HTx Hdy Hkm Hpy1 Hsl) as [K|[_ K]]; [by left|right; eauto]. }
    assert (Htch : ytouch T' sg s11).
    { destruct Hnot as [[_ ->]|[Hmt Hn]]; [apply ytouch_refl|].
      pose proof (Hmvt Hmt) as HTt'.
      refine (ytouch_notify_except T' sg (HC HTt') f t (g_current s10) s11 _ Hn).
      intros y Hy. right. exact (HC HTt' t y (fun _ => HTt') Hy). }
    pose proof (proj1 Htch) as R0.
    assert (Htr : g_trace s' = g_trace s1).
    { assert (X1 : g_trace s' = g_trace s11) by (destruct Hs1' as [->|[_ ->]]; reflexivity).
      rewrite X1, (rs_trace _ _ R0). subst sg. unfold ghost_run_end. assert (X2 : g_trace s10 = g_trace s2) by reflexivity.
      assert (X3 : g_trace s2 = g_trace s1) by (rewrite Es2; reflexivity).
      repeat case_match; simpl; congruence. }
    assert (Hwed : g_flush_owed s' = g_flush_owed s1).
    { assert (X1 : g_flush_owed s' = g_flush_owed s11) by (destruct Hs1' as [->|[_ ->]]; reflexivity).
      rewrite X1, (rs_owed _ _ R0). subst sg. unfold ghost_run_end. assert (X2 : g_flush_owed s10 = g_flush_owed s2) by reflexivity.
      assert (X3 : g_flush_owed s2 = g_flush_owed s1) by (rewrite Es2; reflexivity).
      repeat case_match; simpl; congruence. }
    assert (How : g_owners s' = g_owners s1).
    { assert (X1 : g_owners s' = g_owners s11) by (destruct Hs1' as [->|[_ ->]]; reflexivity).
      rewrite X1, (rs_owners _ _ R0). subst sg. unfold ghost_run_end. assert (X2 : g_owners s10 = g_owners s2) by reflexivity.
      assert (X3 : g_owners s2 = g_owners s1) by (rewrite Es2; reflexivity).
      repeat case_match; simpl; congruence. }
    destruct (rs_fwd _ _ R0 t d10 Hdg) as (d4'&Hd4'&R4). rewrite Hd4 in Hd4'. injection Hd4' as <-.
    assert (Hfl10 : flds s10 = flds s).
    { unfold flds. subst s10 s2. simpl.
      rewrite (at_flags _ _ _ _ _ A1), (at_batch _ _ _ _ _ A1), (at_pull _ _ _ _ _ A1), (at_untracked _ _ _ _ _ A1), (at_flushing _ _ _ _ _ A1).
      reflexivity. }
    assert (Hfl11 : flds s11 = flds s).
    { rewrite <- Hfl10, <- Fg. unfold flds. rewrite (rs_current _ _ R0), (rs_flags _ _ R0), (rs_batch _ _ R0), (rs_pull _ _ R0), (rs_untracked _ _ R0), (rs_flushing _ _ R0). reflexivity. }
    assert (Hflag11 : f_unannounced (g_flags s11) = true).
    { assert (X : g_flags s11 = g_flags s) by (unfold flds in Hfl11; by injection Hfl11).
      rewrite X. exact (b_flag_u _ (proj1 (proj2 Hc))). }
    (* the flag of a run that ends stale *)
    assert (Hend : exists dE, g_nodes s' !! t = Some dE /\ shape_of dE = shape_of d4 /\ read_view_of dE = read_view_of d4 /\
              n_freshness dE = n_freshness d4 /\ n_disposed dE = n_disposed d4 /\ n_scope dE = n_scope d4 /\ n_violated dE = n_violated d4 /\
              quiet dE /\ (n_freshness dE = Clean -> n_unannounced dE = false) /\
              (forall m, m <> t -> g_nodes s' !! m = g_nodes s11 !! m) /\ g_edges s' = g_edges s11 /\ g_ghost s' = g_ghost s11 /\
              g_running s' = g_running s11 /\ flds s' = flds s11 /\ shapes s' = shapes s11).
    { destruct (decide (n_freshness d4 = Clean)) as [Hcl|Hcl].
      - destruct Hs1' as [->|[K _]]; [|done].
        exists d4. split_and!; try done; [by left|].
        intros _. destruct (n_unannounced d4) eqn:E; [|done]. pose proof R4 as (_&_&_&_&_&_&_&_&K). rewrite Hflag10 in K. pose proof (K E). done.
      - rewrite (Hs2' Hcl Hflag11). exists (node_with_unannounced true d4). split_and!; try done.
        + simpl. apply lookup_insert_eq.
        + by right.
        + intros m Hm. simpl. by apply lookup_insert_ne.
        + exact (shapes_set s11 t d4 (node_with_unannounced true d4) Hd4 eq_refl). }
    destruct Hend as (dE&HdE&ShE&VwE&FrE&DiE&ScE&ViE&QE&CfE&NdE&EdE&GhE&RuE&FlE&ShsE).
    assert (Hshs : shapes s' = shapes s1) by (rewrite ShsE, (rise_shapes _ _ R0), Hsh10; done).
    assert (Hruns : g_running s' = g_running s) by (rewrite RuE, (rs_running _ _ R0), Rg, Rg1; done).
    assert (Hfls : flds s' = flds s) by (by rewrite FlE).
    assert (Hed : g_edges s' = w) by exact Hedges'.
    assert (HgG : g_ghost s' = g_ghost sg) by (rewrite GhE, (rs_ghost _ _ R0); done).
    pose proof (Fl1) as _.
    assert (Hvmv : read_view_of dE = read_view_of d10).
    { rewrite VwE. pose proof R4 as (_&X&_). exact X. }
    assert (Hshd : shape_of dE = shape_of d).
    { rewrite ShE. pose proof R4 as (X&_). rewrite X. exact Hshd10. }
    assert (Hqcd : quiet d -> n_freshness dE = Clean).
    { intros Q. assert (Hmf : mv = false) by (apply Hmv; exact (Hqview Q)).
      destruct Hnot as [[_ Hs]|[Hmt _]]; [|congruence]. subst s11.
      rewrite Hdg in Hd4. injection Hd4 as <-. rewrite FrE, Hfr10.
      assert (HnotT : ~ T' t) by (intros [L|[_ L]]; done).
      destruct (at_keep _ _ _ _ _ A1 t (run_node d) Ht0 Hp0 HnotT (or_introl (or_introl eq_refl))) as (e&He&_&_&C).
      rewrite Hd1 in He. injection He as <-. exact (C eq_refl). }
    assert (Hmvp : read_view_of dE <> read_view_of d -> forall c dc, g_nodes s' !! c = Some dc -> c ∉ g_running s' -> n_disposed dc = false ->
       t ∈ (slot_view (g_edges s') c).1 -> qc s' c dc -> ~ settled dc).
    { intros Hne c dc Hdc Hnrc Hlc Hin Hqc Hse.
      assert (Hmt : mv = true).
      { destruct mv; [done|]. exfalso. apply Hne. rewrite Hvmv. by apply Hmv. }
      assert (HTt' := Hmvt Hmt).
      destruct Hnot as [[K _]|[_ Hn]]; [congruence|].
      assert (Hct : c <> t).
      { intros ->. rewrite Hed, Hvt10 in Hin. simpl in Hin. apply elem_take in Hin.
        assert (Hp1 : pure_in (shapes s1) t) by (rewrite (at_shapes _ _ _ _ _ A1); exact Hp0).
        pose proof (w_slots _ Hw1 t d1 Hd1 (or_introl (conj Hp1 Hk1)) t Hin) as [X _]. pose proof (X Hk1). lia. }
      rewrite NdE in Hdc by done.
      destruct (rise_back _ _ _ _ R0 Hdc) as (dm&Hdmg&Rm).
      rewrite Hne10 in Hdmg by done.
      rewrite Hed in Hin. pose proof Hin as Hin0. rewrite Hv10 in Hin0 by done.
      assert (Hobs : c ∈ observer_view (observers_of (g_edges sg) t)).
      { rewrite Eg. exact (proj2 (held_iff_slot w c t (reachable_well_formed _ Hrw)) Hin). }
      assert (Hqm : qc s1 c dm).
      { refine (qc_shapes s1 s' c dm dc Hshs Hdmg _ Hqc). rewrite NdE by done. exact Hdc. }
      assert (Hrc1 : c ∉ g_running s1).
      { rewrite (at_running _ _ _ _ _ A1), Run0. apply not_elem_of_cons. split; [done|]. by rewrite <- Hruns. }
      assert (Hflc : n_unannounced dm = false).
      { destruct Hqm as [[Y1 Y2]|Y].
        - destruct (st_obs _ _ _ St1 t c dm HTt' Hdmg Y2 Y1 Hin0) as [K|[_ K]]; [exfalso; apply Hrc1; exact (st_T _ _ _ St1 c K)|apply K].
        - exact (proj2 (w_effect _ Hw1 c dm Hdmg Y)). }
      assert (Hflc' : n_unannounced dc = false).
      { pose proof Rm as (_&_&_&_&_&_&_&_&K). destruct (n_unannounced dc); [|done]. rewrite Hflc in K. pose proof (K eq_refl). done. }
      assert (Hcln : n_in_cleanup dc = false).
      { destruct (n_in_cleanup dc) eqn:Ec; [|done]. exfalso. apply Hnrc.
        exact (b_cleanup_running _ (proj1 (proj2 Hc')) c dc ltac:(rewrite NdE by done; exact Hdc) Ec). }
      assert (Htk : takes_marks sg c).
      { exists dm. split; [by rewrite Hne10|]. destruct Hqm as [[_ Y2]|[Y2 _]]; [by left|right].
        pose proof Rm as (_&_&Di&Cl&_). split_and!; [done|congruence|congruence]. }
      assert (Hrne : g_current s10 <> Some c).
      { intros Hcc. apply Hnrc. rewrite Hruns.
        assert (Hcc' : g_current s = Some c).
        { unfold flds in Hfl10. injection Hfl10 as X. rewrite <- X. exact Hcc. }
        exact (b_current _ (proj1 (proj2 Hc)) c Hcc'). }
      destruct (notify_dirty_ends_dirty f t (g_current s10) sg s11 c Hobs Htk (or_intror (conj Hn Hrne))) as (dd&Hdd&Hff).
      rewrite Hdc in Hdd. injection Hdd as <-.
      destruct Hse as [K|K]; [done|congruence]. }
    destruct (finish_ok T lim t s s0 s1 s' d d1 dE r Hw St Hlt Hp Hd Hk Hl Sh0 Run0 Nd0 Sv0 Se0 Pa0 Hw1 St1 A1 Hd1 Hk1 Hev Hshs Hruns)
      as (Wf&Sf&Pf&Af&Bf&Rf&Sl&Kf&Zf).
    all: try done.
    - intros c Hct. rewrite Hed. by apply Hv10.
    - rewrite Hed, Hvt10, read_prefix_view. done.
    - rewrite Hed, !read_prefix_view, Hvt10. simpl. rewrite take_take, Nat.min_id. done.
    - intros c y. rewrite HgG. apply Seg.
    - rewrite HgG. exact Pag.
    - intros m dm Hm Hdm. rewrite NdE by done. refine (rs_fwd _ _ R0 m dm _). by rewrite Hne10.
    - intros m dm' Hm Hdm'. rewrite NdE in Hdm' by done. destruct (rise_back _ _ _ _ R0 Hdm') as (x&Hx&Rx). rewrite Hne10 in Hx by done. eauto.
    - intros m Hm Hpm HT0 Hun. rewrite NdE by done.
      destruct Htch as [_ Tch]. rewrite Tch; [by apply Hne10|by rewrite Hsh10|done|].
      intros (e&He&Hu). apply Hun. exists e. rewrite Hne10 in He by done. done.
    - eapply view_matches_view; [exact Hvmv|exact Hvm].
    - rewrite DiE. pose proof R4 as (_&_&X&_). congruence.
    - rewrite ScE. pose proof R4 as (_&_&_&_&_&X&_). congruence.
    - rewrite ViE. pose proof R4 as (_&_&_&_&_&_&X&_). congruence.
    - intros Q. rewrite Hvmv. exact (Hqview Q).
    - unfold deferring. unfold flds in Hfls. injection Hfls as X1 _ _ X2 _ _. rewrite X1, X2. reflexivity.
    - unfold flds in Hfls. injection Hfls as _ _ _ _ _ X. exact X.
    - unfold flds in Hfls. injection Hfls as X1 X2 X4 X5 X6 X7.
      split_and!.
      + split_and!; done.
      + split; try done.
        * rewrite Hshs, (at_shapes _ _ _ _ _ A1). done.
        * intros K. rewrite Hwed. exact (at_owed _ _ _ _ _ A1 K).
        * intros m Hm. rewrite Htr. refine (eq_trans (at_view _ _ _ _ _ A1 m _) _); [|reflexivity].
          intros [[Hpm Hlm]|K]; apply Hm; left; [split; [by rewrite <- Sh0|lia]|injection K as <-; done].
        * rewrite How, (at_owners _ _ _ _ _ A1). reflexivity.
      + split.
        * exists dE. split; [done|]. by left.
        * intros d0 Hd0 Q. rewrite Hd in Hd0. injection Hd0 as <-. exists dE. split; [done|left; exact (Hqcd Q)].
  Qed.
End Step.

Lemma A_ok_all f : A_ok f.
Proof.
  induction f as [|f IH]; [exact A_ok_0|]. split.
  - exact (Aexec_S f IH).
  - exact (Aread_S f IH).
  - exact (Anode_S f IH).
  - exact (Aupd_S f IH).
  - exact (Arop_S f IH).
  - exact (Apull_S f IH).
  - exact (Aref_S f IH).
  - exact (Awalk_S f IH).
  - exact (Arec_S f IH).
  - exact (Arun_S f IH).
Qed.

(** ** Outside the pure nodes *)

(** The invariant at a step of the engine outside every pure run. *)
Definition np (s : state) : Prop := forall m dm, m ∈ g_running s -> g_nodes s !! m = Some dm -> ~ qc s m dm.

(** A new effect, ahead of the mark that schedules its first run. *)
Definition window (d : node) : Prop := n_status d = StUninitialized.

(** The effect ran, and its latest run read and logged what its body answers over the recorded reads. *)
Definition ran_ok (s : state) (c : node_id) (d : node) : Prop :=
  exists r l pre,
    body_eval (seen_ans s c) (n_env d) (n_body d) = Some r /\ body_logs (seen_ans s c) (n_env d) (n_body d) = Some l /\
    trace_view c (g_trace s) = pre ++ EvRun c :: (EvLog c <$> l).

(** [X] holds the effects whose scheduling mark is due. *)
Definition ran (X : node_id -> Prop) (s : state) : Prop :=
  forall c d, g_nodes s !! c = Some d -> quiet_effect s c d -> n_disposed d = false -> n_freshness d <> Dirty ->
    (X c /\ window d) \/ ran_ok s c d.

(** The hosts of owners lie outside the quiet effects. *)
Definition hosts (s : state) : Prop :=
  g_nodes s !! (0 : node_id) = None /\
  forall o y, g_owners s !! o = Some y ->
    (o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d.

(** The facts beside [whole] and [np]. *)
Definition pd (X : node_id -> Prop) (s : state) : Prop :=
  (forall c, c ∈ gh_passed (g_ghost s) -> is_Some (g_nodes s !! c)) /\ ran X s /\ hosts s.

Definition wkx (X : node_id -> Prop) (s : state) : Prop := whole s /\ np s /\ pd X s.

Lemma seen_ans_ext s s' c :
  read_prefix (g_edges s') c = read_prefix (g_edges s) c -> (forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) ->
  forall t, seen_ans s' c t = seen_ans s c t.
Proof. intros H1 H2 t. unfold seen_ans. rewrite H1, H2. done. Qed.

Lemma ran_ok_ext s s' c d d' :
  read_prefix (g_edges s') c = read_prefix (g_edges s) c -> (forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) ->
  n_env d' = n_env d -> n_body d' = n_body d -> trace_view c (g_trace s') = trace_view c (g_trace s) ->
  ran_ok s c d -> ran_ok s' c d'.
Proof.
  intros H1 H2 He Hb Hv (r&l&pre&A&B&C). exists r, l, pre. rewrite He, Hb, Hv.
  assert (Hm : forall t o, seen_ans s c t = Some o -> seen_ans s' c t = Some o) by (intros t o H; exact (eq_trans (seen_ans_ext s s' c H1 H2 t) H)).
  split_and!; [exact (body_eval_mono _ _ _ _ _ Hm A)|exact (body_logs_mono _ _ _ _ _ Hm B)|exact C].
Qed.

Lemma ran_frame X s s' :
  (forall c d', g_nodes s' !! c = Some d' -> quiet_effect s' c d' -> n_disposed d' = false -> n_freshness d' <> Dirty ->
     exists d, g_nodes s !! c = Some d /\ quiet_effect s c d /\ n_disposed d = false /\ n_freshness d <> Dirty /\ (window d -> window d') /\
       n_env d' = n_env d /\ n_body d' = n_body d /\ read_prefix (g_edges s') c = read_prefix (g_edges s) c /\
       (forall y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) /\ trace_view c (g_trace s') = trace_view c (g_trace s)) ->
  ran X s -> ran X s'.
Proof.
  intros F H c d' Hd Hq Hl Hf. destruct (F c d' Hd Hq Hl Hf) as (d&H1&H2&H3&H4&H5&H6&H7&H8&H9&H10).
  destruct (H c d H1 H2 H3 H4) as [[A B]|A]; [left; split; [done|auto]|right].
  exact (ran_ok_ext s s' c d d' H8 H9 H6 H7 H10 A).
Qed.

Lemma hosts_frame s s' :
  (forall m d', g_nodes s' !! m = Some d' -> exists d, g_nodes s !! m = Some d /\ (quiet_effect s' m d' -> quiet_effect s m d)) ->
  (forall m, is_Some (g_nodes s !! m) -> is_Some (g_nodes s' !! m)) ->
  (forall o y, g_owners s' !! o = Some y -> (exists y0, g_owners s !! o = Some y0 /\ o_host y0 = o_host y) \/
     ((o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d)) ->
  hosts s -> hosts s'.
Proof.
  intros Bk Fw Ow [H0 H]. split.
  - destruct (g_nodes s' !! (0 : node_id)) as [d'|] eqn:E; [|done]. destruct (Bk 0 d' E) as (d&Hd&_). congruence.
  - intros o y Hy.
    assert (K : (o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d).
    { destruct (Ow o y Hy) as [(y0&Hy0&E)|K]; [|done]. rewrite <- E. exact (H o y0 Hy0). }
    destruct K as [K1 K2]. split.
    + destruct K1 as [K1|K1]; [by left|right; by apply Fw].
    + intros d' Hd' Hq. destruct (Bk _ d' Hd') as (d&Hd&Hb). exact (K2 d Hd (Hb Hq)).
Qed.

Section WX.
Context {Xe : node_id -> Prop}.
Local Notation wk := (wkx Xe).

Lemma np_pure s t : run_base s -> np s -> pure_in (shapes s) t -> t ∉ g_running s.
Proof.
  intros Hb Hn Hp Hin. destruct (pure_exists s t Hp) as [d Hd].
  destruct (pure_node s t d Hp Hd) as [Hk|(Hk&_&_)].
  - destruct (b_running_kind _ Hb t Hin) as [(e&He&K)|(e&He&K)]; rewrite Hd in He; injection He as <-; congruence.
  - exact (Hn t d Hin Hd (or_introl (conj Hp Hk))).
Qed.

Lemma stk_empty lim s :
  run_base s -> wk s -> (deferring s = true \/ g_flushing s = true) -> stk (fun _ => False) lim s.
Proof.
  intros Hb [Hw [Hn _]] Hd. split; try done.
  - intros m Hm Hp. exfalso. exact (np_pure s m Hb Hn Hp Hm).
  - intros x dx Hx Hdx Hk Hp _. exfalso. exact (Hn x dx Hx Hdx (or_introl (conj Hp Hk))).
  - intros x dx Hx Hdx Hq. exfalso. exact (Hn x dx Hx Hdx Hq).
  - intros x dx Hx Hdx Hq. exfalso. exact (Hn x dx Hx Hdx Hq).
  - intros x. right. intros K; exact K.
Qed.

(** What the invariant reads of a pure node or a quiet effect. *)
Definition wsame (d d' : node) : Prop :=
  shape_of d' = shape_of d /\ read_view_of d' = read_view_of d /\ (n_disposed d = true -> n_disposed d' = true) /\ n_scope d' = n_scope d /\
  n_violated d' = n_violated d /\ n_freshness d' = n_freshness d /\ n_unannounced d' = n_unannounced d.

Definition isq (s : state) (m : node_id) (d : node) : Prop := pure_in (shapes s) m \/ quiet_effect s m d.

Record wframe (s s' : state) : Prop := {
  wf_sh : shapes s' = shapes s;
  wf_fwd : forall m d, g_nodes s !! m = Some d -> isq s m d -> exists d', g_nodes s' !! m = Some d' /\ wsame d d';
  wf_back : forall m d', g_nodes s' !! m = Some d' -> exists d, g_nodes s !! m = Some d /\ shape_of d' = shape_of d /\ (isq s m d -> wsame d d');
  wf_slots : forall c d, g_nodes s !! c = Some d -> isq s c d ->
    slot_view (g_edges s') c = slot_view (g_edges s) c \/ ((slot_view (g_edges s') c).1 = [] /\ exists d', g_nodes s' !! c = Some d' /\ n_disposed d' = true);
  wf_seen : forall c d y, g_nodes s !! c = Some d -> isq s c d -> seen_of (g_ghost s') c y = seen_of (g_ghost s) c y;
  wf_passed : forall c, c ∈ gh_passed (g_ghost s') -> c ∈ gh_passed (g_ghost s) \/
    (is_Some (g_nodes s !! c) /\ ~ pure_in (shapes s) c /\ ~ all_pure s);
  wf_run : forall m d', m ∈ g_running s' -> g_nodes s' !! m = Some d' -> m ∈ g_running s \/ exists d, g_nodes s !! m = Some d /\ ~ isq s m d;
  wf_view : forall c d, g_nodes s !! c = Some d -> quiet_effect s c d -> trace_view c (g_trace s') = trace_view c (g_trace s);
  wf_host : forall o y, g_owners s' !! o = Some y -> (exists y0, g_owners s !! o = Some y0 /\ o_host y0 = o_host y) \/
    ((o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d)
}.

Lemma qc_isq s c d : qc s c d -> isq s c d.
Proof. intros [[H _]|H]; [by left|by right]. Qed.

Lemma isq_shape s s' m d d' : shapes s' = shapes s -> shape_of d' = shape_of d -> quiet_effect s' m d' -> quiet_effect s m d.
Proof.
  intros Hs Hsh Hq. unfold shape_of in Hsh. injection Hsh as K Q E B.
  unfold quiet_effect in *. rewrite Hs in Hq. destruct Hq as [H1 H2]. rewrite E, B in H2. split; [congruence|done].
Qed.

Lemma isq_shape_fwd s s' m d d' : shapes s' = shapes s -> shape_of d' = shape_of d -> quiet_effect s m d -> quiet_effect s' m d'.
Proof.
  intros Hs Hsh Hq. unfold shape_of in Hsh. injection Hsh as K Q E B.
  unfold quiet_effect in *. rewrite Hs. destruct Hq as [H1 H2]. rewrite E, B. split; [congruence|done].
Qed.

Lemma qc_shape_back s s' m d d' : shapes s' = shapes s -> shape_of d' = shape_of d -> qc s' m d' -> qc s m d.
Proof.
  intros Hs Hsh [[H1 H2]|H]; [left; split; [by rewrite <- Hs|]|right].
  - unfold shape_of in Hsh. injection Hsh as K _ _ _. congruence.
  - eapply isq_shape; [exact Hs|exact Hsh|exact H].
Qed.

Lemma all_pure_frame s s' :
  shapes s' = shapes s -> (forall m d, g_nodes s !! m = Some d -> exists d', g_nodes s' !! m = Some d' /\ shape_of d' = shape_of d) ->
  all_pure s' -> all_pure s.
Proof.
  intros Hs Hf Ha n d Hd Hk. destruct (Hf n d Hd) as (d'&Hd'&Hsh). rewrite <- Hs. refine (Ha n d' Hd' _).
  unfold shape_of in Hsh. injection Hsh as K _ _ _. congruence.
Qed.

Lemma wsame_view_matches d d' r : wsame d d' -> view_matches d r -> view_matches d' r.
Proof. intros (_&V&_). by apply view_matches_view. Qed.

Lemma wframe_apply s s' :
  run_base s -> wframe s s' -> wk s -> wk s'.
Proof.
  intros Hb [Sh Fw Bk Sl Sn Pa Rn Vw Ho] [Hw [Hn [Hp [Hran Hho]]]].
  assert (Hqc : forall c d d', g_nodes s !! c = Some d -> shape_of d' = shape_of d -> qc s' c d' -> qc s c d).
  { intros c d d' _ Hsh Hq. exact (qc_shape_back _ _ _ _ _ Sh Hsh Hq). }
  assert (Hsom : forall m, is_Some (g_nodes s !! m) -> is_Some (g_nodes s' !! m)).
  { intros m [d Hd]. pose proof (shapes_lookup s m d Hd) as H1. rewrite <- Sh in H1. unfold shapes in H1. rewrite lookup_fmap in H1.
    destruct (g_nodes s' !! m) as [d'|] eqn:E; [by eexists|done]. }
  assert (Hall : all_pure s' -> all_pure s).
  { apply all_pure_frame; [done|]. intros m d Hd.
    pose proof (shapes_lookup s m d Hd) as H1. rewrite <- Sh in H1. unfold shapes in H1. rewrite lookup_fmap in H1.
    destruct (g_nodes s' !! m) as [d'|] eqn:E; [|done]. simpl in H1. exists d'. split; [done|]. congruence. }
  assert (Hrd : forall c d t, g_nodes s !! c = Some d -> isq s c d -> pure_in (shapes s) t -> rd_ok s c t -> rd_ok s' c t).
  { intros c d t Hd Hi Hpt (H1&e&He&H2). unfold rd_ok. rewrite (Sn c d t Hd Hi). split; [done|].
    destruct (Fw t e He (or_introl Hpt)) as (e'&He'&_&V&_). exists e'. split; [done|]. intros v Hv. rewrite V. auto. }
  assert (Hqg : forall t, pure_in (shapes s) t -> quiet_or_gone s t -> quiet_or_gone s' t).
  { intros t Hpt (e&He&Q). destruct (Fw t e He (or_introl Hpt)) as (e'&He'&_&_&Di&_&_&Fr&Fl). exists e'. split; [done|].
    destruct Q as [[Q|Q]|Q]; [left; left; congruence|left; right; congruence|right; auto]. }
  assert (Hlive : forall c d d', g_nodes s !! c = Some d -> g_nodes s' !! c = Some d' -> isq s c d -> wsame d d' -> n_disposed d' = false ->
            n_disposed d = false /\ slot_view (g_edges s') c = slot_view (g_edges s) c).
  { intros c d d' Hd Hd' Hi (_&_&Di&_) Hl.
    assert (Hl0 : n_disposed d = false) by (destruct (n_disposed d); [rewrite Di in Hl; done|done]).
    split; [done|]. destruct (Sl c d Hd Hi) as [X|[_ (x&Hx&X)]]; [done|]. rewrite Hd' in Hx. injection Hx as <-. congruence. }
  split; [split|split].
  - intros n d' Hd' Hk. destruct (Bk n d' Hd') as (d&Hd&Hsh&K).
    assert (Hkd : n_kind d = KSignal) by (unfold shape_of in Hsh; injection Hsh as X _ _ _; congruence).
    assert (Hpn : pure_in (shapes s) n).
    { unfold pure_in. rewrite pure_at_unfold, (shapes_lookup s n d Hd). unfold shape_of. rewrite Hkd. done. }
    destruct (K (or_introl Hpn)) as (_&V&_). destruct (w_signal _ Hw n d Hd Hkd) as [A B].
    unfold read_view_of in V. injection V as _ X Y. split; congruence.
  - rewrite Sh. intros c d' Hd' Hk Hp0. destruct (Bk c d' Hd') as (d&Hd&Hsh&K).
    assert (Hkd : n_kind d = KMemo) by (unfold shape_of in Hsh; injection Hsh as X _ _ _; congruence).
    destruct (w_memo _ Hw c d Hd Hkd Hp0) as (A&B&C). destruct (K (or_introl Hp0)) as (_&_&_&Sc&Vi&Fr&Fl).
    split_and!; [congruence|congruence|]. intros Hcl. rewrite Fl. apply C. congruence.
  - intros c d' Hd' Hq. destruct (Bk c d' Hd') as (d&Hd&Hsh&K). pose proof (isq_shape _ _ _ _ _ Sh Hsh Hq) as Hq0.
    destruct (K (or_intror Hq0)) as (_&_&_&Sc&_&_&Fl). destruct (w_effect _ Hw c d Hd Hq0) as [A B]. split; congruence.
  - intros c d' Hd' Hq t Ht. rewrite Sh. destruct (Bk c d' Hd') as (d&Hd&Hsh&K). pose proof (Hqc c d d' Hd Hsh Hq) as Hq0.
    destruct (Sl c d Hd (qc_isq _ _ _ Hq0)) as [V|[V _]].
    + rewrite V in Ht. pose proof Hsh as Hs9. unfold shape_of in Hs9. injection Hs9 as Kd _ _ _. rewrite Kd. exact (w_slots _ Hw c d Hd Hq0 t Ht).
    + rewrite V in Ht. by apply not_elem_of_nil in Ht.
  - intros c d' Hd' Hq Hl Hnr Hse t Ht Hnt. destruct (Bk c d' Hd') as (d&Hd&Hsh&K). pose proof (Hqc c d d' Hd Hsh Hq) as Hq0.
    pose proof (qc_isq _ _ _ Hq0) as Hi. pose proof (K Hi) as Wm. pose proof Wm as (_&_&Di&_&_&Fr&Fl).
    destruct (Hlive c d d' Hd Hd' Hi Wm Hl) as [Hl0 V]. rewrite V in Ht.
    destruct (w_slots _ Hw c d Hd Hq0 t Ht) as [_ Hpt].
    refine (Hrd c d t Hd Hi Hpt (w_reads _ Hw c d Hd Hq0 Hl0 (fun Hin => Hn c d Hin Hd Hq0) _ t Ht (np_pure s t Hb Hn Hpt))).
    destruct Hse as [X|X]; [left|right]; congruence.
  - intros c d' Hd' Hk Hp0 Hl Hnr Hse. rewrite Sh in Hp0. destruct (Bk c d' Hd') as (d&Hd&Hsh&K).
    assert (Hkd : n_kind d = KMemo) by (unfold shape_of in Hsh; injection Hsh as X _ _ _; congruence).
    pose proof (K (or_introl Hp0)) as Hsm. pose proof Hsm as (_&_&Di&_&_&Fr&Fl).
    destruct (Hlive c d d' Hd Hd' (or_introl Hp0) Hsm Hl) as [Hl0 V].
    assert (Hset : settled d) by (destruct Hse as [X|X]; [left|right]; congruence).
    destruct (value_ext s s' c d ltac:(by rewrite !read_prefix_view, V) (fun y => Sn c d y Hd (or_introl Hp0)) (w_value _ Hw c d Hd Hkd Hp0 Hl0 (fun Hin => Hn c d Hin Hd (or_introl (conj Hp0 Hkd))) Hset)) as (r&Hb0&Hm0).
    unfold shape_of in Hsh. injection Hsh as _ _ E B. exists r. rewrite E, B. split; [done|]. exact (wsame_view_matches _ _ _ Hsm Hm0).
  - intros c d' Hd' Hk Hp0 Hl Hnr Hq t Ht. rewrite Sh in Hp0. destruct (Bk c d' Hd') as (d&Hd&Hsh&K).
    assert (Hkd : n_kind d = KMemo) by (unfold shape_of in Hsh; injection Hsh as X _ _ _; congruence).
    pose proof (K (or_introl Hp0)) as Hsm. pose proof Hsm as (_&_&Di&_&_&Fr&Fl).
    destruct (Hlive c d d' Hd Hd' (or_introl Hp0) Hsm Hl) as [Hl0 V]. rewrite V in Ht.
    destruct (w_slots _ Hw c d Hd (or_introl (conj Hp0 Hkd)) t Ht) as [_ Hpt].
    apply Hqg; [done|]. refine (w_quiet _ Hw c d Hd Hkd Hp0 Hl0 (fun Hin => Hn c d Hin Hd (or_introl (conj Hp0 Hkd))) _ t Ht).
    destruct Hq as [X|X]; [left|right]; congruence.
  - intros c Hc. rewrite Sh. destruct (Pa c Hc) as [X|(_&X&Y)].
    + destruct (w_passed _ Hw c X) as [A B]. split; [done|]. intros Ha. exact (B (Hall Ha)).
    + split; [done|]. intros Ha. exact (Y (Hall Ha)).
  - intros m d' Hm Hd' Hq. destruct (Bk m d' Hd') as (d&Hd&Hsh&_). pose proof (Hqc m d d' Hd Hsh Hq) as Hq0.
    destruct (Rn m d' Hm Hd') as [X|(d0&Hd0&X)].
    + exact (Hn m d X Hd Hq0).
    + rewrite Hd in Hd0. injection Hd0 as <-. exact (X (qc_isq _ _ _ Hq0)).
  - split_and!.
    + intros c Hc. destruct (Pa c Hc) as [K|(K&_)]; apply Hsom; [by apply Hp|done].
    + apply (ran_frame _ s); [|exact Hran]. intros c d' Hd' Hq Hl Hf. destruct (Bk c d' Hd') as (d&Hd&Hsh&K).
      pose proof (isq_shape _ _ _ _ _ Sh Hsh Hq) as Hq0. pose proof (K (or_intror Hq0)) as Hsm.
      destruct (Hlive c d d' Hd Hd' (or_intror Hq0) Hsm Hl) as [Hl0 V]. pose proof Hsm as (_&Vv&_&_&_&Fr&_).
      unfold shape_of in Hsh. injection Hsh as _ _ E B.
      exists d. split_and!; [done|done|done|congruence| | | | |intros y; exact (Sn c d y Hd (or_intror Hq0))|exact (Vw c d Hd Hq0)]; try done.
      * unfold window, read_view_of in *. injection Vv as _ S1 _. congruence.
      * by rewrite !read_prefix_view, V.
    + apply (hosts_frame s); [| |exact Ho|exact Hho].
      * intros m d' Hd'. destruct (Bk m d' Hd') as (d&Hd&Hsh&_). exists d. split; [done|exact (isq_shape _ _ _ _ _ Sh Hsh)].
      * exact Hsom.
Qed.

(** A step that changes none of what the invariant reads. *)
Lemma wframe_same s s' :
  same4 s s' -> (forall c d, g_nodes s !! c = Some d -> quiet_effect s c d -> trace_view c (g_trace s') = trace_view c (g_trace s)) ->
  (forall o y, g_owners s' !! o = Some y -> (exists y0, g_owners s !! o = Some y0 /\ o_host y0 = o_host y) \/
    ((o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d)) ->
  wframe s s'.
Proof.
  intros H Hv Ho. pose proof (same4_shapes _ _ H) as Hs. destruct H as (H1&H2&H3&H4).
  split; rewrite ?H1, ?H2, ?H3, ?H4; try done.
  - intros m d Hd _. exists d. split; [done|]. split_and!; done.
  - intros m d' Hd'. exists d'. split_and!; [done|done|]. intros _. split_and!; done.
  - intros c d Hc _. by left.
  - intros c Hc. by left.
  - intros m d' Hm _. by left.
Qed.

(** A write of a node's other fields, or of any field of a node outside the invariant. *)
Lemma wframe_set s n d d' :
  g_nodes s !! n = Some d -> shape_of d' = shape_of d -> (isq s n d -> wsame d d') ->
  wframe s (set_node n d' s).
Proof.
  intros Hd Hsh Hq. split.
  - exact (shapes_set s n d d' Hd Hsh).
  - intros m e He Hi. simpl. destruct (decide (m = n)) as [->|Hm].
    + rewrite lookup_insert_eq. rewrite Hd in He. injection He as <-. eexists. split; [done|]. auto.
    + rewrite lookup_insert_ne by done. exists e. split; [done|]. split_and!; done.
  - intros m e. simpl. destruct (decide (m = n)) as [->|Hm].
    + rewrite lookup_insert_eq. intros [=<-]. exists d. split_and!; [done|done|done].
    + rewrite lookup_insert_ne by done. intros He. exists e. split_and!; [done|done|]. intros _. split_and!; done.
  - intros c e He Hi. by left.
  - intros c e y He Hi. done.
  - intros c Hc. by left.
  - intros m e Hm _. by left.
  - done.
  - intros o y Hy. left. exists y. done.
Qed.

(** Marks that start outside the pure nodes. *)
Lemma wk_touch s s' :
  run_base s -> wk s -> ytouch (fun _ => False) s s' -> wk s'.
Proof.
  intros Hb Hk Ht. destruct Hk as [Hw [Hn [Hpd [Hran Hho]]]].
  set (sf := with_flushing true s). set (sf' := with_flushing true s').
  assert (H0 : same4 s sf) by done. assert (H1 : same4 s' sf') by done. assert (H1' : same4 sf' s') by (split_and!; done).
  assert (Hwf : wk sf).
  { split; [exact (whole_same _ _ H0 Hw)|]. split; [|exact (conj Hpd (conj Hran Hho))]. intros m dm Hm Hdm Hq. refine (Hn m dm Hm Hdm _). exact (qc_same _ _ _ _ (ltac:(split_and!; done) : same4 sf s) Hq). }
  assert (Hbf : run_base sf).
  { destruct Hb as [B1 B2 [E1 E2 E3 E4] B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 B16 B17 B18]. split; try done. }
  pose proof (stk_empty 0 sf Hbf Hwf ltac:(by right)) as St.
  pose proof (proj1 Ht) as RR.
  destruct Ht as [[A1 A2 A3 A4 A5 A6 A7 A8 A9 A10 A11 A12 A13 A14] Tch].
  assert (Htf : ytouch (fun _ => False) sf sf').
  { split; [split; try done|exact Tch]. }
  destruct (whole_touch _ _ _ _ (proj1 Hwf) St Htf) as [Hw' _].
  pose proof (rise_shapes _ _ (proj1 Htf)) as Hsh.
  split; [|split].
  - exact (whole_same _ _ H1' Hw').
  - intros m dm Hm Hdm Hq. destruct (rs_back _ _ (proj1 Htf) m dm Hdm) as [d0 Hd0].
    destruct (rise_back _ _ _ _ (proj1 Htf) Hdm) as (d1&Hd1&Hr).
    rewrite A3 in Hm. change (g_nodes s !! m = Some d1) in Hd1.
    refine (Hn m d1 Hm Hd1 _).
    assert (Hss : shapes s' = shapes s) by exact Hsh.
    pose proof Hr as (Hshd&_). exact (qc_shape_back _ _ _ _ _ Hss Hshd Hq).
  - split_and!.
    + intros c Hc. rewrite A2 in Hc. destruct (Hpd c Hc) as [d Hd]. destruct (A11 c d Hd) as (d'&Hd'&_). by eexists.
    + apply (ran_frame _ s); [|exact Hran]. intros c d' Hd' Hq Hl Hf. destruct (rise_back _ _ _ _ RR Hd') as (d&Hd&Hr).
      pose proof Hr as (Hs0&Vv&Di&_&_&_&_&Fl&_).
      pose proof (isq_shape _ _ _ _ _ (rise_shapes _ _ RR) Hs0 Hq) as Hq0.
      unfold shape_of in Hs0. injection Hs0 as _ _ E B.
      exists d. split_and!; [done|done|congruence| | |done|done|by rewrite !read_prefix_view, A1|intros y; by rewrite A2|by rewrite A13].
      * intros K. rewrite K in Fl. unfold freshness_le in Fl. destruct (n_freshness d') eqn:E9; simpl in Fl; try lia. done.
      * unfold window, read_view_of in *. injection Vv as _ S1 _. congruence.
    + apply (hosts_frame s); [| |intros o y Hy; left; exists y; rewrite A14 in Hy; done|exact Hho].
      * intros m d' Hd'. destruct (rise_back _ _ _ _ RR Hd') as (d&Hd&Hr). pose proof Hr as (Hs0&_). exists d. split; [done|exact (isq_shape _ _ _ _ _ (rise_shapes _ _ RR) Hs0)].
      * intros m [d Hd]. destruct (A11 m d Hd) as (d'&Hd'&_). by eexists.
Qed.

Lemma HC_empty s :
  run_base s -> wk s -> forall x y, ysrc (fun _ => False) s x -> y ∈ observer_view (observers_of (g_edges s) x) -> iny (fun _ => False) s y.
Proof.
  intros Hb Hk x y Hx Hin. destruct Hk as [Hw [Hn Hpd]].
  set (sf := with_flushing true s).
  assert (H0 : same4 s sf) by done.
  assert (Hwf : wk sf).
  { split; [exact (whole_same _ _ H0 Hw)|]. split; [|exact Hpd]. intros m dm Hm Hdm Hq. refine (Hn m dm Hm Hdm _). exact (qc_same _ _ _ _ (ltac:(split_and!; done) : same4 sf s) Hq). }
  assert (Hbf : run_base sf).
  { destruct Hb as [B1 B2 [E1 E2 E3 E4] B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 B16 B17 B18]. split; try done. }
  exact (stk_HC _ 0 sf Hbf (proj1 Hwf) (stk_empty 0 sf Hbf Hwf ltac:(by right)) x y Hx Hin).
Qed.

End WX.

(** ** What a call keeps of the older nodes *)

Definition ids_ok (s : state) : Prop := forall n : node_id, is_Some (g_nodes s !! n) -> n <= g_next_id s.

Record kept (s s' : state) : Prop := {
  kp_fw : forall n d, g_nodes s !! n = Some d -> exists d', g_nodes s' !! n = Some d' /\ shape_of d' = shape_of d /\
    (n_scope d' = None -> n_scope d = None);
  kp_bk : forall (n : node_id) d', g_nodes s' !! n = Some d' -> is_Some (g_nodes s !! n) \/ g_next_id s < n;
  kp_nx : g_next_id s <= g_next_id s'
}.

Lemma kept_refl' s : kept s s.
Proof. split; [|intros n d' Hd'; left; by eexists|done]. intros n d Hd. exists d. done. Qed.

Lemma kept_trans' s1 s2 s3 : kept s1 s2 -> kept s2 s3 -> kept s1 s3.
Proof.
  intros [A1 A2 A3] [B1 B2 B3]. split; [|intros n d' Hd'|lia].
  - intros n d Hd. destruct (A1 n d Hd) as (d2&Hd2&X1&X2). destruct (B1 n d2 Hd2) as (d3&Hd3&Y1&Y2).
    exists d3. split_and!; [done|congruence|auto].
  - destruct (B2 n d' Hd') as [[d2 Hd2]|H]; [|right; lia]. destruct (A2 n d2 Hd2) as [H|H]; [by left|right; lia].
Qed.

Definition ko {A} (m : M A) : Prop :=
  forall s s' a, ids_ok s -> m s = Some (s', a) -> ids_ok s' /\ kept s s'.

Lemma ko_return {A} (a : A) : ko (m_return a).
Proof. intros s s' b H [=<- _]. split; [done|apply kept_refl']. Qed.

Lemma ko_stuck {A} : ko (@stuck A).
Proof. intros s s' b _ H. done. Qed.

Lemma ko_bind {A B} (m : M A) (k : A -> M B) : ko m -> (forall a, ko (k a)) -> ko (m_bind m k).
Proof.
  intros Hm Hk s s' b Hi E. apply bind_inv in E as (s1 & a & E1 & E2).
  destruct (Hm _ _ _ Hi E1) as [Hi1 K1]. destruct (Hk a _ _ _ Hi1 E2) as [Hi2 K2]. split; [done|by eapply kept_trans'].
Qed.

Lemma ko_nodes (g : state -> state) :
  (forall s, g_nodes (g s) = g_nodes s) -> (forall s, g_next_id (g s) = g_next_id s) -> ko (m_modify g).
Proof.
  intros H1 H2 s s' a Hi [=<- _]. split.
  - intros n. rewrite H1, H2. apply Hi.
  - split; [|intros n d'; rewrite H1; intros Hd; left; by eexists|by rewrite H2].
    intros n d Hd. exists d. rewrite H1. done.
Qed.

Lemma ko_get : ko m_get.
Proof. intros s s' a Hi [=<- _]. split; [done|apply kept_refl']. Qed.

Lemma ko_get_node n : ko (get_node n).
Proof. intros s s' a Hi E. unfold get_node in E. destruct (g_nodes s !! n); [|done]. injection E as <- _. split; [done|apply kept_refl']. Qed.

Lemma ko_get_owner o : ko (get_owner o).
Proof. intros s s' a Hi E. unfold get_owner in E. destruct (g_owners s !! o); [|done]. injection E as <- _. split; [done|apply kept_refl']. Qed.

Lemma ko_set s n d d' :
  ids_ok s -> g_nodes s !! n = Some d -> shape_of d' = shape_of d -> (n_scope d' = None -> n_scope d = None) ->
  ids_ok (set_node n d' s) /\ kept s (set_node n d' s).
Proof.
  intros Hi Hd Hs Hc. split.
  - intros m Hm. apply Hi. simpl in Hm. destruct (decide (m = n)) as [->|Hne]; [by eexists|]. by rewrite lookup_insert_ne in Hm.
  - split; [|intros m e; simpl|done].
    + intros m e He. simpl. destruct (decide (m = n)) as [->|Hne].
      * rewrite lookup_insert_eq. rewrite Hd in He. injection He as <-. eexists. done.
      * rewrite lookup_insert_ne by done. exists e. done.
    + destruct (decide (m = n)) as [->|Hne]; [left; by eexists|]. rewrite lookup_insert_ne by done. intros He. left. by eexists.
Qed.

Lemma ko_modify n u : (forall d, shape_of (u d) = shape_of d) -> (forall d, n_scope (u d) = None -> n_scope d = None) -> ko (modify_node n u).
Proof.
  intros H1 H2 s s' a Hi E. unfold modify_node in E. destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done]. injection E as <- _.
  apply (ko_set s n d); auto.
Qed.

Lemma ko_put_owner o x : ko (put_owner o x).
Proof. apply ko_nodes; done. Qed.

Lemma ko_edges_opt u : ko (edges_opt u).
Proof.
  intros s s' a Hi E. unfold edges_opt in E. destruct (u (g_edges s)) as [w|]; [|done]. injection E as <- _. split; [done|].
  split; [|intros n d' Hd'; left; by eexists|done]. intros n d Hd. exists d. done.
Qed.

Lemma ko_rise (g : state -> option state) :
  (forall s s', g s = Some s' -> rise s s') -> (forall s s', g s = Some s' -> g_next_id s' = g_next_id s) -> ko (m_step g).
Proof.
  intros H H2 s s' a Hi E. unfold m_step in E. destruct (g s) as [s1|] eqn:Eg; [|done]. injection E as <- _.
  pose proof (H _ _ Eg) as R. split.
  - intros n Hn. destruct Hn as [d' Hd']. rewrite (H2 _ _ Eg). apply Hi. exact (rs_back _ _ R n d' Hd').
  - split; [|intros n d' Hd'; left; exact (rs_back _ _ R n d' Hd')|rewrite (H2 _ _ Eg); lia].
    intros n d Hd. destruct (rs_fwd _ _ R n d Hd) as (d'&Hd'&Hr). exists d'. pose proof Hr as (X&_&_&_&_&Y&_). split_and!; [done|done|congruence].
Qed.

Lemma prim_next s s' : mark_prim s s' -> g_next_id s' = g_next_id s.
Proof. by intros []. Qed.

Lemma marks_next s s' : rtc mark_prim s s' -> g_next_id s' = g_next_id s.
Proof. induction 1 as [s|s1 s2 s3 Hp _ IH]; [done|]. rewrite IH. by apply prim_next. Qed.

Lemma ko_mark (g : state -> option state) :
  (forall s s', g s = Some s' -> rtc mark_prim s s') -> ko (m_step g).
Proof.
  intros H. apply ko_rise; [intros s s' E; apply rtc_rise; eauto|intros s s' E; apply marks_next; eauto].
Qed.

Lemma ko_new_node {B} (D : node_id -> node) (k : node_id -> M B) :
  (forall n, ko (k n)) -> ko (m_bind next_id (fun n => m_bind (add_node n (D n)) (fun _ => k n))).
Proof.
  intros Hk s s' b Hi E. unfold m_bind, next_id, add_node, m_modify in E. simpl in E.
  set (n := S (g_next_id s)) in E.
  set (s1 := ghost_publish n (set_node n (D n) (with_next_id n s))) in E.
  assert (Habs : g_nodes s !! n = None).
  { destruct (g_nodes s !! n) eqn:Hn; [|done]. pose proof (Hi n (mk_is_Some _ _ Hn)). subst n. lia. }
  assert (Hn1 : g_nodes s1 = <[n := D n]> (g_nodes s)).
  { subst s1. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hx1 : g_next_id s1 = n).
  { subst s1. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hi1 : ids_ok s1).
  { intros m Hm. rewrite Hx1. rewrite Hn1 in Hm. destruct (decide (m = n)) as [->|Hne]; [lia|].
    rewrite lookup_insert_ne in Hm by done. pose proof (Hi m Hm). subst n. lia. }
  assert (K1 : kept s s1).
  { split; [|intros m e; rewrite Hn1|rewrite Hx1; subst n; lia].
    - intros m d Hd. exists d. rewrite Hn1. rewrite lookup_insert_ne; [done|]. intros Heq. pose proof (Hi m (mk_is_Some _ _ Hd)) as Hle. subst n. lia.
    - destruct (decide (m = n)) as [->|Hne]; [intros _; right; subst n; lia|]. rewrite lookup_insert_ne by done. intros He. left. by eexists. }
  destruct (Hk n _ _ _ Hi1 E) as [Hi2 K2]. split; [done|by eapply kept_trans'].
Qed.

Lemma ko_current_owner : ko current_owner.
Proof.
  intros s s' a Hi E. unfold current_owner in E.
  destruct (g_current_owner s) as [o|]. { injection E as <- _. split; [done|apply kept_refl']. }
  destruct (g_current s) as [h|]; [|done].
  destruct (g_nodes s !! h) as [d|] eqn:Hd; [|done].
  destruct (n_scope d) as [o|] eqn:Es.
  { injection E as <- _. split; [done|]. split; [|intros n d' Hd'; left; by eexists|done]. intros n e He. exists e. done. }
  destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end).
  - injection E as <- _. apply (ko_set s h d); done.
  - injection E as <- _.
    set (o := stdpp.base.fresh (dom (g_owners s))) in *.
    destruct (ko_set (with_current_owner (Some o) (with_owners (<[o := new_owner h (Some root_owner_id) (n_disposed d)]> (g_owners s)) s)) h d (node_with_scope (Some o) d) Hi Hd eq_refl ltac:(simpl; done)) as [A B].
    split; [exact A|]. destruct B as [B1 B2 B3]. split; done.
Qed.

Lemma ko_dep {A} (m : state -> M A) : (forall s0, ko (m s0)) -> ko (fun s0 => m s0 s0).
Proof. intros H s s' a Hi E. exact (H s s s' a Hi E). Qed.

Lemma ko_hosted {A} n (body : M A) : ko body -> ko (hosted n body).
Proof.
  intros Hb. unfold hosted. apply (ko_dep (fun s0 => (let* _ := m_modify (fun s => with_current_owner None (with_current (Some n) s)) in
      let* r := body in
      let* _ := m_modify (fun s => with_current_owner (g_current_owner s0) (with_current (g_current s0) s)) in
      m_return r))).
  intros s0. apply ko_bind; [apply ko_nodes; done|intros _]. apply ko_bind; [done|intros r]. apply ko_bind; [apply ko_nodes; done|intros _]. apply ko_return.
Qed.

Lemma ko_untracked {A} (body : M A) : ko body -> ko (untracked body).
Proof.
  intros Hb. unfold untracked. apply (ko_dep (fun s0 => (let* _ := m_modify (fun s => with_untracked (match g_current s0 with Some _ => true | None => g_untracked s0 end) (with_current None s)) in
      let* r := body in
      let* _ := m_modify (fun s => with_untracked (g_untracked s0) (with_current (g_current s0) s)) in
      m_return r))).
  intros s0. apply ko_bind; [apply ko_nodes; done|intros _]. apply ko_bind; [done|intros r]. apply ko_bind; [apply ko_nodes; done|intros _]. apply ko_return.
Qed.

Lemma ko_detached o (body : M unit) : ko body -> ko (detached o body).
Proof.
  intros Hb. unfold detached. apply (ko_dep (fun s0 => (let* _ := m_modify (fun s => with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s))) in
      let* _ := body in
      m_modify (fun s => with_batch_depth (pred (g_batch_depth s)) (with_current_owner (g_current_owner s0) (with_current (g_current s0) s)))))).
  intros s0. apply ko_bind; [apply ko_nodes; done|intros _]. apply ko_bind; [done|intros _]. apply ko_nodes; done.
Qed.

Lemma ko_on_value e x (k : val -> M outcome) : (forall v, ko (k v)) -> ko (on_value e x k).
Proof.
  intros H s s' a Hi E. unfold on_value in E. destruct (eval_expr e _ x) as [[v|y]|]; [exact (H v _ _ _ Hi E)| |done].
  injection E as <- _. split; [done|apply kept_refl'].
Qed.

Lemma ko_on_node e x (k : node_id -> M outcome) : (forall v, ko (k v)) -> ko (on_node e x k).
Proof.
  intros H. apply ko_on_value. intros z. destruct (decide _); [apply H|apply ko_stuck].
Qed.

Lemma ko_track n : ko (track n).
Proof.
  intros s s' a Hi E. unfold track in E. destruct (g_current s) as [c|].
  - destruct (sources_add c n (g_edges s)) as [w|]; [|done]. injection E as <- _.
    unfold ghost_read. destruct (g_nodes (with_edges w s) !! n); (split; [done|split; [|intros m d' Hd'; left; by eexists|done]]); intros m d Hd; exists d; done.
  - injection E as <- _. split; [done|apply kept_refl'].
Qed.

Lemma ko_mark_reader f : ko (mark_reader f).
Proof.
  intros s s' a Hi E. unfold mark_reader in E. destruct (g_current s) as [c|]; [|injection E as <- _; split; [done|apply kept_refl']].
  destruct (f_reader_mark (g_flags s)); [|injection E as <- _; split; [done|apply kept_refl']].
  destruct (mark_check f c s) as [s1|] eqn:Em; [|done]. injection E as <- _.
  refine (ko_mark (mark_check f c) _ s s1 tt Hi _); [intros ? ?; apply mark_check_rtc|unfold m_step; by rewrite Em].
Qed.

Lemma ko_announce n : ko (announce n).
Proof.
  intros s s' a Hi E. unfold announce in E. destruct (f_unannounced (g_flags s)); [|injection E as <- _; split; [done|apply kept_refl']].
  exact (ko_modify n (node_with_unannounced true) (fun _ => eq_refl) (fun _ H => H) s s' a Hi E).
Qed.

Lemma ko_emit e : ko (emit e).
Proof. apply ko_nodes; done. Qed.

Lemma ko_note n o : ko (note_host_read n o).
Proof.
  intros s s' a Hi E. unfold note_host_read in E. destruct (g_current s); [injection E as <- _; split; [done|apply kept_refl']|].
  destruct (g_current_owner s); [|injection E as <- _; split; [done|apply kept_refl']].
  destruct (decide _); [|injection E as <- _; split; [done|apply kept_refl']].
  exact (ko_emit (EvHostRead n o) s s' a Hi E).
Qed.

Lemma ko_log_label : ko log_label.
Proof. intros s s' a Hi E. unfold log_label in E. destruct (g_current s), (g_current_owner s); injection E as <- _; (split; [done|apply kept_refl']). Qed.


Lemma ko_unlink l : ko (unlink l).
Proof.
  unfold unlink. apply ko_bind; [apply ko_get_owner|intros x]. destruct (decide _); [apply ko_put_owner|apply ko_return].
Qed.

Lemma ko_record o v : ko (record_error o v).
Proof.
  unfold record_error. apply ko_bind; [apply ko_get_owner|intros x]. destruct (o_sink x); [|apply ko_put_owner].
  apply ko_bind; [apply ko_get_owner|intros y]. apply ko_put_owner.
Qed.

Record K_ok (f : nat) : Prop := {
  Kexec : forall e c, ko (exec f e c);
  Kread : forall n b, ko (read f n b);
  Knode : forall n, ko (read_node f n);
  Kupd : forall n, ko (update_if_necessary f n);
  Krop : forall n, ko (refresh_or_pull f n);
  Kpull : forall n, ko (pull f n);
  Kref : forall n, ko (refresh f n);
  Kwalk : forall n i, ko (resolve_walk f n i);
  Krec : forall n, ko (recompute f n);
  Krun : forall n, ko (run_memo f n);
  Kexe : forall n, ko (execute_effect f n);
  Kreff : forall n, ko (run_effect f n);
  Kbody : forall n, ko (run_effect_body f n);
  Kdis : forall o, ko (discharge f o);
  Kret : forall o, ko (retire f o);
  Kdsc : forall o, ko (dispose_scope f o);
  Ktear : forall o ls cs, ko (teardown f o ls cs);
  Kcl : forall o b e, ko (run_cleanup f o b e);
  Krel : forall n, ko (release f n);
  Kdisp : forall n, ko (dispose_node f n);
  Katt : forall n o, ko (attach f n o);
  Kwrite : forall n v, ko (write_signal f n v);
  Kreq : ko (request_flush f);
  Kset : ko (settle_flush f);
  Kflush : ko (run_flush f);
  Kdrain : ko (drain f)
}.

Lemma K_ok_0 : K_ok 0.
Proof. split; intros; apply ko_stuck. Qed.

Ltac krih IH :=
  first
    [ apply (Kexec _ IH) | apply (Kread _ IH) | apply (Knode _ IH) | apply (Kupd _ IH) | apply (Krop _ IH) | apply (Kpull _ IH)
    | apply (Kref _ IH) | apply (Kwalk _ IH) | apply (Krec _ IH) | apply (Krun _ IH) | apply (Kexe _ IH) | apply (Kreff _ IH)
    | apply (Kbody _ IH) | apply (Kdis _ IH) | apply (Kret _ IH) | apply (Kdsc _ IH) | apply (Ktear _ IH) | apply (Kcl _ IH)
    | apply (Krel _ IH) | apply (Kdisp _ IH) | apply (Katt _ IH) | apply (Kwrite _ IH) | apply (Kreq _ IH) | apply (Kset _ IH)
    | apply (Kflush _ IH) | apply (Kdrain _ IH) ].

Ltac kgo1 IH :=
  first
    [ apply ko_return | apply ko_stuck | apply ko_get | apply ko_get_node | apply ko_get_owner | apply ko_put_owner
    | apply ko_edges_opt | apply ko_current_owner | apply ko_track | apply ko_mark_reader | apply ko_announce | apply ko_note
    | apply ko_log_label | apply ko_emit | apply ko_unlink | apply ko_record
    | apply ko_new_node; intros ?
    | apply ko_on_node; intros ? | apply ko_on_value; intros ?
    | apply ko_hosted | apply ko_untracked | apply ko_detached
    | (apply ko_mark; intros ? ? Hz; first [exact (mark_dirty_rtc _ _ _ _ Hz) | refine (any_mark_rtc _ _ None _ _ _); right; right; right; left; exact Hz | refine (any_mark_rtc _ _ _ _ _ _); right; right; right; right; exact Hz])
    | (apply ko_modify; intros ?; simpl; first [reflexivity|intros H; exact H])
    | (apply ko_nodes; intros ?; first [reflexivity|unfold ghost_run_end; destruct (g_nodes _ !! _); reflexivity|unfold ghost_publish; destruct (g_nodes _ !! _); reflexivity])
    | krih IH
    | apply ko_bind; [|intros ?]
    | match goal with |- ko (match ?x with _ => _ end) => destruct x end
    | match goal with |- ko (if ?x then _ else _) => destruct x end ].

Lemma K_ok_S f : K_ok f -> K_ok (S f).
Proof.
  intros IH. split; intros.
  - destruct c; cbn [exec]; repeat kgo1 IH.
  - cbn [read]; repeat kgo1 IH.
  - cbn [read_node]; repeat kgo1 IH.
  - cbn [update_if_necessary]; repeat kgo1 IH.
  - cbn [refresh_or_pull]; repeat kgo1 IH.
  - cbn [pull]; repeat kgo1 IH.
  - cbn [refresh]; repeat kgo1 IH.
  - cbn [resolve_walk]; repeat kgo1 IH.
  - cbn [recompute]; repeat kgo1 IH.
  - cbn [run_memo]; repeat kgo1 IH.
  - cbn [execute_effect]; repeat kgo1 IH.
  - cbn [run_effect]; repeat kgo1 IH.
  - cbn [run_effect_body]; repeat kgo1 IH.
  - cbn [discharge]; repeat kgo1 IH.
  - cbn [retire]; repeat kgo1 IH.
  - cbn [dispose_scope]; repeat kgo1 IH.
  - cbn [teardown]; repeat kgo1 IH.
  - cbn [run_cleanup]; repeat kgo1 IH.
  - cbn [release]; repeat kgo1 IH.
  - cbn [dispose_node]; repeat kgo1 IH.
  - cbn [attach]; repeat kgo1 IH.
  - cbn [write_signal]; repeat kgo1 IH.
  - cbn [request_flush]; repeat kgo1 IH.
  - cbn [settle_flush]; repeat kgo1 IH.
  - cbn [run_flush]; repeat kgo1 IH.
  - cbn [drain]; repeat kgo1 IH.
Qed.

Lemma K_ok_all f : K_ok f.
Proof. induction f; [exact K_ok_0|by apply K_ok_S]. Qed.

(** ** The fragment, read back from a program's end *)

Section WX1.
Context {Xe : node_id -> Prop}.
Local Notation wk := (wkx Xe).

Definition good (s : state) : Prop := well_behaved_program s /\ effects_only_read s.

Lemma good_back s s' : ids_ok s -> kept s s' -> good s' -> good s.
Proof.
  intros Hi K [Ha He].
  assert (Hsub : forall x v, shapes s !! x = Some v -> shapes s' !! x = Some v).
  { intros x v Hx. unfold shapes in *. rewrite lookup_fmap in Hx |- *. destruct (g_nodes s !! x) as [d|] eqn:E; [|done].
    destruct (kp_fw _ _ K x d E) as (d'&Hd'&H&_). rewrite Hd'. simpl in *. by rewrite H. }
  assert (Hp : forall t : node_id, is_Some (g_nodes s !! t) -> pure_in (shapes s') t -> pure_in (shapes s) t).
  { intros t [x Hx] H. apply (pure_in_sub _ _ _ Hsub); [|done]. rewrite (shapes_lookup s t x Hx). by eexists. }
  split.
  - intros n d Hd Hk. destruct (kp_fw _ _ K n d Hd) as (d'&Hd'&Hs&_).
    apply Hp; [by eexists|]. refine (Ha n d' Hd' _).
    unfold shape_of in Hs. injection Hs as X _ _ _. congruence.
  - intros c d Hd Hk. destruct (kp_fw _ _ K c d Hd) as (d'&Hd'&Hs&_).
    assert (Hk' : n_kind d' = KEffect) by (unfold shape_of in Hs; injection Hs as X _ _ _; congruence).
    destruct (He c d' Hd' Hk') as [_ H2]. split; [done|].
    unfold shape_of in Hs. injection Hs as _ _ E B. rewrite E, B in H2.
    revert H2. apply body_pure_mono. intros t Ht Hr. apply Hp; [exact (readable_node _ _ Hr)|]. exact (Ht (readable_sub _ _ _ Hsub Hr)).
Qed.

Lemma core_ids s : run_core s -> ids_ok s.
Proof. intros [Hi _] n Hn. exact (ci_next _ _ _ Hi n Hn). Qed.

Definition same6 (s s' : state) : Prop :=
  g_nodes s' = g_nodes s /\ g_edges s' = g_edges s /\ g_ghost s' = g_ghost s /\ g_running s' = g_running s /\
  g_trace s' = g_trace s /\ g_owners s' = g_owners s.

Definition wokx (X : node_id -> Prop) {A} (P : state -> Prop) (m : M A) : Prop :=
  ok2 P m /\ ko m /\ forall s s' a, run_core s -> wkx X s -> P s -> m s = Some (s', a) -> good s' -> wkx X s'.
Local Notation wok := (wokx Xe).

Lemma wok_bind {A B} P `{!Stable2 P} (m : M A) (k : A -> M B) :
  wok P m -> (forall a, wok P (k a)) -> wok P (m_bind m k).
Proof.
  intros [Om [Km Wm]] Hk. split; [|split].
  - apply ok2_bind; [done|done|intros a; apply Hk].
  - apply ko_bind; [done|intros a; apply Hk].
  - intros s s' b Hc Hw HP E. apply bind_inv in E as (s1 & a & E1 & E2). intros Hg.
    destruct (Om _ _ _ Hc HP E1) as [Hc1 K1].
    pose proof (stable2 _ _ K1 HP) as HP1.
    destruct (Hk a) as [_ [Kk Wk]].
    destruct (Kk _ _ _ (core_ids _ Hc1) E2) as [_ K2].
    exact (Wk _ _ _ Hc1 (Wm _ _ _ Hc Hw HP E1 (good_back _ _ (core_ids _ Hc1) K2 Hg)) HP1 E2 Hg).
Qed.

Lemma wok_any {A} P (m : M A) : wok (fun _ => True) m -> wok P m.
Proof.
  intros [O [K W]]. split; [by apply ok2_any|split; [done|]]. intros s s' a Hc Hw _ E. exact (W _ _ _ Hc Hw I E).
Qed.

Lemma wok_sameq {A} P (m : M A) :
  ok2 P m -> ko m ->
  (forall s s' a, P s -> m s = Some (s', a) -> same4 s s' /\ (forall c, trace_view c (g_trace s') = trace_view c (g_trace s)) /\
    forall o y, g_owners s' !! o = Some y -> exists y0, g_owners s !! o = Some y0 /\ o_host y0 = o_host y) -> wok P m.
Proof.
  intros O K H. split; [done|split; [done|]]. intros s s' a Hc Hw HP E _. destruct (H _ _ _ HP E) as (H4&Hv&Ho).
  pose proof (proj1 (proj2 Hc)) as Hb. refine (wframe_apply _ _ Hb (wframe_same _ _ H4 (fun c _ _ _ => Hv c) _) Hw).
  intros o y Hy. left. exact (Ho o y Hy).
Qed.

Lemma wok_same {A} P (m : M A) :
  ok2 P m -> ko m -> (forall s s' a, m s = Some (s', a) -> same6 s s') -> wok P m.
Proof.
  intros O K H. apply wok_sameq; [done|done|]. intros s s' a _ E. destruct (H _ _ _ E) as (H1&H2&H3&H4&H5&H6).
  assert (H0 : same4 s s') by (repeat split; assumption).
  split; [exact H0|split; [intros c; by rewrite H5|intros o y Hy; rewrite H6 in Hy; exists y; done]].
Qed.

Lemma wok_return {A} P (a : A) : wok P (m_return a).
Proof. apply wok_same; [apply ok2_return|apply ko_return|]. intros s s' b [=<- _]. split_and!; done. Qed.

Lemma wok_stuck {A} P : wok P (@stuck A).
Proof. split; [apply ok2_stuck|split; [apply ko_stuck|]]. intros s s' a _ _ _ E. done. Qed.

Lemma wok_get P : wok P m_get.
Proof. apply wok_same; [apply ok2_get|apply ko_get|]. intros s s' b [=<- _]. split_and!; done. Qed.

Lemma wok_get_node P n : wok P (get_node n).
Proof.
  apply wok_same; [apply ok2_get_node|apply ko_get_node|]. intros s s' b E. unfold get_node in E.
  destruct (g_nodes s !! n); [|done]. injection E as <- _. split_and!; done.
Qed.

Lemma wok_get_node_at {B} P n (k : node -> M B) :
  (forall d, wok (fun s => P s /\ g_nodes s !! n = Some d) (k d)) -> wok P (m_bind (get_node n) k).
Proof.
  intros Hk. split; [|split].
  - apply ok2_get_node_at. intros d. apply Hk.
  - apply ko_bind; [apply ko_get_node|intros d; apply Hk].
  - intros s s' a Hc Hw HP E. unfold m_bind, get_node in E. destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done].
    destruct (Hk d) as [_ [_ W]]. exact (W _ _ _ Hc Hw (conj HP Hd) E).
Qed.

Lemma trace_view_free c l e : view_ev c e = false -> trace_view c (l ++ [e]) = trace_view c l.
Proof.
  intros H. rewrite trace_view_app. unfold trace_view at 2. rewrite filter_cons_False, filter_nil, app_nil_r; [done|]. rewrite H. done.
Qed.

Lemma wok_emit P e : (forall c, view_ev c e = false) -> wok P (emit e).
Proof.
  intros He. apply wok_sameq; [apply ok2_emit|apply ko_emit|]. intros s s' b _ [=<- _]. split_and!; try done.
  - intros c. apply trace_view_free. apply He.
  - intros o y Hy. exists y. done.
Qed.

Lemma wok_note P n o : wok P (note_host_read n o).
Proof.
  apply wok_sameq; [apply ok2_note|apply ko_note|]. intros s s' b _ E. unfold note_host_read in E.
  destruct (g_current s); [injection E as <- _; split_and!; try done; intros o9 y Hy; exists y; done|].
  destruct (g_current_owner s); [|injection E as <- _; split_and!; try done; intros o9 y Hy; exists y; done].
  destruct (decide _); [|injection E as <- _; split_and!; try done; intros o9 y Hy; exists y; done]. unfold emit, m_modify in E. injection E as <- _.
  split_and!; try done.
  - intros c. by apply trace_view_free.
  - intros o9 y Hy. exists y. done.
Qed.

Lemma stable_emit P `{!Stable2 P} s s1 e : run_core s -> emit e s = Some (s1, tt) -> P s -> P s1.
Proof.
  intros Hc E HP. destruct (ok2_emit (fun _ => True) e s s1 tt Hc I E) as [_ K]. exact (stable2 _ _ K HP).
Qed.

Lemma wok_log {B} P `{!Stable2 P} z (k : M B) :
  wok P k -> wok P (m_bind log_label (fun l => m_bind (emit (EvLog l z)) (fun _ => k))).
Proof.
  intros [O [K W]]. split; [|split].
  - intros s s' a Hc HP E. unfold m_bind at 1 in E.
    destruct (log_label s) as [[s0 l]|] eqn:El; [|done].
    assert (Hs0 : s0 = s) by (unfold log_label in El; destruct (g_current s), (g_current_owner s); injection El as <- _; done). subst s0.
    apply bind_inv in E as (s1 & [] & E1 & E2).
    destruct (ok2_emit (fun _ => True) (EvLog l z) s s1 tt Hc I E1) as [Hc1 K1].
    destruct (O _ _ _ Hc1 (stable_emit P s s1 (EvLog l z) Hc E1 HP) E2) as [Hc2 K2]. split; [done|]. by eapply kept_trans.
  - apply ko_bind; [apply ko_log_label|intros l]. apply ko_bind; [apply ko_emit|done].
  - intros s s' a Hc Hw HP E Hg. unfold m_bind at 1 in E.
    destruct (log_label s) as [[s0 l]|] eqn:El; [|done].
    assert (Hs0 : s0 = s) by (unfold log_label in El; destruct (g_current s), (g_current_owner s); injection El as <- _; done). subst s0.
    apply bind_inv in E as (s1 & [] & E1 & E2).
    destruct (ok2_emit (fun _ => True) (EvLog l z) s s1 tt Hc I E1) as [Hc1 K1].
    assert (Hs1 : s1 = with_trace (g_trace s ++ [EvLog l z]) s) by (unfold emit, m_modify in E1; by injection E1 as <-).
    assert (Hne : forall c d, g_nodes s !! c = Some d -> quiet_effect s c d -> l <> c).
    { intros c d Hd Hq Hlc. subst c. pose proof Hw as (_&Hnp&_&_&H0&Hh). unfold log_label in El. destruct (g_current s) as [m|] eqn:Ecu.
      - injection El as <-. refine (Hnp m d _ Hd (or_intror Hq)). exact (b_current _ (proj1 (proj2 Hc)) m Ecu).
      - destruct (g_current_owner s) as [o|]; injection El as <-.
        + destruct (g_owners s !! o) as [y|] eqn:Ey; simpl in Hd, Hq.
          * exact (proj2 (Hh o y Ey) d Hd Hq).
          * congruence.
        + congruence. }
    assert (Hw1 : wk s1).
    { rewrite Hs1. refine (wframe_apply _ _ (proj1 (proj2 Hc)) (wframe_same _ _ _ _ _) Hw); [split_and!; done| |].
      - intros c d Hd Hq. simpl. apply trace_view_other. exact (Hne c d Hd Hq).
      - intros o y Hy. left. exists y. done. }
    exact (W _ _ _ Hc1 Hw1 (stable_emit P s s1 (EvLog l z) Hc E1 HP) E2 Hg).
Qed.

Lemma wok_log_label P : wok P log_label.
Proof.
  apply wok_same; [apply ok2_log_label|apply ko_log_label|]. intros s s' b E. unfold log_label in E.
  destruct (g_current s), (g_current_owner s); injection E as <- _; split_and!; done.
Qed.

Lemma wok_on_value P e x (k : val -> M outcome) : (forall v, wok P (k v)) -> wok P (on_value e x k).
Proof.
  intros H. split; [|split].
  - apply ok2_on_value. intros v. apply H.
  - apply ko_on_value. intros v. apply H.
  - intros s s' a Hc Hw HP E Hg. unfold on_value in E. destruct (eval_expr e _ x) as [[v|y]|] eqn:Ev; [|injection E as <- _; done|done].
    destruct (H v) as [_ [_ W]]. exact (W _ _ _ Hc Hw HP E Hg).
Qed.

Lemma wok_on_node P e x (k : node_id -> M outcome) : (forall v, wok P (k v)) -> wok P (on_node e x k).
Proof.
  intros H. apply wok_on_value. intros z. destruct (decide _); [apply H|apply wok_stuck].
Qed.

Lemma wok_get_owner_at {B} P o (k : owner_state -> M B) :
  (forall x, ko (k x)) -> (forall x, lists_ok x -> wok (fun s => P s /\ g_owners s !! o = Some x) (k x)) -> wok P (m_bind (get_owner o) k).
Proof.
  intros Hko Hk. split; [|split].
  - intros s s' a Hc HP E. unfold m_bind, get_owner in E. destruct (g_owners s !! o) as [x|] eqn:Ho; [|done].
    destruct (Hk x (b_lists _ (proj1 (proj2 Hc)) o x Ho)) as [O _]. exact (O _ _ _ Hc (conj HP Ho) E).
  - apply ko_bind; [apply ko_get_owner|done].
  - intros s s' a Hc Hw HP E. unfold m_bind, get_owner in E. destruct (g_owners s !! o) as [x|] eqn:Ho; [|done].
    destruct (Hk x (b_lists _ (proj1 (proj2 Hc)) o x Ho)) as [_ [_ W]]. exact (W _ _ _ Hc Hw (conj HP Ho) E).
Qed.

Lemma wok_bind_left {A B} P `{!Stable2 P} (Q : state -> Prop) (m : M A) (k : A -> M B) :
  wok (fun s => P s /\ Q s) m -> (forall a, wok P (k a)) -> wok (fun s => P s /\ Q s) (m_bind m k).
Proof.
  intros [Om [Km Wm]] Hk. split; [|split].
  - intros s s' b Hc HP E. apply bind_inv in E as (s1 & a & E1 & E2).
    destruct (Om _ _ _ Hc HP E1) as [Hc1 K1]. destruct (Hk a) as [O2 _].
    destruct (O2 _ _ _ Hc1 (stable2 _ _ K1 (proj1 HP)) E2) as [Hc2 K2]. split; [done|]. by eapply kept_trans.
  - apply ko_bind; [done|intros a; apply Hk].
  - intros s s' b Hc Hw HP E. apply bind_inv in E as (s1 & a & E1 & E2). intros Hg.
    destruct (Om _ _ _ Hc HP E1) as [Hc1 K1].
    destruct (Hk a) as [_ [Kk Wk]].
    destruct (Kk _ _ _ (core_ids _ Hc1) E2) as [_ K2].
    exact (Wk _ _ _ Hc1 (Wm _ _ _ Hc Hw HP E1 (good_back _ _ (core_ids _ Hc1) K2 Hg)) (stable2 _ _ K1 (proj1 HP)) E2 Hg).
Qed.

Lemma wok_bind_keep {A B} P `{!Stable2 P} (Q : state -> Prop) (m : M A) (k : A -> M B) :
  (forall s s' a, m s = Some (s', a) -> Q s -> Q s') ->
  wok (fun s => P s /\ Q s) m -> (forall a, wok (fun s => P s /\ Q s) (k a)) -> wok (fun s => P s /\ Q s) (m_bind m k).
Proof.
  intros HQ [Om [Km Wm]] Hk. split; [|split].
  - intros s s' b Hc HP E. apply bind_inv in E as (s1 & a & E1 & E2).
    destruct (Om _ _ _ Hc HP E1) as [Hc1 K1]. destruct (Hk a) as [O2 _].
    destruct (O2 _ _ _ Hc1 (conj (stable2 _ _ K1 (proj1 HP)) (HQ _ _ _ E1 (proj2 HP))) E2) as [Hc2 K2]. split; [done|]. by eapply kept_trans.
  - apply ko_bind; [done|intros a; apply Hk].
  - intros s s' b Hc Hw HP E. apply bind_inv in E as (s1 & a & E1 & E2). intros Hg.
    destruct (Om _ _ _ Hc HP E1) as [Hc1 K1].
    destruct (Hk a) as [_ [Kk Wk]].
    destruct (Kk _ _ _ (core_ids _ Hc1) E2) as [_ K2].
    exact (Wk _ _ _ Hc1 (Wm _ _ _ Hc Hw HP E1 (good_back _ _ (core_ids _ Hc1) K2 Hg)) (conj (stable2 _ _ K1 (proj1 HP)) (HQ _ _ _ E1 (proj2 HP))) E2 Hg).
Qed.

Ltac ok2_mod' := apply ok2_modify_same; intros ?; [unfold irrelevant; simpl; split_and!; reflexivity|split; reflexivity].

(** A step on the state's other fields. *)
Lemma wok_modify P (g : state -> state) :
  (forall s, irrelevant s (g s)) -> (forall s, fields_same s (g s)) -> (forall s, same6 s (g s)) -> (forall s, g_next_id (g s) = g_next_id s) ->
  wok P (m_modify g).
Proof.
  intros H1 H2 H3 H4. apply wok_same.
  - by apply ok2_modify_same.
  - apply ko_nodes; [intros s; by destruct (H3 s)|done].
  - intros s s' a [=<- _]. apply H3.
Qed.

Ltac wmod := apply wok_modify; intros ?; [unfold irrelevant; simpl; split_and!; reflexivity|split; reflexivity|split_and!; reflexivity|reflexivity].

Lemma wok_put_owner P o x0 x : lists_ok x -> o_host x = o_host x0 -> wok (fun s => P s /\ g_owners s !! o = Some x0) (put_owner o x).
Proof.
  intros Hx Hh. apply wok_sameq.
  - intros s s' a Hc [_ _] E. exact (ok2_put_owner (fun _ => True) o x Hx s s' a Hc I E).
  - apply ko_put_owner.
  - intros s s' a [_ Ho] E. unfold put_owner, m_modify in E. injection E as <- _.
    split; [repeat split; reflexivity|split; [intros c; reflexivity|]].
    intros o0 y Hy. simpl in Hy. destruct (decide (o0 = o)) as [->|Hne].
    + rewrite lookup_insert_eq in Hy. injection Hy as <-. exists x0. split; [done|]. done.
    + rewrite lookup_insert_ne in Hy by done. exists y. done.
Qed.

(** A write of a node's owner link. *)
Lemma wok_link P n u :
  (forall d, node_step u d) -> (forall d, neutral u d) -> (forall d, wsame d (u d)) ->
  wok P (modify_node n u).
Proof.
  intros H1 H2 H3. split; [by apply ok2_modify_neutral|split].
  - apply ko_modify; intros d; [exact (proj1 (H3 d))|]. destruct (H3 d) as (_&_&_&X&_). congruence.
  - intros s s' a Hc Hw _ E _. unfold modify_node in E. destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done]. injection E as <- _.
    refine (wframe_apply _ _ (proj1 (proj2 Hc)) (wframe_set s n d (u d) Hd (proj1 (H3 d)) (fun _ => H3 d)) Hw).
Qed.

(** ** New nodes *)

Lemma slot_of_new s (n : node_id) :
  run_core s -> g_nodes s !! n = None -> (slot_view (g_edges s) n).1 = [].
Proof.
  intros [Hi Hx] Hn. destruct (slot_view (g_edges s) n) as [l k] eqn:E. simpl.
  destruct l as [|y l]; [done|]. exfalso.
  assert (Hy : y ∈ (slot_view (g_edges s) n).1) by (rewrite E; left).
  destruct (ci_slots _ _ _ Hi n y Hy) as [[d Hd] _]. congruence.
Qed.

Lemma whole_new s (n : node_id) D (X' : node_id -> Prop) :
  run_core s -> wk s -> n = S (g_next_id s) ->
  (n_kind D = KSignal -> n_status D = StNone /\ n_failure D = None) ->
  (n_kind D = KMemo -> n_scope D = None /\ n_violated D = false /\ n_freshness D = Dirty /\ n_unannounced D = false) ->
  (n_kind D = KEffect -> n_scope D = None /\ n_unannounced D = false) ->
  (n_kind D = KEffect -> X' n /\ window D /\ forall c, Xe c -> X' c) ->
  (X' = Xe \/ n_kind D = KEffect) ->
  well_behaved_program (ghost_publish n (set_node n D (with_next_id n s))) ->
  wkx X' (ghost_publish n (set_node n D (with_next_id n s))).
Proof.
  intros Hc [Hw [Hn [Hpd [Hran [Hh0 Hho]]]]] Hid HS HM HE HE2 HX Hall.
  set (s' := ghost_publish n (set_node n D (with_next_id n s))).
  assert (Hlt : forall m : node_id, is_Some (g_nodes s !! m) -> m < n).
  { intros m Hm. pose proof (core_ids s Hc m Hm). lia. }
  assert (Habs : g_nodes s !! n = None).
  { destruct (g_nodes s !! n) eqn:E; [|done]. pose proof (Hlt n (mk_is_Some _ _ E)). lia. }
  assert (Hsn : g_nodes s' = <[n := D]> (g_nodes s)).
  { subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (He' : g_edges s' = g_edges s).
  { subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hrn : g_running s' = g_running s).
  { subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hse : forall c y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y).
  { intros c y. subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hpa : gh_passed (g_ghost s') = gh_passed (g_ghost s)).
  { subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. done. }
  assert (Hsh : shapes s' = <[n := shape_of D]> (shapes s)) by (unfold shapes; rewrite Hsn; apply fmap_insert).
  assert (Ht1 : g_nodes s' !! n = Some D) by (rewrite Hsn; apply lookup_insert_eq).
  assert (Hne1 : forall m : node_id, m <> n -> g_nodes s' !! m = g_nodes s !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hsub : forall x v, shapes s !! x = Some v -> shapes s' !! x = Some v).
  { intros x v Hx. rewrite Hsh. rewrite lookup_insert_ne; [done|]. intros ->. unfold shapes in Hx. rewrite lookup_fmap, Habs in Hx. discriminate Hx. }
  assert (Hpb : forall m : node_id, m < n -> pure_in (shapes s') m -> pure_in (shapes s) m).
  { intros m Hm H. destruct (pure_exists s' m H) as [x Hx]. rewrite Hne1 in Hx by lia.
    apply (pure_in_sub _ _ _ Hsub); [rewrite (shapes_lookup s m x Hx); by eexists|done]. }
  assert (Hpf : forall m : node_id, pure_in (shapes s) m -> pure_in (shapes s') m).
  { intros m H. destruct (pure_exists s m H) as [x Hx]. assert (Hmn : m < n) by exact (Hlt m (mk_is_Some _ _ Hx)).
    destruct (pure_node s m x H Hx) as [Hk|(Hk&_&_)].
    - unfold pure_in. rewrite pure_at_unfold, (shapes_lookup s' m x ltac:(rewrite Hne1 by lia; exact Hx)). unfold shape_of. rewrite Hk. done.
    - exact (Hall m x ltac:(rewrite Hne1 by lia; exact Hx) Hk). }
  assert (Hback : forall m d', g_nodes s' !! m = Some d' -> (m = n /\ d' = D) \/ (m <> n /\ m < n /\ g_nodes s !! m = Some d')).
  { intros m d' Hm. destruct (decide (m = n)) as [->|Hm']; [left|right].
    - rewrite Ht1 in Hm. injection Hm as <-. done.
    - rewrite Hne1 in Hm by done. split_and!; [done|exact (Hlt m (mk_is_Some _ _ Hm))|done]. }
  assert (Hqe : forall c d, c < n -> quiet_effect s' c d -> quiet_effect s c d).
  { intros c d Hcn [H1 H2]. split; [done|]. revert H2. apply body_pure_mono. intros t Ht Hr.
    pose proof (readable_node _ _ Hr) as Hx. exact (Hpb t (Hlt t Hx) (Ht (readable_sub _ _ _ Hsub Hr))). }
  assert (Hqc : forall c d, c < n -> qc s' c d -> qc s c d).
  { intros c d Hcn [[H1 H2]|H]; [left; split; [exact (Hpb c Hcn H1)|done]|right; by apply Hqe]. }
  assert (Hrd : forall c y, y < n -> rd_ok s c y -> rd_ok s' c y).
  { intros c y Hy (H1&e&He&H2). unfold rd_ok. rewrite Hse. split; [done|]. exists e. rewrite Hne1 by lia. split; [done|exact H2]. }
  assert (Hqg : forall y, y < n -> quiet_or_gone s y -> quiet_or_gone s' y).
  { intros y Hy (e&He&H). exists e. rewrite Hne1 by lia. done. }
  assert (Hv : forall c, c <> n -> slot_view (g_edges s') c = slot_view (g_edges s) c) by (intros c _; by rewrite He').
  assert (Hempty : (slot_view (g_edges s') n).1 = []) by (rewrite He'; exact (slot_of_new s n Hc Habs)).
  destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8].
  assert (Hrun : forall m, m ∈ g_running s -> m < n).
  { intros m Hm. exact (Hlt m (ci_running _ _ _ (proj1 Hc) m Hm)). }
  split; [split|split].
  - intros m d Hd Hk. destruct (Hback m d Hd) as [[-> ->]|(_&_&H)]; [auto|by apply (W1 m d)].
  - intros c d Hd Hk Hp. destruct (Hback c d Hd) as [[-> ->]|(_&Hlc&H)].
    + destruct (HM Hk) as (A&B&C&E). split_and!; [done|done|]. intros Hcl. congruence.
    + apply (W2 c d H Hk). exact (Hpb c Hlc Hp).
  - intros c d Hd Hq. destruct (Hback c d Hd) as [[-> ->]|(_&Hlc&H)].
    + apply HE. apply Hq.
    + apply (W3 c d H). by apply Hqe.
  - intros c d Hd Hq t Ht. destruct (Hback c d Hd) as [[-> ->]|(Hcn&Hlc&H)].
    + rewrite Hempty in Ht. by apply not_elem_of_nil in Ht.
    + rewrite He' in Ht. destruct (W4 c d H (Hqc c d Hlc Hq) t Ht) as [A B]. split; [done|]. exact (Hpf t B).
  - intros c d Hd Hq Hl Hnr Hset t Ht Hnt. rewrite Hrn in Hnr, Hnt. destruct (Hback c d Hd) as [[-> ->]|(Hcn&Hlc&H)].
    + rewrite Hempty in Ht. by apply not_elem_of_nil in Ht.
    + rewrite He' in Ht. pose proof (Hqc c d Hlc Hq) as Hq0. destruct (W4 c d H Hq0 t Ht) as [_ A].
      destruct (pure_exists s t A) as [x9 Hx9]. pose proof (Hlt t (mk_is_Some _ _ Hx9)) as A9.
      apply Hrd; [lia|]. exact (W5 c d H Hq0 Hl Hnr Hset t Ht Hnt).
  - intros c d Hd Hk Hp Hl Hnr Hset. rewrite Hrn in Hnr. destruct (Hback c d Hd) as [[-> ->]|(Hcn&Hlc&H)].
    + exfalso. destruct (HM Hk) as (_&_&A&B). destruct Hset as [X|X]; congruence.
    + apply Hpb in Hp; [|done]. refine (value_ext s s' c d _ _ (W6 c d H Hk Hp Hl Hnr Hset)); [by rewrite He'|exact (Hse c)].
  - intros c d Hd Hk Hp Hl Hnr Hq t Ht. rewrite Hrn in Hnr. destruct (Hback c d Hd) as [[-> ->]|(Hcn&Hlc&H)].
    + exfalso. destruct (HM Hk) as (_&_&A&B). destruct Hq as [X|X]; congruence.
    + apply Hpb in Hp; [|done]. rewrite He' in Ht. destruct (W4 c d H (or_introl (conj Hp Hk)) t Ht) as [A0 _]. pose proof (A0 Hk) as A.
      apply Hqg; [lia|]. exact (W7 c d H Hk Hp Hl Hnr Hq t Ht).
  - intros c Hc1. rewrite Hpa in Hc1. destruct (W8 c Hc1) as [A B].
    assert (Hcn : c < n) by exact (Hlt c (Hpd c Hc1)).
    split; [intros K; apply A; by apply Hpb|]. intros Ha. apply B. intros m d Hd Hk. pose proof (Hlt m (mk_is_Some _ _ Hd)) as Hm.
    apply Hpb; [done|]. refine (Ha m d _ Hk). rewrite Hne1 by lia. done.
  - intros m dm Hm Hdm Hq. rewrite Hrn in Hm. pose proof (Hrun m Hm) as Hmn. rewrite Hne1 in Hdm by lia.
    exact (Hn m dm Hm Hdm (Hqc m dm Hmn Hq)).
  - split_and!.
    + intros c Hc1. rewrite Hpa in Hc1. pose proof (Hlt c (Hpd c Hc1)) as Hcn. rewrite Hne1 by lia. exact (Hpd c Hc1).
    + intros c d Hd Hq Hl Hf. destruct (Hback c d Hd) as [[-> ->]|(Hcn&Hlc&H)].
      * left. destruct Hq as [Hk _]. destruct (HE2 Hk) as (A&B&_). done.
      * destruct (Hran c d H (Hqe c d Hlc Hq) Hl Hf) as [[A B]|A].
        -- left. split; [|done]. destruct HX as [->|Hk]; [done|]. exact (proj2 (proj2 (HE2 Hk)) c A).
        -- right. refine (ran_ok_ext s s' c d d _ (Hse c) eq_refl eq_refl _ A); [by rewrite He'|]. subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. reflexivity.
    + split.
      * rewrite Hne1 by lia. exact Hh0.
      * intros o y Hy.
        assert (Hy0 : g_owners s !! o = Some y).
        { revert Hy. subst s'. unfold ghost_publish. simpl. rewrite lookup_insert_eq. simpl. done. }
        destruct (Hho o y Hy0) as [A B].
        assert (Hhl : o_host y < n).
        { destruct A as [A|A]; [lia|exact (Hlt _ A)]. }
        rewrite Hne1 by lia. split.
        -- destruct A as [A|A]; [by left|by right].
        -- intros d0 Hd0 Hq. exact (B d0 Hd0 (Hqe _ _ Hhl Hq)).
Qed.

Global Instance stable2_conj P Q `{!Stable2 P} `{!Stable2 Q} : Stable2 (fun s => P s /\ Q s).
Proof. intros s s' K [A B]. split; [exact (stable2 _ _ K A)|exact (stable2 _ _ K B)]. Qed.

Lemma wok_new_node {B} P `{!Stable2 P} (D : node_id -> node) (k : node_id -> M B) :
  (forall n, n_unannounced (D n) = false) -> (forall n, n_queued (D n) = false) -> (forall n, n_in_cleanup (D n) = false) ->
  (forall n, n_disposed (D n) = false) -> (forall n, n_kind (D n) = KSignal -> n_freshness (D n) = Clean) ->
  (forall n, n_kind (D n) = KSignal -> n_status (D n) = StNone /\ n_failure (D n) = None) ->
  (forall n, n_kind (D n) = KMemo -> n_scope (D n) = None /\ n_violated (D n) = false /\ n_freshness (D n) = Dirty) ->
  (forall n, n_kind (D n) <> KEffect) ->
  (forall n, ko (k n)) ->
  (forall n, wok (fun s => P s /\ has_kind n (n_kind (D n)) s) (k n)) ->
  wok P (m_bind next_id (fun n => m_bind (add_node n (D n)) (fun _ => k n))).
Proof.
  intros Hu Hq Hcl Hdi Hsg H1 H2 H3 Hko Hk. split; [|split].
  - intros s s' b Hc HP Hm. pose proof Hm as Hm0. unfold m_bind, next_id, add_node, m_modify in Hm. simpl in Hm.
    destruct Hc as [Hi Hx].
    set (n := S (g_next_id s)) in Hm.
    destruct (invariant_new_node s n (D n) Hi eq_refl (Hu _)) as [Hi1 F1].
    destruct (extras_new s n (D n) Hi Hx eq_refl (Hq _) (Hcl _) (Hdi _) (Hsg _)) as [Hx1 E1].
    assert (K1 : run_kept s (ghost_publish n (set_node n (D n) (with_next_id n s)))) by (split; done).
    assert (Hhk : has_kind n (n_kind (D n)) (ghost_publish n (set_node n (D n) (with_next_id n s)))).
    { exists (D n). split; [|done]. unfold ghost_publish. simpl. rewrite lookup_insert_eq. simpl. apply lookup_insert_eq. }
    destruct (Hk n) as [O _]. destruct (O _ _ _ (conj Hi1 Hx1) (conj (stable2 _ _ K1 HP) Hhk) Hm) as [Hc2 K2].
    split; [done|]. by eapply kept_trans.
  - by apply ko_new_node.
  - intros s s' b Hc Hw HP Hm Hg.
    pose proof Hm as Hm0. unfold m_bind, next_id, add_node, m_modify in Hm. simpl in Hm.
    set (n := S (g_next_id s)) in Hm.
    set (s1 := ghost_publish n (set_node n (D n) (with_next_id n s))) in Hm.
    pose proof Hc as Hc9. destruct Hc as [Hi Hx].
    destruct (invariant_new_node s n (D n) Hi eq_refl (Hu _)) as [Hi1 F1].
    destruct (extras_new s n (D n) Hi Hx eq_refl (Hq _) (Hcl _) (Hdi _) (Hsg _)) as [Hx1 E1].
    destruct (Hko n _ _ _ (core_ids _ (conj Hi1 Hx1)) Hm) as [_ K9].
    pose proof (good_back _ _ (core_ids _ (conj Hi1 Hx1)) K9 Hg) as Hg1.
    assert (Hw1 : wk s1).
    { apply (whole_new s n (D n) Xe Hc9 Hw eq_refl); [apply H1| | | |by left|exact (proj1 Hg1)].
      - intros Hk0. destruct (H2 n Hk0) as (X1&X2&X3). split_and!; auto.
      - intros Hk0. exfalso. exact (H3 n Hk0).
      - intros Hk0. exfalso. exact (H3 n Hk0). }
    assert (K1 : run_kept s s1) by (split; done).
    assert (Hhk : has_kind n (n_kind (D n)) s1).
    { exists (D n). split; [|done]. subst s1. unfold ghost_publish. simpl. rewrite lookup_insert_eq. simpl. apply lookup_insert_eq. }
    destruct (Hk n) as [_ [_ W]].
    exact (W _ _ _ (conj Hi1 Hx1) Hw1 (conj (stable2 _ _ K1 HP) Hhk) Hm Hg).
Qed.

Lemma wk_own s s' :
  same4 s s' -> g_trace s' = g_trace s ->
  (forall o y, g_owners s' !! o = Some y -> (exists y0, g_owners s !! o = Some y0 /\ o_host y0 = o_host y) \/
    ((o_host y = 0 \/ is_Some (g_nodes s !! o_host y)) /\ forall d, g_nodes s !! o_host y = Some d -> ~ quiet_effect s (o_host y) d)) ->
  wk s -> wk s'.
Proof.
  intros H0 H5 Ho [Hw [Hn [Hp [Hr Hh]]]]. pose proof H0 as (H1&H2&H3&H4). assert (Hb : same4 s' s) by (split_and!; congruence).
  assert (Hsh : shapes s' = shapes s) by exact (same4_shapes _ _ H0).
  split; [exact (whole_same _ _ H0 Hw)|split].
  - intros m dm Hm Hdm Hq. rewrite H4 in Hm. rewrite H1 in Hdm. exact (Hn m dm Hm Hdm (qc_same _ _ _ _ Hb Hq)).
  - split_and!.
    + intros c Hc. rewrite H3 in Hc. rewrite H1. auto.
    + apply (ran_frame _ s); [|exact Hr]. intros c d' Hd' Hq Hl Hf. rewrite H1 in Hd'. exists d'.
      split_and!; try done; [exact (isq_shape _ _ _ _ _ Hsh eq_refl Hq)|by rewrite H2|by rewrite H3|by rewrite H5].
    + apply (hosts_frame s); [| |exact Ho|exact Hh].
      * intros m d' Hd'. rewrite H1 in Hd'. exists d'. split; [done|exact (isq_shape _ _ _ _ _ Hsh eq_refl)].
      * intros m Hm. by rewrite H1.
Qed.

Lemma wk_same s s' : same6 s s' -> wk s -> wk s'.
Proof.
  intros (H1&H2&H3&H4&H5&H6). apply wk_own; [split_and!; done|done|].
  intros o y Hy. left. rewrite H6 in Hy. exists y. done.
Qed.


Lemma not_isq s c dc : run_base s -> np s -> c ∈ g_running s -> g_nodes s !! c = Some dc -> ~ isq s c dc.
Proof.
  intros Hb Hn Hc Hd [Hp|Hq].
  - exact (np_pure s c Hb Hn Hp Hc).
  - exact (Hn c dc Hc Hd (or_intror Hq)).
Qed.

Lemma wok_current_owner P : wok P current_owner.
Proof.
  split; [apply ok2_current_owner|split; [apply ko_current_owner|]].
  intros s s' a Hc Hw _ E _. pose proof (proj1 (proj2 Hc)) as Hb. unfold current_owner in E.
  destruct (g_current_owner s) as [o|]. { injection E as <- _. done. }
  destruct (g_current s) as [h|] eqn:Ec; [|done].
  destruct (g_nodes s !! h) as [d|] eqn:Hd; [|done].
  assert (Hq := not_isq s h d Hb (proj1 (proj2 Hw)) (b_current _ Hb h Ec) Hd).
  destruct (n_scope d) as [o|] eqn:Es.
  { injection E as <- _. apply (wk_same s); [split_and!; done|exact Hw]. }
  destruct (match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end).
  - injection E as <- _. exact (wframe_apply _ _ Hb (wframe_set s h d (node_with_violated true d) Hd eq_refl (fun K => False_ind _ (Hq K))) Hw).
  - injection E as <- _.
    set (o := stdpp.base.fresh (dom (g_owners s))) in *.
    pose proof (wframe_apply _ _ Hb (wframe_set s h d (node_with_scope (Some o) d) Hd eq_refl (fun K => False_ind _ (Hq K))) Hw) as Hw1.
    assert (Hsh1 : shapes (set_node h (node_with_scope (Some o) d) s) = shapes s) by exact (shapes_set s h d (node_with_scope (Some o) d) Hd eq_refl).
    refine (wk_own _ _ _ _ _ Hw1); [split_and!; done|done|].
    intros o0 y Hy. simpl in Hy. destruct (decide (o0 = o)) as [->|Hne].
    + rewrite lookup_insert_eq in Hy. injection Hy as <-. right. simpl. rewrite lookup_insert_eq. split; [right; by eexists|].
      intros d0 Hd0 Hqe. injection Hd0 as <-. apply Hq. right. exact (isq_shape _ _ _ _ _ Hsh1 eq_refl Hqe).
    + rewrite lookup_insert_ne in Hy by done. left. exists y. done.
Qed.

(** A read's [Track], with the current computation outside the invariant. *)
Lemma wk_track s n d s' :
  run_extras s -> wk s -> g_nodes s !! n = Some d -> n_kind d <> KEffect -> track n s = Some (s', tt) -> run_base s' /\ wk s'.
Proof.
  intros Hx Hw Hd Hk E. pose proof (proj1 Hx) as Hb. unfold track in E. destruct (g_current s) as [c|] eqn:Ec.
  2: { injection E as <-. done. }
  destruct (sources_add c n (g_edges s)) as [w|] eqn:Ea; [|done]. injection E as <-.
  destruct (extras_track s c n w d Hx Ec Ea Hd Hk) as (Hb'&_&_). split; [done|].
  destruct (slot_view_add _ _ _ _ (b_cursor _ Hb c) Ea) as (Hoth&_).
  assert (Hrc : c ∈ g_running s) by exact (b_current _ Hb c Ec).
  assert (Hne : forall c0 d0, g_nodes s !! c0 = Some d0 -> isq s c0 d0 -> c0 <> c).
  { intros c0 d0 Hd0 Hi ->. exact (not_isq s c d0 Hb (proj1 (proj2 Hw)) Hrc Hd0 Hi). }
  unfold ghost_read. match goal with |- wk (match ?x with _ => _ end) => assert (Hmx : x = Some d) by exact Hd; rewrite Hmx end.
  refine (wframe_apply _ _ Hb _ Hw). split; try done.
  - intros m e He _. exists e. split; [done|]. split_and!; done.
  - intros m e He. exists e. split_and!; [done|done|]. intros _. split_and!; done.
  - intros c0 d0 Hd0 Hi. left. simpl. apply Hoth. exact (Hne c0 d0 Hd0 Hi).
  - intros c0 d0 y Hd0 Hi. unfold seen_of. simpl. rewrite lookup_insert_ne; [done|]. intros Heq. injection Heq as H1 _. exact (Hne c0 d0 Hd0 Hi (eq_sym H1)).
  - intros c0 Hc0. by left.
  - intros m e Hm _. by left.
  - intros o y Hy. left. exists y. done.
Qed.

Lemma wk_mark_from s c f s' :
  run_base s -> wk s -> ~ pure_in (shapes s) c -> mark_check f c s = Some s' -> wk s'.
Proof.
  intros Hb Hw Hp Hm. refine (wk_touch s s' Hb Hw _).
  refine (ytouch_check _ s (HC_empty s Hb Hw) f c s s' (ytouch_refl _ s) _ Hm). intros K. done.
Qed.

Lemma wok_mark_new P n f : wok (fun s => P s /\ has_kind n KEffect s) (m_step (mark_dirty f n)).
Proof.
  split; [apply ok2_mark_dirty|split].
  - apply ko_mark. intros ? ?. apply mark_dirty_rtc.
  - intros s s' a Hc Hw [_ (d&Hd&Hk)] E _. unfold m_step in E. destruct (mark_dirty f n s) as [s1|] eqn:Em; [|done]. injection E as <- _.
    pose proof (proj1 (proj2 Hc)) as Hb. refine (wk_touch s s1 Hb Hw _).
    refine (ytouch_dirty _ s (HC_empty s Hb Hw) f n s s1 (ytouch_refl _ s) _ Em).
    intros Hp. exfalso. exact (pure_kind s n d Hp Hd Hk).
Qed.

(** ** A write *)

Definition qt_on (R : node_id -> node_id -> Prop) (s : state) : Prop :=
  forall c src dc e, R c src -> g_nodes s !! c = Some dc -> n_kind dc = KMemo -> pure_in (shapes s) c -> n_disposed dc = false ->
    c ∉ g_running s -> g_nodes s !! src = Some e -> n_kind e = KMemo -> src ∈ (slot_view (g_edges s) c).1 -> un e -> n_disposed e = false ->
    un dc.

Definition cf_on (s : state) : Prop :=
  f_unannounced (g_flags s) = true /\ world_well_formed (g_edges s) /\
  forall c d, g_nodes s !! c = Some d -> n_kind d = KMemo -> pure_in (shapes s) c -> n_freshness d = Clean -> n_unannounced d = false.

Lemma cf_rise s s' : rise s s' -> cf_on s -> cf_on s'.
Proof.
  intros R (F&W&H). split_and!; [by rewrite (rs_flags _ _ R)|by rewrite (rs_edges _ _ R)|].
  intros c d Hd Hk Hp Hc. destruct (rise_back _ _ _ _ R Hd) as (d0&Hd0&Hr). rewrite (rise_shapes _ _ R) in Hp.
  pose proof Hr as (_&_&_&_&_&_&_&Hf&Hfl).
  assert (Hc0 : n_freshness d0 = Clean) by (rewrite Hc in Hf; destruct (n_freshness d0); unfold freshness_le in Hf; simpl in Hf; try lia; done).
  pose proof (H c d0 Hd0 ltac:(by rewrite <- (rise_kind _ _ Hr)) Hp Hc0) as K.
  destruct (n_unannounced d) eqn:E; [|done]. rewrite K in Hfl. pose proof (Hfl eq_refl). done.
Qed.

Lemma qt_rise_others (R : node_id -> node_id -> Prop) s n d f u q :
  qt_on R s -> g_nodes s !! n = Some d -> freshness_le (n_freshness d) f -> (u = n_unannounced d \/ u = false) ->
  qt_on (fun c src => R c src /\ src <> n) (set_node n (node_marked f u q d) s).
Proof.
  intros H Hd Hf Hu c src dc e [Hr Hne] Hc Hk Hp Hl Hnr He Hke Hin Hun Hle.
  assert (Hsh : shapes (set_node n (node_marked f u q d) s) = shapes s) by exact (shapes_set s n d (node_marked f u q d) Hd eq_refl).
  rewrite Hsh in Hp. simpl in Hc, He, Hin, Hnr. rewrite lookup_insert_ne in He by done.
  destruct (decide (c = n)) as [->|Hcn].
  - rewrite lookup_insert_eq in Hc. injection Hc as <-. simpl in Hk, Hl.
    destruct (H n src d e Hr Hd Hk Hp Hl Hnr He Hke Hin Hun Hle) as [X1 X2]. split; simpl.
    + by eapply stale_rises.
    + destruct Hu as [->| ->]; done.
  - rewrite lookup_insert_ne in Hc by done. exact (H c src dc e Hr Hc Hk Hp Hl Hnr He Hke Hin Hun Hle).
Qed.

(** A memo's mark leaves it stale with no flag. *)
Lemma check_un f c s s' d :
  cf_on s -> g_nodes s !! c = Some d -> n_kind d = KMemo -> pure_in (shapes s) c -> mark_check f c s = Some s' ->
  exists d', g_nodes s' !! c = Some d' /\ un d'.
Proof.
  intros Hcf Hd Hk Hp Hm. destruct f as [|f]; [done|]. cbn [mark_check] in Hm. rewrite Hd, Hk in Hm.
  destruct Hcf as (Hfl0&_&Hcf).
  destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
  - pose proof (Hcf c d Hd Hk Hp Hc) as Hfl.
    assert (R : rise (set_node c (node_marked Check (n_unannounced d) (n_queued d) d) s) s').
    { apply rtc_rise. eapply notify_with_rtc; [|exact Hm]. intros ???. apply mark_check_rtc. }
    destruct (rs_fwd _ _ R c _ ltac:(simpl; apply lookup_insert_eq)) as (d'&Hd'&Hr). exists d'. split; [done|].
    refine (un_rises _ _ Hr _). split; simpl; done.
  - destruct (announce_due s d) eqn:Ea.
    + assert (R : rise (set_node c (node_marked (n_freshness d) false (n_queued d) d) s) s').
      { apply rtc_rise. eapply notify_with_rtc; [|exact Hm]. intros ???. apply mark_check_rtc. }
      destruct (rs_fwd _ _ R c _ ltac:(simpl; apply lookup_insert_eq)) as (d'&Hd'&Hr). exists d'. split; [done|].
      refine (un_rises _ _ Hr _). split; simpl; [by apply stale_of_not_clean|done].
    + injection Hm as <-. exists d. split; [done|]. split; [by apply stale_of_not_clean|].
      unfold announce_due in Ea. rewrite Hfl0 in Ea. exact Ea.
Qed.

Lemma notify_visit (mark : node_id -> state -> option state) src s s' m :
  (forall c s0 s1, mark c s0 = Some s1 -> rtc mark_prim s0 s1) ->
  notify_with mark src s = Some s' -> m ∈ observer_view (observers_of (g_edges s) src) ->
  exists s1 s2, rtc mark_prim s s1 /\ mark m s1 = Some s2 /\ rtc mark_prim s2 s'.
Proof.
  intros Hr Hn Hin. rewrite notify_with_list in Hn by done.
  destruct (mark_list_visit _ _ _ _ m Hr Hn (elem_rev _ _ Hin)) as (s1&s2&H1&H2&H3). eauto.
Qed.

Definition mq (R : node_id -> node_id -> Prop) (s : state) : Prop := cf_on s /\ qt_on R s.

Lemma qt_forward f R n d F u s s' :
  (forall c R0 s0 s1, mq R0 s0 -> mark_check f c s0 = Some s1 -> mq R0 s1) ->
  mq R s -> g_nodes s !! n = Some d -> freshness_le (n_freshness d) F -> (u = n_unannounced d \/ u = false) ->
  notify_with (mark_check f) n (set_node n (node_marked F u (n_queued d) d) s) = Some s' -> mq R s'.
Proof.
  intros IH [Hcf Hq] Hd Hf Hu Hw.
  set (s1 := set_node n (node_marked F u (n_queued d) d) s) in Hw.
  assert (R1 : rise s s1) by (by apply rise_set).
  assert (Hcf1 : cf_on s1) by exact (cf_rise _ _ R1 Hcf).
  assert (Hq1 : qt_on (fun c src => R c src /\ src <> n) s1) by (by apply qt_rise_others).
  assert (Hend : mq (fun c src => R c src /\ src <> n) s').
  { refine (walk_keeps _ _ _ _ _ _ _ _ Hw). { intros c s0 s2 Hs0 Hm. exact (IH c _ s0 s2 Hs0 Hm). } split; done. }
  assert (Rall : rise s1 s').
  { apply rtc_rise. eapply notify_with_rtc; [|exact Hw]. intros ???. apply mark_check_rtc. }
  destruct Hend as [Hcf' Hq'].
  split; [done|]. intros c src dc e Hr Hc Hk Hp Hl Hnr He Hke Hin Hun Hle.
  destruct (decide (src = n)) as [->|Hne]; [|exact (Hq' c src dc e (conj Hr Hne) Hc Hk Hp Hl Hnr He Hke Hin Hun Hle)].
  rewrite (rs_edges _ _ Rall) in Hin.
  destruct Hcf1 as [Fl1 [Wf1 Cl1]].
  assert (Hobs : c ∈ observer_view (observers_of (g_edges s1) n)) by exact (proj2 (held_iff_slot (g_edges s1) c n Wf1) Hin).
  destruct (notify_visit (mark_check f) n s1 s' c ltac:(intros ???; apply mark_check_rtc) Hw Hobs) as (sv&sw&H1&H2&H3).
  pose proof (rtc_rise _ _ H1) as Rv. pose proof (rtc_rise _ _ H3) as Rw.
  assert (Hcfv : cf_on sv) by (apply (cf_rise s1 sv Rv); split_and!; done).
  assert (Rvw : rise sv sw) by (apply rtc_rise; exact (mark_check_rtc _ _ _ _ H2)).
  destruct (rise_back _ _ _ _ Rw Hc) as (dw&Hdw&Rdw).
  destruct (rise_back _ _ _ _ Rvw Hdw) as (dv&Hdv&Rdv).
  assert (Hshv : shapes sv = shapes s').
  { rewrite (rise_shapes _ _ Rw), (rise_shapes _ _ Rvw). done. }
  destruct (check_un f c sv sw dv Hcfv Hdv ltac:(rewrite <- (rise_kind _ _ Rdv), <- (rise_kind _ _ Rdw); done) ltac:(by rewrite Hshv) H2) as (d'&Hd'&Hu').
  rewrite Hdw in Hd'. injection Hd' as <-. exact (un_rises _ _ Rdw Hu').
Qed.

Lemma effect_marked_mq R F n d s :
  mq R s -> g_nodes s !! n = Some d -> n_kind d = KEffect -> freshness_le (n_freshness d) F -> mq R (effect_marked F n d s).
Proof.
  intros [Hcf Hq] Hd Hk HF. unfold effect_marked. destruct (n_disposed d || n_in_cleanup d); [split; done|].
  assert (Hset : mq R (set_node n (node_marked F (n_unannounced d) true d) s)).
  { pose proof (rise_set s n d F (n_unannounced d) true Hd HF ltac:(by left)) as R0.
    split; [exact (cf_rise _ _ R0 Hcf)|].
    intros c src dc e Hr Hc Hk0 Hp Hl Hnr He Hke Hin Hun Hle.
    assert (Hsh : shapes (set_node n (node_marked F (n_unannounced d) true d) s) = shapes s) by exact (shapes_set s n d (node_marked F (n_unannounced d) true d) Hd eq_refl).
    rewrite Hsh in Hp. assert (Hcn : c <> n).
    { intros ->. simpl in Hc. rewrite lookup_insert_eq in Hc. injection Hc as <-. simpl in Hk0. congruence. }
    assert (Hsn : src <> n).
    { intros ->. simpl in He. rewrite lookup_insert_eq in He. injection He as <-. simpl in Hke. congruence. }
    simpl in Hc, He. rewrite lookup_insert_ne in Hc, He by done. exact (Hq c src dc e Hr Hc Hk0 Hp Hl Hnr He Hke Hin Hun Hle). }
  destruct (n_queued d); [done|]. destruct Hset as [[F1 [W1 C1]] Hq1]. split.
  - split_and!; [done|done|]. exact C1.
  - exact Hq1.
Qed.

Lemma mq_check f : forall c R s s', mq R s -> mark_check f c s = Some s' -> mq R s'.
Proof.
  induction f as [|f IH]; intros n R s s' Hs Hm; simpl in Hm; [done|].
  destruct (g_nodes s !! n) as [d|] eqn:Hd; [|by simplify_eq].
  destruct (n_kind d) eqn:Hk.
  - by simplify_eq.
  - destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
    + eapply (qt_forward f R n d Check); [exact IH|done|done| |by left|exact Hm]. rewrite Hc. apply freshness_le_clean.
    + destruct (announce_due s d); [|by simplify_eq].
      eapply (qt_forward f R n d (n_freshness d)); [exact IH|done|done|apply freshness_le_refl|by right|exact Hm].
  - simplify_eq. apply effect_marked_mq; [done|done|done|].
    destruct (decide _) as [->|_]; [apply freshness_le_clean|apply freshness_le_refl].
Qed.

Lemma mq_dirty f c R s s' : mq R s -> mark_dirty f c s = Some s' -> mq R s'.
Proof.
  rename c into n. intros Hs Hm. destruct f as [|f]; simpl in Hm; [done|].
  destruct (g_nodes s !! n) as [d|] eqn:Hd; [|by simplify_eq].
  destruct (n_kind d) eqn:Hk.
  - by simplify_eq.
  - destruct (decide (n_freshness d = Clean)) as [Hc|Hc].
    + eapply (qt_forward f R n d Dirty); [apply mq_check|done|done|apply freshness_le_dirty|by left|exact Hm].
    + destruct (announce_due s d) eqn:Ea.
      * eapply (qt_forward f R n d Dirty); [apply mq_check|done|done|apply freshness_le_dirty|by right|exact Hm].
      * destruct (decide (n_freshness d = Dirty)); [by simplify_eq|]. simplify_eq.
        destruct Hs as [Hcf Hq].
        pose proof (rise_set s n d Dirty (n_unannounced d) (n_queued d) Hd (freshness_le_dirty _) ltac:(by left)) as R0.
        split; [exact (cf_rise _ _ R0 Hcf)|].
        assert (Hund : un d).
        { split; [by apply stale_of_not_clean|]. unfold announce_due in Ea. rewrite (proj1 Hcf) in Ea. exact Ea. }
        intros c src dc e Hr Hc' Hk0 Hp Hl Hnr He Hke Hin Hun Hle.
        assert (Hsh : shapes (set_node n (node_marked Dirty (n_unannounced d) (n_queued d) d) s) = shapes s) by exact (shapes_set s n d (node_marked Dirty (n_unannounced d) (n_queued d) d) Hd eq_refl).
        rewrite Hsh in Hp. simpl in Hc', He, Hin, Hnr.
        assert (Hsrc : exists e0, g_nodes s !! src = Some e0 /\ un e0 /\ n_kind e0 = KMemo /\ n_disposed e0 = false).
        { destruct (decide (src = n)) as [->|Hsn].
          - rewrite lookup_insert_eq in He. injection He as <-. exists d. split_and!; [done|done|done|exact Hle].
          - rewrite lookup_insert_ne in He by done. eauto. }
        destruct Hsrc as (e0&He0&Hu0&Hk1&Hl1).
        destruct (decide (c = n)) as [->|Hcn].
        -- rewrite lookup_insert_eq in Hc'. injection Hc' as <-. split; simpl; [done|apply Hund].
        -- rewrite lookup_insert_ne in Hc' by done. exact (Hq c src dc e0 Hr Hc' Hk0 Hp Hl Hnr He0 Hk1 Hin Hu0 Hl1).
  - simplify_eq. apply effect_marked_mq; [done|done|done|apply freshness_le_dirty].
Qed.

(** After a write, each pure memo over the signal, and each quiet effect, is [Dirty] with no flag. *)
Lemma dirty_visit f n c s s' d :
  cf_on s -> g_nodes s !! c = Some d -> notify_with (mark_dirty f) n s = Some s' ->
  c ∈ observer_view (observers_of (g_edges s) n) ->
  (n_kind d = KMemo /\ pure_in (shapes s) c) \/ (n_kind d = KEffect /\ n_disposed d = false /\ n_in_cleanup d = false /\ n_unannounced d = false) ->
  exists d', g_nodes s' !! c = Some d' /\ n_freshness d' = Dirty /\ n_unannounced d' = false.
Proof.
  intros Hcf Hd Hw Hin Hk.
  destruct (notify_visit (mark_dirty f) n s s' c ltac:(intros ???; apply mark_dirty_rtc) Hw Hin) as (sv&sw&H1&H2&H3).
  pose proof (rtc_rise _ _ H1) as Rv. pose proof (rtc_rise _ _ H3) as Rw.
  destruct (rs_fwd _ _ Rv c d Hd) as (dv&Hdv&Rdv).
  pose proof (cf_rise _ _ Rv Hcf) as Hcfv. rewrite <- (rise_shapes _ _ Rv) in Hk.
  assert (Hw2 : exists dw, g_nodes sw !! c = Some dw /\ n_freshness dw = Dirty /\ n_unannounced dw = false).
  { destruct f as [|g]; [done|]. cbn [mark_dirty] in H2. rewrite Hdv in H2. rewrite (rise_kind _ _ Rdv) in H2.
    destruct Hk as [[Hk Hp]|(Hk&Hl&Hcl&Hfl)]; rewrite Hk in H2.
    - pose proof Hcfv as (Fl&_&Cf).
      assert (Hkv : n_kind dv = KMemo) by (by rewrite (rise_kind _ _ Rdv)).
      destruct (decide (n_freshness dv = Clean)) as [Hc|Hc].
      + assert (R : rise (set_node c (node_marked Dirty (n_unannounced dv) (n_queued dv) dv) sv) sw).
        { apply rtc_rise. eapply notify_with_rtc; [|exact H2]. intros ???. apply mark_check_rtc. }
        destruct (rs_fwd _ _ R c _ ltac:(simpl; apply lookup_insert_eq)) as (dw&Hdw&Hr). exists dw. split; [done|].
        pose proof Hr as (_&_&_&_&_&_&_&Hf&Hfl). split.
        * simpl in Hf. destruct (n_freshness dw); unfold freshness_le in Hf; simpl in Hf; try lia; done.
        * destruct (n_unannounced dw) eqn:E; [|done]. simpl in Hfl. rewrite (Cf c dv Hdv Hkv Hp Hc) in Hfl. pose proof (Hfl eq_refl). done.
      + destruct (announce_due sv dv) eqn:Ea.
        * assert (R : rise (set_node c (node_marked Dirty false (n_queued dv) dv) sv) sw).
          { apply rtc_rise. eapply notify_with_rtc; [|exact H2]. intros ???. apply mark_check_rtc. }
          destruct (rs_fwd _ _ R c _ ltac:(simpl; apply lookup_insert_eq)) as (dw&Hdw&Hr). exists dw. split; [done|].
          pose proof Hr as (_&_&_&_&_&_&_&Hf&Hfl). split.
          -- simpl in Hf. destruct (n_freshness dw); unfold freshness_le in Hf; simpl in Hf; try lia; done.
          -- destruct (n_unannounced dw) eqn:E; [|done]. pose proof (Hfl eq_refl). done.
        * unfold announce_due in Ea. rewrite Fl in Ea. destruct (decide (n_freshness dv = Dirty)) as [Hdd|Hdd].
          -- injection H2 as <-. exists dv. split_and!; done.
          -- injection H2 as <-. eexists. split; [simpl; apply lookup_insert_eq|]. split; done.
    - injection H2 as <-. unfold effect_marked.
      pose proof Rdv as (_&_&X1&X2&_&_&_&_&X3).
      assert (Hfv : n_unannounced dv = false) by (destruct (n_unannounced dv) eqn:E; [rewrite Hfl in X3; pose proof (X3 eq_refl); done|done]).
      rewrite X1, X2, Hl, Hcl. simpl.
      destruct (n_queued dv); [eexists; split; [simpl; apply lookup_insert_eq|split; done]|].
      eexists. split; [simpl; apply lookup_insert_eq|split; done]. }
  destruct Hw2 as (dw&Hdw&F1&F2). destruct (rs_fwd _ _ Rw c dw Hdw) as (d'&Hd'&Hr). exists d'.
  pose proof Hr as (_&_&_&_&_&_&_&Hf&Hfl). split_and!; [done| |].
  - rewrite F1 in Hf. destruct (n_freshness d'); unfold freshness_le in Hf; simpl in Hf; try lia; done.
  - destruct (n_unannounced d') eqn:E; [|done]. rewrite F2 in Hfl. pose proof (Hfl eq_refl). done.
Qed.

(** A write's state after its notice. *)
Lemma write_step f n d v s s2 :
  run_core s -> wk s -> g_nodes s !! n = Some d -> n_kind d = KSignal ->
  notify_dirty f n (ghost_publish n (set_node n (node_with_value v d) s)) = Some s2 -> wk s2.
Proof.
  intros Hc [Hw [Hnp [Hpd [Hran Hho]]]] Hd Hk Hn. pose proof (proj1 (proj2 Hc)) as Hb.
  set (s1 := set_node n (node_with_value v d) s) in Hn.
  assert (Hd1 : g_nodes s1 !! n = Some (node_with_value v d)) by (simpl; apply lookup_insert_eq).
  unfold ghost_publish in Hn. rewrite Hd1 in Hn.
  set (G := {| gh_published := _; gh_seen := _; gh_unnoticed := _; gh_window := _; gh_reads := _; gh_passed := _; gh_over := _; gh_run_reads := _; gh_nested := _ |}) in Hn.
  set (s3 := with_ghost G s1) in Hn.
  assert (Hsn : g_nodes s3 = <[n := node_with_value v d]> (g_nodes s)) by done.
  assert (He3 : g_edges s3 = g_edges s) by done.
  assert (Hr3 : g_running s3 = g_running s) by done.
  assert (Hse3 : forall c y, seen_of (g_ghost s3) c y = seen_of (g_ghost s) c y) by done.
  assert (Hpa3 : gh_passed (g_ghost s3) = gh_passed (g_ghost s)) by done.
  assert (Hsh3 : shapes s3 = shapes s) by exact (shapes_set s n d (node_with_value v d) Hd eq_refl).
  assert (Hrtc : rtc mark_prim s3 s2).
  { eapply notify_with_rtc; [|exact Hn]. intros ???. apply mark_dirty_rtc. }
  pose proof (rtc_rise _ _ Hrtc) as R. pose proof (marks_fixed _ _ Hrtc) as Mf.
  assert (Hsh : shapes s2 = shapes s) by (rewrite (rise_shapes _ _ R), Hsh3; done).
  assert (He2 : g_edges s2 = g_edges s) by (rewrite (rs_edges _ _ R), He3; done).
  assert (Hg2 : forall c y, seen_of (g_ghost s2) c y = seen_of (g_ghost s) c y) by (intros c y; rewrite (rs_ghost _ _ R); apply Hse3).
  assert (Hr2 : g_running s2 = g_running s) by (rewrite (rs_running _ _ R), Hr3; done).
  assert (Hp2 : gh_passed (g_ghost s2) = gh_passed (g_ghost s)) by (rewrite (rs_ghost _ _ R), Hpa3; done).
  assert (Hne3 : forall m : node_id, m <> n -> g_nodes s3 !! m = g_nodes s !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hfw : forall m dm, m <> n -> g_nodes s !! m = Some dm -> exists dm', g_nodes s2 !! m = Some dm' /\ rise_node dm dm').
  { intros m dm Hm Hdm. refine (rs_fwd _ _ R m dm _). by rewrite Hne3. }
  assert (Hbk : forall m dm', m <> n -> g_nodes s2 !! m = Some dm' -> exists dm, g_nodes s !! m = Some dm /\ rise_node dm dm').
  { intros m dm' Hm Hdm'. destruct (rise_back _ _ _ _ R Hdm') as (x&Hx&Hr). rewrite Hne3 in Hx by done. eauto. }
  destruct (rs_fwd _ _ R n _ (ltac:(rewrite Hsn; apply lookup_insert_eq) : g_nodes s3 !! n = Some (node_with_value v d))) as (dn&Hdn&Rn).
  destruct (mf_forward _ _ Mf n _ (ltac:(rewrite Hsn; apply lookup_insert_eq) : g_nodes s3 !! n = Some (node_with_value v d))) as (dn'&Hdn'&(_&_&_&_&_&_&Hfsig&_)&_).
  rewrite Hdn in Hdn'. injection Hdn' as <-.
  destruct (b_window _ Hb) as (_&_&Wsig&_).
  assert (Hcl_n : n_freshness dn = Clean).
  { rewrite (Hfsig Hk). exact (Wsig n d Hd Hk). }
  assert (Hkn : n_kind dn = KSignal) by (rewrite (rise_kind _ _ Rn); exact Hk).
  (* the invariant of the marks *)
  destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8].
  assert (Hcf3 : cf_on s3).
  { split_and!.
    - exact (b_flag_u _ Hb).
    - rewrite He3. exact (reachable_well_formed _ (me_reachable _ (b_env _ Hb))).
    - intros c dc Hdc Hkc Hpc. rewrite Hsh3 in Hpc.
      assert (Hcn : c <> n) by (intros ->; rewrite Hsn, lookup_insert_eq in Hdc; injection Hdc as <-; simpl in Hkc; congruence).
      rewrite Hne3 in Hdc by done. exact (proj2 (proj2 (W2 c dc Hdc Hkc Hpc))). }
  assert (Hq3 : qt_on (fun _ _ => True) s3).
  { intros c src dc e _ Hdc Hkc Hpc Hlc Hnrc He Hke Hin Hun Hle. rewrite Hsh3 in Hpc. rewrite He3 in Hin. rewrite Hr3 in Hnrc.
    assert (Hcn : c <> n) by (intros ->; rewrite Hsn, lookup_insert_eq in Hdc; injection Hdc as <-; simpl in Hkc; congruence).
    assert (Hsn' : src <> n) by (intros ->; rewrite Hsn, lookup_insert_eq in He; injection He as <-; simpl in Hke; congruence).
    rewrite Hne3 in Hdc, He by done.
    destruct (quiet_dec dc) as [Q|Q].
    - exfalso. destruct (W7 c dc Hdc Hkc Hpc Hlc Hnrc Q src Hin) as (e'&He'&Q'). rewrite He in He'. injection He' as <-.
      destruct Q' as [Q'|Q']; [exact (un_not_quiet _ Hun Q')|congruence].
    - split.
      + apply stale_of_not_clean. intros K. apply Q. by left.
      + destruct (n_unannounced dc) eqn:E; [|done]. exfalso. apply Q. by right. }
  assert (Hmq : mq (fun _ _ => True) s2).
  { refine (walk_keeps _ _ _ _ _ _ _ _ Hn). { intros c s0 s4 Hs0 Hm. exact (mq_dirty f c _ s0 s4 Hs0 Hm). } split; done. }
  destruct Hmq as [_ Hqt].
  assert (Hdirty : forall c dc, g_nodes s2 !! c = Some dc -> c ∉ g_running s -> n_disposed dc = false -> n ∈ (slot_view (g_edges s) c).1 -> qc s2 c dc -> c <> n -> ~ settled dc).
  { intros c dc Hdc Hnr Hl Hin Hq Hcn Hset.
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd).
    assert (Hobs : c ∈ observer_view (observers_of (g_edges s3) n)).
    { rewrite He3. exact (proj2 (held_iff_slot (g_edges s) c n (reachable_well_formed _ (me_reachable _ (b_env _ Hb)))) Hin). }
    assert (Hq0 : qc s c d0).
    { pose proof Rd as (Hs0&_). exact (qc_shape_back _ _ _ _ _ Hsh Hs0 Hq). }
    assert (Hd03 : g_nodes s3 !! c = Some d0) by (by rewrite Hne3).
    assert (Hcase : (n_kind d0 = KMemo /\ pure_in (shapes s3) c) \/ (n_kind d0 = KEffect /\ n_disposed d0 = false /\ n_in_cleanup d0 = false /\ n_unannounced d0 = false)).
    { destruct Hq0 as [[Y1 Y2]|Y]; [left; split; [done|by rewrite Hsh3]|right].
      pose proof Rd as (_&_&Di&_). split_and!; [apply Y|congruence| |exact (proj2 (W3 c d0 Hd0 Y))].
      destruct (n_in_cleanup d0) eqn:E; [|done]. exfalso. apply Hnr. exact (b_cleanup_running _ Hb c d0 Hd0 E). }
    destruct (dirty_visit f n c s3 s2 d0 Hcf3 Hd03 Hn Hobs Hcase) as (d'&Hd'&F1&F2).
    rewrite Hdc in Hd'. injection Hd' as <-. destruct Hset as [X|X]; congruence. }
  split; [split|split].
  - intros m dm Hdm Hkm. destruct (decide (m = n)) as [->|Hm].
    + rewrite Hdn in Hdm. injection Hdm as <-. pose proof Rn as (_&V&_). unfold read_view_of in V. injection V as _ X Y.
      destruct (W1 n d Hd Hk) as [A B]. simpl in X, Y. split; congruence.
    + destruct (Hbk m dm Hm Hdm) as (d0&Hd0&Rd). destruct (W1 m d0 Hd0 ltac:(by rewrite <- (rise_kind _ _ Rd))) as [A B].
      pose proof Rd as (_&V&_). unfold read_view_of in V. injection V as _ X Y. split; congruence.
  - rewrite Hsh. intros c dc Hdc Hkc Hpc. destruct (decide (c = n)) as [->|Hcn]; [rewrite Hdn in Hdc; injection Hdc as <-; congruence|].
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd).
    exact (memo_fields_rise d0 dc Rd (W2 c d0 Hd0 ltac:(by rewrite <- (rise_kind _ _ Rd)) Hpc)).
  - intros c dc Hdc Hq. destruct (decide (c = n)) as [->|Hcn].
    { exfalso. rewrite Hdn in Hdc. injection Hdc as <-. destruct Hq as [Y _]. congruence. }
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd). pose proof Rd as (Hs0&_&_&_&_&Sc&_&_&Fl).
    pose proof (isq_shape _ _ _ _ _ Hsh Hs0 Hq) as Hq0. destruct (W3 c d0 Hd0 Hq0) as [A B]. split; [congruence|].
    destruct (n_unannounced dc) eqn:E; [|done]. rewrite B in Fl. pose proof (Fl eq_refl). done.
  - intros c dc Hdc Hq t Ht. rewrite Hsh. rewrite He2 in Ht. destruct (decide (c = n)) as [->|Hcn].
    { exfalso. rewrite Hdn in Hdc. injection Hdc as <-. destruct Hq as [[_ Y]|[Y _]]; congruence. }
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd). pose proof Rd as (Hs0&_).
    rewrite (rise_kind _ _ Rd). exact (W4 c d0 Hd0 (qc_shape_back _ _ _ _ _ Hsh Hs0 Hq) t Ht).
  - intros c dc Hdc Hq Hl Hnr Hset t Ht Hnt. rewrite He2 in Ht. rewrite Hr2 in Hnr, Hnt.
    destruct (decide (c = n)) as [->|Hcn].
    { exfalso. rewrite Hdn in Hdc. injection Hdc as <-. destruct Hq as [[_ Y]|[Y _]]; congruence. }
    destruct (decide (t = n)) as [->|Htn]; [exfalso; exact (Hdirty c dc Hdc Hnr Hl Ht Hq Hcn Hset)|].
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd). pose proof Rd as (Hs0&_&Di&_).
    destruct (W5 c d0 Hd0 (qc_shape_back _ _ _ _ _ Hsh Hs0 Hq) ltac:(congruence) Hnr (rise_settled _ _ Rd Hset) t Ht Hnt) as (H1&e&He&H2).
    unfold rd_ok. rewrite Hg2. split; [done|]. destruct (Hfw t e Htn He) as (e'&He'&_&V&_). exists e'. split; [done|]. intros x Hx. rewrite V. auto.
  - intros c dc Hdc Hkc Hpc Hl Hnr Hset. rewrite Hr2 in Hnr. rewrite Hsh in Hpc. destruct (decide (c = n)) as [->|Hcn]; [rewrite Hdn in Hdc; injection Hdc as <-; congruence|].
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd). pose proof Rd as (Hs0&V&Di&_).
    destruct (value_ext s s2 c d0 ltac:(by rewrite He2) (Hg2 c) (W6 c d0 Hd0 ltac:(by rewrite <- (rise_kind _ _ Rd)) Hpc ltac:(congruence) Hnr (rise_settled _ _ Rd Hset))) as (r&Hb0&Hm0).
    unfold shape_of in Hs0. injection Hs0 as _ _ E B. exists r. rewrite E, B. split; [done|]. exact (view_matches_view _ _ _ V Hm0).
  - intros c dc Hdc Hkc Hpc Hl Hnr Hqd t Ht. rewrite Hr2 in Hnr. rewrite He2 in Ht. rewrite Hsh in Hpc.
    assert (Hcn : c <> n) by (intros ->; rewrite Hdn in Hdc; injection Hdc as <-; congruence).
    destruct (Hbk c dc Hcn Hdc) as (d0&Hd0&Rd). assert (Hkd0 : n_kind d0 = KMemo) by (by rewrite <- (rise_kind _ _ Rd)).
    destruct (W4 c d0 Hd0 (or_introl (conj Hpc Hkd0)) t Ht) as [_ Hpt].
    destruct (pure_exists s t Hpt) as [e0 He0].
    assert (Hx : exists e, g_nodes s2 !! t = Some e /\ (n_kind e = KSignal \/ n_kind e = KMemo)).
    { destruct (decide (t = n)) as [->|Htn]; [exists dn; split; [done|by left]|].
      destruct (Hfw t e0 Htn He0) as (e&He&Re). exists e. split; [done|]. rewrite (rise_kind _ _ Re).
      destruct (pure_node s t e0 Hpt He0) as [K|(K&_&_)]; [by left|by right]. }
    destruct Hx as (e&He&[Hke|Hke]).
    + exists e. split; [done|]. left; left.
      destruct (decide (t = n)) as [->|Htn]; [rewrite Hdn in He; injection He as <-; done|].
      destruct (mf_forward _ _ Mf t e0 ltac:(by rewrite Hne3)) as (e'&He'&(_&_&_&_&_&_&Hfs&_)&_).
      rewrite He in He'. injection He' as <-. destruct (Hfw t e0 Htn He0) as (e1&He1&Re). rewrite He in He1. injection He1 as <-.
      assert (Hke0 : n_kind e0 = KSignal) by (by rewrite <- (rise_kind _ _ Re)). rewrite (Hfs Hke0). exact (Wsig t e0 He0 Hke0).
    + destruct (quiet_dec e) as [Q|Q]; [exists e; split; [done|by left]|].
      destruct (n_disposed e) eqn:Ed; [exists e; split; [done|by right]|]. exfalso.
      assert (Hu : un e).
      { split.
        - apply stale_of_not_clean. intros K. apply Q. by left.
        - destruct (n_unannounced e) eqn:E; [|done]. exfalso. apply Q. by right. }
      refine (un_not_quiet _ (Hqt c t dc e I Hdc Hkc ltac:(by rewrite Hsh) Hl ltac:(by rewrite Hr2) He Hke ltac:(by rewrite He2) Hu Ed) Hqd).
  - intros c Hc1. rewrite Hsh. rewrite Hp2 in Hc1. destruct (W8 c Hc1) as [A B]. split; [done|]. intros Ha. apply B.
    intros m dm Hdm Hkm. rewrite <- Hsh. destruct (decide (m = n)) as [->|Hm]; [rewrite Hd in Hdm; injection Hdm as <-; congruence|].
    destruct (Hfw m dm Hm Hdm) as (dm'&Hdm'&Rm). refine (Ha m dm' Hdm' _). by rewrite (rise_kind _ _ Rm).
  - intros m dm Hm Hdm Hq. rewrite Hr2 in Hm.
    assert (Hmn : m <> n) by (intros ->; exact (np_pure s n Hb Hnp ltac:(unfold pure_in; rewrite pure_at_unfold, (shapes_lookup s n d Hd); unfold shape_of; rewrite Hk; done) Hm)).
    destruct (Hbk m dm Hmn Hdm) as (d0&Hd0&Rd). pose proof Rd as (Hs0&_).
    exact (Hnp m d0 Hm Hd0 (qc_shape_back _ _ _ _ _ Hsh Hs0 Hq)).
  - split_and!.
    + intros c Hc1. rewrite Hp2 in Hc1. destruct (Hpd c Hc1) as [e He]. destruct (decide (c = n)) as [->|Hcn]; [by eexists|].
      destruct (Hfw c e Hcn He) as (e'&He'&_). by eexists.
    + apply (ran_frame _ s); [|exact Hran]. intros c d' Hd' Hq Hl Hf.
      assert (Hcn : c <> n) by (intros ->; rewrite Hdn in Hd'; injection Hd' as <-; destruct Hq as [Hq _]; congruence).
      destruct (Hbk c d' Hcn Hd') as (d0&Hd0&Rd). pose proof Rd as (Hs0&Vv&Di&_&_&_&_&Fl&_).
      pose proof (isq_shape _ _ _ _ _ Hsh Hs0 Hq) as Hq0.
      unfold shape_of in Hs0. injection Hs0 as _ _ E B.
      exists d0. split_and!; [done|done|congruence| | |done|done|by rewrite !read_prefix_view, He2|exact (Hg2 c)|].
      * intros K. rewrite K in Fl. unfold freshness_le in Fl. destruct (n_freshness d') eqn:E9; simpl in Fl; try lia. done.
      * unfold window, read_view_of in *. injection Vv as _ S1 _. congruence.
      * rewrite (rs_trace _ _ R). reflexivity.
    + apply (hosts_frame s); [| |intros o y Hy; left; exists y; rewrite (rs_owners _ _ R) in Hy; done|exact Hho].
      * intros m d' Hd'. destruct (decide (m = n)) as [->|Hmn].
        -- exists d. split; [done|]. intros [Hq _]. rewrite Hdn in Hd'. injection Hd' as <-. congruence.
        -- destruct (Hbk m d' Hmn Hd') as (d0&Hd0&Rd). pose proof Rd as (Hs0&_). exists d0. split; [done|exact (isq_shape _ _ _ _ _ Hsh Hs0)].
      * intros m [e He]. destruct (decide (m = n)) as [->|Hmn]; [by eexists|].
        destruct (Hfw m e Hmn He) as (e'&He'&_). by eexists.
Qed.

(** ** A quiet effect's run *)

Lemma effect_not_pure s n d : g_nodes s !! n = Some d -> n_kind d = KEffect -> ~ pure_in (shapes s) n.
Proof. intros Hd Hk Hp. exact (pure_kind s n d Hp Hd Hk). Qed.

(** The start of a quiet effect's body, from the state ahead of its dequeue. *)
Lemma effect_begin s0 s7 n d d7 (L : node_id) :
  wk s0 -> run_base s0 -> n ∉ g_running s0 -> g_nodes s0 !! n = Some d -> quiet_effect s0 n d -> n_disposed d = false ->
  g_nodes s7 = <[n := d7]> (g_nodes s0) -> shape_of d7 = shape_of d -> n_scope d7 = n_scope d -> n_unannounced d7 = n_unannounced d -> n_disposed d7 = false ->
  (forall c, c <> n -> slot_view (g_edges s7) c = slot_view (g_edges s0) c) ->
  slot_view (g_edges s7) n = ((slot_view (g_edges s0) n).1, 0) ->
  (forall c y, seen_of (g_ghost s7) c y = seen_of (g_ghost s0) c y) ->
  (forall c, c ∈ gh_passed (g_ghost s7) -> c ∈ gh_passed (g_ghost s0)) ->
  g_running s7 = n :: g_running s0 -> g_current s7 = Some n ->
  whole s7 /\ stk (fun x => x = n) L s7 /\ qc s7 n d7.
Proof.
  intros [Hw [Hnp Hpd]] Hb Hnrn Hd Hq Hl Hsn Hshd Hsc Hfl Hl7 Hv Hvn Hse Hpa Hrun Hcur.
  assert (Hk : n_kind d = KEffect) by apply Hq.
  assert (Hsh : shapes s7 = shapes s0).
  { unfold shapes. rewrite Hsn, fmap_insert, Hshd. apply insert_id. by rewrite lookup_fmap, Hd. }
  assert (Hk7 : n_kind d7 = KEffect) by (unfold shape_of in Hshd; injection Hshd as X _ _ _; congruence).
  assert (Hnpn : ~ pure_in (shapes s0) n) by exact (effect_not_pure s0 n d Hd Hk).
  assert (Ht1 : g_nodes s7 !! n = Some d7) by (rewrite Hsn; apply lookup_insert_eq).
  assert (Hne1 : forall m : node_id, m <> n -> g_nodes s7 !! m = g_nodes s0 !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hback : forall m d', g_nodes s7 !! m = Some d' -> (m = n /\ d' = d7) \/ (m <> n /\ g_nodes s0 !! m = Some d')).
  { intros m d' Hm. destruct (decide (m = n)) as [->|Hm']; [left|right].
    - rewrite Ht1 in Hm. injection Hm as <-. done.
    - rewrite Hne1 in Hm by done. done. }
  assert (Hqe7 : quiet_effect s7 n d7) by exact (isq_shape_fwd _ _ _ _ _ Hsh Hshd Hq).
  assert (Hrd : forall c y, pure_in (shapes s0) y -> rd_ok s0 c y -> rd_ok s7 c y).
  { intros c y Hy (H1&e&He&H2). unfold rd_ok. rewrite Hse. split; [done|]. exists e.
    assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  assert (Hqg : forall y, pure_in (shapes s0) y -> quiet_or_gone s0 y -> quiet_or_gone s7 y).
  { intros y Hy (e&He&Q). exists e. assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  split_and!.
  - destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8]. split; rewrite ?Hsh.
    + intros m dm Hdm Hkm. destruct (Hback m dm Hdm) as [[-> ->]|[_ H]]; [congruence|by apply (W1 m dm)].
    + intros c dc Hdc Hkc Hpc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]]; [congruence|by apply (W2 c dc)].
    + intros c dc Hdc Hqc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]].
      * rewrite Hsc, Hfl. exact (W3 n d Hd Hq).
      * apply (W3 c dc H). exact (isq_shape _ _ _ _ _ Hsh eq_refl Hqc).
    + intros c dc Hdc Hqc t Ht. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]].
      * rewrite Hvn in Ht. pose proof (W4 n d Hd (or_intror Hq) t Ht) as [_ B9]. split; [intros K9; exfalso; pose proof Hshd as Hs9; unfold shape_of in Hs9; injection Hs9 as Kd _ _ _; destruct Hq as [Kq _]; congruence|exact B9].
      * rewrite Hv in Ht by done. exact (W4 c dc H (qc_ext _ _ _ _ Hsh Hqc) t Ht).
    + intros c dc Hdc Hqc Hl0 Hnr0 Hset t Ht Hnt. rewrite Hrun in Hnr0, Hnt. apply not_elem_of_cons in Hnr0 as [Hcn Hnr0]. apply not_elem_of_cons in Hnt as [Htn Hnt].
      rewrite Hne1 in Hdc by done. rewrite Hv in Ht by done. pose proof (qc_ext _ _ _ _ Hsh Hqc) as Hq0.
      destruct (W4 c dc Hdc Hq0 t Ht) as [_ Hpt]. apply Hrd; [done|]. exact (W5 c dc Hdc Hq0 Hl0 Hnr0 Hset t Ht Hnt).
    + intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hset. rewrite Hrun in Hnr0. apply not_elem_of_cons in Hnr0 as [Hcn Hnr0].
      rewrite Hne1 in Hdc by done. refine (value_ext s0 s7 c dc _ _ (W6 c dc Hdc Hkc Hpc Hl0 Hnr0 Hset)); [by rewrite !read_prefix_view, Hv|exact (Hse c)].
    + intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hq0 t Ht. rewrite Hrun in Hnr0. apply not_elem_of_cons in Hnr0 as [Hcn Hnr0].
      rewrite Hne1 in Hdc by done. rewrite Hv in Ht by done. destruct (W4 c dc Hdc (or_introl (conj Hpc Hkc)) t Ht) as [_ Hpt].
      apply Hqg; [done|]. exact (W7 c dc Hdc Hkc Hpc Hl0 Hnr0 Hq0 t Ht).
    + intros c Hc. destruct (W8 c (Hpa c Hc)) as [A B]. split; [done|]. intros Ha. apply B. intros m dm Hdm Hkm.
      rewrite <- Hsh. destruct (decide (m = n)) as [->|Hm]; [rewrite Hd in Hdm; injection Hdm as <-; congruence|].
      refine (Ha m dm _ Hkm). by rewrite Hne1.
  - split; rewrite ?Hsh, ?Hrun.
    + intros m Hm Hpm. exfalso. apply elem_of_cons in Hm as [->|Hm]; [done|exact (np_pure s0 m Hb Hnp Hpm Hm)].
    + intros x ->. left.
    + intros x y dy -> Hdy Hky Hpy Hin. exfalso. assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 in Hdy by done. rewrite Hv in Hin by done.
      exact (Hnpn (proj2 (w_slots _ Hw y dy Hdy (or_introl (conj Hpy Hky)) n Hin))).
    + intros x dx Hx Hdx Hkx Hpx HT. exfalso. apply elem_of_cons in Hx as [->|Hx]; [done|exact (np_pure s0 x Hb Hnp Hpx Hx)].
    + intros x dx Hx Hdx Hq0 t Ht Hnt. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. apply elem_of_cons in Hx as [->|Hx].
      * exfalso. rewrite read_prefix_view, Hvn in Ht. simpl in Ht. rewrite take_0 in Ht. by apply not_elem_of_nil in Ht.
      * exfalso. assert (Hxn : x <> n) by (intros ->; done). rewrite Hne1 in Hdx by done. exact (Hnp x dx Hx Hdx Hq1).
    + intros x dx Hx Hdx Hq0. pose proof (qc_ext _ _ _ _ Hsh Hq0) as Hq1. apply elem_of_cons in Hx as [->|Hx].
      * rewrite Ht1 in Hdx. injection Hdx as <-. done.
      * exfalso. assert (Hxn : x <> n) by (intros ->; done). rewrite Hne1 in Hdx by done. exact (Hnp x dx Hx Hdx Hq1).
    + left. unfold deferring. by rewrite Hcur.
    + intros x. destruct (decide (x = n)); [by left|by right].
  - by right.
Qed.

(** The end of a quiet effect's run. Its slots are what its body read, each still answering what it read. *)
Lemma effect_finish s0 s8 s' n d8 dF (L : node_id) :
  wk s0 -> whole s8 -> stk (fun x => x = n) L s8 -> g_nodes s8 !! n = Some d8 -> qc s8 n d8 -> n_kind d8 = KEffect ->
  shapes s8 = shapes s0 -> (forall c, c ∈ gh_passed (g_ghost s8) -> c ∈ gh_passed (g_ghost s0)) ->
  (forall m dm', g_nodes s8 !! m = Some dm' -> exists dm, g_nodes s0 !! m = Some dm /\ shape_of dm' = shape_of dm) ->
  (forall m dm, g_nodes s0 !! m = Some dm -> is_Some (g_nodes s8 !! m)) ->
  g_running s8 = n :: g_running s0 -> n ∉ g_running s0 ->
  g_nodes s' = <[n := dF]> (g_nodes s8) -> shape_of dF = shape_of d8 -> n_scope dF = n_scope d8 -> n_unannounced dF = n_unannounced d8 ->
  (forall c, c <> n -> slot_view (g_edges s') c = slot_view (g_edges s8) c) ->
  (slot_view (g_edges s') n).1 = read_prefix (g_edges s8) n ->
  (forall c y, seen_of (g_ghost s') c y = seen_of (g_ghost s8) c y) ->
  gh_passed (g_ghost s') = gh_passed (g_ghost s8) -> g_running s' = g_running s0 ->
  ran Xe s' -> hosts s' -> wk s'.
Proof.
  intros [Hw0 [Hnp0 [Hpd0 _]]] Hw St Hd8 Hq8 Hk8 Sh8 Pa8 Bk8 Fw8 Run8 Hnrn Hsn Hshd Hsc Hfl Hv Hvn Hse Hpa Hrun Hran Hho.
  assert (Hsh : shapes s' = shapes s8).
  { unfold shapes. rewrite Hsn, fmap_insert, Hshd. apply insert_id. by rewrite lookup_fmap, Hd8. }
  assert (HkF : n_kind dF = KEffect) by (unfold shape_of in Hshd; injection Hshd as X _ _ _; congruence).
  assert (Hnpn : ~ pure_in (shapes s8) n) by exact (effect_not_pure s8 n d8 Hd8 Hk8).
  assert (Ht1 : g_nodes s' !! n = Some dF) by (rewrite Hsn; apply lookup_insert_eq).
  assert (Hne1 : forall m : node_id, m <> n -> g_nodes s' !! m = g_nodes s8 !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hback : forall m d', g_nodes s' !! m = Some d' -> (m = n /\ d' = dF) \/ (m <> n /\ g_nodes s8 !! m = Some d')).
  { intros m d' Hm. destruct (decide (m = n)) as [->|Hm']; [left|right].
    - rewrite Ht1 in Hm. injection Hm as <-. done.
    - rewrite Hne1 in Hm by done. done. }
  assert (Hrd : forall c y, pure_in (shapes s8) y -> rd_ok s8 c y -> rd_ok s' c y).
  { intros c y Hy (H1&e&He&H2). unfold rd_ok. rewrite Hse. split; [done|]. exists e.
    assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  assert (Hqg : forall y, pure_in (shapes s8) y -> quiet_or_gone s8 y -> quiet_or_gone s' y).
  { intros y Hy (e&He&Q). exists e. assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  assert (Hnr8 : forall m, m ∉ g_running s' -> m <> n -> m ∉ g_running s8).
  { intros m Hm Hmn. rewrite Run8. apply not_elem_of_cons. rewrite Hrun in Hm. done. }
  destruct Hw as [W1 W2 W3 W4 W5 W6 W7 W8].
  split; [split|split].
  - intros m dm Hdm Hkm. destruct (Hback m dm Hdm) as [[-> ->]|[_ H]]; [congruence|by apply (W1 m dm)].
  - rewrite Hsh. intros c dc Hdc Hkc Hpc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]]; [congruence|by apply (W2 c dc)].
  - intros c dc Hdc Hqc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]].
    + rewrite Hsc, Hfl. destruct Hq8 as [[Y _]|Y]; [done|exact (W3 n d8 Hd8 Y)].
    + apply (W3 c dc H). exact (isq_shape _ _ _ _ _ Hsh eq_refl Hqc).
  - rewrite Hsh. intros c dc Hdc Hqc t Ht. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]].
    + rewrite Hvn in Ht. rewrite read_prefix_view in Ht. apply elem_take in Ht. pose proof (W4 n d8 Hd8 Hq8 t Ht) as [_ B9]. split; [intros K9; congruence|exact B9].
    + rewrite Hv in Ht by done. exact (W4 c dc H (qc_ext _ _ _ _ Hsh Hqc) t Ht).
  - intros c dc Hdc Hqc Hl0 Hnr0 Hset t Ht Hnt. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]].
    + rewrite Hvn in Ht. pose proof Ht as Ht0. rewrite read_prefix_view in Ht0. apply elem_take in Ht0.
      destruct (W4 n d8 Hd8 Hq8 t Ht0) as [_ Hpt]. apply Hrd; [done|].
      assert (Htn : t <> n) by (intros ->; done).
      exact (proj1 (st_read _ _ _ St n d8 ltac:(rewrite Run8; left) Hd8 Hq8 t Ht (Hnr8 t Hnt Htn))).
    + rewrite Hv in Ht by done. pose proof (qc_ext _ _ _ _ Hsh Hqc) as Hq0. destruct (W4 c dc H Hq0 t Ht) as [_ Hpt].
      assert (Htn : t <> n) by (intros ->; done). apply Hrd; [done|].
      exact (W5 c dc H Hq0 Hl0 (Hnr8 c Hnr0 Hcn) Hset t Ht (Hnr8 t Hnt Htn)).
  - rewrite Hsh. intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hset. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]]; [congruence|].
    refine (value_ext s8 s' c dc _ _ (W6 c dc H Hkc Hpc Hl0 (Hnr8 c Hnr0 Hcn) Hset)); [by rewrite !read_prefix_view, Hv|exact (Hse c)].
  - rewrite Hsh. intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hq0 t Ht. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]]; [congruence|].
    rewrite Hv in Ht by done. destruct (W4 c dc H (or_introl (conj Hpc Hkc)) t Ht) as [_ Hpt].
    apply Hqg; [done|]. exact (W7 c dc H Hkc Hpc Hl0 (Hnr8 c Hnr0 Hcn) Hq0 t Ht).
  - rewrite Hsh, Hpa. intros c Hc. destruct (W8 c Hc) as [A B]. split; [done|]. intros Ha. apply B. intros m dm Hdm Hkm.
    rewrite <- Hsh. destruct (decide (m = n)) as [->|Hm]; [rewrite Hd8 in Hdm; injection Hdm as <-; congruence|].
    refine (Ha m dm _ Hkm). by rewrite Hne1.
  - intros m dm Hm Hdm Hq. rewrite Hrun in Hm.
    assert (Hmn : m <> n) by (intros ->; done). rewrite Hne1 in Hdm by done.
    destruct (Bk8 m dm Hdm) as (d0&Hd0&Hs0).
    refine (Hnp0 m d0 Hm Hd0 _). refine (qc_shape_back s0 s' m d0 dm _ Hs0 Hq). rewrite Hsh. exact Sh8.
  - split_and!; [|done|done].
    intros c Hc. rewrite Hpa in Hc. destruct (Hpd0 c (Pa8 c Hc)) as [x Hx]. destruct (Fw8 c x Hx) as [y Hy].
    destruct (decide (c = n)) as [->|Hcn]; [rewrite Ht1; by eexists|]. rewrite Hne1 by done. rewrite Hy. by eexists.
Qed.

Lemma shapes_node_back s s' (m : node_id) d' :
  shapes s' = shapes s -> g_nodes s' !! m = Some d' -> exists d, g_nodes s !! m = Some d /\ shape_of d' = shape_of d.
Proof.
  intros H Hd'. pose proof (shapes_lookup s' m d' Hd') as X. rewrite H in X. unfold shapes in X. rewrite lookup_fmap in X.
  destruct (g_nodes s !! m) as [d|] eqn:E; [|done]. simpl in X. exists d. split; [done|]. congruence.
Qed.

(** A flush keeps the shapes, and every quiet or disposed pure node answers as it did. *)
Definition keepq (s s' : state) : Prop :=
  shapes s' = shapes s /\ forall t e, g_nodes s !! t = Some e -> pure_in (shapes s) t -> quiet e \/ n_disposed e = true ->
    exists e', g_nodes s' !! t = Some e' /\ read_view_of e' = read_view_of e /\ (quiet e' \/ n_disposed e' = true).

Lemma keepq_refl s : keepq s s.
Proof. split; [done|]. intros t e He _ Hq. exists e. done. Qed.

Lemma keepq_trans s1 s2 s3 : keepq s1 s2 -> keepq s2 s3 -> keepq s1 s3.
Proof.
  intros [S1 H1] [S2 H2]. split; [congruence|]. intros t e He Hp Hq. destruct (H1 t e He Hp Hq) as (e2&He2&V2&Q2).
  destruct (H2 t e2 He2 ltac:(by rewrite S1) Q2) as (e3&He3&V3&Q3). exists e3. split_and!; [done|congruence|done].
Qed.

Lemma keepq_node s s' n d :
  g_nodes s !! n = Some d -> n_kind d = KEffect -> shapes s' = shapes s -> (forall m : node_id, m <> n -> g_nodes s' !! m = g_nodes s !! m) -> keepq s s'.
Proof.
  intros Hd Hk Sh H. split; [done|]. intros t e He Hp Hq. exists e.
  assert (Htn : t <> n) by (intros ->; rewrite Hd in He; injection He as <-; exact (pure_kind s n d Hp Hd Hk)).
  split_and!; [rewrite H by done; exact He|done|done].
Qed.

Lemma keepq_atom (T : node_id -> Prop) lim x s s' :
  atom T lim x s s' -> (forall t, pure_in (shapes s) t -> ~ T t) -> keepq s s'.
Proof.
  intros A HT. split; [exact (at_shapes _ _ _ _ _ A)|]. intros t e He Hp Hq.
  destruct (at_keep _ _ _ _ _ A t e He Hp (HT t Hp) Hq) as (e'&He'&V&Q&_). exists e'. done.
Qed.

Lemma good_shapes s s' : shapes s' = shapes s -> good s -> good s'.
Proof.
  intros Sh [Ha He]. split.
  - intros n d' Hd' Hk. rewrite Sh. destruct (shapes_node_back s s' n d' Sh Hd') as (d&Hd&Hs). refine (Ha n d Hd _).
    unfold shape_of in Hs. injection Hs as X _ _ _. congruence.
  - intros c d' Hd' Hk. destruct (shapes_node_back s s' c d' Sh Hd') as (d&Hd&Hs).
    assert (Hk0 : n_kind d = KEffect) by (unfold shape_of in Hs; injection Hs as X _ _ _; congruence).
    exact (isq_shape_fwd s s' c d d' Sh Hs (He c d Hd Hk0)).
Qed.

Lemma effect_tail_wk g n d s0 s' a :
  run_core s0 -> wk s0 -> effect_ctx n s0 -> g_nodes s0 !! n = Some d -> quiet_effect s0 n d -> n_disposed d = false ->
  n_freshness d <> Check ->
  (let* _ := modify_node n (fun x => node_with_freshness Clean (node_with_queued false x)) in
   if decide (n_freshness d = Dirty) then run_effect g n else m_return tt) s0 = Some (s', a) ->
  wk s' /\ keepq s0 s'.
Proof.
  intros Hc0 Hw Hctx Hd Hq Hl Hnc Hm.
  rewrite (bind_modify_node _ _ _ _ _ Hd) in Hm.
  set (u := fun x => node_with_freshness Clean (node_with_queued false x)) in Hm.
  pose proof Hc0 as [Hi [Hb Ht]]. pose proof Hw as [Hw0 [Hnp Hpd]].
  assert (Hk : n_kind d = KEffect) by apply Hq.
  destruct Hctx as [Hkd [Hcur [Hfl Hmemo]]].
  assert (Hnr : n ∉ g_running s0).
  { intros Hin. pose proof (Hmemo n Hin) as Hkm. by pose proof (has_kind_unique n _ _ s0 Hkd Hkm). }
  destruct (decide (n_freshness d = Dirty)) as [Hdirty|Hne].
  2: { unfold m_return in Hm. injection Hm as <- _.
       assert (Hclean : n_freshness d = Clean) by (destruct (n_freshness d); done).
       split.
       - refine (wframe_apply _ _ Hb (wframe_set s0 n d (u d) Hd eq_refl _) Hw).
         intros _. split_and!; try done.
       - refine (keepq_node _ _ n d Hd Hk (shapes_set s0 n d (u d) Hd eq_refl) _). intros m Hm9. simpl. by apply lookup_insert_ne. }
  destruct g as [|g]; [done|]. cbn [run_effect] in Hm.
  assert (Hctx0 : effect_ctx n s0) by (split; [done|split_and!; done]).
  assert (Hb1 : run_base (set_node n (u d) s0)).
  { apply (base_set n u d); try done.
    - simpl. intros H1 H2. by eapply (b_disposed _ Hb).
    - simpl. intros Hk0. exfalso. congruence.
    - simpl. intros Hc. split_and!.
      + by eapply (b_cleanup_running _ Hb).
      + by eapply (me_cleanup _ (b_env _ Hb)).
      + intros H1. by eapply (b_cleanup_disposed _ Hb).
      + intros Hcur1. congruence. }
  assert (Ht1 : told_on (fun c _ => c <> n) (set_node n (u d) s0)).
  { pose proof (told_others _ n u d s0 Ht Hd ltac:(simpl; done)) as H.
    intros b c y dc e Hr. apply (H b c y dc e). by split. }
  assert (Hok : ok_under (has_kind n KEffect) (modify_node n u)).
  { apply ok_modify_effect; intros ?; simpl; try done; lia. }
  assert (Hmod : modify_node n u s0 = Some (set_node n (u d) s0, tt)).
  { unfold modify_node. by rewrite Hd. }
  destruct (Hok _ _ _ Hi Hkd Hmod) as [Hi1 _].
  set (s3 := set_node n (u d) s0) in *.
  assert (Hk3 : has_kind n KEffect s3) by (exists (u d); split; [simpl; apply lookup_insert_eq|done]).
  unfold push_running, edges in Hm. rewrite !bind_m_modify in Hm. cbv beta in Hm.
  destruct (extras_effect_begin n s3 Hb1 Ht1 Hk3 ltac:(done) Hfl) as [Hx4 _].
  set (s4 := ghost_effect_start n _) in Hx4, Hm.
  assert (Hi4 : check_invariant s4).
  { refine (invariant_reads_only _ _ _ _ _ _ (invariant_effect_begin n (n :: g_running s3) s3 Hi1 Hk3 _ _ _)).
    - intros c Hc. by right.
    - left.
    - intros c Hc. by apply elem_of_cons in Hc. }
  assert (Hd4 : g_nodes s4 !! n = Some (u d)) by (simpl; apply lookup_insert_eq).
  rewrite (bind_get_node _ _ _ _ Hd4) in Hm.
  destruct (w_effect _ Hw0 n d Hd Hq) as [Hsc Hflag].
  assert (Hsc4 : n_scope (u d) = None) by exact Hsc.
  rewrite Hsc4 in Hm. rewrite bind_m_return in Hm.
  rewrite (bind_get_node _ _ _ _ Hd4) in Hm.
  assert (Hl4 : n_disposed (u d) = false) by exact Hl. rewrite Hl4 in Hm.
  apply bind_inv in Hm as (s9 & [] & Hbody & Hpop).
  destruct g as [|g]; [done|]. cbn [run_effect_body] in Hbody.
  rewrite (bind_get_node _ _ _ _ Hd4), (bind_modify_node _ _ _ _ _ Hd4) in Hbody.
  set (u5 := fun x => node_with_runs (S (n_runs x)) (node_with_failure None (node_with_status StNone x))) in Hbody.
  cbv beta in Hbody. unfold emit in Hbody. rewrite bind_m_modify in Hbody. cbv beta in Hbody.
  set (s6 := with_trace _ _) in Hbody.
  (* the core at the body's start *)
  assert (Hcl4 : n_in_cleanup (u d) = false).
  { destruct (n_in_cleanup d) eqn:E; [|done]. exfalso. apply Hnr. eapply (b_cleanup_running _ Hb); [exact Hd|done]. }
  assert (Hb4 := proj1 Hx4). assert (Ht4 := proj2 Hx4).
  assert (Hnw : gh_window (g_ghost s4) !! n = None).
  { destruct (gh_window (g_ghost s4) !! n) as [r|] eqn:E; [|done]. exfalso.
    destruct (b_window _ Hb4) as (W&_). destruct (W n r E) as (e&He&Hek). rewrite Hd4 in He. injection He as <-. simpl in Hek. congruence. }
  assert (Hru4 : n ∈ g_running s4) by (simpl; left).
  assert (Hx5 : run_extras (set_node n (u5 (u d)) s4)).
  { split.
    - assert (Hcl5 : n_in_cleanup d = false) by exact Hcl4.
      apply (base_set n u5 (u d) s4 Hb4 Hd4); simpl; intros; try done; congruence.
    - apply (told_set _ n u5 (u d) s4 Ht4 Hd4); simpl; try done. by right. }
  assert (Hok5 : ok_under (has_kind n KEffect) (modify_node n u5)).
  { apply ok_modify_effect; intros ?; simpl; try done; lia. }
  assert (Hmod5 : modify_node n u5 s4 = Some (set_node n (u5 (u d)) s4, tt)).
  { unfold modify_node. by rewrite Hd4. }
  destruct (Hok5 _ _ _ Hi4 (ex_intro _ (u d) (conj Hd4 Hk)) Hmod5) as [Hi5 _].
  assert (Hfs6 : fields_same (set_node n (u5 (u d)) s4) s6) by (split; reflexivity).
  assert (Hi6 : check_invariant s6) by (eapply invariant_irrelevant; [|exact Hi5]; unfold irrelevant; simpl; split_and!; reflexivity).
  assert (Hx6 := extras_same _ _ Hfs6 Hx5).
  apply bind_inv in Hbody as (s7 & r & Eh & Hm3).
  destruct (hosted_parts _ _ _ _ _ Eh) as (s8 & Ex & Es7).
  set (sI := with_current_owner None (with_current (Some n) s6)) in Ex.
  assert (Hd6 : g_nodes s6 !! n = Some (u5 (u d))) by (simpl; apply lookup_insert_eq).
  assert (HcI : run_core sI).
  { split.
    - eapply invariant_irrelevant; [|apply invariant_with_current; [|exact Hi6]].
      + unfold irrelevant. simpl. split_and!; reflexivity.
      + intros c [=<-]. simpl. left.
    - eapply extras_same; [|apply extras_current; [exact Hx6|]].
      + split; reflexivity.
      + intros c [=<-]. split; [simpl; left|]. intros e He. rewrite Hd6 in He. injection He as <-. exact Hcl4. }
  assert (HnI : g_nodes sI = <[n := u5 (u d)]> (g_nodes s0)).
  { change (<[n := u5 (u d)]> (<[n := u d]> (g_nodes s0)) = <[n := u5 (u d)]> (g_nodes s0)). apply insert_insert_eq. }
  assert (HvI : forall c, slot_view (g_edges sI) c = if decide (c = n) then ((slot_view (g_edges s0) c).1, 0) else slot_view (g_edges s0) c).
  { intros c. exact (slot_view_begin n (g_edges s0) c). }
  assert (HpaI : forall c, c ∈ gh_passed (g_ghost sI) -> c ∈ gh_passed (g_ghost s0)).
  { intros c Hc. change (c ∈ gh_passed (g_ghost s0) ∖ {[n]}) in Hc. apply elem_of_difference in Hc. tauto. }
  set (L := S (g_next_id sI)).
  assert (Hids : forall m, is_Some (g_nodes sI !! m) -> m < L).
  { intros m Hm5. pose proof (core_ids sI HcI m Hm5). unfold L. lia. }
  destruct (effect_begin s0 sI n d (u5 (u d)) L Hw Hb Hnr Hd Hq Hl HnI eq_refl eq_refl eq_refl Hl
              ltac:(intros c Hne; rewrite HvI; by rewrite decide_False) ltac:(rewrite HvI; by rewrite decide_True)
              ltac:(done) HpaI ltac:(done) ltac:(done)) as (WI&StI&QI).
  assert (HshI : shapes sI = shapes s0).
  { unfold shapes. rewrite HnI, fmap_insert. assert (X : shape_of (u5 (u d)) = shape_of d) by done. rewrite X. apply insert_id. by rewrite lookup_fmap, Hd. }
  assert (HdI : g_nodes sI !! n = Some (u5 (u d))) by exact Hd6.
  assert (HqeI : quiet_effect sI n (u5 (u d))) by exact (isq_shape_fwd _ _ _ _ _ HshI eq_refl Hq).
  assert (Hbp8 : body_pure (fun t => readable (shapes sI) t -> t < L /\ pure_in (shapes sI) t) (n_env (u d)) (n_body (u d))).
  { destruct HqeI as [_ Hb8]. revert Hb8. apply body_pure_mono. intros t Ht9 Hr9. split; [exact (Hids t (readable_node _ _ Hr9))|exact (Ht9 Hr9)]. }
  destruct (Aexec _ (A_ok_all g) (fun x => x = n) L n (u5 (u d)) (n_env (u d)) (n_env (u d)) (n_body (u d)) sI s8 r (fun _ => None) (Ok 0%Z)
              (conj HcI (conj WI StI)) eq_refl HdI QI (env_agree_refl _) (or_intror (conj Hk Hids)) Hbp8
              ltac:(intros K; exfalso; apply K; reflexivity) (or_introl eq_refl) Ex) as ((Hc8&Hw8&St8)&A8&Hev8&_&_&(l8&Hlog8&Hview8)).
  assert (Hrnn : ~ rn sI L None n).
  { intros [[K _]|K]; [|done]. exact (pure_kind sI n _ K HdI Hk). }
  destruct (at_rise _ _ _ _ _ A8 n (u5 (u d)) Hrnn HdI) as (d8&Hd8&R8).
  assert (Hk8 : n_kind d8 = KEffect) by (rewrite (rise_kind _ _ R8); exact Hk).
  assert (Hq8 : qc s8 n d8).
  { right. refine (isq_shape_fwd _ _ _ _ _ _ _ HqeI); [exact (at_shapes _ _ _ _ _ A8)|apply R8]. }
  assert (Hl8 : n_disposed d8 = false) by (pose proof R8 as (_&_&X&_); rewrite X; exact Hl).
  assert (Hd87 : g_nodes s7 !! n = Some d8) by (subst s7; exact Hd8).
  rewrite (bind_get_node _ _ _ _ Hd87), Hl8 in Hm3. cbv iota in Hm3.
  apply bind_edges_opt in Hm3 as (w & Hwe & Hm4). cbv iota in Hwe.
  assert (Hwe8 : sources_end_run n (g_edges s8) = Some w) by (subst s7; exact Hwe).
  pose proof (proj1 (proj2 Hc8)) as Hb8.
  pose proof (fun c => slot_view_end n _ w c Hwe8 (b_cursor _ Hb8 n)) as Hve.
  assert (Sh8 : shapes s8 = shapes s0) by (rewrite (at_shapes _ _ _ _ _ A8); exact HshI).
  assert (Run8 : g_running s8 = n :: g_running s0) by (rewrite (at_running _ _ _ _ _ A8); done).
  assert (Hfin : exists dF, g_nodes s9 = <[n := dF]> (g_nodes s8) /\ shape_of dF = shape_of d8 /\ n_scope dF = n_scope d8 /\ n_unannounced dF = n_unannounced d8 /\
            g_edges s9 = w /\ g_ghost s9 = g_ghost s8 /\ g_running s9 = g_running s8 /\ g_owners s9 = g_owners s8 /\
            forall c, trace_view c (g_trace s9) = trace_view c (g_trace s8)).
  { destruct r as [v|y].
    - unfold m_return in Hm4. injection Hm4 as <-. subst s7. exists d8. split_and!; try done. symmetry. simpl. apply insert_id. exact Hd8.
    - assert (Hd9 : g_nodes (with_edges w s7) !! n = Some d8) by exact Hd87. rewrite (bind_modify_node _ _ _ _ _ Hd9) in Hm4. unfold emit, m_modify in Hm4. injection Hm4 as <-. subst s7.
      eexists. split_and!; try reflexivity. intros c. simpl. apply trace_view_free. reflexivity. }
  destruct Hfin as (dF&HsnF&ShF&ScF&FlF&EF&GF&RF&OF&VF).
  unfold pop_running, m_modify in Hpop. injection Hpop as <- _.
  set (sE := with_running (tail (g_running s9)) s9).
  assert (ShE : shapes sE = shapes s0).
  { change (shapes s9 = shapes s0). unfold shapes. rewrite HsnF, fmap_insert, ShF. rewrite insert_id; [|by rewrite lookup_fmap, Hd8]. exact Sh8. }
  assert (HnE : forall m : node_id, m <> n -> g_nodes sE !! m = g_nodes s8 !! m).
  { intros m Hm5. change (g_nodes s9 !! m = g_nodes s8 !! m). rewrite HsnF. by apply lookup_insert_ne. }
  assert (HtE : g_nodes sE !! n = Some dF).
  { change (g_nodes s9 !! n = Some dF). rewrite HsnF. apply lookup_insert_eq. }
  assert (VE : forall c, trace_view c (g_trace sE) = trace_view c (g_trace s8)) by exact VF.
  assert (SeE : forall c y, seen_of (g_ghost sE) c y = seen_of (g_ghost s8) c y) by (intros c y; unfold seen_of; change (g_ghost sE) with (g_ghost s9); by rewrite GF).
  assert (OE : g_owners sE = g_owners s0) by (change (g_owners s9 = g_owners s0); rewrite OF, (at_owners _ _ _ _ _ A8); reflexivity).
  assert (PrE : forall c, read_prefix (g_edges sE) c = if decide (c = n) then read_prefix (g_edges s8) c else read_prefix (g_edges s8) c).
  { intros c. change (g_edges sE) with (g_edges s9). rewrite EF, !read_prefix_view, (Hve c). destruct (decide (c = n)) as [->|Hne]; [|reflexivity].
    simpl. rewrite take_take, Nat.min_id. reflexivity. }
  assert (HtI : g_trace sI = g_trace s0 ++ [EvRun n]) by reflexivity.
  assert (HranE : ran Xe sE).
  { intros c d' Hd' Hqq Hl1 Hf. destruct (decide (c = n)) as [->|Hne].
    - rewrite HtE in Hd'. injection Hd' as <-. right.
      assert (Hm5 : forall t o, seen_ans s8 n t = Some o -> seen_ans sE n t = Some o).
      { intros t o H5. refine (eq_trans (seen_ans_ext s8 sE n _ _ t) H5).
        - rewrite (PrE n), decide_True by done. reflexivity.
        - intros y. apply SeE. }
      assert (Hsf : shape_of dF = shape_of (u5 (u d))) by (rewrite ShF; apply R8).
      unfold shape_of in Hsf. injection Hsf as _ _ E5 B5.
      exists r, l8, (trace_view n (g_trace s0)). rewrite E5, B5. split_and!.
      + exact (body_eval_mono _ _ _ _ _ Hm5 Hev8).
      + exact (body_logs_mono _ _ _ _ _ Hm5 Hlog8).
      + rewrite VE, Hview8, HtI, trace_view_app. unfold trace_view at 2. rewrite filter_cons_True, filter_nil; [|simpl; by apply bool_decide_eq_true_2].
        rewrite <- app_assoc. reflexivity.
    - destruct (shapes_node_back s0 sE c d' ShE Hd') as (x&Hx&Hsx).
      pose proof (isq_shape _ _ _ _ _ ShE Hsx Hqq) as Hq0.
      assert (Hxp : ~ pure_in (shapes s0) c) by exact (effect_not_pure s0 c x Hx (proj1 Hq0)).
      assert (HxI : g_nodes sI !! c = Some x) by (rewrite HnI; rewrite lookup_insert_ne by done; exact Hx).
      assert (Hrn0 : forall o, o = None \/ o = Some n -> ~ rn sI L o c).
      { intros o Ho [[K _]|K]; [apply Hxp; by rewrite <- HshI|destruct Ho as [ -> | -> ]; [done|injection K as K; done]]. }
      destruct (at_rise _ _ _ _ _ A8 c x (Hrn0 None (or_introl eq_refl)) HxI) as (x8&Hx8&Rx).
      rewrite HnE in Hd' by done. rewrite Hd' in Hx8. injection Hx8 as <-.
      pose proof Rx as (_&Vv&Di&_&_&_&_&Fl&_).
      assert (Hf0 : n_freshness x <> Dirty).
      { intros K. rewrite K in Fl. unfold freshness_le in Fl. destruct (n_freshness d') eqn:E9; simpl in Fl; try lia. done. }
      unfold shape_of in Hsx. injection Hsx as _ _ E5 B5.
      destruct (at_slots _ _ _ _ _ A8 c (Hrn0 (Some n) (or_intror eq_refl))) as [Sl8 Se8].
      assert (Hsl : read_prefix (g_edges sE) c = read_prefix (g_edges s0) c).
      { rewrite PrE, decide_False by done. rewrite !read_prefix_view, Sl8, HvI, decide_False by done. reflexivity. }
      assert (Hse0 : forall y, seen_of (g_ghost sE) c y = seen_of (g_ghost s0) c y).
      { intros y. rewrite SeE, Se8. reflexivity. }
      assert (Hvw : trace_view c (g_trace sE) = trace_view c (g_trace s0)).
      { rewrite VE, (at_view _ _ _ _ _ A8 c (Hrn0 (Some n) (or_intror eq_refl))), HtI. apply trace_view_other. congruence. }
      destruct (proj1 (proj2 (proj2 (proj2 Hw))) c x Hx Hq0 ltac:(congruence) Hf0) as [[K1 K2]|K1].
      + left. split; [done|]. unfold window, read_view_of in *. injection Vv as _ S1 _. congruence.
      + right. exact (ran_ok_ext s0 sE c x d' Hsl Hse0 E5 B5 Hvw K1). }
  assert (HhoE : hosts sE).
  { apply (hosts_frame s0); [| |intros o y Hy; rewrite OE in Hy; left; exists y; done|exact (proj2 (proj2 (proj2 (proj2 Hw))))].
    - intros m d' Hd'. destruct (shapes_node_back s0 sE m d' ShE Hd') as (x&Hx&Hsx). exists x. split; [done|exact (isq_shape _ _ _ _ _ ShE Hsx)].
    - intros m [x Hx]. destruct (shapes_node_back sE s0 m x (eq_sym ShE) Hx) as (y&Hy&_). by eexists. }
  assert (HkE : keepq s0 sE).
  { split; [exact ShE|]. intros t e He9 Hp9 Hq9.
    assert (Htn : t <> n) by (intros ->; rewrite Hd in He9; injection He9 as <-; exact (pure_kind s0 n d Hp9 Hd Hk)).
    assert (HeI : g_nodes sI !! t = Some e) by (rewrite HnI, lookup_insert_ne by done; exact He9).
    assert (HpI : pure_in (shapes sI) t) by (rewrite HshI; exact Hp9).
    destruct (at_keep _ _ _ _ _ A8 t e HeI HpI ltac:(done) Hq9) as (e'&He'&V9&Q9&_). exists e'. split_and!; [|done|done]. rewrite HnE by done. exact He'. }
  split; [|exact HkE].
  refine (effect_finish s0 s8 sE n d8 dF L Hw Hw8 St8 Hd8 Hq8 Hk8 Sh8
            (fun c Hc => HpaI c (at_passed _ _ _ _ _ A8 c Hc))
            (fun m dm' Hm => shapes_node_back s0 s8 m dm' Sh8 Hm) _ Run8 Hnr HsnF ShF ScF FlF _ _ _ _ _ HranE HhoE).
  - intros m dm Hdm. destruct (shapes_node_back s8 s0 m dm (eq_sym Sh8) Hdm) as (x&Hx&_). exact (mk_is_Some _ _ Hx).
  - intros c Hne. simpl. rewrite EF, Hve. by rewrite decide_False.
  - assert (Hx : (slot_view w n).1 = read_prefix (g_edges s8) n).
    { rewrite (Hve n), decide_True by done. rewrite read_prefix_view. done. }
    change (g_edges sE) with (g_edges s9). rewrite EF. exact Hx.
  - intros c y. simpl. unfold seen_of. rewrite GF. done.
  - simpl. rewrite GF. done.
  - simpl. rewrite RF, Run8. done.
Qed.

(** ** The induction over the engine *)

End WX1.

Definition wfx (X : node_id -> Prop) {A} (P : state -> Prop) (m : M A) : Prop :=
  forall s s' a, run_core s -> wkx X s -> P s -> m s = Some (s', a) -> good s' -> wkx X s'.

Definition live_memo (n : node_id) (s : state) : Prop :=
  exists d, g_nodes s !! n = Some d /\ n_kind d = KMemo /\ n_disposed d = false.

Record W_ok (X : node_id -> Prop) (f : nat) : Prop := {
  Wexec : forall e c, wfx X (fun _ => True) (exec f e c);
  Wread : forall n b, wfx X (fun _ => True) (read f n b);
  Wnode : forall n, wfx X (fun _ => True) (read_node f n);
  Wrop : forall n, wfx X (live_memo n) (refresh_or_pull f n);
  Wpull : forall n, wfx X (live_memo n) (pull f n);
  Wexe : forall n, wfx X (effect_ctx n) (execute_effect f n);
  Wret : forall o, wfx X (fun _ => True) (retire f o);
  Wdsc : forall o, wfx X (fun _ => True) (dispose_scope f o);
  Wtear : forall o ls cs, wfx X (fun _ => True) (teardown f o ls cs);
  Wcl : forall o b e, wfx X (fun _ => True) (run_cleanup f o b e);
  Wrel : forall n, wfx X (fun _ => True) (release f n);
  Wdisp : forall n, wfx X (fun _ => True) (dispose_node f n);
  Watt : forall n o, wfx X (fun _ => True) (attach f n o);
  Wwrite : forall n v, wfx X (fun _ => True) (write_signal f n v);
  Wreq : wfx X (fun _ => True) (request_flush f);
  Wset : wfx X (fun _ => True) (settle_flush f);
  Wflush : wfx X (fun s => g_current s = None) (run_flush f);
  Wdrain : wfx X flush_ctx (drain f)
}.

Arguments Wexec {X} f _. Arguments Wread {X} f _. Arguments Wnode {X} f _. Arguments Wrop {X} f _. Arguments Wpull {X} f _.
Arguments Wexe {X} f _. Arguments Wret {X} f _. Arguments Wdsc {X} f _. Arguments Wtear {X} f _. Arguments Wcl {X} f _.
Arguments Wrel {X} f _. Arguments Wdisp {X} f _. Arguments Watt {X} f _. Arguments Wwrite {X} f _. Arguments Wreq {X} f _.
Arguments Wset {X} f _. Arguments Wflush {X} f _. Arguments Wdrain {X} f _.

Lemma W_ok_0 X : W_ok X 0.
Proof. split; intros; intros s s' a _ _ _ E _; done. Qed.

Section WX2.
Context {Xe : node_id -> Prop}.
Local Notation wk := (wkx Xe).
Local Notation wok := (wokx Xe).
Local Notation wf := (wfx Xe).

Ltac wmod := apply wok_modify; intros ?; [unfold irrelevant; simpl; split_and!; reflexivity|split; reflexivity|split_and!; reflexivity|reflexivity].

Lemma wk_cur_none s s0 :
  run_core s -> irrelevant (with_current None s) s0 -> fields_same (with_current None s) s0 -> run_core s0.
Proof.
  intros [Hi Hx] He1 He2. split.
  - eapply invariant_irrelevant; [exact He1|]. apply invariant_with_current; [|exact Hi]. done.
  - eapply extras_same; [exact He2|]. apply extras_current; [exact Hx|done].
Qed.

Lemma wok_detached o (body : M unit) : wok (fun _ => True) body -> wok (fun _ => True) (detached o body).
Proof.
  intros [O [K W]]. split; [by apply ok2_detached|split; [by apply ko_detached|]].
  intros s s' a Hc Hw _ E Hg. unfold detached, m_bind, m_modify in E. simpl in E.
  set (s0 := with_batch_depth _ _) in E.
  destruct (body s0) as [[s1 []]|] eqn:Eb; [|done]. injection E as <- _.
  assert (Hc0 : run_core s0).
  { apply (wk_cur_none s s0 Hc); [unfold irrelevant; simpl; split_and!; reflexivity|split; reflexivity]. }
  assert (Hw0 : wk s0) by (apply (wk_same s); [split_and!; done|exact Hw]).
  assert (Hg1 : good s1).
  { destruct (K _ _ _ (core_ids _ Hc0) Eb) as [Hi1 K1]. revert Hg. apply good_back; [done|].
    split; [intros n d Hd; exists d; done|intros n d' Hd'; left; by eexists|done]. }
  apply (wk_same s1); [split_and!; done|]. exact (W _ _ _ Hc0 Hw0 I Eb Hg1).
Qed.

Lemma wok_untracked {A} (body : M A) : wok (fun _ => True) body -> wok (fun _ => True) (untracked body).
Proof.
  intros [O [K W]]. split; [by apply ok2_untracked|split; [by apply ko_untracked|]].
  intros s s' a Hc Hw _ E Hg. unfold untracked, m_bind, m_modify in E. simpl in E.
  set (s0 := with_untracked _ _) in E.
  destruct (body s0) as [[s1 r]|] eqn:Eb; [|done]. injection E as <- _.
  assert (Hc0 : run_core s0).
  { apply (wk_cur_none s s0 Hc); [unfold irrelevant; simpl; split_and!; reflexivity|split; reflexivity]. }
  assert (Hw0 : wk s0) by (apply (wk_same s); [split_and!; done|exact Hw]).
  assert (Hg1 : good s1).
  { destruct (K _ _ _ (core_ids _ Hc0) Eb) as [Hi1 K1]. revert Hg. apply good_back; [done|].
    split; [intros n d Hd; exists d; done|intros n d' Hd'; left; by eexists|done]. }
  apply (wk_same s1); [split_and!; done|]. exact (W _ _ _ Hc0 Hw0 I Eb Hg1).
Qed.

Ltac ms' :=
  unfold memo_step; split_and!; try done; intros; simpl in *;
  first [lia | done | congruence | by right | by left].

Ltac ns' :=
  unfold node_step; split_and!; try done;
  try (intros; simpl in *; first [lia | done | congruence]);
  try (intros _; ms');
  try (intros; simpl in *; split_and!; done).

Ltac wlink :=
  apply wok_link; [intros ?; ns'|intros ?; unfold neutral; simpl; split_and!; first [reflexivity|done]|intros ?; split_and!; done].


Ltac wlists :=
  eapply wok_put_owner; [|reflexivity]; repeat match goal with H : lists_ok _ |- _ => destruct H end;
  unfold lists_ok; cbn [o_cleanups o_cleanup_envs o_cleanup_order owner_with_cleanup owner_detached owner_retired owner_with_error owner_without owner_with_child];
  rewrite ?length_app; cbn [length]; split; lia.

Ltac wrih IH :=
  first
    [ exact (conj (Rexec _ (runs_ok_all _) _ _) (conj (Kexec _ (K_ok_all _) _ _) (Wexec _ IH _ _)))
    | exact (conj (Rread _ (runs_ok_all _) _ _) (conj (Kread _ (K_ok_all _) _ _) (Wread _ IH _ _)))
    | exact (conj (Rread_node _ (runs_ok_all _) _) (conj (Knode _ (K_ok_all _) _) (Wnode _ IH _)))
    | exact (conj (Rcleanup _ (runs_ok_all _) _ _ _) (conj (Kcl _ (K_ok_all _) _ _ _) (Wcl _ IH _ _ _)))
    | exact (conj (Rrelease _ (runs_ok_all _) _) (conj (Krel _ (K_ok_all _) _) (Wrel _ IH _)))
    | exact (conj (Rdispose _ (runs_ok_all _) _) (conj (Kdisp _ (K_ok_all _) _) (Wdisp _ IH _)))
    | exact (conj (Rattach _ (runs_ok_all _) _ _) (conj (Katt _ (K_ok_all _) _ _) (Watt _ IH _ _)))
    | exact (conj (Rwrite _ (runs_ok_all _) _ _) (conj (Kwrite _ (K_ok_all _) _ _) (Wwrite _ IH _ _)))
    | exact (conj (Rrequest _ (runs_ok_all _)) (conj (Kreq _ (K_ok_all _)) (Wreq _ IH)))
    | exact (conj (Rsettle _ (runs_ok_all _)) (conj (Kset _ (K_ok_all _)) (Wset _ IH)))
    | exact (conj (Rretire _ (runs_ok_all _) _) (conj (Kret _ (K_ok_all _) _) (Wret _ IH _)))
    | exact (conj (Rdispose_scope _ (runs_ok_all _) _) (conj (Kdsc _ (K_ok_all _) _) (Wdsc _ IH _)))
    | exact (conj (Rteardown _ (runs_ok_all _) _ _ _) (conj (Ktear _ (K_ok_all _) _ _ _) (Wtear _ IH _ _ _))) ].

Ltac wgo1 IH :=
  first
    [ apply wok_return | apply wok_stuck | apply wok_get | apply wok_get_node | (apply wok_emit; intros ?; reflexivity) | apply wok_note
    | (apply wok_log; try apply _) | apply wok_log_label | apply wok_current_owner
    | apply wok_on_node; intros ? | apply wok_on_value; intros ?
    | apply wok_detached
    | wlists
    | wlink
    | wmod
    | wrih IH
    | apply wok_any; wrih IH
    | apply wok_get_owner_at; [intros ?; match type of IH with W_ok _ ?ff => repeat kgo1 (K_ok_all ff) end|intros ? ?]
    | apply wok_bind; [apply _| |intros ?]
    | apply wok_bind_left; [apply _| |intros ?]
    | match goal with |- wok _ (match ?x with _ => _ end) => destruct x end
    | match goal with |- wok _ (if ?x then _ else _) => destruct x end ].

Ltac wgo IH := repeat wgo1 IH.

Lemma wok_get_at {B} P (k : state -> M B) :
  (forall s0, wok P (k s0)) -> wok P (m_bind m_get k).
Proof.
  intros Hk. split; [|split].
  - intros s s' a Hc HP E. rewrite bind_get in E. destruct (Hk s) as [O _]. exact (O _ _ _ Hc HP E).
  - apply ko_bind; [apply ko_get|intros s0; apply Hk].
  - intros s s' a Hc Hw HP E. rewrite bind_get in E. destruct (Hk s) as [_ [_ W]]. exact (W _ _ _ Hc Hw HP E).
Qed.

Lemma rs_kind_of_core n s s1 d :
  run_kept s s1 -> g_nodes s !! n = Some d -> n_kind d = KMemo -> exists d1, g_nodes s1 !! n = Some d1 /\ n_kind d1 = KMemo.
Proof. intros [(_&_&Hk&_) _] Hd Hkd. apply Hk. by exists d. Qed.

Section WStep.
  Variable f : nat.
  Hypothesis IHall : forall X', W_ok X' f.
  Let IH : W_ok Xe f := IHall Xe.

  Lemma Wret_S o : wf (fun _ => True) (retire (S f) o).
  Proof.
    assert (H : wok (fun _ => True) (retire (S f) o)).
    { cbn [retire]. apply wok_bind; [apply _| |intros []].
      - apply wok_detached. apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x Hx].
        destruct (o_disposed x); [apply wok_return|]. apply wok_bind_left; [apply _|wlists|intros _]. wrih IH.
      - apply wok_get_at. intros s0. destruct (live_queue s0); [wrih IH|apply wok_return]. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Wdsc_S o : wf (fun _ => True) (dispose_scope (S f) o).
  Proof.
    assert (H : wok (fun _ => True) (dispose_scope (S f) o)).
    { cbn [dispose_scope]. apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x Hx].
      apply wok_bind_left; [apply _|wlists|intros _]. wrih IH. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Wtear_S o ls cs : wf (fun _ => True) (teardown (S f) o ls cs).
  Proof.
    assert (H : wok (fun _ => True) (teardown (S f) o ls cs)).
    { cbn [teardown]. destruct ls as [|l ls']; destruct cs as [|[[b e] a] cs']; wgo IH. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Wcl_S o b e : wf (fun _ => True) (run_cleanup (S f) o b e).
  Proof.
    assert (H : wok (fun _ => True) (run_cleanup (S f) o b e)).
    { cbn [run_cleanup]. apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x Hx].
      apply wok_bind_left; [apply _|apply wok_emit; intros ?; reflexivity|intros _]. apply wok_bind; [apply _|wrih IH|intros r]. destruct r as [v|y]; [apply wok_return|].
      apply wok_bind; [apply _| |intros _; apply wok_emit; intros ?; reflexivity].
      unfold record_error. apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x0 Hx0]. destruct (o_sink x0) as [p|]; [|wlists].
      apply wok_get_owner_at; [intros ?; apply ko_put_owner|intros y0 Hy0]. wlists. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Wrel_S n : wf (fun _ => True) (release (S f) n).
  Proof.
    assert (H : wok (fun _ => True) (release (S f) n)).
    { cbn [release]. apply wok_bind; [apply _|wlink|intros _]. wrih IH. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Watt_S n o : wf (fun _ => True) (attach (S f) n o).
  Proof.
    assert (H : wok (fun _ => True) (attach (S f) n o)).
    { cbn [attach]. apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x Hx].
      destruct (o_disposed x).
      - apply wok_bind_left; [apply _|apply wok_any; wrih IH|intros _]. wlink.
      - apply wok_bind_left; [apply _|wlists|intros _]. wlink. }
    exact (proj2 (proj2 H)).
  Qed.

  Lemma Wreq_S : wf (fun _ => True) (request_flush (S f)).
  Proof.
    intros s s' a Hc Hw _ E Hg. cbn [request_flush] in E. rewrite bind_get in E.
    destruct (live_queue s && _); [|unfold m_return in E; by injection E as <- _].
    destruct (outside_bodies s) eqn:Eo.
    - exact (Wflush _ IH _ _ _ Hc Hw (outside_current _ Eo) E Hg).
    - destruct (g_flushing s); [unfold m_return in E; by injection E as <- _|].
      unfold m_modify in E. injection E as <- _. apply (wk_same s); [split_and!; done|exact Hw].
  Qed.

  Lemma Wset_S : wf (fun _ => True) (settle_flush (S f)).
  Proof.
    intros s s' a Hc Hw _ E Hg. cbn [settle_flush] in E. rewrite bind_get in E.
    destruct (g_flush_owed s && _ && outside_bodies s) eqn:Eo; [|unfold m_return in E; by injection E as <- _].
    apply andb_prop in Eo as [_ Eo]. exact (Wflush _ IH _ _ _ Hc Hw (outside_current _ Eo) E Hg).
  Qed.

  Lemma Wflush_S : wf (fun s => g_current s = None) (run_flush (S f)).
  Proof.
    intros s s' a Hc Hw Hcur E Hg. cbn [run_flush] in E. rewrite bind_get in E.
    destruct (g_flushing s) eqn:Efl; [unfold m_return in E; by injection E as <- _|].
    rewrite bind_m_modify in E. cbv beta in E.
    set (s1 := with_flush_owed false (with_flushing true s)) in E.
    destruct Hc as [Hi [Hb Ht]].
    destruct Hb as [B1 B2 [E1 E2 E3 E4] B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 B16 B17 B18] eqn:Ebase.
    assert (Hb1 : run_base s1).
    { refine (Build_run_base s1 B1 B2 (Build_mark_env s1 E1 E2 E3 E4) B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 _ B17 B18).
      intros c _ _. done. }
    assert (Hi1 : check_invariant s1).
    { eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity. }
    assert (Hmemo : forall c, c ∈ g_running s -> has_kind c KMemo s).
    { intros c Hc. destruct (B15 c Hc) as [K|K]; [done|]. pose proof (B16 c K Hc). congruence. }
    assert (Hctx1 : flush_ctx s1) by (split_and!; [exact Hcur|done|exact Hmemo]).
    apply bind_inv in E as (s2 & [] & Hd & E2').
    unfold m_modify in E2'. injection E2' as <- _.
    assert (Hw1 : wk s1) by (apply (wk_same s); [split_and!; done|exact Hw]).
    assert (Hg2 : good s2).
    { destruct (Kdrain _ (K_ok_all f) _ _ _ (core_ids _ (conj Hi1 (conj Hb1 Ht))) Hd) as [Hi2 K2]. revert Hg. apply good_back; [done|].
      split; [intros n d Hd0; exists d; done|intros n d' Hd'; left; by eexists|done]. }
    apply (wk_same s2); [split_and!; done|]. exact (Wdrain _ IH _ _ _ (conj Hi1 (conj Hb1 Ht)) Hw1 Hctx1 Hd Hg2).
  Qed.

  Lemma Wdrain_S : wf flush_ctx (drain (S f)).
  Proof.
    intros s s' a Hc Hw Hctx E Hg. cbn [drain] in E. rewrite bind_get in E.
    destruct (g_queue s !! g_queue_head s) as [n|] eqn:Eq.
    - rewrite bind_m_modify in E. cbv beta in E.
      assert (Hin : n ∈ g_queue s) by (apply list_elem_of_lookup; by exists (g_queue_head s)).
      set (s1 := with_queue_head (S (g_queue_head s)) s) in E.
      destruct Hc as [Hi Hx].
      assert (Hfs : fields_same s s1) by (split; reflexivity).
      assert (Hi1 : check_invariant s1).
      { eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity. }
      assert (Hx1 := extras_same _ _ Hfs Hx).
      assert (Hk : has_kind n KEffect s1) by apply (ci_queue _ _ _ Hi n Hin).
      assert (Hctx1 : effect_ctx n s1) by (split; [done|exact Hctx]).
      apply bind_inv in E as (s2 & [] & Hex & E2').
      destruct (Rexecute _ (runs_ok_all f) n _ _ _ (conj Hi1 Hx1) Hctx1 Hex) as [Hc2 K2].
      pose proof (stable2 _ _ K2 Hctx1) as [_ Hctx2].
      destruct (Kdrain _ (K_ok_all f) _ _ _ (core_ids _ Hc2) E2') as [_ K3].
      assert (Hg2 : good s2) by exact (good_back _ _ (core_ids _ Hc2) K3 Hg).
      assert (Hw1 : wk s1) by (apply (wk_same s); [split_and!; done|exact Hw]).
      exact (Wdrain _ IH _ _ _ Hc2 (Wexe _ IH n _ _ _ (conj Hi1 Hx1) Hw1 Hctx1 Hex Hg2) Hctx2 E2' Hg).
    - unfold m_modify in E. injection E as <- _. apply (wk_same s); [split_and!; done|exact Hw].
  Qed.
End WStep.

Lemma wk_of_atom (T : node_id -> Prop) lim s s' : wk s -> atom T lim None s s' -> whole s' -> wk s'.
Proof.
  intros [_ [Hn [Hp [Hran Hho]]]] A Hw. pose proof (at_shapes _ _ _ _ _ A) as Sh. split; [done|split].
  - intros m dm Hm Hdm Hq. rewrite (at_running _ _ _ _ _ A) in Hm. destruct (at_back _ _ _ _ _ A m dm Hdm) as [d Hd].
    exact (Hn m d Hm Hd (qc_shapes s s' m d dm Sh Hd Hdm Hq)).
  - split_and!.
    + intros c Hc. destruct (Hp c (at_passed _ _ _ _ _ A c Hc)) as [d Hd]. destruct (at_any _ _ _ _ _ A c d Hd) as (d'&Hd'&_). by eexists.
    + apply (ran_frame _ s); [|exact Hran]. intros c d' Hd' Hq Hl Hf.
      destruct (shapes_node_back s s' c d' Sh Hd') as (d&Hd&Hsx). pose proof (isq_shape _ _ _ _ _ Sh Hsx Hq) as Hq0.
      assert (Hrn : ~ rn s lim None c).
      { intros [[K _]|K]; [exact (effect_not_pure s c d Hd (proj1 Hq0) K)|done]. }
      destruct (at_rise _ _ _ _ _ A c d Hrn Hd) as (x&Hx&Rx). rewrite Hd' in Hx. injection Hx as <-.
      pose proof Rx as (_&Vv&Di&_&_&_&_&Fl&_). unfold shape_of in Hsx. injection Hsx as _ _ E5 B5.
      destruct (at_slots _ _ _ _ _ A c Hrn) as [Sl Se].
      exists d. split_and!; [done|done|congruence| | |done|done|by rewrite !read_prefix_view, Sl|done|exact (at_view _ _ _ _ _ A c Hrn)].
      * intros K. rewrite K in Fl. unfold freshness_le in Fl. destruct (n_freshness d') eqn:E9; simpl in Fl; try lia. done.
      * unfold window, read_view_of in *. injection Vv as _ S1 _. congruence.
    + apply (hosts_frame s); [| |intros o y Hy; rewrite (at_owners _ _ _ _ _ A) in Hy; left; exists y; done|exact Hho].
      * intros m d' Hd'. destruct (shapes_node_back s s' m d' Sh Hd') as (x&Hx&Hsx). exists x. split; [done|exact (isq_shape _ _ _ _ _ Sh Hsx)].
      * intros m [x Hx]. destruct (at_any _ _ _ _ _ A m x Hx) as (y&Hy&_). by eexists.
Qed.

(** An effect going [Clean] over reads it has settled. *)
Lemma effect_clean s s' n d dF :
  wk s -> run_base s -> g_nodes s !! n = Some d -> quiet_effect s n d -> settled d -> n_disposed d = false -> n ∉ g_running s ->
  g_nodes s' = <[n := dF]> (g_nodes s) -> shape_of dF = shape_of d -> n_scope dF = n_scope d -> n_unannounced dF = n_unannounced d -> n_disposed dF = false ->
  g_edges s' = g_edges s -> (forall c y, seen_of (g_ghost s') c y = seen_of (g_ghost s) c y) ->
  gh_passed (g_ghost s') = gh_passed (g_ghost s) -> g_running s' = g_running s ->
  g_trace s' = g_trace s -> g_owners s' = g_owners s -> n_status dF = n_status d -> wk s'.
Proof.
  intros [Hw0 [Hnp [Hpd [Hran Hho]]]] Hb Hd Hq Hset0 Hl Hnr Hsn Hshd Hsc Hfl Hl' He' Hse Hpa Hrun Htr Hown Hst.
  assert (Hk : n_kind d = KEffect) by apply Hq.
  assert (Hsh : shapes s' = shapes s).
  { unfold shapes. rewrite Hsn, fmap_insert, Hshd. apply insert_id. by rewrite lookup_fmap, Hd. }
  assert (HkF : n_kind dF = KEffect) by (unfold shape_of in Hshd; injection Hshd as X _ _ _; congruence).
  assert (Hnpn : ~ pure_in (shapes s) n) by exact (effect_not_pure s n d Hd Hk).
  assert (Ht1 : g_nodes s' !! n = Some dF) by (rewrite Hsn; apply lookup_insert_eq).
  assert (Hne1 : forall m : node_id, m <> n -> g_nodes s' !! m = g_nodes s !! m) by (intros m Hm; rewrite Hsn; by apply lookup_insert_ne).
  assert (Hback : forall m d', g_nodes s' !! m = Some d' -> (m = n /\ d' = dF) \/ (m <> n /\ g_nodes s !! m = Some d')).
  { intros m d' Hm. destruct (decide (m = n)) as [->|Hm']; [left|right].
    - rewrite Ht1 in Hm. injection Hm as <-. done.
    - rewrite Hne1 in Hm by done. done. }
  assert (Hrd : forall c y, pure_in (shapes s) y -> rd_ok s c y -> rd_ok s' c y).
  { intros c y Hy (H1&e&He&H2). unfold rd_ok. rewrite Hse. split; [done|]. exists e.
    assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  assert (Hqg : forall y, pure_in (shapes s) y -> quiet_or_gone s y -> quiet_or_gone s' y).
  { intros y Hy (e&He&Q). exists e. assert (Hyn : y <> n) by (intros ->; done). rewrite Hne1 by done. done. }
  destruct Hw0 as [W1 W2 W3 W4 W5 W6 W7 W8].
  split; [split|split].
  - intros m dm Hdm Hkm. destruct (Hback m dm Hdm) as [[-> ->]|[_ H]]; [congruence|by apply (W1 m dm)].
  - rewrite Hsh. intros c dc Hdc Hkc Hpc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]]; [congruence|by apply (W2 c dc)].
  - intros c dc Hdc Hqc. destruct (Hback c dc Hdc) as [[-> ->]|[_ H]].
    + rewrite Hsc, Hfl. exact (W3 n d Hd Hq).
    + apply (W3 c dc H). exact (isq_shape _ _ _ _ _ Hsh eq_refl Hqc).
  - rewrite Hsh, He'. intros c dc Hdc Hqc t Ht. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]].
    + pose proof (W4 n d Hd (or_intror Hq) t Ht) as [_ B9]. split; [intros K9; congruence|exact B9].
    + exact (W4 c dc H (qc_ext _ _ _ _ Hsh Hqc) t Ht).
  - rewrite He', Hrun. intros c dc Hdc Hqc Hl0 Hnr0 Hset t Ht Hnt. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]].
    + destruct (W4 n d Hd (or_intror Hq) t Ht) as [_ Hpt]. apply Hrd; [done|]. exact (W5 n d Hd (or_intror Hq) Hl Hnr Hset0 t Ht Hnt).
    + pose proof (qc_ext _ _ _ _ Hsh Hqc) as Hq0. destruct (W4 c dc H Hq0 t Ht) as [_ Hpt]. apply Hrd; [done|].
      exact (W5 c dc H Hq0 Hl0 Hnr0 Hset t Ht Hnt).
  - rewrite Hsh, Hrun. intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hset. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]]; [congruence|].
    refine (value_ext s s' c dc _ _ (W6 c dc H Hkc Hpc Hl0 Hnr0 Hset)); [by rewrite He'|exact (Hse c)].
  - rewrite Hsh, He', Hrun. intros c dc Hdc Hkc Hpc Hl0 Hnr0 Hq0 t Ht. destruct (Hback c dc Hdc) as [[-> ->]|[Hcn H]]; [congruence|].
    destruct (W4 c dc H (or_introl (conj Hpc Hkc)) t Ht) as [_ Hpt]. apply Hqg; [done|]. exact (W7 c dc H Hkc Hpc Hl0 Hnr0 Hq0 t Ht).
  - rewrite Hsh, Hpa. intros c Hc. destruct (W8 c Hc) as [A B]. split; [done|]. intros Ha. apply B. intros m dm Hdm Hkm.
    rewrite <- Hsh. destruct (decide (m = n)) as [->|Hm]; [rewrite Hd in Hdm; injection Hdm as <-; congruence|].
    refine (Ha m dm _ Hkm). by rewrite Hne1.
  - intros m dm Hm Hdm Hq0. rewrite Hrun in Hm.
    assert (Hmn : m <> n) by (intros ->; done). rewrite Hne1 in Hdm by done.
    exact (Hnp m dm Hm Hdm (qc_ext _ _ _ _ Hsh Hq0)).
  - split_and!.
    + intros c Hc. rewrite Hpa in Hc. destruct (Hpd c Hc) as [x Hx]. destruct (decide (c = n)) as [->|Hcn]; [rewrite Ht1; by eexists|]. rewrite Hne1 by done. rewrite Hx. by eexists.
    + apply (ran_frame _ s); [|exact Hran]. intros c d' Hd' Hq0 Hl0 Hf0. destruct (Hback c d' Hd') as [[-> ->]|[Hcn H]].
      * unfold shape_of in Hshd. injection Hshd as _ _ E5 B5. exists d. split_and!; [done|done|done| | |done|done|by rewrite He'|exact (Hse n)|by rewrite Htr].
        -- destruct Hset0 as [K|K]; [done|]. rewrite (proj2 (W3 n d Hd Hq)) in K. done.
        -- unfold window. rewrite Hst. done.
      * exists d'. split_and!; [done|exact (isq_shape _ _ _ _ _ Hsh eq_refl Hq0)|done|done|done|done|done|by rewrite He'|exact (Hse c)|by rewrite Htr].
    + apply (hosts_frame s); [| |intros o y Hy; rewrite Hown in Hy; left; exists y; done|exact Hho].
      * intros m d' Hd'. destruct (Hback m d' Hd') as [[-> ->]|[_ H]]; [exists d; split; [done|exact (fun _ => Hq)]|exists d'; split; [done|exact (isq_shape _ _ _ _ _ Hsh eq_refl)]].
      * intros m Hm. destruct (decide (m = n)) as [->|Hmn]; [rewrite Ht1; by eexists|]. by rewrite Hne1.
Qed.

Lemma exe_core f n s s' a :
  run_core s -> wk s -> effect_ctx n s -> good s -> execute_effect (S f) n s = Some (s', a) -> wk s' /\ keepq s s'.
Proof.
    intros Hc Hw Hctx Hg0 E. cbn [execute_effect] in E.
    pose proof Hctx as [(d&Hd&Hkd) Hfc].
    rewrite (bind_get_node _ _ _ _ Hd) in E.
    pose proof (proj2 Hg0 n d Hd Hkd) as Hq.
    pose proof (proj1 (proj2 Hc)) as Hb.
    destruct (n_disposed d) eqn:Edi.
    { unfold modify_node in E. rewrite Hd in E. injection E as <- _. split.
      - refine (wframe_apply _ _ Hb (wframe_set s n d (node_with_queued false d) Hd eq_refl _) Hw). intros _. split_and!; done.
      - refine (keepq_node _ _ n d Hd Hkd (shapes_set s n d (node_with_queued false d) Hd eq_refl) _). intros m Hm9. simpl. by apply lookup_insert_ne. }
    destruct Hfc as (Hcur&Hfl&Hmemo).
    assert (Hnr : n ∉ g_running s).
    { intros Hin. pose proof (Hmemo n Hin) as Hkm. by pose proof (has_kind_unique n _ _ s (ex_intro _ d (conj Hd Hkd)) Hkm). }
    destruct (decide (n_freshness d = Check)) as [Hch|Hch].
    - rewrite bind_bind in E. apply bind_inv in E as (s1 & [] & Hwk & E).
      set (lim := S (g_next_id s)).
      pose proof (stk_empty lim s Hb Hw (or_intror Hfl)) as St.
      assert (Hrnn : ~ rn s lim None n).
      { intros [[Hp _]|K]; [exact (effect_not_pure s n d Hd Hkd Hp)|done]. }
      assert (Hsl : forall u, u ∈ (slot_view (g_edges s) n).1 -> u < lim /\ pure_in (shapes s) u).
      { intros u Hu. destruct (w_slots _ (proj1 Hw) n d Hd (or_intror Hq) u Hu) as [_ Hpu]. split; [|done].
        destruct (pure_exists s u Hpu) as [x Hx]. pose proof (core_ids _ Hc u (mk_is_Some _ _ Hx)). subst lim. lia. }
      destruct (Awalk _ (A_ok_all f) (fun _ => False) lim n 0 s s1 tt (conj Hc (conj (proj1 Hw) St)) Hrnn Hnr Hsl Hwk) as ((Hc1&Hw1&St1)&A1&Hall).
      pose proof (wk_of_atom _ _ _ _ Hw A1 Hw1) as Hw1'.
      assert (Hk1 : keepq s s1) by exact (keepq_atom _ _ _ _ _ A1 (fun _ _ K => K)).
      destruct (at_rise _ _ _ _ _ A1 n d Hrnn Hd) as (d1&Hd1&R1).
      assert (Hq1 : quiet_effect s1 n d1) by exact (isq_shape_fwd _ _ _ _ _ (at_shapes _ _ _ _ _ A1) (proj1 R1) Hq).
      assert (Hl1 : n_disposed d1 = false) by (pose proof R1 as (_&_&X&_); congruence).
      assert (Hctx1 : effect_ctx n s1).
      { destruct (Rwalk _ (runs_ok_all f) n 0 _ _ _ Hc I Hwk) as [_ K1]. exact (stable2 _ _ K1 Hctx). }
      rewrite bind_bind, (bind_get_node _ _ _ _ Hd1) in E.
      destruct (decide (n_freshness d1 = Check)) as [Hc2|Hc2].
      + rewrite bind_bind, bind_m_modify in E. cbv beta in E.
        destruct (ghost_resolved_fields n s1) as (F1&F2&_).
        assert (Hg1 : g_nodes (ghost_resolved n s1) !! n = Some d1) by (by rewrite F2).
        rewrite (bind_modify_node _ _ _ _ _ Hg1) in E.
        assert (Hd2 : g_nodes (set_node n (node_with_freshness Clean d1) (ghost_resolved n s1)) !! n = Some (node_with_freshness Clean d1))
          by (simpl; apply lookup_insert_eq).
        rewrite (bind_get_node _ _ _ _ Hd2) in E.
        rewrite (bind_modify_node _ _ _ _ _ Hd2) in E.
        rewrite decide_False in E by (simpl; done).
        unfold m_return in E. injection E as <- _. rewrite set_node_twice.
        assert (Hqg : forall src e, src ∈ (slot_view (g_edges s1) n).1 -> g_nodes s1 !! src = Some e -> stale_live e && negb (n_unannounced e) = false).
        { intros src e Hin He. apply list_elem_of_lookup in Hin as [j Hj]. unfold slot_view in Hj. simpl in Hj.
          rewrite list_lookup_fmap in Hj. destruct (source_slots (sources_of (g_edges s1) n) !! j) as [[v mh]|] eqn:Es; [|done].
          simpl in Hj. injection Hj as ->. exact (qog_not_passing s1 src (Hall d1 j src mh Hd1 Hc2 Es ltac:(lia)) e He). }
        set (X := node_with_freshness Clean (node_with_queued false (node_with_freshness Clean d1))).
        assert (Hkd1 : n_kind d1 = KEffect) by (rewrite (rise_kind _ _ R1); exact Hkd).
        split.
        2: { refine (keepq_trans _ _ _ Hk1 (keepq_node _ _ n d1 Hd1 Hkd1 _ _)).
             - unfold shapes. change (shape_of <$> <[n := X]> (g_nodes (ghost_resolved n s1)) = shape_of <$> g_nodes s1). rewrite F2, fmap_insert. assert (X9 : shape_of X = shape_of d1) by reflexivity. rewrite X9. apply insert_id. by rewrite lookup_fmap, Hd1.
             - intros m Hm9. change (<[n := X]> (g_nodes (ghost_resolved n s1)) !! m = g_nodes s1 !! m). rewrite lookup_insert_ne by done. by rewrite F2. }
        refine (effect_clean s1 (set_node n X (ghost_resolved n s1)) n d1 X Hw1' (proj1 (proj2 Hc1)) Hd1 Hq1 _ Hl1 _ eq_refl eq_refl eq_refl eq_refl Hl1 eq_refl (fun _ _ => eq_refl) _ eq_refl _ _ eq_refl).
        * left. congruence.
        * rewrite (at_running _ _ _ _ _ A1). done.
        * unfold ghost_resolved. simpl. rewrite slot_holds_false; [done|]. exact Hqg.
        * unfold ghost_resolved. simpl. repeat case_match; reflexivity.
        * unfold ghost_resolved. simpl. repeat case_match; reflexivity.
      + rewrite bind_m_return, (bind_get_node _ _ _ _ Hd1) in E.
        destruct (effect_tail_wk f n d1 s1 s' a Hc1 Hw1' Hctx1 Hd1 Hq1 Hl1 Hc2 E) as [K5 K6]. split; [exact K5|exact (keepq_trans _ _ _ Hk1 K6)].
    - rewrite bind_m_return, (bind_get_node _ _ _ _ Hd) in E.
      exact (effect_tail_wk f n d s s' a Hc Hw Hctx Hd Hq Edi Hch E).
  Qed.

Lemma drain_fw f : forall s s' a, run_core s -> wk s -> flush_ctx s -> good s -> drain f s = Some (s', a) -> wk s' /\ keepq s s'.
Proof.
  induction f as [|f IH]; intros s s' a Hc Hw Hctx Hg E; [done|]. cbn [drain] in E. rewrite bind_get in E.
  destruct (g_queue s !! g_queue_head s) as [n|] eqn:Eq.
  - rewrite bind_m_modify in E. cbv beta in E.
    assert (Hin : n ∈ g_queue s) by (apply list_elem_of_lookup; by exists (g_queue_head s)).
    set (s1 := with_queue_head (S (g_queue_head s)) s) in E.
    destruct Hc as [Hi Hx].
    assert (Hfs : fields_same s s1) by (split; reflexivity).
    assert (Hi1 : check_invariant s1).
    { eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity. }
    assert (Hx1 := extras_same _ _ Hfs Hx).
    assert (Hk : has_kind n KEffect s1) by apply (ci_queue _ _ _ Hi n Hin).
    assert (Hctx1 : effect_ctx n s1) by (split; [done|exact Hctx]).
    apply bind_inv in E as (s2 & [] & Hex & E2').
    destruct (Rexecute _ (runs_ok_all f) n _ _ _ (conj Hi1 Hx1) Hctx1 Hex) as [Hc2 K2].
    pose proof (stable2 _ _ K2 Hctx1) as [_ Hctx2].
    assert (Hw1 : wk s1) by (apply (wk_same s); [split_and!; done|exact Hw]).
    assert (Hg1 : good s1) by exact (good_shapes s s1 eq_refl Hg).
    destruct f as [|f']; [done|].
    destruct (exe_core f' n s1 s2 tt (conj Hi1 Hx1) Hw1 Hctx1 Hg1 Hex) as [Hw2 Hk2].
    destruct (IH s2 s' a Hc2 Hw2 Hctx2 (good_shapes s1 s2 (proj1 Hk2) Hg1) E2') as [Hw3 Hk3].
    split; [done|]. refine (keepq_trans _ _ _ _ (keepq_trans _ _ _ Hk2 Hk3)). split; [reflexivity|]. intros t e He _ Hq. exists e. done.
  - unfold m_modify in E. injection E as <- _. split; [apply (wk_same s); [split_and!; done|exact Hw]|].
    split; [reflexivity|]. intros t e He _ Hq. exists e. done.
Qed.

Lemma settle_fw f s s' a : run_core s -> wk s -> good s -> settle_flush f s = Some (s', a) -> wk s' /\ keepq s s'.
Proof.
  intros Hc Hw Hg E. destruct f as [|f]; [done|]. cbn [settle_flush] in E. rewrite bind_get in E.
  destruct (g_flush_owed s && _ && outside_bodies s) eqn:Eo; [|unfold m_return in E; injection E as <- _; split; [done|apply keepq_refl]].
  apply andb_prop in Eo as [_ Eo]. pose proof (outside_current _ Eo) as Hcur.
  destruct f as [|f]; [done|]. cbn [run_flush] in E. rewrite bind_get in E.
  destruct (g_flushing s) eqn:Efl; [unfold m_return in E; injection E as <- _; split; [done|apply keepq_refl]|].
  rewrite bind_m_modify in E. cbv beta in E.
  set (s1 := with_flush_owed false (with_flushing true s)) in E.
  destruct Hc as [Hi [Hb Ht]].
  destruct Hb as [B1 B2 [E1 E2 E3 E4] B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 B16 B17 B18] eqn:Ebase.
  assert (Hb1 : run_base s1).
  { refine (Build_run_base s1 B1 B2 (Build_mark_env s1 E1 E2 E3 E4) B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 _ B17 B18).
    intros c _ _. done. }
  assert (Hi1 : check_invariant s1).
  { eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity. }
  assert (Hmemo : forall c, c ∈ g_running s -> has_kind c KMemo s).
  { intros c Hc. destruct (B15 c Hc) as [K|K]; [done|]. pose proof (B16 c K Hc). congruence. }
  assert (Hctx1 : flush_ctx s1) by (split_and!; [exact Hcur|done|exact Hmemo]).
  apply bind_inv in E as (s2 & [] & Hd & E2').
  unfold m_modify in E2'. injection E2' as <- _.
  assert (Hw1 : wk s1) by (apply (wk_same s); [split_and!; done|exact Hw]).
  assert (Hg1 : good s1) by exact (good_shapes s s1 eq_refl Hg).
  destruct (drain_fw f s1 s2 tt (conj Hi1 (conj Hb1 Ht)) Hw1 Hctx1 Hg1 Hd) as [Hw2 Hk2].
  split; [apply (wk_same s2); [split_and!; done|exact Hw2]|].
  destruct Hk2 as [S2 K2]. split; [exact S2|]. exact K2.
Qed.

Section WStep2.
  Variable f : nat.
  Hypothesis IHall : forall X', W_ok X' f.
  Let IH : W_ok Xe f := IHall Xe.

  Lemma Wwrite_S n v : wf (fun _ => True) (write_signal (S f) n v).
  Proof.
    intros s s' a Hc Hw _ E Hg. cbn [write_signal] in E.
    destruct (g_nodes s !! n) as [d|] eqn:Hd; [|unfold m_bind, get_node in E; by rewrite Hd in E].
    rewrite (bind_get_node _ _ _ _ Hd) in E. destruct (n_kind d) eqn:Ek; try done.
    destruct (decide _); [unfold m_return in E; by injection E as <- _|].
    rewrite (bind_modify_node _ _ _ _ _ Hd) in E. rewrite bind_m_modify in E.
    apply bind_step in E as (s2 & Hn & Hm2).
    pose proof (write_step f n d v s s2 Hc Hw Hd Ek Hn) as Hw2.
    destruct Hc as [Hi Hx].
    set (s1 := set_node n (node_with_value v d) s) in Hn.
    assert (Hi1 : check_invariant_unpublished (Some n) s1).
    { apply (invariant_set_gen (Some n) None n (node_with_value v) d);
        [by apply invariant_weaken|done|done|done|intros; congruence|done|intros; congruence]. }
    destruct (extras_neutral n (node_with_value v) d s Hx Hd ltac:(unfold neutral; simpl; split_and!; first [reflexivity|done])) as [Hx1 E1].
    assert (Hd1 : g_nodes s1 !! n = Some (node_with_value v d)) by (simpl; apply lookup_insert_eq).
    unfold ghost_publish in Hn. rewrite Hd1 in Hn.
    set (G := {| gh_published := _; gh_seen := _; gh_unnoticed := _; gh_window := _; gh_reads := _; gh_passed := _; gh_over := _; gh_run_reads := _; gh_nested := _ |}) in Hn.
    assert (Hreach : world_reachable (g_edges s1)) by apply (ci_reachable _ _ _ Hi1).
    destruct (invariant_told n (node_with_value v d) s1 G s2) as [Hi2 F2]; [done|done|done|done|done|simpl; set_solver| | |].
    { apply (any_mark_rtc f n None). right; right; right; left. done. }
    { intros c dc Hc Hkc Hpre. right. left.
      eapply (notify_dirty_ends_dirty f n None (with_ghost G s1)).
      - by apply prefix_observer.
      - exists dc. split; [done|by left].
      - left. exact Hn. }
    assert (Hfs : fields_same s1 (with_ghost G s1)) by (split; reflexivity).
    assert (Ham : any_mark f n None (with_ghost G s1) s2) by (right; right; right; left; done).
    destruct (extras_marks _ _ _ _ _ (extras_same _ _ Hfs Hx1) Ham) as [Hx2 E2].
    exact (Wreq _ IH _ _ _ (conj Hi2 Hx2) Hw2 I Hm2 Hg).
  Qed.

  Lemma Wdisp_S n : wf (fun _ => True) (dispose_node (S f) n).
  Proof.
    intros s s' a Hc Hw _ E Hg. cbn [dispose_node] in E. pose proof Hc as [Hi Hx].
    destruct (g_nodes s !! n) as [d|] eqn:Hd; [|by unfold m_bind, get_node in E; rewrite Hd in E].
    rewrite (bind_get_node _ _ _ _ Hd) in E.
    destruct (n_disposed d) eqn:Edi; [unfold m_return in E; by injection E as <- _|].
    rewrite (bind_modify_node _ _ _ _ _ Hd) in E. cbv beta in E.
    set (u := node_with_link None (node_with_disposed true d)) in E.
    set (s1 := set_node n u s) in E.
    assert (Hlists : forall o x, g_owners s !! o = Some x -> lists_ok x) by (apply (b_lists _ (proj1 Hx))).
    apply bind_inv in E as (s2 & [] & Hun & E2).
    assert (Hs2 : exists O, s2 = with_owners O s1 /\ (forall o x, O !! o = Some x -> lists_ok x) /\
              forall o x, O !! o = Some x -> exists x0, g_owners s !! o = Some x0 /\ o_host x0 = o_host x).
    { destruct (n_link d) as [l|].
      - unfold unlink, m_bind, get_owner in Hun.
        destruct (g_owners s1 !! l_owner l) as [x|] eqn:Eo; [|done].
        assert (Hxl : lists_ok x) by (exact (Hlists _ _ Eo)).
        destruct (decide _).
        + injection Hun as <-. eexists. split; [reflexivity|split].
          * intros o y. change (<[l_owner l := owner_without l x]> (g_owners s) !! o = Some y -> lists_ok y).
            destruct (decide (o = l_owner l)) as [->|Hne].
            -- rewrite lookup_insert_eq. intros [=<-]. exact Hxl.
            -- rewrite lookup_insert_ne by done. apply Hlists.
          * intros o y. change (<[l_owner l := owner_without l x]> (g_owners s) !! o = Some y -> exists x0, g_owners s !! o = Some x0 /\ o_host x0 = o_host y).
            destruct (decide (o = l_owner l)) as [->|Hne].
            -- rewrite lookup_insert_eq. intros [=<-]. exists x. split; [exact Eo|reflexivity].
            -- rewrite lookup_insert_ne by done. intros Hy. exists y. done.
        + injection Hun as <-. exists (g_owners s1). split; [by rewrite with_owners_same|split; [exact Hlists|intros o y Hy; exists y; done]].
      - injection Hun as <-. exists (g_owners s1). split; [by rewrite with_owners_same|split; [exact Hlists|intros o y Hy; exists y; done]]. }
    destruct Hs2 as (O & -> & HO & HOh).
    apply bind_edges_opt in E2 as (w & Hwc & E3).
    change (sources_clear n (g_edges s) = Some w) in Hwc.
    destruct (extras_dispose n d w s O Hx Hd Hwc HO) as [Hx3 _].
    assert (Hi1 : check_invariant s1).
    { assert (Hst : node_step (fun x => node_with_link None (node_with_disposed true x)) d) by ns'.
      exact (invariant_set None None n _ d s Hi Hd Hst). }
    assert (Hi2 : check_invariant (with_owners O s1)).
    { eapply invariant_irrelevant; [|exact Hi1]. unfold irrelevant. simpl. split_and!; reflexivity. }
    assert (Hi3 : check_invariant (with_edges w (with_owners O s1))) by (apply (invariant_clear None n w _ Hi2 Hwc)).
    set (s3 := with_edges w (with_owners O s1)) in *.
    assert (Hn3 : g_nodes s3 = <[n := u]> (g_nodes s)) by done.
    assert (Hu3 : g_nodes s3 !! n = Some u) by (rewrite Hn3; apply lookup_insert_eq).
    assert (Hne3 : forall m : node_id, m <> n -> g_nodes s3 !! m = g_nodes s !! m) by (intros m Hm; rewrite Hn3; by apply lookup_insert_ne).
    assert (Hw3 : wk s3).
    { refine (wframe_apply _ _ (proj1 Hx) _ Hw). split.
      - exact (shapes_set s n d u Hd eq_refl).
      - intros m e He _. destruct (decide (m = n)) as [->|Hm].
        + rewrite Hd in He. injection He as <-. exists u. split; [done|]. split_and!; done.
        + exists e. rewrite Hne3 by done. split; [done|]. split_and!; done.
      - intros m e He. destruct (decide (m = n)) as [->|Hm].
        + rewrite Hu3 in He. injection He as <-. exists d. split_and!; [done|done|]. intros _. split_and!; done.
        + rewrite Hne3 in He by done. exists e. split_and!; [done|done|]. intros _. split_and!; done.
      - intros c e He _. change (slot_view w c = slot_view (g_edges s) c \/ ((slot_view w c).1 = [] /\ exists d', g_nodes s3 !! c = Some d' /\ n_disposed d' = true)).
        rewrite (slot_view_clear n _ w c Hwc). destruct (decide (c = n)) as [->|Hne]; [right|by left]. split; [done|]. exists u. done.
      - intros c e y _ _. done.
      - intros c Hc0. by left.
      - intros m e Hm _. by left.
      - done.
      - intros o y Hy. left. exact (HOh o y Hy). }
    destruct (n_scope d) as [o|].
    - exact (Wret _ IH o _ _ _ (conj Hi3 Hx3) Hw3 I E3 Hg).
    - unfold m_return in E3. by injection E3 as <- _.
  Qed.

  Lemma Wrop_S n : wf (live_memo n) (refresh_or_pull (S f) n).
  Proof.
    intros s s' a Hc Hw Hl E Hg. pose proof E as E0. cbn [refresh_or_pull] in E. rewrite bind_get in E.
    destruct Hl as (d&Hd&Hk&Hli).
    destruct (deferring s) eqn:Ed.
    - destruct (Kref _ (K_ok_all f) n _ _ _ (core_ids _ Hc) E) as [_ K].
      pose proof (proj1 (good_back _ _ (core_ids _ Hc) K Hg) n d Hd Hk) as Hp.
      pose proof (stk_empty (S n) s (proj1 (proj2 Hc)) Hw (or_introl Ed)) as St.
      destruct (Aref _ (A_ok_all f) (fun _ => False) (S n) n s s' a d (conj Hc (conj (proj1 Hw) St)) ltac:(lia) Hp Hd Hk Hli E) as ((_&Hw'&_)&A&_).
      exact (wk_of_atom _ _ _ _ Hw A Hw').
    - exact (Wpull _ IH n _ _ _ Hc Hw (ex_intro _ d (conj Hd (conj Hk Hli))) E Hg).
  Qed.

  Lemma Wpull_S n : wf (live_memo n) (pull (S f) n).
  Proof.
    intros s s' a Hc Hw Hl E Hg. cbn [pull] in E. rewrite bind_m_modify in E.
    set (s0 := with_pull_depth (S (g_pull_depth s)) s) in E.
    assert (H0 : same4 s s0) by done.
    destruct Hl as (d&Hd&Hk&Hli).
    apply bind_inv in E as (s1 & a1 & Hr & E). rewrite bind_m_modify, bind_get in E.
    set (s2 := with_pull_depth (pred (g_pull_depth s1)) s1) in E.
    assert (Hc0 : run_core s0) by (by apply core_pull).
    assert (Hw0 : wk s0) by (apply (wk_same s); [split_and!; done|exact Hw]).
    destruct (Kref _ (K_ok_all f) n _ _ _ (core_ids _ Hc0) Hr) as [Hid1 K1].
    assert (Hkept2 : kept s1 s2) by (split; [intros m x Hx; exists x; done|intros m x Hx; left; by eexists|done]).
    assert (Hid2 : ids_ok s2) by exact Hid1.
    assert (Hg2 : good s2).
    { destruct (bool_decide (g_pull_depth s2 = 0) && g_flush_owed s2).
      - destruct (Kset _ (K_ok_all f) _ _ _ Hid2 E) as [_ K3]. exact (good_back _ _ Hid2 K3 Hg).
      - unfold m_return in E. by injection E as <- _. }
    pose proof (good_back _ _ Hid1 Hkept2 Hg2) as Hg1.
    pose proof (good_back _ _ (core_ids _ Hc0) K1 Hg1) as Hg0.
    assert (Hd0 : g_nodes s0 !! n = Some d) by exact Hd.
    pose proof (proj1 Hg0 n d Hd0 Hk) as Hp.
    assert (Hdf : deferring s0 = true).
    { unfold deferring. destruct (g_current s0); [done|]. apply bool_decide_eq_true_2. simpl. lia. }
    pose proof (stk_empty (S n) s0 (proj1 (proj2 Hc0)) Hw0 (or_introl Hdf)) as St.
    destruct (Aref _ (A_ok_all f) (fun _ => False) (S n) n s0 s1 a1 d (conj Hc0 (conj (proj1 Hw0) St)) ltac:(lia) Hp Hd0 Hk Hli Hr) as ((Hc1&Hw1&_)&A&_).
    pose proof (wk_of_atom _ _ _ _ Hw0 A Hw1) as Hw1'.
    assert (Hc2 : run_core s2) by (by apply core_pull).
    assert (Hw2 : wk s2) by (apply (wk_same s1); [split_and!; done|exact Hw1']).
    destruct (bool_decide (g_pull_depth s2 = 0) && g_flush_owed s2).
    - exact (Wset _ IH _ _ _ Hc2 Hw2 I E Hg).
    - unfold m_return in E. by injection E as <- _.
  Qed.

  Lemma Wread_S n b : wf (fun _ => True) (read (S f) n b).
  Proof.
    destruct b; cbn [read].
    - exact (Wnode _ IH n).
    - assert (H : wok (fun _ => True) (untracked (read_node f n))).
      { apply wok_untracked. wrih IH. }
      exact (proj2 (proj2 H)).
  Qed.

  Lemma Wnode_S n : wf (fun _ => True) (read_node (S f) n).
  Proof.
    intros s s' o Hc Hw _ E Hg. cbn [read_node] in E.
    destruct (g_nodes s !! n) as [d|] eqn:Hd; [|unfold m_bind, get_node in E; by rewrite Hd in E].
    rewrite (bind_get_node _ _ _ _ Hd) in E.
    destruct (n_kind d) eqn:Ek.
    - apply bind_inv in E as (s1 & [] & Htr & E). unfold m_return in E. injection E as <- _.
      exact (proj2 (wk_track s n d s1 (proj2 Hc) Hw Hd ltac:(congruence) Htr)).
    - apply bind_inv in E as (s2 & [] & H1 & E).
      destruct (g_nodes s2 !! n) as [e|] eqn:He; [|unfold m_bind, get_node in E; by rewrite He in E].
      rewrite (bind_get_node _ _ _ _ He) in E.
      destruct (read_answer _ _ _ _ E) as [-> _].
      destruct (stale_live d) eqn:El.
      + assert (Hl : n_disposed d = false) by (unfold stale_live in El; destruct (n_disposed d); [by rewrite andb_false_r in El|done]).
        apply bind_inv in H1 as (s1 & a1 & Hrp & H1).
        destruct (Rrefresh_or_pull _ (runs_ok_all f) n _ _ _ Hc (ex_intro _ d (conj Hd Ek)) Hrp) as [Hc1 Kp1].
        assert (Htail : ko (let* _ := track n in let* d' := get_node n in if stale_live d' then mark_reader f else m_return tt)).
        { apply ko_bind; [apply ko_track|intros _]. apply ko_bind; [apply ko_get_node|intros d'].
          destruct (stale_live d'); [apply ko_mark_reader|apply ko_return]. }
        destruct (Htail _ _ _ (core_ids _ Hc1) H1) as [_ K2].
        pose proof (good_back _ _ (core_ids _ Hc1) K2 Hg) as Hg1.
        pose proof (Wrop _ IH n _ _ _ Hc Hw (ex_intro _ d (conj Hd (conj Ek Hl))) Hrp Hg1) as Hw1.
        apply bind_inv in H1 as (st & [] & Htr & H1).
        destruct (rs_kind_of_core n s s1 d Kp1 Hd Ek) as (d1&Hd1&Hk1).
        destruct (wk_track s1 n d1 st (proj2 Hc1) Hw1 Hd1 ltac:(congruence) Htr) as [Hbt Hwt].
        assert (Hdt : g_nodes st !! n = Some d1) by (by rewrite (track_nodes _ _ _ Htr)). rewrite (bind_get_node _ _ _ _ Hdt) in H1.
        destruct (stale_live d1).
        * unfold mark_reader in H1. destruct (g_current st) as [c|] eqn:Ecu; [|by injection H1 as <-].
          destruct (f_reader_mark (g_flags st)); [|by injection H1 as <-].
          destruct (mark_check f c st) as [s9|] eqn:Em; [|done]. injection H1 as <-.
          refine (wk_mark_from st c f s9 Hbt Hwt _ Em). intros Hpc.
          exact (np_pure st c Hbt (proj1 (proj2 Hwt)) Hpc (b_current _ Hbt c Ecu)).
        * unfold m_return in H1. by injection H1 as <-.
      + assert (Hk' : n_kind d <> KEffect) by congruence.
        exact (proj2 (wk_track s n d s2 (proj2 Hc) Hw Hd Hk' H1)).
    - done.
  Qed.

  Lemma Wexe_S n : wf (effect_ctx n) (execute_effect (S f) n).
  Proof.
    intros s s' a Hc Hw Hctx E Hg. pose proof E as E0. pose proof Hctx as [(d&Hd&Hkd) _].
    destruct (Kexe _ (K_ok_all (S f)) n _ _ _ (core_ids _ Hc) E0) as [_ K0].
    pose proof (good_back _ _ (core_ids _ Hc) K0 Hg) as Hg0.
    exact (proj1 (exe_core f n s s' a Hc Hw Hctx Hg0 E)).
  Qed.

  Lemma wk_good_top s : run_core s -> np s -> good s -> g_current s = None.
  Proof.
    intros Hc Hn [Ha He]. destruct (g_current s) as [h|] eqn:Ec; [|done]. exfalso.
    pose proof (proj1 (proj2 Hc)) as Hb. pose proof (b_current _ Hb h Ec) as Hrun.
    destruct (b_running_kind _ Hb h Hrun) as [(e0&He0&Hk0)|(e0&He0&Hk0)].
    - exact (Hn h e0 Hrun He0 (or_introl (conj (Ha h e0 He0 Hk0) Hk0))).
    - exact (Hn h e0 Hrun He0 (or_intror (He h e0 He0 Hk0))).
  Qed.

  Lemma mark_new_dirty g n s s' d :
    mark_dirty g n s = Some s' -> g_nodes s !! n = Some d -> n_kind d = KEffect -> n_disposed d = false -> n_in_cleanup d = false ->
    exists d', g_nodes s' !! n = Some d' /\ n_freshness d' = Dirty.
  Proof.
    intros E Hd Hk Hl Hcl. destruct g as [|g]; [done|]. unfold mark_dirty in E. rewrite Hd, Hk in E. injection E as <-.
    unfold effect_marked. rewrite Hl, Hcl. simpl. destruct (n_queued d); simpl; rewrite lookup_insert_eq; by eexists.
  Qed.

  Lemma Wnew_effect x b k e :
    wok (fun _ => True) (exec (S f) e (CEffect x b k)).
  Proof.
    cbn [exec].
    set (D := fun n : node_id => effect_node b ((x, Z.of_nat n) :: e)).
    set (K0 := fun n : node_id =>
      m_bind current_owner (fun owner => match owner with
        | None => m_return (Fail pure_scope_failure)
        | Some o =>
          m_bind (attach f n o) (fun _ => m_bind (m_step (mark_dirty f n)) (fun _ =>
          m_bind (request_flush f) (fun _ => exec f ((x, Z.of_nat n) :: e) k))) end)).
    change (wok (fun _ => True) (m_bind next_id (fun n => m_bind (add_node n (D n)) (fun _ => K0 n)))).
    assert (WK : forall (Y : node_id -> Prop) n, @wokx Y _ (fun s => True /\ has_kind n KEffect s) (K0 n)).
    { intros Y n. pose proof (IHall Y) as IHY. apply (@wok_bind Y); [apply _|apply wok_current_owner|intros owner]. destruct owner as [o|]; [|apply wok_return].
      apply (@wok_bind Y); [apply _|apply wok_any; wrih IHY|intros _].
      apply (@wok_bind Y); [apply _|apply (@wok_mark_new Y (fun _ => True))|intros _].
      apply (@wok_bind Y); [apply _|apply wok_any; wrih IHY|intros _]. apply wok_any. wrih IHY. }
    split; [|split].
    - intros s s' b0 Hc _ Hm. pose proof Hm as Hm0. unfold m_bind, next_id, add_node, m_modify in Hm. simpl in Hm.
      destruct Hc as [Hi Hx].
      set (n := S (g_next_id s)) in Hm.
      destruct (invariant_new_node s n (D n) Hi eq_refl eq_refl) as [Hi1 F1].
      destruct (extras_new s n (D n) Hi Hx eq_refl eq_refl eq_refl eq_refl (fun H => ltac:(discriminate H))) as [Hx1 E1].
      assert (K1 : run_kept s (ghost_publish n (set_node n (D n) (with_next_id n s)))) by (split; done).
      assert (Hhk : has_kind n (n_kind (D n)) (ghost_publish n (set_node n (D n) (with_next_id n s)))).
      { exists (D n). split; [|done]. unfold ghost_publish. simpl. rewrite lookup_insert_eq. simpl. apply lookup_insert_eq. }
      destruct (proj1 (WK Xe n) _ _ _ (conj Hi1 Hx1) (conj I Hhk) Hm) as [Hc2 K2].
      split; [done|]. by eapply kept_trans.
    - apply ko_new_node. intros n. exact (proj1 (proj2 (WK Xe n))).
    - intros s s' r Hc Hw _ Hm Hg. unfold m_bind at 1, next_id, add_node, m_modify in Hm. simpl in Hm.
      set (n := S (g_next_id s)) in Hm.
      set (s1 := ghost_publish n (set_node n (D n) (with_next_id n s))) in Hm.
      unfold m_bind at 1 in Hm. simpl in Hm. fold s1 in Hm.
      set (X' := fun c => Xe c \/ c = n).
      pose proof Hc as Hc9. destruct Hc as [Hi Hx].
      destruct (invariant_new_node s n (D n) Hi eq_refl eq_refl) as [Hi1 F1].
      destruct (extras_new s n (D n) Hi Hx eq_refl eq_refl eq_refl eq_refl (fun H => ltac:(discriminate H))) as [Hx1 E1].
      assert (Hc1 : run_core s1) by exact (conj Hi1 Hx1).
      assert (Hhk : has_kind n KEffect s1).
      { exists (D n). split; [|reflexivity]. subst s1. unfold ghost_publish. simpl. rewrite lookup_insert_eq. simpl. apply lookup_insert_eq. }
      destruct (WK X' n) as [_ [K1 _]].
      destruct (K1 _ _ _ (core_ids _ Hc1) Hm) as [_ Kk]. pose proof (good_back _ _ (core_ids _ Hc1) Kk Hg) as Hg1.
      assert (Hw1 : wkx X' s1).
      { apply (whole_new s n (D n) X' Hc9 Hw eq_refl).
        - simpl. discriminate.
        - simpl. discriminate.
        - intros _. split; reflexivity.
        - intros _. split_and!; [by right|reflexivity|intros c0 H; by left].
        - by right.
        - exact (proj1 Hg1). }
      pose proof (wk_good_top s1 Hc1 (proj1 (proj2 Hw1)) Hg1) as Hcur.
      unfold K0 in Hm. apply bind_inv in Hm as (s2 & owner & Ho & Hm).
      unfold current_owner in Ho. destruct (g_current_owner s1) as [o|] eqn:Eo; [|rewrite Hcur in Ho; done].
      injection Ho as <- <-.
      apply bind_inv in Hm as (s3 & [] & Ha & Hm).
      destruct (Rattach _ (runs_ok_all f) n o _ _ _ Hc1 I Ha) as [Hc3 R3].
      destruct (Katt _ (K_ok_all f) n o _ _ _ (core_ids _ Hc1) Ha) as [_ K3].
      apply bind_inv in Hm as (s4 & [] & Hk & Hm).
      assert (WR : wok (fun _ => True) (m_bind (request_flush f) (fun _ => exec f ((x, Z.of_nat n) :: e) k))).
      { apply wok_bind; [apply _|apply wok_any; wrih IH|intros _]. apply wok_any. wrih IH. }
      destruct WR as [_ [KR TR]].
      assert (WM := @wok_mark_new X' (fun _ => True) n f). destruct WM as [OM [KM TM]].
      assert (Hhk3 : has_kind n KEffect s3) by exact (stable2 _ _ R3 Hhk).
      destruct (OM _ _ _ Hc3 (conj I Hhk3) Hk) as [Hc4 _].
      destruct (KR _ _ _ (core_ids _ Hc4) Hm) as [_ K5]. pose proof (good_back _ _ (core_ids _ Hc4) K5 Hg) as Hg4.
      destruct (KM _ _ _ (core_ids _ Hc3) Hk) as [_ K4]. pose proof (good_back _ _ (core_ids _ Hc3) K4 Hg4) as Hg3.
      pose proof (Watt _ (IHall X') n o _ _ _ Hc1 Hw1 I Ha Hg3) as Hw3.
      pose proof (TM _ _ _ Hc3 Hw3 (conj I Hhk3) Hk Hg4) as Hw4'.
      assert (Hw4 : wk s4).
      { destruct Hw4' as (W1&W2&W3&W4&W5). split; [done|split; [done|split; [done|split; [|done]]]].
        intros c d0 Hd0 Hq0 Hl0 Hf0. destruct (W4 c d0 Hd0 Hq0 Hl0 Hf0) as [[[A|A] B]|A]; [by left| |by right].
        subst c. exfalso. destruct Hhk3 as (d3&Hd3&Hk3).
        unfold m_step in Hk. destruct (mark_dirty f n s3) as [s5|] eqn:Em; [|done]. injection Hk as <-.
        destruct (n_disposed d3) eqn:Edi.
        - destruct f as [|f']; [done|]. unfold mark_dirty in Em. rewrite Hd3, Hk3 in Em. injection Em as <-. unfold effect_marked in Hd0.
          rewrite Edi in Hd0. simpl in Hd0. rewrite Hd3 in Hd0. injection Hd0 as <-. congruence.
        - assert (Hcl : n_in_cleanup d3 = false).
          { destruct (n_in_cleanup d3) eqn:E9; [|done]. exfalso.
            pose proof (b_cleanup_running _ (proj1 (proj2 Hc3)) n d3 Hd3 E9) as Hrun.
            exact (proj1 (proj2 Hw3) n d3 Hrun Hd3 (or_intror (proj2 Hg3 n d3 Hd3 Hk3))). }
          destruct (mark_new_dirty f n s3 s5 d3 Em Hd3 Hk3 Edi Hcl) as (d5&Hd5&Hf5). rewrite Hd0 in Hd5. injection Hd5 as <-. done. }
      exact (TR _ _ _ Hc4 Hw4 I Hm Hg).
  Qed.

  Lemma Wexec_S e c : wf (fun _ => True) (exec (S f) e c).
  Proof.
    assert (H : wok (fun _ => True) (exec (S f) e c)).
    { destruct c as [x|y|x v k|v a b|x n k|x ok n k|x n k|n v k|x v k|x mo q b k|x b k|b k|n k|b k|v k]; cbn [exec].
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - wgo IH.
      - apply wok_on_value. intros z. apply wok_new_node;
          [apply _|intros ?; reflexivity|intros ?; reflexivity|intros ?; reflexivity|intros ?; reflexivity
          |intros ? Hx0; first [reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ? Hx0; first [split; reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ? Hx0; first [split_and!; reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ?; simpl; discriminate
          |intros ?; repeat kgo1 (K_ok_all f)|intros t].
        apply wok_any. wrih IH.
      - apply wok_new_node;
          [apply _|intros ?; reflexivity|intros ?; reflexivity|intros ?; reflexivity|intros ?; reflexivity
          |intros ? Hx0; first [reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ? Hx0; first [split; reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ? Hx0; first [split_and!; reflexivity|(simpl in Hx0; discriminate Hx0)]
          |intros ?; simpl; discriminate
          |intros ?; repeat kgo1 (K_ok_all f)|intros t].
        apply wok_bind; [apply _|apply wok_current_owner|intros owner]. destruct owner as [o|]; [|apply wok_return].
        apply wok_bind; [apply _|apply wok_any; wrih IH|intros _]. apply wok_any. wrih IH.
      - exact (Wnew_effect x b k e).
      - apply wok_bind; [apply _|apply wok_current_owner|intros owner]. destruct owner as [o|]; [|apply wok_return].
        apply wok_get_owner_at; [intros ?; repeat kgo1 (K_ok_all f)|intros x0 Hx0].
        apply wok_bind_keep; [apply _|intros s0 s1 a0 E0 Q0; unfold m_modify in E0; injection E0 as <- _; exact Q0|wmod|intros _].
        apply wok_bind_left; [apply _| |intros _; apply wok_any; wrih IH].
        destruct (o_disposed x0); [apply wok_any; wgo IH|wlists].
      - wgo IH.
      - wgo IH.
      - wgo IH. }
    exact (proj2 (proj2 H)).
  Qed.
End WStep2.

End WX2.

Lemma W_ok_all f : forall X, W_ok X f.
Proof.
  induction f as [|f IH]; intros X; [exact (W_ok_0 X)|]. split.
  - exact (Wexec_S f IH).
  - exact (Wread_S f IH).
  - exact (Wnode_S f IH).
  - exact (Wrop_S f IH).
  - exact (Wpull_S f IH).
  - exact (Wexe_S f).
  - exact (Wret_S f IH).
  - exact (Wdsc_S f IH).
  - exact (Wtear_S f IH).
  - exact (Wcl_S f IH).
  - exact (Wrel_S f IH).
  - exact (Wdisp_S f IH).
  - exact (Watt_S f IH).
  - exact (Wwrite_S f IH).
  - exact (Wreq_S f IH).
  - exact (Wset_S f IH).
  - exact (Wflush_S f IH).
  - exact (Wdrain_S f IH).
Qed.

(** ** The whole graph *)

Notation wk0 := (wkx (fun _ => False)).

Lemma whole_init : wk0 (init current_flags).
Proof.
  split; [split|split].
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros n d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
  - intros c Hc. simpl in Hc. set_solver.
  - intros m dm Hm. by apply not_elem_of_nil in Hm.
  - split_and!.
    + intros c Hc. simpl in Hc. set_solver.
    + intros c d Hd. simpl in Hd. rewrite lookup_empty in Hd. done.
    + split; [simpl; apply lookup_empty|]. intros o y Hy. simpl in Hy. destruct (decide (o = root_owner_id)) as [->|Hne].
      * rewrite lookup_insert_eq in Hy. injection Hy as <-. simpl. split; [by left|]. intros d Hd. rewrite lookup_empty in Hd. done.
      * rewrite lookup_insert_ne, lookup_empty in Hy by done. done.
Qed.

Lemma reach_core f c s r : run_top f c (init current_flags) = Some (s, r) -> run_core s.
Proof.
  intros Hm. exact (proj1 (Pexec _ _ (engine_settled_preserved f) [] c _ _ _ engine_settled_init I Hm)).
Qed.

Lemma reach_wk f c s r : run_top f c (init current_flags) = Some (s, r) -> good s -> wk0 s.
Proof.
  intros Hm Hg. destruct engine_settled_init as [Hc0 _]. exact (Wexec _ (W_ok_all f _) [] c _ _ _ Hc0 whole_init I Hm Hg).
Qed.

Lemma pure_not_running s t : run_core s -> wk0 s -> pure_in (shapes s) t -> t ∉ g_running s.
Proof. intros Hc Hw Hp. exact (np_pure s t (proj1 (proj2 Hc)) (proj1 (proj2 Hw)) Hp). Qed.

(**md
**A well-behaved program keeps `gh_passed` empty.**

After a whole program from a new graph, `gh_passed` is empty: each computation that resolved `Clean` did so over sources that were clean, disposed or holding their flag. `wake_ok` follows: [`well_behaved_no_lost_wakeups`](#well_behaved_no_lost_wakeups).

Premises: the program's end state has every memo outside the self-dirtying case, and every effect only reads. A `CmpNever` comparer falsifies the conclusion: [`cmp_never_passes_source`](Ranvier.Pure.Witnesses.html#cmp_never_passes_source). So does clearing `f_unannounced`: [`witness_302`](Ranvier.Pure.Witnesses.html#witness_302). An effect that writes or registers a cleanup is unproved either way.
*)
Theorem well_behaved_nothing_passed f c s r :
  run_top f c (init current_flags) = Some (s, r) -> well_behaved_program s -> effects_only_read s -> nothing_passed s.
Proof.
  intros Hm Ha He. destruct (reach_wk f c s r Hm (conj Ha He)) as [Hw _].
  unfold nothing_passed. apply leibniz_equiv. intros x. split; [|set_solver].
  intros Hx. destruct (w_passed _ Hw x Hx) as [_ K]. exfalso. exact (K Ha).
Qed.

(**md
**A well-behaved program leaves no lost wake-up.**

Every observer of a live stale source is stale or queued, or on the running stack, or the source holds its announcement flag.

Premises: as [`well_behaved_nothing_passed`](#well_behaved_nothing_passed).
*)
Corollary well_behaved_no_lost_wakeups f c s r :
  run_top f c (init current_flags) = Some (s, r) -> well_behaved_program s -> effects_only_read s -> wake_ok s.
Proof.
  intros Hm Ha He. refine (no_lost_wakeups_when_nothing_passed f c _ s r engine_settled_init Hm _).
  exact (well_behaved_nothing_passed f c s r Hm Ha He).
Qed.

Lemma body_eval_mono_q (Q : node_id -> Prop) (A B : node_id -> option outcome) e c r :
  body_pure Q e c -> (forall t o, Q t -> A t = Some o -> B t = Some o) -> body_eval A e c = Some r -> body_eval B e c = Some r.
Proof.
  intros Hp H. revert e Hp r. induction c; intros g Hp r0; simpl in *; try done.
  - destruct Hp as [_ Hk]. destruct (ev g e) as [[z|y]|]; [|done|done]. apply IHc, (Hk z eq_refl).
  - destruct Hp as [_ Hab]. destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (Hab z eq_refl) as [Hb Ha].
    destruct (decide _) as [Hz|Hz]; [apply IHc2, (Hb Hz)|apply IHc1, (Ha Hz)].
  - destruct Hp as (_&Ht&Hk). destruct (ev g n) as [[z|y]|] eqn:Ev; [|done|done]. destruct (decide _) as [Hz|Hz]; [|done].
    pose proof (fun o => H _ o (Ht z eq_refl Hz)) as Hq.
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (Hq _ eq_refl); apply IHc, Hk|intros [=<-]; by rewrite (Hq _ eq_refl)|done].
  - destruct Hp as (_&Ht&Hk1&Hk2). destruct (ev g n) as [[z|y]|] eqn:Ev; [|done|done]. destruct (decide _) as [Hz|Hz]; [|done].
    pose proof (fun o => H _ o (Ht z eq_refl Hz)) as Hq.
    destruct (A _) as [[v|y]|] eqn:E; [rewrite (Hq _ eq_refl); apply IHc, Hk1|rewrite (Hq _ eq_refl); apply IHc, Hk2|done].
  - destruct Hp as [Hb Hk]. destruct (body_eval A g c1) as [[z|y]|] eqn:E.
    + rewrite (IHc1 g Hb _ E). apply IHc2, Hk.
    + intros [=<-]. by rewrite (IHc1 g Hb _ E).
    + done.
  - destruct Hp as [_ Hk]. destruct (ev g e) as [[z|y]|]; [|done|done]. apply IHc, Hk.
Qed.

Lemma den_readable k s n o : den k s n = Some o -> readable (shapes s) n.
Proof.
  destruct k as [|k]; [done|]. cbn [den]. unfold readable, shapes. rewrite lookup_fmap. destruct (g_nodes s !! n) as [d|]; [|done].
  simpl. unfold shape_of. destruct (n_kind d); done.
Qed.

Lemma body_eval_ext (A B : node_id -> option outcome) e c :
  (forall t, A t = B t) -> body_eval A e c = body_eval B e c.
Proof.
  intros H. revert e. induction c; intros g; simpl; try done.
  all: try rewrite IHc1; try rewrite IHc2; try rewrite IHc; try rewrite H; repeat first [rewrite IHc | rewrite H | done | destruct (ev _ _) as [[?|?]|] | destruct (decide _) | destruct (B _) as [[?|?]|] | destruct (body_eval _ _ _) as [[?|?]|]].
Qed.

Lemma den_nodes k s s' n : g_nodes s' = g_nodes s -> den k s' n = den k s n.
Proof.
  intros H. revert n. induction k as [|k IH]; intros n; [done|]. cbn [den]. rewrite H.
  destruct (g_nodes s !! n) as [d|]; [|done]. destruct (n_kind d); try done. destruct (n_disposed d); [done|].
  by rewrite (body_eval_ext (den k s') (den k s)) by exact IH.
Qed.

(** What a pure node denotes after a call below a stack, it denoted before the call. *)
Lemma den_atom (T : node_id -> Prop) lim x s s' :
  run_core s -> atom T lim x s s' -> (forall t, pure_in (shapes s) t -> ~ T t) ->
  forall k t o, pure_in (shapes s) t -> den k s' t = Some o -> den k s t = Some o.
Proof.
  intros Hc A HT. destruct (b_window _ (proj1 (proj2 Hc))) as (_&_&Wsig&_). pose proof (at_shapes _ _ _ _ _ A) as Hsh.
  induction k as [|k IH]; intros t o Hp E; [done|].
  destruct (pure_exists s t Hp) as [d Hd]. cbn [den] in E |- *. rewrite Hd.
  destruct (at_any _ _ _ _ _ A t d Hd) as (d'&Hd'&Di). rewrite Hd' in E.
  assert (Hshd : shape_of d' = shape_of d).
  { pose proof (shapes_lookup s t d Hd) as X. pose proof (shapes_lookup s' t d' Hd') as Y. rewrite Hsh, X in Y. congruence. }
  pose proof Hshd as Hs0. unfold shape_of in Hs0. injection Hs0 as Kk _ Ee Bb. rewrite Kk in E.
  destruct (pure_node s t d Hp Hd) as [Hk|(Hk&_&Hb)].
  - rewrite Hk in E |- *. destruct (at_keep _ _ _ _ _ A t d Hd Hp (HT t Hp) (or_introl (or_introl (Wsig t d Hd Hk)))) as (e&He&V&_).
    rewrite Hd' in He. injection He as <-. by rewrite <- V.
  - rewrite Hk in E |- *. rewrite Di in E. destruct (n_disposed d) eqn:Ed.
    + destruct (at_keep _ _ _ _ _ A t d Hd Hp (HT t Hp) (or_intror Ed)) as (e&He&V&_).
      rewrite Hd' in He. injection He as <-. by rewrite <- V.
    + rewrite Ee, Bb in E. revert E. apply (body_eval_mono_q _ _ _ _ _ _ Hb). intros u v Hq0 Hu.
      pose proof (den_readable _ _ _ _ Hu) as Hr. rewrite Hsh in Hr. destruct (Hq0 Hr) as [_ Hpu]. exact (IH u v Hpu Hu).
Qed.

(** The state a whole program ends in, as a read sees it. *)
Lemma reach_top f c s r :
  run_top f c (init current_flags) = Some (s, r) ->
  g_current s = None /\ g_pull_depth s = 0 /\ g_batch_depth s = 0 /\ g_untracked s = false /\ g_flushing s = false /\ g_flush_owed s = false.
Proof.
  intros Hm.
  destruct (Sexec _ (sched_ok_all f) None [] c _ _ _ (sched_inv_init current_flags) I Hm) as [(_&_&C4) ([R1 R2 R3 R4 R5 _]&_)].
  simpl in R1, R2, R3, R4, R5. split_and!; try done.
  destruct (g_flush_owed s) eqn:Eo; [|done]. exfalso. destruct (C4 eq_refl) as [X|[X|[X|X]]].
  - rewrite R4 in X. lia.
  - rewrite R3 in X. lia.
  - rewrite R1 in X. by destruct X.
  - congruence.
Qed.

(**md
**A read answers from the current signals, in a well-behaved program.**

After a whole program from a new graph, a read of a node outside the self-dirtying case answers what the node denotes over the current signals: a signal its value, a live memo its body, with each read in that body answered the same way, and a disposed memo what it stored, as Core.fs 4372-4376 documents.

Premises: the program's end state has every memo outside the self-dirtying case and every effect only reading, and the read returns. `pure_in` is the positive form of `~ self_dirtying`: it quantifies over every answer of a read, so it has no decision, and the negated form gives no proof of it without an axiom.

The statement speaks at a program's end. A read in an open batch or a body is unstated. A read here can settle a flush, and the answer holds across it.

The form over the node alone, with its other nodes free, is false: [`read_settles_writer`](Ranvier.Pure.Witnesses.html#read_settles_writer). At quiescence that form, `read_quiescent_statement`, is unproved with no counterexample known. The form with no exception for a disposed memo is false: [`glitch_free_read_falsified`](Ranvier.Pure.Witnesses.html#glitch_free_read_falsified).
*)
Theorem glitch_free_read_stored f f' c s r n s' o :
  run_top f c (init current_flags) = Some (s, r) -> well_behaved_program s -> effects_only_read s -> pure_in (shapes s) n ->
  read f' n true s = Some (s', o) -> denotes s n o.
Proof.
  intros Hm Ha He Hp Hrd.
  pose proof (reach_core f c s r Hm) as Hc. pose proof (reach_wk f c s r Hm (conj Ha He)) as Hw.
  destruct (reach_top f c s r Hm) as (Hcur&Hpl&Hbt&Hun&Hfl&How).
  assert (Hnp : forall m, m ∈ g_running s -> ~ pure_in (shapes s) m) by (intros m Hin K; exact (pure_not_running s m Hc Hw K Hin)).
  destruct f' as [|f']; [done|]. cbn [read] in Hrd. destruct f' as [|f']; [done|]. cbn [read_node] in Hrd.
  destruct (pure_exists s n Hp) as [d Hd]. rewrite (bind_get_node _ _ _ _ Hd) in Hrd.
  assert (Htrack : track n s = Some (s, tt)) by (unfold track; by rewrite Hcur).
  destruct (n_kind d) eqn:Ek.
  - rewrite (bind_some _ _ _ _ _ Htrack) in Hrd. unfold m_return in Hrd. injection Hrd as <- <-.
    destruct (b_window _ (proj1 (proj2 Hc))) as (_&_&Wsig&_).
    pose proof (quiet_denotes s (proj1 Hw) Hc Hnp n d Hd Hp (or_introl (or_introl (Wsig n d Hd Ek)))) as Hden.
    exists (S n). rewrite Hden. unfold view_answer, read_view_of. simpl. rewrite (proj1 (w_signal _ (proj1 Hw) n d Hd Ek)). done.
  - apply bind_inv in Hrd as (s2' & [] & H1 & Hrd).
    destruct (stale_live d) eqn:El.
    + assert (Hl : n_disposed d = false) by (unfold stale_live in El; destruct (n_disposed d); [by rewrite andb_false_r in El|done]).
      apply bind_inv in H1 as (s1 & a1 & Hrp & H1).
      pose proof Hrp as Hrp0.
      destruct f' as [|f']; [done|]. cbn [refresh_or_pull] in Hrp. rewrite bind_get in Hrp.
      assert (Hdf : deferring s = false) by (unfold deferring; rewrite Hcur, Hpl; done).
      rewrite Hdf in Hrp. destruct f' as [|f']; [done|]. cbn [pull] in Hrp. rewrite bind_m_modify in Hrp.
      set (s0 := with_pull_depth (S (g_pull_depth s)) s) in Hrp.
      assert (H0 : same4 s s0) by done.
      apply bind_inv in Hrp as (sb & a2 & Hrf & Hrp). rewrite bind_m_modify, bind_get in Hrp.
      assert (Hc0 : run_core s0) by (by apply core_pull).
      assert (Hw0 : wk0 s0) by (apply (wk_same s); [split_and!; done|exact Hw]).
      assert (Hdf0 : deferring s0 = true) by (unfold deferring; change (g_current s) with (g_current s0) in Hcur; rewrite Hcur; apply bool_decide_eq_true_2; simpl; lia).
      pose proof (stk_empty (S n) s0 (proj1 (proj2 Hc0)) Hw0 (or_introl Hdf0)) as St.
      assert (Hp0 : pure_in (shapes s0) n) by exact Hp.
      assert (Hd0 : g_nodes s0 !! n = Some d) by exact Hd.
      destruct (Aref _ (A_ok_all f') (fun _ => False) (S n) n s0 sb a2 d (conj Hc0 (conj (proj1 Hw0) St)) ltac:(lia) Hp0 Hd0 Ek Hl Hrf) as ((Hcb&Hwb&_)&A&[Dq _]).
      pose proof (wk_of_atom _ _ _ _ Hw0 A Hwb) as Hwb'.
      destruct Dq as (e&He1&Qe).
      set (s2 := with_pull_depth (pred (g_pull_depth sb)) sb) in Hrp.
      assert (Hek : exists e1, g_nodes s1 !! n = Some e1 /\ read_view_of e1 = read_view_of e).
      { destruct (bool_decide (g_pull_depth s2 = 0) && g_flush_owed s2).
        - assert (Hc2 : run_core s2) by (by apply core_pull).
          assert (Hw2 : wk0 s2) by (apply (wk_same sb); [split_and!; done|exact Hwb']).
          assert (Hsh2 : shapes s2 = shapes s) by (change (shapes sb = shapes s0); exact (at_shapes _ _ _ _ _ A)).
          pose proof (good_shapes s s2 Hsh2 (conj Ha He)) as Hg2.
          destruct (settle_fw f' s2 s1 a1 Hc2 Hw2 Hg2 Hrp) as [_ [_ Hk3]].
          destruct (Hk3 n e He1 ltac:(by rewrite Hsh2) Qe) as (e'&He'&V&_). exists e'. done.
        - unfold m_return in Hrp. injection Hrp as <- _. exists e. done. }
      destruct Hek as (e1&He2&Ve1).
      assert (Hsi : sched_inv None s) by exact (sched_inv_run_top f c _ s r (sched_inv_init current_flags) Hm).
      assert (Hc1cur : g_current s1 = None).
      { destruct (Srop _ (sched_ok_all _) None n _ _ _ Hsi (ex_intro _ d (conj Hd Ek)) Hrp0) as [_ ([R1 _ _ _ _ _]&_)]. rewrite R1. exact Hcur. }
      assert (Ht1 : track n s1 = Some (s1, tt)) by (unfold track; by rewrite Hc1cur).
      rewrite (bind_some _ _ _ _ _ Ht1), (bind_get_node _ _ _ _ He2) in H1.
      assert (Hs2 : s2' = s1).
      { destruct (stale_live e1); [unfold mark_reader in H1; rewrite Hc1cur in H1|]; unfold m_return in H1; by injection H1 as <-. }
      subst s2'. rewrite (bind_get_node _ _ _ _ He2) in Hrd. destruct (read_answer _ _ _ _ Hrd) as [_ ->]. rewrite Ve1.
      assert (Hrnb : forall m, m ∈ g_running sb -> ~ pure_in (shapes sb) m).
      { intros m Hin. rewrite (at_shapes _ _ _ _ _ A). rewrite (at_running _ _ _ _ _ A) in Hin. exact (Hnp m Hin). }
      assert (Hpb2 : pure_in (shapes sb) n) by (rewrite (at_shapes _ _ _ _ _ A); exact Hp0).
      pose proof (quiet_denotes sb Hwb Hcb Hrnb n e He1 Hpb2 Qe) as Hden.
      exists (S n). rewrite <- (den_nodes (S n) s s0 n eq_refl). exact (den_atom _ _ _ s0 sb Hc0 A (fun _ _ K => K) (S n) n _ Hp0 Hden).
    + unfold stale_live in El.
      assert (Hq0 : quiet d \/ n_disposed d = true).
      { destruct (stale (n_freshness d)) eqn:Es; simpl in El.
        - right. destruct (n_disposed d); done.
        - left. left. destruct (n_freshness d); done. }
      rewrite Htrack in H1. injection H1 as <-.
      rewrite (bind_get_node _ _ _ _ Hd) in Hrd. destruct (read_answer _ _ _ _ Hrd) as [_ ->].
      exists (S n). exact (quiet_denotes s (proj1 Hw) Hc Hnp n d Hd Hp Hq0).
  - done.
Qed.

Lemma count_runs_view c pre l tr : trace_view c tr = pre ++ EvRun c :: l -> 0 < count_runs c tr.
Proof.
  intros H. assert (Hin : EvRun c ∈ trace_view c tr) by (rewrite H; set_solver).
  unfold trace_view in Hin. apply list_elem_of_filter in Hin as [_ Hin].
  clear H. induction tr as [|x tr IH]; [by apply not_elem_of_nil in Hin|]. apply elem_of_cons in Hin as [Hx|Hin].
  - subst x. simpl. rewrite decide_True by done. lia.
  - specialize (IH Hin). destruct x; simpl; try done. lia.
Qed.

(** Each live effect is [Clean] and ran, read what its sources denote, and logged what its body logs over them. *)
Definition converged (s : state) : Prop :=
  forall e d, g_nodes s !! e = Some d -> n_kind d = KEffect -> n_disposed d = false ->
    n_freshness d = Clean /\ 0 < count_runs e (g_trace s) /\
    (forall t, t ∈ read_prefix (g_edges s) e -> seen_of (g_ghost s) e t <> [] /\
       forall v, v ∈ seen_of (g_ghost s) e t -> denotes s t (view_answer v)) /\
    exists r' k,
      body_eval (seen_ans s e) (n_env d) (n_body d) = Some r' /\ body_eval (den k s) (n_env d) (n_body d) = Some r' /\
      body_logs (seen_ans s e) (n_env d) (n_body d) = Some (last_run_logs e (g_trace s)) /\
      body_logs (den k s) (n_env d) (n_body d) = Some (last_run_logs e (g_trace s)).

(**md
**Convergence: at quiescence, every effect ran, and its last run read and logged what its sources now denote.**

After a whole program from a new graph that leaves no live queue entry and nothing running, each live effect is `Clean` and has an `EvRun` in the trace. Each source read in its last run has a recorded read, each answering what the source denotes over the current signals. The effect's body, with each read answered from its recorded reads, gives the same result as with each read answered by the denotation, and its logs over the denotation are the `EvLog`s of its last run.

Premises: the program's end state has every memo outside the self-dirtying case, and every effect only reads. Batches and computed read targets are inside the premises, in memos and in effects: [`widened_forms_reached`](Ranvier.Pure.Witnesses.html#widened_forms_reached).

Exclusions from an effect's body:

- A write rests on a counterexample: [`effect_resolves_over_moved_source`](Ranvier.Pure.Witnesses.html#effect_resolves_over_moved_source) falsifies the conclusion ([`effects_only_read_needed`](Ranvier.Pure.Witnesses.html#effects_only_read_needed)). This premise is provisional, pending the owner's ruling on that entry.
- An untracked read: the logs clause is false over it, since the source can move with no run. A weaker form is unproved with no counterexample.
- A cleanup, and an effect or other node created in the body, rest on the proof's reach: unproved with no counterexample.

`quiescent` excludes the queue `failed_batch_leaves_queue` leaves. [`convergence_reached`](Ranvier.Pure.Witnesses.html#convergence_reached) reaches every premise on a live effect over live memos. The form over all programs is the Open `convergence_all_statement`.
*)
Theorem convergence f c s r :
  run_top f c (init current_flags) = Some (s, r) -> well_behaved_program s -> effects_only_read s -> quiescent s ->
  converged s.
Proof.
  intros Hm Ha He [Hpen Hrn] e d Hd Hk Hl.
  pose proof (reach_core f c s r Hm) as Hc. pose proof (reach_wk f c s r Hm (conj Ha He)) as Hw.
  pose proof (Pexec _ _ (engine_settled_preserved f) [] c _ _ _ engine_settled_init I Hm) as Hset.
  pose proof (sched_inv_run_top f c _ s r (sched_inv_init current_flags) Hm) as (Hsd&_).
  assert (Hcl : n_freshness d = Clean).
  { destruct (stale (n_freshness d)) eqn:Es.
    - exfalso. pose proof (sd_stale _ _ Hsd e d Hd Hk Hl Es) as Hq.
      destruct (proj2 (sd_agree _ _ Hsd e d Hd)) as [Hx _]. destruct (Hx Hq) as [K|K]; [rewrite Hpen in K; by apply not_elem_of_nil in K|done].
    - destruct (n_freshness d); done. }
  assert (Hq : qc s e d) by (right; exact (He e d Hd Hk)).
  assert (Hnr : forall m, m ∈ g_running s -> ~ pure_in (shapes s) m) by (intros m Hin; rewrite Hrn in Hin; by apply not_elem_of_nil in Hin).
  assert (Hnrt : forall m, m ∉ g_running s) by (intros m; rewrite Hrn; apply not_elem_of_nil).
  assert (Hnp : nothing_passed s) by exact (well_behaved_nothing_passed f c s r Hm Ha He).
  assert (Hsrc : forall t, t ∈ read_prefix (g_edges s) e -> seen_of (g_ghost s) e t <> [] /\
            exists x, g_nodes s !! t = Some x /\ (forall v, v ∈ seen_of (g_ghost s) e t -> v = read_view_of x) /\ den (S t) s t = Some (view_answer (read_view_of x))).
  { intros t Ht.
    assert (Hsl : t ∈ (slot_view (g_edges s) e).1) by exact (read_prefix_in' _ _ _ Ht).
    destruct (w_slots _ (proj1 Hw) e d Hd Hq t Hsl) as [_ Hpt].
    assert (Hst : settled d) by (left; by rewrite Hcl).
    destruct (w_reads _ (proj1 Hw) e d Hd Hq Hl (Hnrt e) Hst t Hsl (Hnrt t)) as [Hne (x&Hx&Hall)].
    split; [done|]. exists x. split_and!; [done|done|].
    assert (Hqx : quiet x \/ n_disposed x = true).
    { destruct (quiet_dec x) as [Q|Q]; [by left|right]. destruct (n_disposed x) eqn:Ed; [done|exfalso].
      assert (Hsx : stale (n_freshness x) = true).
      { destruct (n_freshness x) eqn:Ef; try done. exfalso. apply Q. by left. }
      destruct (proj2 (proj2 Hc) true e t d x I Hd Hx Ht Hsx Ed Hl) as [K|[K|[K|K]]].
      - rewrite Hcl in K. done.
      - apply Q. right. apply K.
      - simpl in K. unfold nothing_passed in Hnp. rewrite Hnp in K. set_solver.
      - destruct K as (w&Kw&Kr). pose proof (proj2 Hset e d w Hd Kw Hl). lia. }
    exact (quiet_denotes s (proj1 Hw) Hc Hnr t x Hx Hpt Hqx). }
  split_and!; [done| | |].
  - destruct (proj1 (proj2 (proj2 (proj2 Hw))) e d Hd (He e d Hd Hk) Hl ltac:(by rewrite Hcl)) as [[K _]|(r0&l&pre&_&_&K)]; [done|].
    exact (count_runs_view _ _ _ _ K).
  - intros t Ht. destruct (Hsrc t Ht) as [A (x&Hx&B&C)]. split; [done|]. intros v Hv. rewrite (B v Hv). by exists (S t).
  - destruct (proj1 (proj2 (proj2 (proj2 Hw))) e d Hd (He e d Hd Hk) Hl ltac:(by rewrite Hcl)) as [[K _]|(r0&l&pre&A&B&C)]; [done|].
    rewrite (last_run_logs_view _ _ _ _ C).
    assert (Hm5 : forall t o, seen_ans s e t = Some o -> den (S (g_next_id s)) s t = Some o).
    { intros t o Ho. pose proof (seen_ans_prefix _ _ _ _ Ho) as Hpre. destruct (Hsrc t Hpre) as [N (x&Hx&H1&H2)].
      unfold seen_ans in Ho. rewrite decide_True in Ho by done. destruct (seen_of (g_ghost s) e t) as [|v l0] eqn:Es; [done|].
      simpl in Ho. injection Ho as <-. rewrite (H1 v) by left. eapply den_mono; [|exact H2].
      pose proof (core_ids s Hc t (mk_is_Some _ _ Hx)). lia. }
    exists r0, (S (g_next_id s)). split_and!; [done|exact (body_eval_mono _ _ _ _ _ Hm5 A)|done|exact (body_logs_mono _ _ _ _ _ Hm5 B)].
Qed.

(** ** Deciding the premises on a concrete state *)

Definition well_behaved_b (s : state) : bool :=
  forallb (fun x : node_id * node =>
    match n_kind x.2 with KMemo => pure_b (shapes s) (Datatypes.S x.1) x.1 | _ => true end) (map_to_list (g_nodes s)).

Definition effects_only_read_b (s : state) : bool :=
  forallb (fun x : node_id * node =>
    match n_kind x.2 with
    | KEffect => body_ok (fun t => pure_b (shapes s) (Datatypes.S t) t) (static_of (n_env x.2)) (n_body x.2)
    | _ => true
    end) (map_to_list (g_nodes s)).

Lemma well_behaved_of_b s : well_behaved_b s = true -> well_behaved_program s.
Proof.
  intros H n d Hd Hk. unfold well_behaved_b in H. rewrite forallb_forall in H.
  assert (Hin : In (n, d) (map_to_list (g_nodes s))) by (apply list_elem_of_In; apply elem_of_map_to_list; exact Hd).
  specialize (H _ Hin). simpl in H. rewrite Hk in H. exact (pure_b_sound (shapes s) (Datatypes.S n) n H).
Qed.

Lemma effects_only_read_of_b s : effects_only_read_b s = true -> effects_only_read s.
Proof.
  intros H n d Hd Hk. unfold effects_only_read_b in H. rewrite forallb_forall in H.
  assert (Hin : In (n, d) (map_to_list (g_nodes s))) by (apply list_elem_of_In; apply elem_of_map_to_list; exact Hd).
  specialize (H _ Hin). simpl in H. rewrite Hk in H. split; [exact Hk|].
  eapply body_ok_pure; [|apply static_agrees_of|exact H]. intros t Ht _. exact (pure_b_sound (shapes s) (Datatypes.S t) t Ht).
Qed.

(** The grammar of [body_pure], with its reads free. *)
Fixpoint body_shape (c : cmd) : bool :=
  match c with
  | CRet x => expr_plain x
  | CThrow _ => true
  | CLet _ v k => expr_plain v && body_shape k
  | CIf v a b => expr_plain v && body_shape a && body_shape b
  | CRead _ n k | CTryRead _ _ n k => expr_plain n && body_shape k
  | CLog v k => expr_plain v && body_shape k
  | CBatch b k => body_shape b && body_shape k
  | _ => false
  end.

Lemma body_shape_pure (Q : node_id -> Prop) e c : (forall t, Q t) -> body_shape c = true -> body_pure Q e c.
Proof.
  intros HQ. revert e. induction c; intros g; simpl; try done; rewrite ?andb_true_iff.
  - intros [H1 H2]. split; [done|]. intros z _. auto.
  - intros [[H1 H2] H3]. split; [done|]. intros z _. split; intros _; auto.
  - intros [H1 H2]. split_and!; [done|intros; apply HQ|intros v; auto].
  - intros [H1 H2]. split_and!; [done|intros; apply HQ|intros v; auto|intros v; auto].
  - intros [H1 H2]. split; auto.
  - intros [H1 H2]. split; auto.
Qed.

Lemma readable_pure s t : well_behaved_program s -> readable (shapes s) t -> pure_in (shapes s) t.
Proof.
  intros Ha Hr. destruct (readable_node _ _ Hr) as [d Hd]. unfold readable in Hr. rewrite (shapes_lookup s t d Hd) in Hr.
  unfold shape_of in Hr. destruct (n_kind d) eqn:Ek; [|exact (Ha t d Hd Ek)|done].
  unfold pure_in. rewrite pure_at_unfold, (shapes_lookup s t d Hd). unfold shape_of. rewrite Ek. done.
Qed.

(** In a well-behaved program, the premise on effects is their grammar: their reads reach pure nodes whatever the targets compute. *)
Lemma effects_only_read_of_shape s :
  well_behaved_program s ->
  forallb (fun x : node_id * node => match n_kind x.2 with KEffect => body_shape (n_body x.2) | _ => true end) (map_to_list (g_nodes s)) = true ->
  effects_only_read s.
Proof.
  intros Ha H n d Hd Hk. rewrite forallb_forall in H.
  assert (Hin : In (n, d) (map_to_list (g_nodes s))) by (apply list_elem_of_In; apply elem_of_map_to_list; exact Hd).
  specialize (H _ Hin). simpl in H. rewrite Hk in H. split; [exact Hk|]. apply body_shape_pure; [|exact H].
  intros t Hr. exact (readable_pure s t Ha Hr).
Qed.

(** A test that a body is not pure, trying a sample of answers for each read. *)
Fixpoint refute (Q : node_id -> bool) (e : env) (c : cmd) : bool :=
  match c with
  | CRet x => negb (expr_plain x)
  | CThrow _ => false
  | CLet x v k => negb (expr_plain v) || match ev e v with Some (Ok z) => refute Q ((x, z) :: e) k | _ => false end
  | CIf v a b =>
    negb (expr_plain v) ||
    match ev e v with
    | Some (Ok z) => if decide (z = 0%Z) then refute Q e b else refute Q e a
    | _ => false
    end
  | CRead x n k =>
    negb (expr_plain n) ||
    match ev e n with
    | Some (Ok z) => bool_decide (0 <= z)%Z && Q (Z.to_nat z)
    | _ => false
    end || existsb (fun v => refute Q ((x, v) :: e) k) [0%Z; 1%Z; 2%Z; 3%Z]
  | CTryRead x ok n k =>
    negb (expr_plain n) ||
    match ev e n with
    | Some (Ok z) => bool_decide (0 <= z)%Z && Q (Z.to_nat z)
    | _ => false
    end || existsb (fun v => refute Q ((ok, 1%Z) :: (x, v) :: e) k) [0%Z; 1%Z; 2%Z; 3%Z]
  | CLog v k => negb (expr_plain v) || refute Q e k
  | CBatch b k => refute Q e b || refute Q e k
  | _ => true
  end.

Lemma refute_sound (Q : node_id -> bool) (P : node_id -> Prop) e c :
  (forall t, Q t = true -> ~ P t) -> refute Q e c = true -> ~ body_pure P e c.
Proof.
  intros HQ. revert e. induction c; intros g; simpl; try done; try (intros _ K; exact K); rewrite ?orb_true_iff.
  - intros H H1. rewrite H1 in H. done.
  - intros H [H1 H2]. destruct H as [H|H]; [rewrite H1 in H; done|].
    destruct (ev g e) as [[z|y]|]; [|done|done]. exact (IHc _ H (H2 z eq_refl)).
  - intros H [H1 H2]. destruct H as [H|H]; [rewrite H1 in H; done|].
    destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (H2 z eq_refl) as [Hb Ha].
    destruct (decide _) as [Hz|Hz]; [exact (IHc2 _ H (Hb Hz))|exact (IHc1 _ H (Ha Hz))].
  - intros H (H1&H2&H3). destruct H as [[H|H]|H]; [rewrite H1 in H; done| |].
    + destruct (ev g n) as [[z|y]|]; [|done|done]. apply andb_true_iff in H as [Hz H]. apply bool_decide_eq_true_1 in Hz. exact (HQ _ H (H2 z eq_refl Hz)).
    + destruct H as [H|[H|[H|[H|H]]]]; try done; exact (IHc _ H (H3 _)).
  - intros H (H1&H2&H3&_). destruct H as [[H|H]|H]; [rewrite H1 in H; done| |].
    + destruct (ev g n) as [[z|y]|]; [|done|done]. apply andb_true_iff in H as [Hz H]. apply bool_decide_eq_true_1 in Hz. exact (HQ _ H (H2 z eq_refl Hz)).
    + destruct H as [H|[H|[H|[H|H]]]]; try done; exact (IHc _ H (H3 _)).
  - intros H [H1 H2]. destruct H as [H|H]; [exact (IHc1 _ H H1)|exact (IHc2 _ H H2)].
  - intros H [H1 H2]. destruct H as [H|H]; [rewrite H1 in H; done|exact (IHc _ H H2)].
Qed.

Definition readable_b (S : gmap node_id (kind * comparer * env * cmd)) (t : node_id) : bool :=
  match S !! t with
  | Some (KSignal, _, _, _) | Some (KMemo, _, _, _) => true
  | _ => false
  end.

(** A sufficient test that a node is self-dirtying. *)
Fixpoint bad_b (S : gmap node_id (kind * comparer * env * cmd)) (k : nat) (n : node_id) : bool :=
  match k with
  | 0 => true
  | Datatypes.S k' =>
    match S !! n with
    | Some (KSignal, _, _, _) => false
    | Some (KMemo, CmpEq, e, b) => refute (fun t => readable_b S t && (bool_decide (n <= t) || bad_b S k' t)) e b
    | _ => true
    end
  end.

Lemma readable_b_sound S t : readable_b S t = true -> readable S t.
Proof. unfold readable_b, readable. destruct (S !! t) as [[[[[] ?] ?] ?]|]; done. Qed.

Lemma bad_b_sound S : forall k n, bad_b S k n = true -> ~ pure_at S k n.
Proof.
  induction k as [|k IH]; intros n; [simpl; tauto|]. simpl. destruct (S !! n) as [[[[[] [| |]] e] b]|]; try done; try (intros _ K; exact K).
  intros H. refine (refute_sound _ _ _ _ _ H). intros t Ht K.
  apply andb_true_iff in Ht as [H1 H2]. destruct (K (readable_b_sound _ _ H1)) as [K1 K2].
  apply orb_true_iff in H2 as [H2|H2]; [apply bool_decide_eq_true_1 in H2; lia|exact (IH t H2 K2)].
Qed.

Lemma dirtying_b s n : bad_b (shapes s) (Datatypes.S n) n = true -> self_dirtying s n.
Proof. intros H K. exact (bad_b_sound (shapes s) (Datatypes.S n) n H K). Qed.

Lemma not_dirtying_b s n : pure_b (shapes s) (Datatypes.S n) n = true -> ~ self_dirtying s n.
Proof. intros H K. exact (K (pure_b_sound (shapes s) (Datatypes.S n) n H)). Qed.

Lemma not_quiet_b s c d :
  refute (fun t => readable_b (shapes s) t && bad_b (shapes s) (Datatypes.S t) t) (n_env d) (n_body d) = true -> ~ quiet_effect s c d.
Proof.
  intros H [_ K]. refine (refute_sound _ _ _ _ _ H K). intros t Ht K2. apply andb_true_iff in Ht as [H1 H2].
  exact (bad_b_sound (shapes s) (Datatypes.S t) t H2 (K2 (readable_b_sound _ _ H1))).
Qed.

(** ** The read with no exception *)

(** What a node's body computes over the current signals, disposed or not. *)
Fixpoint body_den (k : nat) (s : state) (n : node_id) : option outcome :=
  match k with
  | 0 => None
  | Datatypes.S k' =>
    match g_nodes s !! n with
    | None => None
    | Some d =>
      match n_kind d with
      | KSignal => Some (view_answer (read_view_of d))
      | KEffect => None
      | KMemo => body_eval (body_den k' s) (n_env d) (n_body d)
      end
    end
  end.

(** The first statement of the read: a read of a node outside the self-dirtying case answers what its body computes over
    the current signals. [disposed_stale_source_read] is its counterexample, and Core.fs 4372-4376 documents that answer. *)
Definition glitch_free_read_statement : Prop :=
  forall f f' c s r n s' o,
    run_top f c (init current_flags) = Some (s, r) -> ~ self_dirtying s n ->
    read f' n true s = Some (s', o) -> exists k, body_den k s n = Some o.

(** The read for a pure node, over a program whose other nodes are free. A read can settle a flush, and an effect in it can
    write a source of the node, so this needs a state with nothing queued: [read_settles_writer] is the counterexample. *)
Definition read_per_node_statement : Prop :=
  forall f f' c s r n s' o,
    run_top f c (init current_flags) = Some (s, r) -> pure_in (shapes s) n ->
    read f' n true s = Some (s', o) -> denotes s n o.

(** The same at quiescence. Unproved, and no counterexample is known. *)
Definition read_quiescent_statement : Prop :=
  forall f f' c s r n s' o,
    run_top f c (init current_flags) = Some (s, r) -> quiescent s -> pure_in (shapes s) n ->
    read f' n true s = Some (s', o) -> denotes s n o.

(** ** The statement over all programs *)

(** A memo's body with its writes and cleanups erased and its untracked reads as reads: what it computes from the signals. *)
Fixpoint erase (c : cmd) : cmd :=
  match c with
  | CLet x v k => CLet x v (erase k)
  | CIf v a b => CIf v (erase a) (erase b)
  | CRead x n k => CRead x n (erase k)
  | CTryRead x ok n k => CTryRead x ok n (erase k)
  | CReadUntracked x n k => CRead x n (erase k)
  | CWrite _ _ k => erase k
  | CCleanup _ k => erase k
  | CBatch b k => CBatch (erase b) (erase k)
  | CLog v k => CLog v (erase k)
  | _ => c
  end.

(** What a node's erased body computes over the current signals, and a disposed memo's stored answer. *)
Fixpoint den_all (k : nat) (s : state) (n : node_id) : option outcome :=
  match k with
  | 0 => None
  | Datatypes.S k' =>
    match g_nodes s !! n with
    | None => None
    | Some d =>
      match n_kind d with
      | KSignal => Some (view_answer (read_view_of d))
      | KEffect => None
      | KMemo =>
        if n_disposed d then Some (view_answer (read_view_of d))
        else body_eval (den_all k' s) (n_env d) (erase (n_body d))
      end
    end
  end.

Definition denotes_all (s : state) (n : node_id) (o : outcome) : Prop := exists k, den_all k s n = Some o.

Lemma den_all_S k s n o : den_all k s n = Some o -> den_all (Datatypes.S k) s n = Some o.
Proof.
  revert n o. induction k as [|k IH]; intros n o; [done|]. simpl (den_all (Datatypes.S k)). intros Hd.
  remember (Datatypes.S k) as k0. simpl. rewrite Heqk0 in *. simpl in Hd.
  destruct (g_nodes s !! n) as [d|]; [|done].
  destruct (n_kind d); try done. destruct (n_disposed d); [done|].
  revert Hd. apply body_eval_mono. intros t x. apply IH.
Qed.

Lemma den_all_mono k k' s n o : k <= k' -> den_all k s n = Some o -> den_all k' s n = Some o.
Proof. induction 1 as [|k0 _ IH]; [done|]. intros H. apply den_all_S. auto. Qed.

Lemma denotes_all_unique s n o o' : denotes_all s n o -> denotes_all s n o' -> o = o'.
Proof.
  intros [k H1] [k' H2]. assert (H1' : den_all (k + k') s n = Some o) by (eapply den_all_mono; [|exact H1]; lia).
  assert (H2' : den_all (k + k') s n = Some o') by (eapply den_all_mono; [|exact H2]; lia). congruence.
Qed.

(** Each memo reads, for every answer of its earlier reads, only memos of lower rank: the premise [memos_acyclic] of the
    reference tier, over the end state. *)
Definition acyclic_at (s : state) : Prop :=
  exists rank : node_id -> nat, forall m d, g_nodes s !! m = Some d -> n_kind d = KMemo ->
    body_pure (fun t => match g_nodes s !! t with Some dt => n_kind dt = KMemo -> rank t < rank m | None => True end)
      (n_env d) (erase (n_body d)).

(** A sufficient test of [acyclic_at]: each memo reads only nodes with lower ids, by targets known ahead of the run. *)
Definition acyclic_b (s : state) : bool :=
  forallb (fun x : node_id * node =>
    match n_kind x.2 with
    | KMemo => body_ok (fun t => bool_decide (t < x.1)) (static_of (n_env x.2)) (erase (n_body x.2))
    | _ => true
    end) (map_to_list (g_nodes s)).

Lemma acyclic_of_b s : acyclic_b s = true -> acyclic_at s.
Proof.
  intros H. exists (fun t => t). intros m d Hd Hk. unfold acyclic_b in H. rewrite forallb_forall in H.
  assert (Hin : In (m, d) (map_to_list (g_nodes s))) by (apply list_elem_of_In; apply elem_of_map_to_list; exact Hd).
  specialize (H _ Hin). simpl in H. rewrite Hk in H. eapply body_ok_pure; [|apply static_agrees_of|exact H].
  intros t Ht. apply bool_decide_eq_true_1 in Ht. destruct (g_nodes s !! t); [intros _; exact Ht|done].
Qed.

(** A body with an untracked read: its log can stay behind, and the program holds no record of what the read answered. *)
Fixpoint has_untracked (c : cmd) : bool :=
  match c with
  | CLet _ _ k | CRead _ _ k | CTryRead _ _ _ k | CLog _ k | CWrite _ _ k | CCleanup _ k => has_untracked k
  | CIf _ a b | CBatch a b => has_untracked a || has_untracked b
  | CReadUntracked _ _ _ => true
  | _ => false
  end.

(** Each live effect is [Clean] and ran, each source of its last run answered what its erased body computes now, and,
    with no untracked read in the effect, its last run logged what its erased body logs over those answers. *)
Definition converged_all (s : state) : Prop :=
  forall e d, g_nodes s !! e = Some d -> n_kind d = KEffect -> n_disposed d = false ->
    n_freshness d = Clean /\ 0 < count_runs e (g_trace s) /\
    (forall t, t ∈ read_prefix (g_edges s) e -> seen_of (g_ghost s) e t <> [] /\
       forall v, v ∈ seen_of (g_ghost s) e t -> denotes_all s t (view_answer v)) /\
    (has_untracked (n_body d) = false -> exists k,
       body_logs (den_all k s) (n_env d) (erase (n_body d)) = Some (last_run_logs e (g_trace s))).

(** Over all programs, with memo cycles and self-reads excluded and nothing else. *)
Definition convergence_all_statement : Prop :=
  forall f c s r, run_top f c (init current_flags) = Some (s, r) -> acyclic_at s -> quiescent s -> converged_all s.

(**md
**Open, and false on the present engine: at quiescence, every effect ran and read what its sources compute, in every program without a memo cycle.**

This states what the fix phase owes, as `convergence_all_statement`. The only premises are `quiescent` and `acyclic_at`, as a memo cycle or self-read is outside the contract. A disposed memo answers what it stored, as Core.fs 4372-4376 documents. An effect that writes is inside the statement, and `effect_resolves_over_moved_source` falls under it, pending the owner's ruling on that entry. [`convergence_all_falsified`](Ranvier.Pure.Witnesses.html#convergence_all_falsified) proves the statement false from `self_marking_wake_lost` and from the lost update of `cmp_never_passes_source`. Use [`convergence`](#convergence) until the fix.
*)
Theorem convergence_all : convergence_all_statement.
Proof. Admitted.
