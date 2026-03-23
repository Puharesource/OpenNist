namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestDiagnostics;
using OpenNist.Tests.Wsq.TestFixtures;

[Category("Diagnostic: WSQ - Encoder Blocker Parts")]
internal sealed class WsqHighPrecisionEncoderBlockerPartTests
{
    [Test]
    [DisplayName("Should pinpoint the exact first NIST mismatch coordinate for each targeted 2.25 bpp blocker case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldPinpointTheExactFirstNistMismatchCoordinateForEachTargeted225BppBlockerCase(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(snapshot.MismatchIndex).IsEqualTo(expected.MismatchIndex);
        await Assert.That(snapshot.MismatchLocation.SubbandIndex).IsEqualTo(expected.SubbandIndex);
        await Assert.That(snapshot.MismatchLocation.Row).IsEqualTo(expected.Row);
        await Assert.That(snapshot.MismatchLocation.Column).IsEqualTo(expected.Column);
        await Assert.That(snapshot.MismatchLocation.ImageX).IsEqualTo(expected.ImageX);
        await Assert.That(snapshot.MismatchLocation.ImageY).IsEqualTo(expected.ImageY);
        await Assert.That(snapshot.ProductionQuantizedCoefficient).IsEqualTo(expected.ProductionQuantizedCoefficient);
        await Assert.That(snapshot.ReferenceQuantizedCoefficient).IsEqualTo(expected.ReferenceQuantizedCoefficient);
    }

    [Test]
    [DisplayName("Should characterize the local quantization inputs at the targeted 2.25 bpp blocker coordinate")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldCharacterizeTheLocalQuantizationInputsAtTheTargeted225BppBlockerCoordinate(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        var expected = GetExpectedProfile(testCase.FileName);

        await Assert.That(double.IsFinite(snapshot.ProductionWaveletCoefficient)).IsTrue();
        await Assert.That(double.IsFinite(snapshot.NbisWaveletCoefficient)).IsTrue();
        await Assert.That(snapshot.RawProductionQuantizationBin >= snapshot.ProductionQuantizationBin)
            .IsEqualTo(expected.ExpectRawProductionQuantizationBinAtOrAboveSerializedBin);
        await Assert.That(snapshot.RawProductionHalfZeroBin >= snapshot.ProductionHalfZeroBin)
            .IsEqualTo(expected.ExpectRawProductionHalfZeroBinAtOrAboveSerializedHalfZeroBin);
        await Assert.That(snapshot.ProductionQuantizationBin > snapshot.NbisQuantizationBin)
            .IsEqualTo(expected.ExpectProductionQuantizationBinAboveNbis);
        await Assert.That(snapshot.ProductionHalfZeroBin > snapshot.NbisHalfZeroBin)
            .IsEqualTo(expected.ExpectProductionHalfZeroBinAboveNbis);
        await Assert.That(snapshot.ProductionQuantizedCoefficient - snapshot.ReferenceQuantizedCoefficient)
            .IsEqualTo(expected.ProductionQuantizedDeltaFromReference);
    }

    [Test]
    [DisplayName("Should show the float decomposition stays closer to NBIS than the high-rate path at the targeted 2.25 bpp blocker coordinate")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheFloatDecompositionStaysCloserToNbisThanTheHighRatePathAtTheTargeted225BppBlockerCoordinate(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var snapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAsync(testCase);
        var floatDelta = Math.Abs(snapshot.FloatWaveletCoefficient - snapshot.NbisWaveletCoefficient);
        var highRateDelta = Math.Abs(snapshot.ProductionWaveletCoefficient - snapshot.NbisWaveletCoefficient);

        await Assert.That(floatDelta <= highRateDelta).IsTrue();
    }

    [Test]
    [DisplayName("Should show cmp00003 is a NIST-only blocker while cmp00005 and a070 still align with NBIS at the failing coefficient")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowCmp00003IsANistOnlyBlockerWhileCmp00005AndA070StillAlignWithNbisAtTheFailingCoefficient(
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
                await Assert.That(snapshot.NbisQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.NbisQuantizedCoefficient).IsNotEqualTo(snapshot.ReferenceQuantizedCoefficient);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.NbisQuantizedCoefficient).IsEqualTo(snapshot.ReferenceQuantizedCoefficient);
                await Assert.That(snapshot.NbisQuantizedCoefficient).IsNotEqualTo(snapshot.ProductionQuantizedCoefficient);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the blocker coordinate sits just inside the wrong quantization bucket for the current high-rate path")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheBlockerCoordinateSitsJustInsideTheWrongQuantizationBucketForTheCurrentHighRatePath(
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
                await Assert.That(snapshot.ProductionPreCastQuantizationValue > -2058.0).IsTrue();
                await Assert.That(snapshot.ProductionPreCastQuantizationValue < -2057.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue > -2058.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue < -2057.0).IsTrue();
                break;
            case "cmp00005.raw":
                await Assert.That(snapshot.ProductionPreCastQuantizationValue > 2518.0).IsTrue();
                await Assert.That(snapshot.ProductionPreCastQuantizationValue < 2519.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue > 2519.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue < 2520.0).IsTrue();
                break;
            case "a070.raw":
                await Assert.That(snapshot.ProductionPreCastQuantizationValue > -545.0).IsTrue();
                await Assert.That(snapshot.ProductionPreCastQuantizationValue < -544.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue > -546.0).IsTrue();
                await Assert.That(snapshot.NbisPreCastQuantizationValue < -545.0).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the float-accurate quantizer boundary is the real blocker for the shared NBIS-aligned cases")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheFloatAccurateQuantizerBoundaryIsTheRealBlockerForTheSharedNbisAlignedCases(
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
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue > -2058.0f).IsTrue();
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue < -2057.0f).IsTrue();
                await Assert.That(snapshot.NbisFloatPreCastQuantizationValue > -2058.0f).IsTrue();
                await Assert.That(snapshot.NbisFloatPreCastQuantizationValue < -2057.0f).IsTrue();
                break;
            case "cmp00005.raw":
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue > 2518.0f).IsTrue();
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue < 2519.0f).IsTrue();
                await Assert.That(snapshot.ProductionWithNbisBinsFloatPreCastQuantizationValue > 2519.0f).IsTrue();
                await Assert.That(snapshot.ProductionWithNbisBinsFloatPreCastQuantizationValue < 2520.0f).IsTrue();
                break;
            case "a070.raw":
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue > -545.0f).IsTrue();
                await Assert.That(snapshot.ProductionFloatPreCastQuantizationValue < -544.0f).IsTrue();
                await Assert.That(snapshot.ProductionWithNbisBinsFloatPreCastQuantizationValue > -546.0f).IsTrue();
                await Assert.That(snapshot.ProductionWithNbisBinsFloatPreCastQuantizationValue < -545.0f).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the shared NBIS-aligned blocker class still reduces to a local qbin threshold at the failing coordinate")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSharedNbisAlignedBlockerClassStillReducesToALocalQbinThresholdAtTheFailingCoordinate(
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
                await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket < snapshot.RawProductionQuantizationBin).IsTrue();
                await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket < snapshot.NbisQuantizationBin).IsTrue();
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket < snapshot.RawProductionQuantizationBin).IsTrue();
                await Assert.That(snapshot.ThresholdQuantizationBinForReferenceBucket >= snapshot.NbisQuantizationBin).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should expose the local source-bin quantization matrix at the targeted blocker coordinate")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldExposeTheLocalSourceBinQuantizationMatrixAtTheTargetedBlockerCoordinate(
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
                await Assert.That(snapshot.FloatWithRawProductionBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.FloatWithNbisBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.FloatWithRawProductionBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisBinsQuantizedCoefficient).IsEqualTo(snapshot.ReferenceQuantizedCoefficient);
                await Assert.That(snapshot.FloatWithNbisBinsQuantizedCoefficient).IsEqualTo(snapshot.ReferenceQuantizedCoefficient);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show source-only swaps do not clear the first whole-file NIST mismatch for any targeted blocker case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowSourceOnlySwapsDoNotClearTheFirstWholeFileNistMismatchForAnyTargetedBlockerCase(
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
                await AssertVariantMismatch(snapshot.FloatWithRawProductionBinsVariantMismatch, 52, 0, 2, 0, 0, 2);
                break;
            case "cmp00005.raw":
                await AssertVariantMismatch(snapshot.FloatWithRawProductionBinsVariantMismatch, 26, 0, 1, 0, 0, 1);
                break;
            case "a070.raw":
                await AssertVariantMismatch(snapshot.FloatWithRawProductionBinsVariantMismatch, 6, 0, 0, 6, 6, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show cmp00003 collapses toward the a070 blocker class when only the NIST reference bins are substituted")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowCmp00003CollapsesTowardTheA070BlockerClassWhenOnlyTheNistReferenceBinsAreSubstituted(
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
                await Assert.That(snapshot.ProductionWithReferenceBinsVariantMismatch.MismatchIndex).IsEqualTo(6);
                await Assert.That(snapshot.ProductionWithReferenceBinsVariantMismatch.MismatchLocation).IsNotNull();

                var mismatchLocation = snapshot.ProductionWithReferenceBinsVariantMismatch.MismatchLocation!.Value;
                await Assert.That(mismatchLocation.SubbandIndex).IsEqualTo(0);
                await Assert.That(mismatchLocation.Row).IsEqualTo(0);
                await Assert.That(mismatchLocation.Column).IsEqualTo(6);
                await Assert.That(mismatchLocation.ImageX).IsEqualTo(6);
                await Assert.That(mismatchLocation.ImageY).IsEqualTo(0);
                break;
            case "cmp00005.raw":
                await AssertVariantMismatch(snapshot.ProductionWithReferenceBinsVariantMismatch, 26, 0, 1, 0, 0, 1);
                break;
            case "a070.raw":
                await AssertVariantMismatch(snapshot.ProductionWithReferenceBinsVariantMismatch, 250, 0, 5, 0, 0, 5);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show NBIS-bin substitutions move cmp00005 and a070 away from the blocker coordinate instead of fixing the whole file")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowNbisBinSubstitutionsMoveCmp00005AndA070AwayFromTheBlockerCoordinateInsteadOfFixingTheWholeFile(
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
                await AssertVariantMismatch(snapshot.ProductionWithNbisBinsVariantMismatch, 52, 0, 2, 0, 0, 2);
                break;
            case "cmp00005.raw":
                await AssertVariantMismatch(snapshot.ProductionWithNbisBinsVariantMismatch, 2547, 4, 0, 51, 103, 0);
                break;
            case "a070.raw":
                await AssertVariantMismatch(snapshot.ProductionWithNbisBinsVariantMismatch, 171, 0, 3, 21, 21, 3);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the shared cmp00005 and a070 blocker class is driven by qbin, not half-zero-bin")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSharedCmp00005AndA070BlockerClassIsDrivenByQbinNotHalfZeroBin(
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
                await Assert.That(snapshot.ProductionWithNbisQuantizationBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisZeroBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisQuantizationBinsVariantMismatch.MismatchIndex).IsEqualTo(snapshot.MismatchIndex);
                await Assert.That(snapshot.ProductionWithNbisZeroBinsVariantMismatch.MismatchIndex).IsEqualTo(snapshot.MismatchIndex);
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.ProductionWithNbisQuantizationBinsQuantizedCoefficient).IsEqualTo(snapshot.ReferenceQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisZeroBinsQuantizedCoefficient).IsEqualTo(snapshot.ProductionQuantizedCoefficient);
                await Assert.That(snapshot.ProductionWithNbisQuantizationBinsVariantMismatch)
                    .IsEqualTo(snapshot.ProductionWithNbisBinsVariantMismatch);
                await Assert.That(snapshot.ProductionWithNbisZeroBinsVariantMismatch.MismatchIndex).IsEqualTo(snapshot.MismatchIndex);
                await Assert.That(snapshot.ProductionWithNbisZeroBinsVariantMismatch.MismatchLocation).IsNotNull();
                await Assert.That(snapshot.ProductionWithNbisZeroBinsVariantMismatch.MismatchLocation!.Value)
                    .IsEqualTo(snapshot.MismatchLocation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should show the shared cmp00005 and a070 blocker class already drifts high at the subband-0 variance input")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionBlockerReferenceCases))]
    public async Task ShouldShowTheSharedCmp00005AndA070BlockerClassAlreadyDriftsHighAtTheSubbandZeroVarianceInput(
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
                await Assert.That(snapshot.RawProductionVariance > snapshot.NbisVariance).IsTrue();
                break;
            case "cmp00005.raw":
            case "a070.raw":
                await Assert.That(snapshot.RawProductionVariance > snapshot.NbisVariance).IsTrue();
                await Assert.That(snapshot.RawProductionQuantizationBin > snapshot.NbisQuantizationBin).IsTrue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase.FileName, "Unexpected blocker file.");
        }
    }

    [Test]
    [DisplayName("Should keep the exact NIST 2.25 bpp guard case separate from the NBIS-only divergence path")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeHighPrecisionGuardReferenceCases))]
    public async Task ShouldKeepTheExactNist225BppGuardCaseSeparateFromTheNbisOnlyDivergencePath(
        WsqEncodingReferenceCase testCase)
    {
        if (!WsqNbisOracleReader.IsAvailable())
        {
            return;
        }

        var nistGuardSnapshot = await WsqEncoderBlockerSnapshotBuilder.CreateAgainstNbisAsync(testCase);
        var floatDelta = Math.Abs(nistGuardSnapshot.FloatWaveletCoefficient - nistGuardSnapshot.NbisWaveletCoefficient);
        var highRateDelta = Math.Abs(nistGuardSnapshot.ProductionWaveletCoefficient - nistGuardSnapshot.NbisWaveletCoefficient);

        await Assert.That(nistGuardSnapshot.MismatchIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(highRateDelta <= floatDelta).IsTrue();
    }

    private static WsqEncoderBlockerExpectedProfile GetExpectedProfile(string fileName)
    {
        return fileName switch
        {
            "cmp00003.raw" => new(
                MismatchIndex: 52,
                SubbandIndex: 0,
                Row: 2,
                Column: 0,
                ImageX: 0,
                ImageY: 2,
                ProductionQuantizedCoefficient: -2057,
                ReferenceQuantizedCoefficient: -2058,
                ProductionQuantizedDeltaFromReference: 1,
                ExpectRawProductionQuantizationBinAtOrAboveSerializedBin: true,
                ExpectRawProductionHalfZeroBinAtOrAboveSerializedHalfZeroBin: true,
                ExpectProductionQuantizationBinAboveNbis: true,
                ExpectProductionHalfZeroBinAboveNbis: false),
            "cmp00005.raw" => new(
                MismatchIndex: 26,
                SubbandIndex: 0,
                Row: 1,
                Column: 0,
                ImageX: 0,
                ImageY: 1,
                ProductionQuantizedCoefficient: 2518,
                ReferenceQuantizedCoefficient: 2519,
                ProductionQuantizedDeltaFromReference: -1,
                ExpectRawProductionQuantizationBinAtOrAboveSerializedBin: true,
                ExpectRawProductionHalfZeroBinAtOrAboveSerializedHalfZeroBin: true,
                ExpectProductionQuantizationBinAboveNbis: true,
                ExpectProductionHalfZeroBinAboveNbis: true),
            "a070.raw" => new(
                MismatchIndex: 6,
                SubbandIndex: 0,
                Row: 0,
                Column: 6,
                ImageX: 6,
                ImageY: 0,
                ProductionQuantizedCoefficient: -544,
                ReferenceQuantizedCoefficient: -545,
                ProductionQuantizedDeltaFromReference: 1,
                ExpectRawProductionQuantizationBinAtOrAboveSerializedBin: true,
                ExpectRawProductionHalfZeroBinAtOrAboveSerializedHalfZeroBin: true,
                ExpectProductionQuantizationBinAboveNbis: true,
                ExpectProductionHalfZeroBinAboveNbis: true),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unexpected blocker file."),
        };
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

    private readonly record struct WsqEncoderBlockerExpectedProfile(
        int MismatchIndex,
        int SubbandIndex,
        int Row,
        int Column,
        int ImageX,
        int ImageY,
        short ProductionQuantizedCoefficient,
        short ReferenceQuantizedCoefficient,
        int ProductionQuantizedDeltaFromReference,
        bool ExpectRawProductionQuantizationBinAtOrAboveSerializedBin,
        bool ExpectRawProductionHalfZeroBinAtOrAboveSerializedHalfZeroBin,
        bool ExpectProductionQuantizationBinAboveNbis,
        bool ExpectProductionHalfZeroBinAboveNbis);
}
