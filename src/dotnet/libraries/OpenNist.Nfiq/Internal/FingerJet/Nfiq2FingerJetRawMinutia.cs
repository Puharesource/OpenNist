namespace OpenNist.Nfiq.Internal.FingerJet;

internal readonly record struct Nfiq2FingerJetRawMinutia(
    int X,
    int Y,
    int Angle,
    int Confidence,
    int Type);
