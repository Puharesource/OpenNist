namespace OpenNist.Wsq.Internal.Encoding;

using System.Buffers;
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
        var activeHighPassFilter = GetActiveHighPassFilter(highPassFilter, lowPassFilter.Length % 2 != 0);
        var temporaryBuffer = ArrayPool<float>.Shared.Rent(waveletData.Length);
        try
        {
            for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
            {
                var node = waveletTree[nodeIndex];
                var baseOffset = node.Y * width + node.X;

                GetLets(
                    temporaryBuffer.AsSpan(0, waveletData.Length),
                    0,
                    waveletData,
                    baseOffset,
                    node.Height,
                    node.Width,
                    width,
                    1,
                    activeHighPassFilter,
                    lowPassFilter,
                    node.InvertRows);

                GetLets(
                    waveletData,
                    baseOffset,
                    temporaryBuffer.AsSpan(0, waveletData.Length),
                    0,
                    node.Width,
                    node.Height,
                    1,
                    width,
                    activeHighPassFilter,
                    lowPassFilter,
                    node.InvertColumns);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(temporaryBuffer);
        }

        return waveletData;
    }

    internal static WsqDecompositionTraceStep[] Trace(
        ReadOnlySpan<float> normalizedPixels,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable)
    {
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        var workingWaveletData = normalizedPixels.ToArray();
        var lowPassFilter = GetFilterSpan(transformTable.LowPassFilterCoefficients);
        var highPassFilter = GetFilterSpan(transformTable.HighPassFilterCoefficients);
        var activeHighPassFilter = GetActiveHighPassFilter(highPassFilter, lowPassFilter.Length % 2 != 0);
        var temporaryBuffer = new float[workingWaveletData.Length];
        var steps = new WsqDecompositionTraceStep[waveletTree.Length];

        for (var nodeIndex = 0; nodeIndex < waveletTree.Length; nodeIndex++)
        {
            var node = waveletTree[nodeIndex];
            var baseOffset = node.Y * width + node.X;

            GetLets(
                temporaryBuffer,
                0,
                workingWaveletData,
                baseOffset,
                node.Height,
                node.Width,
                width,
                1,
                activeHighPassFilter,
                lowPassFilter,
                node.InvertRows);

            var rowPassData = FlattenRowPassOutput(temporaryBuffer, width, node.Width, node.Height);

            GetLets(
                workingWaveletData,
                baseOffset,
                temporaryBuffer,
                0,
                node.Width,
                node.Height,
                1,
                width,
                activeHighPassFilter,
                lowPassFilter,
                node.InvertColumns);

            steps[nodeIndex] = new(
                nodeIndex,
                node,
                rowPassData,
                workingWaveletData.ToArray());
        }

        return steps;
    }

    private static ReadOnlySpan<float> GetFilterSpan(IReadOnlyList<float> coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        return coefficients is float[] array
            ? array
            : coefficients.ToArray();
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
        for (var index = 0; index < highPassFilter.Length; index++)
        {
            negatedHighPassFilter[index] = -highPassFilter[index];
        }

        return negatedHighPassFilter;
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
        ReadOnlySpan<float> activeHighPassFilter,
        ReadOnlySpan<float> lowPassFilter,
        bool invertSubbands)
    {
        if (lowPassFilter.Length == 9 && activeHighPassFilter.Length == 7)
        {
            GetLetsStandardOddFilters(
                destination,
                destinationBaseOffset,
                source,
                sourceBaseOffset,
                lineCount,
                lineLength,
                linePitch,
                sampleStride,
                activeHighPassFilter,
                lowPassFilter,
                invertSubbands);
            return;
        }

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
            ? (activeHighPassFilter.Length - 1) / 2 - 1
            : activeHighPassFilter.Length / 2 - 2;
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
                    activeHighPassFilter,
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
    }

    private static void GetLetsStandardOddFilters(
        Span<float> destination,
        int destinationBaseOffset,
        ReadOnlySpan<float> source,
        int sourceBaseOffset,
        int lineCount,
        int lineLength,
        int linePitch,
        int sampleStride,
        ReadOnlySpan<float> activeHighPassFilter,
        ReadOnlySpan<float> lowPassFilter,
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
        var lowPassCenterOffset = 4;
        var highPassCenterOffset = 2;

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
            var lowPassLeftEdgeState = 0;
            var lowPassRightEdgeState = 0;

            var highPassSourceIndex = firstSourceIndex + highPassCenterOffset * sampleStride;
            var highPassSourceStride = sampleStrideBackward;
            var highPassLeftEdgeState = 0;
            var highPassRightEdgeState = 0;

            for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
            {
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample9(
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

                destinationSamples[highPassWriteIndex] = ComputeFilteredSample7(
                    sourceSamples,
                    activeHighPassFilter,
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
                destinationSamples[lowPassWriteIndex] = ComputeFilteredSample9(
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
            filteredSample = MathF.FusedMultiplyAdd(
                sourceSamples[currentSourceIndex],
                filter[filterIndex],
                filteredSample);
        }

        return filteredSample;
    }

    private static float ComputeFilteredSample7(
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

        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[1], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[2], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[3], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[4], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[5], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[6], filteredSample);

        return filteredSample;
    }

    private static float ComputeFilteredSample9(
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

        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[1], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[2], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[3], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[4], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[5], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[6], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[7], filteredSample);
        AdvanceFilterSample(ref currentSourceIndex, ref currentSourceStride, ref currentLeftEdgeState, ref currentRightEdgeState, sourceStartIndex, sourceEndIndex, sampleStrideForward, sampleStrideBackward);
        filteredSample = MathF.FusedMultiplyAdd(sourceSamples[currentSourceIndex], filter[8], filteredSample);

        return filteredSample;
    }

    private static void AdvanceFilterSample(
        ref int currentSourceIndex,
        ref int currentSourceStride,
        ref int currentLeftEdgeState,
        ref int currentRightEdgeState,
        int sourceStartIndex,
        int sourceEndIndex,
        int sampleStrideForward,
        int sampleStrideBackward)
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

    private static float[] FlattenRowPassOutput(
        ReadOnlySpan<float> rowPassBuffer,
        int imageWidth,
        int nodeWidth,
        int nodeHeight)
    {
        var flattenedRowPassData = new float[nodeWidth * nodeHeight];
        var destinationIndex = 0;

        for (var row = 0; row < nodeHeight; row++)
        {
            var rowStart = row * imageWidth;
            rowPassBuffer.Slice(rowStart, nodeWidth).CopyTo(flattenedRowPassData.AsSpan(destinationIndex, nodeWidth));
            destinationIndex += nodeWidth;
        }

        return flattenedRowPassData;
    }
}

internal readonly record struct WsqDecompositionTraceStep(
    int NodeIndex,
    WsqWaveletNode Node,
    float[] RowPassData,
    float[] WaveletDataAfterColumnPass);
