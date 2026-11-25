using DiscImageStudio.Core;
using DvdImageSolver.Solver;
public static class Probe
{
    public static void Run()
    {
        const uint Sectors = 2297888;
        double inner = 23.96623775566068, outer = 57.954483397299796, lin = -4.152250636325952E-05, quad = 4.888878988850371E-05, cub = -1.9188389560484087E-05;
        var opt = new MultiBlockSolveOptions(800000, 64, 0x30000, 0, 1, 1, 0, 0, false,
            new ImageMappingOptions(Sectors, inner, outer, 133.3, 0, true, 128, 1, PitchLinear: lin, PitchQuadratic: quad, PitchCubic: cub, ImageOuterRadiusMm: outer),
            Algorithm: SolverAlgorithm.Dispersion, FastParallelism: 1);
        var log = new List<DvdCodewordSample>();
        FastDispersionImageWriter.WriteScrambledCodewordPatternStream(s => { if (log.Count < 20000) log.Add(s); return 0x92; }, Stream.Null, opt);
        var gaps = new Dictionary<long, int>(); var mod = new Dictionary<long, int>();
        for (int i = 1; i < log.Count; i++)
        {
            long g = (long)(log[i].GlobalCodewordIndex - log[i - 1].GlobalCodewordIndex);
            gaps[g] = gaps.GetValueOrDefault(g) + 1;
            if (g != 1) { long m = (long)(log[i].GlobalCodewordIndex % 93); mod[g * 1000 + m] = mod.GetValueOrDefault(g * 1000 + m) + 1; }
        }
        Console.WriteLine("gaps: " + string.Join(" ", gaps.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")));
        Console.WriteLine("gap*1000+idx%93 after gap: " + string.Join(" ", mod.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")));
        for (int i = 100; i < 104; i++)
        {
            var a = log[i]; double r = Math.Sqrt(a.Xmm * a.Xmm + a.Ymm * a.Ymm), th = Math.Atan2(a.Ymm, a.Xmm);
            Console.WriteLine($"idx {a.GlobalCodewordIndex} r {r:F5} canvasTheta {th:F7} trackAngle {a.TrackAngleRadians:F7}");
        }
        var p = log[200]; var q = log[201];
        Console.WriteLine($"step between words: {Math.Sqrt(Math.Pow(q.Xmm - p.Xmm, 2) + Math.Pow(q.Ymm - p.Ymm, 2)) * 1000:F4} um");
    }
}
