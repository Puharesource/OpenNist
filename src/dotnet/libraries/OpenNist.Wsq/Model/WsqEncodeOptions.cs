namespace OpenNist.Wsq.Model;

/// <summary>
/// Describes WSQ encoding parameters for a single operation.
/// </summary>
/// <param name="BitRate">The target bit allocation rate, for example 0.75 or 2.25.</param>
/// <param name="EncoderNumber">The WSQ encoder number written to the frame header.</param>
/// <param name="SoftwareImplementationNumber">The FBI-assigned software implementation number for the current platform.</param>
public readonly record struct WsqEncodeOptions(
    double BitRate,
    int EncoderNumber = 2,
    int? SoftwareImplementationNumber = null);
