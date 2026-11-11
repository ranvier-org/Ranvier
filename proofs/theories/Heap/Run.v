From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.
From iris.heap_lang Require Import proofmode notation.
From Ranvier.Heap Require Import ObserverSet SourceList Ghost Marking.

Open Scope nat_scope.

(** * Heap execution of expressions, commands, and node runs

Every non-recursive helper of [Pure/Engine.v] is paired with a HeapLang implementation and specification over
[is_graph γ g s], then sealed with [Global Opaque] so the 26 mutually recursive engine step proofs compose
directly through [wp_apply]. *)

(** ** Polymorphic value lists *)

Fixpoint val_list {A} (f : A -> val) (xs : list A) : val :=
  match xs with
  | [] => NONEV
  | x :: r => SOMEV (f x, val_list f r)
  end.

Lemma list_val_eq xs : list_val xs = val_list (fun n : nat => #(Z.of_nat n)) xs.
Proof. induction xs as [|x xs IH]; [done|]. simpl. by rewrite IH. Qed.

Lemma links_val_eq ls : links_val ls = val_list link_val ls.
Proof. induction ls as [|l ls IH]; [done|]. simpl. by rewrite IH. Qed.

Lemma cmds_val_eq bs : cmds_val bs = val_list compile_cmd bs.
Proof. induction bs as [|b bs IH]; [done|]. simpl. by rewrite IH. Qed.

Lemma envs_val_eq es : envs_val es = val_list env_val es.
Proof. induction es as [|e es IH]; [done|]. simpl. by rewrite IH. Qed.

Lemma zs_val_eq zs : zs_val zs = val_list (fun z : Z => #z) zs.
Proof. induction zs as [|z zs IH]; [done|]. simpl. by rewrite IH. Qed.

Lemma env_val_eq e : env_val e = val_list (fun p : var * Z => (#(Z.of_nat p.1), #p.2)%V) e.
Proof. induction e as [|[x z] e IH]; [done|]. simpl. by rewrite IH. Qed.

Definition cleanup_triple_val (c : cmd * env * nat) : val :=
  (compile_cmd c.1.1, (env_val c.1.2, #(Z.of_nat c.2)))%V.

Definition cleanups_val (cs : list (cmd * env * nat)) : val :=
  val_list cleanup_triple_val cs.

Definition vlist_snoc_code : val :=
  rec: "snoc" "l" "x" :=
    match: "l" with
      NONE => SOME ("x", NONE)
    | SOME "p" => SOME (Fst "p", "snoc" (Snd "p") "x")
    end.

Definition vlist_rev_loop_code : val :=
  rec: "loop" "l" "acc" :=
    match: "l" with
      NONE => "acc"
    | SOME "p" => "loop" (Snd "p") (SOME (Fst "p", "acc"))
    end.

Definition vlist_rev_code : val :=
  λ: "l", vlist_rev_loop_code "l" NONE.

Definition vlist_tail_code : val :=
  λ: "l",
    match: "l" with
      NONE => NONE
    | SOME "p" => Snd "p"
    end.

Definition vlist_mem_nat_code : val :=
  rec: "mem" "n" "l" :=
    match: "l" with
      NONE => #false
    | SOME "p" => if: Fst "p" = "n" then #true else "mem" "n" (Snd "p")
    end.

Definition zip_cleanups_code : val :=
  rec: "zip" "bs" "es" "os" :=
    match: "bs" with
      NONE => NONE
    | SOME "bp" =>
        match: "es" with
          NONE => NONE
        | SOME "ep" =>
            match: "os" with
              NONE => NONE
            | SOME "op" =>
                SOME ((Fst "bp", (Fst "ep", Fst "op")),
                      "zip" (Snd "bp") (Snd "ep") (Snd "op"))
            end
        end
    end.

Definition owner_cleanups_code : val :=
  λ: "x",
    let: "a" := Fst "x" in
    vlist_rev_code (zip_cleanups_code (Snd (Fst "a")) (Fst (Snd "a")) (Snd (Snd "a"))).

Definition links_without_code : val :=
  rec: "flt" "l" "ms" :=
    match: "ms" with
      NONE => NONE
    | SOME "p" =>
        let: "m" := Fst "p" in
        let: "r" := "flt" "l" (Snd "p") in
        if: (Fst (Fst (Fst "m")) = Fst (Fst (Fst "l"))) && (Snd "m" = Snd "l")
        then "r"
        else SOME ("m", "r")
    end.

Section pure_helpers.
Context `{!heapGS Σ}.

Lemma vlist_snoc_spec {A} (f : A -> val) (xs : list A) (x : A) :
  {{{ True }}}
    vlist_snoc_code (val_list f xs) (f x)
  {{{ RET val_list f (xs ++ [x]); True }}}.
Proof.
  induction xs as [|y xs IH]; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - wp_apply IH; [done|]. iIntros "_". wp_pures. by iApply "HΦ".
Qed.
Global Opaque vlist_snoc_code.

Lemma vlist_rev_loop_spec {A} (f : A -> val) (xs acc : list A) :
  {{{ True }}}
    vlist_rev_loop_code (val_list f xs) (val_list f acc)
  {{{ RET val_list f (rev_append xs acc); True }}}.
Proof.
  induction xs as [|x xs IH] in acc |- *; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - change (SOMEV (f x, val_list f acc)) with (val_list f (x :: acc)).
    by wp_apply (IH (x :: acc)); [done|].
Qed.

Lemma vlist_rev_spec {A} (f : A -> val) (xs : list A) :
  {{{ True }}}
    vlist_rev_code (val_list f xs)
  {{{ RET val_list f (rev xs); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  change NONEV with (val_list f []).
  wp_apply vlist_rev_loop_spec; [done|].
  by rewrite rev_alt.
Qed.
Global Opaque vlist_rev_code.

Lemma vlist_tail_spec {A} (f : A -> val) (xs : list A) :
  {{{ True }}}
    vlist_tail_code (val_list f xs)
  {{{ RET val_list f (tail xs); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. destruct xs; wp_pures; by iApply "HΦ".
Qed.
Global Opaque vlist_tail_code.

Lemma vlist_mem_nat_spec (n : nat) (xs : list nat) :
  {{{ True }}}
    vlist_mem_nat_code #(Z.of_nat n) (list_val xs)
  {{{ RET #(bool_decide (n ∈ xs)); True }}}.
Proof.
  induction xs as [|x xs IH]; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - rewrite bool_decide_eq_false_2; [by iApply "HΦ"|by apply not_elem_of_nil].
  - destruct (decide (x = n)) as [->|Hne].
    + rewrite (bool_decide_eq_true_2 (#(Z.of_nat n) = #(Z.of_nat n))) //. wp_pures.
      rewrite bool_decide_eq_true_2; [by iApply "HΦ"|by left].
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat x) = #(Z.of_nat n))); [|intros [= ?]; lia]. wp_pures.
      wp_apply IH; [done|]. iIntros "_".
      rewrite (bool_decide_ext (n ∈ xs) (n ∈ x :: xs)); [by iApply "HΦ"|].
      rewrite elem_of_cons. split; [by right|]. intros [->|]; done.
Qed.
Global Opaque vlist_mem_nat_code.

Lemma zip_cleanups_spec (bs : list cmd) (es : list env) (os : list nat) :
  {{{ True }}}
    zip_cleanups_code (cmds_val bs) (envs_val es) (list_val os)
  {{{ RET cleanups_val (zip_cleanups bs es os); True }}}.
Proof.
  induction bs as [|b bs IH] in es, os |- *; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - destruct es as [|e es]; [wp_pures; by iApply "HΦ"|].
    destruct os as [|o os]; [wp_pures; by iApply "HΦ"|].
    wp_pures. wp_apply (IH es os); [done|]. iIntros "_".
    wp_pures. by iApply "HΦ".
Qed.
Global Opaque zip_cleanups_code.

Lemma owner_cleanups_spec (x : owner_state) :
  {{{ True }}}
    owner_cleanups_code (owner_val x)
  {{{ RET cleanups_val (owner_cleanups x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply zip_cleanups_spec; [done|]. iIntros "_".
  wp_apply vlist_rev_spec; [done|].
  by rewrite /owner_cleanups.
Qed.
Global Opaque owner_cleanups_code.

Lemma links_without_spec (l : link) (ms : list link) :
  {{{ True }}}
    links_without_code (link_val l) (links_val ms)
  {{{ RET links_val (filter (fun m => ~ (l_child m = l_child l /\ l_acquired m = l_acquired l)) ms); True }}}.
Proof.
  induction ms as [|m ms IH]; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - wp_apply IH; [done|]. iIntros "_".
    rewrite {1 2 3 4}/link_val. wp_pures.
    rewrite filter_cons.
    case_bool_decide as Hc.
    + assert (l_child m = l_child l) as Hce by (injection Hc; lia). wp_pures.
      case_bool_decide as Ha.
      * assert (l_acquired m = l_acquired l) as Hae by (injection Ha; lia). wp_pures.
        rewrite decide_False; [by iApply "HΦ"|intros Hn; by apply Hn].
      * assert (l_acquired m ≠ l_acquired l) by (intros Heq; apply Ha; by rewrite Heq). wp_pures.
        rewrite decide_True; [by iApply "HΦ"|intros [_ ?]; done].
    + assert (l_child m ≠ l_child l) by (intros Heq; apply Hc; by rewrite Heq). wp_pures.
      rewrite decide_True; [by iApply "HΦ"|intros [? _]; done].
Qed.
Global Opaque links_without_code.

(** ** Environment and expression evaluation *)

Definition env_lookup_code : val :=
  rec: "lookup" "e" "x" :=
    match: "e" with
      NONE => NONE
    | SOME "p" =>
        let: "kv" := Fst "p" in
        if: "x" = Fst "kv" then SOME (Snd "kv") else "lookup" (Snd "p") "x"
    end.

Definition without_prev_code : val :=
  rec: "flt" "e" :=
    match: "e" with
      NONE => NONE
    | SOME "p" =>
        let: "kv" := Fst "p" in
        let: "r" := "flt" (Snd "p") in
        if: Fst "kv" = #(Z.of_nat prev_var) then "r" else SOME ("kv", "r")
    end.

Definition eval_expr_code : val :=
  rec: "eval" "e" "prev" "x" :=
    let: "tag" := Fst "x" in
    if: "tag" = #0 then SOME (InjL (Snd "x"))
    else if: "tag" = #1 then
      match: env_lookup_code "e" (Snd "x") with
        NONE => NONE
      | SOME "z" => SOME (InjL "z")
      end
    else if: "tag" = #2 then
      match: "prev" with
        NONE => NONE
      | SOME "z" => SOME (InjL "z")
      end
    else
      let: "tag'" := Fst "tag" in
      let: "a" := Snd "tag" in
      let: "b" := Snd "x" in
      match: "eval" "e" "prev" "a" with
        NONE => NONE
      | SOME "ra" =>
          match: "ra" with
            InjR "fa" => SOME (InjR "fa")
          | InjL "u" =>
              match: "eval" "e" "prev" "b" with
                NONE => NONE
              | SOME "rb" =>
                  match: "rb" with
                    InjR "fb" => SOME (InjR "fb")
                  | InjL "v" =>
                      if: "tag'" = #3 then SOME (InjL ("u" + "v"))
                      else if: "tag'" = #4 then SOME (InjL ("u" - "v"))
                      else if: "tag'" = #5 then SOME (InjL ("u" * "v"))
                      else if: "tag'" = #6 then
                        if: "v" = #0 then SOME (InjR #div_zero_failure)
                        else SOME (InjL ("u" `quot` "v"))
                      else if: "tag'" = #7 then
                        SOME (InjL (if: "u" < "v" then #1 else #0))
                      else SOME (InjL (if: "u" = "v" then #1 else #0))
                  end
              end
          end
      end.

Definition eval_in_env_code : val :=
  λ: "e" "x",
    let: "prev" := env_lookup_code "e" #(Z.of_nat prev_var) in
    eval_expr_code "e" "prev" "x".

Lemma env_lookup_spec (e : env) (x : var) :
  {{{ True }}}
    env_lookup_code (env_val e) #(Z.of_nat x)
  {{{ RET opt_z_val (env_lookup e x); True }}}.
Proof.
  induction e as [|[y z] e IH]; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - case_bool_decide as Heq.
    + assert (x = y) as -> by (injection Heq; lia).
      rewrite /= Nat.eqb_refl. wp_pures. by iApply "HΦ".
    + assert (x ≠ y) by (intros Heq'; apply Heq; by rewrite Heq').
      rewrite /= (proj2 (Nat.eqb_neq x y) ltac:(done)).
      wp_pures. by wp_apply IH; [done|].
Qed.
Global Opaque env_lookup_code.

Lemma without_prev_spec (e : env) :
  {{{ True }}}
    without_prev_code (env_val e)
  {{{ RET env_val (without_prev e); True }}}.
Proof.
  induction e as [|[y z] e IH]; iIntros (Φ) "_ HΦ"; wp_rec; wp_pures.
  - by iApply "HΦ".
  - wp_apply IH; [done|]. iIntros "_". wp_pures.
    rewrite /without_prev filter_cons.
    case_bool_decide as Heq.
    + assert (y = prev_var) as -> by (injection Heq; lia).
      wp_pures. by iApply "HΦ".
    + assert (y ≠ prev_var) by (intros Heq'; apply Heq; by rewrite Heq').
      wp_pures. rewrite decide_True // /=. by iApply "HΦ".
Qed.
Global Opaque without_prev_code.

Lemma eval_expr_spec (e : env) (p : option Z) (x : Syntax.expr) :
  {{{ True }}}
    eval_expr_code (env_val e) (opt_z_val p) (compile_expr x)
  {{{ RET match eval_expr e p x with Some r => SOMEV (outcome_val r) | None => NONEV end; True }}}.
Proof.
  induction x as [z|y| |a IHa b IHb|a IHa b IHb|a IHa b IHb|a IHa b IHb|a IHa b IHb|a IHa b IHb];
    iIntros (Φ) "_ HΦ"; simpl; wp_rec; wp_pures.
  - by iApply "HΦ".
  - wp_apply env_lookup_spec; [done|]. iIntros "_".
    destruct (env_lookup e y); simpl; wp_pures; by iApply "HΦ".
  - destruct p; simpl; wp_pures; by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    case_bool_decide as Hv.
    + assert (v = 0%Z) as -> by (by injection Hv). wp_pures. by iApply "HΦ".
    + assert (v ≠ 0%Z) by (intros Heq; apply Hv; by rewrite Heq). rewrite decide_False // /=. wp_pures. by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    case_bool_decide as Hlt; wp_pures.
    + rewrite /bool_val (proj2 (Z.ltb_lt u v) Hlt). by iApply "HΦ".
    + rewrite /bool_val (proj2 (Z.ltb_ge u v) ltac:(lia)). by iApply "HΦ".
  - wp_apply IHa; [done|]. iIntros "_".
    destruct (eval_expr e p a) as [[u|fa]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    wp_apply IHb; [done|]. iIntros "_".
    destruct (eval_expr e p b) as [[v|fb]|]; simpl; wp_pures; [|by iApply "HΦ"|by iApply "HΦ"].
    case_bool_decide as Heq; wp_pures.
    + assert (u = v) as -> by (by injection Heq). rewrite /bool_val Z.eqb_refl. by iApply "HΦ".
    + assert (u ≠ v) by (intros Heq'; apply Heq; by rewrite Heq'). rewrite /bool_val (proj2 (Z.eqb_neq u v) ltac:(done)). by iApply "HΦ".
Qed.
Global Opaque eval_expr_code.

Lemma eval_in_env_spec (e : env) (x : Syntax.expr) :
  {{{ True }}}
    eval_in_env_code (env_val e) (compile_expr x)
  {{{ RET match eval_expr e (env_lookup e prev_var) x with Some r => SOMEV (outcome_val r) | None => NONEV end; True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply env_lookup_spec; [done|]. iIntros "_". wp_pures.
  by wp_apply eval_expr_spec; [done|].
Qed.
Global Opaque eval_in_env_code.

(** ** Pure node and owner value transformers *)

Definition node_with_value_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "a" := Fst "ab" in
    ((((Fst (Fst "a"), "v"), Snd "a"), Snd "ab"), Snd "d").

Definition node_with_failure_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "a" := Fst "ab" in
    (((Fst "a", ("v", Snd (Snd "a"))), Snd "ab"), Snd "d").

Definition node_with_status_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "a" := Fst "ab" in
    (((Fst "a", (Fst (Snd "a"), "v")), Snd "ab"), Snd "d").

Definition node_with_freshness_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "b" := Snd "ab" in
    ((Fst "ab", (("v", Snd (Fst "b")), Snd "b")), Snd "d").

Definition node_with_unannounced_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "b" := Snd "ab" in
    ((Fst "ab", ((Fst (Fst "b"), "v"), Snd "b")), Snd "d").

Definition node_with_runs_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "b" := Snd "ab" in
    ((Fst "ab", (Fst "b", ("v", Snd (Snd "b")))), Snd "d").

Definition node_with_published_code : val :=
  λ: "v" "d",
    let: "ab" := Fst "d" in
    let: "b" := Snd "ab" in
    ((Fst "ab", (Fst "b", (Fst (Snd "b"), "v"))), Snd "d").

Definition node_with_disposed_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "c" := Fst "cd" in
    (Fst "d", ((("v", Snd (Fst "c")), Snd "c"), Snd "cd")).

Definition node_with_queued_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "c" := Fst "cd" in
    (Fst "d", (((Fst (Fst "c"), "v"), Snd "c"), Snd "cd")).

Definition node_with_in_cleanup_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "c" := Fst "cd" in
    (Fst "d", ((Fst "c", ("v", Snd (Snd "c"))), Snd "cd")).

Definition node_with_scope_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "c" := Fst "cd" in
    let: "c2" := Snd "c" in
    (Fst "d", ((Fst "c", (Fst "c2", ("v", Snd (Snd "c2")))), Snd "cd")).

Definition node_with_violated_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "c" := Fst "cd" in
    let: "c2" := Snd "c" in
    (Fst "d", ((Fst "c", (Fst "c2", (Fst (Snd "c2"), "v"))), Snd "cd")).

Definition node_with_link_code : val :=
  λ: "v" "d",
    let: "cd" := Snd "d" in
    let: "d2" := Snd "cd" in
    let: "d22" := Snd "d2" in
    (Fst "d", (Fst "cd", (Fst "d2", (Fst "d22", (Fst (Snd "d22"), "v"))))).

Lemma node_with_value_spec (v : Z) (d : node) :
  {{{ True }}} node_with_value_code #v (node_val d) {{{ RET node_val (node_with_value v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_value_code.

Lemma node_with_failure_spec (v : option Z) (d : node) :
  {{{ True }}} node_with_failure_code (opt_z_val v) (node_val d) {{{ RET node_val (node_with_failure v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_failure_code.

Lemma node_with_status_spec (v : status) (d : node) :
  {{{ True }}} node_with_status_code (status_val v) (node_val d) {{{ RET node_val (node_with_status v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_status_code.

Lemma node_with_freshness_spec (v : freshness) (d : node) :
  {{{ True }}} node_with_freshness_code (freshness_val v) (node_val d) {{{ RET node_val (node_with_freshness v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_freshness_code.

Lemma node_with_unannounced_spec (v : bool) (d : node) :
  {{{ True }}} node_with_unannounced_code #v (node_val d) {{{ RET node_val (node_with_unannounced v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_unannounced_code.

Lemma node_with_runs_spec (v : nat) (d : node) :
  {{{ True }}} node_with_runs_code #(Z.of_nat v) (node_val d) {{{ RET node_val (node_with_runs v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_runs_code.

Lemma node_with_published_spec (v : bool) (d : node) :
  {{{ True }}} node_with_published_code #v (node_val d) {{{ RET node_val (node_with_published v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_published_code.

Lemma node_with_disposed_spec (v : bool) (d : node) :
  {{{ True }}} node_with_disposed_code #v (node_val d) {{{ RET node_val (node_with_disposed v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_disposed_code.

Lemma node_with_queued_spec (v : bool) (d : node) :
  {{{ True }}} node_with_queued_code #v (node_val d) {{{ RET node_val (node_with_queued v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_queued_code.

Lemma node_with_in_cleanup_spec (v : bool) (d : node) :
  {{{ True }}} node_with_in_cleanup_code #v (node_val d) {{{ RET node_val (node_with_in_cleanup v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_in_cleanup_code.

Lemma node_with_scope_spec (v : option owner_id) (d : node) :
  {{{ True }}} node_with_scope_code (opt_nat_val v) (node_val d) {{{ RET node_val (node_with_scope v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_scope_code.

Lemma node_with_violated_spec (v : bool) (d : node) :
  {{{ True }}} node_with_violated_code #v (node_val d) {{{ RET node_val (node_with_violated v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_violated_code.

Lemma node_with_link_spec (v : option link) (d : node) :
  {{{ True }}} node_with_link_code (opt_link_val v) (node_val d) {{{ RET node_val (node_with_link v d); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque node_with_link_code.

Definition owner_with_child_code : val :=
  λ: "l" "x",
    let: "a" := Fst "x" in
    let: "b" := Snd "x" in
    let: "acq" := Fst (Fst "b") in
    (((vlist_snoc_code (Fst (Fst "a")) "l", Snd (Fst "a")), Snd "a"),
     ((("acq" + #1, Snd (Fst "b")), Snd "b"))).

Definition owner_with_cleanup_code : val :=
  λ: "b_cmd" "e_env" "x",
    let: "a" := Fst "x" in
    let: "b" := Snd "x" in
    let: "acq" := Fst (Fst "b") in
    let: "cs" := vlist_snoc_code (Snd (Fst "a")) "b_cmd" in
    let: "es" := vlist_snoc_code (Fst (Snd "a")) "e_env" in
    let: "os" := vlist_snoc_code (Snd (Snd "a")) "acq" in
    (((Fst (Fst "a"), "cs"), ("es", "os")),
     ((("acq" + #1, Snd (Fst "b")), Snd "b"))).

Definition owner_detached_code : val :=
  λ: "x",
    let: "b" := Snd "x" in
    let: "gen" := Snd (Fst "b") in
    (((NONE, NONE), (NONE, NONE)),
     (((#0, "gen" + #1), Snd "b"))).

Definition owner_retired_code : val :=
  λ: "x",
    let: "b" := Snd "x" in
    (Fst "x", (Fst "b", (#true, Snd (Snd "b")))).

Definition owner_with_error_code : val :=
  λ: "f" "x",
    let: "b" := Snd "x" in
    let: "b2" := Snd "b" in
    let: "b22" := Snd "b2" in
    let: "eh" := Snd "b22" in
    (Fst "x", (Fst "b", (Fst "b2", (Fst "b22", (vlist_snoc_code (Fst "eh") "f", Snd "eh"))))).

Definition owner_without_code : val :=
  λ: "l" "x",
    let: "a" := Fst "x" in
    (((links_without_code "l" (Fst (Fst "a")), Snd (Fst "a")), Snd "a"), Snd "x").

Lemma owner_with_child_spec (l : link) (x : owner_state) :
  {{{ True }}} owner_with_child_code (link_val l) (owner_val x) {{{ RET owner_val (owner_with_child l x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite links_val_eq. wp_apply vlist_snoc_spec; [done|]. iIntros "_". wp_pures.
  rewrite -links_val_eq (_ : (Z.of_nat (o_acquisitions x) + 1)%Z = Z.of_nat (S (o_acquisitions x))); [|lia].
  by iApply "HΦ".
Qed.
Global Opaque owner_with_child_code.

Lemma owner_with_cleanup_spec (b : cmd) (e : env) (x : owner_state) :
  {{{ True }}}
    owner_with_cleanup_code (compile_cmd b) (env_val e) (owner_val x)
  {{{ RET owner_val (owner_with_cleanup b e x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite cmds_val_eq. wp_apply vlist_snoc_spec; [done|]. iIntros "_". wp_pures.
  rewrite envs_val_eq. wp_apply vlist_snoc_spec; [done|]. iIntros "_". wp_pures.
  rewrite list_val_eq. wp_apply (vlist_snoc_spec (fun n : nat => #(Z.of_nat n)) (o_cleanup_order x) (o_acquisitions x)); [done|].
  iIntros "_". wp_pures.
  rewrite -cmds_val_eq -envs_val_eq -list_val_eq.
  rewrite (_ : (Z.of_nat (o_acquisitions x) + 1)%Z = Z.of_nat (S (o_acquisitions x))); [|lia].
  by iApply "HΦ".
Qed.
Global Opaque owner_with_cleanup_code.

Lemma owner_detached_spec (x : owner_state) :
  {{{ True }}} owner_detached_code (owner_val x) {{{ RET owner_val (owner_detached x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite (_ : (Z.of_nat (o_generation x) + 1)%Z = Z.of_nat (S (o_generation x))); [|lia].
  by iApply "HΦ".
Qed.
Global Opaque owner_detached_code.

Lemma owner_retired_spec (x : owner_state) :
  {{{ True }}} owner_retired_code (owner_val x) {{{ RET owner_val (owner_retired x); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque owner_retired_code.

Lemma owner_with_error_spec (f : Z) (x : owner_state) :
  {{{ True }}} owner_with_error_code #f (owner_val x) {{{ RET owner_val (owner_with_error f x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite zs_val_eq. wp_apply (vlist_snoc_spec (fun z : Z => #z) (o_errors x) f); [done|]. iIntros "_". wp_pures.
  rewrite -zs_val_eq. by iApply "HΦ".
Qed.
Global Opaque owner_with_error_code.

Lemma owner_without_spec (l : link) (x : owner_state) :
  {{{ True }}} owner_without_code (link_val l) (owner_val x) {{{ RET owner_val (owner_without l x); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply links_without_spec; [done|]. iIntros "_". wp_pures. by iApply "HΦ".
Qed.
Global Opaque owner_without_code.

End pure_helpers.

(** ** Ghost and control helpers on [is_graph] *)

Section graph_helpers.
Context `{!heapGS Σ, !graphG Σ}.

Lemma with_ghost_step γ g gh s :
  is_graph γ g s ==∗ is_graph γ g (with_ghost gh s).
Proof. apply is_graph_ghost_step; done. Qed.

Lemma with_trace_step γ g tr s :
  is_graph γ g s ==∗ is_graph γ g (with_trace tr s).
Proof. apply is_graph_ghost_step; done. Qed.

Lemma with_registered_step γ g reg s :
  is_graph γ g s ==∗ is_graph γ g (with_registered reg s).
Proof. apply is_graph_ghost_step; done. Qed.

Lemma emit_step γ g ev s :
  is_graph γ g s ==∗ is_graph γ g (with_trace (g_trace s ++ [ev]) s).
Proof. apply with_trace_step. Qed.

Lemma ghost_read_step γ g c n pre s :
  is_graph γ g s ==∗ is_graph γ g (ghost_read c n pre s).
Proof. rewrite /ghost_read. destruct (g_nodes s !! n); [apply with_ghost_step|by iIntros "$"]. Qed.

Lemma ghost_run_start_step γ g n s :
  is_graph γ g s ==∗ is_graph γ g (ghost_run_start n s).
Proof. apply with_ghost_step. Qed.

Lemma ghost_effect_start_step γ g n s :
  is_graph γ g s ==∗ is_graph γ g (ghost_effect_start n s).
Proof. apply with_ghost_step. Qed.

Lemma ghost_publish_step γ g n s :
  is_graph γ g s ==∗ is_graph γ g (ghost_publish n s).
Proof. rewrite /ghost_publish. destruct (g_nodes s !! n); [apply with_ghost_step|by iIntros "$"]. Qed.

Lemma ghost_run_end_step γ g n r moved s :
  is_graph γ g s ==∗ is_graph γ g (ghost_run_end n r moved s).
Proof. rewrite /ghost_run_end. destruct (g_nodes s !! n); [apply with_ghost_step|by iIntros "$"]. Qed.

Lemma ghost_dirty_cleared_step γ g n runs s :
  is_graph γ g s ==∗ is_graph γ g (ghost_dirty_cleared n runs s).
Proof. apply with_ghost_step. Qed.

Lemma ghost_resolved_step γ g n s :
  is_graph γ g s ==∗ is_graph γ g (ghost_resolved n s).
Proof. apply with_ghost_step. Qed.

Lemma ghost_registered_step γ g h s :
  is_graph γ g s ==∗ is_graph γ g (ghost_registered h s).
Proof. apply with_registered_step. Qed.

Definition graph_with_flushing_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "a" := Fst "ab" in
    graph_set_ctrl_code "g" (((("v", Snd (Fst "a")), Snd "a"), Snd "ab"), Snd "c").

Definition graph_with_flush_owed_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "a" := Fst "ab" in
    graph_set_ctrl_code "g" ((((Fst (Fst "a"), "v"), Snd "a"), Snd "ab"), Snd "c").

Definition graph_with_batch_depth_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "a" := Fst "ab" in
    graph_set_ctrl_code "g" (((Fst "a", ("v", Snd (Snd "a"))), Snd "ab"), Snd "c").

Definition graph_with_pull_depth_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "a" := Fst "ab" in
    graph_set_ctrl_code "g" (((Fst "a", (Fst (Snd "a"), "v")), Snd "ab"), Snd "c").

Definition graph_with_current_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "b" := Snd "ab" in
    graph_set_ctrl_code "g" ((Fst "ab", (("v", Snd (Fst "b")), Snd "b")), Snd "c").

Definition graph_with_untracked_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "b" := Snd "ab" in
    graph_set_ctrl_code "g" ((Fst "ab", ((Fst (Fst "b"), "v"), Snd "b")), Snd "c").

Definition graph_with_current_owner_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "b" := Snd "ab" in
    graph_set_ctrl_code "g" ((Fst "ab", (Fst "b", ("v", Snd (Snd "b")))), Snd "c").

Definition graph_with_next_id_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ab" := Fst "c" in
    let: "b" := Snd "ab" in
    graph_set_ctrl_code "g" ((Fst "ab", (Fst "b", (Fst (Snd "b"), "v"))), Snd "c").

Definition graph_with_running_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "cc" := Snd "c" in
    graph_set_ctrl_code "g" (Fst "c", (("v", Snd (Fst "cc")), Snd "cc")).

Definition graph_with_checking_code : val :=
  λ: "g" "v",
    let: "c" := graph_get_ctrl_code "g" in
    let: "cc" := Snd "c" in
    graph_set_ctrl_code "g" (Fst "c", ((Fst (Fst "cc"), "v"), Snd "cc")).

Lemma graph_with_flushing_spec γ g s (v : bool) :
  {{{ is_graph γ g s }}} graph_with_flushing_code g #v {{{ RET #(); is_graph γ g (with_flushing v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_flushing v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_flushing_code.

Lemma graph_with_flush_owed_spec γ g s (v : bool) :
  {{{ is_graph γ g s }}} graph_with_flush_owed_code g #v {{{ RET #(); is_graph γ g (with_flush_owed v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_flush_owed v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_flush_owed_code.

Lemma graph_with_batch_depth_spec γ g s (v : nat) :
  {{{ is_graph γ g s }}} graph_with_batch_depth_code g #(Z.of_nat v) {{{ RET #(); is_graph γ g (with_batch_depth v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_batch_depth v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_batch_depth_code.

Lemma graph_with_pull_depth_spec γ g s (v : nat) :
  {{{ is_graph γ g s }}} graph_with_pull_depth_code g #(Z.of_nat v) {{{ RET #(); is_graph γ g (with_pull_depth v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_pull_depth v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_pull_depth_code.

Lemma graph_with_current_spec γ g s (v : option node_id) :
  {{{ is_graph γ g s }}} graph_with_current_code g (opt_nat_val v) {{{ RET #(); is_graph γ g (with_current v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_current v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_current_code.

Lemma graph_with_untracked_spec γ g s (v : bool) :
  {{{ is_graph γ g s }}} graph_with_untracked_code g #v {{{ RET #(); is_graph γ g (with_untracked v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_untracked v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_untracked_code.

Lemma graph_with_current_owner_spec γ g s (v : option owner_id) :
  {{{ is_graph γ g s }}} graph_with_current_owner_code g (opt_nat_val v) {{{ RET #(); is_graph γ g (with_current_owner v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_current_owner v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_current_owner_code.

Lemma graph_with_next_id_spec γ g s (v : nat) :
  {{{ is_graph γ g s }}} graph_with_next_id_code g #(Z.of_nat v) {{{ RET #(); is_graph γ g (with_next_id v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_next_id v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_next_id_code.

Lemma graph_with_running_spec γ g s (v : list node_id) :
  {{{ is_graph γ g s }}} graph_with_running_code g (list_val v) {{{ RET #(); is_graph γ g (with_running v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_running v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_running_code.

Lemma graph_with_checking_spec γ g s (v : list node_id) :
  {{{ is_graph γ g s }}} graph_with_checking_code g (list_val v) {{{ RET #(); is_graph γ g (with_checking v s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. wp_apply (graph_set_ctrl_spec _ _ s (with_checking v s) with "Hg"); try done.
Qed.
Global Opaque graph_with_checking_code.

Definition push_running_code : val :=
  λ: "g" "n",
    let: "c" := graph_get_ctrl_code "g" in
    let: "r" := Fst (Fst (Snd "c")) in
    graph_with_running_code "g" (SOME ("n", "r")).

Definition pop_running_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "r" := Fst (Fst (Snd "c")) in
    graph_with_running_code "g" (vlist_tail_code "r").

Definition push_checking_code : val :=
  λ: "g" "n",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ck" := Snd (Fst (Snd "c")) in
    graph_with_checking_code "g" (SOME ("n", "ck")).

Definition pop_checking_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "ck" := Snd (Fst (Snd "c")) in
    graph_with_checking_code "g" (vlist_tail_code "ck").

Lemma push_running_spec γ g s (n : node_id) :
  {{{ is_graph γ g s }}} push_running_code g #(Z.of_nat n) {{{ RET #(); is_graph γ g (with_running (n :: g_running s) s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. change (SOMEV (#(Z.of_nat n), list_val (g_running s))) with (list_val (n :: g_running s)).
  by wp_apply (graph_with_running_spec with "Hg").
Qed.
Global Opaque push_running_code.

Lemma pop_running_spec γ g s :
  {{{ is_graph γ g s }}} pop_running_code g {{{ RET #(); is_graph γ g (with_running (tail (g_running s)) s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. rewrite list_val_eq. wp_apply vlist_tail_spec; [done|]. iIntros "_".
  rewrite -list_val_eq. by wp_apply (graph_with_running_spec with "Hg").
Qed.
Global Opaque pop_running_code.

Lemma push_checking_spec γ g s (n : node_id) :
  {{{ is_graph γ g s }}} push_checking_code g #(Z.of_nat n) {{{ RET #(); is_graph γ g (with_checking (n :: g_checking s) s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. change (SOMEV (#(Z.of_nat n), list_val (g_checking s))) with (list_val (n :: g_checking s)).
  by wp_apply (graph_with_checking_spec with "Hg").
Qed.
Global Opaque push_checking_code.

Lemma pop_checking_spec γ g s :
  {{{ is_graph γ g s }}} pop_checking_code g {{{ RET #(); is_graph γ g (with_checking (tail (g_checking s)) s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. rewrite list_val_eq. wp_apply vlist_tail_spec; [done|]. iIntros "_".
  rewrite -list_val_eq. by wp_apply (graph_with_checking_spec with "Hg").
Qed.
Global Opaque pop_checking_code.

(** ** Engine context and node helpers *)

Definition deferring_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "cur" := Fst (Fst (Snd (Fst "c"))) in
    let: "pd" := Snd (Snd (Fst (Fst "c"))) in
    match: "cur" with
      NONE => #0 < "pd"
    | SOME <> => #true
    end.

Definition outside_bodies_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "pd" := Snd (Snd (Fst (Fst "c"))) in
    let: "cur" := Fst (Fst (Snd (Fst "c"))) in
    let: "ut" := Snd (Fst (Snd (Fst "c"))) in
    let: "no_cur" := match: "cur" with NONE => #true | SOME <> => #false end in
    if: "pd" = #0 then
      if: "no_cur" then ~ "ut" else #false
    else #false.

Definition live_current_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    match: Fst (Fst (Snd (Fst "c"))) with
      NONE => NONE
    | SOME "cid" =>
        match: graph_get_node_code "g" "cid" with
          NONE => NONE
        | SOME "d" =>
            if: Fst (Fst (Fst (Snd "d"))) then NONE else SOME "cid"
        end
    end.

Definition next_id_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "id" := Snd (Snd (Snd (Fst "c"))) + #1 in
    graph_with_next_id_code "g" "id";;
    "id".

Definition add_node_code : val :=
  λ: "g" "n" "d",
    graph_put_node_code "g" "n" "d".

Definition log_label_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "b" := Snd (Fst "c") in
    match: Fst (Fst "b") with
      NONE =>
        match: Fst (Snd "b") with
          NONE => #0
        | SOME "o" =>
            match: graph_get_owner_code "g" "o" with
              NONE => #0
            | SOME "x" => Snd (Snd (Snd (Snd (Snd "x"))))
            end
        end
    | SOME "n" => "n"
    end.

Definition note_host_read_code : val :=
  λ: "g" "n" "o", #().

Definition unlink_code : val :=
  λ: "g" "l",
    let: "o" := Snd (Fst (Fst "l")) in
    let: "lgen" := Snd (Fst "l") in
    match: graph_get_owner_code "g" "o" with
      NONE => #()
    | SOME "x" =>
        let: "xgen" := Snd (Fst (Snd "x")) in
        match: "lgen" with
          NONE => #()
        | SOME "g0" =>
            if: "g0" = "xgen" then
              graph_put_owner_code "g" "o" (owner_without_code "l" "x")
            else #()
        end
    end.

Definition record_error_code : val :=
  λ: "g" "o" "f",
    match: graph_get_owner_code "g" "o" with
      NONE => #()
    | SOME "x" =>
        let: "sink" := Fst (Snd (Snd (Snd "x"))) in
        match: "sink" with
          NONE => graph_put_owner_code "g" "o" (owner_with_error_code "f" "x")
        | SOME "p" =>
            match: graph_get_owner_code "g" "p" with
              NONE => #()
            | SOME "y" => graph_put_owner_code "g" "p" (owner_with_error_code "f" "y")
            end
        end
    end.

Definition announce_code : val :=
  λ: "g" "n",
    let: "c" := graph_get_ctrl_code "g" in
    let: "fu" := Fst (Snd (Snd "c")) in
    if: "fu" then
      match: graph_get_node_code "g" "n" with
        NONE => #()
      | SOME "d" => graph_set_node_code "g" "n" (node_with_unannounced_code #true "d")
      end
    else #().

Definition track_code : val :=
  λ: "g" "n",
    match: live_current_code "g" with
      NONE => #()
    | SOME "c" => graph_sources_add_code "g" "c" "n"
    end.

Definition mark_reader_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "cur" := Fst (Fst (Snd (Fst "c"))) in
    let: "frm" := Snd (Snd (Snd "c")) in
    match: "cur" with
      NONE => #()
    | SOME "cid" => if: "frm" then mark_check_code "g" "cid" else #()
    end.

Definition current_owner_code : val :=
  λ: "g",
    let: "c" := graph_get_ctrl_code "g" in
    let: "b" := Snd (Fst "c") in
    match: Fst (Snd "b") with
      NONE =>
        match: Fst (Fst "b") with
          NONE => NONE
        | SOME "h" =>
            match: graph_get_node_code "g" "h" with
              NONE => NONE
            | SOME "d" =>
                let: "sc" := Fst (Snd (Snd (Fst (Snd "d")))) in
                match: "sc" with
                  NONE =>
                    let: "k" := Fst (Fst (Fst (Fst "d"))) in
                    let: "m" := Fst (Snd (Snd (Snd "d"))) in
                    if: ("k" = #1) && ("m" = #0) then
                      graph_set_node_code "g" "h" (node_with_violated_code #true "d");;
                      NONE
                    else
                      let: "o" := graph_fresh_owner_id_code "g" in
                      let: "disp" := Fst (Fst (Fst (Snd "d"))) in
                      let: "gen" := if: "disp" then #1 else #0 in
                      let: "new_o" := (((NONE, NONE), (NONE, NONE)),
                                       (((#0, "gen"), ("disp", (SOME #(Z.of_nat root_owner_id), (NONE, "h")))))) in
                      graph_put_owner_code "g" "o" "new_o";;
                      graph_set_node_code "g" "h" (node_with_scope_code (SOME "o") "d");;
                      graph_with_current_owner_code "g" (SOME "o");;
                      SOME "o"
                | SOME "o" =>
                    graph_with_current_owner_code "g" (SOME "o");;
                    SOME "o"
                end
            end
        end
    | SOME "o" => SOME "o"
    end.

Definition detached_code : val :=
  λ: "g" "o" "body",
    let: "c0" := graph_get_ctrl_code "g" in
    let: "bd0" := Fst (Snd (Fst (Fst "c0"))) in
    let: "cur0" := Fst (Fst (Snd (Fst "c0"))) in
    let: "co0" := Fst (Snd (Snd (Fst "c0"))) in
    graph_with_current_code "g" NONE;;
    graph_with_current_owner_code "g" (SOME "o");;
    graph_with_batch_depth_code "g" ("bd0" + #1);;
    "body" #();;
    let: "c1" := graph_get_ctrl_code "g" in
    let: "bd1" := Fst (Snd (Fst (Fst "c1"))) in
    let: "bd1'" := if: #0 < "bd1" then "bd1" - #1 else #0 in
    graph_with_current_code "g" "cur0";;
    graph_with_current_owner_code "g" "co0";;
    graph_with_batch_depth_code "g" "bd1'".

Definition hosted_code : val :=
  λ: "g" "n" "body",
    let: "c0" := graph_get_ctrl_code "g" in
    let: "cur0" := Fst (Fst (Snd (Fst "c0"))) in
    let: "co0" := Fst (Snd (Snd (Fst "c0"))) in
    graph_with_current_code "g" (SOME "n");;
    graph_with_current_owner_code "g" NONE;;
    let: "r" := "body" #() in
    graph_with_current_code "g" "cur0";;
    graph_with_current_owner_code "g" "co0";;
    "r".

Definition untracked_code : val :=
  λ: "g" "body",
    let: "c0" := graph_get_ctrl_code "g" in
    let: "cur0" := Fst (Fst (Snd (Fst "c0"))) in
    let: "ut0" := Snd (Fst (Snd (Fst "c0"))) in
    let: "flag" := match: "cur0" with NONE => "ut0" | SOME <> => #true end in
    graph_with_current_code "g" NONE;;
    graph_with_untracked_code "g" "flag";;
    let: "r" := "body" #() in
    graph_with_current_code "g" "cur0";;
    graph_with_untracked_code "g" "ut0";;
    "r".

Lemma deferring_spec γ g s :
  {{{ is_graph γ g s }}} deferring_code g {{{ RET #(deferring s); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /deferring. destruct (g_current s); wp_pures; [by iApply "HΦ"|].
  case_bool_decide as H1; rewrite /bool_decide; case_decide as H2; try lia; by iApply "HΦ".
Qed.
Global Opaque deferring_code.

Lemma outside_bodies_spec γ g s :
  {{{ is_graph γ g s }}} outside_bodies_code g {{{ RET #(outside_bodies s); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /outside_bodies.
  destruct (g_current s); wp_pures.
  - destruct (decide (g_pull_depth s = 0)) as [->|Hne].
    + wp_pures. rewrite andb_false_r /=. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_pull_depth s)) = #0)); [|intros [= ?]; lia].
      wp_pures. rewrite andb_false_r /=. by iApply "HΦ".
  - destruct (decide (g_pull_depth s = 0)) as [->|Hne].
    + rewrite (bool_decide_eq_true_2 (0%nat = 0%nat)) // /=.
      destruct (g_untracked s); wp_pures; by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_pull_depth s)) = #0)); [|intros [= ?]; lia].
      rewrite (bool_decide_eq_false_2 (g_pull_depth s = 0%nat)) // /=. wp_pures. by iApply "HΦ".
Qed.
Global Opaque outside_bodies_code.

Lemma live_current_spec γ g s :
  {{{ is_graph γ g s }}} live_current_code g {{{ RET opt_nat_val (live_current s); is_graph γ g s }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /live_current. destruct (g_current s) as [c|]; wp_pures; [|by iApply "HΦ"].
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  destruct (g_nodes s !! c) as [d|]; simpl; wp_pures; [|by iApply "HΦ"].
  destruct (n_disposed d); wp_pures; by iApply "HΦ".
Qed.
Global Opaque live_current_code.

Lemma next_id_spec γ g s :
  {{{ is_graph γ g s }}}
    next_id_code g
  {{{ RET #(Z.of_nat (S (g_next_id s))); is_graph γ g (with_next_id (S (g_next_id s)) s) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". wp_lam. wp_pures. wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg".
  wp_pures. rewrite (_ : (Z.of_nat (g_next_id s) + 1)%Z = Z.of_nat (S (g_next_id s))); [|lia].
  wp_apply (graph_with_next_id_spec with "Hg"). iIntros "Hg". wp_pures. by iApply "HΦ".
Qed.
Global Opaque next_id_code.

Lemma add_node_spec γ g s (n : node_id) (d : node) :
  {{{ is_graph γ g s }}}
    add_node_code g #(Z.of_nat n) (node_val d)
  {{{ RET #(); is_graph γ g (ghost_publish n (set_node n d s)) }}}.
Proof.
  iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  wp_apply (graph_put_node_spec with "Hg"). iIntros "Hg".
  iMod (ghost_publish_step with "Hg") as "Hg". iModIntro. by iApply "HΦ".
Qed.
Global Opaque add_node_code.

Lemma log_label_spec γ g s (l : node_id) (s' : State.state) :
  log_label s = Some (s', l) ->
  {{{ is_graph γ g s }}} log_label_code g {{{ RET #(Z.of_nat l); is_graph γ g s' }}}.
Proof.
  intros Hl. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /log_label in Hl.
  destruct (g_current s) as [n|]; simpl; wp_pures.
  - simplify_eq. by iApply "HΦ".
  - destruct (g_current_owner s) as [o|]; simpl; wp_pures.
    + wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
      destruct (g_owners s !! o) as [x|]; simplify_eq; simpl; wp_pures; by iApply "HΦ".
    + simplify_eq. by iApply "HΦ".
Qed.
Global Opaque log_label_code.

Lemma note_host_read_spec γ g (n : node_id) (o : outcome) (s s' : State.state) :
  note_host_read n o s = Some (s', tt) ->
  {{{ is_graph γ g s }}} note_host_read_code g #(Z.of_nat n) (outcome_val o) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hnh. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  rewrite /note_host_read /emit /m_modify in Hnh.
  destruct (g_current s), (g_current_owner s); try (simplify_eq; iModIntro; by iApply "HΦ").
  destruct (decide (o0 = root_owner_id)); simplify_eq.
  - iMod (emit_step _ _ (EvHostRead n o) with "Hg") as "Hg". iModIntro. by iApply "HΦ".
  - iModIntro. by iApply "HΦ".
Qed.
Global Opaque note_host_read_code.

Lemma unlink_spec γ g (l : link) (s s' : State.state) :
  unlink l s = Some (s', tt) ->
  {{{ is_graph γ g s }}} unlink_code g (link_val l) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hu. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
  rewrite /unlink /get_owner /put_owner /m_bind /m_modify /m_return in Hu.
  destruct (g_owners s !! l_owner l) as [x|] eqn:Hx; [|done].
  cbv beta iota in Hu. simpl. wp_pures.
  destruct (l_generation l) as [g0|] eqn:Hg0; simpl.
  - wp_pures. case_bool_decide as Heq.
    + assert (g0 = o_generation x) as -> by (injection Heq; lia).
      rewrite decide_True in Hu; [|done]. injection Hu as <-. wp_pures.
      wp_apply (owner_without_spec l x); [done|]. iIntros "_".
      by wp_apply (graph_put_owner_spec _ _ _ (l_owner l) (owner_without l x) with "Hg").
    + assert (g0 ≠ o_generation x) by (intros Heq'; apply Heq; by rewrite Heq').
      rewrite decide_False in Hu; [|intros [= ?]; done]. injection Hu as <-. wp_pures. by iApply "HΦ".
  - injection Hu as <-. wp_pures. by iApply "HΦ".
Qed.
Global Opaque unlink_code.

Lemma record_error_spec γ g (o : owner_id) (f : Z) (s s' : State.state) :
  record_error o f s = Some (s', tt) ->
  {{{ is_graph γ g s }}} record_error_code g #(Z.of_nat o) #f {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hre. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
  rewrite /record_error /get_owner /put_owner /m_bind /m_modify in Hre.
  destruct (g_owners s !! o) as [x|] eqn:Hx; [|done].
  cbv beta iota in Hre. simpl. wp_pures.
  destruct (o_sink x) as [p|] eqn:Hsink; simpl; wp_pures.
  - wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
    destruct (g_owners s !! p) as [y|] eqn:Hy; [|done].
    cbv beta iota in Hre. injection Hre as <-. simpl. wp_pures.
    wp_apply (owner_with_error_spec f y); [done|]. iIntros "_".
    by wp_apply (graph_put_owner_spec _ _ _ p (owner_with_error f y) with "Hg").
  - injection Hre as <-. wp_apply (owner_with_error_spec f x); [done|]. iIntros "_".
    by wp_apply (graph_put_owner_spec _ _ _ o (owner_with_error f x) with "Hg").
Qed.
Global Opaque record_error_code.

Lemma announce_spec γ g (n : node_id) (s s' : State.state) :
  announce n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} announce_code g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Ha. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /announce /modify_node in Ha.
  destruct (f_unannounced (g_flags s)) eqn:Hfu; wp_pures.
  - wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done].
    injection Ha as <-. simpl. wp_pures.
    wp_apply (node_with_unannounced_spec true d); [done|]. iIntros "_".
    by wp_apply (graph_set_node_spec _ _ _ n (node_with_unannounced true d) with "Hg"); [by eexists|].
  - injection Ha as <-. by iApply "HΦ".
Qed.
Global Opaque announce_code.

Lemma track_spec γ g (n : node_id) (s s' : State.state) :
  n ∈ dom (g_nodes s) ->
  track n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} track_code g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hn Ht. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  wp_apply (live_current_spec with "Hg"). iIntros "Hg".
  rewrite /track in Ht.
  destruct (live_current s) as [c|] eqn:Hlc; simpl.
  - assert (Hc : c ∈ dom (g_nodes s)).
    { revert Hlc. rewrite /live_current. destruct (g_current s) as [c0|]; [|done].
      destruct (g_nodes s !! c0) as [dc|] eqn:Hdc; [|done].
      destruct (n_disposed dc); [done|]. intros [= <-]. by apply elem_of_dom_2 in Hdc. }
    destruct (sources_add c n (g_edges s)) as [w|] eqn:Hadd; [|done].
    injection Ht as <-. wp_pures.
    wp_apply (graph_sources_add_spec _ _ _ c n w Hc Hn Hadd with "Hg"). iIntros "Hg".
    iMod (ghost_read_step with "Hg") as "Hg". iModIntro. by iApply "HΦ".
  - injection Ht as <-. wp_pures. iModIntro. by iApply "HΦ".
Qed.
Global Opaque track_code.

Lemma mark_reader_spec γ g (fuel : nat) (s s' : State.state) :
  mark_reader fuel s = Some (s', tt) ->
  {{{ is_graph γ g s }}} mark_reader_code g {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hmr. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /mark_reader in Hmr.
  destruct (g_current s) as [c|]; simpl; wp_pures.
  - destruct (f_reader_mark (g_flags s)); wp_pures.
    + destruct (mark_check fuel c s) as [s1|] eqn:Hmc; [|done].
      injection Hmr as ->. by wp_apply (mark_check_spec _ _ fuel c s s' Hmc with "Hg").
    + injection Hmr as <-. by iApply "HΦ".
  - injection Hmr as <-. by iApply "HΦ".
Qed.
Global Opaque mark_reader_code.

Lemma current_owner_spec γ g (s s' : State.state) (ro : option owner_id) :
  current_owner s = Some (s', ro) ->
  {{{ is_graph γ g s }}} current_owner_code g {{{ RET opt_nat_val ro; is_graph γ g s' }}}.
Proof.
  intros Hco. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite /current_owner in Hco.
  destruct (g_current_owner s) as [o|]; simpl; wp_pures.
  - injection Hco as <- <-. by iApply "HΦ".
  - destruct (g_current s) as [h|]; simpl; wp_pures; [|done].
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    destruct (g_nodes s !! h) as [d|] eqn:Hd; simpl; wp_pures; [|done].
    destruct (n_scope d) as [o|] eqn:Hsc; simpl; wp_pures.
    + injection Hco as <- <-. wp_apply (graph_with_current_owner_spec _ _ _ (Some o) with "Hg").
      iIntros "Hg". wp_pures. by iApply "HΦ".
    + set (pm := match n_kind d, n_mode d with KMemo, Pure => true | _, _ => false end) in *.
      wp_bind (If #(bool_decide _) _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #pm⌝)%I).
      { subst pm. destruct (n_kind d), (n_mode d); wp_pures; done. }
      iIntros (v) "->". destruct pm; wp_pures.
      * injection Hco as <- <-.
        wp_apply (node_with_violated_spec true d); [done|]. iIntros "_".
        wp_apply (graph_set_node_spec _ _ _ h (node_with_violated true d) with "Hg"); [by eexists|].
        iIntros "Hg". wp_pures. by iApply "HΦ".
      * injection Hco as <- <-.
        wp_apply (graph_fresh_owner_id_spec with "Hg"). iIntros "Hg". wp_pures.
        set (o := stdpp.base.fresh (dom (g_owners s))).
        set (xo := new_owner h (Some root_owner_id) (n_disposed d)).
        wp_bind (If _ _ _).
        iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (if n_disposed d then 1%nat else 0%nat))⌝)%I).
        { destruct (n_disposed d); wp_pures; done. }
        iIntros (v) "->". wp_pures.
        change ((((NONEV, NONEV), (NONEV, NONEV)),
          ((#0, #(Z.of_nat (if n_disposed d then 1%nat else 0%nat))), (#(n_disposed d), (SOMEV #(Z.of_nat root_owner_id), (NONEV, #(Z.of_nat h)))))))%V
          with (owner_val xo).
        wp_apply (graph_put_owner_spec _ _ _ o xo with "Hg"). iIntros "Hg". wp_pures.
        wp_apply (node_with_scope_spec (Some o) d); [done|]. iIntros "_".
        wp_apply (graph_set_node_spec _ _ _ h (node_with_scope (Some o) d) with "Hg"); [by eexists|].
        iIntros "Hg". wp_pures.
        wp_apply (graph_with_current_owner_spec _ _ _ (Some o) with "Hg"). iIntros "Hg". wp_pures.
        by iApply "HΦ".
Qed.
Global Opaque current_owner_code.

Lemma detached_spec γ g (o : owner_id) (body : M unit) (body_v : val) (s s' : State.state) :
  detached o body s = Some (s', tt) ->
  (∀ s1 s2, body s1 = Some (s2, tt) ->
    {{{ is_graph γ g s1 }}} body_v #() {{{ RET #(); is_graph γ g s2 }}}) ->
  {{{ is_graph γ g s }}} detached_code g #(Z.of_nat o) body_v {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hd Hbody. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ None with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_owner_spec _ _ _ (Some o) with "Hg"). iIntros "Hg". wp_pures.
  rewrite (_ : (Z.of_nat (g_batch_depth s) + 1)%Z = Z.of_nat (S (g_batch_depth s))); [|lia].
  wp_apply (graph_with_batch_depth_spec _ _ _ (S (g_batch_depth s)) with "Hg"). iIntros "Hg". wp_pures.
  rewrite /detached /m_bind /m_modify in Hd.
  set (s1 := with_batch_depth (S (g_batch_depth s)) (with_current_owner (Some o) (with_current None s))) in *.
  destruct (body s1) as [[s2 []]|] eqn:Hb; [|done].
  injection Hd as <-.
  wp_apply (Hbody s1 s2 Hb with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_bind (If _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (pred (g_batch_depth s2)))⌝)%I).
  { case_bool_decide; wp_pures; iPureIntro; f_equal; f_equal; lia. }
  iIntros (v) "->". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ (g_current s) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_owner_spec _ _ _ (g_current_owner s) with "Hg"). iIntros "Hg". wp_pures.
  by wp_apply (graph_with_batch_depth_spec _ _ _ (pred (g_batch_depth s2)) with "Hg").
Qed.
Global Opaque detached_code.

Lemma hosted_spec {A} (val_of : A -> val) γ g (n : node_id) (body : M A) (body_v : val) (s s' : State.state) (r : A) :
  hosted n body s = Some (s', r) ->
  (∀ s1 s2 r0, body s1 = Some (s2, r0) ->
    {{{ is_graph γ g s1 }}} body_v #() {{{ RET val_of r0; is_graph γ g s2 }}}) ->
  {{{ is_graph γ g s }}} hosted_code g #(Z.of_nat n) body_v {{{ RET val_of r; is_graph γ g s' }}}.
Proof.
  intros Hh Hbody. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ (Some n) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_owner_spec _ _ _ None with "Hg"). iIntros "Hg". wp_pures.
  rewrite /hosted /m_bind /m_modify /m_return in Hh.
  set (s1 := with_current_owner None (with_current (Some n) s)) in *.
  destruct (body s1) as [[s2 r0]|] eqn:Hb; [|done].
  injection Hh as <- ->.
  wp_apply (Hbody s1 s2 r Hb with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ (g_current s) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_owner_spec _ _ _ (g_current_owner s) with "Hg"). iIntros "Hg". wp_pures.
  by iApply "HΦ".
Qed.
Global Opaque hosted_code.

Lemma untracked_spec {A} (val_of : A -> val) γ g (body : M A) (body_v : val) (s s' : State.state) (r : A) :
  untracked body s = Some (s', r) ->
  (∀ s1 s2 r0, body s1 = Some (s2, r0) ->
    {{{ is_graph γ g s1 }}} body_v #() {{{ RET val_of r0; is_graph γ g s2 }}}) ->
  {{{ is_graph γ g s }}} untracked_code g body_v {{{ RET val_of r; is_graph γ g s' }}}.
Proof.
  intros Hu Hbody. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  set (flag := match g_current s with Some _ => true | None => g_untracked s end).
  wp_bind (Match _ _ _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #flag⌝)%I).
  { destruct (g_current s); wp_pures; done. }
  iIntros (v) "->". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ None with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_untracked_spec _ _ _ flag with "Hg"). iIntros "Hg". wp_pures.
  rewrite /untracked /m_bind /m_modify /m_return -/flag in Hu.
  set (s1 := with_untracked flag (with_current None s)) in *.
  destruct (body s1) as [[s2 r0]|] eqn:Hb; [|done].
  injection Hu as <- ->.
  wp_apply (Hbody s1 s2 r Hb with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_current_spec _ _ _ (g_current s) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_with_untracked_spec _ _ _ (g_untracked s) with "Hg"). iIntros "Hg". wp_pures.
  by iApply "HΦ".
Qed.
Global Opaque untracked_code.

(** ** Monad inversion and node/state composite helpers *)

Lemma m_bind_Some {A B} (m : M A) (k : A -> M B) s s' b :
  m_bind m k s = Some (s', b) ->
  ∃ s1 a, m s = Some (s1, a) /\ k a s1 = Some (s', b).
Proof.
  rewrite /m_bind. destruct (m s) as [[s1 a]|]; [eauto|done].
Qed.

Lemma m_return_Some {A} (a r : A) s s' :
  m_return a s = Some (s', r) -> s' = s /\ r = a.
Proof. intros [= <- <-]. done. Qed.

Lemma m_get_Some s s' r :
  m_get s = Some (s', r) -> s' = s /\ r = s.
Proof. intros [= <- <-]. done. Qed.

Lemma m_modify_Some f s s' u :
  m_modify f s = Some (s', u) -> s' = f s /\ u = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma m_step_Some f s s' u :
  m_step f s = Some (s', u) -> f s = Some s' /\ u = tt.
Proof. rewrite /m_step. destruct (f s); intros [= <- <-]; done. Qed.

Lemma get_node_Some n s s' d :
  get_node n s = Some (s', d) -> g_nodes s !! n = Some d /\ n ∈ dom (g_nodes s) /\ s' = s.
Proof.
  rewrite /get_node. destruct (g_nodes s !! n) as [d0|] eqn:Hd; [|done].
  intros [= <- <-]. split_and!; [done|by apply elem_of_dom_2 in Hd|done].
Qed.

Lemma get_owner_Some o s s' x :
  get_owner o s = Some (s', x) -> g_owners s !! o = Some x /\ s' = s.
Proof.
  rewrite /get_owner. destruct (g_owners s !! o) as [x0|]; [intros [= <- <-]; done|done].
Qed.

Lemma put_owner_Some o x s s' u :
  put_owner o x s = Some (s', u) -> s' = with_owners (<[o := x]> (g_owners s)) s /\ u = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma edges_Some u s s' r :
  edges u s = Some (s', r) -> s' = with_edges (u (g_edges s)) s /\ r = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma edges_opt_Some u s s' r :
  edges_opt u s = Some (s', r) -> ∃ w, u (g_edges s) = Some w /\ s' = with_edges w s /\ r = tt.
Proof.
  rewrite /edges_opt. destruct (u (g_edges s)) as [w|]; [intros [= <- <-]; eauto|done].
Qed.

Lemma push_running_Some n s s' r :
  push_running n s = Some (s', r) -> s' = with_running (n :: g_running s) s /\ r = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma pop_running_Some s s' r :
  pop_running s = Some (s', r) -> s' = with_running (tail (g_running s)) s /\ r = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma push_checking_Some n s s' r :
  push_checking n s = Some (s', r) -> s' = with_checking (n :: g_checking s) s /\ r = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma pop_checking_Some s s' r :
  pop_checking s = Some (s', r) -> s' = with_checking (tail (g_checking s)) s /\ r = tt.
Proof. intros [= <- <-]. done. Qed.

Lemma track_dom_nodes n s s' u :
  track n s = Some (s', u) -> dom (g_nodes s') = dom (g_nodes s).
Proof.
  rewrite /track. destruct (live_current s) as [c|]; [|intros [= <- _]; done].
  destruct (sources_add c n (g_edges s)) as [w|]; [|done].
  intros [= <- _]. rewrite /ghost_read.
  destruct (g_nodes (with_edges w s) !! n); done.
Qed.

Lemma modify_node_dom_nodes n f s s' u :
  modify_node n f s = Some (s', u) -> n ∈ dom (g_nodes s) /\ dom (g_nodes s') = dom (g_nodes s).
Proof.
  rewrite /modify_node. destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done].
  intros [= <- _]. apply elem_of_dom_2 in Hd. split; [done|].
  simpl. rewrite dom_insert_L. set_solver.
Qed.

Lemma unlink_dom_nodes l s s' u :
  unlink l s = Some (s', u) -> dom (g_nodes s') = dom (g_nodes s).
Proof.
  rewrite /unlink. intros H. apply m_bind_Some in H as (s1 & x & H1 & H2).
  apply get_owner_Some in H1 as [_ ->].
  case_decide; [apply put_owner_Some in H2 as [-> _]; done|apply m_return_Some in H2 as [-> _]; done].
Qed.

Definition graph_modify_node_code : val :=
  λ: "g" "n" "f",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" => graph_set_node_code "g" "n" ("f" "d")
    end.

Lemma graph_modify_node_spec γ g (n : node_id) (f : node -> node) (fv : val) (s s' : State.state) :
  modify_node n f s = Some (s', tt) ->
  (∀ d, {{{ True }}} fv (node_val d) {{{ RET node_val (f d); True }}}) ->
  {{{ is_graph γ g s }}} graph_modify_node_code g #(Z.of_nat n) fv {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros Hm Hf. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite /modify_node in Hm.
  destruct (g_nodes s !! n) as [d|] eqn:Hd; [|done].
  injection Hm as <-. simpl. wp_pures.
  wp_apply (Hf d); [done|]. iIntros "_".
  by wp_apply (graph_set_node_spec _ _ _ n (f d) with "Hg"); [by eexists|].
Qed.
Global Opaque graph_modify_node_code.

Definition signal_node_code : val :=
  λ: "z",
    (((#0, "z"), (NONE, #0)), (#0, #false, (#0, #false)),
     (((#false, #false), (#false, (NONE, #false))),
      (((#0, (#0, #0)), NONE), (#0, ((#0, #0), NONE))))).

Definition memo_node_code : val :=
  λ: "m" "q" "b" "e",
    (((#1, #0), (NONE, #3)), (#2, #false, (#0, #false)),
     (((#false, #false), (#false, (NONE, #false))),
      (("b", "e"), ("m", ("q", NONE))))).

Definition effect_node_code : val :=
  λ: "b" "e",
    (((#2, #0), (NONE, #3)), (#0, #false, (#0, #false)),
     (((#false, #false), (#false, (NONE, #false))),
      (("b", "e"), (#1, ((#0, #0), NONE))))).

Lemma signal_node_spec (z : Z) :
  {{{ True }}} signal_node_code #z {{{ RET node_val (signal_node z); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque signal_node_code.

Lemma memo_node_spec (m : scope_mode) (q : comparer) (b : cmd) (e : env) :
  {{{ True }}}
    memo_node_code (scope_mode_val m) (comparer_val q) (compile_cmd b) (env_val e)
  {{{ RET node_val (memo_node m q b e); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque memo_node_code.

Lemma effect_node_spec (b : cmd) (e : env) :
  {{{ True }}}
    effect_node_code (compile_cmd b) (env_val e)
  {{{ RET node_val (effect_node b e); True }}}.
Proof. iIntros (Φ) "_ HΦ". wp_lam. wp_pures. by iApply "HΦ". Qed.
Global Opaque effect_node_code.

Definition node_clear_clean_code : val :=
  λ: "d", node_with_unannounced_code #false (node_with_freshness_code #0 "d").

Definition node_memo_start_code : val :=
  λ: "d",
    let: "runs" := Fst (Snd (Snd (Fst "d"))) + #1 in
    node_with_runs_code "runs"
      (node_with_failure_code NONE
        (node_with_status_code #0
          (node_with_unannounced_code #false
            (node_with_freshness_code #0 "d")))).

Definition node_memo_ok_code : val :=
  λ: "v" "d", node_with_published_code #true (node_with_value_code "v" "d").

Definition node_memo_fail_code : val :=
  λ: "y" "d",
    node_with_violated_code #false
      (node_with_status_code #2
        (node_with_failure_code (SOME "y") "d")).

Definition node_memo_cmp_throws_code : val :=
  λ: "y" "prev" "d",
    node_with_status_code #2
      (node_with_failure_code (SOME "y")
        (node_with_value_code "prev" "d")).

Definition node_effect_dequeue_code : val :=
  λ: "d", node_with_freshness_code #0 (node_with_queued_code #false "d").

Definition node_effect_start_code : val :=
  λ: "d",
    let: "runs" := Fst (Snd (Snd (Fst "d"))) + #1 in
    node_with_runs_code "runs"
      (node_with_failure_code NONE
        (node_with_status_code #0 "d")).

Definition node_effect_fail_code : val :=
  λ: "y" "d", node_with_status_code #2 (node_with_failure_code (SOME "y") "d").

Definition node_dispose_clear_code : val :=
  λ: "d", node_with_link_code NONE (node_with_disposed_code #true "d").

Lemma node_clear_clean_spec (d : node) :
  {{{ True }}}
    node_clear_clean_code (node_val d)
  {{{ RET node_val (node_with_unannounced false (node_with_freshness Clean d)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam.
  wp_apply (node_with_freshness_spec Clean d); [done|]. iIntros "_".
  by wp_apply (node_with_unannounced_spec false); [done|].
Qed.
Global Opaque node_clear_clean_code.

Lemma node_memo_start_spec (d : node) :
  {{{ True }}}
    node_memo_start_code (node_val d)
  {{{ RET node_val (node_with_runs (S (n_runs d)) (node_with_failure None (node_with_status StNone
            (node_with_unannounced false (node_with_freshness Clean d))))); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite (_ : (Z.of_nat (n_runs d) + 1)%Z = Z.of_nat (S (n_runs d))); [|lia].
  wp_apply (node_with_freshness_spec Clean d); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_unannounced_spec false); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_status_spec StNone); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_failure_spec None); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_runs_spec (S (n_runs d))); [done|].
Qed.
Global Opaque node_memo_start_code.

Lemma node_memo_ok_spec (v : Z) (d : node) :
  {{{ True }}}
    node_memo_ok_code #v (node_val d)
  {{{ RET node_val (node_with_published true (node_with_value v d)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply (node_with_value_spec v d); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_published_spec true); [done|].
Qed.
Global Opaque node_memo_ok_code.

Lemma node_memo_fail_spec (y : Z) (d : node) :
  {{{ True }}}
    node_memo_fail_code #y (node_val d)
  {{{ RET node_val (node_with_violated false (node_with_status StError (node_with_failure (Some y) d))); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply (node_with_failure_spec (Some y) d); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_status_spec StError); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_violated_spec false); [done|].
Qed.
Global Opaque node_memo_fail_code.

Lemma node_memo_cmp_throws_spec (y prev : Z) (d : node) :
  {{{ True }}}
    node_memo_cmp_throws_code #y #prev (node_val d)
  {{{ RET node_val (node_with_status StError (node_with_failure (Some y) (node_with_value prev d))); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply (node_with_value_spec prev d); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_failure_spec (Some y)); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_status_spec StError); [done|].
Qed.
Global Opaque node_memo_cmp_throws_code.

Lemma node_effect_dequeue_spec (d : node) :
  {{{ True }}}
    node_effect_dequeue_code (node_val d)
  {{{ RET node_val (node_with_freshness Clean (node_with_queued false d)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam.
  wp_apply (node_with_queued_spec false d); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_freshness_spec Clean); [done|].
Qed.
Global Opaque node_effect_dequeue_code.

Lemma node_effect_start_spec (d : node) :
  {{{ True }}}
    node_effect_start_code (node_val d)
  {{{ RET node_val (node_with_runs (S (n_runs d)) (node_with_failure None (node_with_status StNone d))); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  rewrite (_ : (Z.of_nat (n_runs d) + 1)%Z = Z.of_nat (S (n_runs d))); [|lia].
  wp_apply (node_with_status_spec StNone d); [done|]. iIntros "_". wp_pures.
  wp_apply (node_with_failure_spec None); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_runs_spec (S (n_runs d))); [done|].
Qed.
Global Opaque node_effect_start_code.

Lemma node_effect_fail_spec (y : Z) (d : node) :
  {{{ True }}}
    node_effect_fail_code #y (node_val d)
  {{{ RET node_val (node_with_status StError (node_with_failure (Some y) d)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam. wp_pures.
  wp_apply (node_with_failure_spec (Some y) d); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_status_spec StError); [done|].
Qed.
Global Opaque node_effect_fail_code.

Lemma node_dispose_clear_spec (d : node) :
  {{{ True }}}
    node_dispose_clear_code (node_val d)
  {{{ RET node_val (node_with_link None (node_with_disposed true d)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam.
  wp_apply (node_with_disposed_spec true d); [done|]. iIntros "_". wp_pures.
  by wp_apply (node_with_link_spec None); [done|].
Qed.
Global Opaque node_dispose_clear_code.

(** ** Mutual specification record for the 26 engine operations *)

Record engine_spec (go : val) (f : nat) : Prop := {
  es_exec : ∀ γ g e c s s' r,
    exec f e c s = Some (s', r) ->
    {{{ is_graph γ g s }}} go #0 g (env_val e, compile_cmd c)%V {{{ RET outcome_val r; is_graph γ g s' }}};
  es_read : ∀ γ g n tr s s' r,
    read f n tr s = Some (s', r) ->
    {{{ is_graph γ g s }}} go #1 g (#(Z.of_nat n), #tr)%V {{{ RET outcome_val r; is_graph γ g s' }}};
  es_read_node : ∀ γ g n s s' r,
    read_node f n s = Some (s', r) ->
    {{{ is_graph γ g s }}} go #2 g #(Z.of_nat n) {{{ RET outcome_val r; is_graph γ g s' }}};
  es_update_if_necessary : ∀ γ g n s s',
    update_if_necessary f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #3 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_refresh_or_pull : ∀ γ g n s s' d,
    refresh_or_pull f n s = Some (s', d) ->
    {{{ is_graph γ g s }}} go #4 g #(Z.of_nat n) {{{ RET node_val d; is_graph γ g s' }}};
  es_pull : ∀ γ g n s s' d,
    pull f n s = Some (s', d) ->
    {{{ is_graph γ g s }}} go #5 g #(Z.of_nat n) {{{ RET node_val d; is_graph γ g s' }}};
  es_refresh : ∀ γ g n s s',
    refresh f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #6 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_resolve_walk : ∀ γ g n i s s',
    resolve_walk f n i s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #7 g (#(Z.of_nat n), #(Z.of_nat i))%V {{{ RET #(); is_graph γ g s' }}};
  es_recompute : ∀ γ g n s s',
    recompute f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #8 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_run_memo : ∀ γ g n s s',
    run_memo f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #9 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_execute_effect : ∀ γ g n s s',
    execute_effect f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #10 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_run_effect : ∀ γ g n s s',
    run_effect f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #11 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_run_effect_body : ∀ γ g n s s',
    run_effect_body f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #12 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_discharge : ∀ γ g o s s',
    discharge f o s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #13 g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}};
  es_retire : ∀ γ g o s s',
    retire f o s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #14 g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}};
  es_dispose_scope : ∀ γ g o s s',
    dispose_scope f o s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #15 g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}};
  es_teardown : ∀ γ g o ls cs s s',
    teardown f o ls cs s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #16 g (#(Z.of_nat o), (links_val ls, cleanups_val cs))%V {{{ RET #(); is_graph γ g s' }}};
  es_run_cleanup : ∀ γ g o b e s s',
    run_cleanup f o b e s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #17 g (#(Z.of_nat o), (compile_cmd b, env_val e))%V {{{ RET #(); is_graph γ g s' }}};
  es_release : ∀ γ g n s s',
    release f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #18 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_dispose_node : ∀ γ g n s s',
    dispose_node f n s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #19 g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}};
  es_attach : ∀ γ g n o s s',
    attach f n o s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #20 g (#(Z.of_nat n), #(Z.of_nat o))%V {{{ RET #(); is_graph γ g s' }}};
  es_write_signal : ∀ γ g n v s s',
    write_signal f n v s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #21 g (#(Z.of_nat n), #v)%V {{{ RET #(); is_graph γ g s' }}};
  es_request_flush : ∀ γ g s s',
    request_flush f s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #22 g #() {{{ RET #(); is_graph γ g s' }}};
  es_settle_flush : ∀ γ g s s',
    settle_flush f s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #23 g #() {{{ RET #(); is_graph γ g s' }}};
  es_run_flush : ∀ γ g s s',
    run_flush f s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #24 g #() {{{ RET #(); is_graph γ g s' }}};
  es_drain : ∀ γ g s s',
    drain f s = Some (s', tt) ->
    {{{ is_graph γ g s }}} go #25 g #() {{{ RET #(); is_graph γ g s' }}}
}.

(** ** Step implementations and specifications (#1..#8, #10..#25) *)

Local Opaque is_graph.
Local Arguments m_bind {_ _} _ _ _ : simpl never.
Local Arguments m_return {_} _ _ : simpl never.
Local Arguments m_get _ : simpl never.
Local Arguments m_modify _ _ : simpl never.
Local Arguments m_step _ _ : simpl never.
Local Arguments get_node _ _ : simpl never.
Local Arguments modify_node _ _ _ : simpl never.
Local Arguments get_owner _ _ : simpl never.
Local Arguments put_owner _ _ _ : simpl never.
Local Arguments edges _ _ : simpl never.
Local Arguments edges_opt _ _ : simpl never.
Local Arguments push_running _ _ : simpl never.
Local Arguments pop_running _ : simpl never.
Local Arguments push_checking _ _ : simpl never.
Local Arguments pop_checking _ : simpl never.
Local Arguments emit _ _ : simpl never.
Local Arguments exec !fuel _ _ _ /.
Local Arguments read !fuel _ _ _ /.
Local Arguments read_node !fuel _ _ /.
Local Arguments update_if_necessary !fuel _ _ /.
Local Arguments refresh_or_pull !fuel _ _ /.
Local Arguments pull !fuel _ _ /.
Local Arguments refresh !fuel _ _ /.
Local Arguments resolve_walk !fuel _ _ _ /.
Local Arguments recompute !fuel _ _ /.
Local Arguments run_memo !fuel _ _ /.
Local Arguments execute_effect !fuel _ _ /.
Local Arguments run_effect !fuel _ _ /.
Local Arguments run_effect_body !fuel _ _ /.
Local Arguments discharge !fuel _ _ /.
Local Arguments retire !fuel _ _ /.
Local Arguments dispose_scope !fuel _ _ /.
Local Arguments teardown !fuel _ _ _ _ /.
Local Arguments run_cleanup !fuel _ _ _ _ /.
Local Arguments release !fuel _ _ /.
Local Arguments dispose_node !fuel _ _ /.
Local Arguments attach !fuel _ _ _ /.
Local Arguments write_signal !fuel _ _ _ /.
Local Arguments request_flush !fuel _ /.
Local Arguments settle_flush !fuel _ /.
Local Arguments run_flush !fuel _ /.
Local Arguments drain !fuel _ /.

Definition read_step : val :=
  λ: "go" "g" "arg",
    let: "n" := Fst "arg" in
    let: "tr" := Snd "arg" in
    if: "tr" then "go" #2 "g" "n"
    else untracked_code "g" (λ: <>, "go" #2 "g" "n").

Lemma read_step_spec go f (Hes : engine_spec go f) γ g n tr s s' r :
  read (S f) n tr s = Some (s', r) ->
  {{{ is_graph γ g s }}} read_step go g (#(Z.of_nat n), #tr)%V {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. destruct tr; wp_pures.
  - by wp_apply (es_read_node _ _ Hes _ _ _ _ _ _ H with "Hg").
  - wp_apply (untracked_spec outcome_val _ _ (read_node f n) _ _ _ _ H with "Hg"); [|done].
    intros s1 s2 r0 Hr0. iIntros (Ψ) "Hg HΨ". wp_lam.
    by wp_apply (es_read_node _ _ Hes _ _ _ _ _ _ Hr0 with "Hg").
Qed.
Global Opaque read_step.

Definition read_node_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => InjR #0
    | SOME "d" =>
        let: "k" := Fst (Fst (Fst (Fst "d"))) in
        if: "k" = #0 then
          track_code "g" "n";;
          InjL (Snd (Fst (Fst (Fst "d"))))
        else
          let: "d'" :=
            if: node_stale_live_code "d" then
              let: "d1" := "go" #4 "g" "n" in
              track_code "g" "n";;
              (match: graph_get_node_code "g" "n" with
                 NONE => #()
               | SOME "d2" => if: node_stale_live_code "d2" then mark_reader_code "g" else #()
               end);;
              "d1"
            else
              track_code "g" "n";;
              match: graph_get_node_code "g" "n" with
                NONE => "d"
              | SOME "d2" => "d2"
              end in
          let: "st" := Snd (Snd (Fst (Fst "d'"))) in
          if: "st" = #2 then
            match: Fst (Snd (Fst (Fst "d'"))) with
              NONE => InjR #0
            | SOME "f0" => InjR "f0"
            end
          else InjL (Snd (Fst (Fst (Fst "d'"))))
    end.

Lemma read_node_step_spec go f (Hes : engine_spec go f) γ g n s s' r :
  read_node (S f) n s = Some (s', r) ->
  {{{ is_graph γ g s }}} read_node_step go g #(Z.of_nat n) {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & d & Hd & H).
  apply get_node_Some in Hd as (Hd & Hndom & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_kind d) eqn:Hk; [| |done].
  - wp_pures. apply m_bind_Some in H as (s2 & [] & Htr & H).
    apply m_return_Some in H as [-> ->].
    wp_apply (track_spec _ _ n s s2 Hndom Htr with "Hg"). iIntros "Hg".
    wp_pures. by iApply "HΦ".
  - wp_pures. apply m_bind_Some in H as (s2 & d' & Hd' & H).
    wp_apply (node_stale_live_spec d); [done|]. iIntros "_".
    wp_bind (If #(stale_live d) _ _).
    iApply (wp_wand _ _ _ (fun v => ⌜v = node_val d'⌝ ∗ is_graph γ g s2)%I with "[Hg]").
    { destruct (stale_live d); wp_pures.
      - apply m_bind_Some in Hd' as (s11 & d1 & Hrpull & Hd').
        apply m_bind_Some in Hd' as (s12 & [] & Htr_get & Hret).
        apply m_return_Some in Hret as [-> ->].
        apply m_bind_Some in Htr_get as (s11' & [] & Htr & Hget).
        apply m_bind_Some in Hget as (s11'' & d2 & Hd2 & Hmr).
        apply get_node_Some in Hd2 as (Hd2 & Hndom' & ->).
        assert (Hndom11 : n ∈ dom (g_nodes s11)).
        { rewrite -(track_dom_nodes _ _ _ _ Htr). exact Hndom'. }
        wp_apply (es_refresh_or_pull _ _ Hes _ _ _ _ _ _ Hrpull with "Hg"). iIntros "Hg". wp_pures.
        wp_apply (track_spec _ _ n s11 s11' Hndom11 Htr with "Hg"). iIntros "Hg". wp_pures.
        wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
        rewrite Hd2 /=. wp_pures.
        wp_apply (node_stale_live_spec d2); [done|]. iIntros "_".
        destruct (stale_live d2); wp_pures.
        + wp_apply (mark_reader_spec _ _ f _ _ Hmr with "Hg"). iIntros "Hg". wp_pures. by iFrame.
        + apply m_return_Some in Hmr as [-> _]. by iFrame.
      - apply m_bind_Some in Hd' as (s11 & [] & Htr & Hd').
        apply get_node_Some in Hd' as (Hd2 & _ & ->).
        wp_apply (track_spec _ _ n s s11 Hndom Htr with "Hg"). iIntros "Hg". wp_pures.
        wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
        rewrite Hd2 /=. wp_pures. by iFrame. }
    iIntros (v) "[-> Hg]". wp_pures.
    destruct (n_status d') eqn:Hst; apply m_return_Some in H as [-> ->]; simpl; wp_pures;
      try (by iApply "HΦ").
    destruct (n_failure d'); simpl; wp_pures; by iApply "HΦ".
Qed.
Global Opaque read_node_step.

Definition update_if_necessary_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        if: Fst (Fst (Fst (Fst "d"))) = #1 then
          if: node_stale_live_code "d" then "go" #4 "g" "n";; #() else #()
        else #()
    end.

Lemma update_if_necessary_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  update_if_necessary (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} update_if_necessary_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & d & Hd & H).
  apply get_node_Some in Hd as (Hd & _ & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_kind d); wp_pures.
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - wp_apply (node_stale_live_spec d); [done|]. iIntros "_".
    destruct (stale_live d); wp_pures.
    + apply m_bind_Some in H as (s2 & d' & Hrpull & H).
      apply m_return_Some in H as [-> _].
      wp_apply (es_refresh_or_pull _ _ Hes _ _ _ _ _ _ Hrpull with "Hg"). iIntros "Hg". wp_pures.
      by iApply "HΦ".
    + apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque update_if_necessary_step.

Definition refresh_or_pull_step : val :=
  λ: "go" "g" "n",
    if: deferring_code "g" then
      "go" #6 "g" "n";;
      match: graph_get_node_code "g" "n" with
        NONE => #()
      | SOME "d" => "d"
      end
    else "go" #5 "g" "n".

Lemma refresh_or_pull_step_spec go f (Hes : engine_spec go f) γ g n s s' d :
  refresh_or_pull (S f) n s = Some (s', d) ->
  {{{ is_graph γ g s }}} refresh_or_pull_step go g #(Z.of_nat n) {{{ RET node_val d; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H).
  apply m_get_Some in Hget as [-> ->].
  wp_apply (deferring_spec with "Hg"). iIntros "Hg".
  destruct (deferring s); wp_pures.
  - apply m_bind_Some in H as (s2 & [] & Href & H).
    apply get_node_Some in H as (Hd & _ & ->).
    wp_apply (es_refresh _ _ Hes _ _ _ _ _ Href with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hd /=. wp_pures. by iApply "HΦ".
  - by wp_apply (es_pull _ _ Hes _ _ _ _ _ _ H with "Hg").
Qed.
Global Opaque refresh_or_pull_step.

Definition pull_step : val :=
  λ: "go" "g" "n",
    let: "c0" := graph_get_ctrl_code "g" in
    let: "pd0" := Snd (Snd (Fst (Fst "c0"))) in
    graph_with_pull_depth_code "g" ("pd0" + #1);;
    "go" #6 "g" "n";;
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        let: "c1" := graph_get_ctrl_code "g" in
        let: "pd1" := Snd (Snd (Fst (Fst "c1"))) in
        let: "pd1'" := if: #0 < "pd1" then "pd1" - #1 else #0 in
        graph_with_pull_depth_code "g" "pd1'";;
        let: "c2" := graph_get_ctrl_code "g" in
        let: "pd2" := Snd (Snd (Fst (Fst "c2"))) in
        let: "fo2" := Snd (Fst (Fst (Fst "c2"))) in
        (if: ("pd2" = #0) && "fo2" then "go" #23 "g" #() else #());;
        "d"
    end.

Lemma pull_step_spec go f (Hes : engine_spec go f) γ g n s s' d :
  pull (S f) n s = Some (s', d) ->
  {{{ is_graph γ g s }}} pull_step go g #(Z.of_nat n) {{{ RET node_val d; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & [] & H1 & H). apply m_modify_Some in H1 as [-> _].
  apply m_bind_Some in H as (s2 & [] & Href & H).
  apply m_bind_Some in H as (s3 & d0 & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  apply m_bind_Some in H as (s4 & [] & H4 & H). apply m_modify_Some in H4 as [-> _].
  apply m_bind_Some in H as (s5 & s4' & Hget & H). apply m_get_Some in Hget as [-> ->].
  apply m_bind_Some in H as (s6 & [] & Hsf & Hret). apply m_return_Some in Hret as [-> ->].
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  rewrite (_ : (Z.of_nat (g_pull_depth s) + 1)%Z = Z.of_nat (S (g_pull_depth s))); [|lia].
  wp_apply (graph_with_pull_depth_spec _ _ _ (S (g_pull_depth s)) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (es_refresh _ _ Hes _ _ _ _ _ Href with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_bind (If _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (pred (g_pull_depth s2)))⌝)%I).
  { destruct (decide (0 < Z.of_nat (g_pull_depth s2))%Z) as [Hlt|Hge].
    - rewrite (bool_decide_eq_true_2 (0 < Z.of_nat (g_pull_depth s2))%Z) //; wp_pures; iPureIntro; do 2 f_equal; lia.
    - rewrite (bool_decide_eq_false_2 (0 < Z.of_nat (g_pull_depth s2))%Z) //; wp_pures; iPureIntro; do 2 f_equal; lia. }
  iIntros (v) "->". wp_pures.
  wp_apply (graph_with_pull_depth_spec _ _ _ (pred (g_pull_depth s2)) with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  change (g_pull_depth (with_pull_depth (pred (g_pull_depth s2)) s2)) with (pred (g_pull_depth s2)) in Hsf.
  change (g_flush_owed (with_pull_depth (pred (g_pull_depth s2)) s2)) with (g_flush_owed s2) in Hsf.
  destruct (decide (pred (g_pull_depth s2) = 0%nat)) as [Heq|Hne].
  - rewrite (bool_decide_eq_true_2 (#(Z.of_nat (pred (g_pull_depth s2))) = #0)%V); [|by rewrite Heq].
    rewrite (bool_decide_eq_true_2 (pred (g_pull_depth s2) = 0%nat)) // in Hsf.
    destruct (g_flush_owed s2); wp_pures.
    + wp_apply (es_settle_flush _ _ Hes _ _ _ _ Hsf with "Hg"). iIntros "Hg". wp_pures. by iApply "HΦ".
    + apply m_return_Some in Hsf as [-> _]. by iApply "HΦ".
  - rewrite (bool_decide_eq_false_2 (#(Z.of_nat (pred (g_pull_depth s2))) = #0)%V); [|intros [= ?]; lia].
    rewrite (bool_decide_eq_false_2 (pred (g_pull_depth s2) = 0%nat)) // in Hsf.
    wp_pures. apply m_return_Some in Hsf as [-> _]. by iApply "HΦ".
Qed.
Global Opaque pull_step.

Definition refresh_step : val :=
  λ: "go" "g" "n",
    let: "c0" := graph_get_ctrl_code "g" in
    let: "run0" := Fst (Fst (Snd "c0")) in
    let: "chk0" := Snd (Fst (Snd "c0")) in
    if: vlist_mem_nat_code "n" "chk0" then mark_dirty_code "g" "n"
    else if: vlist_mem_nat_code "n" "run0" then #()
    else
      match: graph_get_node_code "g" "n" with
        NONE => #()
      | SOME "d" =>
          (if: Fst (Fst (Snd (Fst "d"))) = #1 then
             push_checking_code "g" "n";;
             "go" #7 "g" ("n", #0);;
             pop_checking_code "g";;
             match: graph_get_node_code "g" "n" with
               NONE => #()
             | SOME "d'" =>
                 if: Fst (Fst (Snd (Fst "d'"))) = #1 then
                   graph_modify_node_code "g" "n" node_clear_clean_code
                 else #()
             end
           else #());;
          match: graph_get_node_code "g" "n" with
            NONE => #()
          | SOME "d''" =>
              if: Fst (Fst (Snd (Fst "d''"))) = #2 then "go" #8 "g" "n" else #()
          end
      end.

Lemma refresh_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  refresh (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} refresh_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H).
  apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply vlist_mem_nat_spec; [done|]. iIntros "_".
  destruct (decide (n ∈ g_checking s)) as [Hchk|Hchk].
  - rewrite (bool_decide_eq_true_2 (n ∈ g_checking s)) //. wp_pures.
    apply m_step_Some in H as [Hmd _].
    by wp_apply (mark_dirty_spec _ _ f n s s' Hmd with "Hg").
  - rewrite (bool_decide_eq_false_2 (n ∈ g_checking s)) //. wp_pures.
    wp_apply vlist_mem_nat_spec; [done|]. iIntros "_".
    destruct (decide (n ∈ g_running s)) as [Hrun|Hrun].
    + rewrite (bool_decide_eq_true_2 (n ∈ g_running s)) //. wp_pures.
      apply m_return_Some in H as [-> _]. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (n ∈ g_running s)) //. wp_pures.
      apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
      apply m_bind_Some in H as (s2 & [] & Hchk_step & H).
      apply m_bind_Some in H as (s3 & d'' & Hd'' & H). apply get_node_Some in Hd'' as (Hd'' & _ & ->).
      wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
      rewrite Hd /=. wp_pures.
      wp_bind (If _ _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s2)%I with "[Hg]").
      { destruct (n_freshness d) eqn:Hfr; simpl in Hchk_step |- *; wp_pures.
        - apply m_return_Some in Hchk_step as [-> _]. by iFrame.
        - apply m_bind_Some in Hchk_step as (s11 & [] & Hpush & Hchk_step).
          apply push_checking_Some in Hpush as [-> _].
          apply m_bind_Some in Hchk_step as (s12 & [] & Hwalk & Hchk_step).
          apply m_bind_Some in Hchk_step as (s13 & [] & Hpop & Hchk_step).
          apply pop_checking_Some in Hpop as [-> _].
          apply m_bind_Some in Hchk_step as (s14 & d' & Hd' & Hchk_step).
          apply get_node_Some in Hd' as (Hd' & _ & ->).
          wp_apply (push_checking_spec with "Hg"). iIntros "Hg". wp_pures.
          change #0 with #(Z.of_nat 0).
          wp_apply (es_resolve_walk _ _ Hes _ _ n 0 _ _ Hwalk with "Hg"). iIntros "Hg". wp_pures.
          wp_apply (pop_checking_spec with "Hg"). iIntros "Hg". wp_pures.
          wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
          rewrite Hd' /=. wp_pures.
          destruct (n_freshness d') eqn:Hfr'; simpl in Hchk_step |- *; wp_pures.
          + apply m_return_Some in Hchk_step as [-> _]. by iFrame.
          + apply m_bind_Some in Hchk_step as (s15 & [] & Hgr & Hmod).
            apply m_modify_Some in Hgr as [-> _].
            iMod (ghost_resolved_step _ _ n with "Hg") as "Hg".
            wp_apply (graph_modify_node_spec _ _ n _ node_clear_clean_code _ _ Hmod with "Hg").
            { intros d0. apply node_clear_clean_spec. }
            iIntros "Hg". by iFrame.
          + apply m_return_Some in Hchk_step as [-> _]. by iFrame.
        - apply m_return_Some in Hchk_step as [-> _]. by iFrame. }
      iIntros (v) "[-> Hg]". wp_pures.
      wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
      rewrite Hd'' /=. wp_pures.
      destruct (n_freshness d'') eqn:Hfr''; simpl in H |- *; wp_pures.
      * apply m_return_Some in H as [-> _]. by iApply "HΦ".
      * apply m_return_Some in H as [-> _]. by iApply "HΦ".
      * by wp_apply (es_recompute _ _ Hes _ _ _ _ _ H with "Hg").
Qed.
Global Opaque refresh_step.

Definition resolve_walk_step : val :=
  λ: "go" "g" "arg",
    let: "n" := Fst "arg" in
    let: "i" := Snd "arg" in
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        if: Fst (Fst (Snd (Fst "d"))) = #1 then
          match: graph_slot_src_code "g" "n" "i" with
            NONE =>
              if: graph_slot_holds_stale_live_code "g" "n" then "go" #7 "g" ("n", #0) else #()
          | SOME "src" =>
              "go" #3 "g" "src";;
              match: graph_get_node_code "g" "n" with
                NONE => #()
              | SOME "d'" =>
                  match: graph_get_node_code "g" "src" with
                    NONE => #()
                  | SOME "d_src" =>
                      let: "c1" := graph_get_ctrl_code "g" in
                      let: "run1" := Fst (Fst (Snd "c1")) in
                      let: "chk1" := Snd (Fst (Snd "c1")) in
                      let: "in_rc" :=
                        if: vlist_mem_nat_code "src" "run1" then #true
                        else vlist_mem_nat_code "src" "chk1" in
                      let: "hit" :=
                        if: Fst (Fst (Snd (Fst "d'"))) = #1 then
                          if: node_stale_live_code "d_src" then "in_rc" else #false
                        else #false in
                      (if: "hit" then mark_dirty_code "g" "n" else #());;
                      "go" #7 "g" ("n", "i" + #1)
                  end
              end
          end
        else #()
    end.

Lemma resolve_walk_step_spec go f (Hes : engine_spec go f) γ g n i s s' :
  resolve_walk (S f) n i s = Some (s', tt) ->
  {{{ is_graph γ g s }}} resolve_walk_step go g (#(Z.of_nat n), #(Z.of_nat i))%V {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  cbn [resolve_walk] in H. apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  apply m_bind_Some in H as (s2 & s0 & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_freshness d) eqn:Hfr; simpl in H |- *; wp_pures.
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - wp_apply (graph_slot_src_spec with "Hg"). iIntros "Hg".
    rewrite /slot_at.
    destruct (source_slots (sources_of (g_edges s) n) !! i) as [[src mh]|] eqn:Hslot; simpl; wp_pures.
    + apply m_bind_Some in H as (s1 & [] & Hupd & H).
      apply m_bind_Some in H as (s2 & d' & Hd' & H). apply get_node_Some in Hd' as (Hd' & _ & ->).
      apply m_bind_Some in H as (s3 & d_src & Hd_src & H). apply get_node_Some in Hd_src as (Hd_src & _ & ->).
      apply m_bind_Some in H as (s4 & s1' & Hget1 & H). apply m_get_Some in Hget1 as [-> ->].
      apply m_bind_Some in H as (s5 & [] & Hmd_opt & Hwalk).
      wp_apply (es_update_if_necessary _ _ Hes _ _ src _ _ Hupd with "Hg"). iIntros "Hg". wp_pures.
      wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
      rewrite Hd' /=. wp_pures.
      wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
      rewrite Hd_src /=. wp_pures.
      wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
      set (in_rc := bool_decide (src ∈ g_running s1 \/ src ∈ g_checking s1)).
      wp_bind (If _ _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #in_rc⌝)%I).
      { wp_apply vlist_mem_nat_spec; [done|]. iIntros "_".
        destruct (decide (src ∈ g_running s1)) as [Hr|Hr].
        - rewrite (bool_decide_eq_true_2 (src ∈ g_running s1)) //. wp_pures.
          iPureIntro. subst in_rc. rewrite bool_decide_eq_true_2; [done|by left].
        - rewrite (bool_decide_eq_false_2 (src ∈ g_running s1)) //. wp_pures.
          wp_apply vlist_mem_nat_spec; [done|]. iIntros "_". iPureIntro. subst in_rc.
          f_equal. f_equal. apply bool_decide_ext. tauto. }
      iIntros (v) "->". wp_pures.
      set (hit := bool_decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s1 \/ src ∈ g_checking s1))).
      wp_bind (If _ _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #hit⌝)%I).
      { destruct (n_freshness d') eqn:Hfr'; simpl; wp_pures.
        - iPureIntro. subst hit. rewrite bool_decide_eq_false_2; [done|by intros [? _]].
        - wp_apply (node_stale_live_spec d_src); [done|]. iIntros "_".
          destruct (stale_live d_src) eqn:Hsl; wp_pures.
          + iPureIntro. subst hit in_rc. f_equal. f_equal. apply bool_decide_ext. tauto.
          + iPureIntro. subst hit. rewrite bool_decide_eq_false_2; [done|by intros [_ [? _]]].
        - iPureIntro. subst hit. rewrite bool_decide_eq_false_2; [done|by intros [? _]]. }
      iIntros (v) "->". wp_pures.
      wp_bind (If #hit _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s5)%I with "[Hg]").
      { subst hit.
        destruct (decide (n_freshness d' = Check /\ stale_live d_src = true /\ (src ∈ g_running s1 \/ src ∈ g_checking s1))) as [Hhit|Hhit].
        - rewrite (bool_decide_eq_true_2 (n_freshness d' = Check /\ _)) //; wp_pures.
          apply m_step_Some in Hmd_opt as [Hmd _].
          wp_apply (mark_dirty_spec _ _ f n _ _ Hmd with "Hg"). iIntros "Hg". by iFrame.
        - rewrite (bool_decide_eq_false_2 (n_freshness d' = Check /\ _)) //; wp_pures.
          apply m_return_Some in Hmd_opt as [-> _]. by iFrame. }
      iIntros (v) "[-> Hg]". wp_pures.
      rewrite (_ : (Z.of_nat i + 1)%Z = Z.of_nat (S i)); [|lia].
      by wp_apply (es_resolve_walk _ _ Hes _ _ n (S i) _ _ Hwalk with "Hg").
    + wp_apply (graph_slot_holds_stale_live_spec with "Hg"). iIntros "Hg".
      destruct (slot_holds stale_live s n); wp_pures.
      * change #0 with #(Z.of_nat 0).
        by wp_apply (es_resolve_walk _ _ Hes _ _ n 0 _ _ H with "Hg").
      * apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque resolve_walk_step.

Definition recompute_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        let: "sc" := Fst (Snd (Snd (Fst (Snd "d")))) in
        match: "sc" with
          NONE => "go" #9 "g" "n"
        | SOME "o" =>
            graph_modify_node_code "g" "n" (λ: "d0", node_with_freshness_code #0 "d0");;
            "go" #13 "g" "o";;
            match: graph_get_node_code "g" "n" with
              NONE => #()
            | SOME "d'" =>
                if: Fst (Fst (Fst (Snd "d'"))) then
                  if: Fst (Fst (Snd (Fst "d'"))) = #0 then
                    graph_modify_node_code "g" "n" (λ: "d0", node_with_unannounced_code #false "d0")
                  else #()
                else if: Fst (Snd (Snd (Fst "d'"))) = Fst (Snd (Snd (Fst "d"))) then
                  "go" #9 "g" "n"
                else if: Fst (Fst (Snd (Fst "d'"))) = #2 then
                  "go" #8 "g" "n"
                else #()
            end
        end
    end.

Lemma recompute_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  recompute (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} recompute_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_scope d) as [o|] eqn:Hsc; simpl; wp_pures.
  - apply m_bind_Some in H as (s1 & [] & Hmod & H).
    apply m_bind_Some in H as (s2 & [] & Hgc & H). apply m_modify_Some in Hgc as [-> _].
    apply m_bind_Some in H as (s3 & [] & Hdis & H).
    apply m_bind_Some in H as (s4 & d' & Hd' & H). apply get_node_Some in Hd' as (Hd' & _ & ->).
    wp_apply (graph_modify_node_spec _ _ n (node_with_freshness Clean) _ _ _ Hmod with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_freshness_spec Clean d0); [done|]. }
    iIntros "Hg". wp_pures.
    iMod (ghost_dirty_cleared_step _ _ n (n_runs d) with "Hg") as "Hg".
    wp_apply (es_discharge _ _ Hes _ _ o _ _ Hdis with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hd' /=. wp_pures.
    destruct (n_disposed d') eqn:Hdisp; wp_pures.
    + destruct (n_freshness d') eqn:Hfr'; simpl in H |- *; wp_pures.
      * wp_apply (graph_modify_node_spec _ _ n (node_with_unannounced false) _ _ _ H with "Hg").
        { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_unannounced_spec false d0); [done|]. }
        iIntros "Hg". iModIntro. by iApply "HΦ".
      * apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
      * apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
    + destruct (decide (n_runs d' = n_runs d)) as [Hruns|Hruns].
      * rewrite Hruns (bool_decide_eq_true_2 (#(Z.of_nat (n_runs d)) = #(Z.of_nat (n_runs d)))%V) //.
        wp_pures. wp_apply (es_run_memo _ _ Hes _ _ n _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
      * rewrite (bool_decide_eq_false_2 (#(Z.of_nat (n_runs d')) = #(Z.of_nat (n_runs d)))%V); [|intros [= ?]; lia].
        wp_pures. destruct (n_freshness d') eqn:Hfr'; simpl in H |- *; wp_pures.
        -- apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
        -- apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
        -- wp_apply (es_recompute _ _ Hes _ _ n _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
  - wp_apply (es_run_memo _ _ Hes _ _ n _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
Qed.
Global Opaque recompute_step.

Definition execute_effect_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        if: Fst (Fst (Fst (Snd "d"))) then
          graph_modify_node_code "g" "n" (λ: "d0", node_with_queued_code #false "d0")
        else
          (if: Fst (Fst (Snd (Fst "d"))) = #1 then
             "go" #7 "g" ("n", #0);;
             match: graph_get_node_code "g" "n" with
               NONE => #()
             | SOME "d'" =>
                 if: Fst (Fst (Snd (Fst "d'"))) = #1 then
                   graph_modify_node_code "g" "n" (λ: "d0", node_with_freshness_code #0 "d0")
                 else #()
             end
           else #());;
          match: graph_get_node_code "g" "n" with
            NONE => #()
          | SOME "d''" =>
              graph_modify_node_code "g" "n" node_effect_dequeue_code;;
              if: Fst (Fst (Snd (Fst "d''"))) = #2 then "go" #11 "g" "n" else #()
          end
    end.

Lemma execute_effect_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  execute_effect (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} execute_effect_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_disposed d) eqn:Hdisp; wp_pures.
  - wp_apply (graph_modify_node_spec _ _ n (node_with_queued false) _ _ _ H with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_queued_spec false d0); [done|]. }
    by iIntros "Hg"; iApply "HΦ".
  - apply m_bind_Some in H as (s2 & [] & Hchk_step & H).
    apply m_bind_Some in H as (s3 & d'' & Hd'' & H). apply get_node_Some in Hd'' as (Hd'' & _ & ->).
    apply m_bind_Some in H as (s4 & [] & Hdeq & H).
    wp_bind (If _ _ _).
    iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s2)%I with "[Hg]").
    { destruct (n_freshness d) eqn:Hfr; simpl in Hchk_step |- *; wp_pures.
      - apply m_return_Some in Hchk_step as [-> _]. by iFrame.
      - apply m_bind_Some in Hchk_step as (s11 & [] & Hwalk & Hchk_step).
        apply m_bind_Some in Hchk_step as (s12 & d' & Hd' & Hchk_step).
        apply get_node_Some in Hd' as (Hd' & _ & ->).
        change #0 with #(Z.of_nat 0).
        wp_apply (es_resolve_walk _ _ Hes _ _ n 0 _ _ Hwalk with "Hg"). iIntros "Hg". wp_pures.
        wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
        rewrite Hd' /=. wp_pures.
        destruct (n_freshness d') eqn:Hfr'; simpl in Hchk_step |- *; wp_pures.
        + apply m_return_Some in Hchk_step as [-> _]. by iFrame.
        + apply m_bind_Some in Hchk_step as (s13 & [] & Hgr & Hmod).
          apply m_modify_Some in Hgr as [-> _].
          iMod (ghost_resolved_step _ _ n with "Hg") as "Hg".
          wp_apply (graph_modify_node_spec _ _ n (node_with_freshness Clean) _ _ _ Hmod with "Hg").
          { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_freshness_spec Clean d0); [done|]. }
          iIntros "Hg". by iFrame.
        + apply m_return_Some in Hchk_step as [-> _]. by iFrame.
      - apply m_return_Some in Hchk_step as [-> _]. by iFrame. }
    iIntros (v) "[-> Hg]". wp_pures.
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hd'' /=. wp_pures.
    wp_apply (graph_modify_node_spec _ _ n _ node_effect_dequeue_code _ _ Hdeq with "Hg").
    { intros d0. apply node_effect_dequeue_spec. }
    iIntros "Hg". wp_pures.
    destruct (n_freshness d'') eqn:Hfr''; simpl in H |- *; wp_pures.
    + apply m_return_Some in H as [-> _]. by iApply "HΦ".
    + apply m_return_Some in H as [-> _]. by iApply "HΦ".
    + by wp_apply (es_run_effect _ _ Hes _ _ n _ _ H with "Hg").
Qed.
Global Opaque execute_effect_step.

Definition run_effect_step : val :=
  λ: "go" "g" "n",
    push_running_code "g" "n";;
    graph_sources_begin_run_code "g" "n";;
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        (match: Fst (Snd (Snd (Fst (Snd "d")))) with
           NONE => #()
         | SOME "o" =>
             graph_modify_node_code "g" "n" (λ: "d0", node_with_in_cleanup_code #true "d0");;
             "go" #13 "g" "o";;
             graph_modify_node_code "g" "n" (λ: "d0", node_with_in_cleanup_code #false "d0")
         end);;
        (match: graph_get_node_code "g" "n" with
           NONE => #()
         | SOME "d'" =>
             if: Fst (Fst (Fst (Snd "d'"))) then #() else "go" #12 "g" "n"
         end);;
        pop_running_code "g"
    end.

Lemma run_effect_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  run_effect (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} run_effect_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & [] & Hpush & H). apply push_running_Some in Hpush as [-> _].
  apply m_bind_Some in H as (s2 & [] & Hbeg & H). apply edges_Some in Hbeg as [-> _].
  apply m_bind_Some in H as (s3 & [] & Hges & H). apply m_modify_Some in Hges as [-> _].
  apply m_bind_Some in H as (s4 & d & Hd & H). apply get_node_Some in Hd as (Hd & Hndom & ->).
  apply m_bind_Some in H as (s5 & [] & Hsc_step & H).
  apply m_bind_Some in H as (s6 & d' & Hd' & H). apply get_node_Some in Hd' as (Hd' & _ & ->).
  apply m_bind_Some in H as (s7 & [] & Hbody & Hpop). apply pop_running_Some in Hpop as [-> _].
  wp_apply (push_running_spec with "Hg"). iIntros "Hg". wp_pures.
  change (n ∈ dom (g_nodes (with_running (n :: g_running s) s))) in Hndom.
  wp_apply (graph_sources_begin_run_spec _ _ _ n Hndom with "Hg"). iIntros "Hg". wp_pures.
  iMod (ghost_effect_start_step _ _ n with "Hg") as "Hg".
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  wp_bind (Match _ _ _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s5)%I with "[Hg]").
  { destruct (n_scope d) as [o|]; simpl; wp_pures.
    - apply m_bind_Some in Hsc_step as (s41 & [] & Hic1 & Hsc_step).
      apply m_bind_Some in Hsc_step as (s42 & [] & Hdis & Hic2).
      wp_apply (graph_modify_node_spec _ _ n (node_with_in_cleanup true) _ _ _ Hic1 with "Hg").
      { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_in_cleanup_spec true d0); [done|]. }
      iIntros "Hg". wp_pures.
      wp_apply (es_discharge _ _ Hes _ _ o _ _ Hdis with "Hg"). iIntros "Hg". wp_pures.
      wp_apply (graph_modify_node_spec _ _ n (node_with_in_cleanup false) _ _ _ Hic2 with "Hg").
      { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_in_cleanup_spec false d0); [done|]. }
      iIntros "Hg". by iFrame.
    - apply m_return_Some in Hsc_step as [-> _]. by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd' /=. wp_pures.
  wp_bind (If _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s7)%I with "[Hg]").
  { destruct (n_disposed d'); wp_pures.
    - apply m_return_Some in Hbody as [-> _]. by iFrame.
    - wp_apply (es_run_effect_body _ _ Hes _ _ n _ _ Hbody with "Hg"). iIntros "Hg". by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  by wp_apply (pop_running_spec with "Hg").
Qed.
Global Opaque run_effect_step.

Definition run_effect_body_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        graph_modify_node_code "g" "n" node_effect_start_code;;
        let: "b" := Fst (Fst (Snd (Snd "d"))) in
        let: "e" := Snd (Fst (Snd (Snd "d"))) in
        let: "r" := hosted_code "g" "n" (λ: <>, "go" #0 "g" ("e", "b")) in
        match: graph_get_node_code "g" "n" with
          NONE => #()
        | SOME "d'" =>
            (if: Fst (Fst (Fst (Snd "d'"))) then
               graph_sources_clear_code "g" "n"
             else
               graph_sources_end_run_code "g" "n");;
            match: "r" with
              InjL <> => #()
            | InjR "y" =>
                graph_modify_node_code "g" "n" (λ: "d0", node_effect_fail_code "y" "d0")
            end
        end
    end.

Lemma run_effect_body_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  run_effect_body (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} run_effect_body_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  apply m_bind_Some in H as (s2 & [] & Hmod1 & H).
  apply m_bind_Some in H as (s3 & [] & Hemit1 & H). apply m_modify_Some in Hemit1 as [-> _].
  apply m_bind_Some in H as (s4 & r & Hhost & H).
  apply m_bind_Some in H as (s5 & d' & Hd' & H). apply get_node_Some in Hd' as (Hd' & Hndom' & ->).
  apply m_bind_Some in H as (s6 & [] & Hedges & H).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  wp_apply (graph_modify_node_spec _ _ n _ node_effect_start_code _ _ Hmod1 with "Hg").
  { intros d0. apply node_effect_start_spec. }
  iIntros "Hg". wp_pures.
  iMod (emit_step _ _ (EvRun n) with "Hg") as "Hg".
  wp_apply (hosted_spec outcome_val _ _ n (exec f (n_env d) (n_body d)) _ _ _ _ Hhost with "Hg").
  { intros s11 s12 r0 Hr0. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    by wp_apply (es_exec _ _ Hes _ _ _ _ _ _ _ Hr0 with "Hg"). }
  iIntros "Hg". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd' /=. wp_pures.
  apply edges_opt_Some in Hedges as (w & Hw & -> & _).
  wp_bind (If _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g (with_edges w s4))%I with "[Hg]").
  { destruct (n_disposed d'); wp_pures.
    - wp_apply (graph_sources_clear_spec _ _ _ n w Hndom' Hw with "Hg"). iIntros "Hg". by iFrame.
    - wp_apply (graph_sources_end_run_spec _ _ _ n w Hndom' Hw with "Hg"). iIntros "Hg". by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  destruct r as [val_v|y]; simpl; wp_pures.
  - apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
  - apply m_bind_Some in H as (s7 & [] & Hmod2 & Hemit2).
    apply m_modify_Some in Hemit2 as [-> _].
    wp_apply (graph_modify_node_spec _ _ n _ _ _ _ Hmod2 with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_effect_fail_spec y d0); [done|]. }
    iIntros "Hg".
    iMod (emit_step _ _ (EvFail n y) with "Hg") as "Hg".
    iModIntro. by iApply "HΦ".
Qed.
Global Opaque run_effect_body_step.

Definition discharge_step : val :=
  λ: "go" "g" "o",
    detached_code "g" "o" (λ: <>, "go" #15 "g" "o");;
    let: "c" := graph_get_ctrl_code "g" in
    let: "fl" := Fst (Fst (Fst (Fst "c"))) in
    let: "bd" := Fst (Snd (Fst (Fst "c"))) in
    let: "lq" := graph_live_queue_code "g" in
    if: ("bd" = #0) && (~ "fl") && "lq" then
      graph_with_flush_owed_code "g" #true
    else #().

Lemma discharge_step_spec go f (Hes : engine_spec go f) γ g o s s' :
  discharge (S f) o s = Some (s', tt) ->
  {{{ is_graph γ g s }}} discharge_step go g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & [] & Hdet & H).
  apply m_bind_Some in H as (s2 & s1' & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (detached_spec _ _ o (dispose_scope f o) _ _ _ Hdet with "Hg").
  { intros s11 s12 Hs11. iIntros (Ψ) "Hg HΨ". wp_lam.
    by wp_apply (es_dispose_scope _ _ Hes _ _ o _ _ Hs11 with "Hg"). }
  iIntros "Hg". wp_pures.
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_live_queue_spec with "Hg"). iIntros "Hg". wp_pures.
  destruct (decide (g_batch_depth s1 = 0%nat)) as [Heq|Hne].
  - rewrite (bool_decide_eq_true_2 (#(Z.of_nat (g_batch_depth s1)) = #0)%V); [|by rewrite Heq].
    rewrite (bool_decide_eq_true_2 (g_batch_depth s1 = 0%nat)) // /= in H.
    destruct (g_flushing s1), (live_queue s1); simpl in H; wp_pures;
      try (apply m_return_Some in H as [-> _]; by iApply "HΦ").
    apply m_modify_Some in H as [-> _].
    by wp_apply (graph_with_flush_owed_spec _ _ _ true with "Hg").
  - rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_batch_depth s1)) = #0)%V); [|intros [= ?]; lia].
    rewrite (bool_decide_eq_false_2 (g_batch_depth s1 = 0%nat)) // /= in H.
    wp_pures. apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque discharge_step.

Definition retire_step : val :=
  λ: "go" "g" "o",
    detached_code "g" "o" (λ: <>,
      match: graph_get_owner_code "g" "o" with
        NONE => #()
      | SOME "x" =>
          if: Fst (Snd (Snd "x")) then #()
          else
            graph_put_owner_code "g" "o" (owner_retired_code "x");;
            "go" #15 "g" "o"
      end);;
    if: graph_live_queue_code "g" then "go" #22 "g" #() else #().

Lemma retire_step_spec go f (Hes : engine_spec go f) γ g o s s' :
  retire (S f) o s = Some (s', tt) ->
  {{{ is_graph γ g s }}} retire_step go g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & [] & Hdet & H).
  apply m_bind_Some in H as (s2 & s1' & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (detached_spec _ _ o _ _ _ _ Hdet with "Hg").
  { intros s11 s12 Hs11. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    apply m_bind_Some in Hs11 as (s11' & x & Hxo & Hs11).
    apply get_owner_Some in Hxo as (Hxo & ->).
    wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
    rewrite Hxo /=. wp_pures.
    destruct (o_disposed x); wp_pures.
    - apply m_return_Some in Hs11 as [-> _]. by iApply "HΨ".
    - apply m_bind_Some in Hs11 as (s11'' & [] & Hput & Hs11).
      apply put_owner_Some in Hput as [-> _].
      wp_apply (owner_retired_spec x); [done|]. iIntros "_".
      wp_apply (graph_put_owner_spec _ _ _ o (owner_retired x) with "Hg"). iIntros "Hg". wp_pures.
      by wp_apply (es_dispose_scope _ _ Hes _ _ o _ _ Hs11 with "Hg"). }
  iIntros "Hg". wp_pures.
  wp_apply (graph_live_queue_spec with "Hg"). iIntros "Hg".
  destruct (live_queue s1); wp_pures.
  - by wp_apply (es_request_flush _ _ Hes _ _ _ _ H with "Hg").
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque retire_step.

Definition dispose_scope_step : val :=
  λ: "go" "g" "o",
    match: graph_get_owner_code "g" "o" with
      NONE => #()
    | SOME "x" =>
        graph_put_owner_code "g" "o" (owner_detached_code "x");;
        let: "ls" := vlist_rev_code (Fst (Fst (Fst "x"))) in
        let: "cs" := owner_cleanups_code "x" in
        "go" #16 "g" ("o", ("ls", "cs"))
    end.

Lemma dispose_scope_step_spec go f (Hes : engine_spec go f) γ g o s s' :
  dispose_scope (S f) o s = Some (s', tt) ->
  {{{ is_graph γ g s }}} dispose_scope_step go g #(Z.of_nat o) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & x & Hxo & H). apply get_owner_Some in Hxo as (Hxo & ->).
  apply m_bind_Some in H as (s2 & [] & Hput & H). apply put_owner_Some in Hput as [-> _].
  wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
  rewrite Hxo /=. wp_pures.
  wp_apply (owner_detached_spec x); [done|]. iIntros "_".
  wp_apply (graph_put_owner_spec _ _ _ o (owner_detached x) with "Hg"). iIntros "Hg". wp_pures.
  rewrite links_val_eq. wp_apply vlist_rev_spec; [done|]. iIntros "_". wp_pures.
  rewrite -links_val_eq.
  wp_apply (owner_cleanups_spec x); [done|]. iIntros "_". wp_pures.
  by wp_apply (es_teardown _ _ Hes _ _ o (rev (o_children x)) (owner_cleanups x) _ _ H with "Hg").
Qed.
Global Opaque dispose_scope_step.

Definition teardown_step : val :=
  λ: "go" "g" "arg",
    let: "o" := Fst "arg" in
    let: "ls" := Fst (Snd "arg") in
    let: "cs" := Snd (Snd "arg") in
    match: "ls" with
      NONE =>
        match: "cs" with
          NONE => #()
        | SOME "cp" =>
            let: "c" := Fst "cp" in
            let: "cs'" := Snd "cp" in
            "go" #17 "g" ("o", (Fst "c", Fst (Snd "c")));;
            "go" #16 "g" ("o", (NONE, "cs'"))
        end
    | SOME "lp" =>
        let: "l" := Fst "lp" in
        let: "ls'" := Snd "lp" in
        match: "cs" with
          NONE =>
            "go" #18 "g" (Fst (Fst (Fst "l")));;
            "go" #16 "g" ("o", ("ls'", NONE))
        | SOME "cp" =>
            let: "c" := Fst "cp" in
            let: "cs'" := Snd "cp" in
            let: "a" := Snd (Snd "c") in
            let: "lacq" := Snd "l" in
            if: "a" < "lacq" then
              "go" #18 "g" (Fst (Fst (Fst "l")));;
              "go" #16 "g" ("o", ("ls'", "cs"))
            else
              "go" #17 "g" ("o", (Fst "c", Fst (Snd "c")));;
              "go" #16 "g" ("o", ("ls", "cs'"))
        end
    end.

Lemma teardown_step_spec go f (Hes : engine_spec go f) γ g o ls cs s s' :
  teardown (S f) o ls cs s = Some (s', tt) ->
  {{{ is_graph γ g s }}} teardown_step go g (#(Z.of_nat o), (links_val ls, cleanups_val cs))%V {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  cbn [teardown] in H.
  destruct ls as [|l ls'], cs as [|[[b e] a] cs']; simpl; wp_pures.
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_bind_Some in H as (s1 & [] & Hrc & Htd).
    wp_apply (es_run_cleanup _ _ Hes _ _ o b e _ _ Hrc with "Hg"). iIntros "Hg". wp_pures.
    change NONEV with (links_val []).
    by wp_apply (es_teardown _ _ Hes _ _ o [] cs' _ _ Htd with "Hg").
  - apply m_bind_Some in H as (s1 & [] & Hrel & Htd).
    wp_apply (es_release _ _ Hes _ _ (l_child l) _ _ Hrel with "Hg"). iIntros "Hg". wp_pures.
    change NONEV with (cleanups_val []).
    by wp_apply (es_teardown _ _ Hes _ _ o ls' [] _ _ Htd with "Hg").
  - destruct (decide (a < l_acquired l)%nat) as [Hlt|Hge].
    + rewrite (bool_decide_eq_true_2 (Z.of_nat a < Z.of_nat (l_acquired l))%Z); [|lia].
      wp_pures.
      apply m_bind_Some in H as (s1 & [] & Hrel & Htd).
      wp_apply (es_release _ _ Hes _ _ (l_child l) _ _ Hrel with "Hg"). iIntros "Hg". wp_pures.
      change (SOMEV ((compile_cmd b, (env_val e, #(Z.of_nat a)))%V, cleanups_val cs'))
        with (cleanups_val ((b, e, a) :: cs')).
      by wp_apply (es_teardown _ _ Hes _ _ o ls' ((b, e, a) :: cs') _ _ Htd with "Hg").
    + rewrite (bool_decide_eq_false_2 (Z.of_nat a < Z.of_nat (l_acquired l))%Z); [|lia].
      wp_pures.
      apply m_bind_Some in H as (s1 & [] & Hrc & Htd).
      wp_apply (es_run_cleanup _ _ Hes _ _ o b e _ _ Hrc with "Hg"). iIntros "Hg". wp_pures.
      change (SOMEV ((#(Z.of_nat (l_child l)), #(Z.of_nat (l_owner l)), opt_nat_val (l_generation l), #(Z.of_nat (l_acquired l)))%V, links_val ls'))
        with (links_val (l :: ls')).
      by wp_apply (es_teardown _ _ Hes _ _ o (l :: ls') cs' _ _ Htd with "Hg").
Qed.
Global Opaque teardown_step.

Definition run_cleanup_step : val :=
  λ: "go" "g" "arg",
    let: "o" := Fst "arg" in
    let: "b" := Fst (Snd "arg") in
    let: "e" := Snd (Snd "arg") in
    match: graph_get_owner_code "g" "o" with
      NONE => #()
    | SOME <> =>
        match: "go" #0 "g" ("e", "b") with
          InjL <> => #()
        | InjR "y" => record_error_code "g" "o" "y"
        end
    end.

Lemma run_cleanup_step_spec go f (Hes : engine_spec go f) γ g o b e s s' :
  run_cleanup (S f) o b e s = Some (s', tt) ->
  {{{ is_graph γ g s }}} run_cleanup_step go g (#(Z.of_nat o), (compile_cmd b, env_val e))%V {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & x & Hxo & H). apply get_owner_Some in Hxo as (Hxo & ->).
  apply m_bind_Some in H as (s2 & [] & Hemit1 & H). apply m_modify_Some in Hemit1 as [-> _].
  apply m_bind_Some in H as (s3 & r & Hexec & H).
  wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
  rewrite Hxo /=. wp_pures.
  iMod (emit_step _ _ (EvCleanup (o_host x)) with "Hg") as "Hg".
  wp_apply (es_exec _ _ Hes _ _ e b _ _ _ Hexec with "Hg"). iIntros "Hg".
  destruct r as [v|y]; simpl; wp_pures.
  - apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
  - apply m_bind_Some in H as (s4 & [] & Hre & Hemit2).
    apply m_modify_Some in Hemit2 as [-> _].
    wp_apply (record_error_spec _ _ o y _ _ Hre with "Hg"). iIntros "Hg".
    iMod (emit_step _ _ (EvFail (o_host x) y) with "Hg") as "Hg".
    iModIntro. by iApply "HΦ".
Qed.
Global Opaque run_cleanup_step.

Definition release_step : val :=
  λ: "go" "g" "n",
    graph_modify_node_code "g" "n" (λ: "d0", node_with_link_code NONE "d0");;
    "go" #19 "g" "n".

Lemma release_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  release (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} release_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & [] & Hmod & Hdisp).
  wp_apply (graph_modify_node_spec _ _ n (node_with_link None) _ _ _ Hmod with "Hg").
  { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. wp_pures. by wp_apply (node_with_link_spec None d0); [done|]. }
  iIntros "Hg". wp_pures.
  by wp_apply (es_dispose_node _ _ Hes _ _ n _ _ Hdisp with "Hg").
Qed.
Global Opaque release_step.

Definition dispose_node_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        if: Fst (Fst (Fst (Snd "d"))) then #()
        else
          graph_modify_node_code "g" "n" node_dispose_clear_code;;
          (match: Snd (Snd (Snd (Snd (Snd "d")))) with
             NONE => #()
           | SOME "l" => unlink_code "g" "l"
           end);;
          graph_sources_clear_code "g" "n";;
          match: Fst (Snd (Snd (Fst (Snd "d")))) with
            NONE => #()
          | SOME "o" => "go" #14 "g" "o"
          end
    end.

Lemma dispose_node_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  dispose_node (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} dispose_node_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & Hndom & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_disposed d); wp_pures.
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_bind_Some in H as (s2 & [] & Hmod & H).
    apply m_bind_Some in H as (s3 & [] & Hunl & H).
    apply m_bind_Some in H as (s4 & [] & Hclr & H).
    apply edges_opt_Some in Hclr as (w & Hw & -> & _).
    assert (Hndom2 : n ∈ dom (g_nodes s2)).
    { destruct (modify_node_dom_nodes _ _ _ _ _ Hmod) as [_ ->]. exact Hndom. }
    assert (Hndom3 : n ∈ dom (g_nodes s3)).
    { destruct (n_link d) as [l|].
      - rewrite (unlink_dom_nodes _ _ _ _ Hunl). exact Hndom2.
      - apply m_return_Some in Hunl as [-> _]. exact Hndom2. }
    wp_apply (graph_modify_node_spec _ _ n _ node_dispose_clear_code _ _ Hmod with "Hg").
    { intros d0. apply node_dispose_clear_spec. }
    iIntros "Hg". wp_pures.
    wp_bind (Match _ _ _ _ _).
    iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s3)%I with "[Hg]").
    { destruct (n_link d) as [l|]; wp_pures.
      - change (#(Z.of_nat (l_child l)), #(Z.of_nat (l_owner l)), opt_nat_val (l_generation l), #(Z.of_nat (l_acquired l)))%V
          with (link_val l).
        wp_apply (unlink_spec _ _ l _ _ Hunl with "Hg"). iIntros "Hg". by iFrame.
      - apply m_return_Some in Hunl as [-> _]. by iFrame. }
    iIntros (v) "[-> Hg]". wp_pures.
    wp_apply (graph_sources_clear_spec _ _ _ n w Hndom3 Hw with "Hg"). iIntros "Hg". wp_pures.
    destruct (n_scope d) as [o|]; simpl; wp_pures.
    + by wp_apply (es_retire _ _ Hes _ _ o _ _ H with "Hg").
    + apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque dispose_node_step.

Definition attach_step : val :=
  λ: "go" "g" "arg",
    let: "n" := Fst "arg" in
    let: "o" := Snd "arg" in
    match: graph_get_owner_code "g" "o" with
      NONE => #()
    | SOME "x" =>
        if: Fst (Snd (Snd "x")) then
          "go" #18 "g" "n";;
          let: "l0" := ("n", "o", NONE, #0) in
          graph_modify_node_code "g" "n" (λ: "d0", node_with_link_code (SOME "l0") "d0")
        else
          let: "acq" := Fst (Fst (Snd "x")) in
          let: "gen" := Snd (Fst (Snd "x")) in
          let: "l" := ("n", "o", SOME "gen", "acq") in
          graph_put_owner_code "g" "o" (owner_with_child_code "l" "x");;
          graph_modify_node_code "g" "n" (λ: "d0", node_with_link_code (SOME "l") "d0")
    end.

Lemma attach_step_spec go f (Hes : engine_spec go f) γ g n o s s' :
  attach (S f) n o s = Some (s', tt) ->
  {{{ is_graph γ g s }}} attach_step go g (#(Z.of_nat n), #(Z.of_nat o))%V {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & x & Hxo & H). apply get_owner_Some in Hxo as (Hxo & ->).
  wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
  rewrite Hxo /=. wp_pures.
  destruct (o_disposed x); wp_pures.
  - apply m_bind_Some in H as (s2 & [] & Hrel & Hmod).
    wp_apply (es_release _ _ Hes _ _ n _ _ Hrel with "Hg"). iIntros "Hg". wp_pures.
    set (l0 := {| l_child := n; l_owner := o; l_generation := None; l_acquired := 0 |}) in *.
    wp_apply (graph_modify_node_spec _ _ n (node_with_link (Some l0)) _ _ _ Hmod with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. wp_pures.
      change (SOMEV (#(Z.of_nat n), #(Z.of_nat o), NONEV, #0)%V) with (opt_link_val (Some l0)).
      by wp_apply (node_with_link_spec (Some l0) d0); [done|]. }
    by iIntros "Hg"; iApply "HΦ".
  - apply m_bind_Some in H as (s2 & [] & Hput & Hmod).
    apply put_owner_Some in Hput as [-> _].
    set (l := {| l_child := n; l_owner := o; l_generation := Some (o_generation x); l_acquired := o_acquisitions x |}) in *.
    change (#(Z.of_nat n), #(Z.of_nat o), SOMEV #(Z.of_nat (o_generation x)), #(Z.of_nat (o_acquisitions x)))%V
      with (link_val l).
    wp_apply (owner_with_child_spec l x); [done|]. iIntros "_".
    wp_apply (graph_put_owner_spec _ _ _ o (owner_with_child l x) with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (graph_modify_node_spec _ _ n (node_with_link (Some l)) _ _ _ Hmod with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. wp_pures.
      change (SOMEV (link_val l)) with (opt_link_val (Some l)).
      by wp_apply (node_with_link_spec (Some l) d0); [done|]. }
    by iIntros "Hg"; iApply "HΦ".
Qed.
Global Opaque attach_step.

Definition write_signal_step : val :=
  λ: "go" "g" "arg",
    let: "n" := Fst "arg" in
    let: "v" := Snd "arg" in
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        if: Snd (Fst (Fst (Fst "d"))) = "v" then #()
        else
          graph_modify_node_code "g" "n" (λ: "d0", node_with_value_code "v" "d0");;
          notify_dirty_code "g" "n";;
          "go" #22 "g" #()
    end.

Lemma write_signal_step_spec go f (Hes : engine_spec go f) γ g n v s s' :
  write_signal (S f) n v s = Some (s', tt) ->
  {{{ is_graph γ g s }}} write_signal_step go g (#(Z.of_nat n), #v)%V {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & _ & ->).
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  destruct (n_kind d); [|done|done].
  destruct (decide (n_value d = v)) as [->|Hne].
  - rewrite (bool_decide_eq_true_2 (#v = #v)%V) //. wp_pures.
    apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - rewrite (bool_decide_eq_false_2 (#(n_value d) = #v)%V); [|intros [= ?]; done]. wp_pures.
    apply m_bind_Some in H as (s1 & [] & Hmod & H).
    apply m_bind_Some in H as (s2 & [] & Hpub & H). apply m_modify_Some in Hpub as [-> _].
    apply m_bind_Some in H as (s3 & [] & Hnd & Hrf). apply m_step_Some in Hnd as [Hnd _].
    wp_apply (graph_modify_node_spec _ _ n (node_with_value v) _ _ _ Hmod with "Hg").
    { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_with_value_spec v d0); [done|]. }
    iIntros "Hg". wp_pures.
    iMod (ghost_publish_step _ _ n with "Hg") as "Hg".
    wp_apply (notify_dirty_spec _ _ f n _ _ Hnd with "Hg"). iIntros "Hg". wp_pures.
    by wp_apply (es_request_flush _ _ Hes _ _ _ _ Hrf with "Hg").
Qed.
Global Opaque write_signal_step.

Definition request_flush_step : val :=
  λ: "go" "g" <>,
    let: "c" := graph_get_ctrl_code "g" in
    let: "fl" := Fst (Fst (Fst (Fst "c"))) in
    let: "bd" := Fst (Snd (Fst (Fst "c"))) in
    let: "lq" := graph_live_queue_code "g" in
    if: "lq" && ("bd" = #0) then
      if: outside_bodies_code "g" then "go" #24 "g" #()
      else if: "fl" then #()
      else graph_with_flush_owed_code "g" #true
    else #().

Lemma request_flush_step_spec go f (Hes : engine_spec go f) γ g s s' :
  request_flush (S f) s = Some (s', tt) ->
  {{{ is_graph γ g s }}} request_flush_step go g #() {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (graph_live_queue_spec with "Hg"). iIntros "Hg". wp_pures.
  destruct (live_queue s); simpl in H; wp_pures.
  - destruct (decide (g_batch_depth s = 0%nat)) as [Heq|Hne].
    + rewrite (bool_decide_eq_true_2 (#(Z.of_nat (g_batch_depth s)) = #0)%V); [|by rewrite Heq].
      rewrite (bool_decide_eq_true_2 (g_batch_depth s = 0%nat)) // /= in H.
      wp_pures. wp_apply (outside_bodies_spec with "Hg"). iIntros "Hg".
      destruct (outside_bodies s); wp_pures.
      * by wp_apply (es_run_flush _ _ Hes _ _ _ _ H with "Hg").
      * destruct (g_flushing s); wp_pures.
        -- apply m_return_Some in H as [-> _]. by iApply "HΦ".
        -- apply m_modify_Some in H as [-> _].
           by wp_apply (graph_with_flush_owed_spec _ _ _ true with "Hg").
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_batch_depth s)) = #0)%V); [|intros [= ?]; lia].
      rewrite (bool_decide_eq_false_2 (g_batch_depth s = 0%nat)) // /= in H.
      wp_pures. apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque request_flush_step.

Definition settle_flush_step : val :=
  λ: "go" "g" <>,
    let: "c" := graph_get_ctrl_code "g" in
    let: "fo" := Snd (Fst (Fst (Fst "c"))) in
    let: "bd" := Fst (Snd (Fst (Fst "c"))) in
    let: "ob" := outside_bodies_code "g" in
    if: "fo" && ("bd" = #0) && "ob" then "go" #24 "g" #() else #().

Lemma settle_flush_step_spec go f (Hes : engine_spec go f) γ g s s' :
  settle_flush (S f) s = Some (s', tt) ->
  {{{ is_graph γ g s }}} settle_flush_step go g #() {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_apply (outside_bodies_spec with "Hg"). iIntros "Hg". wp_pures.
  destruct (g_flush_owed s); simpl in H; wp_pures.
  - destruct (decide (g_batch_depth s = 0%nat)) as [Heq|Hne].
    + rewrite (bool_decide_eq_true_2 (#(Z.of_nat (g_batch_depth s)) = #0)%V); [|by rewrite Heq].
      rewrite (bool_decide_eq_true_2 (g_batch_depth s = 0%nat)) // /= in H.
      destruct (outside_bodies s); simpl in H; wp_pures.
      * by wp_apply (es_run_flush _ _ Hes _ _ _ _ H with "Hg").
      * apply m_return_Some in H as [-> _]. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_batch_depth s)) = #0)%V); [|intros [= ?]; lia].
      rewrite (bool_decide_eq_false_2 (g_batch_depth s = 0%nat)) // /= in H.
      wp_pures. apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque settle_flush_step.

Definition run_flush_step : val :=
  λ: "go" "g" <>,
    let: "c" := graph_get_ctrl_code "g" in
    if: Fst (Fst (Fst (Fst "c"))) then #()
    else
      graph_with_flushing_code "g" #true;;
      graph_with_flush_owed_code "g" #false;;
      "go" #25 "g" #();;
      graph_with_flushing_code "g" #false.

Lemma run_flush_step_spec go f (Hes : engine_spec go f) γ g s s' :
  run_flush (S f) s = Some (s', tt) ->
  {{{ is_graph γ g s }}} run_flush_step go g #() {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
  destruct (g_flushing s); wp_pures.
  - apply m_return_Some in H as [-> _]. by iApply "HΦ".
  - apply m_bind_Some in H as (s1 & [] & H1 & H). apply m_modify_Some in H1 as [-> _].
    apply m_bind_Some in H as (s2 & [] & Hdr & H2). apply m_modify_Some in H2 as [-> _].
    wp_apply (graph_with_flushing_spec _ _ _ true with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (graph_with_flush_owed_spec _ _ _ false with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (es_drain _ _ Hes _ _ _ _ Hdr with "Hg"). iIntros "Hg". wp_pures.
    by wp_apply (graph_with_flushing_spec _ _ _ false with "Hg").
Qed.
Global Opaque run_flush_step.

Definition drain_step : val :=
  λ: "go" "g" <>,
    match: graph_dequeue_code "g" with
      NONE => #()
    | SOME "n" =>
        "go" #10 "g" "n";;
        "go" #25 "g" #()
    end.

Lemma drain_step_spec go f (Hes : engine_spec go f) γ g s s' :
  drain (S f) s = Some (s', tt) ->
  {{{ is_graph γ g s }}} drain_step go g #() {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  simpl in H. apply m_bind_Some in H as (s1 & s0 & Hget & H). apply m_get_Some in Hget as [-> ->].
  wp_apply (graph_dequeue_spec with "Hg"). iIntros "Hg".
  destruct (g_queue s !! g_queue_head s) as [n|]; wp_pures.
  - apply m_bind_Some in H as (s1 & [] & H1 & H). apply m_modify_Some in H1 as [-> _].
    apply m_bind_Some in H as (s2 & [] & Hex & Hdr).
    wp_apply (es_execute_effect _ _ Hes _ _ n _ _ Hex with "Hg"). iIntros "Hg". wp_pures.
    by wp_apply (es_drain _ _ Hes _ _ _ _ Hdr with "Hg").
  - apply m_modify_Some in H as [-> _]. by iApply "HΦ".
Qed.
Global Opaque drain_step.

(** ** Step #9 ([run_memo_step]) and Step #0 ([exec_step]) *)

Definition opt_z_eq_code : val :=
  λ: "a" "b",
    match: "a" with
      NONE => match: "b" with NONE => #true | SOME <> => #false end
    | SOME "x" => match: "b" with NONE => #false | SOME "y" => "x" = "y" end
    end.

Lemma opt_z_eq_spec (a b : option Z) :
  {{{ True }}} opt_z_eq_code (opt_z_val a) (opt_z_val b) {{{ RET #(bool_decide (a = b)); True }}}.
Proof.
  iIntros (Φ) "_ HΦ". wp_lam.
  destruct a as [x|], b as [y|]; simpl; wp_pures.
  - destruct (decide (x = y)) as [->|Hne].
    + rewrite (bool_decide_eq_true_2 (#y = #y)%V) // (bool_decide_eq_true_2 (Some y = Some y)) //. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (#x = #y)%V); [|intros [= ?]; done].
      rewrite (bool_decide_eq_false_2 (Some x = Some y)); [by iApply "HΦ"|intros [= ?]; done].
  - by iApply "HΦ".
  - by iApply "HΦ".
  - by iApply "HΦ".
Qed.
Global Opaque opt_z_eq_code.

Definition memo_moved (n : node_id) (previous : Z) (previous_status : status) (previous_failure : option Z) (d2 : node) : M bool :=
  if decide (n_status d2 ≠ previous_status \/ n_failure d2 ≠ previous_failure) then m_return true
  else
    match n_comparer d2 with
    | CmpEq => m_return (bool_decide (n_value d2 ≠ previous))
    | CmpNever => m_return true
    | CmpThrows y =>
      let* _ := modify_node n (fun x =>
        node_with_status StError (node_with_failure (Some y) (node_with_value previous x))) in
      m_return true
    end.

Definition memo_moved_code : val :=
  λ: "g" "n" "prev" "prev_st" "prev_fail" "d2",
    let: "st2" := Snd (Snd (Fst (Fst "d2"))) in
    let: "fail2" := Fst (Snd (Fst (Fst "d2"))) in
    if: (~ ("st2" = "prev_st")) || (~ (opt_z_eq_code "fail2" "prev_fail")) then
      #true
    else
      let: "cmp" := Fst (Snd (Snd (Snd (Snd "d2")))) in
      let: "ctag" := Fst "cmp" in
      if: "ctag" = #0 then
        ~ (Snd (Fst (Fst (Fst "d2"))) = "prev")
      else if: "ctag" = #1 then
        #true
      else
        let: "y" := Snd "cmp" in
        graph_modify_node_code "g" "n" (λ: "d0", node_memo_cmp_throws_code "y" "prev" "d0");;
        #true.

Lemma memo_moved_spec γ g n prev prev_st prev_fail d2 s s' moved :
  memo_moved n prev prev_st prev_fail d2 s = Some (s', moved) ->
  {{{ is_graph γ g s }}}
    memo_moved_code g #(Z.of_nat n) #prev (status_val prev_st) (opt_z_val prev_fail) (node_val d2)
  {{{ RET #moved; is_graph γ g s' }}}.
Proof.
  intros Hm. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  rewrite /memo_moved in Hm.
  wp_bind (BinOp EqOp (status_val (n_status d2)) (status_val prev_st)).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #(bool_decide (n_status d2 = prev_st))⌝)%I).
  { destruct (n_status d2), prev_st; wp_pures; done. }
  iIntros (v) "->".
  destruct (decide (n_status d2 = prev_st)) as [Hst|Hst].
  - rewrite (bool_decide_eq_true_2 (n_status d2 = prev_st)) //. wp_pures.
    wp_apply opt_z_eq_spec; [done|]. iIntros "_".
    destruct (decide (n_failure d2 = prev_fail)) as [Hfl|Hfl].
    + rewrite (bool_decide_eq_true_2 (n_failure d2 = prev_fail)) //. wp_pures.
      rewrite decide_False in Hm; [|by intros [?|?]].
      destruct (n_comparer d2) as [| |y]; simpl; wp_pures.
      * destruct (decide (n_value d2 = prev)) as [Hv|Hv].
        -- rewrite Hv in Hm |- *.
           rewrite (bool_decide_eq_false_2 (prev ≠ prev)) in Hm; [|by intros Hne].
           apply m_return_Some in Hm as [-> ->].
           rewrite (bool_decide_eq_true_2 (#prev = #prev)%V) //=.
           by iApply "HΦ".
        -- rewrite (bool_decide_eq_true_2 (n_value d2 ≠ prev)) // in Hm.
           apply m_return_Some in Hm as [-> ->].
           rewrite (bool_decide_eq_false_2 (#(n_value d2) = #prev)%V); [|by intros [= ?]].
           simpl. by iApply "HΦ".
      * apply m_return_Some in Hm as [-> ->]. by iApply "HΦ".
      * apply m_bind_Some in Hm as (s1 & [] & Hmod & Hret).
        apply m_return_Some in Hret as [-> ->].
        wp_apply (graph_modify_node_spec _ _ n _ _ _ _ Hmod with "Hg").
        { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. wp_pures. by wp_apply (node_memo_cmp_throws_spec y prev d0); [done|]. }
        iIntros "Hg". wp_pures. by iApply "HΦ".
    + rewrite (bool_decide_eq_false_2 (n_failure d2 = prev_fail)) //. wp_pures.
      rewrite decide_True in Hm; [|by right].
      apply m_return_Some in Hm as [-> ->]. by iApply "HΦ".
  - rewrite (bool_decide_eq_false_2 (n_status d2 = prev_st)) //. wp_pures.
    rewrite decide_True in Hm; [|by left].
    apply m_return_Some in Hm as [-> ->]. by iApply "HΦ".
Qed.
Global Opaque memo_moved_code.

Definition run_memo_step : val :=
  λ: "go" "g" "n",
    match: graph_get_node_code "g" "n" with
      NONE => #()
    | SOME "d" =>
        let: "prev" := Snd (Fst (Fst (Fst "d"))) in
        let: "prev_st" := Snd (Snd (Fst (Fst "d"))) in
        let: "prev_fail" := Fst (Snd (Fst (Fst "d"))) in
        let: "pub" := Snd (Snd (Snd (Fst "d"))) in
        let: "b" := Fst (Fst (Snd (Snd "d"))) in
        let: "e" := Snd (Fst (Snd (Snd "d"))) in
        graph_modify_node_code "g" "n" node_memo_start_code;;
        graph_sources_begin_run_code "g" "n";;
        push_running_code "g" "n";;
        let: "body_env" :=
          if: "pub" then SOME ((#(Z.of_nat prev_var), "prev"), "e")
          else without_prev_code "e" in
        let: "r" := hosted_code "g" "n" (λ: <>, "go" #0 "g" ("body_env", "b")) in
        match: graph_get_node_code "g" "n" with
          NONE => #()
        | SOME "d1" =>
            let: "res" :=
              if: Snd (Snd (Snd (Fst (Snd "d1")))) then InjR #pure_scope_failure
              else "r" in
            (match: "res" with
               InjL "v" => graph_modify_node_code "g" "n" (λ: "d0", node_memo_ok_code "v" "d0")
             | InjR <> => #()
             end);;
            (if: Fst (Fst (Fst (Snd "d1"))) then
               graph_sources_clear_code "g" "n"
             else
               graph_sources_end_run_code "g" "n");;
            pop_running_code "g";;
            (match: "res" with
               InjL <> => #()
             | InjR "y" => graph_modify_node_code "g" "n" (λ: "d0", node_memo_fail_code "y" "d0")
             end);;
            match: graph_get_node_code "g" "n" with
              NONE => #()
            | SOME "d2" =>
                let: "moved" := memo_moved_code "g" "n" "prev" "prev_st" "prev_fail" "d2" in
                (if: "moved" then
                   let: "c0" := graph_get_ctrl_code "g" in
                   let: "cur0" := Fst (Fst (Snd (Fst "c0"))) in
                   notify_dirty_except_code "g" "n" "cur0";;
                   let: "c1" := graph_get_ctrl_code "g" in
                   let: "fl1" := Fst (Fst (Fst (Fst "c1"))) in
                   let: "bd1" := Fst (Snd (Fst (Fst "c1"))) in
                   let: "lq1" := graph_live_queue_code "g" in
                   if: ("bd1" = #0) && (~ "fl1") && "lq1" then
                     graph_with_flush_owed_code "g" #true
                   else #()
                 else #());;
                match: graph_get_node_code "g" "n" with
                  NONE => #()
                | SOME "d3" =>
                    if: Fst (Fst (Snd (Fst "d3"))) = #0 then #()
                    else announce_code "g" "n"
                end
            end
        end
    end.

Lemma run_memo_step_spec go f (Hes : engine_spec go f) γ g n s s' :
  run_memo (S f) n s = Some (s', tt) ->
  {{{ is_graph γ g s }}} run_memo_step go g #(Z.of_nat n) {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". iApply wp_fupd. wp_lam. wp_pures.
  simpl in H.
  apply m_bind_Some in H as (s1 & d & Hd & H). apply get_node_Some in Hd as (Hd & Hndom & ->).
  apply m_bind_Some in H as (s2 & [] & Hmod_start & H).
  apply m_bind_Some in H as (s3 & [] & Hbeg & H). apply edges_Some in Hbeg as [-> _].
  apply m_bind_Some in H as (s4 & [] & Hgrs & H). apply m_modify_Some in Hgrs as [-> _].
  apply m_bind_Some in H as (s5 & [] & Hpush & H). apply push_running_Some in Hpush as [-> _].
  apply m_bind_Some in H as (s6 & r & Hhost & H).
  apply m_bind_Some in H as (s7 & d1 & Hd1 & H). apply get_node_Some in Hd1 as (Hd1 & Hndom6 & ->).
  apply m_bind_Some in H as (s8 & [] & Hmod_ok & H).
  apply m_bind_Some in H as (s9 & [] & Hedges & H).
  apply m_bind_Some in H as (s10 & [] & Hpop & H). apply pop_running_Some in Hpop as [-> _].
  apply m_bind_Some in H as (s11 & [] & Hmod_fail & H).
  apply m_bind_Some in H as (s12 & d2 & Hd2 & H). apply get_node_Some in Hd2 as (Hd2 & _ & ->).
  apply m_bind_Some in H as (s13 & moved & Hmoved & H).
  apply m_bind_Some in H as (s14 & s13' & Hget13 & H). apply m_get_Some in Hget13 as [-> ->].
  apply m_bind_Some in H as (s15 & [] & Hgre & H). apply m_modify_Some in Hgre as [-> _].
  apply m_bind_Some in H as (s16 & [] & Hmov_step & H).
  apply m_bind_Some in H as (s17 & d3 & Hd3 & H). apply get_node_Some in Hd3 as (Hd3 & _ & ->).
  assert (Hndom2 : n ∈ dom (g_nodes s2)).
  { destruct (modify_node_dom_nodes _ _ _ _ _ Hmod_start) as [_ ->]. exact Hndom. }
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd /=. wp_pures.
  wp_apply (graph_modify_node_spec _ _ n _ node_memo_start_code _ _ Hmod_start with "Hg").
  { intros d0. apply node_memo_start_spec. }
  iIntros "Hg". wp_pures.
  wp_apply (graph_sources_begin_run_spec _ _ s2 n Hndom2 with "Hg"). iIntros "Hg". wp_pures.
  iMod (ghost_run_start_step _ _ n with "Hg") as "Hg".
  wp_apply (push_running_spec with "Hg"). iIntros "Hg". wp_pures.
  set (body_env := if n_published d then (prev_var, n_value d) :: n_env d else without_prev (n_env d)) in *.
  wp_bind (If #(n_published d) _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = env_val body_env⌝)%I).
  { subst body_env. destruct (n_published d); wp_pures; [done|].
    by wp_apply without_prev_spec; [done|]. }
  iIntros (v) "->". wp_pures.
  wp_apply (hosted_spec outcome_val _ _ n (exec f body_env (n_body d)) _ _ _ _ Hhost with "Hg").
  { intros s21 s22 r0 Hr0. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    by wp_apply (es_exec _ _ Hes _ _ _ _ _ _ _ Hr0 with "Hg"). }
  iIntros "Hg". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd1 /=. wp_pures.
  set (res := if n_violated d1 then Fail pure_scope_failure else r) in *.
  wp_bind (If #(n_violated d1) _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = outcome_val res⌝)%I).
  { subst res. destruct (n_violated d1); wp_pures; done. }
  iIntros (v) "->". wp_pures.
  assert (Hndom8 : n ∈ dom (g_nodes s8)).
  { destruct res as [val_v|y].
    - destruct (modify_node_dom_nodes _ _ _ _ _ Hmod_ok) as [_ ->]. exact Hndom6.
    - apply m_return_Some in Hmod_ok as [-> _]. exact Hndom6. }
  wp_bind (Match (outcome_val res) _ _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s8)%I with "[Hg]").
  { destruct res as [val_v|y]; simpl; wp_pures.
    - wp_apply (graph_modify_node_spec _ _ n _ _ _ _ Hmod_ok with "Hg").
      { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_memo_ok_spec val_v d0); [done|]. }
      iIntros "Hg". by iFrame.
    - apply m_return_Some in Hmod_ok as [-> _]. by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  apply edges_opt_Some in Hedges as (w & Hw & -> & _).
  wp_bind (If #(n_disposed d1) _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g (with_edges w s8))%I with "[Hg]").
  { destruct (n_disposed d1); wp_pures.
    - wp_apply (graph_sources_clear_spec _ _ s8 n w Hndom8 Hw with "Hg"). iIntros "Hg". by iFrame.
    - wp_apply (graph_sources_end_run_spec _ _ s8 n w Hndom8 Hw with "Hg"). iIntros "Hg". by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  wp_apply (pop_running_spec with "Hg"). iIntros "Hg". wp_pures.
  wp_bind (Match (outcome_val res) _ _ _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s11)%I with "[Hg]").
  { destruct res as [val_v|y]; simpl; wp_pures.
    - apply m_return_Some in Hmod_fail as [-> _]. by iFrame.
    - wp_apply (graph_modify_node_spec _ _ n _ _ _ _ Hmod_fail with "Hg").
      { intros d0. iIntros (Ψ) "_ HΨ". wp_lam. by wp_apply (node_memo_fail_spec y d0); [done|]. }
      iIntros "Hg". by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd2 /=. wp_pures.
  wp_apply (memo_moved_spec _ _ n (n_value d) (n_status d) (n_failure d) d2 _ _ _ Hmoved with "Hg").
  iIntros "Hg". wp_pures.
  iMod (ghost_run_end_step _ _ n (g_current s13) moved with "Hg") as "Hg".
  wp_bind (If #moved _ _).
  iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s16)%I with "[Hg]").
  { destruct moved; wp_pures.
    - apply m_bind_Some in Hmov_step as (s151 & [] & Hnde & Hmov_step).
      apply m_step_Some in Hnde as [Hnde _].
      apply m_bind_Some in Hmov_step as (s152 & s151' & Hget151 & Hmov_step).
      apply m_get_Some in Hget151 as [-> ->].
      wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
      assert (Hcur_gre : g_current (ghost_run_end n (g_current s13) true s13) = g_current s13)
        by (rewrite /ghost_run_end; by destruct (g_nodes s13 !! n)).
      rewrite Hcur_gre.
      wp_apply (notify_dirty_except_spec _ _ f n (g_current s13) _ _ Hnde with "Hg").
      iIntros "Hg". wp_pures.
      wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
      wp_apply (graph_live_queue_spec with "Hg"). iIntros "Hg". wp_pures.
      destruct (decide (g_batch_depth s151 = 0%nat)) as [Heq|Hne].
      + rewrite (bool_decide_eq_true_2 (#(Z.of_nat (g_batch_depth s151)) = #0)%V); [|by rewrite Heq].
        rewrite (bool_decide_eq_true_2 (g_batch_depth s151 = 0%nat)) // /= in Hmov_step.
        destruct (g_flushing s151), (live_queue s151); simpl in Hmov_step; wp_pures;
          try (apply m_return_Some in Hmov_step as [-> _]; by iFrame).
        apply m_modify_Some in Hmov_step as [-> _].
        wp_apply (graph_with_flush_owed_spec _ _ _ true with "Hg"). iIntros "Hg". by iFrame.
      + rewrite (bool_decide_eq_false_2 (#(Z.of_nat (g_batch_depth s151)) = #0)%V); [|intros [= ?]; lia].
        rewrite (bool_decide_eq_false_2 (g_batch_depth s151 = 0%nat)) // /= in Hmov_step.
        wp_pures. apply m_return_Some in Hmov_step as [-> _]. by iFrame.
    - apply m_return_Some in Hmov_step as [-> _]. by iFrame. }
  iIntros (v) "[-> Hg]". wp_pures.
  wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
  rewrite Hd3 /=. wp_pures.
  destruct (n_freshness d3) eqn:Hfr3; simpl in H |- *; wp_pures.
  - apply m_return_Some in H as [-> _]. iModIntro. by iApply "HΦ".
  - wp_apply (announce_spec _ _ n _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
  - wp_apply (announce_spec _ _ n _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
Qed.
Global Opaque run_memo_step.

Definition on_value_code : val :=
  λ: "e" "x" "k",
    match: eval_in_env_code "e" "x" with
      NONE => #()
    | SOME "r" =>
        match: "r" with
          InjL "v" => "k" "v"
        | InjR "f" => InjR "f"
        end
    end.

Definition on_node_code : val :=
  λ: "e" "x" "k",
    on_value_code "e" "x" (λ: "z",
      if: #0 ≤ "z" then "k" "z" else #()).

Lemma on_value_spec γ g (e : env) (x : Syntax.expr) (k : Z -> M outcome) (kv : val) (s s' : State.state) (r : outcome) :
  on_value e x k s = Some (s', r) ->
  (∀ z, k z s = Some (s', r) ->
    {{{ is_graph γ g s }}} kv #z {{{ RET outcome_val r; is_graph γ g s' }}}) ->
  {{{ is_graph γ g s }}} on_value_code (env_val e) (compile_expr x) kv {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros Hov Hk. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply eval_in_env_spec; [done|]. iIntros "_".
  rewrite /on_value in Hov.
  destruct (eval_expr e (env_lookup e prev_var) x) as [[v|f]|]; [| |done].
  - wp_pures. by wp_apply (Hk v Hov with "Hg").
  - injection Hov as <- <-. wp_pures. by iApply "HΦ".
Qed.
Global Opaque on_value_code.

Lemma on_node_spec γ g (e : env) (x : Syntax.expr) (k : node_id -> M outcome) (kv : val) (s s' : State.state) (r : outcome) :
  on_node e x k s = Some (s', r) ->
  (∀ t, k t s = Some (s', r) ->
    {{{ is_graph γ g s }}} kv #(Z.of_nat t) {{{ RET outcome_val r; is_graph γ g s' }}}) ->
  {{{ is_graph γ g s }}} on_node_code (env_val e) (compile_expr x) kv {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros Hon Hk. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  wp_apply (on_value_spec _ _ e x _ _ _ _ _ Hon with "Hg"); [|done].
  intros z Hz. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
  destruct (decide (0 <= z)%Z) as [Hge|Hlt]; [|done].
  rewrite (bool_decide_eq_true_2 (0 <= z)%Z) //. wp_pures.
  rewrite -(Z2Nat.id z Hge).
  by wp_apply (Hk (Z.to_nat z) Hz with "Hg").
Qed.
Global Opaque on_node_code.

Definition exec_step : val :=
  λ: "go" "g" "arg",
    let: "e" := Fst "arg" in
    let: "c" := Snd "arg" in
    let: "c1" := Fst "c" in
    if: "c1" = #0 then
      on_value_code "e" (Snd "c") (λ: "v", InjL "v")
    else if: "c1" = #1 then
      InjR (Snd "c")
    else
      let: "c2" := Fst "c1" in
      let: "k" := Snd "c" in
      if: "c2" = #11 then
        let: "b" := Snd "c1" in
        match: current_owner_code "g" with
          NONE => InjR #pure_scope_failure
        | SOME "o" =>
            match: graph_get_owner_code "g" "o" with
              NONE => #()
            | SOME "x" =>
                (if: Fst (Snd (Snd "x")) then
                   detached_code "g" "o" (λ: <>, "go" #17 "g" ("o", ("b", "e")));;
                   "go" #22 "g" #()
                 else
                   graph_put_owner_code "g" "o" (owner_with_cleanup_code "b" "e" "x"));;
                "go" #0 "g" ("e", "k")
            end
        end
      else if: "c2" = #12 then
        let: "n" := Snd "c1" in
        on_node_code "e" "n" (λ: "t",
          match: graph_get_node_code "g" "t" with
            NONE => #()
          | SOME "d" =>
              if: Fst (Fst (Fst (Fst "d"))) = #0 then #()
              else
                "go" #19 "g" "t";;
                "go" #0 "g" ("e", "k")
          end)
      else if: "c2" = #13 then
        let: "b" := Snd "c1" in
        let: "ctrl0" := graph_get_ctrl_code "g" in
        let: "bd0" := Fst (Snd (Fst (Fst "ctrl0"))) in
        graph_with_batch_depth_code "g" ("bd0" + #1);;
        let: "r" := "go" #0 "g" ("e", "b") in
        let: "ctrl1" := graph_get_ctrl_code "g" in
        let: "bd1" := Fst (Snd (Fst (Fst "ctrl1"))) in
        let: "bd1'" := if: #0 < "bd1" then "bd1" - #1 else #0 in
        graph_with_batch_depth_code "g" "bd1'";;
        match: "r" with
          InjL <> =>
            "go" #22 "g" #();;
            "go" #0 "g" ("e", "k")
        | InjR "y" => InjR "y"
        end
      else if: "c2" = #14 then
        let: "v" := Snd "c1" in
        on_value_code "e" "v" (λ: "z",
          log_label_code "g";;
          "go" #0 "g" ("e", "k"))
      else
        let: "c3" := Fst "c2" in
        if: "c3" = #2 then
          let: "x" := Snd "c2" in
          let: "v" := Snd "c1" in
          on_value_code "e" "v" (λ: "z",
            "go" #0 "g" (SOME (("x", "z"), "e"), "k"))
        else if: "c3" = #3 then
          let: "v" := Snd "c2" in
          let: "a" := Snd "c1" in
          let: "b" := "k" in
          on_value_code "e" "v" (λ: "z",
            if: "z" = #0 then "go" #0 "g" ("e", "b") else "go" #0 "g" ("e", "a"))
        else if: "c3" = #4 then
          let: "x" := Snd "c2" in
          let: "n" := Snd "c1" in
          on_node_code "e" "n" (λ: "t",
            let: "o" := "go" #1 "g" ("t", #true) in
            note_host_read_code "g" "t" "o";;
            match: "o" with
              InjL "v" => "go" #0 "g" (SOME (("x", "v"), "e"), "k")
            | InjR "y" => InjR "y"
            end)
        else if: "c3" = #6 then
          let: "x" := Snd "c2" in
          let: "n" := Snd "c1" in
          on_node_code "e" "n" (λ: "t",
            let: "o" := "go" #1 "g" ("t", #false) in
            note_host_read_code "g" "t" "o";;
            match: "o" with
              InjL "v" => "go" #0 "g" (SOME (("x", "v"), "e"), "k")
            | InjR "y" => InjR "y"
            end)
        else if: "c3" = #7 then
          let: "n" := Snd "c2" in
          let: "v" := Snd "c1" in
          on_node_code "e" "n" (λ: "t",
            on_value_code "e" "v" (λ: "z",
              "go" #21 "g" ("t", "z");;
              "go" #0 "g" ("e", "k")))
        else if: "c3" = #8 then
          let: "x" := Snd "c2" in
          let: "v" := Snd "c1" in
          on_value_code "e" "v" (λ: "z",
            let: "n" := next_id_code "g" in
            add_node_code "g" "n" (signal_node_code "z");;
            "go" #0 "g" (SOME (("x", "n"), "e"), "k"))
        else if: "c3" = #10 then
          let: "x" := Snd "c2" in
          let: "b" := Snd "c1" in
          let: "n" := next_id_code "g" in
          let: "e'" := SOME (("x", "n"), "e") in
          add_node_code "g" "n" (effect_node_code "b" "e'");;
          match: current_owner_code "g" with
            NONE => InjR #pure_scope_failure
          | SOME "o" =>
              "go" #20 "g" ("n", "o");;
              mark_dirty_code "g" "n";;
              "go" #22 "g" #();;
              "go" #0 "g" ("e'", "k")
          end
        else
          let: "c4" := Fst "c3" in
          if: "c4" = #5 then
            let: "x" := Snd "c3" in
            let: "ok" := Snd "c2" in
            let: "n" := Snd "c1" in
            on_node_code "e" "n" (λ: "t",
              let: "o" := "go" #1 "g" ("t", #true) in
              note_host_read_code "g" "t" "o";;
              match: "o" with
                InjL "v" => "go" #0 "g" (SOME (("ok", #1), SOME (("x", "v"), "e")), "k")
              | InjR "y" => "go" #0 "g" (SOME (("ok", #0), SOME (("x", "y"), "e")), "k")
              end)
          else
            let: "x" := Snd "c4" in
            let: "m" := Snd "c3" in
            let: "q" := Snd "c2" in
            let: "b" := Snd "c1" in
            let: "n" := next_id_code "g" in
            let: "e'" := SOME (("x", "n"), "e") in
            add_node_code "g" "n" (memo_node_code "m" "q" "b" "e'");;
            match: current_owner_code "g" with
              NONE => InjR #pure_scope_failure
            | SOME "o" =>
                "go" #20 "g" ("n", "o");;
                "go" #0 "g" ("e'", "k")
            end.

Lemma exec_step_spec go f (Hes : engine_spec go f) γ g e c s s' r :
  exec (S f) e c s = Some (s', r) ->
  {{{ is_graph γ g s }}} exec_step go g (env_val e, compile_cmd c)%V {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  cbn [exec] in H.
  destruct c as [x|y|x v k|v a b|x n k|x ok n k|x n k|n v k|x v k|x m q b k|x b k|b k|n k|b k|v k];
    simpl; wp_pures.
  - (* CRet *)
    wp_apply (on_value_spec _ _ e x _ _ _ _ _ H with "Hg"); [|done].
    intros z Hz. apply m_return_Some in Hz as [-> ->].
    iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures. by iApply "HΨ".
  - (* CThrow *)
    apply m_return_Some in H as [-> ->]. by iApply "HΦ".
  - (* CLet *)
    wp_apply (on_value_spec _ _ e v _ _ _ _ _ H with "Hg"); [|done].
    intros z Hz. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    change (SOMEV ((#(Z.of_nat x), #z)%V, env_val e)) with (env_val ((x, z) :: e)).
    by wp_apply (es_exec _ _ Hes _ _ ((x, z) :: e) k _ _ _ Hz with "Hg").
  - (* CIf *)
    wp_apply (on_value_spec _ _ e v _ _ _ _ _ H with "Hg"); [|done].
    intros z Hz. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    destruct (decide (z = 0%Z)) as [->|Hne].
    + rewrite (bool_decide_eq_true_2 (#0 = #0)%V) //. wp_pures.
      by wp_apply (es_exec _ _ Hes _ _ e b _ _ _ Hz with "Hg").
    + rewrite (bool_decide_eq_false_2 (#z = #0)%V); [|intros [= ?]; done]. wp_pures.
      rewrite decide_False in Hz; [|done].
      by wp_apply (es_exec _ _ Hes _ _ e a _ _ _ Hz with "Hg").
  - (* CRead *)
    wp_apply (on_node_spec _ _ e n _ _ _ _ _ H with "Hg"); [|done].
    intros t Ht. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    apply m_bind_Some in Ht as (s1 & o & Hrd & Ht).
    apply m_bind_Some in Ht as (s2 & [] & Hnh & Ht).
    wp_apply (es_read _ _ Hes _ _ t true _ _ _ Hrd with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (note_host_read_spec _ _ t o _ _ Hnh with "Hg"). iIntros "Hg".
    destruct o as [v|y]; simpl; wp_pures.
    + change (SOMEV ((#(Z.of_nat x), #v)%V, env_val e)) with (env_val ((x, v) :: e)).
      by wp_apply (es_exec _ _ Hes _ _ ((x, v) :: e) k _ _ _ Ht with "Hg").
    + apply m_return_Some in Ht as [-> ->]. by iApply "HΨ".
  - (* CTryRead *)
    wp_apply (on_node_spec _ _ e n _ _ _ _ _ H with "Hg"); [|done].
    intros t Ht. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    apply m_bind_Some in Ht as (s1 & o & Hrd & Ht).
    apply m_bind_Some in Ht as (s2 & [] & Hnh & Ht).
    wp_apply (es_read _ _ Hes _ _ t true _ _ _ Hrd with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (note_host_read_spec _ _ t o _ _ Hnh with "Hg"). iIntros "Hg".
    destruct o as [v|y]; simpl; wp_pures.
    + change (SOMEV ((#(Z.of_nat ok), #1)%V, SOMEV ((#(Z.of_nat x), #v)%V, env_val e)))
        with (env_val ((ok, 1%Z) :: (x, v) :: e)).
      by wp_apply (es_exec _ _ Hes _ _ ((ok, 1%Z) :: (x, v) :: e) k _ _ _ Ht with "Hg").
    + change (SOMEV ((#(Z.of_nat ok), #0)%V, SOMEV ((#(Z.of_nat x), #y)%V, env_val e)))
        with (env_val ((ok, 0%Z) :: (x, y) :: e)).
      by wp_apply (es_exec _ _ Hes _ _ ((ok, 0%Z) :: (x, y) :: e) k _ _ _ Ht with "Hg").
  - (* CReadUntracked *)
    wp_apply (on_node_spec _ _ e n _ _ _ _ _ H with "Hg"); [|done].
    intros t Ht. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    apply m_bind_Some in Ht as (s1 & o & Hrd & Ht).
    apply m_bind_Some in Ht as (s2 & [] & Hnh & Ht).
    wp_apply (es_read _ _ Hes _ _ t false _ _ _ Hrd with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (note_host_read_spec _ _ t o _ _ Hnh with "Hg"). iIntros "Hg".
    destruct o as [v|y]; simpl; wp_pures.
    + change (SOMEV ((#(Z.of_nat x), #v)%V, env_val e)) with (env_val ((x, v) :: e)).
      by wp_apply (es_exec _ _ Hes _ _ ((x, v) :: e) k _ _ _ Ht with "Hg").
    + apply m_return_Some in Ht as [-> ->]. by iApply "HΨ".
  - (* CWrite *)
    wp_apply (on_node_spec _ _ e n _ _ _ _ _ H with "Hg"); [|done].
    intros t Ht. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    wp_apply (on_value_spec _ _ e v _ _ _ _ _ Ht with "Hg"); [|done].
    intros z Hz. iIntros (Ψ') "Hg HΨ'". wp_lam. wp_pures.
    apply m_bind_Some in Hz as (s1 & [] & Hws & Hz).
    wp_apply (es_write_signal _ _ Hes _ _ t z _ _ Hws with "Hg"). iIntros "Hg". wp_pures.
    by wp_apply (es_exec _ _ Hes _ _ e k _ _ _ Hz with "Hg").
  - (* CSignal *)
    wp_apply (on_value_spec _ _ e v _ _ _ _ _ H with "Hg"); [|done].
    intros z Hz. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
    apply m_bind_Some in Hz as (s1 & nid & Hnid & Hz).
    injection Hnid as <- <-.
    apply m_bind_Some in Hz as (s2 & [] & Hadd & Hz).
    injection Hadd as <-.
    wp_apply (next_id_spec with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (signal_node_spec z); [done|]. iIntros "_".
    wp_apply (add_node_spec with "Hg"). iIntros "Hg". wp_pures.
    change (SOMEV ((#(Z.of_nat x), #(Z.of_nat (S (g_next_id s))))%V, env_val e))
      with (env_val ((x, Z.of_nat (S (g_next_id s))) :: e)).
    by wp_apply (es_exec _ _ Hes _ _ ((x, Z.of_nat (S (g_next_id s))) :: e) k _ _ _ Hz with "Hg").
  - (* CMemo *)
    apply m_bind_Some in H as (s1 & nid & Hnid & H). injection Hnid as <- <-.
    apply m_bind_Some in H as (s2 & [] & Hadd & H). injection Hadd as <-.
    apply m_bind_Some in H as (s3 & owner & Hco & H).
    wp_apply (next_id_spec with "Hg"). iIntros "Hg". wp_pures.
    set (e' := (x, Z.of_nat (S (g_next_id s))) :: e) in *.
    change (SOMEV ((#(Z.of_nat x), #(Z.of_nat (S (g_next_id s))))%V, env_val e)) with (env_val e').
    wp_apply (memo_node_spec m q b e'); [done|]. iIntros "_".
    wp_apply (add_node_spec with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (current_owner_spec _ _ _ _ _ Hco with "Hg"). iIntros "Hg".
    destruct owner as [o|]; simpl; wp_pures.
    + apply m_bind_Some in H as (s4 & [] & Hatt & H).
      wp_apply (es_attach _ _ Hes _ _ (S (g_next_id s)) o _ _ Hatt with "Hg"). iIntros "Hg". wp_pures.
      by wp_apply (es_exec _ _ Hes _ _ e' k _ _ _ H with "Hg").
    + apply m_return_Some in H as [-> ->]. by iApply "HΦ".
  - (* CEffect *)
    apply m_bind_Some in H as (s1 & nid & Hnid & H). injection Hnid as <- <-.
    apply m_bind_Some in H as (s2 & [] & Hadd & H). injection Hadd as <-.
    apply m_bind_Some in H as (s3 & owner & Hco & H).
    wp_apply (next_id_spec with "Hg"). iIntros "Hg". wp_pures.
    set (e' := (x, Z.of_nat (S (g_next_id s))) :: e) in *.
    change (SOMEV ((#(Z.of_nat x), #(Z.of_nat (S (g_next_id s))))%V, env_val e)) with (env_val e').
    wp_apply (effect_node_spec b e'); [done|]. iIntros "_".
    wp_apply (add_node_spec with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (current_owner_spec _ _ _ _ _ Hco with "Hg"). iIntros "Hg".
    destruct owner as [o|]; simpl; wp_pures.
    + apply m_bind_Some in H as (s4 & [] & Hatt & H).
      apply m_bind_Some in H as (s5 & [] & Hmd & H). apply m_step_Some in Hmd as [Hmd _].
      apply m_bind_Some in H as (s6 & [] & Hrf & H).
      wp_apply (es_attach _ _ Hes _ _ (S (g_next_id s)) o _ _ Hatt with "Hg"). iIntros "Hg". wp_pures.
      wp_apply (mark_dirty_spec _ _ f (S (g_next_id s)) _ _ Hmd with "Hg"). iIntros "Hg". wp_pures.
      wp_apply (es_request_flush _ _ Hes _ _ _ _ Hrf with "Hg"). iIntros "Hg". wp_pures.
      by wp_apply (es_exec _ _ Hes _ _ e' k _ _ _ H with "Hg").
    + apply m_return_Some in H as [-> ->]. by iApply "HΦ".
  - (* CCleanup *)
    iApply wp_fupd.
    apply m_bind_Some in H as (s1 & owner & Hco & H).
    wp_apply (current_owner_spec _ _ _ _ _ Hco with "Hg"). iIntros "Hg".
    destruct owner as [o|]; simpl; wp_pures.
    + apply m_bind_Some in H as (s2 & x & Hxo & H). apply get_owner_Some in Hxo as (Hxo & ->).
      apply m_bind_Some in H as (s3 & [] & Hreg & H). apply m_modify_Some in Hreg as [-> _].
      apply m_bind_Some in H as (s4 & [] & Hcl_step & H).
      wp_apply (graph_get_owner_spec with "Hg"). iIntros "Hg".
      rewrite Hxo /=. wp_pures.
      iMod (ghost_registered_step _ _ (o_host x) with "Hg") as "Hg".
      wp_bind (If #(o_disposed x) _ _).
      iApply (wp_wand _ _ _ (fun v => ⌜v = #()⌝ ∗ is_graph γ g s4)%I with "[Hg]").
      { destruct (o_disposed x); wp_pures.
        - apply m_bind_Some in Hcl_step as (s31 & [] & Hdet & Hrf).
          wp_apply (detached_spec _ _ o (run_cleanup f o b e) _ _ _ Hdet with "Hg").
          { intros s11 s12 Hs11. iIntros (Ψ) "Hg HΨ". wp_lam. wp_pures.
            by wp_apply (es_run_cleanup _ _ Hes _ _ o b e _ _ Hs11 with "Hg"). }
          iIntros "Hg". wp_pures.
          wp_apply (es_request_flush _ _ Hes _ _ _ _ Hrf with "Hg"). iIntros "Hg". by iFrame.
        - apply put_owner_Some in Hcl_step as [-> _].
          wp_apply (owner_with_cleanup_spec b e x); [done|]. iIntros "_".
          wp_apply (graph_put_owner_spec _ _ _ o (owner_with_cleanup b e x) with "Hg"). iIntros "Hg". by iFrame. }
      iIntros (v) "[-> Hg]". wp_pures.
      wp_apply (es_exec _ _ Hes _ _ e k _ _ _ H with "Hg"). iIntros "Hg". iModIntro. by iApply "HΦ".
    + apply m_return_Some in H as [-> ->]. iModIntro. by iApply "HΦ".
  - (* CDispose *)
    wp_apply (on_node_spec _ _ e n _ _ _ _ _ H with "Hg"); [|done].
    intros t Ht. iIntros (Ψ) "Hg HΨ". iApply wp_fupd. wp_lam. wp_pures.
    apply m_bind_Some in Ht as (s1 & d & Hd & Ht). apply get_node_Some in Hd as (Hd & _ & ->).
    wp_apply (graph_get_node_spec with "Hg"). iIntros "Hg".
    rewrite Hd /=. wp_pures.
    destruct (n_kind d); [done| |]; wp_pures;
      apply m_bind_Some in Ht as (s2 & [] & Hdisp & Ht);
      apply m_bind_Some in Ht as (s3 & [] & Hemit & Ht);
      wp_apply (es_dispose_node _ _ Hes _ _ t _ _ Hdisp with "Hg"); iIntros "Hg"; wp_pures;
      (destruct (n_disposed d);
       [ apply m_return_Some in Hemit as [-> _]
       | apply m_modify_Some in Hemit as [-> _];
         iMod (emit_step _ _ (EvDispose t) with "Hg") as "Hg" ]);
      wp_apply (es_exec _ _ Hes _ _ e k _ _ _ Ht with "Hg"); iIntros "Hg"; iModIntro; by iApply "HΨ".
  - (* CBatch *)
    apply m_bind_Some in H as (s1 & [] & Hbd1 & H). apply m_modify_Some in Hbd1 as [-> _].
    apply m_bind_Some in H as (s2 & rb & Hexec_b & H).
    apply m_bind_Some in H as (s3 & [] & Hbd2 & H). apply m_modify_Some in Hbd2 as [-> _].
    wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
    rewrite (_ : (Z.of_nat (g_batch_depth s) + 1)%Z = Z.of_nat (S (g_batch_depth s))); [|lia].
    wp_apply (graph_with_batch_depth_spec _ _ _ (S (g_batch_depth s)) with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (es_exec _ _ Hes _ _ e b _ _ _ Hexec_b with "Hg"). iIntros "Hg". wp_pures.
    wp_apply (graph_get_ctrl_spec with "Hg"). iIntros "Hg". wp_pures.
    wp_bind (If _ _ _).
    iApply (wp_wand _ _ _ (fun v => ⌜v = #(Z.of_nat (pred (g_batch_depth s2)))⌝)%I).
    { destruct (decide (0 < Z.of_nat (g_batch_depth s2))%Z) as [Hlt|Hge].
      - rewrite (bool_decide_eq_true_2 (0 < Z.of_nat (g_batch_depth s2))%Z) //; wp_pures; iPureIntro; do 2 f_equal; lia.
      - rewrite (bool_decide_eq_false_2 (0 < Z.of_nat (g_batch_depth s2))%Z) //; wp_pures; iPureIntro; do 2 f_equal; lia. }
    iIntros (v) "->". wp_pures.
    wp_apply (graph_with_batch_depth_spec _ _ _ (pred (g_batch_depth s2)) with "Hg"). iIntros "Hg". wp_pures.
    destruct rb as [vb|yb]; simpl; wp_pures.
    + apply m_bind_Some in H as (s4 & [] & Hrf & H).
      wp_apply (es_request_flush _ _ Hes _ _ _ _ Hrf with "Hg"). iIntros "Hg". wp_pures.
      by wp_apply (es_exec _ _ Hes _ _ e k _ _ _ H with "Hg").
    + apply m_return_Some in H as [-> ->]. by iApply "HΦ".
  - (* CLog *)
    wp_apply (on_value_spec _ _ e v _ _ _ _ _ H with "Hg"); [|done].
    intros z Hz. iIntros (Ψ) "Hg HΨ". iApply wp_fupd. wp_lam. wp_pures.
    apply m_bind_Some in Hz as (s1 & l & Hll & Hz).
    apply m_bind_Some in Hz as (s2 & [] & Hemit & Hz). apply m_modify_Some in Hemit as [-> _].
    wp_apply (log_label_spec _ _ _ _ _ Hll with "Hg"). iIntros "Hg". wp_pures.
    iMod (emit_step _ _ (EvLog l z) with "Hg") as "Hg".
    wp_apply (es_exec _ _ Hes _ _ e k _ _ _ Hz with "Hg"). iIntros "Hg". iModIntro. by iApply "HΨ".
Qed.
Global Opaque exec_step.

(** ** Mutual recursive engine dispatcher and top-level specifications *)

Definition engine_dispatch : val :=
  rec: "go" "tag" "g" "arg" :=
    if: "tag" = #0 then exec_step "go" "g" "arg"
    else if: "tag" = #1 then read_step "go" "g" "arg"
    else if: "tag" = #2 then read_node_step "go" "g" "arg"
    else if: "tag" = #3 then update_if_necessary_step "go" "g" "arg"
    else if: "tag" = #4 then refresh_or_pull_step "go" "g" "arg"
    else if: "tag" = #5 then pull_step "go" "g" "arg"
    else if: "tag" = #6 then refresh_step "go" "g" "arg"
    else if: "tag" = #7 then resolve_walk_step "go" "g" "arg"
    else if: "tag" = #8 then recompute_step "go" "g" "arg"
    else if: "tag" = #9 then run_memo_step "go" "g" "arg"
    else if: "tag" = #10 then execute_effect_step "go" "g" "arg"
    else if: "tag" = #11 then run_effect_step "go" "g" "arg"
    else if: "tag" = #12 then run_effect_body_step "go" "g" "arg"
    else if: "tag" = #13 then discharge_step "go" "g" "arg"
    else if: "tag" = #14 then retire_step "go" "g" "arg"
    else if: "tag" = #15 then dispose_scope_step "go" "g" "arg"
    else if: "tag" = #16 then teardown_step "go" "g" "arg"
    else if: "tag" = #17 then run_cleanup_step "go" "g" "arg"
    else if: "tag" = #18 then release_step "go" "g" "arg"
    else if: "tag" = #19 then dispose_node_step "go" "g" "arg"
    else if: "tag" = #20 then attach_step "go" "g" "arg"
    else if: "tag" = #21 then write_signal_step "go" "g" "arg"
    else if: "tag" = #22 then request_flush_step "go" "g" "arg"
    else if: "tag" = #23 then settle_flush_step "go" "g" "arg"
    else if: "tag" = #24 then run_flush_step "go" "g" "arg"
    else drain_step "go" "g" "arg".

Lemma engine_spec_all (f : nat) : engine_spec engine_dispatch f.
Proof.
  induction f as [|f IH].
  - split; intros; discriminate.
  - split; intros; iIntros "Hg HΦ"; wp_rec; wp_pures.
    + by wp_apply (exec_step_spec _ f IH with "Hg").
    + by wp_apply (read_step_spec _ f IH with "Hg").
    + by wp_apply (read_node_step_spec _ f IH with "Hg").
    + by wp_apply (update_if_necessary_step_spec _ f IH with "Hg").
    + by wp_apply (refresh_or_pull_step_spec _ f IH with "Hg").
    + by wp_apply (pull_step_spec _ f IH with "Hg").
    + by wp_apply (refresh_step_spec _ f IH with "Hg").
    + by wp_apply (resolve_walk_step_spec _ f IH with "Hg").
    + by wp_apply (recompute_step_spec _ f IH with "Hg").
    + by wp_apply (run_memo_step_spec _ f IH with "Hg").
    + by wp_apply (execute_effect_step_spec _ f IH with "Hg").
    + by wp_apply (run_effect_step_spec _ f IH with "Hg").
    + by wp_apply (run_effect_body_step_spec _ f IH with "Hg").
    + by wp_apply (discharge_step_spec _ f IH with "Hg").
    + by wp_apply (retire_step_spec _ f IH with "Hg").
    + by wp_apply (dispose_scope_step_spec _ f IH with "Hg").
    + by wp_apply (teardown_step_spec _ f IH with "Hg").
    + by wp_apply (run_cleanup_step_spec _ f IH with "Hg").
    + by wp_apply (release_step_spec _ f IH with "Hg").
    + by wp_apply (dispose_node_step_spec _ f IH with "Hg").
    + by wp_apply (attach_step_spec _ f IH with "Hg").
    + by wp_apply (write_signal_step_spec _ f IH with "Hg").
    + by wp_apply (request_flush_step_spec _ f IH with "Hg").
    + by wp_apply (settle_flush_step_spec _ f IH with "Hg").
    + by wp_apply (run_flush_step_spec _ f IH with "Hg").
    + by wp_apply (drain_step_spec _ f IH with "Hg").
Qed.
Global Opaque engine_dispatch.

Definition exec_code : val := λ: "g" "e" "c", engine_dispatch #0 "g" ("e", "c").
Definition read_code : val := λ: "g" "n" "tr", engine_dispatch #1 "g" ("n", "tr").
Definition read_node_code : val := λ: "g" "n", engine_dispatch #2 "g" "n".
Definition update_if_necessary_code : val := λ: "g" "n", engine_dispatch #3 "g" "n".
Definition refresh_or_pull_code : val := λ: "g" "n", engine_dispatch #4 "g" "n".
Definition pull_code : val := λ: "g" "n", engine_dispatch #5 "g" "n".
Definition refresh_code : val := λ: "g" "n", engine_dispatch #6 "g" "n".
Definition resolve_walk_code : val := λ: "g" "n" "i", engine_dispatch #7 "g" ("n", "i").
Definition recompute_code : val := λ: "g" "n", engine_dispatch #8 "g" "n".
Definition run_memo_code : val := λ: "g" "n", engine_dispatch #9 "g" "n".
Definition execute_effect_code : val := λ: "g" "n", engine_dispatch #10 "g" "n".
Definition run_effect_code : val := λ: "g" "n", engine_dispatch #11 "g" "n".
Definition run_effect_body_code : val := λ: "g" "n", engine_dispatch #12 "g" "n".
Definition discharge_code : val := λ: "g" "o", engine_dispatch #13 "g" "o".
Definition retire_code : val := λ: "g" "o", engine_dispatch #14 "g" "o".
Definition dispose_scope_code : val := λ: "g" "o", engine_dispatch #15 "g" "o".
Definition teardown_code : val := λ: "g" "o" "ls" "cs", engine_dispatch #16 "g" ("o", ("ls", "cs")).
Definition run_cleanup_code : val := λ: "g" "o" "b" "e", engine_dispatch #17 "g" ("o", ("b", "e")).
Definition release_code : val := λ: "g" "n", engine_dispatch #18 "g" "n".
Definition dispose_node_code : val := λ: "g" "n", engine_dispatch #19 "g" "n".
Definition attach_code : val := λ: "g" "n" "o", engine_dispatch #20 "g" ("n", "o").
Definition write_signal_code : val := λ: "g" "n" "v", engine_dispatch #21 "g" ("n", "v").
Definition request_flush_code : val := λ: "g", engine_dispatch #22 "g" #().
Definition settle_flush_code : val := λ: "g", engine_dispatch #23 "g" #().
Definition run_flush_code : val := λ: "g", engine_dispatch #24 "g" #().
Definition drain_code : val := λ: "g", engine_dispatch #25 "g" #().

Definition run_top_code (cv : val) : heap_lang.expr :=
  let: "g" := graph_new_code #() in
  exec_code "g" NONE cv.

Definition init_heap : heap_lang.state :=
  {| heap := ∅; used_proph_id := ∅ |}.

(**md
**`exec_code` refines `exec` over `is_graph` for every environment and command.**

- [γ:] the ghost name.
- [g:] the graph value.
- [e:] the environment.
- [c:] the command.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the returned outcome.
*)
Theorem exec_spec γ g (e : env) (c : cmd) (s : State.state) (fuel : nat) (s' : State.state) (r : outcome) :
  exec fuel e c s = Some (s', r) ->
  {{{ is_graph γ g s }}}
    exec_code g (env_val e) (compile_cmd c)
  {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_exec _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`read_code` refines `read` over `is_graph`, tracked or untracked.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the read node id.
- [tr:] `true` for a tracked read, `false` inside `Untrack`.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the read outcome.
*)
Theorem read_spec γ g (n : node_id) (tr : bool) (s : State.state) (fuel : nat) (s' : State.state) (r : outcome) :
  read fuel n tr s = Some (s', r) ->
  {{{ is_graph γ g s }}}
    read_code g #(Z.of_nat n) #tr
  {{{ RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_read _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`run_memo_code` refines `run_memo` (`Memo.Run`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the memo node id.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem run_memo_spec γ g (n : node_id) (s : State.state) (fuel : nat) (s' : State.state) :
  run_memo fuel n s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    run_memo_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_run_memo _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`execute_effect_code` refines `execute_effect` (`Effect.Execute`) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the effect node id.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem execute_effect_spec γ g (n : node_id) (s : State.state) (fuel : nat) (s' : State.state) :
  execute_effect fuel n s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    execute_effect_code g #(Z.of_nat n)
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_execute_effect _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`write_signal_code` refines `write_signal` (`Signal.Value` setter) over `is_graph`.**

- [γ:] the ghost name.
- [g:] the graph value.
- [n:] the signal node id.
- [v:] the written integer value.
- [s:] the initial pure state.
- [fuel:] the pure step budget.
- [s':] the final pure state.
*)
Theorem write_signal_spec γ g (n : node_id) (v : Z) (s : State.state) (fuel : nat) (s' : State.state) :
  write_signal fuel n v s = Some (s', tt) ->
  {{{ is_graph γ g s }}}
    write_signal_code g #(Z.of_nat n) #v
  {{{ RET #(); is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "Hg HΦ". wp_lam. wp_pures.
  by wp_apply (es_write_signal _ fuel (engine_spec_all fuel) with "Hg").
Qed.

(**md
**`run_top_code` allocates a fresh graph at `init current_flags` and runs `compile_cmd c` to `outcome_val r`.**

- [c:] the top-level command.
- [fuel:] the pure step budget.
- [s':] the final pure state.
- [r:] the returned outcome.
*)
Theorem run_top_spec (c : cmd) (fuel : nat) (s' : State.state) (r : outcome) :
  run_top fuel c (init current_flags) = Some (s', r) ->
  {{{ True }}}
    run_top_code (compile_cmd c)
  {{{ γ g, RET outcome_val r; is_graph γ g s' }}}.
Proof.
  intros H. iIntros (Φ) "_ HΦ". rewrite /run_top_code.
  wp_apply graph_new_spec; [done|]. iIntros (γ g) "Hg". wp_pures.
  change NONEV with (env_val []).
  wp_apply (exec_spec _ _ [] c (init current_flags) fuel _ _ H with "Hg").
  iIntros "Hg". by iApply ("HΦ" $! γ g).
Qed.

End graph_helpers.
