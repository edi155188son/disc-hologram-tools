// Off-axis binary Fresnel zone plates: the simplest two-dimensional hologram (a single point source).
// Each patch focuses a collimated, normally incident beam to a point at height f above the disc,
// laterally offset from the patch. Fringes are circular arcs of every orientation, so a sharp focus
// needs correct placement in BOTH the tangential and the radial direction.
using DvdImageSolver.Solver;

public static class ZonePlates
{
    public const byte Bright = 0x92, Dark = 0xA5;
    public const double Lambda = 0.650e-3;   // design wavelength, mm (red laser pointer)
    public const double Half = 3.0;          // patch half size, mm (6 x 6 mm squares)
    public const double Offset = 6.0;        // patch centre -> optical axis distance, mm

    // (label, patch centre radius mm, centre angle deg, minimum fringe period um)
    public static readonly (string Label, double R, double AngleDeg, double MinPeriodUm)[] Patches =
    [
        ("Z1 min fringe 16 um", 40.0,   0.0, 16),
        ("Z2 min fringe  8 um", 40.0,  90.0,  8),
        ("Z3 min fringe  4 um", 40.0, 180.0,  4),
        ("Z4 min fringe 16 um at outer radius (repeat of Z1)", 49.0, 270.0, 16),
    ];

    public static readonly (double Cx, double Cy, double Ox, double Oy, double F, double Rmax)[] Geometry =
        Patches.Select(p =>
        {
            double a = p.AngleDeg * Math.PI / 180, cx = p.R * Math.Cos(a), cy = p.R * Math.Sin(a);
            // optical axis displaced outwards along the radius, so the patch holds the off-axis zones
            double ox = cx + Offset * Math.Cos(a), oy = cy + Offset * Math.Sin(a);
            double rmax = Math.Sqrt((Offset + Half) * (Offset + Half) + Half * Half);
            double f = p.MinPeriodUm * 1e-3 * rmax / Lambda; // local period = lambda*f/r (paraxial)
            return (cx, cy, ox, oy, f, rmax);
        }).ToArray();

    public static byte Sample(DvdCodewordSample s)
    {
        for (int i = 0; i < Patches.Length; i++)
        {
            var g = Geometry[i];
            double dx = s.Xmm - g.Cx, dy = s.Ymm - g.Cy;
            // patch square aligned to its own radial/tangential axes
            double a = Patches[i].AngleDeg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            double ur = dx * ca + dy * sa, ut = -dx * sa + dy * ca;
            if (Math.Abs(ur) > Half || Math.Abs(ut) > Half) continue;
            double rx = s.Xmm - g.Ox, ry = s.Ymm - g.Oy, r2 = rx * rx + ry * ry;
            double phase = 2 * Math.PI / Lambda * (Math.Sqrt(r2 + g.F * g.F) - g.F);
            return Math.Cos(phase) >= 0 ? Bright : Dark;
        }
        return Noise(s.Xmm, s.Ymm);
    }

    static byte Noise(double x, double y)
    {
        ulong h = (ulong)BitConverter.DoubleToInt64Bits(x * 7919.0 + y) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
        return (byte)h;
    }

    public static void Describe()
    {
        for (int i = 0; i < Patches.Length; i++)
        {
            var p = Patches[i]; var g = Geometry[i];
            double rmin = Offset - Half;
            Console.WriteLine($"  {p.Label}: centre r={p.R} mm @ {p.AngleDeg}deg, 6x6 mm, focus {g.F:F0} mm above the disc, "
                + $"{Offset} mm outward from the patch centre; fringe period {Lambda * g.F / g.Rmax * 1000:F1}-{Lambda * g.F / rmin * 1000:F1} um");
        }
    }
}
