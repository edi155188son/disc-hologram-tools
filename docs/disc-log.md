# Disc log

Distilled from the author's working notes (`findings.md`, last update 2026-11-24, plus the per-disc READMEs in `matlab/` and
`docs/discs/`) and the verification statements supplied with this consolidation. Dates are 2026. "recorded" means the notes
contain a result; "not recorded" means the disc was designed (and usually burned) but the notes available here do not say what
was seen. Light source for all results: 532 nm green laser unless stated (never viewed directly, see [safety.md](safety.md)).

## DVD-R (CMC MAG. AM3, `--sectors 2297888`, calibrated spiral)

| Disc | Date | Tool / script | Content | Result |
|---|---|---|---|---|
| `holo_multi5_s2297888` | Nov | DvdDiffractionRings lattice | F hologram, 5 blocks | F visible in all blocks once sectors = 2297888 (6 mm squares square at r = 28 - 53 mm) |
| `holo_ABCDE` | Nov | `holo_design.m` | 12-point F: threshold / error diffusion / IFTA | direct binarisation best |
| `holo_image5` | Nov | `holo_image.m` | continuous strokes, dots, smiley | continuous strokes with random phase sharpest; visible limit about a 7 x 3 mm "DVD" stroke or 12 - 33 points |
| `holo_avatar` | Nov | `holo_image.m` | line portrait | too dark with a green laser |
| `holo_3d` | Nov | `holo_points3d.m` | 3D point cloud, each point own depth | points focus at the designed height for Z = 200 - 600 mm (no quantitative measurement) |
| `holo_white` | Nov | `holo_imageplane.m` | white-light image-plane | not feasible: ordinary pattern under LED |
| `holo_true`, `holo_true2` | 11-21 | HoloCgh | true-wavefront portrait vs 4-view grating card | `holo_true`: only outlines, no detail; `holo_true2` about as bright as radial rainbow |
| `holo_test` | 11-21 | HoloCgh `--testdisc` | 8 diagnostic 12 mm blocks | not recorded |
| `holo_depth` | 11-22 | HoloCgh `--testdisc --testset depth` | depth / band series D0 - D7 | not recorded |
| Suzanne / avatar 4-view | Nov | stereo layer patch | lenticular multi-view | Suzanne 4 views burned successfully; avatar_stereo project prepared |
| `probes_radial` | 11-24 | `--pattern probes` | alternate 92/A5 every 2 - 8 tracks; spokes 6 - 48 um | P1 - P5 clear radial spots; P6 - P9 round spots (cross-track alignment about 1 um or better) |
| `radial_app` | 11-24 | `radial_app.m` | A1/A2 radial 10/18 deg, B1/B2 oblique, R0 old, C1/C2 rainbow | A1, A2, B1 clear and brighter than R0; B2 blurred; C1/C2 colour change vertical, parallax horizontal |
| `avatar_fp` | 11-24 | `avatar_fp.m` | FP full parallax + RB rainbow, line art | parallax in both directions, vertical stronger; RB clear in white light |
| `avatar_fill` | 11-24 | `avatar_fill.m` | filled (Lee) versions | burn pending in last notes |

## BD-R (Verbatim CMCMAG-BA5 64099/64293, 12,219,392 sectors, inner 23.987042157339335, outer 57.987960341783236 mm)

| Disc | Date | Tool / script | Content | Result |
|---|---|---|---|---|
| `bd1_verbatim` | 11-?? | BdHolo (AA/44), `bd_disc1.m` | DVD tests ported (F, text, smiley, white-light, 3D) | frames square, no imaging, large scattering patch |
| `bd2_probes` | 11-11 | BdHolo `--pattern probes` | 11 diagnostic gratings AA/44 | no diffraction spots |
| `bd3_palette` | 11-11 | BdHolo `--pattern palette` | 7 byte pairs, 16 um radial gratings | 9E/22 clearest; AA/44 weak; 9E/random none |
| `bd4_9E22` | 11-11+ | BdHolo `--bytes 9E,22` | diagnostics + holograms redone with 9E/22, 24.5 GB | to be burned in the 11-24 notes |
| `avatar_bd` (v1) | 11-24 | `avatar_bd.m` + BdAmp Lee | FP + RB 31 x 31 mm, 11-track carrier | "very faint" |
| `avatar_bd2` | 11-24 | `avatar_bd2.m` + BdAmp `--encoding dp` | double-phase, 7-track carrier, F/R labels | verified; base for later discs |
| `color_bd` | 11-25 | `color_avatar.m` + BdAmp `--combine sum --steer-parity on --taper 1.5` | 5 colour blocks | verified; LDC parity steering gave a large visible gain |
| `color_bd_v3` | 11-25 | `bdopt.py` + BdAmp `--opt-bits` | same designs, optimised bytes | better than double-phase |
| `color_bd_v4` | 11-25+ | `fields_gpu.py` (PAPR) + `bdopt.py` | same, PAPR-reduced fields | brighter |
| `bd_holo1` | 11-22 | BdCgh | true-wavefront with/without DSV steering | not recorded |

Camera / viewing notes for BD: diffraction angle for 12T carrier about 38 deg; for radial carriers 7 - 17.5 deg
(see `docs/discs/*`). Burn parameters: ImgBurn "write image file to disc", lowest speed, same Verbatim batch.
Exact dates marked `Nov` / `11-??` are not in the notes.
