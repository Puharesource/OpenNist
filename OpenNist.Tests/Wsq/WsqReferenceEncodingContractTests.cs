namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;

[Category("Contract: WSQ - NIST Reference Encoding")]
internal sealed class WsqReferenceEncodingContractTests
{
    [Test]
    [Skip("Enable when the managed WSQ encoder matches the official NIST reference codestreams byte-for-byte.")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldEncodeEveryOfficialRawFixtureToTheExactReferenceImage(WsqEncodingReferenceCase testCase)
    {
        var codec = CreateCodec();
        await AssertEncodedOutputMatchesReferenceAsync(codec, testCase);
    }

    private static WsqCodec CreateCodec()
    {
        return new();
    }

    private static async Task AssertEncodedOutputMatchesReferenceAsync(
        WsqCodec codec,
        WsqEncodingReferenceCase testCase)
    {
        await using var rawStream = File.OpenRead(testCase.RawPath);
        await using var expectedStream = File.OpenRead(testCase.ReferencePath);
        await using var encodedStream = new MemoryStream();

        await codec.EncodeAsync(
            rawStream,
            encodedStream,
            testCase.RawImage,
            new(testCase.BitRate));

        encodedStream.Position = 0;

        var expectedBytes = await ReadAllBytesAsync(expectedStream);
        var encodedBytes = encodedStream.ToArray();

        await Assert.That(encodedBytes.Length).IsEqualTo(expectedBytes.Length);
        await Assert.That(encodedBytes.SequenceEqual(expectedBytes)).IsTrue();
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
