"""Byte-level iterative optimization of BD hologram blocks with "don't care" outside the viewing windows.
Pipeline: BDAF fields (MATLAB) -> per block, polar tiles (rows = tracks, columns = equal angle) -> every cell is mapped
to the real ECC byte slot (cluster, slot) via the DiscImageStudio geometry/layout -> loss = || FFT(h) - FFT(target) ||^2
inside the target's spectral support (+1/-1 windows) plus a low-frequency term (no zero-order picture) -> Adam on
tanh-relaxed byte values with annealing, initialised from double-phase -> binary bytes written to a BDOPT1 file that
BdAmp (--opt-bits) applies before LDC parity steering. Controllable = payload and LDC parity bytes inside a block.
"""
import argparse, struct, time, math, sys, os
import numpy as np, torch, torch.nn.functional as Fn
from bdgeom import Geometry, ecc_table, CBPRU

ap = argparse.ArgumentParser()
ap.add_argument('--inner', type=float, required=True); ap.add_argument('--outer', type=float, required=True)
ap.add_argument('--fields', required=True); ap.add_argument('--out', required=True)
ap.add_argument('--taper', type=float, default=0.0); ap.add_argument('--tile', type=float, default=1.0)
ap.add_argument('--margin', type=float, default=0.04); ap.add_argument('--iters', type=int, default=200)
ap.add_argument('--lr', type=float, default=0.05); ap.add_argument('--device', default='cuda' if torch.cuda.is_available() else 'cpu')
ap.add_argument('--limit-tiles', type=int, default=0); ap.add_argument('--dc-weight', type=float, default=1.0)
ap.add_argument('--log-every', type=int, default=20); ap.add_argument('--center-first', action='store_true'); ap.add_argument('--lvl-fixed', type=float, default=-1.0); ap.add_argument('--lvl-sync', type=float, default=0.0); ap.add_argument('--truth-fixed', type=float, default=None); ap.add_argument('--profile', action='store_true'); ap.add_argument('--gain', type=float, default=1.0)
A = ap.parse_args(); dev = torch.device(A.device)
geo = Geometry(A.inner, A.outer); ctab, ktab, itab = ecc_table()
ctab_t = torch.tensor(ctab, device=dev, dtype=torch.float64)
CELL = 4*74.5e-6                                   # 4 channel bits = 1/3 byte (mm)
def good(nn):                                       # next FFT-friendly size (factors 2,3,5,7)
    while True:
        m_ = nn
        for p_ in (2, 3, 5, 7):
            while m_ % p_ == 0: m_ //= p_
        if m_ == 1: return nn
        nn += 1

class Field: pass
def load(path):
    rot = None
    if '@' in path: path, rot = path.rsplit('@', 1)          # path@deg: same field, block centre moved to angle deg
    f = Field(); b = open(path, 'rb').read(100)
    assert b[:8] == b'BDAF1\0\0\0', path
    f.nv, f.nu, f.interp = struct.unpack_from('<3i', b, 8)
    (f.v0, f.dv, f.u0, f.du, f.rc, thc, f.hv, f.hu, f.lam, f.thr) = struct.unpack_from('<10d', b, 20)
    if rot is not None: thc = float(rot)
    f.thc = math.radians(thc); f.cx, f.cy = f.rc*math.cos(f.thc), f.rc*math.sin(f.thc); f.ca, f.sa = math.cos(f.thc), math.sin(f.thc)
    f.path = path; f.offset = 100
    return f
def tensor_of(f):
    a = np.frombuffer(open(f.path, 'rb').read(), dtype='<f4', offset=100).reshape(f.nv, f.nu, 2)
    return torch.from_numpy(a.copy()).to(dev).permute(2, 0, 1).unsqueeze(0)      # [1,2,nv,nu]

fields = [load(p.strip()) for p in A.fields.split(',')]
carriers = [f for f in fields if f.lam > 0]
blocks = {}
for f in carriers: blocks.setdefault((round(f.cx, 3), round(f.cy, 3)), []).append(f)
print(f'device {dev}; {len(blocks)} block(s); geometry pitch {geo.pitch*1e6:.2f} nm', flush=True)

store = {}                                          # cluster -> [pflag, pval, qflag, qval] (bool arrays), flushed per radial band
fo = open(A.out, 'wb'); fo.write(b'BDOPT1\0\0'); fo.write(struct.pack('<i', 0)); nrec = 0
def flush():
    global nrec
    for cl in sorted(store):
        st = store[cl]; fo.write(struct.pack('<I', int(cl)))
        for arr in st: fo.write(np.packbits(arr, bitorder='little').tobytes())
        nrec += 1
    store.clear(); fo.flush()
def put(cluster, kind, index, bit):
    order = np.argsort(cluster, kind='stable'); cluster, kind, index, bit = cluster[order], kind[order], index[order], bit[order]
    ucl, starts = np.unique(cluster, return_index=True); ends = np.append(starts[1:], len(cluster))
    for cl, a, b in zip(ucl, starts, ends):
        st = store.get(cl)
        if st is None: st = store[cl] = [np.zeros(65536, bool), np.zeros(65536, bool), np.zeros(9728, bool), np.zeros(9728, bool)]
        k, i, v = kind[a:b], index[a:b], bit[a:b]
        p = k == 0; st[0][i[p]] = True; st[1][i[p]] = v[p]
        q = k == 2; st[2][i[q]] = True; st[3][i[q]] = v[q]

def taper_w(f, v, u):
    if A.taper <= 0: return 1.0
    d = torch.minimum(f.hv - v.abs(), f.hu - u.abs()).clamp(min=0)
    return torch.where(d >= A.taper, torch.ones_like(d), torch.sin(0.5*math.pi*d/A.taper)**2)

t_start = time.time(); ntile = 0; stats = []
for key, fl in blocks.items():
    f0 = fl[0]; rc = f0.rc; thc = f0.thc
    ten = [tensor_of(f) for f in fl]
    rmin, rmax = rc - f0.hv, math.hypot(rc + f0.hv, f0.hu)
    nb_r = math.ceil((rmax - rmin)/A.tile); tiles = []
    for ir in range(nb_r):
        r0 = rmin + ir*A.tile; r1 = min(r0 + A.tile, rmax); rt = (r0 + r1)/2
        half = math.asin(min(1.0, f0.hu/max(r0, 1e-9))) + 0.02
        nb_a = math.ceil(2*half*rt/A.tile)
        for ia in range(nb_a):
            a0 = thc - half + ia*A.tile/rt; a1 = a0 + A.tile/rt
            ac = (a0 + a1)/2; xc, yc = rt*math.cos(ac), rt*math.sin(ac)
            dvc = (xc - f0.cx)*f0.ca + (yc - f0.cy)*f0.sa; duc = -(xc - f0.cx)*f0.sa + (yc - f0.cy)*f0.ca
            if abs(dvc) > f0.hv + A.tile or abs(duc) > f0.hu + A.tile: continue
            tiles.append((math.hypot(dvc, duc), r0, r1, rt, a0, a1))
    if A.center_first: tiles.sort()
    if A.limit_tiles: tiles = tiles[:A.limit_tiles]
    print(f'block @ r={rc:.1f}: {len(tiles)} tiles', flush=True)
    prev_r0 = None
    for (_, r0, r1, rt, a0, a1) in tiles:
        if prev_r0 is not None and r0 != prev_r0 and not A.center_first: flush()
        prev_r0 = r0
        if True:
            ntile += 1; t0 = time.time()
            # grid: rows = tracks, cols = angles
            dphi = CELL/rt; m = A.margin
            ncol = good(int(math.ceil((a1 - a0 + 2*m/rt)/dphi)))
            phis = (a0 - m/rt) + dphi*torch.arange(ncol, device=dev, dtype=torch.float64)
            nlo = math.ceil(((r0 - m - geo.inner)/geo.g + float(phis[0]) - geo.offset)/(2*math.pi)) - 1
            nhi = math.floor(((r1 + m - geo.inner)/geo.g + float(phis[-1]) - geo.offset)/(2*math.pi)) + 1
            nrow = good(nhi - nlo + 1)
            n = torch.arange(nlo, nlo + nrow, device=dev, dtype=torch.float64)
            R = geo.inner + geo.g*(2*math.pi*n[:, None] + geo.offset - phis[None, :])
            X, Y = R*torch.cos(phis)[None, :], R*torch.sin(phis)[None, :]
            V = (X - f0.cx)*f0.ca + (Y - f0.cy)*f0.sa; U = -(X - f0.cx)*f0.sa + (Y - f0.cy)*f0.ca
            inb = (V.abs() <= f0.hv) & (U.abs() <= f0.hu)
            if not bool(inb.any()): continue
            # byte slot of every cell
            G = (geo.inner*0 + (R*torch.sqrt(R*R + geo.g**2) + geo.g**2*torch.asinh(R/geo.g))/(2*geo.g) - geo.p0)/74.5e-6
            cl = torch.floor(G/CBPRU); off = G - cl*CBPRU
            s = torch.searchsorted(ctab_t, off.contiguous()).clamp(1, len(ctab) - 1)
            s = torch.where((off - ctab_t[s - 1]).abs() < (ctab_t[s] - off).abs(), s - 1, s)
            gap = (off - ctab_t[s]).abs() > 6
            kind = torch.tensor(ktab, device=dev)[s]; idx = torch.tensor(itab, device=dev)[s]
            bid = cl.long()*76880 + s
            ctrl = (~gap) & ((kind == 0) | (kind == 2)) & inb
            fpos = off - 2760
            syncgap = gap & ((fpos < 0) | (fpos >= 496*1932) | ((fpos % 1932) < 36))     # run-in / run-out / frame sync
            fixed_val = torch.zeros_like(V, dtype=torch.float32)
            fixed_val = torch.where((~gap) & ((kind == 1) | (kind == 3)), torch.full_like(fixed_val, A.lvl_fixed), fixed_val)
            fixed_val = torch.where(syncgap, torch.full_like(fixed_val, A.lvl_sync), fixed_val)
            fixed_val = torch.where(inb, fixed_val, torch.zeros_like(fixed_val))
            fixf = fixed_val.flatten()
            # target S
            S = torch.zeros_like(V, dtype=torch.complex128)
            for f, tt in zip(fl, ten):
                gx = (2*(U - f.u0)/(f.du*(f.nu - 1)) - 1); gy = (2*(V - f.v0)/(f.dv*(f.nv - 1)) - 1)
                grid = torch.stack([gx, gy], -1).float().unsqueeze(0)
                e = Fn.grid_sample(tt, grid, mode='bicubic', padding_mode='zeros', align_corners=True)[0]
                ec = torch.complex(e[0].double(), e[1].double())*taper_w(f, V, U)
                S += torch.conj(ec)*torch.exp(2j*math.pi*V/f.lam)
            S = torch.where(inb, S, torch.zeros_like(S))
            target = A.gain*(2/math.pi)*2*S.real
            T = torch.fft.fft2(target.float())
            P = (T.abs()**2)
            Pb = Fn.avg_pool2d(P[None, None], 5, 1, 2)[0, 0]
            mask = Pb > 1e-3*Pb.max()
            fr = torch.fft.fftfreq(P.shape[0], d=geo.pitch, device=dev); fc = torch.fft.fftfreq(P.shape[1], d=CELL, device=dev)
            dcm = (fr.abs()[:, None] < 1/0.03) & (fc.abs()[None, :] < 1/0.03)
            # variables
            vb, inv = torch.unique(torch.where(ctrl, bid, torch.full_like(bid, -1)), return_inverse=True)
            has_fixed = bool((vb[0] == -1).item()); nv_ = len(vb)
            par = (n.long() % 2)*2 - 1
            dp = torch.where(torch.cos(torch.angle(S) - par[:, None]*torch.arccos(S.abs().clamp(max=1))) > 0, 1.0, -1.0)
            z0 = torch.zeros(nv_, device=dev, dtype=torch.float32).index_add_(0, inv.flatten(), dp.flatten().float())
            z = torch.atanh((torch.sign(z0)*0.8).clamp(-0.99, 0.99))
            fixmask = torch.zeros(nv_, device=dev, dtype=torch.bool)
            if has_fixed: fixmask[0] = True
            invf = inv.flatten(); NN = invf.numel(); shp = inv.shape
            maskc = mask.to(torch.complex64); dcw = (dcm.float()*A.dc_weight).to(torch.complex64)
            tp = ((T.abs()**2)*mask).sum()
            isvar = (~fixmask[invf])
            def hgrid(bv): return torch.where(isvar, bv[invf], fixf).view(shp)
            def snr(bv):
                Fh = torch.fft.fft2(hgrid(bv)); return float(tp/((((Fh - T).abs()**2)*mask).sum()))
            bdp = torch.sign(z); bdp[fixmask] = 0
            s_dp = snr(bdp)
            if dev.type == 'cuda': torch.cuda.synchronize()
            t_prep = time.time() - t0
            m1 = torch.zeros_like(z); m2 = torch.zeros_like(z); b1, b2 = 0.9, 0.999
            tsec = np.zeros(6)
            def tick(k, t_):
                if A.profile:
                    torch.cuda.synchronize(); tn = time.time(); tsec[k] += tn - t_; return tn
                return t_
            for it in range(1, A.iters + 1):
                tq = time.time()
                beta = 1 + 10*it/A.iters
                bv = torch.tanh(beta*z); bv[fixmask] = 0; tq = tick(0, tq)
                Fh = torch.fft.fft2(hgrid(bv)); tq = tick(1, tq)
                E = maskc*(Fh - T) + dcw*Fh; tq = tick(2, tq)
                gh = torch.where(isvar, torch.fft.ifft2(E).real.flatten(), torch.zeros_like(fixf)); tq = tick(3, tq)
                gb = torch.zeros_like(z).index_add_(0, invf, gh); tq = tick(4, tq)
                g = gb*beta*(1 - bv*bv); g[fixmask] = 0
                g /= g.abs().mean() + 1e-12
                m1.mul_(b1).add_(g, alpha=1 - b1); m2.mul_(b2).addcmul_(g, g, value=1 - b2)
                z -= A.lr*(m1/(1 - b1**it))/((m2/(1 - b2**it)).sqrt() + 1e-8); tq = tick(5, tq)
            if A.profile: print('  profile ms/iter: tanh %.1f fft+gather %.1f err %.1f ifft %.1f index_add %.1f adam %.1f' % tuple(tsec/A.iters*1000))
            bfin = torch.where(z >= 0, 1.0, -1.0); bfin[fixmask] = 0
            s_op = snr(bfin)
            def sig(bv):
                Fh = torch.fft.fft2(hgrid(bv)); return float((((Fh*torch.conj(T))*mask).sum().real/tp))
            g_dp, g_op = sig(bdp), sig(bfin)
            if A.truth_fixed is not None:      # evaluation against a world where BIS/EDC really sit at truth level
                if A.truth_fixed > 5:      # random +-1 per slot
                    rv = (torch.randint(0, 2, (int(bid.max()) - int(bid.min()) + 1,), device=dev).float()*2 - 1)[(bid - bid.min())]
                    ft = torch.where((~gap) & ((kind == 1) | (kind == 3)) & inb, rv, torch.zeros_like(fixed_val)).flatten()
                else:
                    ft = torch.where((~gap) & ((kind == 1) | (kind == 3)) & inb, torch.full_like(fixed_val, A.truth_fixed), torch.zeros_like(fixed_val)).flatten()
                def snr_t(bv):
                    Fh = torch.fft.fft2(torch.where(isvar, bv[invf], ft).view(shp)); return float(tp/((((Fh - T).abs()**2)*mask).sum()))
                print(f'    truth BIS/EDC={A.truth_fixed}:  dp {snr_t(bdp):.1f}  opt {snr_t(bfin):.1f}')
            # write bytes whose representative cell is in the tile core
            core = (R >= r0) & (R < r1) & (phis[None, :] >= a0) & (phis[None, :] < a1) & ctrl
            sel = torch.unique(inv[core])
            sel = sel[~fixmask[sel]]
            ids = vb[sel].cpu().numpy(); bits = (bfin[sel] > 0).cpu().numpy()
            clu = ids//76880; slot = ids % 76880
            put(clu, ktab[slot], itab[slot], bits)
            stats.append((s_dp, s_op, g_dp*A.gain, g_op*A.gain))
            print(f'tile {ntile:5d} r {rt:6.2f} {P.shape[0]}x{P.shape[1]}  prep {t_prep:4.1f}s  vars {nv_}  SNR dp {s_dp:6.1f} -> opt {s_op:6.1f}  face amp dp {g_dp*A.gain:5.2f} -> opt {g_op*A.gain:5.2f}  {time.time() - t0:5.1f}s  total {time.time() - t_start:7.0f}s', flush=True)
    del ten
flush(); fo.seek(8); fo.write(struct.pack('<i', nrec)); fo.close()
sd = np.array(stats) if stats else np.zeros((1, 4))
print(f'wrote {A.out}: {nrec} cluster records; median SNR dp {np.median(sd[:,0]):.1f} -> opt {np.median(sd[:,1]):.1f}; median face amplitude (rel. unit target) dp {np.median(sd[:,2]):.2f} -> opt {np.median(sd[:,3]):.2f}; {time.time() - t_start:.0f}s')
