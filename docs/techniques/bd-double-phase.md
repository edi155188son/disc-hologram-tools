# BD-R: Lee (detour phase) vs double-phase encoding

Both use a carrier along the radial direction (v): period `Lambda = lambda / sin(theta0)`, a whole number of tracks
(0.32 um each), and write a byte as dark (9E) or light (22) from the local complex field `e`.

* **Lee** (`--encoding lee`): bright iff `cos(2 pi v/Lambda - arg e) > cos(pi D)`, `D = asin|e|/pi`. Amplitude is the bright
  fraction, so the disc also carries the picture (average reflectance = |e|). First BD disc `avatar_bd` (11-track carrier,
  3.52 um, +/-3.5 deg cone): "very faint" on the disc.
* **Double-phase** (`--encoding dp`): consecutive tracks alternate `arg e +/- acos|e|`; duty is 50 % everywhere, so no picture
  is visible on the disc and no zero-order image. Needs the parity of the track index (modelled from the spiral; BdAmp prints
  `track parity self-check: 0/2000` and it must be 0). `avatar_bd2`: carrier 7 tracks (2.24 um, 13.7 deg at 532 nm),
  +/-8 deg cone, depth +/-9 mm, brightness about 1/3 - 1/5 of v1 per view because the view area is larger. Verified on a disc;
  it became the base for everything later.
* Sensitivity: double-phase cancels amplitude by adding two tracks, so it is more sensitive to the real contrast of 9E/22
  and to burnt-mark length error. `matlab/bd_color/ksweep.m` simulates noise in the viewing window against tracks-per-carrier-period.

Code: `tools/BdAmp/Program.cs` (`Sample`, `SampleSum`), field generators `matlab/bd_avatar_v1`, `matlab/bd_avatar_v2_dp`.
