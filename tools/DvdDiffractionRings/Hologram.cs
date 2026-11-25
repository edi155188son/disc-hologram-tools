// Multi-point binary Fresnel hologram of the letter "F" (12 dots). Lit by a collimated, normally
// incident beam, one first order converges into a real image of the F at height Z above the disc.
// The image is placed DIAGONALLY from the patch (outward + tangential), so it stays clear of the two
// strong noise lines through the zero order: the tangential comb (periodic DVD sync/ECC structure)
// and the radial streak (track-to-track variation).
// Letter frame: "up" = radially outward, "right" = +tangential (counter-clockwise in DiscImageStudio's
// image coordinates). The F is deliberately asymmetric so mirror/rotation errors are obvious.
using DvdImageSolver.Solver;

public static class FHologram
{
    public const byte Bright = 0x92, Dark = 0xA5;
    public static double Lambda = 0.532e-3;  // design wavelength, mm (green laser); --holo-lambda overrides
    public const double Half = 4.0;          // patch half size (8 x 8 mm)

    // (label, patch centre radius, angle deg, image height Z mm, image offset radial/tangential mm, dot pitch mm)
    public static readonly (string Label, double R, double AngleDeg, double Z, double OffR, double OffT, double Pitch)[] Patches =
    [
        ("H1 F 4.5x9 mm at 400 mm",  40.0,   0.0, 400, 10.0,  8.0, 1.5),
        ("H2 F 7.5x15 mm at 600 mm", 40.0, 180.0, 600, 14.0, 12.0, 2.5),
    ];

    // F on a 4 x 7 grid, row 0 = top
    static readonly (int C, int R)[] Grid =
        [(0,0),(0,1),(0,2),(0,3),(0,4),(0,5),(0,6),(1,0),(2,0),(3,0),(1,3),(2,3)];

    sealed record Patch(double Cx, double Cy, double Ca, double Sa, double Z, double[] Px, double[] Py, double[] Phi);

    static Patch[]? built;
    static Patch[] Built => built ??= Patches.Select((p, idx) =>
    {
        double a = p.AngleDeg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
        double cx = p.R * ca, cy = p.R * sa;
        var rnd = new Random(1234 + idx);
        int n = Grid.Length;
        double[] px = new double[n], py = new double[n], phi = new double[n];
        double jit = p.Pitch / 6;   // position jitter keeps binarisation intermodulation ghosts off the dot lattice
        for (int k = 0; k < n; k++)
        {
            double t = p.OffT + (Grid[k].C - 1.5) * p.Pitch + (rnd.NextDouble() - 0.5) * 2 * jit;
            double r = p.OffR + (3.0 - Grid[k].R) * p.Pitch + (rnd.NextDouble() - 0.5) * 2 * jit;
            px[k] = cx + r * ca - t * sa;
            py[k] = cy + r * sa + t * ca;
            phi[k] = rnd.NextDouble() * 2 * Math.PI;   // random phases: even dot brightness, no hot spot
        }
        return new Patch(cx, cy, ca, sa, p.Z, px, py, phi);
    }).ToArray();

    public static byte Sample(DvdCodewordSample s)
    {
        foreach (var h in Built)
        {
            double dx = s.Xmm - h.Cx, dy = s.Ymm - h.Cy;
            double ur = dx * h.Ca + dy * h.Sa, ut = -dx * h.Sa + dy * h.Ca;
            if (Math.Abs(ur) > Half || Math.Abs(ut) > Half) continue;
            double re = 0, k = 2 * Math.PI / Lambda;
            for (int i = 0; i < h.Px.Length; i++)
            {
                double ex = s.Xmm - h.Px[i], ey = s.Ymm - h.Py[i];
                re += Math.Cos(k * (Math.Sqrt(ex * ex + ey * ey + h.Z * h.Z) - h.Z) + h.Phi[i]);
            }
            return re >= 0 ? Bright : Dark;
        }
        ulong hsh = (ulong)BitConverter.DoubleToInt64Bits(s.Xmm * 7919.0 + s.Ymm) * 0x9E3779B97F4A7C15UL;
        hsh ^= hsh >> 29; hsh *= 0xBF58476D1CE4E5B9UL; hsh ^= hsh >> 32;
        return (byte)hsh;
    }

    public static void Describe()
    {
        Console.WriteLine($"  design wavelength {Lambda * 1e6:F0} nm (--holo-lambda 650 for red)");
        foreach (var p in Patches)
        {
            double fr = p.OffR + 3 * p.Pitch + Half, ft = p.OffT + 1.5 * p.Pitch + Half;
            double rho = Math.Sqrt(fr * fr + ft * ft);
            Console.WriteLine($"  {p.Label}: patch 8x8 mm at r={p.R} mm @ {p.AngleDeg}deg; image {p.Z} mm above, centred "
                + $"{p.OffR} mm outward + {p.OffT} mm tangential; finest fringe ~{Lambda * p.Z / rho * 1000:F1} um");
        }
    }
}
