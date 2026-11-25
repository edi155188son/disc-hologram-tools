# BdAmp

BD-R image generator for radial-carrier holograms. Reads BDAF1 fields (`docs/formats/BDAF1.md`), evaluates for every payload byte
at its nominal physical position (after LDC interleave / BIS) whether it is dark (`--dark`, 9E) or light (`--light`, 22), optionally steers the
LDC parity bytes (`Steer.cs`) and applies optimised bytes (`OptBits.cs`, BDOPT1), and streams a full BD ISO (25 GB;
needs 1 - 20 GB of RAM depending on the field files).

```
BdAmp --inner R --outer R [--sectors 12219392] [--pitch-linear a --pitch-quadratic b --pitch-cubic c] [--start-angle deg] [--max-radius mm]
      [--dark 9E] [--light 22] [--encoding lee|dp] [--combine first|sum] [--taper mm]
      [--steer-parity on] [--steer-verify N] [--opt-bits a.bdopt[,b.bdopt] [--load-fields] [--check-coverage 1]]
      --fields a.bdaf[,b.bdaf[@angle]...] --output out.iso
developer: --geo-check s1,s2,..  --bench N  --range-start S --range-clusters N (raw data of a sector range)
channel:   BdAmp chan --inner R --outer R --start-sector S (--data F | --data-file ISO --data-offset-sector S --clusters N | --fill-scrambled XX,..) [--only list] --out x.bdch
```

Requires DIS with `dis-patches/0001` and `0002`. Self-check lines to look at: `track parity self-check: 0/2000` (double-phase; must be 0),
`Parity steering ... forced x/y`, `Verify ... constrained parity bytes match`, `Optimized bytes applied`.
Burn with ImgBurn "write image file to disc" on BD-R, lowest speed; images below 8.5 GB are refused by ImgBurn (pad with zeros, `--max-radius` for partial discs).
