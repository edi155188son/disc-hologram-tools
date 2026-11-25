function holo_imageplane(LATTICE, VALUES, LAYERS, PNG)
%HOLO_IMAGEPLANE  White-light image-plane hologram on a 10 mm lattice (inner 8 mm, 1 mm Q=92 frame, A5 corner).
%   LAYERS(k).fn = @(R,T) logical shape (mm, patch frame: R radial outward, T tangential), LAYERS(k).Z = height
%   of that layer above the disc (mm, negative = behind the disc). Image light leaves at ALPHA radially outward
%   from the specular direction, with a band-limited random diffuser of +/-SPREAD, design wavelength 550 nm.
%   Binarisation: plain threshold. PNG: what an eye looking along ALPHA (pupil +/-0.5 deg) sees at 450/550/650 nm.
LAMBDA0=550e-6; ALPHA=deg2rad(4); SPREAD=deg2rad(3); DX=2e-3; HALF_IN=4.0; PUPIL=deg2rad(0.5);
BYTE_HI=uint8(hex2dec('92')); BYTE_LO=uint8(hex2dec('A5'));
L6 = read_lattice(LATTICE); n6 = numel(L6.ur);
inner = abs(L6.ur) <= HALF_IN & abs(L6.ut) <= HALF_IN;
ur = L6.ur(inner); ut = L6.ut(inner); ctrl = L6.ctrl(inner); n = numel(ur);
N = round(2*HALF_IN/DX); x = ((0:N-1)-N/2)*DX; [R, T] = ndgrid(x, x);
f = ((0:N-1)-N/2)/(N*DX); [FR, FT] = ndgrid(f, f);
asm = @(E, z, lam) ifft2(ifftshift(fftshift(fft2(E)) .* exp(2i*pi*z*sqrt(max(1/lam^2 - FR.^2 - FT.^2, 0))) .* (FR.^2+FT.^2 < 1/lam^2)));
rng(9); Hd = zeros(N);
for k = 1:numel(LAYERS)
    A = double(LAYERS(k).fn(R, T));
    ph = angle(ifft2(ifftshift((randn(N)+1i*randn(N)) .* (FR.^2+FT.^2 < (sin(SPREAD)/LAMBDA0)^2))));
    E = A .* exp(1i*ph) .* exp(2i*pi*sin(ALPHA)/LAMBDA0*R);
    Hd = Hd + asm(E, -LAYERS(k).Z, LAMBDA0);
end
G = griddedInterpolant({x, x}, real(Hd), 'linear', 'nearest');
s = sign(G(ur, ut)); s(s==0) = 1;
fprintf('%-14s %d layers, Z = %s mm\n', LATTICE, numel(LAYERS), mat2str([LAYERS.Z]));
if ~isempty(PNG)
    h = s; unc = 2*(rand(n,1)>0.5)-1; h(~ctrl) = unc(~ctrl);
    ir = min(max(floor((ur+HALF_IN)/DX)+1,1),N); it = min(max(floor((ut+HALF_IN)/DX)+1,1),N);
    hg = accumarray([ir it], h, [N N]) ./ max(accumarray([ir it], 1, [N N]),1);
    lams = [650 550 450]*1e-6; fg = figure('Visible','off','Position',[0 0 300*numel(LAYERS) 320]);
    for k = 1:numel(LAYERS)
        rgb = zeros(N, N, 3);
        for c = 1:3
            lam = lams(c);
            pup = (FR - sin(ALPHA)/lam).^2 + FT.^2 < (sin(PUPIL)/lam)^2;      % eye along ALPHA, small pupil
            U = asm(hg, LAYERS(k).Z, lam);
            U = ifft2(ifftshift(fftshift(fft2(U)) .* pup));
            rgb(:,:,c) = abs(U).^2;
        end
        rgb = rgb / prctile(rgb(:), 99.7); rgb = min(rgb, 1);
        sel = abs(x) < 4; subplot(1, numel(LAYERS), k);
        image(x(sel), x(sel), permute(rgb(sel,sel,:), [1 2 3])); axis xy image;
        title(sprintf('focus at Z = %+g mm', LAYERS(k).Z)); xlabel('tangential (mm)'); if k==1, ylabel('radial out (mm)'); end
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
