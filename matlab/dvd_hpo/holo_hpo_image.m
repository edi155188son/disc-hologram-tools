function holo_hpo_image(LATTICE, VALUES, IMG, OPT, PNG)
%HOLO_HPO_IMAGE  Picture (PNG/JPG, optional alpha) -> white-light HPO hologram via holo_hpo.
%   IMG: image file path, or a numeric matrix (gray 0..1 / 0..255, or RGB).  Row 1 = radially outward (top).
%   OPT fields (all optional):
%     width_mm  picture width on the disc (default 7, hologram window is 8 mm; height follows aspect)
%     center    [R T] mm of the picture centre (default [0 0])
%     color     'mono' (default, 550 nm) | 'rgb' (650/550/450 nm per channel)
%     gamma     display gamma used to linearise the picture (default 2.2); amplitude = sqrt(linear)
%     invert    true for dark-on-white line art (default false)
%     bg        'alpha' (use alpha channel if present, default) | 'white' | 'black' | 'none': pixels equal to that
%               background get amplitude 0 (after invert)
%     depth     [] flat | 'tilt' (Z grows left->right) | 'lum' (brighter = nearer) | depth-map file / matrix
%               (gray, 255 = nearest)
%     zrange    [Zmin Zmax] mm (default [-3 3]); nlayers depth levels (default 8); Z0 flat height (default 0)
%     ALPHA / SPREAD / seed / DR  passed to holo_hpo (defaults 4.5 deg / 1.0 deg / 7 / 0.01)
if nargin < 4 || isempty(OPT), OPT = struct; end
g = @(f,d) getf(OPT,f,d);
if ischar(IMG) || isstring(IMG)
    [I, ~, al] = imread(char(IMG));
else
    I = IMG; al = [];
end
I = double(I); if max(I(:)) > 1, I = I/255; end
if size(I,3) == 1, I = repmat(I,[1 1 3]); end
if ~isempty(al), al = double(al); if max(al(:)) > 1, al = al/255; end, end
gam = g('gamma',2.2); lin = I.^gam;
if g('invert',false), lin = 1 - lin; end
amp = sqrt(max(lin,0));                                   % field amplitude per channel
bg = g('bg','alpha'); mask = true(size(I,1), size(I,2));
switch lower(bg)
    case 'alpha', if ~isempty(al), mask = al > 0.5; end
    case 'white', mask = ~all(I > 0.97, 3);              % white pixels of the ORIGINAL are background
    case 'black', mask = ~all(I < 0.03, 3);
end
amp = amp .* mask; amp(amp < sqrt(g('floor',0.02))) = 0;
[h, w, ~] = size(amp);
W = g('width_mm',7); H = W*h/w; c = g('center',[0 0]);
if H > 7.8, warning('picture height %.1f mm exceeds the 8 mm window', H); end
ti = c(2) + ((1:w)-(w+1)/2)/w*W;                          % column -> tangential (mm)
ri = c(1) + ((h+1)/2-(1:h))/h*H;                          % row 1 -> radially outward (mm)
% depth index map
dep = g('depth',[]); nl = g('nlayers',8); zr = g('zrange',[-3 3]);
if isempty(dep)
    didx = ones(h,w); Zs = g('Z0',0);
else
    if ischar(dep) || isstring(dep)
        switch lower(char(dep))
            case 'tilt', d = repmat(linspace(0,1,w), h, 1);
            case 'lum',  d = mean(lin,3); d = d/max(d(:)+eps);
            otherwise,   d = double(imread(char(dep))); if size(d,3)>1, d = mean(d,3); end; d = d/max(d(:)+eps);
                         if any(size(d) ~= [h w]), d = interp2(d, linspace(1,size(d,2),w), linspace(1,size(d,1),h)'); end
        end
    else
        d = double(dep); d = d/max(d(:)+eps);
    end
    didx = min(max(round(d*(nl-1))+1, 1), nl); Zs = linspace(zr(1), zr(2), nl);
end
if strcmpi(g('color','mono'),'rgb'), lams = [650 550 450]; chans = 1:3; else, lams = 550; chans = 0; end
S = struct('fn',{}, 'Z',{}, 'lambda',{});
[RI, TI] = ndgrid(ri(end:-1:1), ti);                      % ascending grids for griddedInterpolant
for ch = chans
    if ch == 0, a = mean(amp,3); else, a = amp(:,:,ch); end
    Fa = griddedInterpolant(RI, TI, a(end:-1:1,:), 'linear', 'none');
    Fd = griddedInterpolant(RI, TI, didx(end:-1:1,:), 'nearest', 'none');
    for l = 1:numel(Zs)
        lit = any(amp,3); if ~any(didx(:) == l & lit(:)), continue; end
        S(end+1).fn = make_fn(Fa, Fd, l); %#ok<AGROW>
        S(end).Z = Zs(l); S(end).lambda = lams(max(ch,1));
    end
end
if ischar(IMG) || isstring(IMG), nm = char(IMG); else, nm = 'matrix'; end
fprintf('%s: %dx%d px -> %.1fx%.1f mm, %d scene layers (%s)\n', nm, w, h, W, H, numel(S), g('color','mono'));
o = struct('ALPHA',g('ALPHA',4.5),'SPREAD',g('SPREAD',1.0),'seed',g('seed',7),'DR',g('DR',0.01));
if nargin < 5, PNG = []; end
holo_hpo(LATTICE, VALUES, S, o, PNG);
end

function f = make_fn(Fa, Fd, l)
    f = @(R,T) fillz(Fa(R,T)) .* (fillz(Fd(R,T)) == l);
end
function v = fillz(v), v(isnan(v)) = 0; end
function v = getf(s, f, d)
    if isfield(s, f) && ~isempty(s.(f)), v = s.(f); else, v = d; end
end
