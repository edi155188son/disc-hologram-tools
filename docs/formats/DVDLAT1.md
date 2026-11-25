# DVDLAT1 (+ .val): codeword lattice of a DVD patch

Defined by `tools/DvdDiffractionRings/Lattice.cs`. `--export-lattice FILE --patch-r R --patch-angle A --patch-size S`
enumerates every channel codeword inside a square patch in the patch frame `(ur = radially outward, ut = +tangential,
mm from the patch centre)` using the calibrated spiral. MATLAB (`holo_design.m`, `holo_inverse.m`, `holo_image.m`,
`holo_points3d.m`, `holo_imageplane.m`, `holo_hpo*.m`, `label_values.m`) reads it, designs the pattern and writes a
values file; `--pattern lattice --lattice a.bin --values a.val` imports it.

```
0    8   char[8]  "DVDLAT1\0"
8    4   int32    version (1)
12   8   float64  patch centre radius (mm)
20   8   float64  patch centre angle (degrees)
28   8   float64  patch size (mm)
36   48  6 x float64  inner, outer, pitch-linear, pitch-quadratic, pitch-cubic, sectors   (the calibration used)
84   8   uint64   n   records
92   4   int32    number of tracks
96   n*8 uint64   idx    global codeword index
...  n*4 int32    track  track number within the patch (runs of indices; a gap > 5000 starts a new track)
...  n*4 float32  ur     (mm, patch frame)
...  n*4 float32  ut     (mm, patch frame)
...  n   uint8    ctrl   1 = codeword the solver can set (user data), 0 = interpolated gap (sync, IDs, parity)
```

Values file: exactly `n` bytes, one scrambled byte Q per record (ignored where `ctrl = 0`). On import the codeword
with that global index in a covered patch gets `Q`; everything else is pseudo-random. Roughly 15 % of the records are
uncontrollable format bytes.
