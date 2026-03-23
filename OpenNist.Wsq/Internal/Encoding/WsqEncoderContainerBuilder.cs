namespace OpenNist.Wsq.Internal.Encoding;

using System.Globalization;
using OpenNist.Wsq.Internal;

internal static class WsqEncoderContainerBuilder
{
    public static WsqContainer Build(
        WsqEncoderAnalysisResult analysis,
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var huffmanEncoding = WsqHuffmanEncoder.EncodeBlocks(
            analysis.QuantizedCoefficients,
            analysis.BlockSizes);

        return new(
            analysis.FrameHeader,
            analysis.TransformTable,
            analysis.QuantizationTable,
            huffmanEncoding.HuffmanTables,
            [CreateNistComment(rawImage, options)],
            huffmanEncoding.Blocks,
            rawImage.PixelsPerInch);
    }

    private static WsqCommentSegment CreateNistComment(
        WsqRawImageDescription rawImage,
        WsqEncodeOptions options)
    {
        var ppi = rawImage.PixelsPerInch > 0
            ? rawImage.PixelsPerInch.ToString(CultureInfo.InvariantCulture)
            : "-1";
        var bitRate = options.BitRate.ToString("0.000000", CultureInfo.InvariantCulture);

        var lines = new[]
        {
            "NIST_COM 9",
            $"PIX_WIDTH {rawImage.Width.ToString(CultureInfo.InvariantCulture)}",
            $"PIX_HEIGHT {rawImage.Height.ToString(CultureInfo.InvariantCulture)}",
            $"PIX_DEPTH {rawImage.BitsPerPixel.ToString(CultureInfo.InvariantCulture)}",
            $"PPI {ppi}",
            "LOSSY 1",
            "COLORSPACE GRAY",
            "COMPRESSION WSQ",
            $"WSQ_BITRATE {bitRate}",
        };

        var text = string.Join('\n', lines);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PIX_WIDTH"] = rawImage.Width.ToString(CultureInfo.InvariantCulture),
            ["PIX_HEIGHT"] = rawImage.Height.ToString(CultureInfo.InvariantCulture),
            ["PIX_DEPTH"] = rawImage.BitsPerPixel.ToString(CultureInfo.InvariantCulture),
            ["PPI"] = ppi,
            ["LOSSY"] = "1",
            ["COLORSPACE"] = "GRAY",
            ["COMPRESSION"] = "WSQ",
            ["WSQ_BITRATE"] = bitRate,
        };

        return new(text, fields);
    }
}
