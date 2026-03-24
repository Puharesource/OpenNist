namespace OpenNist.Nfiq.Internal;

using System.Collections.Frozen;

internal static class Nfiq2MuModule
{
    private const int LocalRegionSquare = 32;
    private const string ImageMean = "Mu";
    private const string MeanOfBlockMeans = "MMB";

    public static Nfiq2MuModuleResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var meanOfBlockMeans = ComputeMeanOfBlockMeans(fingerprintImage);
        var (imageMean, sigma) = ComputeImageMeanAndSigma(fingerprintImage);
        return new(
            meanOfBlockMeans,
            imageMean,
            sigma,
            new Dictionary<string, double>(2, StringComparer.Ordinal)
            {
                [MeanOfBlockMeans] = meanOfBlockMeans,
                [ImageMean] = imageMean,
            }.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static double ComputeMeanOfBlockMeans(Nfiq2FingerprintImage fingerprintImage)
    {
        double sumOfBlockMeans = 0.0;
        var blockCount = 0;
        var pixels = fingerprintImage.Pixels.Span;

        for (var row = 0; row < fingerprintImage.Height; row += LocalRegionSquare)
        {
            for (var column = 0; column < fingerprintImage.Width; column += LocalRegionSquare)
            {
                var takenWidth = Math.Min(LocalRegionSquare, fingerprintImage.Width - column);
                var takenHeight = Math.Min(LocalRegionSquare, fingerprintImage.Height - row);

                long blockSum = 0;
                for (var y = 0; y < takenHeight; y++)
                {
                    var rowOffset = checked((row + y) * fingerprintImage.Width);
                    var blockRow = pixels.Slice(rowOffset + column, takenWidth);
                    foreach (var pixel in blockRow)
                    {
                        blockSum += pixel;
                    }
                }

                sumOfBlockMeans += blockSum / (double)(takenWidth * takenHeight);
                blockCount++;
            }
        }

        return sumOfBlockMeans / blockCount;
    }

    private static (double ImageMean, double Sigma) ComputeImageMeanAndSigma(Nfiq2FingerprintImage fingerprintImage)
    {
        var pixels = fingerprintImage.Pixels.Span;
        long sum = 0;
        foreach (var pixel in pixels)
        {
            sum += pixel;
        }

        var imageMean = sum / (double)pixels.Length;
        var varianceSum = 0.0;
        foreach (var pixel in pixels)
        {
            var delta = pixel - imageMean;
            varianceSum += delta * delta;
        }

        var sigma = Math.Sqrt(varianceSum / pixels.Length);
        return (imageMean, sigma);
    }
}

internal sealed record Nfiq2MuModuleResult(
    double MeanOfBlockMeans,
    double ImageMean,
    double Sigma,
    IReadOnlyDictionary<string, double> Features);
