namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

internal static class WsqDecomposition
{
    public static float[] Decompose(
        float[] waveletData,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        var lowPassFilter = GetFilterSpan(transformTable.LowPassFilterCoefficients);
        var highPassFilter = GetFilterSpan(transformTable.HighPassFilterCoefficients);
        var temporaryBuffer = new float[waveletData.Length];

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

    private static ReadOnlySpan<float> GetFilterSpan(IReadOnlyList<float> coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        return coefficients is float[] array
            ? array
            : coefficients.ToArray();
    }

    private static void GetLets(
        Span<float> destination,
        int destinationBaseOffset,
        ReadOnlySpan<float> source,
        int sourceBaseOffset,
        int lineCount,
        int lineLength,
        int linePitch,
        int sampleStride,
        ReadOnlySpan<float> highPassFilter,
        ReadOnlySpan<float> lowPassFilter,
        bool invertSubbands)
    {
        var destinationSamples = destination[destinationBaseOffset..];
        var sourceSamples = source[sourceBaseOffset..];
        var lineLengthIsOdd = lineLength % 2 != 0;
        var lowPassSampleCount = lineLengthIsOdd
            ? (lineLength + 1) / 2
            : lineLength / 2;
        var highPassSampleCount = lineLengthIsOdd
            ? lowPassSampleCount - 1
            : lowPassSampleCount;
        var sampleStrideBackward = -sampleStride;
        var lowPassFilterLengthIsOdd = lowPassFilter.Length % 2 != 0;
        var lowPassCenterOffset = lowPassFilterLengthIsOdd
            ? (lowPassFilter.Length - 1) / 2
            : lowPassFilter.Length / 2 - 2;
        var highPassCenterOffset = lowPassFilterLengthIsOdd
            ? (highPassFilter.Length - 1) / 2 - 1
            : highPassFilter.Length / 2 - 2;
        var initialLowPassLeftEdgeState = lowPassFilterLengthIsOdd ? 0 : 1;
        var initialHighPassLeftEdgeState = lowPassFilterLengthIsOdd ? 0 : 1;

        if (!lowPassFilterLengthIsOdd)
        {
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
        }

        var activeHighPassFilter = GetActiveHighPassFilter(highPassFilter, lowPassFilterLengthIsOdd);

        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            var sourceStartIndex = lineIndex * linePitch;
            var sourceEndIndex = sourceStartIndex + (lineLength - 1) * sampleStride;

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

            var lowPassScanIndex = sourceStartIndex + lowPassCenterOffset * sampleStride;
            var highPassScanIndex = sourceStartIndex + highPassCenterOffset * sampleStride;
            var lowPassScanStride = sampleStrideBackward;
            var highPassScanStride = sampleStrideBackward;
            var nextLowPassLeftEdgeState = initialLowPassLeftEdgeState;
            var nextLowPassRightEdgeState = lowPassFilterLengthIsOdd ? 0 : 1;
            var nextHighPassLeftEdgeState = initialHighPassLeftEdgeState;
            var nextHighPassRightEdgeState = lowPassFilterLengthIsOdd ? 0 : 1;

            for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
            {
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    lowPassFilter,
                    lowPassScanIndex,
                    lowPassScanStride,
                    sourceStartIndex,
                    sourceEndIndex,
                    ref nextLowPassLeftEdgeState,
                    ref nextLowPassRightEdgeState,
                    sampleStride,
                    sampleStrideBackward);
                lowPassWriteIndex += sampleStride;

                destinationSamples[highPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    activeHighPassFilter,
                    highPassScanIndex,
                    highPassScanStride,
                    sourceStartIndex,
                    sourceEndIndex,
                    ref nextHighPassLeftEdgeState,
                    ref nextHighPassRightEdgeState,
                    sampleStride,
                    sampleStrideBackward);
                highPassWriteIndex += sampleStride;

                AdvanceScanIndex(sourceStartIndex, sampleStride, ref lowPassScanIndex, ref lowPassScanStride, ref nextLowPassLeftEdgeState);
                AdvanceScanIndex(sourceStartIndex, sampleStride, ref lowPassScanIndex, ref lowPassScanStride, ref nextLowPassLeftEdgeState);
                AdvanceScanIndex(sourceStartIndex, sampleStride, ref highPassScanIndex, ref highPassScanStride, ref nextHighPassLeftEdgeState);
                AdvanceScanIndex(sourceStartIndex, sampleStride, ref highPassScanIndex, ref highPassScanStride, ref nextHighPassLeftEdgeState);
            }

            if (lineLengthIsOdd)
            {
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample(
                    sourceSamples,
                    lowPassFilter,
                    lowPassScanIndex,
                    lowPassScanStride,
                    sourceStartIndex,
                    sourceEndIndex,
                    ref nextLowPassLeftEdgeState,
                    ref nextLowPassRightEdgeState,
                    sampleStride,
                    sampleStrideBackward);
            }
        }
    }

    private static ReadOnlySpan<float> GetActiveHighPassFilter(
        ReadOnlySpan<float> highPassFilter,
        bool lowPassFilterLengthIsOdd)
    {
        if (lowPassFilterLengthIsOdd)
        {
            return highPassFilter;
        }

        var negatedHighPassFilter = GC.AllocateUninitializedArray<float>(highPassFilter.Length);

        for (var filterIndex = 0; filterIndex < highPassFilter.Length; filterIndex++)
        {
            negatedHighPassFilter[filterIndex] = -highPassFilter[filterIndex];
        }

        return negatedHighPassFilter;
    }

    private static float ComputeFilteredSample(
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> filter,
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
            // The NBIS reference encoder contracts this hot multiply-add path on ARM64.
            filteredSample = MathF.FusedMultiplyAdd(
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
