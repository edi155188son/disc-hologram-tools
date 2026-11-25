// LDC parity steering for BdAmp.
// The drive computes 32 LDC parity bytes per RS(248,216) codeword from the data we hand it (scrambled user data + EDC).
// Parity is GF(2)-linear in the data, so for every codeword whose parity bytes land inside a hologram block we flip
// bits of "free" payload bytes of the same codeword that land outside every block (background noise), such that the
// parity the drive will write equals the hologram pattern. EDC (CRC-32 per 2052-byte frame) is linear too; a sector's
// EDC always lies at or after the codeword being solved, so codewords are solved in order and an EDC byte that falls
// inside the current codeword is included in its equations.
using System.Buffers.Binary;
using DiscImageStudio.BluRay;
using DiscImageStudio.BluRay.Encoding;

static class Steer
{
    static BluRayDiscParameters Disc = null!; static BluRayTrackGeometry Geo = null!;
    static Func<double, double, int> Desired = null!; static byte Dark, Light;
    static readonly byte[][] ParityOfBit = new byte[216 * 8][];   // parity of info with single bit (i, b) set
    static readonly uint[] EdcImpulse = new uint[2048 * 8];       // EDC change for flipping bit b of user byte k
    const int MaxFreeBytes = 48;
    static readonly byte[] Mul = new byte[65536]; static readonly byte[] Inv = new byte[256];
    static readonly byte[] G = new byte[216 * 32];               // parity of info byte value 1 at position i (GF(256)-linear)
    public static long cwGf, cwGf2;
    public static bool UseOpt;
    static long clustersSeen, clustersSteered, cwConstrained, cwSolved, bytesConstrained, bytesForced, verifyLeft, verifyBad, verifyChecked;

    public static void Setup(BluRayDiscParameters disc, BluRayTrackGeometry geo, Func<double, double, int> desired, byte dark, byte light, int verify)
    {
        Disc = disc; Geo = geo; Desired = desired; Dark = dark; Light = light; verifyLeft = verify;
        Span<byte> info = stackalloc byte[216];
        for (int i = 0; i < 216; i++)
            for (int b = 0; b < 8; b++)
            {
                info.Clear(); info[i] = (byte)(1 << b);
                var p = new byte[32]; BluRayReedSolomon.ComputeParity(info, p); ParityOfBit[i * 8 + b] = p;
            }
        {   // GF(256), polynomial 0x11d (same field as BluRayReedSolomon)
            var ex = new int[512]; var lg = new int[256]; int v = 1;
            for (int i = 0; i < 255; i++) { ex[i] = v; lg[v] = i; v <<= 1; if ((v & 0x100) != 0) v ^= 0x11d; }
            for (int i = 255; i < 512; i++) ex[i] = ex[i - 255];
            for (int a = 1; a < 256; a++) { Inv[a] = (byte)ex[255 - lg[a]]; for (int b = 1; b < 256; b++) Mul[a * 256 + b] = (byte)ex[lg[a] + lg[b]]; }
            for (int i = 0; i < 216; i++) ParityOfBit[i * 8].CopyTo(G, i * 32);
            // self-check linearity: parity(x at i) == x * G[i]
            Span<byte> t = stackalloc byte[216]; Span<byte> q = stackalloc byte[32];
            for (int k = 0; k < 64; k++) { int i = (k * 37) % 216; byte x = (byte)(k * 29 + 3); t.Clear(); t[i] = x; BluRayReedSolomon.ComputeParity(t, q);
                for (int j = 0; j < 32; j++) if (q[j] != Mul[x * 256 + G[i * 32 + j]]) throw new InvalidOperationException("GF(256) parity model mismatch"); }
        }
        var zero = new byte[2048];
        for (int k = 0; k < 2048; k++)
            for (int b = 0; b < 8; b++)
            {   // CRC is linear with zero init / xorout: impulse response of one bit
                zero[k] = (byte)(1 << b); EdcImpulse[k * 8 + b] = BluRayEdc.Compute(zero); zero[k] = 0;
            }
    }

    public static double ChannelOffsetPublic(int column, int row) => ChannelOffset(column, row);
    static double ChannelOffset(int column, int row)
    {
        int frame = 2 * row + column % 2;
        int ldcColumn = (column / 2 + 152 - (row * 3) % 152) % 152;
        int eccColumn = ldcColumn + ldcColumn / 38;
        int midpoint = eccColumn * 8 + 4;
        int dc = midpoint < 25 ? 0 : 1 + (midpoint - 25) / 45;
        return 2760 + frame * 1932 + 30 + (midpoint + dc) * 1.5;
    }

    static int DesiredAt(double clusterBase, int column, int row)
    {
        BluRayTrackPoint p;
        try { p = Geo.AtSector((clusterBase + ChannelOffset(column, row)) / (BluRayDiscParameters.ChannelBitsPerRecordingUnit / 32.0)); }
        catch (ArgumentOutOfRangeException) { return -1; }
        return Desired(p.Xmm, p.Ymm);
    }

    public static void Process(BluRayDiscParameters disc, uint firstSector, uint psn, Span<byte> cluster)
    {
        Interlocked.Increment(ref clustersSeen);
        double clusterBase = (long)(firstSector / 32) * BluRayDiscParameters.ChannelBitsPerRecordingUnit;
        // desired parity bytes (-1 = don't care)
        var want = new short[304 * 32]; bool any = false;
        for (int c = 0; c < 304; c++)
            for (int j = 0; j < 32; j++)
            {
                int d = DesiredAt(clusterBase, c, 216 + j);
                int ov = d >= 0 && UseOpt ? OptBits.Parity(firstSector, c, j) : -1;
                want[c * 32 + j] = (short)(d < 0 ? -1 : ov >= 0 ? ov : d == 0 ? Dark : Light);
                any |= d >= 0;
            }
        if (!any) return;
        Interlocked.Increment(ref clustersSteered);
        Span<byte> mask = stackalloc byte[2052];
        BluRayScrambler.FillMask(psn, mask);
        // data block (scrambled): payload + scrambled EDC
        var D = new byte[32 * 2052];
        Span<byte> user = stackalloc byte[2048];
        Span<byte> dp = stackalloc byte[32];
        for (int s = 0; s < 32; s++)
        {
            cluster.Slice(s * 2048, 2048).CopyTo(D.AsSpan(s * 2052, 2048));
            for (int k = 0; k < 2048; k++) user[k] = (byte)(cluster[s * 2048 + k] ^ mask[k]);
            uint edc = BluRayEdc.Compute(user);
            BinaryPrimitives.WriteUInt32BigEndian(D.AsSpan(s * 2052 + 2048, 4), edc);
            for (int k = 0; k < 4; k++) D[s * 2052 + 2048 + k] ^= mask[2048 + k];
        }
        Span<byte> parity = stackalloc byte[32];
        var freeIdx = new int[MaxFreeBytes];
        var rows = new ulong[256 * 7];                  // up to 256 equations x (384 vars + rhs) bits
        for (int c = 0; c < 304; c++)
        {
            int ncon = 0; for (int j = 0; j < 32; j++) if (want[c * 32 + j] >= 0) ncon++;
            if (ncon == 0) continue;
            Interlocked.Increment(ref cwConstrained); Interlocked.Add(ref bytesConstrained, ncon);
            var info = D.AsSpan(c * 216, 216);
            BluRayReedSolomon.ComputeParity(info, parity);
            bool ok = true; for (int j = 0; j < 32 && ok; j++) if (want[c * 32 + j] >= 0 && parity[j] != want[c * 32 + j]) ok = false;
            if (ok) { Interlocked.Increment(ref cwSolved); continue; }
            if (SolveGf(c, clusterBase, cluster, D, parity, want)) { Interlocked.Increment(ref cwSolved); Interlocked.Increment(ref cwGf); Interlocked.Add(ref bytesForced, ncon); continue; }
            Interlocked.Increment(ref cwGf2);
            // free payload bytes of this codeword outside every block
            int nf = 0;
            for (int i = 0; i < 216 && nf < MaxFreeBytes; i++)
            {
                int serial = c * 216 + i, k = serial % 2052;
                if (k >= 2048) continue;                                    // EDC byte, not free
                if (DesiredAt(clusterBase, c, i) >= 0) continue;            // inside a block
                freeIdx[nf++] = i;
            }
            int nvar = nf * 8, words = (nvar + 1 + 63) / 64, rhsBit = nvar;
            if (nvar < ncon * 8) continue;
            // EDC bytes of sectors whose EDC sits inside this codeword
            Array.Clear(rows, 0, ncon * 8 * words);
            for (int vi = 0; vi < nvar; vi++)
            {
                int i = freeIdx[vi / 8], b = vi % 8;
                ParityOfBit[i * 8 + b].CopyTo(dp);
                int serial = c * 216 + i, s = serial / 2052, k = serial % 2052;
                uint de = EdcImpulse[k * 8 + b];
                for (int e = 0; e < 4; e++)
                {
                    int es = s * 2052 + 2048 + e, ei = es - c * 216;
                    byte db = (byte)(de >> (24 - 8 * e));
                    if (db == 0 || ei < 0 || ei >= 216) continue;
                    for (int bb = 0; bb < 8; bb++)
                        if ((db >> bb & 1) != 0) { var q = ParityOfBit[ei * 8 + bb]; for (int j = 0; j < 32; j++) dp[j] ^= q[j]; }
                }
                int r = 0;
                for (int j = 0; j < 32; j++)
                {
                    if (want[c * 32 + j] < 0) continue;
                    for (int bb = 0; bb < 8; bb++, r++)
                        if ((dp[j] >> bb & 1) != 0) rows[r * words + vi / 64] |= 1UL << (vi % 64);
                }
            }
            { int r = 0;
              for (int j = 0; j < 32; j++)
              {
                  if (want[c * 32 + j] < 0) continue;
                  int diff = parity[j] ^ want[c * 32 + j];
                  for (int bb = 0; bb < 8; bb++, r++) if ((diff >> bb & 1) != 0) rows[r * words + rhsBit / 64] |= 1UL << (rhsBit % 64);
              } }
            int m = ncon * 8; var pivotCol = new int[m]; int rank = 0;
            for (int col = 0; col < nvar && rank < m; col++)
            {
                int w = col / 64; ulong bit = 1UL << (col % 64); int pr = -1;
                for (int r = rank; r < m; r++) if ((rows[r * words + w] & bit) != 0) { pr = r; break; }
                if (pr < 0) continue;
                if (pr != rank) for (int t = 0; t < words; t++) (rows[pr * words + t], rows[rank * words + t]) = (rows[rank * words + t], rows[pr * words + t]);
                for (int r = 0; r < m; r++)
                    if (r != rank && (rows[r * words + w] & bit) != 0)
                        for (int t = 0; t < words; t++) rows[r * words + t] ^= rows[rank * words + t];
                pivotCol[rank++] = col;
            }
            bool consistent = true;
            for (int r = rank; r < m; r++) if ((rows[r * words + rhsBit / 64] >> (rhsBit % 64) & 1) != 0) { consistent = false; break; }
            if (!consistent) continue;
            for (int r = 0; r < rank; r++)
            {
                if ((rows[r * words + rhsBit / 64] >> (rhsBit % 64) & 1) == 0) continue;
                int vi = pivotCol[r], i = freeIdx[vi / 8], b = vi % 8;
                int serial = c * 216 + i, s = serial / 2052, k = serial % 2052;
                byte f = (byte)(1 << b);
                cluster[s * 2048 + k] ^= f; D[serial] ^= f;
                uint de = EdcImpulse[k * 8 + b];
                for (int e = 0; e < 4; e++) D[s * 2052 + 2048 + e] ^= (byte)(de >> (24 - 8 * e));
            }
            Interlocked.Increment(ref cwSolved); Interlocked.Add(ref bytesForced, ncon);
        }
        if (Interlocked.Decrement(ref verifyLeft) >= 0) Verify(cluster, psn, want);
    }


    // Byte-level solve over GF(256): parity is GF(256)-linear in the information bytes. Free bytes whose own sector's
    // EDC lies inside this codeword are excluded (EDC is only GF(2)-linear); returns false -> caller uses the GF(2) path.
    static bool SolveGf(int c, double clusterBase, Span<byte> cluster, byte[] D, Span<byte> parity, short[] want)
    {
        int lo = c * 216, hi = lo + 216;
        Span<int> rowsJ = stackalloc int[32]; int m = 0;
        // every constrained row (already-satisfied rows must stay satisfied)
        for (int j = 0; j < 32; j++) if (want[c * 32 + j] >= 0) rowsJ[m++] = j;
        Span<int> freeI = stackalloc int[MaxFreeBytes]; int nf = 0;
        for (int i = 0; i < 216 && nf < MaxFreeBytes; i++)
        {
            int serial = lo + i, s = serial / 2052, k = serial % 2052;
            if (k >= 2048) continue;
            int edc = s * 2052 + 2048; if (edc + 3 >= lo && edc < hi) continue;     // own EDC inside this codeword
            if (DesiredAt(clusterBase, c, i) >= 0) continue;
            freeI[nf++] = i;
        }
        if (nf < m) return false;
        int w = nf + 1; Span<byte> A = stackalloc byte[32 * (MaxFreeBytes + 1)];
        for (int r = 0; r < m; r++)
        {
            int j = rowsJ[r];
            for (int v = 0; v < nf; v++) A[r * w + v] = G[freeI[v] * 32 + j];
            A[r * w + nf] = (byte)(parity[j] ^ want[c * 32 + j]);
        }
        Span<int> piv = stackalloc int[32]; int rank = 0;
        for (int col = 0; col < nf && rank < m; col++)
        {
            int pr = -1; for (int r = rank; r < m; r++) if (A[r * w + col] != 0) { pr = r; break; }
            if (pr < 0) continue;
            if (pr != rank) for (int t = 0; t < w; t++) (A[pr * w + t], A[rank * w + t]) = (A[rank * w + t], A[pr * w + t]);
            int iv = Inv[A[rank * w + col]] * 256;
            for (int t = col; t < w; t++) A[rank * w + t] = Mul[iv + A[rank * w + t]];
            for (int r = 0; r < m; r++)
            {
                if (r == rank) continue; byte f = A[r * w + col]; if (f == 0) continue; int fm = f * 256;
                for (int t = col; t < w; t++) A[r * w + t] ^= Mul[fm + A[rank * w + t]];
            }
            piv[rank++] = col;
        }
        for (int r = rank; r < m; r++) if (A[r * w + nf] != 0) return false;
        Span<byte> dD = stackalloc byte[216]; dD.Clear();
        for (int r = 0; r < rank; r++) dD[freeI[piv[r]]] = A[r * w + nf];
        // apply (payload + EDC of affected sectors; their EDC lies in later codewords)
        for (int i = 0; i < 216; i++)
        {
            byte x = dD[i]; if (x == 0) continue;
            int serial = lo + i, s = serial / 2052, k = serial % 2052;
            cluster[s * 2048 + k] ^= x; D[serial] ^= x;
            uint de = 0; for (int b = 0; b < 8; b++) if ((x >> b & 1) != 0) de ^= EdcImpulse[k * 8 + b];
            for (int e = 0; e < 4; e++) D[s * 2052 + 2048 + e] ^= (byte)(de >> (24 - 8 * e));
        }
        return true;
    }

    static void Verify(Span<byte> cluster, uint psn, short[] want)
    {
        var user = new byte[65536]; var mask = BluRayScrambler.CreateMask(psn);
        for (int s = 0; s < 32; s++) for (int k = 0; k < 2048; k++) user[s * 2048 + k] = (byte)(cluster[s * 2048 + k] ^ mask[k]);
        var ldc = BluRayEccEncoder.EncodeLdcCluster(user, psn);
        long bad = 0, chk = 0;
        for (int c = 0; c < 304; c++)
            for (int j = 0; j < 32; j++)
            {
                if (want[c * 32 + j] < 0) continue;
                int row = 216 + j, outputRow = row * 2 + c % 2, outputColumn = (c / 2 + 152 - (row * 3) % 152) % 152;
                chk++; if (ldc[outputRow * 152 + outputColumn] != want[c * 32 + j]) bad++;
            }
        Interlocked.Add(ref verifyChecked, chk); Interlocked.Add(ref verifyBad, bad);
    }

    public static void Report()
    {
        Console.WriteLine($"Steering solver: GF(256) byte solve {cwGf} codewords, GF(2) bit solve (EDC inside codeword) {cwGf2}");
        Console.WriteLine($"Parity steering: {clustersSteered}/{clustersSeen} clusters touched blocks; codewords constrained {cwConstrained}, solved {cwSolved}; " +
            $"parity bytes forced {bytesForced}/{bytesConstrained}");
        if (verifyChecked > 0) Console.WriteLine($"Verify (re-encoded with DiscImageStudio LDC encoder): {verifyChecked - verifyBad}/{verifyChecked} constrained parity bytes match");
    }
}
