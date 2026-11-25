using DiscImageStudio.Core;
using DvdImageSolver.Solver;
// Polar raster import: a byte map indexed by (radius row, angle column), for large-area along-track
// (HPO) holograms computed externally (MATLAB holo_hpo_big.m).
// File "DVDHPO1\0": int32 version(1); double r0 (mm, first row centre), dr (mm, row pitch), theta0 (rad),
//   dtheta (rad, column pitch); int32 nrows, ncols; then nrows*ncols bytes, row-major (row = radius,
//   column = angle). Byte = scrambled Q to write; 0x00 = pseudo-random (no grating);
//   0x01..0x0F = placeholder k -> k-th sequence of --pairs (e.g. --pairs C5DB,CC67), cycled along the track.
// Several rasters may be given (comma separated); the first one covering a codeword wins.
public static class Polar
{
    const string Magic = "DVDHPO1\0";

    public sealed class Raster
    {
        public double R0, Dr, Th0, Dth; public int Rows, Cols; public byte[] Q = [];
        public double R1 => R0 + Rows * Dr;
        public double Th1 => Th0 + Cols * Dth;
    }

    public static Raster Load(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(8)) != Magic) throw new InvalidDataException("Not a DVDHPO1 file: " + path);
        r.ReadInt32();
        var P = new Raster { R0 = r.ReadDouble(), Dr = r.ReadDouble(), Th0 = r.ReadDouble(), Dth = r.ReadDouble(), Rows = r.ReadInt32(), Cols = r.ReadInt32() };
        P.Q = r.ReadBytes(checked(P.Rows * P.Cols));
        if (P.Q.Length != P.Rows * P.Cols) throw new InvalidDataException("raster truncated: " + path);
        Console.WriteLine($"Polar raster {path}: r {P.R0:F2}..{P.R1:F2} mm ({P.Rows} rows x {P.Dr * 1000:F1} um), " +
            $"angle {P.Th0 * 180 / Math.PI:F1}..{P.Th1 * 180 / Math.PI:F1} deg ({P.Cols} cols, {P.Dth * P.R1 * 1000:F2} um arc at outer edge)");
        return P;
    }

    public static byte[][] Pairs = [];

    public static Func<DvdCodewordSample, byte> Sampler(Raster[] Ps)
    {
        var pairs = Pairs;
        return s =>
        {
            double rr = Math.Sqrt(s.Xmm * s.Xmm + s.Ymm * s.Ymm);
            double th = Math.Atan2(s.Ymm, s.Xmm);
            foreach (var P in Ps)
            {
                if (rr < P.R0 || rr >= P.R1) continue;
                double t = th - P.Th0; t -= Math.Floor(t / (2 * Math.PI)) * 2 * Math.PI;   // into [0, 2pi) from Th0
                if (t >= P.Cols * P.Dth) continue;
                int row = (int)((rr - P.R0) / P.Dr), col = (int)(t / P.Dth);
                byte q = P.Q[row * P.Cols + col];
                if (q >= 1 && q <= 15)
                {
                    if (q - 1 < pairs.Length) { var seq = pairs[q - 1]; return seq[(int)(s.GlobalCodewordIndex % (ulong)seq.Length)]; }
                    break;
                }
                if (q != 0) return q;
                break;
            }
            ulong h = (ulong)BitConverter.DoubleToInt64Bits(s.Xmm * 7919.0 + s.Ymm) * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
            return (byte)h;
        };
    }
}
