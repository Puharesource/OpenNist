namespace OpenNist.Nfiq.Internal.Modules;

using OpenNist.Nfiq.Errors;
using OpenNist.Nfiq.Internal.Model;
using OpenNist.Nfiq.Internal.Support;

internal static class Nfiq2OrientationFlowModule
{
    private const int s_localRegionSquare = 32;
    private const int s_slantedBlockSizeX = 32;
    private const int s_slantedBlockSizeY = 16;
    private const double s_segmentationThreshold = 0.1;
    private const double s_angleMinDegrees = 4.0;
    private const string s_featurePrefix = "OF_Bin10_";
    private static ReadOnlySpan<double> HistogramBoundaries => [1.715e-2, 3.5e-2, 5.57e-2, 8.1e-2, 1.15e-1, 1.718e-1, 2.569e-1, 4.758e-1, 7.48e-1];

    public static Nfiq2OrientationFlowResult Compute(Nfiq2FingerprintImage fingerprintImage)
    {
        ArgumentNullException.ThrowIfNull(fingerprintImage);

        if (fingerprintImage.PixelsPerInch != Nfiq2FingerprintImage.Resolution500Ppi)
        {
            throw new Nfiq2Exception("Only 500 dpi fingerprint images are supported!");
        }

        var segmentationMask = Nfiq2BlockFeatureSupport.CreateSegmentationMask(
            fingerprintImage,
            s_localRegionSquare,
            s_segmentationThreshold);

        var blocks = EnumerateInteriorBlockGrid(fingerprintImage, segmentationMask);
        var loqAll = ComputeLocalOrientationQualityMap(blocks);
        var maskBloqSeg = ComputeForegroundNeighborhoodMask(blocks);

        var angleMinRadians = s_angleMinDegrees * (Math.PI / 180.0);
        var angleDiff = (90.0 - s_angleMinDegrees) * Math.PI / 180.0;

        var values = new List<double>(loqAll.Length);
        for (var index = 0; index < loqAll.Length; index++)
        {
            if (!maskBloqSeg[index])
            {
                continue;
            }

            var loq = loqAll[index];
            if (loq <= angleMinRadians)
            {
                continue;
            }

            values.Add((loq - angleMinRadians) / angleDiff);
        }

        var valueArray = values.ToArray();
        var features = Nfiq2FeatureMath.CreateHistogramFeatures(s_featurePrefix, HistogramBoundaries, valueArray, 10);
        return new(valueArray, features);
    }

    private static Nfiq2OrientationBlock[] EnumerateInteriorBlockGrid(
        Nfiq2FingerprintImage fingerprintImage,
        ReadOnlySpan<byte> segmentationMask)
    {
        var sumSquare = s_slantedBlockSizeX * s_slantedBlockSizeX + s_slantedBlockSizeY * s_slantedBlockSizeY;
        var extractedBlockSize = Math.Ceiling(Math.Sqrt(sumSquare));
        var overlapDifference = extractedBlockSize - s_localRegionSquare;
        var blockOffset = (int)Math.Ceiling(overlapDifference / 2.0);

        var blockRows = 0;
        for (var row = blockOffset; row < fingerprintImage.Height - (s_localRegionSquare + blockOffset - 1); row += s_localRegionSquare)
        {
            blockRows++;
        }

        var blockColumns = 0;
        for (var column = blockOffset; column < fingerprintImage.Width - (s_localRegionSquare + blockOffset - 1); column += s_localRegionSquare)
        {
            blockColumns++;
        }

        var blocks = new Nfiq2OrientationBlock[blockRows * blockColumns];

        var blockRow = 0;
        for (var row = blockOffset; row < fingerprintImage.Height - (s_localRegionSquare + blockOffset - 1); row += s_localRegionSquare)
        {
            var blockColumn = 0;
            for (var column = blockOffset; column < fingerprintImage.Width - (s_localRegionSquare + blockOffset - 1); column += s_localRegionSquare)
            {
                var allNonZero = Nfiq2BlockFeatureSupport.AreAllNonZero(
                    segmentationMask,
                    fingerprintImage.Width,
                    row,
                    column,
                    s_localRegionSquare,
                    s_localRegionSquare);

                var orientation = Nfiq2BlockFeatureSupport.ComputeRidgeOrientation(
                    fingerprintImage.Pixels.Span,
                    fingerprintImage.Width,
                    row,
                    column,
                    s_localRegionSquare,
                    s_localRegionSquare);

                blocks[GetBlockIndex(blockRow, blockColumn, blockColumns)] = new(blockRow, blockColumn, allNonZero, orientation);
                blockColumn++;
            }

            blockRow++;
        }

        return blocks;
    }

    private static double[] ComputeLocalOrientationQualityMap(IReadOnlyList<Nfiq2OrientationBlock> blocks)
    {
        var rows = blocks[^1].BlockRow + 1;
        var columns = blocks[^1].BlockColumn + 1;
        var padded = new double[(rows + 2) * (columns + 2)];

        foreach (var block in blocks)
        {
            padded[GetPaddedIndex(block.BlockRow + 1, block.BlockColumn + 1, columns)] = block.Orientation;
        }

        var threeSixtyRadians = Math.PI * 2.0;
        var result = new double[rows * columns];

        for (var row = 1; row <= rows; row++)
        {
            for (var column = 1; column <= columns; column++)
            {
                var center = padded[GetPaddedIndex(row, column, columns)];
                var sum = 0.0;
                for (var y = row - 1; y <= row + 1; y++)
                {
                    for (var x = column - 1; x <= column + 1; x++)
                    {
                        var angleDiff = Math.Abs(center - padded[GetPaddedIndex(y, x, columns)]);
                        angleDiff = Math.Min(angleDiff, threeSixtyRadians - angleDiff);
                        sum += angleDiff;
                    }
                }

                result[GetBlockIndex(row - 1, column - 1, columns)] = sum / 8.0;
            }
        }

        return result;
    }

    private static bool[] ComputeForegroundNeighborhoodMask(IReadOnlyList<Nfiq2OrientationBlock> blocks)
    {
        var rows = blocks[^1].BlockRow + 1;
        var columns = blocks[^1].BlockColumn + 1;
        var padded = new bool[(rows + 2) * (columns + 2)];

        foreach (var block in blocks)
        {
            padded[GetPaddedIndex(block.BlockRow + 1, block.BlockColumn + 1, columns)] = block.AllNonZero;
        }

        var result = new bool[rows * columns];
        for (var row = 1; row <= rows; row++)
        {
            for (var column = 1; column <= columns; column++)
            {
                var allNonZero = true;
                for (var y = row - 1; y <= row + 1 && allNonZero; y++)
                {
                    for (var x = column - 1; x <= column + 1; x++)
                    {
                        if (!padded[GetPaddedIndex(y, x, columns)])
                        {
                            allNonZero = false;
                            break;
                        }
                    }
                }

                result[GetBlockIndex(row - 1, column - 1, columns)] = allNonZero;
            }
        }

        return result;
    }

    private static int GetBlockIndex(int row, int column, int blockColumns)
    {
        return row * blockColumns + column;
    }

    private static int GetPaddedIndex(int row, int column, int blockColumns)
    {
        return row * (blockColumns + 2) + column;
    }
}

internal sealed record Nfiq2OrientationFlowResult(
    IReadOnlyList<double> Values,
    IReadOnlyDictionary<string, double> Features);

internal sealed record Nfiq2OrientationBlock(
    int BlockRow,
    int BlockColumn,
    bool AllNonZero,
    double Orientation);
