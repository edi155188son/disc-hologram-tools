// Spectral search over repeating scrambled-byte sequences (length 1 or 2), using the project's own
// EFMPlus/NRZI encoder. For each sequence: fill one ECC block, encode, Welch power spectrum of the
// channel levels, report the dominant tangential period and how much power sits in it ("purity").
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using DvdImageSolver.Encoding;
using DvdImageSolver.Solver;

public static class SpectrumSearch
{
    const int N = 4096;
    const double BitNm = 133.3;

    public static int Run(int length, int threads, string output, uint sectors)
    {
        if (length is < 1 or > 2) throw new ArgumentException("--search supports length 1 or 2");
        int total = length == 1 ? 256 : 65536;
        uint lba = 1_000_000 / 16 * 16;
        double[] win = Enumerable.Range(0, N).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N)).ToArray();
        var results = new ConcurrentBag<(int Seq, double PeakT, double Purity, double Second)>();
        var sw = Stopwatch.StartNew();
        int done = 0;
        Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = threads }, () => new DvdEccBlockEncoder(), (seq, _, enc) =>
        {
            byte[] q = length == 1 ? [(byte)seq] : [(byte)(seq >> 8), (byte)seq];
            var opt = RingsTool.CreateOptions(sectors, 23.96623775566068, 57.954483397299796, 0, 0, 0, lba, 16, 1);
            using var ms = new MemoryStream(0x8000);
            FastDispersionImageWriter.WriteScrambledCodewordPatternStream(s => q[(int)(s.GlobalCodewordIndex % (ulong)q.Length)], ms, opt);
            byte[] lv = enc.Encode(ms.ToArray(), lba, 0x30000, new ModulationBoundary(1, 0, true)).ChannelLevels;
            var (peakT, purity, second) = Analyze(lv, win);
            results.Add((seq, peakT, purity, second));
            int d = Interlocked.Increment(ref done);
            if (d % 2048 == 0 || d == total) Console.WriteLine($"{d}/{total}  {sw.Elapsed.TotalSeconds:F0}s");
            return enc;
        }, _ => { });

        var list = results.OrderBy(r => r.Seq).ToList();
        using (var w = new StreamWriter(output))
        {
            w.WriteLine("sequence,peak_T,peak_um,purity,second_peak_fraction");
            foreach (var r in list)
                w.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{Hex(r.Seq, length)},{r.PeakT:F3},{r.PeakT * BitNm / 1000:F3},{r.Purity:F4},{r.Second:F4}"));
        }
        // best sequence per period bucket (0.25 T)
        Console.WriteLine("\nBest sequence per dominant period (purity = power in that line / total):");
        Console.WriteLine("  period      um     purity  sequence");
        foreach (var g in list.GroupBy(r => Math.Round(r.PeakT * 4) / 4).OrderBy(g => g.Key))
        {
            var b = g.MaxBy(r => r.Purity);
            if (b.Purity < 0.25) continue;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {g.Key,6:F2}T  {g.Key * BitNm / 1000,6:F3}  {b.Purity,6:P0}   {Hex(b.Seq, length)}   ({g.Count()} sequences)"));
        }
        Console.WriteLine($"\nFull table: {output}");
        return 0;
    }

    static string Hex(int seq, int length) => length == 1 ? seq.ToString("X2") : seq.ToString("X4");

    static (double PeakT, double Purity, double Second) Analyze(byte[] lv, double[] win)
    {
        double mean = 0; foreach (var b in lv) mean += b; mean /= lv.Length;
        var ps = new double[N / 2];
        var c = new Complex[N];
        for (int s = 0; s + N <= lv.Length; s += N)
        {
            for (int i = 0; i < N; i++) c[i] = (lv[s + i] - mean) * win[i];
            FFT(c);
            for (int k = 1; k < N / 2; k++) ps[k] += c[k].Real * c[k].Real + c[k].Imaginary * c[k].Imaginary;
        }
        double total = 0; for (int k = 1; k < N / 2; k++) total += ps[k];
        // dominant line with period 4.5T..48T (0.6..6.4 um)
        int kMin = (int)Math.Ceiling(N / 48.0), kMax = (int)Math.Floor(N / 4.5);
        int best = kMin; for (int k = kMin; k <= kMax; k++) if (ps[k] > ps[best]) best = k;
        double Band(int k0) { double sum = 0; for (int k = Math.Max(1, k0 - 3); k <= Math.Min(N / 2 - 1, k0 + 3); k++) sum += ps[k]; return sum; }
        double main = Band(best);
        int second = -1; double secondPow = 0;
        for (int k = kMin; k <= kMax; k++) if (Math.Abs(k - best) > 6 && ps[k] > secondPow) { secondPow = ps[k]; second = k; }
        double sec = second > 0 ? Band(second) / total : 0;
        // refine peak location with a parabolic fit
        double y0 = ps[best - 1], y1 = ps[best], y2 = ps[best + 1];
        double delta = (y0 - 2 * y1 + y2) == 0 ? 0 : 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2);
        return (N / (best + delta), main / total, sec);
    }

    static void FFT(Complex[] a)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) (a[i], a[j]) = (a[j], a[i]); }
        for (int len = 2; len <= n; len <<= 1)
        {
            var w = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
            for (int i = 0; i < n; i += len) { Complex wn = 1; for (int j = 0; j < len / 2; j++) { var u = a[i + j]; var v = a[i + j + len / 2] * wn; a[i + j] = u + v; a[i + j + len / 2] = u - v; wn *= w; } }
        }
    }
}
