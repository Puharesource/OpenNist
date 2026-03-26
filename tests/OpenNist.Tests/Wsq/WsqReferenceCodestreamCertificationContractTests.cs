namespace OpenNist.Tests.Wsq;

using System.Text;
using OpenNist.Tests.Wsq.TestDataSources;
using OpenNist.Tests.Wsq.TestFixtures;
using OpenNist.Wsq;
using OpenNist.Wsq.Internal;

[Category("Contract: WSQ - NIST Certification")]
internal sealed class WsqReferenceCodestreamCertificationContractTests
{
    private const double s_fileSizeTolerancePercent = 0.4;
    private const int s_referenceSoftwareImplementationNumber = 38100;

    [Test]
    [DisplayName("Should satisfy the published NIST WSQ file-size and frame-header checks for every encoder reference case")]
    [MethodDataSource(typeof(WsqNistReferenceDataSources), nameof(WsqNistReferenceDataSources.EncodeFileSizeAndFrameHeaderReferenceCases))]
    public async Task ShouldSatisfyThePublishedNistWsqFileSizeAndFrameHeaderChecks(WsqEncodingReferenceCase testCase)
    {
        var rawBytes = await File.ReadAllBytesAsync(testCase.RawPath);
        await using var rawStream = new MemoryStream(rawBytes, writable: false);
        await using var encodedStream = new MemoryStream();
        await using var referenceStream = File.OpenRead(testCase.ReferencePath);
        var codec = new WsqCodec();

        await codec.EncodeAsync(
            rawStream,
            encodedStream,
            testCase.RawImage,
            new(testCase.BitRate, SoftwareImplementationNumber: s_referenceSoftwareImplementationNumber));

        encodedStream.Position = 0;
        var encodedContainer = await WsqContainerReader.ReadAsync(encodedStream);
        var referenceContainer = await WsqContainerReader.ReadAsync(referenceStream);

        var encodedSizeWithoutComments = GetSizeWithoutComments(encodedStream.GetBuffer().AsSpan(0, checked((int)encodedStream.Length)), encodedContainer.Comments);
        var referenceBytes = await File.ReadAllBytesAsync(testCase.ReferencePath);
        var referenceSizeWithoutComments = GetSizeWithoutComments(referenceBytes, referenceContainer.Comments);
        var sizeDeltaPercent = Math.Abs(encodedSizeWithoutComments - referenceSizeWithoutComments)
            / (double)referenceSizeWithoutComments
            * 100.0;

        if (sizeDeltaPercent > s_fileSizeTolerancePercent || !FrameHeadersMatch(encodedContainer.FrameHeader, referenceContainer.FrameHeader))
        {
            throw new InvalidOperationException(
                CreateMismatchMessage(
                    testCase,
                    sizeDeltaPercent,
                    encodedSizeWithoutComments,
                    referenceSizeWithoutComments,
                    encodedContainer.FrameHeader,
                    referenceContainer.FrameHeader));
        }
    }

    private static int GetSizeWithoutComments(ReadOnlySpan<byte> wsqBytes, IReadOnlyList<WsqCommentSegment> comments)
    {
        var commentBytes = 0;
        foreach (var comment in comments)
        {
            commentBytes += 4 + Encoding.ASCII.GetByteCount(comment.Text);
        }

        return wsqBytes.Length - commentBytes;
    }

    private static bool FrameHeadersMatch(WsqFrameHeader actual, WsqFrameHeader expected)
    {
        return actual.Black == expected.Black
            && actual.White == expected.White
            && actual.Height == expected.Height
            && actual.Width == expected.Width
            && actual.Shift.Equals(expected.Shift)
            && actual.Scale.Equals(expected.Scale)
            && actual.WsqEncoder == expected.WsqEncoder
            && actual.SoftwareImplementationNumber == expected.SoftwareImplementationNumber;
    }

    private static string CreateMismatchMessage(
        WsqEncodingReferenceCase testCase,
        double sizeDeltaPercent,
        int actualSizeWithoutComments,
        int expectedSizeWithoutComments,
        WsqFrameHeader actualFrameHeader,
        WsqFrameHeader expectedFrameHeader)
    {
        return $"{testCase.FileName} at {testCase.BitRate:0.##} bpp failed the published NIST encoder file-size/frame-header checks. "
            + $"Size without comments: actual={actualSizeWithoutComments}, expected={expectedSizeWithoutComments}, delta={sizeDeltaPercent:F6}% "
            + $"(limit {s_fileSizeTolerancePercent:F3}%). "
            + $"Actual frame header: {DescribeFrameHeader(actualFrameHeader)}. "
            + $"Expected frame header: {DescribeFrameHeader(expectedFrameHeader)}.";
    }

    private static string DescribeFrameHeader(WsqFrameHeader frameHeader)
    {
        return $"black={frameHeader.Black}, white={frameHeader.White}, width={frameHeader.Width}, height={frameHeader.Height}, "
            + $"shift={frameHeader.Shift:G17}, scale={frameHeader.Scale:G17}, encoder={frameHeader.WsqEncoder}, "
            + $"software={frameHeader.SoftwareImplementationNumber}";
    }
}
