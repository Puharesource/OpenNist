namespace OpenNist.Tests.Nist;

using System.Globalization;
using OpenNist.Nist;

[Category("Unit: NIST - Encoder")]
internal sealed class NistEncoderTests
{
    [Test]
    [DisplayName("should encode records, update LEN fields, and round-trip the logical model")]
    public async Task ShouldEncodeRecordsUpdateLenFieldsAndRoundTripTheLogicalModel()
    {
        var file = NistTestData.CreateSampleFile();
        var expectedFirstRecord = NistTestData.BuildRecord(["1.001:0", "1.002:0500", $"1.003:1{NistSeparators.UnitSeparator}0{NistSeparators.RecordSeparator}2{NistSeparators.UnitSeparator}0"]);
        var expectedSecondRecord = NistTestData.BuildRecord(["2.001:0", "2.002:ABC123"]);
        var expectedEncoded = new byte[expectedFirstRecord.Length + expectedSecondRecord.Length];
        expectedFirstRecord.CopyTo(expectedEncoded, 0);
        expectedSecondRecord.CopyTo(expectedEncoded, expectedFirstRecord.Length);

        var encoded = NistEncoder.Encode(file);
        var decoded = NistDecoder.Decode(encoded);

        await Assert.That(encoded.AsSpan().SequenceEqual(expectedEncoded)).IsTrue();
        await Assert.That(decoded.Records.Count).IsEqualTo(2);
        await Assert.That(decoded.Records[0].FindField(1)?.Value).IsEqualTo(expectedFirstRecord.Length.ToString(CultureInfo.InvariantCulture));
        await Assert.That(decoded.Records[1].FindField(1)?.Value).IsEqualTo(expectedSecondRecord.Length.ToString(CultureInfo.InvariantCulture));
        await Assert.That(decoded.Records[0].FindField(2)?.Value).IsEqualTo("0500");
        await Assert.That(decoded.Records[0].FindField(3)?.Subfields[0].Items).IsEquivalentTo(["1", "0"]);
        await Assert.That(decoded.Records[0].FindField(3)?.Subfields[1].Items).IsEquivalentTo(["2", "0"]);
        await Assert.That(decoded.Records[1].FindField(2)?.Value).IsEqualTo("ABC123");
    }

    [Test]
    [DisplayName("should encode a NIST file into a destination stream")]
    public async Task ShouldEncodeANistFileIntoADestinationStream()
    {
        var file = NistTestData.CreateSampleFile();
        using var output = new MemoryStream();

        NistEncoder.Encode(output, file);

        var decoded = NistDecoder.Decode(output.ToArray());

        await Assert.That(output.Length).IsGreaterThan(0);
        await Assert.That(decoded.Records.Count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("should insert a LEN field when creating a record that does not already provide one")]
    public async Task ShouldInsertLenFieldWhenCreatingARecordWithoutOne()
    {
        var file = new NistFile(
            [
                new(
                    10,
                    [
                        new(new(10, 2), "HDR"),
                        new NistField(new(10, 3), "VALUE"),
                    ]),
            ]);

        var decoded = NistDecoder.Decode(NistEncoder.Encode(file));
        var record = decoded.Records[0];
        var encodedLength = NistEncoder.Encode(file).Length.ToString(CultureInfo.InvariantCulture);

        await Assert.That(record.Fields[0].Tag).IsEqualTo(new NistTag(10, 1));
        await Assert.That(record.FindField(1)?.Value).IsEqualTo(encodedLength);
        await Assert.That(record.FindField(2)?.Value).IsEqualTo("HDR");
        await Assert.That(record.FindField(3)?.Value).IsEqualTo("VALUE");
    }

    [Test]
    [DisplayName("should preserve a correct LEN field when re-encoding an existing record")]
    public async Task ShouldPreserveACorrectLenFieldWhenReEncodingAnExistingRecord()
    {
        var expected = NistTestData.BuildRecord(["1.001:0", "1.002:0500", "1.003:2"]);
        var file = NistDecoder.Decode(expected);

        var encoded = NistEncoder.Encode(file);

        await Assert.That(encoded.AsSpan().SequenceEqual(expected)).IsTrue();
    }
}
