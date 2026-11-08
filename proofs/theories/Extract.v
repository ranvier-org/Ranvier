From Stdlib Require Extraction.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Engine.

(** Generates [engine.ml] and [engine.mli] in the working directory, [proofs/] under [build.sh]. [nat] and [Z]
    stay inductive, so the extracted interpreter computes as [vm_compute] does and adds no realised
    constant to what the differential check trusts. [proofs/differential/] holds the committed copy, and
    [check.sh] fails when the two differ. *)
Extraction Language OCaml.
Set Extraction Output Directory ".".

Extraction "engine.ml" trace_of current_flags.
