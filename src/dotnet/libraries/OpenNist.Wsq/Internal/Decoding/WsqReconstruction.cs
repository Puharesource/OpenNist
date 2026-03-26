namespace OpenNist.Wsq.Internal.Decoding;

using System.Buffers;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ inverse wavelet join mirrors the NBIS reference implementation, including exact zero checks on edge scale factors.")]
internal static class WsqReconstruction
{
    public static byte[] ReconstructToRawPixels(
        float[] waveletData,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable,
        float shift,
        float scale)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        ReconstructToFloatingPointPixels(waveletData, width, height, waveletTree, transformTable);
        return ConvertToBytePixels(waveletData, width, height, shift, scale);
    }

    internal static float[] ReconstructToFloatingPointPixels(
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
            for (var nodeIndex = waveletTree.Length - 1; nodeIndex >= 0; nodeIndex--)
            {
                var node = waveletTree[nodeIndex];
                var baseOffset = node.Y * width + node.X;

                JoinLets(
                    temporaryBuffer.AsSpan(0, waveletData.Length),
                    0,
                    waveletData,
                    baseOffset,
                    node.Width,
                    node.Height,
                    1,
                    width,
                    activeHighPassFilter,
                    lowPassFilter,
                    node.InvertColumns);

                JoinLets(
                    waveletData,
                    baseOffset,
                    temporaryBuffer.AsSpan(0, waveletData.Length),
                    0,
                    node.Height,
                    node.Width,
                    width,
                    1,
                    activeHighPassFilter,
                    lowPassFilter,
                    node.InvertRows);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(temporaryBuffer);
        }

        return waveletData;
    }

    private static ReadOnlySpan<float> GetFilterSpan(IReadOnlyList<float> coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        return coefficients as float[] ?? [.. coefficients];
    }

    private static void JoinLets(
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
        var configuration = CreateJoinConfiguration(lineLength, sampleStride, lowPassFilter.Length, activeHighPassFilter.Length);

        if (!configuration.UsesAsymmetricExtension
            && lowPassFilter.Length == 9
            && activeHighPassFilter.Length == 7)
        {
            JoinLetsStandardOddFilters(
                destinationSamples,
                sourceSamples,
                lineCount,
                linePitch,
                activeHighPassFilter,
                lowPassFilter,
                invertSubbands,
                configuration);
            return;
        }

        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            var writeIndex = lineIndex * linePitch;
            InitializeLineOutput(destinationSamples, writeIndex, configuration.SampleStride);

            var (lowPassBandOffset, highPassBandOffset) = GetBandOffsets(lineIndex, linePitch, invertSubbands, configuration);
            var lowPassBandState = CreateLowPassBandState(writeIndex, lowPassBandOffset, configuration);
            var highPassBandState = CreateHighPassBandState(writeIndex, highPassBandOffset, configuration);

            ProcessInterleavedSamplePairs(
                destinationSamples,
                sourceSamples,
                lowPassFilter,
                activeHighPassFilter,
                ref lowPassBandState,
                ref highPassBandState,
                configuration);

            lowPassBandState.TapStartIndex = GetTrailingLowPassTapStartIndex(configuration);
            WriteLowPassSamples(
                destinationSamples,
                sourceSamples,
                lowPassFilter,
                ref lowPassBandState,
                configuration,
                startingTapIndex: 1,
                endingTapIndex: lowPassBandState.TapStartIndex);

            var trailingHighPassRightEdgeFactor =
                PrepareTrailingHighPassState(ref highPassBandState, activeHighPassFilter.Length, configuration);
            WriteHighPassSamples(
                destinationSamples,
                sourceSamples,
                activeHighPassFilter,
                ref highPassBandState,
                configuration,
                startingTapIndex: 1,
                endingTapIndex: highPassBandState.TapStartIndex,
                initialRightEdgeFactor: trailingHighPassRightEdgeFactor);
        }
    }

    private static void JoinLetsStandardOddFilters(
        Span<float> destinationSamples,
        ReadOnlySpan<float> sourceSamples,
        int lineCount,
        int linePitch,
        ReadOnlySpan<float> activeHighPassFilter,
        ReadOnlySpan<float> lowPassFilter,
        bool invertSubbands,
        JoinConfiguration configuration)
    {
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            var writeIndex = lineIndex * linePitch;
            InitializeLineOutput(destinationSamples, writeIndex, configuration.SampleStride);

            var (lowPassBandOffset, highPassBandOffset) = GetBandOffsets(lineIndex, linePitch, invertSubbands, configuration);
            var lowPassBandState = CreateLowPassBandState(writeIndex, lowPassBandOffset, configuration);
            var highPassBandState = CreateHighPassBandState(writeIndex, highPassBandOffset, configuration);

            for (var samplePairIndex = 0; samplePairIndex < configuration.HighPassSampleCount; samplePairIndex++)
            {
                for (var tapIndex = lowPassBandState.TapStartIndex; tapIndex >= 0; tapIndex--)
                {
                    destinationSamples[lowPassBandState.WriteIndex] =
                        ComputeLowPassSample9(sourceSamples, lowPassFilter, tapIndex, lowPassBandState, configuration);
                    lowPassBandState.WriteIndex += configuration.SampleStride;
                }

                AdvanceLowPassScanState(ref lowPassBandState, configuration);

                for (var tapIndex = highPassBandState.TapStartIndex; tapIndex >= 0; tapIndex--)
                {
                    destinationSamples[highPassBandState.WriteIndex] = ComputeHighPassSample7(
                        destinationSamples[highPassBandState.WriteIndex],
                        sourceSamples,
                        activeHighPassFilter,
                        tapIndex,
                        highPassBandState,
                        configuration);
                    highPassBandState.WriteIndex += configuration.SampleStride;
                }

                AdvanceHighPassScanState(ref highPassBandState, configuration);
            }

            lowPassBandState.TapStartIndex = GetTrailingLowPassTapStartIndex(configuration);
            for (var tapIndex = 1; tapIndex >= lowPassBandState.TapStartIndex; tapIndex--)
            {
                destinationSamples[lowPassBandState.WriteIndex] =
                    ComputeLowPassSample9(sourceSamples, lowPassFilter, tapIndex, lowPassBandState, configuration);
                lowPassBandState.WriteIndex += configuration.SampleStride;
            }

            PrepareTrailingHighPassState(ref highPassBandState, activeHighPassFilter.Length, configuration);
            for (var tapIndex = 1; tapIndex >= highPassBandState.TapStartIndex; tapIndex--)
            {
                destinationSamples[highPassBandState.WriteIndex] = ComputeHighPassSample7(
                    destinationSamples[highPassBandState.WriteIndex],
                    sourceSamples,
                    activeHighPassFilter,
                    tapIndex,
                    highPassBandState,
                    configuration);
                highPassBandState.WriteIndex += configuration.SampleStride;
            }
        }
    }

    private static JoinConfiguration CreateJoinConfiguration(
        int lineLength,
        int sampleStride,
        int lowPassFilterLength,
        int highPassFilterLength)
    {
        var isLineLengthOdd = lineLength % 2 != 0;
        var lowPassSampleCount = isLineLengthOdd
            ? (lineLength + 1) / 2
            : lineLength / 2;
        var highPassSampleCount = isLineLengthOdd
            ? lowPassSampleCount - 1
            : lowPassSampleCount;
        var lowPassFilterLengthIsOdd = lowPassFilterLength % 2 != 0;

        if (lowPassFilterLengthIsOdd)
        {
            return new(
                sampleStride,
                -sampleStride,
                isLineLengthOdd,
                false,
                lowPassSampleCount,
                highPassSampleCount,
                (lowPassFilterLength - 1) / 4,
                (highPassFilterLength + 1) / 4 - 1,
                (lowPassFilterLength - 1) / 2 % 2,
                (highPassFilterLength + 1) / 2 % 2,
                0,
                isLineLengthOdd ? 0 : 1,
                1,
                isLineLengthOdd ? 1 : 0,
                0,
                1.0f);
        }

        var lowPassCenterOffset = lowPassFilterLength / 4 - 1;
        var initialLowPassLeftEdgeState = 1;

        if (lowPassCenterOffset == -1)
        {
            lowPassCenterOffset = 0;
            initialLowPassLeftEdgeState = 0;
        }

        var highPassCenterOffset = highPassFilterLength / 4 - 1;
        var initialHighPassLeftEdgeState = 1;

        if (highPassCenterOffset == -1)
        {
            highPassCenterOffset = 0;
            initialHighPassLeftEdgeState = 0;
        }

        return new(
            sampleStride,
            -sampleStride,
            isLineLengthOdd,
            true,
            lowPassSampleCount,
            highPassSampleCount,
            lowPassCenterOffset,
            highPassCenterOffset,
            lowPassFilterLength / 2 % 2,
            highPassFilterLength / 2 % 2,
            initialLowPassLeftEdgeState,
            isLineLengthOdd ? 0 : 1,
            initialHighPassLeftEdgeState,
            1,
            2,
            -1.0f);
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

    private static void InitializeLineOutput(Span<float> destinationSamples, int writeIndex, int sampleStride)
    {
        destinationSamples[writeIndex] = 0.0f;
        destinationSamples[writeIndex + sampleStride] = 0.0f;
    }

    private static (int LowPassBandOffset, int HighPassBandOffset) GetBandOffsets(
        int lineIndex,
        int linePitch,
        bool invertSubbands,
        JoinConfiguration configuration)
    {
        var writeIndex = lineIndex * linePitch;

        if (invertSubbands)
        {
            var highPassBandOffset = writeIndex;
            var lowPassBandOffset = highPassBandOffset + configuration.SampleStride * configuration.HighPassSampleCount;
            return (lowPassBandOffset, highPassBandOffset);
        }

        var lowPassOffset = writeIndex;
        var highPassOffset = lowPassOffset + configuration.SampleStride * configuration.LowPassSampleCount;
        return (lowPassOffset, highPassOffset);
    }

    private static LowPassBandState CreateLowPassBandState(
        int writeIndex,
        int lowPassBandOffset,
        JoinConfiguration configuration)
    {
        return new(
            writeIndex,
            lowPassBandOffset,
            lowPassBandOffset + (configuration.LowPassSampleCount - 1) * configuration.SampleStride,
            lowPassBandOffset + configuration.LowPassCenterOffset * configuration.SampleStride,
            configuration.BackwardStride,
            configuration.LowPassInitialTapOffset,
            configuration.InitialLowPassLeftEdgeState,
            configuration.InitialLowPassRightEdgeState);
    }

    private static HighPassBandState CreateHighPassBandState(
        int writeIndex,
        int highPassBandOffset,
        JoinConfiguration configuration)
    {
        return new(
            writeIndex,
            highPassBandOffset,
            highPassBandOffset + (configuration.HighPassSampleCount - 1) * configuration.SampleStride,
            highPassBandOffset + configuration.HighPassCenterOffset * configuration.SampleStride,
            configuration.BackwardStride,
            configuration.HighPassInitialTapOffset,
            configuration.InitialHighPassLeftEdgeState,
            configuration.InitialHighPassRightEdgeState,
            configuration.InitialScaleFactor);
    }

    private static void ProcessInterleavedSamplePairs(
        Span<float> destinationSamples,
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> lowPassFilter,
        ReadOnlySpan<float> activeHighPassFilter,
        ref LowPassBandState lowPassBandState,
        ref HighPassBandState highPassBandState,
        JoinConfiguration configuration)
    {
        for (var samplePairIndex = 0; samplePairIndex < configuration.HighPassSampleCount; samplePairIndex++)
        {
            WriteLowPassSamples(
                destinationSamples,
                sourceSamples,
                lowPassFilter,
                ref lowPassBandState,
                configuration,
                startingTapIndex: lowPassBandState.TapStartIndex,
                endingTapIndex: 0);
            AdvanceLowPassScanState(ref lowPassBandState, configuration);

            WriteHighPassSamples(
                destinationSamples,
                sourceSamples,
                activeHighPassFilter,
                ref highPassBandState,
                configuration,
                startingTapIndex: highPassBandState.TapStartIndex,
                endingTapIndex: 0,
                initialRightEdgeFactor: configuration.InitialHighPassRightEdgeFactor);
            AdvanceHighPassScanState(ref highPassBandState, configuration);
        }
    }

    private static void WriteLowPassSamples(
        Span<float> destinationSamples,
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> lowPassFilter,
        ref LowPassBandState bandState,
        JoinConfiguration configuration,
        int startingTapIndex,
        int endingTapIndex)
    {
        for (var tapIndex = startingTapIndex; tapIndex >= endingTapIndex; tapIndex--)
        {
            destinationSamples[bandState.WriteIndex] =
                ComputeLowPassSample(sourceSamples, lowPassFilter, tapIndex, bandState, configuration);
            bandState.WriteIndex += configuration.SampleStride;
        }
    }

    private static float ComputeLowPassSample(
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> lowPassFilter,
        int tapIndex,
        LowPassBandState bandState,
        JoinConfiguration configuration)
    {
        var currentLeftEdgeState = bandState.NextLeftEdgeState;
        var currentRightEdgeState = bandState.NextRightEdgeState;
        var sourceIndex = bandState.ScanIndex;
        var sourceStride = bandState.ScanStride;
        var lowPassValue = sourceSamples[sourceIndex] * lowPassFilter[tapIndex];

        for (var filterIndex = tapIndex + 2; filterIndex < lowPassFilter.Length; filterIndex += 2)
        {
            if (sourceIndex == bandState.StartIndex)
            {
                if (currentLeftEdgeState != 0)
                {
                    sourceStride = 0;
                    currentLeftEdgeState = 0;
                }
                else
                {
                    sourceStride = configuration.SampleStride;
                }
            }

            if (sourceIndex == bandState.EndIndex)
            {
                if (currentRightEdgeState != 0)
                {
                    sourceStride = 0;
                    currentRightEdgeState = 0;
                }
                else
                {
                    sourceStride = configuration.BackwardStride;
                }
            }

            sourceIndex += sourceStride;
            lowPassValue = MathF.FusedMultiplyAdd(
                sourceSamples[sourceIndex],
                lowPassFilter[filterIndex],
                lowPassValue);
        }

        return lowPassValue;
    }

    private static float ComputeLowPassSample9(
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> lowPassFilter,
        int tapIndex,
        LowPassBandState bandState,
        JoinConfiguration configuration)
    {
        var currentLeftEdgeState = bandState.NextLeftEdgeState;
        var currentRightEdgeState = bandState.NextRightEdgeState;
        var sourceIndex = bandState.ScanIndex;
        var sourceStride = bandState.ScanStride;
        var lowPassValue = sourceSamples[sourceIndex] * lowPassFilter[tapIndex];

        AdvanceJoinSample(
            ref sourceIndex,
            ref sourceStride,
            ref currentLeftEdgeState,
            ref currentRightEdgeState,
            bandState.StartIndex,
            bandState.EndIndex,
            configuration.SampleStride,
            configuration.BackwardStride);
        lowPassValue = MathF.FusedMultiplyAdd(sourceSamples[sourceIndex], lowPassFilter[tapIndex + 2], lowPassValue);

        AdvanceJoinSample(
            ref sourceIndex,
            ref sourceStride,
            ref currentLeftEdgeState,
            ref currentRightEdgeState,
            bandState.StartIndex,
            bandState.EndIndex,
            configuration.SampleStride,
            configuration.BackwardStride);
        lowPassValue = MathF.FusedMultiplyAdd(sourceSamples[sourceIndex], lowPassFilter[tapIndex + 4], lowPassValue);

        AdvanceJoinSample(
            ref sourceIndex,
            ref sourceStride,
            ref currentLeftEdgeState,
            ref currentRightEdgeState,
            bandState.StartIndex,
            bandState.EndIndex,
            configuration.SampleStride,
            configuration.BackwardStride);
        lowPassValue = MathF.FusedMultiplyAdd(sourceSamples[sourceIndex], lowPassFilter[tapIndex + 6], lowPassValue);

        if (tapIndex == 0)
        {
            AdvanceJoinSample(
                ref sourceIndex,
                ref sourceStride,
                ref currentLeftEdgeState,
                ref currentRightEdgeState,
                bandState.StartIndex,
                bandState.EndIndex,
                configuration.SampleStride,
                configuration.BackwardStride);
            lowPassValue = MathF.FusedMultiplyAdd(sourceSamples[sourceIndex], lowPassFilter[8], lowPassValue);
        }

        return lowPassValue;
    }

    private static void AdvanceLowPassScanState(ref LowPassBandState bandState, JoinConfiguration configuration)
    {
        if (bandState.ScanIndex == bandState.StartIndex)
        {
            if (bandState.NextLeftEdgeState != 0)
            {
                bandState.ScanStride = 0;
                bandState.NextLeftEdgeState = 0;
            }
            else
            {
                bandState.ScanStride = configuration.SampleStride;
            }
        }

        bandState.ScanIndex += bandState.ScanStride;
        bandState.TapStartIndex = 1;
    }

    private static void WriteHighPassSamples(
        Span<float> destinationSamples,
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> highPassFilter,
        ref HighPassBandState bandState,
        JoinConfiguration configuration,
        int startingTapIndex,
        int endingTapIndex,
        int initialRightEdgeFactor)
    {
        for (var tapIndex = startingTapIndex; tapIndex >= endingTapIndex; tapIndex--)
        {
            destinationSamples[bandState.WriteIndex] = ComputeHighPassSample(
                destinationSamples[bandState.WriteIndex],
                sourceSamples,
                highPassFilter,
                tapIndex,
                initialRightEdgeFactor,
                bandState,
                configuration);
            bandState.WriteIndex += configuration.SampleStride;
        }
    }

    private static float ComputeHighPassSample(
        float currentValue,
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> highPassFilter,
        int tapIndex,
        int initialRightEdgeFactor,
        HighPassBandState bandState,
        JoinConfiguration configuration)
    {
        var currentLeftEdgeState = bandState.NextLeftEdgeState;
        var currentRightEdgeState = bandState.NextRightEdgeState;
        var sourceIndex = bandState.ScanIndex;
        var sourceStride = bandState.ScanStride;
        var rightEdgeFactor = initialRightEdgeFactor;
        var sampleScaleFactor = bandState.CurrentOutputScaleFactor;

        for (var filterIndex = tapIndex; filterIndex < highPassFilter.Length; filterIndex += 2)
        {
            if (sourceIndex == bandState.StartIndex)
            {
                if (currentLeftEdgeState != 0)
                {
                    sourceStride = 0;
                    currentLeftEdgeState = 0;
                }
                else
                {
                    sourceStride = configuration.SampleStride;
                    sampleScaleFactor = 1.0f;
                }
            }

            if (sourceIndex == bandState.EndIndex)
            {
                if (currentRightEdgeState != 0)
                {
                    sourceStride = 0;
                    currentRightEdgeState = 0;

                    if (configuration is { UsesAsymmetricExtension: true, IsLineLengthOdd: true })
                    {
                        currentRightEdgeState = 1;
                        rightEdgeFactor--;
                        sampleScaleFactor = rightEdgeFactor;

                        if (sampleScaleFactor == 0.0f)
                        {
                            currentRightEdgeState = 0;
                        }
                    }
                }
                else
                {
                    sourceStride = configuration.BackwardStride;

                    if (configuration.UsesAsymmetricExtension)
                    {
                        sampleScaleFactor = -1.0f;
                    }
                }
            }

            currentValue = MathF.FusedMultiplyAdd(
                sourceSamples[sourceIndex] * highPassFilter[filterIndex],
                sampleScaleFactor,
                currentValue);
            sourceIndex += sourceStride;
        }

        return currentValue;
    }

    private static float ComputeHighPassSample7(
        float currentValue,
        ReadOnlySpan<float> sourceSamples,
        ReadOnlySpan<float> highPassFilter,
        int tapIndex,
        HighPassBandState bandState,
        JoinConfiguration configuration)
    {
        var currentLeftEdgeState = bandState.NextLeftEdgeState;
        var currentRightEdgeState = bandState.NextRightEdgeState;
        var sourceIndex = bandState.ScanIndex;
        var sourceStride = bandState.ScanStride;
        var sampleScaleFactor = bandState.CurrentOutputScaleFactor;

        currentValue = MathF.FusedMultiplyAdd(
            sourceSamples[sourceIndex] * highPassFilter[tapIndex],
            sampleScaleFactor,
            currentValue);

        AdvanceJoinSample(
            ref sourceIndex,
            ref sourceStride,
            ref currentLeftEdgeState,
            ref currentRightEdgeState,
            bandState.StartIndex,
            bandState.EndIndex,
            configuration.SampleStride,
            configuration.BackwardStride);
        currentValue = MathF.FusedMultiplyAdd(
            sourceSamples[sourceIndex] * highPassFilter[tapIndex + 2],
            1.0f,
            currentValue);

        AdvanceJoinSample(
            ref sourceIndex,
            ref sourceStride,
            ref currentLeftEdgeState,
            ref currentRightEdgeState,
            bandState.StartIndex,
            bandState.EndIndex,
            configuration.SampleStride,
            configuration.BackwardStride);
        currentValue = MathF.FusedMultiplyAdd(
            sourceSamples[sourceIndex] * highPassFilter[tapIndex + 4],
            1.0f,
            currentValue);

        if (tapIndex == 0)
        {
            AdvanceJoinSample(
                ref sourceIndex,
                ref sourceStride,
                ref currentLeftEdgeState,
                ref currentRightEdgeState,
                bandState.StartIndex,
                bandState.EndIndex,
                configuration.SampleStride,
                configuration.BackwardStride);
            currentValue = MathF.FusedMultiplyAdd(
                sourceSamples[sourceIndex] * highPassFilter[6],
                1.0f,
                currentValue);
        }

        return currentValue;
    }

    private static void AdvanceJoinSample(
        ref int sourceIndex,
        ref int sourceStride,
        ref int currentLeftEdgeState,
        ref int currentRightEdgeState,
        int startIndex,
        int endIndex,
        int sampleStride,
        int backwardStride)
    {
        if (sourceIndex == startIndex)
        {
            if (currentLeftEdgeState != 0)
            {
                sourceStride = 0;
                currentLeftEdgeState = 0;
            }
            else
            {
                sourceStride = sampleStride;
            }
        }

        if (sourceIndex == endIndex)
        {
            if (currentRightEdgeState != 0)
            {
                sourceStride = 0;
                currentRightEdgeState = 0;
            }
            else
            {
                sourceStride = backwardStride;
            }
        }

        sourceIndex += sourceStride;
    }

    private static void AdvanceHighPassScanState(ref HighPassBandState bandState, JoinConfiguration configuration)
    {
        if (bandState.ScanIndex == bandState.StartIndex)
        {
            if (bandState.NextLeftEdgeState != 0)
            {
                bandState.ScanStride = 0;
                bandState.NextLeftEdgeState = 0;
            }
            else
            {
                bandState.ScanStride = configuration.SampleStride;
                bandState.CurrentOutputScaleFactor = 1.0f;
            }
        }

        bandState.ScanIndex += bandState.ScanStride;
        bandState.TapStartIndex = 1;
    }

    private static int GetTrailingLowPassTapStartIndex(JoinConfiguration configuration)
    {
        if (configuration.IsLineLengthOdd)
        {
            return configuration.LowPassInitialTapOffset != 0 ? 1 : 0;
        }

        return configuration.LowPassInitialTapOffset != 0 ? 2 : 1;
    }

    private static int PrepareTrailingHighPassState(
        ref HighPassBandState bandState,
        int highPassFilterLength,
        JoinConfiguration configuration)
    {
        if (configuration.IsLineLengthOdd)
        {
            bandState.TapStartIndex = configuration.HighPassInitialTapOffset != 0 ? 1 : 0;

            if (highPassFilterLength == 2)
            {
                bandState.ScanIndex -= bandState.ScanStride;
                return 1;
            }

            return configuration.InitialHighPassRightEdgeFactor;
        }

        bandState.TapStartIndex = configuration.HighPassInitialTapOffset != 0 ? 2 : 1;
        return configuration.InitialHighPassRightEdgeFactor;
    }

    private readonly struct JoinConfiguration
    {
        public JoinConfiguration(
            int sampleStride,
            int backwardStride,
            bool isLineLengthOdd,
            bool usesAsymmetricExtension,
            int lowPassSampleCount,
            int highPassSampleCount,
            int lowPassCenterOffset,
            int highPassCenterOffset,
            int lowPassInitialTapOffset,
            int highPassInitialTapOffset,
            int initialLowPassLeftEdgeState,
            int initialLowPassRightEdgeState,
            int initialHighPassLeftEdgeState,
            int initialHighPassRightEdgeState,
            int initialHighPassRightEdgeFactor,
            float initialScaleFactor)
        {
            SampleStride = sampleStride;
            BackwardStride = backwardStride;
            IsLineLengthOdd = isLineLengthOdd;
            UsesAsymmetricExtension = usesAsymmetricExtension;
            LowPassSampleCount = lowPassSampleCount;
            HighPassSampleCount = highPassSampleCount;
            LowPassCenterOffset = lowPassCenterOffset;
            HighPassCenterOffset = highPassCenterOffset;
            LowPassInitialTapOffset = lowPassInitialTapOffset;
            HighPassInitialTapOffset = highPassInitialTapOffset;
            InitialLowPassLeftEdgeState = initialLowPassLeftEdgeState;
            InitialLowPassRightEdgeState = initialLowPassRightEdgeState;
            InitialHighPassLeftEdgeState = initialHighPassLeftEdgeState;
            InitialHighPassRightEdgeState = initialHighPassRightEdgeState;
            InitialHighPassRightEdgeFactor = initialHighPassRightEdgeFactor;
            InitialScaleFactor = initialScaleFactor;
        }

        public readonly int SampleStride;
        public readonly int BackwardStride;
        public readonly bool IsLineLengthOdd;
        public readonly bool UsesAsymmetricExtension;
        public readonly int LowPassSampleCount;
        public readonly int HighPassSampleCount;
        public readonly int LowPassCenterOffset;
        public readonly int HighPassCenterOffset;
        public readonly int LowPassInitialTapOffset;
        public readonly int HighPassInitialTapOffset;
        public readonly int InitialLowPassLeftEdgeState;
        public readonly int InitialLowPassRightEdgeState;
        public readonly int InitialHighPassLeftEdgeState;
        public readonly int InitialHighPassRightEdgeState;
        public readonly int InitialHighPassRightEdgeFactor;
        public readonly float InitialScaleFactor;
    }

    private struct LowPassBandState(
        int writeIndex,
        int startIndex,
        int endIndex,
        int scanIndex,
        int scanStride,
        int tapStartIndex,
        int nextLeftEdgeState,
        int nextRightEdgeState)
    {
        public int WriteIndex { get; set; } = writeIndex;

        public int StartIndex { get; set; } = startIndex;

        public int EndIndex { get; set; } = endIndex;

        public int ScanIndex { get; set; } = scanIndex;

        public int ScanStride { get; set; } = scanStride;

        public int TapStartIndex { get; set; } = tapStartIndex;

        public int NextLeftEdgeState { get; set; } = nextLeftEdgeState;

        public int NextRightEdgeState { get; set; } = nextRightEdgeState;
    }

    private struct HighPassBandState(
        int writeIndex,
        int startIndex,
        int endIndex,
        int scanIndex,
        int scanStride,
        int tapStartIndex,
        int nextLeftEdgeState,
        int nextRightEdgeState,
        float currentOutputScaleFactor)
    {
        public int WriteIndex { get; set; } = writeIndex;

        public int StartIndex { get; set; } = startIndex;

        public int EndIndex { get; set; } = endIndex;

        public int ScanIndex { get; set; } = scanIndex;

        public int ScanStride { get; set; } = scanStride;

        public int TapStartIndex { get; set; } = tapStartIndex;

        public int NextLeftEdgeState { get; set; } = nextLeftEdgeState;

        public int NextRightEdgeState { get; set; } = nextRightEdgeState;

        public float CurrentOutputScaleFactor { get; set; } = currentOutputScaleFactor;
    }

    private static byte[] ConvertToBytePixels(ReadOnlySpan<float> image, int width, int height, float shift, float scale)
    {
        var pixels = new byte[width * height];
        var imageIndex = 0;

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var value = MathF.FusedMultiplyAdd(image[imageIndex], scale, shift);
                value += 0.5f;

                pixels[imageIndex] = value switch
                {
                    < 0.0f => 0,
                    > 255.0f => 255,
                    _ => (byte)value,
                };

                imageIndex++;
            }
        }

        return pixels;
    }
}
