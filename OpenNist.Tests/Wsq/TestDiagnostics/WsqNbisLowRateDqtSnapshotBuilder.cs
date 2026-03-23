namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;

internal static class WsqNbisLowRateDqtSnapshotBuilder
{
    public static async Task<WsqNbisLowRateDqtSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var managedBytes = await EncodeManagedAsync(rawBytes, testCase).ConfigureAwait(false);
        var nbisBytes = await WsqNbisOracleReader.ReadCodestreamAsync(testCase).ConfigureAwait(false);
        var managedContainer = WsqContainerReader.Read(managedBytes);
        var nbisContainer = WsqContainerReader.Read(nbisBytes);
        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase).ConfigureAwait(false);

        var firstByteMismatchIndex = FindFirstByteMismatchIndex(managedBytes, nbisBytes);
        var firstDqtMismatch = FindFirstDqtMismatch(
            managedContainer.QuantizationTable,
            nbisContainer.QuantizationTable);

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var normalizedImage = WsqFloatImageNormalizer.Normalize(rawBytes);
        var decomposedPixels = WsqDecomposition.Decompose(
            normalizedImage.Pixels,
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            waveletTree,
            WsqReferenceTables.CreateStandardTransformTable());
        var nbisWaveletData = await WsqNbisOracleReader.ReadWaveletDataAsync(testCase).ConfigureAwait(false);
        var quantizationArtifacts = WsqQuantizer.CreateQuantizationArtifacts(
            decomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            (float)testCase.BitRate);
        var managedTrace = WsqQuantizer.CreateQuantizationTrace(
            quantizationArtifacts.Variances,
            (float)testCase.BitRate);
        var managedOnNbisWaveletVariances = WsqVarianceCalculator.Compute(
            nbisWaveletData,
            quantizationTree,
            testCase.RawImage.Width);
        var managedOnNbisWaveletTrace = WsqQuantizer.CreateQuantizationTrace(
            managedOnNbisWaveletVariances,
            (float)testCase.BitRate);

        var artifactScaledValue = firstDqtMismatch.FieldKind == WsqDqtFieldKind.QuantizationBin
            ? WsqScaledValueCodec.ScaleToUInt16(quantizationArtifacts.QuantizationBins[firstDqtMismatch.SubbandIndex])
            : WsqScaledValueCodec.ScaleToUInt16(quantizationArtifacts.ZeroBins[firstDqtMismatch.SubbandIndex]);
        var nbisArtifactScaledValue = firstDqtMismatch.FieldKind == WsqDqtFieldKind.QuantizationBin
            ? WsqScaledValueCodec.ScaleToUInt16(nbisAnalysis.QuantizationBins[firstDqtMismatch.SubbandIndex])
            : WsqScaledValueCodec.ScaleToUInt16(nbisAnalysis.ZeroBins[firstDqtMismatch.SubbandIndex]);

        return new(
            testCase.FileName,
            testCase.BitRate,
            firstByteMismatchIndex,
            firstDqtMismatch.SubbandIndex,
            firstDqtMismatch.FieldKind,
            firstDqtMismatch.ManagedValue.RawValue,
            firstDqtMismatch.ManagedValue.Scale,
            firstDqtMismatch.NbisValue.RawValue,
            firstDqtMismatch.NbisValue.Scale,
            artifactScaledValue.RawValue,
            artifactScaledValue.Scale,
            nbisArtifactScaledValue.RawValue,
            nbisArtifactScaledValue.Scale,
            managedTrace.Sigma[firstDqtMismatch.SubbandIndex],
            managedTrace.InitialQuantizationBins[firstDqtMismatch.SubbandIndex],
            managedTrace.QuantizationScale,
            managedOnNbisWaveletTrace.QuantizationBins[firstDqtMismatch.SubbandIndex],
            managedOnNbisWaveletTrace.ZeroBins[firstDqtMismatch.SubbandIndex]);
    }

    private static async Task<byte[]> EncodeManagedAsync(ReadOnlyMemory<byte> rawBytes, WsqEncodingReferenceCase testCase)
    {
        await using var rawStream = new MemoryStream(rawBytes.ToArray(), writable: false);
        await using var wsqStream = new MemoryStream();
        var codec = new WsqCodec();
        await codec.EncodeAsync(rawStream, wsqStream, testCase.RawImage, new(testCase.BitRate)).ConfigureAwait(false);
        return wsqStream.ToArray();
    }

    private static int FindFirstByteMismatchIndex(ReadOnlySpan<byte> managedBytes, ReadOnlySpan<byte> nbisBytes)
    {
        var limit = Math.Min(managedBytes.Length, nbisBytes.Length);
        for (var index = 0; index < limit; index++)
        {
            if (managedBytes[index] != nbisBytes[index])
            {
                return index;
            }
        }

        return managedBytes.Length == nbisBytes.Length ? -1 : limit;
    }

    private static WsqDqtFieldMismatch FindFirstDqtMismatch(
        WsqQuantizationTable managedTable,
        WsqQuantizationTable nbisTable)
    {
        for (var subbandIndex = 0; subbandIndex < managedTable.SerializedQuantizationBins.Count; subbandIndex++)
        {
            var managedQ = managedTable.SerializedQuantizationBins[subbandIndex];
            var nbisQ = nbisTable.SerializedQuantizationBins[subbandIndex];
            if (managedQ.RawValue != nbisQ.RawValue || managedQ.Scale != nbisQ.Scale)
            {
                return new(subbandIndex, WsqDqtFieldKind.QuantizationBin, managedQ, nbisQ);
            }

            var managedZ = managedTable.SerializedZeroBins[subbandIndex];
            var nbisZ = nbisTable.SerializedZeroBins[subbandIndex];
            if (managedZ.RawValue != nbisZ.RawValue || managedZ.Scale != nbisZ.Scale)
            {
                return new(subbandIndex, WsqDqtFieldKind.ZeroBin, managedZ, nbisZ);
            }
        }

        throw new InvalidOperationException("No DQT mismatch was found between the managed and NBIS codestreams.");
    }
}

internal sealed record WsqNbisLowRateDqtSnapshot(
    string FileName,
    double BitRate,
    int FirstByteMismatchIndex,
    int SubbandIndex,
    WsqDqtFieldKind FieldKind,
    ushort ManagedRawValue,
    byte ManagedScale,
    ushort NbisRawValue,
    byte NbisScale,
    ushort ManagedArtifactRawValue,
    byte ManagedArtifactScale,
    ushort NbisArtifactRawValue,
    byte NbisArtifactScale,
    float ManagedSigma,
    float ManagedInitialQuantizationBin,
    float ManagedQuantizationScale,
    float ManagedOnNbisWaveletQuantizationBin,
    float ManagedOnNbisWaveletZeroBin);

internal readonly record struct WsqDqtFieldMismatch(
    int SubbandIndex,
    WsqDqtFieldKind FieldKind,
    WsqScaledUInt16 ManagedValue,
    WsqScaledUInt16 NbisValue);

internal enum WsqDqtFieldKind
{
    QuantizationBin,
    ZeroBin,
}
