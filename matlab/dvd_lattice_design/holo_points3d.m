function holo_points3d(LATTICE, VALUES, P, PNG, ZSIM)
%HOLO_POINTS3D  Point-cloud hologram with each point at its own depth, on a 6 mm lattice
%   (inner 4 mm hologram, 1 mm Q=92 frame, A5 corner). Plain threshold binarisation.
%   P = [R T Z] (mm): radial-outward / tangential position and height above the disc of every point.
%   ZSIM = heights at which the codeword-level Fresnel simulation is rendered into PNG.
LAMBDA=532e-6; BYTE_HI=uint8(hex2dec('92')); BYTE_LO=uint8(hex2dec('A5')); HALF_IN=2.0; DX=2e-3;
L6 = read_lattice(LATTICE); n6 = numel(L6.ur);
inner = abs(L6.ur) <= HALF_IN & abs(L6.ut) <= HALF_IN;
L = L6; f = fieldnames(L6);
for i = 1:numel(f), v0 = L6.(f{i}); if numel(v0) == n6, L.(f{i}) = v0(inner); end, end
L.size = 2*HALF_IN; n = numel(L.ur);
k = 2*pi/LAMBDA; rng(5); tgt = zeros(n,1);
for i = 1:size(P,1)
    tgt = tgt + cos(k*(sqrt((L.ur-P(i,1)).^2 + (L.ut-P(i,2)).^2 + P(i,3)^2) - P(i,3)) + 2*pi*rand);
end
s = sign(tgt); s(s==0) = 1;
fprintf('%-24s %2d points, Z %.0f..%.0f mm\n', LATTICE, size(P,1), min(P(:,3)), max(P(:,3)));
if ~isempty(PNG)
    h = s; unc = 2*(rand(n,1)>0.5)-1; h(~L.ctrl) = unc(~L.ctrl);
    fg = figure('Visible','off','Position',[0 0 260*numel(ZSIM) 290]);
    for zi = 1:numel(ZSIM)
        [I, xo] = fresnel_sim(h, L, DX, LAMBDA, ZSIM(zi));
        sc = ZSIM(zi)/400; sel = xo > -1*sc & xo < 16*sc;        % same angular window at every height
        subplot(1, numel(ZSIM), zi); imagesc(xo(sel), xo(sel), I(sel,sel).^0.5); axis xy image; colormap(hot);
        title(sprintf('paper at %d mm', ZSIM(zi))); xlabel('tang (mm)'); if zi == 1, ylabel('radial out (mm)'); end
    end
    exportgraphics(fg, PNG, 'Resolution', 80); close(fg);
end
v6 = repmat(BYTE_HI, n6, 1); v6(L6.ur > HALF_IN & L6.ut > HALF_IN) = BYTE_LO;
vin = repmat(BYTE_LO, n, 1); vin(s > 0) = BYTE_HI; v6(inner) = vin;
fid=fopen(VALUES,'w'); fwrite(fid,v6,'uint8'); fclose(fid);
end

function L = read_lattice(path)
    fid = fopen(path, 'r', 'ieee-le');
    magic = fread(fid, 8, '*char')';
    assert(strcmp(magic(1:7), 'DVDLAT1'), 'not a DVDLAT1 file');
    fread(fid, 1, 'int32');
    h = fread(fid, 9, 'double');
    L.patchR = h(1); L.patchAngle = h(2); L.size = h(3);
    L.inner = h(4); L.outer = h(5); L.pitch = h(6:8); L.sectors = h(9);
    n = fread(fid, 1, 'uint64'); L.ntracks = fread(fid, 1, 'int32');
    L.idx  = fread(fid, n, '*uint64');
    L.trk  = fread(fid, n, 'int32');
    L.ur   = fread(fid, n, 'single=>double');
    L.ut   = fread(fid, n, 'single=>double');
    L.ctrl = fread(fid, n, 'uint8') == 1;
    fclose(fid);
end
function [I, xo] = fresnel_sim(h, L, dx, lambda, Z)
    half = L.size/2; N = round(L.size/dx);
    ir = min(max(floor((L.ur+half)/dx)+1,1),N); it = min(max(floor((L.ut+half)/dx)+1,1),N);
    S = accumarray([ir it], h, [N N]); C = accumarray([ir it], 1, [N N]);
    img = S ./ max(C,1);                                 % box-average onto the raster
    x = ((0:N-1)-N/2)*dx; [X, Y] = ndgrid(x, x);
    U = fftshift(fft2(img .* exp(1i*pi*(X.^2+Y.^2)/(lambda*Z))));
    I = abs(U).^2;
    xo = ((0:N-1)-N/2) * lambda*Z/(N*dx);              % image-plane coordinate, mm
end
