namespace OpenNist.Tests.Wsq.TestDataSources;

using OpenNist.Tests.Wsq.TestFixtures;

internal static class WsqNistReferenceDataSources
{
    public static IEnumerable<TestDataRow<WsqNistEncodeFixture>> EncodeFixtures()
    {
        return WsqNistReferenceFixtureCatalog.EncodeFixtures.Select(static fixture => new TestDataRow<WsqNistEncodeFixture>(
            fixture,
            DisplayName: $"should map {fixture.FileName} to both official NIST reference WSQ files"));
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> AllEncodeReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should encode {fixture.FileName} to the exact NIST 0.75 WSQ reference image");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should encode {fixture.FileName} to the exact NIST 2.25 WSQ reference image");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode075CoefficientReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 0.75 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225CoefficientReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> Encode225ActiveExactCoefficientReferenceCases()
    {
        var activeExactFileNames = new HashSet<string>(StringComparer.Ordinal)
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

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => activeExactFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should produce the exact NIST quantized coefficient bins for active 2.25 bpp case {fixture.FileName}");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeCertificationReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should satisfy the published NIST encoder quantization thresholds for {fixture.FileName} at 0.75 bpp");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should satisfy the published NIST encoder quantization thresholds for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionBlockerReferenceCases()
    {
        var blockerFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "a070.raw",
            "cmp00003.raw",
            "cmp00005.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => blockerFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should match the exact NIST quantized coefficient bins for blocker case {fixture.FileName} at 2.25 bpp via the high-precision encoder analysis path");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionGuardReferenceCases()
    {
        var guardFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "cmp00017.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => guardFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should keep the exact NIST 2.25 bpp guard case {fixture.FileName} green while diagnosing the remaining blocker cases");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionProductPrecisionGuardReferenceCases()
    {
        var guardFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "a076.raw",
            "cmp00017.raw",
            "sample_19.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => guardFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should keep exact 2.25 bpp guard case {fixture.FileName} out of the float-product high-rate path");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionNbisAlignedBlockerReferenceCases()
    {
        var blockerFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "a070.raw",
            "cmp00005.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => blockerFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should isolate the NBIS-aligned qbin blocker class for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionRegionTwoFollowOnReferenceCases()
    {
        var followOnFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "cmp00005.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => followOnFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should isolate the region-2 follow-on qbin drift for {fixture.FileName} at 2.25 bpp");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisActiveExactReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            foreach (var referenceCase in new[]
            {
                new { BitRate = 0.75, ReferencePath = fixture.ReferenceBitRate075Path },
                new { BitRate = 2.25, ReferencePath = fixture.ReferenceBitRate225Path },
            })
            {
                yield return new(
                    new(
                        fixture.FileName,
                        referenceCase.BitRate,
                        fixture.RawImage,
                        fixture.RawPath,
                        referenceCase.ReferencePath),
                    DisplayName: $"should match the exact local NBIS encoder analysis output for active case {fixture.FileName} at {referenceCase.BitRate:0.##} bpp");
            }
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis225FocusedBlockerReferenceCases()
    {
        var focusedBlockerFileNames = new HashSet<string>(StringComparer.Ordinal)
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

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => focusedBlockerFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should isolate the focused 2.25 bpp NBIS blocker case {fixture.FileName}");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisCurrentMismatchReferenceCases()
    {
        var currentMismatchCases = new HashSet<string>(StringComparer.Ordinal)
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

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            foreach (var referenceCase in new[]
            {
                new { BitRate = 0.75, ReferencePath = fixture.ReferenceBitRate075Path },
                new { BitRate = 2.25, ReferencePath = fixture.ReferenceBitRate225Path },
            })
            {
                var caseKey = $"{fixture.FileName}|{referenceCase.BitRate:0.##}";
                if (!currentMismatchCases.Contains(caseKey))
                {
                    continue;
                }

                yield return new(
                    new(
                        fixture.FileName,
                        referenceCase.BitRate,
                        fixture.RawImage,
                        fixture.RawPath,
                        referenceCase.ReferencePath),
                    DisplayName: $"should isolate the current NBIS mismatch case {fixture.FileName} at {referenceCase.BitRate:0.##} bpp");
            }
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbisActiveExactCodestreamReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            foreach (var referenceCase in new[]
            {
                new { BitRate = 0.75, ReferencePath = fixture.ReferenceBitRate075Path },
                new { BitRate = 2.25, ReferencePath = fixture.ReferenceBitRate225Path },
            })
            {
                yield return new(
                    new(
                        fixture.FileName,
                        referenceCase.BitRate,
                        fixture.RawImage,
                        fixture.RawPath,
                        referenceCase.ReferencePath),
                    DisplayName: $"should match the exact local NBIS 5.0.0 codestream for active case {fixture.FileName} at {referenceCase.BitRate:0.##} bpp");
            }
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis075FocusedMismatchReferenceCases()
    {
        var focusedMismatchCases = new HashSet<string>(StringComparer.Ordinal)
        {
            "cmp00005.raw|0.75",
            "cmp00011.raw|0.75",
            "sample_19.raw|0.75",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            var caseKey = $"{fixture.FileName}|0.75";
            if (!focusedMismatchCases.Contains(caseKey))
            {
                continue;
            }

            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should isolate the focused 0.75 bpp NBIS mismatch case {fixture.FileName}");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis075DqtOnlyMismatchReferenceCases()
    {
        var focusedMismatchCases = new HashSet<string>(StringComparer.Ordinal)
        {
            "a001.raw|0.75",
            "a018.raw|0.75",
            "a107.raw|0.75",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            var caseKey = $"{fixture.FileName}|0.75";
            if (!focusedMismatchCases.Contains(caseKey))
            {
                continue;
            }

            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should isolate the focused 0.75 bpp NBIS DQT-only mismatch case {fixture.FileName}");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeNbis225RepresentativeDqtOnlyMismatchReferenceCases()
    {
        var focusedMismatchCases = new HashSet<string>(StringComparer.Ordinal)
        {
            "a001.raw|2.25",
            "a076.raw|2.25",
            "cmp00008.raw|2.25",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            var caseKey = $"{fixture.FileName}|2.25";
            if (!focusedMismatchCases.Contains(caseKey))
            {
                continue;
            }

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should isolate the representative 2.25 bpp NBIS DQT-only mismatch case {fixture.FileName}");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeHighPrecisionSerializedBinSensitiveExactReferenceCases()
    {
        var exactSensitiveFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "cmp00014.raw",
            "sample_01.raw",
            "sample_19.raw",
        };

        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures.Where(fixture => exactSensitiveFileNames.Contains(fixture.FileName)))
        {
            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should keep exact 2.25 bpp case {fixture.FileName} out of the serialized subband-0 blocker fix bucket");
        }
    }

    public static IEnumerable<TestDataRow<WsqEncodingReferenceCase>> EncodeFileSizeAndFrameHeaderReferenceCases()
    {
        foreach (var fixture in WsqNistReferenceFixtureCatalog.EncodeFixtures)
        {
            yield return new(
                new(
                    fixture.FileName,
                    0.75,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate075Path),
                DisplayName: $"should satisfy the published NIST encoder file-size and frame-header checks for {fixture.FileName} at 0.75 bpp");

            yield return new(
                new(
                    fixture.FileName,
                    2.25,
                    fixture.RawImage,
                    fixture.RawPath,
                    fixture.ReferenceBitRate225Path),
                DisplayName: $"should satisfy the published NIST encoder file-size and frame-header checks for {fixture.FileName} at 2.25 bpp");
        }
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
}
