%% efm2_analyze.m - mark-area model: does the F appear, and how big is the EFM land-fraction noise vs the signal?
if ~exist('EFMFILE','var'), EFMFILE = 'efm2_A.bin'; end
LAM = 532e-6; Z = 400; HALF_IN = 2.0;
fid = fopen(EFMFILE,'r','ieee-le'); fread(fid,8,'*char'); N = fread(fid,1,'int32'); dx = fread(fid,1,'double'); hdr = fread(fid,3,'double');
E  = reshape(fread(fid,N*N,'single=>double'),N,N).'; fread(fid,N*N,'int32');
W1 = reshape(fread(fid,N*N,'single=>double'),N,N).'; W0 = reshape(fread(fid,N*N,'single=>double'),N,N).'; delta = fread(fid,1,'double'); fclose(fid);
x = ((0:N-1)+0.5)*dx - hdr(3)/2; inner = abs(x) <= HALF_IN; xi = x(inner); n = nnz(inner);
prep = @(A) (A(inner,inner) - mean(A(inner,inner),'all','omitnan'));
L = read_lattice('s7888_lat_r28_a180.bin'); fid = fopen('s7888_v2_A.bin'); v = fread(fid,inf,'*uint8'); fclose(fid);
s = zeros(numel(v),1); s(v==146) = 1; s(v==165) = -1;
ir = floor((L.ur + HALF_IN)/dx) + 1; it = floor((L.ut + HALF_IN)/dx) + 1; ok = L.ctrl & ir>=1 & ir<=n & it>=1 & it<=n;
M = accumarray([ir(ok) it(ok)], s(ok), [n n]) ./ max(accumarray([ir(ok) it(ok)], 1, [n n]), 1); M = M - mean(M(:));
[X, Y] = ndgrid(xi, xi); chirp = exp(1i*pi*(X.^2+Y.^2)/(LAM*Z)); xo = ((0:n-1)-n/2)*LAM*Z/(n*dx);
prop = @(h) abs(fftshift(fft2(h .* chirp))).^2; [R, T] = ndgrid(xo, xo);
gF=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; rng(1); P = zeros(12,2);
for i=1:12, P(i,:) = [7+(3-gF(i,2))+(rand-0.5)/3, 6+(gF(i,1)-1.5)+(rand-0.5)/3]; end
win = R > 3.5 & R < 11.5 & T > 3 & T < 9; dots = false(n); for i=1:12, dots = dots | (R-P(i,1)).^2 + (T-P(i,2)).^2 < 0.25^2; end
bgw = win & ~dots; f = figure('Visible','off','Position',[0 0 1500 520]); sel = xo > -8 & xo < 16; k = 0;
fprintf('mark-area model, delta = %.2f T\n', delta);
for c = {{'land fraction only', prep(E)}, {'marks = level 1', prep(W1)}, {'marks = level 0', prep(W0)}}
    h = c{1}{2}; h(isnan(h)) = 0; a = sum(h(:).*M(:))/sum(M(:).^2); res = h - a*M;
    Is = prop(a*M); Ir = prop(res); I = prop(h);
    pk = arrayfun(@(i) max(I((R-P(i,1)).^2+(T-P(i,2)).^2 < 0.25^2)), 1:12);
    fprintf('%-20s signal amp a = %.4f (92 vs A5 half-contrast), noise rms %.4f | dots/bg total %.1f, signal-part dot peak / noise-part bg = %.1f\n', ...
        c{1}{1}, a, rms(res(:)), mean(pk)/mean(I(bgw)), mean(arrayfun(@(i) max(Is((R-P(i,1)).^2+(T-P(i,2)).^2 < 0.25^2)), 1:12))/mean(Ir(bgw)));
    k = k + 1; subplot(1,3,k); imagesc(xo(sel), xo(sel), log10(I(sel,sel)+1e-30)); axis xy image; colormap(hot);
    cl = log10(mean(I(bgw))); caxis([cl-0.5 cl+2.5]); title(c{1}{1}); xlabel('tangential (mm)'); ylabel('radial outward (mm)');
end
[~, nm] = fileparts(EFMFILE); exportgraphics(f, [nm '_compare.png'], 'Resolution', 85); close(f);
function L = read_lattice(path)
    fid = fopen(path, 'r', 'ieee-le'); fread(fid, 8, '*char'); fread(fid, 1, 'int32'); fread(fid, 9, 'double');
    n = fread(fid, 1, 'uint64'); fread(fid, 1, 'int32'); fseek(fid, n*(8+4), 'cof');
    L.ur = fread(fid, n, 'single=>double'); L.ut = fread(fid, n, 'single=>double'); L.ctrl = fread(fid, n, 'uint8') == 1; fclose(fid);
end
