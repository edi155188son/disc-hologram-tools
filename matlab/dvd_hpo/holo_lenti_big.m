function holo_lenti_big(RASTER, VIEWS, OPT, PNG)
%HOLO_LENTI_BIG  Lenticular-style multi-view stereogram on the disc -> DVDHPO1 polar raster.
%   Each view k is written with its own byte sequence whose intrinsic EFM grating period sends light to a
%   different angle (white light: each view also has its own colour at a given eye angle). Along the track
%   every hogel of length OPT.hogel_mm is split into N equal slots; slot k holds the view-k sequence for a
%   fraction I_k of its length (brightness) and pseudo-random bytes (no grating) for the rest.
%   VIEWS: cell of grey images (0..1, same size), ordered from the smallest to the largest diffraction angle
%   (i.e. from the longest to the shortest period) - render_views with increasing PHI.
%   Sequences by N (period um / angle at 550 nm):
%     2: 5F 2.13/15  A5 0.85/40      3: 5F, CC67 1.23/27, A5      4: 5F, F9 1.43/23, 6F 1.07/31, A5
%     5: 5F, C5DB 1.70/19, CC67, DDB6 0.93/36, A5        7: all of 5F C5DB F9 CC67 6F DDB6 A5
%   OPT: rc 41 | thc_deg 90 | span_deg 40 | copies 1 | r0 28 | r1 54 | DR 0.01 | width_mm 20 | center [v u]
%        hogel_mm 0.2 | gamma 2.2 | floor 0.03 | fill 'hi'|'noise'|'lo' (rows/area outside picture)
%   Prints the --pairs argument to pass to DvdDiffractionRings.
if nargin < 3 || isempty(OPT), OPT = struct; end
g = @(f,d) getf(OPT,f,d);
N = numel(VIEWS);
seqs = struct('name',{'5F','C5DB','F9','CC67','6F','DDB6','A5'}, 'um',{2.133,1.700,1.433,1.233,1.066,0.933,0.853});
pick = {[1 7], [1 4 7], [1 3 5 7], [1 2 4 6 7], [1 2 3 5 6 7], 1:7};
assert(N >= 2 && N <= 7, 'N views must be 2..7'); sel = pick{N-1}; seqs = seqs(sel);
codes = zeros(1,N,'uint8'); pairs = {};
for k = 1:N
    if numel(seqs(k).name) == 2, codes(k) = uint8(hex2dec(seqs(k).name));
    else, pairs{end+1} = seqs(k).name; codes(k) = uint8(numel(pairs)); end %#ok<AGROW>
end
copies = g('copies',1); rc = g('rc',41); thc = deg2rad(g('thc_deg',90)); span = deg2rad(g('span_deg',40));
if copies > 1, span = 2*pi/copies; end
r0 = g('r0',28); r1 = g('r1',54); DR = g('DR',0.01); DX = 2.13e-3; L = g('hogel_mm',0.2);
BYTE_HI = uint8(hex2dec('92')); BYTE_LO = uint8(hex2dec('A5'));
switch lower(g('fill','hi')), case 'hi', FILL = BYTE_HI; case 'lo', FILL = BYTE_LO; otherwise, FILL = uint8(0); end
%% views -> amplitude interpolants in the local (v up, u right) frame
[h, w] = size(VIEWS{1}); W = g('width_mm',20); H = W*h/w; c = g('center',[0 0]);
ui = c(2) + ((1:w)-(w+1)/2)/w*W; vi = c(1) + ((h+1)/2-(1:h))/h*H;
[VI, UI] = ndgrid(vi(end:-1:1), ui); Fv = cell(1,N);
for k = 1:N
    a = double(VIEWS{k}); if size(a,3) > 1, a = mean(a,3); end
    a = a.^g('gamma',2.2); a(a < g('floor',0.03)) = 0;              % linear intensity -> slot duty
    Fv{k} = griddedInterpolant(VI, UI, a(end:-1:1,:), 'linear', 'none');
end
vmin = min(vi); vmax = max(vi); umin = min(ui); umax = max(ui);
%% raster
th0 = thc - span/2; dth = DX / r1; ncols = ceil(span/dth); nrows = round((r1 - r0)/DR);
rows = r0 + ((0:nrows-1) + 0.5)*DR; Q = repmat(FILL, nrows, ncols);
xc = rc*cos(thc); yc = rc*sin(thc); cth = cos(thc); sth = sin(thc);
fprintf('%d views: %s  (angles at 550 nm: %s deg)\n', N, strjoin({seqs.name}, ' '), mat2str(round(asind(0.55./[seqs.um])),2));
th = th0 + ((0:ncols-1)+0.5)*dth; tic
for i = 1:nrows
    r = rows(i); x = r*cos(th); y = r*sin(th);
    u = -(x-xc)*sth + (y-yc)*cth; v = (x-xc)*cth + (y-yc)*sth;
    if all(v < vmin | v > vmax), continue; end
    in = u >= umin & u <= umax & v >= vmin & v <= vmax; if ~any(in), continue; end
    hg = floor((u - umin)/L); p = (u - umin)/L - hg; k = min(floor(p*N), N-1); f = p*N - k;
    uc = umin + (hg + 0.5)*L;                                      % hogel centre
    row = repmat(FILL, 1, ncols); row(in) = 0;                      % inside picture: default random
    for kk = 0:N-1
        m = in & k == kk; if ~any(m), continue; end
        I = Fv{kk+1}(v(m)', uc(m)')'; I(isnan(I)) = 0;
        sel2 = f(m) < I; idx = find(m); row(idx(sel2)) = codes(kk+1);
    end
    Q(i,:) = row;
    if mod(i,400) == 0, fprintf('  row %d/%d r=%.1f %.0fs\n', i, nrows, r, toc); end
end
if copies > 1, Q = repmat(Q, 1, copies); ncols = ncols*copies; fprintf('tiled %d copies every %.0f deg\n', copies, 360/copies); end
fid = fopen(RASTER, 'w', 'ieee-le');
fwrite(fid, ['DVDHPO1' 0], 'char'); fwrite(fid, 1, 'int32');
fwrite(fid, [r0 DR th0 dth], 'double'); fwrite(fid, [nrows ncols], 'int32'); fwrite(fid, Q', 'uint8'); fclose(fid);
fprintf('wrote %s (%d x %d), %.0fs\n', RASTER, nrows, ncols, toc);
if isempty(pairs), fprintf('exe args: --pattern polar --raster %s\n', RASTER);
else, fprintf('exe args: --pattern polar --raster %s --pairs %s\n', RASTER, strjoin(pairs, ',')); end
if nargin >= 4 && ~isempty(PNG)      % per-view occupancy maps (what each eye angle sees), coarse
    f2 = figure('Visible','off','Position',[0 0 260*N 300]); step = 10;
    for kk = 1:N
        nc = ncols/copies; wdw = max(1, round(L/(dth*rc))); M = movmean(double(Q(1:step:end, 1:nc) == codes(kk)), wdw, 2) * N;
        subplot(1,N,kk); imagesc(((1:step:nc)-nc/2)*dth*rc, rows(1:step:end), M(:, 1:step:end), [0 1]); axis xy image; colormap gray
        title(sprintf('view %d: %s', kk, seqs(kk).name)); xlabel('arc (mm)'); if kk==1, ylabel('r (mm)'); end
    end
    exportgraphics(f2, PNG, 'Resolution', 80); close(f2);
end
end
function v = getf(s, f, d), if isfield(s, f) && ~isempty(s.(f)), v = s.(f); else, v = d; end, end
