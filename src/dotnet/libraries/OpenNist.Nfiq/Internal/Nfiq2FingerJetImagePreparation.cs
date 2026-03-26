namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2FingerJetImagePreparation
{
    private const int s_createFeatureSetMaxDimension = 2000;
    private const int s_createFeatureSetMinDpi = 300;
    private const int s_createFeatureSetMaxDpi = 1024;
    private const int s_createFeatureSetMinSizeAt500Scale = 150;
    private const int s_createFeatureSetMaxWidthAt500Scale = 812;
    private const int s_createFeatureSetMaxHeightAt500Scale = 1000;
    private const int s_extractMin500Width = 196;
    private const int s_extractMax500Width = 800;
    private const int s_extractMin500Height = 196;
    private const int s_extractMax500Height = 1000;
    private const int s_extractMinDpi = 300;
    private const int s_extractMaxDpi = 1008;
    private const int s_internalResolutionDpi = 333;
    private const int s_orientationScale = 4;
    private const int s_maxWidth = 256;
    private const int s_maxHeight = 400;
    private const int s_maxBufferSize = s_maxWidth * (s_maxHeight + s_maxHeight / 4);
    private const int s_orientationCellByteWidth = 2;

    public static void ValidateCreateFeatureSetInput(int width, int height, int pixelsPerInch)
    {
        if (width > s_createFeatureSetMaxDimension || height > s_createFeatureSetMaxDimension)
        {
            throw new Nfiq2Exception("FingerJet raw input dimensions exceed the native CreateFeatureSet limits.");
        }

        if (pixelsPerInch is < s_createFeatureSetMinDpi or > s_createFeatureSetMaxDpi)
        {
            throw new Nfiq2Exception("FingerJet raw input resolution falls outside the native CreateFeatureSet limits.");
        }

        if (width * 500 < s_createFeatureSetMinSizeAt500Scale * pixelsPerInch
            || width * 500 > s_createFeatureSetMaxWidthAt500Scale * pixelsPerInch)
        {
            throw new Nfiq2Exception("FingerJet raw input width falls outside the native CreateFeatureSet limits.");
        }

        if (height * 500 < s_createFeatureSetMinSizeAt500Scale * pixelsPerInch
            || height * 500 > s_createFeatureSetMaxHeightAt500Scale * pixelsPerInch)
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

        if (height > s_maxHeight)
        {
            var maxHeightIn = IsApproximately500Dpi(pixelsPerInch)
                ? s_maxHeight * 3 / 2
                : s_maxHeight * pixelsPerInch / s_internalResolutionDpi;
            var diff = inputHeight - maxHeightIn;
            if (diff <= 0)
            {
                throw new Nfiq2Exception("FingerJet vertical crop planning diverged from the native extractor.");
            }

            inputOffset += diff / 2 * inputWidth;
            yOffset += (height - s_maxHeight) / 2;
            height = s_maxHeight;
        }

        if (width > s_maxWidth)
        {
            var maxWidthIn = IsApproximately500Dpi(pixelsPerInch)
                ? s_maxWidth * 3 / 2
                : s_maxWidth * pixelsPerInch / s_internalResolutionDpi;
            var diff = inputWidth - maxWidthIn;
            if (diff <= 0)
            {
                throw new Nfiq2Exception("FingerJet horizontal crop planning diverged from the native extractor.");
            }

            inputOffset += diff / 2;
            xOffset += (width - s_maxWidth) / 2;
            width = s_maxWidth;
        }

        var size = checked(width * height);
        var orientationMapWidth = (width - 1) / s_orientationScale + 1;
        var orientationMapHeight = (height - 1) / s_orientationScale + 1;
        var orientationMapSize = checked(orientationMapWidth * orientationMapHeight);
        var bufferSizeNeeded = size + orientationMapSize * (1 + s_orientationCellByteWidth);
        if (bufferSizeNeeded > s_maxBufferSize)
        {
            throw new Nfiq2Exception("FingerJet working buffer exceeded the native extractor limit.");
        }

        var preparedPixels = GC.AllocateUninitializedArray<byte>(size);
        var sourcePixels = image.Pixels.Span;
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            Resize23(preparedPixels, width, size, sourcePixels, inputOffset, inputWidth);
        }
        else if (Math.Abs(pixelsPerInch - s_internalResolutionDpi) <= 33 && inputWidth % s_orientationScale == 0)
        {
            sourcePixels.Slice(inputOffset, size).CopyTo(preparedPixels);
            pixelsPerInch = Nfiq2FingerJetMath.MulDiv(pixelsPerInch, 5, 3);
        }
        else
        {
            var scale256 = pixelsPerInch * 256 / s_internalResolutionDpi;
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

        if (image.PixelsPerInch is < s_extractMinDpi or > s_extractMaxDpi)
        {
            throw new Nfiq2Exception("FingerJet extraction resolution falls outside the native extractor limits.");
        }

        var widthAt500 = image.Width * 500 / image.PixelsPerInch;
        var heightAt500 = image.Width * 500 / image.PixelsPerInch;
        if (widthAt500 < s_extractMin500Width || heightAt500 < s_extractMin500Height)
        {
            throw new Nfiq2Exception("FingerJet extraction image is too small at native 500 PPI scale.");
        }

        if (widthAt500 > s_extractMax500Width || heightAt500 > s_extractMax500Height)
        {
            throw new Nfiq2Exception("FingerJet extraction image is too large at native 500 PPI scale.");
        }
    }

    private static int ComputePreparedWidth(int inputWidth, int pixelsPerInch)
    {
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            return inputWidth / 6 * 4;
        }

        return (inputWidth * s_internalResolutionDpi / pixelsPerInch) & ~(s_orientationScale - 1);
    }

    private static int ComputePreparedHeight(int inputHeight, int pixelsPerInch)
    {
        if (IsApproximately500Dpi(pixelsPerInch))
        {
            return inputHeight / 6 * 4;
        }

        return (inputHeight * s_internalResolutionDpi / pixelsPerInch) & ~(s_orientationScale - 1);
    }

    private static bool IsApproximately500Dpi(int pixelsPerInch)
    {
        return pixelsPerInch is <= 550 and >= 450;
    }

    private static void Resize23(Span<byte> output, int outputWidth, int outputSize, ReadOnlySpan<byte> input, int inputOffset, int inputWidth)
    {
        Span<byte> firstRowsBuffer = stackalloc byte[s_maxWidth * 2];
        Resize23Block(firstRowsBuffer, outputWidth, input, inputOffset, inputWidth);
        firstRowsBuffer[..(outputWidth * 2)].CopyTo(output);

        var sourceOffset = inputOffset + inputWidth * 3;
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
        const int divisor = 256;
        const int multiplier = divisor / 9;

        for (var inputColumn = 0; inputColumn < outputWidth * 3 / 2; inputColumn += 3)
        {
            var outputColumn = inputColumn / 3 * 2;
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

            var o00 = i00 * 4 + i01 * 2 + i10 * 2 + i11;
            var o01 = i02 * 4 + i01 * 2 + i12 * 2 + i11;
            var o10 = i20 * 4 + i21 * 2 + i10 * 2 + i11;
            var o11 = i22 * 4 + i21 * 2 + i12 * 2 + i11;

            output[outputColumn] = (byte)(o00 * multiplier / divisor);
            output[outputColumn + 1] = (byte)(o01 * multiplier / divisor);
            output[outputWidth + outputColumn] = (byte)(o10 * multiplier / divisor);
            output[outputWidth + outputColumn + 1] = (byte)(o11 * multiplier / divisor);
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
                if (sourceOffset + inputWidth < inputOffset + inputWidth * inputHeight)
                {
                    var topLeft = input[sourceOffset];
                    var topRight = input[sourceOffset + 1];
                    var bottomLeft = input[sourceOffset + inputWidth];
                    var bottomRight = input[sourceOffset + inputWidth + 1];
                    output[destinationOffset] = (byte)(
                        ((topLeft * (256 - dx) + topRight * dx) * (256 - dy)
                        + (bottomLeft * (256 - dx) + bottomRight * dx) * dy) >> 16);
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
