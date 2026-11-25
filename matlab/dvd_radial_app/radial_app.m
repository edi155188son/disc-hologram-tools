% radial_app.m -- radial-amplitude application disc (2026-11-24)
% Builds DVDHPO1 polar rasters for:
%   DvdDiffractionRings --pattern polar --raster A1.hpo,... --sectors 2297888 --output radial_app.iso
% Bytes: 0x92 = bright, 0xA5 = dark (same as probes). Outside the rasters = pseudo-random.
% Radial structure uses single-track resolution (confirmed by probes_radial P1-P5).
% Re-run safe: existing .hpo / .png are skipped.
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
HI = uint8(146); LO = uint8(165);
P = 0.74e-3; DR = P/4; BYTE = 2.13e-3;          % mm
lamL = 532e-6; lamW = 550e-6; Z = 400;          % mm
FLET = [0 0;0 2;0 4;0 6;0 8; 4/3 8; 8/3 8; 4 8; 4/3 4; 8/3 4];   % [u v] mm, F 4 x 8 mm
FLET(:,1) = FLET(:,1) - 2; FLET(:,2) = FLET(:,2) - 4;
B = {};
B{end+1} = struct('name','A1','kind','F','rc',40,'thc',0,  'L',5,'DX',2e-3,'img',[Z*tand(10) 0],   'note','radial carrier 10 deg (~4.1 tracks)');
B{end+1} = struct('name','A2','kind','F','rc',40,'thc',72, 'L',5,'DX',2e-3,'img',[Z*tand(18) 0],   'note','radial carrier 18 deg (~2.3 tracks)');
B{end+1} = struct('name','B1','kind','F','rc',40,'thc',144,'L',5,'DX',1e-3,'img',[Z*tand(5) Z*tand(4)],   'note','diagonal, tangential fringe ~7.6 um');
B{end+1} = struct('name','B2','kind','F','rc',40,'thc',216,'L',5,'DX',1e-3,'img',[Z*tand(8) Z*tand(6.5)], 'note','diagonal, tangential fringe ~4.7 um (near byte limit)');
B{end+1} = struct('name','R0','kind','F','rc',40,'thc',288,'L',5,'DX',2e-3,'img',[10 8],            'note','reference: old-style 1.3 deg diagonal F');
B{end+1} = struct('name','C1','kind','C','rc',48,'thc',36, 'L',6,'DX',2e-3,'k',5, 'note','white-light rainbow, radial carrier 5 tracks');
B{end+1} = struct('name','C2','kind','C','rc',48,'thc',216,'L',6,'DX',2e-3,'k',3, 'note','white-light rainbow, radial carrier 3 tracks');
fid = fopen(fullfile(here,'layout.txt'),'w');
for n = 1:numel(B)
    b = B{n};
    if b.kind == 'F'
        fprintf(fid,'%s  r=%g mm  angle=%g deg  %gx%g mm  F image at Z=%g mm, offset outward %.1f mm, tangential %.1f mm  (%s)\n', ...
            b.name, b.rc, b.thc, b.L, b.L, Z, b.img(1), b.img(2), b.note);
    else
        fprintf(fid,'%s  r=%g mm  angle=%g deg  %gx%g mm  carrier %d tracks = %.2f um, 550 nm -> %.1f deg  (%s)\n', ...
            b.name, b.rc, b.thc, b.L, b.L, b.k, b.k*P*1e3, asind(lamW/(b.k*P)), b.note);
    end
end
fclose(fid);
for n = 1:numel(B)
    b = B{n}; f = fullfile(here, [b.name '.hpo']); tic
    if b.kind == 'F'
        if ~isfile(f), Q = genF(b, P, DR, lamL, Z, FLET, HI, LO); writeHpo(f, b, DR, Q); clear Q; end
        png = fullfile(here, [b.name '_sim.png']);
        if ~isfile(png), simF(b, P, BYTE, lamL, Z, FLET, png); end
    else
        Eimg = objC(b, lamW);
        if ~isfile(f), Q = genC(b, P, DR, Eimg, HI, LO); writeHpo(f, b, DR, Q); clear Q; end
        png = fullfile(here, [b.name '_sim.png']);
        if ~isfile(png), simC(b, P, BYTE, lamW, Eimg, png); end
    end
    fprintf('%s done %.0fs\n', b.name, toc);
end

function [rows, th, r0, th0, dth] = grid(b, DR)
    thc = deg2rad(b.thc); r0 = b.rc - b.L/2; nrows = round(b.L/DR);
    span = b.L/b.rc; th0 = thc - span/2; dth = b.DX/(b.rc + b.L/2); ncols = ceil(span/dth);
    rows = r0 + ((1:nrows)-0.5)*DR; th = th0 + ((1:ncols)-0.5)*dth;
end
function h = Fh(v, u, b, lam, Z, FLET)
    pts = [b.img(1)+FLET(:,2), b.img(2)+FLET(:,1)];
    s = RandStream('mt19937ar','Seed',11); phs = 2*pi*rand(s, size(pts,1), 1);
    pts = pts + 0.25*(2*rand(s, size(pts)) - 1);   % position jitter breaks the binarization ghost lattice
    k = 2*pi/lam; E = zeros(size(v));
    for n = 1:size(pts,1)
        E = E + exp(1i*(k*sqrt((v-pts(n,1)).^2 + (u-pts(n,2)).^2 + Z^2) + phs(n)));
    end
    h = real(E) > 0;
end
function Q = genF(b, P, DR, lam, Z, FLET, HI, LO)
    [rows, th] = grid(b, DR); thc = deg2rad(b.thc); xc = b.rc*cos(thc); yc = b.rc*sin(thc);
    Q = repmat(LO, numel(rows), numel(th));
    for i0 = 1:400:numel(rows)
        ii = i0:min(i0+399, numel(rows));
        [R, T] = ndgrid(rows(ii), th); X = R.*cos(T) - xc; Y = R.*sin(T) - yc;
        v = X*cos(thc) + Y*sin(thc); u = -X*sin(thc) + Y*cos(thc);
        q = repmat(LO, size(v)); q(Fh(v, u, b, lam, Z, FLET)) = HI; Q(ii,:) = q;
    end
end
function Eimg = objC(b, lam)
    % HPO object: ring (front, z=+4 mm) at u=-1.5, X-cross (behind, z=-4 mm) at u=+1.5; rows every 20 um
    dv = 0.02; vimg = -b.L/2:dv:b.L/2; ds = 1e-3; ns = 8192; s = ((0:ns-1)-ns/2)*ds;
    fs = ((0:ns-1)-ns/2)/(ns*ds); SPREAD = deg2rad(5); BAND = deg2rad(6);
    st = RandStream('mt19937ar','Seed',23); Eimg = complex(zeros(numel(vimg), ns));
    for j = 1:numel(vimg)
        vv = vimg(j); E = zeros(1, ns);
        m1 = abs(sqrt((s+1.5).^2 + vv^2) - 1.4) < 0.06;
        uu = s - 1.5; m2 = (abs(uu - vv) < 0.085 | abs(uu + vv) < 0.085) & max(abs(uu), abs(vv)) <= 1.0;
        for sh = 1:2
            if sh == 1, m = m1; z = 4; else, m = m2; z = -4; end
            if ~any(m), continue; end
            nz = randn(st,1,ns) + 1i*randn(st,1,ns);
            ph = angle(ifft(ifftshift(fftshift(fft(nz)) .* (abs(fs) < sin(SPREAD)/lam))));
            o = m .* exp(1i*ph);
            E = E + ifft(ifftshift(fftshift(fft(o)) .* exp(-2i*pi*z*sqrt(max(1/lam^2 - fs.^2, 0))) .* (abs(fs) < sin(BAND)/lam)));
        end
        Eimg(j,:) = E;
    end
end
function h = Ch(v, u, b, P, Eimg)
    % detour-phase: radial carrier (k tracks) shifted radially by the object phase; no field -> uniform bright
    dv = 0.02; ds = 1e-3; ns = size(Eimg, 2);
    j = min(max(round((v + b.L/2)/dv) + 1, 1), size(Eimg,1));
    jj = min(max(round(u/ds) + ns/2 + 1, 1), ns);
    e = Eimg(sub2ind(size(Eimg), j, jj));
    a = abs(Eimg(:)); thr = 0.05*prctile(a(a>0), 95);
    h = cos(2*pi*v/(b.k*P) - angle(e)) > 0;
    h(abs(e) < thr) = true;
end
function Q = genC(b, P, DR, Eimg, HI, LO)
    [rows, th] = grid(b, DR); thc = deg2rad(b.thc);
    Q = repmat(LO, numel(rows), numel(th));
    for i0 = 1:1000:numel(rows)
        ii = i0:min(i0+999, numel(rows));
        [R, T] = ndgrid(rows(ii), th); v = R - b.rc; u = R.*(T - thc);
        q = repmat(LO, size(v)); q(Ch(v, u, b, P, Eimg)) = HI; Q(ii,:) = q;
    end
end
function writeHpo(f, b, DR, Q)
    [~, ~, r0, th0, dth] = grid(b, DR);
    fid = fopen(f, 'w', 'ieee-le');
    fwrite(fid, ['DVDHPO1' 0], 'char'); fwrite(fid, 1, 'int32');
    fwrite(fid, [r0 DR th0 dth], 'double'); fwrite(fid, size(Q), 'int32');
    fwrite(fid, Q', 'uint8'); fclose(fid);
    fprintf('wrote %s  %d x %d  (%.0f MB), bright %.1f%%\n', f, size(Q,1), size(Q,2), numel(Q)/1e6, 100*mean(Q(:)==146));
end
function hh = codewordGrid(h)
    % +-1 field, 15% uncontrollable format bytes
    st = RandStream('mt19937ar','Seed',5); hh = 2*double(h) - 1;
    unc = rand(st, size(hh)) < 0.15; hh(unc) = 2*(rand(st, nnz(unc), 1) > 0.5) - 1;
end
function simF(b, P, BYTE, lam, Z, FLET, png)
    vg = -b.L/2:P:b.L/2; ug = -b.L/2:BYTE:b.L/2; [V, U] = ndgrid(vg, ug);
    hh = codewordGrid(Fh(V, U, b, lam, Z, FLET));
    Nv = numel(vg); Nu = numel(ug); Xv = ((0:Nv-1)-floor(Nv/2))*lam*Z/(Nv*P); Xu = ((0:Nu-1)-floor(Nu/2))*lam*Z/(Nu*BYTE);
    best = []; bs = -1;
    for sg = [1 -1]
        I = abs(fftshift(fft2(hh .* exp(sg*1i*pi*(V.^2+U.^2)/(lam*Z))))).^2;
        sel = abs(Xv' - b.img(1)) < 8 & abs(Xu - b.img(2)) < 6;
        pk = max(I(sel)); if pk > bs, bs = pk; best = I; end
    end
    I = best; cv = abs(Xv - b.img(1)) < 12; cu = abs(Xu - b.img(2)) < 12;
    C = I(cv, cu); C = C / prctile(C(:), 99.9); C = min(C, 1).^0.5;
    fg = figure('Visible','off','Position',[0 0 700 700]);
    imagesc(Xu(cu), Xv(cv), C); axis xy image; colormap(gray);
    xlabel('tangential (mm)'); ylabel('radial outward (mm)');
    title(sprintf('%s  Z=%d mm  532 nm  (sim, codeword grid, 15%% noise)', b.name, Z), 'Interpreter','none');
    exportgraphics(fg, png, 'Resolution', 80); close(fg);
    % also report full-field peak locations for a sanity check
    [~, ix] = max(I(:)); [iv, iu] = ind2sub(size(I), ix);
    fprintf('%s sim: brightest pixel at radial %.1f mm, tangential %.1f mm (target %.1f, %.1f)\n', b.name, Xv(iv), Xu(iu), b.img(1), b.img(2));
end
function simC(b, P, BYTE, lam, Eimg, png)
    vg = -2:P:2; ug = -3:BYTE:3; [V, U] = ndgrid(vg, ug);
    hh = codewordGrid(Ch(V, U, b, P, Eimg));
    Nv = numel(vg); Nu = numel(ug);
    fv = ((0:Nv-1)-floor(Nv/2))/(Nv*P); fu = ((0:Nu-1)-floor(Nu/2))/(Nu*BYTE);
    F2 = fftshift(fft2(hh)); views = [-4 0 4]; out = [];
    for th = views
        pup = (fv' - 1/(b.k*P)).^2 + (fu - sind(th)/lam).^2 < (sind(0.6)/lam)^2;
        I = abs(ifft2(ifftshift(F2 .* pup))).^2;
        k = ones(round(0.04/P), round(0.04/BYTE)); I = conv2(I, k/numel(k), 'same');
        I = I / prctile(I(:), 99.5); out = [out, min(I,1).^0.6, ones(Nv, 20)]; %#ok<AGROW>
    end
    fg = figure('Visible','off','Position',[0 0 1200 420]);
    imagesc(out); axis xy; colormap(gray); set(gca,'XTick',[],'YTick',[]);
    title(sprintf('%s  radial order (%.1f deg at 550 nm), view -4 / 0 / +4 deg tangential: ring z=+4, X z=-4', ...
        b.name, asind(lam/(b.k*P))), 'Interpreter','none');
    exportgraphics(fg, png, 'Resolution', 80); close(fg);
end
