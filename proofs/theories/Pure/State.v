From stdpp Require Import prelude gmap.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList.

Inductive kind := KSignal | KMemo | KEffect.

(** A node's stored [Status]. [n_fresh] determines [Status.Stale]. *)
Inductive status := StNone | StPending | StError | StUninitialized.

Global Instance status_eq_dec : EqDecision status.
Proof. solve_decision. Defined.

(** [OwnerLink]. [l_gen] is the generation the link joined, and [None] once the link has left the
    list, the F# [-1]. *)
Record link := { l_child : nid; l_owner : oid; l_gen : option nat; l_acquired : nat }.

(** [Owner]. [o_children] is stored head to tail. [o_cleanups], [o_cleanup_envs] and [o_cleanup_order]
    are parallel and stored earliest first, and [o_cleanup_envs] holds each cleanup's captured
    environment. Teardown runs both from the end. *)
Record ownerS := {
  o_children : list link;
  o_cleanups : list cmd;
  o_cleanup_envs : list env;
  o_cleanup_order : list nat;
  o_acquisitions : nat;
  o_gen : nat;
  o_disposed : bool;
  o_sink : option oid;
  o_errors : list val
}.

Record node := { n_kind : kind; n_value : val; n_failure : option val; n_status : status;
                 n_fresh : fresh; n_unann : bool; n_runs : nat; n_published : bool;
                 n_disposed : bool; n_queued : bool; n_in_cleanup : bool; n_scope : option oid;
                 n_violated : bool; n_body : cmd; n_env : env; n_mode : scope_mode; n_cmp : cmp }.

(** [f_unannounced] enables the [elif unannounced] branches of marking, and [f_reader_mark] the
    [MarkReaderCheck] of a read. Clearing one gives the engine before that fix. *)
Record flags := { f_unannounced : bool; f_reader_mark : bool }.

Definition current_flags : flags := {| f_unannounced := true; f_reader_mark := true |}.

Record state := { g_flags : flags; g_edges : EdgeWorld; g_nodes : gmap nid node;
                  g_owners : gmap oid ownerS; g_queue : list nid; g_head : nat;
                  g_flushing : bool; g_owed : bool; g_batch : nat; g_pull : nat;
                  g_current : option nid; g_untracked : bool; g_owner : option oid;
                  g_next : nat; g_trace : list event;
                  g_running : list nid (* ghost: between a run's start and EndRun *) }.

(** [c] observes [src]: a slot of [c] and a position of [src]'s observers each hold the other. *)
Definition edge (s : state) (src c : nid) : Prop :=
  exists i h, slot_at (g_edges s) c i = Some (src, Some h) /\
              oitems (obs_of (g_edges s) src) !! h = Some (c, i).
