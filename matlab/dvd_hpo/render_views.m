function VIEWS = render_views(VFILE, FFILE, PHI_DEG, NPX, PNG)
%RENDER_VIEWS  Orthographic grey renders of a mesh from horizontal angles PHI_DEG (deg, + = camera to the right).
%   VFILE/FFILE: vertex (x y z) and 1-based face index text files. Object faces +z, up = +y.
%   NPX: image height in px (default 400). Returns cell of double images (0..1, black background), width
%   already stretched by 1/cos(phi) so columns map to where each ray crosses the z = 0 plane.
if nargin < 4 || isempty(NPX), NPX = 400; end
V = load(VFILE); F = load(FFILE);
V = V - mean([min(V); max(V)]); V = V / max(abs(V(:)));           % centre, unit box
fg = figure('Visible','off','Color','k','Position',[0 0 NPX NPX], 'InvertHardcopy','off');
ax = axes('Parent',fg,'Position',[0 0 1 1],'Color','k'); hold(ax,'on');
patch('Faces',F,'Vertices',V,'FaceColor',[0.85 0.85 0.85],'EdgeColor','none','FaceLighting','gouraud', ...
      'AmbientStrength',0.25,'DiffuseStrength',0.8,'SpecularStrength',0.3,'Parent',ax);
axis(ax,'off','equal'); camproj(ax,'orthographic'); camup(ax,[0 1 0]); camtarget(ax,[0 0 0]);
L = camlight(ax,'headlight');
VIEWS = cell(1, numel(PHI_DEG));
for k = 1:numel(PHI_DEG)
    p = deg2rad(PHI_DEG(k));
    campos(ax, 10*[sin(p) 0 cos(p)]); camva(ax, 2*atand(1.15/10)); camlight(L,'headlight');
    cd0 = print(fg, '-RGBImage', '-r0'); im = double(rgb2gray_(cd0))/255;
    [hh, ww] = size(im); r0 = floor((hh-NPX)/2); c0 = floor((ww-NPX)/2);
    im = im(max(r0,0)+1:min(max(r0,0)+NPX,hh), max(c0,0)+1:min(max(c0,0)+NPX,ww));
    % stretch width by 1/cos(phi): column = u (where the ray meets z=0), not the screen coordinate
    w = size(im,2); w2 = round(w/cos(p)); xi = linspace(1, w, w2);
    st = interp1((1:w)', im', xi', 'linear', 0)';                 % stretched, width w2 >= w
    o = floor((w2 - w)/2); VIEWS{k} = st(:, o+1:o+w);             % centre-crop back to w columns
end
close(fg);
if nargin >= 5 && ~isempty(PNG)
    f2 = figure('Visible','off','Position',[0 0 220*numel(VIEWS) 240]);
    for k = 1:numel(VIEWS), subplot(1,numel(VIEWS),k); imshow(VIEWS{k}); title(sprintf('%+.1f deg', PHI_DEG(k))); end
    exportgraphics(f2, PNG, 'Resolution', 80); close(f2);
end
end
function g = rgb2gray_(c), c = double(c); g = uint8(0.299*c(:,:,1) + 0.587*c(:,:,2) + 0.114*c(:,:,3)); end
