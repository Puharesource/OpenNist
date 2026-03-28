namespace OpenNist.Nfiq.Internal.FingerJet;

internal sealed record Nfiq2FingerJetManagedExtractionResult(
    Nfiq2FingerJetPreparedImage PreparedImage,
    IReadOnlyList<Nfiq2FingerJetComplex> Orientation,
    IReadOnlyList<byte> Footprint,
    byte[] Phasemap,
    IReadOnlyList<Nfiq2FingerJetRawMinutia> RawMinutiae);
