namespace OpenNist.Nfiq.Internal.Csv;

using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Model;

internal static class Nfiq2CsvReportParser
{
    internal sealed record ParsedCsv(
        string Csv,
        FrozenSet<string> HeaderSet,
        IReadOnlyList<string> Columns,
        IReadOnlyList<FrozenDictionary<string, string>> Rows);

    public static Nfiq2CsvReport Parse(string csv)
    {
        var parsed = ParseCore(csv);
        var results = parsed.Rows
            .Select(static row => ToAssessmentResult(row))
            .ToArray();

        return new(parsed.Csv, parsed.Columns, results);
    }

    public static ParsedCsv ParseCore(string csv)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csv);

        var lines = csv
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length < 2)
        {
            throw new Nfiq2Exception("The official NFIQ 2 CLI did not return a valid CSV payload.");
        }

        var columns = ParseLine(lines[0]).ToArray();
        if (columns.Length == 0)
        {
            throw new Nfiq2Exception("The official NFIQ 2 CSV header was empty.");
        }

        var rows = new FrozenDictionary<string, string>[lines.Length - 1];
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = ParseLine(lines[index]);
            if (fields.Count != columns.Length)
            {
                throw new Nfiq2Exception(
                    $"NFIQ 2 CSV row {index} has {fields.Count} fields, but the header has {columns.Length}.");
            }

            rows[index - 1] = columns
                .Select((column, fieldIndex) => KeyValuePair.Create(column, fields[fieldIndex]))
                .ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        }

        return new(
            csv,
            columns.ToFrozenSet(StringComparer.Ordinal),
            columns,
            rows);
    }

    private static Nfiq2AssessmentResult ToAssessmentResult(IReadOnlyDictionary<string, string> row)
    {
        var actionableFeedback = row
            .Where(static pair => Nfiq2ColumnDefinitions.ActionableColumns.Contains(pair.Key))
            .ToFrozenDictionary(
                static pair => pair.Key,
                static pair => ParseNullableDouble(pair.Value),
                StringComparer.Ordinal);

        var nativeQualityMeasures = row
            .Where(static pair =>
                !Nfiq2ColumnDefinitions.FixedColumns.Contains(pair.Key)
                && !Nfiq2ColumnDefinitions.ActionableColumns.Contains(pair.Key)
                && !pair.Key.StartsWith(Nfiq2ColumnDefinitions.MappedPrefix, StringComparison.Ordinal))
            .ToFrozenDictionary(
                static pair => pair.Key,
                static pair => ParseNullableDouble(pair.Value),
                StringComparer.Ordinal);

        var mappedQualityMeasures = row
            .Where(static pair => pair.Key.StartsWith(Nfiq2ColumnDefinitions.MappedPrefix, StringComparison.Ordinal))
            .ToFrozenDictionary(
                static pair => pair.Key,
                static pair => ParseNullableDouble(pair.Value),
                StringComparer.Ordinal);

        return new(
            Filename: GetRequiredString(row, Nfiq2ColumnDefinitions.Filename),
            FingerCode: GetRequiredInt(row, Nfiq2ColumnDefinitions.FingerCode),
            QualityScore: GetRequiredInt(row, Nfiq2ColumnDefinitions.QualityScore),
            OptionalError: ParseNullableString(GetRequiredString(row, Nfiq2ColumnDefinitions.OptionalError)),
            Quantized: GetRequiredInt(row, Nfiq2ColumnDefinitions.Quantized) != 0,
            Resampled: GetRequiredInt(row, Nfiq2ColumnDefinitions.Resampled) != 0,
            ActionableFeedback: actionableFeedback,
            NativeQualityMeasures: nativeQualityMeasures,
            MappedQualityMeasures: mappedQualityMeasures);
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, string> row, string columnName)
    {
        if (row.TryGetValue(columnName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new Nfiq2Exception($"The NFIQ 2 CSV did not contain a value for required column '{columnName}'.");
    }

    private static int GetRequiredInt(IReadOnlyDictionary<string, string> row, string columnName)
    {
        var value = GetRequiredString(row, columnName);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        throw new Nfiq2Exception($"The NFIQ 2 CSV column '{columnName}' did not contain a valid integer value.");
    }

    private static string? ParseNullableString(string value)
    {
        return value.Equals("NA", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static double? ParseNullableDouble(string value)
    {
        if (value.Equals("NA", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        throw new Nfiq2Exception($"'{value}' is not a valid NFIQ 2 numeric value.");
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder(line.Length);
        var insideQuotes = false;

        var index = 0;
        while (index < line.Length)
        {
            var current = line[index];
            if (current == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index += 2;
                    continue;
                }

                insideQuotes = !insideQuotes;
                index++;
                continue;
            }

            if (current == ',' && !insideQuotes)
            {
                fields.Add(builder.ToString());
                builder.Clear();
                index++;
                continue;
            }

            builder.Append(current);
            index++;
        }

        fields.Add(builder.ToString());
        return fields;
    }
}
