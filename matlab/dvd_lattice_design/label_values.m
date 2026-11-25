function label_values(LATTICE, VALUES, letter)
%LABEL_VALUES  5x7 dot-matrix letter (0.5 mm cells) in Q=92 on a pseudo-random background.
%   Letter "up" = radially outward, reading direction = +tangential (same frame as the F hologram).
font = struct('A',['01110';'10001';'10001';'11111';'10001';'10001';'10001'], ...
              'B',['11110';'10001';'10001';'11110';'10001';'10001';'11110'], ...
              'C',['01111';'10000';'10000';'10000';'10000';'10000';'01111'], ...
              'D',['11110';'10001';'10001';'10001';'10001';'10001';'11110'], ...
              'E',['11111';'10000';'10000';'11110';'10000';'10000';'11111']);
bm = font.(letter) == '1'; cell_ = 0.5;
L = read_lattice(LATTICE); n = numel(L.ur);
col = floor((L.ut + 2.5*cell_)/cell_) + 1; row = floor((3.5*cell_ - L.ur)/cell_) + 1;
in = col >= 1 & col <= 5 & row >= 1 & row <= 7;
stroke = false(n,1); stroke(in) = bm(sub2ind([7 5], row(in), col(in)));
rng(7); v = uint8(randi([0 255], n, 1)); v(stroke) = uint8(hex2dec('92'));
fid = fopen(VALUES,'w'); fwrite(fid, v, 'uint8'); fclose(fid);
fprintf('label %s: %d stroke codewords\n', letter, nnz(stroke & L.ctrl));
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
