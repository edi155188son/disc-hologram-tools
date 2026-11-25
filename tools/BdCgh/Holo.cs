using DiscImageStudio.Core;
using System.Collections.Concurrent;
using System.Globalization;
using DiscImageStudio.ImageIO;

public sealed class Opts
{
    public string Texture = "", Depth = "";
    public string? Output, PreviewPath;
    public string[]? LentiViews;
    public double WidthMm = 30, CenterRadius = 41, CenterAngleDeg = -90, NearMm = 3, FarMm = -3;
    public double AlphaDeg = 27, HalfDeg = 7, DarkPeriodUm = 6.4 * 0.13330, LambdaNm = 550, EmitterMm = 0.03, RowMm = 0.03, Gamma = 2.2, Floor = 0.02;
    public int Copies = 2, Threads = 0, Seed = 7;
    public bool Flip, Robust, Diffuse = true;
    public string? TestAssets;
    public string TestSet = "diag";
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
                case "--set": o.TestSet = V(); break;
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
public sealed class Hologram
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

