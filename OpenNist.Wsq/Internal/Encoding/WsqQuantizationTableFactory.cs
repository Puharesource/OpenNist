namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;

internal static class WsqQuantizationTableFactory
{
    private const double BinCenter = 44.0;

    public static WsqQuantizationTable Create(
        ReadOnlySpan<float> quantizationBins,
        ReadOnlySpan<float> zeroBins)
    {
        var serializedQuantizationBins = new double[quantizationBins.Length];
        var serializedZeroBins = new double[zeroBins.Length];

        for (var subband = 0; subband < quantizationBins.Length; subband++)
        {
            serializedQuantizationBins[subband] = WsqScaledValueCodec.RoundTripUInt16(quantizationBins[subband]);
            serializedZeroBins[subband] = WsqScaledValueCodec.RoundTripUInt16(zeroBins[subband]);
        }

        return new(
            BinCenter: WsqScaledValueCodec.RoundTripUInt16(BinCenter),
            QuantizationBins: serializedQuantizationBins,
            ZeroBins: serializedZeroBins);
    }

    public static WsqQuantizationTable Create(
        ReadOnlySpan<double> quantizationBins,
        ReadOnlySpan<double> zeroBins)
    {
        var serializedQuantizationBins = new double[quantizationBins.Length];
        var serializedZeroBins = new double[zeroBins.Length];

        for (var subband = 0; subband < quantizationBins.Length; subband++)
        {
            serializedQuantizationBins[subband] = WsqScaledValueCodec.RoundTripUInt16(quantizationBins[subband]);
            serializedZeroBins[subband] = WsqScaledValueCodec.RoundTripUInt16(zeroBins[subband]);
        }

        return new(
            BinCenter: WsqScaledValueCodec.RoundTripUInt16(BinCenter),
            QuantizationBins: serializedQuantizationBins,
            ZeroBins: serializedZeroBins);
    }
}
