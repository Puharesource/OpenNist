namespace OpenNist.Viewer.Maui.Models;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The public viewer service uses this model in its API.")]
public sealed record ImageExportFormat(string Label, string Extension, string TypeIdentifier)
{
    public static readonly ImageExportFormat Png = new("PNG", ".png", "public.png");
    public static readonly ImageExportFormat Jpeg = new("JPEG", ".jpg", "public.jpeg");
    public static readonly ImageExportFormat Tiff = new("TIFF", ".tif", "public.tiff");
    public static readonly ImageExportFormat Bmp = new("BMP", ".bmp", "com.microsoft.bmp");
}
