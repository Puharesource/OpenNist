namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - NBIS Encoder Blocker Parts")]
internal sealed class WsqNbisEncoderBlockerPartTests
{
    [Test]
    [DisplayName("Should pinpoint the exact first NBIS mismatch coordinate for the focused 2.25 bpp blocker cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis225FocusedBlockerReferenceCases))]
    public async Task ShouldPinpointTheExactFirstNbisMismatchCoordinateForTheFocused225BppBlockerCases(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(snapshot.MismatchIndex).IsEqualTo(expected.MismatchIndex);
        await Assert.That(snapshot.MismatchLocation.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.MismatchLocation.Row).IsEqualTo(expected.Row);
        await Assert.That(snapshot.MismatchLocation.Column).IsEqualTo(expected.Column);
        await Assert.That(snapshot.ProductionQuantizedCoefficient).IsEqualTo(expected.ProductionQuantizedCoefficient);
        await Assert.That(snapshot.NbisQuantizedCoefficient).IsEqualTo(expected.NbisQuantizedCoefficient);
    }

    [Test]
    [DisplayName("Should show the focused 2.25 bpp NBIS blocker cases are still single-bucket qbin misses at the first mismatch coordinate")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeNbis225FocusedBlockerReferenceCases))]
    public async Task ShouldShowTheFocused225BppNbisBlockerCasesAreStillSingleBucketQbinMissesAtTheFirstMismatchCoordinate(WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);

        await Assert.That(Math.Abs(snapshot.ProductionQuantizedCoefficient - snapshot.NbisQuantizedCoefficient)).IsEqualTo(1);
        await Assert.That(snapshot.ProductionHalfZeroBin).IsEqualTo(snapshot.NbisHalfZeroBin);
        await Assert.That(snapshot.ProductionQuantizationBin > snapshot.NbisQuantizationBin).IsTrue();
        await Assert.That(snapshot.ProductionWithNbisZeroBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
        await Assert.That(snapshot.ProductionWithNbisQuantizationBinsQuantizedCoefficient).IsEqualTo(snapshot.NbisQuantizedCoefficient);
    }

    [Test]
    [DisplayName("Should expose the exact local qbin interval for the shared NBIS-aligned 2.25 bpp blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldExposeTheExactLocalQbinIntervalForTheSharedNbisAligned225BppBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);

        await Assert.That(snapshot.ThresholdQuantizationBinForNbisBucket < snapshot.RawProductionQuantizationBin).IsTrue();
        await Assert.That(snapshot.ThresholdQuantizationBinForNbisBucket >= snapshot.NbisQuantizationBin).IsTrue();
    }

    [Test]
    [DisplayName("Should show the shared NBIS-aligned blocker class is upstream of WSQ table rounding")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowTheSharedNbisAlignedBlockerClassIsUpstreamOfWsqTableRounding(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);

        await Assert.That(snapshot.RawProductionQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.ProductionQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
    }

    private static WsqNbisBlockerProfile GetExpectedProfile(string fileName)
    {
        return fileName switch
        {
            "a018.raw" => new(465, 0, 9, 42, 270, 271),
            "a070.raw" => new(6, 0, 0, 6, -544, -545),
            "a076.raw" => new(2499, 4, 1, 49, -50, -51),
            "a089.raw" => new(1560, 0, 31, 10, -344, -345),
            "a107.raw" => new(690, 0, 14, 4, -212, -213),
            "cmp00003.raw" => new(549, 0, 21, 3, 758, 759),
            "cmp00005.raw" => new(26, 0, 1, 0, 2518, 2519),
            "cmp00007.raw" => new(279, 0, 15, 9, 1199, 1200),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected NBIS-focused blocker file."),
        };
    }

    private readonly record struct WsqNbisBlockerProfile(
        int MismatchIndex,
        int SubbandIndex,
        int Row,
        int Column,
        short ProductionQuantizedCoefficient,
        short NbisQuantizedCoefficient);
}
