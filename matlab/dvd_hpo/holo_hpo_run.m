%% holo_hpo_run.m - four white-light HPO test blocks on the existing 10 mm lattices (w_lat_A..D)
heart = @(R,T,s,r0,t0) (((T-t0)/s).^2 + ((R-r0)/s - abs((T-t0)/s).^(2/3)).^2) < 1;   % filled heart
ring  = @(R,T,ro,ri,r0,t0) hypot(R-r0,T-t0) < ro & hypot(R-r0,T-t0) >= ri;
sq    = @(R,T,a,w,r0,t0) max(abs(R-r0),abs(T-t0)) < a & max(abs(R-r0),abs(T-t0)) >= a-w;
disk  = @(R,T,ro,r0,t0) hypot(R-r0,T-t0) < ro;

%% A: three depth layers, monochrome rainbow  (heart +5 in front, ring on disc, square outline -5 behind)
S = struct('fn',{}, 'Z',{}, 'lambda',{});
S(1).fn = @(R,T) heart(R,T,1.0,0.3,-2.4); S(1).Z =  5; S(1).lambda = 550;
S(2).fn = @(R,T) ring(R,T,1.3,0.95,0,0);   S(2).Z =  0; S(2).lambda = 550;
S(3).fn = @(R,T) sq(R,T,1.2,0.3,0,2.4);    S(3).Z = -5; S(3).lambda = 550;
holo_hpo('w_lat_A.bin','hpo_A.bin', S, struct('ALPHA',4.5,'SPREAD',1.5,'seed',1), 'hpo_A.png');

%% B: continuous depth - tilted ring (Z = 0.8*T, -2.4..+2.4 mm) + dot floating at +7 mm
S = struct('fn',{}, 'Z',{}, 'lambda',{});
nz = 16; te = linspace(-3.2, 3.2, nz+1);
for k = 1:nz
    tc = (te(k)+te(k+1))/2; t0 = te(k); t1 = te(k+1);
    S(end+1).fn = @(R,T) ring(R,T,3.2,2.7,0,0) & T >= t0 & T < t1; S(end).Z = 0.8*tc; S(end).lambda = 550; %#ok<SAGROW>
end
S(end+1).fn = @(R,T) disk(R,T,0.5,0,0); S(end).Z = 7; S(end).lambda = 550;
holo_hpo('w_lat_B.bin','hpo_B.bin', S, struct('ALPHA',4.5,'SPREAD',1.5,'seed',2), 'hpo_B.png');

%% C: colour test on the disc plane - red heart / green ring / blue square (narrow spread)
S = struct('fn',{}, 'Z',{}, 'lambda',{});
S(1).fn = @(R,T) heart(R,T,1.0,0.3,-2.4); S(1).Z = 0; S(1).lambda = 650;
S(2).fn = @(R,T) ring(R,T,1.3,0.9,0,0);    S(2).Z = 0; S(2).lambda = 550;
S(3).fn = @(R,T) sq(R,T,1.2,0.3,0,2.4);    S(3).Z = 0; S(3).lambda = 450;
holo_hpo('w_lat_C.bin','hpo_C.bin', S, struct('ALPHA',4.5,'SPREAD',0.5,'seed',3), 'hpo_C.png');

%% D: same scene as A but wide viewing zone (SPREAD 3 deg) and steeper carrier (6 deg)
S = struct('fn',{}, 'Z',{}, 'lambda',{});
S(1).fn = @(R,T) heart(R,T,1.0,0.3,-2.4); S(1).Z =  5; S(1).lambda = 550;
S(2).fn = @(R,T) ring(R,T,1.3,0.95,0,0);   S(2).Z =  0; S(2).lambda = 550;
S(3).fn = @(R,T) sq(R,T,1.2,0.3,0,2.4);    S(3).Z = -5; S(3).lambda = 550;
holo_hpo('w_lat_D.bin','hpo_D.bin', S, struct('ALPHA',6,'SPREAD',3,'seed',4), 'hpo_D.png');
