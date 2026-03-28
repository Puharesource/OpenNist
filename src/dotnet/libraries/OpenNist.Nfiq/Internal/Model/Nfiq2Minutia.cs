namespace OpenNist.Nfiq.Internal.Model;

internal readonly record struct Nfiq2Minutia(
    int X,
    int Y,
    int Angle,
    int Quality,
    int Type);
