function M = holo_image(LATTICE, VALUES, TGT, PNG, RMS)
%HOLO_IMAGE  Fresnel hologram of an arbitrary image on a 6 mm lattice (inner 4 mm, Q=92 frame, A5 corner).
%   TGT.type = 'dots' : TGT.P = [R T] dot positions (mm, image plane), each amplitude 1, random phase
%   TGT.type = 'mask' : TGT.mask (rows = radial, row 1 = outward), TGT.px (mm per mask pixel), TGT.c = [R T] centre;
%                       every image-plane sample inside the mask lit with random phase (continuous strokes)
%   Image plane: Z = 400 mm above the disc, lambda 532 nm, normal incidence.
%   RMS omitted/empty -> plain threshold; RMS given -> error diffusion on the track lattice with that target RMS.
LAMBDA=532e-6; Z=400; BYTE_HI=uint8(hex2dec('92')); BYTE_LO=uint8(hex2dec('A5')); DX=2e-3; HALF_IN=2.0;
L6 = read_lattice(LATTICE); n6 = numel(L6.ur);
inner = abs(L6.ur) <= HALF_IN & abs(L6.ut) <= HALF_IN;
ur = L6.ur(inner); ut = L6.ut(inner); ctrl = L6.ctrl(inner); n = numel(ur);

N = round(2*HALF_IN/DX); x = ((0:N-1)-N/2)*DX; [X, Y] = ndgrid(x, x);
chirp = exp(1i*pi*(X.^2+Y.^2)/(LAMBDA*Z));
xo = ((0:N-1)-N/2) * LAMBDA*Z/(N*DX); [R, T] = ndgrid(xo, xo);
rng(3); E = zeros(N);
switch TGT.type
    case 'dots'
        for i = 1:size(TGT.P,1)
            [~,ir] = min(abs(xo-TGT.P(i,1))); [~,it] = min(abs(xo-TGT.P(i,2)));
            E(ir,it) = exp(2i*pi*rand);
        end
    case 'mask'
        [nr, nc] = size(TGT.mask);
        row = floor((TGT.c(1) + nr*TGT.px/2 - R)/TGT.px) + 1; col = floor((T - TGT.c(2) + nc*TGT.px/2)/TGT.px) + 1;
        in = row >= 1 & row <= nr & col >= 1 & col <= nc;
        lit = false(N); lit(in) = TGT.mask(sub2ind([nr nc], row(in), col(in)));
        E(lit) = exp(2i*pi*rand(nnz(lit),1));
end
g = real(ifft2(ifftshift(E)) .* conj(chirp));
G = griddedInterpolant({x, x}, g, 'linear', 'nearest');
tg = G(ur, ut);
if nargin < 5 || isempty(RMS)
    s = sign(tg); s(s==0) = 1;
else
    trk = L6.trk(inner); tg = tg/sqrt(mean(tg.^2))*RMS;
    starts=[1; find(diff(trk))+1; n+1]; nb=zeros(n,1,'int64');
    for t=1:numel(starts)-2
        a=starts(t); b=starts(t+1)-1; c=starts(t+1); d=starts(t+2)-1;
        if d>c, j=interp1(ut(c:d),(c:d)',ut(a:b),'nearest','extrap'); else, j=repmat(c,b-a+1,1); end
        nb(a:b)=j;
    end
    s = error_diffusion(tg, ctrl, nb, trk, 0);
end

% simulation (format-fixed slots random)
h = s; unc = 2*(rand(n,1)>0.5)-1; h(~ctrl) = unc(~ctrl);
ir = min(max(floor((ur+HALF_IN)/DX)+1,1),N); it = min(max(floor((ut+HALF_IN)/DX)+1,1),N);
img = accumarray([ir it], h, [N N]) ./ max(accumarray([ir it], 1, [N N]),1);
I = abs(fftshift(fft2(img .* chirp))).^2;
lit = abs(E) > 0; share = sum(I(conv2(double(lit), ones(3), "same") > 0))/sum(I(:));
M = share;
fprintf('%-24s %-5s %4d lit samples, image energy share %.2f %%\n', LATTICE, TGT.type, nnz(lit), 100*share);
if nargin >= 4 && ~isempty(PNG)
    f = figure('Visible','off','Position',[0 0 520 520]);
    sel = xo > -1 & xo < 26; imagesc(xo(sel), xo(sel), log10(I(sel,sel)+1)); axis xy image; colormap(hot);
    caxis([log10(mean(I(:)))+0.3, log10(max(I(:)))]); xlabel('tangential (mm)'); ylabel('radial outward (mm)');
    title(strrep(PNG,'_','\_')); exportgraphics(f, PNG, 'Resolution', 90); close(f);
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
