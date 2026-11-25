%% holo_inverse.m  -  DVD-R binary Fresnel hologram design on the REAL codeword lattice
%
% Pipeline
%   1) DvdDiffractionRings --sectors N --export-lattice lattice.bin --patch-r 40 --patch-angle 0 --patch-size 4
%   2) run this script               -> values.bin (one byte per lattice record)
%   3) DvdDiffractionRings --sectors N --output holo.iso --pattern lattice --lattice lattice.bin --values values.bin
%
% Boundary conditions taken from the disc format (all inside lattice.bin):
%   * exact position (ur, ut) of every channel codeword in the patch, from the calibrated spiral
%   * which codewords are controllable (user data) and which are fixed by the format
%     (sync / ID / ECC parity, ~15 %) -> those are treated as unknown with expected value U_EXPECT
%   * tangential quantisation 2.13 um, track pitch 0.738 um (implicit in the positions)
%
% Patch-local frame: ur = radially outward ("up" in the image), ut = +tangential ("right"), mm,
% origin at the patch centre. The image is formed Z mm above the disc by a normally incident
% collimated beam.
%
% Algorithm: target = real Fresnel hologram of a point cloud; binarised with Floyd-Steinberg error
% diffusion on the (track, along-track) lattice, so the binarisation noise and the uncontrollable
% slots are pushed to high spatial frequencies outside the image window. Then a single-FFT
% Fresnel simulation compares plain thresholding with error diffusion.
%
% Note on the palette: with a BINARY palette (two bytes, effective reflectances cA, cB) the written
% field is (cA+cB)/2 + (cA-cB)/2 * s,  s = +/-1.  The constant only feeds the zero order, so the
% design does NOT depend on the measured cA, cB - they only set the efficiency. The measured palette
% matters once you use 3+ bytes (multi-level / phase), see palette disc.

clear; clc;
LATTICE   = 'lattice.bin';
VALUES    = 'values.bin';
LAMBDA    = 532e-6;      % mm (green). 650e-6 red, 405e-6 violet
Z         = 400;         % mm, image distance above the disc
BYTE_HI   = uint8(hex2dec('92'));   % s = +1
BYTE_LO   = uint8(hex2dec('A5'));   % s = -1
U_EXPECT  = 0;           % expected s-value of format-fixed codewords (unknown -> 0)
TARGET_RMS = 0.5;        % target amplitude scaling before binarisation (0.3 .. 0.7)
SIM_DX    = 2e-3;        % mm, simulation raster

%% 1. read lattice
L = read_lattice(LATTICE);
n = numel(L.ur);
fprintf('patch r=%.1f mm @ %.0f deg, %.1f mm, %d codewords on %d tracks, %.1f%% format-fixed\n', ...
    L.patchR, L.patchAngle, L.size, n, L.ntracks, 100*mean(~L.ctrl));

%% 2. target: letter F, 12 dots, in the image plane (ur', ut') at height Z
grid = [0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3];   % [col row], row 0 = top
offR = 7; offT = 6; pitch = 1.0;                           % mm: image centre outward/tangential
rng(1);
P = zeros(size(grid,1),3);
for i = 1:size(grid,1)
    P(i,1) = offR + (3-grid(i,2))*pitch + (rand-0.5)*pitch/3;   % radial position
    P(i,2) = offT + (grid(i,1)-1.5)*pitch + (rand-0.5)*pitch/3; % tangential position
    P(i,3) = 2*pi*rand;                                         % random phase
end
k = 2*pi/LAMBDA;
tgt = zeros(n,1);
for i = 1:size(P,1)
    tgt = tgt + cos(k*(sqrt((L.ur-P(i,1)).^2 + (L.ut-P(i,2)).^2 + Z^2) - Z) + P(i,3));
end
tgt = tgt / sqrt(mean(tgt.^2)) * TARGET_RMS;

%% 3. neighbour of every codeword on the next (outer) track, nearest in ut
starts = [1; find(diff(L.trk))+1; n+1];
nb = zeros(n,1,'int64');
for t = 1:numel(starts)-2
    a = starts(t); b = starts(t+1)-1; c = starts(t+1); d = starts(t+2)-1;
    if d > c
        j = interp1(L.ut(c:d), (c:d)', L.ut(a:b), 'nearest', 'extrap');
    else
        j = repmat(c, b-a+1, 1);
    end
    nb(a:b) = j;
end

%% 4. binarisation
sA = sign(tgt); sA(sA==0) = 1;                          % plain threshold
sB = error_diffusion(tgt, L.ctrl, nb, L.trk, U_EXPECT); % error diffusion

%% 5. simulation (format-fixed slots get random +/-1: their true value is unknown)
unc = 2*(rand(n,1) > 0.5) - 1;
for m = 1:2
    if m == 1, s = sA; name = 'threshold'; else, s = sB; name = 'error diffusion'; end
    h = s; h(~L.ctrl) = unc(~L.ctrl);
    [I, xo] = fresnel_sim(h, L, SIM_DX, LAMBDA, Z);
    [ratio, share] = score(I, xo, P, offR, offT);
    fprintf('%-16s  dot/background %.0f   dot energy share %.2f %%\n', name, ratio, 100*share);
    subplot(1,2,m);
    sel = xo > -2 & xo < 16; selt = xo > -6 & xo < 12;
    imagesc(xo(selt), xo(sel), log10(I(sel,selt)+1)); axis xy image; colormap(inferno_like);
    caxis([log10(mean(I(:)))+0.5, log10(max(I(:)))]);
    xlabel('tangential (mm)'); ylabel('radial outward (mm)'); title(name);
end

%% 6. write values.bin (one byte per lattice record, only controllable ones are used)
v = repmat(BYTE_LO, n, 1); v(sB > 0) = BYTE_HI;
fid = fopen(VALUES, 'w'); fwrite(fid, v, 'uint8'); fclose(fid);
fprintf('wrote %s\n', VALUES);

%% ---------------------------------------------------------------- local functions
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

function s = error_diffusion(tgt, ctrl, nb, trk, uExpect)
    % Floyd-Steinberg on the track lattice: 7/16 along the track, 3/16 5/16 1/16 to the next track.
    n = numel(tgt); w = tgt; s = zeros(n,1);
    for i = 1:n
        if ctrl(i)
            if w(i) >= 0, s(i) = 1; else, s(i) = -1; end
            e = w(i) - s(i);
        else
            e = w(i) - uExpect;    % fixed slot: whatever we wanted here is passed on
        end
        if i < n && trk(i+1) == trk(i), w(i+1) = w(i+1) + e*7/16; end
        j = nb(i);
        if j > 0
            if j > 1 && trk(j-1) == trk(j), w(j-1) = w(j-1) + e*3/16; end
            w(j) = w(j) + e*5/16;
            if j < n && trk(j+1) == trk(j), w(j+1) = w(j+1) + e*1/16; end
        end
    end
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

function [ratio, share] = score(I, xo, P, offR, offT)
    [R, T] = ndgrid(xo, xo);
    win = R > offR-3.5 & R < offR+4.5 & T > offT-3 & T < offT+3;
    dots = false(size(I)); pk = zeros(size(P,1),1);
    for i = 1:size(P,1)
        m = (R-P(i,1)).^2 + (T-P(i,2)).^2 < 0.25^2; dots = dots | m; pk(i) = max(I(m));
    end
    ratio = mean(pk) / mean(I(win & ~dots)); share = sum(pk) / sum(I(:));
end

function c = inferno_like
    t = linspace(0,1,256)';
    c = [min(1,1.6*t), max(0,min(1,1.8*t-0.6)), max(0, 0.6*sin(pi*t) + max(0,3*t-2))];
    c = min(max(c,0),1);
end
