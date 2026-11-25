# Disc holograms: tools, designs and notes

Computer-generated diffraction patterns (holograms) written into the *user data* of ordinary recordable discs
(DVD-R and single-layer BD-R) with an ordinary burner. The tools choose which bytes are burned where, using the
physical layout model (spiral geometry, scrambler, ECC interleave, EFMPlus / 17PP modulation) of
[Disc Image Studio](https://github.com/JiaFeiMiao-K-Cat/discimagestudio) (DIS, GPL-3.0-or-later), so that the marks on
the disc form a grating or a hologram. Nothing here touches drive firmware or the drive's write strategy: every disc
is an ordinary data image burned in "write image file to disc" mode (see [docs/safety.md](docs/safety.md)).

> 中文摘要：把全息圖樣寫進一般 DVD-R / BD-R 的使用者資料區（依 Disc Image Studio 的碟面物理模型挑 byte）。
> 本 repo 整理生成器（C#）、GPU 優化器（Python）、設計腳本（MATLAB）與每張實測盤的紀錄。

## Layout

| Path | What |
|---|---|
| `dis-patches/` | the DIS changes the tools need, as `git format-patch` series against upstream `main` (+ `optional/`, `reference/`) |
| `tools/BdAmp` | BD-R generator: radial-carrier holograms from smooth complex fields (Lee / double-phase), LDC parity steering, optimiser-output reader, channel-bit exporter |
| `tools/BdCgh` | BD-R true-wavefront CGH with per-frame 17PP / DSV design (not yet verified on a disc) |
| `tools/DvdDiffractionRings` | DVD-R generator: rings / probes / lattice import-export / polar raster import (radial-carrier holograms, rainbow, full parallax) |
| `tools/HoloCgh`, `tools/HoloCgh/PhaseProbe` | DVD-R true-wavefront CGH with per-byte EFMPlus phase control; the probe that validates it |
| `python/` | BD geometry + ECC layout (`bdgeom`), GPU byte optimiser (`bdopt`), field generator with PAPR reduction (`fields_gpu`), channel-level far-field simulation (`sim_real`, `chio`); `prototypes/`, `avatar3d/` |
| `matlab/` | design scripts, one folder per disc design (field generators, simulations, README with the burn recipe) |
| `experimental/` | not verified / superseded / incomplete: BdHolo (bitmap), BdSim (DSV experiments, does not build), sigma-delta encoder |
| `docs/` | `techniques/`, `formats/` (BDAF1, BDOPT1, BDCH2, DVDHPO1, DVDLAT1, BDHR1), `discs/` (per-disc READMEs), `disc-log.md`, `building.md`, `safety.md`, `cleanup.md` |
| `scripts/`, `tests/` | `prepare-dis.sh`, `build-all.sh`, `smoke-test.sh`, synthetic-field generator, geometry cross-check |

## Quick start

```bash
scripts/prepare-dis.sh                  # clone upstream DIS at the pinned commit, apply dis-patches/ (local branch only)
export DIS_ROOT=$PWD/external/DiscImageStudio
scripts/build-all.sh                    # .NET 10 SDK; DVD tools need EnableWindowsTargeting off Windows (already set)
scripts/smoke-test.sh                   # BD chain on synthetic data, CPU only, ~40 s
```

Details, options for building against a stock upstream tree, and what could not be tested: [docs/building.md](docs/building.md).

## Status in one table

| Component | Real-disc status |
|---|---|
| DVD radial-carrier off-axis holograms, rainbow geometry, full parallax (DvdDiffractionRings + MATLAB) | verified |
| DVD EFMPlus byte phase control (PhaseProbe, HoloCgh) | verified |
| BD 9E/22 byte pair, double-phase, LDC parity steering, colour frequency multiplexing, byte optimisation, PAPR (BdAmp + python) | verified |
| BdCgh (BD true-wavefront), sigma-delta, BdHolo bitmap path with AA/44 | no image (BdHolo AA/44) or not recorded / not burned |

See [docs/disc-log.md](docs/disc-log.md) for the disc-by-disc table and [docs/techniques/](docs/techniques/README.md) for the methods.

## License

GPL-3.0-or-later, see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). The C# tools link against DIS and include
DIS-derived source (EFMPlus encoder links in `tools/HoloCgh`), so they are derivative works of a GPL-3.0-or-later
program; a binary you distribute needs its complete corresponding source.
