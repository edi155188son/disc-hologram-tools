import torch, time
d=torch.device('cuda'); H,W=3402,3645; N=H*W; nv=3_900_000
inv=torch.randint(0,nv,(N,),device=d); inv_sorted,_=torch.sort(inv)
z=torch.randn(nv,device=d); T=torch.randn(H,W,dtype=torch.complex64,device=d); mask=torch.rand(H,W,device=d)>0.9
mc=mask.to(torch.complex64)
def t(f,n=20):
    torch.cuda.synchronize(); a=time.time()
    for _ in range(n): f()
    torch.cuda.synchronize(); return (time.time()-a)/n*1000
h=torch.randn(H,W,device=d)
print('fft2 real->c', t(lambda: torch.fft.fft2(h)))
hc=h.to(torch.complex64)
print('fft2 c64', t(lambda: torch.fft.fft2(hc)))
print('ifft2', t(lambda: torch.fft.ifft2(hc)))
print('gather', t(lambda: z[inv].view(H,W)))
print('gather sorted', t(lambda: z[inv_sorted]))
g=torch.randn(N,device=d)
print('index_add random', t(lambda: torch.zeros_like(z).index_add_(0,inv,g)))
print('index_add sorted', t(lambda: torch.zeros_like(z).index_add_(0,inv_sorted,g)))
print('elementwise E', t(lambda: mc*(hc-T)+mc*hc))
