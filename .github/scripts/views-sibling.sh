#!/usr/bin/env bash
# Clones ranvier-org/Ranvier.Views beside this repository, if missing, and builds it against this core checkout.
# Usage: .github/scripts/views-sibling.sh [views-root]   (default: ../Ranvier.Views)
set -euo pipefail

# `pwd -W` gives the Windows form of the path under Git Bash, which MSBuild reads.
core="$(cd "$(dirname "$0")/../.." && (pwd -W 2>/dev/null || pwd))"
views="${1:-$core/../Ranvier.Views}"

if [ ! -d "$views" ]; then
  git clone --depth 1 https://github.com/ranvier-org/Ranvier.Views.git "$views"
fi

printf '<Project><PropertyGroup><UseLocalRanvier>true</UseLocalRanvier><RanvierRoot>%s</RanvierRoot></PropertyGroup></Project>\n' \
  "$core" > "$views/Local.Build.props"
