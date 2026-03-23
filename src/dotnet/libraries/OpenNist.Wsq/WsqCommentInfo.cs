namespace OpenNist.Wsq;

/// <summary>
/// Represents a WSQ comment segment.
/// </summary>
/// <param name="Text">The raw comment text.</param>
/// <param name="Fields">Parsed NIST comment fields when present.</param>
public sealed record WsqCommentInfo(
    string Text,
    IReadOnlyDictionary<string, string> Fields);
