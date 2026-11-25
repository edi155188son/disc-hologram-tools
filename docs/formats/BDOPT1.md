# BDOPT1: optimised on-disc byte values (BD)

Written by `python/bdopt.py`, read by `tools/BdAmp/OptBits.cs`. For each 32-sector cluster touched by a hologram
block: which payload bytes and which LDC parity bytes were optimised, and their on-disc value (dark or light byte).

```
0     8    char[8]  "BDOPT1\0\0"
8     4    int32    n          number of records (patched in after the run)
12    ...  n records:
        4     uint32   cluster index = first sector / 32
        8192  bytes    PF   payload "free/optimised" flags, 65536 bits
        8192  bytes    PV   payload values, 65536 bits
        1216  bytes    QF   LDC parity flags, 9728 bits
        1216  bytes    QV   LDC parity values, 9728 bits
```

* Bit order: bit `i` is `(a[i >> 3] >> (i & 7)) & 1` (numpy `packbits(..., bitorder='little')`).
* Payload bit index `i = sector * 2048 + k` for user byte `k` of sector `sector` (0..31) **in the scrambled (on-disc)
  domain**, i.e. before the cluster scrambler is removed. Parity bit index `i = column * 32 + j`: LDC codeword
  `column` (0..303), parity byte `j` (0..31).
* Value bit `1` means the **light** byte, `0` the **dark** byte (`--light`, `--dark` of BdAmp: they must equal the
  pair used by the optimiser, defaults 22 / 9E).
* A cluster that crosses two blocks or radial bands may appear in several records; BdAmp merges them, the later
  record winning where both flag a byte (`OptBits.Merge`).
* BdAmp applies `PF/PV` to the cluster, then solves the remaining LDC parity (flags `QF/QV` request specific parity
  bytes) by changing free background bytes (see `docs/techniques/bd-ldc-parity-steering.md`).
* Each record is 18,820 bytes. A 31 x 31 mm block touches on the order of 10^5 clusters (a couple of GB; estimate from the geometry, not measured).
