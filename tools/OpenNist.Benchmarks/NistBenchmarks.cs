namespace OpenNist.Benchmarks;

using System.IO;
using BenchmarkDotNet.Attributes;
using OpenNist.Nist;

[MemoryDiagnoser]
public class NistBenchmarks
{
    private byte[] _encodedBytes = null!;
    private NistFile _decodedFile = null!;

    [GlobalSetup]
    public void Setup()
    {
        var path = BenchmarkPaths.NistFixture("cognaxon_sample.nist");
        _encodedBytes = File.ReadAllBytes(path);
        _decodedFile = NistDecoder.Decode(_encodedBytes);
    }

    [Benchmark]
    public NistFile DecodeBytes()
    {
        return NistDecoder.Decode(_encodedBytes);
    }

    [Benchmark]
    public NistFile DecodeStream()
    {
        using var stream = new MemoryStream(_encodedBytes, writable: false);
        return NistDecoder.Decode(stream);
    }

    [Benchmark]
    public byte[] EncodeBytes()
    {
        return NistEncoder.Encode(_decodedFile);
    }
}
