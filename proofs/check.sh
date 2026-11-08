#!/usr/bin/env bash
# Audits the Rocq project. Run as `bash proofs/check.sh` from any directory and any shell.
#
# Exits 0 when
#   1. the build is clean;
#   2. every `Print Assumptions` in theories/Audit.v lists only allowlisted assumptions;
#   3. `Admitted`, `admit`, `give_up`, `Admit Obligations` and top-level `Axiom`, `Parameter` and
#      `Conjecture` occur only in declarations named under `## Open theorems` in BUGS.md.
#      A failure of the scan itself fails the check.
#   4. regenerating theories/Conformance/Expected.v from the F# leaves it unchanged. Under WSL this runs
#      the Windows `dotnet.exe` through interop, with a relative path resolved from the Windows view of
#      this directory. RANVIER_DOTNET names another dotnet. A missing dotnet fails the check.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(uname -s)" != "Linux" ]; then
  distro="${RANVIER_WSL_DISTRO:-Ubuntu}"
  wsl_here="$(MSYS_NO_PATHCONV=1 wsl.exe -d "$distro" -- wslpath -u "$(cygpath -m "$here")" | tr -d '\r')"
  exec env MSYS_NO_PATHCONV=1 wsl.exe -d "$distro" -- bash "$wsl_here/$(basename "${BASH_SOURCE[0]}")" "$@"
fi

cd "$here"

# Assumptions the audit accepts. Each entry needs a reason.
# (empty: the smoke theorem and the Iris/HeapLang imports bring none)
ALLOWED_ASSUMPTIONS=()

fail=0
note() { echo "check.sh: $*" >&2; fail=1; }

# Names listed as `- name` under `## Open theorems`.
open_theorems() {
  awk '/^## /{in_s = ($0 == "## Open theorems"); next}
       in_s && /^- /{ s=$0; sub(/^- +/, "", s); sub(/[ \t].*$/, "", s); gsub(/`/, "", s); print s }' BUGS.md
}
is_open() { local names; names="$(open_theorems)"; grep -qxF -- "$1" <<<"$names"; }
is_allowed() {
  local n="$1" a
  for a in "${ALLOWED_ASSUMPTIONS[@]+"${ALLOWED_ASSUMPTIONS[@]}"}"; do [ "$a" = "$n" ] && return 0; done
  is_open "$n"
}

# 1. Build.
bash ./build.sh || { echo "check.sh: build failed" >&2; exit 1; }

# 2. Assumptions.
out="$(mktemp)"
trap 'rm -f "$out"' EXIT
bash ./build.sh --run rocq compile -Q theories Ranvier theories/Audit.v >"$out" 2>&1 \
  || { cat "$out" >&2; echo "check.sh: Audit.v failed to compile" >&2; exit 1; }

expected="$(grep -cE '^[[:space:]]*Print Assumptions ' theories/Audit.v || true)"
closed="$(grep -c '^Closed under the global context' "$out" || true)"
opened="$(grep -c '^Axioms:' "$out" || true)"
if [ "$((closed + opened))" -ne "$expected" ]; then
  note "Audit.v has $expected Print Assumptions but the output has $((closed + opened)) results"
fi
# Names are the unindented `name : type` lines under `Axioms:`.
assumption_names="$(awk '/^Axioms:/{a=1; next} /^Closed under/{a=0; next}
              a && /^[^[:space:]]/{ n=$1; print n }' "$out")" \
  || { echo "check.sh: could not read the assumptions from the Audit.v output" >&2; exit 1; }
while IFS= read -r name; do
  [ -n "$name" ] || continue
  is_allowed "$name" || note "assumption not allowlisted: $name"
done <<<"$assumption_names"

# 3. Admitted / admit / give_up / Admit Obligations / Axiom / Parameter / Conjecture.
# Section `Variable` and `Hypothesis` are not scanned: they are named premises, not assumptions of the
# closed theorem. Each file is scanned on its own so that a failing tool fails the check.
scan_awk="$(cat <<'AWK'
function strip_prefix(s,   again) {
  sub(/^[[:space:]]+/, "", s)
  again = 1
  while (again) {
    again = 0
    if (match(s, /^#\[[^]]*\][[:space:]]*/)) { s = substr(s, RLENGTH + 1); again = 1 }
    else if (match(s, /^(Local|Global|Program|Polymorphic|Monomorphic|Private|Cumulative|NonCumulative)[[:space:]]+/)) {
      s = substr(s, RLENGTH + 1); again = 1 }
  }
  return s
}
function ident_after(s,   t) {
  t = s
  sub(/^[A-Za-z]+/, "", t)
  sub(/^[[:space:]]+/, "", t)
  sub(/[^A-Za-z0-9_'.].*$/, "", t)
  return t
}
{
  s = strip_prefix($0)
  if (s ~ /^(Lemma|Theorem|Corollary|Proposition|Fact|Remark|Definition|Example|Instance|Fixpoint|CoFixpoint|Record|Inductive|CoInductive|Variant|Structure|Class)([^A-Za-z0-9_']|$)/) {
    decl = ident_after(s)
  }
  if (s ~ /^(Parameters?|Conjectures?)([^A-Za-z0-9_']|$)/) {
    printf "%s:%d\t%s\n", file, NR, ident_after(s)
  } else if ($0 ~ /(^|[^A-Za-z0-9_'])Axioms?([^A-Za-z0-9_']|$)/) {
    t = $0; sub(/^.*Axioms?[[:space:]]+/, "", t); sub(/[^A-Za-z0-9_'.].*$/, "", t)
    printf "%s:%d\t%s\n", file, NR, t
  } else if ($0 ~ /(^|[^A-Za-z0-9_'])(Admitted|admit|give_up)([^A-Za-z0-9_']|$)/ ||
             $0 ~ /(^|[^A-Za-z0-9_'])Admit[[:space:]]+Obligations([^A-Za-z0-9_']|$)/) {
    printf "%s:%d\t%s\n", file, NR, decl
  }
}
AWK
)" || { echo "check.sh: could not load the scan program" >&2; exit 1; }

v_files="$(find theories -name '*.v' | sort)" \
  || { echo "check.sh: find failed while listing theories/*.v" >&2; exit 1; }
[ -n "$v_files" ] || { echo "check.sh: no .v files found under theories" >&2; exit 1; }

scan_hits=""
while IFS= read -r f; do
  file_hits="$(awk -v file="$f" "$scan_awk" "$f")" \
    || { echo "check.sh: scan of $f failed" >&2; exit 1; }
  [ -z "$file_hits" ] || scan_hits+="$file_hits"$'\n'
done <<<"$v_files"

while IFS=$'\t' read -r loc name; do
  [ -n "$loc" ] || continue
  if [ -z "$name" ] || ! is_open "$name"; then
    note "$loc: Admitted/admit/give_up/Admit Obligations/Axiom/Parameter/Conjecture in '${name:-<unknown declaration>}' is not listed under '## Open theorems' in BUGS.md"
  fi
done <<<"$scan_hits"

# 4. Conformance. The regenerated file sits beside the script, inside the checkout dotnet.exe can see.
find_dotnet() {
  if [ -n "${RANVIER_DOTNET:-}" ]; then echo "$RANVIER_DOTNET"; return 0; fi
  if command -v dotnet >/dev/null 2>&1; then command -v dotnet; return 0; fi
  if command -v dotnet.exe >/dev/null 2>&1; then command -v dotnet.exe; return 0; fi
  if [ -x "/mnt/c/Program Files/dotnet/dotnet.exe" ]; then echo "/mnt/c/Program Files/dotnet/dotnet.exe"; return 0; fi
  return 1
}
if dotnet_cmd="$(find_dotnet)"; then
  regen="$(mktemp -p conformance .Expected.XXXXXX)"
  trap 'rm -f "$out" "$regen"' EXIT
  if ! "$dotnet_cmd" build ../src/Ranvier -c Release -f net10.0 --nologo -v q >"$out" 2>&1; then
    cat "$out" >&2
    note "dotnet build of src/Ranvier failed"
  elif ! "$dotnet_cmd" fsi conformance/Scenarios.fsx "$regen" >"$out" 2>&1; then
    cat "$out" >&2
    note "conformance/Scenarios.fsx failed"
  elif ! diff -u theories/Conformance/Expected.v "$regen" >&2; then
    note "regenerating theories/Conformance/Expected.v changed it"
  fi
else
  note "no dotnet found, so theories/Conformance/Expected.v could not be regenerated"
fi

if [ "$fail" -ne 0 ]; then
  echo "check.sh: FAILED" >&2
  exit 1
fi
echo "check.sh: OK"
