namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2RidgeValleyUniformityModule
{
    private const int BlockSize = 32;
    private const double SegmentationThreshold = 0.1;
    private const int SlantedBlockWidth = 32;
    private const int SlantedBlockHeight = 16;
    private const bool PadRotatedBlock = true;
    private const string FeaturePrefix = "RVUP_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [0.5, 0.667, 0.8, 1.0, 1.25, 1.5, 2.0, 24.0, 30.0];

    public static Nfiq2RidgeValleyUniformityResult Compute(Nfiq2FingerprintImage fingerprintImage)
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
                orientation,
                PadRotatedBlock);
            var blockCropped = Nfiq2RidgeValleySupport.CropCenteredRotatedBlock(
                rotated,
                geometry.ExtractedBlockSize,
                geometry.ExtractedBlockSize,
                SlantedBlockWidth,
                SlantedBlockHeight);
            var ridgeValley = Nfiq2RidgeValleySupport.GetRidgeValleyStructure(blockCropped, SlantedBlockWidth, SlantedBlockHeight);

            foreach (var ratio in ComputeModuleRatios(ridgeValley.RidgeValleyPattern))
            {
                values.Add(ratio);
            }
        }

        var features = Nfiq2FeatureMath.CreateHistogramFeatures(FeaturePrefix, HistogramBoundaries, values.ToArray(), 10);
        return new(values, features);
    }

    private static IEnumerable<double> ComputeModuleRatios(IReadOnlyList<byte> ridgeValleyPattern)
    {
        if (ridgeValleyPattern.Count < 2)
        {
            yield break;
        }

        var change = new List<byte>(ridgeValleyPattern.Count - 1);
        for (var index = 0; index < ridgeValleyPattern.Count - 1; index++)
        {
            var previousIndex = index == 0 ? ridgeValleyPattern.Count - 1 : index - 1;
            change.Add(ridgeValleyPattern[index] != ridgeValleyPattern[previousIndex] ? (byte)1 : (byte)0);
        }

        var changeIndex = new List<int>();
        for (var index = 1; index < change.Count; index++)
        {
            if (change[index] == 1)
            {
                changeIndex.Add(index - 1);
            }
        }

        if (changeIndex.Count == 0)
        {
            yield break;
        }

        var ridgeValleyComplete = new List<byte>();
        for (var index = changeIndex[0] + 1; index < changeIndex[^1]; index++)
        {
            ridgeValleyComplete.Add(ridgeValleyPattern[index]);
        }

        if (ridgeValleyComplete.Count == 0)
        {
            yield break;
        }

        var changeIndexComplete = new List<int>();
        for (var index = 1; index < changeIndex.Count; index++)
        {
            changeIndexComplete.Add(changeIndex[index] - changeIndex[0]);
        }

        if (changeIndexComplete.Count <= 1)
        {
            yield break;
        }

        var changeComplete2 = new List<int>(changeIndexComplete.Count - 1);
        for (var index = changeIndexComplete.Count - 1; index > 0; index--)
        {
            changeComplete2.Add(changeIndexComplete[index] - changeIndexComplete[index - 1]);
        }

        var ratios = new List<double>();
        for (var index = 0; index < changeComplete2.Count - 1; index++)
        {
            ratios.Add(changeComplete2[index] / (double)changeComplete2[index + 1]);
        }

        var beginsWithRidge = ridgeValleyComplete[0];
        for (var index = beginsWithRidge; index < ratios.Count; index += 2)
        {
            ratios[index] = 1.0 / ratios[index];
        }

        for (var index = 0; index < ratios.Count; index++)
        {
            var ratio = ratios[index];
            if (!double.IsNaN(ratio))
            {
                yield return ratio;
            }
        }
    }
}

internal sealed record Nfiq2RidgeValleyUniformityResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);
