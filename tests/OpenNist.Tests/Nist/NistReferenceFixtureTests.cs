namespace OpenNist.Tests.Nist;

using OpenNist.Nist;
using OpenNist.Nist.Codecs;
using OpenNist.Nist.Model;
using OpenNist.Tests.Nist.TestDataSources;

[Category("Fixture: NIST - Reference Files")]
internal sealed class NistReferenceFixtureTests
{
    [Test]
    [MethodDataSource(typeof(NistReferenceDataSources), nameof(NistReferenceDataSources.ReferenceFiles))]
    public async Task ShouldDecodeOfficialNistReferenceFixtures(string filePath)
    {
        await using var stream = File.OpenRead(filePath);

        var file = NistDecoder.Decode(stream);

        await Assert.That(file.Records.Count).IsGreaterThan(0);

        foreach (var record in file.Records)
        {
            if (record.IsOpaqueBinaryRecord)
            {
                await Assert.That(record.EncodedBytes.Length).IsGreaterThan(0);
                continue;
            }

            await Assert.That(record.Fields.Count).IsGreaterThan(0);
            await Assert.That(record.Fields[0].Tag).IsEqualTo(new NistTag(record.Type, 1));
            await Assert.That(record.FindField(1)).IsNotNull();
        }
    }

    [Test]
    [MethodDataSource(typeof(NistReferenceDataSources), nameof(NistReferenceDataSources.ReferenceFiles))]
    public async Task ShouldRoundTripOfficialNistReferenceFixturesExactly(string filePath)
    {
        var originalBytes = await File.ReadAllBytesAsync(filePath);
        var file = NistDecoder.Decode(originalBytes);

        using var encoded = new MemoryStream();
        NistEncoder.Encode(encoded, file);

        await Assert.That(encoded.ToArray().AsSpan().SequenceEqual(originalBytes)).IsTrue();
    }
}
