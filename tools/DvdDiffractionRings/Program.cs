// DvdDiffractionRings: raw DVD test images for diffraction experiments with DiscImageStudio's DVD engine.
//   --pattern rings  : concentric rings, each filled with one scrambled byte Q (single tangential period)
//   --pattern probes : (1) radial gratings  - bright/dark alternating every N tracks (lines follow the groove)
//                      (2) spoke gratings   - radial lines aligned across tracks, tangential period d
// Rings are rotation-invariant; probes use the calibrated spiral model (incl. cubic pitch terms).
using System.Diagnostics;
using System.Globalization;
using DvdImageSolver.Solver;
using DiscImageStudio.ImageIO;

public static class RingsTool
{
    public const byte Bright = 0x92, Dark = 0xA5;

    // (inner mm, outer mm, Q or -1 for pseudo-random reference, label)
    public static readonly (double R0, double R1, int Q, string Label)[] Rings =
    [
        (24.6, 28.4, 0x92, "R1 Q=92  16T    2.13 um"),
        (29.0, 32.8, 0xB9, "R2 Q=B9  10.67T 1.42 um"),
        (33.4, 37.2, 0x66, "R3 Q=66  8T     1.07 um"),
        (37.8, 41.6, 0xA5, "R4 Q=A5  6.4T   0.85 um"),
        (42.2, 46.0, 0x00, "R5 Q=00  ~32T   4.27 um (mixed spectrum)"),
        (46.6, 50.4, -1,   "R6 random reference (no single period)"),
        (51.0, 54.0, 0x5F, "R7 Q=5F  16T    2.13 um (alt byte, compare R1)"),
        (54.6, 57.4, 0x65, "R8 Q=65  6.4T   0.85 um (alt byte, compare R4)"),
    ];

    // Seven-period rings, ordered by period (long -> short, inner -> outer); null = random reference
    public static readonly (double R0, double R1, byte[]? Seq, string Label)[] Rings7 =
    [
        (24.6, 28.2, [0x5F],       "S1 5F    16T    2.133 um  (purity ~66%)"),
        (28.7, 32.3, [0xC5, 0xDB], "S2 C5DB  12.75T 1.700 um  (~66%)"),
        (32.8, 36.4, [0xF9],       "S3 F9    10.75T 1.433 um  (~68%)"),
        (36.9, 40.5, [0xCC, 0x67], "S4 CC67  9.25T  1.233 um  (~68%)"),
        (41.0, 44.6, [0x6F],       "S5 6F    8T     1.066 um  (~67%)"),
        (45.1, 48.7, [0xDD, 0xB6], "S6 DDB6  7T     0.933 um  (~65%)"),
        (49.2, 52.8, [0xA5],       "S7 A5    6.4T   0.853 um  (~66%)"),
        (53.3, 57.4, null,         "S8 random reference (no grating)"),
    ];

    public static byte SampleRings7(DvdCodewordSample s)
    {
        double r = Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm);
        foreach (var ring in Rings7)
            if (r >= ring.R0 && r < ring.R1 && ring.Seq is { } q) return q[(int)(s.GlobalCodewordIndex % (ulong)q.Length)];
        return Noise(s.Xmm, s.Ymm);
    }

    public enum Kind { RadialTracks, Spokes, Uniform, Random }

    // Probe bands: Param = tracks per period (RadialTracks) or tangential period in um at band centre (Spokes)
    public static readonly (double R0, double R1, Kind Kind, double Param, string Label)[] Probes =
    [
        (24.6, 27.0, Kind.RadialTracks, 2,  "P1 radial grating, 2 tracks/period (~1.48 um)"),
        (27.4, 29.8, Kind.RadialTracks, 3,  "P2 radial grating, 3 tracks/period (~2.22 um)"),
        (30.2, 32.6, Kind.RadialTracks, 4,  "P3 radial grating, 4 tracks/period (~2.96 um)"),
        (33.0, 35.4, Kind.RadialTracks, 6,  "P4 radial grating, 6 tracks/period (~4.44 um)"),
        (35.8, 38.2, Kind.RadialTracks, 8,  "P5 radial grating, 8 tracks/period (~5.9 um, paper Fig.5)"),
        (39.0, 42.0, Kind.Spokes, 6,        "P6 spoke grating, d = 6 um (paper Fig.6)"),
        (42.6, 45.6, Kind.Spokes, 12,       "P7 spoke grating, d = 12 um (paper Fig.7a)"),
        (46.2, 49.2, Kind.Spokes, 24,       "P8 spoke grating, d = 24 um (paper Fig.7b)"),
        (49.8, 52.8, Kind.Spokes, 48,       "P9 spoke grating, d = 48 um (paper Fig.7c)"),
        (53.4, 55.4, Kind.Uniform, 0,       "P10 uniform Q=92 (no cross-track structure)"),
        (56.0, 57.4, Kind.Random, 0,        "P11 random reference"),
    ];

    static readonly int[] SpokeCount = Probes.Select(p => p.Kind == Kind.Spokes
        ? (int)Math.Round(2 * Math.PI * (p.R0 + p.R1) / 2 * 1000 / p.Param) : 0).ToArray();

    public static int Main(string[] args)
    {
        try
        {
            var o = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < args.Length; i += 2) o[args[i]] = args[i + 1];
            if (o.TryGetValue("--search", out var searchLen))
                return SpectrumSearch.Run(int.Parse(searchLen, CultureInfo.InvariantCulture),
                    o.TryGetValue("--threads", out var th) ? int.Parse(th, CultureInfo.InvariantCulture) : Environment.ProcessorCount,
                    o.GetValueOrDefault("--output", $"search{searchLen}.csv"),
                    o.TryGetValue("--sectors", out var se) ? uint.Parse(se, CultureInfo.InvariantCulture) : 2298496);
            if (o.TryGetValue("--holo-lambda", out var hl)) FHologram.Lambda = double.Parse(hl, CultureInfo.InvariantCulture) * 1e-6;
            string pattern = o.GetValueOrDefault("--pattern", "rings");
            if (o.ContainsKey("--export-lattice") && !o.ContainsKey("--output")) o["--output"] = "(none)";
            if (!o.TryGetValue("--sectors", out var sectorsText) || !o.TryGetValue("--output", out var output)
                || pattern is not ("rings" or "rings7" or "probes" or "image" or "zoneplate" or "hologram" or "palette" or "lattice" or "polar")
                || (pattern == "image" && !o.ContainsKey("--image")) || (pattern == "polar" && !o.ContainsKey("--raster")))
            {
                Console.WriteLine("DvdDiffractionRings --sectors N --output out.iso [--pattern rings|rings7|probes|image|zoneplate|hologram|palette|lattice|polar]\n"
                    + "  [--inner 23.96623775566068] [--outer 57.954483397299796]\n"
                    + "  [--pitch-linear -4.152250636325952E-05] [--pitch-quadratic 4.888878988850371E-05] [--pitch-cubic -1.9188389560484087E-05]\n"
                    + "  [--parallelism N]\n"
                    + "  image mode: --pattern image --image file.png [--size-mm 80] [--map 0:A5,128:66,192:B9,224:92,255:R]\n"
                    + "    gray levels are matched to the nearest --map key; R = pseudo-random (no grating); white margins are auto-cropped\n"
                    + "    a map value may be a byte sequence repeated along the track, e.g. 128:A566\n"
                    + "  lattice export: --export-lattice lattice.bin [--patch-r 40] [--patch-angle 0] [--patch-size 4]\n"
                    + "  lattice import: --pattern lattice --lattice a.bin[,b.bin,...] --values a.val[,b.val,...]  (one values file per lattice)\n"
                    + "  polar import: --pattern polar --raster a.hpo[,b.hpo,...]  (DVDHPO1 byte raster indexed by radius row / angle column)\n"
                    + "    [--pairs C5DB,CC67,...]  raster bytes 0x01..0x0F stand for these sequences (cycled along the track); 0x00 = random\n"
                    + "  search mode: --search 1|2 [--threads N] [--output search2.csv]  (spectral search over repeating byte sequences)\nN = total sectors reported by the blank disc (DiscImageStudio 'read from disc').\n\nrings:");
                foreach (var r in Rings) Console.WriteLine($"  {r.R0:F1}-{r.R1:F1} mm  {r.Label}");
                Console.WriteLine("rings7:");
                foreach (var r in Rings7) Console.WriteLine($"  {r.R0:F1}-{r.R1:F1} mm  {r.Label}");
                Console.WriteLine("zoneplate:");
                ZonePlates.Describe();
                Console.WriteLine("hologram:");
                FHologram.Describe();
                Console.WriteLine("palette:");
                foreach (var r in PaletteProbe.Rings) Console.WriteLine($"  {r.R0:F1}-{r.R1:F1} mm  {r.Label}");
                Console.WriteLine("probes:");
                foreach (var p in Probes) Console.WriteLine($"  {p.R0:F1}-{p.R1:F1} mm  {p.Label}");
                return 2;
            }
            uint sectors = uint.Parse(sectorsText, CultureInfo.InvariantCulture);
            double D(string key, double def) => o.TryGetValue(key, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : def;
            int par = o.TryGetValue("--parallelism", out var p2) ? int.Parse(p2, CultureInfo.InvariantCulture) : Environment.ProcessorCount;
            uint fill = o.TryGetValue("--fill", out var f) ? uint.Parse(f, CultureInfo.InvariantCulture) : sectors / 16 * 16;
            uint start = o.TryGetValue("--start", out var s) ? uint.Parse(s, CultureInfo.InvariantCulture) : 0;
            var options = CreateOptions(sectors, D("--inner", 23.96623775566068), D("--outer", 57.954483397299796),
                D("--pitch-linear", -4.152250636325952E-05), D("--pitch-quadratic", 4.888878988850371E-05),
                D("--pitch-cubic", -1.9188389560484087E-05), start, fill, par);

            if (o.TryGetValue("--export-lattice", out var latOut))
                return Lattice.Export(latOut, options, D("--patch-r", 40), D("--patch-angle", 0), D("--patch-size", 4), sectors,
                    D("--inner", 23.96623775566068), D("--outer", 57.954483397299796), D("--pitch-linear", -4.152250636325952E-05),
                    D("--pitch-quadratic", 4.888878988850371E-05), D("--pitch-cubic", -1.9188389560484087E-05), par);
            if (o.TryGetValue("--pairs", out var pairsText) && pairsText.Length > 0)
                Polar.Pairs = pairsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Convert.FromHexString).ToArray();
            Func<DvdCodewordSample, byte> sampler = pattern switch
            {
                "rings" => s0 => SampleRings(s0.Xmm, s0.Ymm),
                "probes" => SampleProbes,
                "rings7" => SampleRings7,
                "zoneplate" => ZonePlates.Sample,
                "hologram" => FHologram.Sample,
                "palette" => PaletteProbe.Sample,
                "polar" => Polar.Sampler(o["--raster"].Split(',').Select(p => Polar.Load(p.Trim())).ToArray()),
                "lattice" => Lattice.Sampler(o["--lattice"].Split(',').Zip(o["--values"].Split(','), (l, v) => Lattice.Load(l.Trim(), v.Trim())).ToArray()),
                _ => CreateImageSampler(o["--image"], D("--size-mm", 80), o.GetValueOrDefault("--map", "0:A5,128:66,192:B9,224:92,255:R")),
            };
            if (File.Exists(output)) throw new IOException("Output exists: " + output);
            var sw = Stopwatch.StartNew();
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20))
            {
                FastDispersionImageWriter.WriteScrambledCodewordPatternStream(sampler, stream, options,
                    pr => { if (pr.CompletedBlocks % 4096 == 0 || pr.CompletedBlocks == pr.TotalBlocks) Console.WriteLine($"{pr.Fraction:P1}  {pr.Elapsed.TotalSeconds:F0}s"); });
            }
            Console.WriteLine($"Done: {output}  ({new FileInfo(output).Length:N0} bytes, {sw.Elapsed.TotalSeconds:F0}s)");
            Console.WriteLine("Burn with 'Write image file to disc' (e.g. ImgBurn), low speed. Do not add it as a file to a data disc.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }

    public static MultiBlockSolveOptions CreateOptions(uint sectors, double inner, double outer,
        double lin, double quad, double cub, uint start, uint fill, int par)
        => new(start, fill, 0x30000, 0, 1, 1, 0, 0, true,
            new ImageMappingOptions(sectors, inner, outer, 133.3, 0, true, 128, 1, PitchLinear: lin, PitchQuadratic: quad, PitchCubic: cub),
            Algorithm: SolverAlgorithm.Dispersion, FastParallelism: Math.Clamp(par, 1, 64));

    public static byte SampleRings(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y);
        foreach (var ring in Rings)
            if (r >= ring.R0 && r < ring.R1 && ring.Q >= 0) return (byte)ring.Q;
        return Noise(x, y);
    }

    public static byte SampleProbes(DvdCodewordSample s)
    {
        double r = Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm);
        for (int i = 0; i < Probes.Length; i++)
        {
            var p = Probes[i];
            if (r < p.R0 || r >= p.R1) continue;
            switch (p.Kind)
            {
                case Kind.RadialTracks:
                {
                    // Whole revolutions of the modelled spiral: switches once per turn, so the
                    // grating lines follow the physical groove; model error only moves the seam.
                    long turn = (long)Math.Floor(s.TrackAngleRadians / (2 * Math.PI));
                    int n = (int)p.Param;
                    return (turn % n) * 2 < n ? Bright : Dark;
                }
                case Kind.Spokes:
                {
                    double phi = Math.Atan2(s.Ymm, s.Xmm) / (2 * Math.PI) * SpokeCount[i];
                    return phi - Math.Floor(phi) < 0.5 ? Bright : Dark;
                }
                case Kind.Uniform: return Bright;
                default: return Noise(s.Xmm, s.Ymm);
            }
        }
        return Noise(s.Xmm, s.Ymm);
    }

    // Image mode: luminance -> nearest map key -> scrambled byte (or -1 = random). The non-white bounding box
    // of the image is fitted, aspect preserved, into a centred square of sizeMm; everything outside is the 255 entry.
    public static Func<DvdCodewordSample, byte> CreateImageSampler(string path, double sizeMm, string map)
    {
        var raster = RasterCodec.LoadBgra32(path);
        int w = raster.Width, h = raster.Height;
        var px = raster.Pixels;
        var keys = map.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.Split(':')).Select(a => (Level: int.Parse(a[0], CultureInfo.InvariantCulture),
                Q: a[1].Equals("R", StringComparison.OrdinalIgnoreCase) ? null : Convert.FromHexString(a[1]))).ToArray();
        byte[]?[] lut = new byte[]?[256];
        for (int l = 0; l < 256; l++) lut[l] = keys.MinBy(k => Math.Abs(k.Level - l)).Q;
        byte[] lum = new byte[w * h];
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                double l = px[i + 3] < 128 ? 255 : 0.114 * px[i] + 0.587 * px[i + 1] + 0.299 * px[i + 2];
                byte b = (byte)Math.Clamp((int)Math.Round(l), 0, 255);
                lum[y * w + x] = b;
                if (b < 250) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            }
        if (x1 < 0) throw new InvalidDataException("Image is blank (all white).");
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        double scale = Math.Max(cw, ch) / sizeMm; // pixels per mm
        double cx = (x0 + x1 + 1) / 2.0, cy = (y0 + y1 + 1) / 2.0;
        byte[]? background = lut[255];
        Console.WriteLine($"Image {w}x{h}, content box {cw}x{ch} px -> {cw / scale:F1} x {ch / scale:F1} mm, {1000 / scale:F1} um per pixel");
        foreach (var k in keys) Console.WriteLine($"  gray ~{k.Level,3} -> {(k.Q is null ? "random (no grating)" : "Q=" + Convert.ToHexString(k.Q))}");
        return s =>
        {
            int ix = (int)Math.Floor(cx + s.Xmm * scale), iy = (int)Math.Floor(cy + s.Ymm * scale);
            byte[]? q = ix >= 0 && iy >= 0 && ix < w && iy < h ? lut[lum[iy * w + ix]] : background;
            return q is null ? Noise(s.Xmm, s.Ymm) : q[(int)(s.GlobalCodewordIndex % (ulong)q.Length)];
        };
    }

    static byte Noise(double x, double y)
    {
        ulong h = (ulong)BitConverter.DoubleToInt64Bits(x * 7919.0 + y) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return (byte)h;
    }
}
