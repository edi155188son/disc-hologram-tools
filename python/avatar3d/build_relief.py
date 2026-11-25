# -*- coding: utf-8 -*-
"""Build a 2.5D textured relief of the avatar (source.jpg) for hologram / multi-view simulation.

Outputs (all aligned to source.jpg, 1079x1079 px):
  depth16.png / depth8.png   white = near, black = far / background (DiscImageStudio depth-map convention)
  mask.png                   object mask
  regions.png                debug colour map of the semantic regions
  avatar_relief.obj/.mtl     textured height-field mesh (front surface + side walls + flat back), mm
  avatar_relief.stl          same solid without texture (binary STL, mm)
  pointcloud.csv             x,y,z (mm), intensity 0..1 of the visible surface (point-source CGH input)
Scale: PX_MM mm per pixel; depth px use the same scale.
"""
import json
import struct
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from skimage import color

PX_MM = 0.05          # 1079 px -> 54 mm wide model
STEP = 2              # mesh / point-cloud sampling step (px)
SRC = "source.jpg"

im8 = np.asarray(Image.open(SRC).convert("RGB"))
im = im8.astype(float) / 255
Hh, Ww = im.shape[:2]
hsv = color.rgb2hsv(im)
H, S, V = hsv[..., 0] * 360, hsv[..., 1], hsv[..., 2]
yy, xx = np.mgrid[0:Hh, 0:Ww].astype(float)


def poly_mask(pts):
    m = Image.new("L", (Ww, Hh), 0)
    ImageDraw.Draw(m).polygon([tuple(p) for p in pts], fill=1)
    return np.asarray(m).astype(bool)


def disk(cx, cy, r):
    return (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r


# ---------------------------------------------------------------- background / object mask
outline = (V < 0.45) & (S < 0.45)                     # dark, desaturated line art
ol = ndi.binary_dilation(outline, iterations=2)
bglike = (H > 195) & (H < 240) & (S > 0.33) & (V < 0.80) & ~ol
lab, _ = ndi.label(bglike)
seeds = set(np.unique(lab[0, :])) | set(np.unique(lab[:, 0])) | set(np.unique(lab[:, -1])) \
    | set(np.unique(lab[-1, :60])) | set(np.unique(lab[-1, -60:]))
seeds.discard(0)
bg = np.isin(lab, list(seeds))

TORSO = [(205, 1079), (212, 1035), (300, 998), (305, 955), (335, 920), (400, 898), (540, 900), (680, 895),
         (760, 915), (805, 950), (830, 995), (915, 1035), (925, 1079)]
torso_poly = poly_mask(TORSO)
upper = ~bg & (yy < 905)
obj = ndi.binary_fill_holes(upper | torso_poly)
obj = ndi.binary_opening(obj, iterations=2)
lab2, n2 = ndi.label(obj)
obj = lab2 == (np.argmax(ndi.sum(obj, lab2, range(1, n2 + 1))) + 1)

# ---------------------------------------------------------------- semantic regions
warm = (H < 70) | (H > 340)
# headset parts traced from the drawing (polygons, px)
cupL = poly_mask([(198, 650), (215, 632), (235, 625), (262, 640), (285, 665), (300, 700), (298, 740), (292, 790),
                  (275, 815), (240, 812), (225, 795), (210, 760), (200, 720)])
cupR = poly_mask([(760, 700), (790, 668), (830, 640), (880, 635), (898, 660), (906, 700), (902, 790), (880, 830),
                  (855, 848), (800, 842), (770, 835), (752, 795), (755, 740)])
band = poly_mask([(868, 515), (895, 520), (900, 535), (880, 600), (868, 642), (845, 642), (845, 600), (860, 530)])
boom = np.zeros_like(obj)
for t in np.linspace(0, 1, 200):                          # mic boom: cup -> mic capsule
    x = 790 + (650 - 790) * t; y = 822 + (925 - 822) * t
    boom |= disk(x, y, 13)
mic = disk(612, 940, 38)
obj = obj | cupL | cupR | band            # the blue LED ring has background colours -> force the traced parts in
headset = obj & (cupL | cupR | band | boom | mic)
face_ell = ((xx - 545) / 300) ** 2 + ((yy - 600) / 335) ** 2 <= 1          # chin bottom ~ y 935
torso = obj & torso_poly & ~headset & ~face_ell
head = obj & ~headset & ~torso
hair = head & (S < 0.18) & (H > 28) & (yy < 640)
hair = ndi.binary_opening(ndi.gaussian_filter(hair.astype(float), 4) > 0.5, iterations=2)

# ---------------------------------------------------------------- depth (px units, + toward viewer)
def inflate(mask, height, power=0.5):
    d = ndi.distance_transform_edt(mask)
    if d.max() == 0:
        return d
    return height * (d / d.max()) ** power


def smooth_in(mask, z, sigma):
    w = ndi.gaussian_filter(mask.astype(float), sigma)
    v = ndi.gaussian_filter(np.where(mask, z, 0.0), sigma)
    return np.where(mask, v / np.maximum(w, 1e-6), 0.0)


# head: ellipsoid + inflation of the whole head silhouette (spikes) + hair thickness on the bangs
cx, cy, a, b, c = 545, 600, 365, 390, 300
q = 1 - ((xx - cx) / a) ** 2 - ((yy - cy) / b) ** 2
z_ell = c * np.sqrt(np.clip(q, 0, None))
z_head = np.maximum(z_ell, inflate(head, 230, 0.5))
z_head += 18 * ndi.gaussian_filter(hair.astype(float), 6)                 # bangs lie on the forehead
z_head += 10 * np.exp(-(((xx - 468) / 16) ** 2 + ((yy - 738) / 20) ** 2))  # nose
z_head -= 6 * np.exp(-(((xx - 360) / 55) ** 2 + ((yy - 690) / 38) ** 2))   # eye sockets
z_head -= 6 * np.exp(-(((xx - 640) / 55) ** 2 + ((yy - 690) / 38) ** 2))
z_head = smooth_in(head, z_head, 3)

# headset: cups sit on the head sides, boom + capsule in front of the chin
z_hs = np.zeros_like(z_ell)


def local_base(m, width=25):
    """mean head height in a ring around region m (so the part sits ON the head surface)."""
    ring = ndi.binary_dilation(m, iterations=width) & ~m & head
    return float(z_head[ring].mean()) if ring.any() else 120.0


for part, lift, bulge in ((cupL, 22, 35), (cupR, 22, 35), (band, 15, 12)):
    m = headset & part & ~boom & ~mic
    z_hs = np.where(m, local_base(part) + lift + inflate(m, bulge, 0.6), z_hs)
mb = headset & boom & ~mic
z_hs = np.where(mb, max(175, local_base(boom, 15) + 15) + inflate(mb, 10), z_hs)
mm = headset & mic
z_hs = np.where(mm, 185 + inflate(mm, 30, 0.5), z_hs)
z_hs = smooth_in(headset, z_hs, 2)

# torso: hoodie behind the head, epaulets slightly raised, neck recessed
z_t = 40 + inflate(torso, 110, 0.5)
neck = torso & warm & (S > 0.15) & (V > 0.5) & (xx > 430) & (xx < 640)
z_t -= 25 * ndi.gaussian_filter(neck.astype(float), 6)
ep = torso & (H > 40) & (H < 70) & (S > 0.15) & (V > 0.85)
z_t += 12 * ndi.gaussian_filter(ep.astype(float), 3)
z_t = smooth_in(torso, z_t, 3)

z = np.where(head, z_head, 0.0)
z = np.where(torso, z_t, z)
z = np.where(headset, z_hs, z)
z = np.where(obj, np.maximum(z, 5.0), 0.0)

# ---------------------------------------------------------------- images
zmax = z.max()
d16 = np.where(obj, 1 + (z / zmax) * 65534, 0).astype(np.uint16)
Image.fromarray(d16).save("depth16.png")
Image.fromarray((np.where(obj, 20 + z / zmax * 235, 0)).astype(np.uint8)).save("depth8.png")
Image.fromarray((obj * 255).astype(np.uint8)).save("mask.png")
reg = np.zeros((Hh, Ww, 3), np.uint8)
reg[torso] = (60, 110, 220); reg[head] = (240, 200, 160); reg[hair] = (255, 245, 200); reg[headset] = (60, 220, 120)
Image.fromarray(reg).save("regions.png")
rgba = np.dstack([im8, (obj * 255).astype(np.uint8)])
Image.fromarray(rgba).save("texture_rgba.png")

# ---------------------------------------------------------------- mesh: front height field + walls + back
ys = np.arange(0, Hh, STEP); xs = np.arange(0, Ww, STEP)
G = obj[np.ix_(ys, xs)]
G = ndi.binary_opening(G, iterations=1)
Z = z[np.ix_(ys, xs)]
ny, nx = G.shape
X = (xs - Ww / 2) * PX_MM
Y = (Hh / 2 - ys) * PX_MM
BACK = -1.0  # mm, flat back plate thickness below z=0

vid = -np.ones((ny, nx), int)
verts, uvs = [], []
for j in range(ny):
    for i in range(nx):
        if G[j, i]:
            vid[j, i] = len(verts)
            verts.append((X[i], Y[j], Z[j, i] * PX_MM))
            uvs.append((xs[i] / (Ww - 1), 1 - ys[j] / (Hh - 1)))
nf = len(verts)
bid = np.where(vid >= 0, vid + nf, -1)                    # back vertices (same order)
verts += [(x, y, BACK) for (x, y, _) in verts]
uvs += uvs[:]
faces = []
for j in range(ny - 1):
    for i in range(nx - 1):
        a_, b_, c_, d_ = vid[j, i], vid[j, i + 1], vid[j + 1, i + 1], vid[j + 1, i]
        if min(a_, b_, c_, d_) >= 0:
            faces.append((a_, d_, c_)); faces.append((a_, c_, b_))                   # front (CCW seen from +z)
            A, B, C, D = bid[j, i], bid[j, i + 1], bid[j + 1, i + 1], bid[j + 1, i]
            faces.append((A, C, D)); faces.append((A, B, C))                          # back
# boundary edges of the quad grid -> side walls
from collections import Counter
ec = Counter()
for (p, q_, r) in faces[:]:
    if p < nf:
        for e in ((p, q_), (q_, r), (r, p)):
            ec[e] += 1
bnd = [e for e in ec if (e[1], e[0]) not in ec]
for (p, q_) in bnd:
    faces.append((q_, p, p + nf)); faces.append((q_, p + nf, q_ + nf))
print(f"mesh: {len(verts)} verts, {len(faces)} tris, boundary edges {len(bnd)}")

with open("avatar_relief.mtl", "w") as fh:
    fh.write("newmtl avatar\nKd 1 1 1\nmap_Kd source.jpg\n")
with open("avatar_relief.obj", "w") as fh:
    fh.write("mtllib avatar_relief.mtl\no avatar_relief\n")
    for v in verts:
        fh.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
    for t in uvs:
        fh.write(f"vt {t[0]:.5f} {t[1]:.5f}\n")
    fh.write("usemtl avatar\n")
    for f in faces:
        fh.write(f"f {f[0]+1}/{f[0]+1} {f[1]+1}/{f[1]+1} {f[2]+1}/{f[2]+1}\n")

V3 = np.array(verts)
with open("avatar_relief.stl", "wb") as fh:
    fh.write(b"avatar relief (mm)".ljust(80, b" ")); fh.write(struct.pack("<I", len(faces)))
    for f in faces:
        p0, p1, p2 = V3[f[0]], V3[f[1]], V3[f[2]]
        nrm = np.cross(p1 - p0, p2 - p0); ln = np.linalg.norm(nrm); nrm = nrm / ln if ln else nrm
        fh.write(struct.pack("<12fH", *nrm, *p0, *p1, *p2, 0))

# ---------------------------------------------------------------- point cloud (visible surface)
lum = 0.299 * im[..., 0] + 0.587 * im[..., 1] + 0.114 * im[..., 2]
with open("pointcloud.csv", "w") as fh:
    fh.write("x_mm,y_mm,z_mm,intensity,r,g,b\n")
    for j, y0 in enumerate(ys):
        for i, x0 in enumerate(xs):
            if G[j, i]:
                r_, g_, b_ = im8[y0, x0]
                fh.write(f"{X[i]:.3f},{Y[j]:.3f},{Z[j, i]*PX_MM:.3f},{lum[y0, x0]:.4f},{r_},{g_},{b_}\n")

info = {"px_mm": PX_MM, "size_mm": [Ww * PX_MM, Hh * PX_MM], "depth_max_mm": float(zmax * PX_MM),
        "back_mm": BACK, "points": int(G.sum()), "verts": len(verts), "tris": len(faces)}
json.dump(info, open("model_info.json", "w"), indent=1)
print(info)
