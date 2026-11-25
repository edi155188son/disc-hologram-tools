# BD-R: byte-level optimisation (v3) and PAPR reduction (v4)

**Byte optimiser** (`python/bdopt.py`, GPU, RTX 3090 in the notes): replaces the analytic double-phase decision by iterative
optimisation with "don't care" regions.
* Model: the real BD byte layout from `bdgeom.py` (496 frames x 155 bytes per cluster, sync, DC bits, one BIS byte every 38+1
  bytes), every byte placed at its nominal position; each byte is a 12-channel-bit rectangle of uniform reflectance.
* Variables: payload bytes and LDC parity bytes inside the block (parity then solved by BdAmp steering); EDC, BIS, sync unknown.
* Loss: squared error to the target spectrum **only inside the viewing windows** (support of the target, +/-1 orders) plus a
  low-frequency term (no picture on the disc); everything else is free.
* Method: tanh relaxation + annealing + Adam, 200 iterations, binarise; 1 mm tiles with 40 um overlap margin, initial solution
  double-phase; 1.5 - 3 s per tile; a 31 mm block is about 1085 tiles (35 min), five blocks about 3 h.
* Model SNR in the window (central tile, no BIS noise): double-phase about 11, optimised about 80 - 110; robust to +/-0.3 um
  track position error (still about 3 x double-phase). Output BDOPT1 -> `BdAmp --opt-bits` (this also turns steering on).
* Caveat from the code header: a byte is modelled as a uniform rectangle; real marks are 1-7PP runs plus DC bits; if the
  difference is large the benefit falls back towards double-phase. Real discs: v3 verified better than double-phase (author's notes).

**PAPR reduction** (`python/fields_gpu.py`, v4): per block, 3 colour channels, layered angular-spectrum propagation (band +/-HU
horizontally, +/-delta vertically); the diffuser phase of each layer is free, the layer amplitude fixed. Gerchberg-Saxton style
loop as in OFDM peak reduction: sum layers -> clip |e| at `clip x rms` (default 1.6) -> back-propagate -> keep phase (8 iterations).
A lower peak-to-average ratio lets the whole field be scaled up before the encoder clips, i.e. a brighter hologram of the same
image (p99.5/rms from 3.2 to about 2.5 on a toy block). Real disc: brighter.

Prototypes: `python/prototypes/opt_prototype.py`, `dp_hold.py` (hold-per-byte vs fine grid models), `gpu_fft_profile.py` (FFT / gather timings).
