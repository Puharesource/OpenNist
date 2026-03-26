namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2MinutiaeCountModule
{
    private const string s_countFeatureName = "FingerJetFX_MinutiaeCount";
    private const string s_countComFeatureName = "FingerJetFX_MinCount_COMMinRect200x200";
    private const int s_countComWidth = 200;
    private const int s_countComHeight = 200;

    public static Nfiq2MinutiaeCountResult Compute(
        IReadOnlyList<Nfiq2Minutia> minutiae,
        int imageWidth,
        int imageHeight)
    {
        ArgumentNullException.ThrowIfNull(minutiae);

        if (minutiae.Count == 0)
        {
            return new(
                [],
                0,
                0,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    [s_countFeatureName] = 0,
                    [s_countComFeatureName] = 0,
                });
        }

        var centerOfMass = ComputeCenterOfMass(minutiae);
        var halfWidth = s_countComWidth / 2;
        var halfHeight = s_countComHeight / 2;
        var startX = Math.Max(0, centerOfMass.X - halfWidth);
        var startY = Math.Max(0, centerOfMass.Y - halfHeight);
        var endX = Math.Min(imageWidth - 1, centerOfMass.X + halfWidth);
        var endY = Math.Min(imageHeight - 1, centerOfMass.Y + halfHeight);

        var countCom = 0;
        for (var index = 0; index < minutiae.Count; index++)
        {
            var minutia = minutiae[index];
            if (minutia.X >= startX && minutia.X <= endX
                && minutia.Y >= startY && minutia.Y <= endY)
            {
                countCom++;
            }
        }

        return new(
            minutiae,
            centerOfMass.X,
            centerOfMass.Y,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [s_countFeatureName] = minutiae.Count,
                [s_countComFeatureName] = countCom,
            });
    }

    private static Nfiq2MinutiaPoint ComputeCenterOfMass(IReadOnlyList<Nfiq2Minutia> minutiae)
    {
        long sumX = 0;
        long sumY = 0;
        for (var index = 0; index < minutiae.Count; index++)
        {
            sumX += minutiae[index].X;
            sumY += minutiae[index].Y;
        }

        return new((int)(sumX / minutiae.Count), (int)(sumY / minutiae.Count));
    }
}

internal sealed record Nfiq2MinutiaeCountResult(
    IReadOnlyList<Nfiq2Minutia> Minutiae,
    int CenterOfMassX,
    int CenterOfMassY,
    IReadOnlyDictionary<string, double> Features);

internal readonly record struct Nfiq2MinutiaPoint(
    int X,
    int Y);
