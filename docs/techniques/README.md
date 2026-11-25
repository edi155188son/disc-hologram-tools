# Techniques

One note per method: what it is, where the code is, what was verified on a real disc and what was not.
"Verified" means a burned disc showed the expected image / diffraction (photos kept by the author, see
[../disc-log.md](../disc-log.md)); simulation-only results are labelled as such.

| Note | Medium | Code | Status |
|---|---|---|---|
| [dvd-radial-carrier.md](dvd-radial-carrier.md): off-axis holograms with a radial carrier, byte-level amplitude, probes | DVD-R | `tools/DvdDiffractionRings`, `matlab/dvd_*` | verified |
| [dvd-rainbow-and-full-parallax.md](dvd-rainbow-and-full-parallax.md): white-light rainbow geometry, full parallax, vertical parallax | DVD-R | `matlab/dvd_radial_app`, `matlab/dvd_avatar_fp` | verified (filled version `avatar_fill`: burn status unknown) |
| [dvd-efm-phase-control.md](dvd-efm-phase-control.md): per-byte EFMPlus phase control, true-wavefront CGH | DVD-R | `tools/HoloCgh`, `PhaseProbe` | verified |
| [dvd-stereo-layer.md](dvd-stereo-layer.md): lenticular-style multi-view layer from per-view byte sequences | DVD-R | `dis-patches/reference`, `matlab/dvd_hpo/holo_lenti_big.m` | burned (grating-card comparison discs) |
| [bd-byte-pair.md](bd-byte-pair.md): which bytes give reflectance contrast on BD-R (9E / 22) | BD-R | `BluRayRunLengthMapping(dark, light)` | verified |
| [bd-double-phase.md](bd-double-phase.md): Lee vs double-phase encoding along the tracks | BD-R | `tools/BdAmp` | verified |
| [bd-ldc-parity-steering.md](bd-ldc-parity-steering.md): make the drive-generated LDC parity bytes part of the pattern | BD-R | `tools/BdAmp/Steer.cs` | verified (large visible gain) |
| [bd-colour-multiplexing.md](bd-colour-multiplexing.md): three carriers in one block | BD-R | `matlab/bd_color`, `tools/BdAmp --combine sum` | verified |
| [bd-byte-optimisation-and-papr.md](bd-byte-optimisation-and-papr.md): GPU byte optimiser (v3), PAPR reduction (v4) | BD-R | `python/bdopt.py`, `python/fields_gpu.py` | verified (v3 better, v4 brighter) |
| [bd-17pp-dsv-design.md](bd-17pp-dsv-design.md): per-frame 17PP / DSV design | BD-R | `tools/BdCgh`, `experimental/BdSim` | not verified |
| [negative-results.md](negative-results.md): things that did not work | both | | |

Common geometry facts (DIS model, 532 nm unless stated):
* DVD-R: track pitch about 0.74 um, one EFMPlus byte = 16 channel bits x 133.3 nm = 2.13 um along the track; codeword
  positions are nominal and the spiral is calibrated per disc brand (`--inner --outer --pitch-linear/-quadratic/-cubic`,
  and `--sectors 2297888` - the value the calibration was fitted with, not the 2298496 that ImgBurn reports).
* BD-R: track pitch 0.32 um, channel bit 74.5 nm, a byte is 12 channel bits (0.894 um), 12,219,392 sectors,
  one 65,536-byte cluster = 496 frames x 155 bytes = 958,272 channel bits (about 72 mm of track at mid radius);
  about 13 % of every cluster is LDC parity, which the drive computes itself.
