// Lattice export / import for external (MATLAB) hologram optimisation.
//
// export: enumerates every channel codeword inside a square patch, in the patch-local frame
//   (ur = radial outward, ut = +tangential, mm from the patch centre), using the calibrated spiral.
//   Codewords the solver can set ("controllable", user data) come straight from the writer; the gaps
//   between them on the same track (sync, IDs, ECC parity) are filled by interpolation and flagged 0.
//   File "DVDLAT1": header, then column arrays idx(uint64) track(int32) ur(single) ut(single) ctrl(uint8).
// import: values file = one uint8 per record (the scrambled byte Q to write; ignored where ctrl = 0).
using System.Buffers.Binary;
using System.Diagnostics;
using DiscImageStudio.Core;
using DvdImageSolver.Solver;

public static class Lattice
{
    const string Magic = "DVDLAT1\0";

    public static int Export(string path, MultiBlockSolveOptions baseOpt, double patchR, double patchAngleDeg, double size,
        uint sectors, double inner, double outer, double lin, double quad, double cub, int par)
    {
        double half = size / 2, a = patchAngleDeg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
        double cx = patchR * ca, cy = patchR * sa;
        double sectorMm = 38688 * 133.3e-6;
        var geo = DvdTrackGeometry.Create(inner, outer, sectors * sectorMm, lin, quad, cub);
        double rMin = patchR - half * Math.Sqrt(2) - 0.05, rMax = patchR + half * Math.Sqrt(2) + 0.05;
        uint lba0 = (uint)(geo.ArcLengthAtRadius(rMin) / sectorMm) / 16 * 16;
        uint lba1 = ((uint)(geo.ArcLengthAtRadius(rMax) / sectorMm) / 16 + 1) * 16;
        Console.WriteLine($"Scanning LBA {lba0}..{lba1} ({lba1 - lba0} sectors) for a {size} mm patch at r={patchR} mm, {patchAngleDeg} deg");
        var opt = RingsTool.CreateOptions(sectors, inner, outer, lin, quad, cub, lba0, lba1 - lba0, par);
        var bag = new System.Collections.Concurrent.ConcurrentBag<List<(ulong I, float R, float T)>>();
        var local = new ThreadLocal<List<(ulong, float, float)>>(() => { var l = new List<(ulong, float, float)>(); bag.Add(l); return l; });
        var sw = Stopwatch.StartNew();
        FastDispersionImageWriter.WriteScrambledCodewordPatternStream(s =>
        {
            double dx = s.Xmm - cx, dy = s.Ymm - cy;
            double ur = dx * ca + dy * sa, ut = -dx * sa + dy * ca;
            if (Math.Abs(ur) <= half && Math.Abs(ut) <= half) local.Value!.Add((s.GlobalCodewordIndex, (float)ur, (float)ut));
            return 0;
        }, Stream.Null, opt, pr => { if (pr.CompletedBlocks % 4096 == 0) Console.WriteLine($"  {pr.Fraction:P0} {pr.Elapsed.TotalSeconds:F0}s"); });
        var all = bag.SelectMany(l => l).ToArray();
        Array.Sort(all, (p, q) => p.I.CompareTo(q.I));
        Console.WriteLine($"{all.Length:N0} controllable codewords in {sw.Elapsed.TotalSeconds:F0}s");

        // tracks = runs of nearby indices; fill small gaps (uncontrollable slots) by interpolation
        var idx = new List<ulong>(all.Length + all.Length / 5); var trk = new List<int>(); var urs = new List<float>(); var uts = new List<float>(); var ctl = new List<byte>();
        int track = 0;
        for (int n = 0; n < all.Length; n++)
        {
            if (n > 0)
            {
                ulong gap = all[n].I - all[n - 1].I;
                if (gap > 5000) track++;
                else
                    for (ulong g = 1; g < gap; g++)
                    {
                        float f = (float)g / gap;
                        idx.Add(all[n - 1].I + g); trk.Add(track);
                        urs.Add(all[n - 1].R + f * (all[n].R - all[n - 1].R)); uts.Add(all[n - 1].T + f * (all[n].T - all[n - 1].T)); ctl.Add(0);
                    }
            }
            idx.Add(all[n].I); trk.Add(track); urs.Add(all[n].R); uts.Add(all[n].T); ctl.Add(1);
        }
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes(Magic));
            w.Write(1);                               // version
            w.Write(patchR); w.Write(patchAngleDeg); w.Write(size);
            w.Write(inner); w.Write(outer); w.Write(lin); w.Write(quad); w.Write(cub); w.Write((double)sectors);
            w.Write((ulong)idx.Count); w.Write(track + 1);
            foreach (var v in idx) w.Write(v);
            foreach (var v in trk) w.Write(v);
            foreach (var v in urs) w.Write(v);
            foreach (var v in uts) w.Write(v);
            foreach (var v in ctl) w.Write(v);
        }
        Console.WriteLine($"Wrote {path}: {idx.Count:N0} codewords ({ctl.Count(c => c == 0):N0} uncontrollable) on {track + 1} tracks");
        return 0;
    }

    public sealed class Loaded
    {
        public double R, AngleDeg, Size; public ulong[] Idx = []; public byte[] Ctrl = []; public byte[] Q = [];
    }

    public static Loaded Load(string latticePath, string valuesPath)
    {
        using var r = new BinaryReader(File.OpenRead(latticePath));
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(8)) != Magic) throw new InvalidDataException("Not a DVDLAT1 file");
        r.ReadInt32();
        var L = new Loaded { R = r.ReadDouble(), AngleDeg = r.ReadDouble(), Size = r.ReadDouble() };
        for (int i = 0; i < 6; i++) r.ReadDouble();
        int n = checked((int)r.ReadUInt64()); r.ReadInt32();
        L.Idx = new ulong[n];
        for (int i = 0; i < n; i++) L.Idx[i] = r.ReadUInt64();
        r.BaseStream.Seek((long)n * (4 + 4 + 4), SeekOrigin.Current);
        L.Ctrl = r.ReadBytes(n);
        L.Q = File.ReadAllBytes(valuesPath);
        if (L.Q.Length != n) throw new InvalidDataException($"values file has {L.Q.Length} bytes, lattice has {n} records");
        Console.WriteLine($"Lattice patch r={L.R} mm @ {L.AngleDeg} deg, {L.Size} mm, {n:N0} records");
        return L;
    }

    public static Func<DvdCodewordSample, byte> Sampler(Loaded[] Ls)
    {
        var P = Ls.Select(L => { double a = L.AngleDeg * Math.PI / 180; return (L, ca: Math.Cos(a), sa: Math.Sin(a), cx: L.R * Math.Cos(a), cy: L.R * Math.Sin(a), half: L.Size / 2 + 0.01); }).ToArray();
        return s =>
        {
            foreach (var p in P)
            {
                double dx = s.Xmm - p.cx, dy = s.Ymm - p.cy;
                double ur = dx * p.ca + dy * p.sa, ut = -dx * p.sa + dy * p.ca;
                if (Math.Abs(ur) <= p.half && Math.Abs(ut) <= p.half)
                {
                    int k = Array.BinarySearch(p.L.Idx, s.GlobalCodewordIndex);
                    if (k >= 0 && p.L.Ctrl[k] == 1) return p.L.Q[k];
                }
            }
            ulong h = (ulong)BitConverter.DoubleToInt64Bits(s.Xmm * 7919.0 + s.Ymm) * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
            return (byte)h;
        };
    }
}

// Palette measurement disc: radial gratings (16 tracks byte A / 16 tracks byte B, period ~23.6 um) in rings.
// First-order / zero-order power of each ring gives |cA - cB| / |cA + cB| for the effective reflectances.
public static class PaletteProbe
{
    public static readonly (double R0, double R1, byte[] A, byte[]? B, string Label)[] Rings =
    [
        (24.6, 27.6, [0x92], [0xA5], "P1 92 / A5"),
        (28.0, 31.0, [0x92], null,   "P2 92 / random"),
        (31.4, 34.4, [0xA5], null,   "P3 A5 / random"),
        (34.8, 37.8, [0x5F], null,   "P4 5F / random"),
        (38.2, 41.2, [0xC5, 0xDB], null, "P5 C5DB / random"),
        (41.6, 44.6, [0xF9], null,   "P6 F9 / random"),
        (45.0, 48.0, [0xCC, 0x67], null, "P7 CC67 / random"),
        (48.4, 51.4, [0x6F], null,   "P8 6F / random"),
        (51.8, 54.8, [0xDD, 0xB6], null, "P9 DDB6 / random"),
        (55.2, 57.4, [0x92], [0x92], "P10 92 only (no grating, reference)"),
    ];

    public static byte Sample(DvdCodewordSample s)
    {
        double r = Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm);
        foreach (var ring in Rings)
        {
            if (r < ring.R0 || r >= ring.R1) continue;
            long turn = (long)Math.Floor(s.TrackAngleRadians / (2 * Math.PI));
            byte[]? seq = (turn & 31) < 16 ? ring.A : ring.B;
            return seq is null ? Noise(s) : seq[(int)(s.GlobalCodewordIndex % (ulong)seq.Length)];
        }
        return Noise(s);
    }

    static byte Noise(DvdCodewordSample s)
    {
        ulong h = (ulong)BitConverter.DoubleToInt64Bits(s.Xmm * 7919.0 + s.Ymm) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return (byte)h;
    }
}
