namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Represents raw CSV output and parsed results from the official NFIQ 2 CLI.
/// </summary>
/// <param name="Csv">The raw CSV emitted by the official CLI.</param>
/// <param name="Columns">The parsed CSV header columns.</param>
/// <param name="Results">The parsed CSV rows.</param>
[PublicAPI]
public sealed record Nfiq2CsvReport(
    string Csv,
    IReadOnlyList<string> Columns,
    IReadOnlyList<Nfiq2AssessmentResult> Results);
