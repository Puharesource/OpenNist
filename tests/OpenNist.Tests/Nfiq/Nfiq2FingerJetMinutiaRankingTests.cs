namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Minutia Ranking")]
internal sealed class Nfiq2FingerJetMinutiaRankingTests
{
    [Test]
    public async Task ShouldReproduceNativeTopNMinutiaSelection()
    {
        Nfiq2FingerJetRawMinutia[] minutiae =
        [
            new(8, 30, 40, 100, 1),
            new(10, 25, 10, 80, 2),
            new(12, 25, 20, 80, 0),
            new(4, 25, 20, 80, 1),
            new(2, 40, 90, 120, 2),
            new(7, 18, 30, 100, 1),
            new(9, 18, 10, 100, 2),
        ];

        var managed = Nfiq2FingerJetMinutiaRanking.SelectTopByConfidence(minutiae, capacity: 4);
        var native = Nfiq2FingerJetOracleReader.ReadRankedRawMinutiae(capacity: 4, minutiae);

        AssertEqual(managed, native);
        await Assert.That(managed.Count).IsEqualTo(native.Count);
    }

    [Test]
    public async Task ShouldKeepNativeOrderingRulesForConfidenceTies()
    {
        Nfiq2FingerJetRawMinutia[] minutiae =
        [
            new(100, 44, 50, 90, 1),
            new(80, 44, 20, 90, 2),
            new(80, 30, 80, 90, 0),
            new(80, 30, 60, 90, 1),
            new(80, 30, 60, 70, 2),
        ];

        var managed = Nfiq2FingerJetMinutiaRanking.SelectTopByConfidence(minutiae, capacity: 5);
        var native = Nfiq2FingerJetOracleReader.ReadRankedRawMinutiae(capacity: 5, minutiae);

        AssertEqual(managed, native);
        await Assert.That(managed.Count).IsEqualTo(native.Count);
    }

    private static void AssertEqual(IReadOnlyList<Nfiq2FingerJetRawMinutia> actual, IReadOnlyList<Nfiq2FingerJetRawMinutia> expected)
    {
        if (actual.Count != expected.Count)
        {
            throw new InvalidOperationException($"Ranked minutia count diverged from native FingerJet. expected={expected.Count}, actual={actual.Count}.");
        }

        for (var index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Ranked minutia diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }
}
