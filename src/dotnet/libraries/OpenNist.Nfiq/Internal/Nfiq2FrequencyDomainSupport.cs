namespace OpenNist.Nfiq.Internal;

using System.Numerics;

internal static class Nfiq2FrequencyDomainSupport
{
    private const double s_neighborContributionWeight = 0.3;

    public static double ComputeFrequencyDomainAnalysisScore(
        ReadOnlySpan<byte> blockCropped,
        int width,
        int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        if (blockCropped.Length != checked(width * height))
        {
            throw new ArgumentException("Block size does not match the supplied dimensions.", nameof(blockCropped));
        }

        var rowMeans = new double[height];
        for (var row = 0; row < height; row++)
        {
            var sum = 0.0;
            var rowOffset = row * width;
            for (var column = 0; column < width; column++)
            {
                sum += blockCropped[rowOffset + column];
            }

            rowMeans[row] = sum / width;
        }

        var amplitudes = ComputeMagnitudeSpectrum(rowMeans);
        if (amplitudes.Length <= 2)
        {
            return 1.0;
        }

        var maxValue = double.MinValue;
        var maxIndex = 0;
        for (var index = 0; index < amplitudes.Length; index++)
        {
            if (amplitudes[index] > maxValue)
            {
                maxValue = amplitudes[index];
                maxIndex = index;
            }
        }

        if (maxIndex == 0 || maxIndex + 1 >= amplitudes.Length)
        {
            return 1.0;
        }

        var denominatorLength = (int)Math.Floor(amplitudes.Length / 2.0);
        var denominator = 0.0;
        for (var index = 0; index < denominatorLength; index++)
        {
            denominator += amplitudes[index];
        }

        return (maxValue + s_neighborContributionWeight * (amplitudes[maxIndex - 1] + amplitudes[maxIndex + 1])) / denominator;
    }

    private static double[] ComputeMagnitudeSpectrum(double[] samples)
    {
        var amplitudes = new double[samples.Length - 1];
        for (var frequency = 1; frequency < samples.Length; frequency++)
        {
            var sum = Complex.Zero;
            for (var sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                var angle = -2.0 * Math.PI * frequency * sampleIndex / samples.Length;
                sum += samples[sampleIndex] * Complex.FromPolarCoordinates(1.0, angle);
            }

            amplitudes[frequency - 1] = sum.Magnitude;
        }

        return amplitudes;
    }
}
