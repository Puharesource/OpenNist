namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetImagePreparation
{
    private const int CreateFeatureSetMaxDimension = 2000;
    private const int CreateFeatureSetMinDpi = 300;
    private const int CreateFeatureSetMaxDpi = 1024;
    private const int CreateFeatureSetMinSizeAt500Scale = 150;
    private const int CreateFeatureSetMaxWidthAt500Scale = 812;
    private const int CreateFeatureSetMaxHeightAt500Scale = 1000;
    private const int ExtractMin500Width = 196;
    private const int ExtractMax500Width = 800;
    private const int ExtractMin500Height = 196;
    private const int ExtractMax500Height = 1000;
    private const int ExtractMinDpi = 300;
    private const int ExtractMaxDpi = 1008;
    private const int InternalResolutionDpi = 333;
    private const int OrientationScale = 4;
    private const int MaxWidth = 256;
    private const int MaxHeight = 400;
    private const int MaxBufferSize = MaxWidth * (MaxHeight + (MaxHeight / 4));
    private const int OrientationCellByteWidth = 2;

    public static void ValidateCreateFeatureSetInput(int width, int height, int pixelsPerInch)
    {
        if (width > CreateFeatureSetMaxDimension || height > CreateFeatureSetMaxDimension)
        {
            throw new Nfiq2Exception("FingerJet raw input dimensions exceed the native CreateFeatureSet limits.");
        }

        if (pixelsPerInch < CreateFeatureSetMinDpi || pixelsPerInch > CreateFeatureSetMaxDpi)
        {
            throw new Nfiq2Exception("FingerJet raw input resolution falls outside the native CreateFeatureSet limits.");
        }

        if ((width * 500) < (CreateFeatureSetMinSizeAt500Scale * pixelsPerInch)
            || (width * 500) > (CreateFeatureSetMaxWidthAt500Scale * pixelsPerInch))
        {
            throw new Nfiq2Exception("FingerJet raw input width falls outside the native CreateFeatureSet limits.");
        }

        if ((height * 500) < (CreateFeatureSetMinSizeAt500Scale * pixelsPerInch)
            || (height * 500) > (CreateFeatureSetMaxHeightAt500Scale * pixelsPerInch))
        {
            throw new Nfiq2Exception("FingerJet raw input height falls outside the native CreateFeatureSet limits.");
        }
    }

    public static Nfiq2FingerJetPreparedImage Prepare(Nfiq2FingerprintImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        ValidateCreateFeatureSetInput(image.Width, image.Height, image.PixelsPerInch);
        ValidateExtractInput(image);

        var inputWidth = image.Width;
        var inputHeight = image.Height;
        var pixelsPerInch = (int)image.PixelsPerInch;
        var width = ComputePreparedWidth(inputWidth, pixelsPerInch);
        var height = ComputePreparedHeight(inputHeight, pixelsPerInch);
        var xOffset = 1;
        var yOffset = 6;
        var inputOffset = 0;

        if (height > MaxHeight)
        {
            var maxHeightIn = IsApproximately500Dpi(pixelsPerInch)
                ? (MaxHeight * 3) / 2
                : (MaxHeight * pixelsPerInch) / InternalResolutionDpi;
            var diff = inputHeight - maxHeightIn;
            if (diff <= 0)
            {
                throw new Nfiq2Exception("FingerJet vertical crop planning diverged from the native extractor.");
            }

            inputOffset += (diff / 2) * inputWidth;
            yOffset += (height - MaxHeight) / 2;
            height = MaxHeight;
        }

        if (width > MaxWidth)
        {
            var maxWidthIn = IsApproximately500Dpi(pixelsPerInch)
                ? (MaxWidth * 3) / 2
                : (MaxWidth * pixelsPerInch) / InternalResolutionDpi;
            var diff = inputWidth - maxWidthIn;
            if (diff <= 0)
            {
                throw new Nfiq2Exception("FingerJet horizontal crop planning diverged from the native extractor.");
            }

            inputOffset += diff / 2;
            xOffset += (width - MaxWidth) / 2;
            width = MaxWidth;
        }

        var size = checked(width * height);
        var orientationMapWidth = ((width - 1) / OrientationScale) + 1;
        var orientationMapHeight = ((height - 1) / OrientationScale) + 1;
        var orientationMapSize = checked(orientationMapWidth * orientationMapHeight);
        var bufferSizeNeeded = size + (orientationMapSize * (1 + OrientationCellByteWidth));
        if (bufferSizeNeeded > MaxBufferSize)
        {
            throw new Nfiq2Exception("FingerJet working buffer exceeded the native extractor limit.");
        }

        var preparedPixels = new byte[size];
        var sourcePixels = image.Pixels.Span;
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            Resize23(preparedPixels, width, size, sourcePixels, inputOffset, inputWidth);
        }
        else if (Math.Abs(pixelsPerInch - InternalResolutionDpi) <= 33 && (inputWidth % OrientationScale) == 0)
        {
            sourcePixels.Slice(inputOffset, size).CopyTo(preparedPixels);
            pixelsPerInch = Nfiq2FingerJetMath.MulDiv(pixelsPerInch, 5, 3);
        }
        else
        {
            var scale256 = (pixelsPerInch * 256) / InternalResolutionDpi;
            Resize(preparedPixels, width, size, sourcePixels, inputOffset, inputWidth, inputHeight, scale256);
            pixelsPerInch = 500;
        }

        return new(preparedPixels, width, height, pixelsPerInch, xOffset, yOffset, orientationMapWidth, orientationMapSize);
    }

    private static void ValidateExtractInput(Nfiq2FingerprintImage image)
    {
        if (image.Pixels.Length < checked(image.Width * image.Height))
        {
            throw new Nfiq2Exception("FingerJet input pixel buffer is smaller than the declared image size.");
        }

        if (image.PixelsPerInch < ExtractMinDpi || image.PixelsPerInch > ExtractMaxDpi)
        {
            throw new Nfiq2Exception("FingerJet extraction resolution falls outside the native extractor limits.");
        }

        var widthAt500 = (image.Width * 500) / image.PixelsPerInch;
        var heightAt500 = (image.Width * 500) / image.PixelsPerInch;
        if (widthAt500 < ExtractMin500Width || heightAt500 < ExtractMin500Height)
        {
            throw new Nfiq2Exception("FingerJet extraction image is too small at native 500 PPI scale.");
        }

        if (widthAt500 > ExtractMax500Width || heightAt500 > ExtractMax500Height)
        {
            throw new Nfiq2Exception("FingerJet extraction image is too large at native 500 PPI scale.");
        }
    }

    private static int ComputePreparedWidth(int inputWidth, int pixelsPerInch)
    {
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            return (inputWidth / 6) * 4;
        }

        return ((inputWidth * InternalResolutionDpi) / pixelsPerInch) & ~(OrientationScale - 1);
    }

    private static int ComputePreparedHeight(int inputHeight, int pixelsPerInch)
    {
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            return (inputHeight / 6) * 4;
        }

        return ((inputHeight * InternalResolutionDpi) / pixelsPerInch) & ~(OrientationScale - 1);
    }

    private static bool IsApproximately500Dpi(int pixelsPerInch)
    {
        return pixelsPerInch <= 550 && pixelsPerInch >= 450;
    }

    private static void Resize23(Span<byte> output, int outputWidth, int outputSize, ReadOnlySpan<byte> input, int inputOffset, int inputWidth)
    {
        Span<byte> firstRowsBuffer = stackalloc byte[MaxWidth * 2];
        Resize23Block(firstRowsBuffer, outputWidth, input, inputOffset, inputWidth);
        firstRowsBuffer[..(outputWidth * 2)].CopyTo(output);

        var sourceOffset = inputOffset + (inputWidth * 3);
        var destinationOffset = outputWidth * 2;
        while (destinationOffset < outputSize)
        {
            Resize23Block(output[destinationOffset..], outputWidth, input, sourceOffset, inputWidth);
            sourceOffset += inputWidth * 3;
            destinationOffset += outputWidth * 2;
        }
    }

    private static void Resize23Block(Span<byte> output, int outputWidth, ReadOnlySpan<byte> input, int inputOffset, int inputWidth)
    {
        const int Divisor = 256;
        const int Multiplier = Divisor / 9;

        for (var inputColumn = 0; inputColumn < outputWidth * 3 / 2; inputColumn += 3)
        {
            var outputColumn = (inputColumn / 3) * 2;
            var row0 = inputOffset + inputColumn;
            var row1 = row0 + inputWidth;
            var row2 = row1 + inputWidth;

            var i00 = input[row0];
            var i01 = input[row0 + 1];
            var i02 = input[row0 + 2];
            var i10 = input[row1];
            var i11 = input[row1 + 1];
            var i12 = input[row1 + 2];
            var i20 = input[row2];
            var i21 = input[row2 + 1];
            var i22 = input[row2 + 2];

            var o00 = (i00 * 4) + (i01 * 2) + (i10 * 2) + i11;
            var o01 = (i02 * 4) + (i01 * 2) + (i12 * 2) + i11;
            var o10 = (i20 * 4) + (i21 * 2) + (i10 * 2) + i11;
            var o11 = (i22 * 4) + (i21 * 2) + (i12 * 2) + i11;

            output[outputColumn] = (byte)((o00 * Multiplier) / Divisor);
            output[outputColumn + 1] = (byte)((o01 * Multiplier) / Divisor);
            output[outputWidth + outputColumn] = (byte)((o10 * Multiplier) / Divisor);
            output[outputWidth + outputColumn + 1] = (byte)((o11 * Multiplier) / Divisor);
        }
    }

    private static void Resize(
        Span<byte> output,
        int outputWidth,
        int outputSize,
        ReadOnlySpan<byte> input,
        int inputOffset,
        int inputWidth,
        int inputHeight,
        int scale256)
    {
        var pivotOffset = inputOffset;
        var dy = 0;
        var destinationOffset = 0;
        while (destinationOffset < outputSize)
        {
            var sourceOffset = pivotOffset;
            var dx = 0;
            var endOfLine = destinationOffset + outputWidth;
            for (; destinationOffset < endOfLine; destinationOffset++)
            {
                if ((sourceOffset + inputWidth) < (inputOffset + (inputWidth * inputHeight)))
                {
                    var topLeft = input[sourceOffset];
                    var topRight = input[sourceOffset + 1];
                    var bottomLeft = input[sourceOffset + inputWidth];
                    var bottomRight = input[sourceOffset + inputWidth + 1];
                    output[destinationOffset] = (byte)(
                        (((topLeft * (256 - dx)) + (topRight * dx)) * (256 - dy)
                        + (((bottomLeft * (256 - dx)) + (bottomRight * dx)) * dy)) >> 16);
                }

                dx += scale256;
                sourceOffset += dx >> 8;
                dx &= 0xff;
            }

            dy += scale256;
            pivotOffset += (dy >> 8) * inputWidth;
            dy &= 0xff;
        }
    }
}
