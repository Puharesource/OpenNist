namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FrequencyDomainAnalysisModule
{
    private const int s_blockSize = 32;
    private const double s_segmentationThreshold = 0.1;
    private const int s_slantedBlockWidth = 32;
    private const int s_slantedBlockHeight = 16;
    private const bool s_padRotatedBlock = true;
    private const string s_featurePrefix = "FDA_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.268, 0.304, 0.33, 0.355, 0.38, 0.407, 0.44, 0.50, 1.0];

    public static Nfiq2FrequencyDomainAnalysisResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        return Compute(fingerprintImage, null);
    }

    public static Nfiq2FrequencyDomainAnalysisResult Compute(
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
                s_slantedBlockHeight,
                s_slantedBlockWidth,
                orientation + Math.PI / 2.0,
                s_padRotatedBlock);
            values.Add(Nfiq2FrequencyDomainSupport.ComputeFrequencyDomainAnalysisScore(
                blockCropped,
                s_slantedBlockHeight,
                s_slantedBlockWidth));
        }

        if (values.Count < 10)
        {
            throw new Nfiq2Exception(
                "Cannot compute Frequency Domain Analysis (FDA): Not enough data to generate histogram bins (is the image blank?)");
        }

        var valueArray = values.ToArray();
        var features = Nfiq2FeatureMath.CreateHistogramFeatures(s_featurePrefix, HistogramBoundaries, valueArray, 10);
        return new(valueArray, features);
    }
}

internal sealed record Nfiq2FrequencyDomainAnalysisResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
