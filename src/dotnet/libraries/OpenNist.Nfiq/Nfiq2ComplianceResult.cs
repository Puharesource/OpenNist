namespace OpenNist.Nfiq;

using JetBrains.Annotations;

/// <summary>
/// Represents the outcome of comparing two NFIQ 2 CSV reports.
/// </summary>
/// <param name="IsConformant">Indicates whether the compared reports conform.</param>
/// <param name="ComparedRowCount">The number of rows compared.</param>
/// <param name="ComparedColumns">The compared column names.</param>
/// <param name="Differences">The detected differences.</param>
[PublicAPI]
public sealed record Nfiq2ComplianceResult(
    bool IsConformant,
    int ComparedRowCount,
    IReadOnlyList<string> ComparedColumns,
    IReadOnlyList<Nfiq2ComplianceDifference> Differences);
