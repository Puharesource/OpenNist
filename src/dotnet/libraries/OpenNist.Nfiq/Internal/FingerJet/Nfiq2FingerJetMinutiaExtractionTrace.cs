namespace OpenNist.Nfiq.Internal.FingerJet;

internal sealed record Nfiq2FingerJetMinutiaExtractionTrace(
    IReadOnlyList<Nfiq2FingerJetRawMinutia> RawMinutiae,
    byte[] DirectionMap,
    byte[] CandidateMap);
