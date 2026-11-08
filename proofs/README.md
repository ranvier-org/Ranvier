# Ranvier core proofs

Rocq/Iris development over the synchronous reactive core in `src/Ranvier/Core.fs`, pinned at
commit `d701e6c5`.

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`

Both run from any directory. See `VERSIONS.md` for the toolchain.

## Layout

- `theories/Base/Prelude.v`: identifiers and the freshness order.
- `theories/Audit.v`: one `Print Assumptions` per theorem; `check.sh` reads it.
- `BUGS.md`: defect ledger. `CORRESPONDENCE.md`: model-to-F# map.

## Theorem index

None yet.
