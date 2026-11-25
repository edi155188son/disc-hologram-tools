#!/usr/bin/env bash
# Clone Disc Image Studio at the pinned upstream commit and apply dis-patches/ on a local branch.
#   scripts/prepare-dis.sh [dest] [--optional] [--stereo]
#   env: DIS_URL (default upstream GitHub), DIS_COMMIT (pinned base commit)
# Nothing is pushed anywhere; the result is a normal local git checkout (branch holo-tools).
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
dest="${1:-$here/external/DiscImageStudio}"; shift || true
url="${DIS_URL:-https://github.com/JiaFeiMiao-K-Cat/discimagestudio}"
commit="${DIS_COMMIT:-6f0d8fd832f95c87ade6bdcb1e38c5955a7f629f}"
opt=0; stereo=0
for a in "$@"; do case "$a" in --optional) opt=1;; --stereo) stereo=1;; *) echo "unknown option $a" >&2; exit 2;; esac; done
if [ ! -d "$dest/.git" ]; then git clone "$url" "$dest"; fi
cd "$dest"
git checkout -q -B holo-tools "$commit"
id=(-c user.name="dis-patch-apply" -c user.email="noreply@users.noreply.github.com")
apply() { git "${id[@]}" am --3way --quiet "$@"; }
apply "$here"/dis-patches/0*.patch
[ "$opt" = 1 ] && apply "$here"/dis-patches/optional/*.patch
[ "$stereo" = 1 ] && apply "$here"/dis-patches/reference/*.patch
git log --oneline -5
echo "DIS_ROOT=$dest"
