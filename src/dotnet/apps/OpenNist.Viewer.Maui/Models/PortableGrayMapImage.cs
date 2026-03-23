namespace OpenNist.Viewer.Maui.Models;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "This model participates in the viewer conversion pipeline.")]
public sealed record PortableGrayMapImage(
    int Width,
    int Height,
    ReadOnlyMemory<byte> Pixels);
