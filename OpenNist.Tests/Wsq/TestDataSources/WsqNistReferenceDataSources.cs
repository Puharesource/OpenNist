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
}
