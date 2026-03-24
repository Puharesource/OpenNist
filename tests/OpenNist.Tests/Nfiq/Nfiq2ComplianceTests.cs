namespace OpenNist.Tests.Nfiq;

using OpenNist.Tests.Nfiq.TestSupport;
using OpenNist.Nfiq;

[Category("Contract: NFIQ2 - Conformance CSV")]
internal sealed class Nfiq2ComplianceTests
{
    [Test]
    [DisplayName("should accept the published NFIQ 2.3.0 standard conformance CSV against itself")]
    public async Task ShouldAcceptThePublishedNfiq230StandardConformanceCsvAgainstItself()
    {
        var result = await Nfiq2Compliance.EvaluateCsvFilesAsync(
            Nfiq2TestPaths.StandardConformanceCsvPath,
            Nfiq2TestPaths.StandardConformanceCsvPath);

        await Assert.That(result.IsConformant).IsTrue();
        await Assert.That(result.Differences.Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("should accept the published NFIQ 2.3.0 mapped conformance CSV against itself")]
    public async Task ShouldAcceptThePublishedNfiq230MappedConformanceCsvAgainstItself()
    {
        var result = await Nfiq2Compliance.EvaluateCsvFilesAsync(
            Nfiq2TestPaths.MappedConformanceCsvPath,
            Nfiq2TestPaths.MappedConformanceCsvPath);

        await Assert.That(result.IsConformant).IsTrue();
        await Assert.That(result.Differences.Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("should report a score difference when the actual CSV is modified")]
    public async Task ShouldReportAScoreDifferenceWhenTheActualCsvIsModified()
    {
        var expectedCsv = await File.ReadAllTextAsync(Nfiq2TestPaths.StandardConformanceCsvPath).ConfigureAwait(false);
        var actualCsv = ModifyFirstQualityScore(expectedCsv);

        var result = Nfiq2Compliance.EvaluateCsv(expectedCsv, actualCsv);

        await Assert.That(result.IsConformant).IsFalse();
        await Assert.That(result.Differences.Count).IsGreaterThan(0);

        var qualityScoreDifference = result.Differences.FirstOrDefault(static difference =>
            difference.Column.Equals("QualityScore", StringComparison.Ordinal));

        if (qualityScoreDifference is null)
        {
            throw new InvalidOperationException("The modified NFIQ 2 CSV did not report a QualityScore difference.");
        }
    }

    private static string ModifyFirstQualityScore(string csv)
    {
        var lines = csv
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var header = lines[0].Split(',');
        var qualityScoreIndex = Array.IndexOf(header, "QualityScore");
        if (qualityScoreIndex < 0)
        {
            throw new InvalidOperationException("The expected conformance CSV did not contain a QualityScore column.");
        }

        var values = lines[1].Split(',');
        values[qualityScoreIndex] = values[qualityScoreIndex] == "1" ? "2" : "1";
        lines[1] = string.Join(',', values);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
