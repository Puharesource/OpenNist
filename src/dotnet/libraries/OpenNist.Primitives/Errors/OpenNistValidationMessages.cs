namespace OpenNist.Primitives.Errors;

using System.Globalization;
using JetBrains.Annotations;

/// <summary>
/// Builds shared public validation failure messages for OpenNist libraries.
/// </summary>
[PublicAPI]
public static class OpenNistValidationMessages
{
    /// <summary>
    /// Builds a single-message or bulleted multi-message validation summary.
    /// </summary>
    /// <typeparam name="TValidationError">The validation error type.</typeparam>
    /// <param name="subject">The user-facing operation or library name.</param>
    /// <param name="validationErrors">The collected validation errors.</param>
    /// <param name="includeIssueCount">Whether to include the issue count in the summary line.</param>
    /// <param name="emptyFallback">The fallback message when no validation errors were supplied.</param>
    /// <returns>The combined validation message.</returns>
    public static string BuildFailureMessage<TValidationError>(
        string subject,
        IReadOnlyList<TValidationError> validationErrors,
        bool includeIssueCount = true,
        string? emptyFallback = null)
        where TValidationError : OpenNistValidationError
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(validationErrors);

        if (validationErrors.Count == 0)
        {
            return emptyFallback ?? $"{subject} validation failed.";
        }

        if (validationErrors.Count == 1)
        {
            return validationErrors[0].Message;
        }

        var summary = includeIssueCount
            ? $"{subject} validation failed with {validationErrors.Count.ToString(CultureInfo.InvariantCulture)} issues:"
            : $"{subject} validation failed:";

        return summary + Environment.NewLine + "- "
            + string.Join(Environment.NewLine + "- ", validationErrors.Select(static error => error.Message));
    }
}
