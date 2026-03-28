namespace OpenNist.Nfiq.Model;

using JetBrains.Annotations;

/// <summary>
/// Represents a single difference between an expected NFIQ 2 CSV and an actual one.
/// </summary>
/// <param name="Filename">The normalized filename key for the difference.</param>
/// <param name="Column">The differing column.</param>
/// <param name="ExpectedValue">The expected value.</param>
/// <param name="ActualValue">The actual value.</param>
[PublicAPI]
public sealed record Nfiq2ComplianceDifference(
    string Filename,
    string Column,
    string? ExpectedValue,
    string? ActualValue);
