namespace OpenNist.Tests.Wsq.TestDataSources;

using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Tests.Wsq.TestSupport;

internal static class WsqNistReferenceDataSources
{
    private static readonly IReadOnlySet<string> s_activeExact225CoefficientFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "a165.raw",
        "b082.raw",
        "b158.raw",
        "b186.raw",
        "cmp00009.raw",
        "cmp00010.raw",
        "cmp00011.raw",
        "cmp00013.raw",
        "cmp00015.raw",
        "cmp00016.raw",
        "cmp00017.raw",
        "sample_01.raw",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionBlockerFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "a070.raw",
        "cmp00003.raw",
        "cmp00005.raw",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionGuardFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "cmp00017.raw",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionProductPrecisionGuardFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "a076.raw",
        "cmp00017.raw",
        "sample_19.raw",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionNbisAlignedBlockerFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "a070.raw",
        "cmp00005.raw",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionRegionTwoFollowOnFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "cmp00005.raw",
    };

    private static readonly IReadOnlySet<string> s_nbis225FocusedBlockerFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "a018.raw",
        "a070.raw",
        "a076.raw",
        "a089.raw",
        "a107.raw",
        "cmp00003.raw",
        "cmp00005.raw",
        "cmp00007.raw",
    };

    private static readonly IReadOnlySet<string> s_nbisCurrentMismatchCaseKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "a002.raw|2.25",
        "a018.raw|2.25",
        "a089.raw|2.25",
        "a107.raw|2.25",
        "b157.raw|2.25",
        "b158.raw|2.25",
        "cmp00001.raw|2.25",
        "cmp00004.raw|2.25",
        "cmp00005.raw|0.75",
        "cmp00005.raw|2.25",
        "cmp00006.raw|2.25",
        "cmp00007.raw|2.25",
        "cmp00008.raw|2.25",
        "cmp00011.raw|0.75",
        "cmp00011.raw|2.25",
        "cmp00017.raw|2.25",
        "sample_11.raw|2.25",
        "sample_19.raw|0.75",
        "sample_19.raw|2.25",
    };

    private static readonly IReadOnlySet<string> s_nbis075FocusedMismatchCaseKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "cmp00005.raw|0.75",
        "cmp00011.raw|0.75",
        "sample_19.raw|0.75",
    };

    private static readonly IReadOnlySet<string> s_nbis075DqtOnlyMismatchCaseKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "a001.raw|0.75",
        "a018.raw|0.75",
        "a107.raw|0.75",
    };

    private static readonly IReadOnlySet<string> s_nbis225RepresentativeDqtOnlyMismatchCaseKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "a001.raw|2.25",
        "a076.raw|2.25",
        "cmp00008.raw|2.25",
    };

    private static readonly IReadOnlySet<string> s_highPrecisionSerializedBinSensitiveExactFileNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "cmp00014.raw",
        "sample_01.raw",
        "sample_19.raw",
    };

    public static IEnumerable<TestDataRow<WsqNistEncodeFixture>> EncodeFixtures()
    {
        return WsqNistReferenceFixtureCatalog.EncodeFixtures.Select(static fixture => new TestDataRow<WsqNistEncodeFixture>(
            fixture,
            DisplayName: $"should map {fixture.FileName} to both official NIST reference WSQ files"));
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> AllEncodeReferenceCases()
    {
        return CreateAllBitRateRows(static (fixture, bitRate) =>
            $"should encode {fixture.FileName} to the exact NIST {WsqTestCaseDefinitions.FormatBitRate(bitRate)} WSQ reference image");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode075CoefficientReferenceCases()
    {
        return CreateSingleBitRateRows(
            WsqTestCaseDefinitions.s_lowBitRate,
            static fixture => $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 0.75 bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225CoefficientReferenceCases()
    {
        return CreateSingleBitRateRows(
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 2.25 bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225ActiveExactCoefficientReferenceCases()
    {
        return CreateFileNameRows(
            s_activeExact225CoefficientFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should produce the exact NIST quantized coefficient bins for active 2.25 bpp case {fixture.FileName}");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeCertificationReferenceCases()
    {
        return CreateAllBitRateRows(static (fixture, bitRate) =>
            $"should satisfy the published NIST encoder quantization thresholds for {fixture.FileName} at {WsqTestCaseDefinitions.FormatBitRate(bitRate)} bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionBlockerReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionBlockerFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should match the exact NIST quantized coefficient bins for blocker case {fixture.FileName} at 2.25 bpp via the high-precision encoder analysis path");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionGuardReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionGuardFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should keep the exact NIST 2.25 bpp guard case {fixture.FileName} green while diagnosing the remaining blocker cases");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionProductPrecisionGuardReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionProductPrecisionGuardFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should keep exact 2.25 bpp guard case {fixture.FileName} out of the float-product high-rate path");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionNbisAlignedBlockerReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionNbisAlignedBlockerFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should isolate the NBIS-aligned qbin blocker class for {fixture.FileName} at 2.25 bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionRegionTwoFollowOnReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionRegionTwoFollowOnFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should isolate the region-2 follow-on qbin drift for {fixture.FileName} at 2.25 bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisActiveExactReferenceCases()
    {
        return CreateAllBitRateRows(static (fixture, bitRate) =>
            $"should match the exact local NBIS encoder analysis output for active case {fixture.FileName} at {WsqTestCaseDefinitions.FormatBitRate(bitRate)} bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis225FocusedBlockerReferenceCases()
    {
        return CreateFileNameRows(
            s_nbis225FocusedBlockerFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should isolate the focused 2.25 bpp NBIS blocker case {fixture.FileName}");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisCurrentMismatchReferenceCases()
    {
        return CreateCaseKeyRows(
            s_nbisCurrentMismatchCaseKeys,
            static (fixture, bitRate) => $"should isolate the current NBIS mismatch case {fixture.FileName} at {WsqTestCaseDefinitions.FormatBitRate(bitRate)} bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisActiveExactCodestreamReferenceCases()
    {
        return CreateAllBitRateRows(static (fixture, bitRate) =>
            $"should match the exact local {WsqTestCaseDefinitions.s_nbis500Version} codestream for active case {fixture.FileName} at {WsqTestCaseDefinitions.FormatBitRate(bitRate)} bpp");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis075FocusedMismatchReferenceCases()
    {
        return CreateCaseKeyRows(
            s_nbis075FocusedMismatchCaseKeys,
            static fixture => $"should isolate the focused 0.75 bpp NBIS mismatch case {fixture.FileName}");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis075DqtOnlyMismatchReferenceCases()
    {
        return CreateCaseKeyRows(
            s_nbis075DqtOnlyMismatchCaseKeys,
            static fixture => $"should isolate the focused 0.75 bpp NBIS DQT-only mismatch case {fixture.FileName}");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis225RepresentativeDqtOnlyMismatchReferenceCases()
    {
        return CreateCaseKeyRows(
            s_nbis225RepresentativeDqtOnlyMismatchCaseKeys,
            static fixture => $"should isolate the representative 2.25 bpp NBIS DQT-only mismatch case {fixture.FileName}");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionSerializedBinSensitiveExactReferenceCases()
    {
        return CreateFileNameRows(
            s_highPrecisionSerializedBinSensitiveExactFileNames,
            WsqTestCaseDefinitions.s_highBitRate,
            static fixture => $"should keep exact 2.25 bpp case {fixture.FileName} out of the serialized subband-0 blocker fix bucket");
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeFileSizeAndFrameHeaderReferenceCases()
    {
        return CreateAllBitRateRows(static (fixture, bitRate) =>
            $"should satisfy the published NIST encoder file-size and frame-header checks for {fixture.FileName} at {WsqTestCaseDefinitions.FormatBitRate(bitRate)} bpp");
    }

    public static IEnumerable<TestDataRow<WsqDecodingReferenceCase>> AllDecodeReferenceCases()
    {
        return WsqNistReferenceFixtureCatalog.DecodeFixtures.Select(static testCase => new TestDataRow<WsqDecodingReferenceCase>(
            testCase,
            DisplayName: $"should decode {testCase.FileName} to the exact NBIS reference reconstruction from {testCase.ReferenceSet}"));
    }

    public static IEnumerable<TestDataRow<WsqDecodingReferenceCase>> NonStandardDecodeCases()
    {
        return WsqNistReferenceFixtureCatalog.NonStandardDecodeFixtures.Select(static testCase => new TestDataRow<WsqDecodingReferenceCase>(
            testCase,
            DisplayName: $"should decode {testCase.FileName} to the exact NBIS reference reconstruction from the non-standard tap-set corpus"));
    }

    private static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> CreateAllBitRateRows(
        Func<WsqNistEncodeFixture, double, string> displayNameFactory)
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return CreateRow(fixture, WsqTestCaseDefinitions.s_lowBitRate, displayNameFactory(fixture, WsqTestCaseDefinitions.s_lowBitRate));
            yield return CreateRow(fixture, WsqTestCaseDefinitions.s_highBitRate, displayNameFactory(fixture, WsqTestCaseDefinitions.s_highBitRate));
        }
    }

    private static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> CreateSingleBitRateRows(
        double bitRate,
        Func<WsqNistEncodeFixture, string> displayNameFactory)
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return CreateRow(fixture, bitRate, displayNameFactory(fixture));
        }
    }

    private static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> CreateFileNameRows(
        IReadOnlySet<string> fileNames,
        double bitRate,
        Func<WsqNistEncodeFixture, string> displayNameFactory)
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => fileNames.Contains(fixture.FileName)))
        {
            yield return CreateRow(fixture, bitRate, displayNameFactory(fixture));
        }
    }

    private static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> CreateCaseKeyRows(
        IReadOnlySet<string> caseKeys,
        Func<WsqNistEncodeFixture, double, string> displayNameFactory)
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            foreach (var bitRate in EnumerateSupportedBitRates())
            {
                if (!caseKeys.Contains(WsqTestCaseDefinitions.CreateCaseKey(fixture.FileName, bitRate)))
                {
                    continue;
                }

                yield return CreateRow(fixture, bitRate, displayNameFactory(fixture, bitRate));
            }
        }
    }

    private static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> CreateCaseKeyRows(
        IReadOnlySet<string> caseKeys,
        Func<WsqNistEncodeFixture, string> displayNameFactory)
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            foreach (var bitRate in EnumerateSupportedBitRates())
            {
                if (!caseKeys.Contains(WsqTestCaseDefinitions.CreateCaseKey(fixture.FileName, bitRate)))
                {
                    continue;
                }

                yield return CreateRow(fixture, bitRate, displayNameFactory(fixture));
            }
        }
    }

    private static IEnumerable<double> EnumerateSupportedBitRates()
    {
        yield return WsqTestCaseDefinitions.s_lowBitRate;
        yield return WsqTestCaseDefinitions.s_highBitRate;
    }

    private static TestDataRow<WsqEncodingReferenceCase> CreateRow(
        WsqNistEncodeFixture fixture,
        double bitRate,
        string displayName)
    {
        return new(WsqTestCaseDefinitions.CreateReferenceCase(fixture, bitRate), DisplayName: displayName);
    }
}
