using DiscImageStudio.BluRay.Encoding;

// Beam-search frame designer: chooses data bits (incl. dc-control bits) so the 17PP/NRZI channel follows a target.
// Target: target(xGlobal, biasSign) -> desired level (0/1). biasSign for each dc block is set to oppose the running DSV.
public sealed class Beam
{
    public static bool IsDc(int i) => i >= 25 && (i - 25) % 46 == 0;
    public static int BlockOfData(int d) => d < 26 ? 0 : 1 + (d - 26) / 46;

    sealed class Node
    {
        public byte[] Data = new byte[1268];
        public int Len, SetData, SetCh; public double Score; public byte PrevMod, Level; public long Dsv;
        public double[] Bias = new double[29]; public double[] BStart = new double[29];
        public double Rank;
        public Node Clone() { var n = (Node)MemberwiseClone(); n.Data = (byte[])Data.Clone(); n.Bias = (double[])Bias.Clone(); n.BStart = (double[])BStart.Clone(); return n; }
    }

    public Func<long, double, int> Target;   // (global channel x, signed duty offset) -> level
    public double A = 12, GMin = 0.06, GMax = 0.25, Lambda = 0.05, Mu = 2, Margin = 10;
    double G(long d) { double g = Math.Clamp((Math.Abs(d) + A) / 138.0, GMin, GMax); return d > 0 ? -g : g; }
    public int Width = 16;
    public bool UseBias = true;

    // fixed: -1 free, 0/1 forced, per data bit (length 1268)
    public (byte[] ecc, byte[] dc) DesignFrame(int frameIndex, sbyte[] fixedBits, bool precedingTerm, byte initLevel, long initDsv, long frameBase)
    {
        int faw = frameIndex % 31;
        byte[] sync = BluRayRecordingFrameEncoder.CreateFrameSync(faw, precedingTerm);
        var root = new Node();
        byte lev = initLevel; long dsv = initDsv;
        foreach (var t in sync) { lev ^= t; dsv += lev == 0 ? -1 : 1; }
        root.Level = lev; root.PrevMod = sync[^1]; root.Dsv = dsv;
        root.Bias[0] = UseBias ? (initDsv > 0 ? -A : A) : double.NaN; root.BStart[0] = initDsv;
        var beam = new List<Node> { root };
        var mod = new byte[1268 * 2]; var cw = new int[1300]; var buf = new byte[1300];
        for (int pos = 0; pos < 1268; pos += 2)
        {
            var next = new List<Node>(beam.Count * 4);
            foreach (var nd in beam)
            {
                for (int v = 0; v < 4; v++)
                {
                    int b0 = v >> 1, b1 = v & 1;
                    if (fixedBits[pos] >= 0 && fixedBits[pos] != b0) continue;
                    if (fixedBits[pos + 1] >= 0 && fixedBits[pos + 1] != b1) continue;
                    var c = nd.Clone();
                    c.Data[pos] = (byte)b0; c.Data[pos + 1] = (byte)b1; c.Len = pos + 2;
                    Advance(c, mod, cw, buf, frameBase);
                    next.Add(c);
                }
            }
            next.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            // dedupe identical recent tails to keep diversity
            beam = new List<Node>(Width);
            var seen = new HashSet<long>();
            foreach (var c in next)
            {
                long key = c.SetData * 1000003L + c.Level * 7 + c.PrevMod * 13 + (long)c.Rank * 31;
                for (int i = Math.Max(c.SetData, 0); i < c.Len; i++) key = key * 3 + c.Data[i];
                if (!seen.Add(key)) continue;
                beam.Add(c); if (beam.Count >= Width) break;
            }
        }
        var best = beam[0];
        byte[] ecc = new byte[155]; byte[] dcb = new byte[28];
        int bit = 0;
        for (int i = 0; i < 1268; i++)
        {
            if (IsDc(i)) dcb[(i - 25) / 46] = best.Data[i];
            else { if (best.Data[i] == 1) ecc[bit / 8] |= (byte)(0x80 >> (bit % 8)); bit++; }
        }
        return (ecc, dcb);
    }

    static readonly byte[] Pad = { 0, 1, 0, 1, 0, 1, 0, 1 };

    void Advance(Node c, byte[] mod, int[] cw, byte[] buf, long frameBase)
    {
        bool final = c.Len == 1268;
        int n = c.Len - c.SetData;
        Array.Copy(c.Data, c.SetData, buf, 0, n);
        int encLen = n;
        if (!final) { Array.Copy(Pad, 0, buf, n, 8); encLen += 8; }
        int ncw = Enc.Encode(buf, 0, encLen, c.PrevMod, mod, cw);
        int settle = 0;
        for (int i = 0; i < ncw; i++) { int e = cw[i]; if (final ? e <= n : e <= n - 14) settle = e; else break; }
        int chN = n * 3 / 2, chSettle = settle * 3 / 2;
        byte lev = c.Level; long dsv = c.Dsv; double score = c.Score; double prov = 0;
        byte setLev = lev; long setDsv = dsv; double setScore = score; byte setPrev = c.PrevMod;
        for (int i = 0; i < chN; i++)
        {
            lev ^= mod[i];
            int chFrame = c.SetCh + i;           // frame-relative channel index (excluding sync)
            int dIdx = chFrame * 2 / 3;
            // dc bias update: when reaching the channel start of dc bit k, set bias of block k+1
            if (UseBias && IsDc(dIdx) && (chFrame - 1) * 2 / 3 != dIdx)
            {
                int k = (dIdx - 25) / 46;
                double m = Math.Abs(2 * c.BStart[k] - dsv) - Math.Abs(dsv);
                double pen = (k > 0 ? Mu * Math.Max(0, Margin - m) : 0) + Mu * Math.Max(0, Margin / 2 + 2 - Math.Abs(dsv));
                if (i < chSettle) score -= pen; else prov -= pen;
                c.Bias[k + 1] = dsv > 0 ? -A : A; c.BStart[k + 1] = dsv;
            }
            int blk = BlockOfData(dIdx);
            int tgt = Target(frameBase + 30 + chFrame, 0);
            double gain = lev == tgt ? 1 : 0;
            dsv += lev == 0 ? -1 : 1;
            if (UseBias)
            {
                int bStartCh = blk == 0 ? -30 : (int)(1.5 * (26 + 46 * (blk - 1)));
                int bLen = 69;
                double prog = Math.Min(1.0, (chFrame - bStartCh + 1) / (double)bLen);
                double want = c.BStart[blk] + (c.Bias[blk] - c.BStart[blk]) * prog;
                gain -= Lambda * Math.Abs(dsv - want);
            }
            if (i < chSettle) score += gain; else prov += gain;
            if (i == chSettle - 1) { setLev = lev; setDsv = dsv; setScore = score; setPrev = mod[i]; }
        }
        if (chSettle > 0)
        {
            c.Level = setLev; c.Dsv = setDsv; c.Score = setScore; c.PrevMod = setPrev;
            c.SetData += settle; c.SetCh += chSettle;
        }
        c.Rank = score + prov;
    }
}
