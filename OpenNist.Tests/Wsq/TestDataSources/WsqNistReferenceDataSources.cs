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

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode075CoefficientReferenceCases()
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
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225CoefficientReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
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

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225ActiveExactCoefficientReferenceCases()
    {
        var activeExactFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "a039.raw",
            "a165.raw",
            "b082.raw",
            "b158.raw",
            "b186.raw",
            "cmp00007.raw",
            "cmp00009.raw",
            "cmp00010.raw",
            "cmp00011.raw",
            "cmp00012.raw",
            "cmp00013.raw",
            "cmp00014.raw",
            "cmp00015.raw",
            "cmp00016.raw",
            "cmp00017.raw",
            "sample_01.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => activeExactFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for active 2.25 bpp case {fixture.FileName}");
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

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionBlockerReferenceCases()
    {
        var blockerFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "a070.raw",
            "cmp00003.raw",
            "cmp00005.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => blockerFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should match the exact NIST quantized coefficient bins for blocker case {fixture.FileName} at 2.25 bpp via the high-precision encoder analysis path");
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
