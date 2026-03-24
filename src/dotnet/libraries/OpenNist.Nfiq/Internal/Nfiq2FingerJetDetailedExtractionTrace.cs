namespace OpenNist.Nfiq.Internal;

internal sealed record Nfiq2FingerJetDetailedExtractionTrace(
    IReadOnlyList<Nfiq2FingerJetRawMinutia> RawMinutiae,
    byte[] DirectionMap,
    byte[] CandidateMap,
    IReadOnlyList<Nfiq2FingerJetMinutiaDebugEntry> DebugEntries);
