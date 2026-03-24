namespace OpenNist.Nfiq.Internal;

internal sealed record Nfiq2FingerJetMinutiaExtractionTrace(
    IReadOnlyList<Nfiq2FingerJetRawMinutia> RawMinutiae,
    byte[] DirectionMap,
    byte[] CandidateMap);
