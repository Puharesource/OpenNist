namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2LocalClarityModule
{
    private const int s_blockSize = 32;
    private const double s_segmentationThreshold = 0.1;
    private const int s_slantedBlockWidth = 32;
    private const int s_slantedBlockHeight = 16;
    private const bool s_padRotatedBlock = false;
    private const string s_featurePrefix = "LCS_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.0, 0.70, 0.74, 0.77, 0.79, 0.81, 0.83, 0.85, 0.87];

    public static Nfiq2LocalClarityResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        return Compute(fingerprintImage, null);
    }

    public static Nfiq2LocalClarityResult Compute(
        Nfiq2FingerprintImage fingerprintImage,
        Nfiq2RidgeValleyFeatureContext? context)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        context ??= Nfiq2RidgeValleySupport.CreateFeatureContext(
            fingerprintImage,
            s_blockSize,
            s_segmentationThreshold,
            s_slantedBlockWidth,
            s_slantedBlockHeight);

        var geometry = context.Geometry;
        var values = new List<double>();

        foreach (var origin in context.ValidOrigins)
        {
            var orientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                origin.Row,
                origin.Column,
                s_blockSize,
                s_blockSize);
            var blockCropped = Nfiq2RidgeValleySupport.GetCenteredRotatedBlock(
                fingerprintImage.Pixels.Span,
                fingerprintImage.Width,
                origin.Row - geometry.BlockOffset,
                origin.Column - geometry.BlockOffset,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                s_slantedBlockWidth,
                s_slantedBlockHeight,
                orientation,
                s_padRotatedBlock);
            var ridgeValley = Nfiq2RidgeValleySupport.GetRidgeValleyStructure(blockCropped, s_slantedBlockWidth, s_slantedBlockHeight);
            values.Add(Nfiq2RidgeValleySupport.ComputeLocalClarityScore(
                blockCropped,
                s_slantedBlockWidth,
                s_slantedBlockHeight,
                ridgeValley.RidgeValleyPattern,
                ridgeValley.TrendLine,
                fingerprintImage.PixelsPerInch));
        }

        var valueArray = values.ToArray();
        var features = Nfiq2FeatureMath.CreateHistogramFeatures(s_featurePrefix, HistogramBoundaries, valueArray, 10);
        return new(valueArray, features);
    }
}

internal sealed record Nfiq2LocalClarityResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
