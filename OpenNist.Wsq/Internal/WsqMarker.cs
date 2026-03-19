namespace OpenNist.Wsq.Internal;

internal enum WsqMarker : ushort
{
    StartOfImage = 0xFFA0,
    EndOfImage = 0xFFA1,
    StartOfFrame = 0xFFA2,
    StartOfBlock = 0xFFA3,
    DefineTransformTable = 0xFFA4,
    DefineQuantizationTable = 0xFFA5,
    DefineHuffmanTable = 0xFFA6,
    DefineRestartMarker = 0xFFA7,
    Comment = 0xFFA8,
}

internal static class WsqMarkerExtensions
{
    public static bool IsValid(this WsqMarker marker)
    {
        return marker is >= WsqMarker.StartOfImage and <= WsqMarker.Comment;
    }

    public static bool IsTableOrCommentBeforeFrame(this WsqMarker marker)
    {
        return marker is WsqMarker.DefineTransformTable
            or WsqMarker.DefineQuantizationTable
            or WsqMarker.DefineHuffmanTable
            or WsqMarker.Comment;
    }

    public static bool IsTableOrCommentBeforeBlock(this WsqMarker marker)
    {
        return marker is WsqMarker.DefineTransformTable
            or WsqMarker.DefineQuantizationTable
            or WsqMarker.DefineHuffmanTable
            or WsqMarker.Comment;
    }
}
