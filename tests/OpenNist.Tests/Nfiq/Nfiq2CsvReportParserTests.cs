namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;

[Category("Unit: NFIQ2 - CSV Parser")]
internal sealed class Nfiq2CsvReportParserTests
{
    [Test]
    [DisplayName("should parse quoted csv fields without hanging on commas or quotes")]
    public async Task ShouldParseQuotedCsvFieldsWithoutHangingOnCommasOrQuotes()
    {
        const string csv =
            "Filename,QualityScore,OptionalError,Comment\n"
            + "\"sample,one.pgm\",1,NA,\"hello, \"\"world\"\"\"";

        var parsed = Nfiq2CsvReportParser.ParseCore(csv);
        var row = parsed.Rows.Single();

        await Assert.That(parsed.Columns.Count).IsEqualTo(4);
        await Assert.That(row["Filename"]).IsEqualTo("sample,one.pgm");
        await Assert.That(row["QualityScore"]).IsEqualTo("1");
        await Assert.That(row["OptionalError"]).IsEqualTo("NA");
        await Assert.That(row["Comment"]).IsEqualTo("hello, \"world\"");
    }
}
