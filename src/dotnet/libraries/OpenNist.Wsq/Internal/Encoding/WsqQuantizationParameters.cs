namespace OpenNist.Wsq.Internal.Encoding;

internal static class WsqQuantizationParameters
{
    private const float s_firstRegionReciprocalAreaSingle = 1.0f / 1024.0f;
    private const float s_secondRegionReciprocalAreaSingle = 1.0f / 256.0f;
    private const float s_thirdRegionReciprocalAreaSingle = 1.0f / 16.0f;

    private const double s_firstRegionReciprocalAreaDouble = 1.0 / 1024.0;
    private const double s_secondRegionReciprocalAreaDouble = 1.0 / 256.0;
    private const double s_thirdRegionReciprocalAreaDouble = 1.0 / 16.0;

    private const float s_defaultSubbandWeight = 1.0f;
    private static readonly float[] s_subbandWeights = CreateSubbandWeights();

    public static ReadOnlySpan<float> SubbandWeights => s_subbandWeights;

    public static void SetReciprocalSubbandAreas(Span<float> reciprocalSubbandAreas)
    {
        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            reciprocalSubbandAreas[subband] = s_firstRegionReciprocalAreaSingle;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.StartSizeRegion3; subband++)
        {
            reciprocalSubbandAreas[subband] = s_secondRegionReciprocalAreaSingle;
        }

        for (var subband = WsqConstants.StartSizeRegion3; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            reciprocalSubbandAreas[subband] = s_thirdRegionReciprocalAreaSingle;
        }
    }

    public static void SetReciprocalSubbandAreas(Span<double> reciprocalSubbandAreas)
    {
        for (var subband = 0; subband < WsqConstants.StartSizeRegion2; subband++)
        {
            reciprocalSubbandAreas[subband] = s_firstRegionReciprocalAreaDouble;
        }

        for (var subband = WsqConstants.StartSizeRegion2; subband < WsqConstants.StartSizeRegion3; subband++)
        {
            reciprocalSubbandAreas[subband] = s_secondRegionReciprocalAreaDouble;
        }

        for (var subband = WsqConstants.StartSizeRegion3; subband < WsqConstants.NumberOfSubbands; subband++)
        {
            reciprocalSubbandAreas[subband] = s_thirdRegionReciprocalAreaDouble;
        }
    }

    private static float[] CreateSubbandWeights()
    {
        var weights = new float[WsqConstants.MaxSubbands];
        Array.Fill(weights, s_defaultSubbandWeight, 0, WsqConstants.StartSubband3);
        weights[52] = 1.32f;
        weights[53] = 1.08f;
        weights[54] = 1.42f;
        weights[55] = 1.08f;
        weights[56] = 1.32f;
        weights[57] = 1.42f;
        weights[58] = 1.08f;
        weights[59] = 1.08f;
        return weights;
    }
}
