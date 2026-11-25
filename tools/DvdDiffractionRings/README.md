# DvdDiffractionRings

DVD-R test / hologram generator on top of `DvdImageSolver` (stock DIS, no patch). Patterns: `rings`, `rings7`, `probes`, `image`, `zoneplate`,
`hologram` (analytic F), `palette`, `lattice` (import), `polar` (import DVDHPO1). Also `--export-lattice` (DVDLAT1 for MATLAB) and `--search 1|2`
(spectral search over repeating byte sequences, which found the 92 / A5 / 5F / C5DB / CC67 ... gratings).

```
DvdDiffractionRings --sectors N --output out.iso [--pattern ...] [--inner ..] [--outer ..] [--pitch-linear ..] [--pitch-quadratic ..] [--pitch-cubic ..]
  lattice export: --export-lattice lattice.bin [--patch-r 40] [--patch-angle 0] [--patch-size 4]
  lattice import: --pattern lattice --lattice a.bin[,..] --values a.val[,..]
  polar import:   --pattern polar --raster a.hpo[,..] [--pairs C5DB,CC67]
  image:          --pattern image --image f.png [--size-mm 80] [--map 0:A5,128:66,192:B9,224:92,255:R]
```
`N` must be the total sector count DIS's calibration used (`2297888` for the CMC MAG. AM3 DVD-R in the notes), not ImgBurn's capacity.
Run with no arguments for the list of rings, probes and palettes. Burn the ISO as an image, never as a file on a data disc.
