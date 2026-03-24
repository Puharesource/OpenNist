namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FrequencyDomainAnalysisModule
{
    private const int BlockSize = 32;
    private const double SegmentationThreshold = 0.1;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;
    private const bool PadRotatedBlock = true;
    private const string FeaturePrefix = "FDA_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.268, 0.304, 0.33, 0.355, 0.38, 0.407, 0.44, 0.50, 1.0];

    public static Nfiq2FrequencyDomainAnalysisResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var geometry = Nfiq2RidgeValleySupport.GetOverlappingBlockGeometry(BlockSize, SlantedBlockWidth, SlantedBlockHeight);
        var segmentationMask = Nfiq2BlockFeatureSupport.CreateSegmentationMask(fingerprintImage, BlockSize, SegmentationThreshold);
        var values = new List<double>();

        foreach (var origin in Nfiq2RidgeValleySupport.EnumerateInteriorBlockOrigins(
                     fingerprintImage.Width,
                     fingerprintImage.Height,
                     BlockSize,
                     SlantedBlockWidth,
                     SlantedBlockHeight))
        {
            if (!Nfiq2BlockFeatureSupport.AreAllNonZero(segmentationMask, fingerprintImage.Width, origin.Row, origin.Column, BlockSize, BlockSize))
            {
                continue;
            }

            var orientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                origin.Row,
                origin.Column,
                BlockSize,
                BlockSize);
            var rotated = Nfiq2RidgeValleySupport.GetRotatedBlock(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                origin.Row - geometry.BlockOffset,
                origin.Column - geometry.BlockOffset,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                orientation + (Math.PI / 2.0),
                PadRotatedBlock);
            var blockCropped = Nfiq2RidgeValleySupport.CropCenteredRotatedBlock(
                rotated,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                SlantedBlockHeight,
                SlantedBlockWidth);
            values.Add(Nfiq2FrequencyDomainSupport.ComputeFrequencyDomainAnalysisScore(
                blockCropped,
                SlantedBlockHeight,
                SlantedBlockWidth));
        }

        if (values.Count < 10)
        {
            throw new Nfiq2Exception(
                "Cannot compute Frequency Domain Analysis (FDA): Not enough data to generate histogram bins (is the image blank?)");
        }

        var features = Nfiq2FeatureMath.CreateHistogramFeatures(FeaturePrefix, HistogramBoundaries, values.ToArray(), 10);
        return new(values, features);
    }
}

internal sealed record Nfiq2FrequencyDomainAnalysisResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
