# DVD: lenticular-style multi-view (stereo) layer

Each view is written with its own scrambled-byte sequence whose EFMPlus marks form a nearly pure along-track grating
(`5F C5DB F9 CC67 6F DDB6 A5`, 2.13 - 0.85 um): each view leaves at its own diffraction angle (about 15 - 40 deg at 550 nm), so
moving the head along the track switches views, like a lenticular card. Used as the comparison "grating card" next to the
true-wavefront holograms (`holo_true`: 4 views from `views4/`). Implemented as a DIS design-layer feature in
`dis-patches/reference/0001-Add-DVD-multi-view-stereo-layers.patch` (StereoOverlay in DvdImageSolver, sidecar
`*.stereo.json`, WPF properties panel) and as DVDHPO1 rasters with `--pairs` (`matlab/dvd_hpo/holo_lenti_big.m`).

Verification recorded in the patch README: the exported-ISO path equals a per-codeword reference sampler byte for byte
(r 30 - 50 mm, 2.7 GB); each sequence carries only its own view; 8 copies placed and rotated correctly; depth-synthesised
views move near and far in opposite directions. The WPF UI was compiled but never opened on Linux.
