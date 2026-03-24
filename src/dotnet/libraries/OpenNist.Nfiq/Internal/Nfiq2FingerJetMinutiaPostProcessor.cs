namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetMinutiaPostProcessor
{
    private const int ImageScale = 127 * 5;
    private const int StdFmdDeserializerResolution = 167;
    private const int RidgeEndingType = 1;
    private const int BifurcationType = 2;

    public static IReadOnlyList<Nfiq2Minutia> Process(
        IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae,
        int imageResolution,
        int xOffset,
        int yOffset)
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageResolution);

        var offsetX = (xOffset * ImageScale) / imageResolution;
        var offsetY = (yOffset * ImageScale) / imageResolution;
        var result = new Nfiq2Minutia[minutiae.Count];

        for (var index = 0; index < minutiae.Count; index++)
        {
            var source = minutiae[index];
            var angle = unchecked((byte)(-source.Angle + 64));
            var scaledX = (source.X * ImageScale) / imageResolution;
            var scaledY = (source.Y * ImageScale) / imageResolution;
            scaledX += offsetX;
            scaledY += offsetY;
            scaledX = Nfiq2FingerJetMath.MulDiv(scaledX, 197, StdFmdDeserializerResolution);
            scaledY = Nfiq2FingerJetMath.MulDiv(scaledY, 197, StdFmdDeserializerResolution);

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
            RidgeEndingType => RidgeEndingType,
            BifurcationType => BifurcationType,
            _ => 0,
        };
    }
}
