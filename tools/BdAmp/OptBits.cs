// BDOPT1 reader (output of gpu_opt/bdopt.py): per cluster, optimized on-disc values for payload bytes
// (sector*2048+index, scrambled domain) and LDC parity bytes (column*32+j) inside hologram blocks.
using DiscImageStudio.BluRay;

static class OptBits
{
    sealed class Rec { public byte[] PF = [], PV = [], QF = [], QV = []; }
    static readonly Dictionary<uint, Rec> Map = new();
    static byte Dark, Light; static long applied, clustersHit, uncovered;
    static BluRayTrackGeometry? Geo; static Func<double, double, bool>? InBlock;
    public static void SetupCoverage(BluRayTrackGeometry geo, Func<double, double, bool> inBlock) { Geo = geo; InBlock = inBlock; }

    public static void Load(string paths, byte dark, byte light)
    {
        foreach (var p in paths.Split(',')) LoadOne(p.Trim(), dark, light);
    }
    static void LoadOne(string path, byte dark, byte light)
    {
        Dark = dark; Light = light;
        using var r = new BinaryReader(File.OpenRead(path));
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(8)) != "BDOPT1\0\0") throw new InvalidDataException(path + ": not BDOPT1");
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            uint cl = r.ReadUInt32();
            var rec = new Rec { PF = r.ReadBytes(8192), PV = r.ReadBytes(8192), QF = r.ReadBytes(1216), QV = r.ReadBytes(1216) };
            if (Map.TryGetValue(cl, out var old))
            {   // a cluster can cross two blocks (or radial bands): merge, later record wins where both are set
                Merge(old.PF, old.PV, rec.PF, rec.PV); Merge(old.QF, old.QV, rec.QF, rec.QV);
            }
            else Map[cl] = rec;
        }
        Console.WriteLine($"Optimized bytes: {path} ({n} records, {Map.Count} clusters)");
    }

    static void Merge(byte[] f, byte[] v, byte[] nf, byte[] nv)
    {
        for (int i = 0; i < f.Length; i++) { v[i] = (byte)((v[i] & ~nf[i]) | (nv[i] & nf[i])); f[i] |= nf[i]; }
    }

    static bool Bit(byte[] a, int i) => (a[i >> 3] >> (i & 7) & 1) != 0;

    public static void Apply(uint firstSector, Span<byte> cluster)
    {
        if (!Map.TryGetValue(firstSector / 32, out var rec)) return;
        Interlocked.Increment(ref clustersHit); long k = 0;
        long miss = 0; double clusterBase = (long)(firstSector / 32) * BluRayDiscParameters.ChannelBitsPerRecordingUnit;
        for (int i = 0; i < 65536; i++)
        {
            if (Bit(rec.PF, i)) { cluster[i] = Bit(rec.PV, i) ? Light : Dark; k++; continue; }
            if (InBlock == null) continue;
            // fast path: an in-block byte without an optimized value keeps the placeholder (dark) -> count it
            int sector = i >> 11, idx = i & 2047, serial = sector * 2052 + idx;
            double off = Steer.ChannelOffsetPublic(serial / 216, serial % 216);
            var p = Geo!.AtSector((clusterBase + off) / (BluRayDiscParameters.ChannelBitsPerRecordingUnit / 32.0));
            if (InBlock(p.Xmm, p.Ymm)) miss++;
        }
        Interlocked.Add(ref applied, k); Interlocked.Add(ref uncovered, miss);
    }

    // -1 = no override, else desired parity byte value
    public static int Parity(uint firstSector, int column, int j)
    {
        if (!Map.TryGetValue(firstSector / 32, out var rec)) return -1;
        int i = column * 32 + j;
        return Bit(rec.QF, i) ? (Bit(rec.QV, i) ? Light : Dark) : -1;
    }

    public static void Report() => Console.WriteLine($"Optimized bytes applied: {applied} payload bytes in {clustersHit} clusters" +
        (InBlock != null ? $"; in-block bytes without an optimized value (left dark): {uncovered}" : ""));
}
