namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq.Internal;

[Category("Diagnostic: WSQ - Encoder Follow-On QBin Synthesis")]
internal sealed class WsqHighPrecisionRegionTwoFollowOnTests
{
    [Test]
    [DisplayName("Should show cmp00005 follow-on qbin stays above both NBIS and NIST in region 2 under the current high-rate path")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnQbinStaysAboveBothNbisAndNistInRegion2UnderTheCurrentHighRatePath(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.TargetSubbandIndex).IsEqualTo(4);
        await Assert.That(snapshot.TargetLocation.SubbandIndex).IsEqualTo(4);
        await Assert.That(snapshot.CurrentQuantizationBin > snapshot.NbisQuantizationBin).IsTrue();
        await Assert.That(snapshot.NbisQuantizationBin > snapshot.ReferenceQuantizationBin).IsTrue();
    }

    [Test]
    [DisplayName("Should expose the exact qbin interval for cmp00005 follow-on in region 2")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldExposeTheExactQbinIntervalForCmp00005FollowOnInRegion2(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.ThresholdQuantizationBinForNbisBucket < snapshot.CurrentQuantizationBin).IsTrue();
        await Assert.That(snapshot.ThresholdQuantizationBinForNbisBucket >= snapshot.NbisQuantizationBin).IsTrue();
        await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket < snapshot.CurrentQuantizationBin).IsTrue();
        await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket >= snapshot.ReferenceQuantizationBin).IsTrue();
    }

    [Test]
    [DisplayName("Should show sigma and initial-bin float precision moves cmp00005 follow-on qbin toward NIST in region 2")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowSigmaAndInitialBinFloatPrecisionMovesCmp00005FollowOnQbinTowardNistInRegion2(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.ReferenceQuantizationBin);
        var sigmaInitialDelta = Math.Abs(snapshot.SigmaAndInitialQuantizationBin - snapshot.ReferenceQuantizationBin);
        var sigmaInitialAndScaleDelta = Math.Abs(snapshot.SigmaInitialAndScaleQuantizationBin - snapshot.ReferenceQuantizationBin);

        await Assert.That(sigmaInitialDelta < currentDelta).IsTrue();
        await Assert.That(sigmaInitialAndScaleDelta < currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show float product precision also moves cmp00005 follow-on qbin toward NIST in region 2, but not enough to close it")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowFloatProductPrecisionAlsoMovesCmp00005FollowOnQbinTowardNistInRegion2ButNotEnoughToCloseIt(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.ReferenceQuantizationBin);
        var productDelta = Math.Abs(snapshot.ProductQuantizationBin - snapshot.ReferenceQuantizationBin);
        var productAndScaleDelta = Math.Abs(snapshot.ProductAndScaleQuantizationBin - snapshot.ReferenceQuantizationBin);

        await Assert.That(productDelta < currentDelta).IsTrue();
        await Assert.That(productAndScaleDelta < currentDelta).IsTrue();
        await Assert.That(snapshot.ProductAndScaleQuantizationBin > snapshot.ReferenceQuantizationBin).IsTrue();
    }

    [Test]
    [DisplayName("Should show even the fully single-precision high-rate trace leaves cmp00005 follow-on qbin above NIST in region 2")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowEvenTheFullySinglePrecisionHighRateTraceLeavesCmp00005FollowOnQbinAboveNistInRegion2(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.ReferenceQuantizationBin);
        var allSinglePrecisionDelta = Math.Abs(snapshot.AllSinglePrecisionQuantizationBin - snapshot.ReferenceQuantizationBin);

        await Assert.That(allSinglePrecisionDelta < currentDelta).IsTrue();
        await Assert.That(snapshot.AllSinglePrecisionQuantizationBin > snapshot.ReferenceQuantizationBin).IsTrue();
    }

    [Test]
    [DisplayName("Should show the known precision variants still stay above the NBIS and NIST qbin thresholds for cmp00005 follow-on in region 2")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowTheKnownPrecisionVariantsStillStayAboveTheNbisAndNistQbinThresholdsForCmp00005FollowOnInRegion2(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.SigmaInitialAndScaleQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.ProductAndScaleQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.AllSinglePrecisionQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.SigmaInitialAndScaleQuantizationBin > snapshot.ThresholdQuantizationBinForReferenceBucket).IsTrue();
        await Assert.That(snapshot.ProductAndScaleQuantizationBin > snapshot.ThresholdQuantizationBinForReferenceBucket).IsTrue();
        await Assert.That(snapshot.AllSinglePrecisionQuantizationBin > snapshot.ThresholdQuantizationBinForReferenceBucket).IsTrue();
    }

    [Test]
    [DisplayName("Should show cmp00005 follow-on region-2 qbin overshoot is not removed by changing only the target initial bin or only the shared scale")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnRegionTwoQbinOvershootIsNotRemovedByChangingOnlyTheTargetInitialBinOrOnlyTheSharedScale(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.SigmaAndInitialInitialWithCurrentScaleQuantizationBin < snapshot.CurrentQuantizationBin).IsTrue();
        await Assert.That(snapshot.CurrentInitialWithSigmaAndInitialScaleQuantizationBin < snapshot.CurrentQuantizationBin).IsTrue();
        await Assert.That(snapshot.SigmaAndInitialInitialWithCurrentScaleQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.CurrentInitialWithSigmaAndInitialScaleQuantizationBin > snapshot.ThresholdQuantizationBinForNbisBucket).IsTrue();
        await Assert.That(snapshot.SigmaAndInitialInitialWithCurrentScaleQuantizationBin > snapshot.ThresholdQuantizationBinForReferenceBucket).IsTrue();
        await Assert.That(snapshot.CurrentInitialWithSigmaAndInitialScaleQuantizationBin > snapshot.ThresholdQuantizationBinForReferenceBucket).IsTrue();
    }

    [Test]
    [DisplayName("Should show a more literal float initial-bin formula is not the missing region-2 lever for cmp00005 follow-on")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowAMoreLiteralFloatInitialBinFormulaIsNotTheMissingRegionTwoLeverForCmp00005FollowOn(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.LiteralSigmaAndInitialInitialQuantizationBin)
            .IsEqualTo(snapshot.SigmaAndInitialInitialQuantizationBin);
        await Assert.That(snapshot.LiteralSigmaAndInitialInitialWithCurrentScaleQuantizationBin)
            .IsEqualTo(snapshot.SigmaAndInitialInitialWithCurrentScaleQuantizationBin);
        await Assert.That(snapshot.LiteralSigmaInitialAndScaleQuantizationBin)
            .IsEqualTo(snapshot.SigmaInitialAndScaleQuantizationBin);
    }

    [Test]
    [DisplayName("Should show cmp00005 follow-on region-2 qbin drift already starts from a higher managed variance input")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnRegion2QbinDriftAlreadyStartsFromAHigherManagedVarianceInput(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.CurrentVariance > snapshot.NbisVariance).IsTrue();
    }

    [Test]
    [DisplayName("Should show cmp00005 follow-on region-2 qbin drift is not explained by the target subband variance alone")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnRegion2QbinDriftIsNotExplainedByTheTargetSubbandVarianceAlone(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var targetSubbandOnlyDelta = Math.Abs(snapshot.TargetSubbandNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);
        var fullRegionTwoDelta = Math.Abs(snapshot.FullRegionTwoNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(targetSubbandOnlyDelta <= currentDelta).IsTrue();
        await Assert.That(snapshot.TargetSubbandNbisVarianceSplicedQuantizationBin)
            .IsNotEqualTo(snapshot.NbisQuantizationBin);
        await Assert.That(fullRegionTwoDelta <= targetSubbandOnlyDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show the full region-2 splice improves cmp00005 follow-on through shared scale coupling more than through subband-4 initial-bin changes alone")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowTheFullRegionTwoSpliceImprovesCmp00005FollowOnThroughSharedScaleCouplingMoreThanThroughSubband4InitialBinChangesAlone(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var scaleOnlyDelta = Math.Abs(snapshot.CurrentInitialWithFullRegionTwoNbisScaleQuantizationBin - snapshot.NbisQuantizationBin);
        var initialOnlyDelta = Math.Abs(snapshot.FullRegionTwoNbisInitialWithCurrentScaleQuantizationBin - snapshot.NbisQuantizationBin);
        var fullRegionTwoDelta = Math.Abs(snapshot.FullRegionTwoNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(scaleOnlyDelta <= initialOnlyDelta).IsTrue();
        await Assert.That(fullRegionTwoDelta <= scaleOnlyDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show cmp00005 follow-on qbin drift is coupled to multiple region-2 variances, not just subband 4")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowCmp00005FollowOnQbinDriftIsCoupledToMultipleRegion2VariancesNotJustSubband4(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.NbisQuantizationBin);
        var bestSingleSubbandDelta =
            Math.Abs(snapshot.BestSingleSubbandNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);
        var bestNonTargetSubbandDelta =
            Math.Abs(snapshot.BestNonTargetSubbandNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);
        var fullRegionTwoDelta =
            Math.Abs(snapshot.FullRegionTwoNbisVarianceSplicedQuantizationBin - snapshot.NbisQuantizationBin);

        await Assert.That(snapshot.ImprovingNonTargetSubbandCount > 0).IsTrue();
        await Assert.That(snapshot.BestNonTargetSubbandNbisVarianceSplicedIndex >= 0).IsTrue();
        await Assert.That(bestNonTargetSubbandDelta < currentDelta).IsTrue();
        await Assert.That(fullRegionTwoDelta <= bestSingleSubbandDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show no single region-2 NBIS variance splice closes cmp00005 follow-on qbin exactly")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowNoSingleRegion2NbisVarianceSpliceClosesCmp00005FollowOnQbinExactly(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);

        await Assert.That(snapshot.BestSingleSubbandNbisVarianceSplicedIndex >= WsqConstants.StartSizeRegion2).IsTrue();
        await Assert.That(snapshot.BestSingleSubbandNbisVarianceSplicedIndex < WsqConstants.StartSizeRegion3).IsTrue();
        await Assert.That(snapshot.BestSingleSubbandNbisVarianceSplicedQuantizationBin)
            .IsNotEqualTo(snapshot.NbisQuantizationBin);
    }

    [Test]
    [DisplayName("Should show the float and NBIS-style accumulation variance variants move cmp00005 follow-on region-2 variance toward NBIS")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowTheFloatAndNbisStyleAccumulationVarianceVariantsMoveCmp00005FollowOnRegion2VarianceTowardNbis(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentVariance - snapshot.NbisVariance);
        var floatDelta = Math.Abs(snapshot.FloatVariance - snapshot.NbisVariance);
        var singlePrecisionAccumulationDelta =
            Math.Abs(snapshot.DoubleSinglePrecisionAccumulationVariance - snapshot.NbisVariance);

        await Assert.That(floatDelta <= currentDelta).IsTrue();
        await Assert.That(singlePrecisionAccumulationDelta <= currentDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show a region-2 variance splice alone is the wrong fix for cmp00005 follow-on qbin")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionRegionTwoFollowOnReferenceCases))]
    public async Task ShouldShowARegion2VarianceSpliceAloneIsTheWrongFixForCmp00005FollowOnQbin(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqHighPrecisionRegionTwoFollowOnSnapshotBuilder.CreateAsync(testCase);
        var currentDelta = Math.Abs(snapshot.CurrentQuantizationBin - snapshot.ReferenceQuantizationBin);
        var floatVarianceDelta = Math.Abs(snapshot.FloatVarianceSplicedQuantizationBin - snapshot.ReferenceQuantizationBin);
        var singlePrecisionAccumulationDelta =
            Math.Abs(snapshot.SinglePrecisionAccumulationVarianceSplicedQuantizationBin - snapshot.ReferenceQuantizationBin);

        await Assert.That(floatVarianceDelta > currentDelta).IsTrue();
        await Assert.That(singlePrecisionAccumulationDelta > currentDelta).IsTrue();
    }
}
