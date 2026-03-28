namespace OpenNist.Nfiq.Internal.Modules;

using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Support;

internal static class Nfiq2OclHistogramModule
{
    private const int s_localRegionSquare = 32;
    private const string s_featurePrefix = "OCL_Bin10_";
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

        for (var row = 0; row < fingerprintImage.Height; row += s_localRegionSquare)
        {
            for (var column = 0; column < fingerprintImage.Width; column += s_localRegionSquare)
            {
                var actualWidth = Math.Min(s_localRegionSquare, fingerprintImage.Width - column);
                var actualHeight = Math.Min(s_localRegionSquare, fingerprintImage.Height - row);
                if (actualWidth != s_localRegionSquare || actualHeight != s_localRegionSquare)
                {
                    continue;
                }

                if (!TryGetBlockOrientationCertainty(
                    source,
                    fingerprintImage.Width,
                    row,
                    column,
                    s_localRegionSquare,
                    out var orientationCertainty))
                {
                    continue;
                }

                values.Add(orientationCertainty);
            }
        }

        var valueArray = values.ToArray();
        var features = Nfiq2FeatureMath.CreateHistogramFeatures(s_featurePrefix, HistogramBoundaries, valueArray, 10);
        return new(valueArray, features);
    }

    internal static bool TryGetBlockOrientationCertainty(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockSize,
        out double orientationCertainty)
    {
        var a = 0.0;
        var b = 0.0;
        var c = 0.0;

        for (var y = 0; y < blockSize; y++)
        {
            var imageRow = row + y;
            var imageRowOffset = imageRow * imageWidth;

            for (var x = 0; x < blockSize; x++)
            {
                var imageColumn = column + x;
                var gx = Nfiq2FeatureMath.ComputeGradientXAt(image, imageRowOffset, imageColumn, x, blockSize);
                var gy = Nfiq2FeatureMath.ComputeGradientYAt(image, imageWidth, imageRow, imageColumn, y, blockSize);
                a += gx * gx;
                b += gy * gy;
                c += gx * gy;
            }
        }

        var pixelCount = blockSize * blockSize;
        a /= pixelCount;
        b /= pixelCount;
        c /= pixelCount;

        var eigenTerm = Math.Sqrt((a - b) * (a - b) + 4.0 * c * c);
        var eigenValueMax = (a + b + eigenTerm) / 2.0;
        if (Math.Abs(eigenValueMax) < double.Epsilon)
        {
            orientationCertainty = 0.0;
            return false;
        }

        var eigenValueMin = (a + b - eigenTerm) / 2.0;
        orientationCertainty = 1.0 - eigenValueMin / eigenValueMax;
        return true;
    }
}

internal sealed record Nfiq2OclHistogramResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
