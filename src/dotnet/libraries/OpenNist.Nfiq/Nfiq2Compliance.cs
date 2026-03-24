namespace OpenNist.Nfiq;

using OpenNist.Nfiq.Internal;
using JetBrains.Annotations;

/// <summary>
/// Compares NFIQ 2 CSV reports using the official conformance-column rules.
/// </summary>
[PublicAPI]
public static class Nfiq2Compliance
{
    /// <summary>
    /// Evaluates two NFIQ 2 CSV files for conformance.
    /// </summary>
    /// <param name="expectedCsvPath">The expected CSV path.</param>
    /// <param name="actualCsvPath">The actual CSV path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conformance result.</returns>
    public static async ValueTask<Nfiq2ComplianceResult> EvaluateCsvFilesAsync(
        string expectedCsvPath,
        string actualCsvPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCsvPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualCsvPath);

        var expectedCsv = await File.ReadAllTextAsync(expectedCsvPath, cancellationToken).ConfigureAwait(false);
        var actualCsv = await File.ReadAllTextAsync(actualCsvPath, cancellationToken).ConfigureAwait(false);
        return EvaluateCsv(expectedCsv, actualCsv);
    }

    /// <summary>
    /// Evaluates two NFIQ 2 CSV strings for conformance.
    /// </summary>
    /// <param name="expectedCsv">The expected CSV.</param>
    /// <param name="actualCsv">The actual CSV.</param>
    /// <returns>The conformance result.</returns>
    public static Nfiq2ComplianceResult EvaluateCsv(string expectedCsv, string actualCsv)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCsv);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualCsv);

        return Nfiq2ComplianceEvaluator.Evaluate(expectedCsv, actualCsv);
    }
}
