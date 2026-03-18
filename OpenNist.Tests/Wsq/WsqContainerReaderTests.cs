namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;

[Category("Unit: WSQ - Bitstream Parsing")]
internal sealed class WsqContainerReaderTests
{
    [Test]
    [DisplayName("should parse official NIST WSQ container metadata")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldParseOfficialNistWsqContainerMetadata(WsqEncodingReferenceCase testCase)
    {
        await using var wsqStream = File.OpenRead(testCase.ReferencePath);

        var container = await WsqContainerReader.ReadAsync(wsqStream);

        await Assert.That(container.FrameHeader.Width).IsEqualTo((ushort)testCase.RawImage.Width);
        await Assert.That(container.FrameHeader.Height).IsEqualTo((ushort)testCase.RawImage.Height);
        await Assert.That(container.FrameHeader.Black).IsEqualTo((byte)0);
        await Assert.That(container.FrameHeader.White).IsEqualTo((byte)255);
        await Assert.That(container.TransformTable.LowPassFilterCoefficients.Count).IsEqualTo(9);
        await Assert.That(container.TransformTable.HighPassFilterCoefficients.Count).IsEqualTo(7);
        await Assert.That(container.QuantizationTable.QuantizationBins.Count).IsEqualTo(64);
        await Assert.That(container.QuantizationTable.ZeroBins.Count).IsEqualTo(64);
        await Assert.That(container.HuffmanTables.Count > 0).IsTrue();
        await Assert.That(container.Blocks.Count).IsEqualTo(3);
        await Assert.That(container.PixelsPerInch).IsNull();
    }

    [Test]
    [DisplayName("should parse each official NIST WSQ block with a defined Huffman table")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldParseEachOfficialNistWsqBlockWithADefinedHuffmanTable(WsqEncodingReferenceCase testCase)
    {
        await using var wsqStream = File.OpenRead(testCase.ReferencePath);

        var container = await WsqContainerReader.ReadAsync(wsqStream);
        var definedTableIds = container.HuffmanTables.Select(static table => table.TableId).ToHashSet();

        await Assert.That(container.Blocks.All(block => block.EncodedByteCount > 0)).IsTrue();
        await Assert.That(container.Blocks.All(block => definedTableIds.Contains(block.HuffmanTableId))).IsTrue();
        await Assert.That(container.Comments.Count).IsEqualTo(0);
    }
}
