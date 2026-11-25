# matlab

One folder per disc design. Scripts write next to the working folder (`HOLO_OUT`, default = current folder) and read the avatar
texture / depth from `HOLO_ASSETS`. Each folder's `README.md` has the burn recipe of that disc (Chinese, from the author's notes).
None of these files were executed during the consolidation (no MATLAB available): treat them as source of record.

| Folder | Medium | Scripts | Output |
|---|---|---|---|
| `dvd_lattice_design/` | DVD | `holo_design`, `holo_inverse`, `holo_framed`, `holo_image`, `holo_points3d`, `holo_imageplane`, `label_values`, `avatar_masks`, `test_dotphase` | values file for `--pattern lattice` (needs DVDLAT1 from `--export-lattice`) |
| `dvd_hpo/` | DVD | `holo_hpo`, `holo_hpo_image`, `holo_hpo_run`, `holo_hpo_big`, `holo_lenti_big`, `render_views` | DVDHPO1 rasters for `--pattern polar` (white-light HPO, lenticular) |
| `dvd_radial_app/` | DVD | `radial_app` | DVDHPO1: radial-carrier off-axis, oblique, rainbow blocks |
| `dvd_avatar_fp/` | DVD | `avatar_fp` (line art), `avatar_fill` (Lee filled) | DVDHPO1: full parallax + rainbow portrait |
| `bd_bdhr_port/` | BD | `bd_disc1` | BDHR1 rasters (legacy BdHolo, AA/44) |
| `bd_avatar_v1/` | BD | `avatar_bd` | BDAF1 (Lee) |
| `bd_avatar_v2_dp/` | BD | `avatar_bd2` | BDAF1 (double-phase) + labels |
| `bd_color/` | BD | `color_avatar`, `color_sim`, `color_snr`, `ksweep` | BDAF1 R/G/B per block, simulations |

Experimental / incomplete MATLAB: `experimental/matlab_sigma_delta`, `experimental/matlab_efm_analysis`.
Known duplication: see `docs/cleanup.md` item 1.
