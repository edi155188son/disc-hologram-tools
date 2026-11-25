// BdHolo: BD-R (single layer) hologram / pattern test images built on DiscImageStudio's BD engine.
// Patches are rasters computed elsewhere (MATLAB); every payload byte is sampled at its nominal
// physical position (after LDC interleaving / BIS) and written as AA ("dark", 3T-rich) or 44
// ("bright", 6T-rich) through the built-in parallel run-length path. Raster value 128 and all space
// outside the patches = per-byte pseudo-random AA/44 (no grating).
using System.Diagnostics;
using System.Globalization;
using DiscImageStudio.BluRay;

static class BdHolo
{
    sealed class Patch
    {
        public int N; public double Dx, R, A, Ca, Sa, Cx, Cy, Half; public byte[] V = [];
    }

    static Patch Load(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        var magic = System.Text.Encoding.ASCII.GetString(r.ReadBytes(8));
        if (magic != "BDHR1\0\0\0") throw new InvalidDataException(path + ": not a BDHR1 raster");
        var p = new Patch { N = r.ReadInt32(), Dx = r.ReadDouble(), R = r.ReadDouble(), A = r.ReadDouble() * Math.PI / 180 };
        p.Ca = Math.Cos(p.A); p.Sa = Math.Sin(p.A); p.Cx = p.R * p.Ca; p.Cy = p.R * p.Sa; p.Half = p.N * p.Dx / 2;
        p.V = r.ReadBytes(p.N * p.N);
        if (p.V.Length != p.N * p.N) throw new InvalidDataException(path + ": truncated");
        Console.WriteLine($"  {Path.GetFileName(path)}: r={p.R} mm @ {p.A * 180 / Math.PI:F2} deg, {2 * p.Half:F2} mm, {p.N}x{p.N} @ {p.Dx * 1000:F2} um");
        return p;
    }

    static byte Noise(double x, double y)
    {
        ulong h = (ulong)BitConverter.DoubleToInt64Bits(x * 7919.0 + y) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return (h & 1) == 0 ? (byte)0 : (byte)255;
    }

    static int Main(string[] args)
    {
        var o = new Dictionary<string, string>();
        for (int i = 0; i + 1 < args.Length; i += 2) o[args[i]] = args[i + 1];
        if (!o.ContainsKey("--output") || !o.ContainsKey("--patches") || !o.ContainsKey("--inner") || !o.ContainsKey("--outer"))
        {
            Console.WriteLine("BdHolo --inner R --outer R [--sectors 12219392] [--pitch-linear a --pitch-quadratic b --pitch-cubic c]\n"
                + "       [--start-angle deg] [--max-radius mm] --patches a.bdhr[,b.bdhr...] --output out.iso\n"
                + "  --max-radius: stop the ISO at this radius (smaller file, partial burn). Default: whole disc.");
            return 2;
        }
        double D(string k, double d) => o.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
        uint total = o.TryGetValue("--sectors", out var st) ? uint.Parse(st, CultureInfo.InvariantCulture) : BluRayDiscParameters.SectorsPerLayer;
        var disc = new BluRayDiscParameters(total, 1, D("--inner", 24), D("--outer", 58), D("--start-angle", 0),
            D("--pitch-linear", 0), D("--pitch-quadratic", 0), D("--pitch-cubic", 0));
        disc.Validate();
        var geo = new BluRayTrackGeometry(disc);
        Console.WriteLine($"BD-R geometry: inner {disc.InnerRadiusMm} outer {disc.OuterRadiusMm} mm, {total} sectors, effective pitch {geo.EffectiveTrackPitchMicrometers:F4} um");
        if (o.TryGetValue("--max-radius", out var mr))
        {
            double rmax = double.Parse(mr, CultureInfo.InvariantCulture);
            uint lo = 32, hi = total;
            while (hi - lo > 32) { uint mid = (lo + hi) / 2 / 32 * 32; if (geo.AtSector(mid).RadiusMm < rmax) lo = mid; else hi = mid; }
            disc = disc with { UsableSectors = hi };
            Console.WriteLine($"Output stops at r = {geo.AtSector(hi).RadiusMm:F3} mm: {hi} sectors ({hi * 2048.0 / 1e9:F2} GB)");
        }
        var patches = o["--patches"].Split(',').Select(s => Load(s.Trim())).ToArray();
        Func<double, double, byte> sample = (x, y) =>
        {
            foreach (var p in patches)
            {
                double dx = x - p.Cx, dy = y - p.Cy;
                if (Math.Abs(dx) > p.Half * 1.5 || Math.Abs(dy) > p.Half * 1.5) continue;
                double ur = dx * p.Ca + dy * p.Sa, ut = -dx * p.Sa + dy * p.Ca;
                int iu = (int)Math.Floor((ur + p.Half) / p.Dx), it = (int)Math.Floor((ut + p.Half) / p.Dx);
                if ((uint)iu < (uint)p.N && (uint)it < (uint)p.N)
                {
                    byte v = p.V[iu * p.N + it];
                    return v == 128 ? Noise(x, y) : v;
                }
            }
            return Noise(x, y);
        };
        var mapping = new BluRayImagePayloadMapping(disc, sample, new BluRayRunLengthMapping(), allowParallelSampling: true);
        var plan = BluRayIsoGenerator.PrepareDrawing(disc);
        var output = o["--output"];
        if (File.Exists(output)) { Console.Error.WriteLine("Output exists: " + output); return 1; }
        var sw = Stopwatch.StartNew(); var next = TimeSpan.Zero;
        var prog = new Progress<BluRayStreamingProgress>(v =>
        {
            if (v.Elapsed < next && v.CompletedBytes != v.TotalBytes) return;
            next = v.Elapsed + TimeSpan.FromSeconds(5);
            Console.WriteLine($"{v.Fraction:P1}  {v.Elapsed.TotalSeconds:F0}s");
        });
        var sum = BluRayIsoGenerator.GenerateFile(output, plan, mapping, prog);
        Console.WriteLine($"Done: {output} ({sum.BytesWritten:N0} bytes, {sw.Elapsed.TotalSeconds:F0}s)");
        Console.WriteLine("Burn with ImgBurn 'Write image file to disc' on BD-R, low speed.");
        return 0;
    }
}
