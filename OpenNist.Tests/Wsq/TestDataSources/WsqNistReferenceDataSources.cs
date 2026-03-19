namespace OpenNist.Tests.Wsq.TestDataSources;

using OpenNist.Tests.Wsq.TestFixtures;

internal static class WsqNistReferenceDataSources
{
    public static IEnumerable<TestDataRow<WsqNistEncodeFixture>> EncodeFixtures()
    {
        return WsqNistReferenceFixtureCatalog.EncodeFixtures.Select(static fixture => new TestDataRow<WsqNistEncodeFixture>(
            fixture,
            DisplayName: $"should map {fixture.FileName} to both official NIST reference WSQ files"));
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> AllEncodeReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should encode {fixture.FileName} to the exact NIST 0.75 WSQ reference image");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should encode {fixture.FileName} to the exact NIST 2.25 WSQ reference image");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeCoefficientReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 0.75 bpp");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeCertificationReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should satisfy the published NIST encoder quantization thresholds for {fixture.FileName} at 0.75 bpp");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should satisfy the published NIST encoder quantization thresholds for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeFileSizeAndFrameHeaderReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should satisfy the published NIST encoder file-size and frame-header checks for {fixture.FileName} at 0.75 bpp");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should satisfy the published NIST encoder file-size and frame-header checks for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqDecodingReferenceCase>> AllDecodeReferenceCases()
    {
        return WsqNistReferenceFixtureCatalog.DecodeFixtures.Select(static testCase => new TestDataRow<WsqDecodingReferenceCase>(
            testCase,
            DisplayName: $"should decode {testCase.FileName} to the exact NBIS reference reconstruction from {testCase.ReferenceSet}"));
    }

    public static IEnumerable<TestDataRow<WsqDecodingReferenceCase>> NonStandardDecodeCases()
    {
        return WsqNistReferenceFixtureCatalog.NonStandardDecodeFixtures.Select(static testCase => new TestDataRow<WsqDecodingReferenceCase>(
            testCase,
            DisplayName: $"should decode {testCase.FileName} to the exact NBIS reference reconstruction from the non-standard tap-set corpus"));
    }
}
