From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Reference Require Semantics.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.
From Ranvier.Pure Require Refinement.

Open Scope nat_scope.

(** * The saturated-write skip

    A signal write skips its notify walk while every observer of the signal is saturated, tracked by
    a graph epoch. This file models the optimised engine as its own transitions beside [Pure/Engine.v],
    sharing every helper of that file, and states what the optimisation keeps. *)

(** ** Saturation *)

(**md
A memo whose `MarkDirty` (Core.fs 4483-4504) changes nothing: `Dirty`, with no announcement due.
*)
Definition memo_saturated (s : state) (d : node) : Prop :=
  n_freshness d = Dirty /\ announce_due s d = false.

(**md
An effect whose `MarkDirty` (Core.fs 4742-4755) changes nothing: disposed, in its cleanups, or `Dirty`
and queued.
*)
Definition effect_saturated (d : node) : Prop :=
  n_disposed d = true \/ n_in_cleanup d = true \/ (n_freshness d = Dirty /\ n_queued d = true).

(**md
Node `n` takes nothing from a `Dirty` mark. A signal and an absent node take no mark.
*)
Definition saturated (s : state) (n : node_id) : Prop :=
  match g_nodes s !! n with
  | Some d => match n_kind d with
              | KMemo => memo_saturated s d | KEffect => effect_saturated d | KSignal => True end
  | None => True
  end.

(**md
The stamp of `src` holds the graph's present epoch.
*)
Definition stamp_current (s : state) (src : node_id) : Prop := g_stamps s !! src = Some (g_epoch s).

(** [c] is an item of the observers [notify_with] walks for [src]. *)
Definition observer_of (s : state) (src c : node_id) : Prop :=
  c ∈ observer_view (observers_of (g_edges s) src).

(**md
Every observer of a signal with a current stamp is saturated.
*)
Definition stamp_ok (s : state) : Prop :=
  forall src c, stamp_current s src -> observer_of s src c -> saturated s c.

(**md
`s` with the fields of the skip at their initial values.
*)
Definition strip (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_queue_head := g_queue_head s; g_flushing := g_flushing s; g_flush_owed := g_flush_owed s;
     g_batch_depth := g_batch_depth s; g_pull_depth := g_pull_depth s; g_current := g_current s;
     g_untracked := g_untracked s; g_current_owner := g_current_owner s; g_next_id := g_next_id s;
     g_trace := g_trace s; g_running := g_running s; g_ghost := g_ghost s; g_registered := g_registered s;
     g_skip := SkipOff; g_epoch := 0; g_stamps := ∅ |}.

(**md
The present engine's state `s` and the optimised engine's state `o` are equal apart from the fields of
the skip, and `o` meets `stamp_ok`.
*)
Definition sim (s o : state) : Prop :=
  strip s = strip o /\ g_skip s = SkipOff /\ g_skip o = SkipAll /\ stamp_ok o.

(** ** The optimised transitions *)

(**md
The Core.fs line of each write that can make a saturated node unsaturated.
*)
Inductive advance_point :=
  | A4191 (* memo [freshness <- Clean], [RecomputeScoped] *)
  | A4207 (* memo [freshness <- Clean], [Run] *)
  | A4290 (* memo [unannounced <- true] *)
  | A4807 (* effect [queued <- false] and [freshness <- Clean], [Execute] *)
  | A4825 (* effect [inCleanup <- false] *).

(**md
`true` when mode `m` advances the epoch at point `p`. `SkipLeaveDirty` advances only where a node leaves
`Dirty`.
*)
Definition advances (m : skip_mode) (p : advance_point) : bool :=
  match m, p with
  | SkipOff, _ => false
  | SkipAll, _ => true
  | SkipLeaveDirty, (A4191 | A4207 | A4807) => true
  | SkipLeaveDirty, (A4290 | A4825) => false
  end.

(** The epoch's advance at point [p]. *)
Definition advance (p : advance_point) : M unit :=
  m_modify (fun s => if advances (g_skip s) p then with_epoch (S (g_epoch s)) s else s).

Definition skip_on (m : skip_mode) : bool := match m with SkipOff => false | _ => true end.

(** The skip test, ahead of the walk of a signal write. At fuel [0] the write walks. *)
Definition skip_due (fuel : nat) (src : node_id) (s : state) : bool :=
  match fuel with
  | 0 => false
  | S _ => skip_on (g_skip s) && bool_decide (g_stamps s !! src = Some (g_epoch s))
  end.

(** The record at the end of a signal write's walk. *)
Definition record (src : node_id) (s : state) : state :=
  if skip_on (g_skip s) then with_stamps (<[src := g_epoch s]> (g_stamps s)) s else s.

(**md
The notify of a signal write (Core.fs 3817-3818): no walk while the stamp is current, else
`notify_dirty` followed by the record.

- [fuel:] the bound on the recursion of the marks.
- [src:] the signal written.
*)
Definition notify_write (fuel : nat) (src : node_id) (s : state) : option state :=
  if skip_due fuel src s then Some s else record src <$> notify_dirty fuel src s.

(** The clear of an observer addition (Core.fs 3775-3780). *)
Definition unstamp (src : node_id) (s : state) : state :=
  if skip_on (g_skip s) then with_stamps (delete src (g_stamps s)) s else s.

(** [sources_add self s w] takes its miss branch, which calls [AddObserver] or [TryAddObserver]. *)
Definition links (self s : node_id) (w : EdgeWorld) : bool :=
  let l := sources_of w self in
  let cur := source_cursor l in
  negb (bool_decide (fst <$> source_slots l !! cur = Some s)) &&
  negb (bool_decide (0 < cur /\ fst <$> source_slots l !! pred cur = Some s)).

(** [Graph.Track], clearing the stamp of [n] when the read adds an observer. *)
Definition otrack (n : node_id) : M unit :=
  fun s =>
    match g_current s with
    | Some c =>
      match sources_add c n (g_edges s) with
      | Some w =>
        let s' := ghost_read c n (read_prefix (g_edges s) c) (with_edges w s) in
        Some (if links c n (g_edges s) then unstamp n s' else s', tt)
      | None => None
      end
    | None => Some (s, tt)
    end.

(** [announce], with the advance of Core.fs 4290. *)
Definition oannounce (n : node_id) : M unit :=
  fun s =>
    if f_unannounced (g_flags s)
    then (let* _ := modify_node n (node_with_unannounced true) in advance A4290) s
    else Some (s, tt).

(**md
The optimised engine. Each function follows its namesake of `Pure/Engine.v` without the `o`, with
three differences: the advances, the notify of a signal write (`notify_write`), and the clear of a
tracked read (`otrack`).

- [fuel:] the bound on nested steps.
- [e:] the local variable environment of the command.
- [c:] the command run.
*)
Fixpoint oexec (fuel : nat) (e : env) (c : cmd) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    match c with
    | CRet x => on_value e x (fun v => m_return (Ok v))
    | CThrow y => m_return (Fail y)
    | CLet x v k => on_value e v (fun z => oexec f ((x, z) :: e) k)
    | CIf v a b => on_value e v (fun z => if decide (z = 0%Z) then oexec f e b else oexec f e a)
    | CRead x n k =>
      on_node e n (fun t =>
        let* o := oread f t true in
        let* _ := note_host_read t o in
        match o with
        | Ok v => oexec f ((x, v) :: e) k
        | Fail y => m_return (Fail y)
        end)
    | CTryRead x ok n k =>
      on_node e n (fun t =>
        let* o := oread f t true in
        let* _ := note_host_read t o in
        match o with
        | Ok v => oexec f ((ok, 1%Z) :: (x, v) :: e) k
        | Fail y => oexec f ((ok, 0%Z) :: (x, y) :: e) k
        end)
    | CReadUntracked x n k =>
      on_node e n (fun t =>
        let* o := oread f t false in
        let* _ := note_host_read t o in
        match o with
        | Ok v => oexec f ((x, v) :: e) k
        | Fail y => m_return (Fail y)
        end)
    | CWrite n v k =>
      on_node e n (fun t =>
        on_value e v (fun z =>
          let* _ := owrite_signal f t z in
          oexec f e k))
    | CSignal x v k =>
      on_value e v (fun z =>
        let* n := next_id in
        let* _ := add_node n (signal_node z) in
        oexec f ((x, Z.of_nat n) :: e) k)
    | CMemo x m q b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (memo_node m q b e') in
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o =>
        let* _ := oattach f n o in
        oexec f e' k
      end
    | CEffect x b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (effect_node b e') in
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o =>
        let* _ := oattach f n o in
        let* _ := m_step (mark_dirty f n) in
        let* _ := orequest_flush f in
        oexec f e' k
      end
    | CCleanup b k =>
      let* owner := current_owner in
      match owner with
      | None => m_return (Fail pure_scope_failure)
      | Some o =>
        let* x := get_owner o in
        let* _ := m_modify (ghost_registered (o_host x)) in
        let* _ :=
          if o_disposed x then
            let* _ := detached o (orun_cleanup f o b e) in
            orequest_flush f
          else put_owner o (owner_with_cleanup b e x) in
        oexec f e k
      end
    | CDispose n k =>
      on_node e n (fun t =>
        let* d := get_node t in
        match n_kind d with
        | KSignal => stuck
        | _ =>
          let* _ := odispose_node f t in
          let* _ := if n_disposed d then m_return tt else emit (EvDispose t) in
          oexec f e k
        end)
    | CBatch b k =>
      let* _ := m_modify (fun s => with_batch_depth (S (g_batch_depth s)) s) in
      let* r := oexec f e b in
      let* _ := m_modify (fun s => with_batch_depth (pred (g_batch_depth s)) s) in
      match r with
      | Ok _ =>
        let* _ := orequest_flush f in
        oexec f e k
      | Fail y => m_return (Fail y)
      end
    | CLog v k =>
      on_value e v (fun z =>
        let* l := log_label in
        let* _ := emit (EvLog l z) in
        oexec f e k)
    end
  end

with oread (fuel : nat) (n : node_id) (tracked : bool) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f => if tracked then oread_node f n else untracked (oread_node f n)
  end

with oread_node (fuel : nat) (n : node_id) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      let* _ := otrack n in
      m_return (Ok (n_value d))
    | KEffect => stuck
    | KMemo =>
      let* _ :=
        if stale_live d then
          let* _ := orefresh_or_pull f n in
          let* _ := otrack n in
          let* d' := get_node n in
          if stale_live d' then mark_reader f else m_return tt
        else otrack n in
      let* d' := get_node n in
      match n_status d' with
      | StError => m_return (Fail (default 0%Z (n_failure d')))
      | _ => m_return (Ok (n_value d'))
      end
    end
  end

with oupdate_if_necessary (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KMemo => if stale_live d then orefresh_or_pull f n else m_return tt
    | _ => m_return tt
    end
  end

with orefresh_or_pull (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if deferring s then orefresh f n else opull f n
  end

with opull (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := m_modify (fun s => with_pull_depth (S (g_pull_depth s)) s) in
    let* _ := orefresh f n in
    let* _ := m_modify (fun s => with_pull_depth (pred (g_pull_depth s)) s) in
    let* s := m_get in
    if bool_decide (g_pull_depth s = 0%nat) && g_flush_owed s then osettle_flush f else m_return tt
  end

with orefresh (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* _ :=
      if decide (n_freshness d = Check) then
        let* _ := oresolve_walk f n 0 in
        let* d' := get_node n in
        if decide (n_freshness d' = Check)
        then
          let* _ := m_modify (ghost_resolved n) in
          modify_node n (fun x => node_with_unannounced false (node_with_freshness Clean x))
        else m_return tt
      else m_return tt in
    let* d' := get_node n in
    if decide (n_freshness d' = Dirty) then orecompute f n else m_return tt
  end

with oresolve_walk (fuel : nat) (n : node_id) (i : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* s := m_get in
    if decide (n_freshness d = Check) then
      match source_slots (sources_of (g_edges s) n) !! i with
      | Some (src, _) =>
        let* _ := oupdate_if_necessary f src in
        oresolve_walk f n (S i)
      | None => m_return tt
      end
    else m_return tt
  end

with orecompute (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_scope d with
    | None => orun_memo f n
    | Some o =>
      let* _ := modify_node n (node_with_freshness Clean) in
      let* _ := advance A4191 in
      let* _ := m_modify (ghost_dirty_cleared n (n_runs d)) in
      let* _ := odischarge f o in
      let* d' := get_node n in
      if n_disposed d' then m_return tt
      else if decide (n_runs d' = n_runs d) then orun_memo f n
      else if decide (n_freshness d' = Dirty) then orecompute f n
      else m_return tt
    end
  end

with orun_memo (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let previous := n_value d in
    let previous_status := n_status d in
    let previous_failure := n_failure d in
    let* _ := modify_node n (fun x =>
      node_with_runs (S (n_runs x)) (node_with_failure None (node_with_status StNone
        (node_with_unannounced false (node_with_freshness Clean x))))) in
    let* _ := advance A4207 in
    let* _ := edges (sources_begin_run n) in
    let* _ := m_modify (ghost_run_start n) in
    let* _ := push_running n in
    let body_env :=
      if n_published d then (prev_var, previous) :: n_env d else without_prev (n_env d) in
    let* r := hosted n (oexec f body_env (n_body d)) in
    let* d1 := get_node n in
    let result := if n_violated d1 then Fail pure_scope_failure else r in
    let* _ :=
      match result with
      | Ok v => modify_node n (fun x => node_with_published true (node_with_value v x))
      | Fail _ => m_return tt
      end in
    let* _ := edges_opt (if n_disposed d1 then sources_clear n else sources_end_run n) in
    let* _ := pop_running in
    let* _ :=
      match result with
      | Ok _ => m_return tt
      | Fail y => modify_node n (fun x =>
          node_with_violated false (node_with_status StError (node_with_failure (Some y) x)))
      end in
    let* d2 := get_node n in
    let* moved :=
      if decide (n_status d2 <> previous_status \/ n_failure d2 <> previous_failure) then m_return true
      else
        match n_comparer d2 with
        | CmpEq => m_return (bool_decide (n_value d2 <> previous))
        | CmpNever => m_return true
        | CmpThrows y =>
          let* _ := modify_node n (fun x =>
            node_with_status StError (node_with_failure (Some y) (node_with_value previous x))) in
          m_return true
        end in
    let* s := m_get in
    let* _ := m_modify (ghost_run_end n (g_current s) moved) in
    let* _ := if moved then m_step (notify_dirty_except f n (g_current s)) else m_return tt in
    let* d3 := get_node n in
    if decide (n_freshness d3 = Clean) then m_return tt else oannounce n
  end

with oexecute_effect (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then modify_node n (node_with_queued false)
    else
      let* _ :=
        if decide (n_freshness d = Check) then
          let* _ := oresolve_walk f n 0 in
          let* d' := get_node n in
          if decide (n_freshness d' = Check)
          then
            let* _ := m_modify (ghost_resolved n) in
            modify_node n (node_with_freshness Clean)
          else m_return tt
        else m_return tt in
      let* d' := get_node n in
      let* _ := modify_node n (fun x => node_with_freshness Clean (node_with_queued false x)) in
      let* _ := advance A4807 in
      if decide (n_freshness d' = Dirty) then orun_effect f n else m_return tt
  end

with orun_effect (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := push_running n in
    let* _ := edges (sources_begin_run n) in
    let* _ := m_modify (ghost_effect_start n) in
    let* d := get_node n in
    let* _ :=
      match n_scope d with
      | Some o =>
        let* _ := modify_node n (node_with_in_cleanup true) in
        let* _ := odischarge f o in
        let* _ := modify_node n (node_with_in_cleanup false) in
        advance A4825
      | None => m_return tt
      end in
    let* d' := get_node n in
    let* _ := if n_disposed d' then m_return tt else orun_effect_body f n in
    pop_running
  end

with orun_effect_body (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* _ := modify_node n (fun x => node_with_runs (S (n_runs x)) (node_with_failure None (node_with_status StNone x))) in
    let* _ := emit (EvRun n) in
    let* r := hosted n (oexec f (n_env d) (n_body d)) in
    let* d' := get_node n in
    let* _ := edges_opt (if n_disposed d' then sources_clear n else sources_end_run n) in
    match r with
    | Ok _ => m_return tt
    | Fail y =>
      let* _ := modify_node n (fun x => node_with_status StError (node_with_failure (Some y) x)) in
      emit (EvFail n y)
    end
  end

with odischarge (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (odispose_scope f o) in
    let* s := m_get in
    if bool_decide (g_batch_depth s = 0%nat) && negb (g_flushing s) && live_queue s
    then m_modify (with_flush_owed true)
    else m_return tt
  end

with oretire (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (
      let* x := get_owner o in
      if o_disposed x then m_return tt
      else
        let* _ := put_owner o (owner_retired x) in
        odispose_scope f o) in
    let* s := m_get in
    if live_queue s then orequest_flush f else m_return tt
  end

with odispose_scope (fuel : nat) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := put_owner o (owner_detached x) in
    oteardown f o (rev (o_children x)) (owner_cleanups x)
  end

with oteardown (fuel : nat) (o : owner_id) (ls : list link) (cs : list (cmd * env * nat)) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    match ls, cs with
    | [], [] => m_return tt
    | l :: ls', [] =>
      let* _ := orelease f (l_child l) in
      oteardown f o ls' []
    | [], (b, e, _) :: cs' =>
      let* _ := orun_cleanup f o b e in
      oteardown f o [] cs'
    | l :: ls', (b, e, a) :: cs' =>
      if decide (a < l_acquired l)%nat then
        let* _ := orelease f (l_child l) in
        oteardown f o ls' cs
      else
        let* _ := orun_cleanup f o b e in
        oteardown f o ls cs'
    end
  end

with orun_cleanup (fuel : nat) (o : owner_id) (b : cmd) (e : env) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := emit (EvCleanup (o_host x)) in
    let* r := oexec f e b in
    match r with
    | Ok _ => m_return tt
    | Fail y =>
      let* _ := record_error o y in
      emit (EvFail (o_host x) y)
    end
  end

with orelease (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := modify_node n (node_with_link None) in
    odispose_node f n
  end

with odispose_node (fuel : nat) (n : node_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then m_return tt
    else
      let* _ := modify_node n (fun x => node_with_link None (node_with_disposed true x)) in
      let* _ := match n_link d with Some l => unlink l | None => m_return tt end in
      let* _ := edges_opt (sources_clear n) in
      match n_scope d with
      | Some o => oretire f o
      | None => m_return tt
      end
  end

with oattach (fuel : nat) (n : node_id) (o : owner_id) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    if o_disposed x then
      let* _ := orelease f n in
      modify_node n (node_with_link (Some {| l_child := n; l_owner := o; l_generation := None; l_acquired := 0 |}))
    else
      let l := {| l_child := n; l_owner := o; l_generation := Some (o_generation x); l_acquired := o_acquisitions x |} in
      let* _ := put_owner o (owner_with_child l x) in
      modify_node n (node_with_link (Some l))
  end

with owrite_signal (fuel : nat) (n : node_id) (v : val) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      if decide (n_value d = v) then m_return tt
      else
        let* _ := modify_node n (node_with_value v) in
        let* _ := m_modify (ghost_publish n) in
        let* _ := m_step (notify_write f n) in
        orequest_flush f
    | _ => stuck
    end
  end

with orequest_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if live_queue s && bool_decide (g_batch_depth s = 0%nat) then
      if outside_bodies s then orun_flush f
      else if g_flushing s then m_return tt
      else m_modify (with_flush_owed true)
    else m_return tt
  end

with osettle_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_flush_owed s && bool_decide (g_batch_depth s = 0%nat) && outside_bodies s then orun_flush f else m_return tt
  end

with orun_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_flushing s then m_return tt
    else
      let* _ := m_modify (fun s => with_flush_owed false (with_flushing true s)) in
      let* _ := odrain f in
      m_modify (with_flushing false)
  end

with odrain (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    match g_queue s !! g_queue_head s with
    | Some n =>
      let* _ := m_modify (with_queue_head (S (g_queue_head s))) in
      let* _ := oexecute_effect f n in
      odrain f
    | None => m_modify (fun s => with_queue [] (with_queue_head 0 s))
    end
  end.

(**md
The event trace of running `c` on a new graph in skip mode `m`, or `None` when the run is stuck or runs
out of fuel.

- [fuel:] the bound on nested steps.
- [fl:] the flags of the new graph.
- [m:] the skip mode.
- [c:] the command run.
*)
Definition trace_of_mode (fuel : nat) (fl : flags) (m : skip_mode) (c : cmd) : option (list event) :=
  match oexec fuel [] c (with_skip m (init fl)) with
  | Some (s, _) => Some (g_trace s)
  | None => None
  end.

(**md
The engine in skip mode `m` as a reference engine, as `engine_of` is for the present engine.
*)
Definition engine_of_mode (m : skip_mode) (k : nat) (p : Semantics.program) (h : Semantics.history)
    : option (list event) :=
  Refinement.project p <$> trace_of_mode k current_flags m (Refinement.erase p h).

(**md
The optimised engine as a reference engine.
*)
Definition engine_of_optimised (k : nat) (p : Semantics.program) (h : Semantics.history) : option (list event) :=
  Refinement.project p <$> trace_of_mode k current_flags SkipAll (Refinement.erase p h).

(** ** Under [SkipOff] the optimised engine is the present engine *)

(** [m] leaves the mode [SkipOff]. *)
Definition keeps_off {A} (m : M A) : Prop :=
  forall s s' a, g_skip s = SkipOff -> m s = Some (s', a) -> g_skip s' = SkipOff.

(** [m1] and [m2] agree from every state in mode [SkipOff], and [m1] stays there. *)
Definition eqM {A} (m1 m2 : M A) : Prop :=
  (forall s, g_skip s = SkipOff -> m1 s = m2 s) /\ keeps_off m1.

Lemma eqM_bind {A B} (m1 m2 : M A) (k1 k2 : A -> M B) :
  eqM m1 m2 -> (forall a, eqM (k1 a) (k2 a)) -> eqM (m_bind m1 k1) (m_bind m2 k2).
Proof.
  intros [E1 K1] Hk. split.
  - intros s Hs. unfold m_bind. rewrite <- (E1 s Hs).
    destruct (m1 s) as [[s1 a]|] eqn:Em; [|done].
    apply (proj1 (Hk a)). by eapply K1.
  - intros s s' b Hs Hm. unfold m_bind in Hm. destruct (m1 s) as [[s1 a]|] eqn:Em; [|done].
    eapply (proj2 (Hk a)); [|exact Hm]. by eapply K1.
Qed.

Lemma eqM_refl {A} (m : M A) : keeps_off m -> eqM m m.
Proof. intros K. split; [done|exact K]. Qed.

(** [g] leaves the mode as it is. *)
Definition skip_fixed (g : state -> state) : Prop := forall s, g_skip (g s) = g_skip s.

Lemma keeps_return {A} (a : A) : keeps_off (m_return a).
Proof. intros s s' b Hs Hm. unfold m_return in Hm. by simplify_eq. Qed.

Lemma keeps_stuck {A} : keeps_off (@stuck A).
Proof. by intros ????. Qed.

Lemma keeps_get : keeps_off m_get.
Proof. intros s s' b Hs Hm. unfold m_get in Hm. by simplify_eq. Qed.

Lemma keeps_modify g : skip_fixed g -> keeps_off (m_modify g).
Proof. intros Hg s s' b Hs Hm. unfold m_modify in Hm. simplify_eq. by rewrite Hg. Qed.

Lemma keeps_step (g : state -> option state) :
  (forall s s', g s = Some s' -> g_skip s' = g_skip s) -> keeps_off (m_step g).
Proof.
  intros Hg s s' b Hs Hm. unfold m_step in Hm. destruct (g s) as [s1|] eqn:E; [|done].
  simplify_eq. by rewrite (Hg _ _ E).
Qed.

Lemma keeps_get_node n : keeps_off (get_node n).
Proof. intros s s' b Hs Hm. unfold get_node in Hm. destruct (g_nodes s !! n); by simplify_eq. Qed.

Lemma keeps_get_owner o : keeps_off (get_owner o).
Proof. intros s s' b Hs Hm. unfold get_owner in Hm. destruct (g_owners s !! o); by simplify_eq. Qed.

Lemma keeps_modify_node n u : keeps_off (modify_node n u).
Proof. intros s s' b Hs Hm. unfold modify_node in Hm. destruct (g_nodes s !! n); by simplify_eq. Qed.

Lemma keeps_edges_opt u : keeps_off (edges_opt u).
Proof. intros s s' b Hs Hm. unfold edges_opt in Hm. destruct (u (g_edges s)); by simplify_eq. Qed.

Lemma keeps_next_id : keeps_off next_id.
Proof. intros s s' b Hs Hm. unfold next_id in Hm. by simplify_eq. Qed.

Lemma keeps_log_label : keeps_off log_label.
Proof. intros s s' b Hs Hm. unfold log_label in Hm. repeat case_match; by simplify_eq. Qed.

Lemma keeps_note n o : keeps_off (note_host_read n o).
Proof.
  intros s s' b Hs Hm. unfold note_host_read in Hm. repeat case_match; unfold emit, m_modify in *; by simplify_eq.
Qed.

Lemma keeps_current_owner : keeps_off current_owner.
Proof. intros s s' b Hs Hm. unfold current_owner in Hm. repeat case_match; by simplify_eq. Qed.

Lemma keeps_track n : keeps_off (track n).
Proof.
  intros s s' b Hs Hm. unfold track in Hm. repeat case_match; simplify_eq; [|done].
  unfold ghost_read. case_match; done.
Qed.

Lemma prim_skip s s' : mark_prim s s' -> g_skip s' = g_skip s.
Proof. by intros []. Qed.

Lemma marks_skip s s' : rtc mark_prim s s' -> g_skip s' = g_skip s.
Proof. induction 1 as [|s1 s2 s3 Hp _ IH]; [done|]. rewrite IH. by apply prim_skip. Qed.

Lemma any_mark_skip f n r s s' : any_mark f n r s s' -> g_skip s' = g_skip s.
Proof. intros H. by eapply marks_skip, any_mark_rtc. Qed.

Lemma keeps_mark_reader f : keeps_off (mark_reader f).
Proof.
  intros s s' b Hs Hm. unfold mark_reader in Hm.
  destruct (g_current s) as [c|]; [|by simplify_eq].
  destruct (f_reader_mark _); [|by simplify_eq].
  destruct (mark_check f c s) as [s1|] eqn:E; [|done]. simplify_eq.
  rewrite <- Hs. eapply (any_mark_skip f c None). left. exact E.
Qed.

Lemma keeps_announce n : keeps_off (announce n).
Proof.
  intros s s' b Hs Hm. unfold announce in Hm. case_match; [|by simplify_eq]. by eapply keeps_modify_node.
Qed.

Lemma eqM_on_value e x (k1 k2 : val -> M outcome) :
  (forall v, eqM (k1 v) (k2 v)) -> eqM (on_value e x k1) (on_value e x k2).
Proof.
  intros Hk. split.
  - intros s Hs. unfold on_value. destruct (eval_expr _ _ _) as [[v|y]|]; [by apply (proj1 (Hk v))|done|done].
  - intros s s' b Hs Hm. unfold on_value in Hm. destruct (eval_expr _ _ _) as [[v|y]|]; [|by simplify_eq|done].
    by eapply (proj2 (Hk v)).
Qed.

Lemma eqM_on_node e x (k1 k2 : node_id -> M outcome) :
  (forall t, eqM (k1 t) (k2 t)) -> eqM (on_node e x k1) (on_node e x k2).
Proof.
  intros Hk. apply eqM_on_value. intros z. destruct (decide _); [apply Hk|apply eqM_refl, keeps_stuck].
Qed.

Local Ltac wrap_tac E K :=
  split;
  [ intros s Hs; unfold hosted, untracked, detached, m_bind, m_modify, m_return; simpl;
    rewrite E; [reflexivity|exact Hs]
  | intros s s' a Hs Hm; unfold hosted, untracked, detached, m_bind, m_modify, m_return in Hm; simpl in Hm;
    match type of Hm with context [match ?b ?x with _ => _ end] =>
      destruct (b x) as [[s1 r]|] eqn:?; [|done] end;
    simplify_eq; simpl; eapply K; [|eassumption]; exact Hs ].

Lemma eqM_hosted {A} n (b1 b2 : M A) : eqM b1 b2 -> eqM (hosted n b1) (hosted n b2).
Proof. intros [E K]. wrap_tac E K. Qed.

Lemma eqM_untracked {A} (b1 b2 : M A) : eqM b1 b2 -> eqM (untracked b1) (untracked b2).
Proof. intros [E K]. wrap_tac E K. Qed.

Lemma eqM_detached o (b1 b2 : M unit) : eqM b1 b2 -> eqM (detached o b1) (detached o b2).
Proof. intros [E K]. wrap_tac E K. Qed.

Lemma eqM_advance_r {A} (m : M A) p (k : unit -> M A) :
  eqM m (k tt) -> eqM m (m_bind (advance p) k).
Proof.
  intros [E K]. split; [|done]. intros s Hs. unfold m_bind, advance, m_modify. rewrite Hs. simpl. by apply E.
Qed.

Lemma eqM_then_advance (m : M unit) p : keeps_off m -> eqM m (m_bind m (fun _ => advance p)).
Proof.
  intros K. split; [|done]. intros s Hs. unfold m_bind, advance, m_modify.
  destruct (m s) as [[s1 []]|] eqn:E; [|done]. by rewrite (K _ _ _ Hs E).
Qed.

Lemma eqM_track n : eqM (track n) (otrack n).
Proof.
  split; [|apply keeps_track]. intros s Hs. unfold track, otrack.
  destruct (g_current s) as [c|]; [|done]. destruct (sources_add _ _ _) as [w|]; [|done].
  assert (Hg : g_skip (ghost_read c n (read_prefix (g_edges s) c) (with_edges w s)) = SkipOff).
  { unfold ghost_read. case_match; done. }
  destruct (links _ _ _); [|done]. unfold unstamp. by rewrite Hg.
Qed.

Lemma eqM_announce n : eqM (announce n) (oannounce n).
Proof.
  split; [|apply keeps_announce]. intros s Hs. unfold announce, oannounce.
  destruct (f_unannounced _); [|done]. unfold m_bind, advance, m_modify, modify_node.
  destruct (g_nodes s !! n); [|done]. simpl. by rewrite Hs.
Qed.

Lemma eqM_notify_write f n : eqM (m_step (notify_dirty f n)) (m_step (notify_write f n)).
Proof.
  split.
  - intros s Hs. unfold m_step, notify_write.
    assert (Hd : skip_due f n s = false) by (unfold skip_due; rewrite Hs; by destruct f).
    rewrite Hd. destruct (notify_dirty f n s) as [s'|] eqn:E; [|done]. simpl.
    unfold record. rewrite (any_mark_skip f n None s s' ltac:(right; right; right; left; exact E)), Hs. done.
  - apply keeps_step. intros s s' E. eapply (any_mark_skip f n None). right; right; right; left. exact E.
Qed.

(** One record per mutual function: the optimised function at fuel [f] agrees with its namesake from
    every state in mode [SkipOff]. *)
Record osims (f : nat) : Prop := {
  Oexec : forall e c, eqM (exec f e c) (oexec f e c);
  Oread : forall n b, eqM (read f n b) (oread f n b);
  Oread_node : forall n, eqM (read_node f n) (oread_node f n);
  Oupdate : forall n, eqM (update_if_necessary f n) (oupdate_if_necessary f n);
  Orefresh_or_pull : forall n, eqM (refresh_or_pull f n) (orefresh_or_pull f n);
  Opull : forall n, eqM (pull f n) (opull f n);
  Orefresh : forall n, eqM (refresh f n) (orefresh f n);
  Owalk : forall n i, eqM (resolve_walk f n i) (oresolve_walk f n i);
  Orecompute : forall n, eqM (recompute f n) (orecompute f n);
  Orun_memo : forall n, eqM (run_memo f n) (orun_memo f n);
  Oexecute : forall n, eqM (execute_effect f n) (oexecute_effect f n);
  Orun_effect : forall n, eqM (run_effect f n) (orun_effect f n);
  Obody : forall n, eqM (run_effect_body f n) (orun_effect_body f n);
  Odischarge : forall o, eqM (discharge f o) (odischarge f o);
  Oretire : forall o, eqM (retire f o) (oretire f o);
  Odispose_scope : forall o, eqM (dispose_scope f o) (odispose_scope f o);
  Oteardown : forall o ls cs, eqM (teardown f o ls cs) (oteardown f o ls cs);
  Ocleanup : forall o b e, eqM (run_cleanup f o b e) (orun_cleanup f o b e);
  Orelease : forall n, eqM (release f n) (orelease f n);
  Odispose : forall n, eqM (dispose_node f n) (odispose_node f n);
  Oattach : forall n o, eqM (attach f n o) (oattach f n o);
  Owrite : forall n v, eqM (write_signal f n v) (owrite_signal f n v);
  Orequest : eqM (request_flush f) (orequest_flush f);
  Osettle : eqM (settle_flush f) (osettle_flush f);
  Oflush : eqM (run_flush f) (orun_flush f);
  Odrain : eqM (drain f) (odrain f)
}.

Local Ltac skip_fixed_tac :=
  intros ?; cbv beta;
  repeat (unfold ghost_read, ghost_publish, ghost_run_start, ghost_effect_start, ghost_run_end,
          ghost_dirty_cleared, ghost_resolved, ghost_registered);
  repeat case_match; reflexivity.

Local Ltac oih IH :=
  first
    [ apply (Oexec _ IH) | apply (Oread _ IH) | apply (Oread_node _ IH) | apply (Oupdate _ IH)
    | apply (Orefresh_or_pull _ IH) | apply (Opull _ IH) | apply (Orefresh _ IH) | apply (Owalk _ IH)
    | apply (Orecompute _ IH) | apply (Orun_memo _ IH) | apply (Oexecute _ IH) | apply (Orun_effect _ IH)
    | apply (Obody _ IH) | apply (Odischarge _ IH) | apply (Oretire _ IH)
    | apply (Odispose_scope _ IH) | apply (Oteardown _ IH) | apply (Ocleanup _ IH)
    | apply (Orelease _ IH) | apply (Odispose _ IH) | apply (Oattach _ IH) | apply (Owrite _ IH)
    | apply (Orequest _ IH) | apply (Osettle _ IH) | apply (Oflush _ IH) | apply (Odrain _ IH) ].

Local Ltac oleaf :=
  apply eqM_refl;
  first
    [ apply keeps_return | apply keeps_stuck | apply keeps_get | apply keeps_get_node | apply keeps_get_owner
    | apply keeps_modify_node | apply keeps_edges_opt | apply keeps_next_id | apply keeps_log_label
    | apply keeps_note | apply keeps_current_owner | apply keeps_mark_reader
    | (apply keeps_step; intros ?? E; eapply (any_mark_skip _ _ None); right; left; exact E)
    | (apply keeps_step; intros ?? E; eapply any_mark_skip; right; right; right; right; exact E)
    | (apply keeps_modify; skip_fixed_tac) ].

Local Ltac ogo1 IH :=
  first
    [ match goal with |- eqM _ (m_bind (advance _) _) => apply eqM_advance_r end
    | match goal with |- eqM ?m (m_bind ?m (fun _ => advance _)) => apply eqM_then_advance; apply keeps_modify_node end
    | apply eqM_track | apply eqM_announce | apply eqM_notify_write
    | apply eqM_on_node; intros ? | apply eqM_on_value; intros ?
    | apply eqM_hosted | apply eqM_untracked | apply eqM_detached
    | oih IH
    | apply eqM_bind; [|intros ?]
    | match goal with |- eqM (match ?x with _ => _ end) _ => destruct x end
    | match goal with |- eqM (if ?x then _ else _) _ => destruct x end
    | oleaf
    | progress cbv beta ].

Local Ltac ogo IH := repeat ogo1 IH.

Lemma osims_0 : osims 0.
Proof. split; intros; apply eqM_refl, keeps_stuck. Qed.

Lemma osims_S f : osims f -> osims (S f).
Proof.
  intros IH. split; intros.
  all: lazymatch goal with
       | |- eqM (exec _ _ ?c) _ => destruct c; cbn [exec oexec]
       | |- eqM (teardown _ _ ?ls ?cs) _ => destruct ls as [|? ?], cs as [|[[? ?] ?] ?]; cbn [teardown oteardown]
       | |- _ => cbn [read oread read_node oread_node update_if_necessary oupdate_if_necessary
                      refresh_or_pull orefresh_or_pull pull opull refresh orefresh resolve_walk oresolve_walk
                      recompute orecompute run_memo orun_memo execute_effect oexecute_effect run_effect orun_effect
                      run_effect_body orun_effect_body discharge odischarge retire oretire dispose_scope odispose_scope
                      run_cleanup orun_cleanup release orelease dispose_node odispose_node attach oattach
                      write_signal owrite_signal request_flush orequest_flush settle_flush osettle_flush
                      run_flush orun_flush drain odrain]
       end.
  all: ogo IH.
Qed.

Lemma osims_all f : osims f.
Proof. induction f; [apply osims_0|by apply osims_S]. Qed.

Lemma with_skip_off_init fl : with_skip SkipOff (init fl) = init fl.
Proof. reflexivity. Qed.

(**md
**Under `SkipOff` the optimised engine is the present engine.**

The trace of `trace_of_mode` in mode `SkipOff` is the trace of `trace_of`, at every fuel and flags.

- [k:] the fuel.
- [fl:] the flags.
- [c:] the program.
*)
Theorem trace_of_mode_off k fl c : trace_of_mode k fl SkipOff c = trace_of k fl c.
Proof.
  unfold trace_of_mode, trace_of, run_top. rewrite with_skip_off_init.
  by rewrite <- (proj1 (Oexec _ (osims_all k) [] c) (init fl) eq_refl).
Qed.

(**md
**Under `SkipOff` the optimised reference engine is the present one.**

- [k:] the fuel.
- [p:] the program.
- [h:] the history.
*)
Corollary engine_of_mode_off k p h : engine_of_mode SkipOff k p h = Refinement.engine_of current_flags k p h.
Proof. unfold engine_of_mode, Refinement.engine_of. by rewrite trace_of_mode_off. Qed.

(** ** Saturation lemmas *)

Global Instance stamp_current_dec s src : Decision (stamp_current s src).
Proof. unfold stamp_current. apply _. Defined.

Global Instance observer_of_dec s src c : Decision (observer_of s src c).
Proof. unfold observer_of. apply _. Defined.

Global Instance saturated_dec s n : Decision (saturated s n).
Proof. unfold saturated, memo_saturated, effect_saturated. repeat case_match; apply _. Defined.

Lemma set_node_same n d s : g_nodes s !! n = Some d -> set_node n d s = s.
Proof. intros Hd. destruct s; unfold set_node; simpl in *. f_equal. by apply insert_id. Qed.

(**md
**A `Dirty` mark of a saturated node changes nothing.**

- [s:] the state.
- [c:] the saturated node.
- [f:] the fuel of the mark, less one.
*)
Theorem mark_dirty_saturated_id s c f : saturated s c -> mark_dirty (S f) c s = Some s.
Proof.
  unfold saturated, mark_dirty. destruct (g_nodes s !! c) as [d|] eqn:Hd; [|done].
  destruct (n_kind d) eqn:Hk; [done| |].
  - intros [Hf Hu]. rewrite decide_False by (rewrite Hf; discriminate).
    rewrite Hu, decide_True by exact Hf. done.
  - intros Hs. unfold effect_marked. f_equal.
    destruct (n_disposed d) eqn:Hdi; [done|]. destruct (n_in_cleanup d) eqn:Hc; [done|]. simpl.
    destruct Hs as [?|[?|[Hf Hq]]]; [congruence|congruence|]. rewrite Hq.
    replace (node_marked Dirty (n_unannounced d) true d) with d; [by apply set_node_same|].
    destruct d; simpl in *; by subst.
Qed.

Lemma prim_keeps_saturated s s' c : mark_prim s s' -> saturated s c -> saturated s' c.
Proof.
  intros Hp. unfold saturated.
  destruct Hp as [s n d f u Hd Hk Hf Hu|s n d f Hd Hk Hq Hf|s n d f Hd Hk Hq Hf]; simpl.
  all: destruct (decide (c = n)) as [->|Hne];
    [rewrite lookup_insert_eq, Hd; simpl; rewrite Hk|rewrite lookup_insert_ne by done].
  all: try (destruct (g_nodes s !! c) as [e|]; [|done]; destruct (n_kind e); unfold memo_saturated, announce_due; simpl; auto).
  - unfold memo_saturated, announce_due. simpl. intros [H1 H2]. split.
    + rewrite H1 in Hf. destruct f; unfold freshness_le in Hf; simpl in Hf; [lia|lia|done].
    + destruct Hu as [->| ->]; [exact H2|by rewrite andb_false_r].
  - unfold effect_saturated. simpl. intros [H|[H|[H1 H2]]]; [by left|by right; left|right; right; split; [|done]].
    rewrite H1 in Hf. destruct f; unfold freshness_le in Hf; simpl in Hf; [lia|lia|done].
  - unfold effect_saturated. simpl. intros [H|[H|[H1 H2]]]; [by left|by right; left|right; right; split; [|done]].
    rewrite H1 in Hf. destruct f; unfold freshness_le in Hf; simpl in Hf; [lia|lia|done].
Qed.

Lemma rtc_keeps_saturated s s' c : rtc mark_prim s s' -> saturated s c -> saturated s' c.
Proof. induction 1; eauto using prim_keeps_saturated. Qed.

(**md
**Marks keep every saturated node saturated.**

- [f:] the fuel.
- [n:] the node or source marked.
- [r:] the current computation skipped by `notify_dirty_except`, or `None`.
- [s:] the state before the marking.
- [s':] the state after the marking.
- [c:] a node saturated in `s`.
*)
Theorem marks_keep_saturated f n r s s' c : any_mark f n r s s' -> saturated s c -> saturated s' c.
Proof. intros H. apply rtc_keeps_saturated. by eapply any_mark_rtc. Qed.

Lemma walk_saturated_id f src i s :
  (forall c, observer_of s src c -> saturated s c) -> walk_marks (mark_dirty (S f)) src i s = Some s.
Proof.
  intros Hs. induction i as [|i IH]; cbn [walk_marks]; [done|].
  destruct (decide _) as [_|Hlt]; [exact IH|].
  destruct (observer_items (observers_of (g_edges s) src) !! i) as [[c slot]|] eqn:Hc.
  - rewrite mark_dirty_saturated_id; [exact IH|]. apply Hs. unfold observer_of. apply view_elem. eauto.
  - destruct (lookup_lt_is_Some_2 (observer_items (observers_of (g_edges s) src)) i) as [x Hx]; [lia|]. congruence.
Qed.

(**md
**A stamped signal's walk changes nothing.**

Under `stamp_ok`, the `notify_dirty` of a signal with a current stamp returns its starting state.

- [s:] the state.
- [src:] the signal.
- [f:] the fuel of the walk, less one.
*)
Theorem notify_dirty_stamped_id s src f : stamp_ok s -> stamp_current s src -> notify_dirty (S f) src s = Some s.
Proof. intros Hok Hc. apply walk_saturated_id. intros c Ho. by apply (Hok src c). Qed.

(** ** The record is unsound inside [RecomputeScoped]

    [RecomputeScoped] (Core.fs 4191) writes [freshness <- Clean] and leaves [unannounced] as it is. A
    memo that is [Clean] with [unannounced] set takes [Dirty] from [MarkDirty] (4483-4498) and keeps
    the flag, so it ends unsaturated: the record after the walk claims a saturation the memo lacks. *)

(** A memo in the window of [RecomputeScoped]: [Clean], holding an announcement. *)
Definition window_memo : node := node_marked Clean true false (memo_node Owning CmpEq (CRet (EConst 0%Z)) []).

(** Memo 2 observes signal 1. *)
Definition window_world : EdgeWorld := default empty_world (sources_add 2 1 (sources_begin_run 2 empty_world)).

(** Signal 1 observed by [window_memo], under [SkipAll] with no stamp. *)
Definition window_state : state :=
  {| g_flags := current_flags; g_edges := window_world;
     g_nodes := <[1 := signal_node 0%Z]> {[2 := window_memo]};
     g_owners := ∅; g_queue := []; g_queue_head := 0; g_flushing := false; g_flush_owed := false;
     g_batch_depth := 0; g_pull_depth := 0; g_current := None; g_untracked := false; g_current_owner := None;
     g_next_id := 2; g_trace := []; g_running := []; g_ghost := empty_ghost; g_registered := ∅;
     g_skip := SkipAll; g_epoch := 3; g_stamps := ∅ |}.

Lemma window_mark : (fun s' => bool_decide (saturated s' 2)) <$> mark_dirty 10 2 window_state = Some false.
Proof. vm_compute. reflexivity. Qed.

(**md
**A `Dirty` mark leaves a clean memo holding an announcement unsaturated.**

`mark_dirty_saturates`, a lemma of the plan for the record's soundness, is false.
*)
Theorem mark_dirty_saturates_falsified : ~ (forall f c s s', mark_dirty f c s = Some s' -> saturated s' c).
Proof.
  intros H. pose proof window_mark as W.
  destruct (mark_dirty 10 2 window_state) as [s'|] eqn:E; [|done]. simpl in W. injection W as W.
  apply bool_decide_eq_false_1 in W. exact (W (H _ _ _ _ E)).
Qed.

Lemma window_facts :
  (fun s' => (g_skip s', bool_decide (stamp_current s' 1), bool_decide (observer_of s' 1 2),
              bool_decide (saturated s' 2), skip_due 10 1 s', (fun d => n_unannounced d) <$> g_nodes s' !! 2,
              (fun x => n_unannounced <$> g_nodes x !! 2) <$> notify_dirty 10 1 s'))
    <$> notify_write 10 1 window_state
  = Some (SkipAll, true, true, false, true, Some true, Some (Some false)).
Proof. vm_compute. reflexivity. Qed.

(**md
**A write to a signal observed by a clean memo holding an announcement records an unsound stamp.**

From `window_state`, the walk of a write records the signal's stamp, and leaves its observer, memo 2,
unsaturated: `stamp_ok` fails. The next write skips, where the present engine's walk spends memo 2's
announcement, so the two engines' states differ.
*)
Theorem saturated_record_unsound :
  exists s', notify_write 10 1 window_state = Some s' /\ g_skip s' = SkipAll /\ stamp_current s' 1 /\
    observer_of s' 1 2 /\ ~ saturated s' 2 /\ ~ stamp_ok s' /\
    notify_write 10 1 s' = Some s' /\ notify_dirty 10 1 s' <> Some s'.
Proof.
  pose proof window_facts as W.
  destruct (notify_write 10 1 window_state) as [s'|] eqn:E; [|done]. simpl in W.
  destruct (g_nodes s' !! 2) as [d|] eqn:Hd; [|done].
  destruct (notify_dirty 10 1 s') as [x|] eqn:Ex; [|done]. simpl in W.
  injection W as Hsk Hst Hob Hsat Hdue Hu Hx.
  apply bool_decide_eq_true_1 in Hst, Hob. apply bool_decide_eq_false_1 in Hsat.
  exists s'. split_and!; try done.
  - intros Hok. apply Hsat. by apply (Hok 1).
  - assert (Hd2 : skip_due 10 1 s' = true) by exact Hdue. unfold notify_write. by rewrite Hd2.
  - intros Heq. rewrite Ex in Heq. injection Heq as Heq. subst x. rewrite Hd in Hx. simpl in Hx. congruence.
Qed.

(** A memo [M] (node 2) of signal [s] (node 1) whose cleanup writes [s] twice and disposes [M], and
    whose body writes [s] while [s < 1]. The host reads [M] twice. The second read runs the cleanup
    inside [RecomputeScoped], with [M] [Clean] and holding the announcement of its first run. *)
Open Scope Z_scope.
Definition prog_window : cmd :=
  CSignal 0 (EConst 0)
  (CMemo 1 Owning CmpEq
     (CCleanup (CWrite (EVar 0) (EConst 10) (CWrite (EVar 0) (EConst 20) (CDispose (EVar 1) (CRet (EConst 0)))))
        (CRead 2 (EVar 0)
           (CIf (ELt (EVar 2) (EConst 1)) (CWrite (EVar 0) (EAdd (EVar 2) (EConst 1)) (CRet (EVar 2)))
              (CRet (EVar 2)))))
     (CRead 3 (EVar 1) (CRead 4 (EVar 1) (CRet (EConst 0))))).
Close Scope Z_scope.

Lemma prog_window_runs :
  (fun r => (g_trace r.1, n_unannounced <$> g_nodes r.1 !! 2)) <$> exec 1000 [] prog_window (init current_flags) =
    Some ([EvHostRead 2 (Ok 0%Z); EvCleanup 2; EvDispose 2; EvHostRead 2 (Ok 0%Z)], Some false) /\
  (fun r => (g_trace r.1, n_unannounced <$> g_nodes r.1 !! 2))
    <$> oexec 1000 [] prog_window (with_skip SkipAll (init current_flags)) =
    Some ([EvHostRead 2 (Ok 0%Z); EvCleanup 2; EvDispose 2; EvHostRead 2 (Ok 0%Z)], Some true).
Proof. split; vm_compute; reflexivity. Qed.

(**md
**The simulation of the plan fails on a reachable program.**

From `sim`-related initial states, `prog_window` ends with equal traces and states outside `sim`:
memo 2 keeps the announcement its present-engine copy spends. `optimised_simulates` and
`stamp_ok_preserved` are false as planned.
*)
Theorem optimised_simulates_falsified :
  sim (init current_flags) (with_skip SkipAll (init current_flags)) /\
  exists s o r1 r2, exec 1000 [] prog_window (init current_flags) = Some (s, r1) /\
    oexec 1000 [] prog_window (with_skip SkipAll (init current_flags)) = Some (o, r2) /\
    g_trace s = g_trace o /\ ~ sim s o.
Proof.
  split.
  { split_and!; [reflexivity|reflexivity|reflexivity|]. intros src c Hc. unfold stamp_current in Hc.
    simpl in Hc. rewrite lookup_empty in Hc. discriminate Hc. }
  destruct prog_window_runs as [W1 W2].
  destruct (exec 1000 [] prog_window (init current_flags)) as [[s r1]|]; [|discriminate W1].
  destruct (oexec 1000 [] prog_window (with_skip SkipAll (init current_flags))) as [[o r2]|]; [|discriminate W2].
  cbn [fmap option_fmap option_map fst] in W1, W2. injection W1 as T1 U1. injection W2 as T2 U2.
  exists s, o, r1, r2. split; [reflexivity|]. split; [reflexivity|]. split; [congruence|].
  intros [Hst _]. assert (Hn : g_nodes s = g_nodes o) by exact (f_equal g_nodes Hst).
  rewrite Hn in U1. congruence.
Qed.

(** ** Why the advances at 4825 and 4290 are needed

    Under [SkipLeaveDirty] only the writes that leave [Dirty] advance the epoch. *)

(** Effect [E] (node 2) reads signal [s] (node 1), and its cleanup writes [s]. The host writes [s]
    twice, and each write flushes. *)
Open Scope Z_scope.
Definition prog_cleanup_rewrite : cmd :=
  CSignal 0 (EConst 0)
  (CEffect 1 (CCleanup (CWrite (EVar 0) (EConst 100) (CRet (EConst 0)))
                (CRead 2 (EVar 0) (CLog (EVar 2) (CRet (EConst 0)))))
  (CWrite (EVar 0) (EConst 1) (CWrite (EVar 0) (EConst 2) (CRet (EConst 0))))).

(** [prog_cleanup_rewrite] up to the second write. *)
Definition prog_cleanup_rewrite_head : cmd :=
  CSignal 0 (EConst 0)
  (CEffect 1 (CCleanup (CWrite (EVar 0) (EConst 100) (CRet (EConst 0)))
                (CRead 2 (EVar 0) (CLog (EVar 2) (CRet (EConst 0)))))
  (CWrite (EVar 0) (EConst 1) (CRet (EConst 0)))).
Close Scope Z_scope.

(**md
**Without the advance at 4825 a cleanup's write hides the next write from its effect.**

The cleanup's write records while the effect is in its cleanups, and the effect leaves them with the
stamp current. Under `SkipLeaveDirty` the host's second write skips, and the trace has no second run
of `E`. `SkipOff` and `SkipAll` agree. The state ahead of that write fails `stamp_ok`.
*)
Example leave_dirty_cleanup_skip :
  trace_of_mode 3000 current_flags SkipLeaveDirty prog_cleanup_rewrite =
    Some [EvRun 2; EvLog 2 0%Z; EvCleanup 2; EvRun 2; EvLog 2 100%Z] /\
  trace_of_mode 3000 current_flags SkipOff prog_cleanup_rewrite =
    Some [EvRun 2; EvLog 2 0%Z; EvCleanup 2; EvRun 2; EvLog 2 100%Z; EvCleanup 2; EvRun 2; EvLog 2 100%Z] /\
  trace_of_mode 3000 current_flags SkipAll prog_cleanup_rewrite =
    trace_of_mode 3000 current_flags SkipOff prog_cleanup_rewrite /\
  (fun r => (bool_decide (stamp_current r.1 1), bool_decide (observer_of r.1 1 2), bool_decide (saturated r.1 2)))
    <$> oexec 3000 [] prog_cleanup_rewrite_head (with_skip SkipLeaveDirty (init current_flags)) =
    Some (true, true, false).
Proof. split_and!; vm_compute; reflexivity. Qed.

(** Memo [M] (node 2) reads signal [s] (node 1), writes [s + 1] while [s < 1], and answers [s / 10]
    otherwise. Memo [N] (node 3) reads [M]. The host reads [N] between writes of [s]. *)
Open Scope Z_scope.
Definition prog_self_write_announce_body (k : cmd) : cmd :=
  CSignal 0 (EConst 0)
  (CMemo 1 Pure CmpEq
     (CRead 2 (EVar 0) (CIf (ELt (EVar 2) (EConst 1)) (CWrite (EVar 0) (EAdd (EVar 2) (EConst 1)) (CRet (EConst 0)))
                          (CRet (EDiv (EVar 2) (EConst 10)))))
  (CMemo 5 Pure CmpEq (CRead 6 (EVar 1) (CRet (EVar 6))) k)).

Definition read_n (k : cmd) : cmd := CRead 7 (EVar 5) k.
Definition write_s (v : val) (k : cmd) : cmd := CWrite (EVar 0) (EConst v) k.

Definition prog_self_write_announce : cmd :=
  prog_self_write_announce_body
    (read_n (read_n (write_s 0 (read_n (write_s 20 (read_n (CRet (EConst 0)))))))).

(** [prog_self_write_announce] up to the write of [20]. *)
Definition prog_self_write_announce_head : cmd :=
  prog_self_write_announce_body (read_n (read_n (write_s 0 (read_n (CRet (EConst 0)))))).
Close Scope Z_scope.

(**md
**Without the advance at 4290 a stale memo's announcement is lost.**

`M`'s run, pulled by the host's read of `N`, writes `s`, records, and ends stale with an unchanged
value, so `N` resolves `Clean` over it. Its announcement then makes `M` unsaturated with no advance.
Under `SkipLeaveDirty` the write of 20 skips, which drops the `NotifyCheck` the announcement owes `N`,
and the last host read answers the stale 0. `SkipOff` and `SkipAll` answer 2. The state ahead of that
write fails `stamp_ok`. The plan's version, with an effect reading `M`, shows no difference: each
`Execute` advances at 4807 after the walk that records.
*)
Example leave_dirty_unannounced_skip :
  trace_of_mode 3000 current_flags SkipLeaveDirty prog_self_write_announce =
    Some [EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z)] /\
  trace_of_mode 3000 current_flags SkipOff prog_self_write_announce =
    Some [EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 2%Z)] /\
  trace_of_mode 3000 current_flags SkipAll prog_self_write_announce =
    trace_of_mode 3000 current_flags SkipOff prog_self_write_announce /\
  (fun r => (bool_decide (stamp_current r.1 1), bool_decide (observer_of r.1 1 2), bool_decide (saturated r.1 2)))
    <$> oexec 3000 [] prog_self_write_announce_head (with_skip SkipLeaveDirty (init current_flags)) =
    Some (true, true, false).
Proof. split_and!; vm_compute; reflexivity. Qed.
