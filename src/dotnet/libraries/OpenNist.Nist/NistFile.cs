namespace OpenNist.Nist;

using JetBrains.Annotations;

/// <summary>
/// Represents one decoded NIST file.
/// </summary>
[PublicAPI]
public sealed class NistFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NistFile"/> class.
    /// </summary>
    /// <param name="records">The logical records contained in the file.</param>
    public NistFile(IEnumerable<NistRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        Records = records is ICollection<NistRecord> collection ? [.. collection] : [.. records];
    }

    /// <summary>
    /// Gets the logical records contained in the file.
    /// </summary>
    public IReadOnlyList<NistRecord> Records { get; }

    /// <summary>
    /// Finds all records of a given type.
    /// </summary>
    /// <param name="recordType">The logical record type number.</param>
    /// <returns>The matching records.</returns>
    public IReadOnlyList<NistRecord> FindRecords(int recordType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recordType);

        var matches = new List<NistRecord>();
        for (var index = 0; index < Records.Count; index++)
        {
            if (Records[index].Type == recordType)
            {
                matches.Add(Records[index]);
            }
        }

        return matches;
    }
}
