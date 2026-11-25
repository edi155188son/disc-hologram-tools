# Notice

Copyright (c) 2026 the authors of this repository.

This repository is licensed under the **GNU General Public License v3.0 or later** (`GPL-3.0-or-later`),
the same license as Disc Image Studio ([LICENSE](LICENSE) is the GPL text copied from the DIS repository).

## Why GPL

* The C# tools reference the DIS assemblies (`DiscImageStudio.BluRay`, `.Core`, `.ImageIO`, `DvdImageSolver`) and call
  non-trivial internal behaviour (track geometry, LDC / EDC / scrambler, channel encoders). A program that is linked
  to a GPL library is a covered work; distributing it under any other license is not permitted.
* `tools/HoloCgh` and `tools/HoloCgh/PhaseProbe` additionally compile DIS source files
  (`src/DvdImageSolver/Encoding/EfmPlus*.cs`, `NrziCodewordMetrics.cs`) through MSBuild `Compile Include` links.
* `tools/BdAmp/Steer.cs` and `python/bdgeom.py` re-implement logic from `DiscImageStudio.BluRay` (LDC parity
  model, geometry, ECC byte layout); `tools/BdCgh/Enc.cs` replicates the DIS 17PP encoder. Treat them as derived.
* `dis-patches/*.patch` are modifications of DIS files and carry DIS's license. The patches in `reference/` are the
  earlier DVD stereo-layer feature.

## What GPL asks of you when you share

* Keep the copyright and license notices; state that you changed the files.
* Binaries (for example a `BdAmp` build, a patched DIS EXE) must be accompanied by, or offer, the complete
  corresponding source *of that build*: this repository at the tag you built from, the exact upstream DIS commit
  and the patch series applied. Pointing at "the latest source" is not enough.
* Python and MATLAB files in this repository are part of the same work and the same license. Data you generate
  (ISO images, field files) is yours; images and meshes you feed in keep their own rights.

## Third-party

* Disc Image Studio, Copyright (c) 2026 Disc Image Studio contributors, GPL-3.0-or-later. It in turn contains code
  ported from CDImage (arduinocelentano, GPL-3.0-or-later) and third-party components listed in its
  `THIRD-PARTY-NOTICES.txt`; none of these files are copied here except the linked EFMPlus sources named above.
* The avatar / relief model scripts in `python/avatar3d` expect a user-supplied picture; no personal pictures or
  generated discs are included.
