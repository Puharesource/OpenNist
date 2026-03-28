namespace OpenNist.Tests.Wsq;

using OpenNist.Wsq;
using OpenNist.Wsq.Codecs;
using OpenNist.Wsq.Errors;

[Category("Unit: WSQ - Public Errors")]
internal sealed class WsqCodecPublicErrorTests
{
    private static readonly WsqCodec s_codec = new();

    [Test]
    [DisplayName("should combine WSQ encode validation failures in the non-throwing API")]
    public async Task ShouldCombineWsqEncodeValidationFailuresInTheNonThrowingApi()
    {
        await using var rawStream = new MemoryStream(Array.Empty<byte>(), writable: false);
        await using var wsqStream = new MemoryStream();

        var result = await s_codec.TryEncodeAsync(
            rawStream,
            wsqStream,
            new(Width: 0, Height: 0, BitsPerPixel: 16),
            new(BitRate: 0, EncoderNumber: 999, SoftwareImplementationNumber: 70000)).ConfigureAwait(false);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        var error = result.Error!;
        await Assert.That(error.Code).IsEqualTo(WsqErrorCodes.ValidationFailed);
        var validationErrors = error.ValidationErrors ?? [];
        var validationCodes = validationErrors.Select(static validationError => validationError.Code).ToArray();
        await Assert.That(validationErrors).Count().IsEqualTo(6);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.RawImageWidthMustBePositive);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.RawImageHeightMustBePositive);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.RawImageBitsPerPixelUnsupported);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.BitRateMustBePositive);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.EncoderNumberOutOfRange);
        await Assert.That(validationCodes).Contains(WsqErrorCodes.SoftwareImplementationNumberOutOfRange);
    }

    [Test]
    [DisplayName("should throw a structured WSQ exception with combined encode validation failures")]
    public async Task ShouldThrowStructuredWsqExceptionWithCombinedEncodeValidationFailures()
    {
        await using var rawStream = new MemoryStream(Array.Empty<byte>(), writable: false);
        await using var wsqStream = new MemoryStream();

        var exception = await Assert.ThrowsAsync<WsqException>(async () =>
        {
            await s_codec.EncodeAsync(
                rawStream,
                wsqStream,
                new(Width: 0, Height: 0, BitsPerPixel: 16),
                new(BitRate: 0, EncoderNumber: 999, SoftwareImplementationNumber: 70000)).ConfigureAwait(false);
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(WsqErrorCodes.ValidationFailed);
        await Assert.That(exception.ValidationErrors).Count().IsEqualTo(6);
    }

    [Test]
    [DisplayName("should return a structured malformed-bitstream failure when inspecting invalid WSQ input")]
    public async Task ShouldReturnAStructuredMalformedBitstreamFailureWhenInspectingInvalidWsqInput()
    {
        await using var wsqStream = new MemoryStream([0x00, 0x00, 0x00, 0x00], writable: false);

        var result = await s_codec.TryInspectAsync(wsqStream).ConfigureAwait(false);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Code).IsEqualTo(WsqErrorCodes.MalformedBitstream);
    }

    [Test]
    [DisplayName("should return a structured malformed-bitstream failure when decoding invalid WSQ input")]
    public async Task ShouldReturnAStructuredMalformedBitstreamFailureWhenDecodingInvalidWsqInput()
    {
        await using var wsqStream = new MemoryStream([0x00, 0x00, 0x00, 0x00], writable: false);
        await using var rawStream = new MemoryStream();

        var result = await s_codec.TryDecodeAsync(wsqStream, rawStream).ConfigureAwait(false);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Code).IsEqualTo(WsqErrorCodes.MalformedBitstream);
    }
}
