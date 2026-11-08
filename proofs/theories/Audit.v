From Ranvier.Base Require Import Prelude.
From Ranvier.Reference Require Import Properties.

(** One [Print Assumptions] per theorem; [check.sh] compares the output with its allowlist. *)

Print Assumptions fresh_le_dec.
Print Assumptions rtrace_deterministic.
Print Assumptions memo_value_extensional.
Print Assumptions glitch_free_by_definition.
Print Assumptions reference_run_redundant.
Print Assumptions untracked_value_not_redundant.
Print Assumptions untracked_branch_not_redundant.
Print Assumptions zero_divisor_later_effects_run.
Print Assumptions obs_settled_coarser.
Print Assumptions refines_full_implies_settled.
Print Assumptions reference_refines_itself.
Print Assumptions silent_engine_not_refines.
Print Assumptions none_engine_refines.
Print Assumptions none_engine_not_answers.
Print Assumptions failing_round_later_rounds_run.
Print Assumptions dispose_runs_cleanups_once.
Print Assumptions redundant_run_settled_equal.
Print Assumptions redundant_run_full_differs.
Print Assumptions disposed_memo_answers_last_value.
Print Assumptions order_varies_by_round.
