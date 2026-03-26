namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Tests.Wsq.TestSupport;
using OpenNist.Wsq;

[Category("Contract: WSQ - NBIS Reference Encoding")]
internal sealed class WsqNbisEncodingContractTests
{
    private const int s_requiredExactCodestreamParityFloor = 80;

    [Test]
    [DisplayName("Should preserve the current exact NBIS 5.0.0 codestream-parity floor across the public encoder corpus")]
    public async Task ShouldPreserveTheCurrentExactNbis500CodestreamParityFloorAcrossThePublicEncoderCorpus()
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var exactCases = new List<string>();
        var mismatchCases = new List<string>();

        foreach (var testCase in WsqTestCaseDefinitions.EnumerateAllEncodeReferenceCases())
        {
            var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
            var managedBytes = await EncodeManagedAsync(rawBytes, testCase).ConfigureAwait(false);
            var nbisBytes = await WsqNbisOracleReader.ReadCodestreamAsync(testCase).ConfigureAwait(false);
            var formattedCase = FormatCaseName(testCase);

            if (managedBytes.AsSpan().SequenceEqual(nbisBytes))
            {
                exactCases.Add(formattedCase);
                continue;
            }

            mismatchCases.Add($"{formattedCase} (managed={managedBytes.Length}, nbis={nbisBytes.Length})");
        }

        if (exactCases.Count < s_requiredExactCodestreamParityFloor)
        {
            throw new InvalidOperationException(
                $"The managed encoder only matches {exactCases.Count} NBIS 5.0.0 codestream cases exactly, below the required floor of {s_requiredExactCodestreamParityFloor}. "
                + $"Exact cases: {string.Join(", ", exactCases)}. "
                + $"Mismatches: {string.Join("; ", mismatchCases)}.");
        }
    }

    [Test]
    [DisplayName("Should match the local NBIS 5.0.0 codestream for the current active exact codestream cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbisActiveExactCodestreamReferenceCases))]
    public async Task ShouldMatchTheLocalNbis500CodestreamForTheCurrentActiveExactCodestreamCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var managedBytes = await EncodeManagedAsync(rawBytes, testCase).ConfigureAwait(false);
        var nbisBytes = await WsqNbisOracleReader.ReadCodestreamAsync(testCase).ConfigureAwait(false);

        if (!managedBytes.AsSpan().SequenceEqual(nbisBytes))
        {
            throw new InvalidOperationException(
                $"{FormatCaseName(testCase)} regressed from the active exact NBIS 5.0.0 codestream set. "
                + $"managed={managedBytes.Length}, nbis={nbisBytes.Length}, firstDiff={FindFirstByteMismatchIndex(managedBytes, nbisBytes)}.");
        }
    }

    [Test]
    [DisplayName("Should match the local NBIS 5.0.0 codestream for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldMatchTheLocalNbis500CodestreamForEveryEncoderReferenceCase(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var managedBytes = await EncodeManagedAsync(rawBytes, testCase).ConfigureAwait(false);
        var nbisBytes = await WsqNbisOracleReader.ReadCodestreamAsync(testCase).ConfigureAwait(false);

        if (!managedBytes.AsSpan().SequenceEqual(nbisBytes))
        {
            throw new InvalidOperationException(
                $"{FormatCaseName(testCase)} diverges from the local NBIS 5.0.0 codestream. "
                + $"managed={managedBytes.Length}, nbis={nbisBytes.Length}, firstDiff={FindFirstByteMismatchIndex(managedBytes, nbisBytes)}.");
        }
    }

    private static async Task<byte[]> EncodeManagedAsync(ReadOnlyMemory<byte> rawBytes, WsqEncodingReferenceCase testCase)
    {
        await using var rawStream = new MemoryStream(rawBytes.ToArray(), writable: false);
        await using var wsqStream = new MemoryStream();
        var codec = new WsqCodec();
        await codec.EncodeAsync(rawStream, wsqStream, testCase.RawImage, new(testCase.BitRate)).ConfigureAwait(false);
        return wsqStream.ToArray();
    }

    private static int FindFirstByteMismatchIndex(ReadOnlySpan<byte> managedBytes, ReadOnlySpan<byte> nbisBytes)
    {
        var limit = Math.Min(managedBytes.Length, nbisBytes.Length);
        for (var index = 0; index < limit; index++)
        {
            if (managedBytes[index] != nbisBytes[index])
            {
                return index;
            }
        }

        return managedBytes.Length == nbisBytes.Length ? -1 : limit;
    }

    private static string FormatCaseName(WsqEncodingReferenceCase testCase)
    {
        return WsqTestCaseDefinitions.FormatCaseName(testCase);
    }
}
