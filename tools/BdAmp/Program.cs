// BdAmp: radial-carrier amplitude (Lee detour-phase) holograms on BD-R, built on DiscImageStudio's BD engine.
// Each field file (BDAF1) holds a smooth complex object field on a coarse grid in a block's local frame
// (v = radially outward at the block centre, u = +angle direction). For every payload byte at its nominal
// physical position the generator evaluates, on the fly:
//     e = interp(field, v, u);  D = asin(min(|e|,1))/pi;  bright <=> cos(2*pi*v/Lambda - arg e) > cos(pi*D)
// so the radial structure is resolved per track (0.32 um) without a gigantic raster.
// Bright -> --light byte, dark -> --dark byte (post-scrambler payload bytes, default 22 / 9E).
// Outside every block: per-byte pseudo-random dark/light (no grating), like BdHolo.
//
// BDAF1 file: "BDAF1\0\0\0"; int32 nv, nu, interpV (0 = nearest row, 1 = bilinear, 2 = bicubic [sum mode]); double v0, dv, u0, du (mm);
//   double rc (mm), thc (deg), halfV, halfU (mm), lambda (carrier period, mm), thr; then nv*nu*2 float32 (re, im), v-major.
using System.Diagnostics;
using System.Globalization;
using DiscImageStudio.BluRay;

static class BdAmp
{
    public sealed class Field
    {
        public int Nv, Nu, InterpV; public double V0, Dv, U0, Du, Rc, Thc, HalfV, HalfU, Lambda, Thr;
        public double Cx, Cy, Ca, Sa, Reach; public float[] E = [];
    }

    static bool HeaderOnly;                        // --opt-bits without --load-fields: carrier field data not needed
    static Field Load(string path)
    {
        double? rot = null;                       // path@deg: same field, block centre moved to angle deg
        int at = path.LastIndexOf('@');
        if (at > 0) { rot = double.Parse(path[(at + 1)..], CultureInfo.InvariantCulture); path = path[..at]; }
        using var r = new BinaryReader(File.OpenRead(path));
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(8)) != "BDAF1\0\0\0") throw new InvalidDataException(path + ": not BDAF1");
        var f = new Field { Nv = r.ReadInt32(), Nu = r.ReadInt32(), InterpV = r.ReadInt32() };
        f.V0 = r.ReadDouble(); f.Dv = r.ReadDouble(); f.U0 = r.ReadDouble(); f.Du = r.ReadDouble();
        f.Rc = r.ReadDouble(); f.Thc = (rot ?? r.ReadDouble()) * Math.PI / 180; if (rot != null) r.ReadDouble(); f.HalfV = r.ReadDouble(); f.HalfU = r.ReadDouble();
        f.Lambda = r.ReadDouble(); f.Thr = r.ReadDouble();
        long n = 2L * f.Nv * f.Nu;
        if (!(HeaderOnly && f.Lambda > 0))
        {
            f.E = new float[n];
            var bytes = r.ReadBytes(checked((int)(n * 4)));
            if (bytes.Length != n * 4) throw new InvalidDataException(path + ": truncated");
            Buffer.BlockCopy(bytes, 0, f.E, 0, bytes.Length);
        }
        f.Ca = Math.Cos(f.Thc); f.Sa = Math.Sin(f.Thc); f.Cx = f.Rc * f.Ca; f.Cy = f.Rc * f.Sa;
        f.Reach = Math.Sqrt(f.HalfV * f.HalfV + f.HalfU * f.HalfU);
        Console.WriteLine($"  {Path.GetFileName(path)}: centre r={f.Rc} mm @ {f.Thc * 180 / Math.PI:F1} deg, block {2 * f.HalfV:F2} x {2 * f.HalfU:F2} mm, " +
            $"grid {f.Nv}x{f.Nu} @ {f.Dv * 1000:F1}x{f.Du * 1000:F1} um, carrier {f.Lambda * 1000:F3} um, interpV={f.InterpV}");
        return f;
    }

    static bool DoublePhase;                       // --encoding dp
    static Func<double, double, int> Parity = (x, y) => 0;

    static bool Sample(Field f, double x, double y, out bool bright)
    {
        bright = false;
        double dx = x - f.Cx, dy = y - f.Cy;
        if (dx * dx + dy * dy > f.Reach * f.Reach) return false;
        double v = dx * f.Ca + dy * f.Sa, u = -dx * f.Sa + dy * f.Ca;
        if (Math.Abs(v) > f.HalfV || Math.Abs(u) > f.HalfU) return false;
        double fi = (v - f.V0) / f.Dv, fj = (u - f.U0) / f.Du;
        int i0 = (int)Math.Floor(fi), j0 = (int)Math.Floor(fj);
        bool density = f.Lambda <= 0;
        if (i0 < 0 || j0 < 0 || i0 + 1 >= f.Nv || j0 + 1 >= f.Nu)                        // inside block, no field
        {
            if (density) return false;
            if (DoublePhase) bright = Math.Cos(2 * Math.PI * v / f.Lambda - (Parity(x, y) == 0 ? 1 : -1) * Math.PI / 2) > 0;
            return true;
        }
        double wi = fi - i0, wj = fj - j0, re, im;
        if (f.InterpV == 0)
        {
            int i = wi < 0.5 ? i0 : i0 + 1; long a = 2L * ((long)i * f.Nu + j0);
            re = f.E[a] * (1 - wj) + f.E[a + 2] * wj; im = f.E[a + 1] * (1 - wj) + f.E[a + 3] * wj;
        }
        else
        {
            long a = 2L * ((long)i0 * f.Nu + j0), b = a + 2L * f.Nu;
            double w00 = (1 - wi) * (1 - wj), w01 = (1 - wi) * wj, w10 = wi * (1 - wj), w11 = wi * wj;
            re = f.E[a] * w00 + f.E[a + 2] * w01 + f.E[b] * w10 + f.E[b + 2] * w11;
            im = f.E[a + 1] * w00 + f.E[a + 3] * w01 + f.E[b + 1] * w10 + f.E[b + 3] * w11;
        }
        if (!density) { double tw0 = TaperWeight(f, v, u); re *= tw0; im *= tw0; }
        double amp = Math.Sqrt(re * re + im * im);
        if (density) { bright = true; return amp > 0.5; }                                  // label strokes = light byte
        if (DoublePhase)
        {   // double-phase: tracks alternate arg e +/- acos|e|; 50 % duty everywhere -> no zero-order picture
            double alpha = Math.Acos(Math.Min(amp, 1.0)) * (Parity(x, y) == 0 ? 1 : -1);
            bright = Math.Cos(2 * Math.PI * v / f.Lambda - Math.Atan2(im, re) - alpha) > 0;
            return true;
        }
        if (amp < f.Thr) return true;
        double D = Math.Asin(Math.Min(amp, 1.0)) / Math.PI;
        bright = Math.Cos(2 * Math.PI * v / f.Lambda - Math.Atan2(im, re)) > Math.Cos(Math.PI * D);
        return true;
    }

    // Field value at a point: 0 = outside block, 1 = inside block but outside grid, 2 = value (re, im); v = local radial coord.
    static int Eval(Field f, double x, double y, out double re, out double im, out double v, out double u)
    {
        re = im = v = u = 0;
        double dx = x - f.Cx, dy = y - f.Cy;
        if (dx * dx + dy * dy > f.Reach * f.Reach) return 0;
        v = dx * f.Ca + dy * f.Sa; u = -dx * f.Sa + dy * f.Ca;
        if (Math.Abs(v) > f.HalfV || Math.Abs(u) > f.HalfU) return 0;
        double fi = (v - f.V0) / f.Dv, fj = (u - f.U0) / f.Du;
        int i0 = (int)Math.Floor(fi), j0 = (int)Math.Floor(fj);
        if (i0 < 0 || j0 < 0 || i0 + 1 >= f.Nv || j0 + 1 >= f.Nu) return 1;
        double wi = fi - i0, wj = fj - j0;
        if (f.InterpV == 2)
        {   // bicubic (Catmull-Rom) for band-limited fields sampled ~2x above Nyquist
            if (i0 < 1 || j0 < 1 || i0 + 2 >= f.Nv || j0 + 2 >= f.Nu) return 1;
            Span<double> cv = stackalloc double[4], cu = stackalloc double[4];
            CatmullRom(wi, cv); CatmullRom(wj, cu);
            for (int a = 0; a < 4; a++)
            {
                long row = 2L * ((long)(i0 - 1 + a) * f.Nu + (j0 - 1));
                double rr = 0, ri = 0;
                for (int b = 0; b < 4; b++) { rr += cu[b] * f.E[row + 2 * b]; ri += cu[b] * f.E[row + 2 * b + 1]; }
                re += cv[a] * rr; im += cv[a] * ri;
            }
            return 2;
        }
        if (f.InterpV == 0)
        {
            int i = wi < 0.5 ? i0 : i0 + 1; long a = 2L * ((long)i * f.Nu + j0);
            re = f.E[a] * (1 - wj) + f.E[a + 2] * wj; im = f.E[a + 1] * (1 - wj) + f.E[a + 3] * wj;
        }
        else
        {
            long a = 2L * ((long)i0 * f.Nu + j0), b = a + 2L * f.Nu;
            double w00 = (1 - wi) * (1 - wj), w01 = (1 - wi) * wj, w10 = wi * (1 - wj), w11 = wi * wj;
            re = f.E[a] * w00 + f.E[a + 2] * w01 + f.E[b] * w10 + f.E[b + 2] * w11;
            im = f.E[a + 1] * w00 + f.E[a + 3] * w01 + f.E[b + 1] * w10 + f.E[b + 3] * w11;
        }
        return 2;
    }

    static double Taper;                           // --taper mm: cos^2 amplitude ramp at block edges (apodization)
    static double TaperWeight(Field f, double v, double u)
    {
        if (Taper <= 0) return 1;
        double d = Math.Min(f.HalfV - Math.Abs(v), f.HalfU - Math.Abs(u));
        if (d >= Taper) return 1;
        double s = Math.Sin(0.5 * Math.PI * Math.Max(d, 0) / Taper); return s * s;
    }

    static void CatmullRom(double t, Span<double> w)
    {
        double t2 = t * t, t3 = t2 * t;
        w[0] = -0.5 * t3 + t2 - 0.5 * t; w[1] = 1.5 * t3 - 2.5 * t2 + 1;
        w[2] = -1.5 * t3 + 2 * t2 + 0.5 * t; w[3] = 0.5 * t3 - 0.5 * t2;
    }

    public static bool ComputeS(Field[] fields, double x, double y, out double sr, out double si)
    {
        sr = si = 0; bool hit = false;
        foreach (var f in fields)
        {
            if (f.Lambda <= 0) continue;
            int k = Eval(f, x, y, out double re, out double im, out double v, out double uu);
            if (k == 0) continue; hit = true; if (k == 1) continue;
            double tw = TaperWeight(f, v, uu); re *= tw; im *= tw;
            double ph = 2 * Math.PI * v / f.Lambda, c = Math.Cos(ph), s = Math.Sin(ph);
            sr += re * c + im * s; si += re * s - im * c;
        }
        return hit;
    }

    // Double-phase with frequency multiplexing: every carrier field covering the point is summed,
    // S = sum conj(e_c) exp(i 2 pi v / L_c), then tracks alternate arg S -/+ acos|S| (50 % duty).
    static bool SampleSum(Field[] fields, double x, double y, out bool bright)
    {
        bright = false; double sr = 0, si = 0, v0 = 0, l0 = 0; bool hit = false;
        foreach (var f in fields)
        {
            if (f.Lambda <= 0) continue;
            int k = Eval(f, x, y, out double re, out double im, out double v, out double uu);
            if (k == 0) continue;
            if (!hit) { v0 = v; l0 = f.Lambda; hit = true; }
            if (k == 1) continue;
            double tw = TaperWeight(f, v, uu); re *= tw; im *= tw;
            double ph = 2 * Math.PI * v / f.Lambda, c = Math.Cos(ph), s = Math.Sin(ph);
            sr += re * c + im * s; si += re * s - im * c;                       // conj(e) * exp(i ph)
        }
        if (!hit) return false;
        double amp = Math.Sqrt(sr * sr + si * si), arg;
        if (amp < 1e-9) { amp = 0; arg = 2 * Math.PI * v0 / l0; } else arg = Math.Atan2(si, sr);
        double alpha = Math.Acos(Math.Min(amp, 1.0)) * (Parity(x, y) == 0 ? 1 : -1);
        bright = Math.Cos(arg - alpha) > 0;
        return true;
    }

    static byte Noise(double x, double y)
    {
        ulong h = (ulong)BitConverter.DoubleToInt64Bits(x * 7919.0 + y) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return (h & 1) == 0 ? (byte)0 : (byte)255;
    }

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "chan") return Chan.Run(args[1..]);
        var o = new Dictionary<string, string>();
        for (int i = 0; i + 1 < args.Length; i += 2) o[args[i]] = args[i + 1];
        if (!o.ContainsKey("--output") || !o.ContainsKey("--fields") || !o.ContainsKey("--inner") || !o.ContainsKey("--outer"))
        {
            Console.WriteLine("BdAmp --inner R --outer R [--sectors 12219392] [--pitch-linear a --pitch-quadratic b --pitch-cubic c]\n"
                + "      [--start-angle deg] [--max-radius mm] [--dark 9E] [--light 22] [--encoding lee|dp] [--combine first|sum] [--taper mm] [--steer-parity on] [--steer-verify N] [--opt-bits file.bdopt [--load-fields]] --fields a.bdaf[,b.bdaf...] --output out.iso");
            return 2;
        }
        double D(string k, double d) => o.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
        byte dark = Convert.FromHexString(o.GetValueOrDefault("--dark", "9E"))[0];
        byte light = Convert.FromHexString(o.GetValueOrDefault("--light", "22"))[0];
        uint total = o.TryGetValue("--sectors", out var st) ? uint.Parse(st, CultureInfo.InvariantCulture) : BluRayDiscParameters.SectorsPerLayer;
        var disc = new BluRayDiscParameters(total, 1, D("--inner", 24), D("--outer", 58), D("--start-angle", 0),
            D("--pitch-linear", 0), D("--pitch-quadratic", 0), D("--pitch-cubic", 0));
        disc.Validate();
        var geo = new BluRayTrackGeometry(disc);
        Console.WriteLine($"BD-R geometry: inner {disc.InnerRadiusMm} outer {disc.OuterRadiusMm} mm, {total} sectors, effective pitch {geo.EffectiveTrackPitchMicrometers:F4} um; bytes dark {dark:X2} light {light:X2}");
        if (o.TryGetValue("--max-radius", out var mr))
        {
            double rmax = double.Parse(mr, CultureInfo.InvariantCulture);
            uint lo = 32, hi = total;
            while (hi - lo > 32) { uint mid = (lo + hi) / 2 / 32 * 32; if (geo.AtSector(mid).RadiusMm < rmax) lo = mid; else hi = mid; }
            disc = disc with { UsableSectors = hi };
            Console.WriteLine($"Output stops at r = {geo.AtSector(hi).RadiusMm:F3} mm: {hi} sectors ({hi * 2048.0 / 1e9:F2} GB)");
        }
        if (o.TryGetValue("--geo-check", out var gc))
        {
            foreach (var t in gc.Split(','))
            {
                double sp = double.Parse(t, System.Globalization.CultureInfo.InvariantCulture); var pt = geo.AtSector(sp);
                Console.WriteLine(FormattableString.Invariant($"GEO {sp:R} {pt.RadiusMm:R} {pt.AngleRadians:R} {pt.Xmm:R} {pt.Ymm:R}"));
            }
            return 0;
        }
        string enc = o.GetValueOrDefault("--encoding", "lee");
        DoublePhase = enc == "dp";
        if (DoublePhase)
        {
            double offset = (disc.StartAngleDegrees % 360) * Math.PI / 180, inner = disc.InnerRadiusMm;
            Func<double, double> theta;
            if (disc.HasVariablePitch)
            {
                var dg = DiscImageStudio.Core.DvdTrackGeometry.Create(disc.InnerRadiusMm, disc.OuterRadiusMm, geo.TrackLengthPerLayerMm,
                    disc.PitchLinear, disc.PitchQuadratic, disc.PitchCubic);
                theta = r => dg.AngleAtRadius(r);
            }
            else { double g = geo.EffectiveTrackPitchMicrometers / (2 * Math.PI * 1000); theta = r => (r - inner) / g; }
            Parity = (x, y) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                long n = (long)Math.Round((theta(r) + Math.Atan2(y, x) - offset) / (2 * Math.PI));
                return (int)(n & 1);
            };
            // self-check: consecutive turns at the same angle must alternate
            int bad = 0;
            for (int k = 0; k < 2000; k++)
            {
                double sec = 100000 + k * 5000.37; var p0 = geo.AtSector(sec);
                double turn = 2 * Math.PI * p0.RadiusMm / (BluRayDiscParameters.ChannelBitLengthNm * 1e-6) / (BluRayDiscParameters.ChannelBitsPerRecordingUnit / 32.0);
                var p1 = geo.AtSector(sec + turn);
                if (Parity(p0.Xmm, p0.Ymm) == Parity(p1.Xmm, p1.Ymm)) bad++;
            }
            Console.WriteLine($"Encoding: double-phase (track parity self-check: {bad}/2000 non-alternating)");
        }
        else Console.WriteLine("Encoding: Lee (detour-phase, width = amplitude)");
        bool fastOpt = o.ContainsKey("--opt-bits") && !o.ContainsKey("--load-fields");
        HeaderOnly = fastOpt;
        if (fastOpt) Console.WriteLine("Fast path: optimized bytes supplied -> carrier field data not loaded (block geometry from headers; --load-fields to disable)");
        var fields = o["--fields"].Split(',').Select(s => Load(s.Trim())).ToArray();
        Func<double, double, bool> inCarrierBlock = (x, y) =>
        {
            foreach (var f in fields)
            {
                if (f.Lambda <= 0) continue;
                double dx = x - f.Cx, dy = y - f.Cy;
                if (dx * dx + dy * dy > f.Reach * f.Reach) continue;
                double v = dx * f.Ca + dy * f.Sa, u = -dx * f.Sa + dy * f.Ca;
                if (Math.Abs(v) <= f.HalfV && Math.Abs(u) <= f.HalfU) return true;
            }
            return false;
        };
        bool sumMode = o.GetValueOrDefault("--combine", "first") == "sum";
        if (sumMode && !DoublePhase) { Console.Error.WriteLine("--combine sum needs --encoding dp"); return 2; }
        if (sumMode) Console.WriteLine("Combine: overlapping carrier fields are summed (colour / frequency multiplexing)");
        Taper = D("--taper", 0);
        if (Taper > 0) Console.WriteLine($"Apodization: cos^2 edge taper {Taper} mm");
        // desired on-disc pattern: 0 / 255, or -1 = free (outside every block -> noise, usable for parity steering)
        Func<double, double, int> desired = (x, y) =>
        {
            if (fastOpt)
            {   // labels, then block membership only (values come from the optimized bytes)
                foreach (var f in fields)
                    if (f.Lambda <= 0 && Sample(f, x, y, out bool lb)) return lb ? 255 : 0;
                return inCarrierBlock(x, y) ? 0 : -1;
            }
            if (sumMode)
            {
                foreach (var f in fields)
                    if (f.Lambda <= 0 && Sample(f, x, y, out bool lb)) return lb ? 255 : 0;
                return SampleSum(fields, x, y, out bool sb) ? (sb ? 255 : 0) : -1;
            }
            foreach (var f in fields)
                if (Sample(f, x, y, out bool b)) return b ? 255 : 0;
            return -1;
        };
        Func<double, double, byte> sample = (x, y) => { int d = desired(x, y); return d < 0 ? Noise(x, y) : (byte)d; };
        bool useOpt = o.TryGetValue("--opt-bits", out var optPath);
        if (useOpt) { OptBits.Load(optPath!, dark, light); Steer.UseOpt = true; if (fastOpt && o.ContainsKey("--check-coverage")) OptBits.SetupCoverage(geo, inCarrierBlock); }
        BluRayClusterPostProcessor? post = null;
        if (useOpt || o.GetValueOrDefault("--steer-parity", "off") == "on")
        {
            Steer.Setup(disc, geo, desired, dark, light, (int)D("--steer-verify", 0));
            post = useOpt
                ? (d, fs, psn, cl) => { OptBits.Apply(fs, cl); Steer.Process(d, fs, psn, cl); }
                : Steer.Process;
            Console.WriteLine("LDC parity steering: on (parity bytes inside blocks are solved via free bytes outside blocks)");
        }
        var mapping = new BluRayImagePayloadMapping(disc, sample, new BluRayRunLengthMapping(dark, light), allowParallelSampling: true);
        if (o.TryGetValue("--bench", out var bn))
        {
            int nb = int.Parse(bn); var cf = fields.First(f => f.Lambda > 0);
            uint lo = 32, hi = disc.TotalSectors;
            while (hi - lo > 32) { uint mid = (lo + hi) / 2 / 32 * 32; if (geo.AtSector(mid).RadiusMm < cf.Rc) lo = mid; else hi = mid; }
            var buf = new byte[65536]; double tFill = 0, tSteer = 0; var swb = new Stopwatch();
            for (int c = 0; c < nb; c++)
            {
                uint fs = lo / 32 * 32 + (uint)c * 32 * 7; uint psn = disc.GetPhysicalSectorNumber(fs);
                swb.Restart();
                for (int s = 0; s < 32; s++) mapping.FillScrambledPayload(new(fs + (uint)s, psn + (uint)s, disc), buf.AsSpan(s * 2048, 2048));
                tFill += swb.Elapsed.TotalSeconds; swb.Restart();
                post?.Invoke(disc, fs, psn, buf);
                tSteer += swb.Elapsed.TotalSeconds;
            }
            Console.WriteLine($"BENCH r={cf.Rc:F2} clusters {nb}: fill {tFill / nb * 1000:F1} ms/cluster, steer {tSteer / nb * 1000:F1} ms/cluster");
            Steer.Report(); return 0;
        }
        if (o.TryGetValue("--range-start", out var rs))
        {   // raw user data for a sector range only (same code path as the ISO stream)
            uint r0 = uint.Parse(rs) / 32 * 32, rn = uint.Parse(o["--range-clusters"]) * 32;
            var swr = Stopwatch.StartNew();
            using (var fsw = File.Create(o["--output"]))
                BluRayStreamingGenerator.GenerateToStream(fsw, new BluRayStreamingOptions(disc, r0, rn) { ClusterPostProcessor = post }, mapping);
            Console.WriteLine($"Range: sectors {r0}..{r0 + rn} -> {o["--output"]} ({swr.Elapsed.TotalSeconds:F0}s)");
            if (post != null) Steer.Report();
            if (useOpt) OptBits.Report();
            return 0;
        }
        var plan = BluRayIsoGenerator.PrepareDrawing(disc);   // no file system: the whole disc is pattern
        var output = o["--output"];
        if (File.Exists(output)) { Console.Error.WriteLine("Output exists: " + output); return 1; }
        var sw = Stopwatch.StartNew(); var next = TimeSpan.Zero;
        var prog = new Progress<BluRayStreamingProgress>(v =>
        {
            if (v.Elapsed < next && v.CompletedBytes != v.TotalBytes) return;
            next = v.Elapsed + TimeSpan.FromSeconds(10);
            Console.WriteLine($"{v.Fraction:P1}  {v.Elapsed.TotalSeconds:F0}s");
        });
        var sum = BluRayStreamingGenerator.GenerateFile(output, new BluRayStreamingOptions(disc, 0, plan.TotalSectors) { ClusterPostProcessor = post }, mapping, prog);
        Console.WriteLine($"Done: {output} ({sum.BytesWritten:N0} bytes, {sw.Elapsed.TotalSeconds:F0}s)");
        if (post != null) Steer.Report();
        if (useOpt) OptBits.Report();
        Console.WriteLine("Burn with ImgBurn 'Write image file to disc' on BD-R, low speed.");
        return 0;
    }
}
