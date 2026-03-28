namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Scaling;

internal static class WsqQuantizationTableFactory
{
    private static readonly WsqScaledUInt16 s_serializedBinCenter = new(44, 2);

    public static WsqQuantizationTable Create(
        ReadOnlySpan<float> quantizationBins,
        ReadOnlySpan<float> zeroBins)
    {
        var serializedQuantizationBins = new double[quantizationBins.Length];
        var serializedZeroBins = new double[zeroBins.Length];
        var serializedQuantizationBinValues = new WsqScaledUInt16[quantizationBins.Length];
        var serializedZeroBinValues = new WsqScaledUInt16[zeroBins.Length];

        for (var subband = 0; subband < quantizationBins.Length; subband++)
        {
            var serializedQuantizationBin = WsqScaledValueCodec.ScaleToUInt16(quantizationBins[subband]);
            var serializedZeroBin = WsqScaledValueCodec.ScaleToUInt16(zeroBins[subband]);
            serializedQuantizationBinValues[subband] = serializedQuantizationBin;
            serializedZeroBinValues[subband] = serializedZeroBin;
            serializedQuantizationBins[subband] = WsqScaledValueCodec.ScaleUInt16ToDouble(
                serializedQuantizationBin.RawValue,
                serializedQuantizationBin.Scale);
            serializedZeroBins[subband] = WsqScaledValueCodec.ScaleUInt16ToDouble(
                serializedZeroBin.RawValue,
                serializedZeroBin.Scale);
        }

        return new(
            BinCenter: WsqScaledValueCodec.ScaleUInt16ToDouble(
                s_serializedBinCenter.RawValue,
                s_serializedBinCenter.Scale),
            SerializedBinCenter: s_serializedBinCenter,
            QuantizationBins: serializedQuantizationBins,
            ZeroBins: serializedZeroBins,
            SerializedQuantizationBins: serializedQuantizationBinValues,
            SerializedZeroBins: serializedZeroBinValues);
    }

    public static WsqQuantizationTable Create(
        ReadOnlySpan<double> quantizationBins,
        ReadOnlySpan<double> zeroBins)
    {
        var serializedQuantizationBins = new double[quantizationBins.Length];
        var serializedZeroBins = new double[zeroBins.Length];
        var serializedQuantizationBinValues = new WsqScaledUInt16[quantizationBins.Length];
        var serializedZeroBinValues = new WsqScaledUInt16[zeroBins.Length];

        for (var subband = 0; subband < quantizationBins.Length; subband++)
        {
            var serializedQuantizationBin = WsqScaledValueCodec.ScaleToUInt16((float)quantizationBins[subband]);
            var serializedZeroBin = WsqScaledValueCodec.ScaleToUInt16((float)zeroBins[subband]);
            serializedQuantizationBinValues[subband] = serializedQuantizationBin;
            serializedZeroBinValues[subband] = serializedZeroBin;
            serializedQuantizationBins[subband] = WsqScaledValueCodec.ScaleUInt16ToDouble(
                serializedQuantizationBin.RawValue,
                serializedQuantizationBin.Scale);
            serializedZeroBins[subband] = WsqScaledValueCodec.ScaleUInt16ToDouble(
                serializedZeroBin.RawValue,
                serializedZeroBin.Scale);
        }

        return new(
            BinCenter: WsqScaledValueCodec.ScaleUInt16ToDouble(
                s_serializedBinCenter.RawValue,
                s_serializedBinCenter.Scale),
            SerializedBinCenter: s_serializedBinCenter,
            QuantizationBins: serializedQuantizationBins,
            ZeroBins: serializedZeroBins,
            SerializedQuantizationBins: serializedQuantizationBinValues,
            SerializedZeroBins: serializedZeroBinValues);
    }
}
