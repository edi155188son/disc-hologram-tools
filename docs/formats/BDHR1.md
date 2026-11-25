# BDHR1: square raster in a patch frame (legacy BD path)

Defined by `experimental/BdHolo/Program.cs` (`Load`), written by `matlab/bd_bdhr_port/bd_disc1.m`.

```
0   8    char[8]  "BDHR1\0\0\0"
8   4    int32    N
12  8    float64  dx       cell size (mm)
20  8    float64  r        patch centre radius (mm)
28  8    float64  angle    patch centre angle (degrees)
36  N*N  bytes    raster, index = iu * N + it   (iu along the radial-outward axis, it along +tangential)
```

Patch half size is `N dx / 2`. Value `128` = per-byte pseudo-random (no grating); values `< 128` map to the dark byte
and `> 128` to the light byte through `BluRayRunLengthMapping` (255 / 0 in the files written by `bd_disc1.m`). The
patch frame samples are taken at the nominal physical position of every payload byte after LDC interleave / BIS.
Superseded by BDAF1 + BdAmp: the raster needs 1 um cells (huge for big blocks) and cannot resolve single tracks.
