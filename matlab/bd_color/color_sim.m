% color_sim.m -- feasibility of white-light colour rainbow holograms on BD-R (2026-11-25)
% Three colour channels share one block: S = sum_c w_c conj(e_c) exp(i 2 pi v / L_c), encoded by double-phase
% (adjacent tracks arg S -/+ acos|S|, 50 % duty) on the BD grid (0.3199 um tracks x 0.929 um bytes, 15 % random bytes).
% L_c = lambda_c / sin(theta0): at the design viewing angle every channel diffracts its own colour.
% Each channel: random phase, band-limited +-HU horizontally and +-delta vertically (the rainbow "slit").
% Reconstruction: white LED (or flat) spectrum, normal incidence, eye pupil +-0.4 deg; CIE 1931 -> sRGB.
% Test patch 0.8 mm: stripes R, G, B, W (= R+G+B) + background.
here = getenv('HOLO_OUT'); if isempty(here), here = pwd; end   % output directory (HOLO_OUT or the current folder)
P = 0.3199e-3; BY = 0.929e-3; Lp = 0.8; HU = 8;
vg = (0:round(Lp/P)-1)'*P; ug = (0:round(Lp/BY)-1)*BY; Nv = numel(vg); Nu = numel(ug);
fv = ((0:Nv-1)' - floor(Nv/2))/(Nv*P); fu = ((0:Nu-1) - floor(Nu/2))/(Nu*BY);
[Vm, Um] = ndgrid(vg, ug);
st = [0.06 0.22; 0.24 0.40; 0.42 0.58; 0.60 0.76];                       % u ranges of R, G, B, W stripes
inV = Vm > 0.08 & Vm < 0.72; mask = false(Nv, Nu, 4);
for k = 1:4, mask(:,:,k) = inV & Um > st(k,1) & Um < st(k,2); end
bg = inV & ~any(mask, 3) & (Um > 0.05 & Um < 0.77);
lam = 400:10:700;
cmf = @(l, m, s1, s2) exp(-0.5*((l - m)./((l < m)*s1 + (l >= m)*s2)).^2);
xb = 1.056*cmf(lam,599.8,37.9,31.0) + 0.362*cmf(lam,442.0,16.0,26.7) - 0.065*cmf(lam,501.1,20.4,26.2);
yb = 0.821*cmf(lam,568.8,46.9,40.5) + 0.286*cmf(lam,530.9,16.3,31.1);
zb = 1.217*cmf(lam,437.0,11.8,36.0) + 0.681*cmf(lam,459.0,26.0,13.8);
M = [3.2406 -1.5372 -0.4986; -0.9689 1.8758 0.0415; 0.0557 -0.2040 1.0570];
srcs.LED = exp(-0.5*((lam-450)/12).^2) + 0.75*exp(-0.5*((lam-565)/55).^2);
srcs.flat = ones(size(lam));
% combos: name, theta0 (deg), [lR lG lB] nm, delta (deg)
CB = { 'A 13.7deg 630/532/460 d1',   13.7, [630 532 460], 1.0;
       'B 13.7deg d0.5',             13.7, [630 532 460], 0.5;
       'C 13.7deg d2',               13.7, [630 532 460], 2.0;
       'D 10deg d1',                 10.0, [630 532 460], 1.0;
       'E 17.5deg d1',               17.5, [630 532 460], 1.0;
       'F 21deg d1',                 21.0, [630 532 460], 1.0;
       'G 13.7deg 610/545/465 d1',   13.7, [610 545 465], 1.0;
       'H 13.7deg 640/530/450 d1',   13.7, [640 530 450], 1.0 };
dth = -3:1:3; res = struct();
rs = RandStream('mt19937ar', 'Seed', 7);
for ci = 1:size(CB, 1)
    th0 = CB{ci,2}; lc = CB{ci,3}*1e-6; del = CB{ci,4}; Lc = lc/sind(th0);
    S = zeros(Nv, Nu);
    for c = 1:3
        a = double(mask(:,:,c) | mask(:,:,4));
        o = a .* exp(2i*pi*rand(rs, Nv, Nu));
        Fo = fftshift(fft2(o)); band = (abs(fu) < sind(HU)/lc(c)) & (abs(fv) < sind(del)/lc(c));
        e = ifft2(ifftshift(Fo .* band)); e = e / prctile(abs(e(a > 0)), 99);
        S = S + conj(e) .* exp(2i*pi*vg/Lc(c));
    end
    S = S / prctile(abs(S(:)), 99.5);
    par = 2*mod(round(vg/P), 2) - 1;
    q = cos(angle(S) - par.*acos(min(abs(S), 1))) > 0;
    h = 2*double(q) - 1; un = rand(rs, Nv, Nu) < 0.15; h(un) = 2*(rand(rs, nnz(un), 1) > 0.5) - 1;
    F = fftshift(fft2(h));
    Isp = zeros(numel(lam), numel(dth), 5);                                % stripe / bg mean intensity
    for ti = 1:numel(dth)
        for li = 1:numel(lam)
            l = lam(li)*1e-6; f0 = sind(th0 + dth(ti))/l; r0 = sind(0.4)/l;
            pup = (fv - f0).^2 + fu.^2 < r0^2;
            I = abs(ifft2(ifftshift(F .* pup))).^2;
            for k = 1:4, Isp(li, ti, k) = mean(I(mask(:,:,k))); end
            Isp(li, ti, 5) = mean(I(bg));
        end
    end
    res(ci).name = CB{ci,1}; res(ci).Isp = Isp; res(ci).L = Lc*1e3; res(ci).k = Lc/P;
    fprintf('%s: carriers %.3f/%.3f/%.3f um (%.2f/%.2f/%.2f tracks)\n', CB{ci,1}, Lc*1e3, Lc/P);
end
save(fullfile(here, 'color_sim.mat'), 'res', 'lam', 'dth', 'CB');
%% render: perceived colour of each stripe vs viewing angle, both sources
for sn = {'LED', 'flat'}
    Sx = srcs.(sn{1}); W0 = M*[sum(Sx.*xb); sum(Sx.*yb); sum(Sx.*zb)];
    img = ones(numel(dth)*44 + 30, size(CB,1)*130, 3); fid = fopen(fullfile(here, ['color_metrics_' sn{1} '.txt']), 'w');
    fprintf(fid, 'source %s; per combo at design angle: stripe sRGB (linear, white-adapted), sat, W balance, contrast stripe/bg\n', sn{1});
    allmax = 0;
    for ci = 1:numel(res), allmax = max(allmax, max(res(ci).Isp(:,4,1:4), [], 'all')*1); end
    for ci = 1:numel(res)
        Isp = res(ci).Isp; C = zeros(numel(dth), 5, 3);
        for ti = 1:numel(dth)
            for k = 1:5
                sp = Isp(:, ti, k)' .* Sx;
                rgb = (M*[sum(sp.*xb); sum(sp.*yb); sum(sp.*zb)]) ./ W0;
                C(ti, k, :) = rgb;
            end
        end
        sc = max(C(:,1:4,:), [], 'all');
        for ti = 1:numel(dth)
            for k = 1:5
                rgb = max(squeeze(C(ti,k,:))/sc, 0).^(1/2.2);
                y0 = 30 + (ti-1)*44; x0 = (ci-1)*130 + 5 + (k-1)*24;
                img(y0+(1:38), x0+(1:22), :) = repmat(reshape(min(rgb,1), 1, 1, 3), 38, 22);
            end
        end
        c0 = squeeze(C(dth == 0, :, :));
        sat = @(x) (max(x) - min(x)) / max(max(x), eps);
        fprintf(fid, '%-28s R[%5.2f %5.2f %5.2f] G[%5.2f %5.2f %5.2f] B[%5.2f %5.2f %5.2f] W[%5.2f %5.2f %5.2f] sat R%.2f G%.2f B%.2f  Wsat %.2f  contrast %.0f\n', ...
            res(ci).name, c0(1,:)/sc, c0(2,:)/sc, c0(3,:)/sc, c0(4,:)/sc, sat(c0(1,:)), sat(c0(2,:)), sat(c0(3,:)), sat(c0(4,:)), ...
            sum(c0(4,:))/max(sum(c0(5,:)), eps));
    end
    fclose(fid);
    fg = figure('Visible', 'off', 'Position', [0 0 size(img,2) size(img,1)+40]); image(img); axis image off;
    for ci = 1:numel(res), text((ci-1)*130 + 5, 14, res(ci).name(1:min(end,18)), 'FontSize', 7, 'Interpreter', 'none'); end
    title(sprintf('%s source: rows = design angle %s deg; stripes R G B W bg', sn{1}, mat2str(dth)), 'FontSize', 8);
    exportgraphics(fg, fullfile(here, ['color_sim_' sn{1} '.png']), 'Resolution', 110); close(fg);
end
disp('done')
