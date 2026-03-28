namespace OpenNist.Nfiq.Internal.Model;

using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Errors;

internal sealed class Nfiq2FingerprintImage
{
    private const double s_nearWhiteThreshold = 250.0;
    private const int s_fingerJetMaxWidth = 800;
    private const int s_fingerJetMaxHeight = 1000;

    public Nfiq2FingerprintImage(ReadOnlyMemory<byte> pixels, int width, int height, byte fingerCode = 0, ushort ppi = Resolution500Ppi)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Image width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Image height must be positive.");
        }

        if (pixels.Length != checked(width * height))
        {
            throw new ArgumentException("Pixel buffer size does not match the supplied image dimensions.", nameof(pixels));
        }

        Pixels = pixels;
        Width = width;
        Height = height;
        FingerCode = fingerCode;
        PixelsPerInch = ppi;
    }

    public const ushort Resolution500Ppi = 500;

    public ReadOnlyMemory<byte> Pixels { get; }

    public int Width { get; }

    public int Height { get; }

    public byte FingerCode { get; }

    public ushort PixelsPerInch { get; }

    public Nfiq2FingerprintImage CopyRemovingNearWhiteFrame()
    {
        var topRowIndex = 0;
        var bottomRowIndex = Height - 1;
        for (; topRowIndex < Height; topRowIndex++)
        {
            if (ComputeMeanFromRow(topRowIndex) <= s_nearWhiteThreshold)
            {
                break;
            }
        }

        if (topRowIndex >= Height)
        {
            throw Nfiq2Errors.ExceptionFrom(Nfiq2Errors.ValidationFailed([Nfiq2Errors.ImageRowsAppearBlank()]));
        }

        for (; bottomRowIndex >= topRowIndex; bottomRowIndex--)
        {
            if (ComputeMeanFromRow(bottomRowIndex) <= s_nearWhiteThreshold)
            {
                break;
            }
        }

        if (bottomRowIndex <= 0)
        {
            bottomRowIndex = 0;
        }

        var leftColumnIndex = 0;
        var rightColumnIndex = Width - 1;
        for (; leftColumnIndex < Width; leftColumnIndex++)
        {
            if (ComputeMeanFromColumn(leftColumnIndex) <= s_nearWhiteThreshold)
            {
                break;
            }
        }

        if (leftColumnIndex >= Width)
        {
            throw Nfiq2Errors.ExceptionFrom(Nfiq2Errors.ValidationFailed([Nfiq2Errors.ImageColumnsAppearBlank()]));
        }

        for (; rightColumnIndex >= leftColumnIndex; rightColumnIndex--)
        {
            if (ComputeMeanFromColumn(rightColumnIndex) <= s_nearWhiteThreshold)
            {
                break;
            }
        }

        if (rightColumnIndex <= 0)
        {
            rightColumnIndex = 0;
        }

        if (rightColumnIndex <= leftColumnIndex || bottomRowIndex <= topRowIndex)
        {
            throw new Nfiq2Exception(
                $"Asked to inclusively crop from ({leftColumnIndex},{topRowIndex}) to ({rightColumnIndex},{bottomRowIndex}).");
        }

        var croppedWidth = rightColumnIndex - leftColumnIndex + 1;
        var croppedHeight = bottomRowIndex - topRowIndex + 1;
        var validationErrors = new List<Nfiq2ValidationError>(capacity: 2);
        if (croppedWidth > s_fingerJetMaxWidth)
        {
            validationErrors.Add(Nfiq2Errors.TrimmedImageWidthTooLarge(croppedWidth, croppedHeight, s_fingerJetMaxWidth));
        }

        if (croppedHeight > s_fingerJetMaxHeight)
        {
            validationErrors.Add(Nfiq2Errors.TrimmedImageHeightTooLarge(croppedWidth, croppedHeight, s_fingerJetMaxHeight));
        }

        if (validationErrors.Count > 0)
        {
            throw Nfiq2Errors.ExceptionFrom(Nfiq2Errors.ValidationFailed(validationErrors));
        }

        var croppedPixels = new byte[croppedWidth * croppedHeight];
        var destinationOffset = 0;
        for (var row = topRowIndex; row <= bottomRowIndex; row++)
        {
            var rowOffset = checked(row * Width + leftColumnIndex);
            Pixels.Span.Slice(rowOffset, croppedWidth).CopyTo(croppedPixels.AsSpan(destinationOffset, croppedWidth));
            destinationOffset += croppedWidth;
        }

        return new(croppedPixels, croppedWidth, croppedHeight, FingerCode, PixelsPerInch);
    }

    private double ComputeMeanFromRow(int rowIndex)
    {
        var rowStart = checked(rowIndex * Width);
        long sum = 0;
        foreach (var pixel in Pixels.Span.Slice(rowStart, Width))
        {
            sum += pixel;
        }

        return sum / (double)Width;
    }

    private double ComputeMeanFromColumn(int columnIndex)
    {
        long sum = 0;
        for (var row = 0; row < Height; row++)
        {
            sum += Pixels.Span[row * Width + columnIndex];
        }

        return sum / (double)Height;
    }
}
