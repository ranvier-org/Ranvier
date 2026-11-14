From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler Ownership Collections.
From iris.heap_lang Require Import proofmode notation.
From iris.heap_lang.lib Require Import array.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking Run Scheduler Ownership.

Open Scope nat_scope.

(**md # Heap collection-boundary primitives

This module lifts the four collection-boundary areas of `Pure/Collections.v` to HeapLang separation logic:

1. Direct-observer edge attachment (`world_direct_link_spec`), detachment (`world_direct_unlink_spec`), and
   `ForwardingEdge` (`forwarding_edge_moved_in_spec`, `forwarding_edge_mark_spec`).
2. Active `OwnerLink` unlinking (`owner_links_active_tail_unlink_spec` and `owner_links_active_mid_unlink_spec`)
   for per-key child scopes and reparenting.
3. Two-queue pass and effect scheduling (`schedule_pass_tq_spec` and `schedule_effect_tq_spec`).
4. Collection signal and row hooks (`write_signal_except_spec` and `notify_removed_spec`), plus
   `Memo.Adopt` companion resource lifecycle (`memo_adopt_spec` and `memo_dispose_adopted_spec`). *)

(** ** Two-queue scheduler heap code (Core.fs lines 2155–2167, 3090–3215) *)

Definition schedule_pass_tq_code : val :=
  λ: "tq" "n",
    let: "ps" := !("tq" +ₗ #4) in
    if: vlist_mem_nat_code "n" "ps" then #()
    else
      "tq" <- vlist_snoc_code !"tq" "n";;
      ("tq" +ₗ #4) <- SOME ("n", "ps").

Definition schedule_effect_tq_code : val :=
  λ: "tq" "n",
    let: "es" := !("tq" +ₗ #5) in
    if: vlist_mem_nat_code "n" "es" then #()
    else
      ("tq" +ₗ #2) <- vlist_snoc_code !("tq" +ₗ #2) "n";;
      ("tq" +ₗ #5) <- SOME ("n", "es").

(** ** ForwardingEdge heap representation and code (Core.fs lines 503–523) *)

Definition forwarding_edge_new_code : val :=
  λ: "target" "h",
    let: "fe" := AllocN #2 #0 in
    "fe" <- "target";;
    ("fe" +ₗ #1) <- "h";;
    "fe".

Definition forwarding_edge_moved_in_code : val :=
  λ: "fe" "_sourceSlot" "newHandle",
    ("fe" +ₗ #1) <- "newHandle".

Definition forwarding_edge_mark_dirty_code : val :=
  λ: "fe" "g",
    let: "target" := !"fe" in
    mark_dirty_code "g" "target".

Definition forwarding_edge_mark_check_code : val :=
  λ: "fe" "g",
    let: "target" := !"fe" in
    mark_check_code "g" "target".

(** ** Memo.Adopt heap representation and code (Core.fs lines 4094, 4647–4651, 4683–4696) *)

Definition adopt_new_code : val :=
  λ: <>,
    let: "ad" := AllocN #3 #0 in
    "ad" <- #false;;
    ("ad" +ₗ #1) <- NONE;;
    ("ad" +ₗ #2) <- NONE;;
    "ad".

Definition adopt_step_code : val :=
  λ: "ad" "c",
    if: !"ad" then
      ("ad" +ₗ #2) <- vlist_snoc_code !("ad" +ₗ #2) "c"
    else
      match: !("ad" +ₗ #1) with
        NONE => ("ad" +ₗ #1) <- SOME "c"
      | SOME "prev" =>
          if: "prev" = "c" then #()
          else
            ("ad" +ₗ #1) <- SOME "c";;
            ("ad" +ₗ #2) <- vlist_snoc_code !("ad" +ₗ #2) "prev"
      end.

Definition adopt_dispose_code : val :=
  λ: "ad",
    if: !"ad" then #()
    else
      "ad" <- #true;;
      match: !("ad" +ₗ #1) with
        NONE => #()
      | SOME "c" =>
          ("ad" +ₗ #1) <- NONE;;
          ("ad" +ₗ #2) <- vlist_snoc_code !("ad" +ₗ #2) "c"
      end.

Section heap_collections.
Context `{!heapGS Σ, !graphG Σ}.

(** ** 1. Direct-observer edge boundary and ForwardingEdge *)

(**md
**Direct-observer edge registration (`sources_add_code`) transitions `is_world tbl w` to `is_world tbl (direct_link self s w)` and preserves `world_well_formed`.**

When a direct observer (`Binding`, `FoldReads`, `FoldRow`, `ValueRow`, or `ForwardingEdge`) with
`source_cursor = length source_slots` attaches to a distinct upstream source `s`, `sources_add_code`
updates the heap edge world to `direct_link self s w` while preserving `world_well_formed`.

- [res:] the resolver value.
- [tbl:] the node table mapping node ids to `(observer_set, source_list)` locations.
- [self:] the direct-observer node id.
- [s:] the upstream source node id.
- [w:] the edge world before linking.
*)
Theorem world_direct_link_spec (res : val) tbl (self s : node_id) (w : EdgeWorld) :
  world_well_formed w ->
  self ∈ dom tbl ->
  s ∈ dom tbl ->
  source_cursor (sources_of w self) = length (source_slots (sources_of w self)) ->
  s ∉ fst <$> source_slots (sources_of w self) ->
  {{{ resolver tbl res ∗ is_world tbl w }}}
    sources_add_code res #(Z.of_nat self) #(Z.of_nat s)
  {{{ RET #(); ⌜world_well_formed (direct_link self s w)⌝ ∗ is_world tbl (direct_link self s w) }}}.
Proof.
  intros Hwf Hself Hs Hcur Hnd.
  destruct (direct_link_is_sources_add self s w Hwf Hcur Hnd) as [Hadd _].
  destruct (direct_link_preserves_well_formed self s w Hwf Hnd) as (Hwf' & _).
  iIntros (Φ) "Hpre HΦ".
  wp_apply (sources_add_spec res tbl self s w (direct_link self s w) (proj1 Hwf) Hself Hs Hadd with "Hpre").
  iIntros "[_ Hw]". iApply "HΦ". by iFrame.
Qed.

(**md
**Direct-observer detachment (`sources_clear_code`) transitions `is_world tbl w` to `is_world tbl w'` and preserves `world_well_formed`.**

When a direct observer (`Binding`, `FoldReads`, `FoldRow`, or `ValueRow`) is disposed, `sources_clear_code`
removes every upstream edge, preserves `world_well_formed`, and leaves `source_slots (sources_of w' self) = []`.

- [res:] the resolver value.
- [tbl:] the node table.
- [self:] the direct-observer node id being cleared.
- [w:] the edge world before detachment.
- [w':] the edge world after `sources_clear`.
*)
Theorem world_direct_unlink_spec (res : val) tbl (self : node_id) (w w' : EdgeWorld) :
  world_well_formed w ->
  self ∈ dom tbl ->
  sources_clear self w = Some w' ->
  {{{ resolver tbl res ∗ is_world tbl w }}}
    sources_clear_code res #(Z.of_nat self)
  {{{ RET #();
      ⌜world_well_formed w' /\ source_slots (sources_of w' self) = [] /\
       (forall src, self ∉ observer_view (observers_of w' src))⌝ ∗
      is_world tbl w' }}}.
Proof.
  intros Hwf Hself Hclr.
  destruct (direct_unlink_preserves_well_formed self w Hwf) as (w'' & Hclr' & Hwf' & Hempty & Hno_obs & _).
  rewrite Hclr in Hclr'. injection Hclr' as <-.
  iIntros (Φ) "Hpre HΦ".
  wp_apply (sources_clear_spec res tbl self w w' (proj1 Hwf) Hself Hclr with "Hpre").
  iIntros "[_ Hw]". iApply "HΦ". iFrame. iPureIntro. done.
Qed.

(**md Heap representation of a `ForwardingEdge` (`Core.fs` lines 503–523): stores the downstream `target`
    node id and the current observer-set handle `h` in the upstream source's `ObserverSet`. *)
Definition is_forwarding_edge (fe : loc) (target : node_id) (h : nat) : iProp Σ :=
  fe ↦ #(Z.of_nat target) ∗
  (fe +ₗ 1) ↦ #(Z.of_nat h).

(**md
**`forwarding_edge_moved_in_code` updates the stored handle in `is_forwarding_edge` when `ObserverSet` swap-removes another entry into `newHandle`.**

- [fe:] the `ForwardingEdge` location.
- [target:] the target node id forwarded to.
- [slot:] the unused source-slot parameter (`0` for `ForwardingEdge`).
- [h:] the previous observer-set handle.
- [h':] the new observer-set handle after swap-remove.
*)
Theorem forwarding_edge_moved_in_spec (fe : loc) (target slot h h' : nat) :
  {{{ is_forwarding_edge fe target h }}}
    forwarding_edge_moved_in_code #fe #(Z.of_nat slot) #(Z.of_nat h')
  {{{ RET #(); is_forwarding_edge fe target h' }}}.
Proof.
  iIntros (Φ) "[H0 H1] HΦ".
  wp_lam. wp_pures. wp_store. iModIntro.
  iApply "HΦ". iFrame.
Qed.

(**md
**`forwarding_edge_mark_dirty_code` and `forwarding_edge_mark_check_code` delegate `MarkDirty` and `MarkCheck` directly to the `target` node in `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fe:] the `ForwardingEdge` location.
- [target:] the target node id (`foldMemo` or `outerMemo`).
- [h:] the observer-set handle.
- [fuel:] the marking fuel.
- [s:] the graph state before marking.
*)
Theorem forwarding_edge_mark_spec γ g (fe : loc) (target h fuel : nat) (s : State.state) :
  (forall s',
     mark_dirty fuel target s = Some s' ->
     {{{ is_forwarding_edge fe target h ∗ is_graph γ g s }}}
       forwarding_edge_mark_dirty_code #fe g
     {{{ RET #(); is_forwarding_edge fe target h ∗ is_graph γ g s' }}}) /\
  (forall s',
     mark_check fuel target s = Some s' ->
     {{{ is_forwarding_edge fe target h ∗ is_graph γ g s }}}
       forwarding_edge_mark_check_code #fe g
     {{{ RET #(); is_forwarding_edge fe target h ∗ is_graph γ g s' }}}).
Proof.
  split.
  - intros s' Hmd. iIntros (Φ) "[[H0 H1] Hg] HΦ".
    wp_lam. wp_pures. wp_load. wp_pures.
    wp_apply (mark_dirty_spec γ g fuel target s s' Hmd with "Hg").
    iIntros "Hg". iApply "HΦ". iFrame.
  - intros s' Hmc. iIntros (Φ) "[[H0 H1] Hg] HΦ".
    wp_lam. wp_pures. wp_load. wp_pures.
    wp_apply (mark_check_spec γ g fuel target s s' Hmc with "Hg").
    iIntros "Hg". iApply "HΦ". iFrame.
Qed.

(** ** 2. Active OwnerLink unlinking for per-key child scopes *)

Definition link_detached (l : link) : link :=
  {| l_child := l_child l; l_owner := l_owner l; l_generation := None; l_acquired := l_acquired l |}.

(**md
**`owner_links_unlink_code` detaches an active tail `OwnerLink` (`l_generation l = Some gen`) in `O(1)` time.**

Covers both the single-child case (`entries = [(lk, l)]`) and the multi-child tail case
(`entries = E0 ++ [(lk_prev, l_prev); (lk, l)]`), updating `ol`'s head/tail pointers and clearing `lk`'s
`prev`, `next`, and `generation` fields.

- [ol:] the `Owner` doubly-linked list header location.
- [o:] the owner id.
- [lk:] the link node location being detached.
- [l:] the pure `link` record with `l_generation l = Some gen`.
- [gen:] the current generation of `ol`.
- [acq:] the current acquisition counter of `ol`.
*)
Theorem owner_links_active_tail_unlink_spec (ol : loc) (o : owner_id) (lk : loc) (l : link) (gen acq : nat) :
  l_generation l = Some gen ->
  {{{ is_owner_links ol o [(lk, l)] gen acq }}}
    owner_links_unlink_code #ol #lk
  {{{ RET #(); is_owner_links ol o [] gen acq ∗ is_link_node lk None None (link_detached l) }}}.
Proof.
  intros Hgen.
  iIntros (Φ) "(Hol0 & Hol1 & Hol2 & Hol3 & [(Hlk0 & Hlk1 & Hlk2 & Hlk3 & Hlk4) _]) HΦ".
  rewrite /owner_links_unlink_code Hgen /=.
  wp_lam. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
  rewrite bool_decide_eq_true_2 //. wp_pures.
  wp_store. wp_load. wp_load. wp_pures.
  wp_store. wp_store. wp_store. wp_store.
  iModIntro. iApply "HΦ".
  rewrite /is_owner_links /is_link_node /link_detached /=.
  iFrame.
Qed.

(**md
**`owner_links_unlink_code` splices an active interior `OwnerLink` (`l_generation l = Some gen`) out of a doubly-linked chain in `O(1)` time.**

When a projection key or reparented child scope is unlinked from the middle of a live parent's `OwnerLink`
list (`prev = Some lk_prev`, `next = Some lk_next`), `owner_links_unlink_code` redirects `lk_prev.next` to
`Some lk_next` and `lk_next.prev` to `Some lk_prev`, and clears `lk`'s `prev`, `next`, and `generation`
fields without traversing the rest of the chain.

- [ol:] the `Owner` doubly-linked list header location.
- [lk_prev:] the preceding link node location.
- [lk:] the interior link node location being unlinked.
- [lk_next:] the succeeding link node location.
- [prev_p:] `lk_prev`'s predecessor pointer.
- [next_n:] `lk_next`'s successor pointer.
- [l_prev:] the pure `link` record of `lk_prev`.
- [l:] the pure `link` record of `lk`, with `l_generation l = Some gen`.
- [l_next:] the pure `link` record of `lk_next`.
- [gen:] the current generation of `ol`.
*)
Theorem owner_links_active_mid_unlink_spec (ol lk_prev lk lk_next : loc)
    (prev_p next_n : option loc) (l_prev l l_next : link) (gen : nat) :
  l_generation l = Some gen ->
  {{{ (ol +ₗ 2) ↦ #(Z.of_nat gen) ∗
      is_link_node lk_prev prev_p (Some lk) l_prev ∗
      is_link_node lk (Some lk_prev) (Some lk_next) l ∗
      is_link_node lk_next (Some lk) next_n l_next }}}
    owner_links_unlink_code #ol #lk
  {{{ RET #();
      (ol +ₗ 2) ↦ #(Z.of_nat gen) ∗
      is_link_node lk_prev prev_p (Some lk_next) l_prev ∗
      is_link_node lk_next (Some lk_prev) next_n l_next ∗
      is_link_node lk None None (link_detached l) }}}.
Proof.
  intros Hgen.
  iIntros (Φ) "(Hol2 & (Hp0 & Hp1 & Hp2 & Hp3 & Hp4) & (Hlk0 & Hlk1 & Hlk2 & Hlk3 & Hlk4) & (Hn0 & Hn1 & Hn2 & Hn3 & Hn4)) HΦ".
  rewrite /owner_links_unlink_code Hgen /=.
  wp_lam. wp_pures. wp_load. wp_pures. wp_load. wp_pures.
  rewrite bool_decide_eq_true_2 //. wp_pures.
  wp_store. wp_load. wp_load. wp_pures.
  wp_store. wp_store. wp_store. wp_store.
  iModIntro. iApply "HΦ".
  rewrite /is_link_node /link_detached /=.
  iFrame.
Qed.

(** ** 3. Two-queue pass and effect scheduling *)

Definition is_two_queue (tq : loc) (st : two_queue_state) (ps es : list node_id) : iProp Σ :=
  ⌜list_to_set ps = tq_pass_set st /\ list_to_set es = tq_effect_set st⌝ ∗
  tq ↦ list_val (tq_passes st) ∗
  (tq +ₗ 1) ↦ #(Z.of_nat (tq_pass_head st)) ∗
  (tq +ₗ 2) ↦ list_val (tq_queue st) ∗
  (tq +ₗ 3) ↦ #(Z.of_nat (tq_queue_head st)) ∗
  (tq +ₗ 4) ↦ list_val ps ∗
  (tq +ₗ 5) ↦ list_val es.

(**md
**`schedule_pass_tq_code` transitions `is_two_queue` along `schedule_pass_tq` and preserves `tq_inv`.**

- [tq:] the two-queue scheduler location.
- [n:] the pass node id scheduled (`SchedulePass`).
- [st:] the two-queue state before scheduling.
- [ps:] the list representation of `tq_pass_set st`.
- [es:] the list representation of `tq_effect_set st`.
*)
Theorem schedule_pass_tq_spec (tq : loc) (n : node_id) (st : two_queue_state) (ps es : list node_id) :
  tq_inv st ->
  {{{ is_two_queue tq st ps es }}}
    schedule_pass_tq_code #tq #(Z.of_nat n)
  {{{ ps', RET #(); ⌜tq_inv (schedule_pass_tq n st)⌝ ∗ is_two_queue tq (schedule_pass_tq n st) ps' es }}}.
Proof.
  intros Hinv.
  destruct (pass_queue_once n st Hinv) as [Hinv' _].
  iIntros (Φ) "([%Hps %Hes] & H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /schedule_pass_tq_code.
  wp_lam. wp_pures. wp_load. wp_pures.
  wp_apply (vlist_mem_nat_spec n ps); [done|]. iIntros "_".
  destruct (decide (n ∈ tq_pass_set st)) as [Hin|Hnin].
  - assert (Hin_ps : n ∈ ps) by set_solver.
    rewrite (bool_decide_eq_true_2 (n ∈ ps) Hin_ps). wp_pures. iModIntro.
    iApply ("HΦ" $! ps). iSplit; [done|].
    rewrite /schedule_pass_tq decide_True // /is_two_queue. iFrame. done.
  - assert (Hnin_ps : n ∉ ps) by set_solver.
    rewrite (bool_decide_eq_false_2 (n ∈ ps) Hnin_ps). wp_pures.
    wp_load. rewrite list_val_eq.
    wp_apply (vlist_snoc_spec (fun k : nat => #(Z.of_nat k)) (tq_passes st) n); [done|].
    iIntros "_". wp_store. wp_store. iModIntro.
    iApply ("HΦ" $! (n :: ps)). iSplit; [done|].
    rewrite /schedule_pass_tq decide_False // /is_two_queue /= (list_val_eq (_ ++ _)). iFrame.
    iPureIntro. split; [set_solver|done].
Qed.

(**md
**`schedule_effect_tq_code` transitions `is_two_queue` along `schedule_effect_tq` and preserves `tq_inv`.**

- [tq:] the two-queue scheduler location.
- [n:] the effect node id scheduled (`Schedule`).
- [st:] the two-queue state before scheduling.
- [ps:] the list representation of `tq_pass_set st`.
- [es:] the list representation of `tq_effect_set st`.
*)
Theorem schedule_effect_tq_spec (tq : loc) (n : node_id) (st : two_queue_state) (ps es : list node_id) :
  tq_inv st ->
  {{{ is_two_queue tq st ps es }}}
    schedule_effect_tq_code #tq #(Z.of_nat n)
  {{{ es', RET #(); ⌜tq_inv (schedule_effect_tq n st)⌝ ∗ is_two_queue tq (schedule_effect_tq n st) ps es' }}}.
Proof.
  intros Hinv.
  destruct (pass_queue_once n st Hinv) as [_ Hinv'].
  iIntros (Φ) "([%Hps %Hes] & H0 & H1 & H2 & H3 & H4 & H5) HΦ".
  rewrite /schedule_effect_tq_code.
  wp_lam. wp_pures. wp_load. wp_pures.
  wp_apply (vlist_mem_nat_spec n es); [done|]. iIntros "_".
  destruct (decide (n ∈ tq_effect_set st)) as [Hin|Hnin].
  - assert (Hin_es : n ∈ es) by set_solver.
    rewrite (bool_decide_eq_true_2 (n ∈ es) Hin_es). wp_pures. iModIntro.
    iApply ("HΦ" $! es). iSplit; [done|].
    rewrite /schedule_effect_tq decide_True // /is_two_queue. iFrame. done.
  - assert (Hnin_es : n ∉ es) by set_solver.
    rewrite (bool_decide_eq_false_2 (n ∈ es) Hnin_es). wp_pures.
    wp_load. rewrite (list_val_eq (tq_queue st)).
    wp_apply (vlist_snoc_spec (fun k : nat => #(Z.of_nat k)) (tq_queue st) n); [done|].
    iIntros "_". wp_store. wp_store. iModIntro.
    iApply ("HΦ" $! (n :: es)). iSplit; [done|].
    rewrite /schedule_effect_tq decide_False // /is_two_queue /= (list_val_eq (tq_queue st ++ [n])). iFrame.
    iPureIntro. split; [done|set_solver].
Qed.

(** ** 4. Collection signal and row hooks *)

(**md
**`notify_dirty_except_code` establishes the collection `WriteExcept` postcondition (`ends_dirty s1 m`) on the heap graph.**

Combines `notify_dirty_except_spec` with `write_signal_except_wakes_others`: after updating signal `n`'s
value and ghost publication, running `notify_dirty_except_code` transitions `is_graph` to `s1` where every
observer `m` (with `r <> Some m` or `n ∈ read_prefix (g_edges s) m`) satisfies `ends_dirty s1 m`.

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the marking fuel.
- [n:] the signal node id written.
- [v:] the new value written.
- [r:] the excluded computation (`Some running` or `None`).
- [d:] the signal node in `s`.
- [s:] the state before the write.
- [s1:] the state after `notify_dirty_except`.
- [m:] an observer of `n` that takes marks.
*)
Theorem write_signal_except_spec γ g (fuel : nat) (n : node_id) (v : Z)
    (r : option node_id) (d : node) (s s1 : State.state) (m : node_id) :
  g_nodes s !! n = Some d ->
  n_kind d = KSignal ->
  m ∈ observer_view (observers_of (g_edges s) n) ->
  takes_marks s m ->
  (r ≠ Some m \/ n ∈ read_prefix (g_edges s) m) ->
  notify_dirty_except fuel n r (ghost_publish n (set_node n (node_with_value v d) s)) = Some s1 ->
  {{{ is_graph γ g (ghost_publish n (set_node n (node_with_value v d) s)) }}}
    notify_dirty_except_code g #(Z.of_nat n) (opt_nat_val r)
  {{{ RET #(); ⌜ends_dirty s1 m⌝ ∗ is_graph γ g s1 }}}.
Proof.
  intros Hd Hk Hin Ht Hcond Hnot.
  destruct (write_signal_except_wakes_others fuel n v r d s s1 m Hd Hk Hin Ht Hcond Hnot) as [Hdirty _].
  iIntros (Φ) "Hg HΦ".
  wp_apply (notify_dirty_except_spec γ g fuel n r _ s1 Hnot with "Hg").
  iIntros "Hg". iApply "HΦ". by iFrame.
Qed.

(**md
**`notify_dirty_code` establishes the `Memo.NotifyRemoved` postcondition (`ends_dirty s1 m`) on the heap graph before a removed key's row memo is disposed.**

- [γ:] the ghost name.
- [g:] the graph value.
- [fuel:] the marking fuel.
- [n:] the retiring row memo node id.
- [s:] the state before `notify_removed`.
- [s1:] the state after `notify_removed`.
- [m:] an observer of `n` that takes marks.
*)
Theorem notify_removed_spec γ g (fuel : nat) (n : node_id) (s s1 : State.state) (m : node_id) :
  m ∈ observer_view (observers_of (g_edges s) n) ->
  takes_marks s m ->
  notify_removed fuel n s = Some (s1, tt) ->
  {{{ is_graph γ g s }}}
    notify_dirty_code g #(Z.of_nat n)
  {{{ RET #(); ⌜ends_dirty s1 m⌝ ∗ is_graph γ g s1 }}}.
Proof.
  intros Hin Ht Hnr.
  pose proof (notify_removed_wakes_observers fuel n s s1 m Hin Ht Hnr) as Hdirty.
  unfold notify_removed, m_step in Hnr.
  destruct (notify_dirty fuel n s) as [s'|] eqn:E; [|done].
  injection Hnr as <-.
  iIntros (Φ) "Hg HΦ".
  wp_apply (notify_dirty_spec γ g fuel n s s' E with "Hg").
  iIntros "Hg". iApply "HΦ". by iFrame.
Qed.

(** ** 4. Memo.Adopt companion resource lifecycle *)

Definition is_adopt (ad : loc) (st : adopt_state) : iProp Σ :=
  ad ↦ #(ad_disposed st) ∗
  (ad +ₗ 1) ↦ opt_nat_val (ad_current st) ∗
  (ad +ₗ 2) ↦ list_val (ad_disposed_log st).

(**md
**`adopt_step_code` transitions `is_adopt ad st` to `is_adopt ad (memo_adopt_step c st)`.**

- [ad:] the adopt state location.
- [c:] the companion id passed to `Memo.Adopt`.
- [st:] the adopt state before the call.
*)
Theorem memo_adopt_spec (ad : loc) (c : nat) (st : adopt_state) :
  {{{ is_adopt ad st }}}
    adopt_step_code #ad #(Z.of_nat c)
  {{{ RET #(); is_adopt ad (memo_adopt_step c st) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /adopt_step_code /memo_adopt_step.
  wp_lam. wp_pures. wp_load.
  destruct (ad_disposed st) eqn:Ed; wp_pures.
  - wp_load. rewrite list_val_eq.
    wp_apply (vlist_snoc_spec (fun n : nat => #(Z.of_nat n)) (ad_disposed_log st) c); [done|].
    iIntros "_". wp_store. iModIntro. iApply "HΦ".
    rewrite /is_adopt /= list_val_eq. iFrame.
  - wp_load. destruct (ad_current st) as [prev|] eqn:Ec; simpl; wp_pures.
    + case_bool_decide as Heq; wp_pures.
      * assert (prev = c) as -> by (injection Heq; lia).
        rewrite decide_True //. iModIntro. iApply "HΦ".
        rewrite /is_adopt Ed Ec. iFrame.
      * assert (prev ≠ c) by (intros ->; done).
        rewrite decide_False //.
        wp_store. wp_load. rewrite list_val_eq.
        wp_apply (vlist_snoc_spec (fun n : nat => #(Z.of_nat n)) (ad_disposed_log st) prev); [done|].
        iIntros "_". wp_store. iModIntro. iApply "HΦ".
        rewrite /is_adopt /= list_val_eq. iFrame.
    + wp_store. iModIntro. iApply "HΦ". rewrite /is_adopt /=. iFrame.
Qed.

(**md
**`adopt_dispose_code` transitions `is_adopt ad st` to `is_adopt ad (memo_dispose_adopted st)` and establishes `adopt_companion_disposed_once` when `st = adopt_many cs empty_adopt_state`.**

- [ad:] the adopt state location.
- [st:] the adopt state before `Memo.Dispose`.
*)
Theorem memo_dispose_adopted_spec (ad : loc) (st : adopt_state) :
  {{{ is_adopt ad st }}}
    adopt_dispose_code #ad
  {{{ RET #(); is_adopt ad (memo_dispose_adopted st) }}}.
Proof.
  iIntros (Φ) "(H0 & H1 & H2) HΦ".
  rewrite /adopt_dispose_code /memo_dispose_adopted.
  wp_lam. wp_pures. wp_load.
  destruct (ad_disposed st) eqn:Ed; wp_pures.
  - iModIntro. iApply "HΦ". rewrite /is_adopt Ed. iFrame.
  - wp_store. wp_load.
    destruct (ad_current st) as [c|] eqn:Ec; simpl; wp_pures.
    + wp_store. wp_load. rewrite list_val_eq.
      wp_apply (vlist_snoc_spec (fun n : nat => #(Z.of_nat n)) (ad_disposed_log st) c); [done|].
      iIntros "_". wp_store. iModIntro. iApply "HΦ".
      rewrite /is_adopt /= list_val_eq. iFrame.
    + iModIntro. iApply "HΦ". rewrite /is_adopt /=. iFrame.
Qed.

(** ** 1-Based Fenwick Prefix Sums and Point Updates *)

Definition fenwick_sum_loop : val :=
  rec: "loop" "arr" "i" "acc" :=
    if: #0 < "i" then
      let: "v" := !("arr" +ₗ "i") in
      let: "lsb" := BinOp AndOp "i" (- "i") in
      "loop" "arr" ("i" - "lsb") ("acc" + "v")
    else "acc".

Definition fenwick_sum_code : val :=
  λ: "arr" "i", fenwick_sum_loop "arr" "i" #0.

Lemma fenwick_sum_loop_spec (arr : loc) (dq : dfrac) (tree : list Z) (i : nat) (acc : Z) :
  (i < length tree)%nat ->
  {{{ arr ↦∗{dq} ((fun z : Z => #z) <$> tree) }}}
    fenwick_sum_loop #arr #(Z.of_nat i) #acc
  {{{ RET #(fenwick_sum_go i tree i acc); arr ↦∗{dq} ((fun z : Z => #z) <$> tree) }}}.
Proof.
  revert acc. induction i as [i IH] using lt_wf_ind.
  intros acc Hlen Φ. iIntros "Hpre HΦ".
  wp_rec. wp_pures.
  destruct (decide (1 <= i)%nat) as [Hi|Hni].
  - rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
    destruct (lookup_lt_is_Some_2 tree i Hlen) as [v Hv].
    wp_apply (wp_load_offset with "Hpre"); [by rewrite list_lookup_fmap Hv|].
    iIntros "Hpre". wp_pures.
    rewrite (z_land_opp_nat i Hi).
    destruct (nat_lsb_bounds i Hi) as [Hpos Hle].
    replace (Z.of_nat i - Z.of_nat (nat_lsb i))%Z with (Z.of_nat (i - nat_lsb i)) by lia.
    wp_apply (IH (i - nat_lsb i) ltac:(lia) (acc + v)%Z ltac:(lia) with "Hpre").
    iIntros "Hpre".
    destruct i as [|i']; [lia|]. cbn [fenwick_sum_go].
    destruct (decide (1 <= S i')%nat) as [_|]; [|lia].
    rewrite Hv /=.
    erewrite fenwick_sum_go_fuel_irrel; [iApply "HΦ"; iExact "Hpre" | lia | lia].
  - assert (i = 0)%nat as -> by lia.
    rewrite bool_decide_eq_false_2; [|lia]. wp_pures.
    cbn [fenwick_sum_go]. iApply "HΦ". iExact "Hpre".
Qed.

(**md
**1-based HeapLang Fenwick prefix sum matches pure `fenwick_sum_pure`.**

Walking down `i` via two's-complement step `i - (i &&& -i)` over the
fractional heap array `arr ↦∗{dq} tree` returns `fenwick_sum_pure tree i`
without mutating the Fenwick tree.

- [arr:] base heap location of the 1-based Fenwick array.
- [dq:] fractional ownership held during the read-only prefix query.
- [tree:] pure integer model of the Fenwick array entries.
- [i:] 1-based prefix index to accumulate (`0` for the empty prefix).
*)
Theorem fenwick_sum_spec (arr : loc) (dq : dfrac) (tree : list Z) (i : nat) :
  (i < length tree)%nat ->
  {{{ arr ↦∗{dq} ((fun z : Z => #z) <$> tree) }}}
    fenwick_sum_code #arr #(Z.of_nat i)
  {{{ RET #(fenwick_sum_pure tree i); arr ↦∗{dq} ((fun z : Z => #z) <$> tree) }}}.
Proof.
  intros Hlen Φ. iIntros "Hpre HΦ".
  wp_lam. wp_pures.
  wp_apply (fenwick_sum_loop_spec with "Hpre"); [exact Hlen|].
  iExact "HΦ".
Qed.

Definition fenwick_add_loop : val :=
  rec: "loop" "arr" "n" "i" "delta" :=
    if: "i" ≤ "n" then
      let: "v" := !("arr" +ₗ "i") in
      ("arr" +ₗ "i") <- ("v" + "delta") ;;
      let: "lsb" := BinOp AndOp "i" (- "i") in
      "loop" "arr" "n" ("i" + "lsb") "delta"
    else #().

Definition fenwick_add_code : val :=
  λ: "arr" "n" "i" "delta", fenwick_add_loop "arr" "n" "i" "delta".

Lemma fenwick_add_loop_spec (arr : loc) (tree : list Z) (n i : nat) (delta : Z) :
  (1 <= i)%nat -> (S n <= length tree)%nat ->
  {{{ arr ↦∗ ((fun z : Z => #z) <$> tree) }}}
    fenwick_add_loop #arr #(Z.of_nat n) #(Z.of_nat i) #delta
  {{{ RET #(); arr ↦∗ ((fun z : Z => #z) <$> fenwick_add_pure tree n i delta) }}}.
Proof.
  remember (S n - i)%nat as rem eqn:Hrem. revert tree i Hrem.
  induction rem as [rem IH] using lt_wf_ind.
  intros tree i Hrem Hi Hlen Φ. iIntros "Hpre HΦ".
  wp_rec. wp_pures.
  destruct (decide (i <= n)%nat) as [Hle|Hgt].
  - rewrite bool_decide_eq_true_2; [|lia]. wp_pures.
    assert (Hil : (i < length tree)%nat) by lia.
    destruct (lookup_lt_is_Some_2 tree i Hil) as [v Hv].
    wp_apply (wp_load_offset with "Hpre"); [by rewrite list_lookup_fmap Hv|].
    iIntros "Hpre". wp_pures.
    wp_apply (wp_store_offset with "Hpre"); [rewrite list_lookup_fmap Hv; eauto|].
    iIntros "Hpre". rewrite -list_fmap_insert. wp_pures.
    rewrite (z_land_opp_nat i Hi).
    destruct (nat_lsb_bounds i Hi) as [Hpos _].
    replace (Z.of_nat i + Z.of_nat (nat_lsb i))%Z with (Z.of_nat (i + nat_lsb i)) by lia.
    set (tree' := <[i := (v + delta)%Z]> tree).
    assert (Hlen' : (S n <= length tree')%nat) by (subst tree'; rewrite length_insert; lia).
    wp_apply (IH (S n - (i + nat_lsb i))%nat ltac:(lia) tree' (i + nat_lsb i)%nat eq_refl ltac:(lia) Hlen' with "Hpre").
    iIntros "Hpre". unfold fenwick_add_pure.
    destruct (S n - i)%nat as [|f'] eqn:Hf; [lia|]. cbn [fenwick_add_go].
    destruct (decide (1 <= i /\ i <= n)%nat) as [_|]; [|lia].
    rewrite Hv. fold tree'.
    erewrite (fenwick_add_go_fuel_irrel (S n - (i + nat_lsb i)) f');
      [iApply "HΦ"; iExact "Hpre" | lia | lia | lia].
  - rewrite bool_decide_eq_false_2; [|lia]. wp_pures.
    unfold fenwick_add_pure. replace (S n - i)%nat with 0%nat by lia.
    cbn [fenwick_add_go]. iApply "HΦ". iExact "Hpre".
Qed.

(**md
**1-based HeapLang Fenwick point update matches pure `fenwick_add_pure`.**

Climbing `i` via `i + (i &&& -i)` up to `n` adds `delta` at every covered
Fenwick bucket in `arr` and leaves `arr` holding `fenwick_add_pure tree n i delta`.

- [arr:] base heap location of the 1-based Fenwick array.
- [tree:] initial integer model of the Fenwick array entries.
- [n:] logical 1-based capacity upper bound (`S n <= length tree`).
- [i:] starting 1-based index being updated (`1 <= i`).
- [delta:] integer delta (`+1` or `-1`) added along the Fenwick ancestor chain.
*)
Theorem fenwick_add_spec (arr : loc) (tree : list Z) (n i : nat) (delta : Z) :
  (1 <= i)%nat -> (S n <= length tree)%nat ->
  {{{ arr ↦∗ ((fun z : Z => #z) <$> tree) }}}
    fenwick_add_code #arr #(Z.of_nat n) #(Z.of_nat i) #delta
  {{{ RET #(); arr ↦∗ ((fun z : Z => #z) <$> fenwick_add_pure tree n i delta) }}}.
Proof.
  intros Hi Hlen Φ. iIntros "Hpre HΦ".
  wp_lam. wp_pures.
  wp_apply (fenwick_add_loop_spec with "Hpre"); [exact Hi|exact Hlen|].
  iExact "HΦ".
Qed.

End heap_collections.
