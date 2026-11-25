% avatar_bd2.m -- BD-R avatar v2 (2026-11-24): double-phase encoding (no zero-order picture), wider view cone, deeper,
% F / R labels. FP (full parallax) @ 0 deg, RB (rainbow) @ 180 deg, r = 41 mm, block 31 x 31 mm, face 30 mm wide.
% Carrier 7 tracks (2.239 um; 532 nm -> 13.7 deg), cone +-8 deg, depth -9..+9 mm (white = near).
% Fields are written as BDAF1 for BdAmp --encoding dp (tracks alternate arg e +/- acos|e|, 50 % duty everywhere).
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
src = getenv('HOLO_ASSETS'); if isempty(src), error('Set HOLO_ASSETS to the folder holding texture_rgba.png and depth16.png (see python/avatar3d)'); end
C.lam = 532e-6; C.P = 0.3199e-3; C.Lr = 7*C.P; C.W = 30; C.L = 31; C.rc = 41; C.ZR = [-9 9]; C.NL = 32; C.HALF = deg2rad(8);
C.BYTE = 0.929e-3;
[I, ~, al] = imread(fullfile(src, 'texture_rgba.png')); I = double(I)/255; al = double(al)/255;
Dm = double(imread(fullfile(src, 'depth16.png'))); Dm = Dm/max(Dm(:));
lum = 0.299*I(:,:,1) + 0.587*I(:,:,2) + 0.114*I(:,:,3);
amp = sqrt(lum.^2.2) .* (al > 0.5);
[h, w] = size(amp); px = C.W/w;
ui = ((1:w) - (w+1)/2)*px; vi = ((h+1)/2 - (1:h))*px;
zmap = C.ZR(1) + Dm*(C.ZR(2) - C.ZR(1));
lidx = min(max(round((zmap - C.ZR(1))/(C.ZR(2) - C.ZR(1))*(C.NL-1)) + 1, 1), C.NL);
C.zl = linspace(C.ZR(1), C.ZR(2), C.NL);
pic.amp = amp(end:-1:1, :); pic.lidx = lidx(end:-1:1, :); pic.vi = vi(end:-1:1); pic.ui = ui;
fprintf('face %.0f mm, depth %g..%g mm, %d layers, carrier %.4f um (%.1f deg @532), cone +-%.0f deg\n', ...
    C.W, C.ZR, C.NL, C.Lr*1e3, asind(C.lam/C.Lr), rad2deg(C.HALF));
%% FP (2 um grid; band +-sin8/lam = 0.26 /um)
fFP = fullfile(here, 'FP.bdaf');
if ~isfile(fFP)
    tic; d = 2e-3; N = 15600; g = ((0:N-1) - N/2)*d;
    f = single(((0:N-1) - N/2)/(N*d)); f2 = f'.^2 + f.^2; cone = f2 < (sin(C.HALF)/C.lam)^2;
    kz = single(sqrt(max(1/C.lam^2 - f2, 0))); clear f2
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
    writeBdaf(fFP, E, 1, g(1), d, g(1), d, C.rc, 0, C.L/2, C.L/2, C.Lr, 0);
    clear E
end
%% RB (rows every 20 um, 1-D band-limited along u)
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
    writeBdaf(fRB, E, 0, vr(1), dv, s(1), ds, C.rc, 180, C.L/2, C.L/2, C.Lr, 0);
    fprintf('RB field %.0fs\n', toc); clear E
end
%% labels: 6 mm letters beside each block (+u side), all-light strokes, density mode (lambda = 0)
glyph.F = ['11111';'10000';'10000';'11110';'10000';'10000';'10000'];
glyph.R = ['11110';'10001';'10001';'11110';'10100';'10010';'10001'];
for lb = {{'F', 0}, {'R', 180}}
    nm = lb{1}{1}; thb = lb{1}{2}; fL = fullfile(here, ['label_' nm '.bdaf']);
    if isfile(fL), continue; end
    G = double(glyph.(nm) == '1'); cell_ = 6/7; dd = 0.02; hv = 3.6; hu = 2.8;
    vv = -hv:dd:hv; uu = -hu:dd:hu; [V, U] = ndgrid(vv, uu);
    row = floor((3.0 - V)/cell_) + 1; col = floor((U + 2.5*cell_)/cell_) + 1;
    ok = row >= 1 & row <= 7 & col >= 1 & col <= 5; M = zeros(size(V));
    M(ok) = G(sub2ind([7 5], row(ok), col(ok)));
    thc = thb + rad2deg(20/C.rc);                                    % 20 mm past the block centre along +u
    writeBdaf(fL, complex(single(M)), 0, vv(1), dd, uu(1), dd, C.rc, thc, hv, hu, 0, 0.5);
end
%% simulation: central 4 x 4 mm on the BD grid, double-phase with track parity, 15 % uncontrolled bytes
for nm = {'FP', 'RB'}
    png = fullfile(here, [nm{1} '_sim.png']); if isfile(png), continue; end
    [Fd, hd] = readBdaf(fullfile(here, [nm{1} '.bdaf']));
    vg = (-2:C.P:2)'; ug = -2:C.BYTE:2; [V, U] = ndgrid(vg, ug);
    e = Fd(V, U); clear U
    par = 2*mod(round(V/C.P), 2) - 1;
    q = cos(2*pi*V/C.Lr - angle(e) - par.*acos(min(abs(e), 1))) > 0; clear V e par
    stn = RandStream('mt19937ar', 'Seed', 5); hh = single(2*q - 1); clear q
    unc = rand(stn, size(hh)) < 0.15; hh(unc) = 2*(rand(stn, nnz(unc), 1) > 0.5) - 1; clear unc
    Nv = numel(vg); Nu = numel(ug);
    fv = single(((0:Nv-1) - floor(Nv/2))/(Nv*C.P))'; fu = single(((0:Nu-1) - floor(Nu/2))/(Nu*C.BYTE));
    F2 = fftshift(fft2(hh));
    th0 = asin(C.lam/C.Lr); pr = deg2rad(0.6); kb = ones(round(0.03/C.P), round(0.03/C.BYTE)); kb = kb/numel(kb);
    if strcmp(nm{1}, 'FP'), views = [0 -6; 0 0; 0 6; -6 0; 6 0]; else, views = [0 -6; 0 0; 0 6]; end
    ri = round(linspace(1, Nv, 400)); ci = round(linspace(1, Nu, 400)); M = [];
    for k = 1:size(views, 1)
        fv0 = sin(th0 + deg2rad(views(k,1)))/C.lam; fu0 = sind(views(k,2))/C.lam;
        pup = (fv - fv0).^2 + (fu - fu0).^2 < (sin(pr)/C.lam)^2;
        Iv = abs(ifft2(ifftshift(F2 .* pup))).^2; Iv = conv2(Iv, kb, 'same');
        Iv = min(Iv/prctile(Iv(:), 99.5), 1).^0.6; Iv = Iv(end:-1:1, :);
        M = [M, Iv(ri, ci), ones(400, 10)]; %#ok<AGROW>
    end
    R0 = conv2(double(hh > 0), ones(round(0.05/C.P), round(0.05/C.BYTE))/numel(ones(round(0.05/C.P), round(0.05/C.BYTE))), 'same');
    R0 = R0(end:-1:1, :); M = [M, min(max((R0(ri, ci)-0.3)/0.4, 0), 1)];  %#ok<AGROW>  % zero-order "photo" (should be flat)
    fg = figure('Visible', 'off', 'Position', [0 0 1800 500]); imagesc(M); axis image off; colormap(gray);
    title(sprintf('%s v2 (central 4 mm) 532 nm views %s | last: mean reflectance 0.3..0.7', nm{1}, mat2str(views)), 'Interpreter', 'none');
    exportgraphics(fg, png, 'Resolution', 80); close(fg); clear F2 hh
end
fprintf('done\n');

function writeBdaf(f, E, interpV, v0, dv, u0, du, rc, thc, hv, hu, lambda, thr)
    fid = fopen(f, 'w', 'ieee-le');
    fwrite(fid, ['BDAF1' 0 0 0], 'char'); fwrite(fid, [size(E,1) size(E,2) interpV], 'int32');
    fwrite(fid, [v0 dv u0 du rc thc hv hu lambda thr], 'double');
    for i = 1:size(E, 1), fwrite(fid, [real(E(i,:)); imag(E(i,:))], 'single'); end
    fclose(fid); fprintf('wrote %s  %d x %d (%.0f MB)\n', f, size(E,1), size(E,2), 8*numel(E)/1e6);
end
function [Fd, hd] = readBdaf(f)
    fid = fopen(f, 'r', 'ieee-le'); fread(fid, 8, 'char');
    n = fread(fid, 3, 'int32'); p = fread(fid, 10, 'double');
    hd.nv = n(1); hd.nu = n(2); hd.iv = n(3); hd.thr = p(10);
    v = p(1) + (0:hd.nv-1)*p(2); u = p(3) + (0:hd.nu-1)*p(4);
    sel = find(abs(v) <= 2.2); sel = sel(1):sel(end);
    fseek(fid, 8 + 12 + 80 + 8*(sel(1)-1)*hd.nu, 'bof');
    raw = fread(fid, [2*hd.nu, numel(sel)], 'single=>single'); fclose(fid);
    E = complex(raw(1:2:end, :), raw(2:2:end, :)).';
    if hd.iv == 1, m = 'linear'; else, m = 'nearest'; end
    Fr = griddedInterpolant({v(sel), u}, real(E), m, 'none'); Fi = griddedInterpolant({v(sel), u}, imag(E), m, 'none');
    Fd = @(V, U) fillz(complex(Fr(V, U), Fi(V, U)));
end
function x = fillz(x), x(isnan(x)) = 0; end
