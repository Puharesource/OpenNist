namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetManagedExtractor
{
    private const int MinimumWidth = 196;
    private const int MinimumHeight = 196;

    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> ExtractRaw(
        Nfiq2FingerprintImage fingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        return BuildManagedExtraction(fingerprintImage, capacity).RawMinutiae;
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

    public static Nfiq2FingerJetManagedExtractionResult BuildManagedExtraction(
        Nfiq2FingerprintImage fingerprintImage,
        int capacity = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        var croppedImage = fingerprintImage.CopyRemovingNearWhiteFrame();
        var paddedImage = PadToMinimumSize(croppedImage);
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
        if (fingerprintImage.Width >= MinimumWidth && fingerprintImage.Height >= MinimumHeight)
        {
            return fingerprintImage;
        }

        var paddedWidth = Math.Max(fingerprintImage.Width, MinimumWidth);
        var paddedHeight = Math.Max(fingerprintImage.Height, MinimumHeight);
        var paddedPixels = Enumerable.Repeat((byte)255, paddedWidth * paddedHeight).ToArray();
        var source = fingerprintImage.Pixels.Span;
        for (var row = 0; row < fingerprintImage.Height; row++)
        {
            source.Slice(row * fingerprintImage.Width, fingerprintImage.Width)
                .CopyTo(paddedPixels.AsSpan(row * paddedWidth, fingerprintImage.Width));
        }

        return new(paddedPixels, paddedWidth, paddedHeight, fingerprintImage.FingerCode, fingerprintImage.PixelsPerInch);
    }
}
