// Band-pass sigma-delta encoding for BdAmp (--encoding sd).
// Along every radial line (fixed polar angle) the tracks form a sequence with spacing = track pitch. The target
// transmittance x_n = g * Re(S_n), S = sum_c conj(e_c) exp(i 2 pi v / L_c), is binarized with error feedback whose
// noise transfer function has zeros at every carrier frequency (poles at radius r for stability):
//   NTF(z) = prod_c (1 - 2 cos w_c z^-1 + z^-2) / (1 - 2 r cos w_c z^-1 + r^2 z^-2),  w_c = 2 pi pitch / L_c.
// Binarization noise is pushed away from the +1 viewing windows. Decisions are precomputed per block on a polar
// grid (columns every ~0.46 um of arc, one entry per track) and looked up per byte (track index + nearest column).
using System.Threading;

sealed class SdGroup
{
    public BdAmp.Field[] Fields = [];
    public double Cx, Cy, Ca, Sa, HalfV, HalfU, Reach, Thc, Phi0, DPhi;
    public int Cols; public long[] NLo = []; public int[] NCount = []; public ulong[][] Bits = [];
}

static partial class BdAmpSd
{
    public static double R = 0.85, Gain = 0, Level = 1.5, ColPitchMm = 0.465e-3;   // Gain 0 = auto: Gain * p99.5|S| = Level
    public static Func<double, double> Theta = null!, ThetaInv = null!; public static double Offset;

    public static SdGroup[] Build(BdAmp.Field[] fields)
    {
        var carrier = fields.Where(f => f.Lambda > 0).ToArray();
        var groups = carrier.GroupBy(f => (Math.Round(f.Cx, 3), Math.Round(f.Cy, 3))).Select(g =>
        {
            var f0 = g.First();
            return new SdGroup { Fields = g.ToArray(), Cx = f0.Cx, Cy = f0.Cy, Ca = f0.Ca, Sa = f0.Sa, HalfV = f0.HalfV, HalfU = f0.HalfU, Reach = f0.Reach };
        }).ToArray();
        foreach (var gr in groups) Precompute(gr);
        return groups;
    }

    static double Wrap(double a) { a %= 2 * Math.PI; if (a > Math.PI) a -= 2 * Math.PI; if (a < -Math.PI) a += 2 * Math.PI; return a; }

    static void Precompute(SdGroup g)
    {
        double rc = Math.Sqrt(g.Cx * g.Cx + g.Cy * g.Cy); g.Thc = Math.Atan2(g.Cy, g.Cx);
        double amin = 0, amax = 0;
        foreach (var (sv, su) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
        {
            double x = g.Cx + sv * g.HalfV * g.Ca - su * g.HalfU * g.Sa, y = g.Cy + sv * g.HalfV * g.Sa + su * g.HalfU * g.Ca;
            double d = Wrap(Math.Atan2(y, x) - g.Thc); amin = Math.Min(amin, d); amax = Math.Max(amax, d);
        }
        g.DPhi = ColPitchMm / rc; g.Phi0 = g.Thc + amin; g.Cols = (int)Math.Ceiling((amax - amin) / g.DPhi) + 1;
        double rmin = rc - g.HalfV - 0.01, rmax = Math.Sqrt((rc + g.HalfV) * (rc + g.HalfV) + g.HalfU * g.HalfU) + 0.01;
        double pitch = (ThetaInv(Theta(rc) + 2 * Math.PI) - rc);              // local track pitch, mm
        // NTF order (coefficients are per column: along a radial line at angle phi the straight carrier advances by
        // pitch * cos(phi - thc) per track, so the notch frequencies shift slightly towards the block sides)
        int M = 2 * g.Fields.Length;
        double gain = Gain;
        if (gain <= 0)
        {   // auto level: sample |S| inside the block
            var rnd = new Random(7); var mags = new List<double>();
            for (int i = 0; i < 200000; i++)
            {
                double v = (rnd.NextDouble() * 2 - 1) * g.HalfV, u = (rnd.NextDouble() * 2 - 1) * g.HalfU;
                double x = g.Cx + v * g.Ca - u * g.Sa, y = g.Cy + v * g.Sa + u * g.Ca;
                BdAmp.ComputeS(g.Fields, x, y, out double a, out double b); double m = Math.Sqrt(a * a + b * b);
                if (m > 1e-6) mags.Add(m);
            }
            mags.Sort(); double p995 = mags.Count > 0 ? mags[(int)(0.995 * (mags.Count - 1))] : 1;
            gain = Level / p995;
            Console.WriteLine($"    auto gain {gain:F3} (p99.5 |S| = {p995:F3}, level {Level})");
        }
        g.NLo = new long[g.Cols]; g.NCount = new int[g.Cols]; g.Bits = new ulong[g.Cols][];
        long done = 0; var sw = System.Diagnostics.Stopwatch.StartNew(); var next = 10.0;
        Console.WriteLine($"  sigma-delta block @ r={rc:F1} mm, {g.Fields.Length} carriers, {g.Cols} columns, pitch {pitch * 1e6:F1} nm, notch pole r={R}");
        Parallel.For(0, g.Cols, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, j =>
        {
            double phi = g.Phi0 + j * g.DPhi, c = Math.Cos(phi), s = Math.Sin(phi);
            long nlo = (long)Math.Ceiling((Theta(rmin) + phi - Offset) / (2 * Math.PI));
            long nhi = (long)Math.Floor((Theta(rmax) + phi - Offset) / (2 * Math.PI));
            int cnt = (int)Math.Max(0, nhi - nlo + 1);
            var bits = new ulong[(cnt + 63) / 64];
            var Eh = new double[M]; var Wh = new double[M];
            double[] A = [1], B = [1]; double cd = Math.Cos(phi - g.Thc);
            foreach (var f in g.Fields)
            {
                double wc = 2 * Math.PI * pitch * cd / f.Lambda;
                A = Conv(A, [1, -2 * Math.Cos(wc), 1]); B = Conv(B, [1, -2 * R * Math.Cos(wc), R * R]);
            }
            var HA = new double[M + 1]; for (int i = 0; i <= M; i++) HA[i] = A[i] - B[i];
            for (int k = 0; k < cnt; k++)
            {
                double r = ThetaInv(2 * Math.PI * (nlo + k) - phi + Offset);
                double x = r * c, y = r * s;
                double dx = x - g.Cx, dy = y - g.Cy, v = dx * g.Ca + dy * g.Sa, u = -dx * g.Sa + dy * g.Ca;
                if (Math.Abs(v) > g.HalfV || Math.Abs(u) > g.HalfU) { Array.Clear(Eh); Array.Clear(Wh); continue; }
                BdAmp.ComputeS(g.Fields, x, y, out double sr, out _);
                double w = 0;
                for (int i = 1; i <= M; i++) w += HA[i] * Eh[i - 1] - B[i] * Wh[i - 1];
                double vv = gain * sr + w, yb = vv >= 0 ? 1 : -1;
                if (yb > 0) bits[k >> 6] |= 1UL << (k & 63);
                for (int i = M - 1; i > 0; i--) { Eh[i] = Eh[i - 1]; Wh[i] = Wh[i - 1]; }
                if (M > 0) { Eh[0] = yb - vv; Wh[0] = w; }
            }
            g.NLo[j] = nlo; g.NCount[j] = cnt; g.Bits[j] = bits;
            long d = Interlocked.Increment(ref done);
            if (sw.Elapsed.TotalSeconds > next) lock (g) { if (sw.Elapsed.TotalSeconds > next) { next += 10; Console.WriteLine($"    {100.0 * d / g.Cols:F1}%  {sw.Elapsed.TotalSeconds:F0}s"); } }
        });
        Console.WriteLine($"    done {sw.Elapsed.TotalSeconds:F0}s, {g.Bits.Sum(b => (long)b.Length) * 8 / 1e9:F2} GB");
    }

    static double[] Conv(double[] a, double[] b)
    {
        var c = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++) for (int j = 0; j < b.Length; j++) c[i + j] += a[i] * b[j];
        return c;
    }

    // 1 = bright, 0 = dark, -1 = not in this group
    public static int Lookup(SdGroup g, double x, double y)
    {
        double dx = x - g.Cx, dy = y - g.Cy;
        if (dx * dx + dy * dy > g.Reach * g.Reach) return -1;
        double v = dx * g.Ca + dy * g.Sa, u = -dx * g.Sa + dy * g.Ca;
        if (Math.Abs(v) > g.HalfV || Math.Abs(u) > g.HalfU) return -1;
        double phi = g.Thc + Wrap(Math.Atan2(y, x) - g.Thc);
        int j = (int)Math.Round((phi - g.Phi0) / g.DPhi); if (j < 0 || j >= g.Cols) return 0;
        double r = Math.Sqrt(x * x + y * y);
        long n = (long)Math.Round((Theta(r) + phi - Offset) / (2 * Math.PI));
        long k = n - g.NLo[j]; if (k < 0 || k >= g.NCount[j]) return 0;
        return (int)((g.Bits[j][k >> 6] >> (int)(k & 63)) & 1);
    }

    // debug: decisions around the block centre, rows = tracks (outward), cols = columns
    public static void Dump(SdGroup g, string path, double halfMm)
    {
        double rc = Math.Sqrt(g.Cx * g.Cx + g.Cy * g.Cy);
        int jc = (int)Math.Round((g.Thc - g.Phi0) / g.DPhi), hj = (int)(halfMm / ColPitchMm);
        long nc = (long)Math.Round((Theta(rc) + g.Thc - Offset) / (2 * Math.PI));
        double pitch = ThetaInv(Theta(rc) + 2 * Math.PI) - rc; int hn = (int)(halfMm / pitch);
        using var w = new BinaryWriter(File.Create(path));
        w.Write(2 * hn + 1); w.Write(2 * hj + 1); w.Write(pitch); w.Write(ColPitchMm);
        for (int a = -hn; a <= hn; a++)
            for (int b = -hj; b <= hj; b++)
            {
                int j = jc + b; long k = nc + a - g.NLo[j];
                w.Write((byte)((k >= 0 && k < g.NCount[j]) ? (g.Bits[j][k >> 6] >> (int)(k & 63)) & 1 : 0));
            }
        for (int a = -hn; a <= hn; a++)
            for (int b = -hj; b <= hj; b++)
            {
                int j = jc + b; double phi = g.Phi0 + j * g.DPhi;
                double r = ThetaInv(2 * Math.PI * (nc + a) - phi + Offset);
                BdAmp.ComputeS(g.Fields, r * Math.Cos(phi), r * Math.Sin(phi), out double sr, out _);
                w.Write((float)sr);
            }
        Console.WriteLine($"  dump {path}: {2 * hn + 1} tracks x {2 * hj + 1} columns");
    }
}
