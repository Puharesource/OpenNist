namespace OpenNist.Wsq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Stable error codes exposed by the public OpenNist WSQ surface.
/// </summary>
[PublicAPI]
public static class WsqErrorCodes
{
    /// <summary>Unexpected library failure without a more specific public code.</summary>
    public const string UnexpectedFailure = "ONWSQ9000";

    /// <summary>One or more WSQ encode validation checks failed.</summary>
    public const string ValidationFailed = "ONWSQ1000";

    /// <summary>Raw-image width must be greater than zero.</summary>
    public const string RawImageWidthMustBePositive = "ONWSQ1001";

    /// <summary>Raw-image height must be greater than zero.</summary>
    public const string RawImageHeightMustBePositive = "ONWSQ1002";

    /// <summary>Only 8-bit grayscale raw WSQ input is currently supported.</summary>
    public const string RawImageBitsPerPixelUnsupported = "ONWSQ1003";

    /// <summary>WSQ bit rate must be greater than zero.</summary>
    public const string BitRateMustBePositive = "ONWSQ1004";

    /// <summary>WSQ encoder number must fit in one byte.</summary>
    public const string EncoderNumberOutOfRange = "ONWSQ1005";

    /// <summary>WSQ software implementation number must fit in an unsigned 16-bit value.</summary>
    public const string SoftwareImplementationNumberOutOfRange = "ONWSQ1006";

    /// <summary>The raw input byte count does not match the declared image area.</summary>
    public const string RawImageByteCountMismatch = "ONWSQ1007";

    /// <summary>The WSQ input bitstream is malformed or unsupported.</summary>
    public const string MalformedBitstream = "ONWSQ2000";
}
