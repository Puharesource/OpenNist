namespace OpenNist.Wsq.Internal.Decoding;

using System.Collections.Concurrent;
using OpenNist.Wsq.Internal.Metadata;

internal static class WsqWaveletTreeBuilder
{
    private static readonly ConcurrentDictionary<ulong, WsqTreeLayout> s_layoutCache = new();

    public static void Build(
        int width,
        int height,
        out WsqWaveletNode[] waveletTree,
        out WsqQuantizationNode[] quantizationTree)
    {
        var layout = s_layoutCache.GetOrAdd(CreateCacheKey(width, height), static key =>
        {
            var layoutWidth = (int)(key >> 32);
            var layoutHeight = (int)(key & uint.MaxValue);
            var layoutWaveletTree = new WsqWaveletNode[WsqConstants.WaveletTreeLength];
            var layoutQuantizationTree = new WsqQuantizationNode[WsqConstants.QuantizationTreeLength];

            BuildWaveletTree(layoutWaveletTree, layoutWidth, layoutHeight);
            BuildQuantizationTree(layoutWaveletTree, layoutQuantizationTree);

            return new(layoutWaveletTree, layoutQuantizationTree);
        });

        waveletTree = layout.WaveletTree;
        quantizationTree = layout.QuantizationTree;
    }

    private static ulong CreateCacheKey(int width, int height)
    {
        return ((ulong)(uint)width << 32) | (uint)height;
    }

    private static void BuildWaveletTree(WsqWaveletNode[] waveletTree, int width, int height)
    {
        SetWaveletInversionFlags(waveletTree);

        SplitWaveletRegion(waveletTree, 0, 1, width, height, 0, 0, stopAtFirstLevel: true);

        var (leftHalfWidth, rightHalfWidth) = SplitLength(waveletTree[1].Width, preferSecondHalfOnOdd: false);
        var (upperHalfHeight, lowerHalfHeight) = SplitLength(waveletTree[1].Height, preferSecondHalfOnOdd: false);

        SplitWaveletRegion(waveletTree, 4, 6, rightHalfWidth, upperHalfHeight, leftHalfWidth, 0, stopAtFirstLevel: false);
        SplitWaveletRegion(waveletTree, 5, 10, leftHalfWidth, lowerHalfHeight, 0, upperHalfHeight, stopAtFirstLevel: false);
        SplitWaveletRegion(waveletTree, 14, 15, leftHalfWidth, upperHalfHeight, 0, 0, stopAtFirstLevel: false);

        waveletTree[19].X = 0;
        waveletTree[19].Y = 0;
        waveletTree[19].Width = waveletTree[15].Width % 2 == 0
            ? waveletTree[15].Width / 2
            : (waveletTree[15].Width + 1) / 2;

        waveletTree[19].Height = waveletTree[15].Height % 2 == 0
            ? waveletTree[15].Height / 2
            : (waveletTree[15].Height + 1) / 2;
    }

    private static void SetWaveletInversionFlags(WsqWaveletNode[] waveletTree)
    {
        SetRowInversion(waveletTree, 2, 4, 7, 9, 11, 13, 16, 18);
        SetColumnInversion(waveletTree, 3, 5, 8, 9, 12, 13, 17, 18);
    }

    private static void SetRowInversion(WsqWaveletNode[] waveletTree, params ReadOnlySpan<int> indices)
    {
        foreach (var index in indices)
        {
            waveletTree[index].InvertRows = true;
        }
    }

    private static void SetColumnInversion(WsqWaveletNode[] waveletTree, params ReadOnlySpan<int> indices)
    {
        foreach (var index in indices)
        {
            waveletTree[index].InvertColumns = true;
        }
    }

    private static void SplitWaveletRegion(
        WsqWaveletNode[] waveletTree,
        int parentIndex,
        int childStartIndex,
        int width,
        int height,
        int x,
        int y,
        bool stopAtFirstLevel)
    {
        var evenWidth = width % 2;
        var evenHeight = height % 2;

        waveletTree[parentIndex].X = x;
        waveletTree[parentIndex].Y = y;
        waveletTree[parentIndex].Width = width;
        waveletTree[parentIndex].Height = height;

        waveletTree[childStartIndex].X = x;
        waveletTree[childStartIndex + 2].X = x;
        waveletTree[childStartIndex].Y = y;
        waveletTree[childStartIndex + 1].Y = y;

        if (evenWidth == 0)
        {
            waveletTree[childStartIndex].Width = width / 2;
            waveletTree[childStartIndex + 1].Width = waveletTree[childStartIndex].Width;
        }
        else if (parentIndex == 4)
        {
            waveletTree[childStartIndex].Width = (width - 1) / 2;
            waveletTree[childStartIndex + 1].Width = waveletTree[childStartIndex].Width + 1;
        }
        else
        {
            waveletTree[childStartIndex].Width = (width + 1) / 2;
            waveletTree[childStartIndex + 1].Width = waveletTree[childStartIndex].Width - 1;
        }

        waveletTree[childStartIndex + 1].X = waveletTree[childStartIndex].Width + x;

        if (!stopAtFirstLevel)
        {
            waveletTree[childStartIndex + 3].Width = waveletTree[childStartIndex + 1].Width;
            waveletTree[childStartIndex + 3].X = waveletTree[childStartIndex + 1].X;
        }

        waveletTree[childStartIndex + 2].Width = waveletTree[childStartIndex].Width;

        if (evenHeight == 0)
        {
            waveletTree[childStartIndex].Height = height / 2;
            waveletTree[childStartIndex + 2].Height = waveletTree[childStartIndex].Height;
        }
        else if (parentIndex == 5)
        {
            waveletTree[childStartIndex].Height = (height - 1) / 2;
            waveletTree[childStartIndex + 2].Height = waveletTree[childStartIndex].Height + 1;
        }
        else
        {
            waveletTree[childStartIndex].Height = (height + 1) / 2;
            waveletTree[childStartIndex + 2].Height = waveletTree[childStartIndex].Height - 1;
        }

        waveletTree[childStartIndex + 2].Y = waveletTree[childStartIndex].Height + y;

        if (!stopAtFirstLevel)
        {
            waveletTree[childStartIndex + 3].Height = waveletTree[childStartIndex + 2].Height;
            waveletTree[childStartIndex + 3].Y = waveletTree[childStartIndex + 2].Y;
        }

        waveletTree[childStartIndex + 1].Height = waveletTree[childStartIndex].Height;
    }

    private static void BuildQuantizationTree(
        WsqWaveletNode[] waveletTree,
        WsqQuantizationNode[] quantizationTree)
    {
        SplitQuantizationRegion16(
            quantizationTree,
            3,
            waveletTree[14].Width,
            waveletTree[14].Height,
            waveletTree[14].X,
            waveletTree[14].Y,
            invertRows: false,
            invertColumns: false);

        SplitQuantizationRegion16(
            quantizationTree,
            19,
            waveletTree[4].Width,
            waveletTree[4].Height,
            waveletTree[4].X,
            waveletTree[4].Y,
            invertRows: false,
            invertColumns: true);

        SplitQuantizationRegion16(
            quantizationTree,
            48,
            waveletTree[0].Width,
            waveletTree[0].Height,
            waveletTree[0].X,
            waveletTree[0].Y,
            invertRows: false,
            invertColumns: false);

        SplitQuantizationRegion16(
            quantizationTree,
            35,
            waveletTree[5].Width,
            waveletTree[5].Height,
            waveletTree[5].X,
            waveletTree[5].Y,
            invertRows: true,
            invertColumns: false);

        SplitQuantizationRegion4(
            quantizationTree,
            0,
            waveletTree[19].Width,
            waveletTree[19].Height,
            waveletTree[19].X,
            waveletTree[19].Y);
    }

    private static void SplitQuantizationRegion16(
        WsqQuantizationNode[] quantizationTree,
        int startIndex,
        int width,
        int height,
        int x,
        int y,
        bool invertRows,
        bool invertColumns)
    {
        var (upperWidth, lowerWidth) = SplitLength(width, preferSecondHalfOnOdd: invertColumns);
        var (upperHeight, lowerHeight) = SplitLength(height, preferSecondHalfOnOdd: invertRows);
        var (upperLeftWidth, upperRightWidth) = SplitLength(upperWidth, preferSecondHalfOnOdd: false);
        var (upperTopHeight, upperBottomHeight) = SplitLength(upperHeight, preferSecondHalfOnOdd: false);
        var (lowerLeftWidth, lowerRightWidth) = SplitLength(lowerWidth, preferSecondHalfOnOdd: true);
        var (lowerTopHeight, lowerBottomHeight) = SplitLength(lowerHeight, preferSecondHalfOnOdd: true);

        SetTwoByTwoRegion(
            quantizationTree,
            startIndex,
            x,
            y,
            upperLeftWidth,
            upperRightWidth,
            upperTopHeight,
            upperBottomHeight);

        SetTwoByTwoRegion(
            quantizationTree,
            startIndex + 4,
            x + upperWidth,
            y,
            lowerLeftWidth,
            lowerRightWidth,
            upperTopHeight,
            upperBottomHeight);

        SetTwoByTwoRegion(
            quantizationTree,
            startIndex + 8,
            x,
            y + upperHeight,
            upperLeftWidth,
            upperRightWidth,
            lowerTopHeight,
            lowerBottomHeight);

        SetTwoByTwoRegion(
            quantizationTree,
            startIndex + 12,
            x + upperWidth,
            y + upperHeight,
            lowerLeftWidth,
            lowerRightWidth,
            lowerTopHeight,
            lowerBottomHeight);
    }

    private static void SplitQuantizationRegion4(
        WsqQuantizationNode[] quantizationTree,
        int startIndex,
        int width,
        int height,
        int x,
        int y)
    {
        var (leftWidth, rightWidth) = SplitLength(width, preferSecondHalfOnOdd: false);
        var (topHeight, bottomHeight) = SplitLength(height, preferSecondHalfOnOdd: false);
        SetTwoByTwoRegion(quantizationTree, startIndex, x, y, leftWidth, rightWidth, topHeight, bottomHeight);
    }

    private static (int FirstHalfLength, int SecondHalfLength) SplitLength(
        int length,
        bool preferSecondHalfOnOdd)
    {
        if (length % 2 == 0)
        {
            var halfLength = length / 2;
            return (halfLength, halfLength);
        }

        var largerHalfLength = (length + 1) / 2;
        return preferSecondHalfOnOdd
            ? (largerHalfLength - 1, largerHalfLength)
            : (largerHalfLength, largerHalfLength - 1);
    }

    private static void SetTwoByTwoRegion(
        WsqQuantizationNode[] quantizationTree,
        int startIndex,
        int x,
        int y,
        int leftWidth,
        int rightWidth,
        int topHeight,
        int bottomHeight)
    {
        SetQuantizationNode(quantizationTree, startIndex, x, y, leftWidth, topHeight);
        SetQuantizationNode(quantizationTree, startIndex + 1, x + leftWidth, y, rightWidth, topHeight);
        SetQuantizationNode(quantizationTree, startIndex + 2, x, y + topHeight, leftWidth, bottomHeight);
        SetQuantizationNode(quantizationTree, startIndex + 3, x + leftWidth, y + topHeight, rightWidth, bottomHeight);
    }

    private static void SetQuantizationNode(
        WsqQuantizationNode[] quantizationTree,
        int index,
        int x,
        int y,
        int width,
        int height)
    {
        quantizationTree[index].X = x;
        quantizationTree[index].Y = y;
        quantizationTree[index].Width = width;
        quantizationTree[index].Height = height;
    }
}

internal readonly record struct WsqTreeLayout(
    WsqWaveletNode[] WaveletTree,
    WsqQuantizationNode[] QuantizationTree);

internal struct WsqWaveletNode
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public bool InvertRows;
    public bool InvertColumns;
}

internal struct WsqQuantizationNode
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
}
