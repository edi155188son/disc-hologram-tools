%% efm_analyze.m - compare the real EFM channel-bit field with the codeword-level model (holo_ABCDE A, threshold F)
if ~exist('EFMFILE','var'), EFMFILE = 'efm_A.bin'; end
LAM = 532e-6; Z = 400; HALF_IN = 2.0;
fid = fopen(EFMFILE,'r','ieee-le'); fread(fid,8,'*char'); N = fread(fid,1,'int32'); dx = fread(fid,1,'double');
hdr = fread(fid,3,'double'); E = reshape(fread(fid,N*N,'single=>double'),N,N).'; Cn = reshape(fread(fid,N*N,'int32'),N,N).'; fclose(fid);
x = ((0:N-1)+0.5)*dx - hdr(3)/2;                       % cell centres, patch frame (rows = ur, cols = ut)
inner = abs(x) <= HALF_IN; xi = x(inner); n = nnz(inner);
Ei = E(inner,inner); Ei(isnan(Ei)) = mean(Ei(:),'omitnan'); Ei = Ei - mean(Ei(:));
% codeword-level model on the same raster (controllable: 92 -> +1, A5 -> -1; format-fixed: random +-1)
L = read_lattice('s7888_lat_r28_a180.bin'); fid = fopen('s7888_v2_A.bin'); v = fread(fid,inf,'*uint8'); fclose(fid);
s = zeros(numel(v),1); s(v==146) = 1; s(v==165) = -1; rng(2); r = 2*(rand(numel(v),1)>0.5)-1; s(~L.ctrl) = r(~L.ctrl);
ir = floor((L.ur + HALF_IN)/dx) + 1; it = floor((L.ut + HALF_IN)/dx) + 1; ok = ir>=1 & ir<=n & it>=1 & it<=n;
M = accumarray([ir(ok) it(ok)], s(ok), [n n]) ./ max(accumarray([ir(ok) it(ok)], 1, [n n]), 1); M = M - mean(M(:));
alpha = sum(Ei(:).*M(:)) / sum(M(:).^2); Res = Ei - alpha*M;
fprintf('EFM field: rms %.4f; codeword-model part alpha*M rms %.4f; residual rms %.4f (%.0f%% of EFM power)\n', ...
    rms(Ei(:)), rms(alpha*M(:)), rms(Res(:)), 100*sum(Res(:).^2)/sum(Ei(:).^2));
% Fresnel propagation
[X, Y] = ndgrid(xi, xi); chirp = exp(1i*pi*(X.^2+Y.^2)/(LAM*Z)); xo = ((0:n-1)-n/2)*LAM*Z/(n*dx);
prop = @(h) abs(fftshift(fft2(h .* chirp))).^2;
IE = prop(Ei); IM = prop(alpha*M); IR = prop(Res);
% F dot positions (same design as holo_design, rng(1))
gF=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; rng(1); P = zeros(12,2);
for i=1:12, P(i,:) = [7+(3-gF(i,2))+(rand-0.5)/3, 6+(gF(i,1)-1.5)+(rand-0.5)/3]; end
[R, T] = ndgrid(xo, xo); win = R > 3.5 & R < 11.5 & T > 3 & T < 9; dots = false(n);
for i=1:12, dots = dots | (R-P(i,1)).^2 + (T-P(i,2)).^2 < 0.25^2; end
bgw = win & ~dots;
for c = {{'codeword model',IM},{'real EFM field',IE},{'EFM residual',IR}}
    I = c{1}{2}; pk = arrayfun(@(i) max(I((R-P(i,1)).^2+(T-P(i,2)).^2 < 0.25^2)), 1:12);
    fprintf('%-16s dot peak (mean) %.3g   background in image window (mean) %.3g   ratio %.0f\n', c{1}{1}, mean(pk), mean(I(bgw)), mean(pk)/mean(I(bgw)));
end
fprintf('EFM residual background / codeword-model background in window: %.2f\n', mean(IR(bgw))/mean(IM(bgw)));
f = figure('Visible','off','Position',[0 0 1500 520]); sel = xo > -8 & xo < 16;
tl = {'codeword model (\alpha M)','real EFM field','EFM residual (not in model)'}; Is = {IM, IE, IR};
top = log10(max(IE(:))); lo = log10(mean(IM(bgw)))-0.5;
for k = 1:3
    subplot(1,3,k); imagesc(xo(sel), xo(sel), log10(Is{k}(sel,sel)+1e-30)); axis xy image; caxis([lo top]); colormap(hot);
    title(tl{k}); xlabel('tangential (mm)'); ylabel('radial outward (mm)');
end
[~, nm] = fileparts(EFMFILE); exportgraphics(f, [nm '_compare.png'], 'Resolution', 85); close(f);
save([nm '_fields.mat'], 'Ei', 'M', 'alpha', 'xi', 'dx', '-v7.3');

function L = read_lattice(path)
    fid = fopen(path, 'r', 'ieee-le'); fread(fid, 8, '*char'); fread(fid, 1, 'int32'); h = fread(fid, 9, 'double');
    n = fread(fid, 1, 'uint64'); fread(fid, 1, 'int32'); fseek(fid, n*(8+4), 'cof');
    L.ur = fread(fid, n, 'single=>double'); L.ut = fread(fid, n, 'single=>double'); L.ctrl = fread(fid, n, 'uint8') == 1; fclose(fid);
end
