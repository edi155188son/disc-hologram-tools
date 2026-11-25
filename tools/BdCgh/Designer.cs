using DiscImageStudio.BluRay.Encoding;

/// <summary>
/// Beam-search design of one BD recording frame (1268 data bits incl. 28 dc-control bits).
/// Maximises sum (2*level-1)*gain over the 1902 modulated channel bits while steering the running
/// DSV so that every dc-control decision taken by a one-block-lookahead DSV-minimising encoder
/// equals the designed one with a margin (see claude/... BD notes). dc-control bits are forced 0.
/// </summary>
public sealed class Designer
{
    public static bool IsDc(int i) => i >= 25 && (i - 25) % 46 == 0;
    static int BlockOfData(int d) => d < 26 ? 0 : 1 + (d - 26) / 46;

    public int Width = 8;
    public double A = 12, Lambda = 0.05, Mu = 2, Margin = 10;

    struct Node
    {
        public int Parent, Len, SetData, SetCh; public byte Bits, PrevMod, Level; public long Dsv; public double Score, Rank;
        public double BTarget, BStart;   // current block's DSV target and start (as of the settled point)
        public long Key;
    }

    Node[] _arena = new Node[1 << 16];
    int _n;
    readonly byte[] _buf = new byte[64], _mod = new byte[128];
    readonly int[] _cw = new int[64];
    static readonly byte[] Pad = { 0, 1, 0, 1, 0, 1, 0, 1 };

    int Add(in Node nd) { if (_n == _arena.Length) Array.Resize(ref _arena, _n * 2); _arena[_n] = nd; return _n++; }

    /// <param name="gain">1902 per-channel-bit gains (after sync); +g wants level 1.</param>
    /// <param name="fixedBits">1268 entries: -1 free, 0/1 forced.</param>
    public (byte[] Ecc, byte[] Dc) Design(float[] gain, sbyte[] fixedBits, int faw, bool precedingTerm, byte initLevel, long initDsv, bool steer)
    {
        byte[] sync = BluRayRecordingFrameEncoder.CreateFrameSync(faw, precedingTerm);
        byte lev = initLevel; long dsv = initDsv;
        foreach (var t in sync) { lev ^= t; dsv += lev == 0 ? -1 : 1; }
        _n = 0;
        var root = new Node { Parent = -1, Level = lev, PrevMod = sync[^1], Dsv = dsv, BStart = initDsv, BTarget = initDsv > 0 ? -A : A };
        var beam = new List<int> { Add(root) };
        var cand = new List<int>(Width * 4);
        var bits = new byte[1268];
        var seen = new HashSet<long>();
        for (int pos = 0; pos < 1268; pos += 2)
        {
            cand.Clear();
            foreach (int pi in beam)
            {
                for (int v = 0; v < 4; v++)
                {
                    int b0 = v >> 1, b1 = v & 1;
                    if (fixedBits[pos] >= 0 && fixedBits[pos] != b0) continue;
                    if (fixedBits[pos + 1] >= 0 && fixedBits[pos + 1] != b1) continue;
                    Node c = _arena[pi];
                    c.Parent = pi; c.Bits = (byte)v; c.Len = pos + 2;
                    Advance(ref c, gain, steer);
                    cand.Add(Add(c));
                }
            }
            cand.Sort((a, b) => _arena[b].Rank.CompareTo(_arena[a].Rank));
            beam.Clear(); seen.Clear();
            foreach (int ci in cand)
            {
                if (!seen.Add(_arena[ci].Key)) continue;
                beam.Add(ci); if (beam.Count >= Width) break;
            }
        }
        // reconstruct
        int idx = beam[0];
        for (int pos = 1266; pos >= 0; pos -= 2) { ref Node nd = ref _arena[idx]; bits[pos] = (byte)(nd.Bits >> 1); bits[pos + 1] = (byte)(nd.Bits & 1); idx = nd.Parent; }
        byte[] ecc = new byte[155], dc = new byte[28];
        int bit = 0;
        for (int i = 0; i < 1268; i++)
        {
            if (IsDc(i)) dc[(i - 25) / 46] = bits[i];
            else { if (bits[i] == 1) ecc[bit / 8] |= (byte)(0x80 >> (bit % 8)); bit++; }
        }
        return (ecc, dc);
    }

    void Advance(ref Node c, float[] gain, bool steer)
    {
        bool final = c.Len == 1268;
        int n = c.Len - c.SetData;
        // gather bits SetData..Len by walking parents (each node holds 2 bits)
        int idx = -1; // c is not yet in arena; its own bits are c.Bits
        _buf[n - 2] = (byte)(c.Bits >> 1); _buf[n - 1] = (byte)(c.Bits & 1);
        idx = c.Parent;
        for (int p = n - 4; p >= 0; p -= 2) { ref Node a = ref _arena[idx]; _buf[p] = (byte)(a.Bits >> 1); _buf[p + 1] = (byte)(a.Bits & 1); idx = a.Parent; }
        int encLen = n;
        if (!final) { Buffer.BlockCopy(Pad, 0, _buf, n, 8); encLen += 8; }
        int ncw = Enc.Encode(_buf, 0, encLen, c.PrevMod, _mod, _cw);
        int settle = 0;
        for (int i = 0; i < ncw; i++) { int e = _cw[i]; if (final ? e <= n : e <= n - 14) settle = e; else break; }
        int chN = n * 3 / 2, chSettle = settle * 3 / 2;
        byte lev = c.Level; long dsv = c.Dsv; double score = c.Score, prov = 0;
        double bT = c.BTarget, bS = c.BStart;
        byte setLev = lev; long setDsv = dsv; double setScore = score; byte setPrev = c.PrevMod; double setBT = bT, setBS = bS;
        for (int i = 0; i < chN; i++)
        {
            lev ^= _mod[i];
            int ch = c.SetCh + i;
            int d = ch * 2 / 3;
            double g = 0;
            if (steer && IsDc(d) && (ch - 1) * 2 / 3 != d)
            {
                int k = (d - 25) / 46;
                double m = Math.Abs(2 * bS - dsv) - Math.Abs(dsv);
                g -= (k > 0 ? Mu * Math.Max(0, Margin - m) : 0) + Mu * Math.Max(0, Margin / 2 + 2 - Math.Abs(dsv));
                bT = dsv > 0 ? -A : A; bS = dsv;
            }
            float gg = gain[ch];
            g += lev == 1 ? gg : -gg;
            dsv += lev == 0 ? -1 : 1;
            if (steer)
            {
                int blk = BlockOfData(d);
                int bStartCh = blk == 0 ? -30 : (int)(1.5 * (26 + 46 * (blk - 1)));
                double prog = Math.Min(1.0, (ch - bStartCh + 1) / 69.0);
                g -= Lambda * Math.Abs(dsv - (bS + (bT - bS) * prog));
            }
            if (i < chSettle) score += g; else prov += g;
            if (i == chSettle - 1) { setLev = lev; setDsv = dsv; setScore = score; setPrev = _mod[i]; setBT = bT; setBS = bS; }
        }
        if (chSettle > 0)
        {
            c.Level = setLev; c.Dsv = setDsv; c.Score = setScore; c.PrevMod = setPrev; c.BTarget = setBT; c.BStart = setBS;
            c.SetData += settle; c.SetCh += chSettle;
        }
        c.Rank = score + prov;
        long key = c.SetData * 1000003L + c.Level * 7 + c.PrevMod * 13 + (long)(c.Dsv * 31);
        for (int i = settle; i < n; i++) key = key * 3 + _buf[i];
        c.Key = key;
    }
}
