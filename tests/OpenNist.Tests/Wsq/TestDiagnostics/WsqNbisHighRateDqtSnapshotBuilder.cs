namespace OpenNist.Tests.Wsq.TestDiagnostics;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;
using OpenNist.Wsq.Codecs;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;
using OpenNist.Wsq.Internal.Scaling;

internal static class WsqNbisHighRateDqtSnapshotBuilder
{
    public static async Task<WsqNbisHighRateDqtSnapshot> CreateAsync(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath).ConfigureAwait(false);
        var managedBytes = await EncodeManagedAsync(rawBytes, testCase).ConfigureAwait(false);
        var nbisBytes = await WsqNbisOracleReader.ReadCodestreamAsync(testCase).ConfigureAwait(false);
        var managedContainer = WsqContainerReader.Read(managedBytes);
        var nbisContainer = WsqContainerReader.Read(nbisBytes);
        var nbisAnalysis = await WsqNbisOracleReader.ReadAnalysisAsync(testCase).ConfigureAwait(false);

        WsqWaveletTreeBuilder.Build(
            testCase.RawImage.Width,
            testCase.RawImage.Height,
            out var waveletTree,
            out var quantizationTree);

        var managedQuantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(
            managedContainer,
            waveletTree,
            quantizationTree);
        var nbisQuantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(
            nbisContainer,
            waveletTree,
            quantizationTree);

        var transformTable = WsqReferenceTables.CreateStandardTransformTable();
        var artifacts = WsqEncoderAnalysisPipeline.AnalyzeHighPrecisionArtifacts(
            rawBytes,
            testCase.RawImage,
            transformTable,
            waveletTree);
        var currentArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            artifacts.DoubleDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);
        var floatArtifacts = WsqHighPrecisionQuantizer.CreateQuantizationArtifacts(
            artifacts.FloatDecomposedPixels,
            quantizationTree,
            testCase.RawImage.Width,
            testCase.BitRate);

        var firstByteMismatchIndex = FindFirstByteMismatchIndex(managedBytes, nbisBytes);
        var firstDqtMismatch = FindFirstDqtMismatch(
            managedContainer.QuantizationTable,
            nbisContainer.QuantizationTable);
        var firstCoefficientMismatchIndex = FindFirstShortMismatchIndex(
            managedQuantizedCoefficients,
            nbisQuantizedCoefficients);

        var currentRawScaledValue = firstDqtMismatch.FieldKind == WsqDqtFieldKind.QuantizationBin
            ? WsqScaledValueCodec.ScaleToUInt16((float)currentArtifacts.QuantizationBins[firstDqtMismatch.SubbandIndex])
            : WsqScaledValueCodec.ScaleToUInt16((float)currentArtifacts.ZeroBins[firstDqtMismatch.SubbandIndex]);
        var floatRawScaledValue = firstDqtMismatch.FieldKind == WsqDqtFieldKind.QuantizationBin
            ? WsqScaledValueCodec.ScaleToUInt16((float)floatArtifacts.QuantizationBins[firstDqtMismatch.SubbandIndex])
            : WsqScaledValueCodec.ScaleToUInt16((float)floatArtifacts.ZeroBins[firstDqtMismatch.SubbandIndex]);
        var nbisRawScaledValue = firstDqtMismatch.FieldKind == WsqDqtFieldKind.QuantizationBin
            ? WsqScaledValueCodec.ScaleToUInt16((float)nbisAnalysis.QuantizationBins[firstDqtMismatch.SubbandIndex])
            : WsqScaledValueCodec.ScaleToUInt16((float)nbisAnalysis.ZeroBins[firstDqtMismatch.SubbandIndex]);

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
            currentRawScaledValue.RawValue,
            currentRawScaledValue.Scale,
            floatRawScaledValue.RawValue,
            floatRawScaledValue.Scale,
            nbisRawScaledValue.RawValue,
            nbisRawScaledValue.Scale,
            firstCoefficientMismatchIndex,
            firstCoefficientMismatchIndex < 0);
    }

    private static async Task<byte[]> EncodeManagedAsync(ReadOnlyMemory<byte> rawBytes, WsqEncodingReferenceCase testCase)
    {
        await using var rawStream = new MemoryStream(rawBytes.ToArray(), writable: false);
        await using var wsqStream = new MemoryStream();
        var codec = new WsqCodec();
        await codec.EncodeAsync(rawStream, wsqStream, testCase.RawImage, new(testCase.BitRate)).ConfigureAwait(false);
        return wsqStream.ToArray();
    }

    private static int FindFirstByteMismatchIndex(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var limit = Math.Min(left.Length, right.Length);
        for (var index = 0; index < limit; index++)
        {
            if (left[index] != right[index])
            {
                return index;
            }
        }

        return left.Length == right.Length ? -1 : limit;
    }

    private static int FindFirstShortMismatchIndex(ReadOnlySpan<short> left, ReadOnlySpan<short> right)
    {
        var limit = Math.Min(left.Length, right.Length);
        for (var index = 0; index < limit; index++)
        {
            if (left[index] != right[index])
            {
                return index;
            }
        }

        return left.Length == right.Length ? -1 : limit;
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

internal sealed record WsqNbisHighRateDqtSnapshot(
    string FileName,
    double BitRate,
    int FirstByteMismatchIndex,
    int SubbandIndex,
    WsqDqtFieldKind FieldKind,
    ushort ManagedRawValue,
    byte ManagedScale,
    ushort NbisRawValue,
    byte NbisScale,
    ushort CurrentArtifactRawValue,
    byte CurrentArtifactScale,
    ushort FloatArtifactRawValue,
    byte FloatArtifactScale,
    ushort NbisArtifactRawValue,
    byte NbisArtifactScale,
    int FirstCoefficientMismatchIndex,
    bool CoefficientsMatchExactly);
