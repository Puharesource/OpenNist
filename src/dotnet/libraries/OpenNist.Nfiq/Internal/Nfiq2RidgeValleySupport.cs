namespace OpenNist.Nfiq.Internal;

internal static class Nfiq2RidgeValleySupport
{
    private const int s_rotationPaddingBorder = 2;
    private const int s_affineBits = 10;
    private const int s_affineScale = 1 << s_affineBits;
    private const double s_scannerNormalizationBase = 125.0;
    private const double s_ridgeWidthMaxAt125Ppi = 5.0;
    private const double s_valleyWidthMaxAt125Ppi = 5.0;
    private const double s_ridgeWidthMin = 3.0;
    private const double s_ridgeWidthMax = 10.0;
    private const double s_valleyWidthMin = 2.0;
    private const double s_valleyWidthMax = 10.0;

    public static Nfiq2BlockGeometry GetOverlappingBlockGeometry(
        int blockSize,
        int slantedBlockWidth,
        int slantedBlockHeight)
    {
        var sumSquare = slantedBlockWidth * slantedBlockWidth + slantedBlockHeight * slantedBlockHeight;
        var extractedBlockSize = (int)Math.Ceiling(Math.Sqrt(sumSquare));
        var overlapDifference = extractedBlockSize - blockSize;
        var blockOffset = (int)Math.Ceiling(overlapDifference / 2.0);
        return new(blockOffset, extractedBlockSize);
    }

    public static IReadOnlyList<Nfiq2BlockOrigin> EnumerateInteriorBlockOrigins(
        int imageWidth,
        int imageHeight,
        int blockSize,
        int slantedBlockWidth,
        int slantedBlockHeight)
    {
        var geometry = GetOverlappingBlockGeometry(blockSize, slantedBlockWidth, slantedBlockHeight);
        var origins = new List<Nfiq2BlockOrigin>();
        for (var row = geometry.BlockOffset; row < imageHeight - (blockSize + geometry.BlockOffset - 1); row += blockSize)
        {
            for (var column = geometry.BlockOffset; column < imageWidth - (blockSize + geometry.BlockOffset - 1); column += blockSize)
            {
                origins.Add(new(row, column));
            }
        }

        return origins;
    }

    public static Nfiq2RidgeValleyFeatureContext CreateFeatureContext(
        Nfiq2FingerprintImage fingerprintImage,
        int blockSize,
        double segmentationThreshold,
        int slantedBlockWidth,
        int slantedBlockHeight)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        var geometry = GetOverlappingBlockGeometry(blockSize, slantedBlockWidth, slantedBlockHeight);
        var segmentationMask = Nfiq2BlockFeatureSupport.CreateSegmentationMask(fingerprintImage, blockSize, segmentationThreshold);
        var validOrigins = EnumerateInteriorBlockOrigins(
                fingerprintImage.Width,
                fingerprintImage.Height,
                blockSize,
                slantedBlockWidth,
                slantedBlockHeight)
            .Where(origin => Nfiq2BlockFeatureSupport.AreAllNonZero(
                segmentationMask,
                fingerprintImage.Width,
                origin.Row,
                origin.Column,
                blockSize,
                blockSize))
            .ToArray();

        return new(geometry, validOrigins);
    }

    public static byte[] ExtractBlock(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var block = new byte[blockWidth * blockHeight];
        for (var y = 0; y < blockHeight; y++)
        {
            var sourceOffset = (row + y) * imageWidth + column;
            image.Slice(sourceOffset, blockWidth).CopyTo(block.AsSpan(y * blockWidth, blockWidth));
        }

        return block;
    }

    public static byte[] GetRotatedBlock(
        ReadOnlySpan<byte> block,
        int blockWidth,
        int blockHeight,
        double orientation,
        bool padFlag)
    {
        ValidateEvenSquareBlock(blockWidth, blockHeight);

        if (!padFlag)
        {
            return RotateBlock(block, blockWidth, blockHeight, blockWidth, blockHeight, orientation);
        }

        var paddedWidth = blockWidth + s_rotationPaddingBorder * 2;
        var paddedHeight = blockHeight + s_rotationPaddingBorder * 2;
        var paddedBlock = new byte[paddedWidth * paddedHeight];

        for (var y = 0; y < blockHeight; y++)
        {
            var destinationOffset = (y + s_rotationPaddingBorder) * paddedWidth + s_rotationPaddingBorder;
            block.Slice(y * blockWidth, blockWidth).CopyTo(paddedBlock.AsSpan(destinationOffset, blockWidth));
        }

        return RotateBlock(paddedBlock, paddedWidth, paddedHeight, blockWidth, blockHeight, orientation);
    }

    public static byte[] GetRotatedBlock(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int sourceRow,
        int sourceColumn,
        int blockWidth,
        int blockHeight,
        double orientation,
        bool padFlag)
    {
        ValidateEvenSquareBlock(blockWidth, blockHeight);

        if (imageWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Image width must be positive.");
        }

        var imageHeight = image.Length / imageWidth;
        var sourceWidth = blockWidth + (padFlag ? s_rotationPaddingBorder * 2 : 0);
        var sourceHeight = blockHeight + (padFlag ? s_rotationPaddingBorder * 2 : 0);
        var sourceBlock = ExtractBlockWithZeroPadding(
            image,
            imageWidth,
            imageHeight,
            sourceRow - (padFlag ? s_rotationPaddingBorder : 0),
            sourceColumn - (padFlag ? s_rotationPaddingBorder : 0),
            sourceWidth,
            sourceHeight);
        return RotateBlock(sourceBlock, sourceWidth, sourceHeight, blockWidth, blockHeight, orientation);
    }

    public static byte[] GetCenteredRotatedBlock(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int sourceRow,
        int sourceColumn,
        int blockWidth,
        int blockHeight,
        int croppedWidth,
        int croppedHeight,
        double orientation,
        bool padFlag)
    {
        ValidateEvenSquareBlock(blockWidth, blockHeight);

        if (imageWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Image width must be positive.");
        }

        var imageHeight = image.Length / imageWidth;
        var sourceWidth = blockWidth + (padFlag ? s_rotationPaddingBorder * 2 : 0);
        var sourceHeight = blockHeight + (padFlag ? s_rotationPaddingBorder * 2 : 0);
        var sourceBlock = ExtractBlockWithZeroPadding(
            image,
            imageWidth,
            imageHeight,
            sourceRow - (padFlag ? s_rotationPaddingBorder : 0),
            sourceColumn - (padFlag ? s_rotationPaddingBorder : 0),
            sourceWidth,
            sourceHeight);
        return RotateCenteredRegion(sourceBlock, sourceWidth, sourceHeight, blockWidth, blockHeight, croppedWidth, croppedHeight, orientation);
    }

    private static byte[] RotateBlock(
        ReadOnlySpan<byte> sourceBlock,
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        double orientation)
    {
        ValidateEvenSquareBlock(destinationWidth, destinationHeight);

        var inverseMatrix = CreateInverseAffineMatrix(
            sourceWidth,
            sourceHeight,
            destinationWidth,
            destinationHeight,
            orientation);
        var adelta = destinationWidth <= 128 ? stackalloc int[destinationWidth] : new int[destinationWidth];
        var bdelta = destinationWidth <= 128 ? stackalloc int[destinationWidth] : new int[destinationWidth];
        for (var x = 0; x < destinationWidth; x++)
        {
            adelta[x] = CvRound(inverseMatrix.M00 * x * s_affineScale);
            bdelta[x] = CvRound(inverseMatrix.M10 * x * s_affineScale);
        }

        var rotatedBlock = new byte[destinationWidth * destinationHeight];
        var roundDelta = s_affineScale / 2;
        for (var y = 0; y < destinationHeight; y++)
        {
            var x0 = CvRound((inverseMatrix.M01 * y + inverseMatrix.M02) * s_affineScale) + roundDelta;
            var y0 = CvRound((inverseMatrix.M11 * y + inverseMatrix.M12) * s_affineScale) + roundDelta;
            var destinationOffset = y * destinationWidth;

            for (var x = 0; x < destinationWidth; x++)
            {
                var sourceX = (x0 + adelta[x]) >> s_affineBits;
                var sourceY = (y0 + bdelta[x]) >> s_affineBits;
                if ((uint)sourceX < (uint)sourceWidth && (uint)sourceY < (uint)sourceHeight)
                {
                    rotatedBlock[destinationOffset + x] = sourceBlock[sourceY * sourceWidth + sourceX];
                }
            }
        }

        return rotatedBlock;
    }

    private static byte[] RotateCenteredRegion(
        ReadOnlySpan<byte> sourceBlock,
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        int croppedWidth,
        int croppedHeight,
        double orientation)
    {
        ValidateEvenSquareBlock(destinationWidth, destinationHeight);

        GetCenteredCropOrigin(destinationWidth, destinationHeight, croppedWidth, croppedHeight, out var rowStart, out var columnStart);

        var inverseMatrix = CreateInverseAffineMatrix(
            sourceWidth,
            sourceHeight,
            destinationWidth,
            destinationHeight,
            orientation);
        var adelta = croppedWidth <= 128 ? stackalloc int[croppedWidth] : new int[croppedWidth];
        var bdelta = croppedWidth <= 128 ? stackalloc int[croppedWidth] : new int[croppedWidth];
        for (var x = 0; x < croppedWidth; x++)
        {
            var fullDestinationX = x + columnStart;
            adelta[x] = CvRound(inverseMatrix.M00 * fullDestinationX * s_affineScale);
            bdelta[x] = CvRound(inverseMatrix.M10 * fullDestinationX * s_affineScale);
        }

        var croppedBlock = new byte[croppedWidth * croppedHeight];
        var roundDelta = s_affineScale / 2;
        for (var y = 0; y < croppedHeight; y++)
        {
            var fullDestinationY = y + rowStart;
            var x0 = CvRound((inverseMatrix.M01 * fullDestinationY + inverseMatrix.M02) * s_affineScale) + roundDelta;
            var y0 = CvRound((inverseMatrix.M11 * fullDestinationY + inverseMatrix.M12) * s_affineScale) + roundDelta;
            var destinationOffset = y * croppedWidth;

            for (var x = 0; x < croppedWidth; x++)
            {
                var sourceX = (x0 + adelta[x]) >> s_affineBits;
                var sourceY = (y0 + bdelta[x]) >> s_affineBits;
                if ((uint)sourceX < (uint)sourceWidth && (uint)sourceY < (uint)sourceHeight)
                {
                    croppedBlock[destinationOffset + x] = sourceBlock[sourceY * sourceWidth + sourceX];
                }
            }
        }

        return croppedBlock;
    }

    public static byte[] CropCenteredRotatedBlock(
        ReadOnlySpan<byte> rotatedBlock,
        int rotatedBlockWidth,
        int rotatedBlockHeight,
        int croppedWidth,
        int croppedHeight)
    {
        ValidateEvenSquareBlock(rotatedBlockWidth, rotatedBlockHeight);

        GetCenteredCropOrigin(rotatedBlockWidth, rotatedBlockHeight, croppedWidth, croppedHeight, out var rowStart, out var columnStart);
        return ExtractBlock(rotatedBlock, rotatedBlockWidth, rowStart, columnStart, croppedWidth, croppedHeight);
    }

    public static Nfiq2RidgeValleyStructureResult GetRidgeValleyStructure(
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

        var columnMeans = new double[width];
        for (var column = 0; column < width; column++)
        {
            var sum = 0.0;
            for (var row = 0; row < height; row++)
            {
                sum += blockCropped[row * width + column];
            }

            columnMeans[column] = sum / height;
        }

        var sampleCount = (double)width;
        var sumX = sampleCount * (sampleCount + 1.0) / 2.0;
        var sumXx = sampleCount * (sampleCount + 1.0) * (2.0 * sampleCount + 1.0) / 6.0;

        var sumY = 0.0;
        var sumXy = 0.0;
        for (var index = 0; index < columnMeans.Length; index++)
        {
            var x = index + 1.0;
            var y = columnMeans[index];
            sumY += y;
            sumXy += x * y;
        }

        var denominator = sampleCount * sumXx - sumX * sumX;
        var slope = Math.Abs(denominator) <= double.Epsilon
            ? 0.0
            : (sampleCount * sumXy - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / sampleCount;

        slope = RoundToTenDecimalPlacesAwayFromZero(slope);
        intercept = RoundToTenDecimalPlacesAwayFromZero(intercept);

        var trendLine = new double[columnMeans.Length];
        var ridgeValleyPattern = new byte[columnMeans.Length];
        for (var index = 0; index < trendLine.Length; index++)
        {
            var expected = (index + 1.0) * slope + intercept;
            trendLine[index] = expected;
            ridgeValleyPattern[index] = columnMeans[index] < expected ? (byte)1 : (byte)0;
        }

        return new(columnMeans, trendLine, ridgeValleyPattern);
    }

    public static IReadOnlyList<double> ComputeRidgeValleyUniformityRatios(ReadOnlySpan<byte> ridgeValleyPattern)
    {
        if (ridgeValleyPattern.IsEmpty)
        {
            return Array.Empty<double>();
        }

        var changeIndex = ComputeChangeIndices(ridgeValleyPattern);
        if (changeIndex.Count == 0)
        {
            return Array.Empty<double>();
        }

        var ridgeValleyComplete = new List<byte>();
        for (var index = changeIndex[0] + 1; index < changeIndex[^1]; index++)
        {
            ridgeValleyComplete.Add(ridgeValleyPattern[index]);
        }

        if (ridgeValleyComplete.Count == 0)
        {
            return Array.Empty<double>();
        }

        var changeIndexComplete = new List<int>();
        for (var index = 1; index < changeIndex.Count; index++)
        {
            changeIndexComplete.Add(changeIndex[index] - changeIndex[0]);
        }

        if (changeIndexComplete.Count <= 1)
        {
            return Array.Empty<double>();
        }

        var changeWidths = new List<int>(changeIndexComplete.Count - 1);
        for (var index = changeIndexComplete.Count - 1; index > 0; index--)
        {
            changeWidths.Add(changeIndexComplete[index] - changeIndexComplete[index - 1]);
        }

        if (changeWidths.Count <= 1)
        {
            return Array.Empty<double>();
        }

        var ratios = new List<double>(changeWidths.Count - 1);
        for (var index = 0; index < changeWidths.Count - 1; index++)
        {
            ratios.Add(changeWidths[index] / (double)changeWidths[index + 1]);
        }

        var begrid = ridgeValleyComplete[0];
        for (var index = begrid; index < ratios.Count; index += 2)
        {
            ratios[index] = 1.0 / ratios[index];
        }

        return ratios;
    }

    public static double ComputeLocalClarityScore(
        ReadOnlySpan<byte> blockCropped,
        int width,
        int height,
        ReadOnlySpan<byte> ridgeValleyPattern,
        ReadOnlySpan<double> trendLine,
        int scannerResolution)
    {
        if (ridgeValleyPattern.IsEmpty || trendLine.IsEmpty)
        {
            return 0.0;
        }

        var changeIndex = ComputeChangeIndices(ridgeValleyPattern);
        if (changeIndex.Count == 0)
        {
            return 0.0;
        }

        var widths = new List<int> { changeIndex[0] };
        for (var index = 1; index < changeIndex.Count; index++)
        {
            widths.Add(changeIndex[index] - changeIndex[index - 1]);
        }

        var beginsWithRidge = ridgeValleyPattern[0] == 1;
        var ridgeWidths = new List<double>();
        var valleyWidths = new List<double>();
        var ridgeScale = scannerResolution / s_scannerNormalizationBase * s_ridgeWidthMaxAt125Ppi;
        var valleyScale = scannerResolution / s_scannerNormalizationBase * s_valleyWidthMaxAt125Ppi;

        if (beginsWithRidge)
        {
            for (var index = 0; index < widths.Count; index += 2)
            {
                ridgeWidths.Add(widths[index] / ridgeScale);
            }

            for (var index = 0; index < widths.Count - 1; index += 2)
            {
                valleyWidths.Add(widths[index + 1] / valleyScale);
            }
        }
        else
        {
            for (var index = 0; index < widths.Count; index += 2)
            {
                valleyWidths.Add(widths[index] / valleyScale);
            }

            for (var index = 0; index < widths.Count - 1; index += 2)
            {
                ridgeWidths.Add(widths[index + 1] / ridgeScale);
            }
        }

        var ridgeMean = ridgeWidths.Count == 0 ? 0.0 : ridgeWidths.Average();
        var valleyMean = valleyWidths.Count == 0 ? 0.0 : valleyWidths.Average();

        var normalizedRidgeMin = s_ridgeWidthMin / ridgeScale;
        var normalizedRidgeMax = s_ridgeWidthMax / ridgeScale;
        var normalizedValleyMin = s_valleyWidthMin / ridgeScale;
        var normalizedValleyMax = s_valleyWidthMax / ridgeScale;

        if (ridgeMean < normalizedRidgeMin || ridgeMean > normalizedRidgeMax
            || valleyMean < normalizedValleyMin || valleyMean > normalizedValleyMax)
        {
            return 0.0;
        }

        var ridgeGood = 0;
        var valleyGood = 0;
        var ridgePixelCount = 0;
        var valleyPixelCount = 0;
        for (var column = 0; column < width; column++)
        {
            var threshold = trendLine[column];
            if (ridgeValleyPattern[column] == 1)
            {
                for (var row = 0; row < height; row++)
                {
                    if (blockCropped[row * width + column] >= threshold)
                    {
                        ridgeGood++;
                    }
                }

                ridgePixelCount += height;
            }
            else
            {
                for (var row = 0; row < height; row++)
                {
                    if (blockCropped[row * width + column] < threshold)
                    {
                        valleyGood++;
                    }
                }

                valleyPixelCount += height;
            }
        }

        if (ridgePixelCount == 0 || valleyPixelCount == 0)
        {
            return 0.0;
        }

        var alpha = valleyGood / (double)valleyPixelCount;
        var beta = ridgeGood / (double)ridgePixelCount;
        return 1.0 - (alpha + beta) / 2.0;
    }

    private static Nfiq2AffineMatrix CreateInverseAffineMatrix(
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        double orientation)
    {
        _ = destinationWidth;
        _ = destinationHeight;

        var angleRadians = orientation;
        var alpha = Math.Cos(angleRadians);
        var beta = Math.Sin(angleRadians);
        var sourceCenterX = sourceWidth / 2.0;
        var sourceCenterY = sourceHeight / 2.0;

        // OpenCV builds the forward transform in the source coordinate space and
        // then internally inverts it for warpAffine when WARP_INVERSE_MAP is not set.
        var m00 = alpha;
        var m01 = beta;
        var m02 = (1.0 - alpha) * sourceCenterX - beta * sourceCenterY;
        var m10 = -beta;
        var m11 = alpha;
        var m12 = beta * sourceCenterX + (1.0 - alpha) * sourceCenterY;

        var determinant = m00 * m11 - m01 * m10;
        var inverseDeterminant = Math.Abs(determinant) > double.Epsilon ? 1.0 / determinant : 0.0;
        var inverse00 = m11 * inverseDeterminant;
        var inverse11 = m00 * inverseDeterminant;
        var inverse01 = m01 * -inverseDeterminant;
        var inverse10 = m10 * -inverseDeterminant;
        var inverse02 = -inverse00 * m02 - inverse01 * m12;
        var inverse12 = -inverse10 * m02 - inverse11 * m12;

        return new(inverse00, inverse01, inverse02, inverse10, inverse11, inverse12);
    }

    private static void ValidateEvenSquareBlock(int blockWidth, int blockHeight)
    {
        if (blockWidth != blockHeight || (blockWidth & 1) != 0)
        {
            throw new Nfiq2Exception(
                $"Wrong block size! Consider block with size of even number (block rows = {blockHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }
    }

    private static int CvRound(double value)
    {
        return (int)Math.Round(value, MidpointRounding.ToEven);
    }

    private static void GetCenteredCropOrigin(
        int fullWidth,
        int fullHeight,
        int croppedWidth,
        int croppedHeight,
        out int rowStart,
        out int columnStart)
    {
        var center = fullHeight / 2;
        rowStart = center - (croppedHeight / 2 - 1) - 1;
        columnStart = fullWidth / 2 - (croppedWidth / 2 - 1) - 1;
    }

    private static byte[] ExtractBlockWithZeroPadding(
        ReadOnlySpan<byte> image,
        int imageWidth,
        int imageHeight,
        int row,
        int column,
        int blockWidth,
        int blockHeight)
    {
        var block = new byte[blockWidth * blockHeight];
        for (var y = 0; y < blockHeight; y++)
        {
            var sourceRow = row + y;
            if ((uint)sourceRow >= (uint)imageHeight)
            {
                continue;
            }

            var sourceColumn = Math.Max(0, column);
            var destinationColumn = Math.Max(0, -column);
            var copyLength = Math.Min(blockWidth - destinationColumn, imageWidth - sourceColumn);
            if (copyLength <= 0)
            {
                continue;
            }

            var sourceOffset = sourceRow * imageWidth + sourceColumn;
            image.Slice(sourceOffset, copyLength).CopyTo(block.AsSpan(y * blockWidth + destinationColumn, copyLength));
        }

        return block;
    }

    private static List<int> ComputeChangeIndices(ReadOnlySpan<byte> ridgeValleyPattern)
    {
        var change = new byte[ridgeValleyPattern.Length];
        for (var index = 0; index < ridgeValleyPattern.Length; index++)
        {
            var previousIndex = index == 0 ? ridgeValleyPattern.Length - 1 : index - 1;
            change[index] = ridgeValleyPattern[index] != ridgeValleyPattern[previousIndex] ? (byte)1 : (byte)0;
        }

        var changeIndex = new List<int>();
        for (var index = 1; index < change.Length; index++)
        {
            if (change[index] == 1)
            {
                changeIndex.Add(index - 1);
            }
        }

        return changeIndex;
    }

    private static double RoundToTenDecimalPlacesAwayFromZero(double value)
    {
        return Math.Round(value * 10000000000.0, MidpointRounding.AwayFromZero) / 10000000000.0;
    }
}

internal sealed record Nfiq2BlockGeometry(
    int BlockOffset,
    int ExtractedBlockSize);

internal sealed record Nfiq2BlockOrigin(
    int Row,
    int Column);

internal readonly record struct Nfiq2AffineMatrix(
    double M00,
    double M01,
    double M02,
    double M10,
    double M11,
    double M12);

internal sealed record Nfiq2RidgeValleyStructureResult(
    double[] ColumnMeans,
    double[] TrendLine,
    byte[] RidgeValleyPattern);

internal sealed record Nfiq2RidgeValleyFeatureContext(
    Nfiq2BlockGeometry Geometry,
    IReadOnlyList<Nfiq2BlockOrigin> ValidOrigins);
