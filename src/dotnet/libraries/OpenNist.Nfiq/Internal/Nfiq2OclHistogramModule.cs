namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2OclHistogramModule
{
    private const int LocalRegionSquare = 32;
    private const string FeaturePrefix = "OCL_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.337, 0.479, 0.579, 0.655, 0.716, 0.766, 0.81, 0.852, 0.898];

    public static Nfiq2OclHistogramResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var source = fingerprintImage.Pixels.Span;
        var values = new List<double>();

        for (var row = 0; row < fingerprintImage.Height; row += LocalRegionSquare)
        {
            for (var column = 0; column < fingerprintImage.Width; column += LocalRegionSquare)
            {
                var actualWidth = Math.Min(LocalRegionSquare, fingerprintImage.Width - column);
                var actualHeight = Math.Min(LocalRegionSquare, fingerprintImage.Height - row);
                if (actualWidth != LocalRegionSquare || actualHeight != LocalRegionSquare)
                {
                    continue;
                }

                if (!TryGetBlockOrientationCertainty(
                    source,
                    fingerprintImage.Width,
                    row,
                    column,
                    LocalRegionSquare,
                    out var orientationCertainty))
                {
                    continue;
                }

                values.Add(orientationCertainty);
            }
        }

        var features = Nfiq2FeatureMath.CreateHistogramFeatures(FeaturePrefix, HistogramBoundaries, values.ToArray(), 10);
        return new(values.ToArray(), features);
    }

    internal static bool TryGetBlockOrientationCertainty(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockSize,
        out double orientationCertainty)
    {
        var gradientX = Nfiq2FeatureMath.ComputeNumericalGradientX(image, imageWidth, row, column, blockSize, blockSize);
        var gradientY = Nfiq2FeatureMath.ComputeNumericalGradientY(image, imageWidth, row, column, blockSize, blockSize);

        double a = 0.0;
        double b = 0.0;
        double c = 0.0;

        for (var index = 0; index < gradientX.Length; index++)
        {
            var gx = gradientX[index];
            var gy = gradientY[index];
            a += gx * gx;
            b += gy * gy;
            c += gx * gy;
        }

        var pixelCount = blockSize * blockSize;
        a /= pixelCount;
        b /= pixelCount;
        c /= pixelCount;

        var eigenTerm = Math.Sqrt(((a - b) * (a - b)) + (4.0 * c * c));
        var eigenValueMax = ((a + b) + eigenTerm) / 2.0;
        if (Math.Abs(eigenValueMax) < double.Epsilon)
        {
            orientationCertainty = 0.0;
            return false;
        }

        var eigenValueMin = ((a + b) - eigenTerm) / 2.0;
        orientationCertainty = 1.0 - (eigenValueMin / eigenValueMax);
        return true;
    }
}

internal sealed record Nfiq2OclHistogramResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
