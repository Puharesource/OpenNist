namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2MinutiaeQualityModule
{
    private const int LocalRegionSquare = 32;
    private const string MuFeatureName = "FJFXPos_Mu_MinutiaeQuality_2";
    private const string OclFeatureName = "FJFXPos_OCL_MinutiaeQuality_80";

    public static IReadOnlyDictionary<string, double> Compute(
        Nfiq2FingerprintImage fingerprintImage,
        IReadOnlyList<Nfiq2Minutia> minutiae)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);
        ArgumentNullException.ThrowIfNull(minutiae);

        if (minutiae.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [MuFeatureName] = 0,
                [OclFeatureName] = 0,
            };
        }

        ComputeGlobalMeanAndStdDev(fingerprintImage.Pixels.Span, out var imageMean, out var imageStdDev);

        var muRange2 = 0;
        var oclRange80 = 0;
        for (var index = 0; index < minutiae.Count; index++)
        {
            var minutia = minutiae[index];
            var muQuality = ComputeMuMinutiaQuality(fingerprintImage, minutia, imageMean, imageStdDev);
            if (muQuality > 0.0 && muQuality <= 0.5)
            {
                muRange2++;
            }

            var oclQuality = ComputeOclMinutiaQuality(fingerprintImage, minutia);
            if (oclQuality > 80)
            {
                oclRange80++;
            }
        }

        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [MuFeatureName] = muRange2 / (double)minutiae.Count,
            [OclFeatureName] = oclRange80 / (double)minutiae.Count,
        };
    }

    private static double ComputeMuMinutiaQuality(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2Minutia minutia,
        double imageMean,
        double imageStdDev)
    {
        var leftX = Math.Max(0, minutia.X - (LocalRegionSquare / 2));
        var topY = Math.Max(0, minutia.Y - (LocalRegionSquare / 2));
        var takenWidth = Math.Min(LocalRegionSquare, fingerprintImage.Width - leftX);
        var takenHeight = Math.Min(LocalRegionSquare, fingerprintImage.Height - topY);
        var block = Nfiq2RidgeValleySupport.ExtractBlock(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            topY,
            leftX,
            takenWidth,
            takenHeight);

        double blockSum = 0.0;
        for (var index = 0; index < block.Length; index++)
        {
            blockSum += block[index];
        }

        var blockMean = blockSum / block.Length;
        return (imageMean - blockMean) / imageStdDev;
    }

    private static int ComputeOclMinutiaQuality(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2Minutia minutia)
    {
        var leftX = Math.Max(0, minutia.X - (LocalRegionSquare / 2));
        var topY = Math.Max(0, minutia.Y - (LocalRegionSquare / 2));
        if (leftX + LocalRegionSquare > fingerprintImage.Width)
        {
            leftX = fingerprintImage.Width - LocalRegionSquare;
        }

        if (topY + LocalRegionSquare > fingerprintImage.Height)
        {
            topY = fingerprintImage.Height - LocalRegionSquare;
        }

        var hasValue = Nfiq2OclHistogramModule.TryGetBlockOrientationCertainty(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            topY,
            leftX,
            LocalRegionSquare,
            out var ocl);
        if (!hasValue)
        {
            return 0;
        }

        return (int)((ocl * 100.0) + 0.5);
    }

    private static void ComputeGlobalMeanAndStdDev(ReadOnlySpan<byte> pixels, out double mean, out double stdDev)
    {
        double sum = 0.0;
        for (var index = 0; index < pixels.Length; index++)
        {
            sum += pixels[index];
        }

        mean = sum / pixels.Length;

        double variance = 0.0;
        for (var index = 0; index < pixels.Length; index++)
        {
            var delta = pixels[index] - mean;
            variance += delta * delta;
        }

        stdDev = Math.Sqrt(variance / pixels.Length);
    }
}
