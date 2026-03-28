namespace OpenNist.Tests.Wsq;

using OpenNist.Wsq.Internal;
using OpenNist.Wsq.Internal.Scaling;

[Category("Unit: WSQ - Value Scaling")]
internal sealed class WsqScaledValueCodecTests
{
    [Test]
    [DisplayName("should roundtrip representative uint16-scaled WSQ values through the shared codec")]
    public async Task ShouldRoundtripRepresentativeUInt16ScaledWsqValuesThroughTheSharedCodec()
    {
        var values = new[]
        {
            0.0,
            0.0001,
            0.75,
            1.2,
            2.4056661128997803,
            14.772222518920898,
            26.658464431762695,
            44.0,
            500.0,
        };

        foreach (var value in values)
        {
            var scaledValue = WsqScaledValueCodec.ScaleToUInt16(value);
            var decodedValue = WsqScaledValueCodec.ScaleUInt16ToDouble(scaledValue.RawValue, scaledValue.Scale);

            await Assert.That(WsqScaledValueCodec.RoundTripUInt16(value)).IsEqualTo(decodedValue);
        }
    }
}
