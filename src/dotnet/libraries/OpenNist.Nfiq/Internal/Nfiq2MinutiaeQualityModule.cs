namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2MinutiaeQualityModule
{
    private const int s_localRegionSquare = 32;
    private const string s_muFeatureName = "FJFXPos_Mu_MinutiaeQuality_2";
    private const string s_oclFeatureName = "FJFXPos_OCL_MinutiaeQuality_80";

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
                [s_muFeatureName] = 0,
                [s_oclFeatureName] = 0,
            };
        }

        ComputeGlobalMeanAndStdDev(fingerprintImage.Pixels.Span, out var imageMean, out var imageStdDev);

        var muRange2 = 0;
        var oclRange80 = 0;
        for (var index = 0; index < minutiae.Count; index++)
        {
            var minutia = minutiae[index];
            var muQuality = ComputeMuMinutiaQuality(fingerprintImage, minutia, imageMean, imageStdDev);
            if (muQuality is > 0.0 and <= 0.5)
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
            [s_muFeatureName] = muRange2 / (double)minutiae.Count,
            [s_oclFeatureName] = oclRange80 / (double)minutiae.Count,
        };
    }

    private static double ComputeMuMinutiaQuality(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2Minutia minutia,
        double imageMean,
        double imageStdDev)
    {
        var leftX = Math.Max(0, minutia.X - s_localRegionSquare / 2);
        var topY = Math.Max(0, minutia.Y - s_localRegionSquare / 2);
        var takenWidth = Math.Min(s_localRegionSquare, fingerprintImage.Width - leftX);
        var takenHeight = Math.Min(s_localRegionSquare, fingerprintImage.Height - topY);
        var pixels = fingerprintImage.Pixels.Span;
        var blockSum = 0.0;
        for (var row = 0; row < takenHeight; row++)
        {
            var rowStart = (topY + row) * fingerprintImage.Width + leftX;
            var rowPixels = pixels.Slice(rowStart, takenWidth);
            for (var column = 0; column < rowPixels.Length; column++)
            {
                blockSum += rowPixels[column];
            }
        }

        var blockMean = blockSum / (takenWidth * takenHeight);
        return (imageMean - blockMean) / imageStdDev;
    }

    private static int ComputeOclMinutiaQuality(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2Minutia minutia)
    {
        var leftX = Math.Max(0, minutia.X - s_localRegionSquare / 2);
        var topY = Math.Max(0, minutia.Y - s_localRegionSquare / 2);
        if (leftX + s_localRegionSquare > fingerprintImage.Width)
        {
            leftX = fingerprintImage.Width - s_localRegionSquare;
        }

        if (topY + s_localRegionSquare > fingerprintImage.Height)
        {
            topY = fingerprintImage.Height - s_localRegionSquare;
        }

        var hasValue = Nfiq2OclHistogramModule.TryGetBlockOrientationCertainty(
            fingerprintImage.Pixels.Span,
            fingerprintImage.Width,
            topY,
            leftX,
            s_localRegionSquare,
            out var ocl);
        if (!hasValue)
        {
            return 0;
        }

        return (int)(ocl * 100.0 + 0.5);
    }

    private static void ComputeGlobalMeanAndStdDev(ReadOnlySpan<byte> pixels, out double mean, out double stdDev)
    {
        var sum = 0.0;
        for (var index = 0; index < pixels.Length; index++)
        {
            sum += pixels[index];
        }

        mean = sum / pixels.Length;

        var variance = 0.0;
        for (var index = 0; index < pixels.Length; index++)
        {
            var delta = pixels[index] - mean;
            variance += delta * delta;
        }

        stdDev = Math.Sqrt(variance / pixels.Length);
    }
}
