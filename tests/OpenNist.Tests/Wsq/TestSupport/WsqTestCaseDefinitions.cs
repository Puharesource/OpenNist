namespace OpenNist.Tests.Wsq.TestSupport;

using System.Globalization;
using OpenNist.Tests.Wsq.TestFixtures;

internal static class WsqTestCaseDefinitions
{
    internal const double s_lowBitRate = 0.75;
    internal const double s_highBitRate = 2.25;
    internal const string s_nbis500Version = "NBIS Release 5.0.0";

    internal static IEnumerable<WsqEncodingReferenceCase> EnumerateAllEncodeReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return CreateReferenceCase(fixture, s_lowBitRate);
            yield return CreateReferenceCase(fixture, s_highBitRate);
        }
    }

    internal static WsqEncodingReferenceCase CreateReferenceCase(WsqNistEncodeFixture fixture, double bitRate)
    {
        return new(
            fixture.FileName,
            bitRate,
            fixture.RawImage,
            fixture.RawPath,
            ResolveReferencePath(fixture, bitRate));
    }

    internal static string CreateCaseKey(string fileName, double bitRate)
    {
        return $"{fileName}|{FormatBitRate(bitRate)}";
    }

    internal static string FormatCaseName(WsqEncodingReferenceCase testCase)
    {
        return $"{testCase.FileName} @ {FormatBitRate(testCase.BitRate)}";
    }

    internal static string FormatBitRate(double bitRate)
    {
        return bitRate.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string ResolveReferencePath(WsqNistEncodeFixture fixture, double bitRate)
    {
        return bitRate switch
        {
            s_lowBitRate => fixture.ReferenceBitRate075Path,
            s_highBitRate => fixture.ReferenceBitRate225Path,
            _ => throw new ArgumentOutOfRangeException(nameof(bitRate), bitRate, "Unsupported WSQ test bitrate."),
        };
    }
}
