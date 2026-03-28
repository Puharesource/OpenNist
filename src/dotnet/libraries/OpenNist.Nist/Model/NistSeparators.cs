namespace OpenNist.Nist.Model;

using JetBrains.Annotations;

/// <summary>
/// Defines the ASCII separator characters used by ANSI/NIST-style textual field structures.
/// </summary>
[PublicAPI]
public static class NistSeparators
{
    /// <summary>
    /// Gets the file separator character.
    /// </summary>
    public const char FileSeparator = (char)0x1C;

    /// <summary>
    /// Gets the group separator character.
    /// </summary>
    public const char GroupSeparator = (char)0x1D;

    /// <summary>
    /// Gets the record separator character.
    /// </summary>
    public const char RecordSeparator = (char)0x1E;

    /// <summary>
    /// Gets the unit separator character.
    /// </summary>
    public const char UnitSeparator = (char)0x1F;

    internal const byte s_fileSeparatorByte = 0x1C;
    internal const byte s_groupSeparatorByte = 0x1D;
    internal const byte s_recordSeparatorByte = 0x1E;
    internal const byte s_unitSeparatorByte = 0x1F;
}
