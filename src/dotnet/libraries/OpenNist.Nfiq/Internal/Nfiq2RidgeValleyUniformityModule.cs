namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2RidgeValleyUniformityModule
{
    private const int s_blockSize = 32;
    private const double s_segmentationThreshold = 0.1;
    private const int s_slantedBlockWidth = 32;
    private const int s_slantedBlockHeight = 16;
    private const bool s_padRotatedBlock = true;
    private const string s_featurePrefix = "RVUP_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.5, 0.667, 0.8, 1.0, 1.25, 1.5, 2.0, 24.0, 30.0];

    public static Nfiq2RidgeValleyUniformityResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        return Compute(fingerprintImage, null);
    }

    public static Nfiq2RidgeValleyUniformityResult Compute(
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

            AppendModuleRatios(values, ridgeValley.RidgeValleyPattern);
        }

        var valueArray = values.ToArray();
        var features = Nfiq2FeatureMath.CreateHistogramFeatures(s_featurePrefix, HistogramBoundaries, valueArray, 10);
        return new(valueArray, features);
    }

    private static void AppendModuleRatios(List<double> destination, ReadOnlySpan<byte> ridgeValleyPattern)
    {
        if (ridgeValleyPattern.Length < 2)
        {
            return;
        }

        var changeLength = ridgeValleyPattern.Length - 1;
        Span<byte> change = stackalloc byte[changeLength];
        for (var index = 0; index < changeLength; index++)
        {
            var previousIndex = index == 0 ? ridgeValleyPattern.Length - 1 : index - 1;
            change[index] = ridgeValleyPattern[index] != ridgeValleyPattern[previousIndex] ? (byte)1 : (byte)0;
        }

        Span<int> changeIndices = stackalloc int[changeLength];
        var changeCount = 0;
        for (var index = 1; index < changeLength; index++)
        {
            if (change[index] == 1)
            {
                changeIndices[changeCount++] = index - 1;
            }
        }

        if (changeCount == 0)
        {
            return;
        }

        var ridgeValleyCompleteStart = changeIndices[0] + 1;
        var ridgeValleyCompleteEnd = changeIndices[changeCount - 1];
        if (ridgeValleyCompleteStart >= ridgeValleyCompleteEnd)
        {
            return;
        }

        var changeIndexCompleteCount = changeCount - 1;
        if (changeIndexCompleteCount <= 1)
        {
            return;
        }

        Span<int> changeIndexComplete = stackalloc int[changeIndexCompleteCount];
        for (var index = 1; index < changeCount; index++)
        {
            changeIndexComplete[index - 1] = changeIndices[index] - changeIndices[0];
        }

        Span<int> changeComplete2 = stackalloc int[changeIndexCompleteCount - 1];
        var changeComplete2Count = 0;
        for (var index = changeIndexCompleteCount - 1; index > 0; index--)
        {
            changeComplete2[changeComplete2Count++] = changeIndexComplete[index] - changeIndexComplete[index - 1];
        }

        var beginsWithRidge = ridgeValleyPattern[ridgeValleyCompleteStart];
        for (var index = 0; index < changeComplete2Count - 1; index++)
        {
            var ratio = changeComplete2[index] / (double)changeComplete2[index + 1];
            if (index >= beginsWithRidge && ((index - beginsWithRidge) % 2) == 0)
            {
                ratio = 1.0 / ratio;
            }

            if (!double.IsNaN(ratio))
            {
                destination.Add(ratio);
            }
        }
    }
}

internal sealed record Nfiq2RidgeValleyUniformityResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
