namespace OpenNist.Nfiq.Internal.FingerJet;

using OpenNist.Nfiq.Internal.Model;

internal static class Nfiq2FingerJetMinutiaPostProcessor
{
    private const int s_imageScale = 127 * 5;
    private const int s_stdFmdDeserializerResolution = 167;
    private const int s_ridgeEndingType = 1;
    private const int s_bifurcationType = 2;

    public static IReadOnlyList<Nfiq2Minutia> Process(
        IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae,
        int imageResolution,
        int xOffset,
        int yOffset)
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageResolution);

        var offsetX = xOffset * s_imageScale / imageResolution;
        var offsetY = yOffset * s_imageScale / imageResolution;
        var result = new Nfiq2Minutia[minutiae.Count];

        for (var index = 0; index < minutiae.Count; index++)
        {
            var source = minutiae[index];
            var angle = unchecked((byte)(-source.Angle + 64));
            var scaledX = source.X * s_imageScale / imageResolution;
            var scaledY = source.Y * s_imageScale / imageResolution;
            scaledX += offsetX;
            scaledY += offsetY;
            scaledX = Nfiq2FingerJetMath.MulDiv(scaledX, 197, s_stdFmdDeserializerResolution);
            scaledY = Nfiq2FingerJetMath.MulDiv(scaledY, 197, s_stdFmdDeserializerResolution);

            result[index] = new(
                scaledX,
                scaledY,
                angle,
                Nfiq2FingerJetMath.QualityFromConfidence((byte)source.Confidence),
                MapType(source.Type));
        }

        return result;
    }

    private static int MapType(int type)
    {
        return type switch
        {
            s_ridgeEndingType => s_ridgeEndingType,
            s_bifurcationType => s_bifurcationType,
            _ => 0,
        };
    }
}
