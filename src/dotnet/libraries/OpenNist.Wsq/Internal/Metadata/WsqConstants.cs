namespace OpenNist.Wsq.Internal.Metadata;

internal static class WsqConstants
{
    public const int BlockCount = 3;
    public const int MaxSubbands = 64;
    public const int MaxHuffmanBits = 16;
    public const int NumberOfSubbands = 60;
    public const int QuantizationTreeLength = 64;
    public const int StartSizeRegion2 = 4;
    public const int StartSizeRegion3 = 51;
    public const int StartSubband2 = 19;
    public const int StartSubband3 = 52;
    public const int StartSubbandDelete = NumberOfSubbands;
    public const float VarianceThreshold = 1.01f;
    public const int WaveletTreeLength = 20;
}
