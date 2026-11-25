# BD-R: which bytes give contrast (9E / 22)

DIS's default BD image mapping uses AA (3T-rich) for dark and 44 (6T-rich) for light, chosen by simulated run counts.
On a real Verbatim BD-R (CMCMAG-BA5, 532 nm laser) that pair gave only weak reflectance contrast:

| Disc | Result |
|---|---|
| `bd1_verbatim` (AA/44, rasters from `bd_disc1.m`) | square frames fine, no holographic imaging, large scattering patch around the zero order |
| `bd2_probes` (AA/44 gratings) | no diffraction spot for any radial / tangential / spoke grating |
| `bd3_palette` (radial gratings 16 um, 7 byte pairs) | P1 AA/44, P2 9E/44, P3 9E/AA, P5 44/random, P6 AA/random, **P7 9E/22** all showed +/-1 orders, **P7 clearest**; only P4 9E/random showed nothing (9E average reflectance is close to random data) |

Average run lengths (1-7PP): 9E 2.59T (shortest), AA 3.09T, random 3.38T, 22 5.17T, 44 5.16T (extreme 5.21T). Conclusion:
AA/44 has contrast but weak, drowned by the disc's own interference fringes in bd1/bd2; **9E (dark) / 22 (light)** is the pair
used by all later discs (BdAmp defaults `--dark 9E --light 22`). The reflectance contrast was not measured, only ranked from
diffraction photos; the authors estimated 0.4 - 1.0 x the DVD contrast.

Tooling: `BluRayRunLengthMapping(byte dark, byte light)` (patch 0001) keeps the SIMD parallel path (about 100 MB/s); any other
mapping falls back to single-threaded sampling (about 5 MB/s). ImgBurn refuses BD images smaller than a DVD-R DL (8.5 GB):
pad the tail with zeros (zero user data is scrambled to random-looking background).
