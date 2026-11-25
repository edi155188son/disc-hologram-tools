using DiscImageStudio.BluRay.Encoding;

// Experiment: design data so the 17PP/NRZI output follows a target grating, with a per-block DSV bias
// that forces the DC-control choice. Verify with several independent DC-control policies.
if (args.Contains("--enctest")) { EncTest.Run(); return; }
double period = Arg("--period", 12);      // channel bits
double beta = Arg("--beta", 0.26);        // bias threshold
int frames = (int)Arg("--frames", 60);
int seed = (int)Arg("--seed", 1);
bool noBias = args.Contains("--nobias");
var rnd = new Random(seed);

// Global channel index of frame f bit 0 = f*1932.
byte[][] ecc = new byte[frames][];
byte[][] dcDesign = new byte[frames][];
long dsv = 0; byte level = 0; bool term = false;
var sw = System.Diagnostics.Stopwatch.StartNew();
double gamma = beta; int dcForce = (int)Arg("--dcforce", -1);
var beamer = new Beam { Width = (int)Arg("--width", 16), UseBias = !noBias,
    A = Arg("--A", 12), Lambda = Arg("--lambda", 0.05), Mu = Arg("--mu", 2), Margin = Arg("--margin", 10), Target = (x, s) => { double fr = x / period; fr -= Math.Floor(fr); double duty = 0.5 + s; return Math.Min(fr, 1 - fr) < duty / 2 ? 1 : 0; } };
for (int f = 0; f < frames; f++)
{
    var fixedBits = new sbyte[1268]; Array.Fill(fixedBits, (sbyte)-1);
    // BIS bytes at ecc columns 38,77,116 fixed random
    int bit = 0;
    var bisCols = new[] { 38, 77, 116 }; var bisVal = bisCols.Select(_ => rnd.Next(256)).ToArray();
    for (int i = 0; i < 1268; i++)
    {
        if (Beam.IsDc(i)) { if (dcForce >= 0) fixedBits[i] = (sbyte)dcForce; continue; }
        int col = bit / 8, j = Array.IndexOf(bisCols, col);
        if (j >= 0) fixedBits[i] = (sbyte)((bisVal[j] >> (7 - bit % 8)) & 1);
        bit++;
    }
    (ecc[f], dcDesign[f]) = beamer.DesignFrame(f, fixedBits, term, level, dsv, (long)f * 1932);
    var res = BluRayRecordingFrameEncoder.Encode(ecc[f], f % 31, dcDesign[f], term, level);
    term = res.UsedTermination; level = res.FinalChannelLevel; dsv += res.DigitalSum;
}
Console.WriteLine($"design {frames} frames in {sw.Elapsed.TotalSeconds:F1}s");

if (args.Contains("--dump")) { Dump.Run(ecc[0], dcDesign[0], period); return; }
if (args.Contains("--diag")) { Diag.Run(ecc, dcDesign); return; }
// Reference: design DC bits.
var design = Policies.EncodeStream(ecc, (f, e, n, t, l, s) => dcDesign[f]);
Report("design", design, design, period);
if (args.Contains("--marg")) { Marg.Run(ecc, dcDesign); return; }
if (args.Contains("--k27")) { K27.Run(ecc, dcDesign); return; }
if (args.Contains("--mis")) { var g = Policies.EncodeStream(ecc, Policies.All[0].Item2); Mis.Run(ecc, dcDesign, g.Dc); return; }
foreach (var (name, pol) in Policies.All)
{
    foreach (long d0 in (args.Contains("--sweep") ? Enumerable.Range(-8, 17).Select(i => (long)(i * 3)).ToArray() : new long[] { 0, 37, -51 }))
    {
        var r = Policies.EncodeStream(ecc, pol, d0);
        Report($"{name} dsv0={d0}", r, design, period);
    }
}

double Arg(string n, double d) { int i = Array.IndexOf(args, n); return i >= 0 ? double.Parse(args[i + 1]) : d; }

static void Report(string name, Policies.Stream r, Policies.Stream design, double period)
{
    int n = r.Levels.Length, same = 0, sameInv = 0;
    for (int i = 0; i < n; i++) { if (r.Levels[i] == design.Levels[i]) same++; else sameInv++; }
    int dcAgree = 0, dcTot = 0;
    for (int f = 0; f < r.Dc.Length; f++) for (int k = 0; k < 28; k++) { dcTot++; if (r.Dc[f][k] == design.Dc[f][k]) dcAgree++; }
    // grating coherence: complex amplitude at 1/period over whole stream vs per-block incoherent sum
    System.Numerics.Complex tot = 0; double inc = 0; int blk = 69;
    for (int s = 0; s + blk <= n; s += blk)
    {
        System.Numerics.Complex c = 0;
        for (int i = s; i < s + blk; i++) c += (r.Levels[i] * 2 - 1) * System.Numerics.Complex.FromPolarCoordinates(1, -2 * Math.PI * i / period);
        tot += c; inc += c.Magnitude;
    }
    // windowed coherence over ~0.2 mm windows (2684 ch bits), and polarity segment count
    double wc = 0; int wn = 0; int W = 2684;
    for (int s = 0; s + W <= n; s += W)
    {
        System.Numerics.Complex c = 0; double ic = 0;
        for (int b0 = s; b0 + blk <= s + W; b0 += blk)
        {
            System.Numerics.Complex cb = 0;
            for (int i = b0; i < b0 + blk; i++) cb += (r.Levels[i] * 2 - 1) * System.Numerics.Complex.FromPolarCoordinates(1, -2 * Math.PI * i / period);
            c += cb; ic += cb.Magnitude;
        }
        wc += c.Magnitude / ic; wn++;
    }
    int flips = 0; int prevSame = -1;
    for (int b0 = 0; b0 + blk <= n; b0 += blk)
    {
        int sm = 0; for (int i = b0; i < b0 + blk; i++) if (r.Levels[i] == design.Levels[i]) sm++;
        int cur = sm * 2 > blk ? 1 : 0; if (prevSame >= 0 && cur != prevSame) flips++; prevSame = cur;
    }
    Console.Write($"win0.2mm {wc / wn:F3} flips/mm {flips / (n * 74.5e-6):F1}  ");
    Console.WriteLine($"{name,-26} levels same {100.0 * same / n,6:F2}%  dc agree {100.0 * dcAgree / dcTot,6:F2}%  coherence {tot.Magnitude / inc:F3}  amp/bit {tot.Magnitude / n:F3}  phase {tot.Phase * 180 / Math.PI:F0}");
}

static class Designer
{
    // DC control positions in the 1268-bit frame data stream.
    public static bool IsDc(int i) => i >= 25 && (i - 25) % 46 == 0;

    public static (byte[] ecc, byte[] dc, BluRayRecordingFrameResult res) DesignFrame(int frameIndex, byte[] bis,
        bool precedingTerm, byte initLevel, long initDsv, double period, double beta)
    {
        int faw = frameIndex % 31;
        byte[] sync = BluRayRecordingFrameEncoder.CreateFrameSync(faw, precedingTerm);
        byte[] syncLevels = BluRay17PpEncoder.ToChannelLevels(sync, initLevel);
        long dsvAfterSync = initDsv + syncLevels.Sum(l => l == 0 ? -1 : 1);
        byte[] data = new byte[1268];
        byte[] ecc = new byte[155];
        byte[] dc = new byte[28];
        int pos = 0; // data bits committed
        double bias = 0; // per-block threshold sign*beta
        // initial bias from DSV after sync
        bias = beta * (dsvAfterSync > 0 ? 1 : -1); // positive threshold -> fewer high levels
        long frameBase = (long)frameIndex * 1932;
        for (int b = 0; b < 155; b++)
        {
            bool isBis = b == 38 || b == 77 || b == 116;
            // bits for this byte occupy the next 8 non-dc positions; dc bits may be interleaved
            int start = pos;
            int bitsNeeded = 8, end = start; int dcIdx = -1;
            while (bitsNeeded > 0) { if (IsDc(end)) dcIdx = end; else bitsNeeded--; end++; }
            if (end < 1268 && IsDc(end)) { dcIdx = end; end++; } // decide dc right after byte too
            int bestScore = int.MinValue; int bestV = 0, bestD = 0;
            int vMin = isBis ? bis[b == 38 ? 0 : b == 77 ? 1 : 2] : 0, vMax = isBis ? vMin : 255;
            for (int v = vMin; v <= vMax; v++)
            for (int d = 0; d <= (dcIdx >= 0 ? 1 : 0); d++)
            {
                Fill(data, start, end, v, d);
                int score = Score(data, end, sync, initLevel, frameBase, period, ref bias, beta, dsvAfterSync, false);
                if (score > bestScore) { bestScore = score; bestV = v; bestD = d; }
            }
            Fill(data, start, end, bestV, bestD);
            ecc[b] = (byte)bestV;
            if (dcIdx >= 0) dc[(dcIdx - 25) / 46] = (byte)bestD;
            pos = end;
            // update bias when we've just passed a dc position: choose next block's bias opposing DSV at the dc point
            if (dcIdx >= 0) Score(data, end, sync, initLevel, frameBase, period, ref bias, beta, dsvAfterSync, true);
        }
        var res = BluRayRecordingFrameEncoder.Encode(ecc, faw, dc, precedingTerm, initLevel);
        return (ecc, dc, res);
    }

    static void Fill(byte[] data, int start, int end, int v, int d)
    {
        int k = 7;
        for (int i = start; i < end; i++)
            data[i] = IsDc(i) ? (byte)d : (byte)((v >> k--) & 1);
    }

    static readonly byte[] Pad = { 0, 1, 0, 1, 0, 1, 0, 1 };

    // Encode prefix data[0..end) + pad, score new channel bits against target. When updateBias, set bias for block after last dc.
    static int Score(byte[] data, int end, byte[] sync, byte initLevel, long frameBase, double period,
        ref double bias, double beta, long dsvAfterSync, bool updateBias)
    {
        int n = end + (end % 2) + 8;
        byte[] buf = new byte[n];
        Array.Copy(data, buf, end);
        if (end % 2 == 1) buf[end] = 0;
        Array.Copy(Pad, 0, buf, end + (end % 2), 8);
        var enc = BluRay17PpEncoder.Encode(buf, sync[^1]);
        // levels
        byte lev = initLevel;
        foreach (var t in sync) lev ^= t;
        int chEnd = (int)(1.5 * end);
        int chStart = Math.Max(0, chEnd - 18);
        int score = 0;
        long dsv = dsvAfterSync;
        for (int i = 0; i < chEnd; i++)
        {
            lev ^= enc.ModulationBits[i];
            if (i >= chStart)
            {
                double x = frameBase + 30 + i;
                int blk = BlockOfChannel(i);
                double thr = BiasFor(blk, ref bias);
                double fr = x / period; fr -= Math.Floor(fr); double duty = 0.5 - thr; int target = Math.Min(fr, 1 - fr) < duty / 2 ? 1 : 0;
                if (lev == target) score++;
            }
            dsv += lev == 0 ? -1 : 1;
        }
        if (updateBias) bias = beta * (dsv > 0 ? 1 : -1);
        return score;
    }

    static int BlockOfChannel(int ch) { int d = (int)(ch / 1.5); return d < 26 ? 0 : 1 + (d - 26) / 46; }
    static double BiasFor(int blk, ref double bias) => bias;
}

static class Policies
{
    public delegate byte[] Policy(int f, byte[] ecc, byte[] next, bool term, byte level, long dsv);
    public record Stream(byte[] Levels, byte[][] Dc);

    public static Stream EncodeStream(byte[][] ecc, Policy pol, long dsv0 = 0)
    {
        var levels = new List<byte>(); var dcs = new byte[ecc.Length][];
        bool term = false; byte level = 0; long dsv = dsv0;
        for (int f = 0; f < ecc.Length; f++)
        {
            byte[] next = f + 1 < ecc.Length ? ecc[f + 1] : null;
            dcs[f] = pol(f, ecc[f], next, term, level, dsv);
            var r = BluRayRecordingFrameEncoder.Encode(ecc[f], f % 31, dcs[f], term, level);
            levels.AddRange(r.ChannelLevels);
            term = r.UsedTermination; level = r.FinalChannelLevel; dsv += r.DigitalSum;
        }
        return new(levels.ToArray(), dcs);
    }

    public static (string, Policy)[] All => new (string, Policy)[]
    {
        ("dis-greedy", (f, e, n, t, l, s) => BluRayGreedyDcControlEncoder.Encode(e, f % 31, n ?? default, t, l, s).DcControlBits),
        ("end-next", (f, e, n, t, l, s) => Generic(f, e, n, t, l, s, 1, Metric.End)),
        ("end-next2", (f, e, n, t, l, s) => Generic(f, e, n, t, l, s, 2, Metric.End)),
        ("peak-next", (f, e, n, t, l, s) => Generic(f, e, n, t, l, s, 1, Metric.Peak)),
        ("sq-next", (f, e, n, t, l, s) => Generic(f, e, n, t, l, s, 1, Metric.Sq)),
        ("tie1-end", (f, e, n, t, l, s) => Generic(f, e, n, t, l, s, 1, Metric.End, true)),
    };

    enum Metric { End, Peak, Sq }

    static byte[] Generic(int f, byte[] ecc, byte[] next, bool term, byte level, long dsv0, int blocks, Metric m, bool tieOne = false)
    {
        var dc = new byte[28];
        for (int k = 0; k < 28; k++)
        {
            long[] sc = new long[2];
            for (int v = 0; v < 2; v++)
            {
                dc[k] = (byte)v;
                var r = BluRayRecordingFrameEncoder.Encode(ecc, f % 31, dc, term, level);
                var lv = new List<byte>(r.ChannelLevels);
                if (next != null)
                {
                    var rn = BluRayRecordingFrameEncoder.Encode(next, (f + 1) % 31, new byte[28], r.UsedTermination, r.FinalChannelLevel);
                    lv.AddRange(rn.ChannelLevels);
                }
                int dcData = 25 + 46 * k;
                int chFrom = 30 + (int)(1.5 * dcData);
                int chTo = Math.Min(lv.Count, 30 + (int)(1.5 * (dcData + 1 + 46 * blocks)) + (k == 27 ? 30 : 0));
                long s = dsv0, peak = 0, sq = 0;
                for (int i = 0; i < chTo; i++)
                {
                    s += lv[i] == 0 ? -1 : 1;
                    if (i >= chFrom) { peak = Math.Max(peak, Math.Abs(s)); sq += s * s; }
                }
                sc[v] = m switch { Metric.End => Math.Abs(s), Metric.Peak => peak, _ => sq };
            }
            dc[k] = (byte)(tieOne ? (sc[1] <= sc[0] ? 1 : 0) : (sc[1] < sc[0] ? 1 : 0));
        }
        return dc;
    }
}
