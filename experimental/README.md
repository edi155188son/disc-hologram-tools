# experimental

Not verified on a disc, superseded, or incomplete. Nothing here is part of `HoloTools.slnx` except `BdHolo`.

| Folder | Why it is here |
|---|---|
| `BdHolo/` | first BD path (BDHR1 bitmaps, AA/44 pair): produced bd1 (no imaging); the snapshot is older than the version that made bd2 - bd4. Superseded by BdAmp. Builds. |
| `BdSim/` | DSV / DC-control policy experiments and beam-search frame designer behind BdCgh. **Does not build** (six helper classes absent from the snapshot). |
| `BdAmp-SigmaDelta/` | band-pass sigma-delta encoding (`--encoding sd`); `SigmaDelta.cs` + `enable-sigma-delta.patch` (apply to `tools/BdAmp/Program.cs`, add the file to the csproj). Simulation only. |
| `matlab_sigma_delta/` | the MATLAB experiments for it (`sd_test.m`, `sd_color.m`). |
| `matlab_efm_analysis/` | real-EFM-field vs model analysis scripts; their data dumper is missing. |
