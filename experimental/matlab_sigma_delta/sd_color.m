% sd_color.m -- 3-carrier colour case: double-phase vs band-pass sigma-delta with an IIR notch NTF
% NTF(z) = prod_c (1 - 2cos(w_c) z^-1 + z^-2) / (1 - 2 r cos(w_c) z^-1 + r^2 z^-2), error feedback along tracks.
P = 0.3199e-3; BY = 0.929e-3; HU = 8; del = 1; Lp = 0.6; lc = [630 532 460]*1e-6; th0 = 17.5;
Lc = lc/sind(th0); kc = Lc/P;
vg = (0:round(Lp/P)-1)'*P; ug = (0:round(Lp/BY)-1)*BY; Nv = numel(vg); Nu = numel(ug);
fv = ((0:Nv-1)' - floor(Nv/2))/(Nv*P); fu = ((0:Nu-1) - floor(Nu/2))/(Nu*BY);
[V, U] = ndgrid(vg, ug); a = double(V > 0.1 & V < 0.5 & U > 0.1 & U < 0.5);
rs = RandStream('mt19937ar', 'Seed', 2); S = zeros(Nv, Nu); E = cell(1,3);
for c = 1:3
    o = a .* exp(2i*pi*rand(rs, Nv, Nu)); band = abs(fu) < sind(HU)/lc(c) & abs(fv) < sind(del)/lc(c);
    E{c} = ifft2(ifftshift(fftshift(fft2(o)) .* band)); E{c} = E{c}/prctile(abs(E{c}(:)), 99);
    S = S + conj(E{c}) .* exp(2i*pi*vg/Lc(c));
end
S = S/prctile(abs(S(:)), 99.5); par = 2*mod(round(vg/P), 2) - 1;
fprintf('k = %.2f / %.2f / %.2f\n', kc);
fprintf('method            SNR per channel R G B (enc only)        with 2%% random            signal G\n');
cfg = {'DP', 0, 0; 'SD r=0.75 g=1.2', 0.75, 1.2; 'SD r=0.8 g=1.0', 0.8, 1.0; 'SD r=0.8 g=1.5', 0.8, 1.5; 'SD r=0.85 g=1.2', 0.85, 1.2; 'SD r=0.85 g=1.6', 0.85, 1.6; 'SD r=0.9 g=1.2', 0.9, 1.2; 'SD r=0.9 g=1.8', 0.9, 1.8};
for ci = 1:size(cfg, 1)
    if ci == 1
        h = 2*double(cos(angle(S) - par.*acos(min(abs(S), 1))) > 0) - 1; ideal = (2/pi)*2*real(S);
    else
        r = cfg{ci,2}; g = cfg{ci,3}; A = 1; B = 1;
        for c = 1:3, w = 2*pi/kc(c); A = conv(A, [1 -2*cos(w) 1]); B = conv(B, [1 -2*r*cos(w) r^2]); end
        HA = A - B;                                  % H = NTF - 1 = (A - B)/B
        x = g*real(S); M = numel(A) - 1; Eh = zeros(M, Nu); Wh = zeros(M, Nu); h = zeros(Nv, Nu);
        for t = 1:Nv
            w_t = HA(2:end)*Eh - B(2:end)*Wh;        % filtered past errors
            vv = x(t, :) + w_t; y = sign(vv); y(y == 0) = 1; h(t, :) = y;
            Eh = [y - vv; Eh(1:end-1, :)]; Wh = [w_t; Wh(1:end-1, :)];
        end
        ideal = x;
        f = linspace(0, pi, 2000); Nf = abs(polyval(A, exp(1i*f))./polyval(B, exp(1i*f)));
    end
    F = fftshift(fft2(h)); Fi = fftshift(fft2(ideal));
    h2 = h; un = rand(rs, Nv, Nu) < 0.02; h2(un) = 2*(rand(rs, nnz(un), 1) > 0.5) - 1; F2 = fftshift(fft2(h2));
    s1 = zeros(1,3); s2 = s1;
    for c = 1:3
        win = abs(fu) < sind(HU)/lc(c) & abs(fv - 1/Lc(c)) < sind(del)/lc(c);
        s1(c) = sum(abs(Fi(win)).^2)/sum(abs(F(win) - Fi(win)).^2); s2(c) = sum(abs(Fi(win)).^2)/sum(abs(F2(win) - Fi(win)).^2);
        if c == 2, sg = sum(abs(Fi(win)).^2)/1e12; end
    end
    extra = ''; if ci > 1, extra = sprintf('  max|NTF| %.2f', max(Nf)); end
    fprintf('%-17s %6.1f %6.1f %6.1f        %6.1f %6.1f %6.1f     %.4f%s\n', cfg{ci,1}, s1, s2, sg, extra);
end
