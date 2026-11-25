// Can the phase of the 16T (one cycle per byte, 2.13 um) EFMPlus grating be chosen byte by byte?
// Greedy, state-aware choice among bytes >= 88 (no DSV-dependent substitution), avoiding the
// State 1/4 DSV branch; then encode with the real encoder from random boundaries and compare.
using System.Numerics;
using DvdImageSolver.Encoding;

const int N = EfmPlusEncoder.BytesPerSyncFrame;
var rng = new Random(1);
double M = args.Length > 0 ? double.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 1.0;
Complex Tw(int globalBit) => Complex.FromPolarCoordinates(1, -2 * Math.PI * M * globalBit / 16);
Complex[] tw = Enumerable.Range(0, 16).Select(t => Tw(t)).ToArray();

// per-codeword fundamental for start level +1 (land); start level -1 flips the sign
Complex Fund(ushort word, bool land)
{
    Complex c = 0; bool lev = land;
    for (int b = 15, t = 0; b >= 0; b--, t++) { if (((word >> b) & 1) != 0) lev = !lev; c += (lev ? 1 : -1) * tw[t]; }
    return c;
}
int Lead(ushort w) { int z = 0; for (int b = 15; b >= 0 && ((w >> b) & 1) == 0; b--) z++; return z; }
int Trail(ushort w) { int z = 0; for (int b = 0; b < 16 && ((w >> b) & 1) == 0; b++) z++; return z; }
int Parity(ushort w) => BitOperations.PopCount(w) & 1;

// reference: pure repeated 5F (the brightest 2.13 um grating in rings7)
Complex[] Measure(byte[] frame, ModulationBoundary input)
{
    var enc = new EfmPlusEncoder(); var lv = new byte[EfmPlusEncoder.BitsPerSyncFrame];
    enc.EncodeSyncFrame(frame, 0, input, lv);
    var c = new Complex[N];
    for (int j = 0; j < N; j++) { Complex s = 0; for (int t = 0; t < 16; t++) s += (lv[32 + 16 * j + t] == 1 ? 1 : -1) * Tw(16 * j + t); c[j] = s; }
    return c;
}
ModulationBoundary RandIn() => new((byte)rng.Next(1, 5), rng.Next(-60, 61), rng.Next(2) == 1);
var ref5F = Measure(Enumerable.Repeat((byte)0x5F, N).ToArray(), RandIn());
double refAmp = 10.25; // pure 5F at 16T
Console.WriteLine($"carrier {16 / M:F2}T = {16 / M * 0.1333:F3} um, 1st order at 550 nm: {Math.Asin(0.55 / (16 / M * 0.1333)) * 180 / Math.PI:F0} deg");

// greedy encoder for target phases; tracks state, trailing zeros and level parity
(byte[] Frame, Complex[] Predicted) Greedy(double[] phi, int minByte, bool avoidBranch)
{
    var frame = new byte[N]; var pred = new Complex[N];
    byte state = 1; int trail = 0; /* sync ends in a ONE */ bool land = true;  // relative level
    for (int j = 0; j < N; j++)
    {
        Complex want = Complex.FromPolarCoordinates(1, phi[j]);
        double best = double.NegativeInfinity; int bq = -1; ushort bw = 0; byte bn = 0;
        for (int q = minByte; q < 256; q++)
        {
            var (w, ns) = EfmPlusTables.LookupMain((byte)q, state);
            int comb = trail + Lead(w); if (comb < 2 || comb > 10) continue;
            if (avoidBranch && state is 1 or 4)
            {
                var a = EfmPlusTables.LookupMain((byte)q, 1).Word; var b = EfmPlusTables.LookupMain((byte)q, 4).Word;
                int ca = trail + Lead(a), cb = trail + Lead(b);
                if (ca is >= 2 and <= 10 && cb is >= 2 and <= 10 && a != b) continue;   // DSV would pick
            }
            double score = (Fund(w, land) * Tw(16 * j) * Complex.Conjugate(want)).Real;
            if (score > best) { best = score; bq = q; bw = w; bn = ns; }
        }
        frame[j] = (byte)bq; pred[j] = Fund(bw, land) * Tw(16 * j);
        if (Parity(bw) == 1) land = !land;
        trail = Trail(bw) == 16 ? trail + 16 : Trail(bw); state = bn;
    }
    return (frame, pred);
}

foreach (var (label, minB, avoid) in new[] { ("bytes>=88, no branch", 88, true) })
{
    double sumCorr = 0, sumEff = 0, sumPredMatch = 0; int trials = 300;
    for (int k = 0; k < trials; k++)
    {
        // smooth-ish random target: a hologram fragment = chirp + random offset
        double f0 = rng.NextDouble() * 0.3 - 0.15, a2 = rng.NextDouble() * 0.004, p0 = rng.NextDouble() * 6.283;
        double[] phi = Enumerable.Range(0, N).Select(j => p0 + 2 * Math.PI * (f0 * j + a2 * j * j)).ToArray();
        var (frame, pred) = Greedy(phi, minB, avoid);
        var c = Measure(frame, RandIn());
        Complex acc = 0; double mag = 0; Complex pm = 0; double pmn = 0;
        for (int j = 0; j < N; j++) { acc += c[j] * Complex.FromPolarCoordinates(1, -phi[j]); mag += c[j].Magnitude; pm += c[j] * Complex.Conjugate(pred[j]); pmn += c[j].Magnitude * pred[j].Magnitude; }
        sumCorr += acc.Magnitude / mag;            // phase fidelity (global sign removed)
        sumEff += acc.Magnitude / N / refAmp;      // coherent amplitude vs pure 5F grating
        sumPredMatch += pm.Magnitude / pmn;        // encoder output == greedy prediction (up to sign)
    }
    Console.WriteLine($"{label,-22}: prediction match {sumPredMatch / trials:F3}, phase fidelity {sumCorr / trials:F3}, coherent amplitude {100 * sumEff / trials:F0}% of pure 5F");
}
