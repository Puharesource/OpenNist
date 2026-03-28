namespace OpenNist.Tests.Nist;

using System.Globalization;
using System.Text;
using OpenNist.Nist;
using OpenNist.Nist.Model;

internal static class NistTestData
{
    public static NistField CreateTransactionContentField(params string[][] descriptors)
    {
        if (descriptors.Length == 0)
        {
            throw new ArgumentException("At least one transaction content descriptor is required.", nameof(descriptors));
        }

        return NistField.Create(new(1, 3), descriptors);
    }

    public static byte[] BuildFile(IReadOnlyList<IReadOnlyList<string>> records)
    {
        using var output = new MemoryStream();

        foreach (var fields in records)
        {
            var recordBytes = BuildRecord(fields);
            output.Write(recordBytes, 0, recordBytes.Length);
        }

        return output.ToArray();
    }

    public static byte[] BuildRecord(IReadOnlyList<string> fields)
    {
        var normalizedFields = fields.ToArray();
        var recordLength = "0";

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var encoded = Encoding.Latin1.GetBytes(
                string.Join(
                    NistSeparators.GroupSeparator,
                    normalizedFields.Select((field, index) => index == 0 ? ReplaceFieldValue(field, recordLength) : field)) +
                NistSeparators.FileSeparator);

            var nextLength = encoded.Length.ToString(CultureInfo.InvariantCulture);
            if (nextLength == recordLength)
            {
                return encoded;
            }

            recordLength = nextLength;
        }

        return Encoding.Latin1.GetBytes(
            string.Join(
                NistSeparators.GroupSeparator,
                normalizedFields.Select((field, index) => index == 0 ? ReplaceFieldValue(field, recordLength) : field)) +
            NistSeparators.FileSeparator);
    }

    public static NistFile CreateSampleFile()
    {
        return new(
            [
                new(
                    1,
                    [
                        new(new(1, 1), "999"),
                        new(new(1, 2), "0500"),
                        CreateTransactionContentField(["1", "0"], ["2", "0"]),
                    ]),
                new(
                    2,
                    [
                        new NistField(new(2, 2), "ABC123"),
                    ]),
            ]);
    }

    private static string ReplaceFieldValue(string field, string value)
    {
        var separatorIndex = field.IndexOf(':', StringComparison.Ordinal);
        return $"{field[..(separatorIndex + 1)]}{value}";
    }
}
