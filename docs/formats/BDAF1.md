# BDAF1: object field of one hologram block (BD)

Defined by `tools/BdAmp/Program.cs` (`Load`), written by `python/fields_gpu.py` (`f.write`) and MATLAB
(`color_avatar.m`, `avatar_bd.m`, `avatar_bd2.m`), read by `python/bdopt.py` (`load`, data offset 100).

```
offset size  type      field
0      8     char[8]   "BDAF1\0\0\0"
8      4     int32     nv      grid rows (along v, radial)
12     4     int32     nu      grid columns (along u, tangential)
16     4     int32     interpV see below
20     8     float64   v0      v coordinate of row 0 (mm, in the block frame)
28     8     float64   dv      row pitch (mm)
36     8     float64   u0      u coordinate of column 0 (mm)
44     8     float64   du      column pitch (mm)
52     8     float64   rc      block centre radius (mm, disc coordinates)
60     8     float64   thc     block centre polar angle (DEGREES, counter-clockwise, engine frame)
68     8     float64   halfV   half extent of the block along v (mm)
76     8     float64   halfU   half extent along u (mm)
84     8     float64   lambda  carrier period along v (mm); <= 0 means "density / label field" (no carrier)
92     8     float64   thr     amplitude threshold (Lee mode: |e| < thr is written dark); 0 for double-phase
100    8*nv*nu float32  data: for i in 0..nv-1, for j in 0..nu-1: re, im   (v-major, u fastest)
```

Total size `100 + 8 * nv * nu` bytes (a 31 x 31 mm block on a 1.5 um x 3 um grid is about 500 MB).

## Block frame
The block centre is at `(rc cos thc, rc sin thc)` in disc millimetres. `v` is the unit vector radially outward at
the centre, `u = +angle` direction (`u = -dx sin thc + dy cos thc`, `v = dx cos thc + dy sin thc`). Samples
outside `|v| <= halfV, |u| <= halfU` do not belong to the block; samples inside the block but outside the grid
are written as carrier with no field (double-phase) or dark/nothing (Lee/density).

## Field semantics
* `lambda > 0`: complex field `e(v,u)` normalised so that `|e| <= 1` is the amplitude of the +1 order. The carrier is
  `exp(i 2 pi v / lambda)`. A byte at (v,u) is
  * **Lee** (`--encoding lee`): bright iff `cos(2 pi v/lambda - arg e) > cos(pi D)`, `D = asin(min(|e|,1))/pi`
    (bright width proportional to amplitude; the disc also shows the picture).
  * **double-phase** (`--encoding dp`): neighbouring tracks alternate `arg e +/- acos|e|`; 50 % bright everywhere (no
    zero-order picture on the disc).
  * **sum** (`--encoding dp --combine sum`): all carrier fields covering the point are summed,
    `S = sum_c conj(e_c) exp(i 2 pi v/lambda_c)`, and `S` is double-phase encoded (colour / frequency multiplexing).
* `lambda <= 0`: label / density field: byte is the light byte where `|e| > 0.5`, otherwise dark.
* Bright bytes are `--light` (default `22`), dark bytes `--dark` (default `9E`), post-scrambler.

## interpV
`0` nearest row in v and linear in u; `1` bilinear; `2` bicubic (Catmull-Rom, in v and u). Only the `--combine sum`
path evaluates `2` as bicubic; the single-field `Sample` path treats every non-zero value as bilinear. Use
bicubic for fields sampled about twice above Nyquist.

## Rotated copies
A path of the form `file.bdaf@ANGLE` loads the same field with the block centre moved to polar angle `ANGLE` (degrees);
the stored `thc` is ignored (BdAmp `Load`, `bdopt.py` `load`).

## Fast header-only mode
With `--opt-bits` and without `--load-fields` BdAmp reads only the 100-byte header of carrier fields (block geometry);
the data come from the BDOPT1 file.
