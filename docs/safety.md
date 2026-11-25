# Safety, scope, wording

## What these tools do
They generate **disc images** (ISO files) whose *user data* is chosen so that the burned marks form a grating or a hologram.
The image is burned with any ordinary burner in the normal way ("write image file to disc", low speed). The tools do not
modify, patch or flash drive firmware, do not change a drive's write strategy, and do not read or write anything but image
files. Keep it that way in public text: say "data pattern", "image", "calibrated geometry", not "firmware" or "drive tweak".
Calibration constants (`--inner`, `--outer`, `--pitch-*`, sector count) describe the disc's recorded spiral, as in DIS.

## What they must not be used for
Do not describe or advertise anything as copy protection circumvention, forged media identifiers, security holograms
or imitation of genuine hologram labels. The output is a diffraction pattern on a burnt dye layer, visible as a
diffraction effect under a point source; it has no relationship to licensed-media authentication features.
The images stored on a disc follow the user's rights in the pictures used.

## Laser and bright-source viewing
The designs are meant to be viewed with a point source: a phone torch, an LED bead or an expanded laser reflected from a wall.
* Never look into a direct or specular laser beam, including the reflection from a disc: a disc is a mirror and a
  reflected 532 nm beam can exceed eye-safe limits. Use a low-power laser pointer only, expanded and diffuse, on a wall,
  with the room dark and no reflective surfaces; follow your local laser-pointer rules.
* The best-looking diffraction angles are wide (14 - 38 deg); keep the beam path out of eye height and away from other people.
* LEDs: very bright 1 W beads are also hazardous at close range; view from 50 cm or more.
* Burning: low speed, blank media, ordinary precautions of burning; a failed burn only wastes a disc.

## Wording for upstream or public posts
* Say what was and what was not tested (`docs/VALIDATION.md` of DIS does this explicitly: "not burned", "Windows only").
* Hardware evidence is per burner / media brand; claim only what was seen (Pioneer BDR-S12JX with Verbatim BD-R,
  CMC MAG. AM3 DVD-R in the notes).
* AI assistance: see `docs/cleanup.md` and the evaluation (upstream already has AI-co-authored commits, but disclose anyway).
