namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal static class Nfiq2QualityMapModule
{
    private const int LocalRegionSquare = 32;
    private const string CoherenceRelative = "OrientationMap_ROIFilter_CoherenceRel";
    private const string CoherenceSum = "OrientationMap_ROIFilter_CoherenceSum";

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

        var roiBlocks = roiResult.RoiBlocks
            .ToFrozenSet();

        double coherenceSum = 0.0;

        for (var row = 0; row < fingerprintImage.Height; row += LocalRegionSquare)
        {
            for (var column = 0; column < fingerprintImage.Width; column += LocalRegionSquare)
            {
                var actualWidth = Math.Min(LocalRegionSquare, fingerprintImage.Width - column);
                var actualHeight = Math.Min(LocalRegionSquare, fingerprintImage.Height - row);
                var roiBlock = new Nfiq2RegionBlock(column, row, actualWidth, actualHeight);
                if (!roiBlocks.Contains(roiBlock))
                {
                    continue;
                }

                var coherence = ComputeBlockCoherence(
                    fingerprintImage.Pixels.Span,
                    fingerprintImage.Width,
                    row,
                    column,
                    actualWidth,
                    actualHeight);

                if (double.IsNaN(coherence))
                {
                    coherence = 0.0;
                }

                coherenceSum += coherence;
            }
        }

        var coherenceRelative = roiResult.RoiBlocks.Count == 0
            ? 0.0
            : coherenceSum / roiResult.RoiBlocks.Count;

        return new(
            coherenceRelative,
            coherenceSum,
            new Dictionary<string, double>(2, StringComparer.Ordinal)
            {
                [CoherenceRelative] = coherenceRelative,
                [CoherenceSum] = coherenceSum,
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
        var gradientX = Nfiq2FeatureMath.ComputeNumericalGradientX(image, imageWidth, row, column, blockWidth, blockHeight);
        var gradientY = Nfiq2FeatureMath.ComputeNumericalGradientY(image, imageWidth, row, column, blockWidth, blockHeight);

        double sumY = 0.0;
        double sumX = 0.0;
        double coherenceDenominator = 0.0;

        for (var y = 0; y < blockHeight; y++)
        {
            var rowOffset = y * blockWidth;
            for (var x = 0; x < blockWidth; x++)
            {
                var gx = gradientX[rowOffset + x];
                var gy = gradientY[rowOffset + x];

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
