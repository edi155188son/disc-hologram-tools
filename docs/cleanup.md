# Cleanup log and open items

## Done in this consolidation
* **Hard-coded paths.** All `G:\claude_disc_pattern_research\...` paths were removed: README command lines now use paths
  relative to the working folder and `<WORK>` for the checkout; MATLAB scripts read the output folder from `HOLO_OUT`
  (default: current folder) and the avatar texture / depth folder from `HOLO_ASSETS` (`texture_rgba.png`,
  `depth16.png`; required, no default); `python/fields_gpu.py --src` is now required (was `..\avatar3d`).
  `grep -r 'G:\\'` finds nothing.
* **Executable names.** The notes used `DvdDiffractionRings_polar.exe`, `_multi.exe`, `BdAmp.exe`, `BdHolo.exe`, `BdCgh.exe`,
  `HoloCgh.exe`; the repository builds one `DvdDiffractionRings` (all patterns), `BdAmp`, `BdCgh`, `HoloCgh`, `BdHolo`.
* **BdAmp** (from the untracked `dis/tools/BdAmp`): global static hook replaced by the per-call
  `BluRayStreamingOptions.ClusterPostProcessor`; `Steer`/`OptBits` take `Span<byte>`; ISO path calls `BluRayStreamingGenerator.GenerateFile`
  directly; the band-pass sigma-delta encoder (not verified on a disc) moved to `experimental/BdAmp-SigmaDelta` with
  `enable-sigma-delta.patch`. Output verified byte-identical to the original build.
* **HoloCgh**: EFMPlus sources linked from DIS (they are `internal` there) instead of requiring `InternalsVisibleTo`; the stereo
  comparison compiles only when the stereo patch is applied (`HAS_STEREO`); `--testset` option added (the snapshot's `TestDisc.cs`
  used `Opts.TestSet`, which `Opts` did not define: the flag name is inferred).
* **Moved to `experimental/`**: BdHolo (older bitmap path), BdSim (incomplete), sigma-delta (C# + MATLAB), EFM channel-field analysis (incomplete).
* Per-disc READMEs kept (Chinese) next to the scripts; `docs/disc-log.md` is the one-page index.

## Remaining hard-coded values (not paths)
* Calibration defaults compiled in: DVD `--inner 23.96623775566068 --outer 57.954483397299796 --pitch-linear -4.152250636325952E-05
  --pitch-quadratic 4.888878988850371E-05 --pitch-cubic -1.9188389560484087E-05` (CMC MAG. AM3 DVD-R, `--sectors` required);
  BD `--inner 23.987042157339335 --outer 57.987960341783236` (Verbatim CMCMAG-BA5) in `BdCgh` defaults and `python/sim_real.py`.
  Always pass the values of the disc you burn.
* Design constants in code: `python/fields_gpu.py` (`K`, `BL`: block at r = 41 mm, the five colour blocks), `matlab/*/*.m` (block radius,
  angles, sizes), `tools/BdCgh` test set layout (r = 31 mm), `tools/HoloCgh/TestDisc.cs` (r = 41 mm).
* MATLAB scripts that expect files in the current folder: `holo_hpo_run.m` (`w_lat_A..D.bin`, `hpo_*.bin`), `holo_inverse.m`
  (`lattice.bin`, `values.bin`), `holo_framed.m` (`lattice6.bin`, `values6.bin`).
* `dis-patches/` authors are placeholders; `scripts/prepare-dis.sh` pins DIS commit `6f0d8fd`.

## Open items (by value)
1. **Run the MATLAB on a machine with MATLAB** (only paths / exe names were edited; no `.m` file was executed here). Seven scripts
   carry an identical local `read_lattice`, three an `error_diffusion`, `fresnel_sim`, `score`: factor into `matlab/common/`.
2. **BdHolo snapshot is an older revision** than the one that made bd2 - bd4 (`--bytes D,L`, `--min-gb`, `--pattern probes|palette`
   are described in the README but missing from the code). Recover the newer source from the PC if those diagnostic gratings are wanted;
   `docs/formats/BDHR1.md` and `matlab/bd_bdhr_port` describe the older path.
3. **BdSim** misses `EncTest`, `Dump`, `Diag`, `Marg`, `K27`, `Mis`; **EFM analysis** misses its raster dumper and `read_lattice` variant.
4. **Deduplicate** `tools/BdCgh/Holo.cs` against `tools/HoloCgh/Program.cs` (same `Opts` / `Hologram`), and `tools/*/Program.cs` option parsing
   (hand-rolled `--key value` parsers; unknown options are silently ignored in BdAmp).
5. **No unit tests for the C# tools** apart from `scripts/smoke-test.sh`; no CI. Add the geometry cross-check and byte-identical regression as GitHub Actions
   once the repo is public (needs a DIS checkout step).
6. `tools/DvdDiffractionRings` still contains early experiments (`ZonePlate.cs`, analytic `Hologram.cs` `--pattern hologram`, `Search.cs` spectral search)
   whose individual verification status is not in the notes; kept because `Program.cs` dispatches to them.
7. AI assistance: the stereo patch carries a `Claude` author line and much of the code was written with an AI assistant. Decide the disclosure wording before publishing (see the evaluation).
