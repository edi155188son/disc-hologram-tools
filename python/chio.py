import numpy as np, struct
def read_ch(path):
    """BDCH2: returns start sector, channel bits per RU, cluster indices (relative), packed levels [n, cb/8], ECC bytes [n, 76880]"""
    b = open(path,'rb').read(); assert b[:8]==b'BDCH2\0\0\0', path
    start, n, cb = struct.unpack_from('<IiI', b, 8); packed=(cb+7)//8; rec=packed+76880
    idx = np.frombuffer(b, '<i4', n, 20)
    a = np.frombuffer(b, np.uint8, offset=20+4*n).reshape(n, rec)
    return start, cb, idx, a[:, :packed], a[:, packed:]
