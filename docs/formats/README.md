# File formats

All integers and floats are little-endian. Specifications are written from the readers/writers in this repository
(file and line references are to the code that defines the format). There is no version negotiation beyond the magic.

| Format | Magic | Written by | Read by | Purpose |
|---|---|---|---|---|
| [BDAF1](BDAF1.md) | `BDAF1\0\0\0` | MATLAB `color_avatar.m`, `avatar_bd*.m`, `python/fields_gpu.py` | `tools/BdAmp`, `python/bdopt.py` | complex object field (or label density) of one hologram block on a BD |
| [BDOPT1](BDOPT1.md) | `BDOPT1\0\0` | `python/bdopt.py` | `tools/BdAmp` (`OptBits.cs`) | optimised on-disc byte values per BD cluster |
| [BDCH2](BDCH2.md) | `BDCH2\0\0\0` | `BdAmp chan` | `python/chio.py`, `python/sim_real.py` | channel-bit levels + ECC bytes of selected BD clusters |
| [DVDHPO1](DVDHPO1.md) | `DVDHPO1\0` | MATLAB `holo_hpo_big.m`, `holo_lenti_big.m`, `radial_app.m`, `avatar_fp.m`, `avatar_fill.m` | `tools/DvdDiffractionRings` (`--pattern polar`) | byte raster in polar coordinates for a DVD |
| [DVDLAT1](DVDLAT1.md) | `DVDLAT1\0` | `DvdDiffractionRings --export-lattice` | same tool, MATLAB `holo_*.m` | list of real DVD codeword positions in a patch + the `.val` values file |
| [BDHR1](BDHR1.md) | `BDHR1\0\0\0` | MATLAB `bd_disc1.m` | `experimental/BdHolo` | square raster in a patch frame, legacy BD path |
