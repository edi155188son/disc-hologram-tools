% avatar_fill.m -- like avatar_fp.m but with AMPLITUDE encoding (Lee fringe-width), so filled / grey images work
% Fringe duty D = asin(a)/pi per carrier period (a = normalised |field|); a = 0 -> no bright track (uniform A5)
%   FP: full 2-D wavefront (vertical + horizontal parallax), view with a narrow-band (single colour) point source
%   RB: HPO rainbow (colour up/down, parallax left/right), view with white light
% Both: avatar3d texture + depth16 (outlines as bright strokes, dim fill), face 15 mm wide, depth -4..+4 mm (white = near = +4, in front of the disc)
% Carrier: straight radial grating, 5 tracks (3.70 um; 532 nm -> 8.3 deg), detour-phase, phase-only, no field -> 0x92
% Output: FP.hpo, RB.hpo (DVDHPO1) for DvdDiffractionRings, plus simulation PNGs. Re-run safe.
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
src = getenv('HOLO_ASSETS'); if isempty(src), error('Set HOLO_ASSETS to the folder holding texture_rgba.png and depth16.png (see python/avatar3d)'); end
C.HI = uint8(146); C.LO = uint8(165);
C.P = 0.74e-3; C.DR = C.P/2; C.BYTE = 2.13e-3; C.lam = 532e-6;
C.simOnly = false; C.Lr = 5*C.P; C.W = 15; C.ZR = [-4 4]; C.NL = 16; C.HALF = deg2rad(3.5);
blocks = {struct('name','FP','rc',44,'thc',0,  'L',16,'DX',1.5e-3), ...
          struct('name','RB','rc',44,'thc',180,'L',16,'DX',1.5e-3)};
%% picture
[I, ~, al] = imread(fullfile(src, 'texture_rgba.png')); I = double(I)/255; al = double(al)/255;
D = double(imread(fullfile(src, 'depth16.png'))); D = D/max(D(:));
lum = 0.299*I(:,:,1) + 0.587*I(:,:,2) + 0.114*I(:,:,3);
msk = al > 0.5; amp = sqrt(lum.^2.2) .* msk;            % natural picture (bright fill, dark outlines)
[h, w] = size(amp); px = C.W/w;
ui = ((1:w) - (w+1)/2)*px; vi = ((h+1)/2 - (1:h))*px;          % row 1 = up (outward)
zmap = C.ZR(1) + D*(C.ZR(2) - C.ZR(1));
lidx = min(max(round((zmap - C.ZR(1))/(C.ZR(2) - C.ZR(1))*(C.NL-1)) + 1, 1), C.NL);
C.zl = linspace(C.ZR(1), C.ZR(2), C.NL);
pic.amp = amp(end:-1:1, :); pic.lidx = lidx(end:-1:1, :); pic.vi = vi(end:-1:1); pic.ui = ui;
fprintf('picture %dx%d, %.4f mm/px, face %.1f x %.1f mm, depth %g..%g mm, %d layers\n', w, h, px, C.W, C.W*h/w, C.ZR, C.NL);
%% FP: full 2-D field on a 4 um grid
fFP = fullfile(here, 'FPf.hpo'); pFP = fullfile(here, 'FPf_sim.png');
if (~isfile(fFP) && ~C.simOnly) || ~isfile(pFP)
    tic; G = fieldFP(pic, C); fprintf('FP field %.0fs\n', toc);
    efun = @(v, u) interpFP(G, v, u);
    if ~isfile(fFP) && ~C.simOnly, Q = raster(blocks{1}, C, efun); writeHpo(fFP, blocks{1}, C, Q); clear Q; end
    if ~isfile(pFP), sim(blocks{1}, C, efun, pFP, true); end
    clear G efun
end
%% RB: per-row (HPO) field, rows every 20 um
fRB = fullfile(here, 'RBf.hpo'); pRB = fullfile(here, 'RBf_sim.png');
if (~isfile(fRB) && ~C.simOnly) || ~isfile(pRB)
    tic; H = fieldRB(pic, C); fprintf('RB field %.0fs\n', toc);
    efun = @(v, u) interpRB(H, v, u);
    if ~isfile(fRB) && ~C.simOnly, Q = raster(blocks{2}, C, efun); writeHpo(fRB, blocks{2}, C, Q); clear Q; end
    if ~isfile(pRB), sim(blocks{2}, C, efun, pRB, false); end
end
fprintf('all done\n');

function G = fieldFP(pic, C)
    d = 4e-3; N = 4500; g = ((0:N-1) - N/2)*d;                   % 18 mm window
    f = single(((0:N-1) - N/2)/(N*d)); f2 = f'.^2 + f.^2;
    cone = f2 < (sin(C.HALF)/C.lam)^2;
    kz = sqrt(max(1/C.lam^2 - f2, 0)); clear f2
    st = RandStream('mt19937ar', 'Seed', 31);
    E = complex(zeros(N, N, 'single'));
    for l = 1:C.NL
        a = pic.amp .* (pic.lidx == l); if ~any(a(:)), continue; end
        F = griddedInterpolant({pic.vi, pic.ui}, a, 'linear', 'none');
        A = single(F({g, g})); A(isnan(A)) = 0; if ~any(A(:)), continue; end
        O = A .* exp(1i*2*pi*rand(st, N, N, 'single'));
        S = fftshift(fft2(O)); clear O
        S = S .* cone .* exp(-2i*pi*single(C.zl(l))*kz);
        E = E + ifft2(ifftshift(S)); clear S
        fprintf('  FP layer %d/%d z=%+.2f\n', l, C.NL, C.zl(l));
    end
    a = abs(E(1:7:end, 1:7:end)); E = E/prctile(a(a > 0), 99); G.thr = 0.02;
    G.Fr = griddedInterpolant({g, g}, real(E), 'linear', 'none');
    G.Fi = griddedInterpolant({g, g}, imag(E), 'linear', 'none');
end
function e = interpFP(G, v, u)
    e = complex(G.Fr(v, u), G.Fi(v, u)); e(isnan(e)) = 0; e(abs(e) < G.thr) = 0;
end
function H = fieldRB(pic, C)
    dv = 0.02; H.v = -8:dv:8; ds = 1e-3; ns = 20480; H.s0 = -ns/2*ds; H.ds = ds;
    s = ((0:ns-1) - ns/2)*ds; fs = ((0:ns-1) - ns/2)/(ns*ds);
    band = abs(fs) < sin(C.HALF)/C.lam; kz = sqrt(max(1/C.lam^2 - fs.^2, 0));
    Fa = griddedInterpolant({pic.vi, pic.ui}, pic.amp, 'linear', 'none');
    Fl = griddedInterpolant({pic.vi, pic.ui}, pic.lidx, 'nearest', 'none');
    st = RandStream('mt19937ar', 'Seed', 37);
    E = complex(zeros(numel(H.v), ns, 'single'));
    for j = 1:numel(H.v)
        a = Fa(repmat(H.v(j), 1, ns), s); a(isnan(a)) = 0; if ~any(a), continue; end
        li = Fl(repmat(H.v(j), 1, ns), s); li(isnan(li)) = 0; row = zeros(1, ns);
        for l = unique(li(li > 0 & a > 0))
            nz = randn(st, 1, ns) + 1i*randn(st, 1, ns);
            ph = angle(ifft(ifftshift(fftshift(fft(nz)) .* band)));
            o = a .* (li == l) .* exp(1i*ph);
            row = row + ifft(ifftshift(fftshift(fft(o)) .* band .* exp(-2i*pi*C.zl(l)*kz)));
        end
        E(j, :) = row;
    end
    a = abs(E(:, 1:7:end)); H.E = E/prctile(a(a > 0), 99); H.thr = 0.02;
end
function e = interpRB(H, v, u)
    j = round((v - H.v(1))/(H.v(2) - H.v(1))) + 1; jj = round((u - H.s0)/H.ds) + 1;
    ok = j >= 1 & j <= numel(H.v) & jj >= 1 & jj <= size(H.E, 2);
    e = complex(zeros(size(v), 'single')); e(ok) = H.E(sub2ind(size(H.E), j(ok), jj(ok)));
    e(abs(e) < H.thr) = 0;
end
function [rows, th, r0, th0, dth] = grid(b, C)
    thc = deg2rad(b.thc); r0 = b.rc - b.L/2; nrows = round(b.L/C.DR);
    span = b.L/b.rc; th0 = thc - span/2; dth = b.DX/(b.rc + b.L/2); ncols = ceil(span/dth);
    rows = r0 + ((1:nrows) - 0.5)*C.DR; th = th0 + ((1:ncols) - 0.5)*dth;
end
function q = encode(v, e, C)
    % Lee detour-phase with amplitude: bright where cos(carrier - phase) > cos(pi*D), D = asin(|e|)/pi in [0, 0.5]
    D = asin(min(abs(e), 1))/pi;
    q = repmat(C.LO, size(v)); q(cos(2*pi*v/C.Lr - angle(e)) > cos(pi*D)) = C.HI;
end
function Q = raster(b, C, efun)
    [rows, th] = grid(b, C); thc = deg2rad(b.thc); xc = b.rc*cos(thc); yc = b.rc*sin(thc);
    Q = repmat(C.HI, numel(rows), numel(th)); tic
    for i0 = 1:500:numel(rows)
        ii = i0:min(i0+499, numel(rows));
        [R, T] = ndgrid(rows(ii), th); X = R.*cos(T) - xc; Y = R.*sin(T) - yc;
        v = X*cos(thc) + Y*sin(thc); u = -X*sin(thc) + Y*cos(thc); clear R T X Y
        Q(ii, :) = encode(v, efun(v, u), C);
        if mod(i0 - 1, 10000) == 0, fprintf('  %s rows %d/%d  %.0fs\n', b.name, i0, numel(rows), toc); end
    end
end
function writeHpo(f, b, C, Q)
    [~, ~, r0, th0, dth] = grid(b, C);
    fid = fopen(f, 'w', 'ieee-le');
    fwrite(fid, ['DVDHPO1' 0], 'char'); fwrite(fid, 1, 'int32');
    fwrite(fid, [r0 C.DR th0 dth], 'double'); fwrite(fid, size(Q), 'int32');
    fwrite(fid, Q', 'uint8'); fclose(fid);
    fprintf('wrote %s  %d x %d (%.0f MB), bright %.1f%%\n', f, size(Q,1), size(Q,2), numel(Q)/1e6, 100*mean(Q(1:97:end) == C.HI));
end
function sim(b, C, efun, png, full)
    % central 8 x 8 mm on the codeword grid (track x byte), 15% uncontrollable bytes
    vg = (-4:C.P:4)'; ug = -4:C.BYTE:4; [V, U] = ndgrid(vg, ug);
    q = encode(V, efun(V, U), C); clear U
    st = RandStream('mt19937ar', 'Seed', 5); hh = single(2*(q == C.HI) - 1); clear q
    unc = rand(st, size(hh)) < 0.15; hh(unc) = 2*(rand(st, nnz(unc), 1) > 0.5) - 1; clear unc
    Nv = numel(vg); Nu = numel(ug);
    fv = single(((0:Nv-1) - floor(Nv/2))/(Nv*C.P))'; fu = single(((0:Nu-1) - floor(Nu/2))/(Nu*C.BYTE));
    F2 = fftshift(fft2(hh)); clear hh
    th0 = asin(C.lam/C.Lr); pr = deg2rad(0.6);
    kb = ones(round(0.04/C.P), round(0.04/C.BYTE)); kb = kb/numel(kb);
    if full, views = [0 -3; 0 0; 0 3; -3 0; 3 0]; else, views = [0 -3; 0 0; 0 3]; end   % [vertical horizontal] deg: left, centre, right, down, up
    tiles = {};
    for k = 1:size(views, 1)                                   % narrow band 532 nm
        fv0 = sin(th0 + deg2rad(views(k,1)))/C.lam; fu0 = sind(views(k,2))/C.lam;
        pup = (fv - fv0).^2 + (fu - fu0).^2 < (sin(pr)/C.lam)^2;
        Iv = abs(ifft2(ifftshift(F2 .* pup))).^2; Iv = conv2(Iv, kb, 'same');
        Iv = min(Iv/prctile(Iv(:), 99.5), 1).^0.6; tiles{end+1} = repmat(Iv(end:-1:1, :), 1, 1, 3); %#ok<AGROW>
    end
    lams = (450:33.3:650)*1e-6; rgbw = @(l) [l > 590e-6, l >= 500e-6 & l <= 590e-6, l < 500e-6] + 0.3*(l >= 560e-6 & l <= 600e-6)*[0 1 0];
    Iw = zeros(Nv, Nu, 3, 'single');                           % white light, eye fixed at the 532 nm direction
    for l = lams
        pup = (fv - sin(th0)/l).^2 + fu.^2 < (sin(pr)/l)^2;
        Iv = abs(ifft2(ifftshift(F2 .* pup))).^2; Iv = conv2(Iv, kb, 'same'); c = rgbw(l);
        for k = 1:3, Iw(:,:,k) = Iw(:,:,k) + c(k)*Iv; end
    end
    Iw = min(Iw/prctile(Iw(:), 99.5), 1).^0.6; tiles{end+1} = Iw(end:-1:1, :, :);
    ri = round(linspace(1, Nv, 400)); ci = round(linspace(1, Nu, 400));   % 8 x 8 mm tiles, square
    sep = ones(400, 12, 3, 'single'); M = tiles{1}(ri, ci, :);
    for k = 2:numel(tiles), M = [M, sep, tiles{k}(ri, ci, :)]; end %#ok<AGROW>
    if full, lbl = 'narrow band 532 nm: left / centre / right / down / up  |  white light'; else, lbl = 'narrow band 532 nm: left / centre / right  |  white light'; end
    fg = figure('Visible', 'off', 'Position', [0 0 1600 700]); image(M); axis image off;
    title(sprintf('%s (central 8 mm)  %s', b.name, lbl), 'Interpreter', 'none');
    exportgraphics(fg, png, 'Resolution', 80); close(fg);
end
