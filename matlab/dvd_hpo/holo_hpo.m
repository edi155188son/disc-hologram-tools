function holo_hpo(LATTICE, VALUES, SCENE, OPT, PNG)
%HOLO_HPO  White-light, horizontal-parallax-only (along-track) Fresnel hologram on a 10 mm lattice.
%   Fringes vary ONLY along the track (tangential, T); every track row is an independent 1-D hologram
%   of the scene slice at that radial position (rainbow-hologram style, no vertical parallax).
%   Light source + eye in the plane that contains the tangential direction; image light leaves the
%   disc at OPT.ALPHA (deg, from the specular direction, along T) for the design wavelength.
%   SCENE(k).fn = @(R,T) logical shape (mm; R radial outward, T tangential), SCENE(k).Z = height above
%   the disc (mm, +toward viewer), SCENE(k).lambda = wavelength (nm) that leaves at ALPHA (default 550).
%   OPT.ALPHA (deg, default 4.5), OPT.SPREAD (deg, band-limited random phase along T, default 1.5),
%   OPT.DR (row spacing mm, default 0.01), OPT.seed.  Inner 8 mm = hologram, 1 mm Q=92 frame, A5 corner.
%   PNG: simulated eye view (pupil +/-0.5 deg at ALPHA) at 650/550/450 nm, focused at each SCENE Z.
if nargin < 4 || isempty(OPT), OPT = struct; end
ALPHA  = deg2rad(getf(OPT,'ALPHA',4.5)); SPREAD = deg2rad(getf(OPT,'SPREAD',1.5));
DR = getf(OPT,'DR',0.01); seed = getf(OPT,'seed',9); PUPIL = deg2rad(0.5);
DX = 2.13e-3; HALF_IN = 4.0; HALF = 5.0; LAM0 = 550e-6;
BYTE_HI = uint8(hex2dec('92')); BYTE_LO = uint8(hex2dec('A5'));
L6 = read_lattice(LATTICE); n6 = numel(L6.ur);
inner = abs(L6.ur) <= HALF_IN & abs(L6.ut) <= HALF_IN;
ur = L6.ur(inner); ut = L6.ut(inner); ctrl = L6.ctrl(inner); n = numel(ur);
NT = 2*round(HALF/DX); x = ((0:NT-1)-NT/2)*DX;            % tangential grid, +/-HALF
NR = 2*round(HALF/DR); r = ((0:NR-1)-NR/2)*DR;            % radial rows, +/-HALF
[R, T] = ndgrid(r, x);
fT = ((0:NT-1)-NT/2)/(NT*DX);                             % 1-D spatial frequency along T (per mm)
asm1 = @(E, z, lam) ifft(ifftshift(fftshift(fft(E,[],2),2) .* exp(2i*pi*z*sqrt(max(1/lam^2 - fT.^2, 0))) .* (abs(fT) < 1/lam), 2), [], 2);
rng(seed); Hd = zeros(NR, NT);
for k = 1:numel(SCENE)
    lam = getf(SCENE(k),'lambda',550) * 1e-6;
    A = double(SCENE(k).fn(R, T));
    if ~any(A(:)), continue; end
    if SPREAD > 0
        nz = randn(NR,NT) + 1i*randn(NR,NT);
        ph = angle(ifft(ifftshift(fftshift(fft(nz,[],2),2) .* (abs(fT) < sin(SPREAD)/lam), 2), [], 2));
    else
        ph = zeros(NR,NT);
    end
    E = A .* exp(1i*ph) .* exp(2i*pi*sin(ALPHA)/lam * T);  % carrier: leaves at ALPHA for this lambda
    Hd = Hd + asm1(E, -SCENE(k).Z, lam);
end
G = griddedInterpolant({r, x}, real(Hd), 'linear', 'nearest');
s = sign(G(ur, ut)); s(s==0) = 1;
fprintf('%-14s %d layers, Z = %s mm, ALPHA %.1f deg, SPREAD %.1f deg, lit %.1f%%\n', LATTICE, numel(SCENE), ...
    mat2str([SCENE.Z]), rad2deg(ALPHA), rad2deg(SPREAD), 100*mean(s>0));
if nargin >= 5 && ~isempty(PNG)
    h = s; unc = 2*(rand(n,1)>0.5)-1; h(~ctrl) = unc(~ctrl);
    ir = min(max(floor((ur+HALF)/DR)+1,1),NR); it = min(max(floor((ut+HALF)/DX)+1,1),NT);
    hg = accumarray([ir it], h, [NR NT]) ./ max(accumarray([ir it], 1, [NR NT]),1);
    lams = linspace(450,650,11)*1e-6;                     % white-light continuum (speckle averages over colour)
    wR = double(lams > 590e-6); wG = double(lams >= 500e-6 & lams <= 590e-6); wB = double(lams < 500e-6);
    Zs = unique([SCENE.Z]); if numel(Zs) > 4, Zs = linspace(min(Zs), max(Zs), 4); end
    sig = 0.04; kr = exp(-((-10:10)*DR).^2/(2*sig^2)); kt = exp(-((-60:60)*DX).^2/(2*sig^2));   % eye blur ~0.1 mm
    kr = kr/sum(kr); kt = kt/sum(kt);
    fg = figure('Visible','off','Position',[0 0 300*numel(Zs) 320]);
    for k = 1:numel(Zs)
        rgb = zeros(NR, NT, 3);
        for c = 1:numel(lams)
            lam = lams(c);
            pup = abs(fT - sin(ALPHA)/lam) < sin(PUPIL)/lam;   % eye along ALPHA, small pupil
            U = asm1(hg, Zs(k), lam);
            U = ifft(ifftshift(fftshift(fft(U,[],2),2) .* pup, 2), [], 2);
            I = abs(U).^2;
            rgb(:,:,1) = rgb(:,:,1) + wR(c)*I; rgb(:,:,2) = rgb(:,:,2) + wG(c)*I; rgb(:,:,3) = rgb(:,:,3) + wB(c)*I;
        end
        for c = 1:3, rgb(:,:,c) = conv2(kr, kt, rgb(:,:,c), 'same'); end
        rgb = rgb / prctile(rgb(:), 99.5); rgb = min(rgb, 1).^(1/2.2);
        sr = abs(r) < 4; st = abs(x) < 4; subplot(1, numel(Zs), k);
        image(x(st), r(sr), rgb(sr,st,:)); axis xy image;
        title(sprintf('focus Z = %+g mm', Zs(k))); xlabel('tangential (mm)'); if k==1, ylabel('radial out (mm)'); end
    end
    exportgraphics(fg, PNG, 'Resolution', 80); close(fg);
end
v6 = repmat(BYTE_HI, n6, 1); v6(L6.ur > HALF_IN & L6.ut > HALF_IN) = BYTE_LO;
vin = repmat(BYTE_LO, n, 1); vin(s > 0) = BYTE_HI; v6(inner) = vin;
fid = fopen(VALUES,'w'); fwrite(fid, v6, 'uint8'); fclose(fid);
end

function v = getf(s, f, d)
    if isfield(s, f) && ~isempty(s.(f)), v = s.(f); else, v = d; end
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
