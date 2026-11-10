From stdpp Require Import prelude gmap.
From iris.algebra Require Import auth gmap excl_auth agree numbers.
From iris.algebra.lib Require Import gmap_view.
From iris.base_logic.lib Require Import own.
From Ranvier.Base Require Import Prelude.
From Ranvier.Pure Require Import ObserverSet SourceList State.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList.

(** * Ghost state of the heap graph

One ghost name holds three parts: the authoritative edge map, with a fragment per node; the monotone freshness
of each node; and the agreement with the pure [state].

Freshness is monotone through a ghost epoch per node. The code of a node is [3 * epoch + rank], where the
rank is [Clean] 0, [Check] 1 or [Dirty] 2, and the epoch advances when the freshness falls. The code only
grows, so a lower bound taken at one state holds at every later one.

Obligations of Task 15:
- The epoch protocol. The epoch is existential in [is_graph] and [graph_fresh_raise] lets any holder raise a
  code, so a lower bound carries no information yet. Task 15 fixes that the epoch advances only at a freshness
  fall ([Dirty] or [Check] to [Clean]), with the ranks [Clean] 0 < [Check] 1 < [Dirty] 2.
- [is_graph]-level lemmas: a combined update of the heap and the ghost state; node allocation, extending the
  table, [is_world], the edge fragments and the freshness entries; [resolver] over a growing table, through an
  invariant with a monotone map; and [edge_match (g_edges s)] with [dom tbl = dom (g_nodes s)], held in
  [is_graph_at] or proved from the engine invariant. *)

Definition edge_entry : Type := observer_set * source_list.

Definition graphUR : ucmra :=
  prodUR (gmap_viewUR node_id (agreeR (leibnizO edge_entry)))
         (prodUR (authUR (gmapUR node_id max_natR)) (excl_authUR (leibnizO State.state))).

(**md
The ghost state of a heap graph: the authoritative edge map, monotone freshness and the agreement with the pure state.
*)
Class graphG Σ := GraphG { graph_inG :: inG Σ graphUR }.

Definition graphΣ : gFunctors := #[GFunctor graphUR].

Global Instance subG_graphΣ {Σ} : subG graphΣ Σ -> graphG Σ.
Proof. solve_inG. Qed.

(**md
The edge entry of each node of `tbl`: its observer set and source list in `w`.
*)
Definition edge_map (tbl : gmap node_id (loc * loc)) (w : EdgeWorld) : gmap node_id edge_entry :=
  map_imap (fun n _ => Some (observers_of w n, sources_of w n)) tbl.

(**md
The freshness code of a node at epoch `e` with freshness `f`.
*)
Definition fresh_code (e : nat) (f : freshness) : nat := 3 * e + freshness_rank f.

(**md
The freshness code of each node of `nodes`, with the epochs `ep`, absent epochs 0.
*)
Definition fresh_map (ep : gmap node_id nat) (nodes : gmap node_id node) : gmap node_id max_nat :=
  map_imap (fun n d => Some (MaxNat (fresh_code (default 0 (ep !! n)) (n_freshness d)))) nodes.

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

Section ghost.
Context `{!graphG Σ}.

Definition graph_edges_auth (γ : gname) (m : gmap node_id edge_entry) : iProp Σ :=
  own γ (gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1) (to_agree <$> m), ε).

(**md
The exclusive fragment of node `n`'s entry `e` in the edge map of `γ`.
*)
Definition graph_edge (γ : gname) (n : node_id) (e : edge_entry) : iProp Σ :=
  own γ (gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e), ε).

Definition graph_fresh_auth (γ : gname) (m : gmap node_id max_nat) : iProp Σ :=
  own γ (ε, (● m, ε)) ∗ own γ (ε, (◯ m, ε)).

(**md
Node `n`'s freshness code in `γ` is at least `k`.
*)
Definition graph_fresh_lb (γ : gname) (n : node_id) (k : nat) : iProp Σ :=
  own γ (ε, (◯ {[n := MaxNat k]}, ε)).

Definition graph_state_auth (γ : gname) (s : State.state) : iProp Σ :=
  own γ (ε, (ε, ●E (s : leibnizO State.state))).

(**md
The client's half of the agreement: the pure state is `s`.
*)
Definition graph_state (γ : gname) (s : State.state) : iProp Σ :=
  own γ (ε, (ε, ◯E (s : leibnizO State.state))).

Global Instance graph_fresh_lb_persistent γ n k : Persistent (graph_fresh_lb γ n k).
Proof. apply _. Qed.

Lemma graph_ghost_alloc (s : State.state) (m : gmap node_id max_nat) :
  ⊢ |==> ∃ γ, graph_edges_auth γ ∅ ∗ graph_fresh_auth γ m ∗ graph_state_auth γ s ∗ graph_state γ s.
Proof.
  iMod (own_alloc ((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                      (to_agree <$> (∅ : gmap node_id edge_entry)), ε) ⋅
                   ((ε, (● m, ε)) ⋅ ((ε, (◯ m, ε)) ⋅
                    ((ε, (ε, ●E (s : leibnizO State.state))) ⋅ (ε, (ε, ◯E (s : leibnizO State.state))))))))
    as (γ) "H".
  { rewrite -!pair_op !left_id !right_id. split; simpl; [apply gmap_view_auth_valid|]. split; simpl.
    - apply auth_both_valid_discrete. split; [done|].
      intros i. destruct (m !! i : option max_natR) eqn:E; rewrite E; done.
    - apply excl_auth_valid. }
  rewrite !own_op. iDestruct "H" as "(H1 & H2 & H3 & H4 & H5)". iExists γ. iFrame. done.
Qed.

Lemma graph_edge_alloc γ m n e :
  m !! n = None -> graph_edges_auth γ m ==∗ graph_edges_auth γ (<[n := e]> m) ∗ graph_edge γ n e.
Proof.
  intros Hn. iIntros "H". rewrite /graph_edges_auth /graph_edge.
  iMod (own_update _ _ ((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                           (to_agree <$> <[n := e]> m), ε) ⋅
                        (gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e), ε))
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
  rewrite lookup_fmap in Hl. destruct (m !! n) as [e'|] eqn:E; simpl in Hl; [|by inversion Hl].
  apply Some_equiv_inj, (inj to_agree), leibniz_equiv in Hl. by rewrite Hl.
Qed.

Lemma graph_edge_update γ m n e e' :
  graph_edges_auth γ m -∗ graph_edge γ n e ==∗ graph_edges_auth γ (<[n := e']> m) ∗ graph_edge γ n e'.
Proof.
  iIntros "Ha Hf". iCombine "Ha Hf" as "H". rewrite /graph_edges_auth /graph_edge.
  iMod (own_update _ _ ((gmap_view_auth (V := agreeR (leibnizO edge_entry)) (DfracOwn 1)
                           (to_agree <$> <[n := e']> m), ε) ⋅
                        (gmap_view_frag (V := agreeR (leibnizO edge_entry)) n (DfracOwn 1) (to_agree e'), ε))
          with "H") as "[$ $]"; [|done].
  rewrite -!pair_op fmap_insert. apply prod_update; simpl; [|done].
  by apply gmap_view_replace.
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

Lemma graph_fresh_raise γ m n k :
  (forall k0, m !! n = Some (MaxNat k0) -> k0 <= k) ->
  graph_fresh_auth γ m ==∗ graph_fresh_auth γ (<[n := MaxNat k]> m).
Proof.
  intros Hle. iIntros "[Ha Hf]". iCombine "Ha Hf" as "H".
  iMod (own_update _ _ ((ε, (● (<[n := MaxNat k]> m), ε)) ⋅ (ε, (◯ (<[n := MaxNat k]> m), ε))) with "H")
    as "[$ $]"; [|done].
  rewrite -!pair_op. apply prod_update; simpl; [done|]. apply prod_update; simpl; [|done].
  apply auth_update. destruct (m !! n) as [[k0]|] eqn:E.
  - eapply insert_local_update; [done|done|]. apply max_nat_local_update. simpl. by apply Hle.
  - by apply alloc_local_update.
Qed.

Lemma graph_state_agree γ s s' : graph_state_auth γ s -∗ graph_state γ s' -∗ ⌜s = s'⌝.
Proof.
  iIntros "Ha Hf". iDestruct (own_valid_2 with "Ha Hf") as %[_ [_ Hv]]. simpl in Hv.
  by apply excl_auth_agree_L in Hv.
Qed.

Lemma graph_state_update γ s s' :
  graph_state_auth γ s -∗ graph_state γ s ==∗ graph_state_auth γ s' ∗ graph_state γ s'.
Proof.
  iIntros "Ha Hf". iCombine "Ha Hf" as "H".
  iMod (own_update _ _ ((ε, (ε, ●E (s' : leibnizO State.state))) ⋅ (ε, (ε, ◯E (s' : leibnizO State.state))))
          with "H") as "[$ $]"; [|done].
  rewrite -!pair_op. apply prod_update; simpl; [done|]. apply prod_update; simpl; [done|].
  apply excl_auth_update.
Qed.

End ghost.

Section graph.
Context `{!heapGS Σ, !graphG Σ}.

(**md
The graph `g` holds the pure state `s` at the epochs `ep`: `g` is the resolver of a node table whose heap holds
the edge world of `s`, the edge map of `γ` holds each node's entry, the freshness of `γ` holds each node's
code at its epoch, and `γ` agrees on `s`. The heap tier covers the present engine, so the fields of the
saturated-write skip hold their initial values.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
- [ep:] the epoch of each node.
*)
Definition is_graph_at (γ : gname) (g : val) (s : State.state) (ep : gmap node_id nat) : iProp Σ :=
  ∃ tbl : gmap node_id (loc * loc),
    ⌜g_skip s = SkipOff /\ g_epoch s = 0 /\ g_stamps s = ∅⌝ ∗
    resolver tbl g ∗ is_world tbl (g_edges s) ∗
    graph_edges_auth γ (edge_map tbl (g_edges s)) ∗ graph_fresh_auth γ (fresh_map ep (g_nodes s)) ∗
    graph_state_auth γ s.

(**md
`is_graph_at` at some epochs.

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the pure state.
*)
Definition is_graph (γ : gname) (g : val) (s : State.state) : iProp Σ := ∃ ep, is_graph_at γ g s ep.

(**md
**A graph holding the pure state agrees with the client's state.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state the graph holds.
- [s':] the client's state.
*)
Lemma is_graph_agree γ g s s' : is_graph γ g s -∗ graph_state γ s' -∗ ⌜s = s'⌝.
Proof.
  iIntros "(%ep & %tbl & _ & _ & _ & _ & _ & Ha) Hf". iApply (graph_state_agree with "Ha Hf").
Qed.

(**md
**A node's edge fragment is its observer set and source list in the graph's world.**

- [γ:] the ghost name.
- [g:] the graph value.
- [s:] the state.
- [n:] the node.
- [e:] the fragment's entry.
*)
Lemma is_graph_edge γ g s n e :
  is_graph γ g s -∗ graph_edge γ n e -∗ ⌜e = (observers_of (g_edges s) n, sources_of (g_edges s) n)⌝.
Proof.
  iIntros "(%ep & %tbl & _ & _ & _ & Ha & _) Hf". iDestruct (graph_edge_agree with "Ha Hf") as %Hl.
  rewrite /edge_map map_lookup_imap in Hl. destruct (tbl !! n); simpl in Hl; by simplify_eq.
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
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n (fresh_code (default 0 (ep !! n)) (n_freshness d)).
Proof.
  iIntros (Hn) "(%tbl & _ & _ & _ & _ & Hf & _)". iApply (graph_fresh_lb_get with "Hf").
  rewrite /fresh_map map_lookup_imap Hn //.
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
  is_graph_at γ g s ep -∗ graph_fresh_lb γ n k -∗
  ⌜k <= fresh_code (default 0 (ep !! n)) (n_freshness d)⌝.
Proof.
  iIntros (Hn) "(%tbl & _ & _ & _ & _ & Hf & _) Hlb".
  iDestruct (graph_fresh_lb_valid with "Hf Hlb") as %(k' & Hl & Hle).
  rewrite /fresh_map map_lookup_imap Hn /= in Hl. by simplify_eq.
Qed.

(**md
**A heap holding an edge world starts a graph.**

The client receives the state's agreement and the edge fragment of every node of the table.

- [g:] the resolver of the table.
- [tbl:] the node table.
- [s:] the pure state, whose skip fields hold their initial values.
*)
Lemma is_graph_alloc (g : val) tbl (s : State.state) :
  g_skip s = SkipOff -> g_epoch s = 0 -> g_stamps s = ∅ ->
  resolver tbl g ∗ is_world tbl (g_edges s) ⊢ |==> ∃ γ, is_graph γ g s ∗ graph_state γ s ∗
    [∗ map] n ↦ e ∈ edge_map tbl (g_edges s), graph_edge γ n e.
Proof.
  iIntros (H1 H2 H3) "[#Hres Hw]".
  iMod (graph_ghost_alloc s (fresh_map ∅ (g_nodes s))) as (γ) "(He & Hf & Ha & Hs)".
  iMod (graph_edges_alloc_all _ (edge_map tbl (g_edges s)) with "He") as "[He Hfr]".
  iModIntro. iExists γ. iSplitR "Hs Hfr"; [|iFrame]. iExists ∅, tbl. iFrame "#". iFrame. done.
Qed.

End graph.
