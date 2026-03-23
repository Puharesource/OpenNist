using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;

var cases = new (string File, double Rate)[] { ("a001.raw", 2.25), ("a076.raw", 2.25), ("cmp00008.raw", 2.25) };
var fixtures = WsqNistReferenceFixtureCatalog.EncodeFixtures.ToDictionary(f => f.FileName, StringComparer.Ordinal);
foreach (var c in cases)
{
    var f = fixtures[c.File];
    var tc = new WsqEncodingReferenceCase(f.FileName, c.Rate, f.RawImage, f.RawPath, c.Rate == 0.75 ? f.ReferenceBitRate075Path : f.ReferenceBitRate225Path);
    var raw = await File.ReadAllBytesAsync(tc.RawPath);
    await using var rawStream = new MemoryStream(raw, writable: false);
    await using var wsqStream = new MemoryStream();
    var codec = new WsqCodec();
    await codec.EncodeAsync(rawStream, wsqStream, tc.RawImage, new(c.Rate));
    var managed = wsqStream.ToArray();
    var nbis = await WsqNbisOracleReader.ReadCodestreamAsync(tc);
    var managedContainer = WsqContainerReader.Read(managed);
    var nbisContainer = WsqContainerReader.Read(nbis);
    var coeffExact = managedContainer.QuantizedCoefficients.SequenceEqual(nbisContainer.QuantizedCoefficients);
    var firstCoeffDiff = -1;
    for (var i = 0; i < Math.Min(managedContainer.QuantizedCoefficients.Length, nbisContainer.QuantizedCoefficients.Length); i++)
    {
        if ((managedContainer.QuantizedCoefficients[i] == nbisContainer.QuantizedCoefficients[i]) == false)
        {
            firstCoeffDiff = i;
            break;
        }
    }

    var qExact = managedContainer.QuantizationTable.SerializedQuantizationBins.SequenceEqual(nbisContainer.QuantizationTable.SerializedQuantizationBins);
    var zExact = managedContainer.QuantizationTable.SerializedZeroBins.SequenceEqual(nbisContainer.QuantizationTable.SerializedZeroBins);
    Console.WriteLine($"{c.File}@{c.Rate}: coeffExact={coeffExact} firstCoeffDiff={firstCoeffDiff} qExact={qExact} zExact={zExact} managedLen={managed.Length} nbisLen={nbis.Length}");
}
