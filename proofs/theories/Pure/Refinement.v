From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Reference Require Semantics Properties.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler WholeGraph.

Open Scope nat_scope.

(** ** More fuel keeps an answer *)

Definition le_m {A} (m m' : M A) : Prop := forall s r, m s = Some r -> m' s = Some r.

Lemma le_bind {A B} (m m' : M A) (k k' : A -> M B) : le_m m m' -> (forall a, le_m (k a) (k' a)) -> le_m (m_bind m k) (m_bind m' k').
Proof.
  intros H1 H2 s r E. unfold m_bind in *. destruct (m s) as [[s1 a]|] eqn:Em; [|done].
  rewrite (H1 s (s1, a) Em). exact (H2 a s1 r E).
Qed.

Lemma le_on_value e x (k k' : val -> M outcome) : (forall v, le_m (k v) (k' v)) -> le_m (on_value e x k) (on_value e x k').
Proof. intros H s r E. unfold on_value in *. destruct (eval_expr _ _ _) as [[v|y]|]; [exact (H v s r E)|done|done]. Qed.

Lemma le_on_node e x (k k' : node_id -> M outcome) : (forall v, le_m (k v) (k' v)) -> le_m (on_node e x k) (on_node e x k').
Proof. intros H. apply le_on_value. intros z s r E. destruct (decide _); [exact (H _ s r E)|done]. Qed.

Lemma le_hosted {A} n (b b' : M A) : le_m b b' -> le_m (hosted n b) (hosted n b').
Proof.
  intros H s r E. unfold hosted, m_bind, m_modify in *. simpl in *.
  destruct (b _) as [[s1 a]|] eqn:Eb; [|done]. rewrite (H _ _ Eb). exact E.
Qed.

Lemma le_untracked {A} (b b' : M A) : le_m b b' -> le_m (untracked b) (untracked b').
Proof.
  intros H s r E. unfold untracked, m_bind, m_modify in *. simpl in *.
  destruct (b _) as [[s1 a]|] eqn:Eb; [|done]. rewrite (H _ _ Eb). exact E.
Qed.

Lemma le_detached o (b b' : M unit) : le_m b b' -> le_m (detached o b) (detached o b').
Proof.
  intros H s r E. unfold detached, m_bind, m_modify in *. simpl in *.
  destruct (b _) as [[s1 a]|] eqn:Eb; [|done]. rewrite (H _ _ Eb). exact E.
Qed.

Lemma le_step (g g' : state -> option state) : (forall s s', g s = Some s' -> g' s = Some s') -> le_m (m_step g) (m_step g').
Proof. intros H s r E. unfold m_step in *. destruct (g s) as [s1|] eqn:Eg; [|done]. by rewrite (H _ _ Eg). Qed.

Lemma walk_le (mk mk' : node_id -> state -> option state) src :
  (forall c s s', mk c s = Some s' -> mk' c s = Some s') ->
  forall n s s', walk_marks mk src n s = Some s' -> walk_marks mk' src n s = Some s'.
Proof.
  intros H. induction n as [|n IH]; intros s s' E; [exact E|]. simpl in *. destruct (decide _); [by apply IH|].
  destruct (_ !! n) as [[c x]|]; [|done]. destruct (mk c s) as [s1|] eqn:Em; [|done]. rewrite (H _ _ _ Em). simpl in *. by apply IH.
Qed.

Lemma mark_check_le f : forall g n s s', f <= g -> mark_check f n s = Some s' -> mark_check g n s = Some s'.
Proof.
  induction f as [|f IH]; intros g n s s' Hl E; [done|]. destruct g as [|g]; [lia|]. simpl in *.
  destruct (g_nodes s !! n) as [d|]; [|exact E]. destruct (n_kind d); try exact E.
  destruct (decide _); [refine (walk_le _ _ _ _ _ _ _ E); intros c x y; apply IH; lia|].
  destruct (announce_due s d); [|exact E]. refine (walk_le _ _ _ _ _ _ _ E); intros c x y; apply IH; lia.
Qed.

Lemma mark_dirty_le f g n s s' : f <= g -> mark_dirty f n s = Some s' -> mark_dirty g n s = Some s'.
Proof.
  intros Hl E. destruct f as [|f]; [done|]. destruct g as [|g]; [lia|]. simpl in *.
  destruct (g_nodes s !! n) as [d|]; [|exact E]. destruct (n_kind d); try exact E.
  destruct (decide _); [refine (walk_le _ _ _ _ _ _ _ E); intros c x y; apply mark_check_le; lia|].
  destruct (announce_due s d); [|exact E]. refine (walk_le _ _ _ _ _ _ _ E); intros c x y; apply mark_check_le; lia.
Qed.

Lemma le_mark_reader f g : f <= g -> le_m (mark_reader f) (mark_reader g).
Proof.
  intros Hl s r E. unfold mark_reader in *. destruct (g_current s) as [c|]; [|exact E]. destruct (f_reader_mark _); [|exact E].
  destruct (mark_check f c s) as [s1|] eqn:Em; [|done]. by rewrite (mark_check_le _ _ _ _ _ Hl Em).
Qed.

Record fuel_le (f g : nat) : Prop := {
  fl_exec : forall e c, le_m (exec f e c) (exec g e c);
  fl_read : forall n t, le_m (read f n t) (read g n t);
  fl_read_node : forall n, le_m (read_node f n) (read_node g n);
  fl_update : forall n, le_m (update_if_necessary f n) (update_if_necessary g n);
  fl_rop : forall n, le_m (refresh_or_pull f n) (refresh_or_pull g n);
  fl_pull : forall n, le_m (pull f n) (pull g n);
  fl_refresh : forall n, le_m (refresh f n) (refresh g n);
  fl_walk : forall n i, le_m (resolve_walk f n i) (resolve_walk g n i);
  fl_recompute : forall n, le_m (recompute f n) (recompute g n);
  fl_run_memo : forall n, le_m (run_memo f n) (run_memo g n);
  fl_execute : forall n, le_m (execute_effect f n) (execute_effect g n);
  fl_run_effect : forall n, le_m (run_effect f n) (run_effect g n);
  fl_run_body : forall n, le_m (run_effect_body f n) (run_effect_body g n);
  fl_discharge : forall o, le_m (discharge f o) (discharge g o);
  fl_retire : forall o, le_m (retire f o) (retire g o);
  fl_scope : forall o, le_m (dispose_scope f o) (dispose_scope g o);
  fl_teardown : forall o ls cs, le_m (teardown f o ls cs) (teardown g o ls cs);
  fl_cleanup : forall o b e, le_m (run_cleanup f o b e) (run_cleanup g o b e);
  fl_release : forall n, le_m (release f n) (release g n);
  fl_dispose : forall n, le_m (dispose_node f n) (dispose_node g n);
  fl_attach : forall n o, le_m (attach f n o) (attach g n o);
  fl_write : forall n v, le_m (write_signal f n v) (write_signal g n v);
  fl_request : le_m (request_flush f) (request_flush g);
  fl_settle : le_m (settle_flush f) (settle_flush g);
  fl_flush : le_m (run_flush f) (run_flush g);
  fl_drain : le_m (drain f) (drain g)
}.

Ltac fl_go IH Hl :=
  match goal with
  | |- le_m ?a ?b => constr_eq a b; intros ? ? ?; assumption
  | |- le_m (m_bind _ _) (m_bind _ _) => apply le_bind; [fl_go IH Hl|intros ?; fl_go IH Hl]
  | |- le_m (on_value _ _ _) (on_value _ _ _) => apply le_on_value; intros ?; fl_go IH Hl
  | |- le_m (on_node _ _ _) (on_node _ _ _) => apply le_on_node; intros ?; fl_go IH Hl
  | |- le_m (hosted _ _) (hosted _ _) => apply le_hosted; fl_go IH Hl
  | |- le_m (untracked _) (untracked _) => apply le_untracked; fl_go IH Hl
  | |- le_m (detached _ _) (detached _ _) => apply le_detached; fl_go IH Hl
  | |- le_m (m_step (mark_dirty _ _)) _ => apply le_step; intros ? ?; apply mark_dirty_le, Hl
  | |- le_m (m_step (notify_dirty _ _)) _ =>
      apply le_step; intros ? ?; unfold notify_dirty, notify_with; apply walk_le; intros ? ? ?; apply mark_dirty_le, Hl
  | |- le_m (m_step (notify_dirty_except _ _ _)) _ =>
      apply le_step; intros ? ?; unfold notify_dirty_except, notify_with; apply walk_le; intros c ? ?; unfold mark_except;
      destruct (decide _); [done|apply mark_dirty_le, Hl]
  | |- le_m (mark_reader _) _ => apply le_mark_reader, Hl
  | |- le_m (exec _ _ _) _ => apply (fl_exec _ _ IH)
  | |- le_m (read _ _ _) _ => apply (fl_read _ _ IH)
  | |- le_m (read_node _ _) _ => apply (fl_read_node _ _ IH)
  | |- le_m (update_if_necessary _ _) _ => apply (fl_update _ _ IH)
  | |- le_m (refresh_or_pull _ _) _ => apply (fl_rop _ _ IH)
  | |- le_m (pull _ _) _ => apply (fl_pull _ _ IH)
  | |- le_m (refresh _ _) _ => apply (fl_refresh _ _ IH)
  | |- le_m (resolve_walk _ _ _) _ => apply (fl_walk _ _ IH)
  | |- le_m (recompute _ _) _ => apply (fl_recompute _ _ IH)
  | |- le_m (run_memo _ _) _ => apply (fl_run_memo _ _ IH)
  | |- le_m (execute_effect _ _) _ => apply (fl_execute _ _ IH)
  | |- le_m (run_effect _ _) _ => apply (fl_run_effect _ _ IH)
  | |- le_m (run_effect_body _ _) _ => apply (fl_run_body _ _ IH)
  | |- le_m (discharge _ _) _ => apply (fl_discharge _ _ IH)
  | |- le_m (retire _ _) _ => apply (fl_retire _ _ IH)
  | |- le_m (dispose_scope _ _) _ => apply (fl_scope _ _ IH)
  | |- le_m (teardown _ _ _ _) _ => apply (fl_teardown _ _ IH)
  | |- le_m (run_cleanup _ _ _ _) _ => apply (fl_cleanup _ _ IH)
  | |- le_m (release _ _) _ => apply (fl_release _ _ IH)
  | |- le_m (dispose_node _ _) _ => apply (fl_dispose _ _ IH)
  | |- le_m (attach _ _ _) _ => apply (fl_attach _ _ IH)
  | |- le_m (write_signal _ _ _) _ => apply (fl_write _ _ IH)
  | |- le_m (request_flush _) _ => apply (fl_request _ _ IH)
  | |- le_m (settle_flush _) _ => apply (fl_settle _ _ IH)
  | |- le_m (run_flush _) _ => apply (fl_flush _ _ IH)
  | |- le_m (drain _) _ => apply (fl_drain _ _ IH)
  | |- _ => case_match; fl_go IH Hl
  end.

Lemma fuel_le_S f g : f <= g -> fuel_le f g -> fuel_le (S f) (S g).
Proof.
  intros Hl IH. split; intros;
    cbn [exec read read_node update_if_necessary refresh_or_pull pull refresh resolve_walk recompute run_memo execute_effect run_effect
         run_effect_body discharge retire dispose_scope teardown run_cleanup release dispose_node attach write_signal request_flush
         settle_flush run_flush drain];
    try fl_go IH Hl.
Qed.

Lemma fuel_le_all f : forall g, f <= g -> fuel_le f g.
Proof.
  induction f as [|f IH]; intros g Hl.
  - split; intros; intros s r E; done.
  - destruct g as [|g]; [lia|]. apply fuel_le_S; [lia|]. apply IH. lia.
Qed.

(** ** Under a flush, entries already taken from the queue change nothing *)

Section Shift.
Variable x : list node_id.

(** The same queue behind the entries [x], with the head moved past them. *)
Definition shift (s : state) : state :=
  with_queue (x ++ g_queue s) (with_queue_head (length x + g_queue_head s) s).

Definition shifted {A} (r : option (state * A)) : option (state * A) := (fun y => (shift y.1, y.2)) <$> r.

(** Under a flush, [m] on the shifted state is [m] on the state, shifted, and the flush stays open. *)
Definition cm {A} (m : M A) : Prop :=
  forall s, g_flushing s = true -> m (shift s) = shifted (m s) /\ forall s' a, m s = Some (s', a) -> g_flushing s' = true.

Lemma cm_bind {A B} (m : M A) (k : A -> M B) : cm m -> (forall a, cm (k a)) -> cm (m_bind m k).
Proof.
  intros H1 H2 s Hf. destruct (H1 s Hf) as [A1 A2]. unfold m_bind. rewrite A1. destruct (m s) as [[s1 a]|] eqn:Em; [|done]. simpl.
  destruct (H2 a s1 (A2 _ _ eq_refl)) as [B1 B2]. split; [exact B1|exact B2].
Qed.

Lemma cm_ret {A} (a : A) : cm (m_return a).
Proof. intros s Hf. split; [done|]. intros s' b E. by injection E as <- _. Qed.

Lemma cm_stuck {A} : cm (@stuck A).
Proof. intros s Hf. split; done. Qed.

Lemma cm_modify (u : state -> state) :
  (forall s, u (shift s) = shift (u s)) -> (forall s, g_flushing (u s) = g_flushing s) -> cm (m_modify u).
Proof. intros H1 H2 s Hf. unfold m_modify. split; [simpl; by rewrite H1|]. intros s' a E. injection E as <- _. by rewrite H2. Qed.

Lemma cm_get {A} (k : state -> M A) :
  (forall s, g_flushing s = true -> k (shift s) = k s) -> (forall s, g_flushing s = true -> cm (k s)) -> cm (m_bind m_get k).
Proof.
  intros H1 H2 s Hf. change (k (shift s) (shift s) = shifted (k s s) /\ forall s' a, k s s = Some (s', a) -> g_flushing s' = true).
  rewrite (H1 s Hf). exact (H2 s Hf s Hf).
Qed.

Lemma live_queue_shift s : live_queue (shift s) = live_queue s.
Proof. unfold live_queue. simpl. apply bool_decide_ext. rewrite length_app. lia. Qed.

Lemma enqueue_shift n s : enqueue n (shift s) = shift (enqueue n s).
Proof. unfold enqueue, shift. simpl. by rewrite <- app_assoc. Qed.

Lemma set_node_shift n d s : set_node n d (shift s) = shift (set_node n d s).
Proof. reflexivity. Qed.

Lemma walk_shift (mk : node_id -> state -> option state) src :
  (forall c s, mk c (shift s) = shift <$> mk c s) ->
  forall n s, walk_marks mk src n (shift s) = shift <$> walk_marks mk src n s.
Proof.
  intros H. induction n as [|n IH]; intros s; [done|]. simpl.
  destruct (decide _) as [|Hne]; [apply IH|]. destruct (_ !! n) as [[c y]|]; [|done].
  rewrite H. destruct (mk c s) as [s1|]; [|done]. simpl. apply IH.
Qed.

Lemma mark_check_shift f : forall n s, mark_check f n (shift s) = shift <$> mark_check f n s.
Proof.
  induction f as [|f IH]; intros n s; [done|]. simpl. destruct (g_nodes s !! n) as [d|]; [|done]. destruct (n_kind d); try done.
  - destruct (decide _).
    + refine (walk_shift _ _ IH _ (set_node n _ s)).
    + unfold announce_due. change (g_flags (shift s)) with (g_flags s). destruct (f_unannounced (g_flags s) && n_unannounced d); [|done]. refine (walk_shift _ _ IH _ (set_node n _ s)).
  - unfold effect_marked. destruct (n_disposed d || n_in_cleanup d); [done|]. destruct (n_queued d); [done|]. simpl. f_equal. exact (enqueue_shift n (set_node n _ s)).
Qed.

Lemma mark_dirty_shift f n s : mark_dirty f n (shift s) = shift <$> mark_dirty f n s.
Proof.
  destruct f as [|f]; [done|]. simpl. destruct (g_nodes s !! n) as [d|]; [|done]. destruct (n_kind d); try done.
  - destruct (decide _).
    + refine (walk_shift _ _ (mark_check_shift f) _ (set_node n _ s)).
    + unfold announce_due. change (g_flags (shift s)) with (g_flags s). destruct (f_unannounced (g_flags s) && n_unannounced d).
      * refine (walk_shift _ _ (mark_check_shift f) _ (set_node n _ s)).
      * destruct (decide _); done.
  - unfold effect_marked. destruct (n_disposed d || n_in_cleanup d); [done|]. destruct (n_queued d); [done|]. simpl. f_equal. exact (enqueue_shift n (set_node n _ s)).
Qed.

Lemma cm_step (g : state -> option state) :
  (forall s, g (shift s) = shift <$> g s) -> (forall s s', g s = Some s' -> g_flushing s' = g_flushing s) -> cm (m_step g).
Proof.
  intros H1 H2 s Hf. unfold m_step. rewrite H1. destruct (g s) as [s1|] eqn:Eg; [|done]. split; [done|]. intros s' a E. injection E as <- _. by rewrite (H2 _ _ Eg).
Qed.

Lemma marks_flushing s s' : rtc mark_prim s s' -> g_flushing s' = g_flushing s.
Proof. intros H. exact (rs_flushing _ _ (rtc_rise _ _ H)). Qed.

Lemma ghost_publish_shift n s : ghost_publish n (shift s) = shift (ghost_publish n s).
Proof. unfold ghost_publish. change (g_nodes (shift s)) with (g_nodes s). destruct (g_nodes s !! n); reflexivity. Qed.

Lemma cm_publish n : cm (m_modify (ghost_publish n)).
Proof. apply cm_modify; [apply ghost_publish_shift|intros s; unfold ghost_publish; case_match; reflexivity]. Qed.

Lemma cm_add_node n d : cm (add_node n d).
Proof.
  apply cm_modify; intros s.
  - rewrite set_node_shift. apply ghost_publish_shift.
  - unfold ghost_publish. case_match; reflexivity.
Qed.

Lemma cm_next_id : cm next_id.
Proof. intros s Hf. split; [done|]. intros s' a E. unfold next_id in E. by injection E as <- _. Qed.

Lemma cm_current_owner : cm current_owner.
Proof.
  intros s Hf. unfold current_owner. change (g_current_owner (shift s)) with (g_current_owner s). change (g_current (shift s)) with (g_current s).
  change (g_nodes (shift s)) with (g_nodes s). change (g_owners (shift s)) with (g_owners s).
  repeat case_match; simplify_eq; split; try done; intros s' a E; by injection E as <- _.
Qed.

Lemma cm_get_node n : cm (get_node n).
Proof. intros s Hf. unfold get_node. change (g_nodes (shift s)) with (g_nodes s). destruct (g_nodes s !! n); split; try done; intros s' a E; by injection E as <- _. Qed.

Lemma cm_modify_node n u : cm (modify_node n u).
Proof. intros s Hf. unfold modify_node. change (g_nodes (shift s)) with (g_nodes s). destruct (g_nodes s !! n); split; try done. intros s' a E. by injection E as <- _. Qed.

Lemma cm_announce n : cm (announce n).
Proof.
  intros s Hf. unfold announce. change (g_flags (shift s)) with (g_flags s). destruct (f_unannounced (g_flags s)).
  - exact (cm_modify_node _ _ s Hf).
  - split; [done|]. intros s' a E. by injection E as <- _.
Qed.

Lemma cm_get_owner o : cm (get_owner o).
Proof. intros s Hf. unfold get_owner. change (g_owners (shift s)) with (g_owners s). destruct (g_owners s !! o); split; try done. intros s' a E. by injection E as <- _. Qed.

Lemma cm_put_owner o y : cm (put_owner o y).
Proof. apply cm_modify; intros; reflexivity. Qed.

Lemma cm_emit e : cm (emit e).
Proof. apply cm_modify; intros; reflexivity. Qed.

Lemma cm_edges u : cm (edges u).
Proof. apply cm_modify; intros; reflexivity. Qed.

Lemma cm_edges_opt u : cm (edges_opt u).
Proof. intros s Hf. unfold edges_opt. change (g_edges (shift s)) with (g_edges s). destruct (u (g_edges s)); split; try done. intros s' a E. by injection E as <- _. Qed.

Lemma cm_track n : cm (track n).
Proof.
  intros s Hf. unfold track. change (g_current (shift s)) with (g_current s). change (g_edges (shift s)) with (g_edges s). destruct (g_current s) as [c|].
  - destruct (sources_add c n (g_edges s)) as [w|]; [|done]. unfold ghost_read. split.
    + change (g_nodes (with_edges w (shift s))) with (g_nodes s). change (g_nodes (with_edges w s)) with (g_nodes s). destruct (g_nodes s !! n); reflexivity.
    + intros s' a E. injection E as <- _. case_match; exact Hf.
  - split; [done|]. intros s' a E. by injection E as <- _.
Qed.

Lemma cm_note n o : cm (note_host_read n o).
Proof.
  intros s Hf. unfold note_host_read. change (g_current (shift s)) with (g_current s). change (g_current_owner (shift s)) with (g_current_owner s).
  destruct (g_current s), (g_current_owner s) as [r|]; try (split; [done|]; intros s' a E; by injection E as <- _).
  destruct (decide _); split; try done; intros s' a E; by injection E as <- _.
Qed.

Lemma cm_log_label : cm log_label.
Proof.
  intros s Hf. unfold log_label. change (g_current (shift s)) with (g_current s). change (g_current_owner (shift s)) with (g_current_owner s).
  repeat case_match; simplify_eq; split; try done; intros s' a E; by injection E as <- _.
Qed.

Lemma cm_mark_reader f : cm (mark_reader f).
Proof.
  intros s Hf. unfold mark_reader. change (g_current (shift s)) with (g_current s). change (g_flags (shift s)) with (g_flags s).
  destruct (g_current s) as [c|]; [|split; [done|]; intros s' a E; by injection E as <- _].
  destruct (f_reader_mark _); [|split; [done|]; intros s' a E; by injection E as <- _].
  rewrite mark_check_shift. destruct (mark_check f c s) as [s1|] eqn:Em; [|done]. split; [done|]. intros s' a E. injection E as <- _.
  by rewrite (marks_flushing _ _ (mark_check_rtc _ _ _ _ Em)).
Qed.

Lemma cm_hosted {A} n (b : M A) : cm b -> cm (hosted n b).
Proof.
  intros H s Hf. unfold hosted, m_bind, m_modify. simpl.
  destruct (H (with_current_owner None (with_current (Some n) s)) Hf) as [A1 A2].
  assert (E : with_current_owner None (with_current (Some n) (shift s)) = shift (with_current_owner None (with_current (Some n) s))) by reflexivity.
  rewrite E, A1. destruct (b (with_current_owner None (with_current (Some n) s))) as [[s1 a]|] eqn:Eb; [|done]. split; [done|].
  intros s' c Ec. unfold m_return in Ec. injection Ec as <- _. exact (A2 _ _ eq_refl).
Qed.

Lemma cm_untracked {A} (b : M A) : cm b -> cm (untracked b).
Proof.
  intros H s Hf. unfold untracked, m_bind, m_modify. simpl.
  set (flag := match g_current s with Some _ => true | None => g_untracked s end).
  destruct (H (with_untracked flag (with_current None s)) Hf) as [A1 A2].
  assert (E : with_untracked flag (with_current None (shift s)) = shift (with_untracked flag (with_current None s))) by reflexivity.
  change (match g_current (shift s) with Some _ => true | None => g_untracked (shift s) end) with flag. rewrite E, A1.
  destruct (b (with_untracked flag (with_current None s))) as [[s1 a]|] eqn:Eb; [|done]. split; [done|].
  intros s' c Ec. unfold m_return in Ec. injection Ec as <- _. exact (A2 _ _ eq_refl).
Qed.

Lemma cm_detached o (b : M unit) : cm b -> cm (detached o b).
Proof.
  intros H s Hf. unfold detached, m_bind, m_modify. simpl.
  set (s1 := with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s))).
  destruct (H s1 Hf) as [A1 A2].
  assert (E : with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None (shift s))) = shift s1) by reflexivity.
  rewrite E, A1. destruct (b s1) as [[s2 a]|] eqn:Eb; [|done]. split; [done|].
  intros s' c Ec. unfold m_return in Ec. injection Ec as <- _. exact (A2 _ _ eq_refl).
Qed.

Record cm_ok (f : nat) : Prop := {
  co_exec : forall e c, cm (exec f e c);
  co_read : forall n t, cm (read f n t);
  co_read_node : forall n, cm (read_node f n);
  co_update : forall n, cm (update_if_necessary f n);
  co_rop : forall n, cm (refresh_or_pull f n);
  co_pull : forall n, cm (pull f n);
  co_refresh : forall n, cm (refresh f n);
  co_walk : forall n i, cm (resolve_walk f n i);
  co_recompute : forall n, cm (recompute f n);
  co_run_memo : forall n, cm (run_memo f n);
  co_execute : forall n, cm (execute_effect f n);
  co_run_effect : forall n, cm (run_effect f n);
  co_run_body : forall n, cm (run_effect_body f n);
  co_discharge : forall o, cm (discharge f o);
  co_retire : forall o, cm (retire f o);
  co_scope : forall o, cm (dispose_scope f o);
  co_teardown : forall o ls cs, cm (teardown f o ls cs);
  co_cleanup : forall o b e, cm (run_cleanup f o b e);
  co_release : forall n, cm (release f n);
  co_dispose : forall n, cm (dispose_node f n);
  co_attach : forall n o, cm (attach f n o);
  co_write : forall n v, cm (write_signal f n v);
  co_request : cm (request_flush f);
  co_settle : cm (settle_flush f);
  co_flush : cm (run_flush f)
}.

Lemma cm_on_value e v (k : val -> M outcome) : (forall z, cm (k z)) -> cm (on_value e v k).
Proof.
  intros H s Hf. unfold on_value. destruct (eval_expr _ _ _) as [[z|y]|]; [exact (H z s Hf)|split; [done|]; intros s' a E; by injection E as <- _|done].
Qed.

Lemma cm_on_node e v (k : node_id -> M outcome) : (forall z, cm (k z)) -> cm (on_node e v k).
Proof. intros H. apply cm_on_value. intros z. destruct (decide _); [apply H|apply cm_stuck]. Qed.

Ltac cm_go IH :=
  lazymatch goal with
  | |- cm (let _ := _ in _) => cbv zeta; cm_go IH
  | |- cm (m_bind m_get _) =>
      apply cm_get; intros ? ?; [cbv beta; try rewrite live_queue_shift; reflexivity|cbv beta; cm_go IH]
  | |- cm (m_bind _ _) => apply cm_bind; [cm_go IH|intros ?; cm_go IH]
  | |- cm next_id => apply cm_next_id
  | |- cm current_owner => apply cm_current_owner
  | |- cm (push_running _) => unfold push_running; cm_go IH
  | |- cm pop_running => unfold pop_running; cm_go IH
  | |- cm (add_node _ _) => apply cm_add_node
  | |- cm (m_modify (ghost_publish _)) => apply cm_publish
  | |- cm (record_error _ _) => unfold record_error; cm_go IH
  | |- cm (unlink _) => unfold unlink; cm_go IH
  | |- cm m_get => intros ? ?; split; [done|]; intros ? ? E; by injection E as <- _
  | |- cm (announce _) => apply cm_announce
  | |- cm (m_return _) => apply cm_ret
  | |- cm stuck => apply cm_stuck
  | |- cm (on_value _ _ _) => apply cm_on_value; intros ?; cm_go IH
  | |- cm (on_node _ _ _) => apply cm_on_node; intros ?; cm_go IH
  | |- cm (hosted _ _) => apply cm_hosted; cm_go IH
  | |- cm (untracked _) => apply cm_untracked; cm_go IH
  | |- cm (detached _ _) => apply cm_detached; cm_go IH
  | |- cm (get_node _) => apply cm_get_node
  | |- cm (modify_node _ _) => apply cm_modify_node
  | |- cm (get_owner _) => apply cm_get_owner
  | |- cm (put_owner _ _) => apply cm_put_owner
  | |- cm (emit _) => apply cm_emit
  | |- cm (edges _) => apply cm_edges
  | |- cm (edges_opt _) => apply cm_edges_opt
  | |- cm (track _) => apply cm_track
  | |- cm (note_host_read _ _) => apply cm_note
  | |- cm log_label => apply cm_log_label
  | |- cm (mark_reader _) => apply cm_mark_reader
  | |- cm (m_step (mark_dirty _ _)) =>
      apply cm_step; [intros ?; apply mark_dirty_shift|intros ? ? ?; refine (marks_flushing _ _ (any_mark_rtc _ _ None _ _ _)); right; left; eassumption]
  | |- cm (m_step (notify_dirty _ _)) =>
      apply cm_step; [intros ?; apply walk_shift; intros ? ?; apply mark_dirty_shift|intros ? ? ?; refine (marks_flushing _ _ (any_mark_rtc _ _ None _ _ _)); right; right; right; left; eassumption]
  | |- cm (exec _ _ _) => apply (co_exec _ IH)
  | |- cm (read _ _ _) => apply (co_read _ IH)
  | |- cm (read_node _ _) => apply (co_read_node _ IH)
  | |- cm (update_if_necessary _ _) => apply (co_update _ IH)
  | |- cm (refresh_or_pull _ _) => apply (co_rop _ IH)
  | |- cm (pull _ _) => apply (co_pull _ IH)
  | |- cm (refresh _ _) => apply (co_refresh _ IH)
  | |- cm (resolve_walk _ _ _) => apply (co_walk _ IH)
  | |- cm (recompute _ _) => apply (co_recompute _ IH)
  | |- cm (run_memo _ _) => apply (co_run_memo _ IH)
  | |- cm (execute_effect _ _) => apply (co_execute _ IH)
  | |- cm (run_effect _ _) => apply (co_run_effect _ IH)
  | |- cm (run_effect_body _ _) => apply (co_run_body _ IH)
  | |- cm (discharge _ _) => apply (co_discharge _ IH)
  | |- cm (retire _ _) => apply (co_retire _ IH)
  | |- cm (dispose_scope _ _) => apply (co_scope _ IH)
  | |- cm (teardown _ _ _ _) => apply (co_teardown _ IH)
  | |- cm (run_cleanup _ _ _ _) => apply (co_cleanup _ IH)
  | |- cm (release _ _) => apply (co_release _ IH)
  | |- cm (dispose_node _ _) => apply (co_dispose _ IH)
  | |- cm (attach _ _ _) => apply (co_attach _ IH)
  | |- cm (write_signal _ _ _) => apply (co_write _ IH)
  | |- cm (request_flush _) => apply (co_request _ IH)
  | |- cm (settle_flush _) => apply (co_settle _ IH)
  | |- cm (run_flush _) => apply (co_flush _ IH)
  | |- cm (m_modify _) =>
      let z := fresh "z" in
      apply cm_modify; intros z; cbv beta;
      unfold ghost_run_end, ghost_run_start, ghost_effect_start, ghost_resolved, ghost_dirty_cleared, ghost_publish, ghost_registered;
      first [reflexivity|(try change (g_nodes (shift z)) with (g_nodes z); repeat (match goal with |- context [g_nodes z !! ?m] => destruct (g_nodes z !! m) end); reflexivity)]
  | |- cm (m_step (notify_dirty_except _ _ _)) =>
      apply cm_step;
      [intros ?; apply walk_shift; intros ? ?; unfold mark_except; destruct (decide _); [done|apply mark_dirty_shift]
      |intros ? ? ?; refine (marks_flushing _ _ (any_mark_rtc _ _ _ _ _ _)); right; right; right; right; eassumption]
  | |- _ => case_match; cm_go IH
  end.

Lemma cm_ok_S f : cm_ok f -> cm_ok (S f).
Proof.
  intros IH. split; intros;
    cbn [exec read read_node update_if_necessary refresh_or_pull pull refresh resolve_walk recompute run_memo execute_effect run_effect
         run_effect_body discharge retire dispose_scope teardown run_cleanup release dispose_node attach write_signal request_flush
         settle_flush run_flush];
    try cm_go IH.
  all: try (destruct c; cm_go IH).
  - apply cm_get; intros s Hf; [reflexivity|]. cbv beta. rewrite Hf. apply cm_ret.
Qed.

Lemma cm_all f : cm_ok f.
Proof.
  induction f as [|f IH]; [split; intros; intros s Hf; split; done|by apply cm_ok_S].
Qed.

End Shift.

(** A read-only body that does not stick on any path. [Q] holds of each read's target. *)
Fixpoint body_ok (Q : node_id -> Prop) (e : env) (c : cmd) : Prop :=
  match c with
  | CRet x => expr_plain x = true /\ is_Some (ev e x)
  | CThrow _ => True
  | CLet x v k => expr_plain v = true /\ is_Some (ev e v) /\ forall z, ev e v = Some (Ok z) -> body_ok Q ((x, z) :: e) k
  | CIf v a b =>
    expr_plain v = true /\ is_Some (ev e v) /\
    forall z, ev e v = Some (Ok z) -> (z = 0%Z -> body_ok Q e b) /\ (z <> 0%Z -> body_ok Q e a)
  | CRead x n k =>
    expr_plain n = true /\ is_Some (ev e n) /\ (forall z, ev e n = Some (Ok z) -> (0 <= z)%Z /\ Q (Z.to_nat z)) /\
    forall v, body_ok Q ((x, v) :: e) k
  | CTryRead x ok n k =>
    expr_plain n = true /\ is_Some (ev e n) /\ (forall z, ev e n = Some (Ok z) -> (0 <= z)%Z /\ Q (Z.to_nat z)) /\
    (forall v, body_ok Q ((ok, 1%Z) :: (x, v) :: e) k) /\ (forall y, body_ok Q ((ok, 0%Z) :: (x, y) :: e) k)
  | CLog v k => expr_plain v = true /\ is_Some (ev e v) /\ body_ok Q e k
  | _ => False
  end.

Lemma body_ok_pure Q e c : body_ok Q e c -> body_pure (fun _ => True) e c.
Proof.
  revert e. induction c; intros g H; simpl in *; try done; try tauto.
  - destruct H as (A&_&B). split; [done|]. intros z Hz. auto.
  - destruct H as (A&_&B). split; [done|]. intros z Hz. destruct (B z Hz) as [C D]. split; auto.
  - destruct H as (A&_&_&B). split_and!; [done|done|]. intros v. auto.
  - destruct H as (A&_&_&B&C). split_and!; [done|done|intros v; auto|intros y; auto].
  - destruct H as (A&_&B). split; auto.
Qed.

Section Total.
Variable Sh : gmap node_id (kind * comparer * env * cmd).
Hypothesis Hbm : forall m q e b, Sh !! m = Some (KMemo, q, e, b) -> body_ok (fun t => t < m /\ readable Sh t) e b.

(** A memo of [Sh] below [lim]: a call on nodes up to there moves the slots of these alone. *)
Definition xl (lim c : node_id) : Prop := c < lim /\ exists q e b, Sh !! c = Some (KMemo, q, e, b).

Record gd (s : state) : Prop := {
  gd_sh : shapes s = Sh;
  gd_scope : forall m d, g_nodes s !! m = Some d -> n_kind d = KMemo -> n_scope d = None;
  gd_reach : world_reachable (g_edges s);
  gd_cursor : forall c, (slot_view (g_edges s) c).2 <= length (slot_view (g_edges s) c).1;
  gd_slots : forall c y, y ∈ (slot_view (g_edges s) c).1 ->
    readable Sh y /\ forall q e b, Sh !! c = Some (KMemo, q, e, b) -> y < c
}.

Record kept (X : node_id -> Prop) (s s' : state) : Prop := {
  kp_slots : forall c, ~ X c -> g_current s <> Some c -> (slot_view (g_edges s') c).1 = (slot_view (g_edges s) c).1;
  kp_cur : g_current s' = g_current s;
  kp_flu : g_flushing s' = g_flushing s;
  kp_bat : g_batch_depth s' = g_batch_depth s
}.

Lemma kept_refl X s : kept X s s.
Proof. split; done. Qed.

Lemma kept_trans X s1 s2 s3 : kept X s1 s2 -> kept X s2 s3 -> kept X s1 s3.
Proof.
  intros [A1 B1 C1 D1] [A2 B2 C2 D2]. split; try congruence.
  intros c H1 H2. rewrite A2, A1; [done|done|done|done|congruence].
Qed.

Definition ev_m {A} (F : nat -> M A) (s : state) (r : state * A) : Prop := exists N, forall f, N <= f -> F f s = Some r.

(** For enough fuel, [F] ends from each state of [gd] and [P], and ends in [gd]. *)
Definition tot {A} (X : node_id -> Prop) (P : state -> Prop) (F : nat -> M A) : Prop :=
  forall s, gd s -> tk_ctx s -> P s -> exists s' a, ev_m F s (s', a) /\ gd s' /\ kept X s s'.

Class TS (P : state -> Prop) := ts : forall X s s', gd s -> gd s' -> kept X s s' -> P s -> P s'.

Global Instance ts_true : TS (fun _ => True).
Proof. intros ? ? ? _ _ _ _. done. Qed.

Global Instance ts_cur c : TS (fun s => g_current s = Some c).
Proof. intros X s s' _ _ K H. by rewrite (kp_cur _ _ _ K). Qed.

Global Instance ts_cur_some : TS cur_some.
Proof. intros X s s' _ _ K [c H]. exists c. by rewrite (kp_cur _ _ _ K). Qed.

Global Instance ts_kind n k : TS (has_kind n k).
Proof.
  intros X s s' G G' _ (d&Hd&Hk). assert (E : shapes s = shapes s') by (by rewrite (gd_sh _ G), (gd_sh _ G')).
  destruct (shapes_node_back s' s n d E Hd) as (x&Hx&Hs). unfold shape_of in Hs. injection Hs as K _ _ _. exists x. split; [done|congruence].
Qed.

(** The read target lies below the current memo. *)
Definition pt (t : node_id) (s : state) : Prop :=
  forall c q e b, g_current s = Some c -> Sh !! c = Some (KMemo, q, e, b) -> t < c.

Global Instance ts_pt t : TS (pt t).
Proof. intros X s s' _ _ K H c q e b Hc. apply (H c q e b). by rewrite <- (kp_cur _ _ _ K). Qed.

Lemma tot_weaken {A} X X' P P' (F : nat -> M A) : (forall c, X c -> X' c) -> (forall s, P' s -> P s) -> tot X P F -> tot X' P' F.
Proof.
  intros HX HP H s G C Hp. destruct (H s G C (HP s Hp)) as (s'&a&Ev&G'&[K1 K2 K3 K4]). exists s', a. split_and!; [done|done|].
  split; try done. intros c H1 H2. apply K1; [intros K; exact (H1 (HX c K))|done].
Qed.

Lemma tot_S {A} X P (F : nat -> M A) : tot X P (fun f => F (S f)) -> tot X P F.
Proof.
  intros H s G C Hp. destruct (H s G C Hp) as (s'&a&[N Ev]&G'&K). exists s', a. split_and!; [|done|done].
  exists (S N). intros f Hf. destruct f as [|f]; [lia|]. apply Ev. lia.
Qed.

Lemma tot_const {A} X P (m : M A) :
  (forall s, gd s -> tk_ctx s -> P s -> exists s' a, m s = Some (s', a) /\ gd s' /\ kept X s s') -> tot X P (fun _ => m).
Proof. intros H s G C Hp. destruct (H s G C Hp) as (s'&a&E&G'&K). exists s', a. split_and!; [|done|done]. by exists 0. Qed.

Lemma tot_bind {A B} X P `{!TS P} (G0 : nat -> M A) (k : nat -> A -> M B) :
  tot X P G0 -> (forall a, tot X P (fun f => k f a)) -> tot X P (fun f => m_bind (G0 f) (k f)).
Proof.
  intros H1 H2 s G C Hp. destruct (H1 s G C Hp) as (s1&a&[N1 Ev1]&G1&K1).
  assert (C1 : tk_ctx s1) by (unfold tk_ctx in *; rewrite (kp_flu _ _ _ K1), (kp_bat _ _ _ K1); exact C).
  destruct (H2 a s1 G1 C1 (ts _ _ _ G G1 K1 Hp)) as (s2&b&[N2 Ev2]&G2&K2).
  exists s2, b. split_and!; [|done|].
  - exists (N1 `max` N2). intros f Hf. unfold m_bind. rewrite (Ev1 f) by lia. apply Ev2. lia.
  - exact (kept_trans _ _ _ _ K1 K2).
Qed.

Lemma tot_ret {A} X P (a : A) : tot X P (fun _ => m_return a).
Proof. apply tot_const. intros s G _ _. exists s, a. split_and!; [done|done|apply kept_refl]. Qed.

Lemma tot_get X P : tot X P (fun _ => m_get).
Proof. apply tot_const. intros s G _ _. exists s, s. split_and!; [done|done|apply kept_refl]. Qed.

Lemma tot_node {B} X P n (k : nat -> node -> M B) :
  (forall s, gd s -> P s -> is_Some (g_nodes s !! n)) ->
  (forall d, (exists s, gd s /\ P s /\ g_nodes s !! n = Some d) -> tot X P (fun f => k f d)) ->
  tot X P (fun f => m_bind (get_node n) (k f)).
Proof.
  intros He H s G C Hp. destruct (He s G Hp) as [d Hd].
  destruct (H d (ex_intro _ s (conj G (conj Hp Hd))) s G C Hp) as (s'&a&[N Ev]&G'&K). exists s', a. split_and!; [|done|done].
  exists N. intros f Hf. unfold m_bind, get_node. rewrite Hd. by apply Ev.
Qed.

(** A step that keeps the nodes' shapes and every field [kept] and [gd] read. *)
Lemma gd_same s s' :
  shapes s' = shapes s -> (forall m d', g_nodes s' !! m = Some d' -> n_kind d' = KMemo -> exists d, g_nodes s !! m = Some d /\ n_kind d = KMemo /\ n_scope d' = n_scope d) ->
  g_edges s' = g_edges s -> gd s -> gd s'.
Proof.
  intros H1 H2 H3 [A B C D E]. split; try (rewrite H3; done); [congruence|].
  intros m d' Hd' Hk. destruct (H2 m d' Hd' Hk) as (d&Hd&Hkd&->). exact (B m d Hd Hkd).
Qed.

Lemma tot_modify X P (u : state -> state) :
  (forall s, g_nodes (u s) = g_nodes s /\ g_edges (u s) = g_edges s /\ g_current (u s) = g_current s /\ g_flushing (u s) = g_flushing s /\ g_batch_depth (u s) = g_batch_depth s) ->
  tot X P (fun _ => m_modify u).
Proof.
  intros H. apply tot_const. intros s G _ _. destruct (H s) as (A&B&C&D&E). exists (u s), tt. split_and!; [done| |split; try done].
  - apply (gd_same s); try done.
    + unfold shapes. by rewrite A.
    + intros m d' Hd' Hk. rewrite A in Hd'. by exists d'.
  - intros c _ _. by rewrite B.
Qed.

Lemma tot_modify_node X P n (u : node -> node) :
  (forall s, gd s -> P s -> is_Some (g_nodes s !! n)) -> (forall d, shape_of (u d) = shape_of d) -> (forall d, n_scope (u d) = n_scope d) ->
  tot X P (fun _ => modify_node n u).
Proof.
  intros He H1 H2. apply tot_const. intros s G _ Hp. destruct (He s G Hp) as [d Hd]. unfold modify_node. rewrite Hd.
  exists (set_node n (u d) s), tt. split_and!; [done| |split; done].
  apply (gd_same s); try done.
  - unfold shapes. change (shape_of <$> <[n := u d]> (g_nodes s) = shape_of <$> g_nodes s). rewrite fmap_insert, H1. apply insert_id. by rewrite lookup_fmap, Hd.
  - intros m d' Hd' Hk. change (<[n := u d]> (g_nodes s) !! m = Some d') in Hd'. destruct (decide (m = n)) as [->|Hne].
    + rewrite lookup_insert_eq in Hd'. injection Hd' as <-. exists d. pose proof (H1 d) as X1. unfold shape_of in X1. injection X1 as K _ _ _. split_and!; [done|congruence|apply H2].
    + rewrite lookup_insert_ne in Hd' by done. by exists d'.
Qed.

(** Marks reach no edge and keep shapes. *)
Lemma gd_marks s s' : rtc mark_prim s s' -> gd s -> gd s' /\ kept (fun _ => False) s s'.
Proof.
  intros Hm G. pose proof (rtc_rise _ _ Hm) as R. pose proof (marks_edges _ _ Hm) as E. split.
  - apply (gd_same s); [|intros m d' Hd' Hk|done|done].
    + apply map_eq. intros m. unfold shapes. rewrite !lookup_fmap. destruct (g_nodes s !! m) as [d|] eqn:Hd.
      * destruct (rs_fwd _ _ R m d Hd) as (d'&Hd'&Y&_). rewrite Hd'. simpl. by rewrite Y.
      * destruct (g_nodes s' !! m) as [d'|] eqn:Hd'; [|done]. destruct (rs_back _ _ R m d' Hd') as [x Hx]. congruence.
    + destruct (rs_back _ _ R m d' Hd') as [d Hd]. destruct (rs_fwd _ _ R m d Hd) as (x&Hx&Y&X). rewrite Hd' in Hx. injection Hx as <-.
      unfold shape_of in Y. injection Y as K _ _ _. pose proof X as (_&_&_&_&Z&_). exists d. split_and!; [done|congruence|exact Z].
  - split; [intros c _ _; by rewrite E|apply (rs_current _ _ R)|apply (rs_flushing _ _ R)|apply (rs_batch _ _ R)].
Qed.

Lemma tot_mark_reader X P : tot X P (fun f => mark_reader f).
Proof.
  intros s G _ _. destruct (g_current s) as [c|] eqn:Hc.
  2: { exists s, tt. split_and!; [exists 0; intros f _; unfold mark_reader; rewrite Hc; done|done|split; try done]. }
  destruct (f_reader_mark (g_flags s)) eqn:Hf.
  2: { exists s, tt. split_and!; [exists 0; intros f _; unfold mark_reader; rewrite Hc, Hf; done|done|split; try done]. }
  destruct (mark_check_some (S (mark_measure s)) c s ltac:(lia)) as [s1 Hm].
  destruct (gd_marks s s1 (mark_check_rtc _ _ _ _ Hm) G) as [G1 [K1 K2 K3 K4]]. exists s1, tt. split_and!; [|done|split; try done].
  - exists (S (mark_measure s)). intros f Hl. unfold mark_reader. rewrite Hc, Hf, (mark_check_le _ _ _ _ _ Hl Hm). done.
  - intros y _ Hy. apply K1; [intros []|done].
Qed.

Lemma tot_notify_except X P n r : tot X P (fun f => m_step (notify_dirty_except f n r)).
Proof.
  intros s G _ _. destruct (proj2 (proj2 (notify_terminates (S (mark_measure s)) n r s ltac:(lia)))) as [s1 Hm].
  assert (Hrt : rtc mark_prim s s1).
  { refine (any_mark_rtc _ _ _ _ _ _). right. right. right. right. exact Hm. }
  destruct (gd_marks s s1 Hrt G) as [G1 [K1 K2 K3 K4]]. exists s1, tt. split_and!; [|done|split; try done].
  - exists (S (mark_measure s)). intros f Hl. unfold m_step.
    assert (E : notify_dirty_except f n r s = Some s1).
    { revert Hm. unfold notify_dirty_except, notify_with. apply walk_le. intros c x y. unfold mark_except. destruct (decide _); [done|]. by apply mark_dirty_le. }
    by rewrite E.
  - intros y _ Hy. apply K1; [intros []|done].
Qed.

Lemma tot_track X t (Hr : readable Sh t) : tot X (pt t) (fun _ => track t).
Proof.
  apply tot_const. intros s G _ Hp. unfold track. destruct (g_current s) as [c|] eqn:Hc.
  2: { exists s, tt. split_and!; [done|done|apply kept_refl]. }
  pose proof (gd_reach _ G) as Hre. pose proof (proj1 (reachable_well_formed _ Hre)) as Hem.
  assert (Ha : exists w, sources_add c t (g_edges s) = Some w).
  { unfold sources_add. destruct (decide _); [by eexists|]. destruct (decide _); [by eexists|]. unfold sources_end_run.
    destruct (decide _); [|by eexists]. destruct (sources_trim_from_ok c (source_cursor (sources_of (g_edges s) c)) (g_edges s) Hem) as (w'&E&_). rewrite E. simpl. by eexists. }
  destruct Ha as [w Ea]. rewrite Ea.
  destruct (slot_view_add _ _ _ _ (gd_cursor _ G c) Ea) as (Hoth&Hle&Hsl&_).
  assert (Hg : g_nodes (ghost_read c t (read_prefix (g_edges s) c) (with_edges w s)) = g_nodes s /\ g_edges (ghost_read c t (read_prefix (g_edges s) c) (with_edges w s)) = w /\
               g_current (ghost_read c t (read_prefix (g_edges s) c) (with_edges w s)) = g_current s /\ g_flushing (ghost_read c t (read_prefix (g_edges s) c) (with_edges w s)) = g_flushing s /\
               g_batch_depth (ghost_read c t (read_prefix (g_edges s) c) (with_edges w s)) = g_batch_depth s).
  { unfold ghost_read. destruct (g_nodes (with_edges w s) !! t); split_and!; reflexivity. }
  destruct Hg as (A&B&C&D&E). eexists _, tt. split_and!; [reflexivity| |split; try done].
  - split.
    + rewrite <- (gd_sh _ G). unfold shapes. by rewrite A.
    + intros m d Hd. rewrite A in Hd. exact (gd_scope _ G m d Hd).
    + rewrite B. exact (reachable_add _ _ _ _ Hre Ea).
    + rewrite B. intros y. destruct (decide (y = c)) as [->|Hne]; [exact Hle|]. rewrite (Hoth y Hne). exact (gd_cursor _ G y).
    + rewrite B. intros y x Hx. destruct (decide (y = c)) as [->|Hne].
      * destruct (Hsl x Hx) as [K| ->]; [exact (gd_slots _ G c x K)|]. split; [exact Hr|]. intros q e b Hs. exact (Hp c q e b Hc Hs).
      * rewrite (Hoth y Hne) in Hx. exact (gd_slots _ G y x Hx).
  - intros y _ Hy. rewrite B. rewrite (Hoth y); [done|]. intros ->. by apply Hy.
Qed.

Lemma gd_node s n x : gd s -> Sh !! n = Some x -> exists d, g_nodes s !! n = Some d /\ shape_of d = x.
Proof.
  intros G Hx. rewrite <- (gd_sh _ G) in Hx. unfold shapes in Hx. rewrite lookup_fmap in Hx.
  destruct (g_nodes s !! n) as [d|]; [|done]. injection Hx as <-. by exists d.
Qed.

Lemma gd_shape s n d : gd s -> g_nodes s !! n = Some d -> Sh !! n = Some (shape_of d).
Proof. intros G Hd. rewrite <- (gd_sh _ G). unfold shapes. by rewrite lookup_fmap, Hd. Qed.

Lemma tot_on_value X P e' e x (k : nat -> val -> M outcome) :
  env_agree e' e -> expr_plain x = true -> is_Some (ev e x) -> (forall v, ev e x = Some (Ok v) -> tot X P (fun f => k f v)) ->
  tot X P (fun f => on_value e' x (k f)).
Proof.
  intros Ha Hp [o Ho] H s G C HP. unfold on_value. rewrite (ev_of _ _ _ Ha Hp), Ho. destruct o as [v|y].
  - exact (H v Ho s G C HP).
  - exists s, (Fail y). split_and!; [by exists 0|done|apply kept_refl].
Qed.

Lemma tot_on_node X P e' e x (k : nat -> node_id -> M outcome) :
  env_agree e' e -> expr_plain x = true -> is_Some (ev e x) ->
  (forall z, ev e x = Some (Ok z) -> (0 <= z)%Z /\ tot X P (fun f => k f (Z.to_nat z))) ->
  tot X P (fun f => on_node e' x (k f)).
Proof.
  intros Ha Hp He H. unfold on_node. apply (tot_on_value _ _ _ e); [done|done|done|]. intros z Hz. destruct (H z Hz) as [H0 H1].
  intros s G C HP. destruct (H1 s G C HP) as (s'&a&[N Ev]&G'&K). exists s', a. split_and!; [|done|done].
  exists N. intros f Hf. rewrite decide_True by done. exact (Ev f Hf).
Qed.

Lemma tot_note X n o c : tot X (fun s => g_current s = Some c) (fun _ => note_host_read n o).
Proof.
  apply tot_const. intros s G _ Hc. unfold note_host_read. rewrite Hc. exists s, tt. split_and!; [done|done|apply kept_refl].
Qed.

Lemma tot_emit X P e : tot X P (fun _ => emit e).
Proof. unfold emit. apply tot_modify. intros s. split_and!; reflexivity. Qed.

Lemma exec_tot X cur Q :
  (forall t, Q t -> tot X (pt t) (fun f => read_node f t)) ->
  (forall t q e b, Q t -> Sh !! cur = Some (KMemo, q, e, b) -> t < cur) ->
  forall c e' e, env_agree e' e -> body_ok Q e c -> tot X (fun s => g_current s = Some cur) (fun f => exec f e' c).
Proof.
  intros Hrd Hlow. set (P := fun s => g_current s = Some cur).
  assert (Hrd' : forall t, Q t -> tot X P (fun f => read f t true)).
  { intros t Ht. apply tot_S. cbn [read]. refine (tot_weaken _ _ _ _ _ _ _ (Hrd t Ht)); [done|]. intros s Hs c q e b Hc Hm. subst P. simpl in Hs. rewrite Hs in Hc. injection Hc as <-. exact (Hlow t q e b Ht Hm). }
  induction c; intros e' g Ha Hb; apply tot_S; cbn [exec]; simpl in Hb; try done.
  - destruct Hb as (A&B). apply (tot_on_value _ _ _ g); [done|done|done|]. intros v _. apply tot_ret.
  - apply tot_ret.
  - destruct Hb as (A&B&D). apply (tot_on_value _ _ _ g); [done|done|done|]. intros z Hz. apply (IHc _ ((x, z) :: g)); [by apply env_agree_cons|by apply D].
  - destruct Hb as (A&B&D). apply (tot_on_value _ _ _ g); [done|done|done|]. intros z Hz. destruct (D z Hz) as [D1 D2].
    destruct (decide (z = 0%Z)) as [H0|H0]; [apply (IHc2 _ g); [done|by apply D1]|apply (IHc1 _ g); [done|by apply D2]].
  - destruct Hb as (A&B&D&D'). apply (tot_on_node _ _ _ g); [done|done|done|]. intros z Hz. destruct (D z Hz) as [H0 HQ]. split; [done|].
    apply tot_bind; [apply _|exact (Hrd' _ HQ)|intros o]. apply tot_bind; [apply _|apply tot_note|intros _]. destruct o as [v|y]; [|apply tot_ret].
    apply (IHc _ ((x, v) :: g)); [by apply env_agree_cons|apply D'].
  - destruct Hb as (A&B&D&D1&D2). apply (tot_on_node _ _ _ g); [done|done|done|]. intros z Hz. destruct (D z Hz) as [H0 HQ]. split; [done|].
    apply tot_bind; [apply _|exact (Hrd' _ HQ)|intros o]. apply tot_bind; [apply _|apply tot_note|intros _]. destruct o as [v|y].
    + apply (IHc _ ((ok, 1%Z) :: (x, v) :: g)); [by do 2 apply env_agree_cons|apply D1].
    + apply (IHc _ ((ok, 0%Z) :: (x, y) :: g)); [by do 2 apply env_agree_cons|apply D2].
  - destruct Hb as (A&B&D). apply (tot_on_value _ _ _ g); [done|done|done|]. intros z _.
    apply tot_bind; [apply _|apply tot_const; intros s G _ Hc; unfold log_label; rewrite Hc; exists s, cur; split_and!; [done|done|apply kept_refl]|intros l].
    apply tot_bind; [apply _|apply tot_emit|intros _]. apply (IHc _ g); done.
Qed.

Lemma tot_hosted {A} X n (F : nat -> M A) :
  X n -> tot X (fun s => g_current s = Some n) F -> tot X (fun _ => True) (fun f => hosted n (F f)).
Proof.
  intros Hn H s G C _.
  assert (G1 : gd (with_current_owner None (with_current (Some n) s))) by (destruct G; split; done).
  assert (C1 : tk_ctx (with_current_owner None (with_current (Some n) s))) by exact C.
  destruct (H _ G1 C1 eq_refl) as (s1&a&[N Ev]&G2&[K1 K2 K3 K4]).
  eexists _, a. split_and!.
  - exists N. intros f Hf. unfold hosted, m_bind, m_modify. simpl. rewrite (Ev f Hf). reflexivity.
  - destruct G2; split; done.
  - split; simpl; try done. intros c Hc Hc2. apply K1; [done|]. simpl. intros K. injection K as ->. by apply Hc.
Qed.

Lemma tot_begin X P n : tot X P (fun _ => edges (sources_begin_run n)).
Proof.
  apply tot_const. intros s G _ _. unfold edges, m_modify. exists (with_edges (sources_begin_run n (g_edges s)) s), tt. split_and!; [done| |split; try done].
  - split.
    + exact (gd_sh _ G).
    + exact (gd_scope _ G).
    + exact (reachable_begin _ _ (gd_reach _ G)).
    + intros c. change ((slot_view (sources_begin_run n (g_edges s)) c).2 <= length (slot_view (sources_begin_run n (g_edges s)) c).1).
      rewrite slot_view_begin. destruct (decide (c = n)) as [->|]; [simpl; lia|exact (gd_cursor _ G c)].
    + intros c y. change (y ∈ (slot_view (sources_begin_run n (g_edges s)) c).1 -> readable Sh y /\ forall q e b, Sh !! c = Some (KMemo, q, e, b) -> y < c).
      rewrite slot_view_begin. destruct (decide (c = n)) as [->|]; intros Hy; exact (gd_slots _ G _ y Hy).
  - intros c _ _. change ((slot_view (sources_begin_run n (g_edges s)) c).1 = (slot_view (g_edges s) c).1).
    rewrite slot_view_begin. destruct (decide (c = n)) as [->|]; done.
Qed.

Lemma elem_of_take_in {A} (x : A) k (l : list A) : x ∈ take k l -> x ∈ l.
Proof.
  revert k. induction l as [|a l IH]; intros [|k] H; simpl in *; try by apply not_elem_of_nil in H.
  apply elem_of_cons in H as [->|H]; [left|right; exact (IH k H)].
Qed.

Lemma tot_end_run X P n (b : bool) :
  X n -> tot X P (fun _ => edges_opt (if b then sources_clear n else sources_end_run n)).
Proof.
  intros Hn. apply tot_const. intros s G _ _. unfold edges_opt.
  pose proof (gd_reach _ G) as Hre. pose proof (proj1 (reachable_well_formed _ Hre)) as Hem.
  assert (Hex : exists w, (if b then sources_clear n else sources_end_run n) (g_edges s) = Some w /\ world_reachable w /\
     (forall c, c <> n -> slot_view w c = slot_view (g_edges s) c) /\ (slot_view w n).2 <= length (slot_view w n).1 /\ (forall x, x ∈ (slot_view w n).1 -> x ∈ (slot_view (g_edges s) n).1)).
  { destruct b.
    - assert (Ee : exists w, sources_clear n (g_edges s) = Some w).
      { unfold sources_clear. destruct (sources_trim_from_ok n 0 (g_edges s) Hem) as (w1&E&_). rewrite E. simpl. by eexists. }
      destruct Ee as [w Ec]. exists w. split; [exact Ec|].
      split_and!; [exact (reachable_clear _ _ _ Hre Ec)|intros c Hc; rewrite (slot_view_clear _ _ _ _ Ec), decide_False by done; done|rewrite (slot_view_clear _ _ _ _ Ec), decide_True by done; simpl; lia|rewrite (slot_view_clear _ _ _ _ Ec), decide_True by done; simpl; intros x Hx; by apply not_elem_of_nil in Hx].
    - assert (Ee : exists w, sources_end_run n (g_edges s) = Some w).
      { unfold sources_end_run. destruct (decide _); [|by eexists]. destruct (sources_trim_from_ok n (source_cursor (sources_of (g_edges s) n)) (g_edges s) Hem) as (w1&E&_). by rewrite E; eexists. }
      destruct Ee as [w Ee]. exists w. rewrite Ee. split; [done|]. pose proof (slot_view_end _ _ _ n Ee (gd_cursor _ G n)) as Hv0. pose proof (gd_cursor _ G n) as Hcr.
      split_and!; [exact (reachable_end _ _ _ Hre Ee)|intros c Hc; rewrite (slot_view_end _ _ _ c Ee (gd_cursor _ G n)), decide_False by done; done| |].
      + rewrite Hv0, decide_True by done. cbn [fst snd]. rewrite length_take. lia.
      + rewrite Hv0, decide_True by done. cbn [fst snd]. intros x. apply elem_of_take_in. }
  destruct Hex as (w&Ew&Rw&Ho&Hcw&Hsw). rewrite Ew. eexists _, tt. split_and!; [reflexivity| |split; try done].
  - split.
    + exact (gd_sh _ G).
    + exact (gd_scope _ G).
    + exact Rw.
    + intros c. change ((slot_view w c).2 <= length (slot_view w c).1). destruct (decide (c = n)) as [->|Hne]; [exact Hcw|rewrite (Ho c Hne); exact (gd_cursor _ G c)].
    + intros c y. change (y ∈ (slot_view w c).1 -> readable Sh y /\ forall q e b, Sh !! c = Some (KMemo, q, e, b) -> y < c).
      destruct (decide (c = n)) as [->|Hne]; [intros Hy; exact (gd_slots _ G n y (Hsw y Hy))|rewrite (Ho c Hne); exact (gd_slots _ G c y)].
  - intros c Hc _. change ((slot_view w c).1 = (slot_view (g_edges s) c).1). rewrite Ho; [done|]. intros ->. by apply Hc.
Qed.

Lemma tot_settle X P : tot X P (fun f => settle_flush f).
Proof.
  intros s G C _. exists s, tt. split_and!; [|done|apply kept_refl]. exists 2. intros f Hf. destruct f as [|[|f]]; [lia|lia|].
  cbn [settle_flush run_flush]. rewrite bind_get.
  destruct (g_flush_owed s && _ && _) eqn:Eb; [|done]. apply andb_prop in Eb as [Eb _]. apply andb_prop in Eb as [_ Hb]. apply bool_decide_eq_true_1 in Hb.
  assert (Hfl : g_flushing s = true) by (destruct C as [H|H]; [done|lia]). rewrite bind_get, Hfl. done.
Qed.

Lemma tot_announce X P n q e b : Sh !! n = Some (KMemo, q, e, b) -> tot X P (fun _ => announce n).
Proof.
  intros Hs s G C HP. destruct (f_unannounced (g_flags s)) eqn:Ef.
  - assert (Hm : tot X P (fun _ => modify_node n (node_with_unannounced true))).
    { apply tot_modify_node; [|done|done]. intros s0 G0 _. destruct (gd_node s0 n _ G0 Hs) as (d&Hd&_). by eexists. }
    destruct (Hm s G C HP) as (s'&a&[N Ev]&G'&K). exists s', a. split_and!; [|done|done].
    exists N. intros f Hf. unfold announce. rewrite Ef. exact (Ev f Hf).
  - exists s, tt. split_and!; [|done|apply kept_refl]. exists 0. intros f _. unfold announce. by rewrite Ef.
Qed.

Lemma tot_node_at {B} X P n x (k : nat -> node -> M B) :
  Sh !! n = Some x ->
  (forall d, shape_of d = x -> tot X P (fun f => k f d)) ->
  tot X P (fun f => m_bind (get_node n) (k f)).
Proof.
  intros Hs H. apply tot_node.
  - intros s G _. destruct (gd_node s n x G Hs) as (d&Hd&_). by eexists.
  - intros d (s&G&_&Hd). apply H. pose proof (gd_shape s n d G Hd) as Y. rewrite Hs in Y. by injection Y as <-.
Qed.

Section Memo.
Variable n : node_id.
Variables (q0 : comparer) (e0 : env) (b0 : cmd).
Hypothesis Hsn : Sh !! n = Some (KMemo, q0, e0, b0).
Hypothesis Hrd : forall t, t < n -> readable Sh t -> tot (xl n) (pt t) (fun f => read_node f t).
Hypothesis Hup : forall t, t < n -> readable Sh t -> tot (xl n) (pt t) (fun f => update_if_necessary f t).

Lemma xl_mono a b c : a <= b -> xl a c -> xl b c.
Proof. intros H [A B]. split; [lia|done]. Qed.

Lemma xl_self : xl (S n) n.
Proof. split; [lia|by eexists _, _, _]. Qed.

Lemma run_memo_tot : tot (xl (S n)) (pt n) (fun f => run_memo f n).
Proof.
  apply tot_S. cbn [run_memo]. apply (tot_node_at _ _ _ _ _ Hsn). intros d Hd. cbv zeta.
  assert (Hmod : forall u : node -> node, (forall x, shape_of (u x) = shape_of x) -> (forall x, n_scope (u x) = n_scope x) -> tot (xl (S n)) (pt n) (fun _ => modify_node n u)).
  { intros u H1 H2. apply tot_modify_node; [|done|done]. intros s0 G0 _. destruct (gd_node s0 n _ G0 Hsn) as (x&Hx&_). by eexists. }
  apply tot_bind; [apply _|apply Hmod; intros ?; reflexivity|intros _].
  apply tot_bind; [apply _|apply tot_begin|intros _].
  apply tot_bind; [apply _|apply tot_modify; intros ?; split_and!; reflexivity|intros _].
  apply tot_bind; [apply _|apply tot_modify; intros ?; split_and!; reflexivity|intros _].
  unfold shape_of in Hd. injection Hd as Hk Hq He Hb.
  apply tot_bind; [apply _| |intros r].
  { refine (tot_weaken _ _ (fun _ => True) _ _ _ _ _); [done|done|]. apply tot_hosted; [exact xl_self|].
    refine (tot_weaken (xl n) _ _ _ _ (fun c => xl_mono n (S n) c ltac:(lia)) (fun _ H => H) _).
    apply (exec_tot _ n (fun t => t < n /\ readable Sh t) ltac:(intros t [A B]; exact (Hrd t A B)) ltac:(intros t ??? [A _] _; exact A) _ _ e0).
    - rewrite <- He. destruct (n_published d); [apply env_agree_prev|apply env_agree_without].
    - rewrite Hb. exact (Hbm _ _ _ _ Hsn). }
  apply (tot_node_at _ _ _ _ _ Hsn). intros d1 _.
  apply tot_bind; [apply _| |intros _].
  { destruct (if n_violated d1 then Fail pure_scope_failure else r); [apply Hmod; intros ?; reflexivity|apply tot_ret]. }
  apply tot_bind; [apply _|apply tot_end_run; exact xl_self|intros _].
  apply tot_bind; [apply _|apply tot_modify; intros ?; split_and!; reflexivity|intros _].
  apply tot_bind; [apply _| |intros _].
  { destruct (if n_violated d1 then Fail pure_scope_failure else r); [apply tot_ret|apply Hmod; intros ?; reflexivity]. }
  apply (tot_node_at _ _ _ _ _ Hsn). intros d2 _.
  apply tot_bind; [apply _| |intros moved].
  { destruct (decide _); [apply tot_ret|]. destruct (n_comparer d2); try apply tot_ret.
    apply tot_bind; [apply _|apply Hmod; intros ?; reflexivity|intros _]. apply tot_ret. }
  apply tot_bind; [apply _|apply tot_get|intros s1].
  apply tot_bind; [apply _|apply tot_modify; intros s9; unfold ghost_run_end; destruct (g_nodes s9 !! n); split_and!; reflexivity|intros _].
  apply tot_bind; [apply _| |intros _].
  { destruct moved; [apply tot_notify_except|apply tot_ret]. }
  apply (tot_node_at _ _ _ _ _ Hsn). intros d3 _. destruct (decide _); [apply tot_ret|exact (tot_announce _ _ _ _ _ _ Hsn)].
Qed.

Lemma recompute_tot : tot (xl (S n)) (pt n) (fun f => recompute f n).
Proof.
  apply tot_S. cbn [recompute]. intros s G C HP. destruct (gd_node s n _ G Hsn) as (d&Hd&Hsd).
  pose proof Hsd as Y. unfold shape_of in Y. injection Y as Hk _ _ _.
  pose proof (gd_scope _ G n d Hd Hk) as Hsc.
  destruct (run_memo_tot s G C HP) as (s'&a&[N Ev]&G'&K). exists s', a. split_and!; [|done|done].
  exists N. intros f Hf. unfold m_bind, get_node. rewrite Hd, Hsc. exact (Ev f Hf).
Qed.

Lemma pt_ne s : pt n s -> g_current s <> Some n.
Proof. intros H Hc. pose proof (H n _ _ _ Hc Hsn). lia. Qed.

Lemma pt_below t s : t < n -> pt n s -> pt t s.
Proof. intros Ht H c q e b Hc Hs. pose proof (H c q e b Hc Hs). lia. Qed.

End Memo.

(** The walk over the slots of [n]. Each step brings a source current, which keeps [n]'s slots. *)
Lemma walk_tot X P `{!TS P} n x0 :
  ~ X n -> Sh !! n = Some x0 -> (forall s, P s -> g_current s <> Some n) ->
  (forall src, readable Sh src -> (forall q e b, Sh !! n = Some (KMemo, q, e, b) -> src < n) -> tot X P (fun f => update_if_necessary f src)) ->
  forall k i s, gd s -> tk_ctx s -> P s -> length (slot_view (g_edges s) n).1 <= i + k ->
    exists s' a, ev_m (fun f => resolve_walk f n i) s (s', a) /\ gd s' /\ kept X s s'.
Proof.
  intros HX Hsx Hne Hsrc. induction k as [|k IH]; intros i s G C HP Hl.
  all: destruct (gd_node s n x0 G Hsx) as (d&Hd&_).
  all: destruct (decide (n_freshness d = Check)) as [Hch|Hch].
  2, 4: exists s, tt; split_and!; [|done|apply kept_refl]; exists 1; intros f Hf; destruct f as [|f]; [lia|]; cbn [resolve_walk];
        rewrite (bind_get_node _ _ _ _ Hd), bind_get, decide_False by done; reflexivity.
  - exists s, tt. split_and!; [|done|apply kept_refl]. exists 1. intros f Hf. destruct f as [|f]; [lia|]. cbn [resolve_walk].
    rewrite (bind_get_node _ _ _ _ Hd), bind_get, decide_True by done. rewrite (proj2 (lookup_ge_None _ _)); [reflexivity|]. rewrite slots_length. lia.
  - destruct (source_slots (sources_of (g_edges s) n) !! i) as [[src mh]|] eqn:Es.
    2: { exists s, tt. split_and!; [|done|apply kept_refl]. exists 1. intros f Hf. destruct f as [|f]; [lia|]. cbn [resolve_walk].
         rewrite (bind_get_node _ _ _ _ Hd), bind_get, decide_True by done. rewrite Es. reflexivity. }
    assert (Hin : src ∈ (slot_view (g_edges s) n).1).
    { apply list_elem_of_lookup. exists i. unfold slot_view. simpl. rewrite list_lookup_fmap, Es. reflexivity. }
    destruct (gd_slots _ G n src Hin) as [Hrs Hlow].
    destruct (Hsrc src Hrs Hlow s G C HP) as (s1&a1&[N1 Ev1]&G1&K1).
    assert (C1 : tk_ctx s1) by (unfold tk_ctx in *; rewrite (kp_flu _ _ _ K1), (kp_bat _ _ _ K1); exact C).
    assert (Hsame : (slot_view (g_edges s1) n).1 = (slot_view (g_edges s) n).1) by exact (kp_slots _ _ _ K1 n HX (Hne s HP)).
    destruct (IH (S i) s1 G1 C1 (ts _ _ _ G G1 K1 HP) ltac:(rewrite Hsame; lia)) as (s2&a2&[N2 Ev2]&G2&K2).
    exists s2, a2. split_and!; [|done|exact (kept_trans _ _ _ _ K1 K2)].
    exists (S (N1 `max` N2)). intros f Hf. destruct f as [|f]; [lia|]. cbn [resolve_walk].
    rewrite (bind_get_node _ _ _ _ Hd), bind_get, decide_True by done. rewrite Es. unfold m_bind. rewrite (Ev1 f) by lia. apply Ev2. lia.
Qed.

Lemma tot_refresh_of n q0 e0 b0 :
  Sh !! n = Some (KMemo, q0, e0, b0) ->
  (forall t, t < n -> readable Sh t -> tot (xl n) (pt t) (fun f => read_node f t)) ->
  (forall t, t < n -> readable Sh t -> tot (xl n) (pt t) (fun f => update_if_necessary f t)) ->
  tot (xl (S n)) (pt n) (fun f => refresh f n).
Proof.
  intros Hsn Hrd Hup. apply tot_S. cbn [refresh]. apply (tot_node_at _ _ _ _ _ Hsn). intros d _.
  assert (Hmod : forall u : node -> node, (forall x, shape_of (u x) = shape_of x) -> (forall x, n_scope (u x) = n_scope x) -> tot (xl (S n)) (pt n) (fun _ => modify_node n u)).
  { intros u H1 H2. apply tot_modify_node; [|done|done]. intros s0 G0 _. destruct (gd_node s0 n _ G0 Hsn) as (x&Hx&_). by eexists. }
  apply tot_bind; [apply _| |intros _].
  - destruct (decide _); [|apply tot_ret]. apply tot_bind; [apply _| |intros _].
    + assert (W : tot (xl n) (pt n) (fun f => resolve_walk f n 0)).
      { intros s G C HP. refine (walk_tot (xl n) (pt n) n _ _ _ _ _ (length (slot_view (g_edges s) n).1) 0 s G C HP _).
        - intros [A _]. lia.
        - exact Hsn.
        - intros s0 HH Hc. pose proof (HH n _ _ _ Hc Hsn). lia.
        - intros src Hr Hl. refine (tot_weaken _ _ _ _ _ _ _ (Hup src (Hl _ _ _ Hsn) Hr)); [done|]. intros s0 H c q1 e1 b1 Hc Hs. pose proof (H c q1 e1 b1 Hc Hs). pose proof (Hl _ _ _ Hsn). lia.
        - lia. }
      refine (tot_weaken _ _ _ _ _ _ _ W); [|done]. intros c. apply xl_mono. lia.
    + apply (tot_node_at _ _ _ _ _ Hsn). intros d' _. destruct (decide _); [|apply tot_ret].
      apply tot_bind; [apply _|apply tot_modify; intros s9; split_and!; reflexivity|intros _]. apply Hmod; intros ?; reflexivity.
  - apply (tot_node_at _ _ _ _ _ Hsn). intros d' _. destruct (decide _); [exact (recompute_tot n _ _ _ Hsn Hrd)|apply tot_ret].
Qed.

Lemma tot_pull_of n q0 e0 b0 :
  Sh !! n = Some (KMemo, q0, e0, b0) -> tot (xl (S n)) (pt n) (fun f => refresh f n) -> tot (xl (S n)) (pt n) (fun f => refresh_or_pull f n).
Proof.
  intros Hsn Hr. apply tot_S. cbn [refresh_or_pull]. apply tot_bind; [apply _|apply tot_get|intros s0].
  assert (Hup : forall k : nat -> M unit, tot (xl (S n)) (pt n) k -> tot (xl (S n)) (pt n) k) by done.
  destruct (deferring s0); [exact Hr|]. apply tot_S. cbn [pull].
  apply tot_bind; [apply _|apply tot_modify; intros ?; split_and!; reflexivity|intros _].
  apply tot_bind; [apply _|exact Hr|intros _].
  apply tot_bind; [apply _|apply tot_modify; intros ?; split_and!; reflexivity|intros _].
  apply tot_bind; [apply _|apply tot_get|intros s1]. destruct (_ && _); [apply tot_settle|apply tot_ret].
Qed.

(** Reads and refreshes of every node below [lim] end. *)
Definition below (lim : node_id) : Prop :=
  forall t, t < lim -> readable Sh t ->
    tot (xl lim) (pt t) (fun f => read_node f t) /\ tot (xl lim) (pt t) (fun f => update_if_necessary f t).

Lemma below_step lim : below lim -> below (S lim).
Proof.
  intros IH t Ht Hr.
  assert (Hsub : forall (P : state -> Prop) {A} (F : nat -> M A), tot (xl lim) P F -> tot (xl (S lim)) P F).
  { intros P A F H. refine (tot_weaken _ _ _ _ _ _ _ H); [|done]. intros c. apply xl_mono. lia. }
  destruct (decide (t < lim)) as [Hl|Hl].
  { destruct (IH t Hl Hr) as [A B]. split; apply Hsub; done. }
  assert (Ht0 : t = lim) by lia. subst t. clear Hl Ht.
  unfold readable in Hr. destruct (Sh !! lim) as [[[[k c] e] b]|] eqn:Hs; [|done].
  assert (Hbl : forall t, t < lim -> readable Sh t -> tot (xl lim) (pt t) (fun f => read_node f t)) by (intros u A B; exact (proj1 (IH u A B))).
  assert (Hbu : forall t, t < lim -> readable Sh t -> tot (xl lim) (pt t) (fun f => update_if_necessary f t)) by (intros u A B; exact (proj2 (IH u A B))).
  destruct k; try done.
  - split.
    + apply tot_S. cbn [read_node]. apply (tot_node_at _ _ _ _ _ Hs). intros d Hd. unfold shape_of in Hd. injection Hd as -> _ _ _.
      apply tot_bind; [apply _|apply tot_track; unfold readable; rewrite Hs; done|intros _]. apply tot_ret.
    + apply tot_S. cbn [update_if_necessary]. apply (tot_node_at _ _ _ _ _ Hs). intros d Hd. unfold shape_of in Hd. injection Hd as -> _ _ _. apply tot_ret.
  - assert (Hrop : tot (xl (S lim)) (pt lim) (fun f => refresh_or_pull f lim)).
    { apply (tot_pull_of _ _ _ _ Hs). exact (tot_refresh_of _ _ _ _ Hs Hbl Hbu). }
    split.
    + apply tot_S. cbn [read_node]. apply (tot_node_at _ _ _ _ _ Hs). intros d Hd. unfold shape_of in Hd. injection Hd as -> _ _ _.
      apply tot_bind; [apply _| |intros _].
      * destruct (stale_live d).
        -- apply tot_bind; [apply _|exact Hrop|intros _].
           apply tot_bind; [apply _|apply tot_track; unfold readable; rewrite Hs; done|intros _].
           apply (tot_node_at _ _ _ _ _ Hs). intros d' _. destruct (stale_live d'); [apply tot_mark_reader|apply tot_ret].
        -- apply tot_track. unfold readable; rewrite Hs; done.
      * apply (tot_node_at _ _ _ _ _ Hs). intros d' _. destruct (n_status d'); apply tot_ret.
    + apply tot_S. cbn [update_if_necessary]. apply (tot_node_at _ _ _ _ _ Hs). intros d Hd. unfold shape_of in Hd. injection Hd as -> _ _ _.
      destruct (stale_live d); [exact Hrop|apply tot_ret].
Qed.

(**md
**A read or a walk step on any readable node ends: the rank of the id bounds the memo calls, the body bounds each run and the slot count bounds each walk.**

Every memo body in `Sh` meets `body_ok`, with its reads below its own id. From a state of `gd` under a flush or a batch, `read_node` and `update_if_necessary` end at enough fuel, and the result is in `gd`.
*)
Theorem reads_end lim : below lim.
Proof.
  induction lim as [|lim IH]; [intros t Ht; lia|]. by apply below_step.
Qed.

End Total.

(** ** The engine program of a reference program *)

(** Moves a body's variables off [prev_var], the variable the engine binds to a memo's previous value. *)
Definition rv (x : var) : var := if Nat.ltb x prev_var then x else S x.

Fixpoint rename_expr (e : expr) : expr :=
  match e with
  | EConst z => EConst z
  | EVar x => EVar (rv x)
  | EPrev => EPrev
  | EAdd a b => EAdd (rename_expr a) (rename_expr b)
  | ESub a b => ESub (rename_expr a) (rename_expr b)
  | EMul a b => EMul (rename_expr a) (rename_expr b)
  | EDiv a b => EDiv (rename_expr a) (rename_expr b)
  | ELt a b => ELt (rename_expr a) (rename_expr b)
  | EEq a b => EEq (rename_expr a) (rename_expr b)
  end.

(** Reference node [n] is engine node [S n]: the engine's first id is 1, and a reference program can use 0. *)
Definition rename_target (e : expr) : expr := EAdd (rename_expr e) (EConst 1%Z).

(** A body with its variables renamed and its targets shifted. A form outside the reference tier's fragment stays as it is:
    a well-formed program holds none. *)
Fixpoint rename (c : cmd) : cmd :=
  match c with
  | CRet e => CRet (rename_expr e)
  | CThrow f => CThrow f
  | CLet x e k => CLet (rv x) (rename_expr e) (rename k)
  | CIf e a b => CIf (rename_expr e) (rename a) (rename b)
  | CRead x n k => CRead (rv x) (rename_target n) (rename k)
  | CTryRead x ok n k => CTryRead (rv x) (rv ok) (rename_target n) (rename k)
  | CReadUntracked x n k => CReadUntracked (rv x) (rename_target n) (rename k)
  | CWrite n e k => CWrite (rename_target n) (rename_expr e) (rename k)
  | CCleanup b k => CCleanup (rename b) (rename k)
  | CBatch b k => CBatch (rename b) (rename k)
  | CLog e k => CLog (rename_expr e) (rename k)
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => c
  end.

(** A body with no result. *)
Definition stuck_cmd : cmd := CRet (EVar 0).

Definition done_cmd : cmd := CRet (EConst 0%Z).

(** Creates the node at an id. An id the program does not use holds a memo with no result, so a read of it has no result, as
    in the reference, and later ids keep their places. A memo takes [Pure] and [CmpEq]. *)
Definition node_cmd (p : Semantics.program) (n : node_id) (k : cmd) : cmd :=
  match Semantics.program_signals p !! n, Semantics.program_memos p !! n, Semantics.program_effects p !! n with
  | Some v, _, _ => CSignal prev_var (EConst v) k
  | None, Some c, _ => CMemo prev_var Pure CmpEq (rename c) k
  | None, None, Some c => CEffect prev_var (rename c) k
  | None, None, None => CMemo prev_var Pure CmpEq stuck_cmd k
  end.

(** One more than every id of the program. *)
Definition node_bound (p : Semantics.program) : nat :=
  S (max_list (elements (dom (Semantics.program_signals p) ∪ dom (Semantics.program_memos p) ∪ dom (Semantics.program_effects p)))).

Definition setup_cmd (p : Semantics.program) : cmd := foldr (node_cmd p) done_cmd (seq 0 (node_bound p)).

Definition engine_id (n : node_id) : expr := EConst (Z.of_nat (S n)).

(** A host input. A host read is a [CTryRead], so a failed memo's failure is the read's answer and the program goes on. A
    disposal of a node the reference has no memo or effect at has no result. *)
Definition input_cmd (p : Semantics.program) (i : Semantics.host_input) (k : cmd) : cmd :=
  match i with
  | Semantics.RWrite t v => CWrite (engine_id t) (EConst v) k
  | Semantics.RRead n => CTryRead prev_var prev_var (engine_id n) k
  | Semantics.RDispose n =>
    match Semantics.program_memos p !! n, Semantics.program_effects p !! n with
    | None, None => stuck_cmd
    | _, _ => CDispose (engine_id n) k
    end
  end.

Definition batch_cmd (p : Semantics.program) (b : list Semantics.host_input) (k : cmd) : cmd :=
  CBatch (foldr (input_cmd p) done_cmd b) k.

Definition history_cmd (p : Semantics.program) (h : Semantics.history) : cmd := foldr (batch_cmd p) done_cmd h.

(** The engine program for a reference program and a history: every node created in one batch, in id order, then each batch of
    the history. Each closing batch flushes, as settling does in the reference. *)
Definition erase (p : Semantics.program) (h : Semantics.history) : cmd := CBatch (setup_cmd p) (history_cmd p h).

(** An event in the reference's ids. *)
Definition event_back (ev : event) : event :=
  match ev with
  | EvRun n => EvRun (pred n)
  | EvLog n v => EvLog (pred n) v
  | EvCleanup n => EvCleanup (pred n)
  | EvFail n f => EvFail (pred n) f
  | EvDispose n => EvDispose (pred n)
  | EvHostRead n o => EvHostRead (pred n) o
  end.

(** An event the reference can hold. The reference discards a memo's logs, so only an effect's events and the host's stay. *)
Definition event_kept (p : Semantics.program) (ev : event) : bool :=
  match ev with
  | EvRun n | EvLog n _ | EvCleanup n | EvFail n _ => bool_decide (is_Some (Semantics.program_effects p !! pred n))
  | EvDispose _ | EvHostRead _ _ => true
  end.

Definition project (p : Semantics.program) (t : list event) : list event :=
  event_back <$> filter (fun ev => event_kept p ev = true) t.

Definition engine_of (fl : flags) (k : nat) (p : Semantics.program) (h : Semantics.history) : option (list event) :=
  project p <$> trace_of k fl (erase p h).

(** ** The order of a flush *)

(** The entries of [l] in the order of [q], each value's copies together, then the entries [q] does not hold. *)
Fixpoint order_by (q l : list node_id) : list node_id :=
  match q with
  | [] => l
  | x :: q' => filter (fun y => y = x) l ++ order_by q' (filter (fun y => y <> x) l)
  end.

(** The queue a flush starts from, in the reference's ids. *)
Definition live_queue_of (s : state) : list node_id := pred <$> drop (g_queue_head s) (g_queue s).

Definition engine_order (s : state) : Semantics.effect_order := fun _ l => order_by (live_queue_of s) l.

(** ** A body read through its answers, in the reference *)

(** The grammar of a memo of the reference tier, and of an effect that only reads: no write, cleanup or untracked read. *)
Fixpoint frag_body (c : cmd) : bool :=
  match c with
  | CRet _ | CThrow _ => true
  | CLet _ _ k | CRead _ _ k | CTryRead _ _ _ k | CLog _ k => frag_body k
  | CIf _ a b | CBatch a b => frag_body a && frag_body b
  | _ => false
  end.

(** The tracked reads of a body whose reads are answered by [B]. *)
Fixpoint tracked (B : node_id -> option outcome) (g : env) (c : cmd) : list (node_id * outcome) :=
  match c with
  | CLet x v k => match ev g v with Some (Ok z) => tracked B ((x, z) :: g) k | _ => [] end
  | CIf v a b =>
    match ev g v with
    | Some (Ok z) => if decide (z = 0%Z) then tracked B g b else tracked B g a
    | _ => []
    end
  | CRead x n k =>
    match ev g n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match B (Z.to_nat z) with
        | Some (Ok v) => (Z.to_nat z, Ok v) :: tracked B ((x, v) :: g) k
        | Some (Fail y) => [(Z.to_nat z, Fail y)]
        | None => []
        end
      else []
    | _ => []
    end
  | CTryRead x ok n k =>
    match ev g n with
    | Some (Ok z) =>
      if decide (0 <= z)%Z then
        match B (Z.to_nat z) with
        | Some (Ok v) => (Z.to_nat z, Ok v) :: tracked B ((ok, 1%Z) :: (x, v) :: g) k
        | Some (Fail y) => (Z.to_nat z, Fail y) :: tracked B ((ok, 0%Z) :: (x, y) :: g) k
        | None => []
        end
      else []
    | _ => []
    end
  | CLog v k => match ev g v with Some (Ok _) => tracked B g k | _ => [] end
  | CBatch b k =>
    tracked B g b ++ match body_eval B g b with Some (Ok _) => tracked B g k | _ => [] end
  | _ => []
  end.

(** A body state after reads [R] and events [E]. *)
Definition adv (st : Semantics.body_state) (R : list (node_id * outcome)) (E : list event) : Semantics.body_state :=
  {| Semantics.body_signals := Semantics.body_signals st; Semantics.body_reads := Semantics.body_reads st ++ R;
     Semantics.body_all_reads := Semantics.body_all_reads st ++ R; Semantics.body_cleanups := Semantics.body_cleanups st;
     Semantics.body_events := Semantics.body_events st ++ E; Semantics.body_writes := Semantics.body_writes st;
     Semantics.body_retired := Semantics.body_retired st |}.

Lemma adv_nil st : adv st [] [] = st.
Proof. destruct st. unfold adv. simpl. by rewrite !app_nil_r. Qed.

Lemma adv_adv st R1 E1 R2 E2 : adv (adv st R1 E1) R2 E2 = adv st (R1 ++ R2) (E1 ++ E2).
Proof. unfold adv. simpl. by rewrite <- !app_assoc. Qed.

Lemma track_adv t o st : Semantics.body_track t o st = adv st [(t, o)] [].
Proof. unfold adv, Semantics.body_track. by rewrite app_nil_r. Qed.

Lemma emit_adv e st : Semantics.body_emit e st = adv st [] [e].
Proof. unfold adv, Semantics.body_emit. by rewrite !app_nil_r. Qed.

Lemma body_logs_of_eval (A : node_id -> option outcome) e c r :
  body_eval A e c = Some r -> exists l, body_logs A e c = Some l.
Proof.
  revert e r. induction c; intros g r0; simpl; try done; try (intros; by eexists).
  - destruct (ev g e) as [o|]; [intros; by eexists|done].
  - destruct (ev g e) as [[z|y]|]; [apply IHc|intros; by eexists|done].
  - destruct (ev g e) as [[z|y]|]; [|intros; by eexists|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|intros; by eexists|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|]; [apply IHc|intros; by eexists|done].
  - destruct (ev g n) as [[z|y]|]; [|intros; by eexists|done]. destruct (decide _); [|done].
    destruct (A _) as [[v|y]|]; [apply IHc|apply IHc|done].
  - destruct (body_eval A g c1) as [[z|y]|] eqn:E1; intros H; [|injection H as <-|done].
    + destruct (IHc1 _ _ E1) as [l1 ->]. destruct (IHc2 _ _ H) as [l2 ->]. by eexists.
    + destruct (IHc1 _ _ E1) as [l1 ->]. by eexists.
  - destruct (ev g e) as [[z|y]|]; [|intros; by eexists|done]. intros H. destruct (IHc _ _ H) as [l ->]. by eexists.
Qed.

(** A body of the grammar runs in the reference as [body_eval] and [body_logs] say, over the answers the reader gives. *)
Lemma rx_run p (rd : Semantics.reader) (B : node_id -> option outcome) n c :
  frag_body c = true ->
  forall g st r l,
    (forall t, rd (Semantics.body_signals st) t = B t) ->
    body_eval B g c = Some r -> body_logs B g c = Some l ->
    Semantics.exec p rd n g c st = Some (r, adv st (tracked B g c) (EvLog n <$> l)).
Proof.
  induction c; intros Hf g st r0 l0 Hrd He Hl; simpl in *; try done; rewrite ?andb_true_iff in Hf;
    change (eval_expr g None) with (ev g) in *.
  - destruct (ev g e) as [o|]; [|done]. injection He as <-. injection Hl as <-. simpl. rewrite adv_nil. done.
  - injection He as <-. injection Hl as <-. rewrite adv_nil. done.
  - destruct (ev g e) as [[z|y]|]; simpl; [|injection He as <-; injection Hl as <-; rewrite adv_nil; done|done].
    apply IHc; auto.
  - destruct Hf as [Hf1 Hf2]. destruct (ev g e) as [[z|y]|]; simpl; [|injection He as <-; injection Hl as <-; rewrite adv_nil; done|done].
    destruct (decide _); auto.
  - destruct (ev g n0) as [[z|y]|]; simpl; [|injection He as <-; injection Hl as <-; rewrite adv_nil; done|done].
    unfold Semantics.node_of. destruct (decide _) as [Hz|Hz]; [|done]. simpl. rewrite Hrd.
    destruct (B _) as [[v|y]|] eqn:Eb; simpl; [|injection He as <-; injection Hl as <-|done].
    + rewrite track_adv, (IHc Hf _ (adv st _ _) _ _ Hrd He Hl), adv_adv. done.
    + rewrite track_adv. done.
  - destruct (ev g n0) as [[z|y]|]; simpl; [|injection He as <-; injection Hl as <-; rewrite adv_nil; done|done].
    unfold Semantics.node_of. destruct (decide _) as [Hz|Hz]; [|done]. simpl. rewrite Hrd.
    destruct (B _) as [[v|y]|] eqn:Eb; simpl; [| |done].
    + rewrite track_adv, (IHc Hf _ (adv st _ _) _ _ Hrd He Hl), adv_adv. done.
    + rewrite track_adv, (IHc Hf _ (adv st _ _) _ _ Hrd He Hl), adv_adv. done.
  - destruct Hf as [Hf1 Hf2]. destruct (body_eval B g c1) as [[z|y]|] eqn:E1; [|injection He as <-|done];
      destruct (body_logs_of_eval _ _ _ _ E1) as [l1 E2]; rewrite E2 in Hl.
    + destruct (body_logs B g c2) as [l2|] eqn:E3; simpl in Hl; [|done]. injection Hl as <-.
      rewrite (IHc1 Hf1 _ _ _ _ Hrd E1 E2). simpl.
      rewrite (IHc2 Hf2 _ (adv st _ _) _ _ Hrd He E3), adv_adv, fmap_app. done.
    + injection Hl as <-. rewrite (IHc1 Hf1 _ _ _ _ Hrd E1 E2). simpl. rewrite app_nil_r. done.
  - destruct (ev g e) as [[z|y]|]; simpl; [|injection He as <-; injection Hl as <-; rewrite adv_nil; done|done].
    destruct (body_logs B g c) as [l1|] eqn:E1; simpl in Hl; [|done]. injection Hl as <-.
    rewrite emit_adv, (IHc Hf _ (adv st _ _) _ _ Hrd He E1), adv_adv. done.
Qed.

(** Whether some read differs from the answers [B], oldest first. *)
Fixpoint moved_by (B : node_id -> option outcome) (R : list (node_id * outcome)) : option bool :=
  match R with
  | [] => Some false
  | (t, o) :: R' =>
    cur ← B t;
    if decide (cur = o) then moved_by B R' else Some true
  end.

Lemma reads_moved_by p frz sigs R :
  Semantics.reads_moved p frz sigs R = moved_by (Semantics.memo_value p frz sigs) R.
Proof. induction R as [|[t o] R IH]; [done|]. simpl. destruct (Semantics.memo_value p frz sigs t) as [cur|]; simpl; [|done]. by rewrite IH. Qed.

Lemma moved_by_app B R1 R2 :
  moved_by B (R1 ++ R2) = (m ← moved_by B R1; if (m : bool) then Some true else moved_by B R2).
Proof.
  induction R1 as [|[t o] R1 IH]; [done|]. simpl. destruct (B t) as [cur|]; simpl; [|done]. destruct (decide _); [done|done].
Qed.

Lemma moved_self (B : node_id -> option outcome) g c r :
  body_eval B g c = Some r -> moved_by B (tracked B g c) = Some false.
Proof.
  revert g r. induction c; intros g r0; simpl; try done.
  - destruct (ev g e) as [[z|y]|]; [apply IHc|done|done].
  - destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (B _) as [[v|y]|] eqn:Eb; [|intros _; simpl; rewrite Eb; simpl; by rewrite decide_True|done].
    intros H. simpl. rewrite Eb. simpl. rewrite decide_True by done. exact (IHc _ _ H).
  - destruct (ev g n) as [[z|y]|]; [|done|done]. destruct (decide _); [|done].
    destruct (B _) as [[v|y]|] eqn:Eb; [| |done]; intros H; simpl; rewrite Eb; simpl; rewrite decide_True by done; exact (IHc _ _ H).
  - destruct (body_eval B g c1) as [[z|y]|] eqn:E1; intros H; [|injection H as <-|done].
    + rewrite moved_by_app, (IHc1 _ _ E1). simpl. exact (IHc2 _ _ H).
    + rewrite moved_by_app, (IHc1 _ _ E1). done.
  - destruct (ev g e) as [[z|y]|]; [apply IHc|done|done].
Qed.

Lemma moved_cons B t o R cur : B t = Some cur -> moved_by B ((t, o) :: R) = if decide (cur = o) then moved_by B R else Some true.
Proof. intros H. simpl. rewrite H. reflexivity. Qed.

(** A body evaluated over [Bo] and over [B]: its reads under [Bo] answer whether anything moved, and with nothing moved the
    body gives one result, one list of logs and one list of reads. *)
Lemma moved_path (Bo B : node_id -> option outcome) g c ro r :
  body_eval Bo g c = Some ro -> body_eval B g c = Some r ->
  exists m, moved_by B (tracked Bo g c) = Some m /\
    (m = false -> r = ro /\ body_logs B g c = body_logs Bo g c /\ tracked B g c = tracked Bo g c).
Proof.
  revert g ro r. induction c; intros g r0 r1; simpl; try (intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence).
  - destruct (ev g e) as [[z|y]|]; [apply IHc|intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence|done].
  - destruct (ev g e) as [[z|y]|]; [|intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence|done]. destruct (decide _); [apply IHc2|apply IHc1].
  - destruct (ev g n) as [[z|y]|]; [|intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence|done]. destruct (decide _); [|done].
    destruct (Bo _) as [[v|y]|] eqn:Eo; [| |done]; destruct (B _) as [[w|y']|] eqn:En; try done; intros H1 H2; cbv beta iota; rewrite (moved_cons _ _ _ _ _ En).
    + destruct (decide (Ok w = Ok v)) as [Hq|Hq].
      * injection Hq as ->. destruct (IHc _ _ _ H1 H2) as (m&Hm&Hk). exists m. split; [done|]. intros Hf. destruct (Hk Hf) as (A1&A2&A3). split_and!; [done|done|by rewrite A3].
      * exists true. done.
    + rewrite decide_False by done. exists true. done.
    + rewrite decide_False by done. exists true. done.
    + destruct (decide (Fail y' = Fail y)) as [Hq|Hq]; [injection Hq as ->|exists true; done].
      exists false. split; [done|]. intros _. split_and!; congruence.
  - destruct (ev g n) as [[z|y]|]; [|intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence|done]. destruct (decide _); [|done].
    destruct (Bo _) as [[v|y]|] eqn:Eo; [| |done]; destruct (B _) as [[w|y']|] eqn:En; try done; intros H1 H2; cbv beta iota; rewrite (moved_cons _ _ _ _ _ En).
    + destruct (decide (Ok w = Ok v)) as [Hq|Hq].
      * injection Hq as ->. destruct (IHc _ _ _ H1 H2) as (m&Hm&Hk). exists m. split; [done|]. intros Hf. destruct (Hk Hf) as (A1&A2&A3). split_and!; [done|done|by rewrite A3].
      * exists true. done.
    + rewrite decide_False by done. exists true. done.
    + rewrite decide_False by done. exists true. done.
    + destruct (decide (Fail y' = Fail y)) as [Hq|Hq]; [injection Hq as ->|exists true; done].
      destruct (IHc _ _ _ H1 H2) as (m&Hm&Hk). exists m. split; [done|]. intros Hf. destruct (Hk Hf) as (A1&A2&A3). split_and!; [done|done|by rewrite A3].
  - destruct (body_eval Bo g c1) as [ro1|] eqn:E1; [|done]. destruct (body_eval B g c1) as [r2|] eqn:E2; [|destruct ro1; done].
    destruct (IHc1 _ _ _ E1 E2) as (m1&Hm1&Hk1). rewrite moved_by_app, Hm1. simpl. destruct m1.
    + intros _ _. exists true. done.
    + destruct (Hk1 eq_refl) as (->&A2&A3). rewrite A2, A3. destruct ro1 as [z|y].
      * intros H1 H2. destruct (IHc2 _ _ _ H1 H2) as (m&Hm&Hk). exists m. split; [done|]. intros Hf. destruct (Hk Hf) as (B1&B2&B3). rewrite B2, B3. split_and!; done.
      * intros H1 H2. exists false. rewrite app_nil_r. split; [done|]. intros _. split_and!; congruence.
  - destruct (ev g e) as [[z|y]|]; [|intros H1 H2; exists false; split; [done|]; intros _; split_and!; congruence|done].
    intros H1 H2. destruct (IHc _ _ _ H1 H2) as (m&Hm&Hk). exists m. split; [done|]. intros Hf. destruct (Hk Hf) as (A1&A2&A3). split_and!; [done|by rewrite A2|done].
Qed.

(** ** The reference over a program whose effects only read *)

Definition blk (n : node_id) (r : outcome) (l : list val) : list event :=
  EvRun n :: (EvLog n <$> l) ++ fail_evs n r.

Definition blkF (F : node_id -> outcome * list val) (n : node_id) : list event := blk n (F n).1 (F n).2.

Section RefRun.
Variable p : Semantics.program.
Hypothesis Hfrag : forall n c, Semantics.program_effects p !! n = Some c -> frag_body c = true.

Lemma ref_effect frz sigs n c r l :
  frag_body c = true ->
  body_eval (Semantics.memo_value p frz sigs) [] c = Some r -> body_logs (Semantics.memo_value p frz sigs) [] c = Some l ->
  exists bs, Semantics.run_effect p (Semantics.live_read p frz) n c [] sigs = Some bs /\
    Semantics.body_signals bs = sigs /\ Semantics.body_reads bs = tracked (Semantics.memo_value p frz sigs) [] c /\
    Semantics.body_cleanups bs = [] /\ Semantics.body_events bs = blk n r l.
Proof.
  intros Hf He Hl. set (st0 := Semantics.body_emit (EvRun n) (Semantics.body_untracked (Semantics.body_start sigs))).
  assert (Hr : Semantics.run_effect p (Semantics.live_read p frz) n c [] sigs =
    (r0 ← Semantics.exec p (Semantics.live_read p frz) n [] c st0; Some (Semantics.note_fail n r0.1 r0.2))) by reflexivity.
  rewrite Hr, (rx_run p (Semantics.live_read p frz) (Semantics.memo_value p frz sigs) n c Hf [] st0 r l (fun t => eq_refl) He Hl). simpl.
  eexists. split; [reflexivity|]. destruct r as [v|y]; simpl; split_and!; try reflexivity; unfold blk; simpl; rewrite ?app_nil_r; reflexivity.
Qed.

Lemma ref_round (F : node_id -> outcome * list val) ns :
  forall st,
    (forall n, n ∈ ns -> exists c, Semantics.program_effects p !! n = Some c /\
       body_eval (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c = Some (F n).1 /\
       body_logs (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c = Some (F n).2 /\
       (Semantics.effect_entry st n).2 = []) ->
    exists st' rs, Semantics.run_round p ns st = Some (st', rs) /\
      Semantics.rs_signals st' = Semantics.rs_signals st /\ Semantics.rs_frozen st' = Semantics.rs_frozen st /\
      Semantics.rs_disposed st' = Semantics.rs_disposed st /\ Semantics.rs_round st' = Semantics.rs_round st /\
      (forall m c, m ∈ ns -> Semantics.program_effects p !! m = Some c ->
         Semantics.rs_effects st' !! m = Some (tracked (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c, [])) /\
      (forall m, m ∉ ns -> Semantics.rs_effects st' !! m = Semantics.rs_effects st !! m) /\
      concat (Semantics.run_events <$> rs) = concat (blkF F <$> ns).
Proof.
  induction ns as [|n ns IH]; intros st H.
  - exists st, []. split_and!; try done. intros m c Hm. by apply not_elem_of_nil in Hm.
  - destruct (H n (list_elem_of_here _ _)) as (c&Hc&He&Hl&Hcs). simpl. rewrite Hc. simpl. rewrite Hcs.
    destruct (ref_effect _ _ n c _ _ (Hfrag n c Hc) He Hl) as (bs&Hb&Bs&Br&Bc&Bv). rewrite Hb. simpl.
    set (st1 := {| Semantics.rs_signals := Semantics.body_signals bs; Semantics.rs_effects := <[n := (Semantics.body_reads bs, Semantics.body_cleanups bs)]> (Semantics.rs_effects st);
                   Semantics.rs_frozen := Semantics.rs_frozen st; Semantics.rs_disposed := Semantics.rs_disposed st; Semantics.rs_round := Semantics.rs_round st |}).
    assert (Hs1 : Semantics.rs_signals st1 = Semantics.rs_signals st) by exact Bs.
    assert (Hf1 : Semantics.rs_frozen st1 = Semantics.rs_frozen st) by reflexivity.
    destruct (IH st1) as (st2&rs&E2&S2&Fz2&D2&R2&In2&Out2&Ev2).
    { intros m Hm. destruct (H m (list_elem_of_further _ _ _ Hm)) as (c1&Hc1&He1&Hl1&Hcs1). exists c1. rewrite Hs1, Hf1. split_and!; [done|done|done|].
      unfold Semantics.effect_entry in *. simpl. destruct (decide (m = n)) as [->|Hne].
      - rewrite lookup_insert_eq. simpl. done.
      - rewrite lookup_insert_ne by done. done. }
    rewrite E2. simpl. eexists _, _. split; [reflexivity|]. rewrite Hs1, Hf1 in *. split_and!; [congruence|congruence|exact D2|exact R2| | |].
    + intros m c1 Hm Hc1. destruct (decide (m ∈ ns)) as [Hin|Hin]; [apply In2; done|].
      apply elem_of_cons in Hm as [->|Hm]; [|done]. rewrite Out2 by done. simpl. rewrite lookup_insert_eq. rewrite Hc in Hc1. injection Hc1 as <-. rewrite Br, Bc. done.
    + intros m Hm. rewrite Out2; [|intros K; apply Hm; by right]. simpl. rewrite lookup_insert_ne; [done|]. intros ->. apply Hm. left.
    + rewrite !fmap_cons. simpl. rewrite Bv, Ev2. done.
Qed.

Lemma ref_run_set st ids :
  (forall n, n ∈ ids -> exists m, moved_by (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) (Semantics.effect_entry st n).1 = Some m) ->
  exists ns, Semantics.run_set p st ids = Some ns /\ ns `sublist_of` ids /\
    forall x, x ∈ ns <-> x ∈ ids /\ moved_by (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) (Semantics.effect_entry st x).1 = Some true.
Proof.
  induction ids as [|n ids IH]; intros H.
  - exists []. split_and!; [done|done|]. intros x. split; [intros K; by apply not_elem_of_nil in K|intros [K _]; by apply not_elem_of_nil in K].
  - destruct (H n (list_elem_of_here _ _)) as [m Hm]. destruct IH as (ns&E&Sb&Hx); [intros y Hy; apply H; by right|].
    simpl. rewrite reads_moved_by, Hm. simpl. rewrite E. simpl. destruct m.
    + exists (n :: ns). split_and!; [done|by apply sublist_skip|]. intros x. rewrite !elem_of_cons, Hx. split.
      * intros [->|[K L]]; [split; [by left|done]|split; [by right|done]].
      * intros [[->|K] L]; [by left|right; done].
    + exists ns. split_and!; [done|by apply sublist_cons|]. intros x. rewrite Hx, elem_of_cons. split.
      * intros [K L]. split; [by right|done].
      * intros [[->|K] L]; [congruence|done].
Qed.

Lemma effect_ids_in n : n ∈ Semantics.effect_ids p <-> is_Some (Semantics.program_effects p !! n).
Proof.
  unfold Semantics.effect_ids. rewrite list_elem_of_fmap. split.
  - intros ([x c]&->&Hin). apply elem_of_map_to_list in Hin. simpl. by eexists.
  - intros [c Hc]. exists (n, c). split; [done|]. by apply elem_of_map_to_list.
Qed.

(** The state settling reaches: each live effect holds its reads over the current signals. *)
Definition ref_settled (st : Semantics.reference_state) : Prop :=
  forall n c, Semantics.program_effects p !! n = Some c ->
    (n ∈ Semantics.rs_disposed st /\ Semantics.rs_effects st !! n = None) \/
    (n ∉ Semantics.rs_disposed st /\
     Semantics.rs_effects st !! n = Some (tracked (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c, [])).

(** A live effect holds reads over some earlier answers, and evaluates over the current ones to [F]. *)
Definition ref_pending (F : node_id -> outcome * list val) (st : Semantics.reference_state) : Prop :=
  forall n c, Semantics.program_effects p !! n = Some c ->
    (n ∈ Semantics.rs_disposed st /\ Semantics.rs_effects st !! n = None) \/
    (n ∉ Semantics.rs_disposed st /\
     body_eval (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c = Some (F n).1 /\
     body_logs (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) [] c = Some (F n).2 /\
     exists Bo ro, body_eval Bo [] c = Some ro /\ Semantics.rs_effects st !! n = Some (tracked Bo [] c, [])).

Lemma ref_settle ord F st :
  Semantics.is_effect_order ord -> ref_pending F st ->
  exists ns st' rs, Semantics.settle 1 ord p st = Some (st', rs) /\
    Semantics.rs_signals st' = Semantics.rs_signals st /\ Semantics.rs_frozen st' = Semantics.rs_frozen st /\
    Semantics.rs_disposed st' = Semantics.rs_disposed st /\ ref_settled st' /\
    ns `sublist_of` Semantics.effect_ids p /\
    (forall x, x ∈ ns <-> is_Some (Semantics.program_effects p !! x) /\
       moved_by (Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)) (Semantics.effect_entry st x).1 = Some true) /\
    concat (Semantics.run_events <$> rs) = concat (blkF F <$> ord (Semantics.rs_round st) ns).
Proof.
  intros Hord Hp. set (B := Semantics.memo_value p (Semantics.rs_frozen st) (Semantics.rs_signals st)).
  assert (Hm : forall n, n ∈ Semantics.effect_ids p -> exists m, moved_by B (Semantics.effect_entry st n).1 = Some m).
  { intros n Hn. apply effect_ids_in in Hn as [c Hc]. unfold Semantics.effect_entry. destruct (Hp n c Hc) as [[_ ->]|(_&He&_&Bo&ro&Ho&->)].
    - simpl. exists false. done.
    - simpl. destruct (moved_path _ _ _ _ _ _ Ho He) as (m&Hm&_). exists m. done. }
  destruct (ref_run_set st _ Hm) as (ns&Ens&Sb&Hx).
  assert (Hnot : forall n c, Semantics.program_effects p !! n = Some c -> n ∉ ns -> n ∉ Semantics.rs_disposed st ->
            Semantics.rs_effects st !! n = Some (tracked B [] c, [])).
  { intros n c Hc Hnn Hl. destruct (Hp n c Hc) as [[K _]|(_&He&Hlg&Bo&ro&Ho&Hen)]; [done|]. rewrite Hen.
    destruct (moved_path _ _ _ _ _ _ Ho He) as (m&Hm0&Hk). destruct m.
    - exfalso. apply Hnn. apply Hx. split; [apply effect_ids_in; by eexists|]. unfold Semantics.effect_entry. rewrite Hen. exact Hm0.
    - destruct (Hk eq_refl) as (_&_&H3). exact (f_equal (fun R => Some (R, [])) (eq_sym H3)). }
  assert (Hxl : forall x, x ∈ ns -> x ∉ Semantics.rs_disposed st).
  { intros x Hin Hd. apply Hx in Hin as [Hi Hmv]. apply effect_ids_in in Hi as [c Hc]. destruct (Hp x c Hc) as [[_ K]|(K&_)]; [|exact (K Hd)].
    unfold Semantics.effect_entry in Hmv. rewrite K in Hmv. simpl in Hmv. done. }
  destruct ns as [|n0 ns0] eqn:Ens0.
  - exists [], st, []. simpl. rewrite Ens. simpl. split_and!; try done.
    + intros n c Hc. destruct (Hp n c Hc) as [K|(Hl&_)]; [by left|right]. split; [done|]. apply Hnot; [done|apply not_elem_of_nil|done].
    + intros x. rewrite Hx, effect_ids_in. done.
    + assert (K : ord (Semantics.rs_round st) [] = []) by (apply Permutation_nil; symmetry; apply Hord). rewrite K. done.
  - assert (Hround : forall n, n ∈ ord (Semantics.rs_round st) (n0 :: ns0) -> exists c, Semantics.program_effects p !! n = Some c /\
       body_eval B [] c = Some (F n).1 /\ body_logs B [] c = Some (F n).2 /\ (Semantics.effect_entry st n).2 = []).
    { intros n Hn. rewrite (Hord _ _) in Hn. pose proof (proj1 (Hx n) Hn) as [Hi _]. apply effect_ids_in in Hi as [c Hc]. exists c.
      destruct (Hp n c Hc) as [[K _]|(_&He&Hlg&Bo&ro&_&Hen)]; [exfalso; exact (Hxl n Hn K)|]. split_and!; [done|done|done|].
      unfold Semantics.effect_entry. rewrite Hen. done. }
    destruct (ref_round F _ st Hround) as (st1&rs1&E1&S1&Fz1&D1&_&In1&Out1&Ev1).
    set (st2 := Semantics.next_round st1).
    assert (Hset2 : ref_settled st2).
    { intros n c Hc. change (Semantics.rs_disposed st2) with (Semantics.rs_disposed st1). change (Semantics.rs_frozen st2) with (Semantics.rs_frozen st1).
      change (Semantics.rs_signals st2) with (Semantics.rs_signals st1). change (Semantics.rs_effects st2) with (Semantics.rs_effects st1).
      rewrite S1, Fz1, D1. destruct (decide (n ∈ n0 :: ns0)) as [Hin|Hin].
      - right. split; [exact (Hxl n Hin)|]. apply In1; [by rewrite (Hord _ _)|done].
      - rewrite Out1 by (rewrite (Hord _ _); done). destruct (Hp n c Hc) as [K|(Hl&_)]; [by left|right]. split; [done|]. apply Hnot; done. }
    assert (Hmself : forall n c, Semantics.program_effects p !! n = Some c -> n ∉ Semantics.rs_disposed st2 ->
              body_eval (Semantics.memo_value p (Semantics.rs_frozen st2) (Semantics.rs_signals st2)) [] c = Some (F n).1).
    { intros n c Hc Hl. change (Semantics.rs_frozen st2) with (Semantics.rs_frozen st1). change (Semantics.rs_signals st2) with (Semantics.rs_signals st1). rewrite S1, Fz1.
      assert (Hl0 : n ∉ Semantics.rs_disposed st) by (intros K; apply Hl; change (n ∈ Semantics.rs_disposed st1); by rewrite D1).
      destruct (Hp n c Hc) as [[K _]|(_&He&_)]; done. }
    assert (Hm2 : forall n, n ∈ Semantics.effect_ids p -> exists m, moved_by (Semantics.memo_value p (Semantics.rs_frozen st2) (Semantics.rs_signals st2)) (Semantics.effect_entry st2 n).1 = Some m).
    { intros n Hn. exists false. apply effect_ids_in in Hn as [c Hc]. unfold Semantics.effect_entry. destruct (Hset2 n c Hc) as [[_ ->]|(Hl&->)]; [done|]. simpl.
      exact (moved_self _ _ _ _ (Hmself n c Hc Hl)). }
    destruct (ref_run_set st2 _ Hm2) as (ns2&Ens2&_&Hx2).
    assert (Hns2 : ns2 = []).
    { destruct ns2 as [|y ns2']; [done|]. exfalso. destruct (proj1 (Hx2 y) (list_elem_of_here _ _)) as [Hi Hmv]. apply effect_ids_in in Hi as [c Hc].
      unfold Semantics.effect_entry in Hmv. destruct (Hset2 y c Hc) as [[_ K]|(Hl&K)]; rewrite K in Hmv; [simpl in Hmv; done|].
      change (moved_by (Semantics.memo_value p (Semantics.rs_frozen st2) (Semantics.rs_signals st2)) (tracked (Semantics.memo_value p (Semantics.rs_frozen st2) (Semantics.rs_signals st2)) [] c) = Some true) in Hmv.
      rewrite (moved_self _ _ _ _ (Hmself y c Hc Hl)) in Hmv. done. }
    subst ns2.
    assert (Hs0 : Semantics.settle 0 ord p (Semantics.next_round st1) = Some (st2, [])).
    { change (Semantics.settle 0 ord p st2 = Some (st2, [])). cbn [Semantics.settle]. rewrite Ens2. reflexivity. }
    assert (Hs1 : Semantics.settle 1 ord p st = Some (st2, rs1)).
    { cbn [Semantics.settle]. rewrite Ens.
      change (('(s1, r1) ← Semantics.run_round p (ord (Semantics.rs_round st) (n0 :: ns0)) st;
               '(s2, r2) ← Semantics.settle 0 ord p (Semantics.next_round s1); Some (s2, r1 ++ r2)) = Some (st2, rs1)).
      rewrite E1. change (('(s2, r2) ← Semantics.settle 0 ord p (Semantics.next_round st1); Some (s2, rs1 ++ r2)) = Some (st2, rs1)).
      rewrite Hs0. change (Some (st2, rs1 ++ []) = Some (st2, rs1)). by rewrite app_nil_r. }
    exists (n0 :: ns0), st2, rs1. split_and!; [exact Hs1|exact S1|exact Fz1|exact D1|exact Hset2|exact Sb| |exact Ev1].
    intros x. rewrite Hx, effect_ids_in. done.
Qed.

End RefRun.

(** ** The settled view *)

Definition aft (v : option Semantics.settled_view) (n : node_id) (r : outcome) (l : list val) : Semantics.settled_view :=
  let cur := default {| Semantics.sv_last := []; Semantics.sv_failures := []; Semantics.sv_disposed := false |} v in
  {| Semantics.sv_last := (EvLog n <$> l) ++ fail_evs n r;
     Semantics.sv_failures :=
       match r with
       | Ok _ => Semantics.sv_failures cur
       | Fail y => if decide (y ∈ Semantics.sv_failures cur) then Semantics.sv_failures cur else Semantics.sv_failures cur ++ [y]
       end;
     Semantics.sv_disposed := Semantics.sv_disposed cur |}.

Lemma aft_idem v n r l : aft (Some (aft v n r l)) n r l = aft v n r l.
Proof.
  unfold aft. simpl. destruct r as [z|y]; simpl; [done|]. f_equal.
  set (fs := Semantics.sv_failures _). destruct (decide (y ∈ fs)) as [H|H].
  - rewrite decide_True by done. done.
  - rewrite decide_True by (apply elem_of_app; right; left). done.
Qed.

Lemma foldl_logs M n (v : Semantics.settled_view) l :
  foldl Semantics.settled_step (<[n := v]> M) (EvLog n <$> l) =
    <[n := {| Semantics.sv_last := Semantics.sv_last v ++ (EvLog n <$> l); Semantics.sv_failures := Semantics.sv_failures v;
              Semantics.sv_disposed := Semantics.sv_disposed v |}]> M.
Proof.
  revert v. induction l as [|z l IH]; intros v; simpl.
  - rewrite app_nil_r. by destruct v.
  - rewrite lookup_insert_eq, insert_insert_eq. simpl. rewrite IH. simpl. rewrite <- app_assoc. done.
Qed.

Lemma foldl_blk M n r l : foldl Semantics.settled_step M (blk n r l) = <[n := aft (M !! n) n r l]> M.
Proof.
  unfold blk. simpl. rewrite foldl_app, foldl_logs. simpl. destruct r as [z|y]; simpl.
  - unfold aft. simpl. rewrite app_nil_r. done.
  - rewrite lookup_insert_eq, insert_insert_eq. unfold aft. simpl. done.
Qed.

Lemma foldl_blocks (F : node_id -> outcome * list val) es :
  forall M m, foldl Semantics.settled_step M (concat (blkF F <$> es)) !! m =
    if decide (m ∈ es) then Some (aft (M !! m) m (F m).1 (F m).2) else M !! m.
Proof.
  induction es as [|n es IH]; intros M m.
  - rewrite decide_False; [done|apply not_elem_of_nil].
  - rewrite fmap_cons. change (concat (blkF F n :: (blkF F <$> es))) with (blkF F n ++ concat (blkF F <$> es)).
    rewrite foldl_app. unfold blkF at 1. rewrite foldl_blk, IH. destruct (decide (m ∈ es)) as [H|H].
    + rewrite decide_True by (by right). destruct (decide (m = n)) as [->|Hne].
      * rewrite lookup_insert_eq, aft_idem. done.
      * rewrite lookup_insert_ne by done. done.
    + destruct (decide (m = n)) as [->|Hne].
      * rewrite decide_True by left. rewrite lookup_insert_eq. done.
      * rewrite decide_False by (intros K; apply elem_of_cons in K as [K|K]; done). rewrite lookup_insert_ne by done. done.
Qed.

(** ** Renaming *)

Lemma rv_ne x : rv x <> prev_var.
Proof. unfold rv. destruct (Nat.ltb_spec x prev_var); lia. Qed.

Lemma rv_inj x y : rv x = rv y -> x = y.
Proof. unfold rv. destruct (Nat.ltb_spec x prev_var), (Nat.ltb_spec y prev_var); lia. Qed.

(** [e] holds [g]'s bindings under [rv]. *)
Definition env_rel (g e : env) : Prop := forall x, env_lookup e (rv x) = env_lookup g x.

Lemma env_rel_cons x v g e : env_rel g e -> env_rel ((x, v) :: g) ((rv x, v) :: e).
Proof.
  intros H y. simpl. destruct (Nat.eqb_spec (rv y) (rv x)) as [K|K].
  - apply rv_inj in K as ->. by rewrite Nat.eqb_refl.
  - destruct (Nat.eqb_spec y x) as [->|_]; [done|]. apply H.
Qed.

Lemma ev_rename g e x : env_rel g e -> ev e (rename_expr x) = ev g x.
Proof.
  intros H. unfold ev. induction x; simpl; try done; try (rewrite IHx1, IHx2; done). rewrite H. done.
Qed.

Lemma ev_target g e x :
  env_rel g e -> ev e (rename_target x) = match ev g x with Some (Ok z) => Some (Ok (z + 1)%Z) | r => r end.
Proof.
  intros H. pose proof (ev_rename g e x H) as K. unfold rename_target, ev in *. simpl. rewrite K. destruct (eval_expr g None x) as [[z|y]|]; done.
Qed.

(** A target's shift: a nonnegative target is the next id, and [-1] meets node 0, which answers nothing. *)
Ltac target_cases z H0 :=
  destruct (decide (0 <= z)%Z) as [H2|H2];
  [ rewrite decide_True by lia; assert (Hs : Z.to_nat (z + 1) = S (Z.to_nat z)) by lia; rewrite Hs
  | destruct (decide (0 <= z + 1)%Z) as [H1|H1]; [assert (Hs : Z.to_nat (z + 1) = 0) by lia; rewrite Hs, H0; done|done] ].

(** [body_eval] over the renamed body. A target's shift meets node 0, which answers nothing. *)
Lemma eval_rename (A : node_id -> option outcome) c :
  A 0 = None ->
  forall g e, env_rel g e -> body_eval A e (rename c) = body_eval (fun t => A (S t)) g c.
Proof.
  intros H0. induction c; intros g e0 Hr; simpl; try done.
  - apply (ev_rename _ _ _ Hr).
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [|done|done]. apply IHc, env_rel_cons, Hr.
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (decide _); auto.
  - rewrite (ev_target _ _ _ Hr). destruct (ev g n) as [[z|y]|]; [|done|done].
    target_cases z H0. destruct (A (S (Z.to_nat z))) as [[v|w]|]; [apply IHc, env_rel_cons, Hr|done|done].
  - rewrite (ev_target _ _ _ Hr). destruct (ev g n) as [[z|y]|]; [|done|done].
    target_cases z H0. destruct (A (S (Z.to_nat z))) as [[v|w]|]; [apply IHc; by do 2 apply env_rel_cons|apply IHc; by do 2 apply env_rel_cons|done].
  - rewrite (IHc1 _ _ Hr). destruct (body_eval _ g c1) as [[z|y]|]; [apply IHc2, Hr|done|done].
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [apply IHc, Hr|done|done].
Qed.

Lemma logs_rename (A : node_id -> option outcome) c :
  A 0 = None ->
  forall g e, env_rel g e -> body_logs A e (rename c) = body_logs (fun t => A (S t)) g c.
Proof.
  intros H0. induction c; intros g e0 Hr; simpl; try done.
  - rewrite (ev_rename _ _ _ Hr). done.
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [|done|done]. apply IHc, env_rel_cons, Hr.
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [|done|done]. destruct (decide _); auto.
  - rewrite (ev_target _ _ _ Hr). destruct (ev g n) as [[z|y]|]; [|done|done].
    target_cases z H0. destruct (A (S (Z.to_nat z))) as [[v|w]|]; [apply IHc, env_rel_cons, Hr|done|done].
  - rewrite (ev_target _ _ _ Hr). destruct (ev g n) as [[z|y]|]; [|done|done].
    target_cases z H0. destruct (A (S (Z.to_nat z))) as [[v|w]|]; [apply IHc; by do 2 apply env_rel_cons|apply IHc; by do 2 apply env_rel_cons|done].
  - rewrite (eval_rename A c1 H0 _ _ Hr), (IHc1 _ _ Hr), (IHc2 _ _ Hr). done.
  - rewrite (ev_rename _ _ _ Hr). destruct (ev g e) as [[z|y]|]; [|done|done]. rewrite (IHc _ _ Hr). done.
Qed.

Lemma plain_rename x : expr_no_prev x = true -> expr_plain (rename_expr x) = true.
Proof.
  induction x; simpl; try done; rewrite ?andb_true_iff; try (intros [H1 H2]; auto).
  intros _. apply negb_true_iff, Nat.eqb_neq, rv_ne.
Qed.

Lemma shape_rename c : Semantics.well_behaved_effect c = true -> frag_body c = true ->
  body_shape (rename c) = true /\ has_untracked (rename c) = false.
Proof.
  induction c; simpl; rewrite ?andb_true_iff; try done.
  - intros H _. split; [by apply plain_rename|done].
  - intros [H1 H2] Hf. destruct (IHc H2 Hf) as [A B]. rewrite A, B, plain_rename by done. done.
  - intros [[H0 H1] H2] [F1 F2]. destruct (IHc1 H1 F1) as [A1 B1]. destruct (IHc2 H2 F2) as [A2 B2]. rewrite A1, A2, B1, B2, plain_rename by done. done.
  - intros [H1 H2] Hf. destruct (IHc H2 Hf) as [A B]. unfold rename_target. simpl. rewrite A, B, plain_rename by done. done.
  - intros [H1 H2] Hf. destruct (IHc H2 Hf) as [A B]. unfold rename_target. simpl. rewrite A, B, plain_rename by done. done.
  - intros [H1 H2] [F1 F2]. destruct (IHc1 H1 F1) as [A1 B1]. destruct (IHc2 H2 F2) as [A2 B2]. rewrite A1, A2, B1, B2. done.
  - intros [H1 H2] Hf. destruct (IHc H2 Hf) as [A B]. rewrite A, B, plain_rename by done. done.
Qed.

Lemma memo_frag c : well_behaved_memo c = true -> frag_body c = true.
Proof.
  induction c; simpl; rewrite ?andb_true_iff; try done; intros Hw; repeat match goal with H : _ /\ _ |- _ => destruct H end; try split; auto.
Qed.

Lemma body_pure_env (Q : node_id -> Prop) c :
  forall e1 e2, env_agree e1 e2 -> body_pure Q e2 c -> body_pure Q e1 c.
Proof.
  induction c; intros e1 e2 Ha H; simpl in *; try done;
    assert (Hev : forall x, expr_plain x = true -> ev e1 x = ev e2 x) by (intros x0 Hx0; exact (eval_agree _ _ None None _ Ha Hx0)).
  - destruct H as [Hx Hk]. split; [done|]. intros z Hz. rewrite (Hev _ Hx) in Hz. refine (IHc _ _ (env_agree_cons _ _ _ _ Ha) (Hk z Hz)).
  - destruct H as [Hx Hk]. split; [done|]. intros z Hz. rewrite (Hev _ Hx) in Hz. destruct (Hk z Hz) as [A B]. split; intros K; [exact (IHc2 _ _ Ha (A K))|exact (IHc1 _ _ Ha (B K))].
  - destruct H as (Hx&Ht&Hk). split_and!; [done| |]. { intros z Hz. rewrite (Hev _ Hx) in Hz. exact (Ht z Hz). } intros v. exact (IHc _ _ (env_agree_cons _ _ _ _ Ha) (Hk v)).
  - destruct H as (Hx&Ht&Hk1&Hk2). split_and!; [done| | |]. { intros z Hz. rewrite (Hev _ Hx) in Hz. exact (Ht z Hz). }
    + intros v. exact (IHc _ _ (env_agree_cons _ _ _ _ (env_agree_cons _ _ _ _ Ha)) (Hk1 v)).
    + intros y. exact (IHc _ _ (env_agree_cons _ _ _ _ (env_agree_cons _ _ _ _ Ha)) (Hk2 y)).
  - by destruct H as [? _].
  - destruct H as [A B]. split; eauto.
  - destruct H as [Hx Hk]. split; [done|eauto].
Qed.

(** ** The premises, and the table of nodes *)

(** Node ids are a creation order for memos: every read of a memo's body reaches a lower id, or an id past every node, on every
    path, a stuck path included. *)
Definition memos_below (p : Semantics.program) : Prop :=
  forall m c, Semantics.program_memos p !! m = Some c ->
    body_pure (fun u => u <= node_bound p -> u < S m) [] (rename c).

(** Each effect only reads, through tracked reads. *)
Definition effects_read_only (p : Semantics.program) : Prop :=
  forall n c, Semantics.program_effects p !! n = Some c -> frag_body c = true.

(** The history disposes no memo. *)
Definition no_memo_disposal (p : Semantics.program) (h : Semantics.history) : Prop :=
  forall b n, b ∈ h -> Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None.

Definition refinable (p : Semantics.program) (h : Semantics.history) : Prop :=
  memos_below p /\ effects_read_only p /\ no_memo_disposal p h.

(** An environment binding only [prev_var]. *)
Definition binder_env (e : env) : Prop := env_agree e [].

Definition node_match (p : Semantics.program) (n : node_id) (d : node) : Prop :=
  n_comparer d = CmpEq /\ binder_env (n_env d) /\
  match Semantics.program_signals p !! n, Semantics.program_memos p !! n, Semantics.program_effects p !! n with
  | Some _, _, _ => n_kind d = KSignal
  | None, Some c, _ => n_kind d = KMemo /\ n_body d = rename c
  | None, None, Some c => n_kind d = KEffect /\ n_body d = rename c
  | None, None, None => n_kind d = KMemo /\ n_body d = stuck_cmd
  end.

(** The nodes the engine program creates. *)
Definition tbl (p : Semantics.program) (s : state) : Prop :=
  g_nodes s !! (0 : node_id) = None /\
  forall n, (n < node_bound p -> exists d, g_nodes s !! (S n : node_id) = Some d /\ node_match p n d) /\
            (node_bound p <= n -> g_nodes s !! (S n : node_id) = None).

Lemma node_case p n d : node_match p n d ->
  (exists v, Semantics.program_signals p !! n = Some v /\ n_kind d = KSignal) \/
  (Semantics.program_signals p !! n = None /\ exists c, Semantics.program_memos p !! n = Some c /\ n_kind d = KMemo /\ n_body d = rename c) \/
  (Semantics.program_signals p !! n = None /\ Semantics.program_memos p !! n = None /\ exists c, Semantics.program_effects p !! n = Some c /\ n_kind d = KEffect /\ n_body d = rename c) \/
  (Semantics.program_signals p !! n = None /\ Semantics.program_memos p !! n = None /\ Semantics.program_effects p !! n = None /\ n_kind d = KMemo /\ n_body d = stuck_cmd).
Proof.
  intros (_&_&H). destruct (Semantics.program_signals p !! n) as [v|] eqn:E1; [left; by exists v|right].
  destruct (Semantics.program_memos p !! n) as [c|] eqn:E2; [left; split; [done|by exists c]|right].
  destruct (Semantics.program_effects p !! n) as [c|] eqn:E3; [left; split_and!; [done|done|by exists c]|right; done].
Qed.

Lemma node_match_shape p n d d' : shape_of d' = shape_of d -> node_match p n d -> node_match p n d'.
Proof.
  intros Hs (H1&H2&H3). unfold shape_of in Hs. injection Hs as K Q E B. unfold node_match. rewrite K, Q, E, B. done.
Qed.

Lemma tbl_shapes p s s' : shapes s' = shapes s -> tbl p s -> tbl p s'.
Proof.
  intros Sh [H0 H]. split.
  - destruct (g_nodes s' !! (0 : node_id)) as [d'|] eqn:E; [|done]. destruct (shapes_node_back s s' 0 d' Sh E) as (d&Hd&_). congruence.
  - intros n. destruct (H n) as [A B]. split.
    + intros Hn. destruct (A Hn) as (d&Hd&Hm). destruct (shapes_node_back s' s (S n) d (eq_sym Sh) Hd) as (d'&Hd'&Hs).
      exists d'. split; [done|]. refine (node_match_shape _ _ _ _ _ Hm). congruence.
    + intros Hn. destruct (g_nodes s' !! (S n : node_id)) as [d'|] eqn:E; [|done]. destruct (shapes_node_back s s' (S n) d' Sh E) as (d&Hd&_). rewrite (B Hn) in Hd. done.
Qed.

Lemma pure_at_mono Sh k k' n : k <= k' -> pure_at Sh k n -> pure_at Sh k' n.
Proof. induction 1 as [|k' _ IH]; [done|]. intros H. apply pure_at_S. auto. Qed.

Lemma tbl_lookup p s (m : node_id) d : tbl p s -> g_nodes s !! m = Some d -> exists n, m = S n /\ n < node_bound p /\ node_match p n d.
Proof.
  intros [H0 H] Hd. destruct m as [|n]; [congruence|]. exists n. destruct (H n) as [A B]. destruct (Nat.lt_ge_cases n (node_bound p)) as [Hn|Hn].
  - destruct (A Hn) as (d'&Hd'&Hm). rewrite Hd in Hd'. injection Hd' as <-. done.
  - rewrite (B Hn) in Hd. done.
Qed.

Lemma tbl_pure p s : memos_below p -> tbl p s ->
  forall n d, g_nodes s !! (S n : node_id) = Some d -> n_kind d <> KEffect -> pure_in (shapes s) (S n).
Proof.
  intros Hb Ht. induction n as [n IH] using lt_wf_ind. intros d Hd Hk.
  destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&Hlt&Hm0). injection Hn0 as <-. pose proof Hm0 as (Hc&He&_).
  unfold pure_in. rewrite pure_at_unfold, (shapes_lookup s _ d Hd). unfold shape_of. rewrite Hc.
  destruct (n_kind d) eqn:Ek; [done| |done].
  assert (HQ : forall u, u <= node_bound p -> u < S (S n) -> readable (shapes s) u -> u < S n \/ True) by (intros; by right).
  assert (Hlow : forall (u : node_id), (u <= node_bound p -> u < S n) -> readable (shapes s) u -> u < S n /\ pure_at (shapes s) (S n) u).
  { intros u Hu Hr. destruct (readable_node _ _ Hr) as [x Hx]. destruct (tbl_lookup p s u x Ht Hx) as (t&->&Htb&_).
    assert (Hlt0 : S t < S n) by (apply Hu; lia). split; [done|].
    assert (Hkx : n_kind x <> KEffect).
    { intros K. unfold readable in Hr. rewrite (shapes_lookup s _ x Hx) in Hr. unfold shape_of in Hr. rewrite K in Hr. done. }
    apply (pure_at_mono _ (S (S t))); [lia|]. exact (IH t ltac:(lia) x Hx Hkx). }
  destruct (node_case p n d Hm0) as [(v&_&K)|[(_&c&Em&_&->)|[(_&_&c&_&K&_)|(_&_&_&_&->)]]]; [congruence| |congruence|done].
  apply (body_pure_env _ _ _ [] He). refine (body_pure_mono _ _ _ _ _ _ (Hb n c Em)). intros t Ht0 Hr. exact (Hlow t Ht0 Hr).
Qed.

Lemma tbl_good p s :
  memos_below p -> effects_read_only p -> map_Forall (fun _ c => Semantics.well_behaved_effect c = true) (Semantics.program_effects p) ->
  tbl p s -> good s /\ no_untracked s.
Proof.
  intros Hb Hr Hw Ht.
  assert (Ha : well_behaved_program s).
  { intros u d Hd Hk. destruct (tbl_lookup p s u d Ht Hd) as (n&->&_&_). refine (tbl_pure p s Hb Ht n d Hd _). congruence. }
  assert (Heff : forall u d, g_nodes s !! u = Some d -> n_kind d = KEffect -> exists c, Semantics.program_effects p !! pred u = Some c /\ n_body d = rename c).
  { intros u d Hd Hk. destruct (tbl_lookup p s u d Ht Hd) as (n&->&_&Hm). simpl.
    destruct (node_case p n d Hm) as [(v&_&K)|[(_&c&_&K&_)|[(_&_&c&Ec&_&Hbd)|(_&_&_&K&_)]]]; try congruence. by exists c. }
  split; [split; [exact Ha|]|].
  - intros u d Hd Hk. split; [done|]. destruct (Heff u d Hd Hk) as (c&Hc&->).
    destruct (shape_rename c (Hw _ _ Hc) (Hr _ _ Hc)) as [A _]. apply body_shape_pure; [|exact A]. intros t Ht1. exact (readable_pure s t Ha Ht1).
  - intros u d Hd Hk. destruct (Heff u d Hd Hk) as (c&Hc&->). exact (proj2 (shape_rename c (Hw _ _ Hc) (Hr _ _ Hc))).
Qed.

(** ** The denotation is the reference's value *)

Lemma node_value_mono p :
  map_Forall (fun _ c => well_behaved_memo c = true) (Semantics.program_memos p) ->
  forall frz sigs k k' t o, (k <= k')%nat -> Semantics.node_value k p frz sigs t = Some o -> Semantics.node_value k' p frz sigs t = Some o.
Proof.
  intros Hw frz sigs. induction k as [|k IH]; intros k' t o Hle Hv; [done|]. destruct k' as [|k']; [lia|]. simpl in *.
  destruct (frz !! t) as [o0|]; [done|]. destruct (Semantics.program_signals p !! t) as [v|]; [done|].
  destruct (Semantics.program_memos p !! t) as [c|] eqn:Em; [|done].
  destruct (Semantics.exec p (fun _ => Semantics.node_value k p frz sigs) t [] c (Semantics.body_start ∅)) as [[o1 st1]|] eqn:Ex; simpl in Hv; [|done].
  rewrite (Properties.exec_ext p _ (Semantics.node_value k' p frz sigs) t c _ _ _ _ (Hw _ _ Em) Ex); [done|].
  intros u w _ Hu. apply (IH k'); [lia|done].
Qed.

(** A memo's value needs at most one nested evaluation per memo of lower rank, and one for a signal. *)
Lemma node_value_bound p :
  Semantics.well_formed_program p ->
  forall sigs t o k, Semantics.node_value k p ∅ sigs t = Some o -> Semantics.memo_value p ∅ sigs t = Some o.
Proof.
  intros (_&_&_&Hw&_&rank&Hr) sigs.
  set (below := fun t : node_id => filter (fun m => rank m < rank t) (dom (Semantics.program_memos p)) : gset node_id).
  assert (Hdepth : forall t, is_Some (Semantics.program_memos p !! t) -> S (size (below t)) <= size (Semantics.program_memos p)).
  { intros t Ht. rewrite <- (size_dom (Semantics.program_memos p)). apply subset_size. split.
    - intros x Hx. apply elem_of_filter in Hx. tauto.
    - intros K. assert (Hin : t ∈ below t) by (apply K; by apply elem_of_dom). apply elem_of_filter in Hin. lia. }
  assert (Hlt : forall u t, is_Some (Semantics.program_memos p !! u) -> rank u < rank t -> size (below u) < size (below t)).
  { intros u t Hu Hrk. apply subset_size. split.
    - intros x Hx. apply elem_of_filter in Hx as [H1 H2]. apply elem_of_filter. split; [lia|done].
    - intros K. assert (Hin : u ∈ below t) by (apply elem_of_filter; split; [done|by apply elem_of_dom]). apply K, elem_of_filter in Hin. lia. }
  assert (Hmain : forall N t o k, size (below t) = N -> is_Some (Semantics.program_memos p !! t) ->
            Semantics.node_value k p ∅ sigs t = Some o -> Semantics.node_value (S (S N)) p ∅ sigs t = Some o).
  { induction N as [N IH] using lt_wf_ind. intros t o k HN Ht Hv. destruct k as [|k]; [done|].
    remember (S N) as N1 eqn:EN1. simpl in Hv |- *. rewrite lookup_empty in Hv |- *.
    destruct (Semantics.program_signals p !! t) as [v|] eqn:Es; [done|].
    destruct Ht as [c Em]. rewrite Em in Hv |- *.
    destruct (Semantics.exec p (fun _ => Semantics.node_value k p ∅ sigs) t [] c (Semantics.body_start ∅)) as [[o1 st1]|] eqn:Ex; simpl in Hv; [|done].
    rewrite (Properties.exec_ext p _ (Semantics.node_value N1 p ∅ sigs) t c _ _ _ _ (Hw _ _ Em) Ex); [done|].
    intros u w Hin Hu. destruct (decide (is_Some (Semantics.program_memos p !! u))) as [Hm|Hm].
    - assert (Hrk : rank u < rank t) by exact (Hr t c Em _ _ _ _ _ Ex Hin Hm).
      pose proof (Hlt u t Hm Hrk) as Hsz.
      apply (node_value_mono p Hw _ _ (S (S (size (below u))))); [subst N1; lia|]. exact (IH (size (below u)) ltac:(lia) u w k eq_refl Hm Hu).
    - destruct k as [|k]; [done|]. simpl in Hu. rewrite lookup_empty in Hu. subst N1. simpl. rewrite lookup_empty.
      destruct (Semantics.program_signals p !! u) as [v|]; [done|].
      destruct (Semantics.program_memos p !! u) as [c1|] eqn:Em1; [exfalso; apply Hm; by eexists|done]. }
  intros t o k Hv. unfold Semantics.memo_value. destruct (decide (is_Some (Semantics.program_memos p !! t))) as [Hm|Hm].
  - apply (node_value_mono p Hw _ _ (S (S (size (below t))))); [pose proof (Hdepth t Hm); lia|]. exact (Hmain _ t o k eq_refl Hm Hv).
  - destruct k as [|k]; [done|]. simpl in Hv |- *. rewrite lookup_empty in Hv |- *. destruct (Semantics.program_signals p !! t) as [v|]; [done|].
    destruct (Semantics.program_memos p !! t) as [c1|] eqn:Em1; [exfalso; apply Hm; by eexists|done].
Qed.

(** The signals of a state, in the reference's ids. *)
Definition sig_rel (p : Semantics.program) (sigs : gmap node_id val) (s : state) : Prop :=
  forall (n : node_id) d, g_nodes s !! (S n : node_id) = Some d -> n_kind d = KSignal -> sigs !! n = Some (n_value d).

(** Every memo of the state is live. *)
Definition memos_live (s : state) : Prop :=
  forall m d, g_nodes s !! m = Some d -> n_kind d = KMemo -> n_disposed d = false.

Lemma den_zero p s k : tbl p s -> den k s 0 = None.
Proof. intros [H0 _]. destruct k; [done|]. simpl. by rewrite H0. Qed.

Lemma binder_rel e : binder_env e -> env_rel [] e.
Proof. intros H x. simpl. exact (H (rv x) (rv_ne x)). Qed.

(** A denotation is the reference's value over the same signals. *)
Lemma den_node_value p sigs s :
  map_Forall (fun _ c => well_behaved_memo c = true) (Semantics.program_memos p) ->
  tbl p s -> sig_rel p sigs s -> memos_live s -> (forall n d, g_nodes s !! n = Some d -> n_kind d = KSignal -> n_status d = StNone) ->
  forall k t o, den k s (S t) = Some o -> Semantics.node_value k p ∅ sigs t = Some o.
Proof.
  intros Hw Ht Hsg Hl Hst. induction k as [|k IH]; intros t o Hd; [done|].
  cbn [den] in Hd. destruct (g_nodes s !! (S t : node_id)) as [d|] eqn:Hn; [|done].
  destruct (tbl_lookup p s _ d Ht Hn) as (t0&Ht0&_&Hm). injection Ht0 as <-. pose proof Hm as (_&He&_).
  simpl. rewrite lookup_empty.
  destruct (node_case p t d Hm) as [(v&Es&Ek)|[(Es&c&Em&Ek&Hb)|[(_&_&c&_&Ek&_)|(Es&Em&_&Ek&Hb)]]].
  - rewrite Es, (Hsg t d Hn Ek). simpl. rewrite Ek in Hd. injection Hd as <-. unfold view_answer, read_view_of. simpl. rewrite (Hst _ d Hn Ek). done.
  - rewrite Ek, (Hl _ d Hn Ek), Hb in Hd. rewrite Es, Em. rewrite (eval_rename _ c (den_zero p s k Ht) _ _ (binder_rel _ He)) in Hd.
    assert (He2 : body_eval (Semantics.node_value k p ∅ sigs) [] c = Some o).
    { revert Hd. apply body_eval_mono. intros u v Hu. exact (IH u v Hu). }
    destruct (body_logs_of_eval _ _ _ _ He2) as [l Hl2].
    rewrite (rx_run p (fun _ => Semantics.node_value k p ∅ sigs) _ t c (memo_frag _ (Hw _ _ Em)) [] _ o l (fun _ => eq_refl) He2 Hl2). done.
  - rewrite Ek in Hd. done.
  - exfalso. rewrite Ek, (Hl _ d Hn Ek), Hb in Hd. simpl in Hd.
    assert (Hu : env_lookup (n_env d) 0 = None) by (rewrite (He 0); [done|unfold prev_var; lia]). unfold ev in Hd. simpl in Hd. rewrite Hu in Hd. done.
Qed.

(** ** A read in an open batch *)

Lemma tk_shapes s s' : tk_step s s' -> shapes s' = shapes s.
Proof.
  intros [F B _ _ _ _ _ _ _ _]. apply map_eq. intros m. unfold shapes. rewrite !lookup_fmap.
  destruct (g_nodes s !! m) as [d|] eqn:E.
  - destruct (F m d E) as (d'&Hd'&H&_). rewrite Hd'. simpl. by rewrite H.
  - destruct (g_nodes s' !! m) as [d'|] eqn:E'; [|done]. destruct (B m d' E') as [x Hx]. congruence.
Qed.

Lemma read_open f' s n s' o :
  run_core s -> wk0 s -> good s -> g_current s = None -> g_pull_depth s = 0 -> 0 < g_batch_depth s -> pure_in (shapes s) n ->
  read f' n true s = Some (s', o) ->
  den (S n) s n = Some o /\ keepq s s' /\ keepd s s'.
Proof.
  intros Hc Hw Hg Hcur Hpl Hbt Hp Hrd. destruct Hg as [Ha He].
  pose proof Hrd as Hrd0.
  assert (Hnp : forall m, m ∈ g_running s -> ~ pure_in (shapes s) m) by (intros m Hin K; exact (pure_not_running s m Hc Hw K Hin)).
  destruct f' as [|f']; [done|]. cbn [read] in Hrd. destruct f' as [|f']; [done|]. cbn [read_node] in Hrd.
  destruct (pure_exists s n Hp) as [d Hd]. rewrite (bind_get_node _ _ _ _ Hd) in Hrd.
  assert (Htrack : track n s = Some (s, tt)) by (unfold track; by rewrite Hcur).
  destruct (n_kind d) eqn:Ek.
  - rewrite (bind_some _ _ _ _ _ Htrack) in Hrd. unfold m_return in Hrd. injection Hrd as <- <-.
    destruct (b_window _ (proj1 (proj2 Hc))) as (_&_&Wsig&_).
    pose proof (quiet_denotes s (proj1 Hw) Hc Hnp n d Hd Hp (or_introl (or_introl (Wsig n d Hd Ek)))) as Hden.
    rewrite Hden. unfold view_answer, read_view_of. simpl. rewrite (proj1 (w_signal _ (proj1 Hw) n d Hd Ek)). split_and!; [done|apply keepq_refl|apply keepd_refl].
  - apply bind_inv in Hrd as (s2' & [] & H1 & Hrd).
    destruct (stale_live d) eqn:El.
    + assert (Hl : n_disposed d = false) by (unfold stale_live in El; destruct (n_disposed d); [by rewrite andb_false_r in El|done]).
      apply bind_inv in H1 as (s1 & a1 & Hrp & H1).
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
      destruct Dq as (e&He1&Qe).
      set (s2 := with_pull_depth (pred (g_pull_depth sb)) sb) in Hrp.
      assert (Hbat : 0 < g_batch_depth s2) by (change (0 < g_batch_depth sb); rewrite (at_batch _ _ _ _ _ A); exact Hbt).
      assert (Hs1 : s1 = s2).
      { destruct (bool_decide (g_pull_depth s2 = 0) && g_flush_owed s2).
        - destruct f' as [|f']; [done|]. cbn [settle_flush] in Hrp. rewrite bind_get in Hrp.
          assert (Hb0 : bool_decide (g_batch_depth s2 = 0) = false) by (apply bool_decide_eq_false_2; lia).
          rewrite Hb0, andb_false_r in Hrp. simpl in Hrp. unfold m_return in Hrp. by injection Hrp as <-.
        - unfold m_return in Hrp. by injection Hrp as <-. }
      subst s1.
      assert (Hc1cur : g_current s2 = None) by (change (g_current sb = None); rewrite (at_current _ _ _ _ _ A); exact Hcur).
      assert (Ht1 : track n s2 = Some (s2, tt)) by (unfold track; by rewrite Hc1cur).
      assert (He2 : g_nodes s2 !! n = Some e) by exact He1.
      rewrite (bind_some _ _ _ _ _ Ht1), (bind_get_node _ _ _ _ He2) in H1.
      assert (Hs2 : s2' = s2).
      { destruct (stale_live e); [unfold mark_reader in H1; rewrite Hc1cur in H1|]; unfold m_return in H1; by injection H1 as <-. }
      subst s2'. rewrite (bind_get_node _ _ _ _ He2) in Hrd. destruct (read_answer _ _ _ _ Hrd) as [Es' ->].
      assert (Hrnb : forall m, m ∈ g_running sb -> ~ pure_in (shapes sb) m).
      { intros m Hin. rewrite (at_shapes _ _ _ _ _ A). rewrite (at_running _ _ _ _ _ A) in Hin. exact (Hnp m Hin). }
      assert (Hpb2 : pure_in (shapes sb) n) by (rewrite (at_shapes _ _ _ _ _ A); exact Hp0).
      pose proof (quiet_denotes sb Hwb Hcb Hrnb n e He1 Hpb2 Qe) as Hden.
      rewrite Es'. split_and!.
      * rewrite <- (den_nodes (S n) s s0 n eq_refl). exact (den_atom _ _ _ s0 sb Hc0 A (fun _ _ K => K) (S n) n _ Hp0 Hden).
      * exact (keepq_atom _ _ _ _ _ A (fun _ _ K => K)).
      * exact (keepd_atom _ _ _ _ _ A).
    + unfold stale_live in El.
      assert (Hq0 : quiet d \/ n_disposed d = true).
      { destruct (stale (n_freshness d)) eqn:Es; simpl in El.
        - right. destruct (n_disposed d); done.
        - left. left. destruct (n_freshness d); done. }
      rewrite Htrack in H1. injection H1 as <-.
      rewrite (bind_get_node _ _ _ _ Hd) in Hrd. destruct (read_answer _ _ _ _ Hrd) as [Es' ->]. rewrite Es'.
      split_and!; [exact (quiet_denotes s (proj1 Hw) Hc Hnp n d Hd Hp Hq0)|apply keepq_refl|apply keepd_refl].
  - done.
Qed.

(** ** The projection of a trace *)

Lemma project_app p a b : project p (a ++ b) = project p a ++ project p b.
Proof. unfold project. by rewrite filter_app, fmap_app. Qed.

Lemma project_drop p m v : Semantics.program_effects p !! pred m = None -> project p [EvLog m v] = [].
Proof.
  intros H. assert (K : event_kept p (EvLog m v) = false).
  { change (bool_decide (is_Some (Semantics.program_effects p !! pred m)) = false). apply bool_decide_eq_false_2. rewrite H. intros [? X]. done. }
  unfold project. rewrite filter_cons_False, filter_nil; [done|]. rewrite K. done.
Qed.

Lemma project_keep p e m : is_Some (Semantics.program_effects p !! pred m) ->
  e = EvLog m 0%Z \/ (exists v, e = EvLog m v) \/ (exists v, e = EvFail m v) \/ e = EvRun m -> project p [e] = [event_back e].
Proof.
  intros H He. assert (K : event_kept p e = true).
  { destruct He as [ -> | [ [v ->] | [ [v ->] | -> ] ] ]; apply bool_decide_eq_true_2; exact H. }
  unfold project. rewrite filter_cons_True, filter_nil; [done|]. rewrite K. done.
Qed.

(** ** The host's steps *)

Definition answers (B : node_id -> option outcome) (t : node_id) : option outcome :=
  match t with 0 => None | S u => B u end.

Lemma eval_lift (A : node_id -> option outcome) B e c r :
  binder_env e -> (forall t o, A t = Some o -> answers B t = Some o) ->
  body_eval A e (rename c) = Some r -> body_eval B [] c = Some r.
Proof.
  intros He H Hv. apply (body_eval_mono _ (answers B) _ _ _ H) in Hv.
  rewrite (eval_rename (answers B) c eq_refl _ _ (binder_rel _ He)) in Hv. exact Hv.
Qed.

Lemma logs_lift (A : node_id -> option outcome) B e c l :
  binder_env e -> (forall t o, A t = Some o -> answers B t = Some o) ->
  body_logs A e (rename c) = Some l -> body_logs B [] c = Some l.
Proof.
  intros He H Hv. apply (body_logs_mono _ (answers B) _ _ _ H) in Hv.
  rewrite (logs_rename (answers B) c eq_refl _ _ (binder_rel _ He)) in Hv. exact Hv.
Qed.

Definition content (p : Semantics.program) (sigs : gmap node_id val) (n : node_id) : outcome * list val :=
  match Semantics.program_effects p !! n with
  | Some c =>
    (default (Ok 0%Z) (body_eval (Semantics.memo_value p ∅ sigs) [] c), default [] (body_logs (Semantics.memo_value p ∅ sigs) [] c))
  | None => (Ok 0%Z, [])
  end.

Lemma content_eq p sigs n c r l :
  Semantics.program_effects p !! n = Some c -> body_eval (Semantics.memo_value p ∅ sigs) [] c = Some r ->
  body_logs (Semantics.memo_value p ∅ sigs) [] c = Some l -> content p sigs n = (r, l).
Proof. intros H1 H2 H3. unfold content. by rewrite H1, H2, H3. Qed.

(** The state between a host's inputs. *)
Record host_ok (p : Semantics.program) (sigs : gmap node_id val) (D : gset node_id) (s : state) : Prop := {
  ho_core : run_core s;
  ho_wk : wk0 s;
  ho_sched : sched_inv None s;
  ho_cur : g_current s = None;
  ho_pull : g_pull_depth s = 0;
  ho_unt : g_untracked s = false;
  ho_flu : g_flushing s = false;
  ho_owed : g_flush_owed s = false;
  ho_own : g_current_owner s = Some root_owner_id;
  ho_run : g_running s = [];
  ho_tbl : tbl p s;
  ho_sig : sig_rel p sigs s;
  ho_live : memos_live s;
  ho_disp : forall (n : node_id) d, g_nodes s !! (S n : node_id) = Some d -> n_kind d = KEffect -> (n_disposed d = true <-> n ∈ D)
}.

Section Host.
Variable p : Semantics.program.
Hypothesis Hwf : Semantics.well_formed_program p.
Hypothesis Hmb : memos_below p.
Hypothesis Hro : effects_read_only p.

Let Hwm : map_Forall (fun _ c => well_behaved_memo c = true) (Semantics.program_memos p).
Proof. destruct Hwf as (_&_&_&H&_). exact H. Qed.
Let Hwe : map_Forall (fun _ c => Semantics.well_behaved_effect c = true) (Semantics.program_effects p).
Proof. destruct Hwf as (_&_&_&_&H&_). exact H. Qed.

Lemma tbl_good' s : tbl p s -> good s /\ no_untracked s.
Proof. exact (tbl_good p s Hmb Hro Hwe). Qed.

(** What a node of a kind holds in the program. *)
Lemma node_memo n d : node_match p n d -> n_kind d = KMemo -> Semantics.program_effects p !! n = None.
Proof.
  intros Hm Hk. destruct Hwf as (_&_&Hdj&_).
  destruct (node_case p n d Hm) as [(v&_&K)|[(_&c&Em&_)|[(_&_&c&_&K&_)|(_&_&Ee&_)]]]; try congruence.
  destruct (Semantics.program_effects p !! n) as [c1|] eqn:Ee; [|done]. exfalso.
  apply (Hdj n); apply elem_of_dom; by eexists.
Qed.

Lemma node_effect n d : node_match p n d -> n_kind d = KEffect ->
  exists c, Semantics.program_effects p !! n = Some c /\ n_body d = rename c /\ Semantics.program_memos p !! n = None.
Proof.
  intros Hm Hk. destruct (node_case p n d Hm) as [(v&_&K)|[(_&c&_&K&_)|[(_&Em&c&Ec&_&Hb)|(_&_&_&K&_)]]]; try congruence. by exists c.
Qed.

Lemma effect_node s (n : node_id) c : tbl p s -> Semantics.program_effects p !! n = Some c ->
  exists d, g_nodes s !! (S n : node_id) = Some d /\ n_kind d = KEffect /\ n_body d = rename c /\ binder_env (n_env d).
Proof.
  intros [_ Ht] Hc. destruct Hwf as (_&Hd1&Hd2&_).
  assert (Hlt : n < node_bound p).
  { unfold node_bound. apply Nat.lt_succ_r. apply max_list_elem_of_le. apply elem_of_elements, elem_of_union. right. apply elem_of_dom. by eexists. }
  destruct (proj1 (Ht n) Hlt) as (d&Hd&Hm). exists d. split; [done|]. pose proof Hm as (_&He&_).
  destruct (node_case p n d Hm) as [(v&Es&_)|[(_&c1&Em&_)|[(_&_&c1&Ec&K&B)|(_&_&Ee&_)]]].
  - exfalso. apply (Hd1 n); apply elem_of_dom; by eexists.
  - exfalso. apply (Hd2 n); apply elem_of_dom; by eexists.
  - rewrite Hc in Ec. injection Ec as <-. done.
  - rewrite Hc in Ee. done.
Qed.

Lemma memo_logs_project s b : tbl p s -> memo_logs s b -> project p b = [].
Proof.
  intros Ht. induction b as [|ev b IH]; intros H; [done|]. change (ev :: b) with ([ev] ++ b). rewrite project_app, IH; [|intros e He; apply H; by right].
  destruct (H ev (list_elem_of_here _ _)) as (m&v&->&d&Hd&Hk). destruct (tbl_lookup p s m d Ht Hd) as (n&->&_&Hm).
  rewrite (project_drop p (S n) v (node_memo n d Hm Hk)). done.
Qed.

Lemma run_project s (n : node_id) d l b :
  tbl p s -> g_nodes s !! (S n : node_id) = Some d -> n_kind d = KEffect ->
  trace_view (S n) b = EvLog (S n) <$> l -> (forall ev, ev ∈ b -> exists m v, ev = EvLog m v /\ (m = S n \/ memo_at s m)) ->
  project p b = EvLog n <$> l.
Proof.
  intros Ht Hd Hk. destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&_&Hm). injection Hn0 as <-. destruct (node_effect n d Hm Hk) as (c&Hc&_&_).
  revert l. induction b as [|ev b IH]; intros l Hv Hb.
  - destruct l; [done|]. done.
  - destruct (Hb ev (list_elem_of_here _ _)) as (m&v&->&Hmm). change (EvLog m v :: b) with ([EvLog m v] ++ b). rewrite project_app.
    assert (Hb' : forall e, e ∈ b -> exists m v, e = EvLog m v /\ (m = S n \/ memo_at s m)) by (intros e He; apply Hb; by right).
    destruct (decide (m = S n)) as [->|Hne].
    + unfold trace_view in Hv. rewrite filter_cons_True in Hv; [|simpl; by apply bool_decide_eq_true_2].
      destruct l as [|z l]; [done|]. simpl in Hv. injection Hv as Hz Hv. subst z. rewrite (IH l Hv Hb').
      rewrite (project_keep p (EvLog (S n) v) (S n) (mk_is_Some _ _ Hc) (or_intror (or_introl (ex_intro _ v eq_refl)))). done.
    + unfold trace_view in Hv. rewrite filter_cons_False in Hv; [|simpl; rewrite bool_decide_eq_false_2; [done|done]].
      rewrite (IH l Hv Hb'). destruct Hmm as [K|(x&Hx&Hkx)]; [done|]. destruct (tbl_lookup p s m x Ht Hx) as (u&->&_&Hmx).
      rewrite (project_drop p (S u) v (node_memo u x Hmx Hkx)). done.
Qed.

Lemma fail_project (n : node_id) r : is_Some (Semantics.program_effects p !! n) -> project p (fail_evs (S n) r) = fail_evs n r.
Proof.
  intros Hs. destruct r as [v|y]; [done|]. exact (project_keep p (EvFail (S n) y) (S n) Hs (or_intror (or_intror (or_introl (ex_intro _ y eq_refl))))).
Qed.

(** The read of a reference's value out of a pure node. *)
Lemma host_answers sigs D s t o :
  host_ok p sigs D s -> den (S t) s t = Some o -> answers (Semantics.memo_value p ∅ sigs) t = Some o.
Proof.
  intros Hh Hd. destruct t as [|u]; [rewrite (den_zero p s _ (ho_tbl _ _ _ _ Hh)) in Hd; done|]. simpl.
  refine (node_value_bound p Hwf sigs u o _ (den_node_value p sigs s Hwm (ho_tbl _ _ _ _ Hh) (ho_sig _ _ _ _ Hh) (ho_live _ _ _ _ Hh) _ _ _ _ Hd)).
  intros n d Hn Hk. exact (proj1 (w_signal _ (proj1 (ho_wk _ _ _ _ Hh)) n d Hn Hk)).
Qed.

Lemma host_den sigs D s k t o :
  host_ok p sigs D s -> den k s t = Some o -> answers (Semantics.memo_value p ∅ sigs) t = Some o.
Proof.
  intros Hh Hd. destruct t as [|u]; [rewrite (den_zero p s _ (ho_tbl _ _ _ _ Hh)) in Hd; done|]. simpl.
  refine (node_value_bound p Hwf sigs u o _ (den_node_value p sigs s Hwm (ho_tbl _ _ _ _ Hh) (ho_sig _ _ _ _ Hh) (ho_live _ _ _ _ Hh) _ _ _ _ Hd)).
  intros n d Hn Hk. exact (proj1 (w_signal _ (proj1 (ho_wk _ _ _ _ Hh)) n d Hn Hk)).
Qed.

(** A run's entry reads the reference's values. *)
Lemma entry_content sigs D s (n : node_id) d r l b :
  host_ok p sigs D s -> g_nodes s !! (S n : node_id) = Some d -> n_kind d = KEffect ->
  run_entry s (S n) (n_env d) (n_body d) r l b -> content p sigs n = (r, l).
Proof.
  intros Hh Hd Hk (_&_&A&G&R). pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct (tbl_good' s Ht) as [Hg Hnu].
  destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&_&Hm). injection Hn0 as <-. destruct (node_effect n d Hm Hk) as (c&Hc&Hb&_). destruct Hm as (_&He&_).
  destruct (proj2 Hg _ d Hd Hk) as [_ Hq].
  destruct (body_run_eval _ _ _ _ _ _ _ _ Hq (fun _ => Hnu _ d Hd Hk) R) as [E1 E2].
  assert (HA : forall t o, A t = Some o -> answers (Semantics.memo_value p ∅ sigs) t = Some o).
  { intros t o Ho. destruct (G t o Ho) as (Hp&_&e&Het&Qe&Hv). apply (host_den sigs D s (S t)); [done|]. rewrite <- Hv.
    assert (Hnp : forall m, m ∈ g_running s -> ~ pure_in (shapes s) m) by (intros m Hin; rewrite (ho_run _ _ _ _ Hh) in Hin; by apply not_elem_of_nil in Hin).
    exact (quiet_denotes s (proj1 (ho_wk _ _ _ _ Hh)) (ho_core _ _ _ _ Hh) Hnp t e Het Hp Qe). }
  rewrite Hb in E1, E2. apply (content_eq p sigs n c); [done| |].
  - exact (eval_lift _ _ _ _ _ He HA E1).
  - exact (logs_lift _ _ _ _ _ He HA E2).
Qed.

(** The labels and contents of a flush, in the reference's ids. *)
Lemma flush_project sigs D s t rs :
  host_ok p sigs D s -> flush_tr s t rs ->
  exists es, project p t = concat (blkF (content p sigs) <$> es) /\
    (forall n, n ∈ es -> is_Some (Semantics.program_effects p !! n)) /\ (forall m, m ∈ es -> EvRun (S m) ∈ t) /\
    (forall m, m ∉ es -> Forall (fun x : node_id * outcome * list val => x.1.1 <> S m) rs).
Proof.
  intros Hh H. pose proof (ho_tbl _ _ _ _ Hh) as Ht. induction H as [|pre t rs Hm _ IH|m d r l b t rs Hd Hk Hr _ IH].
  - exists []. split_and!; try done.
    + intros n Hn. by apply not_elem_of_nil in Hn.
    + intros n Hn. by apply not_elem_of_nil in Hn.
  - destruct IH as (es&E&H1&H2&H3). exists es. rewrite project_app, (memo_logs_project s pre Ht Hm). split_and!; [done|done| |done].
    intros m0 Hm0. apply elem_of_app. right. auto.
  - destruct IH as (es&E&H1&H2&H3). destruct (tbl_lookup p s _ d Ht Hd) as (n&->&_&Hm). destruct (node_effect n d Hm Hk) as (c&Hc&_&_).
    exists (n :: es). pose proof Hr as (V&Lb&_). pose proof (entry_content sigs D s n d r l b Hh Hd Hk Hr) as Hcn.
    split_and!.
    + change (EvRun (S n) :: b ++ fail_evs (S n) r ++ t) with ([EvRun (S n)] ++ b ++ fail_evs (S n) r ++ t). rewrite !project_app, E.
      rewrite (run_project s n d l b Ht Hd Hk V Lb), (fail_project n r ltac:(by eexists)).
      assert (Hrun : project p [EvRun (S n)] = [EvRun n]).
      { exact (project_keep p (EvRun (S n)) (S n) (mk_is_Some _ _ Hc) (or_intror (or_intror (or_intror eq_refl)))). }
      rewrite Hrun. simpl. unfold blkF at 1. rewrite Hcn. unfold blk. simpl. rewrite <- !app_assoc. done.
    + intros x Hx. apply elem_of_cons in Hx as [->|Hx]; [by eexists|auto].
    + intros m0 Hm0. apply elem_of_cons in Hm0 as [->|Hm0]; [left|]. right. apply elem_of_app. right. apply elem_of_app. right. auto.
    + intros m0 Hm0. apply Forall_cons_2.
      * simpl. intros K. injection K as ->. apply Hm0. left.
      * apply H3. intros K. apply Hm0. by right.
Qed.

Lemma view_nil c l : (forall ev, ev ∈ l -> view_ev c ev = false) -> trace_view c l = [].
Proof.
  induction l as [|x l IH]; intros H; [done|]. unfold trace_view in *. rewrite filter_cons_False; [apply IH; intros ev Hev; apply H; by right|].
  rewrite (H x (list_elem_of_here _ _)). done.
Qed.

(** A flush holds nothing of an effect it did not run. *)
Lemma flush_view s t rs (n : node_id) d :
  flush_tr s t rs -> g_nodes s !! (S n : node_id) = Some d -> n_kind d = KEffect ->
  Forall (fun x : node_id * outcome * list val => x.1.1 <> S n) rs -> trace_view (S n) t = [].
Proof.
  intros H Hd Hk. induction H as [|pre t rs Hm _ IH|m d0 r l b t rs Hd0 Hk0 Hr _ IH]; intros Hf; [done| |].
  - rewrite trace_view_app, (IH Hf), app_nil_r. apply view_nil. intros ev Hev. destruct (Hm ev Hev) as (u&v&->&x&Hx&Kx). simpl. apply bool_decide_eq_false_2. intros ->. rewrite Hd in Hx. injection Hx as <-. congruence.
  - apply Forall_cons in Hf as [Hne Hf]. simpl in Hne. pose proof Hr as (_&Lb&_).
    change (EvRun m :: b ++ fail_evs m r ++ t) with ([EvRun m] ++ b ++ fail_evs m r ++ t). rewrite !trace_view_app, (IH Hf).
    rewrite (view_nil (S n) [EvRun m]); [|intros ev Hev; apply list_elem_of_singleton in Hev as ->; simpl; by apply bool_decide_eq_false_2].
    rewrite (view_nil (S n) b).
    + rewrite (view_nil (S n) (fail_evs m r)); [done|]. intros ev Hev. destruct r; simpl in Hev; [by apply not_elem_of_nil in Hev|]. apply list_elem_of_singleton in Hev as ->. simpl. by apply bool_decide_eq_false_2.
    + intros ev Hev. destruct (Lb ev Hev) as (u&v&->&[->|(x&Hx&Kx)]); simpl; apply bool_decide_eq_false_2; [done|]. intros ->. rewrite Hd in Hx. injection Hx as <-. congruence.
Qed.

Lemma aft_congr v n r1 r2 l : fail_evs n r1 = fail_evs n r2 -> aft v n r1 l = aft v n r2 l.
Proof.
  destruct r1 as [a|y1], r2 as [b|y2]; simpl; intros H; try done. injection H as ->. done.
Qed.

(** The part of a view after its last run. *)
Lemma last_block_unique c pre1 pre2 l1 l2 r1 r2 :
  pre1 ++ EvRun c :: (EvLog c <$> l1) ++ fail_evs c r1 = pre2 ++ EvRun c :: (EvLog c <$> l2) ++ fail_evs c r2 ->
  l1 = l2 /\ fail_evs c r1 = fail_evs c r2.
Proof.
  intros H.
  assert (Hno : forall l r, EvRun c ∉ (EvLog c <$> l) ++ fail_evs c r).
  { intros l r K. apply elem_of_app in K as [K|K].
    - apply list_elem_of_fmap in K as (?&K&_). done.
    - destruct r; simpl in K; [by apply not_elem_of_nil in K|]. apply list_elem_of_singleton in K. done. }
  assert (Htail : (EvLog c <$> l1) ++ fail_evs c r1 = (EvLog c <$> l2) ++ fail_evs c r2).
  { revert pre2 H. induction pre1 as [|x pre1 IH]; intros pre2 H; destruct pre2 as [|y pre2]; simpl in H.
    - by injection H.
    - injection H as <- H. exfalso. apply (Hno l1 r1). rewrite H. apply elem_of_app. right. left.
    - injection H as -> H. exfalso. apply (Hno l2 r2). rewrite <- H. apply elem_of_app. right. left.
    - injection H as -> H. exact (IH _ H). }
  clear H Hno. revert l2 Htail. induction l1 as [|z l1 IH]; intros l2 H; destruct l2 as [|w l2]; simpl in H.
  - done.
  - destruct r1; simpl in H; [done|]. injection H; done.
  - destruct r2; simpl in H; [done|]. injection H; done.
  - injection H as -> H. destruct (IH _ H) as [-> Hf]. done.
Qed.

Lemma foldl_cmp (F : node_id -> outcome * list val) M es ls :
  (forall m, m ∈ es -> m ∉ ls -> M !! m = Some (aft (M !! m) m (F m).1 (F m).2)) ->
  (forall m, m ∉ es -> m ∈ ls -> M !! m = Some (aft (M !! m) m (F m).1 (F m).2)) ->
  foldl Semantics.settled_step M (concat (blkF F <$> es)) = foldl Semantics.settled_step M (concat (blkF F <$> ls)).
Proof.
  intros H1 H2. apply map_eq. intros m. rewrite !foldl_blocks.
  destruct (decide (m ∈ es)) as [A|A], (decide (m ∈ ls)) as [B|B]; try done.
  - symmetry. exact (H1 m A B).
  - exact (H2 m A B).
Qed.

Lemma host_reads_app a b : Semantics.host_reads (a ++ b) = Semantics.host_reads a ++ Semantics.host_reads b.
Proof. unfold Semantics.host_reads. apply omap_app. Qed.

Lemma host_reads_blocks F es : Semantics.host_reads (concat (blkF F <$> es)) = [].
Proof.
  induction es as [|n es IH]; [done|]. rewrite fmap_cons. change (concat (blkF F n :: (blkF F <$> es))) with (blkF F n ++ concat (blkF F <$> es)).
  rewrite host_reads_app, IH, app_nil_r. unfold blkF, blk.
  change (Semantics.host_reads ((EvLog n <$> (F n).2) ++ fail_evs n (F n).1) = []).
  rewrite host_reads_app. assert (H : forall l : list val, Semantics.host_reads (EvLog n <$> l) = []) by (induction l as [|z l IHl]; [done|exact IHl]). rewrite H. by destruct (F n).1.
Qed.

Definition evs (rs : list Semantics.run) : list event := concat (Semantics.run_events <$> rs).

Lemma evs_app a b : evs (a ++ b) = evs a ++ evs b.
Proof. unfold evs. by rewrite fmap_app, concat_app. Qed.

(** The run events of a trace. *)
Definition runs_in : list event -> list node_id :=
  omap (fun e => match e with EvRun n => Some n | _ => None end).

Lemma runs_in_app a b : runs_in (a ++ b) = runs_in a ++ runs_in b.
Proof. unfold runs_in. apply omap_app. Qed.

(** The entries taken by the drain of a flush requested at fuel [g], in order. *)
Definition flush_queue (g : nat) (s : state) : list node_id :=
  match g with
  | S (S g') => dequeued g' (with_flush_owed false (with_flushing true s))
  | _ => []
  end.

Lemma runs_in_logs (b : list event) : (forall ev, ev ∈ b -> exists m v, ev = EvLog m v) -> runs_in b = [].
Proof.
  induction b as [|e b IH]; intros H; [done|].
  destruct (H e ltac:(left)) as (m&v&->). apply IH. intros ev Hev. apply H. by right.
Qed.

Lemma flush_tr_runs s t rs : flush_tr s t rs -> runs_in t = (fun x => x.1.1) <$> rs.
Proof.
  induction 1 as [|pre t rs Hm _ IH|n d r l b t rs _ _ Hr _ IH]; [done|..].
  - rewrite runs_in_app, IH, (runs_in_logs pre); [done|]. intros ev Hev. destruct (Hm ev Hev) as (m&v&->&_). by exists m, v.
  - change (n :: runs_in (b ++ fail_evs n r ++ t) = n :: ((fun x => x.1.1) <$> rs)). f_equal.
    pose proof Hr as (_&Hb&_). rewrite runs_in_app, (runs_in_logs b), runs_in_app, IH; [by destruct r|].
    intros ev Hev. destruct (Hb ev Hev) as (m&v&->&_). by exists m, v.
Qed.

Lemma runs_in_blocks F es : runs_in (concat (blkF F <$> es)) = es.
Proof.
  induction es as [|n es IH]; [done|]. rewrite fmap_cons. change (concat (blkF F n :: (blkF F <$> es))) with (blkF F n ++ concat (blkF F <$> es)).
  rewrite runs_in_app, IH. unfold blkF, blk.
  change ((n :: runs_in ((EvLog n <$> (F n).2) ++ fail_evs n (F n).1)) ++ es = n :: es).
  rewrite runs_in_app. assert (H : forall l : list val, runs_in (EvLog n <$> l) = []) by (induction l as [|z l IHl]; [done|exact IHl]). rewrite H. by destruct (F n).1.
Qed.

Lemma count_runs_in n t : EvRun n ∈ t -> 0 < count_runs n t.
Proof.
  induction t as [|x t IH]; intros H; [by apply not_elem_of_nil in H|]. apply elem_of_cons in H as [<-|H].
  - simpl. rewrite decide_True by done. lia.
  - specialize (IH H). destruct x; simpl; try done. lia.
Qed.

Lemma sched_up s : sched_inv None s -> sched_inv None (with_batch_depth (S (g_batch_depth s)) s).
Proof.
  intros (D&C&_). split_and!.
  - refine (data_of_same _ s _ _ D). split; done.
  - destruct C. split; done.
  - intros _. right. left. simpl. lia.
Qed.

Lemma sched_down s v : sched_inv None s -> g_flush_owed s = false -> sched_inv None (with_batch_depth v s).
Proof.
  intros (D&C&_) Ho. split_and!.
  - refine (data_of_same _ s _ _ D). split; done.
  - destruct C. split; done.
  - intros K. simpl in K. congruence.
Qed.

(** ** A program's start *)

Definition setup_state (i : nat) (s : state) : Prop :=
  g_next_id s = i /\ g_nodes s !! (0 : node_id) = None /\
  (forall n, n < i -> exists d, g_nodes s !! (S n : node_id) = Some d /\ node_match p n d /\ n_disposed d = false /\
      (n_kind d = KSignal -> Semantics.program_signals p !! (n : node_id) = Some (n_value d))) /\
  (forall n, i <= n -> g_nodes s !! (S n : node_id) = None) /\
  g_current s = None /\ g_current_owner s = Some root_owner_id /\ 0 < g_batch_depth s /\
  (exists y, g_owners s !! root_owner_id = Some y /\ o_disposed y = false) /\ g_trace s = [].

Lemma setup_add (i : node_id) s D :
  setup_state i s -> node_match p i D -> n_disposed D = false ->
  (n_kind D = KSignal -> Semantics.program_signals p !! i = Some (n_value D)) ->
  setup_state (S i) (ghost_publish (S i) (set_node (S i) D (with_next_id (S i) s))).
Proof.
  intros (H1&H2&H3&H4&H5&H6&H7&H8&H9) Hm Hd Hv. unfold ghost_publish. simpl. rewrite lookup_insert_eq. unfold setup_state. simpl.
  split_and!; try done.
  - rewrite lookup_insert_ne; [done|lia].
  - intros n Hn. destruct (decide (n = i)) as [->|Hne].
    + exists D. rewrite lookup_insert_eq. done.
    + rewrite lookup_insert_ne by lia. apply H3. lia.
  - intros n Hn. rewrite lookup_insert_ne by lia. apply H4. lia.
Qed.

Lemma setup_marks i s s' : setup_state i s -> rtc mark_prim s s' -> setup_state i s'.
Proof.
  intros (H1&H2&H3&H4&H5&H6&H7&H8&H9) Hm. pose proof (rtc_rise _ _ Hm) as R.
  unfold setup_state. rewrite (marks_next _ _ Hm), (marks_own _ _ Hm), (rs_current _ _ R), (rs_batch _ _ R), (rs_owners _ _ R), (rs_trace _ _ R). split_and!; try done.
  - destruct (g_nodes s' !! (0 : node_id)) as [x|] eqn:E; [|done]. destruct (rs_back _ _ R 0 x E) as [y Hy]. congruence.
  - intros n Hn. destruct (H3 n Hn) as (d&Hd&Mm&Di&Vl). destruct (rs_fwd _ _ R _ d Hd) as (d'&Hd'&Rr). pose proof Rr as (Y1&Y2&Y3&_).
    exists d'. split_and!; [done|exact (node_match_shape _ _ _ _ Y1 Mm)|congruence|]. intros Hk. rewrite (rise_kind _ _ Rr) in Hk. unfold read_view_of in Y2. injection Y2 as Yv _ _. rewrite Yv. auto.
  - intros n Hn. destruct (g_nodes s' !! (S n : node_id)) as [x|] eqn:E; [|done]. destruct (rs_back _ _ R _ x E) as [y Hy]. rewrite (H4 n Hn) in Hy. done.
Qed.

Lemma setup_attach g n i s s' a :
  setup_state i s -> g_nodes s !! n = None \/ True -> attach g n root_owner_id s = Some (s', a) -> setup_state i s'.
Proof.
  intros (H1&H2&H3&H4&H5&H6&H7&(y&Hy&Hyd)&H9) _ E. destruct g as [|g]; [done|]. cbn [attach] in E. unfold m_bind at 1, get_owner in E. rewrite Hy, Hyd in E.
  unfold put_owner, m_bind, m_modify, modify_node in E. simpl in E. destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done]. injection E as <- _.
  unfold setup_state. simpl. split_and!; try done.
  - destruct n as [|u]; [rewrite Hd in H2; done|]. rewrite lookup_insert_ne; [done|lia].
  - intros u Hu. destruct (H3 u Hu) as (x&Hx&Mm&Di&Vl). destruct (decide (S u = n)) as [<-|Hne].
    + rewrite lookup_insert_eq. rewrite Hd in Hx. injection Hx as <-. eexists. split; [done|]. split_and!; [|done|done]. exact (node_match_shape _ _ _ _ eq_refl Mm).
    + rewrite lookup_insert_ne by done. exists x. done.
  - intros u Hu. destruct (decide (S u = n)) as [<-|Hne]; [rewrite (H4 u Hu) in Hd; done|]. rewrite lookup_insert_ne by done. auto.
  - eexists. rewrite lookup_insert_eq. split; [done|]. done.
Qed.

Lemma setup_exec m :
  forall i f e s s' r, setup_state i s -> binder_env e -> exec f e (foldr (node_cmd p) done_cmd (seq i m)) s = Some (s', r) ->
  r = Ok 0%Z /\ setup_state (i + m) s'.
Proof.
  induction m as [|m IH]; intros i f e s s' r Hs He E; simpl in E.
  - destruct f as [|f]; [done|]. cbn [exec] in E. unfold on_value in E. simpl in E. unfold m_return in E. injection E as <- <-. rewrite Nat.add_0_r. done.
  - assert (Hbe : forall z, binder_env ((prev_var, z) :: e)).
    { intros z y Hy. simpl. rewrite (proj2 (Nat.eqb_neq y prev_var) Hy). exact (He y Hy). }
    destruct Hs as [Hx Hrest]. pose proof (conj Hx Hrest) as Hs0. destruct f as [|f]; [done|].
    assert (Hnc : forall i0 k, node_cmd p i0 k = match Semantics.program_signals p !! i0, Semantics.program_memos p !! i0, Semantics.program_effects p !! i0 with
      | Some v, _, _ => CSignal prev_var (EConst v) k
      | None, Some c, _ => CMemo prev_var Pure CmpEq (rename c) k
      | None, None, Some c => CEffect prev_var (rename c) k
      | None, None, None => CMemo prev_var Pure CmpEq stuck_cmd k
      end) by reflexivity.
    rewrite Hnc in E.
    assert (Hmk : forall D, node_match p i D -> n_disposed D = false -> (n_kind D = KSignal -> Semantics.program_signals p !! (i : node_id) = Some (n_value D)) ->
              setup_state (S i) (ghost_publish (S i) (set_node (S i) D (with_next_id (S i) s)))).
    { intros D A B C. exact (setup_add i s D Hs0 A B C). }
    assert (Hend : forall f1 e1 s1, setup_state (S i) s1 -> binder_env e1 -> exec f1 e1 (foldr (node_cmd p) done_cmd (seq (S i) m)) s1 = Some (s', r) ->
              r = Ok 0%Z /\ setup_state (i + S m) s').
    { intros f1 e1 s1 H1 H2 H3. rewrite Nat.add_succ_r, <- Nat.add_succ_l. exact (IH _ f1 e1 s1 s' r H1 H2 H3). }
    destruct (Semantics.program_signals p !! (i : node_id)) as [v|] eqn:Es.
    + cbn [exec] in E. unfold on_value in E. simpl in E. unfold m_bind, next_id, add_node, m_modify in E. simpl in E. rewrite Hx in E.
      refine (Hend _ _ _ _ (Hbe _) E). apply Hmk; simpl.
      * unfold node_match. rewrite Es. split_and!; try done.
      * done.
      * intros _. done.
    + destruct (Semantics.program_memos p !! (i : node_id)) as [c|] eqn:Em; [|destruct (Semantics.program_effects p !! (i : node_id)) as [c|] eqn:Ee].
      * cbn [exec] in E. unfold m_bind at 1, next_id in E. simpl in E. unfold m_bind at 1, add_node, m_modify in E. simpl in E.
        rewrite Hx in E. set (s1 := ghost_publish (S i) _) in E.
        assert (S1 : setup_state (S i) s1).
        { subst s1. apply Hmk.
          - unfold node_match. rewrite Es, Em. simpl. split_and!; try done.
          - done.
          - done. }
        pose proof S1 as (_&_&_&_&_&Own&_). unfold m_bind at 1, current_owner in E. rewrite Own in E. simpl in E.
        apply bind_inv in E as (s2 & [] & Ea & E). exact (Hend _ _ _ (setup_attach _ _ _ _ _ _ S1 (or_intror I) Ea) (Hbe _) E).
      * cbn [exec] in E. unfold m_bind at 1, next_id in E. simpl in E. unfold m_bind at 1, add_node, m_modify in E. simpl in E.
        rewrite Hx in E. set (s1 := ghost_publish (S i) _) in E.
        assert (S1 : setup_state (S i) s1).
        { subst s1. apply Hmk.
          - unfold node_match. rewrite Es, Em, Ee. simpl. split_and!; try done.
          - done.
          - done. }
        pose proof S1 as (_&_&_&_&_&Own&_). unfold m_bind at 1, current_owner in E. rewrite Own in E. simpl in E.
        apply bind_inv in E as (s2 & [] & Ea & E). pose proof (setup_attach _ _ _ _ _ _ S1 (or_intror I) Ea) as S2.
        apply bind_step in E as (s3 & Hm & E). pose proof (setup_marks _ _ _ S2 (mark_dirty_rtc _ _ _ _ Hm)) as S3.
        apply bind_inv in E as (s4 & [] & Er & E).
        assert (Hs4 : s4 = s3).
        { destruct f as [|f]; [done|]. cbn [request_flush] in Er. rewrite bind_get in Er. pose proof S3 as (_&_&_&_&_&_&Bt&_).
          rewrite (bool_decide_eq_false_2 (g_batch_depth s3 = 0)) in Er by lia. rewrite andb_false_r in Er. unfold m_return in Er. by injection Er as <-. }
        subst s4. exact (Hend _ _ _ S3 (Hbe _) E).
      * cbn [exec] in E. unfold m_bind at 1, next_id in E. simpl in E. unfold m_bind at 1, add_node, m_modify in E. simpl in E.
        rewrite Hx in E. set (s1 := ghost_publish (S i) _) in E.
        assert (S1 : setup_state (S i) s1).
        { subst s1. apply Hmk.
          - unfold node_match. rewrite Es, Em, Ee. simpl. split_and!; try done.
          - done.
          - done. }
        pose proof S1 as (_&_&_&_&_&Own&_). unfold m_bind at 1, current_owner in E. rewrite Own in E. simpl in E.
        apply bind_inv in E as (s2 & [] & Ea & E). exact (Hend _ _ _ (setup_attach _ _ _ _ _ _ S1 (or_intror I) Ea) (Hbe _) E).
Qed.

Lemma binder_nil : binder_env [].
Proof. intros y _. done. Qed.

(** The state after creating the nodes, with the batch closed and its flush ahead. *)
Lemma init_host f s r :
  exec f [] (setup_cmd p) (with_batch_depth 1 (init current_flags)) = Some (s, r) ->
  r = Ok 0%Z /\ g_trace s = [] /\ g_batch_depth s = 1 /\
  host_ok p (Semantics.program_signals p) ∅ (with_batch_depth 0 s).
Proof.
  intros E. set (s0 := with_batch_depth 1 (init current_flags)) in E.
  assert (S0 : setup_state 0 s0).
  { unfold setup_state. simpl. split_and!; try done; first [ (intros n Hn; lia) | (intros n _; apply lookup_empty) | (exists (new_owner 0 None false); rewrite lookup_insert_eq; done) | lia ]. }
  destruct (setup_exec (node_bound p) 0 f [] s0 s r S0 binder_nil E) as [-> S1]. simpl in S1.
  destruct S1 as (H1&H2&H3&H4&H5&H6&H7&H8&H9).
  assert (Ht : tbl p s).
  { split; [done|]. intros n. split; [|apply H4]. intros Hn. destruct (H3 n Hn) as (d&Hd&Mm&_). by exists d. }
  destruct (tbl_good' s Ht) as [Hg _].
  destruct engine_settled_init as [Hc00 _].
  assert (Hc0 : run_core s0) by (by apply core_batch).
  assert (Hw0 : wk0 s0) by (apply (wk_same (init current_flags)); [split_and!; done|exact whole_init]).
  assert (Hs0 : sched_inv None s0) by exact (sched_up _ (sched_inv_init current_flags)).
  destruct (Rexec _ (runs_ok_all f) [] _ _ _ _ Hc0 I E) as [Hc1 [Hfr _]].
  pose proof (Wexec f (W_ok_all f (fun _ => False)) [] _ _ _ _ Hc0 Hw0 I E Hg) as Hw1.
  destruct (Sexec _ (sched_ok_all f) None [] _ _ _ _ Hs0 I E) as [Hs1 [[R1 R2 R3 R4 R5 R6] _]].
  assert (Hbd : g_batch_depth s = 1) by exact R3.
  assert (Hown : g_flush_owed s = false).
  { destruct (g_flush_owed s) eqn:Eo; [|done]. destruct (R6 eq_refl) as [K|K]; simpl in K; [done|lia]. }
  split_and!; try done. split.
  - by apply core_batch.
  - apply (wk_same s); [split_and!; done|exact Hw1].
  - exact (sched_down _ _ Hs1 Hown).
  - exact H5.
  - exact R4.
  - exact R2.
  - exact R5.
  - exact Hown.
  - exact H6.
  - destruct Hfr as (Hr&_). exact Hr.
  - exact Ht.
  - intros n d Hd Hk. change (g_nodes s !! (S n : node_id) = Some d) in Hd. destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&Hlt&_). injection Hn0 as <-. destruct (H3 n Hlt) as (x&Hx&_&_&Vl). rewrite Hd in Hx. injection Hx as <-. auto.
  - intros m d Hd Hk. change (g_nodes s !! m = Some d) in Hd. destruct (tbl_lookup p s m d Ht Hd) as (n0&->&Hlt&_). destruct (H3 n0 Hlt) as (x&Hx&_&Di&_). rewrite Hd in Hx. injection Hx as <-. exact Di.
  - intros n d Hd Hk. change (g_nodes s !! (S n : node_id) = Some d) in Hd. destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&Hlt&_). injection Hn0 as <-. destruct (H3 n Hlt) as (x&Hx&_&Di&_). rewrite Hd in Hx. injection Hx as <-. rewrite Di. set_solver.
Qed.


(** A live effect's content at a program's end. *)
Lemma converged_content sigs D s (n : node_id) c :
  host_ok p sigs D s -> converged s -> Semantics.program_effects p !! n = Some c -> n ∉ D ->
  exists r l pre, content p sigs n = (r, l) /\
    body_eval (Semantics.memo_value p ∅ sigs) [] c = Some r /\ body_logs (Semantics.memo_value p ∅ sigs) [] c = Some l /\
    trace_view (S n) (g_trace s) = pre ++ EvRun (S n) :: (EvLog (S n) <$> l) ++ fail_evs (S n) r.
Proof.
  intros Hh Hcv Hc Hn. pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct (effect_node s n c Ht Hc) as (d&Hd&Hk&Hb&He).
  assert (Hl : n_disposed d = false) by (destruct (n_disposed d) eqn:E; [exfalso; apply Hn; by apply (ho_disp _ _ _ _ Hh n d)|done]).
  destruct (Hcv (S n) d Hd Hk Hl) as (_&_&_&_&Hu).
  destruct (tbl_good' s Ht) as [_ Hnu]. destruct (Hu (Hnu _ d Hd Hk)) as (r&k&_&E1&_&E2&pre&V).
  assert (HA : forall t o, den k s t = Some o -> answers (Semantics.memo_value p ∅ sigs) t = Some o) by (intros t o H; exact (host_den sigs D s k t o Hh H)).
  rewrite Hb in E1, E2.
  pose proof (eval_lift _ _ _ _ _ He HA E1) as F1. pose proof (logs_lift _ _ _ _ _ He HA E2) as F2.
  exists r, (last_run_logs (S n) (g_trace s)), pre. split_and!; [exact (content_eq p sigs n c _ _ Hc F1 F2)|done|done|done].
Qed.

(** A step of the engine keeps [host_ok] once it keeps the program's nodes and the signals. *)
Lemma host_of {A} (m : M A) sigs D sigs' D' s s' a :
  host_ok p sigs D s -> ok2 (fun _ => True) m -> wfx (fun _ => False) (fun _ => True) m -> sok None None (fun _ => True) m ->
  m s = Some (s', a) -> g_current_owner s' = g_current_owner s ->
  tbl p s' -> sig_rel p sigs' s' -> memos_live s' ->
  (forall n d, g_nodes s' !! (S n : node_id) = Some d -> n_kind d = KEffect -> (n_disposed d = true <-> n ∈ D')) ->
  host_ok p sigs' D' s' /\ post s s'.
Proof.
  intros Hh O W S E Ho Ht Hsg Hl Hd.
  destruct (O _ _ _ (ho_core _ _ _ _ Hh) I E) as [Hc [Hf _]].
  destruct (S _ _ _ (ho_sched _ _ _ _ Hh) I E) as [Hsc Hp].
  pose proof (proj1 (tbl_good' s' Ht)) as Hg.
  pose proof (W _ _ _ (ho_core _ _ _ _ Hh) (ho_wk _ _ _ _ Hh) I E Hg) as Hw.
  pose proof Hp as [[R1 R2 R3 R4 R5 R6] _].
  split; [|done]. split; try done.
  - rewrite R1. apply (ho_cur _ _ _ _ Hh).
  - rewrite R4. apply (ho_pull _ _ _ _ Hh).
  - rewrite R2. apply (ho_unt _ _ _ _ Hh).
  - rewrite R5. apply (ho_flu _ _ _ _ Hh).
  - destruct (g_flush_owed s') eqn:Eo; [exfalso|done]. destruct (R6 eq_refl) as [K|K]; [rewrite (ho_owed _ _ _ _ Hh) in K; done|].
    destruct Hsc as (_&_&Hin). destruct (Hin Eo) as [X|[X|[X|X]]].
    + rewrite R4, (ho_pull _ _ _ _ Hh) in X. lia.
    + rewrite R3, K in X. lia.
    + rewrite R1, (ho_cur _ _ _ _ Hh) in X. by destruct X.
    + rewrite R2, (ho_unt _ _ _ _ Hh) in X. done.
  - rewrite Ho. apply (ho_own _ _ _ _ Hh).
  - destruct Hf as (Hrun&_). rewrite Hrun. apply (ho_run _ _ _ _ Hh).
Qed.

Lemma ghost_publish_keep n s :
  g_batch_depth (ghost_publish n s) = g_batch_depth s /\ g_trace (ghost_publish n s) = g_trace s /\ g_current_owner (ghost_publish n s) = g_current_owner s.
Proof. unfold ghost_publish. destruct (g_nodes s !! n); done. Qed.

Lemma host_write g (t : node_id) v sigs D s s' a :
  host_ok p sigs D s -> 0 < g_batch_depth s -> write_signal g (S t) v s = Some (s', a) ->
  is_Some (Semantics.program_signals p !! t) /\ host_ok p (<[t := v]> sigs) D s' /\ g_trace s' = g_trace s /\ g_batch_depth s' = g_batch_depth s.
Proof.
  intros Hh Hb E. pose proof E as E0. pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct g as [|g]; [done|]. cbn [write_signal] in E.
  unfold m_bind at 1, get_node in E. destruct (g_nodes s !! (S t : node_id)) as [d|] eqn:Hd; [|done].
  destruct (n_kind d) eqn:Ek; try done.
  assert (Hsig : is_Some (Semantics.program_signals p !! t)).
  { destruct (tbl_lookup p s _ d Ht Hd) as (n0&Hn0&_&Hm). injection Hn0 as <-.
    destruct (node_case p t d Hm) as [(x&Es&_)|[(_&c&_&K&_)|[(_&_&c&_&K&_)|(_&_&_&K&_)]]]; [by eexists|congruence|congruence|congruence]. }
  split; [done|]. destruct (decide (n_value d = v)) as [Hv|Hv].
  - unfold m_return in E. injection E as <- _. rewrite insert_id; [split_and!; try done|].
    rewrite (ho_sig _ _ _ _ Hh t d Hd Ek), Hv. done.
  - rewrite (bind_modify_node _ _ _ _ _ Hd), bind_m_modify in E. cbv beta in E.
    set (s1 := ghost_publish (S t) (set_node (S t) (node_with_value v d) s)) in E.
    apply bind_step in E as (s2 & Hmk & E).
    assert (Hn1 : g_nodes s1 = <[(S t : node_id) := node_with_value v d]> (g_nodes s)).
    { subst s1. unfold ghost_publish. simpl. rewrite lookup_insert_eq. reflexivity. }
    assert (Hrise : rtc mark_prim s1 s2) by (refine (any_mark_rtc _ _ None _ _ _); right; right; right; left; exact Hmk).
    pose proof (rtc_rise _ _ Hrise) as R.
    assert (Hs2 : s' = s2).
    { destruct g as [|g]; [done|]. cbn [request_flush] in E. rewrite bind_get in E.
      assert (Hbd : bool_decide (g_batch_depth s2 = 0) = false).
      { apply bool_decide_eq_false_2. rewrite (rs_batch _ _ R). subst s1. rewrite (proj1 (ghost_publish_keep _ _)). simpl. lia. }
      rewrite Hbd, andb_false_r in E. unfold m_return in E. by injection E as <-. }
    subst s'.
    assert (Hsh1 : shapes s1 = shapes s).
    { unfold shapes. rewrite Hn1, fmap_insert. assert (X : shape_of (node_with_value v d) = shape_of d) by reflexivity. rewrite X. apply insert_id. by rewrite lookup_fmap, Hd. }
    assert (Hsh : shapes s2 = shapes s) by (rewrite (rise_shapes _ _ R); exact Hsh1).
    assert (Hback : forall m x, g_nodes s2 !! m = Some x -> exists y, g_nodes s1 !! m = Some y /\ rise_node y x).
    { intros m x Hx. destruct (rs_back _ _ R m x Hx) as [y Hy]. destruct (rs_fwd _ _ R m y Hy) as (x'&Hx'&Rr). rewrite Hx in Hx'. injection Hx' as <-. by exists y. }
    destruct (host_of (write_signal (S g) (S t) v) sigs D (<[t := v]> sigs) D s s2 a Hh (Rwrite _ (runs_ok_all _) _ _) (Wwrite _ (W_ok_all _ _) _ _) (Swrite _ (sched_ok_all _) _ _ _) E0) as [Ho _].
    + rewrite (marks_own _ _ Hrise). subst s1. rewrite (proj2 (proj2 (ghost_publish_keep _ _))). done.
    + exact (tbl_shapes p s s2 Hsh Ht).
    + intros n x Hx Hk. destruct (Hback _ _ Hx) as (y&Hy&Rr). pose proof Rr as (Y1&Y2&_). rewrite Hn1 in Hy. unfold read_view_of in Y2. injection Y2 as Yv _ _.
      destruct (decide (n = t)) as [->|Hne].
      * rewrite lookup_insert_eq in Hy. injection Hy as <-. rewrite lookup_insert_eq, Yv. done.
      * rewrite lookup_insert_ne in Hy by congruence. rewrite lookup_insert_ne by done. rewrite Yv. apply (ho_sig _ _ _ _ Hh n y Hy). rewrite <- (rise_kind _ _ Rr). exact Hk.
    + intros m x Hx Hk. destruct (Hback _ _ Hx) as (y&Hy&Rr). pose proof Rr as (_&_&Y3&_). rewrite Hn1 in Hy. rewrite Y3.
      destruct (decide (m = S t)) as [->|Hne].
      * rewrite lookup_insert_eq in Hy. injection Hy as <-. rewrite (rise_kind _ _ Rr) in Hk. simpl in Hk. congruence.
      * rewrite lookup_insert_ne in Hy by done. apply (ho_live _ _ _ _ Hh m y Hy). rewrite <- (rise_kind _ _ Rr). exact Hk.
    + intros n x Hx Hk. destruct (Hback _ _ Hx) as (y&Hy&Rr). pose proof Rr as (_&_&Y3&_). rewrite Hn1 in Hy. rewrite Y3.
      destruct (decide (S n = S t)) as [He|Hne].
      * rewrite He in Hy. rewrite lookup_insert_eq in Hy. injection Hy as <-. rewrite (rise_kind _ _ Rr) in Hk. simpl in Hk. congruence.
      * rewrite lookup_insert_ne in Hy by done. apply (ho_disp _ _ _ _ Hh n y Hy). rewrite <- (rise_kind _ _ Rr). exact Hk.
    + split_and!; [exact Ho| |].
      * rewrite (rs_trace _ _ R). subst s1. rewrite (proj1 (proj2 (ghost_publish_keep _ _))). done.
      * rewrite (rs_batch _ _ R). subst s1. rewrite (proj1 (ghost_publish_keep _ _)). done.
Qed.

Lemma host_read g (t : node_id) sigs D s s' o :
  host_ok p sigs D s -> 0 < g_batch_depth s -> read g (S t) true s = Some (s', o) ->
  Semantics.memo_value p ∅ sigs t = Some o /\ host_ok p sigs D s' /\ g_batch_depth s' = g_batch_depth s /\
  exists b, g_trace s' = g_trace s ++ b /\ memo_logs s' b.
Proof.
  intros Hh Hb E. pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct (tbl_good' s Ht) as [Hg _].
  destruct (readable_node _ _ (read_readable _ _ _ _ _ E)) as [d Hd].
  pose proof (read_readable _ _ _ _ _ E) as Hrd.
  assert (Hp : pure_in (shapes s) (S t)).
  { refine (tbl_pure p s Hmb Ht t d Hd _). intros K. unfold readable in Hrd. rewrite (shapes_lookup s _ d Hd) in Hrd. unfold shape_of in Hrd. rewrite K in Hrd. done. }
  destruct (read_open g s (S t) s' o (ho_core _ _ _ _ Hh) (ho_wk _ _ _ _ Hh) Hg (ho_cur _ _ _ _ Hh) (ho_pull _ _ _ _ Hh) Hb Hp E) as (Hden&Kq&Kd).
  destruct (Tread _ (T_ok_all g) (S t) s s' o (tk_inv_of s (proj1 (ho_wk _ _ _ _ Hh)) (proj1 Hg)) (or_intror Hb) I E) as [_ Stp].
  pose proof (host_answers sigs D s (S t) o Hh Hden) as Hans. simpl in Hans.
  destruct (host_of (read g (S t) true) sigs D sigs D s s' o Hh (Rread _ (runs_ok_all _) _ _) (Wread _ (W_ok_all _ _) _ _) (Sread _ (sched_ok_all _) _ _ _) E (tk_own _ _ Stp)) as [Ho _].
  - exact (tbl_shapes p s s' (proj1 Kq) Ht).
  - intros n x Hx Hk. pose proof (shapes_node_back s s' (S n) x (proj1 Kq) Hx) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KSignal) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    assert (Hpy : pure_in (shapes s) (S n)) by (refine (tbl_pure p s Hmb Ht n y Hy _); congruence).
    destruct (b_window _ (proj1 (proj2 (ho_core _ _ _ _ Hh)))) as (_&_&Wsig&_).
    destruct (proj2 Kq (S n) y Hy Hpy (or_introl (or_introl (Wsig _ y Hy Hky)))) as (z&Hz&Vz&_). rewrite Hx in Hz. injection Hz as <-.
    unfold read_view_of in Vz. injection Vz as Vv _ _. rewrite Vv. exact (ho_sig _ _ _ _ Hh n y Hy Hky).
  - intros m x Hx Hk. pose proof (shapes_node_back s s' m x (proj1 Kq) Hx) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KMemo) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    destruct (Kd m y Hy) as (z&Hz&Dz). rewrite Hx in Hz. injection Hz as <-. rewrite Dz. exact (ho_live _ _ _ _ Hh m y Hy Hky).
  - intros n x Hx Hk. pose proof (shapes_node_back s s' (S n) x (proj1 Kq) Hx) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KEffect) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    destruct (Kd _ y Hy) as (z&Hz&Dz). rewrite Hx in Hz. injection Hz as <-. rewrite Dz. exact (ho_disp _ _ _ _ Hh n y Hy Hky).
  - split_and!; [exact Hans|exact Ho|exact (tk_bat _ _ Stp)|]. destruct (tk_out _ _ Stp) as (b&Tb&Ob). exists b. split; [done|].
    intros ev Hev. destruct (Ob ev Hev) as (m&v&->&[K|K]); [rewrite (ho_cur _ _ _ _ Hh) in K; done|]. exists m, v. split; [done|]. exact (memo_at_shapes s s' m (proj1 Kq) K).
Qed.

Lemma host_dispose g (n : node_id) sigs D s s' a d :
  host_ok p sigs D s -> g_nodes s !! (S n : node_id) = Some d -> n_kind d = KEffect -> dispose_node g (S n) s = Some (s', a) ->
  host_ok p sigs ({[ n ]} ∪ D) s' /\ g_trace s' = g_trace s /\ g_batch_depth s' = g_batch_depth s.
Proof.
  intros Hh Hd Hk E. pose proof E as E0. pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct (tbl_good' s Ht) as [Hg _].
  destruct g as [|g]; [done|]. cbn [dispose_node] in E. rewrite (bind_get_node _ _ _ _ Hd) in E.
  destruct (n_disposed d) eqn:Edi.
  - unfold m_return in E. injection E as <- _. split_and!; try done.
    assert (Hin : n ∈ D) by (apply (ho_disp _ _ _ _ Hh n d Hd Hk); done).
    assert (HD : {[ n ]} ∪ D = D) by set_solver. rewrite HD. exact Hh.
  - rewrite (bind_modify_node _ _ _ _ _ Hd) in E.
    set (x := node_with_link None (node_with_disposed true d)) in E.
    set (s1 := set_node (S n) x s) in E.
    assert (Hun : exists s2, (match n_link d with Some l => unlink l | None => m_return tt end) s1 = Some (s2, tt) /\ g_nodes s2 = g_nodes s1 /\ g_trace s2 = g_trace s1 /\ g_batch_depth s2 = g_batch_depth s1 /\ g_current_owner s2 = g_current_owner s1 /\ g_edges s2 = g_edges s1).
    { apply bind_inv in E as (s2 & [] & E1 & _). exists s2. split; [done|]. destruct (n_link d) as [l|].
      - unfold unlink, m_bind, get_owner in E1. destruct (g_owners s1 !! l_owner l) as [y|]; [|done]. destruct (decide _); unfold put_owner, m_modify, m_return in E1; by injection E1 as <-.
      - unfold m_return in E1. by injection E1 as <-. }
    destruct Hun as (s2&E1&N2&T2&B2&O2&_). rewrite (bind_some _ _ _ _ _ E1) in E.
    apply bind_edges_opt in E as (w & _ & E).
    destruct (w_effect _ (proj1 (ho_wk _ _ _ _ Hh)) (S n) d Hd (proj2 Hg _ d Hd Hk)) as [Hsc _]. rewrite Hsc in E. unfold m_return in E. injection E as <- _.
    assert (Hn' : g_nodes (with_edges w s2) = <[(S n : node_id) := x]> (g_nodes s)) by (simpl; rewrite N2; reflexivity).
    assert (Hsh : shapes (with_edges w s2) = shapes s).
    { unfold shapes. rewrite Hn', fmap_insert. assert (X : shape_of x = shape_of d) by reflexivity. rewrite X. apply insert_id. by rewrite lookup_fmap, Hd. }
    destruct (host_of (dispose_node (S g) (S n)) sigs D sigs ({[ n ]} ∪ D) s (with_edges w s2) a Hh (Rdispose _ (runs_ok_all _) _) (Wdisp _ (W_ok_all _ _) _) (Sdispose _ (sched_ok_all _) _ _) E0) as [Ho _].
    + simpl. rewrite O2. done.
    + exact (tbl_shapes p s _ Hsh Ht).
    + intros m y Hy Hky. rewrite Hn' in Hy. destruct (decide (S m = S n)) as [He|Hne].
      * rewrite He, lookup_insert_eq in Hy. injection Hy as <-. simpl in Hky. congruence.
      * rewrite lookup_insert_ne in Hy by done. exact (ho_sig _ _ _ _ Hh m y Hy Hky).
    + intros m y Hy Hky. rewrite Hn' in Hy. destruct (decide (m = S n)) as [->|Hne].
      * rewrite lookup_insert_eq in Hy. injection Hy as <-. simpl in Hky. congruence.
      * rewrite lookup_insert_ne in Hy by done. exact (ho_live _ _ _ _ Hh m y Hy Hky).
    + intros m y Hy Hky. rewrite Hn' in Hy. destruct (decide (m = n)) as [->|Hne].
      * rewrite lookup_insert_eq in Hy. injection Hy as <-. simpl. set_solver.
      * rewrite lookup_insert_ne in Hy by congruence. rewrite (ho_disp _ _ _ _ Hh m y Hy Hky). set_solver.
    + split_and!; [exact Ho|simpl; rewrite T2; reflexivity|simpl; rewrite B2; reflexivity].
Qed.

Lemma on_node_id e t (k : node_id -> M outcome) : on_node e (engine_id t) k = k (S t).
Proof.
  change ((fun s => (if decide (0 <= Z.of_nat (S t))%Z then k (Z.to_nat (Z.of_nat (S t))) else stuck) s) = k (S t)).
  rewrite decide_True by lia. rewrite Nat2Z.id. reflexivity.
Qed.

Lemma host_emit sigs D s e s' :
  host_ok p sigs D s -> (forall c, view_ev c e = false) -> plain_event e = true -> emit e s = Some (s', tt) -> host_ok p sigs D s'.
Proof.
  intros Hh Hv Hpl E. pose proof E as E0. unfold emit, m_modify in E. injection E as <-.
  refine (proj1 (host_of (emit e) sigs D sigs D s _ tt Hh (ok2_emit _ _) (proj2 (proj2 (wok_emit (Xe := fun _ => False) _ e Hv))) (sok_emit _ _ e Hpl) E0 eq_refl _ _ _ _)).
  - exact (ho_tbl _ _ _ _ Hh).
  - exact (ho_sig _ _ _ _ Hh).
  - exact (ho_live _ _ _ _ Hh).
  - exact (ho_disp _ _ _ _ Hh).
Qed.

Lemma apply_batch_step q i b0 st st1 r1 st2 r2 :
  Semantics.apply_input q i st = Some (st1, r1) -> Semantics.apply_batch q b0 st1 = Some (st2, r2) ->
  Semantics.apply_batch q (i :: b0) st = Some (st2, r1 ++ r2).
Proof.
  intros H1 H2. cbn [Semantics.apply_batch]. rewrite H1.
  change (('(st3, r3) ← Semantics.apply_batch q b0 st1; Some (st3, r1 ++ r3)) = Some (st2, r1 ++ r2)). rewrite H2. reflexivity.
Qed.

(** The reference's state at a host's inputs. *)
Definition ref_rel (sigs : gmap node_id val) (D : gset node_id) (st : Semantics.reference_state) : Prop :=
  Semantics.rs_signals st = sigs /\ Semantics.rs_frozen st = ∅ /\ Semantics.rs_disposed st = D.

(** The entries of live effects hold no cleanups, and disposed effects hold no entry. *)
Definition ref_entries (Bs : node_id -> node_id -> option outcome) (st : Semantics.reference_state) : Prop :=
  forall n c, Semantics.program_effects p !! n = Some c ->
    (n ∈ Semantics.rs_disposed st /\ Semantics.rs_effects st !! n = None) \/
    (n ∉ Semantics.rs_disposed st /\ Semantics.rs_effects st !! n = Some (tracked (Bs n) [] c, [])).

Lemma host_inputs b :
  forall f e sigs D s s' r st Bs,
    host_ok p sigs D s -> 0 < g_batch_depth s -> binder_env e -> ref_rel sigs D st -> ref_entries Bs st ->
    (forall n, Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None) ->
    exec f e (foldr (input_cmd p) done_cmd b) s = Some (s', r) ->
    exists sigs' D' st' rs tb,
      r = Ok 0%Z /\ Semantics.apply_batch p b st = Some (st', rs) /\ host_ok p sigs' D' s' /\ ref_rel sigs' D' st' /\ ref_entries Bs st' /\
      g_batch_depth s' = g_batch_depth s /\ g_trace s' = g_trace s ++ tb /\ project p tb = concat (Semantics.run_events <$> rs) /\
      (forall n c ev, Semantics.program_effects p !! n = Some c -> ev ∈ tb -> view_ev (S n) ev = false) /\
      D ⊆ D' /\ (forall ev, ev ∈ concat (Semantics.run_events <$> rs) -> (exists m o, ev = EvHostRead m o) \/ (exists m, ev = EvDispose m /\ m ∈ D')).
Proof.
  induction b as [|i b IH]; intros f e sigs D s s' r st Bs Hh Hb He Hr Hen Hdm E; cbn [foldr] in E.
  - destruct f as [|f]; [done|]. cbn [exec] in E. unfold done_cmd, on_value in E. simpl in E. unfold m_return in E. injection E as <- <-.
    exists sigs, D, st, [], []. split_and!; try done.
    + simpl. rewrite app_nil_r. done.
    + intros n c ev _ Hev. by apply not_elem_of_nil in Hev.
    + intros ev Hev. by apply not_elem_of_nil in Hev.
  - destruct (Hr) as (Rs&Rf&Rd). destruct f as [|f]; [done|]. destruct i as [t v|n|n]; unfold input_cmd in E.
    + (* a write *)
      cbn [exec] in E. rewrite on_node_id in E. change (on_value e (EConst v)) with (fun k : val -> M outcome => k v) in E. cbv beta in E.
      apply bind_inv in E as (s1 & [] & Ew & E).
      destruct (host_write f t v sigs D s s1 tt Hh Hb Ew) as (Hsig&Ho1&T1&B1).
      set (st1 := {| Semantics.rs_signals := <[t := v]> (Semantics.rs_signals st); Semantics.rs_effects := Semantics.rs_effects st; Semantics.rs_frozen := Semantics.rs_frozen st;
                     Semantics.rs_disposed := Semantics.rs_disposed st; Semantics.rs_round := Semantics.rs_round st |}).
      destruct (IH f e (<[t := v]> sigs) D s1 s' r st1 Bs Ho1 ltac:(lia) He ltac:(split_and!; simpl; [by rewrite Rs|done|done]) Hen ltac:(intros m Hm; apply Hdm; by right) E)
        as (sigs2&D2&st2&rs&tb&Hr0&Ab&Ho2&Rr2&En2&B2&T2&Pj&Vw&Sb&Fe).
      exists sigs2, D2, st2, rs, tb. split_and!; try done; [|congruence|congruence].
      refine (apply_batch_step _ _ _ _ st1 [] _ _ _ Ab). unfold Semantics.apply_input. rewrite decide_True by done. reflexivity.
    + (* a disposal *)
      assert (Hnm : Semantics.program_memos p !! n = None) by (apply Hdm; left).
      rewrite Hnm in E. destruct (Semantics.program_effects p !! n) as [c|] eqn:Ec.
      2: { exfalso. cbn [exec] in E. unfold stuck_cmd, on_value in E. simpl in E.
           assert (Hu : env_lookup e 0 = None) by (rewrite (He 0); [done|unfold prev_var; lia]). rewrite Hu in E. simpl in E. done. }
      cbn [exec] in E. rewrite on_node_id in E. destruct (effect_node s n c (ho_tbl _ _ _ _ Hh) Ec) as (d&Hd&Hk&Hbd&Hen0).
      rewrite (bind_get_node _ _ _ _ Hd), Hk in E.
      apply bind_inv in E as (s1 & [] & Ew & E).
      destruct (host_dispose f n sigs D s s1 tt d Hh Hd Hk Ew) as (Ho1&T1&B1).
      apply bind_inv in E as (s2 & [] & Em & E).
      assert (Hdisp : n_disposed d = true <-> n ∈ D) by exact (ho_disp _ _ _ _ Hh n d Hd Hk).
      destruct (Hen n c Ec) as [[Hin HnE]|[Hin HnE]].
      * (* already disposed *)
        rewrite Rd in Hin. rewrite (proj2 Hdisp Hin) in Em. unfold m_return in Em. injection Em as <-.
        assert (HD : {[ n ]} ∪ D = D) by (clear -Hin; set_solver). rewrite HD in Ho1.
        destruct (IH f e sigs D s1 s' r st Bs Ho1 ltac:(lia) He Hr Hen ltac:(intros m Hm; apply Hdm; by right) E)
          as (sigs2&D2&st2&rs&tb&Hr0&Ab&Ho2&Rr2&En2&B2&T2&Pj&Vw&Sb&Fe).
        exists sigs2, D2, st2, rs, tb. split_and!; try done; [|congruence|congruence].
        refine (apply_batch_step _ _ _ _ st [] _ _ _ Ab). unfold Semantics.apply_input, Semantics.apply_dispose. rewrite decide_True by (by rewrite Rd). reflexivity.
      * rewrite Rd in Hin. assert (Hdf : n_disposed d = false) by (destruct (n_disposed d) eqn:X; [exfalso; apply Hin; by apply Hdisp|done]). rewrite Hdf in Em.
        unfold emit, m_modify in Em. injection Em as <-.
        set (st1 := {| Semantics.rs_signals := Semantics.rs_signals st; Semantics.rs_effects := delete n (Semantics.rs_effects st); Semantics.rs_frozen := Semantics.rs_frozen st;
                       Semantics.rs_disposed := {[ n ]} ∪ Semantics.rs_disposed st; Semantics.rs_round := Semantics.rs_round st |}).
        assert (Ho2 : host_ok p sigs ({[ n ]} ∪ D) (with_trace (g_trace s1 ++ [EvDispose (S n)]) s1)) by exact (host_emit _ _ s1 (EvDispose (S n)) _ Ho1 (fun _ => eq_refl) eq_refl eq_refl).
        assert (Hen1 : ref_entries Bs st1).
        { intros m c1 Hc1. destruct (decide (m = n)) as [->|Hne].
          - left. split; [apply elem_of_union_l, elem_of_singleton; done|simpl; apply lookup_delete_eq].
          - destruct (Hen m c1 Hc1) as [[A1 A2]|[A1 A2]]; [left|right]; split.
            + apply elem_of_union_r. exact A1.
            + simpl. rewrite lookup_delete_ne by done. done.
            + intros K. apply elem_of_union in K as [K|K]; [apply elem_of_singleton in K; done|exact (A1 K)].
            + simpl. rewrite lookup_delete_ne by done. done. }
        destruct (IH f e sigs ({[ n ]} ∪ D) _ s' r st1 Bs Ho2 ltac:(simpl; lia) He ltac:(split_and!; simpl; [done|done|by rewrite Rd]) Hen1 ltac:(intros m Hm; apply Hdm; by right) E)
          as (sigs2&D2&st2&rs&tb&Hr0&Ab&Ho3&Rr2&En2&B2&T2&Pj&Vw&Sb&Fe).
        exists sigs2, D2, st2, ({| Semantics.run_node := n; Semantics.run_kind := Semantics.RkDispose; Semantics.run_snapshot := Semantics.rs_signals st; Semantics.run_frozen := Semantics.rs_frozen st;
                  Semantics.run_ran_cleanups := []; Semantics.run_reads := []; Semantics.run_all_reads := []; Semantics.run_events := [EvDispose n]; Semantics.run_writes := [] |} :: rs), (EvDispose (S n) :: tb).
        split_and!; try done.
        -- refine (apply_batch_step _ _ _ _ st1 [_] _ _ _ Ab). unfold Semantics.apply_input, Semantics.apply_dispose. rewrite decide_False by (by rewrite Rd). rewrite Ec.
           unfold Semantics.effect_entry. rewrite HnE. reflexivity.
        -- simpl in B2. congruence.
        -- simpl in T2. rewrite T2, T1. rewrite <- !app_assoc. done.
        -- change (EvDispose (S n) :: tb) with ([EvDispose (S n)] ++ tb). rewrite project_app, Pj. done.
        -- intros m c1 ev Hc1 Hev. apply elem_of_cons in Hev as [->|Hev]; [done|exact (Vw m c1 ev Hc1 Hev)].
        -- intros x Hx. apply Sb, elem_of_union_r. exact Hx.
        -- intros ev Hev. simpl in Hev. apply elem_of_cons in Hev as [->|Hev]; [right; exists n; split; [done|apply Sb, elem_of_union_l, elem_of_singleton; done]|exact (Fe ev Hev)].
    + (* a host read *)
      cbn [exec] in E. rewrite on_node_id in E.
      apply bind_inv in E as (s1 & o & Ew & E).
      destruct (host_read f n sigs D s s1 o Hh Hb Ew) as (Hv&Ho1&B1&(bm&T1&Ml)).
      apply bind_inv in E as (s2 & [] & En & E).
      unfold note_host_read in En. rewrite (ho_cur _ _ _ _ Ho1), (ho_own _ _ _ _ Ho1), decide_True in En by done.
      unfold emit, m_modify in En. injection En as <-.
      assert (Ho2 : host_ok p sigs D (with_trace (g_trace s1 ++ [EvHostRead (S n) o]) s1)) by exact (host_emit _ _ s1 (EvHostRead (S n) o) _ Ho1 (fun _ => eq_refl) eq_refl eq_refl).
      assert (He' : forall x v, binder_env ((prev_var, v) :: (prev_var, x) :: e)).
      { intros x v y Hy. simpl. rewrite (proj2 (Nat.eqb_neq y prev_var) Hy). exact (He y Hy). }
      assert (Hrest : exists e', binder_env e' /\ exec f e' (foldr (input_cmd p) done_cmd b) (with_trace (g_trace s1 ++ [EvHostRead (S n) o]) s1) = Some (s', r)).
      { destruct o as [v|y]; eexists; (split; [apply He'|exact E]). }
      destruct Hrest as (e'&He2&E2).
      destruct (IH f e' sigs D _ s' r st Bs Ho2 ltac:(simpl; lia) He2 Hr Hen ltac:(intros m Hm; apply Hdm; by right) E2)
        as (sigs2&D2&st2&rs&tb&Hr0&Ab&Ho3&Rr2&En2&B2&T2&Pj&Vw&Sb&Fe).
      exists sigs2, D2, st2, ({| Semantics.run_node := n; Semantics.run_kind := Semantics.RkHostRead; Semantics.run_snapshot := Semantics.rs_signals st; Semantics.run_frozen := Semantics.rs_frozen st;
                  Semantics.run_ran_cleanups := []; Semantics.run_reads := []; Semantics.run_all_reads := []; Semantics.run_events := [EvHostRead n o]; Semantics.run_writes := [] |} :: rs),
        (bm ++ EvHostRead (S n) o :: tb).
      split_and!; try done.
      * refine (apply_batch_step _ _ _ _ st [_] _ _ _ Ab). unfold Semantics.apply_input, Semantics.host_read, Semantics.live_read. rewrite Rs, Rf, Hv. simpl. rewrite <- Rs, <- Rf. reflexivity.
      * simpl in B2. congruence.
      * simpl in T2. rewrite T2, T1. rewrite <- !app_assoc. done.
      * rewrite project_app, (memo_logs_project s1 bm (ho_tbl _ _ _ _ Ho1) Ml). simpl.
        change (EvHostRead (S n) o :: tb) with ([EvHostRead (S n) o] ++ tb). rewrite project_app, Pj. done.
      * intros m c1 ev Hc1 Hev. apply elem_of_app in Hev as [Hev|Hev].
        -- destruct (Ml ev Hev) as (u&v&->&dm&Hdn&Kdm). simpl. apply bool_decide_eq_false_2. intros ->.
           destruct (effect_node s1 m c1 (ho_tbl _ _ _ _ Ho1) Hc1) as (x&Hx&Kx&_). rewrite Hx in Hdn. injection Hdn as <-. congruence.
        -- apply elem_of_cons in Hev as [->|Hev]; [done|exact (Vw m c1 ev Hc1 Hev)].
      * intros ev Hev. simpl in Hev. apply elem_of_cons in Hev as [->|Hev]; [left; by eexists _, _|exact (Fe ev Hev)].
Qed.

Lemma host_batch sigs D s v : host_ok p sigs D s -> sched_inv None (with_batch_depth v s) -> host_ok p sigs D (with_batch_depth v s).
Proof.
  intros Hh Hs. destruct Hh as [H1 H2 H3 H4 H5 H6 H7 H8 H9 H10 H11 H12 H13 H14]. split; try done.
  - by apply core_batch.
  - apply (wk_same s); [split_and!; done|exact H2].
Qed.

(** A flush from a state at the top. *)
Lemma host_flush g sigs D s s' a :
  host_ok p sigs D s -> g_batch_depth s = 0 -> request_flush g s = Some (s', a) ->
  host_ok p sigs D s' /\ g_batch_depth s' = 0 /\ pending s' = [] /\ post s s' /\
  exists t rs, g_trace s' = g_trace s ++ t /\ flush_tr s' t rs /\
    (fun x => x.1.1) <$> rs `sublist_of` flush_queue g s /\ drop (g_queue_head s) (g_queue s) `prefix_of` flush_queue g s.
Proof.
  intros Hh Hb E. pose proof E as E0. pose proof (ho_tbl _ _ _ _ Hh) as Ht. destruct (tbl_good' s Ht) as [Hg Hnu].
  destruct g as [|g]; [done|]. cbn [request_flush] in E. rewrite bind_get in E.
  assert (Hbd : bool_decide (g_batch_depth s = 0) = true) by (by apply bool_decide_eq_true_2). rewrite Hbd, andb_true_r in E.
  destruct (live_queue s) eqn:Elq.
  2: { unfold m_return in E. injection E as <- _. split_and!; [done|done| |apply post_refl|].
       - unfold pending. apply drop_ge. unfold live_queue in Elq. apply bool_decide_eq_false_1 in Elq. lia.
       - exists [], []. split_and!; [simpl; rewrite app_nil_r; done|constructor|apply sublist_nil_l|].
         rewrite drop_ge; [apply prefix_nil|]. unfold live_queue in Elq. apply bool_decide_eq_false_1 in Elq. lia. }
  assert (Hout : outside_bodies s = true).
  { unfold outside_bodies. rewrite (ho_pull _ _ _ _ Hh), (ho_cur _ _ _ _ Hh), (ho_unt _ _ _ _ Hh). done. }
  rewrite Hout in E. pose proof E as Ef.
  destruct g as [|g]; [done|]. cbn [run_flush] in E. rewrite bind_get, (ho_flu _ _ _ _ Hh), bind_m_modify in E. cbv beta in E.
  set (s1 := with_flush_owed false (with_flushing true s)) in E.
  destruct (ho_core _ _ _ _ Hh) as [Hi [Hb0 Hx]] eqn:Ecore.
  destruct Hb0 as [B1 B2 [E1 E2 E3 E4] B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 B16 B17 B18] eqn:Ebase.
  assert (Hb1 : run_base s1).
  { refine (Build_run_base s1 B1 B2 (Build_mark_env s1 E1 E2 E3 E4) B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 _ B17 B18).
    intros c _ _. done. }
  assert (Hi1 : check_invariant s1).
  { eapply invariant_irrelevant; [|exact Hi]. unfold irrelevant. simpl. split_and!; reflexivity. }
  assert (Hmemo : forall c, c ∈ g_running s -> has_kind c KMemo s).
  { intros c Hc. rewrite (ho_run _ _ _ _ Hh) in Hc. by apply not_elem_of_nil in Hc. }
  assert (Hctx1 : flush_ctx s1) by (split_and!; [exact (ho_cur _ _ _ _ Hh)|done|exact Hmemo]).
  apply bind_inv in E as (s2 & [] & Hd & E2').
  unfold m_modify in E2'. injection E2' as <- _.
  assert (Hw1 : wk0 s1) by (apply (wk_same s); [split_and!; done|exact (ho_wk _ _ _ _ Hh)]).
  assert (Hg1 : good s1) by exact (good_shapes s s1 eq_refl Hg).
  destruct (drain_fw g s1 s2 tt (conj Hi1 (conj Hb1 Hx)) Hw1 Hctx1 Hg1 Hd) as (_&Kq&Kd&Ow&Hl).
  assert (Kq' : keepq s (with_flushing false s2)) by exact Kq.
  assert (Kd' : keepd s (with_flushing false s2)) by exact Kd.
  assert (Ksh : shapes (with_flushing false s2) = shapes s) by exact (proj1 Kq).
  destruct (host_of (request_flush (S (S g))) sigs D sigs D s (with_flushing false s2) a Hh (Rrequest _ (runs_ok_all _)) (Wreq _ (W_ok_all _ _)) (Srequest _ (sched_ok_all _) _) E0) as [Ho Hpost].
  - exact Ow.
  - exact (tbl_shapes p s _ Ksh Ht).
  - intros n x Hx1 Hk. pose proof (shapes_node_back s _ (S n) x Ksh Hx1) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KSignal) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    assert (Hpy : pure_in (shapes s) (S n)) by (refine (tbl_pure p s Hmb Ht n y Hy _); congruence).
    destruct (b_window _ (proj1 (proj2 (ho_core _ _ _ _ Hh)))) as (_&_&Wsig&_).
    destruct (proj2 Kq' (S n) y Hy Hpy (or_introl (or_introl (Wsig _ y Hy Hky)))) as (z&Hz&Vz&_). rewrite Hx1 in Hz. injection Hz as <-.
    unfold read_view_of in Vz. injection Vz as Vv _ _. rewrite Vv. exact (ho_sig _ _ _ _ Hh n y Hy Hky).
  - intros m x Hx1 Hk. pose proof (shapes_node_back s _ m x Ksh Hx1) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KMemo) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    destruct (Kd' m y Hy) as (z&Hz&Dz). rewrite Hx1 in Hz. injection Hz as <-. rewrite Dz. exact (ho_live _ _ _ _ Hh m y Hy Hky).
  - intros n x Hx1 Hk. pose proof (shapes_node_back s _ (S n) x Ksh Hx1) as (y&Hy&Hsy).
    assert (Hky : n_kind y = KEffect) by (unfold shape_of in Hsy; injection Hsy as K0 _ _ _; congruence).
    destruct (Kd' _ y Hy) as (z&Hz&Dz). rewrite Hx1 in Hz. injection Hz as <-. rewrite Dz. exact (ho_disp _ _ _ _ Hh n y Hy Hky).
  - pose proof Hpost as [[_ _ R3 _ _ _] _]. split_and!; [exact Ho|rewrite R3; exact Hb| |exact Hpost|].
    + exact (proj1 (proj2 (drain_no_escape _ _ _ _ (ho_flu _ _ _ _ Hh) Ef))).
    + destruct (Hl Hnu) as (t&rs&Tt&Ft&Sb&Pf). exists t, rs. split_and!; [exact Tt| |exact Sb|exact Pf].
      refine (flush_tr_keep _ _ _ _ _ Ft). split; [reflexivity|]. intros u e He _ Hq. exists e. done.
Qed.

(** One batch and its flush from a settled state. *)
Lemma host_step g b sigs D s s' r st Bs :
  host_ok p sigs D s -> g_batch_depth s = 0 -> ref_rel sigs D st -> ref_entries Bs st ->
  (forall n, Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None) ->
  exec g [] (batch_cmd p b done_cmd) s = Some (s', r) ->
  exists sigs' D' st' rs tb t fr,
    r = Ok 0%Z /\ Semantics.apply_batch p b st = Some (st', rs) /\ host_ok p sigs' D' s' /\ g_batch_depth s' = 0 /\ pending s' = [] /\
    ref_rel sigs' D' st' /\ ref_entries Bs st' /\
    g_trace s' = g_trace s ++ tb ++ t /\ project p tb = evs rs /\ flush_tr s' t fr /\
    (forall n c ev, Semantics.program_effects p !! n = Some c -> ev ∈ tb -> view_ev (S n) ev = false) /\
    (forall n c, Semantics.program_effects p !! n = Some c -> n ∈ D' -> count_runs (S n) t = 0) /\
    D ⊆ D' /\ (forall ev, ev ∈ evs rs -> (exists m o, ev = EvHostRead m o) \/ (exists m, ev = EvDispose m /\ m ∈ D')).
Proof.
  intros Hh Hb Hr Hen Hdm E. destruct g as [|g]; [done|]. unfold batch_cmd in E. cbn [exec] in E. rewrite bind_m_modify in E. cbv beta in E.
  set (s1 := with_batch_depth (S (g_batch_depth s)) s) in E.
  apply bind_inv in E as (s2 & r0 & Ei & E).
  assert (Ho1 : host_ok p sigs D s1) by (apply host_batch; [exact Hh|exact (sched_up _ (ho_sched _ _ _ _ Hh))]).
  destruct (host_inputs b g [] sigs D s1 s2 r0 st Bs Ho1 ltac:(simpl; lia) binder_nil Hr Hen Hdm Ei)
    as (sigs2&D2&st2&rs&tb&->&Ab&Ho2&Rr2&En2&B2&T2&Pj&Vw&Sb&Fe).
  rewrite bind_m_modify in E. cbv beta in E.
  set (s3 := with_batch_depth (pred (g_batch_depth s2)) s2) in E.
  assert (Hb3 : g_batch_depth s3 = 0) by (simpl; rewrite B2; simpl; rewrite Hb; done).
  assert (Ho3 : host_ok p sigs2 D2 s3) by (apply host_batch; [exact Ho2|exact (sched_down _ _ (ho_sched _ _ _ _ Ho2) (ho_owed _ _ _ _ Ho2))]).
  apply bind_inv in E as (s4 & [] & Ef & E).
  destruct (host_flush g sigs2 D2 s3 s4 tt Ho3 Hb3 Ef) as (Ho4&Hb4&Hp4&Hpost&t&fr&Tt&Ft&_).
  destruct g as [|g]; [done|]. cbn [exec] in E. unfold on_value in E. simpl in E. unfold m_return in E. injection E as <- <-.
  exists sigs2, D2, st2, rs, tb, t, fr. split_and!; try done.
  - rewrite Tt. change (g_trace s3) with (g_trace s2). rewrite T2. change (g_trace s1) with (g_trace s). rewrite <- app_assoc. done.
  - intros n c Hc Hn. destruct (effect_node s3 n c (ho_tbl _ _ _ _ Ho3) Hc) as (d&Hd&Hk&_).
    pose proof (proj2 (ho_disp _ _ _ _ Ho3 n d Hd Hk) Hn) as Hdi.
    destruct Hpost as [_ [[_ Rr _ _] _]]. pose proof (Rr _ d Hd Hk Hdi) as Hc0. rewrite Tt, count_runs_app in Hc0. lia.
Qed.

End Host.

#[local] Arguments Semantics.settle : simpl never.

Definition ref_all (ord : Semantics.effect_order) (p : Semantics.program) (h : Semantics.history)
    : option (Semantics.reference_state * list Semantics.run) :=
  '(st1, r1) ← Semantics.run_round p (ord 0%nat (Semantics.effect_ids p)) (Semantics.reference_start p);
  '(st2, r2) ← Semantics.settle 1 ord p (Semantics.next_round st1);
  '(st3, r3) ← Semantics.run_history 1 ord p h st2;
  Some (st3, r1 ++ r2 ++ r3).

Lemma ref_all_trace ord p h st rs :
  ref_all ord p h = Some (st, rs) -> Semantics.reference_trace 1 ord p h = Some (evs rs).
Proof.
  unfold ref_all, Semantics.reference_trace, Semantics.reference_runs. intros H.
  destruct (Semantics.run_round _ _ _) as [[st1 r1]|]; simpl in *; [|done].
  destruct (Semantics.settle _ _ _ _) as [[st2 r2]|]; simpl in *; [|done].
  destruct (Semantics.run_history _ _ _ _ _) as [[st3 r3]|]; simpl in *; [|done]. injection H as <- <-. done.
Qed.

Lemma run_history_snoc f ord p h b st st1 r1 st2 r2 st3 r3 :
  Semantics.run_history f ord p h st = Some (st1, r1) -> Semantics.apply_batch p b st1 = Some (st2, r2) -> Semantics.settle f ord p st2 = Some (st3, r3) ->
  Semantics.run_history f ord p (h ++ [b]) st = Some (st3, r1 ++ r2 ++ r3).
Proof.
  revert st r1. induction h as [|b0 h IH]; intros st r1 H A S; simpl in *.
  - injection H as <- <-. rewrite A. simpl. rewrite S. simpl. by rewrite !app_nil_r.
  - destruct (Semantics.apply_batch p b0 st) as [[s0 r0]|]; simpl in *; [|done].
    destruct (Semantics.settle f ord p s0) as [[s1 r10]|]; simpl in *; [|done].
    destruct (Semantics.run_history f ord p h s1) as [[s2 r20]|] eqn:E; simpl in *; [|done]. injection H as <- <-.
    rewrite (IH _ _ E A S). simpl. by rewrite <- !app_assoc.
Qed.

Lemma ref_all_snoc ord p h b st rs st2 r2 st3 r3 :
  ref_all ord p h = Some (st, rs) -> Semantics.apply_batch p b st = Some (st2, r2) -> Semantics.settle 1 ord p st2 = Some (st3, r3) ->
  ref_all ord p (h ++ [b]) = Some (st3, rs ++ r2 ++ r3).
Proof.
  unfold ref_all. intros H A S.
  destruct (Semantics.run_round _ _ _) as [[s1 r1]|]; simpl in *; [|done].
  destruct (Semantics.settle _ _ _ _) as [[s2 r20]|]; simpl in *; [|done].
  destruct (Semantics.run_history _ _ _ _ _) as [[s3 r30]|] eqn:E; simpl in *; [|done]. injection H as <- <-.
  rewrite (run_history_snoc _ _ _ _ _ _ _ _ _ _ _ _ E A S). simpl. by rewrite <- !app_assoc.
Qed.

Lemma history_split p b h :
  forall g s s'' r, exec g [] (history_cmd p (h ++ [b])) s = Some (s'', r) ->
  exists s1 r1, exec g [] (history_cmd p h) s = Some (s1, r1) /\
    ((exists y, r1 = Fail y) \/ (r1 = Ok 0%Z /\ exists g', exec g' [] (batch_cmd p b done_cmd) s1 = Some (s'', r))).
Proof.
  induction h as [|b0 h IH]; intros g s s'' r E; simpl in E.
  - destruct g as [|g0]; [done|]. exists s, (Ok 0%Z). split; [cbn [exec]; unfold on_value; simpl; done|right]. split; [done|]. exists (S g0). exact E.
  - destruct g as [|g0]; [done|]. unfold batch_cmd at 1 in E. cbn [exec] in E. rewrite bind_m_modify in E. cbv beta in E.
    apply bind_inv in E as (s2 & r2 & Ei & E). rewrite bind_m_modify in E. cbv beta in E. simpl.
    unfold batch_cmd at 1. cbn [exec]. rewrite bind_m_modify. cbv beta. rewrite (bind_some _ _ _ _ _ Ei), bind_m_modify. cbv beta.
    destruct r2 as [v|y].
    + apply bind_inv in E as (s3 & [] & Er & E). rewrite (bind_some _ _ _ _ _ Er). exact (IH _ _ _ _ E).
    + eexists _, (Fail y). split; [reflexivity|]. left. by exists y.
Qed.

(** Splits a program's first batch off. *)
Lemma erase_parts p h f s r :
  run_top f (erase p h) (init current_flags) = Some (s, r) ->
  exists g s1 r1 s2,
    f = S g /\ exec g [] (setup_cmd p) (with_batch_depth 1 (init current_flags)) = Some (s1, r1) /\
    (match r1 with
     | Ok _ => request_flush g (with_batch_depth (pred (g_batch_depth s1)) s1) = Some (s2, tt) /\ exec g [] (history_cmd p h) s2 = Some (s, r)
     | Fail _ => True
     end).
Proof.
  unfold run_top, erase. intros E. destruct f as [|g]; [done|]. cbn [exec] in E. rewrite bind_m_modify in E. cbv beta in E.
  apply bind_inv in E as (s1 & r1 & E1 & E). rewrite bind_m_modify in E. cbv beta in E. destruct r1 as [v|y].
  - apply bind_inv in E as (s2 & [] & Er & E). exists g, s1, (Ok v), s2. done.
  - exists g, s1, (Fail y), s. done.
Qed.

Lemma erase_build p h g s1 s2 s r v :
  exec g [] (setup_cmd p) (with_batch_depth 1 (init current_flags)) = Some (s1, Ok v) ->
  request_flush g (with_batch_depth (pred (g_batch_depth s1)) s1) = Some (s2, tt) -> exec g [] (history_cmd p h) s2 = Some (s, r) ->
  run_top (S g) (erase p h) (init current_flags) = Some (s, r).
Proof.
  intros E1 E2 E3. unfold run_top, erase. cbn [exec]. rewrite bind_m_modify. cbv beta. rewrite (bind_some _ _ _ _ _ E1), bind_m_modify. cbv beta. rewrite (bind_some _ _ _ _ _ E2). exact E3.
Qed.

Lemma pfx_refl {A} (l : list A) : l `prefix_of` l.
Proof. exists []. by rewrite app_nil_r. Qed.

(** ** The simulation *)

Section Sim.
Variable p : Semantics.program.
Hypothesis Hwf : Semantics.well_formed_program p.
Hypothesis Hmb : memos_below p.
Hypothesis Hro : effects_read_only p.
Variable ord : Semantics.effect_order.
Hypothesis Hord : Semantics.is_effect_order ord.

Definition settled_map (t : list event) : gmap node_id Semantics.settled_view := foldl Semantics.settled_step ∅ t.

(** What a prefix of the history holds. The last clause is the exact trace, given matching runs. *)
Record sim_inv (s : state) (st : Semantics.reference_state) (rs : list Semantics.run) (sigs : gmap node_id val) (D : gset node_id) : Prop := {
  si_host : host_ok p sigs D s;
  si_bat : g_batch_depth s = 0;
  si_quiet : pending s = [];
  si_ref : ref_rel sigs D st;
  si_set : ref_settled p st;
  si_map : settled_map (project p (g_trace s)) = settled_map (evs rs);
  si_reads : Semantics.host_reads (project p (g_trace s)) = Semantics.host_reads (evs rs);
  si_j : forall n c, Semantics.program_effects p !! n = Some c -> n ∉ D ->
    settled_map (evs rs) !! n = Some (aft (settled_map (evs rs) !! n) n (content p sigs n).1 (content p sigs n).2)
}.

Lemma sim_converged s sigs D :
  host_ok p sigs D s -> pending s = [] -> (exists f c r, run_top f c (init current_flags) = Some (s, r)) -> converged s.
Proof.
  intros Hh Hq (f&c&r&Hm). destruct (tbl_good' p Hwf Hmb Hro s (ho_tbl _ _ _ _ Hh)) as [Hg _].
  exact (convergence f c s r Hm (proj1 Hg) (proj2 Hg) (conj Hq (ho_run _ _ _ _ Hh))).
Qed.

Lemma pending_of sigs D st Bs (s : state) :
  host_ok p sigs D s -> converged s -> ref_rel sigs D st -> ref_entries p Bs st ->
  (forall n c, Semantics.program_effects p !! n = Some c -> n ∉ D -> exists ro, body_eval (Bs n) [] c = Some ro) ->
  ref_pending p (content p sigs) st.
Proof.
  intros Hh Hc (Rs&Rf&Rd) Hen Hold n c Hn. destruct (Hen n c Hn) as [K|[K1 K2]]; [by left|right]. split; [done|]. rewrite Rs, Rf.
  rewrite Rd in K1. destruct (converged_content p Hwf Hmb Hro sigs D s n c Hh Hc Hn K1) as (r&l&pre&Hcn&E1&E2&_). rewrite Hcn. split_and!; [done|done|].
  destruct (Hold n c Hn K1) as [ro Hro0]. exists (Bs n), ro. split; [done|]. exact K2.
Qed.

Lemma fail_shift (c m : node_id) a b : fail_evs c a = fail_evs c b -> fail_evs m a = fail_evs m b.
Proof. destruct a, b; simpl; try done. intros H. injection H as ->. done. Qed.

Lemma foldl_inputs M (D' : gset node_id) evs0 m :
  (forall ev, ev ∈ evs0 -> (exists u o, ev = EvHostRead u o) \/ (exists u, ev = EvDispose u /\ u ∈ D')) -> m ∉ D' ->
  foldl Semantics.settled_step M evs0 !! m = M !! m.
Proof.
  revert M. induction evs0 as [|ev evs0 IH]; intros M H Hm; [done|]. simpl. rewrite IH; [|intros e He; apply H; by right|done].
  destruct (H ev (list_elem_of_here _ _)) as [(u&o&->)|(u&->&Hu)]; [done|]. simpl. rewrite lookup_insert_ne; [done|]. intros ->. done.
Qed.

Lemma runs_in_inputs (D' : gset node_id) evs0 :
  (forall ev, ev ∈ evs0 -> (exists u o, ev = EvHostRead u o) \/ (exists u, ev = EvDispose u /\ u ∈ D')) -> runs_in evs0 = [].
Proof.
  induction evs0 as [|ev evs0 IH]; intros H; [done|]. destruct (H ev (list_elem_of_here _ _)) as [(u&o&->)|(u&->&_)]; simpl; apply IH; intros e He; apply H; by right.
Qed.

(** The step: from a prefix to the prefix with one more batch. *)
Lemma sim_step g b sigs D s s' r st rs :
  sim_inv s st rs sigs D ->
  (forall n, Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None) ->
  exec g [] (batch_cmd p b done_cmd) s = Some (s', r) -> (exists f c r0, run_top f c (init current_flags) = Some (s', r0)) ->
  (exists f c r0, run_top f c (init current_flags) = Some (s, r0)) ->
  exists sigs' D' st' rs0 rs1 es ls pre,
    r = Ok 0%Z /\ Semantics.apply_batch p b st = Some (fst rs0, snd rs0) /\ Semantics.settle 1 ord p (fst rs0) = Some (st', rs1) /\
    sim_inv s' st' (rs ++ snd rs0 ++ rs1) sigs' D' /\
    project p (g_trace s') = project p (g_trace s) ++ pre ++ concat (blkF (content p sigs') <$> es) /\
    evs (rs ++ snd rs0 ++ rs1) = evs rs ++ pre ++ concat (blkF (content p sigs') <$> ls) /\ runs_in pre = [].
Proof.
  intros Hi Hdm E Hrun Hrun0. destruct Hi as [Hh Hb Hq Hr Hset Hmap Hrd Hj].
  pose proof Hr as Hr0. destruct Hr as (Rs&Rf&Rd).
  set (Bs := fun _ : node_id => Semantics.memo_value p ∅ sigs).
  assert (Hen : ref_entries p Bs st).
  { intros n c Hc. destruct (Hset n c Hc) as [K|[K1 K2]]; [by left|right]. rewrite Rs, Rf in K2. done. }
  destruct (host_step p Hwf Hmb Hro g b sigs D s s' r st Bs Hh Hb Hr0 Hen Hdm E)
    as (sigs1&D1&st1&rs1&tb&t&fr&->&Ab&Ho&Hb'&Hq'&Hr1&En1&Tr&Pj&Ft&Vw&Cnt&Sb&Fe).
  pose proof (sim_converged s' sigs1 D1 Ho Hq' Hrun) as Hcv.
  pose proof (sim_converged s sigs D Hh Hq Hrun0) as Hcv0.
  assert (Hold : forall n c, Semantics.program_effects p !! n = Some c -> n ∉ D ->
     body_eval (Bs n) [] c = Some (content p sigs n).1 /\ body_logs (Bs n) [] c = Some (content p sigs n).2 /\
     exists pre, trace_view (S n) (g_trace s) = pre ++ EvRun (S n) :: (EvLog (S n) <$> (content p sigs n).2) ++ fail_evs (S n) (content p sigs n).1).
  { intros n c Hc Hn. destruct (converged_content p Hwf Hmb Hro sigs D s n c Hh Hcv0 Hc Hn) as (r0&l0&pre&Hcn&E1&E2&V). rewrite Hcn. split_and!; [exact E1|exact E2|by exists pre]. }
  assert (Hnew : forall n c, Semantics.program_effects p !! n = Some c -> n ∉ D1 ->
     body_eval (Semantics.memo_value p ∅ sigs1) [] c = Some (content p sigs1 n).1 /\ body_logs (Semantics.memo_value p ∅ sigs1) [] c = Some (content p sigs1 n).2 /\
     exists pre, trace_view (S n) (g_trace s') = pre ++ EvRun (S n) :: (EvLog (S n) <$> (content p sigs1 n).2) ++ fail_evs (S n) (content p sigs1 n).1).
  { intros n c Hc Hn. destruct (converged_content p Hwf Hmb Hro sigs1 D1 s' n c Ho Hcv Hc Hn) as (r0&l0&pre&Hcn&E1&E2&V). rewrite Hcn. split_and!; [exact E1|exact E2|by exists pre]. }
  assert (Hpen : ref_pending p (content p sigs1) st1).
  { refine (pending_of sigs1 D1 st1 Bs s' Ho Hcv Hr1 En1 _). intros n c Hc Hn. assert (Hn0 : n ∉ D) by set_solver. destruct (Hold n c Hc Hn0) as (A&_). by eexists. }
  destruct (ref_settle p Hro ord (content p sigs1) st1 Hord Hpen) as (ns&st2&rs2&Es&S2&Fz2&D2&Hset2&Sbn&Hx&Ev2).
  destruct Hr1 as (Rs1&Rf1&Rd1).
  destruct (flush_project p Hwf Hmb Hro sigs1 D1 s' t fr Ho Ft) as (es&Pt&He&H2&H3).
  set (F := content p sigs1). set (Fo := content p sigs).
  set (ls := ord (Semantics.rs_round st1) ns).
  set (M := settled_map (evs rs)).
  set (M1 := foldl Semantics.settled_step M (evs rs1)).
  assert (HM1 : forall m, m ∉ D1 -> M1 !! m = M !! m) by (intros m Hm; exact (foldl_inputs M D1 _ m Fe Hm)).
  (* Nothing moved: the content holds. *)
  assert (Hmv : forall m c, Semantics.program_effects p !! m = Some c -> m ∉ D1 -> m ∉ ns -> F m = Fo m).
  { intros m c Hc Hm Hns. assert (Hm0 : m ∉ D) by set_solver. destruct (Hold m c Hc Hm0) as (O1&O2&_). destruct (Hnew m c Hc Hm) as (N1&N2&_).
    destruct (moved_path _ _ _ _ _ _ O1 N1) as (mv&Hmm&Hk). destruct mv.
    - exfalso. apply Hns. apply Hx. split; [by eexists|]. unfold Semantics.effect_entry. rewrite Rf1, Rs1.
      destruct (En1 m c Hc) as [[K _]|[_ K]]; [rewrite Rd1 in K; done|]. rewrite K. exact Hmm.
    - destruct (Hk eq_refl) as (A1&A2&_). unfold F, Fo. rewrite A2, O2 in N2. injection N2 as N2. rewrite (surjective_pairing (content p sigs1 m)), (surjective_pairing (content p sigs m)). f_equal; [done|]. done. }
  (* A live effect no flush ran: its last run stands. *)
  assert (Hnr : forall m c, Semantics.program_effects p !! m = Some c -> m ∉ D1 -> m ∉ es ->
            (F m).2 = (Fo m).2 /\ fail_evs m (F m).1 = fail_evs m (Fo m).1).
  { intros m c Hc Hm Hes. assert (Hm0 : m ∉ D) by set_solver. destruct (Hold m c Hc Hm0) as (_&_&pre0&V0). destruct (Hnew m c Hc Hm) as (_&_&pre1&V1).
    destruct (effect_node p Hwf s' m c (ho_tbl _ _ _ _ Ho) Hc) as (d&Hd&Hk&_).
    assert (Hvw : trace_view (S m) (g_trace s') = trace_view (S m) (g_trace s)).
    { rewrite Tr, !trace_view_app, (view_nil (S m) tb); [|intros ev Hev; exact (Vw m c ev Hc Hev)].
      rewrite (flush_view s' t fr m d Ft Hd Hk (H3 m Hes)). simpl. by rewrite !app_nil_r. }
    rewrite Hvw, V0 in V1. destruct (last_block_unique _ _ _ _ _ _ _ V1) as [A1 A2]. split; [done|].
    exact (fail_shift _ _ _ _ (eq_sym A2)). }
  assert (HJ : forall m c, Semantics.program_effects p !! m = Some c -> m ∉ D1 -> (F m).2 = (Fo m).2 -> fail_evs m (F m).1 = fail_evs m (Fo m).1 ->
            M1 !! m = Some (aft (M1 !! m) m (F m).1 (F m).2)).
  { intros m c Hc Hm A1 A2. assert (Hm0 : m ∉ D) by (intros K; apply Hm; exact (Sb m K)). rewrite HM1 by done.
    pose proof (Hj m c Hc Hm0) as Hjm. change (M !! m = Some (aft (M !! m) m (Fo m).1 (Fo m).2)) in Hjm.
    rewrite A1, (aft_congr _ _ _ _ _ A2). exact Hjm. }
  assert (Hes_live : forall m, m ∈ es -> m ∉ D1).
  { intros m Hm Hd. destruct (He m Hm) as [c Hc]. pose proof (Cnt m c Hc Hd). pose proof (count_runs_in _ _ (H2 m Hm)). lia. }
  assert (Hns_live : forall m, m ∈ ns -> m ∉ D1 /\ is_Some (Semantics.program_effects p !! m)).
  { intros m Hm. destruct (proj1 (Hx m) Hm) as [[c Hc] Hmv0]. split; [|by eexists]. intros Hd. unfold Semantics.effect_entry in Hmv0.
    destruct (En1 m c Hc) as [[_ K]|[K _]]; [rewrite K in Hmv0; simpl in Hmv0; done|rewrite Rd1 in K; done]. }
  assert (Hls : forall m, m ∈ ls <-> m ∈ ns) by (intros m; unfold ls; rewrite (Hord _ ns); done).
  assert (Hmap' : foldl Semantics.settled_step M1 (concat (blkF F <$> es)) = foldl Semantics.settled_step M1 (concat (blkF F <$> ls))).
  { apply foldl_cmp.
    - intros m Hm Hnl. destruct (He m Hm) as [c Hc]. pose proof (Hmv m c Hc (Hes_live m Hm) (fun K => Hnl (proj2 (Hls m) K))) as Heq.
      apply (HJ m c Hc (Hes_live m Hm)); by rewrite Heq.
    - intros m Hnes Hm. pose proof (proj1 (Hls m) Hm) as Hm1. destruct (Hns_live m Hm1) as [Hl [c Hc]]. destruct (Hnr m c Hc Hl Hnes) as [A1 A2]. exact (HJ m c Hc Hl A1 A2). }
  assert (Evs : evs (rs ++ rs1 ++ rs2) = evs rs ++ evs rs1 ++ concat (blkF F <$> ls)).
  { rewrite !evs_app. unfold evs at 3. rewrite Ev2. done. }
  assert (Pjs : project p (g_trace s') = project p (g_trace s) ++ evs rs1 ++ concat (blkF F <$> es)).
  { rewrite Tr, !project_app, Pt, Pj. done. }
  assert (HMfin : settled_map (evs (rs ++ rs1 ++ rs2)) = foldl Semantics.settled_step M1 (concat (blkF F <$> ls))).
  { unfold settled_map. rewrite Evs, !foldl_app. done. }
  exists sigs1, D1, st2, (st1, rs1), rs2, es, ls, (evs rs1). simpl. split_and!; try done.
  - split.
    + exact Ho.
    + exact Hb'.
    + exact Hq'.
    + split_and!; [congruence|congruence|congruence].
    + exact Hset2.
    + rewrite HMfin, <- Hmap'. unfold settled_map. rewrite Pjs, !foldl_app. fold (settled_map (project p (g_trace s))). rewrite Hmap. done.
    + rewrite Pjs, Evs, !host_reads_app, Hrd, !host_reads_blocks. done.
    + intros n c Hc Hn. rewrite HMfin, foldl_blocks. destruct (decide (n ∈ ls)) as [Hin|Hin].
      * rewrite aft_idem. done.
      * pose proof (Hmv n c Hc Hn (fun K => Hin (proj2 (Hls n) K))) as Heq. apply (HJ n c Hc Hn); by unfold F in *; rewrite Heq.
  - exact (runs_in_inputs D1 _ Fe).
Qed.

Lemma sim_base f s r :
  run_top f (erase p []) (init current_flags) = Some (s, r) ->
  exists st rs sigs D es ls,
    r = Ok 0%Z /\ ref_all ord p [] = Some (st, rs) /\ sim_inv s st rs sigs D /\
    project p (g_trace s) = concat (blkF (content p sigs) <$> es) /\ evs rs = concat (blkF (content p sigs) <$> ls).
Proof.
  intros E. pose proof E as E0. destruct (erase_parts p [] f s r E) as (g&s1&r1&s2&->&Es&Hrest).
  destruct (init_host p Hwf Hmb Hro g s1 r1 Es) as (->&Tr1&B1&Ho1).
  destruct Hrest as (Ef&Ed). rewrite B1 in Ef. simpl in Ef.
  destruct (host_flush p Hwf Hmb Hro g _ _ _ s2 tt Ho1 eq_refl Ef) as (Ho&Hb&Hq&_&t&fr&Tt&Ft&_).
  destruct g as [|g]; [done|]. simpl in Ed. cbn [exec] in Ed. unfold on_value in Ed. simpl in Ed. unfold m_return in Ed. injection Ed as <- <-.
  set (sigs := Semantics.program_signals p). set (F := content p sigs). fold sigs in Ho.
  pose proof (sim_converged s2 sigs ∅ Ho Hq (ex_intro _ _ (ex_intro _ _ (ex_intro _ _ E0)))) as Hcv.
  assert (Hnew : forall n c, Semantics.program_effects p !! n = Some c ->
     body_eval (Semantics.memo_value p ∅ sigs) [] c = Some (F n).1 /\ body_logs (Semantics.memo_value p ∅ sigs) [] c = Some (F n).2 /\
     exists pre, trace_view (S n) (g_trace s2) = pre ++ EvRun (S n) :: (EvLog (S n) <$> (F n).2) ++ fail_evs (S n) (F n).1).
  { intros n c Hc. destruct (converged_content p Hwf Hmb Hro sigs ∅ s2 n c Ho Hcv Hc ltac:(set_solver)) as (r0&l0&pre&Hcn&E1&E2&V). unfold F. rewrite Hcn. split_and!; [exact E1|exact E2|by exists pre]. }
  set (ls0 := ord 0%nat (Semantics.effect_ids p)).
  assert (Hls0 : forall m, m ∈ ls0 <-> is_Some (Semantics.program_effects p !! m)).
  { intros m. unfold ls0. rewrite (Hord _ _). exact (effect_ids_in p m). }
  destruct (ref_round p Hro F ls0 (Semantics.reference_start p)) as (st1&rs1&E1&S1&Fz1&D1&_&In1&Out1&Ev1).
  { intros n Hn. destruct (proj1 (Hls0 n) Hn) as [c Hc]. exists c. destruct (Hnew n c Hc) as (A1&A2&_). split_and!; [done|exact A1|exact A2|done]. }
  simpl in S1, Fz1, D1.
  assert (Hpen : ref_pending p F (Semantics.next_round st1)).
  { intros n c Hc. right. simpl. rewrite D1, S1, Fz1. destruct (Hnew n c Hc) as (A1&A2&_). split_and!; [set_solver|exact A1|exact A2|].
    exists (Semantics.memo_value p ∅ sigs), (F n).1. split; [exact A1|]. apply (In1 n c); [apply Hls0; by eexists|done]. }
  destruct (ref_settle p Hro ord F _ Hord Hpen) as (ns&st2&rs2&Es2&S2&Fz2&D2&Hset2&_&Hx&Ev2).
  assert (Hns : ns = []).
  { destruct ns as [|y ns]; [done|]. exfalso. destruct (proj1 (Hx y) (list_elem_of_here _ _)) as [[c Hc] Hmv].
    change (moved_by (Semantics.memo_value p (Semantics.rs_frozen st1) (Semantics.rs_signals st1)) (default ([], []) (Semantics.rs_effects st1 !! y)).1 = Some true) in Hmv.
    rewrite Fz1, (In1 y c ltac:(apply Hls0; by eexists) Hc), S1 in Hmv.
    change (moved_by (Semantics.memo_value p ∅ sigs) (tracked (Semantics.memo_value p ∅ sigs) [] c) = Some true) in Hmv.
    destruct (Hnew y c Hc) as (A1&_). rewrite (moved_self _ _ _ _ A1) in Hmv. done. }
  subst ns. assert (Hrnil : ord (Semantics.rs_round (Semantics.next_round st1)) [] = []) by (apply Permutation_nil; symmetry; apply Hord). rewrite Hrnil in Ev2. simpl in Ev2.
  destruct (flush_project p Hwf Hmb Hro sigs ∅ s2 t fr Ho Ft) as (es&Pt&He&H2&H3).
  assert (Trs : g_trace s2 = t).
  { rewrite Tt. simpl. rewrite Tr1. done. }
  assert (HMfin : settled_map (evs (rs1 ++ rs2 ++ [])) = foldl Semantics.settled_step ∅ (concat (blkF F <$> ls0))).
  { unfold settled_map. rewrite !evs_app. unfold evs at 2. rewrite Ev2. simpl. unfold evs at 1. rewrite Ev1. rewrite !app_nil_r. done. }
  assert (Evs : evs (rs1 ++ rs2 ++ []) = concat (blkF F <$> ls0)).
  { rewrite !evs_app. unfold evs at 2. rewrite Ev2. simpl. unfold evs at 1. rewrite Ev1. by rewrite !app_nil_r. }
  assert (Hmp : foldl Semantics.settled_step ∅ (concat (blkF F <$> es)) = foldl Semantics.settled_step ∅ (concat (blkF F <$> ls0))).
  { apply foldl_cmp.
    - intros m Hm Hnl. exfalso. apply Hnl. apply Hls0. exact (He m Hm).
    - intros m Hnes Hm. exfalso. destruct (proj1 (Hls0 m) Hm) as [c Hc]. destruct (Hnew m c Hc) as (_&_&pre&V).
      destruct (effect_node p Hwf s2 m c (ho_tbl _ _ _ _ Ho) Hc) as (d&Hd&Hk&_).
      rewrite Trs, (flush_view s2 t fr m d Ft Hd Hk (H3 m Hnes)) in V. destruct pre; done. }
  assert (Hpj : project p (g_trace s2) = concat (blkF F <$> es)) by (rewrite Trs; exact Pt).
  exists st2, (rs1 ++ rs2 ++ []), sigs, ∅, es, ls0. split_and!; try done.
  - unfold ref_all. fold ls0. rewrite E1. simpl. rewrite Es2. simpl. reflexivity.
  - split.
    + exact Ho.
    + exact Hb.
    + exact Hq.
    + split_and!; [rewrite S2; simpl; exact S1|rewrite Fz2; simpl; exact Fz1|rewrite D2; simpl; exact D1].
    + exact Hset2.
    + rewrite HMfin, <- Hmp, Hpj. done.
    + rewrite Hpj, Evs, !host_reads_blocks. done.
    + intros n c Hc _. rewrite Evs. unfold settled_map. rewrite foldl_blocks, decide_True by (apply Hls0; by eexists). rewrite aft_idem. done.
Qed.

(** The engine's and the reference's runs agree at every prefix up to [h]. *)
Definition runs_agree (f : nat) (h : Semantics.history) : Prop :=
  forall h0 s0 r0 st0 rs0, h0 `prefix_of` h -> run_top f (erase p h0) (init current_flags) = Some (s0, r0) ->
    ref_all ord p h0 = Some (st0, rs0) -> runs_in (project p (g_trace s0)) = runs_in (evs rs0).

Lemma sim_all h :
  forall f s r, (forall b n, b ∈ h -> Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None) ->
  run_top f (erase p h) (init current_flags) = Some (s, r) ->
  exists st rs sigs D, r = Ok 0%Z /\ ref_all ord p h = Some (st, rs) /\ sim_inv s st rs sigs D /\
    (runs_agree f h -> project p (g_trace s) = evs rs).
Proof.
  induction h as [|b h IH] using rev_ind; intros f s r Hdm E.
  - destruct (sim_base f s r E) as (st&rs&sigs&D&es&ls&->&Ra&Si&Pj&Ev). exists st, rs, sigs, D. split_and!; try done.
    intros Hag. pose proof (Hag [] s _ st rs (prefix_nil _) E Ra) as Hrun. rewrite Pj, Ev, !runs_in_blocks in Hrun. subst ls. rewrite Pj, Ev. done.
  - pose proof E as E0. destruct (erase_parts p _ f s r E) as (g&s1&r1&s2&->&Es&Hrest).
    destruct (init_host p Hwf Hmb Hro g s1 r1 Es) as (->&_&_&_). destruct Hrest as (Ef&Eh).
    destruct (history_split p b h _ _ _ _ Eh) as (s3&r3&Ep&Hor).
    pose proof (erase_build p h g s1 s2 s3 r3 _ Es Ef Ep) as Eb.
    assert (Hdm0 : forall b0 n, b0 ∈ h -> Semantics.RDispose n ∈ b0 -> Semantics.program_memos p !! n = None).
    { intros b0 n Hb0. apply Hdm. apply elem_of_app. by left. }
    destruct (IH (S g) s3 r3 Hdm0 Eb) as (st&rs&sigs&D&->&Ra&Si&Fx).
    destruct Hor as [[y Hy]|[_ (g'&Eg)]]; [done|].
    destruct (sim_step g' b sigs D s3 s r st rs Si) as (sigs'&D'&st'&rs0&rs1&es&ls&pre&->&Ab&Es1&Si'&Pj&Ev&Rp).
    + intros n. apply Hdm. apply elem_of_app. right. left.
    + exact Eg.
    + exact (ex_intro _ _ (ex_intro _ _ (ex_intro _ _ E0))).
    + exact (ex_intro _ _ (ex_intro _ _ (ex_intro _ _ Eb))).
    + exists st', (rs ++ snd rs0 ++ rs1), sigs', D'. split_and!; try done.
      * exact (ref_all_snoc ord p h b st rs _ _ _ _ Ra Ab Es1).
      * intros Hag.
        assert (Hag0 : runs_agree (S g) h).
        { intros h0 s0 r0 st0 rs4 Hpf. apply Hag. destruct Hpf as [k ->]. exists (k ++ [b]). by rewrite <- app_assoc. }
        pose proof (Fx Hag0) as Heq.
        pose proof (Hag (h ++ [b]) s _ st' _ (pfx_refl _) E0 (ref_all_snoc ord p h b st rs _ _ _ _ Ra Ab Es1)) as Hrun.
        rewrite Pj, Ev, Heq, !runs_in_app, Rp, !runs_in_blocks in Hrun. simpl in Hrun. apply app_inv_head in Hrun. subst ls. rewrite Pj, Ev, Heq. done.
Qed.

(** A program's prefix runs at the same fuel, with a trace extended by the rest. *)
Lemma prefix_run k :
  forall h f s r, (forall b n, b ∈ h ++ k -> Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None) ->
  run_top f (erase p (h ++ k)) (init current_flags) = Some (s, r) ->
  exists s0 r0, run_top f (erase p h) (init current_flags) = Some (s0, r0) /\ g_trace s0 `prefix_of` g_trace s.
Proof.
  induction k as [|b k IH] using rev_ind; intros h f s r Hdm E.
  - rewrite app_nil_r in E. exists s, r. split; [done|apply pfx_refl].
  - rewrite app_assoc in E. pose proof E as E0. destruct (erase_parts p _ f s r E) as (g&s1&r1&s2&->&Es&Hrest).
    destruct (init_host p Hwf Hmb Hro g s1 r1 Es) as (->&_&_&_). destruct Hrest as (Ef&Eh).
    destruct (history_split p b (h ++ k) _ _ _ _ Eh) as (s3&r3&Ep&Hor).
    pose proof (erase_build p (h ++ k) g s1 s2 s3 r3 _ Es Ef Ep) as Eb.
    assert (Hdm0 : forall b0 n, b0 ∈ h ++ k -> Semantics.RDispose n ∈ b0 -> Semantics.program_memos p !! n = None).
    { intros b0 n Hb0. apply Hdm. rewrite app_assoc. apply elem_of_app. by left. }
    destruct (IH h (S g) s3 r3 Hdm0 Eb) as (s0&r0&E3&Pf).
    destruct (sim_all (h ++ k) (S g) s3 r3 Hdm0 Eb) as (st&rs&sigs&D&->&_&Si&_).
    destruct Hor as [[y Hy]|[_ (g'&Eg)]]; [done|].
    set (Bs := fun _ : node_id => Semantics.memo_value p ∅ sigs).
    assert (Hen : ref_entries p Bs st).
    { pose proof (si_ref _ _ _ _ _ Si) as (Rs&Rf&_). intros n c Hc. destruct (si_set _ _ _ _ _ Si n c Hc) as [K|[K1 K2]]; [by left|right]. rewrite Rs, Rf in K2. done. }
    assert (Hdm1 : forall n, Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None).
    { intros n. apply Hdm. rewrite app_assoc. apply elem_of_app. right. left. }
    destruct (host_step p Hwf Hmb Hro g' b sigs D s3 s r st Bs (si_host _ _ _ _ _ Si) (si_bat _ _ _ _ _ Si) (si_ref _ _ _ _ _ Si) Hen Hdm1 Eg)
      as (_&_&_&_&tb&t&_&_&_&_&_&_&_&_&Tr&_).
    exists s0, r0. split; [done|]. destruct Pf as [x Hx]. exists (x ++ tb ++ t). rewrite Tr, Hx, <- app_assoc. done.
Qed.

End Sim.

(** ** The refinements *)

(** [Semantics.refines] over the programs and histories satisfying [P]. *)
Definition refines_on (P : Semantics.program -> Semantics.history -> Prop) (O : Semantics.observation)
    (engine : Semantics.program -> Semantics.history -> option (list event)) : Prop :=
  forall p h t, Semantics.well_formed_program p -> P p h -> engine p h = Some t ->
    exists ord, Semantics.is_effect_order ord /\
      forall h', h' `prefix_of` h ->
        exists te fuel tr, engine p h' = Some te /\ te `prefix_of` t /\
          Semantics.reference_trace fuel ord p h' = Some tr /\ Semantics.observe O te = Semantics.observe O tr.

Lemma project_prefix p a b : a `prefix_of` b -> project p a `prefix_of` project p b.
Proof. intros [x ->]. exists (project p x). apply project_app. Qed.

(**md
**Weak refinement: on a refinable program, the engine's settled view is the reference's, at every batch prefix.**

For each fuel, each trace `engine_of` gives on a refinable program extends to each batch prefix of the history, and the prefix's trace has the `observe_settled` of the reference's trace at that prefix.

Premises, beyond `well_formed_program`, each named by `refinable`:

- `memos_below`: node ids are a creation order for memos. Every read of a memo's body, on every path a stuck path included, reaches a lower id. `well_formed_program` asks for some rank over the paths that return. This is the proof's reach: no counterexample is known for a memo reading a higher id, and `erase` cannot renumber a computed target.
- `effects_read_only`: an effect's body holds no write, cleanup or untracked read. Effect cleanups and child effects are excluded by the owner's choice, with no proof attempted. Effect writes are excluded pending the ruling on `effect_resolves_over_moved_source`. An untracked read rests on a counterexample: [`redundant_run_reads_untracked`](Ranvier.Pure.Witnesses.html#redundant_run_reads_untracked).
- `no_memo_disposal`: the history disposes no memo. This rests on the two counterexamples [`disposed_unread_memo_value`](Ranvier.Pure.Witnesses.html#disposed_unread_memo_value) and [`write_then_dispose_memo_current_value`](Ranvier.Pure.Witnesses.html#write_then_dispose_memo_current_value), both of which reproduce on the F#, Awaiting ruling. It also excludes a memo disposed where its stored value is current, such as `host_read_then_dispose_memo`.

`erase` gives every memo `Pure` and `CmpEq`. A default-policy memo returning a new reference value each run, or `nan`, is `CmpNever` in the model: this theorem does not cover it, and `cmp_never_passes_source` under Confirmed in `BUGS.md` shows it losing updates.

The unconditional statement is the Open `weak_refinement_statement`. `engine_answers` is a separate obligation: see `engine_of_answers_statement`.
*)
Theorem weak_refinement_except k :
  refines_on refinable Semantics.observe_settled (engine_of current_flags k).
Proof.
  intros p h t Hwf (Hmb&Hro&Hnd) He.
  set (ord := fun (_ : nat) (l : list node_id) => l).
  assert (Hord : Semantics.is_effect_order ord) by (intros i l; done).
  exists ord. split; [done|]. intros h' Hp.
  unfold engine_of, trace_of in He. destruct (run_top k (erase p h) (init current_flags)) as [[s r]|] eqn:E; [|done]. simpl in He. injection He as <-.
  destruct Hp as [k0 ->].
  destruct (prefix_run p Hwf Hmb Hro ord Hord k0 h' k s r Hnd E) as (s0&r0&E0&Pf).
  assert (Hnd0 : forall b n, b ∈ h' -> Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None).
  { intros b n Hb. apply Hnd. apply elem_of_app. by left. }
  destruct (sim_all p Hwf Hmb Hro ord Hord h' k s0 r0 Hnd0 E0) as (st&rs&sigs&D&_&Ra&Si&_).
  exists (project p (g_trace s0)), 1%nat, (evs rs). split_and!.
  - unfold engine_of, trace_of. rewrite E0. done.
  - exact (project_prefix p _ _ Pf).
  - exact (ref_all_trace ord p h' st rs Ra).
  - simpl. f_equal; [exact (si_map _ _ _ _ _ _ Si)|exact (si_reads _ _ _ _ _ _ Si)].
Qed.

(**md
**Exact refinement, where the engine and the reference run the same effects in the same order.**

On a refinable program, with an order under which every batch prefix has the same sequence of `EvRun`s in the engine's trace and in the reference's, the two traces are equal at every prefix.

With equal `EvRun` sequences, logs, failures, disposals and host reads agree event for event. The premise `runs_agree` excludes a redundant run (`batch_restored_value_reruns`, S07, and `diamond_marks_running_effect`, S15), a missed run and a reordering. No theorem derives `runs_agree` from the absence of redundant runs: see `runs_agree_bridge` in `BUGS.md`. The unconditional statement is the Open `exact_refinement_statement`.
*)
Theorem exact_refinement_except k p h t ord :
  Semantics.well_formed_program p -> refinable p h -> Semantics.is_effect_order ord ->
  engine_of current_flags k p h = Some t -> runs_agree p ord k h ->
  forall h', h' `prefix_of` h ->
    exists te tr, engine_of current_flags k p h' = Some te /\ te `prefix_of` t /\
      Semantics.reference_trace 1 ord p h' = Some tr /\ te = tr.
Proof.
  intros Hwf (Hmb&Hro&Hnd) Hord He Hag h' Hp.
  unfold engine_of, trace_of in He. destruct (run_top k (erase p h) (init current_flags)) as [[s r]|] eqn:E; [|done]. simpl in He. injection He as <-.
  pose proof Hp as [k0 Hk0]. subst h.
  destruct (prefix_run p Hwf Hmb Hro ord Hord k0 h' k s r Hnd E) as (s0&r0&E0&Pf).
  assert (Hnd0 : forall b n, b ∈ h' -> Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None).
  { intros b n Hb. apply Hnd. apply elem_of_app. by left. }
  destruct (sim_all p Hwf Hmb Hro ord Hord h' k s0 r0 Hnd0 E0) as (st&rs&sigs&D&_&Ra&_&Fx).
  exists (project p (g_trace s0)), (evs rs). split_and!.
  - unfold engine_of, trace_of. rewrite E0. done.
  - exact (project_prefix p _ _ Pf).
  - exact (ref_all_trace ord p h' st rs Ra).
  - apply Fx. intros h0 s1 r1 st1 rs1 [x ->]. apply Hag. exists (x ++ k0). by rewrite <- app_assoc.
Qed.

(**md
**A refinable program meets the premises of `convergence` at every batch.**

This is the link between `well_formed_program` and `well_behaved_program`, `effects_only_read` and `quiescent`. Each gap between them, and what closes it:

- **Rank to id.** `memos_acyclic` gives some rank over the paths that return. `pure_at` asks that reads reach lower ids, since nodes are created in id order. `erase` keeps ids, shifted by one so that node 0 exists, and it cannot renumber a computed target. `memos_below` is the named premise. A memo reading a higher id is outside the theorems, by the proof's reach, with no counterexample known.
- **Stuck paths.** `memo_reads_below` says nothing of a path with no result, and `body_pure` covers every path. `memos_below` asks for every path, so a memo reading a higher id on a stuck path is outside the theorems, conservatively. Such a read has no effect on either side's answer.
- **`EVar 4096`.** `expr_no_prev` accepts it and `expr_plain` rejects it. `rename` closes this gap with no premise: `rv` moves a body's variables off `prev_var`, so a renamed body's expressions are plain ([`plain_rename`](#plain_rename)) and the engine's binding of the previous value is out of reach of every body.
- **`CmpEq`.** `erase` chooses it for every memo. A default-policy memo returning a new reference value each run, or `nan`, is `CmpNever` in the model, so neither this theorem nor the refinements cover such a memo. `cmp_never_passes_source` shows that memo losing updates.
- **Memo logs.** `well_behaved_memo` allows `CLog`, and the reference discards a memo's logs. `engine_of` projects them out of the trace, which is what the reference's semantics asks for, so the refinements say nothing about how often a memo's body runs.
- **Effects.** `well_behaved_effect` allows writes, cleanups and untracked reads. `effects_read_only` rejects them, as the docs of [`weak_refinement_except`](#weak_refinement_except) say.
*)
Theorem refinable_meets_convergence p h f s r :
  Semantics.well_formed_program p -> refinable p h -> run_top f (erase p h) (init current_flags) = Some (s, r) ->
  well_behaved_program s /\ effects_only_read s /\ quiescent s.
Proof.
  intros Hwf (Hmb&Hro&Hnd) E.
  destruct (sim_all p Hwf Hmb Hro (fun _ l => l) (fun _ _ => reflexivity _) h f s r Hnd E) as (st&rs&sigs&D&_&_&Si&_).
  pose proof (si_host _ _ _ _ _ _ Si) as Hh. destruct (tbl_good' p Hwf Hmb Hro s (ho_tbl _ _ _ _ Hh)) as [[A B] _].
  split_and!; [exact A|exact B|]. split; [exact (si_quiet _ _ _ _ _ _ Si)|exact (ho_run _ _ _ _ Hh)].
Qed.

(** ** The order of a flush *)

Lemma sublist_in (x : node_id) (l q : list node_id) : l `sublist_of` q -> x ∈ l -> x ∈ q.
Proof.
  induction 1 as [|y l q _ IH|y l q _ IH]; intros H.
  - by apply not_elem_of_nil in H.
  - apply elem_of_cons in H as [->|H]; [left|right; by apply IH].
  - right. by apply IH.
Qed.

Lemma sublist_pred (l q : list node_id) : l `sublist_of` q -> pred <$> l `sublist_of` pred <$> q.
Proof.
  induction 1 as [|y l q _ IH|y l q _ IH]; simpl.
  - apply sublist_nil_l.
  - by apply sublist_skip.
  - by apply sublist_cons.
Qed.

Lemma filter_absent (x : node_id) (l : list node_id) :
  x ∉ l -> filter (fun y => y = x) l = [] /\ filter (fun y => y <> x) l = l.
Proof.
  induction l as [|y l IH]; intros H; [done|].
  apply not_elem_of_cons in H as [Hy Hl]. destruct (IH Hl) as [A B].
  rewrite filter_cons_False, filter_cons_True by done. rewrite A, B. done.
Qed.

Lemma order_by_sublist (q l : list node_id) : NoDup q -> l `sublist_of` q -> order_by q l = l.
Proof.
  revert l. induction q as [|x q IH]; intros l Hn Hs; [done|].
  apply NoDup_cons in Hn as [Hx Hn]. simpl. apply sublist_cons_r in Hs as [Hs|(l'&->&Hs)].
  - destruct (filter_absent x l (fun K => Hx (sublist_in _ _ _ Hs K))) as [A B]. rewrite A, B. exact (IH l Hn Hs).
  - destruct (filter_absent x l' (fun K => Hx (sublist_in _ _ _ Hs K))) as [A B].
    rewrite filter_cons_True, filter_cons_False by (try done; intros K; exact (K eq_refl)). rewrite A, B, (IH l' Hn Hs). done.
Qed.

(** The flush takes only the entries live at its start, once each. Scenario [S15] has an effect requeued during its own run. *)
Definition queue_steady (g : nat) (s : state) : Prop :=
  NoDup (pred <$> flush_queue g s) /\ pred <$> flush_queue g s = live_queue_of s.

(**md
**The `EvRun`s of a flush follow the entries its drain takes.**

For a refinable program, take the state of the engine at a batch's flush, and let `runs` be the flush's `EvRun`s and `Q` the entries the drain takes, in the reference's ids. Then:

- `runs` is a sublist of `Q`: an entry whose effect is clean or disposed is skipped.
- `Q` extends the live queue at the flush's start, in order: `live_queue_of` is the head of `Q`.
- Under `queue_steady`, `runs` is ordered by `engine_order`.

The last conjunct is conditional on its premise: `queue_steady` is a statement about the computed drain, not a class of programs. What this theorem delivers for every refinable program is "runs follow the drain". The last conjunct with no premise is the Open `engine_order_exact_statement`, and [`engine_order_exact_falsified`](Ranvier.Pure.Witnesses.html#engine_order_exact_falsified) refutes it for the queue at a flush's start. That says nothing of [`history_order`](#history_order_is_effect_order), the order that fits a whole history.
*)
Theorem engine_order_exact_except k p h b s0 r0 s1 r1 s2 a t :
  Semantics.well_formed_program p -> refinable p (h ++ [b]) ->
  run_top k (erase p h) (init current_flags) = Some (s0, r0) ->
  exec k [] (foldr (input_cmd p) done_cmd b) (with_batch_depth 1 s0) = Some (s1, r1) ->
  request_flush k (with_batch_depth 0 s1) = Some (s2, a) ->
  g_trace s2 = g_trace s1 ++ t ->
  (pred <$> runs_in t) `sublist_of` (pred <$> flush_queue k (with_batch_depth 0 s1)) /\
  live_queue_of (with_batch_depth 0 s1) `prefix_of` (pred <$> flush_queue k (with_batch_depth 0 s1)) /\
  (queue_steady k (with_batch_depth 0 s1) ->
     pred <$> runs_in t = engine_order (with_batch_depth 0 s1) 0%nat (pred <$> runs_in t)).
Proof.
  intros Hwf (Hmb&Hro&Hnd) E0 Ei Ef Tt.
  assert (Hnd0 : forall b0 n, b0 ∈ h -> Semantics.RDispose n ∈ b0 -> Semantics.program_memos p !! n = None).
  { intros b0 n Hb0. apply Hnd. apply elem_of_app. by left. }
  assert (Hnd1 : forall n, Semantics.RDispose n ∈ b -> Semantics.program_memos p !! n = None).
  { intros n. apply (Hnd b n). apply elem_of_app. right. by left. }
  destruct (sim_all p Hwf Hmb Hro (fun _ l => l) (fun _ _ => reflexivity _) h k s0 r0 Hnd0 E0) as (st&rs&sigs&D&_&_&Si&_).
  destruct Si as [Hh Hb0 _ Hr Hset _ _ _].
  pose proof Hr as Hr0. destruct Hr as (Rs&Rf&Rd).
  set (Bs := fun _ : node_id => Semantics.memo_value p ∅ sigs).
  assert (Hen : ref_entries p Bs st).
  { intros n c Hc. destruct (Hset n c Hc) as [K|[K1 K2]]; [by left|right]. rewrite Rs, Rf in K2. done. }
  assert (Hup : with_batch_depth 1 s0 = with_batch_depth (S (g_batch_depth s0)) s0) by (by rewrite Hb0).
  rewrite Hup in Ei.
  assert (Ho1 : host_ok p sigs D (with_batch_depth (S (g_batch_depth s0)) s0)) by (apply host_batch; [exact Hh|exact (sched_up _ (ho_sched _ _ _ _ Hh))]).
  destruct (host_inputs p Hwf Hmb Hro b k [] sigs D _ s1 r1 st Bs Ho1 ltac:(simpl; lia) binder_nil Hr0 Hen Hnd1 Ei)
    as (sigs2&D2&_&_&_&_&_&Ho2&_&_&B2&_).
  assert (Ho3 : host_ok p sigs2 D2 (with_batch_depth (pred (g_batch_depth s1)) s1)) by (apply host_batch; [exact Ho2|exact (sched_down _ _ (ho_sched _ _ _ _ Ho2) (ho_owed _ _ _ _ Ho2))]).
  assert (Hdn : with_batch_depth (pred (g_batch_depth s1)) s1 = with_batch_depth 0 s1) by (rewrite B2; simpl; by rewrite Hb0).
  rewrite Hdn in Ho3.
  destruct (host_flush p Hwf Hmb Hro k sigs2 D2 _ s2 a Ho3 eq_refl Ef) as (_&_&_&_&t'&fr&Tt'&Ft&Sb&Pf).
  assert (Htt : t' = t).
  { apply (app_inv_head (g_trace s1)). rewrite <- Tt. exact (eq_sym Tt'). }
  subst t'. rewrite (flush_tr_runs _ _ _ Ft).
  set (Q := flush_queue k (with_batch_depth 0 s1)) in *.
  assert (Sb' : pred <$> ((fun x : node_id * outcome * list val => x.1.1) <$> fr) `sublist_of` (pred <$> Q)) by (by apply sublist_pred).
  split_and!.
  - exact Sb'.
  - unfold live_queue_of. destruct Pf as [x Hx]. rewrite Hx, fmap_app. by eexists.
  - intros [Hn He]. unfold engine_order. rewrite <- He. symmetry. exact (order_by_sublist _ _ Hn Sb').
Qed.

(** ** An order over a whole history *)

Lemma filter_split (x : node_id) (l : list node_id) : filter (fun y => y = x) l ++ filter (fun y => y <> x) l ≡ₚ l.
Proof.
  induction l as [|y l IH]; [done|]. destruct (decide (y = x)) as [->|Hne].
  - rewrite filter_cons_True, filter_cons_False by (try done; intros K; exact (K eq_refl)). simpl. by rewrite IH.
  - rewrite filter_cons_False, filter_cons_True by done. by rewrite <- Permutation_middle, IH.
Qed.

Lemma order_by_perm (q l : list node_id) : order_by q l ≡ₚ l.
Proof.
  revert l. induction q as [|x q IH]; intros l; [done|]. simpl. rewrite IH. apply filter_split.
Qed.

(** The engine's runs at a history, in the reference's ids. *)
Definition engine_runs (k : nat) (p : Semantics.program) (h : Semantics.history) : list node_id :=
  runs_in (default [] (engine_of current_flags k p h)).

(** The runs of each batch of [h] in turn, after the runs of [pre]. A batch with no run takes no entry, as a settled run set takes
    no round index in the reference. *)
Fixpoint batch_rounds (k : nat) (p : Semantics.program) (pre h : Semantics.history) : list (list node_id) :=
  match h with
  | [] => []
  | b :: h' =>
    let new := drop (length (engine_runs k p pre)) (engine_runs k p (pre ++ [b])) in
    (if decide (new = []) then [] else [new]) ++ batch_rounds k p (pre ++ [b]) h'
  end.

(** The runs of each round of the history, the initial round first. *)
Definition engine_rounds (k : nat) (p : Semantics.program) (h : Semantics.history) : list (list node_id) :=
  engine_runs k p [] :: batch_rounds k p [] h.

(** Each round's effects in the order of that round's runs in the engine. *)
Definition history_order (k : nat) (p : Semantics.program) (h : Semantics.history) : Semantics.effect_order :=
  fun i l => order_by (default [] (engine_rounds k p h !! i)) l.

(**md
**`history_order` is an effect order, so it can instantiate `exact_refinement_except`.**

Each round's list is a permutation, whatever the engine ran.
*)
Theorem history_order_is_effect_order k p h : Semantics.is_effect_order (history_order k p h).
Proof. intros i l. apply order_by_perm. Qed.

(**md
**Exact refinement under the engine's own order.**

`exact_refinement_except` at `history_order`: on a refinable program, with the engine's rounds agreeing with the reference's under that order at every prefix, the traces are equal. `runs_agree` stays a premise. [`history_order_reached`](Ranvier.Pure.Witnesses.html#history_order_reached) meets it on a program where the order of creation does not.
*)
Corollary exact_refinement_history k p h t :
  Semantics.well_formed_program p -> refinable p h ->
  engine_of current_flags k p h = Some t -> runs_agree p (history_order k p h) k h ->
  forall h', h' `prefix_of` h ->
    exists te tr, engine_of current_flags k p h' = Some te /\ te `prefix_of` t /\
      Semantics.reference_trace 1 (history_order k p h) p h' = Some tr /\ te = tr.
Proof.
  intros Hwf Hr He Hag. exact (exact_refinement_except k p h t _ Hwf Hr (history_order_is_effect_order k p h) He Hag).
Qed.

(** ** What a marking walk does to the queue *)

(** Each entry at or below position [n] of the source's observers is an effect ready to enqueue. *)
Definition walk_ready (s : state) (ids : list node_id) : Prop :=
  forall c, c ∈ ids -> exists d, g_nodes s !! c = Some d /\ n_kind d = KEffect /\ n_disposed d = false /\ n_in_cleanup d = false /\ n_queued d = false.

Lemma walk_appends f src : forall i s, i <= length (observer_items (observers_of (g_edges s) src)) ->
  let ids := take i (observer_items (observers_of (g_edges s) src)).*1 in
  NoDup ids -> walk_ready s ids ->
  exists s', walk_marks (mark_dirty (S f)) src i s = Some s' /\ g_edges s' = g_edges s /\ g_queue s' = g_queue s ++ rev ids.
Proof.
  induction i as [|i IH]; intros s Hi ids Hn Hr.
  - exists s. split_and!; [done|done|]. subst ids. simpl. by rewrite app_nil_r.
  - assert (Hlt : i < length (observer_items (observers_of (g_edges s) src))) by lia.
    destruct (lookup_lt_is_Some_2 _ _ Hlt) as [[c x] Hc].
    assert (Hfst : (observer_items (observers_of (g_edges s) src)).*1 !! i = Some c) by (by rewrite list_lookup_fmap, Hc).
    assert (Hids : ids = take i (observer_items (observers_of (g_edges s) src)).*1 ++ [c]).
    { subst ids. by rewrite (take_S_r _ _ c). }
    rewrite Hids in Hn, Hr. apply NoDup_app in Hn as (Hn1 & Hdis & _).
    destruct (Hr c ltac:(apply elem_of_app; right; left)) as (d & Hd & Hk & Hdi & Hcl & Hq).
    set (s1 := enqueue c (set_node c (node_marked Dirty (n_unannounced d) true d) s)).
    assert (Hm : mark_dirty (S f) c s = Some s1).
    { simpl. rewrite Hd, Hk. unfold effect_marked. rewrite Hdi, Hcl, Hq. reflexivity. }
    assert (Hw : walk_marks (mark_dirty (S f)) src (S i) s = walk_marks (mark_dirty (S f)) src i s1).
    { cbn [walk_marks]. rewrite decide_False by lia. rewrite Hc. cbv iota beta. rewrite Hm. reflexivity. }
    rewrite Hw.
    assert (He1 : g_edges s1 = g_edges s) by reflexivity.
    destruct (IH s1) as (s' & W & E' & Q').
    + rewrite He1. lia.
    + rewrite He1. exact Hn1.
    + rewrite He1. intros y Hy. destruct (Hr y ltac:(apply elem_of_app; by left)) as (e & He & P).
      assert (Hne : y <> c) by (intros ->; exact (Hdis c Hy ltac:(left))).
      exists e. split; [|exact P]. change (<[c := node_marked Dirty (n_unannounced d) true d]> (g_nodes s) !! y = Some e).
      rewrite lookup_insert_ne by done. exact He.
    + exists s'. split; [exact W|]. split; [by rewrite E'|]. rewrite Q'. rewrite He1. rewrite Hids, rev_app_distr.
      change (g_queue s1) with (g_queue s ++ [c]). simpl. rewrite <- app_assoc. reflexivity.
Qed.

(**md
**A write's walk over effect observers queues them from the last observer position to the first.**

Each observer of `src` is an effect that is live, unqueued and outside a cleanup, each once. `notify_dirty` then appends their ids to the queue in the reverse of `observer_items`.

This covers a source's direct observers. A memo among them forwards through `mark_check`, depth first, under a freshness test that reads the state: `BUGS.md` under `queue_order_through_memos` names what is open.
*)
Theorem notify_queues_observers_reversed f src s :
  let ids := (observer_items (observers_of (g_edges s) src)).*1 in
  NoDup ids -> walk_ready s ids ->
  exists s', notify_dirty (S f) src s = Some s' /\ g_queue s' = g_queue s ++ rev ids.
Proof.
  intros ids Hn Hr. unfold notify_dirty, notify_with.
  assert (Ht : take (length (observer_items (observers_of (g_edges s) src))) (observer_items (observers_of (g_edges s) src)).*1 = ids).
  { apply take_ge. subst ids. rewrite length_fmap. lia. }
  destruct (walk_appends f src (length (observer_items (observers_of (g_edges s) src))) s (le_n _)) as (s' & W & _ & Q).
  - cbv zeta. rewrite Ht. exact Hn.
  - rewrite Ht. exact Hr.
  - exists s'. split; [exact W|]. rewrite Q, Ht. reflexivity.
Qed.

(**md
**After a swap-removal, the walk queues the moved observer in the removed one's place.**

`observers_remove_at_swap` moves the last observer `m` into the removed position. On the result, the order of `notify_queues_observers_reversed` has `m` between the observers below that position and those above it, where it was first before.
*)
Corollary notify_after_swap f src s pre (m : node_id) (ms : nat) mid :
  observer_items (observers_of (g_edges s) src) = pre ++ (m, ms) :: mid ->
  NoDup (pre.*1 ++ m :: mid.*1) -> walk_ready s (pre.*1 ++ m :: mid.*1) ->
  exists s', notify_dirty (S f) src s = Some s' /\ g_queue s' = g_queue s ++ rev mid.*1 ++ m :: rev pre.*1.
Proof.
  intros Hi Hn Hr. destruct (notify_queues_observers_reversed f src s) as (s' & W & Q).
  - rewrite Hi, fmap_app. exact Hn.
  - rewrite Hi, fmap_app. exact Hr.
  - exists s'. split; [exact W|]. rewrite Q, Hi, fmap_app. simpl. rewrite rev_app_distr. simpl. rewrite <- app_assoc. reflexivity.
Qed.

(** ** The statements with no premise *)

Definition engine_order_exact_statement : Prop :=
  forall k p h b s0 r0 s1 r1 s2 a t,
    Semantics.well_formed_program p -> refinable p (h ++ [b]) ->
    run_top k (erase p h) (init current_flags) = Some (s0, r0) ->
    exec k [] (foldr (input_cmd p) done_cmd b) (with_batch_depth 1 s0) = Some (s1, r1) ->
    request_flush k (with_batch_depth 0 s1) = Some (s2, a) ->
    g_trace s2 = g_trace s1 ++ t ->
    pred <$> runs_in t = engine_order (with_batch_depth 0 s1) 0%nat (pred <$> runs_in t).

(**md
**Open, and false on the present engine: the `EvRun`s of a flush on a refinable program are in the order of the queue at its start.**

[`engine_order_exact_falsified`](Ranvier.Pure.Witnesses.html#engine_order_exact_falsified) proves `engine_order_exact_statement` false from a requeue of the `S15` class. Use [`engine_order_exact_except`](#engine_order_exact_except).
*)
Theorem engine_order_exact : engine_order_exact_statement.
Proof. Admitted.

Definition weak_refinement_statement : Prop :=
  forall k, Semantics.refines Semantics.observe_settled (engine_of current_flags k).

(**md
**Open, and false on the present engine: every trace of the engine on a well-formed program has the reference's settled view.**

This states what a fix owes, or what the owner's rulings on the reference would have to change, as `weak_refinement_statement`. [`weak_refinement_falsified`](Ranvier.Pure.Witnesses.html#weak_refinement_falsified) proves it false from `disposed_unread_memo_value`. Use [`weak_refinement_except`](#weak_refinement_except) until then.
*)
Theorem weak_refinement : weak_refinement_statement.
Proof. Admitted.

Definition exact_refinement_statement : Prop :=
  forall k, Semantics.refines Semantics.observe_full (engine_of current_flags k).

(**md
**Open, and false on the present engine: every trace of the engine on a well-formed program is the reference's, event for event.**

[`exact_refinement_falsified`](Ranvier.Pure.Witnesses.html#exact_refinement_falsified) proves `exact_refinement_statement` false from `batch_restored_value_reruns` (S07). Use [`exact_refinement_except`](#exact_refinement_except).
*)
Theorem exact_refinement : exact_refinement_statement.
Proof. Admitted.

Definition engine_of_answers_statement : Prop := Semantics.engine_answers (engine_of current_flags).

(**md
**Open, and false on the present engine: the engine answers wherever the reference settles.**

[`engine_of_answers_falsified`](Ranvier.Pure.Witnesses.html#engine_of_answers_falsified) proves `engine_of_answers_statement` false at every fuel, on `restored_source`, whose effect writes.
*)
Theorem engine_of_answers : engine_of_answers_statement.
Proof. Admitted.
