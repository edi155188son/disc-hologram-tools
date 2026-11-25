// BdCgh - true-wavefront (horizontal-parallax) computer-generated holograms on BD-R (single layer).
// Each recording frame is designed bit by bit (17PP + NRZI simulated exactly) so the channel follows
// Re{object wave x carrier}; the running DSV is steered so a DSV-minimising recorder makes the
// designed dc-control decisions (phase known over ~mm instead of ~10 um).
using System.Diagnostics;
using System.Globalization;
using DiscImageStudio.BluRay;
using DiscImageStudio.BluRay.Encoding;

var a = args;
string Opt(string k, string d) { int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
double D(string k, double d) => double.Parse(Opt(k, d.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
string assets = Opt("--assets", "");
string output = Opt("--output", "");
int threads = (int)D("--threads", Environment.ProcessorCount);
int verify = (int)D("--verify", 0); ClusterWriter.dump = a.Contains("--dump");
int width = (int)D("--width", 8);
bool dry = a.Contains("--dry");
if (assets == "") { Console.WriteLine("BdCgh --assets <dir with texture_rgba.png depth8.png white.png flat_depth.png lines.png lines_depth.png> --output bd.iso [--threads N] [--verify K] [--width 8] [--dry]"); return 1; }

var disc = new BluRayDiscParameters((uint)D("--sectors", 12219392), 1, D("--inner", 23.987042157339335), D("--outer", 57.987960341783236));
var geo = new BluRayTrackGeometry(disc);
Console.WriteLine($"BD-R geometry: inner {disc.InnerRadiusMm} outer {disc.OuterRadiusMm} mm, pitch {geo.EffectiveTrackPitchMicrometers:F4} um");

var patches = TestSet.Build(assets, a);
double rMaxNeed = patches.Max(p => p.H.RMax) + 0.2;
uint lo = 32, hi = disc.TotalSectors;
while (hi - lo > 32) { uint mid = (lo + hi) / 2 / 32 * 32; if (geo.AtSector(mid).RadiusMm < rMaxNeed) lo = mid; else hi = mid; }
uint outSectors = Math.Max(hi, (uint)(D("--min-gb", 8.6) * 1e9 / 2048 / 32 + 1) * 32);
Console.WriteLine($"Image area ends at sector {hi} (r {geo.AtSector(hi).RadiusMm:F2} mm); ISO {outSectors} sectors ({outSectors * 2048.0 / 1e9:F2} GB, zero user data beyond)");

var writer = new ClusterWriter(disc, geo, patches) { Width = width };
if (verify > 0) { writer.Verify(verify, D("--verify-r", patches[0].H.Rc), D("--verify-angle", patches[0].H.Theta0 * 180 / Math.PI)); return 0; }
if (dry) { writer.Estimate(); return 0; }
if (output == "" || File.Exists(output)) { Console.Error.WriteLine("need a new --output path"); return 1; }

var sw = Stopwatch.StartNew();
uint clusters = outSectors / 32, designEnd = hi / 32;
long designed = 0;
using (var fs = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 22))
{
    int batch = Math.Max(1, threads * 2);
    var bufs = new byte[batch][];
    for (int i = 0; i < batch; i++) bufs[i] = new byte[65536];
    var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
    var last = TimeSpan.Zero;
    for (uint c0 = 0; c0 < clusters; c0 += (uint)batch)
    {
        int cnt = (int)Math.Min(batch, clusters - c0);
        Parallel.For(0, cnt, po, i =>
        {
            uint c = c0 + (uint)i;
            if (c < designEnd && writer.FillCluster(c, bufs[i])) Interlocked.Increment(ref designed);
            else Array.Clear(bufs[i]);
        });
        for (int i = 0; i < cnt; i++) fs.Write(bufs[i]);
        if (sw.Elapsed - last > TimeSpan.FromSeconds(20) || c0 + (uint)cnt >= clusters)
        {
            last = sw.Elapsed;
            Console.WriteLine($"{100.0 * (c0 + cnt) / clusters:F1}%  cluster {c0 + cnt}/{clusters}  designed {designed}  frames {writer.FramesDesigned:N0}  {sw.Elapsed.TotalMinutes:F1} min");
        }
    }
}
Console.WriteLine($"Done: {output} ({new FileInfo(output).Length:N0} bytes, {sw.Elapsed.TotalMinutes:F1} min)");
Console.WriteLine("Burn with ImgBurn 'Write image file to disc' on BD-R, low speed.");
return 0;

public sealed record Patch(string Label, Hologram H, bool Steer, double GratingPeriodBits = 0);

static class TestSet
{
    public static List<Patch> Build(string dir, string[] args)
    {
        string P(string f) => Path.Combine(dir, f);
        Opts Base(double angle, string tex, string depth, double z, bool diffuse, double width = 12, double half = 7)
        {
            var o = new Opts
            {
                Texture = P(tex), Depth = P(depth), WidthMm = width, CenterRadius = 31, CenterAngleDeg = angle, Copies = 1,
                NearMm = z, FarMm = -z, Diffuse = diffuse, AlphaDeg = 38, HalfDeg = half, LambdaNm = 550,
                DarkPeriodUm = 4.5 * 0.0745,
            };
            return o;
        }
        // seven 12 mm patches round r = 31 mm (canvas deg, -90 = 12 o'clock)
        var list = new List<Patch>
        {
            new("B1 12:00  carrier grating 12T (0.894 um, 38 deg), DSV-steered", new Hologram(Base(-90, "white.png", "flat_depth.png", 0, false, 8)), true),
            new("B2 01:43  carrier grating 12T, NOT steered (dc-control left to the drive)", new Hologram(Base(-38.6, "white.png", "flat_depth.png", 0, false, 8)), false),
            new("B3 03:26  avatar 12 mm, depth +/-6 mm, smooth, steered", new Hologram(Base(12.9, "texture_rgba.png", "depth8.png", 6, false)), true),
            new("B4 05:09  avatar 12 mm, depth +/-6 mm, smooth, NOT steered", new Hologram(Base(64.3, "texture_rgba.png", "depth8.png", 6, false)), false),
            new("B5 06:51  two lines, depth +/-9 mm, steered", new Hologram(Base(115.7, "lines.png", "lines_depth.png", 9, false)), true),
            new("B6 08:34  avatar 12 mm, depth +/-6 mm, diffuse, steered", new Hologram(Base(167.1, "texture_rgba.png", "depth8.png", 6, true)), true),
            new("B7 10:17  avatar 12 mm, depth +/-3 mm, smooth, steered", new Hologram(Base(218.6, "texture_rgba.png", "depth8.png", 3, false)), true),
        };
        foreach (var p in list) Console.WriteLine("  " + p.Label);
        return list;
    }
}

public sealed class ClusterWriter(BluRayDiscParameters disc, BluRayTrackGeometry geo, List<Patch> patches)
{
    const int RU = BluRayDiscParameters.ChannelBitsPerRecordingUnit;
    const double BitUm = 0.0745;
    public int Width = 8;
    public static bool dump;
    public long FramesDesigned;
    readonly ThreadLocal<Designer> _des = new(() => new Designer());
    readonly double _rMin = patches.Min(p => p.H.RMin) - 0.2, _rMax = patches.Max(p => p.H.RMax) + 0.2;

    (double X, double Y) Pos(uint cluster, double offset)
    {
        var t = geo.AtSector(((long)cluster * RU + offset) / (RU / 32.0));
        return (t.Xmm, t.Ymm);
    }

    // inverse LDC/BIS mapping: frame f (<432), ecc column e (not BIS) -> scrambled payload index, or -1 for EDC
    static int PayloadIndex(int f, int e)
    {
        int g = e / 39, p = e % 39; if (p == 38) return -2;     // BIS
        int j = g * 38 + p, row = f / 2;
        int c2 = (j + (row * 3) % 152) % 152, column = 2 * c2 + f % 2;
        int serial = column * 216 + row, sector = serial / 2052, off = serial % 2052;
        return off >= 2048 ? -1 : sector * 2048 + off;
    }

    Patch Which(double x, double y, out int row, out double s)
    {
        row = -1; s = 0;
        foreach (var p in patches) if (p.H.Locate(x, y, out row, out s)) return p;
        return null;
    }

    bool Touches(uint cluster)
    {
        for (int f = 0; f < 432; f += 4)
        {
            var (x, y) = Pos(cluster, 2760 + f * 1932);
            double r = Math.Sqrt(x * x + y * y);
            if (r < _rMin || r > _rMax) return false;
            for (int q = 0; q < 2; q++) { var (xx, yy) = Pos(cluster, 2760 + f * 1932 + q * 1932 * 2); if (Which(xx, yy, out _, out _) != null) return true; }
        }
        return false;
    }

    /// <summary>Fills the 65536-byte user-data cluster (unscrambled). Returns false (zeros) if no patch.</summary>
    public bool FillCluster(uint cluster, byte[] user) => FillCluster(cluster, user, null);

    public bool FillCluster(uint cluster, byte[] user, List<byte[]> designLevels)
    {
        if (!Touches(cluster)) { Array.Clear(user); return false; }
        uint psn = disc.GetPhysicalSectorNumber(cluster * 32);
        var mask = BluRayScrambler.CreateMask(psn, 2048);
        var payload = new byte[65536];                       // scrambled domain; default = random-looking (user 0)
        for (int s = 0; s < 32; s++) Buffer.BlockCopy(mask, 0, payload, s * 2048, 2048);
        var bis = BluRayEccEncoder.EncodeBisCluster(psn);
        var des = _des.Value; des.Width = Width;
        var gain = new float[1902];
        var fixedBits = new sbyte[1268];
        byte level = 0; long dsv = 0; bool term = false;
        var ecc = new byte[155];
        for (int f = 0; f < 432; f++)
        {
            // current ecc bytes (undesigned: from payload; EDC unknown -> 0; BIS known)
            for (int e = 0; e < 155; e++)
            {
                int pi = PayloadIndex(f, e);
                ecc[e] = pi >= 0 ? payload[pi] : pi == -2 ? bis[f * 3 + e / 39] : (byte)0;
            }
            bool any = Gains(cluster, f, gain, out bool steer);
            byte[] dcBits;
            if (any)
            {
                Array.Fill(fixedBits, (sbyte)-1);
                int bit = 0;
                for (int i = 0; i < 1268; i++)
                {
                    if (Designer.IsDc(i)) { fixedBits[i] = 0; continue; }
                    int col = bit / 8;
                    if (col % 39 == 38) fixedBits[i] = (sbyte)((ecc[col] >> (7 - bit % 8)) & 1);
                    bit++;
                }
                var (de, dd) = des.Design(gain, fixedBits, f % 31, term, level, dsv, steer);
                for (int e = 0; e < 155; e++) { int pi = PayloadIndex(f, e); if (pi >= 0) payload[pi] = de[e]; }
                Array.Copy(de, ecc, 155);
                dcBits = dd;
                Interlocked.Increment(ref FramesDesigned);
            }
            else dcBits = BluRayGreedyDcControlEncoder.Encode(ecc, f % 31, default, term, level, dsv).DcControlBits;
            var r = BluRayRecordingFrameEncoder.Encode(ecc, f % 31, dcBits, term, level);
            designLevels?.Add(any && steer ? r.ChannelLevels : null);
            term = r.UsedTermination; level = r.FinalChannelLevel; dsv += r.DigitalSum;
        }
        for (int i = 0; i < 65536; i++) user[i] = (byte)(payload[i] ^ mask[i % 2048]);
        return true;
    }

    static ulong Mix(ulong x) { x += 0x9E3779B97F4A7C15UL; x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL; x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL; return x ^ (x >> 31); }

    /// <summary>Per-channel-bit gains of data frame f. Returns false if the frame touches no patch.</summary>
    bool Gains(uint cluster, int f, float[] gain, out bool steer)
    {
        steer = true;
        double baseOff = 2760 + f * 1932 + 30;
        var (x0, y0) = Pos(cluster, baseOff);
        var (x1, y1) = Pos(cluster, baseOff + 1902);
        bool any = false; int steerVotes = 0, votes = 0;
        const int Seg = 16;
        for (int s0 = 0; s0 < 1902; s0 += Seg)
        {
            int len = Math.Min(Seg, 1902 - s0);
            double ta = (s0 + 0.5 * len) / 1902.0;
            double xm = x0 + (x1 - x0) * ta, ym = y0 + (y1 - y0) * ta;
            var p = Which(xm, ym, out int row, out double sMid);
            Hologram.Row rw = p is null ? null : p.H.GetRow(row);
            bool image = false;
            if (p is not null)
            {
                any = true; votes++; if (p.Steer) steerVotes++;
                if (rw is not null)
                {
                    // arc direction: s along the track; derive ds per bit from a second point
                    double tb = (s0 + 0.5 * len + 8) / 1902.0;
                    p.H.Locate(x0 + (x1 - x0) * tb, y0 + (y1 - y0) * tb, out _, out double sB);
                    double dsBit = (sB - sMid) / 8.0;
                    var (er, ei) = Hologram.Envelope(rw, sMid);
                    double mag = Math.Sqrt(er * er + ei * ei);
                    double u = (Mix(((ulong)cluster << 32) ^ (ulong)(f * 2048 + s0)) >> 11) * (1.0 / (1UL << 53));
                    if (mag >= p.H.DarkThreshold && u < mag / p.H.FullAmplitude)
                    {
                        image = true;
                        for (int t = 0; t < len; t++)
                        {
                            double st = sMid + (t - 0.5 * len + 0.5) * dsBit;
                            var (aa, bb) = Hologram.Envelope(rw, st);
                            double m = Math.Sqrt(aa * aa + bb * bb) + 1e-9;
                            var (sn, cs) = Math.SinCos(p.H.K * p.H.SinAlpha * st);
                            gain[s0 + t] = (float)(0.5 * (aa * cs - bb * sn) / m);
                        }
                    }
                }
            }
            if (!image)
            {
                double darkK = 2 * Math.PI / (4.5 * BitUm);
                for (int t = 0; t < len; t++) gain[s0 + t] = (float)(0.5 * Math.Cos(darkK * BitUm * (s0 + t)));
            }
        }
        steer = votes == 0 || steerVotes * 2 >= votes;
        return any;
    }

    public void Estimate()
    {
        long touched = 0; uint end = 0;
        for (uint c = 0; c < disc.TotalSectors / 32; c += 7) { var (x, y) = Pos(c, 0); double r = Math.Sqrt(x * x + y * y); if (r > _rMax) { end = c; break; } if (r >= _rMin && Touches(c)) touched++; }
        Console.WriteLine($"clusters touching patches (sampled 1/7, scaled): {touched * 7:N0}; image area ends near cluster {end}");
        var sw = Stopwatch.StartNew(); var buf = new byte[65536]; int n = 0;
        for (uint c = end / 2; n < 2 && c < end; c++) if (FillCluster(c, buf)) n++;
        Console.WriteLine($"single-thread time per designed cluster ~{sw.Elapsed.TotalSeconds / Math.Max(1, n):F1} s, frames designed {FramesDesigned}");
    }

    /// <summary>Re-encodes designed clusters with DiscImageStudio's full BD-R recording-unit encoder
    /// (real EDC, BIS, LDC parity, greedy dc control) and compares the channel with the design.</summary>
    public void Verify(int count, double r, double angleDeg)
    {
        // find a cluster near (r, angle)
        double tx = r * Math.Cos(angleDeg * Math.PI / 180), ty = r * Math.Sin(angleDeg * Math.PI / 180);
        uint lo = 0, hi = disc.TotalSectors / 32;
        while (hi - lo > 1) { uint mid = (lo + hi) / 2; var (x, y) = Pos(mid, 0); if (Math.Sqrt(x * x + y * y) < r) lo = mid; else hi = mid; }
        int done = 0;
        for (uint c = lo; done < count && c < lo + 20000; c++)
        {
            var user = new byte[65536]; var lv = new List<byte[]>();
            var sw = Stopwatch.StartNew();
            if (!FillCluster(c, user, lv)) continue;
            double t = sw.Elapsed.TotalSeconds;
            uint psn = disc.GetPhysicalSectorNumber(c * 32);
            var ru = BluRayRecordingUnitEncoder.Encode(user, psn, appendGuard3: false);
            long blocks = 0, agree = 0, flips = 0, nbits = 0; int prev = -1;
            double winSum = 0; int wins = 0; double wAcc = 0, wAbs = 0; int wCnt = 0;
            int dumpRows = 0; var rowStr = new System.Text.StringBuilder();
            for (int f = 0; f < 432; f++)
            {
                if (rowStr.Length > 0 && dumpRows < 400 && done == 0 && dump) { Console.WriteLine($"f{f - 1,3} {rowStr}"); dumpRows++; }
                rowStr.Clear();
                if (lv[f] is null) { prev = -1; continue; }
                for (int b0 = 30; b0 + 69 <= 1932; b0 += 69)
                {
                    int same = 0;
                    for (int i = b0; i < b0 + 69; i++) if (ru.ChannelLevels[2760 + f * 1932 + i] == lv[f][i]) same++;
                    double corr = (2.0 * same - 69) / 69;
                    int sgn = corr >= 0 ? 1 : 0;
                    blocks++; if (Math.Abs(corr) > 0.9) agree++;
                    if (prev >= 0 && sgn != prev) flips++; prev = sgn;
                    nbits += 69;
                    if (dumpRows < 400) rowStr.Append(Math.Abs(corr) < 0.9 ? 'x' : corr > 0 ? '+' : '-');
                    wAcc += corr; wAbs += Math.Abs(corr); wCnt++;
                    if (wCnt == 39) { winSum += Math.Abs(wAcc) / wAbs; wins++; wAcc = wAbs = 0; wCnt = 0; }
                }
            }
            Console.WriteLine($"cluster {c}: design {t:F1}s, designed blocks {blocks}, |corr|>0.9 {100.0 * agree / Math.Max(1, blocks):F1}%, polarity flips/mm {flips / (nbits * BitUm * 1e-3 + 1e-9):F2}, 0.2mm-window coherence {winSum / Math.Max(1, wins):F3}, greedy-suboptimal {ru.SuboptimalDcControlDecisions}");
            done++;
        }
    }
}
