namespace OpenNist.Nfiq.Errors;

using JetBrains.Annotations;

/// <summary>
/// Stable error codes exposed by the public OpenNist NFIQ 2 surface.
/// </summary>
[PublicAPI]
public static class Nfiq2ErrorCodes
{
    /// <summary>Unexpected library failure without a more specific public code.</summary>
    public const string UnexpectedFailure = "ONNFIQ9000";

    /// <summary>Aggregate validation failure for one or more NFIQ 2 input issues.</summary>
    public const string ValidationFailed = "ONNFIQ1000";

    /// <summary>Raw-image width must be greater than zero.</summary>
    public const string RawImageWidthMustBePositive = "ONNFIQ1001";

    /// <summary>Raw-image height must be greater than zero.</summary>
    public const string RawImageHeightMustBePositive = "ONNFIQ1002";

    /// <summary>Raw-image bits per pixel must be 8.</summary>
    public const string RawImageBitsPerPixelUnsupported = "ONNFIQ1003";

    /// <summary>Raw-image resolution must be 500 PPI.</summary>
    public const string RawImagePixelsPerInchUnsupported = "ONNFIQ1004";

    /// <summary>Raw pixel buffer length must match width multiplied by height.</summary>
    public const string RawImagePixelBufferLengthMismatch = "ONNFIQ1005";

    /// <summary>Near-white trimming determined that every row is blank.</summary>
    public const string ImageRowsAppearBlank = "ONNFIQ1006";

    /// <summary>Near-white trimming determined that every column is blank.</summary>
    public const string ImageColumnsAppearBlank = "ONNFIQ1007";

    /// <summary>The trimmed fingerprint region remains wider than the supported limit.</summary>
    public const string TrimmedImageWidthTooLarge = "ONNFIQ1008";

    /// <summary>The trimmed fingerprint region remains taller than the supported limit.</summary>
    public const string TrimmedImageHeightTooLarge = "ONNFIQ1009";

    /// <summary>FingerJet CreateFeatureSet dimensions exceed the supported native limit.</summary>
    public const string FingerJetCreateFeatureSetDimensionsExceeded = "ONNFIQ1010";

    /// <summary>FingerJet CreateFeatureSet resolution falls outside the supported native range.</summary>
    public const string FingerJetCreateFeatureSetResolutionOutOfRange = "ONNFIQ1011";

    /// <summary>FingerJet CreateFeatureSet width falls outside the supported native range.</summary>
    public const string FingerJetCreateFeatureSetWidthOutOfRange = "ONNFIQ1012";

    /// <summary>FingerJet CreateFeatureSet height falls outside the supported native range.</summary>
    public const string FingerJetCreateFeatureSetHeightOutOfRange = "ONNFIQ1013";

    /// <summary>FingerJet extraction resolution falls outside the supported native range.</summary>
    public const string FingerJetExtractionResolutionOutOfRange = "ONNFIQ1014";

    /// <summary>FingerJet extraction width is smaller than the supported 500 PPI minimum.</summary>
    public const string FingerJetExtractionWidthTooSmall = "ONNFIQ1015";

    /// <summary>FingerJet extraction width exceeds the supported 500 PPI maximum.</summary>
    public const string FingerJetExtractionWidthTooLarge = "ONNFIQ1016";

    /// <summary>FingerJet extraction height is smaller than the supported 500 PPI minimum.</summary>
    public const string FingerJetExtractionHeightTooSmall = "ONNFIQ1017";

    /// <summary>FingerJet extraction height exceeds the supported 500 PPI maximum.</summary>
    public const string FingerJetExtractionHeightTooLarge = "ONNFIQ1018";

    /// <summary>Prepared pixel data is shorter than the declared prepared image size.</summary>
    public const string FingerJetInputPixelBufferTooSmall = "ONNFIQ1019";

    /// <summary>FingerJet crop planning diverged from the expected native behavior.</summary>
    public const string FingerJetCropPlanningDiverged = "ONNFIQ1020";

    /// <summary>FingerJet working-buffer requirements exceeded the supported native limit.</summary>
    public const string FingerJetWorkingBufferExceeded = "ONNFIQ1021";
}
