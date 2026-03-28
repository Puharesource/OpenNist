namespace OpenNist.Wsq.Internal.Encoding;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Container;

internal static class WsqReferenceTables
{
    private static readonly float[] s_highPassFilterCoefficients =
    [
        0.06453888262893845f,
        -0.04068941760955844f,
        -0.41809227322221221f,
        0.78848561640566439f,
        -0.41809227322221221f,
        -0.04068941760955844f,
        0.06453888262893845f,
    ];

    private static readonly float[] s_lowPassFilterCoefficients =
    [
        0.03782845550699546f,
        -0.02384946501938000f,
        -0.11062440441842342f,
        0.37740285561265380f,
        0.85269867900940344f,
        0.37740285561265380f,
        -0.11062440441842342f,
        -0.02384946501938000f,
        0.03782845550699546f,
    ];

    public static WsqTransformTable StandardTransformTable { get; } = CreateStandardTransformTable();

    public static WsqTransformTable CreateStandardTransformTable()
    {
        return new(
            HighPassFilterLength: (byte)s_highPassFilterCoefficients.Length,
            LowPassFilterLength: (byte)s_lowPassFilterCoefficients.Length,
            LowPassFilterCoefficients: s_lowPassFilterCoefficients.ToArray(),
            HighPassFilterCoefficients: s_highPassFilterCoefficients.ToArray());
    }
}
