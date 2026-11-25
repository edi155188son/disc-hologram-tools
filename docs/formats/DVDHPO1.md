# DVDHPO1: polar byte raster for DVD

Defined by `tools/DvdDiffractionRings/Polar.cs`; written by MATLAB (`holo_hpo_big.m`, `holo_lenti_big.m`,
`radial_app.m`, `avatar_fp.m`, `avatar_fill.m`: `fwrite(fid, ['DVDHPO1' 0], 'char'); fwrite(fid, 1, 'int32'); ...`).

```
0   8   char[8]  "DVDHPO1\0"
8   4   int32    version (1)
12  8   float64  r0      radius of the centre of row 0 (mm)
20  8   float64  dr      row pitch (mm)
28  8   float64  theta0  angle of column 0 (RADIANS, engine polar angle atan2(y, x) of the codeword position)
36  8   float64  dtheta  column pitch (radians)
44  4   int32    nrows
48  4   int32    ncols
52  nrows*ncols bytes, row-major (row = radius index, column = angle index)
```

Sampling (`Polar.Sampler`): a codeword at radius `r`, angle `th` uses row `floor((r - r0)/dr)` (must be inside
`r0 .. r0 + nrows dr`) and column `floor(((th - theta0) mod 2 pi) / dtheta)` (must be `< ncols`). With several
rasters (`--raster a.hpo,b.hpo`) the first one covering the codeword wins; otherwise pseudo-random.

Byte value:
* `0x00`: pseudo-random (no grating);
* `0x01..0x0F`: placeholder k: the k-th sequence of `--pairs` (e.g. `--pairs C5DB,CC67`), repeated along the track
  (index = global codeword index mod length) - used by the lenticular multi-view rasters;
* anything else: the **scrambled** byte Q written as-is (for example `92` bright, `A5` dark at 16T / 6.4T).
