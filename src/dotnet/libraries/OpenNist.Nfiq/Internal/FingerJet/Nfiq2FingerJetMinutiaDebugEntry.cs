namespace OpenNist.Nfiq.Internal.FingerJet;

internal sealed record Nfiq2FingerJetMinutiaDebugEntry(
    int X,
    int Y,
    byte CandidateAngle,
    byte FinalAngle,
    int Confidence,
    int Type,
    bool AdjustedAbsolute,
    bool AdjustedRelative);
