# EFM channel-field analysis (incomplete)

`efm_analyze.m`, `efm2_analyze.m`, `efm_comb.m` compare the *real* EFM channel-bit field of a lattice patch with the
codeword-level model (does the F still appear, how large is the EFM land-fraction noise against the signal).
They read `efm_A.bin` / `efm2_A.bin`: a header (8-byte magic, int32 N, float64 dx, 3 x float64, then N x N float32
field and N x N int32 codeword index), written by a DIS-based dumper that is **not part of the snapshot**, and the
lattice/values files of `holo_ABCDE` (`s7888_lat_r28_a180.bin`, `s7888_v2_A.bin`). Kept for reference; the
conclusion drawn from them is in `docs/techniques/dvd-radial-carrier.md` (EFM noise exceeds binarisation noise).
