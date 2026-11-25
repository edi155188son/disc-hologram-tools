// chan mode: user data (ISO sectors) -> DiscImageStudio ECC + 17PP recording-unit encoder -> channel levels.
// Output BDCH1: header (start sector, clusters, channel bits per RU), then per cluster packed levels (MSB first) + ECC cluster bytes.
using System.Globalization;
using DiscImageStudio.BluRay;
using DiscImageStudio.BluRay.Encoding;

static class Chan
{
    public static int Run(string[] args)
    {
        var o = new Dictionary<string, string>();
        for (int i = 0; i + 1 < args.Length; i += 2) o[args[i]] = args[i + 1];
        double D(string k, double d) => o.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
        uint total = o.TryGetValue("--sectors", out var st) ? uint.Parse(st) : BluRayDiscParameters.SectorsPerLayer;
        var disc = new BluRayDiscParameters(total, 1, D("--inner", 24), D("--outer", 58), D("--start-angle", 0), 0, 0, 0);
        uint start = uint.Parse(o["--start-sector"]);
        var data = o.ContainsKey("--data") ? File.ReadAllBytes(o["--data"]) : [];
        if (o.TryGetValue("--data-offset-sector", out var dos))
        {   // read a slice of a big file (an ISO): sectors [start, start + clusters*32)
            int ncl0 = int.Parse(o["--clusters"]);
            data = new byte[ncl0 * 65536];
            using var fsr = File.OpenRead(o["--data-file"]);
            fsr.Seek((long)uint.Parse(dos) * 2048, SeekOrigin.Begin); fsr.ReadExactly(data);
        }
        if (o.TryGetValue("--fill-scrambled", out var fsv))
        {   // synthetic: every payload byte has this on-disc (scrambled) value, alternating per cluster for a comma list
            var vals = fsv.Split(',').Select(v => Convert.FromHexString(v)[0]).ToArray();
            int ncf = int.Parse(o.GetValueOrDefault("--clusters", "4")); data = new byte[ncf * 65536];
            var mk = new byte[2048];
            for (int c = 0; c < ncf; c++)
            {
                uint psn0 = disc.GetPhysicalSectorNumber(start + (uint)c * 32); BluRayScrambler.FillMask(psn0, mk);
                for (int s = 0; s < 32; s++) for (int k = 0; k < 2048; k++) data[c * 65536 + s * 2048 + k] = (byte)(vals[c % vals.Length] ^ mk[k]);
            }
        }
        int ncl = data.Length / 65536;
        int cb = BluRayRecordingUnitEncoder.RecordingUnitChannelBits, packed = (cb + 7) / 8, rec = packed + BluRayEccEncoder.EccClusterBytes;
        
        var sw = System.Diagnostics.Stopwatch.StartNew(); int done = 0;
        int[] list = o.TryGetValue("--only", out var onlyPath)
            ? File.ReadAllLines(onlyPath).Where(l => l.Trim().Length > 0).Select(int.Parse).Where(c => c >= 0 && c < ncl).ToArray()
            : Enumerable.Range(0, ncl).ToArray();
        Console.WriteLine($"chan: encoding {list.Length} of {ncl} clusters");
        var outBuf = new byte[(long)list.Length * rec];
        Parallel.For(0, list.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, li =>
        {
            int c = list[li];
            uint psn = disc.GetPhysicalSectorNumber(start + (uint)c * 32);
            byte[] ecc = BluRayEccEncoder.Encode(data.AsSpan(c * 65536, 65536), psn);
            var res = BluRayRecordingUnitEncoder.EncodeEccCluster(ecc, psn, appendGuard3: false);
            var lv = res.ChannelLevels; long b0 = (long)li * rec;
            for (int i = 0; i < cb; i++) if (lv[i] != 0) outBuf[b0 + (i >> 3)] |= (byte)(0x80 >> (i & 7));
            ecc.CopyTo(outBuf, b0 + packed);
            int d = Interlocked.Increment(ref done); if (d % 200 == 0) Console.WriteLine($"  {d}/{list.Length} clusters {sw.Elapsed.TotalSeconds:F0}s");
        });
        using (var w = new BinaryWriter(File.Create(o["--out"])))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("BDCH2\0\0\0")); w.Write(start); w.Write(list.Length); w.Write(cb);
            foreach (int c in list) w.Write(c); w.Write(outBuf);
        }
        Console.WriteLine($"chan: {ncl} clusters from sector {start} -> {o["--out"]} ({sw.Elapsed.TotalSeconds:F0}s)");
        return 0;
    }
}
