From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking.

Open Scope nat_scope.

(** ** The monad *)

(** A step of the engine. [None] is exhausted fuel or a stuck body, so a [Some] result means the
    operation returned. *)
Definition M A := state -> option (state * A).

Definition m_ret {A} (a : A) : M A := fun s => Some (s, a).

Definition m_bind {A B} (m : M A) (k : A -> M B) : M B :=
  fun s => match m s with Some (s', a) => k a s' | None => None end.

Definition stuck {A} : M A := fun _ => None.

Notation "'let*' x ':=' m 'in' k" := (m_bind m (fun x => k))
  (at level 200, x name, m at level 100, k at level 200).

Definition m_get : M state := fun s => Some (s, s).
Definition m_mod (f : state -> state) : M unit := fun s => Some (f s, tt).
Definition m_step (f : state -> option state) : M unit :=
  fun s => match f s with Some s' => Some (s', tt) | None => None end.

(** ** State updates *)

Definition with_edges (v : EdgeWorld) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := v; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_owners (v : gmap oid ownerS) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := v;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_queue (v : list nid) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := v; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_head (v : nat) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := v; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_flushing (v : bool) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := v; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_owed (v : bool) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := v;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := g_running s |}.

Definition with_batch (v : nat) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := v; g_pull := g_pull s; g_current := g_current s; g_untracked := g_untracked s;
     g_owner := g_owner s; g_next := g_next s; g_trace := g_trace s; g_running := g_running s |}.

Definition with_pull (v : nat) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := v; g_current := g_current s; g_untracked := g_untracked s;
     g_owner := g_owner s; g_next := g_next s; g_trace := g_trace s; g_running := g_running s |}.

Definition with_current (v : option nid) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := v; g_untracked := g_untracked s;
     g_owner := g_owner s; g_next := g_next s; g_trace := g_trace s; g_running := g_running s |}.

Definition with_untracked (v : bool) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s; g_untracked := v;
     g_owner := g_owner s; g_next := g_next s; g_trace := g_trace s; g_running := g_running s |}.

Definition with_owner (v : option oid) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := v; g_next := g_next s; g_trace := g_trace s;
     g_running := g_running s |}.

Definition with_next (v : nat) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := v; g_trace := g_trace s;
     g_running := g_running s |}.

Definition with_trace (v : list event) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s; g_trace := v;
     g_running := g_running s |}.

Definition with_running (v : list nid) (s : state) : state :=
  {| g_flags := g_flags s; g_edges := g_edges s; g_nodes := g_nodes s; g_owners := g_owners s;
     g_queue := g_queue s; g_head := g_head s; g_flushing := g_flushing s; g_owed := g_owed s;
     g_batch := g_batch s; g_pull := g_pull s; g_current := g_current s;
     g_untracked := g_untracked s; g_owner := g_owner s; g_next := g_next s;
     g_trace := g_trace s; g_running := v |}.

Definition nset_value (v : val) (d : node) : node :=
  {| n_kind := n_kind d; n_value := v; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_failure (v : option val) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := v; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_status (v : status) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := v;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_fresh (v : fresh) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := v; n_unann := n_unann d; n_runs := n_runs d; n_published := n_published d;
     n_disposed := n_disposed d; n_queued := n_queued d; n_in_cleanup := n_in_cleanup d;
     n_scope := n_scope d; n_violated := n_violated d; n_body := n_body d; n_env := n_env d;
     n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_unann (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := v; n_runs := n_runs d; n_published := n_published d;
     n_disposed := n_disposed d; n_queued := n_queued d; n_in_cleanup := n_in_cleanup d;
     n_scope := n_scope d; n_violated := n_violated d; n_body := n_body d; n_env := n_env d;
     n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_runs (v : nat) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := v; n_published := n_published d;
     n_disposed := n_disposed d; n_queued := n_queued d; n_in_cleanup := n_in_cleanup d;
     n_scope := n_scope d; n_violated := n_violated d; n_body := n_body d; n_env := n_env d;
     n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_published (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d; n_published := v;
     n_disposed := n_disposed d; n_queued := n_queued d; n_in_cleanup := n_in_cleanup d;
     n_scope := n_scope d; n_violated := n_violated d; n_body := n_body d; n_env := n_env d;
     n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_disposed (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := v; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_queued (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := v;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_in_cleanup (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := v; n_scope := n_scope d; n_violated := n_violated d; n_body := n_body d;
     n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_scope (v : option oid) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := v; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d;
     n_link := n_link d |}.

Definition nset_violated (v : bool) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := v; n_body := n_body d;
     n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d; n_link := n_link d |}.

Definition nset_link (v : option link) (d : node) : node :=
  {| n_kind := n_kind d; n_value := n_value d; n_failure := n_failure d; n_status := n_status d;
     n_fresh := n_fresh d; n_unann := n_unann d; n_runs := n_runs d;
     n_published := n_published d; n_disposed := n_disposed d; n_queued := n_queued d;
     n_in_cleanup := n_in_cleanup d; n_scope := n_scope d; n_violated := n_violated d;
     n_body := n_body d; n_env := n_env d; n_mode := n_mode d; n_cmp := n_cmp d; n_link := v |}.

Definition emit (e : event) : M unit := m_mod (fun s => with_trace (g_trace s ++ [e]) s).

Definition get_node (n : nid) : M node :=
  fun s => match g_nodes s !! n with Some d => Some (s, d) | None => None end.

Definition mod_node (n : nid) (u : node -> node) : M unit :=
  fun s => match g_nodes s !! n with Some d => Some (set_node n (u d) s, tt) | None => None end.

Definition get_owner (o : oid) : M ownerS :=
  fun s => match g_owners s !! o with Some x => Some (s, x) | None => None end.

Definition put_owner (o : oid) (x : ownerS) : M unit :=
  m_mod (fun s => with_owners (<[o := x]> (g_owners s)) s).

Definition edges (u : EdgeWorld -> EdgeWorld) : M unit :=
  m_mod (fun s => with_edges (u (g_edges s)) s).

Definition edges_opt (u : EdgeWorld -> option EdgeWorld) : M unit :=
  fun s => match u (g_edges s) with Some w => Some (with_edges w s, tt) | None => None end.

(** [g_running] between a run's start and its [EndRun]. *)
Definition push_running (n : nid) : M unit := m_mod (fun s => with_running (n :: g_running s) s).
Definition pop_running : M unit := m_mod (fun s => with_running (tail (g_running s)) s).

(** ** Owners *)

(** The id of the graph's root owner. *)
Definition root_oid : oid := 0.

(** A new scope, disposed from the start when its host already is. *)
Definition new_owner (host : nid) (sink : option oid) (disposed : bool) : ownerS :=
  {| o_children := []; o_cleanups := []; o_cleanup_envs := []; o_cleanup_order := [];
     o_acquisitions := 0; o_gen := if disposed then 1 else 0; o_disposed := disposed; o_sink := sink;
     o_errors := []; o_host := host |}.

Definition owner_with_child (l : link) (x : ownerS) : ownerS :=
  {| o_children := o_children x ++ [l]; o_cleanups := o_cleanups x;
     o_cleanup_envs := o_cleanup_envs x; o_cleanup_order := o_cleanup_order x;
     o_acquisitions := S (o_acquisitions x); o_gen := o_gen x; o_disposed := o_disposed x;
     o_sink := o_sink x; o_errors := o_errors x; o_host := o_host x |}.

(** [Owner.AddCleanup] on a live scope: the writer of the three cleanup lists. *)
Definition owner_with_cleanup (b : cmd) (e : env) (x : ownerS) : ownerS :=
  {| o_children := o_children x; o_cleanups := o_cleanups x ++ [b];
     o_cleanup_envs := o_cleanup_envs x ++ [e]; o_cleanup_order := o_cleanup_order x ++ [o_acquisitions x];
     o_acquisitions := S (o_acquisitions x); o_gen := o_gen x; o_disposed := o_disposed x;
     o_sink := o_sink x; o_errors := o_errors x; o_host := o_host x |}.

(** The reader of the three cleanup lists: each cleanup with its environment and acquisition count,
    newest first. *)
Fixpoint zip_cleanups (bs : list cmd) (es : list env) (os : list nat) : list (cmd * env * nat) :=
  match bs, es, os with
  | b :: bs', e :: es', o :: os' => (b, e, o) :: zip_cleanups bs' es' os'
  | _, _, _ => []
  end.

Definition owner_cleanups (x : ownerS) : list (cmd * env * nat) :=
  rev (zip_cleanups (o_cleanups x) (o_cleanup_envs x) (o_cleanup_order x)).

(** The start of [DisposeScope]: both lists detached, and a new generation. *)
Definition owner_detached (x : ownerS) : ownerS :=
  {| o_children := []; o_cleanups := []; o_cleanup_envs := []; o_cleanup_order := [];
     o_acquisitions := 0; o_gen := S (o_gen x); o_disposed := o_disposed x;
     o_sink := o_sink x; o_errors := o_errors x; o_host := o_host x |}.

Definition owner_retired (x : ownerS) : ownerS :=
  {| o_children := o_children x; o_cleanups := o_cleanups x;
     o_cleanup_envs := o_cleanup_envs x; o_cleanup_order := o_cleanup_order x;
     o_acquisitions := o_acquisitions x; o_gen := o_gen x; o_disposed := true;
     o_sink := o_sink x; o_errors := o_errors x; o_host := o_host x |}.

Definition owner_with_error (f : val) (x : ownerS) : ownerS :=
  {| o_children := o_children x; o_cleanups := o_cleanups x;
     o_cleanup_envs := o_cleanup_envs x; o_cleanup_order := o_cleanup_order x;
     o_acquisitions := o_acquisitions x; o_gen := o_gen x; o_disposed := o_disposed x;
     o_sink := o_sink x; o_errors := o_errors x ++ [f]; o_host := o_host x |}.

Definition owner_without (l : link) (x : ownerS) : ownerS :=
  {| o_children :=
       filter (fun m => ~ (l_child m = l_child l /\ l_acquired m = l_acquired l)) (o_children x);
     o_cleanups := o_cleanups x; o_cleanup_envs := o_cleanup_envs x;
     o_cleanup_order := o_cleanup_order x; o_acquisitions := o_acquisitions x; o_gen := o_gen x;
     o_disposed := o_disposed x; o_sink := o_sink x; o_errors := o_errors x; o_host := o_host x |}.

(** [Owner.Unlink]: a link from an earlier generation changes nothing. *)
Definition unlink (l : link) : M unit :=
  let* x := get_owner (l_owner l) in
  if decide (l_gen l = Some (o_gen x)) then put_owner (l_owner l) (owner_without l x) else m_ret tt.

(** [Owner.RecordError]. Every scope sinks into the root, and the root has no sink. *)
Definition record_error (o : oid) (f : val) : M unit :=
  let* x := get_owner o in
  match o_sink x with
  | Some p => let* y := get_owner p in put_owner p (owner_with_error f y)
  | None => put_owner o (owner_with_error f x)
  end.

(** The node an [EvLog], [EvCleanup] or [EvFail] names: the running computation, else the host of the
    current owner, which is a cleanup's node. The root owner's host is 0, and node ids start at 1. *)
Definition log_label : M nid :=
  fun s =>
    match g_current s, g_owner s with
    | Some n, _ => Some (s, n)
    | None, Some o => Some (s, default 0 (o_host <$> g_owners s !! o))
    | None, None => Some (s, 0)
    end.

(** The failure of a node created under a pure memo. F# raises a new exception on each. *)
Definition pure_scope_failure : val := (-2)%Z.

(** The variable holding the previous value in a memo body's environment, and in the environments of
    closures it creates. Programs use smaller variables. *)
Definition prev_var : var := 4096%nat.

(** ** Contexts *)

(** [Graph.Detached]. *)
Definition detached (o : oid) (body : M unit) : M unit :=
  fun s0 =>
    let body' :=
      let* _ := m_mod (fun s => with_batch (S (g_batch s)) (with_owner (Some o) (with_current None s))) in
      let* _ := body in
      m_mod (fun s => with_batch (pred (g_batch s)) (with_owner (g_owner s0) (with_current (g_current s0) s))) in
    body' s0.

(** [Graph.RunHosted]. *)
Definition hosted {A} (n : nid) (body : M A) : M A :=
  fun s0 =>
    let body' :=
      let* _ := m_mod (fun s => with_owner None (with_current (Some n) s)) in
      let* r := body in
      let* _ := m_mod (fun s => with_owner (g_owner s0) (with_current (g_current s0) s)) in
      m_ret r in
    body' s0.

(** [Graph.RunUntracked]. *)
Definition untracked {A} (body : M A) : M A :=
  fun s0 =>
    let flag := match g_current s0 with Some _ => true | None => g_untracked s0 end in
    let body' :=
      let* _ := m_mod (fun s => with_untracked flag (with_current None s)) in
      let* r := body in
      let* _ := m_mod (fun s => with_untracked (g_untracked s0) (with_current (g_current s0) s)) in
      m_ret r in
    body' s0.

(** [Graph.Deferring]. *)
Definition deferring (s : state) : bool :=
  match g_current s with
  | Some _ => true
  | None => bool_decide (0 < g_pull s)%nat
  end.

Definition live_queue (s : state) : bool := bool_decide (g_head s < length (g_queue s))%nat.

(** The tests of [RequestFlush] and [SettleFlush] beside the batch depth. *)
Definition outside_bodies (s : state) : bool :=
  bool_decide (g_pull s = 0%nat) &&
  match g_current s with Some _ => false | None => true end &&
  negb (g_untracked s).

Definition stale_live (d : node) : bool := stale (n_fresh d) && negb (n_disposed d).

(** [Graph.Track]. *)
Definition track (n : nid) : M unit :=
  fun s =>
    match g_current s with
    | Some c => match sl_add c n (g_edges s) with Some w => Some (with_edges w s, tt) | None => None end
    | None => Some (s, tt)
    end.

(** [Graph.MarkReaderCheck], under [f_reader_mark]. *)
Definition mark_reader (fuel : nat) : M unit :=
  fun s =>
    match g_current s with
    | Some c =>
      if f_reader_mark (g_flags s)
      then match mark_check fuel c s with Some s' => Some (s', tt) | None => None end
      else Some (s, tt)
    | None => Some (s, tt)
    end.

Definition on_value (e : env) (x : expr) (k : val -> M outcome) : M outcome :=
  fun s =>
    match eval_expr e (env_lookup e prev_var) x with
    | Some (Ok v) => k v s
    | Some (Fail f) => Some (s, Fail f)
    | None => None
    end.

Definition on_node (e : env) (x : expr) (k : nid -> M outcome) : M outcome :=
  on_value e x (fun z => if decide (0 <= z)%Z then k (Z.to_nat z) else stuck).

(** A host read: outside every computation and cleanup. *)
Definition note_host_read (n : nid) (o : outcome) : M unit :=
  fun s =>
    match g_current s, g_owner s with
    | None, Some r =>
      if decide (r = root_oid) then emit (EvHostRead n o) s else Some (s, tt)
    | _, _ => Some (s, tt)
    end.

(** [Graph.NextId]. *)
Definition next_id : M nid :=
  fun s => Some (with_next (S (g_next s)) s, S (g_next s)).

Definition signal_node (v : val) : node :=
  {| n_kind := KSignal; n_value := v; n_failure := None; n_status := StNone; n_fresh := Clean;
     n_unann := false; n_runs := 0; n_published := false; n_disposed := false; n_queued := false;
     n_in_cleanup := false; n_scope := None; n_violated := false; n_body := CRet (EConst 0%Z);
     n_env := []; n_mode := Pure; n_cmp := CmpEq; n_link := None |}.

(** The fields of [Memo] at construction: [Dirty] and [Uninitialized], with the default value. *)
Definition memo_node (m : scope_mode) (c : cmp) (b : cmd) (e : env) : node :=
  {| n_kind := KMemo; n_value := 0%Z; n_failure := None; n_status := StUninitialized; n_fresh := Dirty;
     n_unann := false; n_runs := 0; n_published := false; n_disposed := false; n_queued := false;
     n_in_cleanup := false; n_scope := None; n_violated := false; n_body := b;
     n_env := e; n_mode := m; n_cmp := c; n_link := None |}.

(** The fields of [Effect] at construction: [Clean] and [Uninitialized]. *)
Definition effect_node (b : cmd) (e : env) : node :=
  {| n_kind := KEffect; n_value := 0%Z; n_failure := None; n_status := StUninitialized; n_fresh := Clean;
     n_unann := false; n_runs := 0; n_published := false; n_disposed := false; n_queued := false;
     n_in_cleanup := false; n_scope := None; n_violated := false; n_body := b;
     n_env := e; n_mode := Owning; n_cmp := CmpEq; n_link := None |}.

Definition add_node (n : nid) (d : node) : M unit := m_mod (set_node n d).

(** [Graph.CurrentOwner]. [None] is a pure memo's refusal: the [InvalidOperationException] of its
    [IScopeHost.Scope], with [n_violated] set. *)
Definition current_owner : M (option oid) :=
  fun s =>
    match g_owner s, g_current s with
    | Some o, _ => Some (s, Some o)
    | None, None => None
    | None, Some h =>
      match g_nodes s !! h with
      | None => None
      | Some d =>
        match n_scope d with
        | Some o => Some (with_owner (Some o) s, Some o)
        | None =>
          if match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end
          then Some (set_node h (nset_violated true d) s, None)
          else
            let o : oid := stdpp.base.fresh (dom (g_owners s)) in
            let s1 := with_owners (<[o := new_owner h (Some root_oid) (n_disposed d)]> (g_owners s)) s in
            Some (with_owner (Some o) (set_node h (nset_scope (Some o) d) s1), Some o)
        end
      end
    end.

(** ** The interpreter *)

(** Each function follows the member of [Core.fs] it names. An F# exception inside a body is a [Fail]
    outcome, so the [finally] paths are the code after the body returns. *)
Fixpoint exec (fuel : nat) (e : env) (c : cmd) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    match c with
    | CRet x => on_value e x (fun v => m_ret (Ok v))
    | CThrow y => m_ret (Fail y)
    | CLet x v k => on_value e v (fun z => exec f ((x, z) :: e) k)
    | CIf v a b => on_value e v (fun z => if decide (z = 0%Z) then exec f e b else exec f e a)
    | CRead x n k =>
      on_node e n (fun t =>
        let* o := read f t true in
        let* _ := note_host_read t o in
        match o with
        | Ok v => exec f ((x, v) :: e) k
        | Fail y => m_ret (Fail y)
        end)
    | CTryRead x ok n k =>
      on_node e n (fun t =>
        let* o := read f t true in
        let* _ := note_host_read t o in
        match o with
        | Ok v => exec f ((ok, 1%Z) :: (x, v) :: e) k
        | Fail y => exec f ((ok, 0%Z) :: (x, y) :: e) k
        end)
    | CReadU x n k =>
      on_node e n (fun t =>
        let* o := read f t false in
        let* _ := note_host_read t o in
        match o with
        | Ok v => exec f ((x, v) :: e) k
        | Fail y => m_ret (Fail y)
        end)
    | CWrite n v k =>
      on_node e n (fun t =>
        on_value e v (fun z =>
          let* _ := write_signal f t z in
          exec f e k))
    | CSignal x v k =>
      on_value e v (fun z =>
        let* n := next_id in
        let* _ := add_node n (signal_node z) in
        exec f ((x, Z.of_nat n) :: e) k)
    | CMemo x m q b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (memo_node m q b e') in
      let* owner := current_owner in
      match owner with
      | None => m_ret (Fail pure_scope_failure)
      | Some o =>
        let* _ := attach f n o in
        exec f e' k
      end
    | CEffect x b k =>
      let* n := next_id in
      let e' := (x, Z.of_nat n) :: e in
      let* _ := add_node n (effect_node b e') in
      let* owner := current_owner in
      match owner with
      | None => m_ret (Fail pure_scope_failure)
      | Some o =>
        let* _ := attach f n o in
        let* _ := m_step (mark_dirty f n) in
        let* _ := request_flush f in
        exec f e' k
      end
    | CCleanup b k =>
      let* owner := current_owner in
      match owner with
      | None => m_ret (Fail pure_scope_failure)
      | Some o =>
        let* x := get_owner o in
        let* _ :=
          if o_disposed x then
            let* _ := detached o (run_cleanup f o b e) in
            request_flush f
          else put_owner o (owner_with_cleanup b e x) in
        exec f e k
      end
    | CDispose n k =>
      on_node e n (fun t =>
        let* d := get_node t in
        match n_kind d with
        | KSignal => stuck
        | _ =>
          let* _ := dispose_node f t in
          let* _ := if n_disposed d then m_ret tt else emit (EvDispose t) in
          exec f e k
        end)
    | CBatch b k =>
      let* _ := m_mod (fun s => with_batch (S (g_batch s)) s) in
      let* r := exec f e b in
      let* _ := m_mod (fun s => with_batch (pred (g_batch s)) s) in
      match r with
      | Ok _ =>
        let* _ := request_flush f in
        exec f e k
      | Fail y => m_ret (Fail y)
      end
    | CLog v k =>
      on_value e v (fun z =>
        let* l := log_label in
        let* _ := emit (EvLog l z) in
        exec f e k)
    end
  end

(** [Signal.Value], [Memo.Value], or the same inside [Graph.Untrack]. *)
with read (fuel : nat) (n : nid) (tracked : bool) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f => if tracked then read_node f n else untracked (read_node f n)
  end

with read_node (fuel : nat) (n : nid) {struct fuel} : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      let* _ := track n in
      m_ret (Ok (n_value d))
    | KEffect => stuck
    | KMemo =>
      (* TrackCurrent *)
      let* _ :=
        if stale_live d then
          let* _ := refresh_or_pull f n in
          let* _ := track n in
          let* d' := get_node n in
          if stale_live d' then mark_reader f else m_ret tt
        else track n in
      let* d' := get_node n in
      match n_status d' with
      | StError => m_ret (Fail (default 0%Z (n_failure d')))
      | _ => m_ret (Ok (n_value d'))
      end
    end
  end

(** [Memo.EnsureCurrent]. A signal has nothing to bring current. *)
with update_if_necessary (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KMemo => if stale_live d then refresh_or_pull f n else m_ret tt
    | _ => m_ret tt
    end
  end

with refresh_or_pull (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if deferring s then refresh f n else pull f n
  end

(** [Memo.Pull]: [EnterPull], [Refresh] and [ExitPull]. *)
with pull (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := m_mod (fun s => with_pull (S (g_pull s)) s) in
    let* _ := refresh f n in
    let* _ := m_mod (fun s => with_pull (pred (g_pull s)) s) in
    let* s := m_get in
    if bool_decide (g_pull s = 0%nat) && g_owed s then settle_flush f else m_ret tt
  end

(** [Memo.Refresh], with [ResolveCheck]. *)
with refresh (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* _ :=
      if decide (n_fresh d = Check) then
        let* _ := resolve_walk f n 0 in
        let* d' := get_node n in
        if decide (n_fresh d' = Check)
        then mod_node n (fun x => nset_unann false (nset_fresh Clean x))
        else m_ret tt
      else m_ret tt in
    let* d' := get_node n in
    if decide (n_fresh d' = Dirty) then recompute f n else m_ret tt
  end

(** The loop of [ResolveCheck] and of [Effect.Execute]. The slot count and the freshness are read at
    each step. *)
with resolve_walk (fuel : nat) (n : nid) (i : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* s := m_get in
    if decide (n_fresh d = Check) then
      match sslots (src_of (g_edges s) n) !! i with
      | Some (src, _) =>
        let* _ := update_if_necessary f src in
        resolve_walk f n (S i)
      | None => m_ret tt
      end
    else m_ret tt
  end

(** [Memo.Recompute] and [Memo.RecomputeScoped]. *)
with recompute (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_scope d with
    | None => run_memo f n
    | Some o =>
      let* _ := mod_node n (nset_fresh Clean) in
      let* _ := discharge f o in
      let* d' := get_node n in
      if n_disposed d' then m_ret tt
      else if decide (n_runs d' = n_runs d) then run_memo f n
      else if decide (n_fresh d' = Dirty) then recompute f n
      else m_ret tt
    end
  end

(** [Memo.Run]. *)
with run_memo (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let previous := n_value d in
    let previous_status := n_status d in
    let previous_failure := n_failure d in
    let* _ := mod_node n (fun x =>
      nset_runs (S (n_runs x)) (nset_failure None (nset_status StNone
        (nset_unann false (nset_fresh Clean x))))) in
    let* _ := edges (sl_begin n) in
    let* _ := push_running n in
    let body_env := if n_published d then (prev_var, previous) :: n_env d else n_env d in
    let* r := hosted n (exec f body_env (n_body d)) in
    let* d1 := get_node n in
    let result := if n_violated d1 then Fail pure_scope_failure else r in
    let* _ :=
      match result with
      | Ok v => mod_node n (fun x => nset_published true (nset_value v x))
      | Fail _ => m_ret tt
      end in
    let* _ := edges_opt (if n_disposed d1 then sl_clear n else sl_end n) in
    let* _ := pop_running in
    let* _ :=
      match result with
      | Ok _ => m_ret tt
      | Fail y => mod_node n (fun x =>
          nset_violated false (nset_status StError (nset_failure (Some y) x)))
      end in
    let* d2 := get_node n in
    let* moved :=
      if decide (n_status d2 <> previous_status \/ n_failure d2 <> previous_failure) then m_ret true
      else
        match n_cmp d2 with
        | CmpEq => m_ret (bool_decide (n_value d2 <> previous))
        | CmpNever => m_ret true
        | CmpThrows y =>
          let* _ := mod_node n (fun x =>
            nset_status StError (nset_failure (Some y) (nset_value previous x))) in
          m_ret true
        end in
    let* s := m_get in
    let* _ := if moved then m_step (notify_dirty_except f n (g_current s)) else m_ret tt in
    let* d3 := get_node n in
    if decide (n_fresh d3 = Clean) then m_ret tt else mod_node n (nset_unann true)
  end

(** [Effect.Execute]. *)
with execute_effect (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then mod_node n (nset_queued false)
    else
      let* _ :=
        if decide (n_fresh d = Check) then
          let* _ := resolve_walk f n 0 in
          let* d' := get_node n in
          if decide (n_fresh d' = Check) then mod_node n (nset_fresh Clean) else m_ret tt
        else m_ret tt in
      let* d' := get_node n in
      let* _ := mod_node n (fun x => nset_fresh Clean (nset_queued false x)) in
      if decide (n_fresh d' = Dirty) then run_effect f n else m_ret tt
  end

(** [Effect.Run]. *)
with run_effect (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := push_running n in
    let* _ := edges (sl_begin n) in
    let* d := get_node n in
    let* _ :=
      match n_scope d with
      | Some o =>
        let* _ := mod_node n (nset_in_cleanup true) in
        let* _ := discharge f o in
        mod_node n (nset_in_cleanup false)
      | None => m_ret tt
      end in
    let* d' := get_node n in
    let* _ := if n_disposed d' then m_ret tt else run_effect_body f n in
    pop_running
  end

(** [Effect.RunBody]. *)
with run_effect_body (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    let* _ := mod_node n (fun x => nset_runs (S (n_runs x)) (nset_failure None (nset_status StNone x))) in
    let* _ := emit (EvRun n) in
    let* r := hosted n (exec f (n_env d) (n_body d)) in
    let* d' := get_node n in
    let* _ := edges_opt (if n_disposed d' then sl_clear n else sl_end n) in
    match r with
    | Ok _ => m_ret tt
    | Fail y =>
      let* _ := mod_node n (fun x => nset_status StError (nset_failure (Some y) x)) in
      emit (EvFail n y)
    end
  end

(** [Graph.Discharge]. *)
with discharge (fuel : nat) (o : oid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (dispose_scope f o) in
    let* s := m_get in
    if bool_decide (g_batch s = 0%nat) && negb (g_flushing s) && live_queue s
    then m_mod (with_owed true)
    else m_ret tt
  end

(** [Graph.Retire], with [Owner.Dispose]. *)
with retire (fuel : nat) (o : oid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := detached o (
      let* x := get_owner o in
      if o_disposed x then m_ret tt
      else
        let* _ := put_owner o (owner_retired x) in
        dispose_scope f o) in
    let* s := m_get in
    if live_queue s then request_flush f else m_ret tt
  end

(** [Owner.DisposeScope]. *)
with dispose_scope (fuel : nat) (o : oid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := put_owner o (owner_detached x) in
    teardown f o (rev (o_children x)) (owner_cleanups x)
  end

(** The loop of [DisposeScope]: children and cleanups, newest acquisition first. *)
with teardown (fuel : nat) (o : oid) (ls : list link) (cs : list (cmd * env * nat)) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    match ls, cs with
    | [], [] => m_ret tt
    | l :: ls', [] =>
      let* _ := release f (l_child l) in
      teardown f o ls' []
    | [], (b, e, _) :: cs' =>
      let* _ := run_cleanup f o b e in
      teardown f o [] cs'
    | l :: ls', (b, e, a) :: cs' =>
      if decide (a < l_acquired l)%nat then
        let* _ := release f (l_child l) in
        teardown f o ls' cs
      else
        let* _ := run_cleanup f o b e in
        teardown f o ls cs'
    end
  end

(** A cleanup: its event, its body, and [RecordError] for its failure. *)
with run_cleanup (fuel : nat) (o : oid) (b : cmd) (e : env) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    let* _ := emit (EvCleanup (o_host x)) in
    let* r := exec f e b in
    match r with
    | Ok _ => m_ret tt
    | Fail y =>
      let* _ := record_error o y in
      emit (EvFail (o_host x) y)
    end
  end

(** [IOwned.Release]. *)
with release (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* _ := mod_node n (nset_link None) in
    dispose_node f n
  end

(** [Memo.Dispose] and [Effect.Dispose]. *)
with dispose_node (fuel : nat) (n : nid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    if n_disposed d then m_ret tt
    else
      let* _ := mod_node n (fun x => nset_link None (nset_disposed true x)) in
      let* _ := match n_link d with Some l => unlink l | None => m_ret tt end in
      let* _ := edges_opt (sl_clear n) in
      match n_scope d with
      | Some o => retire f o
      | None => m_ret tt
      end
  end

(** [Owner.AttachLinked]: on a disposed owner, the node is released at once and holds a link with no
    generation. *)
with attach (fuel : nat) (n : nid) (o : oid) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* x := get_owner o in
    if o_disposed x then
      let* _ := release f n in
      mod_node n (nset_link (Some {| l_child := n; l_owner := o; l_gen := None; l_acquired := 0 |}))
    else
      let l := {| l_child := n; l_owner := o; l_gen := Some (o_gen x); l_acquired := o_acquisitions x |} in
      let* _ := put_owner o (owner_with_child l x) in
      mod_node n (nset_link (Some l))
  end

(** The setter of [Signal.Value]. *)
with write_signal (fuel : nat) (n : nid) (v : val) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      if decide (n_value d = v) then m_ret tt
      else
        let* _ := mod_node n (nset_value v) in
        let* _ := m_step (notify_dirty f n) in
        request_flush f
    | _ => stuck
    end
  end

(** [Graph.RequestFlush]. *)
with request_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if live_queue s && bool_decide (g_batch s = 0%nat) then
      if outside_bodies s then run_flush f
      else if g_flushing s then m_ret tt
      else m_mod (with_owed true)
    else m_ret tt
  end

(** [Graph.SettleFlush]. *)
with settle_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_owed s && bool_decide (g_batch s = 0%nat) && outside_bodies s then run_flush f else m_ret tt
  end

(** [Graph.RunFlush]. *)
with run_flush (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    if g_flushing s then m_ret tt
    else
      let* _ := m_mod (fun s => with_owed false (with_flushing true s)) in
      let* _ := drain f in
      m_mod (with_flushing false)
  end

(** The loop of [RunFlush]. A clean drain resets the queue. *)
with drain (fuel : nat) {struct fuel} : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* s := m_get in
    match g_queue s !! g_head s with
    | Some n =>
      let* _ := m_mod (with_head (S (g_head s))) in
      let* _ := execute_effect f n in
      drain f
    | None => m_mod (fun s => with_queue [] (with_head 0 s))
    end
  end.

(** A new graph, with the root owner as the current owner. *)
Definition init (fl : flags) : state :=
  {| g_flags := fl; g_edges := empty_world; g_nodes := ∅;
     g_owners := <[root_oid := new_owner 0 None false]> ∅; g_queue := []; g_head := 0;
     g_flushing := false; g_owed := false; g_batch := 0; g_pull := 0; g_current := None;
     g_untracked := false; g_owner := Some root_oid; g_next := 0; g_trace := []; g_running := [] |}.

Definition run_top (fuel : nat) (c : cmd) : M outcome := exec fuel [] c.

Definition trace_of (fuel : nat) (fl : flags) (c : cmd) : option (list event) :=
  match run_top fuel c (init fl) with
  | Some (s, _) => Some (g_trace s)
  | None => None
  end.
