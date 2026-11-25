"""Far-field simulation of the REAL on-disc channel pattern (DiscImageStudio ECC + 17PP encoder) vs the byte model.
plan: choose a tile in a block and list the clusters it touches.
sim : for each version (channel file + its fields) compute, inside the viewing-window mask of the target:
      P_sig = |<F,T>|^2/<T,T> (face brightness), SNR, P_band, under several reflectance models."""
import argparse, json, math, struct, sys, time, os
import numpy as np, torch, torch.nn.functional as Fn
from bdgeom import Geometry, BIT_MM, CBPRU
from chio import read_ch

ap = argparse.ArgumentParser()
ap.add_argument('mode', choices=['plan', 'sim'])
ap.add_argument('--inner', type=float, default=23.987042157339335); ap.add_argument('--outer', type=float, default=57.987960341783236)
ap.add_argument('--fields', help='plan: carrier fields of the block (headers only)')
ap.add_argument('--tile', type=float, default=0.5); ap.add_argument('--dv', type=float, default=0.0); ap.add_argument('--du', type=float, default=0.0)
ap.add_argument('--plan', default='tile_plan.json')
ap.add_argument('--versions', help='sim: name=chanfile=field1,field2,field3;...')
ap.add_argument('--taper', type=float, default=1.5); ap.add_argument('--seed', type=int, default=1)
ap.add_argument('--out', default='sim_real_result.txt')
A = ap.parse_args()
geo = Geometry(A.inner, A.outer)
DARK, LIGHT = 0x9E, 0x22

def good(nn):
    while True:
        m_ = nn
        for p_ in (2, 3, 5, 7):
            while m_ % p_ == 0: m_ //= p_
        if m_ == 1: return nn
        nn += 1

class Field: pass
def load(path):
    f = Field(); b = open(path, 'rb').read(108)
    assert b[:8] == b'BDAF1\0\0\0', path
    f.nv, f.nu, f.interp = struct.unpack_from('<3i', b, 8)
    (f.v0, f.dv, f.u0, f.du, f.rc, thc, f.hv, f.hu, f.lam, f.thr) = struct.unpack_from('<10d', b, 20)
    f.thc = math.radians(thc); f.cx, f.cy = f.rc*math.cos(f.thc), f.rc*math.sin(f.thc); f.ca, f.sa = math.cos(f.thc), math.sin(f.thc)
    f.path = path; return f

def tile_grid(f0, dev):
    xc = f0.cx + A.dv*f0.ca - A.du*f0.sa; yc = f0.cy + A.dv*f0.sa + A.du*f0.ca
    rt, ac = math.hypot(xc, yc), math.atan2(yc, xc); h = A.tile/2
    r0, r1, a0, a1 = rt - h, rt + h, ac - h/rt, ac + h/rt
    dphi = BIT_MM/rt; ncol = good(int(math.ceil((a1 - a0)/dphi)))
    phis = a0 + dphi*torch.arange(ncol, device=dev, dtype=torch.float64)
    nlo = math.ceil(((r0 - geo.inner)/geo.g + float(phis[0]) - geo.offset)/(2*math.pi))
    nhi = math.floor(((r1 - geo.inner)/geo.g + float(phis[-1]) - geo.offset)/(2*math.pi))
    nrow = good(nhi - nlo + 1)
    n = torch.arange(nlo, nlo + nrow, device=dev, dtype=torch.float64)
    R = geo.inner + geo.g*(2*math.pi*n[:, None] + geo.offset - phis[None, :])
    return R, phis, n, (rt, ac)

def G_of(R):
    g = geo.g; return ((R*torch.sqrt(R*R + g*g) + g*g*torch.asinh(R/g))/(2*g) - geo.p0)/BIT_MM

if A.mode == 'plan':
    fl = [load(p) for p in A.fields.split(',')]; f0 = fl[0]
    R, phis, n, (rt, ac) = tile_grid(f0, torch.device('cpu'))
    G = G_of(R); cl = torch.floor(G/CBPRU).long()
    u = torch.unique(cl).numpy(); c0 = int(u.min()); c1 = int(u.max())
    plan = dict(start_sector=c0*32, clusters=c1 - c0 + 1, touched=len(u), rt=rt, ac=ac, shape=list(R.shape), tile=A.tile, dv=A.dv, du=A.du)
    json.dump(plan, open(A.plan, 'w'), indent=1)
    open(os.path.splitext(A.plan)[0] + '_only.txt', 'w').write('\n'.join(str(int(c) - c0) for c in u))
    print(plan); sys.exit(0)

# ---------------- sim ----------------
dev = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
plan = json.load(open(A.plan)); t0 = time.time()
vers = []
for item in A.versions.split(';'):
    name, chf, fls = item.split('='); vers.append((name, chf, [load(p) for p in fls.split(',')]))
f0 = vers[0][2][0]
R, phis, n, _ = tile_grid(f0, dev); shp = R.shape
G = G_of(R); clg = torch.floor(G/CBPRU); off = (G - clg*CBPRU)
offb = torch.floor(off).long(); clg = clg.long()
X, Y = R*torch.cos(phis)[None, :], R*torch.sin(phis)[None, :]
print(f'grid {shp}, device {dev}', flush=True)

# ECC slot of every cell (nearest byte centre within 6 channel bits)
f_ = np.repeat(np.arange(496), 155); j_ = np.tile(np.arange(155), 496)
mid = j_*8 + 4; dc = np.where(mid < 25, 0, 1 + (mid - 25)//45)
centre = 2760 + f_*1932 + 30 + (mid + dc)*1.5
order = np.argsort(centre); cs = torch.tensor(centre[order], device=dev); orig = torch.tensor(order, device=dev)
s = torch.searchsorted(cs, off.contiguous()).clamp(1, len(cs) - 1)
s = torch.where((off - cs[s - 1]).abs() < (cs[s] - off).abs(), s - 1, s)
inbyte = (off - cs[s]).abs() <= 6
slot = orig[s]                                   # frame-major ECC index (f*155+j)
grp, pos = j_//39, j_ % 39
kind_np = np.where((pos == 38) & (grp < 3), 3, 0)   # BIS
r_ = f_//2; ldccol = j_ - j_//39; c_ = 2*((ldccol + (3*r_) % 152) % 152) + f_ % 2
serial = c_*216 + r_
kind_np = np.where(kind_np == 3, 3, np.where(r_ >= 216, 2, np.where(serial % 2052 >= 2048, 1, 0)))
kind = torch.tensor(kind_np, device=dev)[slot]
# DC segment id (polarity segment) of every cell
fo = (off - 2760); frame = torch.floor(fo/1932).long(); posf = fo - frame*1932
dbit = (posf - 30)/1.5
seg = torch.where(dbit < 26, torch.zeros_like(frame), 1 + torch.floor((dbit - 26)/46).long())
inframe = (frame >= 0) & (frame < 496)
segid = torch.where(inframe, (clg*496 + frame)*32 + seg, clg*496*32 - 1 - (fo >= 0).long())
def rnd_bit(ids, seed):
    h = (ids*0x9E3779B1 + seed*0x7F4A7C15) & 0xFFFFFFFF
    h = (h ^ (h >> 15))*0x2C1B3C6D & 0xFFFFFFFF; h = (h ^ (h >> 12))*0x297A2D39 & 0xFFFFFFFF
    return ((h ^ (h >> 15)) & 1)

def target_of(fl):
    S = torch.zeros(shp, device=dev, dtype=torch.complex128)
    for f in fl:
        a = np.fromfile(f.path, dtype='<f4', offset=100).reshape(f.nv, f.nu, 2)
        tt = torch.from_numpy(a).to(dev).permute(2, 0, 1).unsqueeze(0)
        V = (X - f.cx)*f.ca + (Y - f.cy)*f.sa; U = -(X - f.cx)*f.sa + (Y - f.cy)*f.ca
        gx = (2*(U - f.u0)/(f.du*(f.nu - 1)) - 1); gy = (2*(V - f.v0)/(f.dv*(f.nv - 1)) - 1)
        e = Fn.grid_sample(tt, torch.stack([gx, gy], -1).float().unsqueeze(0), mode='bicubic', padding_mode='zeros', align_corners=True)[0]
        d = torch.minimum(f.hv - V.abs(), f.hu - U.abs()).clamp(min=0)
        w = torch.where(d >= A.taper, torch.ones_like(d), torch.sin(0.5*math.pi*d/A.taper)**2) if A.taper > 0 else 1.0
        S += torch.conj(torch.complex(e[0].double(), e[1].double())*w)*torch.exp(2j*math.pi*V/f.lam)
        del tt, a
    T = torch.fft.fft2(((4/math.pi)*S.real).float())
    P = T.abs()**2; Pb = Fn.avg_pool2d(P[None, None], 5, 1, 2)[0, 0]
    return T, Pb > 1e-3*Pb.max()

lines = []
def out(sx): print(sx, flush=True); lines.append(sx)
out(f'tile {A.tile} mm at block offset dv={A.dv} du={A.du}; grid {tuple(shp)}; plan {plan["start_sector"]}+{plan["clusters"]}cl')
res = {}
for name, chf, fl in vers:
    start, cb, idx, Pk, E = read_ch(chf)
    c0 = start//32
    lut = torch.full((int(idx.max()) + 2,), -1, dtype=torch.long, device=dev); lut[torch.tensor(idx, device=dev).long()] = torch.arange(len(idx), device=dev)
    rel = (clg - c0); ok = (rel >= 0) & (rel < len(lut)); row = torch.where(ok, lut[rel.clamp(0, len(lut) - 1)], torch.full_like(rel, -1))
    assert bool((row >= 0).all()), f'{name}: tile touches clusters not in channel file'
    Pt = torch.tensor(Pk, device=dev); Et = torch.tensor(E, device=dev)
    def bit_at(o):
        o = o.clamp(0, cb - 1); return ((Pt[row, o >> 3] >> (7 - (o & 7))) & 1).to(torch.int16)
    lv = bit_at(offb)
    # run length containing each cell (both sides, capped)
    left = torch.zeros(shp, dtype=torch.int16, device=dev); right = torch.zeros_like(left)
    aliveL = torch.ones(shp, dtype=torch.bool, device=dev); aliveR = aliveL.clone()
    for k in range(1, 16):
        aliveL &= bit_at(offb - k) == lv; left += aliveL
        aliveR &= bit_at(offb + k) == lv; right += aliveR
    L = (1 + left + right).float()
    byte = Et[row, slot].long()
    T, mask = target_of(fl)
    tp = ((T.abs()**2)*mask).sum()
    pol_r = rnd_bit(segid, A.seed).to(torch.int16)
    models = {}
    for oth in (-1.0, -0.6, 0.0):
        hb = torch.where(byte == LIGHT, 1.0, torch.where(byte == DARK, -1.0, oth)); models[f'byte(other={oth:+.1f})'] = torch.where(inbyte, hb, torch.zeros_like(hb))
    models['linear, greedy DC'] = lv.float()
    for d in (0.5, 1.0, 1.5):
        a = (1 - d/L).clamp(min=0)
        models[f'mark shrink {d}T, random DC'] = -((lv ^ pol_r).float()*a)
        if d == 1.0: models['mark shrink 1T, greedy DC'] = -(lv.float()*a)
    athr = torch.where(L <= 2, 0.0, torch.where(L <= 3, 0.5, 1.0))
    models['2T absent 3T half, random DC'] = -((lv ^ pol_r).float()*athr)
    # effective byte levels (payload cells only, polarity-averaged expectation is what a hologram can use)
    pay = inbyte & (kind == 0)
    for mname, h in models.items():
        h = h - h.mean()
        F = torch.fft.fft2(h.float())
        g = ((F*torch.conj(T))*mask).sum()/tp
        psig = float((g.abs()**2)*tp); pn = float((((F - g*T).abs()**2)*mask).sum()); pband = float(((F.abs()**2)*mask).sum())
        lvls = [float(h[pay & (byte == DARK)].mean()), float(h[pay & (byte == LIGHT)].mean()), float(h[inbyte & (byte != DARK) & (byte != LIGHT)].mean())]
        ptot = float((F.abs()**2).sum())
        fr_ = torch.fft.fftfreq(shp[0], d=geo.pitch, device=dev); fc_ = torch.fft.fftfreq(shp[1], d=BIT_MM, device=dev)
        low = ((fr_.abs()[:, None] < 1/0.03) & (fc_.abs()[None, :] < 1/0.03))
        plow = float(((F.abs()**2)*low).sum())
        res[(name, mname)] = (psig, psig/max(pn, 1e-30), pband, lvls, ptot, plow)
    fr = float((byte[pay] == DARK).float().mean() + (byte[pay] == LIGHT).float().mean())
    out(f'{name}: payload cells that are 9E/22: {fr*100:.1f}%  (cells in bytes {float(inbyte.float().mean())*100:.1f}%)  {time.time()-t0:.0f}s')
    del Pt, Et, T, mask, models, L, lv, byte
    torch.cuda.empty_cache() if dev.type == 'cuda' else None

names = [v[0] for v in vers]; base = names[0]
mnames = sorted({k[1] for k in res}, key=lambda s_: list(dict.fromkeys(k[1] for k in res)).index(s_))
out('\nP_sig = power of the target-shaped (face) component in the viewing window, relative to ' + base)
out('model'.ljust(34) + ''.join(f'{nm:>22}' for nm in names))
for m in mnames:
    out(m.ljust(34) + ''.join(f'{res[(nm, m)][0]/res[(base, m)][0]:8.2f} (SNR{res[(nm, m)][1]:6.1f})' for nm in names))
out('\nP_band (all light in the viewing window: face + in-band noise) / P_total(AC) / P_low (<1/30um^-1, surface picture), relative to ' + base)
for m in mnames:
    out(m.ljust(34) + ''.join(f'{res[(nm, m)][2]/res[(base, m)][2]:7.2f}/{res[(nm, m)][4]/res[(base, m)][4]:5.2f}/{res[(nm, m)][5]/max(res[(base, m)][5],1e-30):6.2f}' for nm in names))
out('\neffective levels (mean of model value, zero-mean): 9E payload / 22 payload / other bytes, in ' + base)
for m in mnames:
    l = res[(base, m)][3]; out(m.ljust(34) + f'{l[0]:+.3f} / {l[1]:+.3f} / {l[2]:+.3f}')
open(A.out, 'w').write('\n'.join(lines) + '\n')
