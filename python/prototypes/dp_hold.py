import numpy as np, sys
P=0.3199e-3; BY=0.929e-3; th0=17.5; lam=np.array([630,532,460])*1e-6; Lc=lam/np.sin(np.radians(th0))
def run(HU, holdmode, nch=3, seed=5):
    rng=np.random.default_rng(seed); Lp=0.5; fine=0.155e-3
    nr=int(Lp/P); nf=int(Lp/fine); v=(np.arange(nr)-nr//2)*P; uf=(np.arange(nf)-nf//2)*fine
    # fields on the fine grid (exact), random phase square, band +-HU x +-1 deg
    V,U=np.meshgrid(v,uf,indexing='ij'); a=((abs(V)<0.15)&(abs(U)<0.15)).astype(float)
    fv=np.fft.fftfreq(nr,P); fu=np.fft.fftfreq(nf,fine); S=np.zeros((nr,nf),complex); es=[]
    chans=range(3) if nch==3 else [1]
    for c in chans:
        o=a*np.exp(2j*np.pi*rng.random(a.shape)); band=(abs(fu)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(fv)[:,None]<np.sin(np.radians(1))/lam[c])
        e=np.fft.ifft2(np.fft.fft2(o)*band); e/=np.percentile(abs(e),99)*1.7; S+=np.conj(e)*np.exp(2j*np.pi*v[:,None]/Lc[c])
    par=(2*(np.arange(nr)%2)-1)[:,None]
    def dp(Sx): return np.where(np.cos(np.angle(Sx)-par*np.arccos(np.minimum(abs(Sx),1)))>0,1.,-1.)
    if holdmode=='grid': h=dp(S)
    else:
        h=np.zeros((nr,nf)); r2=np.random.default_rng(9)
        for t in range(nr):
            off=r2.random()*BY if holdmode=='random' else 0.0
            k=np.floor((uf-off)/BY); centre=off+(k+0.5)*BY; j=np.clip(np.round(centre/fine).astype(int)+nf//2,0,nf-1)
            h[t]=dp(S[t:t+1,j][0:1])[0] if False else np.where(np.cos(np.angle(S[t,j])-par[t,0]*np.arccos(np.minimum(abs(S[t,j]),1)))>0,1.,-1.)
    ideal=(2/np.pi)*2*S.real
    F=np.fft.fftshift(np.fft.fft2(h)); Fi=np.fft.fftshift(np.fft.fft2(ideal)); fvs=np.fft.fftshift(fv); fus=np.fft.fftshift(fu); o=[]
    for c in chans:
        win=(abs(fus)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(fvs-1/Lc[c])[:,None]<np.sin(np.radians(1))/lam[c])
        A=F[win]; B=Fi[win]; k=np.vdot(B,A)/np.vdot(B,B); o.append(np.sum(abs(k*B)**2)/np.sum(abs(A-k*B)**2))
    return o
for nch in [1,3]:
  for HU in [4,6,8]:
    print(f'ch{nch} HU {HU}:', ' '.join(f"{m}:{'/'.join(f'{x:5.1f}' for x in run(HU,m,nch))}" for m in ['grid','aligned','random']))
