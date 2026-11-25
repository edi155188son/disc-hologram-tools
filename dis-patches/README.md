# dis-patches: changes to Disc Image Studio

Base: upstream `main` at `6f0d8fd832f95c87ade6bdcb1e38c5955a7f629f` (2026-11-23, "Merge PR #393 ...").
Apply to a *checkout of DIS*, never to this repository: `scripts/prepare-dis.sh` does it on a local branch.
Authors are a placeholder (`Hologram Tools <noreply@example.invalid>`); use `git commit --amend --reset-author`
(or `git rebase --exec`) before you send anything upstream.

## Upstream-able series (apply in order)

| Patch | Concern | Size | Test |
|---|---|---|---|
| `0001-BD-allow-an-explicit-dark-light-byte-pair-in-BluRayR.patch` | `BluRayRunLengthMapping(byte dark, byte light)`; default and inverted behaviour unchanged | 1 file | batched path equals a scalar oracle for 9E/22; default/inverted outputs unchanged |
| `0002-BD-optional-per-cluster-post-processing-hook-in-BluR.patch` | `BluRayStreamingOptions.ClusterPostProcessor` (delegate), per-call, null by default | 1 file | hook runs once per cluster with correct first sector / PSN, edits land before descrambling, sequential and parallel path |

Verified on a fresh upstream checkout with the series applied: Release build of `DiscImageStudio.slnx` with
0 warnings / 0 errors (`-p:EnableWindowsTargeting=true` on Linux), DVD `selftest` passes, and the Blu-ray
architecture test files `BluRayEncodingTests`, `BluRayParallelGenerationTests` (with the new cases),
`BluRayPixelReuseTests`, `BluRayCubicPitchTests` pass when run from a Linux console harness. The full
`ArchitectureTests` program targets `net10.0-windows` + WPF: it compiles but was **not run** here, so run it
on Windows before opening a PR (CONTRIBUTING.md requires it).

Not included (needed before a real PR, per `CONTRIBUTING.md`): a `CHANGELOG.md` Unreleased entry in Chinese and
English, and a `docs/VALIDATION.md` entry that says what was and was not run (upstream writes "not burned" explicitly).

### Overlap with the in-progress hologram layer
`/home/claude/dis_holo` (branch `holo-layer`, another agent) carries its own copy of the pair constructor and an
*internal* `IBluRayBatchPayloadMapping.PostProcessCluster` hook used by `BluRayHologram`. If the hologram layer is
what gets proposed, patch 0001 should be folded into it (identical idea) and patch 0002 is **not needed upstream**:
the in-tree feature does its parity steering through the mapping, not through a public delegate. Keep 0002 only
for the external tools (`BdAmp`, `BdCgh`) that live outside DIS.

## `optional/`
* `0001-BD-do-not-cap-parallel-cluster-workers-at-16.patch`: removes `Math.Min(16, ...)` on BD generator workers. Only
  helps with more than 17 logical processors; not benchmarked beyond one machine. Do not upstream without numbers.

## `reference/`
* `0001-Add-DVD-multi-view-stereo-layers.patch`: the earlier DVD multi-view (lenticular-style) stereo layer, 948
  insertions over 9 files including WPF UI and localisation. Re-based onto `6f0d8fd` (originally 28310c87); applies
  with `git am --3way` and `DvdImageSolver` builds. WPF UI was not opened (Linux). Kept for reference and for
  `tools/HoloCgh --lenti`; it is a feature-sized PR and would need its own tests, docs and localisation review.
