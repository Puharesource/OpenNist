namespace OpenNist.Nfiq.Internal.Compliance;

using System.Globalization;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Csv;
using OpenNist.Nfiq.Model;

internal static class Nfiq2ComplianceEvaluator
{
    private const string s_metadataColumnName = "__metadata__";
    private const string s_rowCountFilename = "*";

    public static Nfiq2ComplianceResult Evaluate(string expectedCsv, string actualCsv)
    {
        var expected = Nfiq2CsvReportParser.ParseCore(expectedCsv);
        var actual = Nfiq2CsvReportParser.ParseCore(actualCsv);

        var comparedColumns = DetermineComparedColumns(expected, actual);
        var differences = new List<Nfiq2ComplianceDifference>();

        var expectedRows = CreateNormalizedRowLookup(expected.Rows);
        var actualRows = CreateNormalizedRowLookup(actual.Rows);

        if (expectedRows.Count != actualRows.Count)
        {
            differences.Add(new(
                s_rowCountFilename,
                s_metadataColumnName,
                expectedRows.Count.ToString(CultureInfo.InvariantCulture),
                actualRows.Count.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var expectedFileName in expectedRows.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!actualRows.TryGetValue(expectedFileName, out var actualRow))
            {
                differences.Add(new(expectedFileName, Nfiq2ColumnDefinitions.Filename, expectedFileName, null));
                continue;
            }

            var expectedRow = expectedRows[expectedFileName];
            foreach (var column in comparedColumns)
            {
                if (!expectedRow.TryGetValue(column, out var expectedValue))
                {
                    throw new Nfiq2Exception($"Expected NFIQ 2 CSV row '{expectedFileName}' is missing column '{column}'.");
                }

                if (!actualRow.TryGetValue(column, out var actualValue))
                {
                    differences.Add(new(expectedFileName, column, expectedValue, null));
                    continue;
                }

                if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
                {
                    differences.Add(new(expectedFileName, column, expectedValue, actualValue));
                }
            }
        }

        foreach (var actualFileName in actualRows.Keys
                     .Where(actualFileName => !expectedRows.ContainsKey(actualFileName))
                     .OrderBy(static value => value, StringComparer.Ordinal))
        {
            differences.Add(new(actualFileName, Nfiq2ColumnDefinitions.Filename, null, actualFileName));
        }

        return new(
            IsConformant: differences.Count == 0,
            ComparedRowCount: expectedRows.Count,
            ComparedColumns: comparedColumns,
            Differences: differences);
    }

    private static IReadOnlyList<string> DetermineComparedColumns(
        Nfiq2CsvReportParser.ParsedCsv expected,
        Nfiq2CsvReportParser.ParsedCsv actual)
    {
        var expectedColumnOrder = expected.Columns.ToArray();

        if (expected.Columns.Any(static column => column.StartsWith(Nfiq2ColumnDefinitions.MappedPrefix, StringComparison.Ordinal)))
        {
            var missingColumns = expected.Columns.Where(column => !actual.HeaderSet.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new Nfiq2Exception(
                    $"The actual NFIQ 2 CSV is missing expected columns: {string.Join(", ", missingColumns)}.");
            }

            return expected.Columns;
        }

        var missingComplianceColumns = Nfiq2ColumnDefinitions.StandardComplianceColumns
            .Where(column => !expected.HeaderSet.Contains(column) || !actual.HeaderSet.Contains(column))
            .ToArray();

        if (missingComplianceColumns.Length > 0)
        {
            throw new Nfiq2Exception(
                $"The NFIQ 2 CSV inputs are missing required compliance columns: {string.Join(", ", missingComplianceColumns)}.");
        }

        return Nfiq2ColumnDefinitions.StandardComplianceColumns
            .OrderBy(column => Array.IndexOf(expectedColumnOrder, column))
            .ToArray();
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> CreateNormalizedRowLookup(
        IEnumerable<IReadOnlyDictionary<string, string>> rows)
    {
        var lookup = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var filename = row[Nfiq2ColumnDefinitions.Filename];
            var normalizedFileName = NormalizeFileName(filename);
            if (!lookup.TryAdd(normalizedFileName, row))
            {
                throw new Nfiq2Exception(
                    $"Duplicate normalized filename '{normalizedFileName}' detected in the NFIQ 2 CSV.");
            }
        }

        return lookup;
    }

    private static string NormalizeFileName(string path)
    {
        return Path.GetFileName(path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }
}
