"""GPU port of color_bd/color_avatar.m with PAPR reduction (2026-11-25).
Per block: 3 colour channels, layered angular-spectrum propagation (band +-HU horizontal, +-delta vertical).
PAPR reduction (Gerchberg-Saxton style, as in OFDM peak reduction): the object amplitude per layer is fixed, only the
diffuser phase is free; iterate  e = sum_l P_l{A_l e^{i phi_l}} -> clip |e| at clip*rms -> back-propagate -> keep phase.
Lower peak-to-average lets the whole field be scaled up for the same clipping (brighter hologram, same object image)."""
import argparse, math, struct, time, os
import numpy as np, torch
from PIL import Image
ap = argparse.ArgumentParser()
ap.add_argument('--src', required=True); ap.add_argument('--out', required=True)
ap.add_argument('--blocks', default='1,2,3,4,5'); ap.add_argument('--papr-iters', type=int, default=8)
ap.add_argument('--clip', type=float, default=1.6); ap.add_argument('--W', type=float, default=30.0); ap.add_argument('--L', type=float, default=31.0)
ap.add_argument('--device', default='cuda' if torch.cuda.is_available() else 'cpu'); ap.add_argument('--seed', type=int, default=1)
A = ap.parse_args(); dev = torch.device(A.device); os.makedirs(A.out, exist_ok=True)
K = dict(rc=41.0, ZR=(-4.0, 4.0), NL=12, HU=8.0)
BL = {1: (13.7, (630, 532, 460), 1.0, (1, 1, 0.9)), 2: (13.7, (610, 545, 465), 1.0, (1, 1, 0.9)),
      3: (10.0, (630, 532, 460), 1.0, (1, 1, 0.9)), 4: (17.5, (630, 532, 460), 1.0, (1, 1, 0.9)),
      5: (13.7, (630, 532, 460), 0.5, (1, 1, 0.9))}
im = Image.open(os.path.join(A.src, 'texture_rgba.png')).convert('RGBA'); I = np.asarray(im).astype(np.float64)/255
D = np.asarray(Image.open(os.path.join(A.src, 'depth16.png'))).astype(np.float64); D = D/D.max()
h, w = I.shape[:2]; px = A.W/w
amp = np.sqrt(I[:, :, :3]**2.2)*(I[:, :, 3:4] > 0.5)                        # per-channel field amplitude
zmap = K['ZR'][0] + D*(K['ZR'][1] - K['ZR'][0])
lidx = np.clip(np.round((zmap - K['ZR'][0])/(K['ZR'][1] - K['ZR'][0])*(K['NL'] - 1)), 0, K['NL'] - 1).astype(int)
zl = np.linspace(K['ZR'][0], K['ZR'][1], K['NL'])
def sampler(img, gv, gu):
    # image row i <-> v = ((h-1)/2 - i)*px ; column j <-> u = (j - (w-1)/2)*px   (top of image = +v, outward)
    fi = (h - 1)/2 - gv/px; fj = gu/px + (w - 1)/2
    i0 = torch.floor(fi).long(); j0 = torch.floor(fj).long(); wi = (fi - i0).float(); wj = (fj - j0).float()
    vi = (i0 >= 0) & (i0 + 1 < h); vj = (j0 >= 0) & (j0 + 1 < w)
    i0c = i0.clamp(0, h - 2); j0c = j0.clamp(0, w - 2)
    t = img[:, j0c]*(1 - wj)[None, :] + img[:, j0c + 1]*wj[None, :]; t = t*vj[None, :]
    out = t[i0c, :]*(1 - wi)[:, None] + t[i0c + 1, :]*wi[:, None]
    return out*vi[:, None]
def write_bdaf(path, E, v0, dv, u0, du, rc, thc, hv, hu, lam):
    with open(path, 'wb') as f:
        f.write(b'BDAF1\0\0\0'); f.write(struct.pack('<3i', E.shape[0], E.shape[1], 2))
        f.write(struct.pack('<10d', v0, dv, u0, du, rc, thc, hv, hu, lam, 0.0))
        for i0 in range(0, E.shape[0], 256):
            blk = E[i0:i0 + 256].cpu().numpy(); a = np.empty(blk.shape + (2,), '<f4'); a[..., 0] = blk.real; a[..., 1] = blk.imag
            f.write(a.tobytes())
gen = torch.Generator(device=dev); gen.manual_seed(A.seed)
for b in [int(x) for x in A.blocks.split(',')]:
    th0, lc_nm, dl, wc = BL[b]; lc = np.array(lc_nm)*1e-6; Lc = lc/math.sin(math.radians(th0)); thb = (b - 1)*72.0
    du = lc.min()/(4*math.sin(math.radians(K['HU']))); dv = lc.min()/(4*math.sin(math.radians(dl)))
    Nu = 2*math.ceil(A.L/2/du) + 8; Nv = 2*math.ceil(A.L/2/dv) + 8
    gu = (torch.arange(Nu, device=dev, dtype=torch.float64) - Nu/2)*du; gv = (torch.arange(Nv, device=dev, dtype=torch.float64) - Nv/2)*dv
    fu = torch.fft.fftfreq(Nu, du, device=dev).float(); fv = torch.fft.fftfreq(Nv, dv, device=dev).float()
    print(f'block {b}: theta0 {th0}, carriers {np.round(Lc*1e3,4)} um, delta {dl}, grid {Nv}x{Nu}', flush=True); t0 = time.time()
    E = []
    for c in range(3):
        H = (fu.abs()[None, :] < math.sin(math.radians(K['HU']))/lc[c]) & (fv.abs()[:, None] < math.sin(math.radians(dl))/lc[c])
        kz = torch.sqrt(torch.clamp(1/lc[c]**2 - fu[None, :]**2 - fv[:, None]**2, min=0))
        img_c = torch.tensor(amp[:, :, c], device=dev, dtype=torch.float32)
        layers = [l for l in range(K['NL']) if (lidx == l).any() and (amp[:, :, c]*(lidx == l)).any()]
        masks = {l: torch.tensor(lidx == l, device=dev, dtype=torch.float32) for l in layers}
        def Al(l): return sampler(img_c*masks[l], gv, gu)
        def Pl(l): return torch.exp(-2j*math.pi*float(zl[l])*kz)*H
        pin = dev.type == 'cuda'
        def to_host(t): t = t.half().cpu(); return t.pin_memory() if pin else t
        phi = {l: to_host(torch.rand(Nv, Nu, generator=gen, device=dev)*2*math.pi) for l in layers}   # phases kept in host RAM
        def ph(l): return phi[l].to(dev, non_blocking=True).float()
        def forward():
            e = torch.zeros(Nv, Nu, dtype=torch.complex64, device=dev)
            for l in layers:
                X = torch.fft.fft2(Al(l)*torch.exp(1j*ph(l))); X *= Pl(l); e += torch.fft.ifft2(X); del X
            return e
        def papr(e):
            a = e.abs(); s = a[a > 1e-6*a.max()]; s = s[torch.randperm(s.numel(), device=dev)[:2_000_000]]
            rms = s.pow(2).mean().sqrt(); return float(torch.quantile(s, 0.995)/rms), float(rms)
        e = forward(); p0, rms0 = papr(e)
        # gradient descent on the diffuser phases: L = mean(relu(|e| - clip*rms0)^2)  (peak reduction)
        T = A.clip*rms0; m1 = {l: to_host(torch.zeros(Nv, Nu, device=dev)) for l in layers}; m2 = {l: to_host(torch.zeros(Nv, Nu, device=dev)) for l in layers}
        for it in range(1, A.papr_iters + 1):
            a = e.abs(); g = torch.where(a > T, (a - T)/a.clamp(min=1e-12), torch.zeros_like(a))*e   # dL/de*
            del a; Fg = torch.fft.fft2(g); del g
            for l in layers:
                p_ = ph(l); x = Al(l)*torch.exp(1j*p_)
                Bl = Fg*torch.conj(Pl(l)); Bl = torch.fft.ifft2(Bl)
                gr = -2*torch.imag(x*torch.conj(Bl)); del x, Bl
                gr = gr/(gr.abs().mean() + 1e-20)
                mm = m1[l].to(dev).float().mul_(0.9).add_(gr, alpha=0.1)
                vv = m2[l].to(dev).float().mul_(0.999).addcmul_(gr, gr, value=0.001); del gr
                p_ = (p_ - 0.15*(mm/(1 - 0.9**it))/((vv/(1 - 0.999**it)).sqrt() + 1e-8)).remainder(2*math.pi)
                phi[l] = to_host(p_); m1[l] = to_host(mm); m2[l] = to_host(vv.clamp(max=6e4)); del p_, mm, vv
            del Fg
            e = forward()
            if it % 5 == 0: print(f'    it {it}: PAPR {papr(e)[0]:.2f}', flush=True)
        del m1, m2
        p1, _ = papr(e)
        print(f'  channel {"RGB"[c]}: {len(layers)} layers, PAPR p99.5/rms {p0:.2f} -> {p1:.2f}  {time.time() - t0:.0f}s', flush=True)
        E.append(e.cpu()); del phi, kz, H
        if dev.type == 'cuda': torch.cuda.empty_cache()
    tot = sum((wc[c]*E[c][::5, ::5].abs())**2 for c in range(3)).sqrt(); t = tot[tot > 0]
    kap = 1/float(torch.quantile(t[torch.randperm(t.numel())[:4_000_000]], 0.995))
    for c in range(3):
        write_bdaf(os.path.join(A.out, f'C{b}_{"RGB"[c]}.bdaf'), E[c]*wc[c]*kap, float(gv[0]), dv, float(gu[0]), du, K['rc'], thb, A.L/2, A.L/2, float(Lc[c]))
    print(f'block {b} written ({time.time() - t0:.0f}s), kappa {kap:.3f}', flush=True)
print('done')
