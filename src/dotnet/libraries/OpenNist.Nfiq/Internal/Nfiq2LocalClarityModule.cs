namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2LocalClarityModule
{
    private const int BlockSize = 32;
    private const double SegmentationThreshold = 0.1;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;
    private const bool PadRotatedBlock = false;
    private const string FeaturePrefix = "LCS_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.0, 0.70, 0.74, 0.77, 0.79, 0.81, 0.83, 0.85, 0.87];

    public static Nfiq2LocalClarityResult Compute(Nfiq2FingerprintImage fingerprintImage)
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
            var block = Nfiq2RidgeValleySupport.ExtractBlock(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                origin.Row - geometry.BlockOffset,
                origin.Column - geometry.BlockOffset,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize);
            var rotated = Nfiq2RidgeValleySupport.GetRotatedBlock(
                block,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                orientation,
                PadRotatedBlock);
            var blockCropped = Nfiq2RidgeValleySupport.CropCenteredRotatedBlock(
                rotated,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                SlantedBlockWidth,
                SlantedBlockHeight);
            var ridgeValley = Nfiq2RidgeValleySupport.GetRidgeValleyStructure(blockCropped, SlantedBlockWidth, SlantedBlockHeight);
            values.Add(Nfiq2RidgeValleySupport.ComputeLocalClarityScore(
                blockCropped,
                SlantedBlockWidth,
                SlantedBlockHeight,
                ridgeValley.RidgeValleyPattern.ToArray(),
                ridgeValley.TrendLine.ToArray(),
                fingerprintImage.PixelsPerInch));
        }

        var features = Nfiq2FeatureMath.CreateHistogramFeatures(FeaturePrefix, HistogramBoundaries, values.ToArray(), 10);
        return new(values, features);
    }
}

internal sealed record Nfiq2LocalClarityResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
