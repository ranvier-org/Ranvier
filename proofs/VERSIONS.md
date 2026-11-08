# Toolchain

Platform: WSL, Ubuntu 24.04.4 LTS (distribution `Ubuntu`), opam 2.3.0 switch `ranvier-proofs`.
`build.sh` and `check.sh` re-execute themselves in that distribution when started from Git Bash;
set `RANVIER_WSL_DISTRO` to use another distribution name.

| Component | Version |
|---|---|
| OCaml | 4.14.2 (`ocaml-base-compiler`) |
| Rocq (`rocq-core`, `rocq-runtime`) | 9.1.0 |
| `rocq-stdlib` | 9.2.0 |
| stdpp (`rocq-stdpp`) | 1.13.0 |
| Iris (`rocq-iris`, `rocq-iris-heap-lang`) | 4.5.0 |
| zarith | 1.14 |

## Why WSL and not the Windows installer

Rocq Platform 2026.07.0 for Windows (Rocq 9.1.0, signed by INRIA) installs per user without
elevation, and the HeapLang smoke theorem compiles with it. It ships no `make`, which
`rocq makefile` requires, so `build.sh` cannot run there. WSL is the fallback the plan names.

## Reproducing the switch

The WSL account has no `sudo`. Three prerequisites were therefore installed in user space:
the `opam` binary, GMP 6.3.0 (headers for zarith, prefix `~/.local/gmp`), and a Python `unzip`
shim in `~/.local/bin` (opam requires an `unzip` command). `build.sh` exports the GMP paths
when `~/.local/gmp` exists.

```sh
export PATH=$HOME/.local/bin:$PATH
export CPATH=$HOME/.local/gmp/include LIBRARY_PATH=$HOME/.local/gmp/lib LD_LIBRARY_PATH=$HOME/.local/gmp/lib
export OPAMASSUMEDEPEXTS=1 OPAMYES=1
opam init --bare --disable-sandboxing --no-setup
opam switch create ranvier-proofs ocaml-base-compiler.4.14.2
eval "$(opam env --switch=ranvier-proofs)"
opam repo add rocq-released https://rocq-prover.org/opam/released
opam repo add iris-dev https://gitlab.mpi-sws.org/iris/opam.git
opam update
opam install -j16 rocq-core.9.1.0 rocq-stdlib rocq-iris-heap-lang.4.5.0
```

On a machine with `sudo`, install `libgmp-dev`, `unzip` and `bubblewrap` with apt and drop the
user-space workarounds and `--disable-sandboxing`.

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`
- Run a command in the toolchain environment: `bash proofs/build.sh --run CMD ARGS`

## Smoke theorem

Compiles under both the Windows install and the WSL switch:

```rocq
From iris.heap_lang Require Import proofmode notation.
Lemma smoke `{!heapGS Σ} (l : loc) : l ↦ #1 ⊢ WP !#l {{ v, ⌜v = #1⌝ ∗ l ↦ #1 }}.
Proof. iIntros "H". wp_load. iFrame. done. Qed.
```
