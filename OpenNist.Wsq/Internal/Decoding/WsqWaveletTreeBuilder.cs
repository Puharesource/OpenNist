namespace OpenNist.Wsq.Internal.Decoding;

internal static class WsqWaveletTreeBuilder
{
    public static void Build(
        int width,
        int height,
        out WsqWaveletNode[] waveletTree,
        out WsqQuantizationNode[] quantizationTree)
    {
        waveletTree = new WsqWaveletNode[WsqConstants.WaveletTreeLength];
        quantizationTree = new WsqQuantizationNode[WsqConstants.QuantizationTreeLength];

        BuildWaveletTree(waveletTree, width, height);
        BuildQuantizationTree(waveletTree, quantizationTree);
    }

    private static void BuildWaveletTree(WsqWaveletNode[] waveletTree, int width, int height)
    {
        waveletTree[2].InvertRows = true;
        waveletTree[4].InvertRows = true;
        waveletTree[7].InvertRows = true;
        waveletTree[9].InvertRows = true;
        waveletTree[11].InvertRows = true;
        waveletTree[13].InvertRows = true;
        waveletTree[16].InvertRows = true;
        waveletTree[18].InvertRows = true;
        waveletTree[3].InvertColumns = true;
        waveletTree[5].InvertColumns = true;
        waveletTree[8].InvertColumns = true;
        waveletTree[9].InvertColumns = true;
        waveletTree[12].InvertColumns = true;
        waveletTree[13].InvertColumns = true;
        waveletTree[17].InvertColumns = true;
        waveletTree[18].InvertColumns = true;

        SplitWaveletRegion(waveletTree, 0, 1, width, height, 0, 0, stopAtFirstLevel: true);

        int lenx;
        int lenx2;

        if (waveletTree[1].Width % 2 == 0)
        {
            lenx = waveletTree[1].Width / 2;
            lenx2 = lenx;
        }
        else
        {
            lenx = (waveletTree[1].Width + 1) / 2;
            lenx2 = lenx - 1;
        }

        int leny;
        int leny2;

        if (waveletTree[1].Height % 2 == 0)
        {
            leny = waveletTree[1].Height / 2;
            leny2 = leny;
        }
        else
        {
            leny = (waveletTree[1].Height + 1) / 2;
            leny2 = leny - 1;
        }

        SplitWaveletRegion(waveletTree, 4, 6, lenx2, leny, lenx, 0, stopAtFirstLevel: false);
        SplitWaveletRegion(waveletTree, 5, 10, lenx, leny2, 0, leny, stopAtFirstLevel: false);
        SplitWaveletRegion(waveletTree, 14, 15, lenx, leny, 0, 0, stopAtFirstLevel: false);

        waveletTree[19].X = 0;
        waveletTree[19].Y = 0;
        waveletTree[19].Width = waveletTree[15].Width % 2 == 0
            ? waveletTree[15].Width / 2
            : (waveletTree[15].Width + 1) / 2;

        waveletTree[19].Height = waveletTree[15].Height % 2 == 0
            ? waveletTree[15].Height / 2
            : (waveletTree[15].Height + 1) / 2;
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
        var evenWidth = width % 2;
        var evenHeight = height % 2;

        int upperWidth;
        int lowerWidth;

        if (evenWidth == 0)
        {
            upperWidth = width / 2;
            lowerWidth = upperWidth;
        }
        else if (invertColumns)
        {
            lowerWidth = (width + 1) / 2;
            upperWidth = lowerWidth - 1;
        }
        else
        {
            upperWidth = (width + 1) / 2;
            lowerWidth = upperWidth - 1;
        }

        int upperHeight;
        int lowerHeight;

        if (evenHeight == 0)
        {
            upperHeight = height / 2;
            lowerHeight = upperHeight;
        }
        else if (invertRows)
        {
            lowerHeight = (height + 1) / 2;
            upperHeight = lowerHeight - 1;
        }
        else
        {
            upperHeight = (height + 1) / 2;
            lowerHeight = upperHeight - 1;
        }

        var evenUpperWidth = upperWidth % 2;
        var evenUpperHeight = upperHeight % 2;
        var nodeIndex = startIndex;

        quantizationTree[nodeIndex].X = x;
        quantizationTree[nodeIndex + 2].X = x;
        quantizationTree[nodeIndex].Y = y;
        quantizationTree[nodeIndex + 1].Y = y;

        if (evenUpperWidth == 0)
        {
            quantizationTree[nodeIndex].Width = upperWidth / 2;
            quantizationTree[nodeIndex + 1].Width = quantizationTree[nodeIndex].Width;
            quantizationTree[nodeIndex + 2].Width = quantizationTree[nodeIndex].Width;
            quantizationTree[nodeIndex + 3].Width = quantizationTree[nodeIndex].Width;
        }
        else
        {
            quantizationTree[nodeIndex].Width = (upperWidth + 1) / 2;
            quantizationTree[nodeIndex + 1].Width = quantizationTree[nodeIndex].Width - 1;
            quantizationTree[nodeIndex + 2].Width = quantizationTree[nodeIndex].Width;
            quantizationTree[nodeIndex + 3].Width = quantizationTree[nodeIndex + 1].Width;
        }

        quantizationTree[nodeIndex + 1].X = x + quantizationTree[nodeIndex].Width;
        quantizationTree[nodeIndex + 3].X = quantizationTree[nodeIndex + 1].X;

        if (evenUpperHeight == 0)
        {
            quantizationTree[nodeIndex].Height = upperHeight / 2;
            quantizationTree[nodeIndex + 1].Height = quantizationTree[nodeIndex].Height;
            quantizationTree[nodeIndex + 2].Height = quantizationTree[nodeIndex].Height;
            quantizationTree[nodeIndex + 3].Height = quantizationTree[nodeIndex].Height;
        }
        else
        {
            quantizationTree[nodeIndex].Height = (upperHeight + 1) / 2;
            quantizationTree[nodeIndex + 1].Height = quantizationTree[nodeIndex].Height;
            quantizationTree[nodeIndex + 2].Height = quantizationTree[nodeIndex].Height - 1;
            quantizationTree[nodeIndex + 3].Height = quantizationTree[nodeIndex + 2].Height;
        }

        quantizationTree[nodeIndex + 2].Y = y + quantizationTree[nodeIndex].Height;
        quantizationTree[nodeIndex + 3].Y = quantizationTree[nodeIndex + 2].Y;

        var evenLowerWidth = lowerWidth % 2;

        quantizationTree[nodeIndex + 4].X = x + upperWidth;
        quantizationTree[nodeIndex + 6].X = quantizationTree[nodeIndex + 4].X;
        quantizationTree[nodeIndex + 4].Y = y;
        quantizationTree[nodeIndex + 5].Y = y;
        quantizationTree[nodeIndex + 6].Y = quantizationTree[nodeIndex + 2].Y;
        quantizationTree[nodeIndex + 7].Y = quantizationTree[nodeIndex + 2].Y;
        quantizationTree[nodeIndex + 4].Height = quantizationTree[nodeIndex].Height;
        quantizationTree[nodeIndex + 5].Height = quantizationTree[nodeIndex].Height;
        quantizationTree[nodeIndex + 6].Height = quantizationTree[nodeIndex + 2].Height;
        quantizationTree[nodeIndex + 7].Height = quantizationTree[nodeIndex + 2].Height;

        if (evenLowerWidth == 0)
        {
            quantizationTree[nodeIndex + 4].Width = lowerWidth / 2;
            quantizationTree[nodeIndex + 5].Width = quantizationTree[nodeIndex + 4].Width;
            quantizationTree[nodeIndex + 6].Width = quantizationTree[nodeIndex + 4].Width;
            quantizationTree[nodeIndex + 7].Width = quantizationTree[nodeIndex + 4].Width;
        }
        else
        {
            quantizationTree[nodeIndex + 5].Width = (lowerWidth + 1) / 2;
            quantizationTree[nodeIndex + 4].Width = quantizationTree[nodeIndex + 5].Width - 1;
            quantizationTree[nodeIndex + 6].Width = quantizationTree[nodeIndex + 4].Width;
            quantizationTree[nodeIndex + 7].Width = quantizationTree[nodeIndex + 5].Width;
        }

        quantizationTree[nodeIndex + 5].X = quantizationTree[nodeIndex + 4].X + quantizationTree[nodeIndex + 4].Width;
        quantizationTree[nodeIndex + 7].X = quantizationTree[nodeIndex + 5].X;

        var evenLowerHeight = lowerHeight % 2;

        quantizationTree[nodeIndex + 8].X = x;
        quantizationTree[nodeIndex + 9].X = quantizationTree[nodeIndex + 1].X;
        quantizationTree[nodeIndex + 10].X = x;
        quantizationTree[nodeIndex + 11].X = quantizationTree[nodeIndex + 1].X;
        quantizationTree[nodeIndex + 8].Y = y + upperHeight;
        quantizationTree[nodeIndex + 9].Y = quantizationTree[nodeIndex + 8].Y;
        quantizationTree[nodeIndex + 8].Width = quantizationTree[nodeIndex].Width;
        quantizationTree[nodeIndex + 9].Width = quantizationTree[nodeIndex + 1].Width;
        quantizationTree[nodeIndex + 10].Width = quantizationTree[nodeIndex].Width;
        quantizationTree[nodeIndex + 11].Width = quantizationTree[nodeIndex + 1].Width;

        if (evenLowerHeight == 0)
        {
            quantizationTree[nodeIndex + 8].Height = lowerHeight / 2;
            quantizationTree[nodeIndex + 9].Height = quantizationTree[nodeIndex + 8].Height;
            quantizationTree[nodeIndex + 10].Height = quantizationTree[nodeIndex + 8].Height;
            quantizationTree[nodeIndex + 11].Height = quantizationTree[nodeIndex + 8].Height;
        }
        else
        {
            quantizationTree[nodeIndex + 10].Height = (lowerHeight + 1) / 2;
            quantizationTree[nodeIndex + 11].Height = quantizationTree[nodeIndex + 10].Height;
            quantizationTree[nodeIndex + 8].Height = quantizationTree[nodeIndex + 10].Height - 1;
            quantizationTree[nodeIndex + 9].Height = quantizationTree[nodeIndex + 8].Height;
        }

        quantizationTree[nodeIndex + 10].Y = quantizationTree[nodeIndex + 8].Y + quantizationTree[nodeIndex + 8].Height;
        quantizationTree[nodeIndex + 11].Y = quantizationTree[nodeIndex + 10].Y;

        quantizationTree[nodeIndex + 12].X = quantizationTree[nodeIndex + 4].X;
        quantizationTree[nodeIndex + 13].X = quantizationTree[nodeIndex + 5].X;
        quantizationTree[nodeIndex + 14].X = quantizationTree[nodeIndex + 4].X;
        quantizationTree[nodeIndex + 15].X = quantizationTree[nodeIndex + 5].X;
        quantizationTree[nodeIndex + 12].Y = quantizationTree[nodeIndex + 8].Y;
        quantizationTree[nodeIndex + 13].Y = quantizationTree[nodeIndex + 8].Y;
        quantizationTree[nodeIndex + 14].Y = quantizationTree[nodeIndex + 10].Y;
        quantizationTree[nodeIndex + 15].Y = quantizationTree[nodeIndex + 10].Y;
        quantizationTree[nodeIndex + 12].Width = quantizationTree[nodeIndex + 4].Width;
        quantizationTree[nodeIndex + 13].Width = quantizationTree[nodeIndex + 5].Width;
        quantizationTree[nodeIndex + 14].Width = quantizationTree[nodeIndex + 4].Width;
        quantizationTree[nodeIndex + 15].Width = quantizationTree[nodeIndex + 5].Width;
        quantizationTree[nodeIndex + 12].Height = quantizationTree[nodeIndex + 8].Height;
        quantizationTree[nodeIndex + 13].Height = quantizationTree[nodeIndex + 8].Height;
        quantizationTree[nodeIndex + 14].Height = quantizationTree[nodeIndex + 10].Height;
        quantizationTree[nodeIndex + 15].Height = quantizationTree[nodeIndex + 10].Height;
    }

    private static void SplitQuantizationRegion4(
        WsqQuantizationNode[] quantizationTree,
        int startIndex,
        int width,
        int height,
        int x,
        int y)
    {
        var evenWidth = width % 2;
        var evenHeight = height % 2;

        quantizationTree[startIndex].X = x;
        quantizationTree[startIndex + 2].X = x;
        quantizationTree[startIndex].Y = y;
        quantizationTree[startIndex + 1].Y = y;

        if (evenWidth == 0)
        {
            quantizationTree[startIndex].Width = width / 2;
            quantizationTree[startIndex + 1].Width = quantizationTree[startIndex].Width;
            quantizationTree[startIndex + 2].Width = quantizationTree[startIndex].Width;
            quantizationTree[startIndex + 3].Width = quantizationTree[startIndex].Width;
        }
        else
        {
            quantizationTree[startIndex].Width = (width + 1) / 2;
            quantizationTree[startIndex + 1].Width = quantizationTree[startIndex].Width - 1;
            quantizationTree[startIndex + 2].Width = quantizationTree[startIndex].Width;
            quantizationTree[startIndex + 3].Width = quantizationTree[startIndex + 1].Width;
        }

        quantizationTree[startIndex + 1].X = x + quantizationTree[startIndex].Width;
        quantizationTree[startIndex + 3].X = quantizationTree[startIndex + 1].X;

        if (evenHeight == 0)
        {
            quantizationTree[startIndex].Height = height / 2;
            quantizationTree[startIndex + 1].Height = quantizationTree[startIndex].Height;
            quantizationTree[startIndex + 2].Height = quantizationTree[startIndex].Height;
            quantizationTree[startIndex + 3].Height = quantizationTree[startIndex].Height;
        }
        else
        {
            quantizationTree[startIndex].Height = (height + 1) / 2;
            quantizationTree[startIndex + 1].Height = quantizationTree[startIndex].Height;
            quantizationTree[startIndex + 2].Height = quantizationTree[startIndex].Height - 1;
            quantizationTree[startIndex + 3].Height = quantizationTree[startIndex + 2].Height;
        }

        quantizationTree[startIndex + 2].Y = y + quantizationTree[startIndex].Height;
        quantizationTree[startIndex + 3].Y = quantizationTree[startIndex + 2].Y;
    }
}

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
