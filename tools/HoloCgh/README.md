# HoloCgh and PhaseProbe

True-wavefront (horizontal-parallax) computer-generated hologram on DVD-R with per-byte EFMPlus phase control, plus the probe that validates
the phase control. Method and limits: `docs/techniques/dvd-efm-phase-control.md`. Disc READMEs: `docs/discs/holo_true_dvd.md`, `holo_test_dvd.md`, `holo_depth_dvd.md`.

```
HoloCgh --texture tex.png --depth depth.png [--output holo.iso] [--preview prev.png] [--width-mm 30 --radius 41 --angle -90 --copies 2
        --near-mm 3 --far-mm -3 --alpha 27 --half 7 --dark-period 0.853 --flip --robust --smooth --lenti v1.png,v2.png ...]
HoloCgh probe                       # codeword position statistics of the writer
HoloCgh --testdisc ASSETS [--testset depth]
PhaseProbe [M]                      # 16T phase-control probe (M = harmonic multiplier)
```
`--preview` renders the simulated 21 / 27 / 33 deg views without writing an ISO (the safe way to try it; a real ISO is 4.7 GB).
`--lenti` needs the stereo reference patch.
