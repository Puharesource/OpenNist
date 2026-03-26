namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal static class Nfiq2QualityMapModule
{
    private const string s_coherenceRelative = "OrientationMap_ROIFilter_CoherenceRel";
    private const string s_coherenceSum = "OrientationMap_ROIFilter_CoherenceSum";

    public static Nfiq2QualityMapResult Compute(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2ImgProcRoiResult roiResult)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);
        ArgumentNullException.ThrowIfNull(roiResult);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var coherenceSum = 0.0;
        foreach (var roiBlock in roiResult.RoiBlocks)
        {
            var coherence = ComputeBlockCoherence(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                roiBlock.Y,
                roiBlock.X,
                roiBlock.Width,
                roiBlock.Height);

            if (double.IsNaN(coherence))
            {
                coherence = 0.0;
            }

            coherenceSum += coherence;
        }

        var coherenceRelative = roiResult.RoiBlocks.Count == 0
            ? 0.0
            : coherenceSum / roiResult.RoiBlocks.Count;

        return new(
            coherenceRelative,
            coherenceSum,
            new Dictionary<string, double>(2, StringComparer.Ordinal)
            {
                [s_coherenceRelative] = coherenceRelative,
                [s_coherenceSum] = coherenceSum,
            }.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static double ComputeBlockCoherence(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var sumY = 0.0;
        var sumX = 0.0;
        var coherenceDenominator = 0.0;

        for (var y = 0; y < blockHeight; y++)
        {
            var imageRow = row + y;
            var imageRowOffset = imageRow * imageWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                var imageColumn = column + x;
                var gx = Nfiq2FeatureMath.ComputeGradientXAt(image, imageRowOffset, imageColumn, x, blockWidth);
                var gy = Nfiq2FeatureMath.ComputeGradientYAt(image, imageWidth, imageRow, imageColumn, y, blockHeight);

                var twiceProduct = 2.0 * gx * gy;
                if (!double.IsNaN(twiceProduct))
                {
                    sumY += twiceProduct;
                }

                var deltaSquares = (gx * gx) - (gy * gy);
                if (!double.IsNaN(deltaSquares))
                {
                    sumX += deltaSquares;
                }

                coherenceDenominator += Math.Sqrt((twiceProduct * twiceProduct) + (deltaSquares * deltaSquares));
            }
        }

        var coherenceNumerator = Math.Sqrt((sumX * sumX) + (sumY * sumY));
        if (double.IsNaN(coherenceNumerator))
        {
            coherenceNumerator = 0.0;
        }

        if (double.IsNaN(coherenceDenominator))
        {
            coherenceDenominator = 0.0;
        }

        return Math.Abs(coherenceDenominator) < double.Epsilon
            ? 0.0
            : coherenceNumerator / coherenceDenominator;
    }
}

internal sealed record Nfiq2QualityMapResult(
    double CoherenceRelative,
    double CoherenceSum,
    IReadOnlyDictionary<string, double> Features);
