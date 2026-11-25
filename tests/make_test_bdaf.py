"""Writes two tiny synthetic BDAF1 fields (a 0.12 mm carrier block at r = 41 mm and a 'label' density field) for smoke tests.
usage: make_test_bdaf.py OUTDIR"""
import numpy as np, struct, sys, os
out = sys.argv[1] if len(sys.argv) > 1 else '.'
os.makedirs(out, exist_ok=True)
def write(path, E, v0,dv,u0,du,rc,thc,hv,hu,lam,thr=0.0,interp=2):
    with open(path,'wb') as f:
        f.write(b'BDAF1\0\0\0'); f.write(struct.pack('<3i',E.shape[0],E.shape[1],interp))
        f.write(struct.pack('<10d',v0,dv,u0,du,rc,thc,hv,hu,lam,thr))
        a=np.empty((E.shape[0],E.shape[1],2),'<f4'); a[...,0]=E.real; a[...,1]=E.imag; f.write(a.tobytes())
rng=np.random.default_rng(1)
hv=hu=0.06; dv=du=0.004; n=int(2*hv/dv)+1
v=(np.arange(n)-n//2)*dv
V,U=np.meshgrid(v,v,indexing='ij')
amp=np.exp(-(V**2+U**2)/(2*0.03**2))*0.8
ph=np.cumsum(np.cumsum(rng.standard_normal((n,n)),0),1)*0.05
write(os.path.join(out, 'blk.bdaf'),amp*np.exp(1j*ph),v[0],dv,v[0],du,41.0,0.0,hv,hu,2.246e-3)
# label (density, lambda=0) next to the block, +u direction  (centre moved by tangential offset: rc same, angle small)
lab=((abs(V)<0.03)&(abs(U)<0.02)).astype(float)
write(os.path.join(out, 'lab.bdaf'),lab+0j,v[0],dv,v[0],du,41.0,np.degrees(0.15/41.0),hv,hu,0.0,0.0,interp=1)
