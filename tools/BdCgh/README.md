# BdCgh (not verified on a disc)

True-wavefront CGH on BD-R with bit-level 17PP / DSV design: see `docs/techniques/bd-17pp-dsv-design.md` and `docs/discs/bd_holo1_bdcgh.md`.
`BdCgh --assets DIR --output bd.iso [--threads N] [--verify K] [--width 8] [--dry] [--min-gb 8.6]`; `DIR` needs `texture_rgba.png depth8.png white.png
flat_depth.png lines.png lines_depth.png`. `--dry` estimates the work (about 2.6 s per designed cluster per thread; a full test disc is a long run).
