// Fast 17PP encoder replicating DiscImageStudio.BluRay.Encoding.BluRay17PpEncoder exactly,
// with codeword boundaries exposed so encoding can restart mid-stream.
static class Enc
{
    // Encodes bits[from..to) (to-from even). Writes modulation bits into mod (length >= 1.5*(to-from)).
    // cwEnds receives data offsets (relative to from) at each codeword end. Returns count of codewords.
    public static int Encode(byte[] bits, int from, int to, int prev, byte[] mod, int[] cwEnds)
    {
        int src = from, outp = 0, n = 0;
        while (src < to)
        {
            int len, ch;
            int rem = to - src;
            if (rem >= 8 && Read(bits, src, 6) == 0b110111 && Begins010(bits, src + 6, to))
            { len = 6; ch = 0b001000000; }
            else Convert(bits, src, rem, prev, out len, out ch);
            int cl = len / 2 * 3;
            for (int b = cl - 1; b >= 0; b--) mod[outp++] = (byte)((ch >> b) & 1);
            prev = mod[outp - 1];
            src += len;
            cwEnds[n++] = src - from;
        }
        return n;
    }

    static bool Begins010(byte[] bits, int src, int to)
    {
        Convert(bits, src, to - src, 1, out int len, out int ch);
        return (ch >> (len / 2 * 3 - 3)) == 0b010;
    }

    static int Read(byte[] b, int s, int n) { int v = 0; for (int i = 0; i < n; i++) v = (v << 1) | b[s + i]; return v; }

    static void Convert(byte[] bits, int s, int rem, int prev, out int len, out int ch)
    {
        int p1 = Read(bits, s, 2);
        if (p1 == 1) { len = 2; ch = 0b010; return; }
        if (p1 == 2) { len = 2; ch = 0b001; return; }
        if (p1 == 3) { len = 2; ch = prev == 1 ? 0b000 : 0b101; return; }
        if (rem == 2) { len = 2; ch = 0; return; }
        int p2 = Read(bits, s + 2, 2);
        if (p2 == 1) { len = 4; ch = 0b000100; return; }
        if (p2 == 2) { len = 4; ch = 0b010000; return; }
        if (p2 == 3) { len = 4; ch = 0b010100; return; }
        if (rem == 4) { len = 4; ch = 0b010100; return; }
        int p3 = Read(bits, s + 4, 2);
        if (rem >= 8 && Read(bits, s + 6, 2) == 0)
        {
            if (p3 == 0) { len = 8; ch = 0b010100100100; return; }
            if (p3 == 2) { len = 8; ch = 0b000100100100; return; }
        }
        len = 6;
        ch = p3 switch { 0 => 0b010100000, 1 => 0b010100100, 2 => 0b000100000, _ => 0b000100100 };
    }
}
