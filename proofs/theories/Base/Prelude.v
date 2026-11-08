From stdpp Require Import prelude.
From Stdlib Require Import ZArith.

Definition node_id := nat.
Definition owner_id := nat.
Definition val := Z.

Inductive freshness := Clean | Check | Dirty.

Definition stale (f : freshness) : bool :=
  match f with Clean => false | _ => true end.

Definition freshness_rank (f : freshness) : nat :=
  match f with Clean => 0 | Check => 1 | Dirty => 2 end.

(** Order [Clean <= Check <= Dirty]. *)
Definition freshness_le (f g : freshness) : Prop := freshness_rank f <= freshness_rank g.

Global Instance freshness_eq_dec : EqDecision freshness.
Proof. solve_decision. Defined.

Global Instance freshness_le_dec : RelDecision freshness_le.
Proof. intros f g. unfold freshness_le. apply _. Defined.
