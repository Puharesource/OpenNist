namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal.Decoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ encoder must mirror the NBIS reference implementation's exact zero checks for quantization bins.")]
internal static class WsqCoefficientQuantizer
{
    public static short[] Quantize(
        ReadOnlySpan<float> waveletData,
        ReadOnlySpan<WsqQuantizationNode> quantizationTree,
        int width,
        ReadOnlySpan<float> quantizationBins,
        ReadOnlySpan<float> zeroBins)
    {
        var quantizedCoefficients = new short[waveletData.Length];
        var coefficientIndex = 0;

        for (var subband = 0; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            if (quantizationBins[subband] == 0.0f)
            {
                continue;
            }

            var node = quantizationTree[subband];
            var halfZeroBin = (float)(zeroBins[subband] / 2.0);
            var rowStart = node.Y * width + node.X;

            for (var row = 0; row < node.Height; row++)
            {
                var pixelIndex = rowStart + row * width;

                for (var column = 0; column < node.Width; column++)
                {
                    var coefficient = waveletData[pixelIndex + column];
                    short quantizedCoefficient;

                    if (-halfZeroBin <= coefficient && coefficient <= halfZeroBin)
                    {
                        quantizedCoefficient = 0;
                    }
                    else if (coefficient > 0.0f)
                    {
                        quantizedCoefficient = checked((short)(((coefficient - halfZeroBin) / quantizationBins[subband]) + 1.0));
                    }
                    else
                    {
                        quantizedCoefficient = checked((short)(((coefficient + halfZeroBin) / quantizationBins[subband]) - 1.0));
                    }

                    quantizedCoefficients[coefficientIndex++] = quantizedCoefficient;
                }
            }
        }

        Array.Resize(ref quantizedCoefficients, coefficientIndex);
        return quantizedCoefficients;
    }
}
