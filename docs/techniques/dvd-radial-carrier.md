# DVD: radial carrier, byte-level amplitude

**Idea.** Bytes written in a DVD-R are scrambled and EFMPlus-modulated; some repeated bytes give a nearly
pure pit/land period (92 = 16T, 2.13 um; A5 = 6.4T, 0.85 um; byte combinations are at the limit: 92 is 7.28T, A5 is 3.39T
average run, extremes 7.41T / 3.36T). Alternating two bytes with different average reflectance across tracks gives an
*amplitude* grating whose period is chosen in whole tracks. Across tracks the structure is robust because the average
reflectance does not depend on the sign (polarity) of a sync frame, so the radial direction can be used for large
deflection angles without any cross-track phase alignment; along the track (tangential) the usable period is limited
by the byte length.

**Measured on discs (532 nm laser):**
* `probes_radial` (`--pattern probes`): alternating 92 / A5 every 2, 3, 4, 6, 8 tracks all gave clean radial diffraction
  spots (k = 2 tracks: period 1.48 um, about 21 deg); angle ratios 1 : 0.67 : 0.48 match 1/k; spoke gratings
  (radial lines aligned across tracks, 6 - 48 um) gave round spots, i.e. cross-track angular alignment is about 1 um or better with the
  calibrated model.
* Radial carriers 10 deg / 18 deg (about 4.1 / 2.3 tracks) off-axis holograms (`radial_app` A1, A2) and an oblique
  one (B1, tangential fringe 7.6 um) were clear and brighter than the old small-angle hologram R0 (1.3 deg); B2 (tangential
  4.7 um, about 2.2 bytes per period) was already blurred. Practical tangential limit: about 5 - 7 um; radial resolution
  is 3 - 4 times better.
* Design rules found: detour-phase radial carriers sample once per track, so k = 4 aliases the 3rd harmonic onto the
  +1 order (use k = 3 or 5); give point sources a random jitter, otherwise binarisation intermodulation forms a regular
  ghost lattice.

**Code and workflow.** `DvdDiffractionRings --export-lattice` writes the real codeword positions of a patch
(DVDLAT1), MATLAB designs the binary pattern (`matlab/dvd_lattice_design/holo_*.m`: threshold / error diffusion / IFTA,
3D point clouds, image plane), `--pattern lattice` imports the values; for large areas `matlab/dvd_hpo`,
`dvd_radial_app`, `dvd_avatar_fp` write DVDHPO1 polar rasters for `--pattern polar`.

**Findings that shaped the tools** (see also [negative-results.md](negative-results.md)): continuous strokes with random
phase and direct binarisation gave the sharpest images; error diffusion / IFTA looked no better because EFM noise exceeds
binarisation noise, and they cost over 30 % brightness; visible area with a green laser is limited (about a 7 x 3 mm
"DVD" stroke or 12 - 33 points), so a stronger source (DPSS) was the next step; coherence aperture about 0.5 mm limits
resolution at Z = 400 mm.
