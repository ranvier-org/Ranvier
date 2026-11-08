#!/usr/bin/env bash
# Audits the Rocq project. Run as `bash proofs/check.sh` from any directory and any shell.
#
# Exits 0 when
#   1. the build is clean;
#   2. every `Print Assumptions` in theories/Audit.v lists only allowlisted assumptions;
#   3. `Admitted`, `admit` and `Axiom` occur only in declarations named under `## Open theorems`
#      in BUGS.md.
# Conformance regeneration is added with the conformance layer.
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
is_open() { open_theorems | grep -qxF -- "$1"; }
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
while IFS= read -r name; do
  is_allowed "$name" || note "assumption not allowlisted: $name"
done < <(awk '/^Axioms:/{a=1; next} /^Closed under/{a=0; next}
              a && /^[^[:space:]]/{ n=$1; print n }' "$out")

# 3. Admitted / admit / Axiom.
while IFS=$'\t' read -r loc name; do
  if [ -z "$name" ] || ! is_open "$name"; then
    note "$loc: Admitted/admit/Axiom in '${name:-<unknown declaration>}' is not listed under '## Open theorems' in BUGS.md"
  fi
done < <(
  for f in $(find theories -name '*.v' | sort); do
    awk -v file="$f" '
      /^[[:space:]]*(Local[[:space:]]+|Global[[:space:]]+)?(Lemma|Theorem|Corollary|Proposition|Fact|Remark|Definition|Instance|Example)[[:space:]]/ {
        t=$0; sub(/^[[:space:]]*((Local|Global)[[:space:]]+)?[A-Za-z]+[[:space:]]+/, "", t); sub(/[^A-Za-z0-9_'"'"'.].*$/, "", t); decl=t }
      {
        if (match($0, /(^|[^A-Za-z0-9_'"'"'])Axioms?([^A-Za-z0-9_'"'"']|$)/)) {
          t=$0; sub(/^.*Axioms?[[:space:]]+/, "", t); sub(/[^A-Za-z0-9_'"'"'.].*$/, "", t)
          printf "%s:%d\t%s\n", file, NR, t
        } else if ($0 ~ /(^|[^A-Za-z0-9_'"'"'])(Admitted|admit)([^A-Za-z0-9_'"'"']|$)/) {
          printf "%s:%d\t%s\n", file, NR, decl
        }
      }' "$f"
  done
)

if [ "$fail" -ne 0 ]; then
  echo "check.sh: FAILED" >&2
  exit 1
fi
echo "check.sh: OK"
