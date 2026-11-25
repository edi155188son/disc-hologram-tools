# DVD: rainbow geometry and full parallax with a radial carrier

* **Rainbow (white-light) geometry** (`radial_app` C1/C2): detour-phase radial carrier of 5 / 3 tracks, horizontal-parallax
  object (ring at z = +4 mm, X at z = -4 mm). Observed both effects: moving vertically changes colour (dispersion along the
  radial direction), moving sideways moves the ring and the X relative to each other (parallax along the track).
  First time dispersion and parallax were separated into two directions on an ordinary burner's DVD-R: the standard rainbow
  hologram geometry, no drive changes. Brightness about equal to the tangential-carrier true-wavefront hologram.
* **Full parallax** (`avatar_fp`, line-art portrait 15 mm wide, depth +/-4 mm, 16 layers): FP block (full 2-D wavefront,
  viewed with narrow-band light) showed vertical and horizontal parallax, vertical stronger; RB block (HPO rainbow) is
  clear in white light. The carrier is a straight radial line of 5 tracks (a carrier curved along the track would send
  the two sides of a big block in different directions). Phase-only encoding: anything filled is as bright as the brightest
  line, so only bright strokes carry field.
* **Filled version** (`avatar_fill.m`): Lee amplitude encoding (duty `D = asin(a)/pi` per carrier period); the simulation
  restores shading but with full speckle. Burn status was "to be burned" in the last notes of 2026-11-24.
* Not feasible on DVD: white-light image-plane holograms (`holo_imageplane.m`): only an ordinary pattern under LED light.

Code: `matlab/dvd_radial_app/radial_app.m`, `matlab/dvd_avatar_fp/avatar_fp.m`, `avatar_fill.m`,
`matlab/dvd_hpo/holo_hpo_big.m`; burn recipes in each folder's README (they call `DvdDiffractionRings --pattern polar --raster ...`).
