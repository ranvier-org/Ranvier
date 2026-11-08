From stdpp Require Import prelude gmap.
From Stdlib Require Extraction.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.

(** ** The end-state view *)

(** The fields of a node the differential check compares. A kind's irrelevant fields hold their
    construction values, and the driver leaves them out. *)
Record node_view := {
  v_id : nid;
  v_kind : kind;
  v_value : val;
  v_status : status;
  v_failure : option val;
  v_fresh : fresh;
  v_unann : bool;
  v_runs : nat;
  v_disposed : bool;
  v_queued : bool;
  v_sources : nat;
  v_observers : nat
}.

(** The live queue length and the root owner's error count, beside each node in id order. *)
Record state_view := { v_nodes : list node_view; v_queue : nat; v_errors : nat }.

Definition view_node (s : state) (n : nid) (d : node) : node_view :=
  {| v_id := n; v_kind := n_kind d; v_value := n_value d; v_status := n_status d;
     v_failure := n_failure d; v_fresh := n_fresh d; v_unann := n_unann d; v_runs := n_runs d;
     v_disposed := n_disposed d; v_queued := n_queued d;
     v_sources := length (sslots (src_of (g_edges s) n));
     v_observers := length (oitems (obs_of (g_edges s) n)) |}.

(** Node ids run from 1 to [g_next]. *)
Definition node_ids (s : state) : list nid := seq 1 (g_next s).

Definition view_of (s : state) : state_view :=
  {| v_nodes := omap (fun n => view_node s n <$> g_nodes s !! n) (node_ids s);
     v_queue := length (pending s);
     v_errors := default 0 (length ∘ o_errors <$> g_owners s !! root_oid) |}.

Definition memo_ids (s : state) : list nid :=
  omap (fun n => match n_kind <$> g_nodes s !! n with Some KMemo => Some n | _ => None end) (node_ids s).

(** A host read of each memo existing at the start, in id order, then [Graph.Flush]. *)
Definition closing (fuel : nat) : M unit :=
  fun s =>
    match fold_left
       (fun (m : M unit) n =>
          let* _ := m in
          let* _ := exec fuel [] (CTryRead 0 1 (EConst (Z.of_nat n)) (CRet (EConst 0%Z))) in
          m_ret tt)
       (memo_ids s) (m_ret tt)
      s with
    | Some (s', _) => run_flush fuel s'
    | None => None
    end.

(** The trace through the closing reads and flush, the view at the program's end, and the view after
    the closing. *)
Definition observed (fuel : nat) (fl : flags) (c : cmd) : option (list event * state_view * state_view) :=
  match run_top fuel c (init fl) with
  | None => None
  | Some (s, _) =>
    match closing fuel s with
    | None => None
    | Some (s', _) => Some (g_trace s', view_of s, view_of s')
    end
  end.

(** ** Extraction *)

(** Generates [engine.ml] and [engine.mli] in the working directory, [proofs/] under [build.sh]. [nat] and [Z]
    stay inductive, so the extracted interpreter computes as [vm_compute] does and adds no realised
    constant to what the differential check trusts. [proofs/differential/] holds the committed copy, and
    [check.sh] fails when the two differ. *)
Extraction Language OCaml.
Set Extraction Output Directory ".".

Extraction "engine.ml" trace_of observed current_flags.
