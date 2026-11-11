From stdpp Require Import prelude gmap.
From iris.algebra Require Import auth gmap excl_auth agree numbers.
From iris.algebra.lib Require Import gmap_view.
From iris.base_logic.lib Require Import own.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList.

Open Scope nat_scope.

(** * Ghost state of the heap graph

One ghost name holds four parts: the authoritative edge map, with a fragment per node; the monotone freshness
of each node; the agreement with the pure [state]; and the monotone map from each node to the locations of its
observer set and source list.

Freshness is monotone through a ghost epoch per node. The code of a node is [3 * epoch + rank], where the
rank is [Clean] 0, [Check] 1 or [Dirty] 2. A node's epoch advances by one at a freshness fall, a step that
lowers its rank, and is unchanged at every other step ([epoch_rule]). The code only grows, so a lower bound
taken at one state holds at every later one, and while the epoch is unchanged the bound is a lower bound on
the freshness itself ([is_graph_fresh_epoch]).

The graph value is a four-cell heap block holding the resolver closure (field 0), the node table (field 1),
the effect queue and its bounds (field 2), and the owner table with the next owner id and control state
(field 3). A new node extends the resolver by [resolver_extend] and the node table by [table_set], and its
locations join the monotone location map, so a location read at one state is the node's location at every
later one ([graph_loc_agree]).

The five heap-tier obligations are discharged as follows:

1. The node fields, effect queue, owners and control state are joined into [is_graph_at].
2. [is_graph_at] is sealed ([Global Opaque is_graph_at]), with [is_graph_open] and [graph_add_node_spec]
   the only lemmas that change freshness codes, so the epoch protocol holds by construction.
3. Every freshness write steps the ghost state via [epoch_next] ([graph_set_node_spec]).
4. The cursor-indexed walk specification ([walk_cursor_spec] in [Heap/ObserverSet.v]) gives each visit's
   place in the order for [walk_marks].
5. A freshness lower bound on an absent id is excluded by [is_graph_fresh_dom]. *)

Definition edge_entry : Type := observer_set * source_list.

(**md
The entry of node `n` in the edge world `w`: its observer set and its source list.
*)
Definition edge_entry_of (w : EdgeWorld) (n : node_id) : edge_entry := (observers_of w n, sources_of w n).

Definition graphUR : ucmra :=
  prodUR (gmap_viewUR node_id (agreeR (leibnizO edge_entry)))
         (prodUR (authUR (gmapUR node_id max_natR))
                 (prodUR (excl_authUR (leibnizO State.state))
                         (authUR (gmapUR node_id (agreeR (leibnizO (loc * loc))))))).

(**md
The ghost state of a heap graph: the authoritative edge map, monotone freshness, the agreement with the pure
state and the monotone location map.
*)
Class graphG Σ := GraphG { graph_inG :: inG Σ graphUR }.

Definition graphΣ : gFunctors := #[GFunctor graphUR].

Global Instance subG_graphΣ {Σ} : subG graphΣ Σ -> graphG Σ.
Proof. solve_inG. Qed.

(**md
The edge entry of each node of `tbl`: its observer set and source list in `w`.
*)
Definition edge_map (tbl : gmap node_id (loc * loc)) (w : EdgeWorld) : gmap node_id edge_entry :=
  map_imap (fun n _ => Some (edge_entry_of w n)) tbl.

Definition locs_ra (tbl : gmap node_id (loc * loc)) : gmap node_id (agreeR (leibnizO (loc * loc))) :=
  to_agree <$> tbl.

(**md
The freshness code of a node at epoch `e` with freshness `f`.
*)
Definition fresh_code (e : nat) (f : freshness) : nat := 3 * e + freshness_rank f.

(** The epoch of node [n] in [ep], 0 when absent. *)
Definition epoch (ep : gmap node_id nat) (n : node_id) : nat := default 0 (ep !! n).

(**md
The freshness code of each node of `nodes`, with the epochs `ep`.
*)
Definition fresh_map (ep : gmap node_id nat) (nodes : gmap node_id node) : gmap node_id max_nat :=
  map_imap (fun n d => Some (MaxNat (fresh_code (epoch ep n) (n_freshness d)))) nodes.

(**md
The freshness of `d'` is below that of `d`: a freshness fall.
*)
Definition fell (d d' : node) : bool :=
  bool_decide (freshness_rank (n_freshness d') < freshness_rank (n_freshness d)).

(**md
The epoch protocol, from the nodes `m` at the epochs `ep` to the nodes `m'` at the epochs `ep'`: each node of
both maps advances its epoch by one when its freshness falls, and keeps it otherwise.

- [m:] the nodes before the step.
- [m':] the nodes after it.
- [ep:] the epochs before the step.
- [ep':] the epochs after it.
*)
Definition epoch_rule (m m' : gmap node_id node) (ep ep' : gmap node_id nat) : Prop :=
  forall n d d', m !! n = Some d -> m' !! n = Some d' ->
    epoch ep' n = if fell d d' then S (epoch ep n) else epoch ep n.

(**md
The epochs after a step from `m` to `m'`, advancing by one at every node whose freshness fell.
*)
Definition epoch_next (m m' : gmap node_id node) (ep : gmap node_id nat) : gmap node_id nat :=
  map_imap (fun n d' =>
    match m !! n with
    | Some d => Some (if fell d d' then S (epoch ep n) else epoch ep n)
    | None => ep !! n
    end) m'.

(**md
**`epoch_next` satisfies the epoch protocol for any node map transition.**

- [m:] the nodes before the step.
- [m':] the nodes after it.
- [ep:] the epochs before the step.
*)
Lemma epoch_next_rule m m' ep : epoch_rule m m' ep (epoch_next m m' ep).
Proof.
  intros n d d' Hd Hd'. rewrite /epoch /epoch_next map_lookup_imap Hd' /= Hd //.
Qed.

(**md
**A freshness code orders epoch first, then freshness.**

- [e:] the epoch of the first code.
- [f:] the freshness of the first code.
- [e':] the epoch of the second code.
- [f':] the freshness of the second code.
*)
Lemma fresh_code_le e f e' f' :
  fresh_code e f <= fresh_code e' f' <-> e < e' \/ (e = e' /\ freshness_le f f').
Proof.
  rewrite /fresh_code /freshness_le.
  assert (freshness_rank f < 3 /\ freshness_rank f' < 3) by (destruct f, f'; simpl; lia). lia.
Qed.

(**md
**A step that keeps every node's freshness at or above its own keeps the epochs.**

- [m:] the nodes before the step.
- [m':] the nodes after it.
- [ep:] the epochs.
*)
Lemma epoch_rule_rise m m' ep :
  (forall n d d', m !! n = Some d -> m' !! n = Some d' -> freshness_le (n_freshness d) (n_freshness d')) ->
  epoch_rule m m' ep ep.
Proof.
  intros Hle n d d' Hd Hd'. specialize (Hle n d d' Hd Hd'). rewrite /fell bool_decide_eq_false_2 //.
  unfold freshness_le in Hle. lia.
Qed.

(**md
**Under the epoch protocol every freshness code grows.**

- [m:] the nodes before the step.
- [m':] the nodes after it, a superset of `m`'s.
- [ep:] the epochs before the step.
- [ep':] the epochs after it.
*)
Lemma fresh_map_grows m m' ep ep' :
  epoch_rule m m' ep ep' -> dom m ⊆ dom m' ->
  forall n k, fresh_map ep m !! n = Some (MaxNat k) ->
    exists k', fresh_map ep' m' !! n = Some (MaxNat k') /\ k <= k'.
Proof.
  intros Hr Hdom n k Hk. rewrite /fresh_map map_lookup_imap in Hk.
  destruct (m !! n) as [d|] eqn:Hd; simpl in Hk; [|done]. injection Hk as <-.
  assert (n ∈ dom m') as [d' Hd']%elem_of_dom by (apply Hdom; by apply elem_of_dom_2 in Hd).
  rewrite /fresh_map map_lookup_imap Hd' /=. eexists; split; [reflexivity|].
  rewrite (Hr n d d' Hd Hd') /fresh_code /fell.
  assert (freshness_rank (n_freshness d) < 3) by (destruct (n_freshness d); simpl; lia).
  case_bool_decide; lia.
Qed.

(** ** Fresh owner ids *)

Lemma foldr_max_S_le (l : list nat) (k : nat) :
  foldr (Nat.max ∘ S) 0 l <= k <-> forall y, y ∈ l -> S y <= k.
Proof.
  induction l as [|a l IH]; simpl.
  - split; [intros _ y Hy; by apply not_elem_of_nil in Hy|intros _; lia].
  - rewrite Nat.max_lub_iff IH. split.
    + intros [Ha Hl] y [->|Hy]%elem_of_cons; [done|by apply Hl].
    + intros H. split; [apply H; left|intros y Hy; apply H; by right].
Qed.

Lemma fresh_gset_nat_le (X : gset nat) (k : nat) :
  stdpp.base.fresh X <= k <-> forall y, y ∈ X -> S y <= k.
Proof.
  change (stdpp.base.fresh X) with (foldr (Nat.max ∘ S) 0 (elements X)).
  rewrite foldr_max_S_le. setoid_rewrite elem_of_elements. done.
Qed.

Lemma fresh_insert_gset_nat (o : nat) (X : gset nat) :
  stdpp.base.fresh ({[o]} ∪ X) = Nat.max (S o) (stdpp.base.fresh X).
Proof.
  apply Nat.le_antisymm.
  - rewrite fresh_gset_nat_le. intros y [->%elem_of_singleton|Hy]%elem_of_union.
    + lia.
    + assert (S y <= stdpp.base.fresh X).
      { apply (proj1 (fresh_gset_nat_le X (stdpp.base.fresh X))); done. }
      lia.
  - apply Nat.max_lub.
    + apply (proj1 (fresh_gset_nat_le ({[o]} ∪ X) _)); [done|set_solver].
    + rewrite fresh_gset_nat_le. intros y Hy.
      apply (proj1 (fresh_gset_nat_le ({[o]} ∪ X) _)); [done|set_solver].
Qed.

(** ** Value encodings *)

Definition freshness_val (f : freshness) : val :=
  match f with Clean => #0 | Check => #1 | Dirty => #2 end.

Definition kind_val (k : kind) : val :=
  match k with KSignal => #0 | KMemo => #1 | KEffect => #2 end.

Definition status_val (st : status) : val :=
  match st with StNone => #0 | StPending => #1 | StError => #2 | StUninitialized => #3 end.

Definition scope_mode_val (m : scope_mode) : val :=
  match m with Pure => #0 | Owning => #1 end.

Definition comparer_val (c : comparer) : val :=
  match c with
  | CmpEq => (#0, #0)%V
  | CmpNever => (#1, #0)%V
  | CmpThrows f => (#2, #f)%V
  end.

Definition outcome_val (r : outcome) : val :=
  match r with Ok v => InjLV #v | Fail f => InjRV #f end.

Definition opt_nat_val (x : option nat) : val :=
  match x with None => NONEV | Some n => SOMEV #(Z.of_nat n) end.

Definition opt_z_val (x : option Z) : val :=
  match x with None => NONEV | Some z => SOMEV #z end.

Definition link_val (l : link) : val :=
  (#(Z.of_nat (l_child l)), #(Z.of_nat (l_owner l)), opt_nat_val (l_generation l), #(Z.of_nat (l_acquired l)))%V.

Definition opt_link_val (ml : option link) : val :=
  match ml with None => NONEV | Some l => SOMEV (link_val l) end.

Fixpoint links_val (ls : list link) : val :=
  match ls with
  | [] => NONEV
  | l :: r => SOMEV (link_val l, links_val r)
  end.

Fixpoint zs_val (zs : list Z) : val :=
  match zs with
  | [] => NONEV
  | z :: r => SOMEV (#z, zs_val r)
  end.

Fixpoint env_val (e : env) : val :=
  match e with
  | [] => NONEV
  | (x, z) :: r => SOMEV ((#(Z.of_nat x), #z)%V, env_val r)
  end.

Fixpoint compile_expr (e : Syntax.expr) : val :=
  match e with
  | EConst z => (#0, #z)%V
  | EVar x => (#1, #(Z.of_nat x))%V
  | EPrev => (#2, #0)%V
  | EAdd a b => (#3, compile_expr a, compile_expr b)%V
  | ESub a b => (#4, compile_expr a, compile_expr b)%V
  | EMul a b => (#5, compile_expr a, compile_expr b)%V
  | EDiv a b => (#6, compile_expr a, compile_expr b)%V
  | ELt a b => (#7, compile_expr a, compile_expr b)%V
  | EEq a b => (#8, compile_expr a, compile_expr b)%V
  end.

Fixpoint compile_cmd (c : cmd) : val :=
  match c with
  | CRet e => (#0, compile_expr e)%V
  | CThrow f => (#1, #f)%V
  | CLet x e k => (#2, #(Z.of_nat x), compile_expr e, compile_cmd k)%V
  | CIf e t f => (#3, compile_expr e, compile_cmd t, compile_cmd f)%V
  | CRead x n k => (#4, #(Z.of_nat x), compile_expr n, compile_cmd k)%V
  | CTryRead x ok n k => (#5, #(Z.of_nat x), #(Z.of_nat ok), compile_expr n, compile_cmd k)%V
  | CReadUntracked x n k => (#6, #(Z.of_nat x), compile_expr n, compile_cmd k)%V
  | CWrite n e k => (#7, compile_expr n, compile_expr e, compile_cmd k)%V
  | CSignal x e k => (#8, #(Z.of_nat x), compile_expr e, compile_cmd k)%V
  | CMemo x m q b k => (#9, #(Z.of_nat x), scope_mode_val m, comparer_val q, compile_cmd b, compile_cmd k)%V
  | CEffect x b k => (#10, #(Z.of_nat x), compile_cmd b, compile_cmd k)%V
  | CCleanup b k => (#11, compile_cmd b, compile_cmd k)%V
  | CDispose n k => (#12, compile_expr n, compile_cmd k)%V
  | CBatch b k => (#13, compile_cmd b, compile_cmd k)%V
  | CLog e k => (#14, compile_expr e, compile_cmd k)%V
  end.

Fixpoint cmds_val (bs : list cmd) : val :=
  match bs with
  | [] => NONEV
  | b :: r => SOMEV (compile_cmd b, cmds_val r)
  end.

Fixpoint envs_val (es : list env) : val :=
  match es with
  | [] => NONEV
  | e :: r => SOMEV (env_val e, envs_val r)
  end.

Definition node_val_a (d : node) : val :=
  ((kind_val (n_kind d), #(n_value d)), (opt_z_val (n_failure d), status_val (n_status d)))%V.

Definition node_val_b (d : node) : val :=
  ((freshness_val (n_freshness d), #(n_unannounced d)), (#(Z.of_nat (n_runs d)), #(n_published d)))%V.

Definition node_val_c (d : node) : val :=
  ((#(n_disposed d), #(n_queued d)), (#(n_in_cleanup d), (opt_nat_val (n_scope d), #(n_violated d))))%V.

Definition node_val_d (d : node) : val :=
  ((compile_cmd (n_body d), env_val (n_env d)),
   (scope_mode_val (n_mode d), (comparer_val (n_comparer d), opt_link_val (n_link d))))%V.

Definition node_val (d : node) : val :=
  ((node_val_a d, node_val_b d), (node_val_c d, node_val_d d))%V.

Definition owner_val_a (x : owner_state) : val :=
  ((links_val (o_children x), cmds_val (o_cleanups x)),
   (envs_val (o_cleanup_envs x), list_val (o_cleanup_order x)))%V.

Definition owner_val_b (x : owner_state) : val :=
  ((#(Z.of_nat (o_acquisitions x)), #(Z.of_nat (o_generation x))),
   (#(o_disposed x), (opt_nat_val (o_sink x), (zs_val (o_errors x), #(Z.of_nat (o_host x))))))%V.

Definition owner_val (x : owner_state) : val :=
  (owner_val_a x, owner_val_b x)%V.

Definition ctrl_val_a (s : State.state) : val :=
  ((#(g_flushing s), #(g_flush_owed s)),
   (#(Z.of_nat (g_batch_depth s)), #(Z.of_nat (g_pull_depth s))))%V.

Definition ctrl_val_b (s : State.state) : val :=
  ((opt_nat_val (g_current s), #(g_untracked s)),
   (opt_nat_val (g_current_owner s), #(Z.of_nat (g_next_id s))))%V.

Definition ctrl_val_c (s : State.state) : val :=
  ((list_val (g_running s), list_val (g_checking s)),
   (#(f_unannounced (g_flags s)), #(f_reader_mark (g_flags s))))%V.

Definition ctrl_val (s : State.state) : val :=
  ((ctrl_val_a s, ctrl_val_b s), ctrl_val_c s)%V.

Definition table_empty : val := λ: <>, NONE.

Definition table_set : val :=
  λ: "t" "k" "v", λ: "m", if: "m" = "k" then SOME "v" else "t" "m".

Section ghost.
Context `{!graphG Σ}.

Definition graph_edges_auth (γ : gname) (m : gmap node_id edge_entry) : iProp Σ :=
  own γ ((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1) (to_agree <$> m), ε) : graphUR).

(**md
The exclusive fragment of node `n`'s entry `e` in the edge map of `γ`.
*)
Definition graph_edge (γ : gname) (n : node_id) (e : edge_entry) : iProp Σ :=
  own γ ((gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e), ε) : graphUR).

Definition graph_fresh_auth (γ : gname) (m : gmap node_id max_nat) : iProp Σ :=
  own γ ((ε, (● m, ε)) : graphUR) ∗ own γ ((ε, (◯ m, ε)) : graphUR).

(**md
Node `n`'s freshness code in `γ` is at least `k`.
*)
Definition graph_fresh_lb (γ : gname) (n : node_id) (k : nat) : iProp Σ :=
  own γ ((ε, (◯ {[n := MaxNat k]}, ε)) : graphUR).

Definition graph_state_auth (γ : gname) (s : State.state) : iProp Σ :=
  own γ ((ε, (ε, (●E (s : leibnizO State.state), ε))) : graphUR).

(**md
The client's half of the agreement: the pure state is `s`.
*)
Definition graph_state (γ : gname) (s : State.state) : iProp Σ :=
  own γ ((ε, (ε, (◯E (s : leibnizO State.state), ε))) : graphUR).

Definition graph_locs_auth (γ : gname) (tbl : gmap node_id (loc * loc)) : iProp Σ :=
  own γ ((ε, (ε, (ε, ● locs_ra tbl))) : graphUR) ∗ own γ ((ε, (ε, (ε, ◯ locs_ra tbl))) : graphUR).

(**md
Node `n`'s observer set and source list are at the locations `p`, at every state of `γ`.
*)
Definition graph_loc (γ : gname) (n : node_id) (p : loc * loc) : iProp Σ :=
  own γ ((ε, (ε, (ε, ◯ ({[n := to_agree p]} : gmap node_id (agreeR (leibnizO (loc * loc))))))) : graphUR).

Global Instance graph_fresh_lb_persistent γ n k : Persistent (graph_fresh_lb γ n k).
Proof. apply _. Qed.

Global Instance graph_loc_persistent γ n p : Persistent (graph_loc γ n p).
Proof. apply _. Qed.

Lemma locs_ra_valid tbl : ✓ locs_ra tbl.
Proof. intros i. rewrite /locs_ra lookup_fmap. by destruct (tbl !! i). Qed.

Lemma graph_ghost_alloc (s : State.state) (m : gmap node_id max_nat) (tbl : gmap node_id (loc * loc)) :
  ⊢ |==> ∃ γ, graph_edges_auth γ ∅ ∗ graph_fresh_auth γ m ∗ graph_state_auth γ s ∗ graph_state γ s ∗
    graph_locs_auth γ tbl.
Proof.
  iMod (own_alloc (((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                      (to_agree <$> (∅ : gmap node_id edge_entry)), ε) : graphUR) ⋅
                   (((ε, (● m, ε)) : graphUR) ⋅ (((ε, (◯ m, ε)) : graphUR) ⋅
                    (((ε, (ε, (●E (s : leibnizO State.state), ε))) : graphUR) ⋅
                     (((ε, (ε, (◯E (s : leibnizO State.state), ε))) : graphUR) ⋅
                      (((ε, (ε, (ε, ● locs_ra tbl))) : graphUR) ⋅ ((ε, (ε, (ε, ◯ locs_ra tbl))) : graphUR))))))))
    as (γ) "H".
  { rewrite -!pair_op. split; simpl; [rewrite !right_id; apply gmap_view_auth_valid|]. split; simpl.
    - rewrite !left_id !right_id. apply auth_both_valid_discrete. split; [done|].
      intros i. destruct (m !! i : option max_natR) eqn:E; rewrite E; done.
    - split; simpl; [rewrite !left_id !right_id; apply excl_auth_valid|].
      rewrite !left_id. apply auth_both_valid_discrete. split; [done|]. apply locs_ra_valid. }
  rewrite !own_op. iDestruct "H" as "(H1 & H2 & H3 & H4 & H5 & H6 & H7)". iExists γ. iFrame. done.
Qed.

Lemma graph_edge_alloc γ m n e :
  m !! n = None -> graph_edges_auth γ m ==∗ graph_edges_auth γ (<[n := e]> m) ∗ graph_edge γ n e.
Proof.
  intros Hn. iIntros "H". rewrite /graph_edges_auth /graph_edge.
  iMod (own_update _ _ (((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                           (to_agree <$> <[n := e]> m), ε) : graphUR) ⋅
                        ((gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e), ε) : graphUR))
          with "H") as "[$ $]"; [|done].
  rewrite -pair_op fmap_insert. apply prod_update; simpl.
  - apply gmap_view_alloc; [by rewrite lookup_fmap Hn|done|done].
  - by rewrite left_id.
Qed.

Lemma graph_edges_alloc_all γ (m : gmap node_id edge_entry) :
  graph_edges_auth γ ∅ ==∗ graph_edges_auth γ m ∗ [∗ map] n ↦ e ∈ m, graph_edge γ n e.
Proof.
  induction m as [|n e m Hn IH] using map_ind; iIntros "H".
  - iModIntro. iFrame. by rewrite big_sepM_empty.
  - iMod (IH with "H") as "[H Hm]". iMod (graph_edge_alloc _ _ n e Hn with "H") as "[$ He]".
    iModIntro. rewrite big_sepM_insert //. iFrame.
Qed.

Lemma graph_edge_agree γ m n e : graph_edges_auth γ m -∗ graph_edge γ n e -∗ ⌜m !! n = Some e⌝.
Proof.
  iIntros "Ha Hf". iDestruct (own_valid_2 with "Ha Hf") as %[Hv _].
  simpl in Hv. apply gmap_view_both_valid in Hv as (_ & _ & Hl).
  rewrite lookup_fmap in Hl. apply fmap_Some_equiv in Hl as (e' & Hm & He).
  apply (inj to_agree), leibniz_equiv in He. iPureIntro. subst e'. exact Hm.
Qed.

Lemma graph_edge_update γ m n e e' :
  graph_edges_auth γ m -∗ graph_edge γ n e ==∗ graph_edges_auth γ (<[n := e']> m) ∗ graph_edge γ n e'.
Proof.
  iIntros "Ha Hf". iCombine "Ha Hf" as "H". rewrite /graph_edges_auth /graph_edge.
  iMod (own_update _ _ (((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                           (to_agree <$> <[n := e']> m), ε) : graphUR) ⋅
                        ((gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e'), ε) : graphUR))
          with "H") as "[$ $]"; [|done].
  rewrite -!pair_op fmap_insert. apply prod_update; simpl; [|done].
  by apply gmap_view_replace.
Qed.

(** The fragments of the nodes [X] move from the entries [f] to the entries [f']. *)
Lemma graph_edges_set γ m (X : gset node_id) (f f' : node_id -> edge_entry) :
  graph_edges_auth γ m -∗ ([∗ set] n ∈ X, graph_edge γ n (f n)) ==∗
  ∃ m', ⌜forall n, n ∈ X -> is_Some (m !! n)⌝ ∗
        ⌜forall n, m' !! n = if decide (n ∈ X) then Some (f' n) else m !! n⌝ ∗
        graph_edges_auth γ m' ∗ [∗ set] n ∈ X, graph_edge γ n (f' n).
Proof.
  induction X as [|x Y Hx IH] using set_ind_L; iIntros "Ha Hf".
  - iModIntro. iExists m. iFrame. iSplit; [iPureIntro; set_solver|].
    iSplit; [iPureIntro; intros n; case_decide; [set_solver|done]|]. by iApply big_sepS_empty.
  - rewrite !big_sepS_insert //. iDestruct "Hf" as "[Hx Hf]".
    iMod (IH with "Ha Hf") as (m1) "(%Hdom & %Hm1 & Ha & Hf)".
    iDestruct (graph_edge_agree with "Ha Hx") as %Hl.
    iMod (graph_edge_update _ _ _ _ (f' x) with "Ha Hx") as "[Ha Hx]".
    iModIntro. iExists _. iFrame "Ha". rewrite big_sepS_insert //. iFrame. iPureIntro. split.
    + intros n [->%elem_of_singleton|Hn]%elem_of_union; [|by apply Hdom].
      revert Hl. rewrite Hm1. case_decide; [done|]. intros ->. done.
    + intros n. destruct (decide (n = x)) as [->|Hne].
      * rewrite lookup_insert_eq. case_decide; [done|set_solver].
      * rewrite lookup_insert_ne // Hm1. repeat case_decide; set_solver.
Qed.

Lemma graph_fresh_lb_get γ m n k : m !! n = Some (MaxNat k) -> graph_fresh_auth γ m -∗ graph_fresh_lb γ n k.
Proof.
  iIntros (Hn) "[_ Hf]". iApply (own_mono with "Hf"). apply prod_included. split; [done|].
  apply prod_included. split; [|done]. simpl. apply auth_frag_mono.
  apply singleton_included_l. exists (MaxNat k). rewrite Hn. split; done.
Qed.

Lemma graph_fresh_lb_valid γ m n k :
  graph_fresh_auth γ m -∗ graph_fresh_lb γ n k -∗ ⌜exists k', m !! n = Some (MaxNat k') /\ k <= k'⌝.
Proof.
  iIntros "[Ha _] Hf". iDestruct (own_valid_2 with "Ha Hf") as %[_ [Hv _]]. simpl in Hv.
  apply auth_both_valid_discrete in Hv as [Hin _].
  apply singleton_included_l in Hin as ([k'] & Hl & Hle). apply leibniz_equiv in Hl.
  apply Some_included_total, max_nat_included in Hle. simpl in Hle. iPureIntro. eauto.
Qed.

(** The codes [m] grow to [m']. *)
Local Lemma graph_fresh_grow γ m m' :
  (forall n k, m !! n = Some (MaxNat k) -> exists k', m' !! n = Some (MaxNat k') /\ k <= k') ->
  graph_fresh_auth γ m ==∗ graph_fresh_auth γ m'.
Proof.
  intros Hle. iIntros "[Ha Hf]". iCombine "Ha Hf" as "H".
  iMod (own_update _ _ (((ε, (● m', ε)) : graphUR) ⋅ ((ε, (◯ m', ε)) : graphUR)) with "H") as "[$ $]"; [|done].
  rewrite -!pair_op. apply prod_update; simpl; [done|]. apply prod_update; simpl; [|done].
  assert (Hop : m' ⋅ m = m').
  { apply map_eq. intros i. rewrite lookup_op.
    destruct (m !! i : option max_natR) as [[k]|] eqn:E; rewrite E.
    - destruct (Hle i k E) as (k' & Hk' & Hk).
      rewrite (Hk' : (m' !! i : option max_natR) = Some (MaxNat k')).
      rewrite -Some_op max_nat_op. do 2 f_equal. lia.
    - by rewrite right_id. }
  apply auth_update. rewrite -Hop. apply op_local_update_discrete. intros _. rewrite Hop.
  intros i. destruct (m' !! i : option max_natR) eqn:E; rewrite E; done.
Qed.

Lemma graph_state_agree γ s s' : graph_state_auth γ s -∗ graph_state γ s' -∗ ⌜s = s'⌝.
Proof.
  iIntros "Ha Hf". iDestruct (own_valid_2 with "Ha Hf") as %[_ [_ [Hv _]]]. simpl in Hv.
  by apply excl_auth_agree_L in Hv.
Qed.

Lemma graph_state_update γ s s' :
  graph_state_auth γ s -∗ graph_state γ s ==∗ graph_state_auth γ s' ∗ graph_state γ s'.
Proof.
  iIntros "Ha Hf". iCombine "Ha Hf" as "H".
  iMod (own_update _ _ (((ε, (ε, (●E (s' : leibnizO State.state), ε))) : graphUR) ⋅
                        ((ε, (ε, (◯E (s' : leibnizO State.state), ε))) : graphUR)) with "H") as "[$ $]"; [|done].
  rewrite -!pair_op. apply prod_update; simpl; [done|]. apply prod_update; simpl; [done|].
  apply prod_update; simpl; [|done]. apply excl_auth_update.
Qed.

Lemma graph_loc_get γ tbl n p : tbl !! n = Some p -> graph_locs_auth γ tbl -∗ graph_loc γ n p.
Proof.
  iIntros (Hn) "[_ Hf]". iApply (own_mono with "Hf"). apply prod_included. split; [done|].
  apply prod_included. split; [done|]. apply prod_included. split; [done|]. apply auth_frag_mono.
  apply (proj2 (singleton_included_l (locs_ra tbl) n (to_agree p))).
  exists (to_agree p). rewrite /locs_ra lookup_fmap Hn. split; done.
Qed.

(**md
**A location of the monotone map is the node's location in the table.**

- [γ:] the ghost name.
- [tbl:] the table of the graph's current state.
- [n:] the node.
- [p:] the locations taken from the map.
*)
Lemma graph_loc_agree γ tbl n p : graph_locs_auth γ tbl -∗ graph_loc γ n p -∗ ⌜tbl !! n = Some p⌝.
Proof.
  iIntros "[Ha _] Hf". iDestruct (own_valid_2 with "Ha Hf") as %[_ [_ [_ Hv]]]. simpl in Hv.
  apply auth_both_valid_discrete in Hv as [Hin _].
  apply (singleton_included_l (locs_ra tbl) n (to_agree p)) in Hin as (y & Hl & Hle). rewrite /locs_ra lookup_fmap in Hl.
  apply fmap_Some_equiv in Hl as (p' & Hm & Hy). rewrite Hy in Hle. iPureIntro.
  apply Some_included in Hle as [Hle|Hle].
  - apply (inj to_agree), leibniz_equiv in Hle. subst p'. exact Hm.
  - apply (to_agree_included (A := leibnizO (loc * loc))), leibniz_equiv in Hle. subst p'. exact Hm.
Qed.

Lemma graph_locs_insert γ tbl n p :
  tbl !! n = None -> graph_locs_auth γ tbl ==∗ graph_locs_auth γ (<[n := p]> tbl).
Proof.
  intros Hn. iIntros "[Ha Hf]". iCombine "Ha Hf" as "H".
  iMod (own_update _ _ (((ε, (ε, (ε, ● locs_ra (<[n := p]> tbl)))) : graphUR) ⋅ ((ε, (ε, (ε, ◯ locs_ra (<[n := p]> tbl)))) : graphUR))
          with "H") as "[$ $]"; [|done].
  rewrite -!pair_op. apply prod_update; simpl; [done|]. apply prod_update; simpl; [done|].
  apply prod_update; simpl; [done|]. apply auth_update.
  assert (Hins : locs_ra (<[n := p]> tbl) = <[n := to_agree (p : leibnizO (loc * loc))]> (locs_ra tbl)).
  { apply map_eq. intros i. rewrite /locs_ra lookup_fmap. destruct (decide (i = n)) as [->|Hne].
    - rewrite !lookup_insert_eq. done.
    - rewrite !lookup_insert_ne // lookup_fmap //. }
  rewrite Hins. apply alloc_local_update; [|done]. rewrite /locs_ra lookup_fmap Hn //.
Qed.

End ghost.

(** ** Resolvers and tables over a growing graph *)

(**md
The resolver `res` extended by node `n` at the locations `p`.
*)
Definition resolver_extend : val := λ: "res" "n" "p", λ: "m", if: "m" = "n" then "p" else "res" "m".

Section graph.
Context `{!heapGS Σ, !graphG Σ}.

(**md
**Extending a resolver resolves the new node and every node it resolved.**

- [tbl:] the table resolved by `res`.
- [res:] the resolver.
- [n:] the new node.
- [lo:] the location of the new node's observer set.
- [ls:] the location of the new node's source list.
*)
Lemma resolver_extend_spec tbl (res : val) (n : node_id) (lo ls : loc) :
  {{{ resolver tbl res }}} resolver_extend res #(Z.of_nat n) (#lo, #ls)%V
  {{{ r, RET r; resolver (<[n := (lo, ls)]> tbl) r }}}.
Proof.
  iIntros (Φ) "#Hres HΦ". wp_lam. wp_pures. iModIntro. iApply "HΦ". iModIntro.
  iIntros (m lo' ls' Hm). wp_pures. case_bool_decide as Heq.
  - assert (m = n) as -> by (injection Heq; lia). rewrite lookup_insert_eq in Hm. simplify_eq. by wp_pures.
  - rewrite lookup_insert_ne in Hm; [|intros ->; done]. wp_pures. by iApply "Hres".
Qed.

Definition nodes_table (m : gmap node_id node) (ntab : val) : iProp Σ :=
  □ ∀ (n : node_id),
      WP ntab #(Z.of_nat n) {{ v, ⌜v = match m !! n with Some d => SOMEV (node_val d) | None => NONEV end⌝ }}.

Definition queue_table (q : list node_id) (qtab : val) : iProp Σ :=
  □ ∀ (i : nat), WP qtab #(Z.of_nat i) {{ v, ⌜v = opt_nat_val (q !! i)⌝ }}.

Definition owners_table (m : gmap owner_id owner_state) (otab : val) : iProp Σ :=
  □ ∀ (o : owner_id),
      WP otab #(Z.of_nat o) {{ v, ⌜v = match m !! o with Some x => SOMEV (owner_val x) | None => NONEV end⌝ }}.

Lemma resolver_empty_spec : ⊢ resolver ∅ table_empty.
Proof. iModIntro. iIntros (n lo ls Hn). rewrite lookup_empty in Hn. done. Qed.

Lemma nodes_table_empty : ⊢ nodes_table ∅ table_empty.
Proof. iModIntro. iIntros (n). wp_lam. rewrite lookup_empty. by wp_pures. Qed.

Lemma nodes_table_set m (ntab : val) (n : node_id) (d : node) :
  {{{ nodes_table m ntab }}} table_set ntab #(Z.of_nat n) (node_val d)
  {{{ ntab', RET ntab'; nodes_table (<[n := d]> m) ntab' }}}.
Proof.
  iIntros (Φ) "#Ht HΦ". wp_lam. wp_pures. iModIntro. iApply "HΦ". iModIntro.
  iIntros (k). wp_pures. case_bool_decide as Heq.
  - assert (k = n) as -> by (injection Heq; lia). rewrite lookup_insert_eq. by wp_pures.
  - rewrite lookup_insert_ne; [|intros ->; done]. wp_pures. by iApply "Ht".
Qed.

Lemma queue_table_empty : ⊢ queue_table [] table_empty.
Proof. iModIntro. iIntros (i). wp_lam. by wp_pures. Qed.

Lemma queue_table_snoc q (qtab : val) (n : node_id) :
  {{{ queue_table q qtab }}} table_set qtab #(Z.of_nat (length q)) #(Z.of_nat n)
  {{{ qtab', RET qtab'; queue_table (q ++ [n]) qtab' }}}.
Proof.
  iIntros (Φ) "#Hq HΦ". wp_lam. wp_pures. iModIntro. iApply "HΦ". iModIntro.
  iIntros (i). wp_pures. case_bool_decide as Heq.
  - assert (i = length q) as -> by (injection Heq; lia).
    rewrite lookup_app_r // Nat.sub_diag /=. by wp_pures.
  - assert (Hne : i ≠ length q) by (intros ->; done).
    assert (Hlkp : (q ++ [n]) !! i = q !! i).
    { destruct (decide (i < length q)) as [Hlt|Hge].
      - by apply lookup_app_l.
      - rewrite lookup_app_r; [|lia].
        rewrite (lookup_ge_None_2 [n]); [|simpl; lia].
        symmetry. apply lookup_ge_None_2. lia. }
    rewrite Hlkp. wp_pures. by iApply "Hq".
Qed.

Lemma owners_table_empty : ⊢ owners_table ∅ table_empty.
Proof. iModIntro. iIntros (o). wp_lam. rewrite lookup_empty. by wp_pures. Qed.

Lemma owners_table_set m (otab : val) (o : owner_id) (x : owner_state) :
  {{{ owners_table m otab }}} table_set otab #(Z.of_nat o) (owner_val x)
  {{{ otab', RET otab'; owners_table (<[o := x]> m) otab' }}}.
Proof.
  iIntros (Φ) "#Ho HΦ". wp_lam. wp_pures. iModIntro. iApply "HΦ". iModIntro.
  iIntros (k). wp_pures. case_bool_decide as Heq.
  - assert (k = o) as -> by (injection Heq; lia). rewrite lookup_insert_eq. by wp_pures.
  - rewrite lookup_insert_ne; [|intros ->; done]. wp_pures. by iApply "Ho".
Qed.

Global Opaque table_set.

(**md
The heap tier covers the present engine: the fields of the saturated-write skip hold their initial values.
*)
Definition skip_ok (s : State.state) : Prop := g_skip s = SkipOff /\ g_epoch s = 0 /\ g_stamps s = ∅.

(**md
The graph `g` holds the pure state `s` at the epochs `ep`. `g` is a four-cell block holding the resolver of the
node table, the node table itself, the effect queue with its length and head, and the owner table with the next
fresh owner id and control state; the heap holds the edge world of `s`, which is coherent; the edge map of `γ`
holds each node's entry, the freshness of `γ` holds each node's code at its epoch, `γ` agrees on `s`, and the
location map of `γ` is the table.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [ep:] the epoch of each node.
*)
Definition is_graph_at (γ : gname) (g : val) (s : State.state) (ep : gmap node_id nat) : iProp Σ :=
  ∃ (gl : loc) (res ntab qtab otab : val) (tbl : gmap node_id (loc * loc)),
    ⌜g = #gl⌝ ∗ ⌜skip_ok s⌝ ∗ ⌜edge_match (g_edges s)⌝ ∗ ⌜dom tbl = dom (g_nodes s)⌝ ∗
    gl ↦ res ∗ (gl +ₗ 1) ↦ ntab ∗
    (gl +ₗ 2) ↦ (qtab, #(Z.of_nat (length (g_queue s))), #(Z.of_nat (g_queue_head s)))%V ∗
    (gl +ₗ 3) ↦ (otab, #(Z.of_nat (stdpp.base.fresh (dom (g_owners s)))), ctrl_val s)%V ∗
    resolver tbl res ∗ nodes_table (g_nodes s) ntab ∗ queue_table (g_queue s) qtab ∗
    owners_table (g_owners s) otab ∗ is_world tbl (g_edges s) ∗
    graph_edges_auth γ (edge_map tbl (g_edges s)) ∗ graph_fresh_auth γ (fresh_map ep (g_nodes s)) ∗
    graph_state_auth γ s ∗ graph_locs_auth γ tbl.

(**md
`is_graph_at` at some epochs, bundled with the client state agreement and the edge fragments of all nodes in `s`.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Definition is_graph (γ : gname) (g : val) (s : State.state) : iProp Σ :=
  ∃ ep, is_graph_at γ g s ep ∗ graph_state γ s ∗
    [∗ set] n ∈ dom (g_nodes s), graph_edge γ n (edge_entry_of (g_edges s) n).

(**md
**A graph holding the pure state agrees with any state token.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state the graph holds.
- [s':] the state token's state.
*)
Lemma is_graph_agree γ g s s' : is_graph γ g s -∗ graph_state γ s' -∗ ⌜s = s'⌝.
Proof.
  iIntros "(%ep & (%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Ha & _) & _) Hf".
  iApply (graph_state_agree with "Ha Hf").
Qed.

(**md
**A graph's edge world is coherent.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
*)
Lemma is_graph_edge_match γ g s ep : is_graph_at γ g s ep -∗ ⌜edge_match (g_edges s)⌝.
Proof. iIntros "(%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & $ & _)". Qed.

(**md
**A node's edge fragment is its observer set and source list in the graph's world.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [n:] the node.
- [e:] the fragment's entry.
*)
Lemma is_graph_edge γ g s n e :
  is_graph γ g s -∗ graph_edge γ n e -∗ ⌜e = edge_entry_of (g_edges s) n⌝.
Proof.
  iIntros "(%ep & (%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Ha & _) & _) Hf".
  iDestruct (graph_edge_agree with "Ha Hf") as %Hl.
  rewrite /edge_map map_lookup_imap in Hl. destruct (tbl !! n); simpl in Hl; by simplify_eq.
Qed.

(**md
**Every node of a graph has its locations in the monotone map.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
- [n:] a node of `s`.
*)
Lemma is_graph_loc γ g s ep n :
  n ∈ dom (g_nodes s) -> is_graph_at γ g s ep -∗ ∃ p, graph_loc γ n p.
Proof.
  iIntros (Hn) "(%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & %Hdom & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Hl)".
  rewrite -Hdom in Hn. apply elem_of_dom in Hn as [p Hp]. iExists p. by iApply (graph_loc_get with "Hl").
Qed.

(**md
**A freshness lower bound is only held on an existing node.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
- [n:] the node of the bound.
- [k:] the lower bound.
*)
Lemma is_graph_fresh_dom γ g s ep n k :
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n k -∗ ⌜n ∈ dom (g_nodes s)⌝.
Proof.
  iIntros "(%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Hf & _) Hlb".
  iDestruct (graph_fresh_lb_valid with "Hf Hlb") as %(k' & Hl & _).
  rewrite /fresh_map map_lookup_imap in Hl.
  destruct (g_nodes s !! n) eqn:Hn; [|done].
  iPureIntro. by apply elem_of_dom_2 in Hn.
Qed.

(**md
**A freshness lower bound taken from a graph holds at its later states.**

The bound is the node's code at its epoch, and every later code is at least it: the freshness has risen, or the
epoch has advanced.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
- [n:] the node.
- [d:] the node's record.
*)
Lemma is_graph_fresh_lb γ g s ep n d :
  g_nodes s !! n = Some d ->
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n (fresh_code (epoch ep n) (n_freshness d)).
Proof.
  iIntros (Hn) "(%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Hf & _)".
  iApply (graph_fresh_lb_get with "Hf"). rewrite /fresh_map map_lookup_imap Hn //.
Qed.

(**md
**A freshness lower bound is below the node's current code.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
- [n:] the node.
- [d:] the node's record.
- [k:] the lower bound.
*)
Lemma is_graph_fresh_valid γ g s ep n d k :
  g_nodes s !! n = Some d ->
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n k -∗ ⌜k <= fresh_code (epoch ep n) (n_freshness d)⌝.
Proof.
  iIntros (Hn) "(%gl & %res & %ntab & %qtab & %otab & %tbl & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & _ & Hf & _) Hlb".
  iDestruct (graph_fresh_lb_valid with "Hf Hlb") as %(k' & Hl & Hle).
  rewrite /fresh_map map_lookup_imap Hn /= in Hl. by simplify_eq.
Qed.

(**md
**At an unchanged epoch, a freshness lower bound bounds the freshness.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
- [n:] the node.
- [d:] the node's record.
- [f:] the freshness of the bound.
*)
Lemma is_graph_fresh_epoch γ g s ep n d f :
  g_nodes s !! n = Some d ->
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n (fresh_code (epoch ep n) f) -∗ ⌜freshness_le f (n_freshness d)⌝.
Proof.
  iIntros (Hn) "Hg Hlb". iDestruct (is_graph_fresh_valid with "Hg Hlb") as %Hle; [done|].
  iPureIntro. apply fresh_code_le in Hle as [Hlt|[_ Hle]]; [lia|done].
Qed.

(**md
**A graph opens to its heap, and closes at a later state under the epoch protocol.**

The opened graph gives its four cells, its tables and the edge world in the heap. It closes at a state `s'` with
the same nodes, whose edge world in the heap is coherent, at epochs `ep'` that follow the epoch protocol. The
fragments of the nodes `X` move to their entries at `s'`, and every other node keeps its entry.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [ep:] the epochs.
*)
Lemma is_graph_open γ g s ep :
  is_graph_at γ g s ep -∗
  ∃ (gl : loc) (res ntab qtab otab : val) (tbl : gmap node_id (loc * loc)),
    ⌜g = #gl⌝ ∗ ⌜skip_ok s⌝ ∗ ⌜edge_match (g_edges s)⌝ ∗ ⌜dom tbl = dom (g_nodes s)⌝ ∗
    gl ↦ res ∗ (gl +ₗ 1) ↦ ntab ∗
    (gl +ₗ 2) ↦ (qtab, #(Z.of_nat (length (g_queue s))), #(Z.of_nat (g_queue_head s)))%V ∗
    (gl +ₗ 3) ↦ (otab, #(Z.of_nat (stdpp.base.fresh (dom (g_owners s)))), ctrl_val s)%V ∗
    resolver tbl res ∗ nodes_table (g_nodes s) ntab ∗ queue_table (g_queue s) qtab ∗
    owners_table (g_owners s) otab ∗ is_world tbl (g_edges s) ∗ graph_locs_auth γ tbl ∗
    ∀ (s' : State.state) (ep' : gmap node_id nat) (ntab' qtab' otab' : val) (X : gset node_id)
      (_ : skip_ok s') (_ : edge_match (g_edges s')) (_ : dom (g_nodes s') = dom (g_nodes s))
      (_ : epoch_rule (g_nodes s) (g_nodes s') ep ep')
      (_ : forall n, n ∈ dom (g_nodes s) -> n ∉ X -> edge_entry_of (g_edges s') n = edge_entry_of (g_edges s) n),
      gl ↦ res -∗ (gl +ₗ 1) ↦ ntab' -∗
      (gl +ₗ 2) ↦ (qtab', #(Z.of_nat (length (g_queue s'))), #(Z.of_nat (g_queue_head s')))%V -∗
      (gl +ₗ 3) ↦ (otab', #(Z.of_nat (stdpp.base.fresh (dom (g_owners s')))), ctrl_val s')%V -∗
      nodes_table (g_nodes s') ntab' -∗ queue_table (g_queue s') qtab' -∗
      owners_table (g_owners s') otab' -∗ is_world tbl (g_edges s') -∗
      graph_locs_auth γ tbl -∗ graph_state γ s -∗
      ([∗ set] n ∈ X, graph_edge γ n (edge_entry_of (g_edges s) n)) ==∗
      is_graph_at γ g s' ep' ∗ graph_state γ s' ∗ [∗ set] n ∈ X, graph_edge γ n (edge_entry_of (g_edges s') n).
Proof.
  iIntros "(%gl & %res & %ntab & %qtab & %otab & %tbl & -> & %Hskip & %Hm & %Hdom & Hgl & Hn & Hq & Ho & #Hres & #Hntab & #Hqtab & #Hotab & Hw & He & Hf & Ha & Hl)".
  iExists gl, res, ntab, qtab, otab, tbl.
  iFrame "Hgl Hn Hq Ho Hres Hntab Hqtab Hotab Hw Hl".
  iSplit; [done|]. iSplit; [done|]. iSplit; [done|]. iSplit; [done|].
  iIntros (s' ep' ntab' qtab' otab' X Hskip' Hm' Hdom' Hrule HX) "Hgl Hn Hq Ho #Hntab' #Hqtab' #Hotab' Hw Hl Hs HXf".
  iMod (graph_edges_set _ _ X (edge_entry_of (g_edges s)) (edge_entry_of (g_edges s')) with "He HXf")
    as (m') "(%HXd & %Hm'l & He & HXf)".
  iMod (graph_fresh_grow _ _ (fresh_map ep' (g_nodes s')) with "Hf") as "Hf".
  { apply fresh_map_grows; [done|]. by rewrite Hdom'. }
  iMod (graph_state_update _ _ s' with "Ha Hs") as "[Ha Hs]".
  assert (Hm'e : m' = edge_map tbl (g_edges s')).
  { apply map_eq. intros n. rewrite Hm'l /edge_map !map_lookup_imap. case_decide as Hn.
    - destruct (HXd n Hn) as [e He]. rewrite map_lookup_imap in He.
      destruct (tbl !! n); simpl in *; done.
    - destruct (tbl !! n) as [p|] eqn:Htbl; [|done].
      assert (Hndom : n ∈ dom (g_nodes s)) by (rewrite -Hdom; by apply elem_of_dom_2 in Htbl).
      rewrite (HX n Hndom Hn). done. }
  rewrite Hm'e. iModIntro. iFrame "Hs HXf".
  iExists gl, res, ntab', qtab', otab', tbl.
  iFrame "Hgl Hn Hq Ho Hw He Hf Ha Hl". iFrame "#". iPureIntro.
  split_and!; [done|done|done|by rewrite Hdom'].
Qed.

(**md
**A heap holding an edge world and tables starts a graph.**

- [gl:] the four-cell location of the graph.
- [res:] the resolver of the table.
- [ntab:] the node table closure.
- [qtab:] the queue table closure.
- [otab:] the owner table closure.
- [tbl:] the node location table, whose domain is the nodes of `s`.
- [s:] the pure state, whose skip fields hold their initial values and whose edge world is coherent.
*)
Lemma is_graph_alloc (gl : loc) (res ntab qtab otab : val) tbl (s : State.state) :
  skip_ok s -> edge_match (g_edges s) -> dom tbl = dom (g_nodes s) ->
  gl ↦ res ∗ (gl +ₗ 1) ↦ ntab ∗
  (gl +ₗ 2) ↦ (qtab, #(Z.of_nat (length (g_queue s))), #(Z.of_nat (g_queue_head s)))%V ∗
  (gl +ₗ 3) ↦ (otab, #(Z.of_nat (stdpp.base.fresh (dom (g_owners s)))), ctrl_val s)%V ∗
  resolver tbl res ∗ nodes_table (g_nodes s) ntab ∗ queue_table (g_queue s) qtab ∗
  owners_table (g_owners s) otab ∗ is_world tbl (g_edges s)
  ⊢ |==> ∃ γ, is_graph γ #gl s.
Proof.
  iIntros (Hskip Hm Hdom) "(Hgl & Hn & Hq & Ho & #Hres & #Hntab & #Hqtab & #Hotab & Hw)".
  iMod (graph_ghost_alloc s (fresh_map ∅ (g_nodes s)) tbl) as (γ) "(He & Hf & Ha & Hs & Hl)".
  iMod (graph_edges_alloc_all _ (edge_map tbl (g_edges s)) with "He") as "[He Hfr]".
  iModIntro. iExists γ, ∅. iFrame "Hs".
  iSplitR "Hfr".
  - iExists gl, res, ntab, qtab, otab, tbl. iFrame "#". iFrame. done.
  - assert (Hedom : dom (edge_map tbl (g_edges s)) = dom (g_nodes s)).
    { rewrite -Hdom. apply set_eq. intros n. rewrite !elem_of_dom /edge_map map_lookup_imap.
      destruct (tbl !! n); simpl; split; intros [? ?]; try done; by eexists. }
    rewrite -Hedom -big_sepM_dom. iApply (big_sepM_impl with "Hfr").
    iIntros "!>" (n e Hne) "H".
    revert Hne. rewrite /edge_map map_lookup_imap.
    destruct (tbl !! n); simpl; intros [= <-]; done.
Qed.

(** ** Node allocation *)

(** [graph_add_node_code g n d lo ls] makes the observer set at [lo] and the source list at [ls] node [n]'s,
    and records [d] in the node table. *)
Definition graph_add_node_code : val :=
  λ: "g" "n" "d" "lo" "ls",
    "g" <- resolver_extend (! "g") "n" ("lo", "ls");;
    ("g" +ₗ #1) <- table_set (! ("g" +ₗ #1)) "n" "d".

Lemma fresh_map_insert ep m n d :
  m !! n = None -> fresh_map ep (<[n := d]> m) = <[n := MaxNat (fresh_code (epoch ep n) (n_freshness d))]> (fresh_map ep m).
Proof.
  intros Hn. apply map_eq. intros i. rewrite /fresh_map map_lookup_imap.
  destruct (decide (i = n)) as [->|Hne].
  - by rewrite !lookup_insert_eq.
  - rewrite !lookup_insert_ne // map_lookup_imap //.
Qed.

Lemma edge_map_insert tbl w n p :
  edge_map (<[n := p]> tbl) w = <[n := edge_entry_of w n]> (edge_map tbl w).
Proof.
  apply map_eq. intros i. rewrite /edge_map map_lookup_imap.
  destruct (decide (i = n)) as [->|Hne].
  - by rewrite !lookup_insert_eq.
  - rewrite !lookup_insert_ne // map_lookup_imap //.
Qed.

(**md
**A new node joins the graph with an empty observer set and an empty source list.**

The table, the resolver, the node table, the heap's edge world, the edge map, the freshness map and the location
map gain the node. The node's epoch is its epoch in `ep`. The client receives the node's edge fragment and its
locations.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the allocation.
- [s':] the state after it: `s` with node `n` holding `d`, the same edge world, queue, owners, control fields
  and skip fields at their initial values.
- [ep:] the epochs.
- [n:] the new node.
- [d:] its record.
- [lo:] the location of its observer set.
- [ls:] the location of its source list.
*)
Lemma graph_add_node_spec γ g s s' ep (n : node_id) d (lo ls : loc) :
  g_nodes s !! n = None -> g_nodes s' = <[n := d]> (g_nodes s) -> g_edges s' = g_edges s ->
  g_queue s' = g_queue s -> g_queue_head s' = g_queue_head s -> g_owners s' = g_owners s ->
  ctrl_val s' = ctrl_val s -> skip_ok s' ->
  {{{ is_graph_at γ g s ep ∗ graph_state γ s ∗ is_observers lo empty_observer_set ∗
      is_source_list ls empty_source_list ∅ }}}
    graph_add_node_code g #(Z.of_nat n) (node_val d) #lo #ls
  {{{ RET #(); is_graph_at γ g s' ep ∗ graph_state γ s' ∗
      graph_edge γ n (empty_observer_set, empty_source_list) ∗ graph_loc γ n (lo, ls) }}}.
Proof.
  intros Hn Hnodes Hedges Hq Hqh Hown Hctrl Hskip'. iIntros (Φ) "(Hg & Hs & Ho & Hsl) HΦ".
  iDestruct "Hg" as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & He & Hf & Ha & Hl)".
  assert (Ht : tbl !! n = None) by (apply not_elem_of_dom; rewrite Hdom; by apply not_elem_of_dom).
  iDestruct "Hw" as (Hin) "[Hobs Hsrc]".
  assert (Hw : world_observers (g_edges s) !! n = None /\ world_sources (g_edges s) !! n = None).
  { destruct Hin as [Hio His]. split; apply not_elem_of_dom; intros Hd; apply (not_elem_of_dom tbl n); auto. }
  assert (He0 : edge_entry_of (g_edges s) n = (empty_observer_set, empty_source_list)).
  { rewrite /edge_entry_of /observers_of /sources_of. destruct Hw as [-> ->]. done. }
  wp_lam. wp_pures. wp_load. wp_apply (resolver_extend_spec with "Hres"). iIntros (r) "#Hr". wp_store.
  wp_pures. wp_load. wp_apply (nodes_table_set with "Hntab"). iIntros (ntab') "#Hntab'". wp_store.
  iMod (graph_edge_alloc _ _ n (edge_entry_of (g_edges s) n) with "He") as "[He Hen]".
  { rewrite /edge_map map_lookup_imap Ht //. }
  iMod (graph_fresh_grow _ _ (fresh_map ep (g_nodes s')) with "Hf") as "Hf".
  { intros i k Hk. rewrite Hnodes fresh_map_insert //. exists k. split; [|done].
    rewrite lookup_insert_ne //. intros ->. rewrite /fresh_map map_lookup_imap Hn in Hk. done. }
  iMod (graph_state_update _ _ s' with "Ha Hs") as "[Ha Hs]".
  iMod (graph_locs_insert _ _ n (lo, ls) with "Hl") as "Hl"; [done|].
  iDestruct (graph_loc_get _ _ n (lo, ls) with "Hl") as "#Hloc"; [by rewrite lookup_insert_eq|].
  iModIntro. iApply "HΦ". rewrite He0. iFrame "Hen Hloc Hs".
  iExists gl, r, ntab', qtab, otab, (<[n := (lo, ls)]> tbl).
  rewrite Hedges Hnodes Hq Hqh Hown Hctrl edge_map_insert He0.
  iFrame "#". iFrame. iSplit; [done|]. iSplit; [done|]. iSplit; [done|].
  iSplit; [iPureIntro; rewrite !dom_insert_L Hdom //|].
  iSplit.
  - iPureIntro. destruct Hin as [Hio His]. rewrite /world_in dom_insert_L. split; set_solver.
  - rewrite /obs_part /src_part !big_sepM_insert //. iSplitL "Ho Hobs".
    + rewrite /observers_of. destruct Hw as [-> _]. simpl. iFrame.
    + rewrite /sources_of. destruct Hw as [_ ->]. simpl. iFrame "Hsrc". iExists ∅. iFrame.
Qed.

(**md
**Resolving an existing node id returns its observer-set and source-list locations.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] a node in `dom (g_nodes s)`.
*)
Lemma is_graph_resolve_spec γ g s (n : node_id) :
  n ∈ dom (g_nodes s) ->
  {{{ is_graph γ g s }}}
    (! g) #(Z.of_nat n)
  {{{ (lo ls : loc), RET (#lo, #ls)%V; is_graph γ g s ∗ graph_loc γ n (lo, ls) }}}.
Proof.
  intros Hn. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_loc _ _ _ _ n Hn with "Hg") as ([lo ls]) "#Hloc".
  iDestruct "Hg" as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & He & Hf & Ha & Hl)".
  iDestruct (graph_loc_agree with "Hl Hloc") as %Htbl.
  wp_load. iApply (wp_wand with "(Hres [//])"). iIntros (v ->).
  iApply ("HΦ" $! lo ls). iFrame "Hloc". iExists ep. iFrame "Hs Hfr". iExists gl, res, ntab, qtab, otab, tbl.
  iFrame "#". iFrame. done.
Qed.

(**md
**An existing node's observer set can be borrowed from `is_graph` and returned unchanged.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] the node whose observer set is borrowed.
- [lo:] the location of `n`'s observer set.
- [ls:] the location of `n`'s source list.
*)
Lemma is_graph_observers_acc γ g s (n : node_id) (lo ls : loc) :
  is_graph γ g s -∗ graph_loc γ n (lo, ls) -∗
  is_observers lo (observers_of (g_edges s) n) ∗
  (is_observers lo (observers_of (g_edges s) n) -∗ is_graph γ g s).
Proof.
  iIntros "(%ep & Hg & Hs & Hfr) #Hloc".
  iDestruct "Hg" as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & (%Hin & Hobs & Hsrc) & He & Hf & Ha & Hl)".
  iDestruct (graph_loc_agree with "Hl Hloc") as %Htbl.
  iDestruct (big_sepM_lookup_acc _ tbl n (lo, ls) Htbl with "Hobs") as "[Ho Hclose]".
  iFrame "Ho". iIntros "Ho". iDestruct ("Hclose" with "Ho") as "Hobs".
  iExists ep. iFrame "Hs Hfr". iExists gl, res, ntab, qtab, otab, tbl.
  iFrame "#". iFrame. done.
Qed.

(**md
**An absent node has an empty observer set in the graph's edge world.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] a node absent from `g_nodes s`.
*)
Lemma is_graph_absent_observers γ g s (n : node_id) :
  g_nodes s !! n = None ->
  is_graph γ g s -∗ ⌜observers_of (g_edges s) n = empty_observer_set⌝.
Proof.
  intros Hn. iIntros "(%ep & Hg & _)".
  iDestruct "Hg" as (gl res ntab qtab otab tbl)
    "(_ & _ & _ & %Hdom & _ & _ & _ & _ & _ & _ & _ & _ & (%Hin & _) & _)".
  iPureIntro. rewrite /observers_of.
  destruct (world_observers (g_edges s) !! n) as [o|] eqn:Eo; [|done].
  exfalso. apply elem_of_dom_2 in Eo.
  assert (Hndom : n ∈ dom (g_nodes s)) by (rewrite -Hdom; apply (proj1 Hin); exact Eo).
  by apply elem_of_dom in Hndom as [? ?]; congruence.
Qed.

(** Seal [is_graph_at] so every change to freshness codes goes through [is_graph_open] or [graph_add_node_spec]. *)
Global Opaque is_graph_at.

(** ** Graph operations above the sealed [is_graph_at] *)

Lemma epoch_rule_refl m ep : epoch_rule m m ep ep.
Proof. apply epoch_rule_rise. intros ? ? ? -> [= ->]. apply freshness_le_refl. Qed.

Lemma edge_unchanged_refl (m : gmap node_id node) w (n : node_id) :
  n ∈ dom m -> n ∉ (∅ : gset node_id) -> edge_entry_of w n = edge_entry_of w n.
Proof. done. Qed.

Lemma edge_unchanged_dom (m : gmap node_id node) w w' (n : node_id) :
  n ∈ dom m -> n ∉ dom m -> edge_entry_of w' n = edge_entry_of w n.
Proof. intros H1 H2. contradiction. Qed.

Definition same_skip (s' s : State.state) : Prop :=
  g_skip s' = g_skip s /\ g_epoch s' = g_epoch s /\ g_stamps s' = g_stamps s.

Lemma same_skip_ok s' s : same_skip s' s -> skip_ok s -> skip_ok s'.
Proof. intros (?&?&?) (?&?&?). split_and!; congruence. Qed.

(**md
**A pure state change to trace or ghost fields steps `is_graph` without heap operations.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the ghost update.
- [s':] the state after it, sharing all physical fields with `s`.
*)
Lemma is_graph_ghost_step γ g s s' :
  same_skip s' s -> g_edges s' = g_edges s -> g_nodes s' = g_nodes s ->
  g_owners s' = g_owners s -> g_queue s' = g_queue s -> g_queue_head s' = g_queue_head s ->
  ctrl_val s' = ctrl_val s ->
  is_graph γ g s ==∗ is_graph γ g s'.
Proof.
  intros Hss Hedges Hnodes Howners Hq Hqh Hctrl.
  iIntros "(%ep & Hg & Hs & Hfr)".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  assert (Hskip' : skip_ok s') by (by eapply same_skip_ok).
  assert (Hm' : edge_match (g_edges s')) by (by rewrite Hedges).
  assert (Hdom' : dom (g_nodes s') = dom (g_nodes s)) by (by rewrite Hnodes).
  assert (Hrule : epoch_rule (g_nodes s) (g_nodes s') ep ep) by (rewrite Hnodes; apply epoch_rule_refl).
  assert (HX : forall n : node_id, n ∈ dom (g_nodes s) -> n ∉ (∅ : gset node_id) ->
    edge_entry_of (g_edges s') n = edge_entry_of (g_edges s) n)
    by (intros ? _ _; by rewrite Hedges).
  iMod ("Hclose" $! s' ep ntab qtab otab ∅ Hskip' Hm' Hdom' Hrule HX
          with "Hgl Hnt [Hqt] [Hot] [Hntab] [Hqtab] [Hotab] [Hw] Hl Hs []") as "(Hg & Hs & _)".
  - by rewrite Hq Hqh.
  - by rewrite Howners Hctrl.
  - by rewrite Hnodes.
  - by rewrite Hq.
  - by rewrite Howners.
  - by rewrite Hedges.
  - by iApply big_sepS_empty.
  - iModIntro. iExists ep. rewrite Hnodes Hedges. iFrame.
Qed.

Definition graph_get_node_code : val :=
  λ: "g" "n", (! ("g" +ₗ #1)) "n".

Definition graph_set_node_code : val :=
  λ: "g" "n" "d", ("g" +ₗ #1) <- table_set (! ("g" +ₗ #1)) "n" "d".

Definition graph_put_node_code : val :=
  λ: "g" "n" "d",
    match: (! ("g" +ₗ #1)) "n" with
      NONE =>
        let: "lo" := observers_new_code #() in
        let: "ls" := sources_new_code #() in
        graph_add_node_code "g" "n" "d" "lo" "ls"
    | SOME <> =>
        graph_set_node_code "g" "n" "d"
    end.

(**md
**Reading node `n` from the graph returns `SOME (node_val d)` when present and `NONE` when absent.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] the node id looked up.
*)
Lemma graph_get_node_spec γ g s (n : node_id) :
  {{{ is_graph γ g s }}}
    graph_get_node_code g #(Z.of_nat n)
  {{{ RET match g_nodes s !! n with Some d => SOMEV (node_val d) | None => NONEV end;
      is_graph γ g s }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load.
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iApply (wp_wand with "Hntab"). iIntros (v ->).
  iApply "HΦ". iExists _. iFrame.
Qed.

(**md
**Updating an existing node `n` in the graph transitions `s` to `set_node n d s` and steps the epoch via `epoch_next`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the write.
- [n:] an existing node of `s`.
- [d:] the new node record.
*)
Lemma graph_set_node_spec γ g s (n : node_id) (d : node) :
  is_Some (g_nodes s !! n) ->
  {{{ is_graph γ g s }}}
    graph_set_node_code g #(Z.of_nat n) (node_val d)
  {{{ RET #(); is_graph γ g (set_node n d s) }}}.
Proof.
  intros Hn. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_apply (nodes_table_set with "Hntab"). iIntros (ntab') "#Hntab'". wp_store.
  assert (Hdom' : dom (g_nodes (set_node n d s)) = dom (g_nodes s)).
  { simpl. apply dom_insert_lookup_L. exact Hn. }
  iMod ("Hclose" $! (set_node n d s) (epoch_next (g_nodes s) (g_nodes (set_node n d s)) ep) ntab' qtab otab ∅
          Hskip Hm Hdom' (epoch_next_rule (g_nodes s) (g_nodes (set_node n d s)) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab' Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. iApply "HΦ". iExists _. rewrite Hdom'. iFrame.
Qed.

(**md
**Putting node `n` into the graph allocates it when absent and updates it when present.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the put.
- [n:] the node id.
- [d:] the node record.
*)
Lemma graph_put_node_spec γ g s (n : node_id) (d : node) :
  {{{ is_graph γ g s }}}
    graph_put_node_code g #(Z.of_nat n) (node_val d)
  {{{ RET #(); is_graph γ g (set_node n d s) }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_bind (ntab _). iApply (wp_wand with "Hntab"). iIntros (v ->).
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  destruct (g_nodes s !! n) as [d0|] eqn:Hn.
  - wp_pures. wp_apply (graph_set_node_spec with "[Hg Hs Hfr]").
    + by eexists.
    + iExists _. iFrame.
    + iExact "HΦ".
  - wp_pures. wp_apply observers_new_spec; [done|]. iIntros (lo) "Ho".
    wp_pures. wp_apply sources_new_spec; [done|]. iIntros (ls) "Hsl". wp_pures.
    iDestruct (is_graph_open with "Hg") as (gl' res' ntab' qtab' otab' tbl')
      "(%Heq & %Hskip' & %Hm' & %Hdom' & Hgl & Hnt & Hqt & Hot & #Hres' & #Hntab' & #Hqtab' & #Hotab' & (%Hin & Hobs & Hsrc) & Hl & Hclose)".
    injection Heq as <-.
    assert (Ht : tbl' !! n = None) by (apply not_elem_of_dom; rewrite Hdom'; by apply not_elem_of_dom).
    assert (Hw : world_observers (g_edges s) !! n = None /\ world_sources (g_edges s) !! n = None).
    { destruct Hin as [Hio His]. split; apply not_elem_of_dom; intros Hd; apply (not_elem_of_dom tbl' n); auto. }
    assert (He0 : edge_entry_of (g_edges s) n = (empty_observer_set, empty_source_list)).
    { rewrite /edge_entry_of /observers_of /sources_of. destruct Hw as [-> ->]. done. }
    iMod ("Hclose" $! s ep ntab' qtab' otab' ∅ Hskip' Hm' eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab' Hqtab' Hotab' [Hobs Hsrc] Hl Hs []") as "(Hg & Hs & _)".
    { iSplit; [done|]. iFrame. }
    { by iApply big_sepS_empty. }
    wp_apply (graph_add_node_spec γ #gl s (set_node n d s) _ n d lo ls Hn eq_refl eq_refl eq_refl eq_refl eq_refl eq_refl Hskip'
                with "[$Hg $Hs $Ho $Hsl]").
    iIntros "(Hg & Hs & Hen & _)". iApply "HΦ". iExists _. iFrame "Hg Hs".
    simpl. rewrite dom_insert_L big_sepS_insert; [|by apply not_elem_of_dom].
    rewrite He0. iFrame.
Qed.

(** ** Queue operations *)

Definition graph_enqueue_code : val :=
  λ: "g" "n",
    let: "q" := ! ("g" +ₗ #2) in
    let: "qtab" := Fst (Fst "q") in
    let: "len" := Snd (Fst "q") in
    let: "hd" := Snd "q" in
    ("g" +ₗ #2) <- (table_set "qtab" "len" "n", "len" + #1, "hd").

Definition graph_live_queue_code : val :=
  λ: "g",
    let: "q" := ! ("g" +ₗ #2) in
    Snd "q" < Snd (Fst "q").

Definition graph_dequeue_code : val :=
  λ: "g",
    let: "q" := ! ("g" +ₗ #2) in
    let: "qtab" := Fst (Fst "q") in
    let: "len" := Snd (Fst "q") in
    let: "hd" := Snd "q" in
    match: "qtab" "hd" with
      SOME "n" =>
        ("g" +ₗ #2) <- ("qtab", "len", "hd" + #1);;
        SOME "n"
    | NONE =>
        ("g" +ₗ #2) <- (table_empty, #0, #0);;
        NONE
    end.

(**md
**Enqueuing node `n` appends it to `g_queue` as `enqueue n s` does.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before enqueuing.
- [n:] the node enqueued.
*)
Lemma graph_enqueue_spec γ g s (n : node_id) :
  {{{ is_graph γ g s }}}
    graph_enqueue_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g (enqueue n s) }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_pures.
  wp_apply (queue_table_snoc with "Hqtab"). iIntros (qtab') "#Hqtab'". wp_pures. wp_store.
  rewrite (_ : (Z.of_nat (length (g_queue s)) + 1)%Z = Z.of_nat (length (g_queue (enqueue n s))));
    [|simpl; rewrite length_app /=; lia].
  iMod ("Hclose" $! (enqueue n s) ep ntab qtab' otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab' Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`graph_live_queue_code` decides whether `g_queue_head s < length (g_queue s)`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Lemma graph_live_queue_spec γ g s :
  {{{ is_graph γ g s }}}
    graph_live_queue_code g
  {{{ RET #(live_queue s); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_load. wp_pures.
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. rewrite /live_queue.
  rewrite (bool_decide_ext (Z.of_nat (g_queue_head s) < Z.of_nat (length (g_queue s)))%Z
                           (g_queue_head s < length (g_queue s))); [|lia].
  iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`graph_dequeue_code` pops the next queued effect or resets the exhausted queue.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Lemma graph_dequeue_spec γ g s :
  {{{ is_graph γ g s }}}
    graph_dequeue_code g
  {{{ RET opt_nat_val (g_queue s !! g_queue_head s);
      is_graph γ g (match g_queue s !! g_queue_head s with
                    | Some _ => with_queue_head (S (g_queue_head s)) s
                    | None => with_queue [] (with_queue_head 0 s)
                    end) }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_load. wp_pures. wp_bind (qtab _). iApply (wp_wand with "Hqtab"). iIntros (v ->).
  destruct (g_queue s !! g_queue_head s) as [n|] eqn:Eq; simpl.
  - wp_pures. wp_store. wp_pures.
    rewrite (_ : (Z.of_nat (g_queue_head s) + 1)%Z = Z.of_nat (S (g_queue_head s))); [|lia].
    iMod ("Hclose" $! (with_queue_head (S (g_queue_head s)) s) ep ntab qtab otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
    { by iApply big_sepS_empty. }
    iModIntro. iApply "HΦ". iExists ep. iFrame.
  - wp_pures. wp_store. wp_pures.
    iPoseProof queue_table_empty as "#Hq0".
    iMod ("Hclose" $! (with_queue [] (with_queue_head 0 s)) ep ntab table_empty otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hq0 Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
    { by iApply big_sepS_empty. }
    iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(** ** Owner and control operations *)

Definition graph_get_owner_code : val :=
  λ: "g" "o", (Fst (Fst (! ("g" +ₗ #3)))) "o".

Definition graph_put_owner_code : val :=
  λ: "g" "o" "x",
    let: "p" := ! ("g" +ₗ #3) in
    let: "otab" := Fst (Fst "p") in
    let: "fr" := Snd (Fst "p") in
    let: "ctrl" := Snd "p" in
    let: "fr'" := if: "fr" < "o" + #1 then "o" + #1 else "fr" in
    ("g" +ₗ #3) <- (table_set "otab" "o" "x", "fr'", "ctrl").

Definition graph_fresh_owner_id_code : val :=
  λ: "g", Snd (Fst (! ("g" +ₗ #3))).

Definition graph_get_ctrl_code : val :=
  λ: "g", Snd (! ("g" +ₗ #3)).

Definition graph_set_ctrl_code : val :=
  λ: "g" "c",
    let: "p" := ! ("g" +ₗ #3) in
    ("g" +ₗ #3) <- (Fst (Fst "p"), Snd (Fst "p"), "c").

(**md
**Reading owner `o` returns `SOME (owner_val x)` when present and `NONE` when absent.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [o:] the owner id.
*)
Lemma graph_get_owner_spec γ g s (o : owner_id) :
  {{{ is_graph γ g s }}}
    graph_get_owner_code g #(Z.of_nat o)
  {{{ RET match g_owners s !! o with Some x => SOMEV (owner_val x) | None => NONEV end;
      is_graph γ g s }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_pures.
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iApply (wp_wand with "Hotab"). iIntros (v ->).
  iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**Writing owner `o` updates `g_owners` to `<[o := x]> (g_owners s)` and advances the fresh id.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the write.
- [o:] the owner id.
- [x:] the new owner state.
*)
Lemma graph_put_owner_spec γ g s (o : owner_id) (x : owner_state) :
  {{{ is_graph γ g s }}}
    graph_put_owner_code g #(Z.of_nat o) (owner_val x)
  {{{ RET #(); is_graph γ g (with_owners (<[o := x]> (g_owners s)) s) }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_pures.
  set (fr := stdpp.base.fresh (dom (g_owners s))).
  wp_bind (If _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (Nat.max (S o) fr))⌝)%I).
  { case_bool_decide; wp_pures; iPureIntro; f_equal; f_equal; lia. }
  iIntros (v ->). wp_pures.
  wp_apply (owners_table_set with "Hotab"). iIntros (otab') "#Hotab'". wp_pures. wp_store.
  assert (Hfr' : Nat.max (S o) fr = stdpp.base.fresh (dom (<[o := x]> (g_owners s)))).
  { rewrite dom_insert_L fresh_insert_gset_nat //. }
  rewrite Hfr'.
  iMod ("Hclose" $! (with_owners (<[o := x]> (g_owners s)) s) ep ntab qtab otab' ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab' Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`graph_fresh_owner_id_code` returns `fresh (dom (g_owners s))`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Lemma graph_fresh_owner_id_spec γ g s :
  {{{ is_graph γ g s }}}
    graph_fresh_owner_id_code g
  {{{ RET #(Z.of_nat (stdpp.base.fresh (dom (g_owners s)))); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_load. wp_pures.
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**Reading the control state returns `ctrl_val s`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Lemma graph_get_ctrl_spec γ g s :
  {{{ is_graph γ g s }}}
    graph_get_ctrl_code g
  {{{ RET ctrl_val s; is_graph γ g s }}}.
Proof.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_load. wp_pures.
  iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_refl (g_nodes s) (g_edges s))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
  { by iApply big_sepS_empty. }
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**Writing the control state transitions `s` to `s'` when only control, trace or ghost fields change.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before the write.
- [s':] the state after the write.
*)
Lemma graph_set_ctrl_spec γ g s s' :
  same_skip s' s -> g_edges s' = g_edges s -> g_nodes s' = g_nodes s ->
  g_owners s' = g_owners s -> g_queue s' = g_queue s -> g_queue_head s' = g_queue_head s ->
  {{{ is_graph γ g s }}}
    graph_set_ctrl_code g (ctrl_val s')
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hss Hedges Hnodes Howners Hq Hqh.
  iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load. wp_pures. wp_store.
  assert (Hskip' : skip_ok s') by (by eapply same_skip_ok).
  assert (Hm' : edge_match (g_edges s')) by (by rewrite Hedges).
  assert (Hdom' : dom (g_nodes s') = dom (g_nodes s)) by (by rewrite Hnodes).
  assert (Hrule : epoch_rule (g_nodes s) (g_nodes s') ep ep) by (rewrite Hnodes; apply epoch_rule_refl).
  assert (HX : forall n : node_id, n ∈ dom (g_nodes s) -> n ∉ (∅ : gset node_id) ->
    edge_entry_of (g_edges s') n = edge_entry_of (g_edges s) n)
    by (intros ? _ _; by rewrite Hedges).
  iMod ("Hclose" $! s' ep ntab qtab otab ∅ Hskip' Hm' Hdom' Hrule HX
          with "Hgl Hnt [Hqt] [Hot] [Hntab] [Hqtab] [Hotab] [Hw] Hl Hs []") as "(Hg & Hs & _)".
  - by rewrite Hq Hqh.
  - by rewrite Howners.
  - by rewrite Hnodes.
  - by rewrite Hq.
  - by rewrite Howners.
  - by rewrite Hedges.
  - by iApply big_sepS_empty.
  - iModIntro. iApply "HΦ". iExists ep. rewrite Hnodes Hedges. iFrame.
Qed.

(** ** Graph-level edge operations *)

Definition graph_sources_begin_run_code : val :=
  λ: "g" "n", sources_begin_run_code (! "g") "n".

Definition graph_sources_end_run_code : val :=
  λ: "g" "n", sources_end_run_code (! "g") "n".

Definition graph_sources_clear_code : val :=
  λ: "g" "n", sources_clear_code (! "g") "n".

Definition graph_sources_add_code : val :=
  λ: "g" "c" "n", sources_add_code (! "g") "c" "n".

Definition graph_slot_src_code : val :=
  λ: "g" "n" "i",
    match: graph_get_node_code "g" "n" with
      NONE => NONE
    | SOME <> => sources_slot_src_code (! "g") "n" "i"
    end.

Definition graph_held_in_prefix_code : val :=
  λ: "g" "c" "s",
    match: graph_get_node_code "g" "c" with
      NONE => #false
    | SOME <> => sources_held_in_prefix_code (! "g") "c" "s"
    end.

Definition node_stale_live_code : val :=
  λ: "d",
    let: "f" := Fst (Fst (Snd (Fst "d"))) in
    let: "disp" := Fst (Fst (Fst (Snd "d"))) in
    ("f" ≠ #0) && (~ "disp").

Definition graph_slot_holds_loop : val :=
  rec: "loop" "g" "n" "i" :=
    match: graph_slot_src_code "g" "n" "i" with
      NONE => #false
    | SOME "src" =>
        let: "ok" :=
          match: graph_get_node_code "g" "src" with
            NONE => #false
          | SOME "e" => node_stale_live_code "e"
          end in
        if: "ok" then #true else "loop" "g" "n" ("i" + #1)
    end.

Definition graph_slot_holds_stale_live_code : val :=
  λ: "g" "n", graph_slot_holds_loop "g" "n" #0.

Lemma edge_match_begin_run self w : edge_match w -> edge_match (sources_begin_run self w).
Proof.
  intros Hm c i s h. rewrite /sources_begin_run /slot_at sources_of_set_sources observers_of_set_sources.
  destruct (decide (c = self)) as [->|]; apply Hm.
Qed.

(**md
**`BeginRun` on the graph resets node `n`'s source cursor to `0`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before `BeginRun`.
- [n:] an existing node of `s`.
*)
Lemma graph_sources_begin_run_spec γ g s (n : node_id) :
  n ∈ dom (g_nodes s) ->
  {{{ is_graph γ g s }}}
    graph_sources_begin_run_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g (with_edges (sources_begin_run n (g_edges s)) s) }}}.
Proof.
  intros Hn. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ". iApply wp_fupd.
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load.
  assert (Hntbl : n ∈ dom tbl) by (by rewrite Hdom).
  wp_apply (sources_begin_run_spec res tbl n (g_edges s) Hntbl with "[$Hres $Hw]").
  iIntros "Hw".
  iMod ("Hclose" $! (with_edges (sources_begin_run n (g_edges s)) s) ep ntab qtab otab (dom (g_nodes s))
          Hskip (edge_match_begin_run n _ Hm) eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_dom (g_nodes s) (g_edges s) (sources_begin_run n (g_edges s)))
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs Hfr") as "(Hg & Hs & Hfr)".
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`EndRun` on the graph trims node `n`'s trailing sources as `sources_end_run` does.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before `EndRun`.
- [n:] an existing node of `s`.
- [w':] the world `sources_end_run` produces.
*)
Lemma graph_sources_end_run_spec γ g s (n : node_id) (w' : EdgeWorld) :
  n ∈ dom (g_nodes s) -> sources_end_run n (g_edges s) = Some w' ->
  {{{ is_graph γ g s }}}
    graph_sources_end_run_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g (with_edges w' s) }}}.
Proof.
  intros Hn Hend. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ". iApply wp_fupd.
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load.
  assert (Hntbl : n ∈ dom tbl) by (by rewrite Hdom).
  wp_apply (sources_end_run_spec res tbl n (g_edges s) w' Hm Hntbl Hend with "[$Hres $Hw]").
  iIntros "[%Hm' Hw]".
  iMod ("Hclose" $! (with_edges w' s) ep ntab qtab otab (dom (g_nodes s))
          Hskip Hm' eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_dom (g_nodes s) (g_edges s) w')
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs Hfr") as "(Hg & Hs & Hfr)".
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`Clear` on the graph removes all sources of node `n` as `sources_clear` does.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before `Clear`.
- [n:] an existing node of `s`.
- [w':] the world `sources_clear` produces.
*)
Lemma graph_sources_clear_spec γ g s (n : node_id) (w' : EdgeWorld) :
  n ∈ dom (g_nodes s) -> sources_clear n (g_edges s) = Some w' ->
  {{{ is_graph γ g s }}}
    graph_sources_clear_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g (with_edges w' s) }}}.
Proof.
  intros Hn Hclr. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ". iApply wp_fupd.
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load.
  assert (Hntbl : n ∈ dom tbl) by (by rewrite Hdom).
  wp_apply (sources_clear_spec res tbl n (g_edges s) w' Hm Hntbl Hclr with "[$Hres $Hw]").
  iIntros "[%Hm' Hw]".
  iMod ("Hclose" $! (with_edges w' s) ep ntab qtab otab (dom (g_nodes s))
          Hskip Hm' eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_dom (g_nodes s) (g_edges s) w')
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs Hfr") as "(Hg & Hs & Hfr)".
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`Add` on the graph records that computation `c` read source `n` as `sources_add` does.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state before `Add`.
- [c:] the reading computation, in `dom (g_nodes s)`.
- [n:] the source read, in `dom (g_nodes s)`.
- [w':] the world `sources_add` produces.
*)
Lemma graph_sources_add_spec γ g s (c n : node_id) (w' : EdgeWorld) :
  c ∈ dom (g_nodes s) -> n ∈ dom (g_nodes s) -> sources_add c n (g_edges s) = Some w' ->
  {{{ is_graph γ g s }}}
    graph_sources_add_code g #(Z.of_nat c) #(Z.of_nat n)
  {{{ RET #(); is_graph γ g (with_edges w' s) }}}.
Proof.
  intros Hc Hn Hadd. iIntros (Φ) "(%ep & Hg & Hs & Hfr) HΦ". iApply wp_fupd.
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  wp_lam. wp_pures. wp_load.
  assert (Hctbl : c ∈ dom tbl) by (by rewrite Hdom).
  assert (Hntbl : n ∈ dom tbl) by (by rewrite Hdom).
  wp_apply (sources_add_spec res tbl c n (g_edges s) w' Hm Hctbl Hntbl Hadd with "[$Hres $Hw]").
  iIntros "[%Hm' Hw]".
  iMod ("Hclose" $! (with_edges w' s) ep ntab qtab otab (dom (g_nodes s))
          Hskip Hm' eq_refl
          (epoch_rule_refl (g_nodes s) ep)
          (edge_unchanged_dom (g_nodes s) (g_edges s) w')
          with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs Hfr") as "(Hg & Hs & Hfr)".
  iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`graph_slot_src_code` returns the source at slot `i` of node `n`, or `NONE` past the end.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] the computation id.
- [i:] the slot index.
*)
Lemma graph_slot_src_spec γ g s (n : node_id) (i : nat) :
  {{{ is_graph γ g s }}}
    graph_slot_src_code g #(Z.of_nat n) #(Z.of_nat i)
  {{{ RET opt_nat_val (fst <$> slot_at (g_edges s) n i); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "(%ep & Hg & Hs & Hfr)".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  destruct (g_nodes s !! n) as [d|] eqn:Hn.
  - wp_pures. wp_load.
    assert (Hntbl : n ∈ dom tbl) by (rewrite Hdom; by apply elem_of_dom_2 in Hn).
    wp_apply (sources_slot_src_spec res tbl n i (g_edges s) Hntbl with "[$Hres $Hw]").
    iIntros "Hw".
    iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
    { by iApply big_sepS_empty. }
    iModIntro.
    replace (match slot_at (g_edges s) n i with Some p => SOMEV #(Z.of_nat p.1) | None => NONEV end)
      with (opt_nat_val (fst <$> slot_at (g_edges s) n i))
      by (by destruct (slot_at (g_edges s) n i)).
    iApply "HΦ". iExists ep. iFrame.
  - wp_pures.
    iDestruct "Hw" as "[%Hin Hw']".
    assert (Hnone : slot_at (g_edges s) n i = None).
    { destruct (slot_at (g_edges s) n i) as [x|] eqn:Ex; [|done].
      exfalso. apply lookup_dom_sources in Ex.
      assert (Hndom : n ∈ dom (g_nodes s)) by (rewrite -Hdom; apply (proj2 Hin); exact Ex).
      by apply elem_of_dom in Hndom as [? ?]; congruence. }
    rewrite Hnone /=.
    iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab [Hw'] Hl Hs []") as "(Hg & Hs & _)".
    { iSplit; [done|iExact "Hw'"]. }
    { by iApply big_sepS_empty. }
    iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

(**md
**`graph_held_in_prefix_code` decides whether `src` is in `read_prefix (g_edges s) c`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [c:] the computation whose prefix is checked.
- [src:] the source looked for.
*)
Lemma graph_held_in_prefix_spec γ g s (c src : node_id) :
  {{{ is_graph γ g s }}}
    graph_held_in_prefix_code g #(Z.of_nat c) #(Z.of_nat src)
  {{{ RET #(bool_decide (src ∈ read_prefix (g_edges s) c)); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "(%ep & Hg & Hs & Hfr)".
  iDestruct (is_graph_open with "Hg") as (gl res ntab qtab otab tbl)
    "(-> & %Hskip & %Hm & %Hdom & Hgl & Hnt & Hqt & Hot & #Hres & #Hntab & #Hqtab & #Hotab & Hw & Hl & Hclose)".
  destruct (g_nodes s !! c) as [d|] eqn:Hc.
  - wp_pures. wp_load.
    assert (Hctbl : c ∈ dom tbl) by (rewrite Hdom; by apply elem_of_dom_2 in Hc).
    wp_apply (sources_held_in_prefix_spec res tbl c src (g_edges s) Hctbl with "[$Hres $Hw]").
    iIntros "Hw".
    iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab Hw Hl Hs []") as "(Hg & Hs & _)".
    { by iApply big_sepS_empty. }
    iModIntro. iApply "HΦ". iExists ep. iFrame.
  - wp_pures.
    iDestruct "Hw" as "[%Hin Hw']".
    assert (Hnil : read_prefix (g_edges s) c = []).
    { rewrite /read_prefix /sources_of.
      destruct (world_sources (g_edges s) !! c) as [sl|] eqn:Esl; [|done].
      exfalso. apply elem_of_dom_2 in Esl.
      assert (Hcdom : c ∈ dom (g_nodes s)) by (rewrite -Hdom; apply (proj2 Hin); exact Esl).
      by apply elem_of_dom in Hcdom as [? ?]; congruence. }
    rewrite Hnil bool_decide_eq_false_2; [|by apply not_elem_of_nil].
    iMod ("Hclose" $! s ep ntab qtab otab ∅ Hskip Hm eq_refl
            (epoch_rule_refl (g_nodes s) ep)
            (edge_unchanged_refl (g_nodes s) (g_edges s))
            with "Hgl Hnt Hqt Hot Hntab Hqtab Hotab [Hw'] Hl Hs []") as "(Hg & Hs & _)".
    { iSplit; [done|iExact "Hw'"]. }
    { by iApply big_sepS_empty. }
    iModIntro. iApply "HΦ". iExists ep. iFrame.
Qed.

Lemma node_stale_live_spec (e : node) :
  {{{ True }}} node_stale_live_code (node_val e) {{{ RET #(stale_live e); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. rewrite /node_val /node_val_a /node_val_b /node_val_c /node_val_d.
  wp_pures. rewrite /stale_live.
  destruct (n_freshness e), (n_disposed e); simpl; wp_pures; by iApply "HΦ".
Qed.

Lemma graph_slot_holds_loop_spec γ g s (n : node_id) (i : nat) :
  {{{ is_graph γ g s }}}
    graph_slot_holds_loop g #(Z.of_nat n) #(Z.of_nat i)
  {{{ RET #(existsb (fun src => match g_nodes s !! src with Some e => stale_live e | None => false end)
              (drop i (fst <$> source_slots (sources_of (g_edges s) n))));
      is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". iLöb as "IH" forall (i).
  wp_rec. wp_pures.
  wp_apply (graph_slot_src_spec with "Hg"). iIntros "Hg".
  set (sl := source_slots (sources_of (g_edges s) n)).
  change (slot_at (g_edges s) n i) with (sl !! i).
  destruct (sl !! i) as [[src mh]|] eqn:Hsl; simpl.
  - wp_pures. wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    assert (Hdrop : drop i (fst <$> sl) = src :: drop (S i) (fst <$> sl)).
    { apply (drop_S (fst <$> sl) src i). rewrite list_lookup_fmap Hsl //. }
    rewrite Hdrop /=.
    destruct (g_nodes s !! src) as [e|].
    + wp_pures. wp_apply node_stale_live_spec; [done|]. iIntros "_".
      destruct (stale_live e); wp_pures.
      * iModIntro. by iApply "HΦ".
      * rewrite (_ : (Z.of_nat i + 1)%Z = Z.of_nat (S i)); [|lia].
        by iApply ("IH" $! (S i) with "Hg").
    + wp_pures. rewrite (_ : (Z.of_nat i + 1)%Z = Z.of_nat (S i)); [|lia].
      by iApply ("IH" $! (S i) with "Hg").
  - wp_pures. iModIntro.
    rewrite drop_ge; [by iApply "HΦ"|].
    rewrite length_fmap. by apply lookup_ge_None_1 in Hsl.
Qed.

(**md
**`graph_slot_holds_stale_live_code` computes `slot_holds stale_live s n`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [n:] the computation whose slots are checked.
*)
Lemma graph_slot_holds_stale_live_spec γ g s (n : node_id) :
  {{{ is_graph γ g s }}}
    graph_slot_holds_stale_live_code g #(Z.of_nat n)
  {{{ RET #(slot_holds stale_live s n); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_slot_holds_loop_spec _ _ _ _ 0 with "Hg").
  rewrite drop_0. done.
Qed.

(** ** Allocating an initial graph *)

Definition graph_new_code : val :=
  λ: <>,
    let: "gl" := AllocN #4 #0 in
    "gl" <- table_empty;;
    ("gl" +ₗ #1) <- table_empty;;
    ("gl" +ₗ #2) <- (table_empty, #0, #0);;
    let: "otab" := table_set table_empty #(Z.of_nat 0) (owner_val (new_owner 0 None false)) in
    ("gl" +ₗ #3) <- ("otab", #1, ctrl_val (init current_flags));;
    "gl".

(**md
**`graph_new_code` allocates a fresh graph holding `init current_flags`.**
*)
Lemma graph_new_spec :
  {{{ True }}}
    graph_new_code #()
  {{{ γ g, RET g; is_graph γ g (init current_flags) }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_alloc gl as "Hgl"; [lia|].
  rewrite (_ : Z.to_nat 4 = 4%nat) // /=.
  iDestruct (fields4 with "Hgl") as "(H0 & H1 & H2 & H3)".
  wp_pures. wp_store. wp_pures. wp_store. wp_pures. wp_store. wp_pures.
  iPoseProof owners_table_empty as "#Ho0".
  wp_apply (owners_table_set ∅ table_empty 0 (new_owner 0 None false) with "Ho0"). iIntros (otab) "#Hotab".
  wp_pures. wp_store.
  iPoseProof resolver_empty_spec as "#Hres".
  iPoseProof nodes_table_empty as "#Hntab".
  iPoseProof queue_table_empty as "#Hqtab".
  assert (Hskip0 : skip_ok (init current_flags)) by (split_and!; done).
  assert (Hm0 : edge_match (g_edges (init current_flags))) by (intros c i s h; split; intros H; done).
  assert (Hdom0 : dom (∅ : gmap node_id (loc * loc)) = dom (g_nodes (init current_flags)))
    by (simpl; by rewrite dom_empty_L).
  iMod (is_graph_alloc gl table_empty table_empty table_empty otab ∅ (init current_flags)
          Hskip0 Hm0 Hdom0
          with "[$H0 $H1 $H2 $H3]") as (γ) "Hg".
  { iFrame "#". iSplit; [iPureIntro; split; simpl; rewrite dom_empty_L; done|].
    rewrite /obs_part /src_part !big_sepM_empty. by iSplit. }
  iModIntro. iApply ("HΦ" $! γ #gl). iExact "Hg".
Qed.

Global Opaque graph_get_node_code graph_set_node_code graph_put_node_code.
Global Opaque graph_enqueue_code graph_live_queue_code graph_dequeue_code.
Global Opaque graph_get_owner_code graph_put_owner_code graph_fresh_owner_id_code.
Global Opaque graph_get_ctrl_code graph_set_ctrl_code.
Global Opaque graph_sources_begin_run_code graph_sources_end_run_code graph_sources_clear_code graph_sources_add_code.
Global Opaque graph_slot_src_code graph_held_in_prefix_code node_stale_live_code graph_slot_holds_stale_live_code.
Global Opaque graph_new_code.

End graph.
