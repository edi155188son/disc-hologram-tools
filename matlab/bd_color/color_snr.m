% color_snr.m -- signal / background at the design angle: mono vs 3-channel, with/without random bytes
P = 0.3199e-3; BY = 0.929e-3; Lp = 1.2; HU = 8; del = 1; th0 = 13.7; lc = [630 532 460]*1e-6; Lc = lc/sind(th0);
vg = (0:round(Lp/P)-1)'*P; ug = (0:round(Lp/BY)-1)*BY; Nv = numel(vg); Nu = numel(ug);
fv = ((0:Nv-1)' - floor(Nv/2))/(Nv*P); fu = ((0:Nu-1) - floor(Nu/2))/(Nu*BY);
[Vm, Um] = ndgrid(vg, ug); sq = Vm > 0.4 & Vm < 0.8 & Um > 0.4 & Um < 0.8;
far = (Vm < 0.25 | Vm > 0.95) & Um > 0.1 & Um < 1.1;
rs = RandStream('mt19937ar', 'Seed', 3);
for nch = [1 3]
  for noise = [0.02 0.15]
    S = zeros(Nv, Nu);
    for c = (nch == 1)*2 + (nch == 3)*(1:3)
        if c == 0, continue; end
        o = double(sq) .* exp(2i*pi*rand(rs, Nv, Nu));
        band = (abs(fu) < sind(HU)/lc(c)) & (abs(fv) < sind(del)/lc(c));
        e = ifft2(ifftshift(fftshift(fft2(o)) .* band)); e = e / prctile(abs(e(sq)), 99);
        S = S + conj(e) .* exp(2i*pi*vg/Lc(c));
    end
    S = S / prctile(abs(S(:)), 99.5);
    par = 2*mod(round(vg/P), 2) - 1; h = 2*double(cos(angle(S) - par.*acos(min(abs(S), 1))) > 0) - 1;
    un = rand(rs, Nv, Nu) < noise; h(un) = 2*(rand(rs, nnz(un), 1) > 0.5) - 1;
    F = fftshift(fft2(h)); l = 532e-6; pup = (fv - sind(th0)/l).^2 + fu.^2 < (sind(0.4)/l)^2;
    I = abs(ifft2(ifftshift(F .* pup))).^2;
    fprintf('channels %d, random bytes %.0f%%: G-square %.3g, far bg %.3g, ratio %.0f\n', nch, noise*100, mean(I(sq)), mean(I(far)), mean(I(sq))/mean(I(far)));
  end
end
