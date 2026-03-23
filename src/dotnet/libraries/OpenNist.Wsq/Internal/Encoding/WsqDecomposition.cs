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

    private static unsafe void GetLets(
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
        var hi = highPassFilter.ToArray();
        var lo = lowPassFilter.ToArray();

        fixed (float* destinationPointer = destination)
        fixed (float* sourcePointer = source)
        fixed (float* hiPointer = hi)
        fixed (float* loPointer = lo)
        {
            var daEv = lineLength % 2;
            var fiEv = lo.Length % 2;
            int loc;
            int hoc;
            int olle;
            int ohle;
            int olre;
            int ohre;

            if (fiEv != 0)
            {
                loc = (lo.Length - 1) / 2;
                hoc = (hi.Length - 1) / 2 - 1;
                olle = 0;
                ohle = 0;
                olre = 0;
                ohre = 0;
            }
            else
            {
                loc = lo.Length / 2 - 2;
                hoc = hi.Length / 2 - 2;
                olle = 1;
                ohle = 1;
                olre = 1;
                ohre = 1;

                if (loc == -1)
                {
                    loc = 0;
                    olle = 0;
                }

                if (hoc == -1)
                {
                    hoc = 0;
                    ohle = 0;
                }

                for (var index = 0; index < hi.Length; index++)
                {
                    hiPointer[index] *= -1.0f;
                }
            }

            var pstr = sampleStride;
            var nstr = -pstr;
            int llen;
            int hlen;
            if (daEv != 0)
            {
                llen = (lineLength + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = lineLength / 2;
                hlen = llen;
            }

            for (var rwCl = 0; rwCl < lineCount; rwCl++)
            {
                float* lopass;
                float* hipass;
                if (invertSubbands)
                {
                    hipass = destinationPointer + destinationBaseOffset + (rwCl * linePitch);
                    lopass = hipass + (hlen * sampleStride);
                }
                else
                {
                    lopass = destinationPointer + destinationBaseOffset + (rwCl * linePitch);
                    hipass = lopass + (llen * sampleStride);
                }

                var p0 = sourcePointer + sourceBaseOffset + (rwCl * linePitch);
                var p1 = p0 + ((lineLength - 1) * sampleStride);

                var lspx = p0 + (loc * sampleStride);
                var lspxstr = nstr;
                var lle2 = olle;
                var lre2 = olre;
                var hspx = p0 + (hoc * sampleStride);
                var hspxstr = nstr;
                var hle2 = ohle;
                var hre2 = ohre;

                for (var pix = 0; pix < hlen; pix++)
                {
                    var lpxstr = lspxstr;
                    var lpx = lspx;
                    var lle = lle2;
                    var lre = lre2;
                    *lopass = *lpx * loPointer[0];
                    for (var index = 1; index < lo.Length; index++)
                    {
                        if (lpx == p0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }

                        if (lpx == p1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }

                        lpx += lpxstr;
                        *lopass = MathF.FusedMultiplyAdd(*lpx, loPointer[index], *lopass);
                    }

                    lopass += sampleStride;

                    var hpxstr = hspxstr;
                    var hpx = hspx;
                    var hle = hle2;
                    var hre = hre2;
                    *hipass = *hpx * hiPointer[0];
                    for (var index = 1; index < hi.Length; index++)
                    {
                        if (hpx == p0)
                        {
                            if (hle != 0)
                            {
                                hpxstr = 0;
                                hle = 0;
                            }
                            else
                            {
                                hpxstr = pstr;
                            }
                        }

                        if (hpx == p1)
                        {
                            if (hre != 0)
                            {
                                hpxstr = 0;
                                hre = 0;
                            }
                            else
                            {
                                hpxstr = nstr;
                            }
                        }

                        hpx += hpxstr;
                        *hipass = MathF.FusedMultiplyAdd(*hpx, hiPointer[index], *hipass);
                    }

                    hipass += sampleStride;

                    for (var index = 0; index < 2; index++)
                    {
                        if (lspx == p0)
                        {
                            if (lle2 != 0)
                            {
                                lspxstr = 0;
                                lle2 = 0;
                            }
                            else
                            {
                                lspxstr = pstr;
                            }
                        }

                        lspx += lspxstr;
                        if (hspx == p0)
                        {
                            if (hle2 != 0)
                            {
                                hspxstr = 0;
                                hle2 = 0;
                            }
                            else
                            {
                                hspxstr = pstr;
                            }
                        }

                        hspx += hspxstr;
                    }
                }

                if (daEv != 0)
                {
                    var lpxstr = lspxstr;
                    var lpx = lspx;
                    var lle = lle2;
                    var lre = lre2;
                    *lopass = *lpx * loPointer[0];
                    for (var index = 1; index < lo.Length; index++)
                    {
                        if (lpx == p0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }

                        if (lpx == p1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }

                        lpx += lpxstr;
                        *lopass = MathF.FusedMultiplyAdd(*lpx, loPointer[index], *lopass);
                    }
                }
            }

            if (fiEv == 0)
            {
                for (var index = 0; index < hi.Length; index++)
                {
                    hiPointer[index] *= -1.0f;
                }
            }
        }
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
