From stdpp Require Import prelude.
From Stdlib Require Import ZArith.

Definition nid := nat.
Definition oid := nat.
Definition val := Z.

Inductive fresh := Clean | Check | Dirty.

Definition stale (f : fresh) : bool :=
  match f with Clean => false | _ => true end.

Definition fresh_rank (f : fresh) : nat :=
  match f with Clean => 0 | Check => 1 | Dirty => 2 end.

(** Order [Clean <= Check <= Dirty]. *)
Definition fresh_le (f g : fresh) : Prop := fresh_rank f <= fresh_rank g.

Global Instance fresh_eq_dec : EqDecision fresh.
Proof. solve_decision. Defined.

Global Instance fresh_le_dec : RelDecision fresh_le.
Proof. intros f g. unfold fresh_le. apply _. Defined.
