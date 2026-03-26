namespace OpenNist.Wsq.Internal;

internal static class WsqCommentParser
{
    public static WsqCommentInfo ReadCommentInfo(ref WsqBufferReader reader)
    {
        var payload = reader.ReadSegmentPayload();
        return ReadCommentInfo(payload);
    }

    public static WsqCommentInfo ReadCommentInfo(ReadOnlySpan<byte> payload)
    {
        var text = System.Text.Encoding.ASCII.GetString(payload);
        var fields = ParseNistComFields(text);
        return new(text, fields);
    }

    public static WsqCommentSegment ReadCommentSegment(ref WsqBufferReader reader)
    {
        var payload = reader.ReadSegmentPayload();
        return ReadCommentSegment(payload);
    }

    public static WsqCommentSegment ReadCommentSegment(ReadOnlySpan<byte> payload)
    {
        var comment = ReadCommentInfo(payload);
        return new(comment.Text, comment.Fields);
    }

    private static Dictionary<string, string> ParseNistComFields(string text)
    {
        if (!text.StartsWith("NIST_COM", StringComparison.Ordinal))
        {
            return new(0, StringComparer.Ordinal);
        }

        var fields = new Dictionary<string, string>(capacity: 8, comparer: StringComparer.Ordinal);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOfAny([' ', '\t']);
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            fields[key] = value;
        }

        return fields;
    }
}
