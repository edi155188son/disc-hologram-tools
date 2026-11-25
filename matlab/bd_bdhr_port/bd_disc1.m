%% bd_disc1.m  -  BD-R port of every hologram test done on DVD-R (rasters for BdHolo)
% Each patch is a raster in the patch frame (rows = radial outward ur, cols = tangential ut, 1 um cells):
% 255 = byte 44 (6T-rich, "bright"), 0 = byte AA (3T-rich, "dark"), 128 = pseudo-random (no grating).
% Laser tests: Fresnel, lambda 532 nm, normal incidence. White tests: image plane, 550 nm, 4 deg radial.
clear; clc; OUT = pwd; DX = 1e-3; LAM = 532e-6; rng(1);
fD=['11110';'10001';'10001';'10001';'10001';'10001';'11110']; fV=['10001';'10001';'10001';'10001';'10001';'01010';'00100'];
txt=[fD repmat('0',7,1) fV repmat('0',7,1) fD]=='1';
heart = @(R,T,s) ((T/s).^2 + (R/s).^2 - 1).^3 - (T/s).^2 .* (R/s).^3 <= 0;
star  = @(R,T,r0) hypot(R,T) <= r0*(0.6 + 0.4*cos(5*atan2(T,R)));

P = struct('name',{},'r',{},'a',{},'size',{},'letter',{},'kind',{},'arg',{});
% ring 1, r = 28.5 mm: 6 mm laser patches (4 mm hologram)
gF=[0 0;0 1;0 2;0 3;0 4;0 5;0 6;1 0;2 0;3 0;1 3;2 3]; PF=zeros(12,3);
for i=1:12, PF(i,:)=[7+(3-gF(i,2))+(rand-0.5)/3, 6+(gF(i,1)-1.5)+(rand-0.5)/3, 400]; end
th=linspace(0,2*pi,25)'; th(end)=[]; sm=[7+2.5*sin(th) 7+2.5*cos(th); 7.8 6.1; 7.8 7.9];
mth=deg2rad(linspace(205,335,7))'; sm=[sm; 6.7+1.4*sin(mth) 7+1.4*cos(mth)]; sm(:,3)=400;
sep=[0.2 0.3 0.4 0.6 0.8]; pairs=[]; for i=1:5, pairs=[pairs; 4+1.6*i 5 400; 4+1.6*i 5+sep(i) 400]; end %#ok<AGROW>
P(end+1)=struct('name','A_F','r',28.5,'a',0,'size',6,'letter','A','kind','points','arg',PF);
P(end+1)=struct('name','B_text','r',28.5,'a',90,'size',6,'letter','B','kind','mask','arg',{{txt,0.4,[7 8]}});
P(end+1)=struct('name','C_smiley','r',28.5,'a',180,'size',6,'letter','C','kind','points','arg',sm);
P(end+1)=struct('name','D_pairs','r',28.5,'a',270,'size',6,'letter','D','kind','points','arg',pairs);
% ring 2, r = 35.5 mm: 8 mm white-light patches (6 mm hologram)
P(end+1)=struct('name','E_heart3','r',35.5,'a',0,'size',8,'letter','E','kind','white','arg',{{ {@(R,T) heart(R+0.3,T,1.6)}, 3 }});
P(end+1)=struct('name','F_ringstar','r',35.5,'a',120,'size',8,'letter','F','kind','white','arg',{{ {@(R,T) abs(hypot(R,T)-2.2)<0.25, @(R,T) star(R,T,1.3)}, [2 12] }});
P(end+1)=struct('name','G_heart10','r',35.5,'a',240,'size',8,'letter','G','kind','white','arg',{{ {@(R,T) heart(R+0.3,T,1.6)}, 10 }});
% ring 3, r = 42.5 mm: 6 mm 3D point clouds
d0=[8 8]/400; at=@(o,Z) [(d0(1)+o(:,1)/400).*Z, (d0(2)+o(:,2)/400).*Z, Z];
plus=[0 0;1.5 0;-1.5 0;0 1.5;0 -1.5]; t8=(0:7)'*pi/4; ring=2.5*[sin(t8) cos(t8)];
ti=(0:11)'; zc=linspace(200,600,7)';
f3=['11110';'00001';'00001';'01110';'00001';'00001';'11110']=='1';
[r3,c3]=find(f3); o3=[(4-r3)*0.8,(c3-3)*0.8-3.5]+(rand(numel(r3),2)-0.5)*0.8/3;
[rD,cD]=find(fD=='1'); oD=[(4-rD)*0.8,(cD-3)*0.8+3.5]+(rand(numel(rD),2)-0.5)*0.8/3;
P(end+1)=struct('name','H_ladder','r',42.5,'a',0,'size',6,'letter','H','kind','points','arg',at([zeros(7,1) linspace(-4.5,4.5,7)'], zc));
P(end+1)=struct('name','I_3D','r',42.5,'a',90,'size',6,'letter','I','kind','points','arg',[at(o3,250*ones(numel(r3),1)); at(oD,550*ones(numel(rD),1))]);
P(end+1)=struct('name','J_helix','r',42.5,'a',180,'size',6,'letter','J','kind','points','arg',at(3*[sin(2*pi*ti/12) cos(2*pi*ti/12)],250+300*ti/11));
P(end+1)=struct('name','K_plusring','r',42.5,'a',270,'size',6,'letter','K','kind','points','arg',[at(plus,250*ones(5,1)); at(ring,550*ones(8,1))]);

files = {};
for k = 1:numel(P)
    p = P(k); hin = p.size/2 - 1; tic;
    switch p.kind
        case 'points', s = holo_points(p.arg, hin, DX, LAM);
        case 'mask',   s = holo_mask(p.arg{1}, p.arg{2}, p.arg{3}, hin, DX, LAM, 400);
        case 'white',  s = holo_white(p.arg{1}, p.arg{2}, hin);
    end
    N = round(p.size/DX); n = size(s,1); o = (N-n)/2;
    V = repmat(uint8(255), N, N);                                         % frame = 44
    x = ((0:N-1)+0.5)*DX - p.size/2; [UR, UT] = ndgrid(x, x);
    V(UR > hin & UT > hin) = 0;                                           % orientation corner = AA
    V(o+1:o+n, o+1:o+n) = uint8(255*(s > 0));
    fn = fullfile(OUT, ['bd1_' p.name '.bdhr']); write_bdhr(fn, V, DX, p.r, p.a); files{end+1} = fn; %#ok<AGROW>
    % label: 4 mm, 5x7 letter, 0.5 mm cells, tangentially before the patch
    la = p.a - rad2deg((p.size/2 + 1 + 2)/p.r);
    fn = fullfile(OUT, ['bd1_lab_' p.letter '.bdhr']); write_bdhr(fn, letter_raster(p.letter), 0.05, p.r, la); files{end+1} = fn; %#ok<AGROW>
    fprintf('%-12s r=%.1f a=%3d  %.1f s\n', p.name, p.r, p.a, toc);
end
fid = fopen(fullfile(OUT,'bd1_patches.txt'),'w'); fprintf(fid, '%s', strjoin(files, ',')); fclose(fid);
save(fullfile(OUT,'bd1_design.mat'), 'P');

%% ------------------------------------------------------------------ functions
function s = holo_points(PZ, hin, DX, LAM)
    x = ((0:round(2*hin/DX)-1)+0.5)*DX - hin; [R, T] = ndgrid(x, x); k = 2*pi/LAM; t = zeros(size(R));
    for i = 1:size(PZ,1)
        t = t + cos(k*(sqrt((R-PZ(i,1)).^2 + (T-PZ(i,2)).^2 + PZ(i,3)^2) - PZ(i,3)) + 2*pi*rand);
    end
    s = sign(t);
end
function s = holo_mask(mask, px, c, hin, DX, LAM, Z)
    N = round(2*hin/DX); x = ((0:N-1)-N/2)*DX; [X, Y] = ndgrid(x, x);
    xo = ((0:N-1)-N/2)*LAM*Z/(N*DX); [R, T] = ndgrid(xo, xo); [nr, nc] = size(mask);
    row = floor((c(1) + nr*px/2 - R)/px) + 1; col = floor((T - c(2) + nc*px/2)/px) + 1;
    in = row>=1 & row<=nr & col>=1 & col<=nc; lit = false(N); lit(in) = mask(sub2ind([nr nc], row(in), col(in)));
    E = zeros(N); E(lit) = exp(2i*pi*rand(nnz(lit),1));
    s = sign(real(ifft2(ifftshift(E)) .* exp(-1i*pi*(X.^2+Y.^2)/(LAM*Z))));
end
function s = holo_white(fns, Zs, hin)
    LAM=550e-6; ALPHA=deg2rad(4); SPREAD=deg2rad(3); D2=2e-3; N=round(2*hin/D2);
    x = ((0:N-1)-N/2)*D2; [R, T] = ndgrid(x, x); f = ((0:N-1)-N/2)/(N*D2); [FR, FT] = ndgrid(f, f);
    asm = @(E, z) ifft2(ifftshift(fftshift(fft2(E)) .* exp(2i*pi*z*sqrt(max(1/LAM^2-FR.^2-FT.^2,0))) .* (FR.^2+FT.^2 < 1/LAM^2)));
    H = zeros(N);
    for k = 1:numel(fns)
        ph = angle(ifft2(ifftshift((randn(N)+1i*randn(N)) .* (FR.^2+FT.^2 < (sin(SPREAD)/LAM)^2))));
        H = H + asm(double(fns{k}(R,T)) .* exp(1i*ph) .* exp(2i*pi*sin(ALPHA)/LAM*R), -Zs(k));
    end
    s = kron(sign(real(H)), ones(2));                                     % 2 um -> 1 um cells
end
function V = letter_raster(L)
    F = {'A',['01110';'10001';'10001';'11111';'10001';'10001';'10001'], 'B',['11110';'10001';'10001';'11110';'10001';'10001';'11110'], ...
         'C',['01111';'10000';'10000';'10000';'10000';'10000';'01111'], 'D',['11110';'10001';'10001';'10001';'10001';'10001';'11110'], ...
         'E',['11111';'10000';'10000';'11110';'10000';'10000';'11111'], 'F',['11111';'10000';'10000';'11110';'10000';'10000';'10000'], ...
         'G',['01111';'10000';'10000';'10011';'10001';'10001';'01111'], 'H',['10001';'10001';'10001';'11111';'10001';'10001';'10001'], ...
         'I',['11111';'00100';'00100';'00100';'00100';'00100';'11111'], 'J',['00111';'00010';'00010';'00010';'00010';'10010';'01100'], ...
         'K',['10001';'10010';'10100';'11000';'10100';'10010';'10001']};
    font = F{find(strcmp(F(1:2:end), L))*2};
    bm = font == '1'; V = repmat(uint8(128), 80, 80);                    % 4 mm @ 50 um
    for r = 1:7, for c = 1:5
        if bm(r,c), V((75-10*r+1):(75-10*(r-1)), 15+10*(c-1)+(1:10)) = 255; end   % font row 1 = outward = high ur index
    end, end
end
function write_bdhr(fn, V, dx, r, a)
    fid = fopen(fn, 'w', 'ieee-le'); fwrite(fid, ['BDHR1' 0 0 0], 'uint8');
    fwrite(fid, size(V,1), 'int32'); fwrite(fid, [dx r a], 'double'); fwrite(fid, V', 'uint8'); fclose(fid);
end
