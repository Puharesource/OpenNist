namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - Encoder Blocker Parts")]
internal sealed class WsqHighPrecisionSubbandZeroBiasTests
{
    [Test]
    [DisplayName("Should show a tiny uniform subband-0 qbin bias can move the shared blocker class at the local coefficient")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowATinyUniformSubbandZeroQbinBiasCanMoveTheSharedBlockerClassAtTheLocalCoefficient(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasQuantizedCoefficient)
                    .IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasQuantizedCoefficient)
                    .IsEqualTo(snapshot.ReferenceQuantizedCoefficient);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show a tiny uniform subband-0 qbin bias is not a safe whole-file fix for the blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowATinyUniformSubbandZeroQbinBiasIsNotASafeWholeFileFixForTheBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch.MismatchIndex).IsEqualTo(52);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch.MismatchIndex >= 0).IsTrue();
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch.MismatchIndex)
                    .IsNotEqualTo(snapshot.MismatchIndex);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should pinpoint where the subband-0 qbin bias first fails next for each blocker class")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldPinpointWhereTheSubbandZeroQbinBiasFirstFailsNextForEachBlockerClass(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await AssertVariantMismatch(snapshot.ProductionWithSubbandZeroBiasVariantMismatch, 6576, 5, 30, 24, 24, 78);
                break;
            case "cmp00005.raw":
                await AssertVariantMismatch(snapshot.ProductionWithSubbandZeroBiasVariantMismatch, 2547, 4, 0, 51, 103, 0);
                break;
            case "a070.raw":
                await AssertVariantMismatch(snapshot.ProductionWithSubbandZeroBiasVariantMismatch, 171, 0, 3, 21, 21, 3);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the subband-0 qbin bias collapses cmp00005 and a070 into the same next mismatch as NBIS-bin substitution")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSubbandZeroQbinBiasCollapsesCmp00005AndA070IntoTheSameNextMismatchAsNbisBinSubstitution(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch)
                    .IsNotEqualTo(snapshot.ProductionWithNbisBinsVariantMismatch);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch)
                    .IsEqualTo(snapshot.ProductionWithNbisBinsVariantMismatch);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the subband-0 qbin bias exposes different second-stage classes for cmp00005 and a070")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSubbandZeroQbinBiasExposesDifferentSecondStageClassesForCmp00005AndA070(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot).IsNotNull();
        var followOn = snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot!.Value;

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(5);
                break;
            case "cmp00005.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(4);
                break;
            case "a070.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(0);
                await Assert.That(followOn.VariantQuantizationBin < snapshot.RawProductionQuantizationBin).IsTrue();
                await Assert.That(followOn.VariantQuantizationBin > followOn.NbisQuantizationBin).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the subband-0 qbin bias only closes the local blocker bucket and not the deeper follow-on bucket")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSubbandZeroQbinBiasOnlyClosesTheLocalBlockerBucketAndNotTheDeeperFollowOnBucket(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot).IsNotNull();
        var followOn = snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot!.Value;

        await Assert.That(followOn.VariantQuantizedCoefficient).IsNotEqualTo(followOn.ReferenceQuantizedCoefficient);

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
            case "cmp00005.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsNotEqualTo(0);
                await Assert.That(followOn.VariantQuantizedCoefficient).IsEqualTo(followOn.NbisQuantizedCoefficient);
                break;
            case "a070.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(0);
                await Assert.That(followOn.VariantFloatPreCastQuantizationValue).IsNotEqualTo(followOn.NbisFloatPreCastQuantizationValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should split the follow-on blocker class into cmp00005 subband-4 qbin drift and a070 deeper subband-0 behavior")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldSplitTheFollowOnBlockerClassIntoCmp00005Subband4QbinDriftAndA070DeeperSubband0Behavior(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot).IsNotNull();
        var followOn = snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot!.Value;

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(5);
                break;
            case "cmp00005.raw":
                await Assert.That(followOn.VariantQuantizedCoefficient).IsEqualTo(followOn.NbisQuantizedCoefficient);
                await Assert.That(followOn.VariantQuantizedCoefficient).IsNotEqualTo(followOn.ReferenceQuantizedCoefficient);
                await Assert.That(followOn.VariantQuantizationBin > followOn.NbisQuantizationBin).IsTrue();
                await Assert.That(followOn.NbisQuantizationBin > followOn.ReferenceQuantizationBin).IsTrue();
                break;
            case "a070.raw":
                await Assert.That(followOn.VariantQuantizedCoefficient).IsEqualTo(followOn.NbisQuantizedCoefficient);
                await Assert.That(followOn.VariantQuantizedCoefficient).IsNotEqualTo(followOn.ReferenceQuantizedCoefficient);
                await Assert.That(Math.Abs(followOn.FloatWaveletCoefficient - followOn.NbisWaveletCoefficient)).IsEqualTo(0.0f);
                await Assert.That(followOn.VariantQuantizationBin < followOn.NbisQuantizationBin).IsTrue();
                await Assert.That(followOn.VariantQuantizationBin > followOn.ReferenceQuantizationBin).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show cmp00005 follow-on is a region-2 qbin drift while a070 follow-on stays in the region-1 source-sensitive path")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnIsARegion2QbinDriftWhileA070FollowOnStaysInTheRegion1SourceSensitivePath(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot).IsNotNull();
        var followOn = snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot!.Value;

        switch (testCase.FileName)
        {
            case "cmp00003.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsGreaterThanOrEqualTo(4);
                break;
            case "cmp00005.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(4);
                await Assert.That(Math.Abs(followOn.ProductionWaveletCoefficient - followOn.NbisWaveletCoefficient) < 1e-4).IsTrue();
                await Assert.That(followOn.VariantQuantizationBin > followOn.ReferenceQuantizationBin).IsTrue();
                break;
            case "a070.raw":
                await Assert.That(followOn.MismatchLocation.SubbandIndex).IsEqualTo(0);
                await Assert.That(Math.Abs(followOn.FloatWaveletCoefficient - followOn.NbisWaveletCoefficient)).IsEqualTo(0.0f);
                await Assert.That(Math.Abs(followOn.ProductionWaveletCoefficient - followOn.NbisWaveletCoefficient) > 0.0).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should expose the exact qbin interval at the first follow-on mismatch after the subband-0 bias")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldExposeTheExactQbinIntervalAtTheFirstFollowOnMismatchAfterTheSubbandZeroBias(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        await Assert.That(snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot).IsNotNull();
        var followOn = snapshot.ProductionWithSubbandZeroBiasFollowOnSnapshot!.Value;

        switch (testCase.FileName)
        {
            case "cmp00005.raw":
                await Assert.That(followOn.ThresholdQuantizationBinForNbisBucket < followOn.VariantQuantizationBin).IsTrue();
                await Assert.That(followOn.ThresholdQuantizationBinForReferenceBucket >= followOn.ReferenceQuantizationBin).IsTrue();
                await Assert.That(followOn.ThresholdQuantizationBinForReferenceBucket < followOn.VariantQuantizationBin).IsTrue();
                break;
            case "a070.raw":
                await Assert.That(followOn.ThresholdQuantizationBinForNbisBucket <= followOn.VariantQuantizationBin).IsTrue();
                await Assert.That(followOn.ThresholdQuantizationBinForReferenceBucket < followOn.VariantQuantizationBin).IsTrue();
                await Assert.That(followOn.ThresholdQuantizationBinForReferenceBucket > followOn.ReferenceQuantizationBin).IsTrue();
                break;
            case "cmp00003.raw":
                await Assert.That(followOn.ThresholdQuantizationBinForReferenceBucket < followOn.VariantQuantizationBin).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show a tiny uniform subband-0 qbin bias is unsafe for exact 2.25 bpp guard cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionProductPrecisionGuardReferenceCases))]
    public async Task ShouldShowATinyUniformSubbandZeroQbinBiasIsUnsafeForExact225BppGuardCases(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.ProductionWithSubbandZeroBiasVariantMismatch.MismatchIndex >= 0).IsTrue();
    }

    private static async Task AssertVariantMismatch(
        WsqVariantMismatchSnapshot snapshot,
        int mismatchIndex,
        int subbandIndex,
        int row,
        int column,
        int imageX,
        int imageY)
    {
        await Assert.That(snapshot.MismatchIndex).IsEqualTo(mismatchIndex);
        await Assert.That(snapshot.MismatchLocation).IsNotNull();

        var mismatchLocation = snapshot.MismatchLocation!.Value;
        await Assert.That(mismatchLocation.SubbandIndex).IsEqualTo(subbandIndex);
        await Assert.That(mismatchLocation.Row).IsEqualTo(row);
        await Assert.That(mismatchLocation.Column).IsEqualTo(column);
        await Assert.That(mismatchLocation.ImageX).IsEqualTo(imageX);
        await Assert.That(mismatchLocation.ImageY).IsEqualTo(imageY);
    }
}
