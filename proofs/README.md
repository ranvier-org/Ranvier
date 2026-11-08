# Ranvier core proofs

Rocq/Iris development over the synchronous reactive core in `src/Ranvier/Core.fs`, pinned at
commit `d701e6c5`.

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`

Both run from any directory. See `VERSIONS.md` for the toolchain.

## Layout

- `theories/Base/Prelude.v`: identifiers and the freshness order.
- `theories/Base/Syntax.v`: the command language.
- `theories/Reference/Semantics.v`: the meaning of a program and an input history.
- `theories/Reference/Properties.v`: determinism, glitch freedom, redundancy, examples.
- `theories/Audit.v`: one `Print Assumptions` per theorem; `check.sh` reads it.
- `BUGS.md`: defect ledger. `CORRESPONDENCE.md`: model-to-F# map.

## Theorem index

| Theorem | File | Claim |
|---|---|---|
| `rtrace_deterministic` | `Reference/Properties.v` | Agreeing orders and any two sufficient fuels give one trace. |
| `memo_value_extensional` | `Reference/Properties.v` | A memo's value depends only on the signals its evaluation discovers. |
| `glitch_free_by_definition` | `Reference/Properties.v` | Each `EvLog` belongs to one run. A run that writes no signal has its reads and events computed from one signal snapshot and its cleanups' captured environments, which can hold values from an earlier snapshot. |
| `reference_run_redundant` | `Reference/Properties.v` | Witness: the reference tier has a redundant run when an effect restores a value mid-round. |
| `untracked_value_not_redundant` | `Reference/Properties.v` | A run whose untracked read moved is not redundant. |
| `untracked_branch_not_redundant` | `Reference/Properties.v` | A run whose reads are a subset of the previous run's, selected by an untracked read, is not redundant. |
| `zero_divisor_later_effects_run` | `Reference/Properties.v` | A zero divisor fails its effect, and later effects run. |
