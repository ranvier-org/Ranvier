#!/usr/bin/env bash
# The differential check: `bash proofs/differential/run.sh [count] [seed]`, from any directory and any shell. Generates
# programs, runs each on the F# core (Generate.fsx) and on the extracted interpreter (driver.ml), and compares the traces.
# Agreement is evidence that the model matches the F#, not a proof.
#
# Exits 0 when every compared program agrees, skipped programs are under 5%, and a run of 200 or more reaches every
# shape Generate.fsx requires. Otherwise prints the first differing program with both traces, and the seed.
#
# Needs dotnet and ocamlopt. Under WSL it runs the Windows dotnet.exe through interop, as check.sh does, and takes OCaml
# from the opam switch of VERSIONS.md. RANVIER_DOTNET names another dotnet. A missing tool fails the check.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(uname -s)" != "Linux" ]; then
  distro="${RANVIER_WSL_DISTRO:-Ubuntu}"
  wsl_here="$(MSYS_NO_PATHCONV=1 wsl.exe -d "$distro" -- wslpath -u "$(cygpath -m "$here")" | tr -d '\r')"
  exec env MSYS_NO_PATHCONV=1 wsl.exe -d "$distro" -- bash "$wsl_here/$(basename "${BASH_SOURCE[0]}")" "$@"
fi

count="${1:-200}"
seed="${2:-1}"
fuel="${RANVIER_DIFF_FUEL:-2000}"

if ! command -v ocamlopt >/dev/null 2>&1; then
  export PATH="$HOME/.local/bin:$PATH"
  if command -v opam >/dev/null 2>&1; then
    eval "$(opam env --switch=ranvier-proofs --set-switch)"
  fi
fi
command -v ocamlopt >/dev/null 2>&1 || { echo "run.sh: no ocamlopt found" >&2; exit 1; }

find_dotnet() {
  if [ -n "${RANVIER_DOTNET:-}" ]; then echo "$RANVIER_DOTNET"; return 0; fi
  if command -v dotnet >/dev/null 2>&1; then command -v dotnet; return 0; fi
  if command -v dotnet.exe >/dev/null 2>&1; then command -v dotnet.exe; return 0; fi
  if [ -x "/mnt/c/Program Files/dotnet/dotnet.exe" ]; then echo "/mnt/c/Program Files/dotnet/dotnet.exe"; return 0; fi
  return 1
}
dotnet_cmd="$(find_dotnet)" || { echo "run.sh: no dotnet found" >&2; exit 1; }

# Windows dotnet resolves a relative path from the Windows view of this directory, so everything stays inside the checkout.
cd "$here/.."
work="$(mktemp -d -p differential .run.XXXXXX)"
trap 'rm -rf "$work"' EXIT

"$dotnet_cmd" build ../src/Ranvier -c Release -f net10.0 --nologo -v q >"$work/build.log" 2>&1 \
  || { cat "$work/build.log" >&2; echo "run.sh: dotnet build of src/Ranvier failed" >&2; exit 1; }

cp differential/engine.ml differential/engine.mli differential/driver.ml "$work/"
(cd "$work" && ocamlopt -w -a engine.mli engine.ml driver.ml -o driver) \
  || { echo "run.sh: building the driver failed" >&2; exit 1; }

"$dotnet_cmd" fsi differential/Generate.fsx generate "$count" "$seed" "$work" | tr -d '\r' \
  || { echo "run.sh: Generate.fsx failed at seed $seed" >&2; exit 1; }
[ "${PIPESTATUS[0]}" -eq 0 ] || { echo "run.sh: Generate.fsx failed at seed $seed" >&2; exit 1; }

# The extracted interpreter recurses as deep as its fuel.
ulimit -s unlimited 2>/dev/null || ulimit -s "$(ulimit -Hs)"
"$work/driver" "$fuel" <"$work/programs.txt" >"$work/ocaml.txt" \
  || { echo "run.sh: the driver failed at seed $seed" >&2; exit 1; }

set +e
awk -F '\t' -v seed="$seed" -v count="$count" -v fuel="$fuel" -v programs="$work/programs.txt" '
function program(i,   line, n) {
  n = 0
  while ((getline line < programs) > 0) { if (n == i) { close(programs); return line } n++ }
  close(programs); return "<absent>"
}
function differ(i, why, f, o) {
  printf "run.sh: %s at seed %d, program %d\nprogram: %s\nF#:      %s\nmodel:   %s\n", why, seed, i, program(i), f, o > "/dev/stderr"
  failed = 1
  exit 1
}
NR == FNR { fstatus[$1] = $2; ftrace[$1] = $3; fsharp++; next }
{
  i = $1; ostatus = $2; otrace = $3; ocaml++
  if (!(i in fstatus)) differ(i, "no F# result", "<absent>", otrace)
  s = fstatus[i]
  if (s == "harness") differ(i, "the F# harness failed", ftrace[i], otrace)
  if (s == "excluded") { excluded++; next }
  if (s == "stuck") {
    if (ostatus != "none") differ(i, "F# is stuck and the model is not", "<stuck>", otrace)
    stuck++; next
  }
  if (s == "budget") { budget++; if (ostatus == "ok") budget_only++; next }
  if (ostatus == "none") { fuel_out++; next }
  if (ftrace[i] != otrace) differ(i, "the traces differ", ftrace[i], otrace)
  compared++
}
END {
  if (failed) exit 1
  if (fsharp != count + 0 || ocaml != count + 0) {
    printf "run.sh: expected %d results, F# wrote %d and the driver %d\n", count, fsharp, ocaml > "/dev/stderr"
    exit 1
  }
  skipped = fuel_out + budget + stuck
  printf "seed %d, fuel %d: %d programs compared and agree (evidence, not a proof)\n", seed, fuel, compared
  printf "skipped: %d (%d with the model out of fuel or stuck and the F# finished, %d past the F# budget of which %d the model finished, %d stuck on both sides)\n", skipped, fuel_out, budget, budget_only, stuck
  printf "excluded: %d programs raising more than one zero divisor\n", excluded
  if (skipped * 20 >= count) {
    printf "run.sh: %d of %d skipped, 5%% or more, at seed %d: the generator bounds are too loose\n", skipped, count, seed > "/dev/stderr"
    exit 1
  }
  if (compared == 0) { print "run.sh: no program compared" > "/dev/stderr"; exit 1 }
}' "$work/fsharp.txt" "$work/ocaml.txt"
verdict=$?
set -e

tr -d '\r' <"$work/stats.txt"
if [ -s "$work/missing.txt" ]; then
  echo "run.sh: at seed $seed the programs did not reach: $(tr -d '\r' <"$work/missing.txt")" >&2
  verdict=1
fi
exit "$verdict"
