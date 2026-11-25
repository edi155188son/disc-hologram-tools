"""BD single-layer geometry and ECC byte layout, ported from DiscImageStudio.BluRay (constant-pitch model).
Global channel position G (channel bits from the start of the data zone, user offset 0):
  cluster = floor(G / CBPRU), byte centres inside a cluster from the ECC layout table."""
import math, numpy as np
CBPRU = 2760 + 496*1932 + 1104          # channel bits per recording unit (cluster)
BIT_MM = 74.5e-6
SECTORS = 12_219_392

def primitive(r, b):
    return (r*np.sqrt(r*r+b*b) + b*b*np.arcsinh(r/b)) / (2*b)

class Geometry:
    def __init__(self, inner, outer, sectors=SECTORS, start_angle_deg=0.0):
        self.inner, self.outer, self.sectors = inner, outer, sectors
        self.track_len = sectors/32*CBPRU*BIT_MM
        self.offset = math.radians(start_angle_deg % 360)
        lo, hi = 1e-15, 1.0
        while primitive(outer, hi) - primitive(inner, hi) > self.track_len: hi *= 2
        for _ in range(100):
            b = (lo+hi)/2
            if primitive(outer, b) - primitive(inner, b) > self.track_len: lo = b
            else: hi = b
        self.g = (lo+hi)/2; self.p0 = primitive(inner, self.g)
        self.pitch = 2*math.pi*self.g        # mm
    def at_sector(self, sp):             # same as BluRayTrackGeometry.AtSector (user offset 0)
        target = np.asarray(sp, float)/self.sectors*self.track_len
        return self.at_arc(target)
    def at_arc(self, target):
        r = np.sqrt(self.inner**2 + 2*self.g*target)
        for _ in range(2):
            err = primitive(r, self.g) - self.p0 - target
            r = np.clip(r - err*self.g/np.sqrt(r*r+self.g*self.g), self.inner, self.outer)
        return r, self.offset - (r - self.inner)/self.g
    # track n at polar angle phi (unwrapped consistently with BdAmp: theta(r) = (r-inner)/g = 2 pi n + offset - phi)
    def radius_of(self, n, phi):
        return self.inner + self.g*(2*np.pi*n + self.offset - phi)
    def channel_of(self, r):             # global channel position at radius r on the spiral
        return (primitive(r, self.g) - self.p0)/BIT_MM
    def track_index(self, x, y):
        r = np.hypot(x, y); return np.round(((r-self.inner)/self.g + np.arctan2(y, x) - self.offset)/(2*np.pi)).astype(np.int64)

def ecc_table():
    """76,880 ECC byte slots per cluster: centre channel offset, kind (0 payload, 1 EDC, 2 parity, 3 BIS) and indices.
    payload: sector*2048+idx ; parity: column*32+j ; BIS: row*3+group."""
    f = np.repeat(np.arange(496), 155); j = np.tile(np.arange(155), 496)
    mid = j*8 + 4; dc = np.where(mid < 25, 0, 1 + (mid - 25)//45)
    centre = 2760 + f*1932 + 30 + (mid + dc)*1.5
    grp, pos = j//39, j % 39
    bis = (pos == 38) & (grp < 3)
    ldccol = j - j//39
    r = f//2; c = 2*((ldccol + (3*r) % 152) % 152) + f % 2
    serial = c*216 + r; sec = serial//2052; idx = serial % 2052
    kind = np.where(bis, 3, np.where(r >= 216, 2, np.where(idx >= 2048, 1, 0)))
    index = np.where(kind == 0, sec*2048 + idx, np.where(kind == 2, c*32 + (r - 216), np.where(kind == 3, f*3 + grp, -1)))
    order = np.argsort(centre)
    return centre[order], kind[order].astype(np.int8), index[order].astype(np.int64)

if __name__ == '__main__':
    g = Geometry(23.987042157339335, 57.987960341783236)
    for sp in [0, 1234.5, 100000.25, 5000000.75, 12000000]:
        r, a = g.at_sector(sp); print('PY', sp, repr(float(r)), repr(float(a)), repr(float(r*np.cos(a))), repr(float(r*np.sin(a))))
    c, k, i = ecc_table(); print('table', len(c), np.bincount(k), c[:3], c[-1], 'unique', len(np.unique(c)))
    # consistency with DiscImageStudio payload channel offsets: payload byte (sector 0, idx 0) and (31, 2047)
    for s_, i_ in [(0, 0), (31, 2047), (5, 1000)]:
        print('payload', s_, i_, c[(k == 0) & (i == s_*2048+i_)])
