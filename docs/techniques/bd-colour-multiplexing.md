# BD-R: colour by frequency multiplexing (rainbow geometry)

One block carries three colour channels on the same area, each with its own carrier period `Lambda_c = lambda_c / sin(theta0)`
(R, G, B, e.g. 630 / 532 / 460 nm at theta0 = 13.7 deg gives 2.660 / 2.246 / 1.942 um = 8.3 / 7.0 / 6.1 tracks). At the design
angle each carrier diffracts its own colour; in white light the face appears coloured. BdAmp evaluates, per byte,
`S = sum_c conj(e_c) exp(i 2 pi v / Lambda_c)` over every carrier field covering the point (`--combine sum`) and double-phase encodes `S`;
double-phase is linear for the +1 order, so channels do not interfere and there are no side-band ghosts of an interleaved layout.

Parameters per channel: horizontal +/-8 deg (parallax), vertical +/-delta (the rainbow slit: sets colour purity and the vertical
range with correct colour), depth +/-4 mm in 12 layers, amplitude = sqrt(linear brightness) of the colour texture, white
balance R:G:B = 1 : 1 : 0.9. The `color_bd` disc had five blocks (theta0 13.7 / 13.7 (LED spectrum) / 10 / 17.5 / 13.7 (delta 0.5)
deg), each 31 x 31 mm at r = 41 mm, `--taper 1.5` (cos^2 edge apodisation) and parity steering.

Simulation numbers (single channel about 25 : 1, three channels about 7 : 1 each; with parity steering 22 -> 84 and 7 -> 21):
`matlab/bd_color/color_snr.m`, `color_sim.m`. Full parallax in colour was not attempted: every channel also diffracts the other
two colours, giving six cross-colour ghosts 2 - 3 deg above and below the image inside the +/-8 deg view range.
The later "carrier forbidden zones" rule (choosing carriers so cross-colour orders miss each channel's window) is recorded in the author's
project notes, not in the code here: `fields_gpu.py` / `color_avatar.m` use the five fixed rows above.

Code: `matlab/bd_color/color_avatar.m` (or GPU `python/fields_gpu.py`), `BdAmp --encoding dp --combine sum --steer-parity on --taper 1.5`.
Memory: about 20 GB of RAM for five blocks of BDAF1 (18 GB of field files).
