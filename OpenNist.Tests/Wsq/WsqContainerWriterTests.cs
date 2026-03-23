namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;

[Category("Unit: WSQ - Bitstream Writing")]
[Skip("Official bundled NIST WSQ fixtures use a different wire-format target than local NBIS 5.0.0; while NBIS 5.0.0 byte parity is the active target this official-corpus roundtrip contract is diagnostic only.")]
internal sealed class WsqContainerWriterTests
{
    [Test]
    [Skip("Official bundled NIST WSQ fixtures use a different wire-format target than local NBIS 5.0.0; while NBIS 5.0.0 byte parity is the active target this official-corpus roundtrip contract is diagnostic only.")]
    [DisplayName("should roundtrip official NIST WSQ containers through the writer without changing the parsed structure")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.AllEncodeReferenceCases))]
    public async Task ShouldRoundtripOfficialNistWsqContainersThroughTheWriterWithoutChangingTheParsedStructure(
        WsqEncodingReferenceCase testCase)
    {
        await using var referenceStream = File.OpenRead(testCase.ReferencePath);
        var originalContainer = await WsqContainerReader.ReadAsync(referenceStream);

        await using var rewrittenStream = new MemoryStream();
        await WsqContainerWriter.WriteAsync(rewrittenStream, originalContainer);
        rewrittenStream.Position = 0;

        var rewrittenContainer = await WsqContainerReader.ReadAsync(rewrittenStream);

        await Assert.That(rewrittenContainer.FrameHeader).IsEquivalentTo(originalContainer.FrameHeader);
        await Assert.That(rewrittenContainer.HuffmanTables).IsEquivalentTo(originalContainer.HuffmanTables);
        await Assert.That(rewrittenContainer.Comments).IsEquivalentTo(originalContainer.Comments);
        await Assert.That(rewrittenContainer.PixelsPerInch).IsEqualTo(originalContainer.PixelsPerInch);
        await Assert.That(rewrittenContainer.Blocks.Count).IsEqualTo(originalContainer.Blocks.Count);
        AssertTransformTablesAreEquivalent(rewrittenContainer.TransformTable, originalContainer.TransformTable);
        AssertQuantizationTablesAreEquivalent(rewrittenContainer.QuantizationTable, originalContainer.QuantizationTable);

        for (var blockIndex = 0; blockIndex < originalContainer.Blocks.Count; blockIndex++)
        {
            var originalBlock = originalContainer.Blocks[blockIndex];
            var rewrittenBlock = rewrittenContainer.Blocks[blockIndex];
            await Assert.That(rewrittenBlock.HuffmanTableId).IsEqualTo(originalBlock.HuffmanTableId);
            await Assert.That(rewrittenBlock.EncodedData.SequenceEqual(originalBlock.EncodedData)).IsTrue();
        }
    }

    private static void AssertTransformTablesAreEquivalent(
        WsqTransformTable actualTable,
        WsqTransformTable expectedTable)
    {
        const double coefficientTolerance = 0.000001d;

        AssertSequenceIsApproximatelyEqual(
            actualTable.HighPassFilterCoefficients,
            expectedTable.HighPassFilterCoefficients,
            coefficientTolerance,
            "high-pass filter");
        AssertSequenceIsApproximatelyEqual(
            actualTable.LowPassFilterCoefficients,
            expectedTable.LowPassFilterCoefficients,
            coefficientTolerance,
            "low-pass filter");
    }

    private static void AssertQuantizationTablesAreEquivalent(
        WsqQuantizationTable actualTable,
        WsqQuantizationTable expectedTable)
    {
        const double coefficientTolerance = 0.000001d;

        if (Math.Abs(actualTable.BinCenter - expectedTable.BinCenter) > coefficientTolerance)
        {
            throw new InvalidOperationException(
                $"WSQ bin-center roundtrip drift exceeded tolerance: actual={actualTable.BinCenter:G17}, expected={expectedTable.BinCenter:G17}.");
        }

        AssertSequenceIsApproximatelyEqual(
            actualTable.QuantizationBins,
            expectedTable.QuantizationBins,
            coefficientTolerance,
            "quantization bins");
        AssertSequenceIsApproximatelyEqual(
            actualTable.ZeroBins,
            expectedTable.ZeroBins,
            coefficientTolerance,
            "zero bins");
    }

    private static void AssertSequenceIsApproximatelyEqual(
        IReadOnlyList<float> actualValues,
        IReadOnlyList<float> expectedValues,
        double tolerance,
        string sequenceName)
    {
        if (actualValues.Count != expectedValues.Count)
        {
            throw new InvalidOperationException(
                $"WSQ {sequenceName} count changed during roundtrip: actual={actualValues.Count}, expected={expectedValues.Count}.");
        }

        for (var index = 0; index < actualValues.Count; index++)
        {
            var absoluteDifference = Math.Abs(actualValues[index] - expectedValues[index]);

            if (absoluteDifference <= tolerance)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"WSQ {sequenceName} coefficient {index} drifted beyond tolerance: actual={actualValues[index]:G9}, expected={expectedValues[index]:G9}.");
        }
    }

    private static void AssertSequenceIsApproximatelyEqual(
        IReadOnlyList<double> actualValues,
        IReadOnlyList<double> expectedValues,
        double tolerance,
        string sequenceName)
    {
        if (actualValues.Count != expectedValues.Count)
        {
            throw new InvalidOperationException(
                $"WSQ {sequenceName} count changed during roundtrip: actual={actualValues.Count}, expected={expectedValues.Count}.");
        }

        for (var index = 0; index < actualValues.Count; index++)
        {
            var absoluteDifference = Math.Abs(actualValues[index] - expectedValues[index]);

            if (absoluteDifference <= tolerance)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"WSQ {sequenceName} coefficient {index} drifted beyond tolerance: actual={actualValues[index]:G17}, expected={expectedValues[index]:G17}.");
        }
    }
}
