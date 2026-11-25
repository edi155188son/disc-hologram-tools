LAM=532e-6; Z=400; HALF_IN=2.0;
fid=fopen('efm2_A.bin','r','ieee-le'); fread(fid,8,'*char'); N=fread(fid,1,'int32'); dx=fread(fid,1,'double'); hdr=fread(fid,3,'double');
fread(fid,N*N,'single'); fread(fid,N*N,'int32'); W1=reshape(fread(fid,N*N,'single=>double'),N,N).'; fclose(fid);
x=((0:N-1)+0.5)*dx-hdr(3)/2; in=abs(x)<=HALF_IN; h=W1(in,in); h(isnan(h))=mean(h(:),'omitnan'); h=h-mean(h(:)); n=size(h,1); xi=x(in);
[X,Y]=ndgrid(xi,xi); I=abs(fftshift(fft2(h.*exp(1i*pi*(X.^2+Y.^2)/(LAM*Z))))).^2; xo=((0:n-1)-n/2)*LAM*Z/(n*dx); [R,T]=ndgrid(xo,xo);
gF=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; rng(1); P=zeros(12,2);
for i=1:12, P(i,:)=[7+(3-gF(i,2))+(rand-0.5)/3, 6+(gF(i,1)-1.5)+(rand-0.5)/3]; end
dotpk=mean(arrayfun(@(i) max(I((R-P(i,1)).^2+(T-P(i,2)).^2<0.25^2)),1:12));
bg=mean(I(R>3.5&R<11.5&T>3&T<9),'all');
sp=LAM*Z/(1488*133.3e-6); fprintf('sync-frame period 198.4 um -> comb spacing %.3f mm at Z=400 (tangential axis)\n',sp);
for m=[1 2 3 5 10 20]
  for sgn=[1 -1]
    t0=sgn*m*sp; k=abs(R)<0.15 & abs(T-t0)<0.15; if m==1&&sgn==1, row=[]; end
    row(end+1)=max(I(k)); %#ok<SAGROW>
  end
  fprintf('order %2d: comb peak / F dot peak = %.3g / %.3g\n', m, row(end-1)/dotpk, row(end)/dotpk);
end
k=abs(R)<0.15 & abs(T)>0.3 & abs(T)<12; strip=I(k);
fprintf('mean along tangential axis (|R|<0.15 mm) / F dot = %.3g ; image-window background / F dot = %.3g\n', mean(strip)/dotpk, bg/dotpk);
k2=abs(T)<0.15 & abs(R)>0.3 & abs(R)<12; fprintf('mean along radial axis / F dot = %.3g\n', mean(I(k2))/dotpk);
