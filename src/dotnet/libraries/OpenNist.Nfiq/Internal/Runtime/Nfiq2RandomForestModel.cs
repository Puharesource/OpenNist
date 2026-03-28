namespace OpenNist.Nfiq.Internal.Runtime;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OpenNist.Nfiq.Configuration;
using OpenNist.Nfiq.Errors;

internal sealed class Nfiq2RandomForestModel
{
    private const string s_modelRoot = "my_random_trees:";
    private const string s_treeCollection = "trees:";
    private const string s_treeNodes = "nodes:";
    private static readonly Regex s_integerValuePattern = new(@":\s*(?<value>-?\d+)", RegexOptions.Compiled);
    private readonly Nfiq2RandomForestTree[] _trees;

    private Nfiq2RandomForestModel(int treeCount, string parameterHash, Nfiq2RandomForestTree[] trees)
    {
        TreeCount = treeCount;
        ParameterHash = parameterHash;
        _trees = trees;
    }

    public int TreeCount { get; }

    public string ParameterHash { get; }

    public static Nfiq2RandomForestModel FromFile(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var fullPath = Path.GetFullPath(modelPath);
        var yaml = File.ReadAllText(fullPath);
        return Parse(yaml, CalculateMd5Hex(yaml));
    }

    public static Nfiq2RandomForestModel FromModelInfo(Nfiq2ModelInfo modelInfo)
    {
        ArgumentNullException.ThrowIfNull(modelInfo);

        var yaml = File.ReadAllText(modelInfo.ModelPath);
        var hash = CalculateMd5Hex(yaml);
        if (!hash.Equals(modelInfo.ModelHash, StringComparison.Ordinal))
        {
            throw new Nfiq2Exception(
                $"The trained NFIQ 2 model hash '{hash}' did not match the declared model-info hash '{modelInfo.ModelHash}'.");
        }

        return Parse(yaml, hash);
    }

    public static Nfiq2RandomForestModel Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        return Parse(yaml, CalculateMd5Hex(yaml));
    }

    internal static Nfiq2RandomForestModel Parse(string yaml, string parameterHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterHash);

        var treeCount = 0;
        var inModelRoot = false;
        var inTreeCollection = false;
        List<FlatNode>? currentTree = null;
        FlatNode? currentNode = null;
        StringBuilder? currentSplitBuilder = null;
        var trees = new List<Nfiq2RandomForestTree>();

        foreach (var rawLine in EnumerateNonEmptyLines(yaml))
        {
            var indent = GetIndentCount(rawLine);
            var trimmed = rawLine.Trim();

            if (!inModelRoot)
            {
                if (trimmed == s_modelRoot)
                {
                    inModelRoot = true;
                }

                continue;
            }

            if (trimmed.StartsWith("ntrees:", StringComparison.Ordinal))
            {
                treeCount = ParseRequiredInteger(trimmed);
                continue;
            }

            if (trimmed == s_treeCollection)
            {
                inTreeCollection = true;
                continue;
            }

            if (!inTreeCollection)
            {
                continue;
            }

            if (indent == 6 && trimmed == "-")
            {
                FinalizeNode(ref currentNode, currentTree);
                FinalizeTree(ref currentTree, trees);
                currentTree = [];
                continue;
            }

            if (trimmed == s_treeNodes)
            {
                continue;
            }

            if (indent == 12 && trimmed == "-")
            {
                FinalizeNode(ref currentNode, currentTree);
                currentNode = new();
                continue;
            }

            if (currentNode is null)
            {
                continue;
            }

            if (currentSplitBuilder is not null)
            {
                currentSplitBuilder.Append(' ');
                currentSplitBuilder.Append(trimmed);
                if (trimmed.Contains('}', StringComparison.Ordinal))
                {
                    currentNode.Split = ParseSplit(currentSplitBuilder.ToString());
                    currentSplitBuilder = null;
                }

                continue;
            }

            if (trimmed.StartsWith("depth:", StringComparison.Ordinal))
            {
                currentNode.Depth = ParseRequiredInteger(trimmed);
                continue;
            }

            if (trimmed.StartsWith("norm_class_idx:", StringComparison.Ordinal))
            {
                currentNode.ClassIndex = ParseRequiredInteger(trimmed);
                continue;
            }

            if (trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                if (trimmed.Contains('}', StringComparison.Ordinal))
                {
                    currentNode.Split = ParseSplit(trimmed);
                }
                else
                {
                    currentSplitBuilder = new(trimmed);
                }
            }
        }

        FinalizeNode(ref currentNode, currentTree);
        FinalizeTree(ref currentTree, trees);

        if (treeCount <= 0)
        {
            throw new Nfiq2Exception("The NFIQ 2 random forest model did not declare a valid tree count.");
        }

        if (trees.Count != treeCount)
        {
            throw new Nfiq2Exception(
                $"The NFIQ 2 random forest model declared {treeCount.ToString(CultureInfo.InvariantCulture)} trees, "
                + $"but {trees.Count.ToString(CultureInfo.InvariantCulture)} were parsed.");
        }

        return new(treeCount, parameterHash, [.. trees]);
    }

    public static string CalculateMd5Hex(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var bytes = Encoding.UTF8.GetBytes(content);
#pragma warning disable CA5351 // Official NFIQ 2 model-info files declare and verify the model YAML with MD5.
        var hash = MD5.HashData(bytes);
#pragma warning restore CA5351
        return Convert.ToHexStringLower(hash);
    }

    public int Evaluate(IReadOnlyDictionary<string, double?> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);

        Span<float> features = stackalloc float[Nfiq2RandomForestFeatureOrder.NativeMeasureOrder.Length];
        for (var index = 0; index < Nfiq2RandomForestFeatureOrder.NativeMeasureOrder.Length; index++)
        {
            var featureName = Nfiq2RandomForestFeatureOrder.NativeMeasureOrder[index];
            if (!nativeQualityMeasures.TryGetValue(featureName, out var value) || value is null)
            {
                throw new Nfiq2Exception(
                    $"The managed NFIQ 2 scoring model requires native feature '{featureName}', but it was missing.");
            }

            features[index] = checked((float)value.Value);
        }

        var rawPrediction = 0;
        foreach (var tree in _trees)
        {
            rawPrediction += tree.Evaluate(features);
        }

        var scaledPrediction = rawPrediction / (double)TreeCount * 100.0;
        var qualityScore = checked((int)Math.Floor(scaledPrediction + 0.5));
        if (qualityScore is < 0 or > 100)
        {
            throw new Nfiq2Exception(
                $"The managed NFIQ 2 scoring model computed an out-of-range quality score of {qualityScore.ToString(CultureInfo.InvariantCulture)}.");
        }

        return qualityScore;
    }

    public int Evaluate(IReadOnlyDictionary<string, double> nativeQualityMeasures)
    {
        ArgumentNullException.ThrowIfNull(nativeQualityMeasures);

        Span<float> features = stackalloc float[Nfiq2RandomForestFeatureOrder.NativeMeasureOrder.Length];
        for (var index = 0; index < Nfiq2RandomForestFeatureOrder.NativeMeasureOrder.Length; index++)
        {
            var featureName = Nfiq2RandomForestFeatureOrder.NativeMeasureOrder[index];
            if (!nativeQualityMeasures.TryGetValue(featureName, out var value))
            {
                throw new Nfiq2Exception(
                    $"The managed NFIQ 2 scoring model requires native feature '{featureName}', but it was missing.");
            }

            features[index] = checked((float)value);
        }

        var rawPrediction = 0;
        foreach (var tree in _trees)
        {
            rawPrediction += tree.Evaluate(features);
        }

        var scaledPrediction = rawPrediction / (double)TreeCount * 100.0;
        var qualityScore = checked((int)Math.Floor(scaledPrediction + 0.5));
        if (qualityScore is < 0 or > 100)
        {
            throw new Nfiq2Exception(
                $"The managed NFIQ 2 scoring model computed an out-of-range quality score of {qualityScore.ToString(CultureInfo.InvariantCulture)}.");
        }

        return qualityScore;
    }

    private static IEnumerable<string> EnumerateNonEmptyLines(string content)
    {
        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line));
    }

    private static int GetIndentCount(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static int ParseRequiredInteger(string line)
    {
        var match = s_integerValuePattern.Match(line);
        if (!match.Success)
        {
            throw new Nfiq2Exception($"Could not parse an integer value from '{line}'.");
        }

        return int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    private static Nfiq2RandomForestSplit ParseSplit(string line)
    {
        var splitBody = line.Trim();
        splitBody = splitBody.TrimStart('-', ' ');
        splitBody = splitBody.TrimStart('{');
        splitBody = splitBody.TrimEnd('}');

        var featureIndex = -1;
        double? threshold = null;
        foreach (var part in splitBody.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("var:", StringComparison.Ordinal))
            {
                featureIndex = int.Parse(part["var:".Length..], CultureInfo.InvariantCulture);
            }
            else if (part.StartsWith("le:", StringComparison.Ordinal))
            {
                threshold = double.Parse(part["le:".Length..], CultureInfo.InvariantCulture);
            }
        }

        if (featureIndex < 0 || threshold is null)
        {
            throw new Nfiq2Exception($"Could not parse a valid NFIQ 2 random forest split from '{line}'.");
        }

        return new(featureIndex, threshold.Value);
    }

    private static void FinalizeNode(ref FlatNode? currentNode, List<FlatNode>? currentTree)
    {
        if (currentNode is null)
        {
            return;
        }

        if (currentTree is null)
        {
            throw new Nfiq2Exception("Encountered a random forest node before its tree was initialized.");
        }

        currentTree.Add(currentNode);
        currentNode = null;
    }

    private static void FinalizeTree(ref List<FlatNode>? currentTree, List<Nfiq2RandomForestTree> trees)
    {
        if (currentTree is null)
        {
            return;
        }

        trees.Add(BuildTree(currentTree));
        currentTree = null;
    }

    private static Nfiq2RandomForestTree BuildTree(IReadOnlyList<FlatNode> flatNodes)
    {
        if (flatNodes.Count == 0)
        {
            throw new Nfiq2Exception("Encountered an empty tree in the NFIQ 2 random forest model.");
        }

        var nodes = flatNodes
            .Select(static node => new MutableNode(node.Depth, node.ClassIndex, node.Split))
            .ToArray();

        var index = 0;
        var rootIndex = BuildNode(nodes, ref index, expectedDepth: 0);
        if (rootIndex != 0 || index != nodes.Length)
        {
            throw new Nfiq2Exception("The NFIQ 2 random forest tree structure could not be reconstructed.");
        }

        return new(nodes.Select(static node => node.ToImmutable()).ToArray());
    }

    private static int BuildNode(MutableNode[] nodes, ref int index, int expectedDepth)
    {
        if (index >= nodes.Length)
        {
            throw new Nfiq2Exception("The NFIQ 2 random forest tree terminated unexpectedly while reconstructing children.");
        }

        var currentIndex = index;
        var currentNode = nodes[currentIndex];
        if (currentNode.Depth != expectedDepth)
        {
            throw new Nfiq2Exception(
                $"Encountered tree depth {currentNode.Depth.ToString(CultureInfo.InvariantCulture)} where "
                + $"{expectedDepth.ToString(CultureInfo.InvariantCulture)} was expected.");
        }

        index++;
        if (currentNode.Split is null)
        {
            return currentIndex;
        }

        var leftChildIndex = BuildNode(nodes, ref index, expectedDepth + 1);
        var rightChildIndex = BuildNode(nodes, ref index, expectedDepth + 1);
        currentNode.LeftChildIndex = leftChildIndex;
        currentNode.RightChildIndex = rightChildIndex;
        nodes[currentIndex] = currentNode;
        return currentIndex;
    }

    private sealed record FlatNode
    {
        public int Depth { get; set; }

        public int ClassIndex { get; set; }

        public Nfiq2RandomForestSplit? Split { get; set; }
    }

    private sealed record MutableNode(int Depth, int ClassIndex, Nfiq2RandomForestSplit? Split)
    {
        public int? LeftChildIndex { get; set; }

        public int? RightChildIndex { get; set; }

        public Nfiq2RandomForestNode ToImmutable()
        {
            return new(Depth, ClassIndex, Split, LeftChildIndex, RightChildIndex);
        }
    }
}

internal sealed record Nfiq2RandomForestTree(IReadOnlyList<Nfiq2RandomForestNode> Nodes)
{
    public int Evaluate(ReadOnlySpan<float> features)
    {
        var nodeIndex = 0;
        while (true)
        {
            var node = Nodes[nodeIndex];
            if (node.Split is null)
            {
                return node.ClassIndex;
            }

            var split = node.Split;
            nodeIndex = features[split.FeatureIndex] <= split.Threshold
                ? node.LeftChildIndex ?? throw new Nfiq2Exception("The NFIQ 2 random forest node was missing a left child.")
                : node.RightChildIndex ?? throw new Nfiq2Exception("The NFIQ 2 random forest node was missing a right child.");
        }
    }
}

internal sealed record Nfiq2RandomForestNode(
    int Depth,
    int ClassIndex,
    Nfiq2RandomForestSplit? Split,
    int? LeftChildIndex,
    int? RightChildIndex);

internal sealed record Nfiq2RandomForestSplit(int FeatureIndex, double Threshold);
