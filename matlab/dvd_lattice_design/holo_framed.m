%% holo_framed.m  -  same error-diffusion F hologram as holo_inverse.m, plus a visible locator frame
%   Lattice must be exported with --patch-size 6 (lattice6.bin). Inner 4x4 mm = hologram,
%   outer 1 mm band = frame of solid Q=92 (2.13 um tangential grating, glows under white light),
%   corner (outward + tangential, where the F image appears) = solid A5 as an orientation mark.
% set LATTICE / VALUES before running to override (batch use)
if ~exist('LATTICE','var'), LATTICE='lattice6.bin'; end
if ~exist('VALUES','var'), VALUES='values6.bin'; end
LAMBDA=532e-6; Z=400; BYTE_HI=uint8(hex2dec('92')); BYTE_LO=uint8(hex2dec('A5'));
U_EXPECT=0; TARGET_RMS=0.5; SIM_DX=2e-3; HALF_IN=2.0;

L6 = read_lattice(LATTICE); n6 = numel(L6.ur);
inner = abs(L6.ur) <= HALF_IN & abs(L6.ut) <= HALF_IN;
L = L6; f = fieldnames(L6);
for i = 1:numel(f), v0 = L6.(f{i}); if numel(v0) == n6, L.(f{i}) = v0(inner); end, end
L.size = 2*HALF_IN; n = numel(L.ur);
fprintf('6 mm lattice: %d records, inner 4 mm: %d (%.1f%% format-fixed)\n', n6, n, 100*mean(~L.ctrl));

grid=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; offR=7; offT=6; pitch=1.0; rng(1);
P=zeros(size(grid,1),3);
for i=1:size(grid,1)
    P(i,1)=offR+(3-grid(i,2))*pitch+(rand-0.5)*pitch/3;
    P(i,2)=offT+(grid(i,1)-1.5)*pitch+(rand-0.5)*pitch/3; P(i,3)=2*pi*rand;
end
k=2*pi/LAMBDA; tgt=zeros(n,1);
for i=1:size(P,1), tgt=tgt+cos(k*(sqrt((L.ur-P(i,1)).^2+(L.ut-P(i,2)).^2+Z^2)-Z)+P(i,3)); end
tgt=tgt/sqrt(mean(tgt.^2))*TARGET_RMS;

starts=[1; find(diff(L.trk))+1; n+1]; nb=zeros(n,1,'int64');
for t=1:numel(starts)-2
    a=starts(t); b=starts(t+1)-1; c=starts(t+1); d=starts(t+2)-1;
    if d>c, j=interp1(L.ut(c:d),(c:d)',L.ut(a:b),'nearest','extrap'); else, j=repmat(c,b-a+1,1); end
    nb(a:b)=j;
end
s = error_diffusion(tgt, L.ctrl, nb, L.trk, U_EXPECT);

unc=2*(rand(n,1)>0.5)-1; h=s; h(~L.ctrl)=unc(~L.ctrl);
[I,xo]=fresnel_sim(h,L,SIM_DX,LAMBDA,Z); [ratio,share]=score(I,xo,P,offR,offT);
fprintf('inner hologram: dot/background %.0f, dot energy share %.2f %%\n', ratio, 100*share);

v6 = repmat(BYTE_HI, n6, 1);                                   % frame = solid 92
v6(L6.ur > HALF_IN & L6.ut > HALF_IN) = BYTE_LO;               % orientation corner = solid A5
vin = repmat(BYTE_LO, n, 1); vin(s > 0) = BYTE_HI; v6(inner) = vin;
fid=fopen(VALUES,'w'); fwrite(fid,v6,'uint8'); fclose(fid);
fprintf('wrote %s (%d bytes)\n', VALUES, n6);

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
