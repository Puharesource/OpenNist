namespace OpenNist.Tests.Wsq;

using OpenNist.Tests.Wsq.TestDataReaders;
using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Scaling;

[Category("Oracle: NBIS 5.0.0 - WSQ Value Scaling")]
internal sealed class WsqNbisScaledValueOracleTests
{
    [Test]
    [DisplayName("should match NBIS 5.0.0 uint16 WSQ scaled values for representative DQT boundary cases")]
    public async Task ShouldMatchNbisUInt16ScaledValuesForRepresentativeDqtBoundaryCases()
    {
        if (!WsqNbisOracleReader.TryScaleUInt16(39.417499542236328f, out var scaledCmp00001Q)
            || !WsqNbisOracleReader.TryScaleUInt16(39.417495727539062f, out var scaledCmp00001QExpected)
            || !WsqNbisOracleReader.TryScaleUInt16(4.04010009765625f, out var scaledA001Q)
            || !WsqNbisOracleReader.TryScaleUInt16(4.040048122406006f, out var scaledA001QExpected))
        {
            return;
        }

        await Assert.That(WsqScaledValueCodec.ScaleToUInt16(39.417499542236328f)).IsEqualTo(scaledCmp00001Q);
        await Assert.That(WsqScaledValueCodec.ScaleToUInt16(39.417495727539062f)).IsEqualTo(scaledCmp00001QExpected);
        await Assert.That(WsqScaledValueCodec.ScaleToUInt16(4.04010009765625f)).IsEqualTo(scaledA001Q);
        await Assert.That(WsqScaledValueCodec.ScaleToUInt16(4.040048122406006f)).IsEqualTo(scaledA001QExpected);
    }
}
