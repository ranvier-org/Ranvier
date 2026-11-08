From stdpp Require Import prelude.
From Ranvier.Base Require Import Prelude.
From Ranvier.Pure Require Import ObserverSet.

(** ** Walks *)

Local Ltac walk_steps :=
  repeat first
    [ eapply walk_visit; [reflexivity|reflexivity|reflexivity|]
    | apply walk_end; reflexivity ].

(** An observer held twice can be missed though present throughout. Observer 1 sits below the cursor
    and gains a second position above it. Removing the lower position moves observer 3 into it, so the
    walk visits 3 where 1 was. [BUGS.md] closes this as spec corrected: [ObserverSet] documents each
    observer as held at most once, and [observer_held_once] proves that of every reachable world. *)
Example walk_covers_duplicate_observer :
  let o := {| oitems := [(1, 0); (2, 0)]; olatest := 1 |} in
  let muts := [[WAdd 1 1; WAdd 3 0; WRemove 1 0]; []] in
  (exists o', walk (length (oitems o)) o muts [2; 3] o') /\
  Forall (fun p => 1 ∈ obs_view p) (mut_points (concat muts) o) /\
  1 ∉ [2; 3].
Proof.
  simpl. split_and!.
  - eexists. walk_steps.
  - repeat apply Forall_cons_2; try apply Forall_nil_2; cbv beta; apply view_elem;
      first [exists 0; eexists; reflexivity | exists 1; eexists; reflexivity | exists 2; eexists; reflexivity].
  - set_solver.
Qed.

(** A removal below the cursor moves the visited last observer into the hole, and the walk visits it
    again. *)
Example walk_visits_moved_twice :
  let o := {| oitems := [(1, 0); (2, 0); (3, 0)]; olatest := 2 |} in
  exists o', walk (length (oitems o)) o [[WRemove 1 0]; []; []] [3; 2; 3] o'.
Proof. simpl. eexists. walk_steps. Qed.

(** An observer added during a walk is visited when a later removal moves it below the cursor. *)
Example walk_visits_added_observer :
  let o := {| oitems := [(1, 0); (2, 0); (3, 0); (4, 0)]; olatest := 3 |} in
  exists o', walk (length (oitems o)) o [[WAdd 5 0; WRemove 1 0]; []; []; []] [4; 3; 2; 5] o'.
Proof. simpl. eexists. walk_steps. Qed.
