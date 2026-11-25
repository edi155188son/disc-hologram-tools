# BDCH2: channel-bit levels of selected BD clusters

Written by `tools/BdAmp/Chan.cs` (`BdAmp chan ...`), read by `python/chio.py:read_ch` (used by `python/sim_real.py`).
It is the output of DIS's ECC encoder + 17PP recording-unit encoder for user data you supply, used to simulate the
real far field of a pattern instead of the byte model.

```
0     8   char[8]  "BDCH2\0\0\0"
8     4   uint32   start       first sector of the data (cluster 0)
12    4   int32    n           number of clusters stored
16    4   int32    cb          channel bits per recording unit (962136 with the DIS encoder, appendGuard3 = false)
20    4*n int32    cluster indices, relative to start (cluster c = sectors start + 32c)
20+4n n records of  (packed levels: ceil(cb/8) bytes, MSB first, 1 = level high) + (ECC cluster: 76880 bytes)
```

* `76880 = 496 frames x 155 bytes`: the 64 KiB cluster after EDC + LDC (RS(248,216)) + BIS, **before** 17PP.
* Levels are the NRZI channel levels the writer would use after DC control (DIS's automatic DC), one bit per
  channel bit (74.5 nm).
* Options of `chan`: `--start-sector`, one of `--data FILE` / `--data-file ISO --data-offset-sector S` (slice) /
  `--fill-scrambled XX[,YY..]` (synthetic), `--clusters N`, `--only LISTFILE` (indices to encode), `--out`.
