# python

| File | What | Needs |
|---|---|---|
| `bdgeom.py` | BD single-layer geometry (constant-pitch spiral, ported from `DiscImageStudio.BluRay`) and the 76,880-slot ECC byte layout of a cluster (payload / EDC / LDC parity / BIS, with channel offsets). `python bdgeom.py` prints sample points that `BdAmp --geo-check` reproduces bit for bit (`tests/check_geometry.py`). | numpy |
| `bdopt.py` | byte-level iterative optimiser with don't-care regions: BDAF1 fields -> BDOPT1. See `docs/techniques/bd-byte-optimisation-and-papr.md`, `docs/discs/gpu_opt_bdopt.md`. | torch (GPU) |
| `fields_gpu.py` | GPU port of `matlab/bd_color/color_avatar.m` with PAPR reduction: writes `C<n>_<R|G|B>.bdaf`. `--src` folder with `texture_rgba.png` + `depth16.png`. | torch, Pillow |
| `sim_real.py`, `chio.py` | far-field simulation of the **real** channel pattern (BDCH2 from `BdAmp chan`) against the byte model: `plan` (clusters of a tile), `sim` (brightness, SNR under several reflectance models). `chio.read_ch` reads BDCH2. | torch |
| `prototypes/` | `opt_prototype.py`, `dp_hold.py` (small numpy models used before `bdopt.py`), `gpu_fft_profile.py` (CUDA timing of FFT / gather / index_add for the optimiser) | numpy / CUDA |
| `avatar3d/` | `build_relief.py` (2.5D relief + depth maps + point clouds from a portrait picture), `render_views.py` (Blender: multi-view renders), `README_relief_model.md` | scipy, scikit-image, Blender |

Typical BD chain (see `matlab/bd_color/README.md`): `fields_gpu.py` (or MATLAB) -> `BdAmp ... --fields ... --output x.iso`; with optimisation:
`bdopt.py --inner .. --outer .. --fields ... --out x.bdopt --taper 1.5` -> `BdAmp ... --opt-bits x.bdopt`.
`python/bdopt.py --device cpu --limit-tiles 2 --tile 0.06` runs a tiny tile without a GPU (used by `scripts/smoke-test.sh`).
