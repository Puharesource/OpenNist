namespace OpenNist.Wsq.Internal.Decoding;

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

        var blockSizes = new int[WsqConstants.BlockCount];
        blockSizes[0] = waveletTree[14].Width * waveletTree[14].Height;
        blockSizes[1] = (waveletTree[5].Height * waveletTree[1].Width)
            + (waveletTree[4].Width * waveletTree[4].Height);
        blockSizes[2] = (waveletTree[2].Width * waveletTree[2].Height)
            + (waveletTree[3].Width * waveletTree[3].Height);

        for (var node = 0; node < WsqConstants.StartSubband2; node++)
        {
            if ((float)quantizationTable.QuantizationBins[node] == 0.0f)
            {
                blockSizes[0] -= quantizationTree[node].Width * quantizationTree[node].Height;
            }
        }

        for (var node = WsqConstants.StartSubband2; node < WsqConstants.StartSubband3; node++)
        {
            if ((float)quantizationTable.QuantizationBins[node] == 0.0f)
            {
                blockSizes[1] -= quantizationTree[node].Width * quantizationTree[node].Height;
            }
        }

        for (var node = WsqConstants.StartSubband3; node < WsqConstants.StartSubbandDelete; node++)
        {
            if ((float)quantizationTable.QuantizationBins[node] == 0.0f)
            {
                blockSizes[2] -= quantizationTree[node].Width * quantizationTree[node].Height;
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

        var pixels = new float[width * height];
        var coefficientIndex = 0;
        var binCenter = (float)quantizationTable.BinCenter;

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            var quantizationBin = (float)quantizationTable.QuantizationBins[subband];

            if (quantizationBin == 0.0f)
            {
                continue;
            }

            var zeroBin = (float)quantizationTable.ZeroBins[subband];
            var node = quantizationTree[subband];
            var rowStart = (node.Y * width) + node.X;

            for (var row = 0; row < node.Height; row++)
            {
                var pixelIndex = rowStart + (row * width);

                for (var column = 0; column < node.Width; column++)
                {
                    var quantizedValue = quantizedCoefficients[coefficientIndex++];
                    float pixelValue;

                    switch (quantizedValue)
                    {
                        case 0:
                            pixelValue = 0.0f;
                            break;
                        case > 0:
                            pixelValue = quantizationBin * (quantizedValue - binCenter);
                            pixelValue = (float)(pixelValue + (zeroBin / 2.0));
                            break;
                        default:
                            pixelValue = quantizationBin * (quantizedValue + binCenter);
                            pixelValue = (float)(pixelValue - (zeroBin / 2.0));
                            break;
                    }

                    pixels[pixelIndex + column] = pixelValue;
                }
            }
        }

        if (coefficientIndex != quantizedCoefficients.Length)
        {
            throw new InvalidDataException(
                $"Consumed {coefficientIndex} quantized coefficients, but decoded {quantizedCoefficients.Length}.");
        }

        return pixels;
    }
}
