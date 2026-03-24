namespace OpenNist.Nfiq.Internal;

using System.Globalization;
using System.Text;

internal static class Nfiq2CsvReportBuilder
{
    private static readonly string[] s_fixedColumns =
    [
        Nfiq2ColumnDefinitions.Filename,
        Nfiq2ColumnDefinitions.FingerCode,
        Nfiq2ColumnDefinitions.QualityScore,
        Nfiq2ColumnDefinitions.OptionalError,
        Nfiq2ColumnDefinitions.Quantized,
        Nfiq2ColumnDefinitions.Resampled,
    ];

    private static readonly string[] s_actionableColumns =
    [
        "UniformImage",
        "EmptyImageOrContrastTooLow",
        "FingerprintImageWithMinutiae",
        "SufficientFingerprintForeground",
    ];

    public static Nfiq2CsvReport Build(
        IReadOnlyList<Nfiq2AssessmentResult> results,
        bool includeMappedQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(results);

        var nativeColumns = results
            .SelectMany(static result => result.NativeQualityMeasures.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static column => column, StringComparer.Ordinal)
            .ToArray();

        var mappedColumns = includeMappedQualityMeasures
            ? results
                .SelectMany(static result => result.MappedQualityMeasures.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static column => column, StringComparer.Ordinal)
                .ToArray()
            : [];

        var columns = s_fixedColumns
            .Concat(s_actionableColumns)
            .Concat(nativeColumns)
            .Concat(mappedColumns)
            .ToArray();

        var csv = BuildCsv(results, columns);
        return new(csv, columns, results);
    }

    private static string BuildCsv(
        IReadOnlyList<Nfiq2AssessmentResult> results,
        IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        WriteRow(builder, columns.Select(Escape));

        foreach (var result in results)
        {
            var fields = columns.Select(column => Escape(GetValue(result, column)));
            WriteRow(builder, fields);
        }

        return builder.ToString();
    }

    private static string GetValue(Nfiq2AssessmentResult result, string column)
    {
        return column switch
        {
            Nfiq2ColumnDefinitions.Filename => result.Filename,
            Nfiq2ColumnDefinitions.FingerCode => result.FingerCode.ToString(CultureInfo.InvariantCulture),
            Nfiq2ColumnDefinitions.QualityScore => result.QualityScore.ToString(CultureInfo.InvariantCulture),
            Nfiq2ColumnDefinitions.OptionalError => result.OptionalError ?? "NA",
            Nfiq2ColumnDefinitions.Quantized => result.Quantized ? "1" : "0",
            Nfiq2ColumnDefinitions.Resampled => result.Resampled ? "1" : "0",
            _ when result.ActionableFeedback.TryGetValue(column, out var actionable) => FormatNullableDouble(actionable),
            _ when result.NativeQualityMeasures.TryGetValue(column, out var native) => FormatNullableDouble(native),
            _ when result.MappedQualityMeasures.TryGetValue(column, out var mapped) => FormatNullableDouble(mapped),
            _ => "NA",
        };
    }

    private static string FormatNullableDouble(double? value)
    {
        return value?.ToString("G17", CultureInfo.InvariantCulture) ?? "NA";
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',', StringComparison.Ordinal)
            && !value.Contains('"', StringComparison.Ordinal)
            && !value.Contains('\r', StringComparison.Ordinal)
            && !value.Contains('\n', StringComparison.Ordinal))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static void WriteRow(StringBuilder builder, IEnumerable<string> fields)
    {
        builder.AppendJoin(',', fields);
        builder.AppendLine();
    }
}
