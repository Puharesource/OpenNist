namespace OpenNist.Benchmarks.Benchmarks;

using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using OpenNist.Benchmarks.Fixtures;
using OpenNist.Wsq.Codecs;
using OpenNist.Wsq.Internal.Container;
using OpenNist.Wsq.Internal.Decoding;
using OpenNist.Wsq.Internal.Encoding;
using OpenNist.Wsq.Model;

[MemoryDiagnoser]
public class WsqBenchmarks
{
    private readonly WsqCodec _codec = new();
    private byte[] _rawBytes = null!;
    private byte[] _wsqBytes = null!;
    private WsqRawImageDescription _rawImage;
    private WsqContainer _container = null!;
    private WsqWaveletNode[] _waveletTree = null!;
    private WsqQuantizationNode[] _quantizationTree = null!;
    private WsqEncoderAnalysisResult _analysis = null!;
    private short[] _quantizedCoefficients = null!;
    private float[] _floatingPointPixels = null!;
    private WsqDualNormalizedImage _dualNormalizedImage;
    private float[] _highPrecisionFloatDecomposedPixels = null!;
    private WsqQuantizationArtifacts _highPrecisionQuantizationArtifacts = null!;

    [GlobalSetup]
    public void Setup()
    {
        const string sampleFileName = "a001";
        _wsqBytes = File.ReadAllBytes(BenchmarkPaths.WsqReferenceFixture("BitRate225", $"{sampleFileName}.wsq"));
        _rawBytes = File.ReadAllBytes(BenchmarkPaths.WsqRawFixture($"{sampleFileName}.raw"));
        _rawImage = LoadRawImageDescription($"{sampleFileName}.raw");
        _container = WsqContainerReader.Read(_wsqBytes);
        WsqWaveletTreeBuilder.Build(_container.FrameHeader.Width, _container.FrameHeader.Height, out _waveletTree, out _quantizationTree);
        _quantizedCoefficients = WsqHuffmanDecoder.DecodeQuantizedCoefficients(_container, _waveletTree, _quantizationTree);
        _floatingPointPixels = WsqQuantizationDecoder.Unquantize(
            _container.QuantizationTable,
            _quantizationTree,
            _quantizedCoefficients,
            _container.FrameHeader.Width,
            _container.FrameHeader.Height);
        _dualNormalizedImage = WsqDualImageNormalizer.Normalize(_rawBytes);
        _highPrecisionFloatDecomposedPixels = WsqDecomposition.Decompose(
            (float[])_dualNormalizedImage.FloatImage.Pixels.Clone(),
            _rawImage.Width,
            _rawImage.Height,
            _waveletTree,
            WsqReferenceTables.StandardTransformTable);
        _highPrecisionQuantizationArtifacts = WsqQuantizer.CreateQuantizationArtifacts(
            _highPrecisionFloatDecomposedPixels,
            _quantizationTree,
            _rawImage.Width,
            2.25f);
        _analysis = WsqEncoderAnalysisPipeline.Analyze(_rawBytes, _rawImage, new(2.25));
    }

    [Benchmark]
    public WsqFileInfo Inspect()
    {
        using var stream = new MemoryStream(_wsqBytes, writable: false);
        return _codec.InspectAsync(stream).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public WsqRawImageDescription Decode()
    {
        using var wsqStream = new MemoryStream(_wsqBytes, writable: false);
        using var rawStream = new MemoryStream();
        return _codec.DecodeAsync(wsqStream, rawStream).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public int ReadContainer()
    {
        return WsqContainerReader.Read(_wsqBytes).Blocks.Count;
    }

    [Benchmark]
    public int DecodeQuantizedCoefficients()
    {
        return WsqHuffmanDecoder.DecodeQuantizedCoefficients(_container, _waveletTree, _quantizationTree).Length;
    }

    [Benchmark]
    public int DecodeUnquantize()
    {
        return WsqQuantizationDecoder.Unquantize(
            _container.QuantizationTable,
            _quantizationTree,
            _quantizedCoefficients,
            _container.FrameHeader.Width,
            _container.FrameHeader.Height).Length;
    }

    [Benchmark]
    public int DecodeReconstruct()
    {
        return WsqReconstruction.ReconstructToRawPixels(
            (float[])_floatingPointPixels.Clone(),
            _container.FrameHeader.Width,
            _container.FrameHeader.Height,
            _waveletTree,
            _container.TransformTable,
            (float)_container.FrameHeader.Shift,
            (float)_container.FrameHeader.Scale).Length;
    }

    [Benchmark]
    public int DecodeInverseWavelet()
    {
        return WsqReconstruction.ReconstructToFloatingPointPixels(
            (float[])_floatingPointPixels.Clone(),
            _container.FrameHeader.Width,
            _container.FrameHeader.Height,
            _waveletTree,
            _container.TransformTable).Length;
    }

    [Benchmark]
    public int EncodeAnalyze()
    {
        return WsqEncoderAnalysisPipeline.Analyze(_rawBytes, _rawImage, new(2.25)).QuantizedCoefficients.Length;
    }

    [Benchmark]
    public int EncodeNormalizeDual()
    {
        return WsqDualImageNormalizer.Normalize(_rawBytes).FloatImage.Pixels.Length;
    }

    [Benchmark]
    public int EncodeHighPrecisionDecomposeFloat()
    {
        return WsqDecomposition.Decompose(
            (float[])_dualNormalizedImage.FloatImage.Pixels.Clone(),
            _rawImage.Width,
            _rawImage.Height,
            _waveletTree,
            WsqReferenceTables.StandardTransformTable).Length;
    }

    [Benchmark]
    public int EncodeHighPrecisionVariance()
    {
        return WsqVarianceCalculator.Compute(
            _highPrecisionFloatDecomposedPixels,
            _quantizationTree,
            _rawImage.Width).Length;
    }

    [Benchmark]
    public int EncodeHighPrecisionCreateQuantizationArtifacts()
    {
        return WsqQuantizer.CreateQuantizationArtifacts(
            _highPrecisionFloatDecomposedPixels,
            _quantizationTree,
            _rawImage.Width,
            2.25f).QuantizationBins.Length;
    }

    [Benchmark]
    public int EncodeHighPrecisionQuantizeCoefficients()
    {
        return WsqCoefficientQuantizer.Quantize(
            _highPrecisionFloatDecomposedPixels,
            _quantizationTree,
            _rawImage.Width,
            _highPrecisionQuantizationArtifacts.QuantizationBins,
            _highPrecisionQuantizationArtifacts.ZeroBins).Length;
    }

    [Benchmark]
    public int EncodeBuildContainer()
    {
        return WsqEncoderContainerBuilder.Build(_analysis, _rawImage, new(2.25)).Blocks.Count;
    }

    [Benchmark]
    public int EncodeWriteContainer()
    {
        using var wsqStream = new MemoryStream();
        WsqContainerWriter.WriteAsync(wsqStream, WsqEncoderContainerBuilder.Build(_analysis, _rawImage, new(2.25)))
            .AsTask()
            .GetAwaiter()
            .GetResult();
        return checked((int)wsqStream.Length);
    }

    [Benchmark]
    public int Encode()
    {
        using var rawStream = new MemoryStream(_rawBytes, writable: false);
        using var wsqStream = new MemoryStream();
        _codec.EncodeAsync(rawStream, wsqStream, _rawImage, new(2.25)).AsTask().GetAwaiter().GetResult();
        return checked((int)wsqStream.Length);
    }

    private static WsqRawImageDescription LoadRawImageDescription(string fileName)
    {
        using var stream = File.OpenRead(BenchmarkPaths.WsqRawDimensionsMetadata());
        using var document = JsonDocument.Parse(stream);

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!string.Equals(item.GetProperty("fileName").GetString(), fileName, StringComparison.Ordinal))
            {
                continue;
            }

            return new(
                item.GetProperty("width").GetInt32(),
                item.GetProperty("height").GetInt32());
        }

        throw new InvalidOperationException($"WSQ raw fixture metadata for '{fileName}' was not found.");
    }
}
