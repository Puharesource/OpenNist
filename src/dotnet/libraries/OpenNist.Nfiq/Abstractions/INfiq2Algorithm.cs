namespace OpenNist.Nfiq.Abstractions;

using JetBrains.Annotations;
using OpenNist.Nfiq.Configuration;
using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Model;

/// <summary>
/// Defines an NFIQ 2 quality-scoring integration surface.
/// </summary>
[PublicAPI]
public interface INfiq2Algorithm
{
    /// <summary>
    /// Tries to analyze a fingerprint image file supported by the managed NFIQ 2 pipeline.
    /// </summary>
    /// <param name="fingerprintPath">The fingerprint image path.</param>
    /// <param name="options">The analysis options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A non-throwing structured result.</returns>
    ValueTask<Nfiq2Result<Nfiq2AssessmentResult>> TryAnalyzeFileAsync(
        string fingerprintPath,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a fingerprint image file supported by the managed NFIQ 2 pipeline.
    /// </summary>
    /// <param name="fingerprintPath">The fingerprint image path.</param>
    /// <param name="options">The analysis options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed NFIQ 2 result for the single file.</returns>
    ValueTask<Nfiq2AssessmentResult> AnalyzeFileAsync(
        string fingerprintPath,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to analyze an in-memory 8-bit grayscale fingerprint image.
    /// </summary>
    /// <param name="rawPixels">The raw grayscale pixels in row-major order.</param>
    /// <param name="rawImage">The raw image description.</param>
    /// <param name="options">The analysis options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A non-throwing structured result.</returns>
    ValueTask<Nfiq2Result<Nfiq2AssessmentResult>> TryAnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes an in-memory 8-bit grayscale fingerprint image.
    /// </summary>
    /// <param name="rawPixels">The raw grayscale pixels in row-major order.</param>
    /// <param name="rawImage">The raw image description.</param>
    /// <param name="options">The analysis options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed NFIQ 2 result for the in-memory image.</returns>
    ValueTask<Nfiq2AssessmentResult> AnalyzeAsync(
        ReadOnlyMemory<byte> rawPixels,
        Nfiq2RawImageDescription rawImage,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes one or more fingerprint paths and returns the raw CSV and parsed rows.
    /// </summary>
    /// <param name="fingerprintPaths">The fingerprint paths.</param>
    /// <param name="options">The analysis options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw CSV output and parsed NFIQ 2 rows.</returns>
    ValueTask<Nfiq2CsvReport> AnalyzeFilesAsync(
        IEnumerable<string> fingerprintPaths,
        Nfiq2AnalysisOptions options = default,
        CancellationToken cancellationToken = default);
}
