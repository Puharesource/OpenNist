namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - Encoder QBin Synthesis")]
internal sealed class WsqHighPrecisionQbinSynthesisTests
{
    [Test]
    [DisplayName("Should show replacing only subband-0 variance with the NBIS value moves qbin[0] toward NBIS for each blocker case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowReplacingOnlySubbandZeroVarianceWithTheNbisValueMovesQbinZeroTowardNbisForEachBlockerCase(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var subbandZeroDelta = Math.Abs(snapshot.SubbandZeroNbisQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(snapshot.SubbandZeroNbisQuantizationBin < snapshot.CurrentQuantizationBin).IsTrue();
        await Assert.That(subbandZeroDelta < currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show the shared NBIS-aligned blocker class keeps the same active-set shape when only subband-0 variance changes")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowTheSharedNbisAlignedBlockerClassKeepsTheSameActiveSetShapeWhenOnlySubbandZeroVarianceChanges(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.SubbandZeroNbisIterationCount).IsEqualTo(snapshot.CurrentIterationCount);
        await Assert.That(snapshot.SubbandZeroNbisFinalActiveSubbandCount).IsEqualTo(snapshot.CurrentFinalActiveSubbandCount);
    }

    [Test]
    [DisplayName("Should show the shared NBIS-aligned blocker class is not explained by subband-0 variance alone")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowTheSharedNbisAlignedBlockerClassIsNotExplainedBySubbandZeroVarianceAlone(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.SubbandZeroNbisQuantizationBin).IsNotEqualTo(snapshot.NbisQuantizationBin);

        var subbandZeroDelta = Math.Abs(snapshot.SubbandZeroNbisQuantizationBin - snapshot.NbisQuantizationBin);
        var fullNbisDelta = Math.Abs(snapshot.NbisTraceQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(fullNbisDelta <= subbandZeroDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show the shared NBIS-aligned blocker class depends on more early-region variance coupling than subband 0 alone")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowTheSharedNbisAlignedBlockerClassDependsOnMoreEarlyRegionVarianceCouplingThanSubbandZeroAlone(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var subbandZeroDelta = Math.Abs(snapshot.SubbandZeroNbisQuantizationBin - snapshot.NbisQuantizationBin);
        var firstFourDelta = Math.Abs(snapshot.FirstFourNbisQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(firstFourDelta <= subbandZeroDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show the float early-region variance oracle is at least as useful as the current high-rate early-region variance for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowTheFloatEarlyRegionVarianceOracleIsAtLeastAsUsefulAsTheCurrentHighRateEarlyRegionVarianceForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var firstFourFloatDelta = Math.Abs(snapshot.FirstFourFloatQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(firstFourFloatDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show double-source variance with NBIS-style float accumulation is at least as useful as the current high-rate early-region variance for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowDoubleSourceVarianceWithNbisStyleFloatAccumulationIsAtLeastAsUsefulAsTheCurrentHighRateEarlyRegionVarianceForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var doubleSinglePrecisionAccumulationDelta =
            Math.Abs(snapshot.FirstFourDoubleSinglePrecisionAccumulationQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(doubleSinglePrecisionAccumulationDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show NBIS-style float scale precision moves qbin[0] toward NBIS relative to the old double-trace baseline")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowNbisStyleFloatScalePrecisionMovesQbinZeroTowardNbisRelativeToTheOldDoubleTraceBaseline(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentDoubleTraceQuantizationBin - snapshot.NbisQuantizationBin);
        var floatScaleDelta = Math.Abs(snapshot.CurrentSinglePrecisionScaleQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(floatScaleDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show NBIS-style float product precision moves qbin[0] toward NBIS for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowNbisStyleFloatProductPrecisionMovesQbinZeroTowardNbisForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentDoubleTraceQuantizationBin - snapshot.NbisQuantizationBin);
        var floatProductDelta = Math.Abs(snapshot.CurrentSinglePrecisionProductQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(floatProductDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show NBIS-style float product precision plus scale casting improves or matches the current production qbin[0] for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowNbisStyleFloatProductPrecisionPlusScaleCastingImprovesOrMatchesTheCurrentProductionQbinZeroForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var floatProductAndScaleDelta = Math.Abs(snapshot.CurrentSinglePrecisionProductAndScaleQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(floatProductAndScaleDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show NBIS-style float sigma and initial-bin precision moves qbin[0] toward NBIS for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowNbisStyleFloatSigmaAndInitialBinPrecisionMovesQbinZeroTowardNbisForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var sigmaAndInitialBinDelta = Math.Abs(snapshot.CurrentSinglePrecisionSigmaAndInitialBinQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(sigmaAndInitialBinDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show NBIS-style float sigma and initial-bin precision plus scale casting improves or matches the current production qbin[0] for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowNbisStyleFloatSigmaAndInitialBinPrecisionPlusScaleCastingImprovesOrMatchesTheCurrentProductionQbinZeroForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var sigmaInitialAndScaleDelta = Math.Abs(snapshot.CurrentSinglePrecisionSigmaInitialAndScaleQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(sigmaInitialAndScaleDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show reciprocal-area precision is not the lever for the shared blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionNbisAlignedBlockerReferenceCases))]
    public async Task ShouldShowReciprocalAreaPrecisionIsNotTheLeverForTheSharedBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionQbinSynthesisSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.CurrentSinglePrecisionReciprocalAreaSum).IsEqualTo(snapshot.CurrentDoubleTraceReciprocalAreaSum);
        await Assert.That(snapshot.CurrentSinglePrecisionReciprocalAreaQuantizationBin).IsEqualTo(snapshot.CurrentDoubleTraceQuantizationBin);
    }
}
