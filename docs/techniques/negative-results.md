# What did not work (so nobody repeats it)

* **AA/44 on BD-R** (DIS default pair): weak contrast, no imaging in bd1/bd2. Use 9E/22.
* **White-light image-plane hologram on DVD-R**: only an ordinary pattern appears under LED / torch light.
* **Error diffusion / IFTA instead of direct binarisation on DVD**: EFM noise exceeds binarisation noise; no visible gain, over 30 % less
  brightness (exception: regular lattices with many points, 49, where intermodulation forms a grid and error diffusion 0.5 was needed).
* **Tangential carriers below about 5 um on DVD**: blurred (about 2 bytes per period).
* **92 grating order 14.5 deg**: copies of the F smear at the byte-grating order; use only the 1.3 deg main image.
* **Sampling at the wrong total sector count**: ImgBurn's 2298496 instead of DIS's 2297888 skews the spiral (6 mm squares become arcs,
  about 115 deg at r = 28 mm).
* **Band-pass sigma-delta along the tracks** (`experimental/BdAmp-SigmaDelta`, `matlab_sigma_delta`): simulation only; no disc.
* **Colour full parallax**: cross-colour ghosts overlap the view range; limited to rainbow geometry.
