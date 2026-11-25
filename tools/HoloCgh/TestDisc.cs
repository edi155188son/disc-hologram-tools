// Diagnostic disc: eight 12 mm patches round r = 41 mm, one variable changed per patch.
using DiscImageStudio.Core;
using DvdImageSolver.Solver;

static class TestDisc
{
    public enum Kind { Sequence, Grating, Image }
    public sealed record Patch(string Label, Hologram Geo, Kind Kind, byte[]? Seq, double PeriodUm, bool Robust);

    public static int Run(Opts o)
    {
        string a = o.TestAssets!;
        string P(string f) => Path.Combine(a, f);
        Opts Base(double angle, string tex, string depth, double near, double far, bool diffuse, bool robust)
        {
            var x = o.Clone();
            x.Texture = P(tex); x.Depth = P(depth); x.WidthMm = 12; x.CenterRadius = 41; x.CenterAngleDeg = angle; x.Copies = 1;
            x.NearMm = near; x.FarMm = far; x.Diffuse = diffuse; x.Robust = robust;
            return x;
        }
        StereoOverlay? lenti = null;
        List<Patch> patches;
        if (o.TestSet == "depth")
        {
            Opts D(double angle, string tex, string depth, double z, double half, bool diffuse)
            { var x = Base(angle, tex, depth, z, -z, diffuse, false); x.WidthMm = 20; x.HalfDeg = half; return x; }
            (string L, double A, string T, string Dp, double Z, double H, bool Df)[] spec =
            [
                ("D0", -90, "texture_rgba.png", "depth8.png", 3, 7, false),
                ("D1", -45, "texture_rgba.png", "depth8.png", 6, 7, false),
                ("D2",   0, "texture_rgba.png", "depth8.png", 9, 7, false),
                ("D3",  45, "texture_rgba.png", "depth8.png", 6, 10, false),
                ("D4",  90, "texture_rgba.png", "depth8.png", 9, 10, false),
                ("D5", 135, "texture_rgba.png", "depth8.png", 6, 7, true),
                ("D6", 180, "texture_rgba.png", "depth8.png", 9, 10, true),
                ("D7", 225, "lines.png", "lines_depth.png", 9, 10, false),
            ];
            patches = spec.Select(p => new Patch(
                $"{p.L} {(p.A + 90) / 30 + 12:00.0}h  {(p.T.StartsWith("lines") ? "two lines" : "avatar 20 mm")}, depth +/-{p.Z} mm, band +/-{p.H} deg, {(p.Df ? "diffuse" : "smooth")}, expected shift +/-{p.Z * Math.Tan(p.H * Math.PI / 180):F2} mm",
                new Hologram(D(p.A, p.T, p.Dp, p.Z, p.H, p.Df)), Kind.Image, null, 0, false)).ToList();
        }
        else if (o.TestSet == "avatar")
        {
            Opts Big(double angle, bool diffuse) { var x = Base(angle, "texture_rgba.png", "depth8.png", o.NearMm, o.FarMm, diffuse, true); x.WidthMm = o.WidthMm; return x; }
            patches =
            [
                new($"H1 12:00  avatar {o.WidthMm} mm, depth {o.FarMm}..{o.NearMm} mm, diffuse, robust", new Hologram(Big(-90, true)), Kind.Image, null, 0, true),
                new($"H2 18:00  avatar {o.WidthMm} mm, depth {o.FarMm}..{o.NearMm} mm, smooth, robust", new Hologram(Big(90, false)), Kind.Image, null, 0, true),
            ];
            if (o.LentiViews is { Length: > 0 })
            {
#if HAS_STEREO
                lenti = StereoOverlay.Create([new StereoLayerSpec(o.LentiViews, null, null, o.LentiViews.Length, 0, 41, 0,
                    o.WidthMm, o.WidthMm, 90, 0.2, 2)]);
                Console.WriteLine($"  L  03:00 + 09:00  lenticular, {o.LentiViews.Length} views in the given order");
#else
                throw new InvalidOperationException("--lenti needs a Disc Image Studio tree with the stereo-layer patch (dis-patches/reference)");
#endif
            }
        }
        else patches = new List<Patch>
        {
            new("P0 12:00  sequence CC67 (1.233 um, 26.5 deg) reference", new Hologram(Base(-90, "white.png", "flat_depth.png", 0, 0, false, false)), Kind.Sequence, [0xCC, 0x67], 0, false),
            new("P1 13:30  greedy grating 1.233 um, ideal scoring", new Hologram(Base(-45, "white.png", "flat_depth.png", 0, 0, false, false)), Kind.Grating, null, 1.2333, false),
            new("P2 15:00  greedy grating 1.300 um (no plain sequence), robust", new Hologram(Base(0, "white.png", "flat_depth.png", 0, 0, false, true)), Kind.Grating, null, 1.300, true),
            new("P3 16:30  face, flat z=0, smooth phase (no diffuser), robust", new Hologram(Base(45, "texture_rgba.png", "depth8.png", 0, 0, false, true)), Kind.Image, null, 0, true),
            new("P4 18:00  face, flat z=0, diffuse, robust", new Hologram(Base(90, "texture_rgba.png", "depth8.png", 0, 0, true, true)), Kind.Image, null, 0, true),
            new("P5 19:30  face, depth +/-3 mm, diffuse, robust", new Hologram(Base(135, "texture_rgba.png", "depth8.png", 3, -3, true, true)), Kind.Image, null, 0, true),
            new("P6 21:00  two lines: left z=+4 mm, right z=-4 mm, smooth, robust", new Hologram(Base(180, "lines.png", "lines_depth.png", 4, -4, false, true)), Kind.Image, null, 0, true),
            new("P7 22:30  face, flat z=0, diffuse, ideal scoring (vs P4)", new Hologram(Base(225, "texture_rgba.png", "depth8.png", 0, 0, true, false)), Kind.Image, null, 0, false),
        };
        Console.WriteLine("layout (clock = position when the label side faces you is NOT used; positions are canvas angles):");
        foreach (var p in patches) Console.WriteLine("  " + p.Label);
        if (o.Output is null) return 0;
        if (File.Exists(o.Output)) { Console.Error.WriteLine("Output exists: " + o.Output); return 1; }

        var sampler = new MultiSampler(patches, lenti);
        var opt = new MultiBlockSolveOptions(0, o.Sectors / 16 * 16, 0x30000, 0, 1, 1, 0, 0, false,
            new ImageMappingOptions(o.Sectors, o.Inner, o.Outer, 133.3, 0, true, 128, 1,
                PitchLinear: o.Lin, PitchQuadratic: o.Quad, PitchCubic: o.Cub, ImageOuterRadiusMm: o.Outer),
            Algorithm: SolverAlgorithm.Dispersion, FastParallelism: o.Threads);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var stream = new FileStream(o.Output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20))
            FastDispersionImageWriter.WriteScrambledCodewordPatternStream(sampler.Sample, stream, opt,
                p => { if (p.CompletedBlocks % 4096 == 0 || p.CompletedBlocks == p.TotalBlocks) Console.WriteLine($"{p.Fraction:P1}  {p.Elapsed.TotalSeconds:F0}s"); });
        Console.WriteLine($"Done: {o.Output} ({new FileInfo(o.Output).Length:N0} bytes, {sw.Elapsed.TotalSeconds:F0}s)");
        return 0;
    }

    sealed class MultiSampler(List<Patch> patches, StereoOverlay? lenti)
    {
        const double BitUm = 0.13330;
        readonly ThreadLocal<Efm.Chain> _chain = new(() => new Efm.Chain());
        readonly ThreadLocal<float[]> _target = new(() => new float[16]);

        public byte Sample(DvdCodewordSample s)
        {
            var ch = _chain.Value!;
            ulong idx = s.GlobalCodewordIndex;
            long pos = (long)(idx % 93) - 2;
            if (pos == 0) ch.Reset(true);
            else if (idx != ch.Last + 1) ch.Reset(false);
            ch.Last = idx;
            var cands = Efm.Table[ch.State][ch.Trail];
            ulong hash = Mix(idx);
            if (lenti is not null)
            {
                int q = lenti.Sample(s.Xmm, s.Ymm, idx);
                if (q >= 0) { ch.Reset(false); ch.Last = idx; return (byte)q; }
            }
            foreach (var p in patches)
            {
                if (!p.Geo.Locate(s.Xmm, s.Ymm, out int row, out double sc)) continue;
                double first = sc - 7.5 * BitUm;
                switch (p.Kind)
                {
                    case Kind.Sequence:
                        ch.Reset(false); ch.Last = idx;                  // chain unknown after a fixed byte
                        return p.Seq![(int)(idx % (ulong)p.Seq.Length)];
                    case Kind.Grating:
                        return Push(ch, ChainSampler.Choose(p.Geo, ch, cands, null, first, BitUm, hash, _target.Value!, out _, p.Robust, 2 * Math.PI / p.PeriodUm));
                    default:
                        var r = p.Geo.GetRow(row);
                        return Push(ch, ChainSampler.Choose(p.Geo, ch, cands, r, first, BitUm, hash, _target.Value!, out _, p.Robust));
                }
            }
            double s0 = Math.Atan2(s.Ymm, s.Xmm) * Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm) * 1000 - 7.5 * BitUm;
            return Push(ch, ChainSampler.Choose(patches[0].Geo, ch, cands, null, s0, BitUm, hash, _target.Value!, out _));
        }

        static byte Push(Efm.Chain ch, Efm.Cand c) { ch.Push(c); return c.Q; }
        static ulong Mix(ulong x) { x += 0x9E3779B97F4A7C15UL; x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL; x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL; return x ^ (x >> 31); }
    }
}
