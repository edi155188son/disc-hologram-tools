# BD-R: per-frame 17PP / DSV design (BdCgh, not verified)

`tools/BdCgh` designs each 1268-bit recording frame bit by bit (17PP + NRZI simulated exactly, `Enc.cs` replicates DIS's encoder)
so that the channel follows `Re{object wave x carrier}` along the track (carrier 12T = 0.894 um, about 38 deg at 550 nm), and
steers the running DSV (+/-12) so that a DSV-minimising recorder takes the DC-control decisions the design assumed. Without
steering the phase flips about every 10 - 15 um; with it the phase should hold for about 1 mm. `bd_holo1.iso` (docs/discs/bd_holo1_bdcgh.md)
tests this (B1/B2 carrier with/without steering, avatar B3/B4, two lines B5 for parallax, diffuse B6, B7). The project notes
that were available do not record a result; treat it as unverified. `experimental/BdSim` (beam-search designer + DC policy
experiments) is the research code behind it and does not build (six diagnostic helper classes are missing from the snapshot).
`BdCgh --dry` estimates cost (about 2.6 s per designed cluster per thread); `--verify K` re-encodes K frames.
Open issue: `tools/BdCgh/Holo.cs` is a near-copy of the DVD `HoloCgh` hologram model (`Opts`, `Hologram`): deduplicate before extending.
