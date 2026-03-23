namespace OpenNist.Viewer.Maui.Models;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The public viewer service uses this model in its API.")]
public sealed record ViewerExportDocument(string SuggestedFileName, ReadOnlyMemory<byte> FileBytes);
