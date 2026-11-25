# Byte-level iterative optimization with "don't care" outside the viewing windows (prototype, 2026-11-25)
import numpy as np, time, sys
P=0.3199e-3; BY=0.929e-3; th0=17.5; lam=np.array([630,532,460])*1e-6; Lc=lam/np.sin(np.radians(th0))
nch=int(sys.argv[1]) if len(sys.argv)>1 else 1; HU=8; Lp=0.5; fine=0.155e-3; sub=6
rng=np.random.default_rng(5); nr=int(Lp/P); nb=int(Lp/BY)+2; nf=nb*sub
v=(np.arange(nr)-nr//2)*P; uf=(np.arange(nf)-nf//2)*fine
V,U=np.meshgrid(v,uf,indexing='ij'); a=((abs(V)<0.15)&(abs(U)<0.15)).astype(float)
fv=np.fft.fftfreq(nr,P); fu=np.fft.fftfreq(nf,fine); S=np.zeros((nr,nf),complex); chans=[1] if nch==1 else [0,1,2]
for c in chans:
    o=a*np.exp(2j*np.pi*rng.random(a.shape)); band=(abs(fu)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(fv)[:,None]<np.sin(np.radians(1))/lam[c])
    e=np.fft.ifft2(np.fft.fft2(o)*band); e/=np.percentile(abs(e),99)*1.7; S+=np.conj(e)*np.exp(2j*np.pi*v[:,None]/Lc[c])
# byte layout: track t, byte k covers fine columns [off_t + k*sub, +sub) (offset in fine cells, random per track)
off=np.random.default_rng(9).integers(0,sub,nr)
col=np.arange(nf); kidx=np.clip((col[None,:]-off[:,None])//sub,0,nb-1)          # byte index per fine cell
cen=((kidx*sub+off[:,None]+sub/2)-nf//2)*fine                                 # byte centre (u)
def render(b): return np.take_along_axis(b, kidx, axis=1)
def adjoint(g):                                                               # sum fine cells into their byte
    out=np.zeros((nr,nb)); np.add.at(out,(np.repeat(np.arange(nr),nf),kidx.ravel()),g.ravel()); return out
# DP baseline evaluated at byte centres
par=(2*(np.arange(nr)%2)-1)[:,None]
Sb=np.take_along_axis(S, np.clip(np.round(cen/fine).astype(int)+nf//2,0,nf-1), axis=1)[:, ::sub][:, :nb] if False else None
jc=np.clip(np.arange(nb)[None,:]*sub+off[:,None]+sub//2,0,nf-1).astype(int)
Sbyte=np.take_along_axis(S,jc,axis=1)
bdp=np.where(np.cos(np.angle(Sbyte)-par*np.arccos(np.minimum(abs(Sbyte),1)))>0,1.,-1.)
ideal=(2/np.pi)*2*S.real; Fi=np.fft.fft2(ideal)
W=np.zeros((nr,nf),bool)
for c in chans: W|=(abs(fu)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(abs(fv)-1/Lc[c])[:,None]<np.sin(np.radians(1))/lam[c])   # +1 and -1 windows
def snr(b, noise=0.0):
    bb=b.copy()
    if noise>0:
        r=np.random.default_rng(1); m=r.random(bb.shape)<noise; bb[m]=np.where(r.random(m.sum())>0.5,1,-1)
    F=np.fft.fft2(render(bb)); out=[]
    for c in chans:
        win=(abs(fu)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(fv-1/Lc[c])[:,None]<np.sin(np.radians(1))/lam[c])
        A=F[win]; B=Fi[win]; k=np.vdot(B,A)/np.vdot(B,B); out.append(np.sum(abs(k*B)**2)/np.sum(abs(A-k*B)**2))
    return out
print('DP      :', ['%.1f'%x for x in snr(bdp)], ' with 2%%:', ['%.1f'%x for x in snr(bdp,0.02)])
# optimization: z -> b = tanh(beta z), loss = ||W (F(render b) - kappa Fi)||^2, Adam, beta annealed, then sign
kappa=1.0; z=np.arctanh(np.clip(bdp*0.8,-0.99,0.99)); m1=np.zeros_like(z); m2=np.zeros_like(z); lr=0.05; t0=time.time()
for it in range(1,301):
    beta=1+it/30.0; b=np.tanh(beta*z)
    R=np.fft.fft2(render(b)); E=np.where(W,R-kappa*Fi,0)
    g_h=np.real(np.fft.ifft2(E))*2*E.size                                     # d loss / d h (up to scale)
    g=adjoint(g_h)*beta*(1-b*b)
    g/=np.abs(g).mean()+1e-12; m1=0.9*m1+0.1*g; m2=0.999*m2+0.001*g*g
    z-=lr*(m1/(1-0.9**it))/(np.sqrt(m2/(1-0.999**it))+1e-8)
    if it%100==0: print(f'  it {it}  binary SNR', ['%.1f'%x for x in snr(np.sign(z)+(z==0))], f'{time.time()-t0:.0f}s')
bopt=np.where(z>=0,1.,-1.)
print('OPT     :', ['%.1f'%x for x in snr(bopt)], ' with 2%%:', ['%.1f'%x for x in snr(bopt,0.02)], ' mean', bopt.mean().round(3))
F=np.fft.fft2(render(bopt)); Fd=np.fft.fft2(render(bdp)); lo=(abs(fu)[None,:]<60)&(abs(fv)[:,None]<60)
print('low-freq (photo) power  DP %.3g  OPT %.3g'%(np.sum(abs(Fd[lo])**2)-abs(Fd[0,0])**2, np.sum(abs(F[lo])**2)-abs(F[0,0])**2))
# robustness: real byte positions differ from the model -> shift every track by a random number of fine cells
for sh in [1,2,3]:
    rr=np.random.default_rng(11); d=rr.integers(-sh,sh+1,nr)
    k2=np.clip((col[None,:]-(off+d)[:,None])//sub,0,nb-1)
    def snr2(b):
        F=np.fft.fft2(np.take_along_axis(b,k2,axis=1)); o=[]
        for c in chans:
            win=(abs(fu)[None,:]<np.sin(np.radians(HU))/lam[c])&(abs(fv-1/Lc[c])[:,None]<np.sin(np.radians(1))/lam[c])
            A=F[win]; B=Fi[win]; k=np.vdot(B,A)/np.vdot(B,B); o.append(np.sum(abs(k*B)**2)/np.sum(abs(A-k*B)**2))
        return o
    print(f'track jitter +-{sh*0.155:.2f} um:  DP', ['%.1f'%x for x in snr2(bdp)], ' OPT', ['%.1f'%x for x in snr2(bopt)])
