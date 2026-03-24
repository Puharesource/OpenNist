namespace OpenNist.Tests.Nfiq.TestSupport;

using System.Collections.Frozen;
using System.Globalization;

internal static class Nfiq2ExpectedOutputParser
{
    private static readonly FrozenSet<string> s_actionableMeasureNames = new[]
    {
        "UniformImage",
        "EmptyImageOrContrastTooLow",
        "FingerprintImageWithMinutiae",
        "SufficientFingerprintForeground",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static Nfiq2ExpectedOutput Parse(string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            throw new InvalidOperationException($"The expected NFIQ 2 output file '{path}' was empty.");
        }

        var parsedValues = new Dictionary<string, double>(StringComparer.Ordinal);
        var qualityScore = 0;

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"The expected NFIQ 2 output line '{line}' was invalid.");
            }

            var key = line[..separatorIndex].Trim();
            var valueText = line[(separatorIndex + 1)..].Trim();

            if (key.Equals("UnifiedQualityScore", StringComparison.Ordinal))
            {
                qualityScore = int.Parse(valueText, CultureInfo.InvariantCulture);
                continue;
            }

            parsedValues.Add(key, double.Parse(valueText, CultureInfo.InvariantCulture));
        }

        var actionableFeedback = parsedValues
            .Where(static pair => s_actionableMeasureNames.Contains(pair.Key))
            .ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

        var nativeQualityMeasures = parsedValues
            .Where(static pair => !s_actionableMeasureNames.Contains(pair.Key))
            .ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

        return new(qualityScore, actionableFeedback, nativeQualityMeasures);
    }
}
