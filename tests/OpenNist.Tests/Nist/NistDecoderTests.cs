namespace OpenNist.Tests.Nist;

using System.Text;
using OpenNist.Nist;
using OpenNist.Nist.Codecs;
using OpenNist.Nist.Errors;
using OpenNist.Nist.Model;

[Category("Unit: NIST - Decoder")]
internal sealed class NistDecoderTests
{
    [Test]
    [DisplayName("should decode logical records, fields, subfields, and items from bytes")]
    public async Task ShouldDecodeLogicalRecordsFieldsSubfieldsAndItemsFromBytes()
    {
        var contentField = NistTestData.CreateTransactionContentField(["1", "0"], ["2", "0"]);
        var bytes = NistTestData.BuildFile(
            [
                [$"1.001:0", "1.002:0500", $"{contentField.Tag}:{contentField.Value}"],
                ["2.001:0", "2.002:ABC123"],
            ]);

        var file = NistDecoder.Decode(bytes);

        await Assert.That(file.Records.Count).IsEqualTo(2);
        await Assert.That(file.Records[0].Type).IsEqualTo(1);
        await Assert.That(file.Records[1].Type).IsEqualTo(2);
        await Assert.That(file.Records[0].FindField(2)?.Value).IsEqualTo("0500");
        await Assert.That(file.Records[0].FindField(3)?.Subfields.Count).IsEqualTo(2);
        await Assert.That(file.Records[0].FindField(3)?.Subfields[0].Items).IsEquivalentTo(["1", "0"]);
        await Assert.That(file.Records[0].FindField(3)?.Subfields[1].Items).IsEquivalentTo(["2", "0"]);
        await Assert.That(file.Records[1].FindField(2)?.Value).IsEqualTo("ABC123");
    }

    [Test]
    [DisplayName("should decode NIST data from a stream")]
    public async Task ShouldDecodeNistDataFromAStream()
    {
        var bytes = NistTestData.BuildFile(
            [
                ["1.001:0", "1.002:0500"],
                ["2.001:0", "2.002:ABC123"],
            ]);

        using var stream = new MemoryStream(bytes, writable: false);
        var file = NistDecoder.Decode(stream);

        await Assert.That(file.Records.Count).IsEqualTo(2);
        await Assert.That(file.Records[0].FindField(2)?.Value).IsEqualTo("0500");
        await Assert.That(file.Records[1].FindField(2)?.Value).IsEqualTo("ABC123");
    }

    [Test]
    [DisplayName("should reject a logical record whose LEN exceeds remaining bytes")]
    public async Task ShouldRejectALogicalRecordWhoseLenExceedsRemainingBytes()
    {
        var bytes = Encoding.Latin1.GetBytes($"1.001:999{NistSeparators.GroupSeparator}1.002:0500{NistSeparators.FileSeparator}");

        var result = NistDecoder.TryDecode(bytes);
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Code).IsEqualTo(NistErrorCodes.RecordLengthExceedsRemainingBytes);

        var act = () => NistDecoder.Decode(bytes);
        var exception = await Assert.ThrowsAsync<NistException>(async () => await Task.Run(act));
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(NistErrorCodes.RecordLengthExceedsRemainingBytes);
    }

    [Test]
    [DisplayName("should reject a logical record without a trailing file separator")]
    public async Task ShouldRejectALogicalRecordWithoutATrailingFileSeparator()
    {
        var validRecord = NistTestData.BuildRecord(["1.001:0", "1.002:0500"]);
        var invalidRecord = validRecord[..^1];

        var result = NistDecoder.TryDecode(invalidRecord);
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Code).IsEqualTo(NistErrorCodes.RecordLengthExceedsRemainingBytes);

        var act = () => NistDecoder.Decode(invalidRecord);
        var exception = await Assert.ThrowsAsync<NistException>(async () => await Task.Run(act));
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(NistErrorCodes.RecordLengthExceedsRemainingBytes);
    }

    [Test]
    [DisplayName("should reject a record that does not start with a LEN field")]
    public async Task ShouldRejectARecordThatDoesNotStartWithALenField()
    {
        var bytes = Encoding.Latin1.GetBytes($"1.002:0500{NistSeparators.FileSeparator}");

        var result = NistDecoder.TryDecode(bytes);
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Code).IsEqualTo(NistErrorCodes.MissingLenField);

        var act = () => NistDecoder.Decode(bytes);
        var exception = await Assert.ThrowsAsync<NistException>(async () => await Task.Run(act));
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(NistErrorCodes.MissingLenField);
    }
}
