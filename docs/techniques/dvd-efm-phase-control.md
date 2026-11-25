# DVD: EFMPlus byte phase control and true-wavefront CGH

**Question.** Can the phase of the 16T (one cycle per byte, 2.13 um) EFMPlus grating be chosen byte by byte?
**Answer** (`tools/HoloCgh/PhaseProbe`): greedy, state-aware choice among bytes >= 0x58 (no DSV-dependent substitution)
while avoiding the DSV-chosen State 1/4 branch makes the pit/land pattern of every byte in a sync frame known exactly, up
to one global sign per sync frame (the writer's DSV decision). Encoding with the real encoder from random boundaries gave
prediction match 1.000, phase fidelity 0.961 and coherent amplitude 90 % of a pure `5F` grating (output of the probe).

**HoloCgh** (`tools/HoloCgh`): object = picture + depth map as point emitters along every track (arc). For each radius
row the 1-D object wave along the arc is computed (emitters every 0.03 mm, random phase, brightness and depth from the
images), multiplied by a 27 deg carrier, and for every codeword the byte whose 16 channel bits best match
`Re{object wave x carrier}` is written. Dark areas and background get a 2.13 um (or `--dark-period`) grating that sends light
outside the viewing zone; grey levels are written as the probability |object|/full of using hologram bytes.
Viewing: 550 nm, 20 - 34 deg (27 +/- 7 deg), continuous horizontal parallax, real depth; colour changes blue to white to yellow with angle.

**Limits** (from the notes): each sync frame (0.19 mm) has a random sign and tracks are independent, so the focus is about
0.2 mm; the first frame of each sector has an uncertain start (3.9 %); depth beyond about 5 mm blurs in white light.
`holo_true2` was subjectively as bright as the radial-carrier rainbow hologram.

Options: `HoloCgh --texture T.png --depth D.png [--width-mm 30 --radius 41 --angle -90 --copies 2 --near-mm 3 --far-mm -3
--alpha 27 --half 7 --flip --robust --smooth --lenti v1.png,.. --preview P.png --output X.iso]`; `HoloCgh probe` prints the
codeword-position statistics of the writer; `--testdisc DIR [--testset depth]` builds the diagnostic discs
(`docs/discs/holo_test_dvd.md`, `holo_depth_dvd.md`). `--testset` is the name given to `Opts.TestSet`, which the snapshot
used but never parsed: the flag name is inferred.
