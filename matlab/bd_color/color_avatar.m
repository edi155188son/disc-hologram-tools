% color_avatar.m -- BD-R colour rainbow-hologram test disc (2026-11-25): 5 blocks, each a full-colour avatar with a
% different RGB carrier combination. Each block = 3 BDAF fields (R, G, B) at the same place; BdAmp --encoding dp
% --combine sum adds conj(e_c) exp(i 2 pi v / L_c) per byte and double-phase encodes the sum.
% L_c = lambda_c / sin(theta0): at the design viewing angle each channel diffracts its own colour (white light).
% Each channel: band +-8 deg horizontal (parallax), +-delta vertical (rainbow slit), layered angular spectrum.
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
srcdir = getenv('HOLO_ASSETS'); if isempty(srcdir), error('Set HOLO_ASSETS to the folder holding texture_rgba.png and depth16.png (see python/avatar3d)'); end
K.W = 30; K.L = 31; K.rc = 41; K.ZR = [-4 4]; K.NL = 12; K.HU = 8;
BL = { % theta0  [R G B] nm        delta  weights
        13.7,  [630 532 460],  1.0,  [1 1 0.9];   % 1 baseline
        13.7,  [610 545 465],  1.0,  [1 1 0.9];   % 2 LED-friendly primaries
        10.0,  [630 532 460],  1.0,  [1 1 0.9];   % 3 smaller angle (more tracks per period)
        17.5,  [630 532 460],  1.0,  [1 1 0.9];   % 4 larger angle
        13.7,  [630 532 460],  0.5,  [1 1 0.9] }; % 5 narrower slit (purer colour, smaller zone)
[I, ~, al] = imread(fullfile(srcdir, 'texture_rgba.png')); I = double(I)/255; al = double(al)/255;
Dm = double(imread(fullfile(srcdir, 'depth16.png'))); Dm = Dm/max(Dm(:));
[h, w, ~] = size(I); px = K.W/w; ui = ((1:w) - (w+1)/2)*px; vi = ((h+1)/2 - (1:h))*px;
lin = I.^2.2; amp = sqrt(lin) .* (al > 0.5);                               % per-channel field amplitude
zmap = K.ZR(1) + Dm*(K.ZR(2) - K.ZR(1));
lidx = min(max(round((zmap - K.ZR(1))/(K.ZR(2) - K.ZR(1))*(K.NL-1)) + 1, 1), K.NL); zl = linspace(K.ZR(1), K.ZR(2), K.NL);
amp = amp(end:-1:1, :, :); lidx = lidx(end:-1:1, :); vi = vi(end:-1:1);
for b = 1:size(BL, 1)
    th0 = BL{b,1}; lc = BL{b,2}*1e-6; del = BL{b,3}; wc = BL{b,4}; Lc = lc/sind(th0); thb = (b-1)*72;
    names = arrayfun(@(c) fullfile(here, sprintf('C%d_%s.bdaf', b, char(double('R')*(c==1) + double('G')*(c==2) + double('B')*(c==3)))), 1:3, 'UniformOutput', false);
    if all(cellfun(@isfile, names)), continue; end
    tic; du = min(lc)/(4*sind(K.HU)); dv = min(lc)/(4*sind(del));          % ~2x oversampled for bicubic
    Nu = 2*ceil(K.L/2/du) + 8; Nv = 2*ceil(K.L/2/dv) + 8;
    gu = ((0:Nu-1) - Nu/2)*du; gv = ((0:Nv-1) - Nv/2)*dv;
    fu = single(ifftshift((0:Nu-1) - floor(Nu/2))/(Nu*du)); fv = single(ifftshift((0:Nv-1)' - floor(Nv/2))/(Nv*dv));
    fprintf('block %d: theta0 %.1f, carriers %s um, delta %.1f, grid %d x %d (%.2f x %.2f um)\n', b, th0, mat2str(Lc*1e3, 4), del, Nv, Nu, dv*1e3, du*1e3);
    E = cell(1, 3); for c = 1:3, E{c} = complex(zeros(Nv, Nu, 'single')); end
    st = RandStream('mt19937ar', 'Seed', 100 + b);
    for l = 1:K.NL
        m = (lidx == l); if ~any(m(:)), continue; end
        for c = 1:3
            a = amp(:, :, c) .* m; if ~any(a(:)), continue; end
            Fi = griddedInterpolant({vi, ui}, a, 'linear', 'none');
            A = single(Fi({gv, gu})); A(isnan(A)) = 0;
            O = A .* exp(1i*2*pi*rand(st, Nv, Nu, 'single')); clear A
            H = (abs(fu) < sind(K.HU)/lc(c)) & (abs(fv) < sind(del)/lc(c));
            kz = sqrt(max(1/lc(c)^2 - fu.^2 - fv.^2, 0));
            S = fft2(O); clear O
            S = S .* H .* exp(-2i*pi*single(zl(l))*kz); clear kz H
            E{c} = E{c} + ifft2(S); clear S
        end
        fprintf('  layer %d/%d  %.0fs\n', l, K.NL, toc);
    end
    tot = zeros(Nv, Nu, 'single'); for c = 1:3, E{c} = E{c}*wc(c); tot = tot + abs(E{c}).^2; end
    t = sqrt(tot(1:5:end, 1:5:end)); kap = 1/prctile(t(t > 0), 99.5); clear tot t
    for c = 1:3
        writeBdaf(names{c}, E{c}*kap, 2, gv(1), dv, gu(1), du, K.rc, thb, K.L/2, K.L/2, Lc(c), 0); E{c} = [];
    end
    fprintf('block %d done %.0fs\n', b, toc);
end
% digit labels 1..5 at r = 50 mm, 23 deg past each block centre
G = {['00100';'01100';'00100';'00100';'00100';'00100';'01110'], ['01110';'10001';'00001';'00010';'00100';'01000';'11111'], ...
     ['11110';'00001';'00001';'01110';'00001';'00001';'11110'], ['00010';'00110';'01010';'10010';'11111';'00010';'00010'], ...
     ['11111';'10000';'11110';'00001';'00001';'10001';'01110']};
for b = 1:5
    fL = fullfile(here, sprintf('L%d.bdaf', b)); if isfile(fL), continue; end
    g = double(G{b} == '1'); cell_ = 6/7; dd = 0.02; hv = 3.6; hu = 2.8;
    vv = -hv:dd:hv; uu = -hu:dd:hu; [V, U] = ndgrid(vv, uu);
    row = floor((3.0 - V)/cell_) + 1; col = floor((U + 2.5*cell_)/cell_) + 1;
    ok = row >= 1 & row <= 7 & col >= 1 & col <= 5; M = zeros(size(V)); M(ok) = g(sub2ind([7 5], row(ok), col(ok)));
    writeBdaf(fL, complex(single(M)), 0, vv(1), dd, uu(1), dd, 50, (b-1)*72 + 23, hv, hu, 0, 0.5);
end
disp('done')

function writeBdaf(f, E, interpV, v0, dv, u0, du, rc, thc, hv, hu, lambda, thr)
    fid = fopen(f, 'w', 'ieee-le');
    fwrite(fid, ['BDAF1' 0 0 0], 'char'); fwrite(fid, [size(E,1) size(E,2) interpV], 'int32');
    fwrite(fid, [v0 dv u0 du rc thc hv hu lambda thr], 'double');
    for i = 1:size(E, 1), fwrite(fid, [real(E(i,:)); imag(E(i,:))], 'single'); end
    fclose(fid); fprintf('wrote %s  %d x %d (%.0f MB)\n', f, size(E,1), size(E,2), 8*numel(E)/1e6);
end
