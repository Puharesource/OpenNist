namespace OpenNist.Wsq.Internal.Encoding;

using System.Globalization;
using OpenNist.Wsq.Internal;

internal static class WsqEncoderContainerBuilder
{
    public static WsqContainer Build(
        WsqEncoderAnalysisResult analysis,
        WsqRawImageDescription rawImage)
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
            [CreateNistComment(rawImage)],
            huffmanEncoding.Blocks,
            rawImage.PixelsPerInch);
    }

    private static WsqCommentSegment CreateNistComment(WsqRawImageDescription rawImage)
    {
        var ppi = rawImage.PixelsPerInch > 0
            ? rawImage.PixelsPerInch.ToString(CultureInfo.InvariantCulture)
            : "-1";

        var lines = new[]
        {
            "NIST_COM 7",
            $"PIX_WIDTH {rawImage.Width.ToString(CultureInfo.InvariantCulture)}",
            $"PIX_HEIGHT {rawImage.Height.ToString(CultureInfo.InvariantCulture)}",
            $"PIX_DEPTH {rawImage.BitsPerPixel.ToString(CultureInfo.InvariantCulture)}",
            $"PPI {ppi}",
            "LOSSY 1",
            "COLORSPACE GRAY",
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
        };

        return new(text, fields);
    }
}
