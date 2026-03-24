namespace OpenNist.Tests.Nfiq;

using OpenNist.Nfiq.Internal;
using OpenNist.Tests.Nfiq.TestSupport;

[Category("Integration: NFIQ2 - FingerJet Orientation Support")]
internal sealed class Nfiq2FingerJetOrientationSupportTests
{
    [Test]
    [Arguments(250000, 125000, 0)]
    [Arguments(-32000, 9000, 0)]
    [Arguments(1000, 800, 100)]
    [Arguments(0, 0, 0)]
    public async Task ShouldReproduceNativeOctSign(int real, int imaginary, int threshold)
    {
        var managed = Nfiq2FingerJetOrientationSupport.OctSign(new(real, imaginary), threshold);
        var native = Nfiq2FingerJetOracleReader.ReadOctSign(real, imaginary, threshold);
        await Assert.That(managed).IsEqualTo(native);
    }

    [Test]
    [Arguments(250000, 125000)]
    [Arguments(-32000, 9000)]
    [Arguments(3000, -12000)]
    public async Task ShouldReproduceNativeDiv2(int real, int imaginary)
    {
        var managed = Nfiq2FingerJetOrientationSupport.Div2(new(real, imaginary));
        var native = Nfiq2FingerJetOracleReader.ReadDiv2(real, imaginary);
        await Assert.That(managed).IsEqualTo(native);
    }

    [Test]
    public async Task ShouldReproduceNativeFillHoles()
    {
        byte[] values =
        [
            0, 0, 1, 0, 0, 1, 0, 0,
            0, 0, 0, 0, 1, 0, 0, 1,
            0, 1, 0, 0, 0, 0, 1, 0,
        ];

        var managed = values.ToArray();
        Nfiq2FingerJetOrientationSupport.FillHoles(managed, strideX: 1, sizeX: 8, strideY: 8, sizeY: managed.Length);
        var native = Nfiq2FingerJetOracleReader.ReadFillHoles(1, 8, 8, managed.Length, values);
        AssertEqual(managed, native);
        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    [Test]
    [Arguments(3, 8, 24, 3)]
    [Arguments(5, 8, 24, 11)]
    [Arguments(5, 8, 24, 14)]
    public async Task ShouldReproduceNativeByteBoxFilters(int boxSize, int width, int size, int threshold)
    {
        byte[] values =
        [
            0, 1, 0, 1, 0, 1, 0, 0,
            1, 1, 0, 0, 1, 0, 1, 0,
            0, 0, 1, 1, 0, 0, 1, 1,
        ];

        var managed = values.ToArray();
        Nfiq2FingerJetOrientationSupport.BoxFilterByte(managed, width, size, boxSize, (byte)threshold);
        var native = Nfiq2FingerJetOracleReader.ReadBoxFilter(boxSize, width, size, threshold, values);
        AssertEqual(managed, native);
        await Assert.That(managed.Length).IsEqualTo(native.Length);
    }

    private static void AssertEqual(byte[] actual, byte[] expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException($"Byte vector length diverged from native FingerJet. expected={expected.Length}, actual={actual.Length}.");
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Byte vector diverged from native FingerJet at index {index}. expected={expected[index]}, actual={actual[index]}.");
            }
        }
    }
}
