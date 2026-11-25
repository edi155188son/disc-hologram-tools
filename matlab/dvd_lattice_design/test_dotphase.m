diary(fullfile(pwd,"test_dotphase.log")); diary on;
rng(1); lam=532e-6; Z=400; dx=2e-3; N=2000; x=((0:N-1)-N/2)*dx; [X,Y]=ndgrid(x,x);
grid=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; P=zeros(12,2);
for i=1:12, P(i,:)=[7+(3-grid(i,2))+(rand-0.5)/3, 6+(grid(i,1)-1.5)+(rand-0.5)/3]; end
k=2*pi/lam; xo=((0:N-1)-N/2)*lam*Z/(N*dx); [R,T]=ndgrid(xo,xo);
W=zeros(N,N,12,'single'); for i=1:12, W(:,:,i)=single(k*(sqrt((X-P(i,1)).^2+(Y-P(i,2)).^2+Z^2)-Z)); end
idx=zeros(12,1); for i=1:12, [~,idx(i)]=min((R(:)-P(i,1)).^2+(T(:)-P(i,2)).^2); end
chirp=exp(1i*pi*(X.^2+Y.^2)/(lam*Z));
res=zeros(20,2); tots=zeros(20,1); pks=zeros(20,1);
for trial=1:20
  [pk,tot]=ev(2*pi*rand(12,1),W,chirp,idx); res(trial,:)=[sum(pk)/tot, min(pk)/mean(pk)]; pks(trial)=min(pk);
end
fprintf('random phases (20 trials): dot share %.2f%% (%.2f-%.2f), uniformity min/mean %.2f\n',100*mean(res(:,1)),100*min(res(:,1)),100*max(res(:,1)),mean(res(:,2)));
ph=2*pi*rand(12,1); [pk,tot]=ev(ph,W,chirp,idx); best=min(pk); tic
for sweep=1:3
  for i=1:12
    for c=(0:7)*pi/4
      p2=ph; p2(i)=c; pk2=ev(p2,W,chirp,idx);
      if min(pk2)>best, best=min(pk2); ph=p2; end
    end
  end
end
[pk,tot]=ev(ph,W,chirp,idx); toc
fprintf('optimized phases: dot share %.2f%%, uniformity %.2f, weakest dot %.2fx (vs random-trial average weakest dot)\n',100*sum(pk)/tot,min(pk)/mean(pk),min(pk)/mean(pks));
diary off;
function [pk,tot]=ev(ph,W,chirp,idx)
  t=zeros(size(chirp),'single'); for i=1:numel(ph), t=t+cos(W(:,:,i)+ph(i)); end
  I=abs(fftshift(fft2(double(sign(t)).*chirp))).^2; pk=I(idx); tot=sum(I(:));
end
