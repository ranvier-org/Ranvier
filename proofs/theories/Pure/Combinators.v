From stdpp Require Import prelude.
From Ranvier.Base Require Import Prelude.

Open Scope nat_scope.

(**md # Closed-window `*On` primitives, reactive combinators, and unobserved `map`-chain fusion

This module formalizes and verifies the pure state machines and algebraic refinements (`B16`–`B20`)
for Ranvier's closed-window `*On` split-phase computations (`createMemoOn*`, `createAsyncOn*`,
`createStreamOn*`), static-source combinators (`map`, `map2`, `zip`, `bind`), and single-consumer
unobserved `map`-chain fusion (`MapFusion`):

1. **`B16` (`memo_on_cutoff_eq`)**: Two-phase `(read, compute)` surfaces Phase 1 `CPending`/`CFailed`
   readings while holding `(mo_has_key, mo_last_key, mo_phase2_out, mo_scope_gen)` intact, restores
   `mo_phase2_out` with `0` Phase 2 invocations and unchanged `mo_scope_gen` when Phase 1 settles to
   an `eq_K`-equal key (including after arbitrary transient `CPending`/`CFailed` excursions), and
   increments `mo_scope_gen` and `mo_compute_runs` only on initial or `eq_K`-unequal settled keys.
2. **`B17` (`async_on_flight_preservation_eq`)**: Across all six `FlightPolicy` constructors
   (`CancelPrevious`, `KeepLatest`, `Queue`, `FinishCurrent`, `Drop`, `DropWhilePending`), settling
   on an `eq_K`-equal key clears any Phase 1 overlay and preserves `ao_flight_gen`, `ao_queued`,
   `ao_scope_gen`, and `ao_phase2_slot`; Phase 1 unready or unequal keys while `ao_queued <> 0` under
   `FinishCurrent`/`Drop`/`DropWhilePending` set `ao_queued = 2` without clobbering `ao_last_key`;
   background flight completion during a Phase 1 unready overlay updates `ao_phase2_slot` and surfaces
   immediately once Phase 1 recovers to `ao_last_key`; and `Queue` drains out-of-order completions in
   FIFO start order.
3. **`B18` (`fused_map_chain_eq_unfused`)**: Over heterogeneous $k$-stage `map` chains with arbitrary
   boolean stage cutoffs `eq_i` (no Leibniz equality requirement) and explicit state `St` (capturing
   allocation counters and heap-identity tokens), fused evaluation with per-stage cached ready values
   and unready cache resets produces the exact same `(St * comb_reading B)` and `origin` as sequential
   unfused `Memo.map` evaluation across `CPending`, upstream `CFailed`, stage throw at index `i`,
   intermediate cutoff early-exit at stage `i`, and arbitrary multi-step input traces.
4. **`B19` (`map2_zip_status_precedence_eq`)**: Static two-input `eval_map2` (both-ready fast path
   plus left-to-right unready precedence) and `eval_zip` match sequential monadic
   `let! x = r1 in let! y = r2 in f x y` across all `3 * 3` `comb_reading` pairs while preserving
   `St`, failure `origin`, and static two-slot upstream edges.
5. **`B20` (`split_bind_inner_tick_eq`)**: Split `bind` invokes `binder` and discharges the previous
   outer scope if and only if outer settles to a new `eq_K`-unequal key, refreshes the bound inner
   source across arbitrary inner `U` transitions with `0` `binder` invocations and `0` scope
   discharges, and preserves the outer key, scope, and inner link across transient outer
   `CPending`/`CFailed` excursions recovering to an `eq_K`-equal key. *)

(**md Outcome of reading a reactive source or derived node, carrying the pending source id on
    suspension and both the exception id and `ErrorOrigin` node id on failure. *)
Inductive comb_reading (V : Type) : Type :=
  | CReady (v : V)
  | CPending (src : nat)
  | CFailed (ex : nat) (origin : nat).

Arguments CReady {V} _.
Arguments CPending {V} _.
Arguments CFailed {V} _ _.

(**md Outcome of a synchronous closed-window callback (`compute`, `mapping`, or `binder`). *)
Inductive result (V : Type) : Type :=
  | RVal (v : V)
  | RThrow (ex : nat).

Arguments RVal {V} _.
Arguments RThrow {V} _.

(**md True when a `comb_reading` is `CPending` or `CFailed`. *)
Definition is_unready_reading {V : Type} (r : comb_reading V) : bool :=
  match r with
  | CReady _ => false
  | CPending _ | CFailed _ _ => true
  end.

(**md Converts a callback `result` on node `self_id` into a `comb_reading` attributing throws to `self_id`. *)
Definition result_to_reading {V : Type} (self_id : nat) (res : result V) : comb_reading V :=
  match res with
  | RVal v => CReady v
  | RThrow ex => CFailed ex self_id
  end.

(**md Projects the unready reading across value types when `is_unready_reading r = true`. *)
Definition project_unready {A B : Type} (r : comb_reading A) (fallback : comb_reading B) : comb_reading B :=
  match r with
  | CReady _ => fallback
  | CPending src => CPending src
  | CFailed ex orig => CFailed ex orig
  end.

(** ** B16. Closed-window MemoOn settled-key cutoff and transient-unready preservation *)

(**md Pure state of a two-phase `createMemoOn` / `createMemoWithOn` node. *)
Record memo_on_state (K U : Type) := {
  mo_has_key : bool;
  mo_last_key : K;
  mo_phase2_out : comb_reading U;
  mo_visible : comb_reading U;
  mo_scope_gen : nat;
  mo_compute_runs : nat;
}.

Arguments Build_memo_on_state {K U} _ _ _ _ _ _.
Arguments mo_has_key {K U} _.
Arguments mo_last_key {K U} _.
Arguments mo_phase2_out {K U} _.
Arguments mo_visible {K U} _.
Arguments mo_scope_gen {K U} _.
Arguments mo_compute_runs {K U} _.

(**md Single-step transition of `createMemoOn` / `createMemoWithOn` on Phase 1 reading `r1`. *)
Definition memo_on_step {K U : Type}
    (self_id : nat) (eq_K : K -> K -> bool) (compute : K -> result U)
    (r1 : comb_reading K) (st : memo_on_state K U) : memo_on_state K U :=
  match r1 with
  | CPending src =>
      {| mo_has_key := mo_has_key st;
         mo_last_key := mo_last_key st;
         mo_phase2_out := mo_phase2_out st;
         mo_visible := CPending src;
         mo_scope_gen := mo_scope_gen st;
         mo_compute_runs := mo_compute_runs st |}
  | CFailed ex orig =>
      {| mo_has_key := mo_has_key st;
         mo_last_key := mo_last_key st;
         mo_phase2_out := mo_phase2_out st;
         mo_visible := CFailed ex orig;
         mo_scope_gen := mo_scope_gen st;
         mo_compute_runs := mo_compute_runs st |}
  | CReady k =>
      if mo_has_key st && eq_K (mo_last_key st) k then
        {| mo_has_key := true;
           mo_last_key := mo_last_key st;
           mo_phase2_out := mo_phase2_out st;
           mo_visible := mo_phase2_out st;
           mo_scope_gen := mo_scope_gen st;
           mo_compute_runs := mo_compute_runs st |}
      else
        let out := result_to_reading self_id (compute k) in
        {| mo_has_key := true;
           mo_last_key := k;
           mo_phase2_out := out;
           mo_visible := out;
           mo_scope_gen := S (mo_scope_gen st);
           mo_compute_runs := S (mo_compute_runs st) |}
  end.

(**md Multi-step iteration of `memo_on_step` over a sequence of Phase 1 readings. *)
Fixpoint memo_on_steps {K U : Type}
    (self_id : nat) (eq_K : K -> K -> bool) (compute : K -> result U)
    (rs : list (comb_reading K)) (st : memo_on_state K U) : memo_on_state K U :=
  match rs with
  | [] => st
  | r :: rest => memo_on_steps self_id eq_K compute rest (memo_on_step self_id eq_K compute r st)
  end.

Lemma memo_on_steps_unready_preserves {K U : Type}
    (self_id : nat) (eq_K : K -> K -> bool) (compute : K -> result U)
    (us : list (comb_reading K)) (st : memo_on_state K U) :
  Forall (fun r => is_unready_reading r = true) us ->
  let st' := memo_on_steps self_id eq_K compute us st in
  mo_has_key st' = mo_has_key st /\
  mo_last_key st' = mo_last_key st /\
  mo_phase2_out st' = mo_phase2_out st /\
  mo_scope_gen st' = mo_scope_gen st /\
  mo_compute_runs st' = mo_compute_runs st.
Proof.
  revert st. induction us as [|r rest IH]; intros st Hall; simpl.
  - repeat split; reflexivity.
  - inversion Hall as [|? ? Hr Hrest]; subst.
    destruct r as [v|src|ex orig]; [simpl in Hr; discriminate| |];
      exact (IH _ Hrest).
Qed.

Lemma memo_on_steps_app {K U : Type}
    (self_id : nat) (eq_K : K -> K -> bool) (compute : K -> result U)
    (rs1 rs2 : list (comb_reading K)) (st : memo_on_state K U) :
  memo_on_steps self_id eq_K compute (rs1 ++ rs2) st =
  memo_on_steps self_id eq_K compute rs2 (memo_on_steps self_id eq_K compute rs1 st).
Proof.
  revert st. induction rs1 as [|r rest IH]; intros st; simpl; [reflexivity|apply IH].
Qed.

(**md
**Two-phase `memo_on_step` surfaces Phase 1 `CPending`/`CFailed` while preserving `(mo_has_key, mo_last_key, mo_phase2_out, mo_scope_gen)`, restores `mo_phase2_out` with zero Phase 2 runs on `eq_K`-equal settled keys (even after arbitrary unready excursions), and increments `mo_scope_gen` and `mo_compute_runs` only on initial or `eq_K`-unequal settled keys.**

Matches `createMemoOn`, `createMemoOnWithComparer`, `createMemoWithOn`, and `createOwningMemoOnWithComparer` (`Core.fs`, `Api.fs`):
Phase 1 unready readings never clobber the Phase 2 key or discharge its owned scope, equal settled keys restore the cached Phase 2 reading (`CReady` or Phase 2 `CFailed`), and unequal settled keys discharge the prior scope generation and invoke `compute` once.

- [K:] Phase 1 key type.
- [U:] Phase 2 output value type.
- [self_id:] node id of the `Memo`, used as `origin` if `compute` throws.
- [eq_K:] boolean cutoff predicate over settled `K` keys.
- [compute:] closed-window Phase 2 body.
- [r1:] single-step Phase 1 reading.
- [k:] settled Phase 1 key.
- [us:] transient Phase 1 unready excursion trace.
- [st:] state before the step or excursion.
*)
Theorem memo_on_cutoff_eq (K U : Type)
    (self_id : nat) (eq_K : K -> K -> bool) (compute : K -> result U)
    (r1 : comb_reading K) (k : K) (us : list (comb_reading K)) (st : memo_on_state K U) :
  (is_unready_reading r1 = true ->
     let st' := memo_on_step self_id eq_K compute r1 st in
     mo_visible st' = project_unready r1 (mo_visible st) /\
     mo_has_key st' = mo_has_key st /\
     mo_last_key st' = mo_last_key st /\
     mo_phase2_out st' = mo_phase2_out st /\
     mo_scope_gen st' = mo_scope_gen st /\
     mo_compute_runs st' = mo_compute_runs st) /\
  (mo_has_key st = true -> eq_K (mo_last_key st) k = true ->
     Forall (fun r => is_unready_reading r = true) us ->
     let st' := memo_on_steps self_id eq_K compute (us ++ [CReady k]) st in
     mo_visible st' = mo_phase2_out st /\
     mo_phase2_out st' = mo_phase2_out st /\
     mo_has_key st' = true /\
     mo_last_key st' = mo_last_key st /\
     mo_scope_gen st' = mo_scope_gen st /\
     mo_compute_runs st' = mo_compute_runs st) /\
  (mo_has_key st = false \/ eq_K (mo_last_key st) k = false ->
     let st' := memo_on_step self_id eq_K compute (CReady k) st in
     mo_has_key st' = true /\
     mo_last_key st' = k /\
     mo_phase2_out st' = result_to_reading self_id (compute k) /\
     mo_visible st' = result_to_reading self_id (compute k) /\
     mo_scope_gen st' = S (mo_scope_gen st) /\
     mo_compute_runs st' = S (mo_compute_runs st)).
Proof.
  split_and!.
  - intros Hunr. destruct r1 as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
      simpl; repeat split; reflexivity.
  - intros Hhas Heq Hall.
    rewrite memo_on_steps_app. simpl.
    pose proof (memo_on_steps_unready_preserves self_id eq_K compute us st Hall)
      as (Hhk & Hlk & Hp2 & Hsg & Hcr).
    unfold memo_on_step.
    rewrite Hhk, Hlk, Hhas, Heq. simpl.
    rewrite Hp2, Hsg, Hcr.
    repeat split; reflexivity.
  - intros Hnew. unfold memo_on_step.
    destruct Hnew as [Hno | Hneq].
    + rewrite Hno. simpl. repeat split; reflexivity.
    + rewrite Hneq, andb_false_r. repeat split; reflexivity.
Qed.

(** ** B17. Closed-window AsyncOn six-policy flight preservation *)

(**md All six `FlightPolicy` constructors of `Ranvier.FlightPolicy` (`Types.fs`). *)
Inductive flight_policy : Type :=
  | CancelPrevious
  | KeepLatest
  | Queue
  | FinishCurrent
  | Drop
  | DropWhilePending.

Global Instance flight_policy_eq_dec : EqDecision flight_policy.
Proof. solve_decision. Defined.

(**md Policies whose active flights defer or discard intervening changes via `queued = 2` (`Core.fs:14315`). *)
Definition is_deferring_policy (p : flight_policy) : bool :=
  match p with
  | FinishCurrent | Drop | DropWhilePending => true
  | CancelPrevious | KeepLatest | Queue => false
  end.

(**md Pure state of a two-phase `AsyncMemo` `*On` node across all six flight policies. *)
Record async_on_state (K U : Type) := {
  ao_policy : flight_policy;
  ao_self_id : nat;
  ao_has_key : bool;
  ao_last_key : K;
  ao_flight_gen : nat;
  ao_queued : nat;
  ao_flying : nat;
  ao_scope_gen : nat;
  ao_phase2_slot : comb_reading U;
  ao_phase1_overlay : option (comb_reading U);
  ao_fifo : list (nat * option (result U));
}.

Arguments Build_async_on_state {K U} _ _ _ _ _ _ _ _ _ _ _.
Arguments ao_policy {K U} _.
Arguments ao_self_id {K U} _.
Arguments ao_has_key {K U} _.
Arguments ao_last_key {K U} _.
Arguments ao_flight_gen {K U} _.
Arguments ao_queued {K U} _.
Arguments ao_flying {K U} _.
Arguments ao_scope_gen {K U} _.
Arguments ao_phase2_slot {K U} _.
Arguments ao_phase1_overlay {K U} _.
Arguments ao_fifo {K U} _.

(**md Externally visible reading of an `AsyncMemo` `*On` node: Phase 1 overlay when active, otherwise Phase 2's slot. *)
Definition ao_visible {K U : Type} (st : async_on_state K U) : comb_reading U :=
  match ao_phase1_overlay st with
  | Some r1 => r1
  | None => ao_phase2_slot st
  end.

(**md Launches a new untracked Phase 2 flight for settled key `k`, advancing `ao_flight_gen` and `ao_scope_gen`. *)
Definition async_on_launch {K U : Type} (k : K) (st : async_on_state K U) : async_on_state K U :=
  let next_gen := S (ao_flight_gen st) in
  let '(next_queued, next_flying, next_fifo) :=
    match ao_policy st with
    | CancelPrevious => (0, 1, [])
    | KeepLatest => (0, S (ao_flying st), [])
    | Queue => (S (ao_queued st), 0, ao_fifo st ++ [(next_gen, None)])
    | FinishCurrent | Drop | DropWhilePending => (1, 0, [])
    end in
  {| ao_policy := ao_policy st;
     ao_self_id := ao_self_id st;
     ao_has_key := true;
     ao_last_key := k;
     ao_flight_gen := next_gen;
     ao_queued := next_queued;
     ao_flying := next_flying;
     ao_scope_gen := S (ao_scope_gen st);
     ao_phase2_slot := CPending (ao_self_id st);
     ao_phase1_overlay := None;
     ao_fifo := next_fifo |}.

(**md Phase 1 refresh step of `createAsyncOn*` across all six `FlightPolicy` constructors. *)
Definition async_on_step {K U : Type}
    (eq_K : K -> K -> bool) (r1 : comb_reading K) (st : async_on_state K U)
    : async_on_state K U :=
  match r1 with
  | CReady k =>
      if ao_has_key st && eq_K (ao_last_key st) k then
        {| ao_policy := ao_policy st;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := ao_queued st;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := None;
           ao_fifo := ao_fifo st |}
      else if is_deferring_policy (ao_policy st) && negb (Nat.eqb (ao_queued st) 0) then
        {| ao_policy := ao_policy st;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 2;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := None;
           ao_fifo := ao_fifo st |}
      else
        async_on_launch k st
  | CPending src =>
      if is_deferring_policy (ao_policy st) && negb (Nat.eqb (ao_queued st) 0) then
        {| ao_policy := ao_policy st;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 2;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := ao_phase1_overlay st;
           ao_fifo := ao_fifo st |}
      else
        {| ao_policy := ao_policy st;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := ao_queued st;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (CPending src);
           ao_fifo := ao_fifo st |}
  | CFailed ex orig =>
      if is_deferring_policy (ao_policy st) && negb (Nat.eqb (ao_queued st) 0) then
        {| ao_policy := ao_policy st;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 2;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := ao_phase1_overlay st;
           ao_fifo := ao_fifo st |}
      else
        match ao_policy st with
        | Queue =>
            if negb (Nat.eqb (ao_queued st) 0) then
              let fail_gen := S (ao_flight_gen st) in
              {| ao_policy := Queue;
                 ao_self_id := ao_self_id st;
                 ao_has_key := ao_has_key st;
                 ao_last_key := ao_last_key st;
                 ao_flight_gen := fail_gen;
                 ao_queued := S (ao_queued st);
                 ao_flying := ao_flying st;
                 ao_scope_gen := ao_scope_gen st;
                 ao_phase2_slot := ao_phase2_slot st;
                 ao_phase1_overlay := ao_phase1_overlay st;
                 ao_fifo := ao_fifo st ++ [(fail_gen, Some (RThrow ex))] |}
            else
              {| ao_policy := Queue;
                 ao_self_id := ao_self_id st;
                 ao_has_key := ao_has_key st;
                 ao_last_key := ao_last_key st;
                 ao_flight_gen := ao_flight_gen st;
                 ao_queued := 0;
                 ao_flying := ao_flying st;
                 ao_scope_gen := ao_scope_gen st;
                 ao_phase2_slot := ao_phase2_slot st;
                 ao_phase1_overlay := Some (CFailed ex orig);
                 ao_fifo := ao_fifo st |}
        | _ =>
            {| ao_policy := ao_policy st;
               ao_self_id := ao_self_id st;
               ao_has_key := ao_has_key st;
               ao_last_key := ao_last_key st;
               ao_flight_gen := ao_flight_gen st;
               ao_queued := ao_queued st;
               ao_flying := ao_flying st;
               ao_scope_gen := ao_scope_gen st;
               ao_phase2_slot := ao_phase2_slot st;
               ao_phase1_overlay := Some (CFailed ex orig);
               ao_fifo := ao_fifo st |}
        end
  end.

(**md Records a settled flight outcome in `Queue`'s start-ordered FIFO buffer. *)
Fixpoint fifo_record {U : Type} (gen : nat) (out : result U)
    (q : list (nat * option (result U))) : list (nat * option (result U)) :=
  match q with
  | [] => []
  | (g, cur) :: rest =>
      if Nat.eqb g gen then (g, Some out) :: rest
      else (g, cur) :: fifo_record gen out rest
  end.

(**md Drains consecutive settled head entries under `FlightPolicy.Queue` (`ApplyQueued` in `Core.fs:14247`). *)
Fixpoint fifo_drain {U : Type} (self_id : nat) (queued : nat) (slot : comb_reading U)
    (q : list (nat * option (result U)))
    : nat * comb_reading U * list (nat * option (result U)) :=
  match q with
  | (_, Some out) :: rest =>
      fifo_drain self_id (Nat.pred queued) (result_to_reading self_id out) rest
  | _ => (queued, slot, q)
  end.

(**md Background Phase 2 flight completion (`ApplyResult` / `ApplyQueued` in `Core.fs:14113`), updating
    `ao_phase2_slot` without clearing any active Phase 1 unready overlay. *)
Definition async_on_settle {K U : Type}
    (gen : nat) (out : result U) (st : async_on_state K U) : async_on_state K U :=
  let settled := result_to_reading (ao_self_id st) out in
  match ao_policy st with
  | CancelPrevious | KeepLatest =>
      let next_slot :=
        if Nat.eqb gen (ao_flight_gen st) then settled else ao_phase2_slot st in
      {| ao_policy := ao_policy st;
         ao_self_id := ao_self_id st;
         ao_has_key := ao_has_key st;
         ao_last_key := ao_last_key st;
         ao_flight_gen := ao_flight_gen st;
         ao_queued := ao_queued st;
         ao_flying := Nat.pred (ao_flying st);
         ao_scope_gen := ao_scope_gen st;
         ao_phase2_slot := next_slot;
         ao_phase1_overlay := ao_phase1_overlay st;
         ao_fifo := ao_fifo st |}
  | Queue =>
      let '(q', slot', fifo') :=
        fifo_drain (ao_self_id st) (ao_queued st) (ao_phase2_slot st)
          (fifo_record gen out (ao_fifo st)) in
      {| ao_policy := Queue;
         ao_self_id := ao_self_id st;
         ao_has_key := ao_has_key st;
         ao_last_key := ao_last_key st;
         ao_flight_gen := ao_flight_gen st;
         ao_queued := q';
         ao_flying := ao_flying st;
         ao_scope_gen := ao_scope_gen st;
         ao_phase2_slot := slot';
         ao_phase1_overlay := ao_phase1_overlay st;
         ao_fifo := fifo' |}
  | FinishCurrent =>
      let owed := Nat.eqb (ao_queued st) 2 in
      let next_slot :=
        if Nat.eqb gen (ao_flight_gen st) then
          if owed then
            match out with
            | RVal u => CReady u
            | RThrow _ => CPending (ao_self_id st)
            end
          else settled
        else ao_phase2_slot st in
      {| ao_policy := FinishCurrent;
         ao_self_id := ao_self_id st;
         ao_has_key := ao_has_key st;
         ao_last_key := ao_last_key st;
         ao_flight_gen := ao_flight_gen st;
         ao_queued := 0;
         ao_flying := ao_flying st;
         ao_scope_gen := ao_scope_gen st;
         ao_phase2_slot := next_slot;
         ao_phase1_overlay := ao_phase1_overlay st;
         ao_fifo := ao_fifo st |}
  | Drop =>
      let next_slot :=
        if Nat.eqb gen (ao_flight_gen st) then settled else ao_phase2_slot st in
      {| ao_policy := Drop;
         ao_self_id := ao_self_id st;
         ao_has_key := ao_has_key st;
         ao_last_key := ao_last_key st;
         ao_flight_gen := ao_flight_gen st;
         ao_queued := 0;
         ao_flying := ao_flying st;
         ao_scope_gen := ao_scope_gen st;
         ao_phase2_slot := next_slot;
         ao_phase1_overlay := ao_phase1_overlay st;
         ao_fifo := ao_fifo st |}
  | DropWhilePending =>
      let owed :=
        match out with
        | RVal _ => false
        | RThrow _ => Nat.eqb (ao_queued st) 2
        end in
      let next_slot :=
        if Nat.eqb gen (ao_flight_gen st) then
          if owed then CPending (ao_self_id st) else settled
        else ao_phase2_slot st in
      {| ao_policy := DropWhilePending;
         ao_self_id := ao_self_id st;
         ao_has_key := ao_has_key st;
         ao_last_key := ao_last_key st;
         ao_flight_gen := ao_flight_gen st;
         ao_queued := 0;
         ao_flying := ao_flying st;
         ao_scope_gen := ao_scope_gen st;
         ao_phase2_slot := next_slot;
         ao_phase1_overlay := ao_phase1_overlay st;
         ao_fifo := ao_fifo st |}
  end.

(**md Active background flight precondition across all six policies when Phase 1 enters an unready overlay `r_unr`. *)
Definition active_overlay_flight {K U : Type} (r_unr : comb_reading K) (st : async_on_state K U) : Prop :=
  match ao_policy st with
  | CancelPrevious | KeepLatest => True
  | Queue =>
      ao_fifo st = [(ao_flight_gen st, None)] /\
      ao_queued st = 1 /\
      exists src, r_unr = CPending src
  | FinishCurrent | Drop | DropWhilePending => ao_queued st = 0
  end.

(**md
**Across all six `FlightPolicy` constructors, `async_on_step` preserves `ao_flight_gen`, `ao_queued`, `ao_scope_gen`, and `ao_phase2_slot` on `eq_K`-equal settled keys, marks `ao_queued = 2` without clobbering `ao_last_key` on unready or unequal keys while `ao_queued <> 0` under `FinishCurrent`/`Drop`/`DropWhilePending`, surfaces background flight completions immediately once Phase 1 recovers from an unready overlay to `ao_last_key`, and drains out-of-order `Queue` completions in FIFO start order.**

Certifies the two-phase `AsyncMemo` `*On` engine (`createAsyncOn`, `createAsyncWithOn`, `createAsyncOnWithPolicy`, `createOwningAsyncOnWithPolicy`, `AsyncMemo.bind*`, and `AsyncSource.bind`):
1. Equal settled keys clear `ao_phase1_overlay` and keep active flights, `ao_queued`, and settled slots intact under every `FlightPolicy`.
2. Intervening `CPending`/`CFailed` or unequal `CReady k'` during an active deferring flight (`ao_queued <> 0`) sets `ao_queued = 2` while retaining `ao_last_key` and `ao_flight_gen`.
3. Under `active_overlay_flight r_unr st` (covering all six policies), when Phase 1 enters `r_unr` and `ao_flight_gen`'s background flight settles, `ao_phase2_slot` updates behind the overlay and surfaces immediately with zero new launches once Phase 1 settles back to an `eq_K`-equal key.
4. Under `Queue`, out-of-order completions `g2` then `g1` (`g1 <> g2`) hold `ao_phase2_slot` until `g1` arrives and apply `out1` then `out2` in start order.

- [K:] Phase 1 key type.
- [U:] Phase 2 async result type.
- [eq_K:] boolean cutoff predicate over settled `K` keys.
- [k:] settled Phase 1 key matching `ao_last_key`.
- [r_chg:] Phase 1 unready or unequal key arriving during an active deferring flight.
- [r_unr:] Phase 1 unready reading creating an overlay.
- [out:] background flight completion outcome.
- [out1:] outcome of earlier queued flight `g1`.
- [out2:] outcome of later queued flight `g2`.
- [g1:] generation of first queued flight under `Queue`.
- [g2:] generation of second queued flight under `Queue`.
- [st:] `async_on_state` before the step.
*)
Theorem async_on_flight_preservation_eq (K U : Type)
    (eq_K : K -> K -> bool) (k : K)
    (r_chg r_unr : comb_reading K)
    (out out1 out2 : result U) (g1 g2 : nat)
    (st : async_on_state K U) :
  (ao_has_key st = true -> eq_K (ao_last_key st) k = true ->
     let st' := async_on_step eq_K (CReady k) st in
     ao_flight_gen st' = ao_flight_gen st /\
     ao_queued st' = ao_queued st /\
     ao_scope_gen st' = ao_scope_gen st /\
     ao_last_key st' = ao_last_key st /\
     ao_phase2_slot st' = ao_phase2_slot st /\
     ao_phase1_overlay st' = None /\
     ao_visible st' = ao_phase2_slot st) /\
  (is_deferring_policy (ao_policy st) = true -> ao_queued st <> 0 ->
     (is_unready_reading r_chg = true \/
      exists k', r_chg = CReady k' /\ (ao_has_key st = false \/ eq_K (ao_last_key st) k' = false)) ->
     let st' := async_on_step eq_K r_chg st in
     ao_queued st' = 2 /\
     ao_last_key st' = ao_last_key st /\
     ao_flight_gen st' = ao_flight_gen st /\
     ao_scope_gen st' = ao_scope_gen st /\
     ao_phase2_slot st' = ao_phase2_slot st) /\
  (ao_has_key st = true -> eq_K (ao_last_key st) k = true ->
     is_unready_reading r_unr = true ->
     active_overlay_flight r_unr st ->
     let st_ov := async_on_step eq_K r_unr st in
     let st_bg := async_on_settle (ao_flight_gen st) out st_ov in
     let st_rec := async_on_step eq_K (CReady k) st_bg in
     ao_visible st_bg = project_unready r_unr (ao_phase2_slot st) /\
     ao_phase2_slot st_bg = result_to_reading (ao_self_id st) out /\
     ao_visible st_rec = result_to_reading (ao_self_id st) out /\
     ao_flight_gen st_rec = ao_flight_gen st /\
     ao_scope_gen st_rec = ao_scope_gen st /\
     ao_last_key st_rec = ao_last_key st) /\
  (ao_policy st = Queue -> g1 <> g2 ->
     ao_queued st = 2 -> ao_fifo st = [(g1, None); (g2, None)] ->
     let st_early := async_on_settle g2 out2 st in
     let st_both := async_on_settle g1 out1 st_early in
     ao_phase2_slot st_early = ao_phase2_slot st /\
     ao_queued st_early = 2 /\
     ao_queued st_both = 0 /\
     ao_fifo st_both = [] /\
     ao_phase2_slot st_both = result_to_reading (ao_self_id st) out2).
Proof.
  split_and!.
  - intros Hhas Heq.
    destruct (ao_policy st); unfold async_on_step, ao_visible;
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
  - intros Hdef Hq Hchg.
    assert (Hq_nz : negb (Nat.eqb (ao_queued st) 0) = true).
    { destruct (Nat.eqb (ao_queued st) 0) eqn:Eq0; [|reflexivity].
      apply Nat.eqb_eq in Eq0. contradiction. }
    destruct Hchg as [Hunr | (k' & -> & Hnew)].
    + destruct r_chg as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
        unfold async_on_step; rewrite Hdef, Hq_nz; simpl; repeat split; reflexivity.
    + unfold async_on_step.
      destruct Hnew as [Hno | Hneq];
        [rewrite Hno | rewrite Hneq, andb_false_r];
        rewrite Hdef, Hq_nz; simpl; repeat split; reflexivity.
  - intros Hhas Heq Hunr Hact.
    unfold active_overlay_flight in Hact.
    destruct (ao_policy st) eqn:Epol.
    + assert (Hov :
        async_on_step eq_K r_unr st =
        {| ao_policy := CancelPrevious;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := ao_queued st;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (project_unready r_unr (ao_phase2_slot st));
           ao_fifo := ao_fifo st |})
        by (destruct r_unr as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
            unfold async_on_step; rewrite Epol; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl.
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen].
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
    + assert (Hov :
        async_on_step eq_K r_unr st =
        {| ao_policy := KeepLatest;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := ao_queued st;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (project_unready r_unr (ao_phase2_slot st));
           ao_fifo := ao_fifo st |})
        by (destruct r_unr as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
            unfold async_on_step; rewrite Epol; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl.
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen].
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
    + destruct Hact as (Hfifo & Hq1 & src & ->).
      assert (Hov :
        async_on_step eq_K (CPending src) st =
        {| ao_policy := Queue;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 1;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (CPending src);
           ao_fifo := [(ao_flight_gen st, None)] |})
        by (unfold async_on_step; rewrite Epol, Hq1, Hfifo; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle fifo_record ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl; cbn [fifo_drain Nat.pred].
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen].
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
    + assert (Hov :
        async_on_step eq_K r_unr st =
        {| ao_policy := FinishCurrent;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 0;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (project_unready r_unr (ao_phase2_slot st));
           ao_fifo := ao_fifo st |})
        by (destruct r_unr as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
            unfold async_on_step; rewrite Epol, Hact; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl; simpl.
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen].
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
    + assert (Hov :
        async_on_step eq_K r_unr st =
        {| ao_policy := Drop;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 0;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (project_unready r_unr (ao_phase2_slot st));
           ao_fifo := ao_fifo st |})
        by (destruct r_unr as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
            unfold async_on_step; rewrite Epol, Hact; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl; simpl.
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen].
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
    + assert (Hov :
        async_on_step eq_K r_unr st =
        {| ao_policy := DropWhilePending;
           ao_self_id := ao_self_id st;
           ao_has_key := ao_has_key st;
           ao_last_key := ao_last_key st;
           ao_flight_gen := ao_flight_gen st;
           ao_queued := 0;
           ao_flying := ao_flying st;
           ao_scope_gen := ao_scope_gen st;
           ao_phase2_slot := ao_phase2_slot st;
           ao_phase1_overlay := Some (project_unready r_unr (ao_phase2_slot st));
           ao_fifo := ao_fifo st |})
        by (destruct r_unr as [v|src|ex orig]; [simpl in Hunr; discriminate| |];
            unfold async_on_step; rewrite Epol, Hact; reflexivity).
      cbv zeta; rewrite Hov; cbn [async_on_settle ao_policy ao_flight_gen ao_self_id ao_has_key ao_last_key ao_queued ao_flying ao_scope_gen ao_phase2_slot ao_phase1_overlay ao_fifo].
      rewrite Nat.eqb_refl; destruct out; simpl;
      cbn [async_on_step ao_visible ao_has_key ao_last_key ao_phase1_overlay ao_phase2_slot ao_flight_gen ao_scope_gen];
      rewrite Hhas, Heq; simpl; repeat split; reflexivity.
  - intros Hpol Hg12 Hq2 Hfifo.
    cbv zeta. unfold async_on_settle.
    rewrite Hpol, Hq2, Hfifo. simpl.
    assert (Hne12 : Nat.eqb g1 g2 = false) by (apply Nat.eqb_neq; exact Hg12).
    rewrite Hne12, Nat.eqb_refl. simpl.
    rewrite Nat.eqb_refl. simpl.
    repeat split; reflexivity.
Qed.

(** ** B18. Unobserved map-chain fusion equivalence with per-stage cutoffs *)

(**md Declarative heterogeneous $k$-stage `map` specification from `A` to `B` over state `St`. *)
Inductive chain_spec (St : Type) : Type -> Type -> Type :=
  | CS_Single :
      forall {A B : Type}
        (cs_id : nat)
        (cs_eq : B -> B -> bool)
        (cs_step : St -> A -> St * result B),
        chain_spec St A B
  | CS_Cons :
      forall {A M B : Type}
        (cs_id : nat)
        (cs_eq : M -> M -> bool)
        (cs_step : St -> A -> St * result M),
        chain_spec St M B ->
        chain_spec St A B.

(**md Unfused sequential `Memo.map` chain state: each stage holds its own cached `option (comb_reading M)`. *)
Inductive unfused_chain (St : Type) : Type -> Type -> Type :=
  | UC_Single :
      forall {A B : Type}
        (uc_id : nat)
        (uc_eq : B -> B -> bool)
        (uc_cached : option (comb_reading B))
        (uc_step : St -> A -> St * result B),
        unfused_chain St A B
  | UC_Cons :
      forall {A M B : Type}
        (uc_id : nat)
        (uc_eq : M -> M -> bool)
        (uc_cached : option (comb_reading M))
        (uc_step : St -> A -> St * result M),
        unfused_chain St M B ->
        unfused_chain St A B.

(**md Fused `MapFusion` stage spine: each stage stores only its last settled ready value `option M`. *)
Inductive fused_stages (St : Type) : Type -> Type -> Type :=
  | FS_Single :
      forall {A B : Type}
        (fs_id : nat)
        (fs_eq : B -> B -> bool)
        (fs_ready : option B)
        (fs_step : St -> A -> St * result B),
        fused_stages St A B
  | FS_Cons :
      forall {A M B : Type}
        (fs_id : nat)
        (fs_eq : M -> M -> bool)
        (fs_ready : option M)
        (fs_step : St -> A -> St * result M),
        fused_stages St M B ->
        fused_stages St A B.

Arguments CS_Single {St A B} _ _ _.
Arguments CS_Cons {St A M B} _ _ _ _.
Arguments UC_Single {St A B} _ _ _ _.
Arguments UC_Cons {St A M B} _ _ _ _ _.
Arguments FS_Single {St A B} _ _ _ _.
Arguments FS_Cons {St A M B} _ _ _ _ _.

(**md Complete state of a fused `MapFusion` chain: stage ready-value slots plus the tail's cached reading. *)
Record fused_chain (St A B : Type) := {
  fc_stages : fused_stages St A B;
  fc_tail_out : option (comb_reading B);
}.

Arguments Build_fused_chain {St A B} _ _.
Arguments fc_stages {St A B} _.
Arguments fc_tail_out {St A B} _.

(**md Compiles a `chain_spec` into its initial unpulled unfused `Memo.map` chain. *)
Fixpoint init_unfused_chain {St A B : Type} (spec : chain_spec St A B) : unfused_chain St A B :=
  match spec with
  | CS_Single id eq_b step => UC_Single id eq_b None step
  | CS_Cons id eq_m step rest => UC_Cons id eq_m None step (init_unfused_chain rest)
  end.

(**md Compiles a `chain_spec` into its initial unpulled `fused_stages` spine. *)
Fixpoint init_fused_stages {St A B : Type} (spec : chain_spec St A B) : fused_stages St A B :=
  match spec with
  | CS_Single id eq_b step => FS_Single id eq_b None step
  | CS_Cons id eq_m step rest => FS_Cons id eq_m None step (init_fused_stages rest)
  end.

(**md Compiles a `chain_spec` into its initial unpulled `fused_chain`. *)
Definition init_fused_chain {St A B : Type} (spec : chain_spec St A B) : fused_chain St A B :=
  Build_fused_chain (init_fused_stages spec) None.

(**md Clears `fs_ready` slots along `fused_stages` when an upstream or intermediate stage becomes unready. *)
Fixpoint clear_fused_ready {St A B : Type} (fs : fused_stages St A B) : fused_stages St A B :=
  match fs with
  | FS_Single id eq_b _ step => FS_Single id eq_b None step
  | FS_Cons id eq_m _ step rest => FS_Cons id eq_m None step (clear_fused_ready rest)
  end.

(**md Direct fused pipeline evaluation with per-stage cutoff early-exit and stage-attributed throws. *)
Fixpoint eval_fused_stages {St A B : Type}
    (st : St) (v_in : A) (prev_tail : option (comb_reading B))
    (fs : fused_stages St A B)
    : St * comb_reading B * fused_stages St A B :=
  match fs in fused_stages _ A0 B0
    return A0 -> option (comb_reading B0) -> St * comb_reading B0 * fused_stages St A0 B0
  with
  | FS_Single id eq_b _ step =>
      fun v0 _ =>
        let '(st', res) := step st v0 in
        match res with
        | RVal v_out =>
            (st', CReady v_out, FS_Single id eq_b (Some v_out) step)
        | RThrow ex =>
            (st', CFailed ex id, FS_Single id eq_b None step)
        end
  | FS_Cons id eq_m prev_m step rest =>
      fun v0 tail0 =>
        let '(st', res) := step st v0 in
        match res with
        | RThrow ex =>
            (st', CFailed ex id, FS_Cons id eq_m None step (clear_fused_ready rest))
        | RVal v_mid =>
            match prev_m, tail0 with
            | Some old_m, Some cached_tail =>
                if eq_m old_m v_mid then
                  (st', cached_tail, FS_Cons id eq_m (Some v_mid) step rest)
                else
                  let '(st'', out, rest') := eval_fused_stages st' v_mid tail0 rest in
                  (st'', out, FS_Cons id eq_m (Some v_mid) step rest')
            | _, _ =>
                let '(st'', out, rest') := eval_fused_stages st' v_mid tail0 rest in
                (st'', out, FS_Cons id eq_m (Some v_mid) step rest')
            end
        end
  end v_in prev_tail.

(**md Evaluates a fused `MapFusion` chain over root reading `r_in`. *)
Definition eval_fused_chain {St A B : Type}
    (st : St) (r_in : comb_reading A) (fc : fused_chain St A B)
    : St * comb_reading B * fused_chain St A B :=
  match r_in with
  | CPending src =>
      let out := CPending src in
      (st, out, Build_fused_chain (clear_fused_ready (fc_stages fc)) (Some out))
  | CFailed ex orig =>
      let out := CFailed ex orig in
      (st, out, Build_fused_chain (clear_fused_ready (fc_stages fc)) (Some out))
  | CReady v_in =>
      let '(st', out, stages') :=
        eval_fused_stages st v_in (fc_tail_out fc) (fc_stages fc) in
      (st', out, Build_fused_chain stages' (Some out))
  end.

(**md Stage movement test matching `Memo.Publish` (`Core.fs:12392`): compares status, failure `(ex, origin)`, or ready values via `eq_V`. *)
Definition reading_moved {V : Type} (eq_V : V -> V -> bool)
    (prev : option (comb_reading V)) (next : comb_reading V) : bool :=
  match prev, next with
  | None, _ => true
  | Some (CReady v0), CReady v1 => negb (eq_V v0 v1)
  | Some (CPending s0), CPending s1 => negb (Nat.eqb s0 s1)
  | Some (CFailed e0 o0), CFailed e1 o1 => negb (Nat.eqb e0 e1 && Nat.eqb o0 o1)
  | Some _, _ => true
  end.

(**md Evaluates a single unfused `Memo.map` stage: resolves `Check -> Clean` without running `step` when `up_moved = false`. *)
Definition eval_unfused_stage {St A M : Type}
    (id : nat) (eq_m : M -> M -> bool) (prev : option (comb_reading M))
    (step : St -> A -> St * result M)
    (st : St) (up_moved : bool) (r_in : comb_reading A)
    : St * bool * comb_reading M * option (comb_reading M) :=
  match up_moved, prev with
  | false, Some cached => (st, false, cached, Some cached)
  | _, _ =>
      let '(st', out) :=
        match r_in with
        | CPending src => (st, CPending src)
        | CFailed ex orig => (st, CFailed ex orig)
        | CReady v_in =>
            let '(st1, res) := step st v_in in
            (st1, result_to_reading id res)
        end in
      (st', reading_moved eq_m prev out, out, Some out)
  end.

(**md Sequential unfused evaluation threading `(st, up_moved, r_mid)` across every `Memo.map` node. *)
Fixpoint eval_unfused_chain_go {St A B : Type}
    (st : St) (up_moved : bool) (r_in : comb_reading A) (uc : unfused_chain St A B)
    : St * comb_reading B * unfused_chain St A B :=
  match uc in unfused_chain _ A0 B0
    return comb_reading A0 -> St * comb_reading B0 * unfused_chain St A0 B0
  with
  | UC_Single id eq_b prev step =>
      fun r0 =>
        let '(st', _, out, next_prev) :=
          eval_unfused_stage id eq_b prev step st up_moved r0 in
        (st', out, UC_Single id eq_b next_prev step)
  | UC_Cons id eq_m prev step rest =>
      fun r0 =>
        let '(st', moved, out_m, next_prev) :=
          eval_unfused_stage id eq_m prev step st up_moved r0 in
        let '(st'', out_b, rest') :=
          eval_unfused_chain_go st' moved out_m rest in
        (st'', out_b, UC_Cons id eq_m next_prev step rest')
  end r_in.

(**md Evaluates an unfused `Memo.map` chain from a root reading `r_in`. *)
Definition eval_unfused_chain {St A B : Type}
    (st : St) (r_in : comb_reading A) (uc : unfused_chain St A B)
    : St * comb_reading B * unfused_chain St A B :=
  eval_unfused_chain_go st true r_in uc.

(**md Multi-step trace evaluation of `eval_fused_chain` over an input list. *)
Fixpoint run_fused_chain {St A B : Type}
    (st : St) (inputs : list (comb_reading A)) (fc : fused_chain St A B)
    : St * list (comb_reading B) * fused_chain St A B :=
  match inputs with
  | [] => (st, [], fc)
  | r :: rest =>
      let '(st1, out1, fc1) := eval_fused_chain st r fc in
      let '(st2, outs, fc2) := run_fused_chain st1 rest fc1 in
      (st2, out1 :: outs, fc2)
  end.

(**md Multi-step trace evaluation of `eval_unfused_chain` over an input list. *)
Fixpoint run_unfused_chain {St A B : Type}
    (st : St) (inputs : list (comb_reading A)) (uc : unfused_chain St A B)
    : St * list (comb_reading B) * unfused_chain St A B :=
  match inputs with
  | [] => (st, [], uc)
  | r :: rest =>
      let '(st1, out1, uc1) := eval_unfused_chain st r uc in
      let '(st2, outs, uc2) := run_unfused_chain st1 rest uc1 in
      (st2, out1 :: outs, uc2)
  end.

(**md Type-erased unready tag for synchronizing unready states across heterogeneous stage types. *)
Inductive unready_tag : Type :=
  | UTPending (src : nat)
  | UTFailed (ex : nat) (orig : nat).

Global Instance unready_tag_eq_dec : EqDecision unready_tag.
Proof. solve_decision. Defined.

Definition tag_to_reading {V : Type} (t : unready_tag) : comb_reading V :=
  match t with
  | UTPending src => CPending src
  | UTFailed ex orig => CFailed ex orig
  end.

Definition opt_ready {V : Type} (r : option (comb_reading V)) : option V :=
  match r with
  | Some (CReady v) => Some v
  | _ => None
  end.

Fixpoint set_unfused_unready {St A B : Type} (t : unready_tag) (uc : unfused_chain St A B)
    : unfused_chain St A B :=
  match uc with
  | UC_Single id eq_b _ step => UC_Single id eq_b (Some (tag_to_reading t)) step
  | UC_Cons id eq_m _ step rest => UC_Cons id eq_m (Some (tag_to_reading t)) step (set_unfused_unready t rest)
  end.

(**md Simulation relation linking `unfused_chain` and `(fused_stages, tail_out)` across all runs. *)
Inductive stages_sync {St : Type} :
    forall {A B : Type}, unfused_chain St A B -> fused_stages St A B -> option (comb_reading B) -> Prop :=
  | Sync_Single :
      forall {A B : Type} (id : nat) (eq_b : B -> B -> bool)
        (prev : option (comb_reading B)) (step : St -> A -> St * result B),
        stages_sync (UC_Single id eq_b prev step) (FS_Single id eq_b (opt_ready prev) step) prev
  | Sync_Cons_None :
      forall {A M B : Type} (id : nat) (eq_m : M -> M -> bool)
        (step : St -> A -> St * result M)
        (uc_rest : unfused_chain St M B) (fs_rest : fused_stages St M B),
        stages_sync uc_rest fs_rest None ->
        stages_sync (UC_Cons id eq_m None step uc_rest) (FS_Cons id eq_m None step fs_rest) None
  | Sync_Cons_Unready :
      forall {A M B : Type} (id : nat) (eq_m : M -> M -> bool)
        (step : St -> A -> St * result M) (t : unready_tag)
        (uc_rest : unfused_chain St M B) (fs_rest : fused_stages St M B),
        uc_rest = set_unfused_unready t uc_rest ->
        fs_rest = clear_fused_ready fs_rest ->
        stages_sync uc_rest fs_rest (Some (tag_to_reading t)) ->
        stages_sync
          (UC_Cons id eq_m (Some (tag_to_reading t)) step uc_rest)
          (FS_Cons id eq_m None step fs_rest)
          (Some (tag_to_reading t))
  | Sync_Cons_Ready :
      forall {A M B : Type} (id : nat) (eq_m : M -> M -> bool) (v_m : M) (r_tail : comb_reading B)
        (step : St -> A -> St * result M)
        (uc_rest : unfused_chain St M B) (fs_rest : fused_stages St M B),
        stages_sync uc_rest fs_rest (Some r_tail) ->
        stages_sync
          (UC_Cons id eq_m (Some (CReady v_m)) step uc_rest)
          (FS_Cons id eq_m (Some v_m) step fs_rest)
          (Some r_tail).

(**md Chain-level bisimulation invariant between `unfused_chain` and `fused_chain`. *)
Definition chain_sync {St A B : Type} (uc : unfused_chain St A B) (fc : fused_chain St A B) : Prop :=
  stages_sync uc (fc_stages fc) (fc_tail_out fc).

Lemma init_chain_sync {St A B : Type} (spec : chain_spec St A B) :
  chain_sync (init_unfused_chain spec) (init_fused_chain spec).
Proof.
  unfold chain_sync, init_fused_chain; simpl.
  induction spec as [A B id eq_b step | A M B id eq_m step rest IH]; simpl.
  - exact (Sync_Single id eq_b None step).
  - exact (Sync_Cons_None id eq_m step _ _ IH).
Qed.

Lemma clear_fused_ready_idemp {St A B : Type} (fs : fused_stages St A B) :
  clear_fused_ready (clear_fused_ready fs) = clear_fused_ready fs.
Proof.
  induction fs as [A B id eq_b rdy step | A M B id eq_m rdy step rest IH]; simpl;
    [reflexivity | rewrite IH; reflexivity].
Qed.

Lemma set_unfused_unready_idemp {St A B : Type} (t : unready_tag) (uc : unfused_chain St A B) :
  set_unfused_unready t (set_unfused_unready t uc) = set_unfused_unready t uc.
Proof.
  induction uc as [A B id eq_b prev step | A M B id eq_m prev step rest IH]; simpl;
    [reflexivity | rewrite IH; reflexivity].
Qed.

Lemma reading_moved_tag_refl {V : Type} (eq_V : V -> V -> bool) (t : unready_tag) :
  reading_moved eq_V (Some (tag_to_reading t)) (tag_to_reading t) = false.
Proof.
  destruct t as [src | ex orig]; simpl.
  - rewrite Nat.eqb_refl. reflexivity.
  - rewrite !Nat.eqb_refl. reflexivity.
Qed.

Lemma unfused_false_noop {St A B : Type}
    (uc : unfused_chain St A B) (fs : fused_stages St A B) (tail_opt : option (comb_reading B)) :
  stages_sync uc fs tail_opt ->
  forall (r_tail : comb_reading B) (st : St) (r_in : comb_reading A),
    tail_opt = Some r_tail ->
    eval_unfused_chain_go st false r_in uc = (st, r_tail, uc).
Proof.
  intros Hsync.
  induction Hsync as
    [ A B id eq_b prev step
    | A M B id eq_m step uc_rest fs_rest Hrest IH
    | A M B id eq_m step t uc_rest fs_rest Huc Hfs Hrest IH
    | A M B id eq_m v_m r_b step uc_rest fs_rest Hrest IH ];
    intros r_tail st r_in Htail.
  - subst prev. reflexivity.
  - discriminate Htail.
  - injection Htail as <-. simpl.
    rewrite (IH (tag_to_reading t) st (tag_to_reading t) eq_refl). reflexivity.
  - injection Htail as <-. simpl.
    rewrite (IH r_b st (CReady v_m) eq_refl). reflexivity.
Qed.

Lemma unfused_propagate_unready {St A B : Type}
    (uc : unfused_chain St A B) (fs : fused_stages St A B) (tail_opt : option (comb_reading B)) :
  stages_sync uc fs tail_opt ->
  forall (t : unready_tag) (st : St),
    eval_unfused_chain_go st true (tag_to_reading t) uc =
      (st, tag_to_reading t, set_unfused_unready t uc) /\
    stages_sync (set_unfused_unready t uc) (clear_fused_ready fs) (Some (tag_to_reading t)).
Proof.
  intros Hsync.
  induction Hsync as
    [ A B id eq_b prev step
    | A M B id eq_m step uc_rest fs_rest Hrest IH
    | A M B id eq_m step t0 uc_rest fs_rest Huc Hfs Hrest IH
    | A M B id eq_m v_m r_b step uc_rest fs_rest Hrest IH ];
    intros t st.
  - simpl. split; [destruct t; reflexivity|].
    pose proof (Sync_Single id eq_b (Some (tag_to_reading t)) step) as Hs.
    destruct t; simpl in Hs |- *; exact Hs.
  - destruct (IH t st) as [Heval Hsync'].
    simpl. destruct t as [s1 | e1 o1]; simpl in Heval |- *; rewrite Heval;
      [ (split; [reflexivity|]);
        apply (Sync_Cons_Unready id eq_m step (UTPending s1) _ _);
          [ symmetry; apply set_unfused_unready_idemp
          | symmetry; apply clear_fused_ready_idemp
          | exact Hsync' ]
      | (split; [reflexivity|]);
        apply (Sync_Cons_Unready id eq_m step (UTFailed e1 o1) _ _);
          [ symmetry; apply set_unfused_unready_idemp
          | symmetry; apply clear_fused_ready_idemp
          | exact Hsync' ] ].
  - destruct (decide (t0 = t)) as [<- | Hne].
    + simpl.
      assert (Hmv : reading_moved eq_m (Some (tag_to_reading t0)) (tag_to_reading t0) = false)
        by apply reading_moved_tag_refl.
      destruct t0 as [s0 | e0 o0]; simpl in Hmv |- *; rewrite Hmv;
        [ rewrite (unfused_false_noop uc_rest fs_rest _ Hrest (CPending s0) st (CPending s0) eq_refl);
          rewrite <- Huc, <- Hfs;
          (split; [reflexivity|]);
          exact (Sync_Cons_Unready id eq_m step (UTPending s0) uc_rest fs_rest Huc Hfs Hrest)
        | rewrite (unfused_false_noop uc_rest fs_rest _ Hrest (CFailed e0 o0) st (CFailed e0 o0) eq_refl);
          rewrite <- Huc, <- Hfs;
          (split; [reflexivity|]);
          exact (Sync_Cons_Unready id eq_m step (UTFailed e0 o0) uc_rest fs_rest Huc Hfs Hrest) ].
    + assert (Hmv : reading_moved eq_m (Some (tag_to_reading t0)) (tag_to_reading t) = true).
      { destruct t0 as [s0 | e0 o0], t as [s1 | e1 o1]; simpl; try reflexivity.
        - destruct (Nat.eqb s0 s1) eqn:Es; [|reflexivity].
          apply Nat.eqb_eq in Es. subst. contradiction.
        - destruct (Nat.eqb e0 e1) eqn:Ee; simpl; [|reflexivity].
          destruct (Nat.eqb o0 o1) eqn:Eo; simpl; [|reflexivity].
          apply Nat.eqb_eq in Ee; apply Nat.eqb_eq in Eo; subst; contradiction. }
      destruct (IH t st) as [Heval Hsync'].
      destruct t as [s1 | e1 o1]; simpl in Hmv, Heval |- *; rewrite Hmv, Heval;
        [ (split; [reflexivity|]);
          apply (Sync_Cons_Unready id eq_m step (UTPending s1) _ _);
            [ symmetry; apply set_unfused_unready_idemp
            | symmetry; apply clear_fused_ready_idemp
            | exact Hsync' ]
        | (split; [reflexivity|]);
          apply (Sync_Cons_Unready id eq_m step (UTFailed e1 o1) _ _);
            [ symmetry; apply set_unfused_unready_idemp
            | symmetry; apply clear_fused_ready_idemp
            | exact Hsync' ] ].
  - destruct (IH t st) as [Heval Hsync'].
    destruct t as [s1 | e1 o1]; simpl in Heval |- *; rewrite Heval;
      [ (split; [reflexivity|]);
        apply (Sync_Cons_Unready id eq_m step (UTPending s1) _ _);
          [ symmetry; apply set_unfused_unready_idemp
          | symmetry; apply clear_fused_ready_idemp
          | exact Hsync' ]
      | (split; [reflexivity|]);
        apply (Sync_Cons_Unready id eq_m step (UTFailed e1 o1) _ _);
          [ symmetry; apply set_unfused_unready_idemp
          | symmetry; apply clear_fused_ready_idemp
          | exact Hsync' ] ].
Qed.

Lemma eval_fused_stages_eq_unfused {St A B : Type}
    (uc : unfused_chain St A B) (fs : fused_stages St A B) (tail_opt : option (comb_reading B)) :
  stages_sync uc fs tail_opt ->
  forall (st : St) (v_in : A),
    let '(st_f, out_f, fs') := eval_fused_stages st v_in tail_opt fs in
    let '(st_u, out_u, uc') := eval_unfused_chain_go st true (CReady v_in) uc in
    st_f = st_u /\ out_f = out_u /\ stages_sync uc' fs' (Some out_f).
Proof.
  intros Hsync.
  induction Hsync as
    [ A B id eq_b prev step
    | A M B id eq_m step uc_rest fs_rest Hrest IH
    | A M B id eq_m step t0 uc_rest fs_rest Huc Hfs Hrest IH
    | A M B id eq_m v_m r_b step uc_rest fs_rest Hrest IH ];
    intros st v_in; simpl.
  - destruct (step st v_in) as [st' [v_out | ex]]; simpl.
    + repeat split; [exact (Sync_Single id eq_b (Some (CReady v_out)) step)].
    + repeat split; [exact (Sync_Single id eq_b (Some (CFailed ex id)) step)].
  - destruct (step st v_in) as [st' [v_mid | ex]]; simpl.
    + specialize (IH st' v_mid).
      destruct (eval_fused_stages st' v_mid None fs_rest) as [[st_f out_f] fs_rest'].
      destruct (eval_unfused_chain_go st' true (CReady v_mid) uc_rest) as [[st_u out_u] uc_rest'].
      destruct IH as (-> & -> & Hsync').
      repeat split. exact (Sync_Cons_Ready id eq_m v_mid out_u step uc_rest' fs_rest' Hsync').
    + destruct (unfused_propagate_unready uc_rest fs_rest None Hrest (UTFailed ex id) st')
        as [Heval Hsync'].
      simpl in Heval. rewrite Heval.
      repeat split.
      apply (Sync_Cons_Unready id eq_m step (UTFailed ex id) _ _);
        [ symmetry; apply set_unfused_unready_idemp
        | symmetry; apply clear_fused_ready_idemp
        | exact Hsync' ].
  - destruct (step st v_in) as [st' [v_mid | ex]]; simpl.
    + destruct t0 as [s0 | e0 o0]; simpl;
        specialize (IH st' v_mid); simpl in IH;
        [ destruct (eval_fused_stages st' v_mid (Some (CPending s0)) fs_rest) as [[st_f out_f] fs_rest']
        | destruct (eval_fused_stages st' v_mid (Some (CFailed e0 o0)) fs_rest) as [[st_f out_f] fs_rest'] ];
        destruct (eval_unfused_chain_go st' true (CReady v_mid) uc_rest) as [[st_u out_u] uc_rest'];
        destruct IH as (-> & -> & Hsync');
        repeat split; exact (Sync_Cons_Ready id eq_m v_mid out_u step uc_rest' fs_rest' Hsync').
    + destruct (decide (t0 = UTFailed ex id)) as [-> | Hne].
      * simpl. rewrite !Nat.eqb_refl. simpl.
        rewrite (unfused_false_noop uc_rest fs_rest _ Hrest (CFailed ex id) st' (CFailed ex id) eq_refl).
        rewrite <- Hfs.
        repeat split.
        exact (Sync_Cons_Unready id eq_m step (UTFailed ex id) uc_rest fs_rest Huc Hfs Hrest).
      * destruct (unfused_propagate_unready uc_rest fs_rest _ Hrest (UTFailed ex id) st')
          as [Heval Hsync'].
        simpl in Heval.
        destruct t0 as [s0 | e0 o0]; simpl.
        -- rewrite Heval. repeat split.
           apply (Sync_Cons_Unready id eq_m step (UTFailed ex id) _ _);
             [ symmetry; apply set_unfused_unready_idemp
             | symmetry; apply clear_fused_ready_idemp
             | exact Hsync' ].
        -- assert (Hneq_b : negb (Nat.eqb e0 ex && Nat.eqb o0 id) = true).
           { destruct (Nat.eqb e0 ex) eqn:Ee; simpl; [|reflexivity].
             destruct (Nat.eqb o0 id) eqn:Eo; simpl; [|reflexivity].
             apply Nat.eqb_eq in Ee; apply Nat.eqb_eq in Eo; subst; contradiction. }
           rewrite Hneq_b, Heval. repeat split.
           apply (Sync_Cons_Unready id eq_m step (UTFailed ex id) _ _);
             [ symmetry; apply set_unfused_unready_idemp
             | symmetry; apply clear_fused_ready_idemp
             | exact Hsync' ].
  - destruct (step st v_in) as [st' [v_mid | ex]]; simpl.
    + destruct (eq_m v_m v_mid) eqn:Eeq; simpl.
      * rewrite (unfused_false_noop uc_rest fs_rest _ Hrest r_b st' (CReady v_mid) eq_refl).
        repeat split. exact (Sync_Cons_Ready id eq_m v_mid r_b step uc_rest fs_rest Hrest).
      * specialize (IH st' v_mid).
        destruct (eval_fused_stages st' v_mid (Some r_b) fs_rest) as [[st_f out_f] fs_rest'].
        destruct (eval_unfused_chain_go st' true (CReady v_mid) uc_rest) as [[st_u out_u] uc_rest'].
        destruct IH as (-> & -> & Hsync').
        repeat split. exact (Sync_Cons_Ready id eq_m v_mid out_u step uc_rest' fs_rest' Hsync').
    + destruct (unfused_propagate_unready uc_rest fs_rest _ Hrest (UTFailed ex id) st')
        as [Heval Hsync'].
      simpl in Heval. rewrite Heval.
      repeat split.
      apply (Sync_Cons_Unready id eq_m step (UTFailed ex id) _ _);
        [ symmetry; apply set_unfused_unready_idemp
        | symmetry; apply clear_fused_ready_idemp
        | exact Hsync' ].
Qed.

Lemma eval_fused_chain_step_sync {St A B : Type}
    (uc : unfused_chain St A B) (fc : fused_chain St A B)
    (st : St) (r_in : comb_reading A) :
  chain_sync uc fc ->
  let '(st_f, out_f, fc') := eval_fused_chain st r_in fc in
  let '(st_u, out_u, uc') := eval_unfused_chain st r_in uc in
  st_f = st_u /\ out_f = out_u /\ chain_sync uc' fc'.
Proof.
  intros Hsync. unfold chain_sync in *.
  destruct r_in as [v_in | src | ex orig].
  - unfold eval_fused_chain, eval_unfused_chain.
    pose proof (eval_fused_stages_eq_unfused uc (fc_stages fc) (fc_tail_out fc) Hsync st v_in) as Hstep.
    destruct (eval_fused_stages st v_in (fc_tail_out fc) (fc_stages fc)) as [[st_f out_f] fs'].
    destruct (eval_unfused_chain_go st true (CReady v_in) uc) as [[st_u out_u] uc'].
    exact Hstep.
  - unfold eval_fused_chain, eval_unfused_chain.
    destruct (unfused_propagate_unready uc (fc_stages fc) (fc_tail_out fc) Hsync (UTPending src) st)
      as [Heval Hsync'].
    simpl in Heval. rewrite Heval.
    repeat split; exact Hsync'.
  - unfold eval_fused_chain, eval_unfused_chain.
    destruct (unfused_propagate_unready uc (fc_stages fc) (fc_tail_out fc) Hsync (UTFailed ex orig) st)
      as [Heval Hsync'].
    simpl in Heval. rewrite Heval.
    repeat split; exact Hsync'.
Qed.

Lemma run_fused_chain_sync {St A B : Type}
    (inputs : list (comb_reading A))
    (uc : unfused_chain St A B) (fc : fused_chain St A B) (st : St) :
  chain_sync uc fc ->
  let '(st_f, outs_f, fc') := run_fused_chain st inputs fc in
  let '(st_u, outs_u, uc') := run_unfused_chain st inputs uc in
  st_f = st_u /\ outs_f = outs_u /\ chain_sync uc' fc'.
Proof.
  revert uc fc st.
  induction inputs as [|r rest IH]; intros uc fc st Hsync; simpl.
  - repeat split; exact Hsync.
  - pose proof (eval_fused_chain_step_sync uc fc st r Hsync) as H1.
    destruct (eval_fused_chain st r fc) as [[st_f1 out_f1] fc1].
    destruct (eval_unfused_chain st r uc) as [[st_u1 out_u1] uc1].
    destruct H1 as (<- & <- & Hsync1).
    specialize (IH uc1 fc1 st_f1 Hsync1).
    destruct (run_fused_chain st_f1 rest fc1) as [[st_f2 outs_f] fc2].
    destruct (run_unfused_chain st_f1 rest uc1) as [[st_u2 outs_u] uc2].
    destruct IH as (<- & <- & Hsync2).
    repeat split; exact Hsync2.
Qed.

(**md
**Fused `eval_fused_chain` produces the exact same `(St * comb_reading B)`, `origin`, and multi-step trace as sequential unfused `eval_unfused_chain` over arbitrary boolean stage cutoffs `eq_i` without Leibniz equality, early-exiting on intermediate stage cutoffs and attributing stage throws to the throwing stage's `node_id`.**

Certifies `MapFusion` across `Signal.map`, `Memo.map`, `AsyncMemo.map`, `AsyncSource.map`, and `StreamMemo.map` (`Core.fs`, `Combinators.fs`):
1. From any synchronized state `chain_sync uc fc`, single-step `eval_fused_chain` and `eval_unfused_chain` return identical `(St, comb_reading B)` and preserve `chain_sync` across `CPending`, upstream `CFailed`, stage throws, intermediate cutoffs, and full `CReady` propagation.
2. Over any input trace `inputs` starting from `init_fused_chain spec` and `init_unfused_chain spec`, `run_fused_chain` and `run_unfused_chain` yield identical final `St` (allocation/identity state) and output reading sequences.
3. On an intermediate cutoff (`eq_m old_m v_mid = true`) or stage throw (`RThrow ex`), `eval_fused_chain` stops at that stage without invoking downstream `rest` steps, preserving `cached_tail` or attributing `CFailed ex id` to stage `id`.

- [St:] explicit heap/allocation state threaded through stage callbacks.
- [A:] root input value type.
- [M:] intermediate stage output type for direct cutoff/throw inspection.
- [B:] tail output value type.
- [spec:] declarative heterogeneous chain of `(node_id, eq_i, step_i)` stages.
- [uc:] unfused chain state synchronized with `fc`.
- [fc:] fused chain state synchronized with `uc`.
- [st:] starting `St` state.
- [r_in:] single-step root reading.
- [inputs:] multi-step sequence of root readings.
- [id:] `node_id` of an inspected head stage.
- [eq_m:] boolean cutoff predicate of the head stage.
- [old_m:] cached ready value of the head stage.
- [cached_tail:] cached tail reading returned on head-stage cutoff.
- [step_m:] step callback of the head stage.
- [fs_rest:] downstream fused stages skipped on cutoff or throw.
- [v_in:] ready root input value for head-stage cutoff/throw inspection.
*)
Theorem fused_map_chain_eq_unfused (St A M B : Type)
    (spec : chain_spec St A B)
    (uc : unfused_chain St A B) (fc : fused_chain St A B)
    (st : St) (r_in : comb_reading A) (inputs : list (comb_reading A))
    (id : nat) (eq_m : M -> M -> bool) (old_m : M) (cached_tail : comb_reading B)
    (step_m : St -> A -> St * result M) (fs_rest : fused_stages St M B) (v_in : A) :
  (chain_sync uc fc ->
     let '(st_f, out_f, fc') := eval_fused_chain st r_in fc in
     let '(st_u, out_u, uc') := eval_unfused_chain st r_in uc in
     st_f = st_u /\ out_f = out_u /\ chain_sync uc' fc') /\
  (let '(st_f, outs_f, _) := run_fused_chain st inputs (init_fused_chain spec) in
   let '(st_u, outs_u, _) := run_unfused_chain st inputs (init_unfused_chain spec) in
   st_f = st_u /\ outs_f = outs_u) /\
  (forall st1 v_mid,
     step_m st v_in = (st1, RVal v_mid) ->
     eq_m old_m v_mid = true ->
     eval_fused_chain st (CReady v_in)
       (Build_fused_chain (FS_Cons id eq_m (Some old_m) step_m fs_rest) (Some cached_tail)) =
     (st1, cached_tail,
      Build_fused_chain (FS_Cons id eq_m (Some v_mid) step_m fs_rest) (Some cached_tail))) /\
  (forall st1 ex,
     step_m st v_in = (st1, RThrow ex) ->
     eval_fused_chain st (CReady v_in)
       (Build_fused_chain (FS_Cons id eq_m (Some old_m) step_m fs_rest) (Some cached_tail)) =
     (st1, CFailed ex id,
      Build_fused_chain (FS_Cons id eq_m None step_m (clear_fused_ready fs_rest))
        (Some (CFailed ex id)))).
Proof.
  split_and!.
  - exact (eval_fused_chain_step_sync uc fc st r_in).
  - pose proof (run_fused_chain_sync inputs (init_unfused_chain spec) (init_fused_chain spec) st
                  (init_chain_sync spec)) as Hrun.
    destruct (run_fused_chain st inputs (init_fused_chain spec)) as [[st_f outs_f] fc'].
    destruct (run_unfused_chain st inputs (init_unfused_chain spec)) as [[st_u outs_u] uc'].
    destruct Hrun as (-> & -> & _). split; reflexivity.
  - intros st1 v_mid Hstep Heq.
    unfold eval_fused_chain; simpl. rewrite Hstep, Heq. reflexivity.
  - intros st1 ex Hstep.
    unfold eval_fused_chain; simpl. rewrite Hstep. reflexivity.
Qed.

(** ** B19. Static map2 and zip left-to-right status precedence *)

(**md Monadic `Value` bind over `comb_reading`: short-circuits on `CPending` or `CFailed` preserving `origin`. *)
Definition comb_bind {St A B : Type}
    (r : comb_reading A) (st : St) (k : St -> A -> St * comb_reading B)
    : St * comb_reading B :=
  match r with
  | CReady x => k st x
  | CPending src => (st, CPending src)
  | CFailed ex orig => (st, CFailed ex orig)
  end.

(**md Reference sequential dynamic body `let! x = r1 in let! y = r2 in f x y` on node `self_id`. *)
Definition eval_seq_let2 {St A B C : Type}
    (self_id : nat) (f : St -> A -> B -> St * result C)
    (st : St) (r1 : comb_reading A) (r2 : comb_reading B)
    : St * comb_reading C :=
  comb_bind r1 st (fun st1 x =>
    comb_bind r2 st1 (fun st2 y =>
      let '(st3, res) := f st2 x y in
      (st3, result_to_reading self_id res))).

(**md Static two-source `eval_map2` matching `Core.fs`: fast both-ready path plus slow left-to-right unready precedence, keeping static edges `[id1; id2]`. *)
Definition eval_map2 {St A B C : Type}
    (self_id id1 id2 : nat) (f : St -> A -> B -> St * result C)
    (st : St) (r1 : comb_reading A) (r2 : comb_reading B)
    : St * comb_reading C * list nat :=
  let edges := [id1; id2] in
  match negb (is_unready_reading r1) && negb (is_unready_reading r2) with
  | true =>
      match r1, r2 with
      | CReady x, CReady y =>
          let '(st', res) := f st x y in
          (st', result_to_reading self_id res, edges)
      | _, _ => (st, CPending self_id, edges)
      end
  | false =>
      match r1 with
      | CPending src1 => (st, CPending src1, edges)
      | CFailed ex1 orig1 => (st, CFailed ex1 orig1, edges)
      | CReady _ =>
          match r2 with
          | CPending src2 => (st, CPending src2, edges)
          | CFailed ex2 orig2 => (st, CFailed ex2 orig2, edges)
          | CReady _ => (st, CPending self_id, edges)
          end
      end
  end.

(**md Static `eval_zip` pairing two `comb_reading` inputs via `eval_map2`. *)
Definition eval_zip {A B : Type}
    (self_id id1 id2 : nat) (r1 : comb_reading A) (r2 : comb_reading B)
    : comb_reading (A * B) * list nat :=
  let '(_, out, edges) :=
    eval_map2 self_id id1 id2 (fun (_ : unit) x y => (tt, RVal (x, y))) tt r1 r2 in
  (out, edges).

(**md
**Static `eval_map2` and `eval_zip` match sequential `let! x = r1 in let! y = r2 in f x y` across all `3 * 3` `comb_reading` pairs, preserving `St`, failure `origin`, and static `[id1; id2]` upstream edges.**

Certifies `map2` and `zip` across `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`, and `RanvierExtensions.Zip`:
the fast both-ready check combined with slow left-to-right unready resolution yields the exact same `(St * comb_reading C)` and `origin` as monadic `comb_bind` evaluation across all nine constructor combinations, skips `f` (`St` unchanged) whenever either input is unready, and retains both static upstream edges `[id1; id2]` without dynamic re-tracking churn.

- [St:] explicit state threaded through `f`.
- [A:] first source value type.
- [B:] second source value type.
- [C:] combined output value type.
- [self_id:] node id of the `map2` / `zip` `Memo`.
- [id1:] upstream node id of `source1`.
- [id2:] upstream node id of `source2`.
- [f:] closed-window binary mapping function.
- [st:] initial `St` state before evaluation.
- [r1:] reading of `source1`.
- [r2:] reading of `source2`.
*)
Theorem map2_zip_status_precedence_eq (St A B C : Type)
    (self_id id1 id2 : nat) (f : St -> A -> B -> St * result C)
    (st : St) (r1 : comb_reading A) (r2 : comb_reading B) :
  let '(st_m, out_m, edges_m) := eval_map2 self_id id1 id2 f st r1 r2 in
  let '(st_s, out_s) := eval_seq_let2 self_id f st r1 r2 in
  st_m = st_s /\
  out_m = out_s /\
  edges_m = [id1; id2] /\
  (is_unready_reading r1 = true \/ is_unready_reading r2 = true -> st_m = st) /\
  eval_zip self_id id1 id2 r1 r2 =
    (snd (eval_seq_let2 self_id (fun (_ : unit) x y => (tt, RVal (x, y))) tt r1 r2), [id1; id2]).
Proof.
  unfold eval_map2, eval_seq_let2, comb_bind, eval_zip.
  destruct r1 as [x | s1 | e1 o1], r2 as [y | s2 | e2 o2]; simpl.
  - destruct (f st x y) as [st' [v | ex]]; simpl;
      (split_and!; [reflexivity | reflexivity | reflexivity | intros [H1 | H2]; discriminate | reflexivity]).
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
  - split_and!; try reflexivity; intros _; reflexivity.
Qed.

(** ** B20. Split bind inner-tick and transient-unready preservation *)

(**md Pure state of a split `Signal.bind` / `Memo.bind` node tracking outer key, inner link, binder runs, and outer scope discharges. *)
Record split_bind_state (K InnerId U : Type) := {
  sb_self_id : nat;
  sb_has_key : bool;
  sb_last_key : K;
  sb_bound : option (nat * result InnerId);
  sb_scope_gen : nat;
  sb_discharges : nat;
  sb_binder_runs : nat;
  sb_visible : comb_reading U;
}.

Arguments Build_split_bind_state {K InnerId U} _ _ _ _ _ _ _ _.
Arguments sb_self_id {K InnerId U} _.
Arguments sb_has_key {K InnerId U} _.
Arguments sb_last_key {K InnerId U} _.
Arguments sb_bound {K InnerId U} _.
Arguments sb_scope_gen {K InnerId U} _.
Arguments sb_discharges {K InnerId U} _.
Arguments sb_binder_runs {K InnerId U} _.
Arguments sb_visible {K InnerId U} _.

(**md Resolves the current inner reading from a bound `(alloc_tok, result InnerId)` outcome. *)
Definition resolve_bound_inner {InnerId U : Type}
    (self_id : nat) (inner : InnerId -> comb_reading U)
    (b : option (nat * result InnerId)) (fallback : comb_reading U) : comb_reading U :=
  match b with
  | None => fallback
  | Some (_, RVal id) => inner id
  | Some (_, RThrow ex) => CFailed ex self_id
  end.

(**md Single-step evaluation of `Signal.bind` / `Memo.bind` over outer `r_outer`, `binder : K -> nat * result InnerId`, and `inner : InnerId -> comb_reading U`. *)
Definition split_bind_step {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (inner : InnerId -> comb_reading U)
    (r_outer : comb_reading K)
    (st : split_bind_state K InnerId U)
    : split_bind_state K InnerId U :=
  match r_outer with
  | CPending src =>
      {| sb_self_id := sb_self_id st;
         sb_has_key := sb_has_key st;
         sb_last_key := sb_last_key st;
         sb_bound := sb_bound st;
         sb_scope_gen := sb_scope_gen st;
         sb_discharges := sb_discharges st;
         sb_binder_runs := sb_binder_runs st;
         sb_visible := CPending src |}
  | CFailed ex orig =>
      {| sb_self_id := sb_self_id st;
         sb_has_key := sb_has_key st;
         sb_last_key := sb_last_key st;
         sb_bound := sb_bound st;
         sb_scope_gen := sb_scope_gen st;
         sb_discharges := sb_discharges st;
         sb_binder_runs := sb_binder_runs st;
         sb_visible := CFailed ex orig |}
  | CReady k =>
      if sb_has_key st && eq_K (sb_last_key st) k then
        {| sb_self_id := sb_self_id st;
           sb_has_key := true;
           sb_last_key := sb_last_key st;
           sb_bound := sb_bound st;
           sb_scope_gen := sb_scope_gen st;
           sb_discharges := sb_discharges st;
           sb_binder_runs := sb_binder_runs st;
           sb_visible := resolve_bound_inner (sb_self_id st) inner (sb_bound st) (sb_visible st) |}
      else
        let b_out := binder k in
        let next_discharges :=
          if sb_has_key st then S (sb_discharges st) else sb_discharges st in
        {| sb_self_id := sb_self_id st;
           sb_has_key := true;
           sb_last_key := k;
           sb_bound := Some b_out;
           sb_scope_gen := S (sb_scope_gen st);
           sb_discharges := next_discharges;
           sb_binder_runs := S (sb_binder_runs st);
           sb_visible :=
             match snd b_out with
             | RVal id => inner id
             | RThrow ex => CFailed ex (sb_self_id st)
             end |}
  end.

(**md Evaluates a sequence of inner `U` environment ticks while outer settles to `CReady k`. *)
Fixpoint split_bind_inner_ticks {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (k : K)
    (inners : list (InnerId -> comb_reading U))
    (st : split_bind_state K InnerId U)
    : split_bind_state K InnerId U :=
  match inners with
  | [] => st
  | inner_i :: rest =>
      split_bind_inner_ticks eq_K binder k rest
        (split_bind_step eq_K binder inner_i (CReady k) st)
  end.

(**md Evaluates a sequence of outer `comb_reading K` transitions against a fixed inner environment `inner`. *)
Fixpoint split_bind_outer_steps {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (inner : InnerId -> comb_reading U)
    (outers : list (comb_reading K))
    (st : split_bind_state K InnerId U)
    : split_bind_state K InnerId U :=
  match outers with
  | [] => st
  | r :: rest =>
      split_bind_outer_steps eq_K binder inner rest
        (split_bind_step eq_K binder inner r st)
  end.

Lemma split_bind_inner_ticks_preserves {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (k : K) (tok : nat) (inner_id : InnerId)
    (inners : list (InnerId -> comb_reading U))
    (inner_last : InnerId -> comb_reading U)
    (st : split_bind_state K InnerId U) :
  sb_has_key st = true ->
  eq_K (sb_last_key st) k = true ->
  sb_bound st = Some (tok, RVal inner_id) ->
  let st' := split_bind_inner_ticks eq_K binder k (inners ++ [inner_last]) st in
  sb_binder_runs st' = sb_binder_runs st /\
  sb_scope_gen st' = sb_scope_gen st /\
  sb_discharges st' = sb_discharges st /\
  sb_last_key st' = sb_last_key st /\
  sb_bound st' = Some (tok, RVal inner_id) /\
  sb_visible st' = inner_last inner_id.
Proof.
  revert st.
  induction inners as [|inner_i rest IH]; intros st Hhas Heq Hbnd;
    cbn [split_bind_inner_ticks app].
  - unfold split_bind_step. rewrite Hhas, Heq, Hbnd. simpl.
    repeat split; reflexivity.
  - assert (Hstep :
      split_bind_step eq_K binder inner_i (CReady k) st =
      {| sb_self_id := sb_self_id st;
         sb_has_key := true;
         sb_last_key := sb_last_key st;
         sb_bound := Some (tok, RVal inner_id);
         sb_scope_gen := sb_scope_gen st;
         sb_discharges := sb_discharges st;
         sb_binder_runs := sb_binder_runs st;
         sb_visible := inner_i inner_id |}).
    { unfold split_bind_step. rewrite Hhas, Heq, Hbnd. simpl. reflexivity. }
    cbv zeta. rewrite Hstep.
    exact (IH {| sb_self_id := sb_self_id st;
                 sb_has_key := true;
                 sb_last_key := sb_last_key st;
                 sb_bound := Some (tok, RVal inner_id);
                 sb_scope_gen := sb_scope_gen st;
                 sb_discharges := sb_discharges st;
                 sb_binder_runs := sb_binder_runs st;
                 sb_visible := inner_i inner_id |}
              eq_refl Heq eq_refl).
Qed.

Lemma split_bind_outer_unready_preserves {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (inner : InnerId -> comb_reading U)
    (us : list (comb_reading K))
    (st : split_bind_state K InnerId U) :
  Forall (fun r => is_unready_reading r = true) us ->
  let st' := split_bind_outer_steps eq_K binder inner us st in
  sb_self_id st' = sb_self_id st /\
  sb_has_key st' = sb_has_key st /\
  sb_last_key st' = sb_last_key st /\
  sb_bound st' = sb_bound st /\
  sb_scope_gen st' = sb_scope_gen st /\
  sb_discharges st' = sb_discharges st /\
  sb_binder_runs st' = sb_binder_runs st.
Proof.
  revert st. induction us as [|r rest IH]; intros st Hall; simpl.
  - repeat split; reflexivity.
  - inversion Hall as [|? ? Hr Hrest]; subst.
    destruct r as [v | src | ex orig]; [simpl in Hr; discriminate| |];
      exact (IH _ Hrest).
Qed.

Lemma split_bind_outer_steps_app {K InnerId U : Type}
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (inner : InnerId -> comb_reading U)
    (rs1 rs2 : list (comb_reading K))
    (st : split_bind_state K InnerId U) :
  split_bind_outer_steps eq_K binder inner (rs1 ++ rs2) st =
  split_bind_outer_steps eq_K binder inner rs2
    (split_bind_outer_steps eq_K binder inner rs1 st).
Proof.
  revert st. induction rs1 as [|r rest IH]; intros st; simpl; [reflexivity | apply IH].
Qed.

(**md
**Split `split_bind_step` invokes `binder` iff outer settles to an initial or `eq_K`-unequal key, discharges the prior outer scope iff `sb_has_key = true` and outer settles to an `eq_K`-unequal key, and performs zero `binder` calls or scope discharges across inner `U` transitions and transient outer `CPending`/`CFailed` excursions recovering to an `eq_K`-equal key.**

Certifies `Signal.bind`, `Memo.bind`, and `RanvierExtensions.SelectMany` (`Core.fs`, `Combinators.fs`, `Ranvier.CSharp/Extensions.fs`):
1. `sb_binder_runs` increments if and only if `r_outer = CReady k` with `sb_has_key = false` or `eq_K sb_last_key k = false`; otherwise `sb_binder_runs` is unchanged.
2. `sb_discharges` increments if and only if `r_outer = CReady k` with `sb_has_key = true` and `eq_K sb_last_key k = false`; otherwise `sb_discharges` is unchanged.
3. Across any sequence of inner `U` transitions `inners ++ [inner_last]` while outer settles to an `eq_K`-equal key `k`, `split_bind_inner_ticks` updates `sb_visible` to `inner_last inner_id` with zero `binder` runs and zero scope discharges.
4. Across any sequence `us` of transient outer `CPending`/`CFailed` readings followed by `CReady k` with `eq_K sb_last_key k = true`, `split_bind_outer_steps` preserves `sb_bound`, `sb_last_key`, `sb_binder_runs`, and `sb_discharges`, resuming `resolve_bound_inner` directly.

- [K:] outer reactive value type.
- [InnerId:] identifier type of inner `Memo<'U>` nodes returned by `binder`.
- [U:] inner reactive value type.
- [eq_K:] boolean cutoff predicate over settled outer `K` values.
- [binder:] closed-window outer binder returning an allocation token and `result InnerId`.
- [inner:] reading lookup for inner `InnerId` nodes.
- [inner_last:] final inner reading lookup after a sequence of inner `U` ticks.
- [inners:] intermediate inner `U` tick environments.
- [r_outer:] single-step outer reading.
- [k:] settled outer key matching `sb_last_key`.
- [tok:] scope/allocation token recorded when `inner_id` was bound.
- [inner_id:] currently linked inner node id.
- [b_out:] bound `(tok, result InnerId)` outcome preserved across outer unready excursions.
- [us:] transient outer `CPending`/`CFailed` excursion trace.
- [st:] `split_bind_state` before the step or trace.
*)
Theorem split_bind_inner_tick_eq (K InnerId U : Type)
    (eq_K : K -> K -> bool)
    (binder : K -> nat * result InnerId)
    (inner inner_last : InnerId -> comb_reading U)
    (inners : list (InnerId -> comb_reading U))
    (r_outer : comb_reading K) (k : K) (tok : nat) (inner_id : InnerId)
    (b_out : nat * result InnerId)
    (us : list (comb_reading K))
    (st : split_bind_state K InnerId U) :
  (let st' := split_bind_step eq_K binder inner r_outer st in
   (sb_binder_runs st' = S (sb_binder_runs st) <->
      exists k', r_outer = CReady k' /\ (sb_has_key st = false \/ eq_K (sb_last_key st) k' = false)) /\
   (sb_binder_runs st' = sb_binder_runs st \/ sb_binder_runs st' = S (sb_binder_runs st)) /\
   (sb_discharges st' = S (sb_discharges st) <->
      exists k', r_outer = CReady k' /\ sb_has_key st = true /\ eq_K (sb_last_key st) k' = false) /\
   (sb_discharges st' = sb_discharges st \/ sb_discharges st' = S (sb_discharges st))) /\
  (sb_has_key st = true -> eq_K (sb_last_key st) k = true ->
     sb_bound st = Some (tok, RVal inner_id) ->
     let st' := split_bind_inner_ticks eq_K binder k (inners ++ [inner_last]) st in
     sb_binder_runs st' = sb_binder_runs st /\
     sb_scope_gen st' = sb_scope_gen st /\
     sb_discharges st' = sb_discharges st /\
     sb_last_key st' = sb_last_key st /\
     sb_bound st' = Some (tok, RVal inner_id) /\
     sb_visible st' = inner_last inner_id) /\
  (sb_has_key st = true -> eq_K (sb_last_key st) k = true ->
     sb_bound st = Some b_out ->
     Forall (fun r => is_unready_reading r = true) us ->
     let st' := split_bind_outer_steps eq_K binder inner (us ++ [CReady k]) st in
     sb_binder_runs st' = sb_binder_runs st /\
     sb_scope_gen st' = sb_scope_gen st /\
     sb_discharges st' = sb_discharges st /\
     sb_last_key st' = sb_last_key st /\
     sb_bound st' = Some b_out /\
     sb_visible st' =
       match snd b_out with
       | RVal id => inner id
       | RThrow ex => CFailed ex (sb_self_id st)
       end).
Proof.
  split_and!.
  - cbv zeta. split_and!.
    + split.
      * destruct r_outer as [k' | src | ex orig]; simpl.
        -- destruct (sb_has_key st) eqn:Ehk; simpl.
           ++ destruct (eq_K (sb_last_key st) k') eqn:Eeq; simpl.
              ** intros Hbad; lia.
              ** intros _. exists k'. split; [reflexivity | right; exact Eeq].
           ++ intros _. exists k'. split; [reflexivity | left; reflexivity].
        -- intros Hbad; lia.
        -- intros Hbad; lia.
      * intros (k' & -> & Hnew). unfold split_bind_step.
        destruct Hnew as [Hno | Hneq];
          [rewrite Hno; reflexivity | rewrite Hneq, andb_false_r; reflexivity].
    + destruct r_outer as [k' | src | ex orig]; simpl;
        [destruct (sb_has_key st && eq_K (sb_last_key st) k'); simpl; [left | right]; reflexivity
        | left; reflexivity
        | left; reflexivity].
    + split.
      * destruct r_outer as [k' | src | ex orig]; simpl.
        -- destruct (sb_has_key st) eqn:Ehk; simpl.
           ++ destruct (eq_K (sb_last_key st) k') eqn:Eeq; simpl.
              ** intros Hbad; lia.
              ** intros _. exists k'. split_and!; [reflexivity | reflexivity | exact Eeq].
           ++ intros Hbad; lia.
        -- intros Hbad; lia.
        -- intros Hbad; lia.
      * intros (k' & -> & Hhk & Hneq). unfold split_bind_step.
        rewrite Hhk, Hneq. simpl. reflexivity.
    + destruct r_outer as [k' | src | ex orig]; simpl;
        [destruct (sb_has_key st) eqn:Ehk; simpl;
          [destruct (eq_K (sb_last_key st) k'); simpl; [left | right]; reflexivity
          | left; reflexivity]
        | left; reflexivity
        | left; reflexivity].
  - exact (split_bind_inner_ticks_preserves eq_K binder k tok inner_id inners inner_last st).
  - intros Hhas Heq Hb_out Hall.
    rewrite split_bind_outer_steps_app. simpl.
    pose proof (split_bind_outer_unready_preserves eq_K binder inner us st Hall)
      as (Hsid & Hhk & Hlk & Hbnd & Hsg & Hdis & Hbr).
    cbv zeta.
    set (st_u := split_bind_outer_steps eq_K binder inner us st) in *.
    unfold split_bind_step.
    rewrite Hhk, Hlk, Hhas, Heq. simpl.
    rewrite Hbr, Hsg, Hdis, Hbnd, Hsid, Hb_out.
    destruct b_out as [t [id | ex]]; simpl; repeat split; reflexivity.
Qed.
