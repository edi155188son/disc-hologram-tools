% ksweep.m -- double-phase encoding noise inside the +1 viewing window vs tracks-per-carrier-period k (2026-11-25)
% Single channel, random-phase square, band +-8 deg (u) x +-1 deg (v); no random bytes (encoding noise only).
P = 0.3199e-3; BY = 0.929e-3; lam = 532e-6; HU = 8; del = 1; Lp = 0.6;
vg = (0:round(Lp/P)-1)'*P; ug = (0:round(Lp/BY)-1)*BY; Nv = numel(vg); Nu = numel(ug);
fv = ((0:Nv-1)' - floor(Nv/2))/(Nv*P); fu = ((0:Nu-1) - floor(Nu/2))/(Nu*BY);
[V, U] = ndgrid(vg, ug); a = double(V > 0.1 & V < 0.5 & U > 0.1 & U < 0.5);
rs = RandStream('mt19937ar', 'Seed', 1);
o = a .* exp(2i*pi*rand(rs, Nv, Nu)); band = abs(fu) < sind(HU)/lam & abs(fv) < sind(del)/lam;
e = ifft2(ifftshift(fftshift(fft2(o)) .* band)); e = e/prctile(abs(e(:)), 99.5);
par = 2*mod(round(vg/P), 2) - 1;
ks = 4.5:0.05:16.5; snr = zeros(size(ks));
for i = 1:numel(ks)
    L = ks(i)*P; S = conj(e) .* exp(2i*pi*vg/L);
    h = 2*double(cos(angle(S) - par.*acos(min(abs(S), 1))) > 0) - 1;
    ideal = (2/pi)*2*real(S);                                   % +1 and -1 orders of an ideal linear encoder
    F = fftshift(fft2(h)); Fi = fftshift(fft2(ideal));
    win = abs(fu) < sind(HU)/lam & abs(fv - 1/L) < sind(del)/lam;
    snr(i) = sum(abs(Fi(win)).^2) / sum(abs(F(win) - Fi(win)).^2);
end
fg = figure('Visible', 'off', 'Position', [0 0 900 360]); semilogy(ks, snr, '-'); grid on;
xlabel('tracks per carrier period k'); ylabel('signal / encoding noise in +1 window');
xline(6:2:16, 'r:'); xline(5:2:15, 'g:');
title('double-phase on BD tracks: red = even k, green = odd k');
exportgraphics(fg, 'ksweep.png', 'Resolution', 100); close(fg);
kk = [8.32 7.02 6.07 8.05 7.19 6.14 11.34 9.58 8.28 6.55 5.53 4.78];
fprintf('%5.2f  %8.1f\n', [kk; interp1(ks, snr, kk)]); save('ksweep.mat','ks','snr');
