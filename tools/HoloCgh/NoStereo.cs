#if !HAS_STEREO
// Placeholder used when the Disc Image Studio tree has no StereoOverlay (the stereo-layer patch is not applied):
// the lenticular comparison copies (--lenti) are then unavailable and the writer never consults an overlay.
sealed class StereoOverlay
{
    public int Sample(double xMm, double yMm, ulong globalCodewordIndex) => -1;
}
#endif
