namespace OpenNist.Tests.Wsq;

using System.Globalization;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

[Category("Integration: WSQ - Encoding")]
internal sealed class WsqCodecEncodingIntegrationTests
{
    [Test]
    [DisplayName("should encode every official raw fixture into a parseable WSQ stream that preserves the quantized coefficient bins")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldEncodeEveryOfficialRawFixtureIntoAParseableWsqStreamThatPreservesTheQuantizedCoefficientBins(
        WsqEncodingReferenceCase testCase)
    {
        var expectedRawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        var expectedAnalysis = WsqEncoderAnalysisPipeline.Analyze(expectedRawBytes, testCase.RawImage, new(testCase.BitRate));

        await using var rawStream = new MemoryStream(expectedRawBytes, writable: false);
        await using var wsqStream = new MemoryStream();
        var codec = new WsqCodec();

        await codec.EncodeAsync(rawStream, wsqStream, testCase.RawImage, new(testCase.BitRate));

        wsqStream.Position = 0;
        var container = await WsqContainerReader.ReadAsync(wsqStream);
        WsqWaveletTreeBuilder.Build(
            container.FrameHeader.Width,
            container.FrameHeader.Height,
            out var waveletTree,
            out var quantizationTree);

        var decodedQuantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(container, waveletTree, quantizationTree);

        await Assert.That(container.FrameHeader.Width).IsEqualTo((ushort)testCase.RawImage.Width);
        await Assert.That(container.FrameHeader.Height).IsEqualTo((ushort)testCase.RawImage.Height);
        await Assert.That(container.PixelsPerInch).IsEqualTo(testCase.RawImage.PixelsPerInch);
        await Assert.That(decodedQuantizedCoefficients.SequenceEqual(expectedAnalysis.QuantizedCoefficients)).IsTrue();
        await Assert.That(container.QuantizationTable).IsEquivalentTo(expectedAnalysis.QuantizationTable);
        await Assert.That(container.Blocks.Count).IsEqualTo(3);
        await Assert.That(container.HuffmanTables.Count).IsEqualTo(2);
        await Assert.That(container.Blocks[0].HuffmanTableId).IsEqualTo((byte)0);
        await Assert.That(container.Blocks[1].HuffmanTableId).IsEqualTo((byte)1);
        await Assert.That(container.Blocks[2].HuffmanTableId).IsEqualTo((byte)1);
        await Assert.That(container.Comments.Count).IsEqualTo(1);
        await Assert.That(container.Comments[0].Fields["COMPRESSION"]).IsEqualTo("WSQ");
        await Assert.That(container.Comments[0].Fields["WSQ_BITRATE"]).IsEqualTo(testCase.BitRate.ToString("0.000000", CultureInfo.InvariantCulture));
    }
}
