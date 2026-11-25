% sd_test.m -- double-phase vs band-pass sigma-delta (error feedback along the radial track sequence) (2026-11-25)
% NTF has zeros at the carrier frequency -> binarization noise is pushed away from the +1 viewing window.
P = 0.3199e-3; BY = 0.929e-3; lam = 532e-6; HU = 8; del = 1; Lp = 0.6;
vg = (0:round(Lp/P)-1)'*P; ug = (0:round(Lp/BY)-1)*BY; Nv = numel(vg); Nu = numel(ug);
fv = ((0:Nv-1)' - floor(Nv/2))/(Nv*P); fu = ((0:Nu-1) - floor(Nu/2))/(Nu*BY);
[V, U] = ndgrid(vg, ug); a = double(V > 0.1 & V < 0.5 & U > 0.1 & U < 0.5);
rs = RandStream('mt19937ar', 'Seed', 1);
o = a .* exp(2i*pi*rand(rs, Nv, Nu)); band = abs(fu) < sind(HU)/lam & abs(fv) < sind(del)/lam;
e = ifft2(ifftshift(fftshift(fft2(o)) .* band)); e = e/prctile(abs(e(:)), 99.5);
par = 2*mod(round(vg/P), 2) - 1; kb = ones(round(0.05/P), round(0.05/BY)); kb = kb/numel(kb);
fprintf('   k   method        window SNR (enc only)  (2%% random)  photo std\n');
for k = [5.53 6.55 9.6]
    L = k*P; S = conj(e) .* exp(2i*pi*vg/L); w0 = 2*pi/k;
    win = abs(fu) < sind(HU)/lam & abs(fv - 1/L) < sind(del)/lam;
    meth = {'DP', 'SD2_0.8', 'SD2_1.0', 'SD2_1.2', 'SD2_1.5'};
    for mi = 1:numel(meth)
        switch meth{mi}
            case 'DP'
                h = 2*double(cos(angle(S) - par.*acos(min(abs(S), 1))) > 0) - 1; ideal = (2/pi)*2*real(S);
            otherwise
                c = [1 -2*cos(w0) 1]; g = str2double(meth{mi}(5:end));
                x = g*real(S);  % target transmittance (+1 and -1 orders), |x| <= g
                hcoef = c(2:end); M = numel(hcoef); E = zeros(M, Nu); h = zeros(Nv, Nu);
                for t = 1:Nv
                    vv = x(t, :) + hcoef*E;        % error feedback: NTF(z) = 1 + sum hcoef z^-j
                    y = sign(vv); y(y == 0) = 1; h(t, :) = y;
                    E = [y - vv; E(1:end-1, :)];
                end
                ideal = x;
        end
        F = fftshift(fft2(h)); Fi = fftshift(fft2(ideal));
        snr = sum(abs(Fi(win)).^2) / sum(abs(F(win) - Fi(win)).^2);
        h2 = h; un = rand(rs, Nv, Nu) < 0.02; h2(un) = 2*(rand(rs, nnz(un), 1) > 0.5) - 1;
        F2 = fftshift(fft2(h2)); snr2 = sum(abs(Fi(win)).^2) / sum(abs(F2(win) - Fi(win)).^2);
        R0 = conv2((h + 1)/2, kb, 'valid');
        fprintf('%5.2f  %-8s  %8.1f  %10.1f   %6.3f   signal %.3f\n', k, meth{mi}, snr, snr2, std(R0(:)), sum(abs(Fi(win)).^2)/1e12);
    end
end
