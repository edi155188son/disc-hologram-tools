// HoloCgh - true-wavefront (horizontal-parallax) computer-generated hologram on DVD-R.
//
// The object (picture + depth map) is a set of point emitters along each track (arc). Each row
// (radius bin) gets the 1-D object wave along the arc; the writer then picks, codeword by codeword,
// the scrambled byte whose EFMPlus pit/land pattern best matches Re{object wave x carrier} over its
// 16 channel bits. Only bytes >= 0x58 (no DSV-chosen substitution) and no DSV-chosen State 1/4
// branch are used, so the pattern of every byte in a sync frame is known exactly, up to one sign
// per frame. See claude/white-light-3d-hologram-others-2026-11.md for the probe that verified this.
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using DiscImageStudio.Core;
using DiscImageStudio.ImageIO;
using DvdImageSolver.Encoding;
using DvdImageSolver.Solver;

if (args.Length > 0 && args[0] == "probe") { Probe.Run(); return 0; }
var o = Opts.Parse(args);
if (o is null) { Opts.Usage(); return 1; }
if (o.TestAssets is not null) return TestDisc.Run(o);
var holo = new Hologram(o);
if (o.PreviewPath is not null) { Preview.Render(holo, o); }
if (o.Output is null) return 0;
if (File.Exists(o.Output)) { Console.Error.WriteLine("Output exists: " + o.Output); return 1; }

StereoOverlay? lenti = null;
if (o.LentiViews is { Length: > 0 })
{
#if HAS_STEREO
    double a = o.CenterAngleDeg * Math.PI / 180 + Math.PI / o.Copies;     // between the hologram copies
    double cx = o.CenterRadius * Math.Cos(a), cy = o.CenterRadius * Math.Sin(a);
    double rot = Math.Atan2(cx, -cy) * 180 / Math.PI;
    lenti = StereoOverlay.Create([new StereoLayerSpec(o.LentiViews, null, null, o.LentiViews.Length, 0, cx, cy,
        o.WidthMm, o.WidthMm * holo.PictureAspect, rot, 0.2, o.Copies)]);
    Console.WriteLine($"lenticular comparison: {o.LentiViews.Length} views, copies centred at {a * 180 / Math.PI:F0} deg + k*{360 / o.Copies} deg");
#else
    Console.Error.WriteLine("--lenti needs a Disc Image Studio tree with the stereo-layer patch (dis-patches/reference)");
    return 1;
#endif
}

var writer = new ChainSampler(holo, lenti);
var opt = new MultiBlockSolveOptions(0, o.Sectors / 16 * 16, 0x30000, 0, 1, 1, 0, 0, false,
    new ImageMappingOptions(o.Sectors, o.Inner, o.Outer, 133.3, 0, true, 128, 1,
        PitchLinear: o.Lin, PitchQuadratic: o.Quad, PitchCubic: o.Cub, ImageOuterRadiusMm: o.Outer),
    Algorithm: SolverAlgorithm.Dispersion, FastParallelism: o.Threads);
var sw = System.Diagnostics.Stopwatch.StartNew();
using (var stream = new FileStream(o.Output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20))
    FastDispersionImageWriter.WriteScrambledCodewordPatternStream(writer.Sample, stream, opt,
        p => { if (p.CompletedBlocks % 4096 == 0 || p.CompletedBlocks == p.TotalBlocks) Console.WriteLine($"{p.Fraction:P1}  {p.Elapsed.TotalSeconds:F0}s  rows cached {holo.RowsComputed}"); });
Console.WriteLine($"Done: {o.Output} ({new FileInfo(o.Output).Length:N0} bytes, {sw.Elapsed.TotalSeconds:F0}s)");
Console.WriteLine($"hologram words {writer.HoloWords:N0} (dark {writer.DarkWords:N0}), frames with uncertain start {writer.UncertainWords:N0} words");
Console.WriteLine("Burn with 'Write image file to disc' (ImgBurn), low speed.");
return 0;

// ------------------------------------------------------------------------------------------

sealed class Opts
{
    public string Texture = "", Depth = "";
    public string? Output, PreviewPath;
    public string[]? LentiViews;
    public double WidthMm = 30, CenterRadius = 41, CenterAngleDeg = -90, NearMm = 3, FarMm = -3;
    public double AlphaDeg = 27, HalfDeg = 7, DarkPeriodUm = 6.4 * 0.13330, LambdaNm = 550, EmitterMm = 0.03, RowMm = 0.03, Gamma = 2.2, Floor = 0.02;
    public int Copies = 2, Threads = 0, Seed = 7;
    public bool Flip, Robust, Diffuse = true;
    public string? TestAssets, TestSet;   // --testdisc <asset dir> [--testset depth]
    public int MarkShrink;            // preview only: bits removed from each written mark (1 = trailing end, 2 = both ends)
    public uint Sectors = 2297888;
    public double Inner = 23.96623775566068, Outer = 57.954483397299796;
    public double Lin = -4.152250636325952E-05, Quad = 4.888878988850371E-05, Cub = -1.9188389560484087E-05;

    public Opts Clone() => (Opts)MemberwiseClone();

    public static void Usage() => Console.WriteLine("""
        HoloCgh --texture tex.png --depth depth.png [--output holo.iso] [--preview prev.png]
          --width-mm 30  --radius 41  --angle -90 (canvas deg, -90 = 12 o'clock)  --copies 2
          --near-mm 3  --far-mm -3   depth of white / black (mm, + = toward viewer)
          --alpha 27  --half 10      carrier and +/- angular band (deg at --lambda 550 nm)
          --dark-period 0.853        grating (um) written where the object is dark / outside: 387 nm at 27 deg, harmonics beyond 40 deg
          --flip                     swap front/back (pseudoscopic fix)
          --lenti v1.png,v2.png,...  lenticular comparison copies between the hologram copies
          --emitter-mm 0.03 --row-mm 0.03 --gamma 2.2 --floor 0.02 --threads 0 --seed 7
          --sectors 2297888 (+ --inner/--outer/--pitch-linear/--pitch-quadratic/--pitch-cubic)
        """);

    public static Opts? Parse(string[] a)
    {
        var o = new Opts();
        double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        for (int i = 0; i < a.Length; i++)
        {
            string k = a[i];
            string V() => i + 1 < a.Length ? a[++i] : throw new ArgumentException("missing value for " + k);
            switch (k)
            {
                case "--texture": o.Texture = V(); break;
                case "--depth": o.Depth = V(); break;
                case "--output": o.Output = V(); break;
                case "--preview": o.PreviewPath = V(); break;
                case "--lenti": o.LentiViews = V().Split(','); break;
                case "--width-mm": o.WidthMm = D(V()); break;
                case "--radius": o.CenterRadius = D(V()); break;
                case "--angle": o.CenterAngleDeg = D(V()); break;
                case "--copies": o.Copies = int.Parse(V()); break;
                case "--near-mm": o.NearMm = D(V()); break;
                case "--far-mm": o.FarMm = D(V()); break;
                case "--alpha": o.AlphaDeg = D(V()); break;
                case "--half": o.HalfDeg = D(V()); break;
                case "--dark-period": o.DarkPeriodUm = D(V()); break;
                case "--lambda": o.LambdaNm = D(V()); break;
                case "--emitter-mm": o.EmitterMm = D(V()); break;
                case "--row-mm": o.RowMm = D(V()); break;
                case "--gamma": o.Gamma = D(V()); break;
                case "--floor": o.Floor = D(V()); break;
                case "--threads": o.Threads = int.Parse(V()); break;
                case "--seed": o.Seed = int.Parse(V()); break;
                case "--flip": o.Flip = true; break;
                case "--mark-shrink": o.MarkShrink = int.Parse(V()); break;
                case "--robust": o.Robust = true; break;
                case "--smooth": o.Diffuse = false; break;
                case "--testdisc": o.TestAssets = V(); break;
                case "--testset": o.TestSet = V(); break;
                case "--sectors": o.Sectors = uint.Parse(V()); break;
                case "--inner": o.Inner = D(V()); break;
                case "--outer": o.Outer = D(V()); break;
                case "--pitch-linear": o.Lin = D(V()); break;
                case "--pitch-quadratic": o.Quad = D(V()); break;
                case "--pitch-cubic": o.Cub = D(V()); break;
                default: Console.Error.WriteLine("unknown option " + k); return null;
            }
        }
        return (o.Texture.Length > 0 && o.Depth.Length > 0) || o.TestAssets is not null ? o : null;
    }
}

/// <summary>Object model and per-row baseband object waves (lazily computed, cached).</summary>
sealed class Hologram
{
    public readonly Opts O;
    readonly int _w, _h;
    readonly float[] _amp, _depth;            // amplitude sqrt(linear intensity), depth 0..1 (0 = transparent)
    readonly bool[] _opaque;
    public readonly double W, H, Rc, SinAlpha, K, TanHalf, CopyStep, Theta0;
    public readonly double RMin, RMax;
    public const double BaseStepUm = 0.5;     // baseband sample step along the arc
    readonly ConcurrentDictionary<int, Lazy<Row?>> _rows = new();
    public int RowsComputed;
    public double PictureAspect => (double)_h / _w;
    public readonly float DarkThreshold, FullAmplitude;
    public double DarkK => 2 * Math.PI / O.DarkPeriodUm;

    public sealed record Row(double S0Um, float[] Re, float[] Im);   // baseband E(s) from S0, step BaseStepUm

    public Hologram(Opts o)
    {
        O = o;
        BgraRaster tex = RasterCodec.LoadBgra32(o.Texture);
        BgraRaster dep = RasterCodec.LoadBgra32(o.Depth);
        if (tex.Width != dep.Width || tex.Height != dep.Height) throw new InvalidDataException("texture and depth sizes differ");
        _w = tex.Width; _h = tex.Height;
        _amp = new float[_w * _h]; _depth = new float[_w * _h]; _opaque = new bool[_w * _h];
        for (int i = 0; i < _amp.Length; i++)
        {
            int q = i * 4;
            double lum = (0.114 * tex.Pixels[q] + 0.587 * tex.Pixels[q + 1] + 0.299 * tex.Pixels[q + 2]) / 255.0;
            double lin = Math.Pow(lum, o.Gamma);
            _opaque[i] = tex.Pixels[q + 3] >= 128 && lin >= o.Floor;
            _amp[i] = _opaque[i] ? (float)Math.Sqrt(lin) : 0f;
            _depth[i] = (float)(dep.Pixels[q + 2] / 255.0);
        }
        W = o.WidthMm; H = o.WidthMm * _h / _w; Rc = o.CenterRadius;
        double lam = o.LambdaNm * 1e-3;                        // um
        K = 2 * Math.PI / lam;                                 // rad / um
        SinAlpha = Math.Sin(o.AlphaDeg * Math.PI / 180);
        TanHalf = Math.Tan(o.HalfDeg * Math.PI / 180);
        CopyStep = 2 * Math.PI / o.Copies;
        Theta0 = o.CenterAngleDeg * Math.PI / 180;
        RMin = Rc - H / 2 - 0.01; RMax = Rc + H / 2 + 0.01;
        FullAmplitude = (float)(1 / Math.Sqrt(o.EmitterMm));            // rms |E| of a full-white diffuse area
        DarkThreshold = 0.03f * FullAmplitude;
        double band0 = (SinAlpha - Math.Sin(o.HalfDeg * Math.PI / 180)) / lam, band1 = (SinAlpha + Math.Sin(o.HalfDeg * Math.PI / 180)) / lam;
        Console.WriteLine($"picture {_w}x{_h} px -> {W:F1} x {H:F1} mm at r={Rc}, {o.Copies} copies; depth {o.FarMm:+0.0;-0.0}..{o.NearMm:+0.0;-0.0} mm");
        Console.WriteLine($"carrier {o.AlphaDeg} deg +/- {o.HalfDeg} deg at {o.LambdaNm} nm: periods {1 / band1:F2}..{1 / band0:F2} um (writable 0.85..2.13)");
    }

    /// <summary>Copy-local arc coordinates of a canvas point, or false outside every copy's picture.</summary>
    public bool Locate(double x, double y, out int row, out double sUm)
    {
        row = -1; sUm = 0;
        double r = Math.Sqrt(x * x + y * y);
        if (r < RMin || r > RMax) return false;
        double d = Math.Atan2(y, x) - Theta0;
        d -= Math.Round(d / CopyStep) * CopyStep;               // nearest copy
        double u = r * Math.Sin(d), v = r * Math.Cos(d) - Rc;
        if (Math.Abs(u) > W / 2 || Math.Abs(v) > H / 2) return false;
        row = (int)((r - RMin) / O.RowMm);
        sUm = r * d * 1000;
        return true;
    }

    public double RowRadius(int row) => RMin + (row + 0.5) * O.RowMm;

    public Row? GetRow(int row) => _rows.GetOrAdd(row, k => new Lazy<Row?>(() => Compute(k))).Value;

    /// <summary>Drops cached rows well below the radius the writer has reached.</summary>
    public void Evict(int belowRow)
    {
        foreach (int k in _rows.Keys) if (k < belowRow) _rows.TryRemove(k, out _);
    }

    (float A, float Z, bool Ok) Pixel(double u, double v)
    {
        double px = (u / W + 0.5) * _w, py = (0.5 - v / H) * _h;
        int ix = (int)px, iy = (int)py;
        if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return (0, 0, false);
        int i = iy * _w + ix;
        return (_amp[i], _depth[i], _opaque[i]);
    }

    Row? Compute(int row)
    {
        double r = RowRadius(row);
        double de = O.EmitterMm;
        // arc span of the picture at this radius
        double halfArc = Math.Asin(Math.Min(1, (W / 2) / r)) * r;
        int ne = (int)Math.Ceiling(2 * halfArc / de);
        var em = new List<(double S, double Z, double A, double Psi)>(ne);
        var rng = new Random(HashCode.Combine(O.Seed, row));
        for (int e = 0; e < ne; e++)
        {
            double s = -halfArc + (e + 0.5) * de;                 // mm
            double d = s / r;
            double u = r * Math.Sin(d), v = r * Math.Cos(d) - Rc;
            var (a, z01, ok) = Pixel(u, v);
            double psi = O.Diffuse ? rng.NextDouble() * 2 * Math.PI : 0;
            if (!ok) continue;
            double z = (O.FarMm + z01 * (O.NearMm - O.FarMm)) * 1000;   // um
            em.Add((s * 1000, z, a, psi));
        }
        Interlocked.Increment(ref RowsComputed);
        if (em.Count == 0) return null;
        const double zMinUm = 100;
        double maxL = Math.Max(Math.Max(Math.Abs(O.NearMm), Math.Abs(O.FarMm)) * 1000, zMinUm) * TanHalf;
        double s0 = -halfArc * 1000 - maxL - 2;
        int n = (int)Math.Ceiling((2 * halfArc * 1000 + 2 * maxL + 4) / BaseStepUm) + 1;
        var re = new float[n]; var im = new float[n];
        double flip = O.Flip ? -1 : 1;
        // parallel over emitter chunks; per-chunk accumulators
        int chunks = Math.Max(1, Math.Min(Environment.ProcessorCount, em.Count / 8));
        var parts = new (float[] Re, float[] Im)[chunks];
        Parallel.For(0, chunks, c =>
        {
            var pr = new float[n]; var pi = new float[n];
            for (int e = c; e < em.Count; e += chunks)
            {
                var (se, z, a, psi) = em[e];
                double az = Math.Max(Math.Abs(z), zMinUm);
                double L = az * TanHalf;
                double amp = a / Math.Sqrt(2 * L / 1000);           // equal brightness for any depth
                double sign = -Math.Sign(z == 0 ? 1 : z) * flip;     // front: converging, back: diverging
                int i0 = Math.Max(0, (int)((se - L - s0) / BaseStepUm)), i1 = Math.Min(n - 1, (int)((se + L - s0) / BaseStepUm));
                for (int i = i0; i <= i1; i++)
                {
                    double ds = s0 + i * BaseStepUm - se;
                    double R = Math.Sqrt(ds * ds + az * az);
                    double ph = sign * K * (R - az) + psi;
                    var (sn, cs) = Math.SinCos(ph);
                    pr[i] += (float)(amp * cs); pi[i] += (float)(amp * sn);
                }
            }
            parts[c] = (pr, pi);
        });
        foreach (var (pr, pi) in parts)
            for (int i = 0; i < n; i++) { re[i] += pr[i]; im[i] += pi[i]; }
        return new Row(s0, re, im);
    }

    /// <summary>Baseband object wave at arc position sUm (linear interpolation).</summary>
    public static (float Re, float Im) Envelope(Row row, double sUm)
    {
        double f = (sUm - row.S0Um) / BaseStepUm;
        int i = (int)Math.Floor(f);
        if (i < 0 || i >= row.Re.Length - 1) return (0, 0);
        float t = (float)(f - i);
        return (row.Re[i] + t * (row.Re[i + 1] - row.Re[i]), row.Im[i] + t * (row.Im[i + 1] - row.Im[i]));
    }
}

/// <summary>EFMPlus candidate tables for the state-aware greedy choice.</summary>
static class Efm
{
    public const int MinByte = 88;
    public sealed record Cand(byte Q, ushort Word, byte Next, int Lead, int Trail, bool Odd, float[] Levels);
    // [state 1..4][trail 0..15] -> legal, branch-free candidates
    public static readonly Cand[][][] Table = Build();

    static int Lead(ushort w) { int z = 0; for (int b = 15; b >= 0 && ((w >> b) & 1) == 0; b--) z++; return z; }
    static int Trail(ushort w) { int z = 0; for (int b = 0; b < 16 && ((w >> b) & 1) == 0; b++) z++; return z; }

    static Cand[][][] Build()
    {
        var t = new Cand[5][][];
        for (byte st = 1; st <= 4; st++)
        {
            t[st] = new Cand[16][];
            for (int trail = 0; trail < 16; trail++)
            {
                var list = new List<Cand>();
                for (int q = MinByte; q < 256; q++)
                {
                    var (w, ns) = EfmPlusTables.LookupMain((byte)q, st);
                    int comb = trail + Lead(w);
                    if (comb is < 2 or > 10) continue;
                    if (st is 1 or 4)
                    {
                        var a = EfmPlusTables.LookupMain((byte)q, 1); var b = EfmPlusTables.LookupMain((byte)q, 4);
                        bool both = trail + Lead(a.Word) is >= 2 and <= 10 && trail + Lead(b.Word) is >= 2 and <= 10;
                        if (both && (a.Word != b.Word || a.NextState != b.NextState)) continue;   // DSV would choose
                    }
                    var lev = new float[16]; bool level = true;
                    for (int bit = 15, k = 0; bit >= 0; bit--, k++) { if (((w >> bit) & 1) != 0) level = !level; lev[k] = level ? 1f : -1f; }
                    list.Add(new Cand((byte)q, w, ns, Lead(w), Trail(w), (BitOperations.PopCount(w) & 1) == 1, lev));
                }
                t[st][trail] = list.ToArray();
            }
        }
        return t;
    }

    public sealed class Chain
    {
        public byte State = 1; public int Trail; public float Sign = 1; public ulong Last = ulong.MaxValue; public bool Valid;
        public void Reset(bool valid) { State = 1; Trail = 0; Sign = 1; Valid = valid; }
        public void Push(Cand c) { State = c.Next; Trail = Math.Min(c.Trail, 15); if (c.Odd) Sign = -Sign; }
    }
}

/// <summary>Writer callback: tracks the EFMPlus chain per thread and picks every byte.</summary>
sealed class ChainSampler(Hologram holo, StereoOverlay? lenti)
{
    const double BitUm = 0.13330;
    readonly ThreadLocal<Efm.Chain> _chain = new(() => new Efm.Chain());
    readonly ThreadLocal<float[]> _target = new(() => new float[16]);
    public long HoloWords, DarkWords, UncertainWords;
    int _evictRow;

    public byte Sample(DvdCodewordSample s)
    {
        var ch = _chain.Value!;
        ulong idx = s.GlobalCodewordIndex;
        long pos = (long)(idx % 93) - 2;
        if (pos == 0) ch.Reset(true);
        else if (idx != ch.Last + 1) ch.Reset(false);          // format bytes before us: start unknown
        ch.Last = idx;
        var cands = Efm.Table[ch.State][ch.Trail];

        if (lenti is not null)
        {
            int q = lenti.Sample(s.Xmm, s.Ymm, idx);
            if (q >= 0) { ch.Valid = false; ch.Reset(false); ch.Last = idx; return (byte)q; }
        }

        if (holo.Locate(s.Xmm, s.Ymm, out int row, out double sc) && holo.GetRow(row) is { } r)
        {
            Interlocked.Increment(ref HoloWords);
            if (!ch.Valid) Interlocked.Increment(ref UncertainWords);
            if (row - 6 > Volatile.Read(ref _evictRow)) { Volatile.Write(ref _evictRow, row - 6); holo.Evict(row - 6); }
            var c = Choose(holo, ch, cands, r, sc - 7.5 * BitUm, BitUm, Mix(idx), _target.Value!, out bool dark);
            if (dark) Interlocked.Increment(ref DarkWords);
            return Pick(ch, cands, c);
        }
        // background: phase-continuous dark grating (light leaves outside the viewing band)
        double s0 = Math.Atan2(s.Ymm, s.Xmm) * Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm) * 1000 - 7.5 * BitUm;
        return Pick(ch, cands, Choose(holo, ch, cands, null, s0, BitUm, Mix(idx), _target.Value!, out _));
    }

    /// <summary>Hologram byte with probability |E|/full, otherwise a byte following the grating
    /// gratingK (rad/um); both by maximum correlation of the 16 channel levels with the target.
    /// robust: score the written reflectance of both frame polarities with every mark shortened
    /// by one channel bit at each end (burn model), averaged, instead of the ideal +/-1 levels.</summary>
    public static Efm.Cand Choose(Hologram holo, Efm.Chain ch, Efm.Cand[] cands, Hologram.Row? row, double firstBitUm, double bitUm,
        ulong hash, float[] tgt, out bool dark, bool robust = false, double gratingK = 0)
    {
        dark = true;
        if (gratingK == 0) gratingK = holo.DarkK;
        if (row is not null)
        {
            var (er, ei) = Hologram.Envelope(row, firstBitUm + 7.5 * bitUm);
            double mag = Math.Sqrt(er * er + ei * ei);
            double u = (hash >> 11) * (1.0 / (1UL << 53));
            if (mag >= holo.DarkThreshold && u < mag / holo.FullAmplitude)
            {
                dark = false;
                for (int t = 0; t < 16; t++)
                {
                    double st = firstBitUm + t * bitUm;
                    var (a, b) = Hologram.Envelope(row, st);
                    var (sn, cs) = Math.SinCos(holo.K * holo.SinAlpha * st);
                    tgt[t] = (float)(a * cs - b * sn);             // Re{E e^{i k sin(alpha) s}}
                }
            }
        }
        if (dark)
            for (int t = 0; t < 16; t++) tgt[t] = (float)Math.Cos(gratingK * (firstBitUm + t * bitUm));
        Efm.Cand best = cands[0]; float bestScore = float.NegativeInfinity;
        if (robust)
        {
            Span<float> p = stackalloc float[16];
            foreach (var c in cands)
            {
                float score = 0;
                for (int f = -1; f <= 1; f += 2)
                {
                    float prev = f * ch.Sign;                     // absolute level before this word
                    for (int t = 0; t < 16; t++)
                    {
                        float a = f * ch.Sign * c.Levels[t];
                        float before = t == 0 ? prev : f * ch.Sign * c.Levels[t - 1];
                        bool startsHere = a != before, endsNext = t < 15 && f * ch.Sign * c.Levels[t + 1] != a;
                        p[t] = a < 0 && (startsHere || endsNext) ? 1f : a;   // mark edge bits burn as land
                    }
                    float d = 0; for (int t = 0; t < 16; t++) d += p[t] * tgt[t];
                    score += f * d;
                }
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }
        var tv0 = new Vector<float>(tgt, 0); var tv1 = Vector<float>.Count == 8 ? new Vector<float>(tgt, 8) : default;
        foreach (var c in cands)
        {
            float score;
            if (Vector<float>.Count == 8) score = Vector.Dot(new Vector<float>(c.Levels, 0), tv0) + Vector.Dot(new Vector<float>(c.Levels, 8), tv1);
            else { score = 0; for (int t = 0; t < 16; t++) score += c.Levels[t] * tgt[t]; }
            score *= ch.Sign;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    static byte Pick(Efm.Chain ch, Efm.Cand[] _, Efm.Cand c) { ch.Push(c); return c.Q; }

    static ulong Mix(ulong x) { x += 0x9E3779B97F4A7C15UL; x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL; x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL; return x ^ (x >> 31); }
}

/// <summary>White-light (F, d, C) view simulation of one copy, using the same greedy on an ideal
/// track model (93-codeword frames, sync noise, random frame sign).</summary>
static class Preview
{
    public static void Render(Hologram h, Opts o)
    {
        const double BitUm = 0.13330;
        double[] views = [o.AlphaDeg - 6, o.AlphaDeg, o.AlphaDeg + 6];
        (double Lam, int Ch)[] lines = [(656.3, 0), (587.6, 1), (486.1, 2)];   // C, d, F
        double rowStep = 0.12;                                        // mm between simulated rows
        int nRows = (int)(h.H / rowStep);
        double half = h.W / 2 + 1;                                    // mm of arc simulated
        int nBits = 1 << (int)Math.Ceiling(Math.Log2(2 * half * 1000 / BitUm));
        int outW = 600;
        var img = new float[views.Length][]; for (int v = 0; v < views.Length; v++) img[v] = new float[nRows * outW * 3];
        Console.WriteLine($"preview (mark shrink {o.MarkShrink} bit): {nRows} rows x {nBits} bits, views {string.Join("/", views)} deg, lines C/d/F");
        Parallel.For(0, nRows, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, ri =>
        {
            double rMm = h.Rc + h.H / 2 - (ri + 0.5) * rowStep;
            int row = (int)((rMm - h.RMin) / o.RowMm);
            var rowData = h.GetRow(row);
            var lev = new Complex[nBits];
            var rng = new Random(ri * 7919 + 1);
            var ch = new Efm.Chain(); var tgtBuf = new float[16];
            double s0 = -nBits / 2 * BitUm;
            int frameBits = 93 * 16;
            float frameSign = 1;
            for (int b0 = 0; b0 < nBits; b0 += 16)
            {
                int cw = b0 / 16; int pos = cw % 93 - 2;
                if (pos == -2) frameSign = rng.Next(2) == 0 ? 1 : -1;
                if (pos < 0) { for (int t = 0; t < 16 && b0 + t < nBits; t++) lev[b0 + t] = rng.Next(2) == 0 ? 1 : -1; continue; }
                if (pos == 0) ch.Reset(true);
                var cands = Efm.Table[ch.State][ch.Trail];
                var pick = ChainSampler.Choose(h, ch, cands, rowData, s0 + b0 * BitUm, BitUm, (ulong)rng.NextInt64(), tgtBuf, out _, o.Robust);
                for (int t = 0; t < 16; t++) lev[b0 + t] = pick.Levels[t] * ch.Sign * frameSign;
                ch.Push(pick);
            }
            _ = frameBits;
            if (o.MarkShrink > 0)
            {
                // marks = level -1 runs (absolute, after frame sign); shorten each by MarkShrink bits
                var phys = new Complex[nBits];
                int i0 = 0;
                while (i0 < nBits)
                {
                    int i1 = i0; while (i1 < nBits && lev[i1].Real == lev[i0].Real) i1++;
                    bool mark = lev[i0].Real < 0;
                    int a = i0, b = i1;
                    if (mark) { if (o.MarkShrink >= 2) a++; b--; }
                    for (int i = i0; i < i1; i++) phys[i] = (mark && i >= a && i < b) ? -1 : 1;
                    i0 = i1;
                }
                lev = phys;
            }
            var spec = (Complex[])lev.Clone(); Fft.Transform(spec, false);
            for (int v = 0; v < views.Length; v++)
            {
                double th = views[v] * Math.PI / 180;
                foreach (var (lamNm, c) in lines)
                {
                    double lam = lamNm * 1e-3;
                    double f0 = Math.Sin(th) / lam, df = Math.Sin(0.5 * Math.PI / 180) / lam * Math.Cos(th);
                    var sel = new Complex[nBits];
                    for (int k = 0; k < nBits; k++)
                    {
                        double f = (k < nBits / 2 ? k : k - nBits) / (nBits * BitUm);
                        if (Math.Abs(f - f0) < df) sel[k] = spec[k];
                    }
                    Fft.Transform(sel, true);
                    // intensity along the arc, binned to the output width (eye focused on the disc plane)
                    var acc = new double[outW]; var cnt = new int[outW];
                    for (int i = 0; i < nBits; i++)
                    {
                        double sMm = (s0 + i * BitUm) / 1000;
                        int x = (int)((sMm + h.W / 2) / h.W * outW);
                        if ((uint)x >= (uint)outW) continue;
                        acc[x] += sel[i].Magnitude * sel[i].Magnitude; cnt[x]++;
                    }
                    for (int x = 0; x < outW; x++) img[v][(ri * outW + x) * 3 + c] = (float)(cnt[x] > 0 ? acc[x] / cnt[x] : 0);
                }
            }
        });
        // normalise all views together, gamma 1/2.2, side by side
        double peak = img.SelectMany(a => a).Where(x => x > 0).OrderBy(x => x).Skip((int)(img.Sum(a => a.Length) * 0.995 / 3)).FirstOrDefault();
        var all = img.SelectMany(a => a).Where(x => x > 0).ToArray(); Array.Sort(all);
        peak = all.Length > 0 ? all[(int)(all.Length * 0.995)] : 1;
        int gap = 10, W2 = views.Length * outW + (views.Length - 1) * gap;
        int rowsOut = (int)Math.Round(nRows * (outW / h.W * rowStep));   // keep aspect
        var px = new byte[W2 * rowsOut * 4];
        for (int y = 0; y < rowsOut; y++)
        {
            int ri = Math.Min(nRows - 1, (int)((double)y / rowsOut * nRows));
            for (int v = 0; v < views.Length; v++)
                for (int x = 0; x < outW; x++)
                {
                    int d = (y * W2 + v * (outW + gap) + x) * 4;
                    for (int c = 0; c < 3; c++)
                    {
                        double val = Math.Pow(Math.Min(1, img[v][(ri * outW + x) * 3 + c] / peak), 1 / 2.2);
                        px[d + 2 - c] = (byte)(255 * val);
                    }
                    px[d + 3] = 255;
                }
        }
        RasterCodec.SavePng(o.PreviewPath!, W2, rowsOut, px, grayscale: false);
        Console.WriteLine($"preview written: {o.PreviewPath} ({W2}x{rowsOut}); views left->right {string.Join(", ", views)} deg");
    }
}

static class Fft
{
    public static void Transform(Complex[] a, bool inverse)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2 * Math.PI / len * (inverse ? 1 : -1);
            Complex wl = new(Math.Cos(ang), Math.Sin(ang));
            for (int i = 0; i < n; i += len)
            {
                Complex w = 1;
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = a[i + j], v = a[i + j + len / 2] * w;
                    a[i + j] = u + v; a[i + j + len / 2] = u - v; w *= wl;
                }
            }
        }
        if (inverse) for (int i = 0; i < n; i++) a[i] /= n;
    }
}
