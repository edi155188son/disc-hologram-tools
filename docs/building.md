# Building

## Requirements
* .NET 10 SDK (DIS pins `10.0.100`, `rollForward: latestFeature`; tested with 10.0.404 on Linux).
* A DIS checkout at the base commit with `dis-patches/` applied: `scripts/prepare-dis.sh [dest] [--optional] [--stereo]`.
  That clones `https://github.com/JiaFeiMiao-K-Cat/discimagestudio` (override with `DIS_URL`), checks out the pinned
  commit on a local branch `holo-tools` and runs `git am`. Alternatively add upstream as a git submodule at
  `external/DiscImageStudio` and apply the patches yourself.
* Tell MSBuild where it is: `DIS_ROOT=/path/to/DiscImageStudio` (environment) or `-p:DisRoot=/path/...`.
  `Directory.Build.props` defaults to `external/DiscImageStudio` and fails early if the folder is not a DIS tree.
* Python 3.10+ with `numpy`, `torch` (CUDA optional), `Pillow` (`python/requirements.txt`); MATLAB R2024b+ for the `.m` scripts.

## Build

```bash
scripts/build-all.sh                  # or: dotnet build HoloTools.slnx -c Release
dotnet run --project tools/BdAmp -c Release            # prints usage when required options are missing
```

| Project | Target | Needs from DIS |
|---|---|---|
| `tools/BdAmp` | net10.0 | `DiscImageStudio.BluRay`, patches 0001 + 0002 |
| `tools/BdCgh` | net10.0 | `DiscImageStudio.BluRay`, `.Core`, `.ImageIO` |
| `tools/DvdDiffractionRings` | net10.0-windows | `DvdImageSolver`, `.Core`, `.ImageIO` (no patch needed) |
| `tools/HoloCgh`, `PhaseProbe` | net10.0-windows | `DvdImageSolver`; EFMPlus sources linked from DIS; `--lenti` only if the stereo reference patch is applied (`HAS_STEREO` is defined automatically when `StereoOverlay.cs` exists) |
| `experimental/BdHolo` | net10.0 | `DiscImageStudio.BluRay` |

`net10.0-windows` projects reference WPF-capable DIS libraries; off Windows they compile with `EnableWindowsTargeting`
(set in the csproj) and the console tools run (checked on Linux: DvdDiffractionRings, HoloCgh `--preview` and `probe`,
PhaseProbe), but WPF itself cannot run.

## Tests
* `scripts/smoke-test.sh`: BdAmp lee / double-phase / parity-steering runs on a synthetic block, parity checked against
  DIS's own LDC encoder, `bdopt.py` (CPU) to BDOPT1 to BdAmp `--opt-bits`, `BdAmp chan` to `chio.read_ch`, and
  `tests/check_geometry.py` (python geometry equals `BdAmp --geo-check` bit for bit). About 40 s.
* DIS's own checks (for PRs): `dotnet build DiscImageStudio.slnx -c Release`, `dotnet run --project src/DvdImageSolver -c Release -- selftest`,
  `dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release` (Windows).

## What was built and tested in this consolidation (Linux, 2 cores, no GPU, no MATLAB)
* Built with 0 warnings / 0 errors against upstream `6f0d8fd` + patches 0001-0002: BdAmp, BdCgh, DvdDiffractionRings,
  HoloCgh, PhaseProbe, BdHolo; against `+ stereo reference patch` also (HoloCgh with `HAS_STEREO`). Against **stock upstream
  `main` with no patch** everything builds except BdAmp (it needs the pair constructor and the hook).
* BdAmp refactor (options hook, `Span<byte>`, sigma-delta removed) produces output byte-identical to the original
  working-copy build for Lee, double-phase, double-phase + parity steering (2697/2697 parity bytes re-verified), `--opt-bits`,
  and a whole small ISO with steering enabled.
* Python: all files byte-compile; `bdgeom`, `bdopt` (CPU, tiny tile), `fields_gpu` (CPU, tiny block), `sim_real plan`,
  `chio` run. `sim_real sim`, `prof.py`/`gpu_fft_profile.py` (CUDA only), `build_relief.py` (needs the avatar picture and
  scikit-image) and `render_views.py` (needs Blender's `bpy`) were not run.
* Not built / not run: `experimental/BdSim` (snapshot misses 6 diagnostic classes: `EncTest`, `Dump`, `Diag`, `Marg`, `K27`, `Mis`),
  all MATLAB (`.m` files only edited for paths and exe names, never executed), WPF UI of the stereo patch, DIS
  `ArchitectureTests` (needs Windows), any full-disc ISO (25 GB / 4.7 GB) and any real disc.
