% avatar_bd.m -- BD-R version of avatar_fill (2x linear size): full-parallax FP + rainbow RB (2026-11-24)
% Writes smooth complex object fields (BDAF1) for BdAmp, which evaluates the Lee detour-phase carrier per byte.
% Face 30 mm wide, blocks 31 x 31 mm at r = 41 mm (FP @ 0 deg, RB @ 180 deg), depth -6..+6 mm (white = near),
% view cone +-3.5 deg, straight radial carrier 3.52 um (= 11 BD tracks; 532 nm -> 8.7 deg), amplitude-encoded.
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
src = getenv('HOLO_ASSETS'); if isempty(src), error('Set HOLO_ASSETS to the folder holding texture_rgba.png and depth16.png (see python/avatar3d)'); end
C.lam = 532e-6; C.Lr = 3.52e-3; C.W = 30; C.L = 31; C.rc = 41; C.ZR = [-6 6]; C.NL = 24; C.HALF = deg2rad(3.5);
C.P = 0.32e-3; C.BYTE = 0.929e-3;                                % BD codeword grid (simulation only)
[I, ~, al] = imread(fullfile(src, 'texture_rgba.png')); I = double(I)/255; al = double(al)/255;
D = double(imread(fullfile(src, 'depth16.png'))); D = D/max(D(:));
lum = 0.299*I(:,:,1) + 0.587*I(:,:,2) + 0.114*I(:,:,3);
amp = sqrt(lum.^2.2) .* (al > 0.5);
[h, w] = size(amp); px = C.W/w;
ui = ((1:w) - (w+1)/2)*px; vi = ((h+1)/2 - (1:h))*px;
zmap = C.ZR(1) + D*(C.ZR(2) - C.ZR(1));
lidx = min(max(round((zmap - C.ZR(1))/(C.ZR(2) - C.ZR(1))*(C.NL-1)) + 1, 1), C.NL);
C.zl = linspace(C.ZR(1), C.ZR(2), C.NL);
pic.amp = amp(end:-1:1, :); pic.lidx = lidx(end:-1:1, :); pic.vi = vi(end:-1:1); pic.ui = ui;
fprintf('face %.0f mm, %.4f mm/px, depth %g..%g mm, %d layers\n', C.W, px, C.ZR, C.NL);
%% FP
fFP = fullfile(here, 'FP.bdaf');
if ~isfile(fFP)
    tic; d = 4e-3; N = 8000; g = ((0:N-1) - N/2)*d;
    f = single(((0:N-1) - N/2)/(N*d)); f2 = f'.^2 + f.^2; cone = f2 < (sin(C.HALF)/C.lam)^2;
    kz = sqrt(max(1/C.lam^2 - f2, 0)); clear f2
    st = RandStream('mt19937ar', 'Seed', 41); E = complex(zeros(N, N, 'single'));
    for l = 1:C.NL
        a = pic.amp .* (pic.lidx == l); if ~any(a(:)), continue; end
        F = griddedInterpolant({pic.vi, pic.ui}, a, 'linear', 'none');
        A = single(F({g, g})); A(isnan(A)) = 0; if ~any(A(:)), continue; end
        O = A .* exp(1i*2*pi*rand(st, N, N, 'single')); clear A
        S = fftshift(fft2(O)); clear O
        S = S .* cone .* exp(-2i*pi*single(C.zl(l))*kz);
        E = E + ifft2(ifftshift(S)); clear S
        fprintf('  FP layer %d/%d  %.0fs\n', l, C.NL, toc);
    end
    clear kz cone
    a = abs(E(1:7:end, 1:7:end)); E = E / prctile(a(a > 0), 99);
    writeBdaf(fFP, E, 1, g(1), d, g(1), d, C, 0, 0.02);
    save(fullfile(here, 'FP_sim_field.mat'), 'C');                 % (simulation re-reads the .bdaf)
    clear E
end
%% RB
fRB = fullfile(here, 'RB.bdaf');
if ~isfile(fRB)
    tic; dv = 0.02; vr = -C.L/2:dv:C.L/2; ds = 1e-3; ns = 32768; s = ((0:ns-1) - ns/2)*ds;
    fs = ((0:ns-1) - ns/2)/(ns*ds); band = abs(fs) < sin(C.HALF)/C.lam; kz = sqrt(max(1/C.lam^2 - fs.^2, 0));
    Fa = griddedInterpolant({pic.vi, pic.ui}, pic.amp, 'linear', 'none');
    Fl = griddedInterpolant({pic.vi, pic.ui}, pic.lidx, 'nearest', 'none');
    st = RandStream('mt19937ar', 'Seed', 43); E = complex(zeros(numel(vr), ns, 'single'));
    for j = 1:numel(vr)
        a = Fa(repmat(vr(j), 1, ns), s); a(isnan(a)) = 0; if ~any(a), continue; end
        li = Fl(repmat(vr(j), 1, ns), s); li(isnan(li)) = 0; row = zeros(1, ns);
        for l = unique(li(li > 0 & a > 0))
            nz = randn(st, 1, ns) + 1i*randn(st, 1, ns);
            ph = angle(ifft(ifftshift(fftshift(fft(nz)) .* band)));
            o = a .* (li == l) .* exp(1i*ph);
            row = row + ifft(ifftshift(fftshift(fft(o)) .* band .* exp(-2i*pi*C.zl(l)*kz)));
        end
        E(j, :) = row;
    end
    a = abs(E(:, 1:7:end)); E = E / prctile(a(a > 0), 99);
    writeBdaf(fRB, E, 0, vr(1), dv, s(1), ds, C, 180, 0.02);
    fprintf('RB field %.0fs\n', toc); clear E
end
%% simulation (central 4 x 4 mm on the BD codeword grid)
for nm = {'FP', 'RB'}
    png = fullfile(here, [nm{1} '_sim.png']); if isfile(png), continue; end
    [Fd, hd] = readBdaf(fullfile(here, [nm{1} '.bdaf']));
    vg = (-2:C.P:2)'; ug = -2:C.BYTE:2; [V, U] = ndgrid(vg, ug);
    e = Fd(V, U); clear U
    Dd = asin(min(abs(e), 1))/pi; q = cos(2*pi*V/C.Lr - angle(e)) > cos(pi*Dd); q(abs(e) < hd.thr) = false; clear V e Dd
    stn = RandStream('mt19937ar', 'Seed', 5); hh = single(2*q - 1); clear q
    unc = rand(stn, size(hh)) < 0.15; hh(unc) = 2*(rand(stn, nnz(unc), 1) > 0.5) - 1; clear unc
    Nv = numel(vg); Nu = numel(ug);
    fv = single(((0:Nv-1) - floor(Nv/2))/(Nv*C.P))'; fu = single(((0:Nu-1) - floor(Nu/2))/(Nu*C.BYTE));
    F2 = fftshift(fft2(hh)); clear hh
    th0 = asin(C.lam/C.Lr); pr = deg2rad(0.6); kb = ones(round(0.03/C.P), round(0.03/C.BYTE)); kb = kb/numel(kb);
    if strcmp(nm{1}, 'FP'), views = [0 -2.5; 0 0; 0 2.5; -2.5 0; 2.5 0]; else, views = [0 -2.5; 0 0; 0 2.5]; end
    ri = round(linspace(1, Nv, 400)); ci = round(linspace(1, Nu, 400)); M = [];
    for k = 1:size(views, 1)
        fv0 = sin(th0 + deg2rad(views(k,1)))/C.lam; fu0 = sind(views(k,2))/C.lam;
        pup = (fv - fv0).^2 + (fu - fu0).^2 < (sin(pr)/C.lam)^2;
        Iv = abs(ifft2(ifftshift(F2 .* pup))).^2; Iv = conv2(Iv, kb, 'same');
        Iv = min(Iv/prctile(Iv(:), 99.5), 1).^0.6; Iv = Iv(end:-1:1, :);
        M = [M, Iv(ri, ci), ones(400, 10)]; %#ok<AGROW>
    end
    fg = figure('Visible', 'off', 'Position', [0 0 1500 500]); imagesc(M); axis image off; colormap(gray);
    title(sprintf('%s BD (central 4 mm) 532 nm views: %s', nm{1}, mat2str(views)), 'Interpreter', 'none');
    exportgraphics(fg, png, 'Resolution', 80); close(fg); clear F2
end
fprintf('done\n');

function writeBdaf(f, E, interpV, v0, dv, u0, du, C, thc, thr)
    fid = fopen(f, 'w', 'ieee-le');
    fwrite(fid, ['BDAF1' 0 0 0], 'char'); fwrite(fid, [size(E,1) size(E,2) interpV], 'int32');
    fwrite(fid, [v0 dv u0 du C.rc thc C.L/2 C.L/2 C.Lr thr], 'double');
    for i = 1:size(E, 1), fwrite(fid, [real(E(i,:)); imag(E(i,:))], 'single'); end
    fclose(fid); fprintf('wrote %s  %d x %d (%.0f MB)\n', f, size(E,1), size(E,2), 8*numel(E)/1e6);
end
function [Fd, hd] = readBdaf(f)
    fid = fopen(f, 'r', 'ieee-le'); fread(fid, 8, 'char');
    n = fread(fid, 3, 'int32'); p = fread(fid, 10, 'double');
    hd.nv = n(1); hd.nu = n(2); hd.iv = n(3); hd.thr = p(10);
    v = p(1) + (0:hd.nv-1)*p(2); u = p(3) + (0:hd.nu-1)*p(4);
    sel = find(abs(v) <= 2.2); sel = sel(1):sel(end);                 % only the central rows (memory)
    fseek(fid, 8 + 12 + 80 + 8*(sel(1)-1)*hd.nu, 'bof');
    raw = fread(fid, [2*hd.nu, numel(sel)], 'single=>single'); fclose(fid);
    E = complex(raw(1:2:end, :), raw(2:2:end, :)).';
    if hd.iv == 1, m = 'linear'; else, m = 'nearest'; end
    Fr = griddedInterpolant({v(sel), u}, real(E), m, 'none'); Fi = griddedInterpolant({v(sel), u}, imag(E), m, 'none');
    Fd = @(V, U) fillz(complex(Fr(V, U), Fi(V, U)));
end
function x = fillz(x), x(isnan(x)) = 0; end
