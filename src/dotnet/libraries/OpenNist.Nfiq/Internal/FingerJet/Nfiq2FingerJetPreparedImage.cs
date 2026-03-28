namespace OpenNist.Nfiq.Internal.FingerJet;

internal sealed record Nfiq2FingerJetPreparedImage(
    ReadOnlyMemory<byte> Pixels,
    int Width,
    int Height,
    int PixelsPerInch,
    int XOffset,
    int YOffset,
    int OrientationMapWidth,
    int OrientationMapSize);
