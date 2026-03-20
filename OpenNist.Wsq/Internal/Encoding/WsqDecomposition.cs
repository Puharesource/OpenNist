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
                highPassFilter,
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
                highPassFilter,
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
        var dataLengthIsOdd = lineLength % 2 != 0;
        var filterLengthIsOdd = lowPassFilter.Length % 2 != 0;
        var activeHighPassFilter = GetActiveHighPassFilter(highPassFilter, filterLengthIsOdd);
        var positiveStride = sampleStride;
        var negativeStride = -positiveStride;

        var lowPassCenterOffset = filterLengthIsOdd
            ? (lowPassFilter.Length - 1) / 2
            : lowPassFilter.Length / 2 - 2;
        var highPassCenterOffset = filterLengthIsOdd
            ? (activeHighPassFilter.Length - 1) / 2 - 1
            : activeHighPassFilter.Length / 2 - 2;
        var initialLowPassLeftEdgeState = filterLengthIsOdd ? 0 : 1;
        var initialHighPassLeftEdgeState = filterLengthIsOdd ? 0 : 1;
        var initialLowPassRightEdgeState = filterLengthIsOdd ? 0 : 1;
        var initialHighPassRightEdgeState = filterLengthIsOdd ? 0 : 1;

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

        var lowPassSampleCount = dataLengthIsOdd
            ? (lineLength + 1) / 2
            : lineLength / 2;
        var highPassSampleCount = dataLengthIsOdd
            ? lowPassSampleCount - 1
            : lowPassSampleCount;

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

            var sourceStartIndex = lineIndex * linePitch;
            var sourceEndIndex = sourceStartIndex + ((lineLength - 1) * sampleStride);
            var lowPassStartIndex = sourceStartIndex + (lowPassCenterOffset * sampleStride);
            var lowPassStartStride = negativeStride;
            var lowPassLeftEdgeState = initialLowPassLeftEdgeState;
            var lowPassRightEdgeState = initialLowPassRightEdgeState;
            var highPassStartIndex = sourceStartIndex + (highPassCenterOffset * sampleStride);
            var highPassStartStride = negativeStride;
            var highPassLeftEdgeState = initialHighPassLeftEdgeState;
            var highPassRightEdgeState = initialHighPassRightEdgeState;

            for (var sampleIndex = 0; sampleIndex < highPassSampleCount; sampleIndex++)
            {
                var lowPassSourceStride = lowPassStartStride;
                var lowPassSourceIndex = lowPassStartIndex;
                var currentLowPassLeftEdgeState = lowPassLeftEdgeState;
                var currentLowPassRightEdgeState = lowPassRightEdgeState;
                destinationSamples[lowPassWriteIndex] = sourceSamples[lowPassSourceIndex] * lowPassFilter[0];
                for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
                {
                    if (lowPassSourceIndex == sourceStartIndex)
                    {
                        if (currentLowPassLeftEdgeState != 0)
                        {
                            lowPassSourceStride = 0;
                            currentLowPassLeftEdgeState = 0;
                        }
                        else
                        {
                            lowPassSourceStride = positiveStride;
                        }
                    }

                    if (lowPassSourceIndex == sourceEndIndex)
                    {
                        if (currentLowPassRightEdgeState != 0)
                        {
                            lowPassSourceStride = 0;
                            currentLowPassRightEdgeState = 0;
                        }
                        else
                        {
                            lowPassSourceStride = negativeStride;
                        }
                    }

                    lowPassSourceIndex += lowPassSourceStride;
                    destinationSamples[lowPassWriteIndex] += sourceSamples[lowPassSourceIndex] * lowPassFilter[filterIndex];
                }

                lowPassWriteIndex += sampleStride;

                var highPassSourceStride = highPassStartStride;
                var highPassSourceIndex = highPassStartIndex;
                var currentHighPassLeftEdgeState = highPassLeftEdgeState;
                var currentHighPassRightEdgeState = highPassRightEdgeState;
                destinationSamples[highPassWriteIndex] = sourceSamples[highPassSourceIndex] * activeHighPassFilter[0];
                for (var filterIndex = 1; filterIndex < activeHighPassFilter.Length; filterIndex++)
                {
                    if (highPassSourceIndex == sourceStartIndex)
                    {
                        if (currentHighPassLeftEdgeState != 0)
                        {
                            highPassSourceStride = 0;
                            currentHighPassLeftEdgeState = 0;
                        }
                        else
                        {
                            highPassSourceStride = positiveStride;
                        }
                    }

                    if (highPassSourceIndex == sourceEndIndex)
                    {
                        if (currentHighPassRightEdgeState != 0)
                        {
                            highPassSourceStride = 0;
                            currentHighPassRightEdgeState = 0;
                        }
                        else
                        {
                            highPassSourceStride = negativeStride;
                        }
                    }

                    highPassSourceIndex += highPassSourceStride;
                    destinationSamples[highPassWriteIndex] += sourceSamples[highPassSourceIndex] * activeHighPassFilter[filterIndex];
                }

                highPassWriteIndex += sampleStride;

                for (var advanceIndex = 0; advanceIndex < 2; advanceIndex++)
                {
                    if (lowPassStartIndex == sourceStartIndex)
                    {
                        if (lowPassLeftEdgeState != 0)
                        {
                            lowPassStartStride = 0;
                            lowPassLeftEdgeState = 0;
                        }
                        else
                        {
                            lowPassStartStride = positiveStride;
                        }
                    }

                    lowPassStartIndex += lowPassStartStride;

                    if (highPassStartIndex == sourceStartIndex)
                    {
                        if (highPassLeftEdgeState != 0)
                        {
                            highPassStartStride = 0;
                            highPassLeftEdgeState = 0;
                        }
                        else
                        {
                            highPassStartStride = positiveStride;
                        }
                    }

                    highPassStartIndex += highPassStartStride;
                }
            }

            if (dataLengthIsOdd)
            {
                var lowPassSourceStride = lowPassStartStride;
                var lowPassSourceIndex = lowPassStartIndex;
                var currentLowPassLeftEdgeState = lowPassLeftEdgeState;
                var currentLowPassRightEdgeState = lowPassRightEdgeState;
                destinationSamples[lowPassWriteIndex] = sourceSamples[lowPassSourceIndex] * lowPassFilter[0];
                for (var filterIndex = 1; filterIndex < lowPassFilter.Length; filterIndex++)
                {
                    if (lowPassSourceIndex == sourceStartIndex)
                    {
                        if (currentLowPassLeftEdgeState != 0)
                        {
                            lowPassSourceStride = 0;
                            currentLowPassLeftEdgeState = 0;
                        }
                        else
                        {
                            lowPassSourceStride = positiveStride;
                        }
                    }

                    if (lowPassSourceIndex == sourceEndIndex)
                    {
                        if (currentLowPassRightEdgeState != 0)
                        {
                            lowPassSourceStride = 0;
                            currentLowPassRightEdgeState = 0;
                        }
                        else
                        {
                            lowPassSourceStride = negativeStride;
                        }
                    }

                    lowPassSourceIndex += lowPassSourceStride;
                    destinationSamples[lowPassWriteIndex] += sourceSamples[lowPassSourceIndex] * lowPassFilter[filterIndex];
                }
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
