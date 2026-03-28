namespace OpenNist.Nfiq.Internal.FingerJet;

using OpenNist.Nfiq.Internal.Model;

internal static class Nfiq2FingerJetManagedExtractor
{
    private const int s_minimumWidth = 196;
    private const int s_minimumHeight = 196;

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        Nfiq2FingerprintImage fingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        return BuildManagedExtraction(fingerprintImage, capacity).RawMinutiae;
    }

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRawFromCroppedImage(
        Nfiq2FingerprintImage croppedFingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(croppedFingerprintImage);

        return BuildManagedExtractionFromCroppedImage(croppedFingerprintImage, capacity).RawMinutiae;
    }

    public static IReadOnlyList<Nfiq2Minutia> Extract(
        Nfiq2FingerprintImage fingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        var extraction = BuildManagedExtraction(fingerprintImage, capacity);
        return Nfiq2FingerJetMinutiaPostProcessor.Process(
            extraction.RawMinutiae,
            extraction.PreparedImage.PixelsPerInch,
            extraction.PreparedImage.XOffset,
            extraction.PreparedImage.YOffset);
    }

    public static IReadOnlyList<Nfiq2Minutia> ExtractFromCroppedImage(
        Nfiq2FingerprintImage croppedFingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(croppedFingerprintImage);

        var extraction = BuildManagedExtractionFromCroppedImage(croppedFingerprintImage, capacity);
        return Nfiq2FingerJetMinutiaPostProcessor.Process(
            extraction.RawMinutiae,
            extraction.PreparedImage.PixelsPerInch,
            extraction.PreparedImage.XOffset,
            extraction.PreparedImage.YOffset);
    }

    public static Nfiq2FingerJetManagedExtractionResult BuildManagedExtraction(
        Nfiq2FingerprintImage fingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        var croppedImage = fingerprintImage.CopyRemovingNearWhiteFrame();
        return BuildManagedExtractionFromCroppedImage(croppedImage, capacity);
    }

    public static Nfiq2FingerJetManagedExtractionResult BuildManagedExtractionFromCroppedImage(
        Nfiq2FingerprintImage croppedFingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(croppedFingerprintImage);

        var paddedImage = PadToMinimumSize(croppedFingerprintImage);
        var preparedImage = Nfiq2FingerJetImagePreparation.Prepare(paddedImage);
        var enhancedImage = Nfiq2FingerJetFftEnhancement.Enhance(preparedImage);
        var orientationMap = Nfiq2FingerJetOrientationMap.Compute(preparedImage, enhancedImage);
        var phasemap = Nfiq2FingerJetPhasemap.Build(enhancedImage, orientationMap.Orientation);
        var rawMinutiae = Nfiq2FingerJetMinutiaExtractor.ExtractRaw(phasemap, enhancedImage.Width, capacity);
        return new(
            enhancedImage,
            orientationMap.Orientation,
            orientationMap.Footprint,
            phasemap,
            rawMinutiae);
    }

    private static Nfiq2FingerprintImage PadToMinimumSize(Nfiq2FingerprintImage fingerprintImage)
    {
        if (fingerprintImage is { Width: >= s_minimumWidth, Height: >= s_minimumHeight })
        {
            return fingerprintImage;
        }

        var paddedWidth = Math.Max(fingerprintImage.Width, s_minimumWidth);
        var paddedHeight = Math.Max(fingerprintImage.Height, s_minimumHeight);
        var paddedPixels = GC.AllocateUninitializedArray<byte>(paddedWidth * paddedHeight);
        paddedPixels.AsSpan().Fill(byte.MaxValue);
        var source = fingerprintImage.Pixels.Span;
        for (var row = 0; row < fingerprintImage.Height; row++)
        {
            source.Slice(row * fingerprintImage.Width, fingerprintImage.Width)
                .CopyTo(paddedPixels.AsSpan(row * paddedWidth, fingerprintImage.Width));
        }

        return new(paddedPixels, paddedWidth, paddedHeight, fingerprintImage.FingerCode, fingerprintImage.PixelsPerInch);
    }
}
