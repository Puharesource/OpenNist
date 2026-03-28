namespace OpenNist.Wsq.Internal.Decoding;

using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Metadata;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ decoder must mirror the NBIS reference implementation's exact zero checks for quantization bins.")]
internal static class WsqQuantizationDecoder
{
    public static int[] ComputeBlockSizes(
        WsqQuantizationTable quantizationTable,
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree)
    {
        ArgumentNullException.ThrowIfNull(quantizationTable);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(quantizationTree);

        var quantizationBins = GetValueSpan(quantizationTable.QuantizationBins);
        ReadOnlySpan<WsqWaveletNode> waveletNodes = waveletTree;
        ReadOnlySpan<WsqQuantizationNode> quantizationNodes = quantizationTree;
        var blockSizes = new int[WsqConstants.BlockCount];
        blockSizes[0] = waveletNodes[14].Width * waveletNodes[14].Height;
        blockSizes[1] = waveletNodes[5].Height * waveletNodes[1].Width
            + waveletNodes[4].Width * waveletNodes[4].Height;
        blockSizes[2] = waveletNodes[2].Width * waveletNodes[2].Height
            + waveletNodes[3].Width * waveletNodes[3].Height;

        for (var node = 0; node < WsqConstants.StartSubband2; node++)
        {
            if ((float)quantizationBins[node] == 0.0f)
            {
                blockSizes[0] -= quantizationNodes[node].Width * quantizationNodes[node].Height;
            }
        }

        for (var node = WsqConstants.StartSubband2; node < WsqConstants.StartSubband3; node++)
        {
            if ((float)quantizationBins[node] == 0.0f)
            {
                blockSizes[1] -= quantizationNodes[node].Width * quantizationNodes[node].Height;
            }
        }

        for (var node = WsqConstants.StartSubband3; node < WsqConstants.StartSubbandDelete; node++)
        {
            if ((float)quantizationBins[node] == 0.0f)
            {
                blockSizes[2] -= quantizationNodes[node].Width * quantizationNodes[node].Height;
            }
        }

        return blockSizes;
    }

    public static float[] Unquantize(
        WsqQuantizationTable quantizationTable,
        WsqQuantizationNode[] quantizationTree,
        short[] quantizedCoefficients,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(quantizationTable);
        ArgumentNullException.ThrowIfNull(quantizationTree);
        ArgumentNullException.ThrowIfNull(quantizedCoefficients);

        var quantizationBins = GetValueSpan(quantizationTable.QuantizationBins);
        var zeroBins = GetValueSpan(quantizationTable.ZeroBins);
        ReadOnlySpan<WsqQuantizationNode> quantizationNodes = quantizationTree;
        ReadOnlySpan<short> coefficients = quantizedCoefficients;
        var pixels = new float[width * height];
        var pixelSpan = pixels.AsSpan();
        var coefficientIndex = 0;
        var binCenter = (float)quantizationTable.BinCenter;

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            var quantizationBin = (float)quantizationBins[subband];

            if (quantizationBin == 0.0f)
            {
                continue;
            }

            var halfZeroBin = (float)(zeroBins[subband] / 2.0);
            var node = quantizationNodes[subband];
            var rowStart = node.Y * width + node.X;

            for (var row = 0; row < node.Height; row++)
            {
                var pixelIndex = rowStart + row * width;

                for (var column = 0; column < node.Width; column++)
                {
                    var quantizedValue = coefficients[coefficientIndex++];
                    float pixelValue;

                    switch (quantizedValue)
                    {
                        case 0:
                            pixelValue = 0.0f;
                            break;
                        case > 0:
                            pixelValue = quantizationBin * (quantizedValue - binCenter);
                            pixelValue += halfZeroBin;
                            break;
                        default:
                            pixelValue = quantizationBin * (quantizedValue + binCenter);
                            pixelValue -= halfZeroBin;
                            break;
                    }

                    pixelSpan[pixelIndex + column] = pixelValue;
                }
            }
        }

        if (coefficientIndex != coefficients.Length)
        {
            throw new InvalidDataException(
                $"Consumed {coefficientIndex} quantized coefficients, but decoded {coefficients.Length}.");
        }

        return pixels;
    }

    private static ReadOnlySpan<double> GetValueSpan(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values as double[] ?? [.. values];
    }
}
