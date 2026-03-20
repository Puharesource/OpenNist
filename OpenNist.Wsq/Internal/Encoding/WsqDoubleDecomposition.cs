namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

internal static class WsqDoubleDecomposition
{
    private static readonly double[] s_referenceLowPassFilter =
    [
        0.03782845550699546,
        -0.02384946501938000,
        -0.11062440441842342,
        0.37740285561265380,
        0.85269867900940344,
        0.37740285561265380,
        -0.11062440441842342,
        -0.02384946501938000,
        0.03782845550699546,
    ];

    private static readonly double[] s_referenceHighPassFilter =
    [
        0.06453888262893845,
        -0.04068941760955844,
        -0.41809227322221221,
        0.78848561640566439,
        -0.41809227322221221,
        -0.04068941760955844,
        0.06453888262893845,
    ];

    public static double[] Decompose(
        double[] waveletData,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        var lowPassFilter = s_referenceLowPassFilter.ToArray();
        var highPassFilter = s_referenceHighPassFilter.ToArray();
        var temporaryBuffer = new double[waveletData.Length];

        for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
        {
            var node = waveletTree[nodeIndex];
            var baseOffset = node.Y * width + node.X;

            GetLets(
                temporaryBuffer,
                0,
                waveletData,
                baseOffset,
                node.Height,
                node.Width,
                width,
                1,
                highPassFilter,
                lowPassFilter,
                node.InvertRows);

            GetLets(
                waveletData,
                baseOffset,
                temporaryBuffer,
                0,
                node.Width,
                node.Height,
                1,
                width,
                highPassFilter,
                lowPassFilter,
                node.InvertColumns);
        }

        return waveletData;
    }

    private static void GetLets(
        Span<double> destination,
        int destinationBaseOffset,
        ReadOnlySpan<double> source,
        int sourceBaseOffset,
        int lineCount,
        int lineLength,
        int linePitch,
        int sampleStride,
        double[] highPassFilter,
        double[] lowPassFilter,
        bool invertSubbands)
    {
        var destinationSamples = destination[destinationBaseOffset..];
        var sourceSamples = source[sourceBaseOffset..];
        var lineLengthIsOdd = lineLength % 2;
        var lowPassSampleCount = lineLengthIsOdd != 0
            ? (lineLength + 1) / 2
            : lineLength / 2;
        var highPassSampleCount = lineLengthIsOdd != 0
            ? lowPassSampleCount - 1
            : lowPassSampleCount;
        var sampleStrideForward = sampleStride;
        var sampleStrideBackward = -sampleStrideForward;
        var filterLengthIsOdd = lowPassFilter.Length % 2;
        var lowPassCenterOffset = filterLengthIsOdd != 0
            ? (lowPassFilter.Length - 1) / 2
            : lowPassFilter.Length / 2 - 2;
        var highPassCenterOffset = filterLengthIsOdd != 0
            ? (highPassFilter.Length - 1) / 2 - 1
            : highPassFilter.Length / 2 - 2;
        var initialLowPassLeftEdgeState = filterLengthIsOdd != 0 ? 0 : 1;
        var initialHighPassLeftEdgeState = filterLengthIsOdd != 0 ? 0 : 1;
        var initialLowPassRightEdgeState = 0;
        var initialHighPassRightEdgeState = 0;

        if (filterLengthIsOdd == 0)
        {
            initialLowPassRightEdgeState = 1;
            initialHighPassRightEdgeState = 1;

            if (lowPassCenterOffset == -1)
            {
                lowPassCenterOffset = 0;
                initialLowPassLeftEdgeState = 0;
            }

            if (highPassCenterOffset == -1)
            {
                highPassCenterOffset = 0;
                initialHighPassLeftEdgeState = 0;
            }

            for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
            {
                highPassFilter[filterIndex] *= -1.0;
            }
        }

        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            int lowPassWriteIndex;
            int highPassWriteIndex;

            if (invertSubbands)
            {
                highPassWriteIndex = lineIndex * linePitch;
                lowPassWriteIndex = highPassWriteIndex + highPassSampleCount * sampleStride;
            }
            else
            {
                lowPassWriteIndex = lineIndex * linePitch;
                highPassWriteIndex = lowPassWriteIndex + lowPassSampleCount * sampleStride;
            }

            var firstSourceIndex = lineIndex * linePitch;
            var lastSourceIndex = firstSourceIndex + (lineLength - 1) * sampleStride;

            var lowPassSourceIndex = firstSourceIndex + lowPassCenterOffset * sampleStride;
            var lowPassSourceStride = sampleStrideBackward;
            var lowPassLeftEdgeState = initialLowPassLeftEdgeState;
            var lowPassRightEdgeState = initialLowPassRightEdgeState;

            var highPassSourceIndex = firstSourceIndex + highPassCenterOffset * sampleStride;
            var highPassSourceStride = sampleStrideBackward;
            var highPassLeftEdgeState = initialHighPassLeftEdgeState;
            var highPassRightEdgeState = initialHighPassRightEdgeState;

            for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
            {
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    lowPassFilter,
                    lowPassSourceIndex,
                    lowPassSourceStride,
                    firstSourceIndex,
                    lastSourceIndex,
                    ref lowPassLeftEdgeState,
                    ref lowPassRightEdgeState,
                    sampleStrideForward,
                    sampleStrideBackward);
                lowPassWriteIndex += sampleStride;

                destinationSamples[highPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    highPassFilter,
                    highPassSourceIndex,
                    highPassSourceStride,
                    firstSourceIndex,
                    lastSourceIndex,
                    ref highPassLeftEdgeState,
                    ref highPassRightEdgeState,
                    sampleStrideForward,
                    sampleStrideBackward);
                highPassWriteIndex += sampleStride;

                AdvanceScanIndex(
                    firstSourceIndex,
                    sampleStrideForward,
                    ref lowPassSourceIndex,
                    ref lowPassSourceStride,
                    ref lowPassLeftEdgeState);
                AdvanceScanIndex(
                    firstSourceIndex,
                    sampleStrideForward,
                    ref lowPassSourceIndex,
                    ref lowPassSourceStride,
                    ref lowPassLeftEdgeState);
                AdvanceScanIndex(
                    firstSourceIndex,
                    sampleStrideForward,
                    ref highPassSourceIndex,
                    ref highPassSourceStride,
                    ref highPassLeftEdgeState);
                AdvanceScanIndex(
                    firstSourceIndex,
                    sampleStrideForward,
                    ref highPassSourceIndex,
                    ref highPassSourceStride,
                    ref highPassLeftEdgeState);
            }

            if (lineLengthIsOdd != 0)
            {
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    lowPassFilter,
                    lowPassSourceIndex,
                    lowPassSourceStride,
                    firstSourceIndex,
                    lastSourceIndex,
                    ref lowPassLeftEdgeState,
                    ref lowPassRightEdgeState,
                    sampleStrideForward,
                    sampleStrideBackward);
            }
        }

        if (filterLengthIsOdd == 0)
        {
            for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
            {
                highPassFilter[filterIndex] *= -1.0;
            }
        }
    }

    private static double ComputeFilteredSample(
        ReadOnlySpan<double> sourceSamples,
        ReadOnlySpan<double> filter,
        int scanIndex,
        int scanStride,
        int sourceStartIndex,
        int sourceEndIndex,
        ref int nextLeftEdgeState,
        ref int nextRightEdgeState,
        int sampleStrideForward,
        int sampleStrideBackward)
    {
        var currentSourceIndex = scanIndex;
        var currentSourceStride = scanStride;
        var currentLeftEdgeState = nextLeftEdgeState;
        var currentRightEdgeState = nextRightEdgeState;
        var filteredSample = sourceSamples[currentSourceIndex] * filter[0];

        for (var filterIndex = 1; filterIndex < filter.Length; filterIndex++)
        {
            if (currentSourceIndex == sourceStartIndex)
            {
                if (currentLeftEdgeState != 0)
                {
                    currentSourceStride = 0;
                    currentLeftEdgeState = 0;
                }
                else
                {
                    currentSourceStride = sampleStrideForward;
                }
            }

            if (currentSourceIndex == sourceEndIndex)
            {
                if (currentRightEdgeState != 0)
                {
                    currentSourceStride = 0;
                    currentRightEdgeState = 0;
                }
                else
                {
                    currentSourceStride = sampleStrideBackward;
                }
            }

            currentSourceIndex += currentSourceStride;
            filteredSample = Math.FusedMultiplyAdd(
                sourceSamples[currentSourceIndex],
                filter[filterIndex],
                filteredSample);
        }

        return filteredSample;
    }

    private static void AdvanceScanIndex(
        int sourceStartIndex,
        int sampleStrideForward,
        ref int scanIndex,
        ref int scanStride,
        ref int nextLeftEdgeState)
    {
        if (scanIndex == sourceStartIndex)
        {
            if (nextLeftEdgeState != 0)
            {
                scanStride = 0;
                nextLeftEdgeState = 0;
            }
            else
            {
                scanStride = sampleStrideForward;
            }
        }

        scanIndex += scanStride;
    }
}
