namespace OpenNist.Wasm.Contracts;

using System.Globalization;
using OpenNist.Nist.Model;

internal sealed record OpenNistNistFieldResult(
    string Tag,
    string Value,
    int SubfieldCount,
    int ItemCount);

internal sealed record OpenNistNistRecordResult(
    int Type,
    int FieldCount,
    int? LogicalRecordLength,
    int ByteOffset,
    int EncodedByteCount,
    bool IsOpaqueBinaryRecord,
    List<OpenNistNistFieldResult> Fields);

internal sealed record OpenNistNistFileResult(
    int RecordCount,
    string? Version,
    string? ContentSummary,
    List<OpenNistNistRecordResult> Records)
{
    public static OpenNistNistFileResult FromFile(NistFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var typeOneRecords = file.FindRecords(1);
        var typeOneRecord = typeOneRecords.Count > 0 ? typeOneRecords[0] : null;
        var records = new List<OpenNistNistRecordResult>(file.Records.Count);
        var byteOffset = 0;

        foreach (var record in file.Records)
        {
            var logicalRecordLength = TryParseLength(record.FindField(1)?.Value);
            var encodedByteCount = record.IsOpaqueBinaryRecord
                ? record.EncodedBytes.Length
                : logicalRecordLength ?? 0;

            records.Add(
                new(
                    record.Type,
                    record.Fields.Count,
                    logicalRecordLength,
                    byteOffset,
                    encodedByteCount,
                    record.IsOpaqueBinaryRecord,
                    record.Fields
                        .Select(
                            static field => new OpenNistNistFieldResult(
                                field.Tag.ToString(),
                                field.Value,
                                field.Subfields.Count,
                                field.Subfields.Sum(static subfield => subfield.Items.Count)))
                        .ToList()));

            byteOffset += encodedByteCount;
        }

        return new(
            file.Records.Count,
            typeOneRecord?.FindField(2)?.Value,
            typeOneRecord?.FindField(3)?.Value,
            records);
    }

    private static int? TryParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}

internal sealed record OpenNistNistFieldInput(string Tag, string Value);

internal sealed record OpenNistNistRecordInput(int Type, List<OpenNistNistFieldInput> Fields);

internal sealed record OpenNistNistFileInput(List<OpenNistNistRecordInput> Records)
{
    public NistFile ToFile()
    {
        return new(
            Records.Select(
                static record => new NistRecord(
                    record.Type,
                    record.Fields.Select(static field => new NistField(NistTag.Parse(field.Tag), field.Value)))));
    }
}
