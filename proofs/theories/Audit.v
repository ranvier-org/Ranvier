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
