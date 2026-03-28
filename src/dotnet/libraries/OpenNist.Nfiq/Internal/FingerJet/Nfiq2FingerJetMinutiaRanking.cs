namespace OpenNist.Nfiq.Internal.FingerJet;

internal static class Nfiq2FingerJetMinutiaRanking
{
    public static IReadOnlyList<Nfiq2FingerJetRawMinutia> SelectTopByConfidence(
        IReadOnlyList<Nfiq2FingerJetRawMinutia> minutiae,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var selected = new List<Nfiq2FingerJetRawMinutia>(Math.Min(minutiae.Count, capacity));
        foreach (var minutia in minutiae)
        {
            if (selected.Count < capacity)
            {
                selected.Add(minutia);
                if (selected.Count == capacity)
                {
                    selected.Sort(CompareAscending);
                }

                continue;
            }

            if (Compare(minutia, selected[0]) <= 0)
            {
                continue;
            }

            selected[0] = minutia;
            selected.Sort(CompareAscending);
        }

        selected.Sort(CompareDescending);
        return selected;
    }

    public static int Compare(Nfiq2FingerJetRawMinutia left, Nfiq2FingerJetRawMinutia right)
    {
        if (left.Confidence != right.Confidence)
        {
            return left.Confidence.CompareTo(right.Confidence);
        }

        if (left.Y != right.Y)
        {
            return right.Y.CompareTo(left.Y);
        }

        if (left.X != right.X)
        {
            return right.X.CompareTo(left.X);
        }

        if (left.Angle != right.Angle)
        {
            return right.Angle.CompareTo(left.Angle);
        }

        return 0;
    }

    private static int CompareAscending(Nfiq2FingerJetRawMinutia left, Nfiq2FingerJetRawMinutia right)
    {
        return Compare(left, right);
    }

    private static int CompareDescending(Nfiq2FingerJetRawMinutia left, Nfiq2FingerJetRawMinutia right)
    {
        return -Compare(left, right);
    }
}
