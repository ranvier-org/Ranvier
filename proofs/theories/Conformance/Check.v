From stdpp Require Import prelude.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine Witnesses.
From Ranvier.Conformance Require Import Expected.

Open Scope Z_scope.

(** One program per scenario of [proofs/conformance/Scenarios.fsx]. A node's variable is its id. A body's
    locals start at 10. *)
Local Notation V := EVar.
Local Notation K := EConst.
Local Notation Nop := (CRet (EConst 0)).
Local Notation R := CRead.
Local Notation W := CWrite.
Local Notation L := CLog.

Definition prog_S01 : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq (R 10 (V 1) (L (V 10) (CRet (EAdd (V 10) (K 1))))) (
  CMemo 3 Pure CmpEq (R 10 (V 1) (L (V 10) (CRet (EMul (V 10) (K 2))))) (
  CEffect 4 (R 10 (V 2) (R 11 (V 3) (L (EAdd (V 10) (V 11)) Nop))) (
  W (V 1) (K 1) (W (V 1) (K 1) (W (V 1) (K 2) Nop)))))).

Definition prog_S02 : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq (R 10 (V 1) (L (V 10) (CRet (ELt (V 10) (K 10))))) (
  CEffect 3 (R 10 (V 2) (L (V 10) Nop)) (
  W (V 1) (K 1) (W (V 1) (K 20) (W (V 1) (K 30) Nop))))).

Local Definition s03_batch (i : val) : cmd :=
  W (V 2) (K i) (W (V 3) (K i) (W (V 1) (K i) Nop)).

Definition prog_S03 : cmd :=
  CSignal 1 (K 0) (CSignal 2 (K 0) (CSignal 3 (K 0) (
  CMemo 4 Pure CmpEq (R 10 (V 1) (CRet (V 10))) (
  CMemo 5 Pure CmpEq (R 10 (V 3) (R 11 (V 4) (CRet (V 11)))) (
  CMemo 6 Pure CmpEq
    (R 10 (V 2) (R 11 (V 5) (R 12 (V 4) (L (V 12) (CRet (EDiv (V 12) (K 2))))))) (
  CEffect 7 (R 10 (V 6) (L (V 10) Nop)) (
  CBatch (s03_batch 1) (CBatch (s03_batch 2) (CBatch (s03_batch 3) (CBatch (s03_batch 4) Nop)))))))))).

Definition prog_S04 : cmd :=
  CSignal 1 (K 0) (CSignal 2 (K 0) (
  CMemo 3 Pure CmpEq
    (R 10 (V 1) (CIf (ELt (V 10) (K 3)) (W (V 1) (EAdd (V 10) (K 1)) (CRet (V 10))) (CRet (V 10)))) (
  CEffect 4 (R 10 (V 2) (CIf (V 10) (R 11 (V 3) (L (V 11) Nop)) (L (K (-1)) Nop))) (
  W (V 2) (K 1) (R 20 (V 1) Nop))))).

Definition prog_S05 : cmd :=
  CSignal 1 (K 1) (CSignal 2 (K 10) (CSignal 3 (K 20) (
  CEffect 4
    (R 10 (V 1) (CIf (V 10) (R 11 (V 2) (L (V 11) Nop)) (R 11 (V 3) (L (V 11) Nop)))) (
  W (V 1) (K 0) (W (V 2) (K 11) (W (V 3) (K 21) Nop)))))).

Definition prog_S06 : cmd :=
  CSignal 1 (K 0) (CSignal 2 (K 0) (
  CEffect 3
    (R 10 (V 1) (
     CIf (EEq (V 10) (K 0))
       (R 11 (V 2) (R 12 (V 1) (L (EAdd (EAdd (V 10) (V 11)) (V 12)) Nop)))
       (CReadU 11 (V 2) (L (EAdd (V 10) (V 11)) Nop)))) (
  W (V 1) (K 1) (W (V 2) (K 5) (W (V 1) (K 2) Nop))))).

Definition prog_S07 : cmd :=
  CSignal 1 (K 0) (
  CEffect 2 (R 10 (V 1) (L (V 10) Nop)) (
  CBatch (W (V 1) (K 1) (W (V 1) (K 0) Nop)) Nop)).

Definition prog_S08 : cmd :=
  CSignal 1 (K 0) (
  CEffect 2
    (R 10 (V 1) (L (V 10) (CCleanup (W (V 1) (EAdd (V 10) (K 10)) Nop) Nop))) (
  W (V 1) (K 1) (W (V 1) (K 11) Nop))).

Definition prog_S09a : cmd :=
  CSignal 1 (K 0) (
  CEffect 2 (R 10 (V 1) (L (V 10) Nop)) (
  CEffect 3 (R 10 (V 1) (CIf (ELt (K 0) (V 10)) (CThrow 7) (L (V 10) Nop))) (
  CEffect 4 (R 10 (V 1) (L (V 10) Nop)) (
  W (V 1) (K 1) (W (V 1) (K 0) Nop))))).

Definition prog_S09b : cmd :=
  CSignal 1 (K 0) (
  CEffect 2 (R 10 (V 1) (L (V 10) Nop)) (
  CEffect 3 (R 10 (V 1) (CCleanup (CThrow 8) (L (V 10) Nop))) (
  W (V 1) (K 1) (W (V 1) (K 2) Nop)))).

Definition prog_S10 : cmd :=
  CSignal 1 (K 0) (
  CBatch
    (CEffect 2
      (R 10 (V 1) (L (V 10) (CCleanup (L (K 100) Nop)
        (CDispose (V 2) (R 11 (V 1) (L (V 11) Nop)))))) Nop) (
  CEffect 3 (R 10 (V 1) (L (V 10) Nop)) (
  W (V 1) (K 1) (W (V 1) (K 2) Nop)))).

Definition prog_S11 : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Owning CmpEq
    (R 10 (V 1) (
     CCleanup
       (CIf (EEq (V 10) (K 1))
         (W (V 1) (K 2) (R 11 (V 2) (L (V 11) Nop)))
         (R 11 (V 2) (L (V 11) Nop)))
     (CRet (V 10)))) (
  CEffect 3 (R 10 (V 2) (L (V 10) Nop)) (
  W (V 1) (K 1) (W (V 1) (K 5) Nop)))).

Definition prog_S12 : cmd :=
  CEffect 1
    (CCleanup (L (K 1) Nop) (
     CEffect 2 (CCleanup (L (K 21) Nop) Nop) (
     CCleanup (L (K 2) Nop) (
     CEffect 3 (CCleanup (L (K 31) Nop) Nop) (
     CCleanup (L (K 3) (CCleanup (L (K 4) Nop) Nop)) Nop))))) (
  CDispose (V 1) (CDispose (V 1) (CDispose (K 2) Nop))).

Definition prog_S13 : cmd :=
  CSignal 1 (K 0) (
  CEffect 2
    (R 10 (V 1) (L (V 10) (CIf (ELt (V 10) (K 3)) (W (V 1) (EAdd (V 10) (K 1)) Nop) Nop))) Nop).

Definition prog_S14 : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq
    (R 10 (V 1) (L (V 10) (CIf (ELt (V 10) (K 3)) (W (V 1) (EAdd (V 10) (K 1)) Nop) Nop))) (
  CEffect 3 (R 10 (V 2) (L (V 10) Nop)) (
  R 20 (V 2) (R 21 (V 1) Nop)))).

Definition prog_S15 : cmd :=
  CSignal 1 (K 0) (CSignal 2 (K 0) (
  CMemo 3 Pure CmpEq (R 10 (V 2) (L (V 10) (CRet (V 10)))) (
  CMemo 4 Pure CmpEq
    (R 10 (V 1) (R 11 (V 3) (L (EAdd (V 10) (V 11)) (CRet (EAdd (V 10) (V 11)))))) (
  CEffect 5
    (R 10 (V 1) (R 11 (V 4) (R 12 (V 3) (L (EAdd (EAdd (V 10) (V 11)) (V 12)) Nop)))) (
  CBatch (W (V 1) (K 1) (W (V 2) (K 1) Nop)) Nop))))).

Definition prog_in_cleanup_observer_stays_clean : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq (R 10 (V 1) (CRet (V 10))) (
  CEffect 3
    (R 10 (V 2) (L (V 10) (CCleanup (W (V 1) (K 100) Nop) Nop))) (
  W (V 1) (K 1) (R 20 (V 1) (W (V 1) (K 2) (R 21 (V 2) Nop)))))).

Definition prog_disposed_running_observer_stays_clean : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq (R 10 (V 1) (CRet (V 10))) (
  CBatch
    (CEffect 3
      (CDispose (V 3) (R 10 (V 2) (L (V 10) (W (V 1) (EAdd (V 10) (K 1)) Nop)))) Nop) (
  W (V 1) (K 50) (R 20 (V 2) Nop)))).

Definition prog_self_marking_cleanup : cmd := self_marking_cleanup (R 20 (V 5) Nop).
Definition prog_self_marking_body : cmd := self_marking_body (R 20 (V 2) Nop).
Definition prog_self_marking_through_memo : cmd := self_marking_through_memo (R 20 (V 3) Nop).
Definition prog_nested_self_run : cmd := nested_self_run (R 20 (V 4) Nop).
Definition prog_check_over_moved : cmd := check_over_moved (R 22 (V 4) Nop).
Definition prog_check_two_reads : cmd := check_two_reads (R 22 (V 3) Nop).
Definition prog_stale_source_reader : cmd := stale_source_reader (R 22 (V 3) (R 23 (V 3) (R 24 (V 2) Nop))).
Definition prog_disposed_self_window : cmd := disposed_window (R 22 (V 4) Nop).

Definition prog_previous_value : cmd :=
  CSignal 1 (K 0) (
  CMemo 2 Pure CmpEq
    (R 10 (V 1) (CIf (EEq (V 10) (K 0)) (CRet (K 1)) (CRet (EAdd EPrev (K 1))))) (
  CMemo 3 Owning CmpEq
    (R 10 (V 1) (
     CIf (V 10)
       (CMemo 11 Pure CmpEq (CRet (K 7)) (R 12 (V 11) (L (V 12) (CRet (V 10)))))
       (CRet (K 42)))) (
  R 20 (V 2) (R 21 (V 3) (W (V 1) (K 5) (R 22 (V 2) (R 23 (V 3) Nop))))))).

Example check_S01 : trace_of 100000 current_flags prog_S01 = Some exp_S01.
Proof. vm_compute. reflexivity. Qed.
Example check_S02 : trace_of 100000 current_flags prog_S02 = Some exp_S02.
Proof. vm_compute. reflexivity. Qed.
Example check_S03 : trace_of 100000 current_flags prog_S03 = Some exp_S03.
Proof. vm_compute. reflexivity. Qed.
Example check_S04 : trace_of 100000 current_flags prog_S04 = Some exp_S04.
Proof. vm_compute. reflexivity. Qed.
Example check_S05 : trace_of 100000 current_flags prog_S05 = Some exp_S05.
Proof. vm_compute. reflexivity. Qed.
Example check_S06 : trace_of 100000 current_flags prog_S06 = Some exp_S06.
Proof. vm_compute. reflexivity. Qed.
Example check_S07 : trace_of 100000 current_flags prog_S07 = Some exp_S07.
Proof. vm_compute. reflexivity. Qed.
Example check_S08 : trace_of 100000 current_flags prog_S08 = Some exp_S08.
Proof. vm_compute. reflexivity. Qed.
Example check_S09a : trace_of 100000 current_flags prog_S09a = Some exp_S09a.
Proof. vm_compute. reflexivity. Qed.
Example check_S09b : trace_of 100000 current_flags prog_S09b = Some exp_S09b.
Proof. vm_compute. reflexivity. Qed.
Example check_S10 : trace_of 100000 current_flags prog_S10 = Some exp_S10.
Proof. vm_compute. reflexivity. Qed.
Example check_S11 : trace_of 100000 current_flags prog_S11 = Some exp_S11.
Proof. vm_compute. reflexivity. Qed.
Example check_S12 : trace_of 100000 current_flags prog_S12 = Some exp_S12.
Proof. vm_compute. reflexivity. Qed.
Example check_S13 : trace_of 100000 current_flags prog_S13 = Some exp_S13.
Proof. vm_compute. reflexivity. Qed.
Example check_S14 : trace_of 100000 current_flags prog_S14 = Some exp_S14.
Proof. vm_compute. reflexivity. Qed.
Example check_S15 : trace_of 100000 current_flags prog_S15 = Some exp_S15.
Proof. vm_compute. reflexivity. Qed.
Example check_in_cleanup_observer_stays_clean :
  trace_of 100000 current_flags prog_in_cleanup_observer_stays_clean =
    Some exp_in_cleanup_observer_stays_clean.
Proof. vm_compute. reflexivity. Qed.
Example check_disposed_running_observer_stays_clean :
  trace_of 100000 current_flags prog_disposed_running_observer_stays_clean =
    Some exp_disposed_running_observer_stays_clean.
Proof. vm_compute. reflexivity. Qed.

(** The state the F# scenarios assert beside their traces. *)
Local Definition final (p : cmd) : option state :=
  fst <$> run_top 100000 p (init current_flags).

(** [S09a] before its recovering write: the effect holds its failure. *)
Example check_S09a_status :
  let p :=
    CSignal 1 (K 0) (
    CEffect 2 (R 10 (V 1) (L (V 10) Nop)) (
    CEffect 3 (R 10 (V 1) (CIf (ELt (K 0) (V 10)) (CThrow 7) (L (V 10) Nop))) (
    CEffect 4 (R 10 (V 1) (L (V 10) Nop)) (
    W (V 1) (K 1) Nop)))) in
  (s ← final p; d ← g_nodes s !! 3%nat; Some (n_status d, n_failure d)) = Some (StError, Some 7).
Proof. vm_compute. reflexivity. Qed.

(** [S09b]: each cleanup failure stays out of the flush and lands in the root's errors. *)
Example check_S09b_errors :
  (s ← final prog_S09b; x ← g_owners s !! root_oid; Some (o_errors x)) = Some [8; 8].
Proof. vm_compute. reflexivity. Qed.

(** [S10]: only the live effect observes the signal. *)
Example check_S10_edges :
  (s ← final prog_S10; Some (oitems (obs_of (g_edges s) 1%nat))) = Some [(3%nat, 0%nat)].
Proof. vm_compute. reflexivity. Qed.

(** The disposed effect's edge to the memo ends with its run. *)
Example check_disposed_running_edges :
  (s ← final prog_disposed_running_observer_stays_clean; Some (oitems (obs_of (g_edges s) 2%nat))) = Some [].
Proof. vm_compute. reflexivity. Qed.

(** The scenario ends with [Graph.Flush], which has no command. *)
Local Definition trace_then_flush (p : cmd) : option (list event) :=
  r ← run_top 100000%nat p (init current_flags);
  r' ← run_flush 100000%nat r.1;
  Some (g_trace r'.1).

Example check_self_marking_cleanup :
  trace_then_flush prog_self_marking_cleanup = Some exp_self_marking_cleanup.
Proof. vm_compute. reflexivity. Qed.
Example check_self_marking_body :
  trace_of 100000 current_flags prog_self_marking_body = Some exp_self_marking_body.
Proof. vm_compute. reflexivity. Qed.
Example check_self_marking_through_memo :
  trace_of 100000 current_flags prog_self_marking_through_memo = Some exp_self_marking_through_memo.
Proof. vm_compute. reflexivity. Qed.
Example check_nested_self_run :
  trace_of 100000 current_flags prog_nested_self_run = Some exp_nested_self_run.
Proof. vm_compute. reflexivity. Qed.
Example check_check_over_moved :
  trace_of 100000 current_flags prog_check_over_moved = Some exp_check_over_moved.
Proof. vm_compute. reflexivity. Qed.
Example check_check_two_reads :
  trace_of 100000 current_flags prog_check_two_reads = Some exp_check_two_reads.
Proof. vm_compute. reflexivity. Qed.
Example check_stale_source_reader :
  trace_of 100000 current_flags prog_stale_source_reader = Some exp_stale_source_reader.
Proof. vm_compute. reflexivity. Qed.
Example check_disposed_self_window :
  trace_of 100000 current_flags prog_disposed_self_window = Some exp_disposed_self_window.
Proof. vm_compute. reflexivity. Qed.
Example check_previous_value :
  trace_of 100000 current_flags prog_previous_value = Some exp_previous_value.
Proof. vm_compute. reflexivity. Qed.

(** A memo nested in a published memo has no previous value on its first run, as [compute] gets
    [ValueNone] at Core.fs 4234: [EPrev] is stuck. *)
Example nested_memo_prev_stuck :
  trace_of 100000 current_flags
    (CSignal 1 (K 0) (
     CMemo 2 Owning CmpEq
       (R 10 (V 1) (CIf (V 10)
         (CMemo 11 Pure CmpEq (CRet EPrev) (R 12 (V 11) (L (V 12) (CRet (V 10)))))
         (CRet (K 42)))) (
     R 20 (V 2) (W (V 1) (K 5) (R 21 (V 2) Nop))))) = None.
Proof. vm_compute. reflexivity. Qed.

(** The window of [disposed_running_observer_stays_clean], from a body running for effect 3 after its
    disposal: the read links an edge, and the write leaves the effect clean and unqueued. *)
Example check_disposed_window :
  let setup :=
    CSignal 1 (K 0) (CMemo 2 Pure CmpEq (R 10 (V 1) (CRet (V 10))) (CEffect 3 Nop Nop)) in
  let e : env := [(3%nat, 3); (2%nat, 2); (1%nat, 1)] in
  let run (b : cmd) :=
    r ← run_top 100000%nat setup (init current_flags);
    r' ← hosted 3%nat (exec 100000%nat e (CDispose (V 3) b)) r.1;
    Some r'.1 in
  (s ← run (R 10 (V 2) Nop); Some (oitems (obs_of (g_edges s) 2%nat))) = Some [(3%nat, 0%nat)] /\
  (s ← run (R 10 (V 2) (W (V 1) (K 1) Nop)); d ← g_nodes s !! 3%nat;
   Some (n_fresh d, n_queued d, length (pending s))) = Some (Clean, false, 0%nat).
Proof. split; vm_compute; reflexivity. Qed.
