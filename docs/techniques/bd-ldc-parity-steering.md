# BD-R: LDC parity steering

Every cluster is 304 RS(248,216) codewords. The drive computes the 32 parity bytes of each codeword from the data handed to
it (scrambled user data + EDC), and they cover about 13 % of the track (9.4 mm of every ~72 mm cluster), spread through the
interleave over the whole block. Without steering those bytes are random noise inside the hologram.

**Method** (`tools/BdAmp/Steer.cs`, hook `BluRayStreamingOptions.ClusterPostProcessor`): parity is linear over GF(256) in the
payload bytes. For every codeword with parity bytes that fall inside a block, choose the desired parity (dark/light byte of
the pattern at that position), then change "free" payload bytes of the *same codeword* that land outside every block
(background noise) so the drive-computed parity equals the pattern. Details:
* up to 48 free bytes per codeword; first try a direct GF(256) solve, otherwise a GF(2) bit solve when an EDC (CRC-32 per
  2052-byte frame, also linear) lies inside the codeword; codewords are solved in order because an EDC byte can fall inside the
  current codeword;
* `--steer-parity on`, or implied by `--opt-bits`; `--steer-verify N` re-encodes N clusters with DIS's own LDC encoder and prints
  `constrained parity bytes match`;
* the pattern ("desired") function gives dark / light / free (-1) for any position; with `--opt-bits` the optimiser's requested
  parity values (QF/QV of BDOPT1) win.

**Evidence.** Simulation: single colour 22 : 1 to 84 : 1, three-channel 7 : 1 to 21 : 1 per colour (SNR in the viewing window).
Container test: 1,337,267 constrained parity bytes re-computed with DIS's LDC encoder, 100 % matched. Real disc: "large visible
gain" (author's project notes). In this repository's synthetic smoke test 2697/2697 constrained parity bytes verify.
Cost: about 60 % longer generation. Limitation: a hologram block must leave enough free background bytes in each codeword; blocks
covering whole codewords have nothing to steer with (`parity bytes forced x/y` is printed).
