function holo_hpo_big(RASTER, IMG, OPT, PNG)
%HOLO_HPO_BIG  Large-area white-light HPO hologram of a picture -> DVDHPO1 polar raster for
%   DvdDiffractionRings --pattern polar --raster <RASTER>.
%   Every row of the raster is one radius (row pitch OPT.DR); the row is a 1-D Fresnel hologram, along the
%   track (arc length), of the picture sampled along that arc. Columns are uniform in angle.
%   Picture frame: centred at (OPT.rc, OPT.thc_deg) on the disc, "up" = radially outward, "right" = +angle
%   (tangential at the centre). Row 1 of the picture = up. Width OPT.width_mm (height follows aspect).
%   OPT (defaults): rc 41 | thc_deg 90 | span_deg 40 | r0/r1 28/54 (radial extent of the raster) | DR 0.01
%     width_mm 24 | center [v u] mm offset | color 'mono'|'rgb' | gamma 2.2 | invert | bg 'alpha'|'white'|'black'|'none'
%     floor 0.02 | depth [] | 'tilt' | 'lum' | file/matrix | zrange [-3 3] | nlayers 8 | Z0 0
%     ALPHA 4.5 (deg, carrier for each design wavelength) | SPREAD 1.0 (deg, along-track random phase band)
%     dead 0.05 (|field| below this x row 95th pct -> fill byte; cuts noise from dark areas) | seed 7 | fill 'hi' (rows without picture: 92) | 'noise' (pseudo-random) | 'lo' (A5)
%   PNG: simulated eye view (white light 450-650 nm, pupil 0.5 deg at ALPHA), rows x arc, 50 um/px.
if nargin < 3 || isempty(OPT), OPT = struct; end
g = @(f,d) getf(OPT,f,d);
copies = g('copies',1);
rc = g('rc',41); thc = deg2rad(g('thc_deg',90)); span = deg2rad(g('span_deg',40));
if copies > 1, span = 2*pi/copies; end                     % tile the picture round the whole disc
r0 = g('r0',28); r1 = g('r1',54); DR = g('DR',0.01); DX = 2.13e-3;
ALPHA = deg2rad(g('ALPHA',4.5)); SPREAD = deg2rad(g('SPREAD',1.0)); rng(g('seed',7));
BYTE_HI = uint8(hex2dec('92')); BYTE_LO = uint8(hex2dec('A5'));
switch lower(g('fill','hi')), case 'hi', FILL = BYTE_HI; case 'lo', FILL = BYTE_LO; otherwise, FILL = uint8(0); end
%% picture -> amplitude / depth maps in the local (v up, u right) frame
if ischar(IMG) || isstring(IMG), [I, ~, al] = imread(char(IMG)); nm = char(IMG); else, I = IMG; al = []; nm = 'matrix'; end
I = double(I); if max(I(:)) > 1, I = I/255; end
if size(I,3) == 1, I = repmat(I,[1 1 3]); end
if ~isempty(al), al = double(al); if max(al(:)) > 1, al = al/255; end, end
lin = I.^g('gamma',2.2); if g('invert',false), lin = 1 - lin; end
amp = sqrt(max(lin,0)); mask = true(size(I,1), size(I,2));
switch lower(g('bg','alpha'))
    case 'alpha', if ~isempty(al), mask = al > 0.5; end
    case 'white', mask = ~all(I > 0.97, 3);
    case 'black', mask = ~all(I < 0.03, 3);
end
amp = amp .* mask; amp(amp < sqrt(g('floor',0.02))) = 0;
[h, w, ~] = size(amp); W = g('width_mm',24); H = W*h/w; c = g('center',[0 0]);
ui = c(2) + ((1:w)-(w+1)/2)/w*W; vi = c(1) + ((h+1)/2-(1:h))/h*H;   % column -> u, row -> v (row 1 up)
dep = g('depth',[]); nl = g('nlayers',8); zr = g('zrange',[-3 3]);
if isempty(dep)
    didx = ones(h,w); Zs = g('Z0',0);
else
    if ischar(dep) || isstring(dep)
        switch lower(char(dep))
            case 'tilt', d = repmat(linspace(0,1,w), h, 1);
            case 'lum',  d = mean(lin,3); d = d/max(d(:)+eps);
            otherwise,   d = double(imread(char(dep))); if size(d,3)>1, d = mean(d,3); end; d = d/max(d(:)+eps);
                         if any(size(d) ~= [h w]), d = interp2(d, linspace(1,size(d,2),w), linspace(1,size(d,1),h)'); end
        end
    else, d = double(dep); d = d/max(d(:)+eps);
    end
    didx = min(max(round(d*(nl-1))+1, 1), nl); Zs = linspace(zr(1), zr(2), nl);
end
if strcmpi(g('color','mono'),'rgb'), lams = [650 550 450]*1e-6; chans = 1:3; else, lams = 550e-6; chans = 0; end
[VI, UI] = ndgrid(vi(end:-1:1), ui);
Fa = cell(1, numel(chans)); for k = 1:numel(chans)
    ch = chans(k); if ch == 0, a = mean(amp,3); else, a = amp(:,:,ch); end
    Fa{k} = griddedInterpolant(VI, UI, a(end:-1:1,:), 'linear', 'none'); end
Fd = griddedInterpolant(VI, UI, didx(end:-1:1,:), 'nearest', 'none');
vmin = min(vi) - 0.5; vmax = max(vi) + 0.5;                   % rows outside the picture are skipped
%% raster geometry
th0 = thc - span/2; dth = DX / r1; ncols = ceil(span/dth); nrows = round((r1 - r0)/DR);
rows = r0 + ((0:nrows-1) + 0.5)*DR;
Q = repmat(FILL, nrows, ncols);
fprintf('%s: %dx%d px -> %.1fx%.1f mm at r=%.1f, %.0f deg; raster %d rows x %d cols (%.1f MB)\n', nm, w, h, W, H, rc, rad2deg(thc), nrows, ncols, nrows*ncols/1e6);
xc = rc*cos(thc); yc = rc*sin(thc); cth = cos(thc); sth = sin(thc);
doPng = nargin >= 4 && ~isempty(PNG);
if doPng, simR = 1:5:nrows; SIM = zeros(numel(simR), ceil(r1*span/0.05), 3); lamsim = linspace(450,650,7)*1e-6; end
tic
for i = 1:nrows
    r = rows(i);
    v_c = (r*cos(thc) - xc)*cth + (r*sin(thc) - yc)*sth;       % v of this row at the centre column (= r - rc)
    if v_c < vmin - 8 || v_c > vmax + 8, continue; end         % far outside picture (keep 8 mm for fringe zones)
    smax = r*span/2; ns = 2^nextpow2(2*smax/DX); s = ((0:ns-1)-ns/2)*DX;
    th = thc + s/r; x = r*cos(th); y = r*sin(th);
    u = -(x-xc)*sth + (y-yc)*cth; v = (x-xc)*cth + (y-yc)*sth;
    fs = ((0:ns-1)-ns/2)/(ns*DX);
    dl = fillz(Fd(v, u)); Hd = zeros(1, ns);
    for k = 1:numel(chans)
        lam = lams(k); A = fillz(Fa{k}(v, u));
        if ~any(A), continue; end
        for l = unique(dl(dl>0 & A>0))
            Al = A .* (dl == l); if ~any(Al), continue; end
            nz = randn(1,ns) + 1i*randn(1,ns);
            ph = angle(ifft(ifftshift(fftshift(fft(nz)) .* (abs(fs) < sin(SPREAD)/lam))));
            E = Al .* exp(1i*ph) .* exp(2i*pi*sin(ALPHA)/lam * s);
            Hd = Hd + ifft(ifftshift(fftshift(fft(E)) .* exp(-2i*pi*Zs(l)*sqrt(max(1/lam^2 - fs.^2, 0))) .* (abs(fs) < 1/lam)));
        end
    end
    if ~any(Hd), continue; end
    sg = real(Hd) > 0; dead = abs(Hd) < g('dead',0.05) * prctile(abs(Hd(abs(Hd)>0)), 95);   % no field -> fill byte
    sj = r*(th0 + ((0:ncols-1)+0.5)*dth - thc);                 % arc position of each raster column
    jj = min(max(round(sj/DX) + ns/2 + 1, 1), ns);
    row = repmat(BYTE_LO, 1, ncols); row(sg(jj)) = BYTE_HI; row(dead(jj)) = FILL; Q(i,:) = row;
    if doPng && any(simR == i)
        hg = 2*double(sg) - 1; hg(dead) = 1; unc = rand(1,ns) < 0.17; hg(unc) = 2*(rand(1,nnz(unc))>0.5)-1;
        acc = zeros(1, ns, 3);
        for c2 = 1:numel(lamsim)
            lam = lamsim(c2); pup = abs(fs - sin(ALPHA)/lam) < sin(deg2rad(0.5))/lam;
            U = ifft(ifftshift(fftshift(fft(hg)) .* pup)); Iu = abs(U).^2;
            wt = [lam > 590e-6, lam >= 500e-6 & lam <= 590e-6, lam < 500e-6];
            for c3 = 1:3, acc(1,:,c3) = acc(1,:,c3) + wt(c3)*Iu; end
        end
        nb = round(0.05/DX); for c3 = 1:3, a1 = conv(acc(1,:,c3), ones(1,nb)/nb, 'same'); SIM(simR == i, :, c3) = interp1(s, a1, ((1:size(SIM,2))-size(SIM,2)/2)*0.05, 'linear', 0); end
    end
    if mod(i, 200) == 0, fprintf('  row %d/%d  r=%.1f  %.0fs\n', i, nrows, r, toc); end
end
if copies > 1, Q = repmat(Q, 1, copies); ncols = ncols*copies; fprintf('tiled %d copies every %.0f deg\n', copies, 360/copies); end
fid = fopen(RASTER, 'w', 'ieee-le');
fwrite(fid, ['DVDHPO1' 0], 'char'); fwrite(fid, 1, 'int32');
fwrite(fid, [r0 DR th0 dth], 'double'); fwrite(fid, [nrows ncols], 'int32');
fwrite(fid, Q', 'uint8'); fclose(fid);                        % row-major
fprintf('wrote %s (%d x %d), lit %.1f%%, %.0fs\n', RASTER, nrows, ncols, 100*mean(Q(:)==BYTE_HI), toc);
if doPng
    SIM = SIM / prctile(SIM(SIM>0), 99.5); SIM = min(SIM,1).^(1/2.2);
    fg = figure('Visible','off','Position',[0 0 900 700]);
    image(((1:size(SIM,2))-size(SIM,2)/2)*0.05, rows(simR), SIM); axis xy image;
    xlabel('arc from centre (mm)'); ylabel('radius (mm)'); title(sprintf('%s  white-light view at %.1f deg', nm, rad2deg(ALPHA)), 'Interpreter','none');
    exportgraphics(fg, PNG, 'Resolution', 80); close(fg);
end
end
function v = fillz(v), v(isnan(v)) = 0; end
function v = getf(s, f, d), if isfield(s, f) && ~isempty(s.(f)), v = s.(f); else, v = d; end, end
