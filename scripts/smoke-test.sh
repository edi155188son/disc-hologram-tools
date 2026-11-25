#!/usr/bin/env bash
# End-to-end smoke test of the BD chain on synthetic data (about 2 minutes on 2 cores, ~300 MB temp, CPU only):
#   python fields -> BdAmp (lee / double-phase / parity steering) -> parity verified with DIS' own LDC encoder
#   python bdopt (CPU) -> BDOPT1 -> BdAmp --opt-bits -> BdAmp chan -> python chio
# Needs: built BdAmp (scripts/build-all.sh), python3 with numpy + torch (CPU is fine).
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet || true)")" 2>/dev/null || true)}"
bdamp="${BDAMP:-$here/artifacts/bin/BdAmp/Release/net10.0/BdAmp}"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
geo=(--inner 23.987042157339335 --outer 57.987960341783236 --dark 9E --light 22)
rng=(--range-start 4821952 --range-clusters 700)   # sector range that crosses the synthetic block at r = 41 mm
python3 "$here/tests/make_test_bdaf.py" "$tmp"
python3 "$here/tests/check_geometry.py" "$bdamp"
for mode in "lee" "dp" "dp --steer-parity on --steer-verify 20"; do
  out="$("$bdamp" "${geo[@]}" "${rng[@]}" --encoding $mode --fields "$tmp/blk.bdaf,$tmp/lab.bdaf" --output "$tmp/o.bin")"
  case "$mode" in *steer*) echo "$out" | grep -E "constrained parity bytes match" | awk '{split($0,a,": "); split(a[2],b," "); split(b[1],c,"/"); if (c[1]!=c[2]||c[1]==0) {print "parity verify FAILED: " $0; exit 1} else print "parity verify OK: " $0}';; esac
  echo "mode [$mode]: $(sha256sum "$tmp/o.bin" | cut -c1-16)"; rm -f "$tmp/o.bin"
done
python3 "$here/python/bdopt.py" "${geo[@]:0:4}" --fields "$tmp/blk.bdaf" --out "$tmp/t.bdopt" --device cpu --iters 10 --limit-tiles 2 --tile 0.06 | tail -1
"$bdamp" "${geo[@]}" "${rng[@]}" --encoding dp --opt-bits "$tmp/t.bdopt" --fields "$tmp/blk.bdaf,$tmp/lab.bdaf" --steer-verify 20 --output "$tmp/o.bin" | grep -E "Optimized bytes applied|constrained parity bytes match"
"$bdamp" chan --inner 23.987042157339335 --outer 57.987960341783236 --start-sector 4821952 --data-file "$tmp/o.bin" --data-offset-sector 0 --clusters 4 --out "$tmp/t.bdch" | tail -1
(cd "$here/python" && python3 -c "from chio import read_ch; s,cb,i,l,e=read_ch('$tmp/t.bdch'); print('BDCH2 read: start',s,'channel bits/cluster',cb,'clusters',len(i),'levels',l.shape,'ecc',e.shape)")
echo "smoke test passed"
