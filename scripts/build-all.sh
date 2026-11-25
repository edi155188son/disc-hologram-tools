#!/usr/bin/env bash
# Build every C# tool against $DIS_ROOT (default external/DiscImageStudio). Linux needs EnableWindowsTargeting
# for the DVD tools (DvdImageSolver targets net10.0-windows); it is set in the csproj files.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
export DIS_ROOT="${DIS_ROOT:-$here/external/DiscImageStudio}"
dotnet="${DOTNET:-dotnet}"
for p in tools/BdAmp tools/BdCgh tools/DvdDiffractionRings tools/HoloCgh/HoloCgh.csproj tools/HoloCgh/PhaseProbe experimental/BdHolo; do
  echo "== $p"; "$dotnet" build "$here/$p" -c Release --nologo -v q
done
