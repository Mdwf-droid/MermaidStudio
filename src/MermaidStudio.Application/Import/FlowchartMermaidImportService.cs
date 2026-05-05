using System.Net;
using System.Text.RegularExpressions;
using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.Application.Import;

public sealed class FlowchartMermaidImportService
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*(?:flowchart|graph)\s+(?<dir>LR|TB|TD|RL|BT)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EdgeRegex = new(
        @"^\s*(?<src>.+?)\s*(?<token>-->|-.->|==>)\s*(?:\s*\|\s*(?<label>[^|]*)\s*\|)?\s*(?<dst>.+?)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PlainIdRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex CircleRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*\(\(\s*(?:""(?<quoted>[^""]*)""|(?<raw>.+?))\s*\)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex RoundedRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*\(\s*(?:""(?<quoted>[^""]*)""|(?<raw>.+?))\s*\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DecisionRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*\{\s*(?:""(?<quoted>[^""]*)""|(?<raw>.+?))\s*\}\s*$",
        RegexOptions.Compiled);

    private static readonly Regex RectangleRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*\[\s*(?:""(?<quoted>[^""]*)""|(?<raw>.+?))\s*\]\s*$",
        RegexOptions.Compiled);

    public DiagramDocument Import(string mermaidText)
    {
        if (string.IsNullOrWhiteSpace(mermaidText))
            throw new InvalidOperationException("Le texte Mermaid à importer est vide.");

        var decoded = WebUtility.HtmlDecode(mermaidText);

        var lines = decoded
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        var document = new DiagramDocument
        {
            Kind = DiagramKind.Flowchart
        };

        bool headerFound = false;
        int edgeCounter = 1;
        var nodeMap = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line == "```" || line.Equals("```mermaid", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("%%", StringComparison.Ordinal))
                continue;

            if (!headerFound)
            {
                var m = HeaderRegex.Match(line);
                if (!m.Success)
                    throw new InvalidOperationException("En-tête Mermaid invalide.");

                document.Direction = ParseDirection(m.Groups["dir"].Value);
                headerFound = true;
                continue;
            }

            // Déclaration de node seule
            if (TryParseNode(line, out var nid, out var lbl, out var style))
            {
                UpsertNode(nid, lbl, style, nodeMap, document);
                continue;
            }

            // Ligne d'edge avec endpoints simples OU inline
            var em = EdgeRegex.Match(line);
            if (em.Success)
            {
                var srcId = ResolveEndpoint(em.Groups["src"].Value, nodeMap, document);
                var dstId = ResolveEndpoint(em.Groups["dst"].Value, nodeMap, document);

                document.Edges.Add(new Edge
                {
                    Id = new EntityId($"E{edgeCounter++}"),
                    SourceNodeId = new EntityId(srcId),
                    TargetNodeId = new EntityId(dstId),
                    Label = em.Groups["label"].Success ? em.Groups["label"].Value.Trim() : "",
                    Kind = em.Groups["token"].Value switch
                    {
                        "-.->" => EdgeKind.Dashed,
                        "==>" => EdgeKind.Thick,
                        _ => EdgeKind.Default
                    },
                    Direction = DocumentEdgeDirection.Forward
                });
                continue;
            }

            throw new InvalidOperationException($"Syntaxe Mermaid non supportée dans S18 : '{line}'.");
        }

        if (!headerFound)
            throw new InvalidOperationException("Aucun en-tête flowchart valide n'a été trouvé.");

        ApplyTreeLikeLayout(document);
        return document;
    }

    private static string ResolveEndpoint(string expr, IDictionary<string, Node> map, DiagramDocument doc)
    {
        expr = expr.Trim();

        if (PlainIdRegex.IsMatch(expr))
        {
            EnsureNodeExists(expr, map, doc);
            return expr;
        }

        if (TryParseNode(expr, out var id, out var lbl, out var style))
        {
            UpsertNode(id, lbl, style, map, doc);
            return id;
        }

        throw new InvalidOperationException($"Endpoint Mermaid non supporté : '{expr}'.");
    }

    private static void UpsertNode(
        string id,
        string label,
        NodeVisualStyle style,
        IDictionary<string, Node> map,
        DiagramDocument doc)
    {
        if (!map.TryGetValue(id, out var node))
        {
            node = new Node { Id = new EntityId(id) };
            map[id] = node;
            doc.Nodes.Add(node);
        }

        node.Label = string.IsNullOrWhiteSpace(label) ? id : label.Trim();
        node.VisualStyle = style;
    }

    private static bool TryParseNode(
        string line,
        out string id,
        out string label,
        out NodeVisualStyle style)
    {
        if (TryMatch(RectangleRegex, line, NodeVisualStyle.Rectangle, out id, out label, out style)) return true;
        if (TryMatch(RoundedRegex, line, NodeVisualStyle.Rounded, out id, out label, out style)) return true;
        if (TryMatch(DecisionRegex, line, NodeVisualStyle.Decision, out id, out label, out style)) return true;
        if (TryMatch(CircleRegex, line, NodeVisualStyle.Circle, out id, out label, out style)) return true;

        id = string.Empty;
        label = string.Empty;
        style = NodeVisualStyle.Rectangle;
        return false;
    }

    private static bool TryMatch(
        Regex rx,
        string line,
        NodeVisualStyle st,
        out string id,
        out string label,
        out NodeVisualStyle style)
    {
        var m = rx.Match(line);
        if (!m.Success)
        {
            id = string.Empty;
            label = string.Empty;
            style = NodeVisualStyle.Rectangle;
            return false;
        }

        id = m.Groups["id"].Value;
        label = m.Groups["quoted"].Success
            ? m.Groups["quoted"].Value
            : m.Groups["raw"].Success
                ? m.Groups["raw"].Value
                : string.Empty;
        style = st;
        return true;
    }

    private static void EnsureNodeExists(string id, IDictionary<string, Node> map, DiagramDocument doc)
    {
        if (map.ContainsKey(id))
            return;

        var n = new Node
        {
            Id = new EntityId(id),
            Label = id,
            VisualStyle = NodeVisualStyle.Rectangle
        };

        map[id] = n;
        doc.Nodes.Add(n);
    }

    private static FlowDirection ParseDirection(string d) => d.ToUpperInvariant() switch
    {
        "TB" => FlowDirection.TB,
        "TD" => FlowDirection.TB,
        "RL" => FlowDirection.RL,
        "BT" => FlowDirection.BT,
        _ => FlowDirection.LR
    };

    private static void ApplyTreeLikeLayout(DiagramDocument doc)
    {
        if (doc.Nodes.Count == 0)
            return;

        var orderedNodes = doc.Nodes
            .Select((n, i) => new { Node = n, Index = i })
            .ToList();

        var orderIndex = orderedNodes.ToDictionary(
            x => x.Node.Id.Value,
            x => x.Index,
            StringComparer.Ordinal);

        // Estimation simple des tailles visuelles des nodes
        var sizeById = doc.Nodes.ToDictionary(
            n => n.Id.Value,
            n => EstimateNodeSize(n),
            StringComparer.Ordinal);

        var outgoing = doc.Nodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        var incoming = doc.Nodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var edge in doc.Edges)
        {
            var src = edge.SourceNodeId.Value;
            var dst = edge.TargetNodeId.Value;

            if (!outgoing.ContainsKey(src) || !incoming.ContainsKey(dst))
                continue;

            if (!outgoing[src].Contains(dst, StringComparer.Ordinal))
                outgoing[src].Add(dst);

            if (!incoming[dst].Contains(src, StringComparer.Ordinal))
                incoming[dst].Add(src);
        }

        foreach (var key in outgoing.Keys.ToList())
            outgoing[key] = outgoing[key].OrderBy(id => orderIndex[id]).ToList();

        foreach (var key in incoming.Keys.ToList())
            incoming[key] = incoming[key].OrderBy(id => orderIndex[id]).ToList();

        // Arbre directeur : un parent principal par node = premier incoming stable
        var parentById = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in doc.Nodes)
        {
            var id = node.Id.Value;
            parentById[id] = incoming[id].Count > 0 ? incoming[id][0] : null;
        }

        var childrenById = doc.Nodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var kv in parentById)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value) && childrenById.ContainsKey(kv.Value!))
                childrenById[kv.Value!].Add(kv.Key);
        }

        foreach (var key in childrenById.Keys.ToList())
            childrenById[key] = childrenById[key].OrderBy(id => orderIndex[id]).ToList();

        var roots = doc.Nodes
            .Where(n => parentById[n.Id.Value] == null)
            .OrderBy(n => orderIndex[n.Id.Value])
            .Select(n => n.Id.Value)
            .ToList();

        if (roots.Count == 0)
            roots.Add(doc.Nodes.OrderBy(n => orderIndex[n.Id.Value]).First().Id.Value);

        var depthById = new Dictionary<string, int>(StringComparer.Ordinal);
        var subtreeSpanById = new Dictionary<string, double>(StringComparer.Ordinal);
        var xCenterById = new Dictionary<string, double>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void AssignDepth(string id, int depth)
        {
            if (visited.Contains(id))
            {
                depthById[id] = Math.Max(depthById[id], depth);
                return;
            }

            visited.Add(id);
            depthById[id] = depth;

            foreach (var child in childrenById[id])
                AssignDepth(child, depth + 1);
        }

        foreach (var root in roots)
            AssignDepth(root, 0);

        foreach (var node in doc.Nodes.OrderBy(n => orderIndex[n.Id.Value]))
        {
            if (!depthById.ContainsKey(node.Id.Value))
                depthById[node.Id.Value] = 0;
        }

        double ComputeSubtreeSpan(string id)
        {
            if (subtreeSpanById.TryGetValue(id, out var cached))
                return cached;

            var selfWidth = sizeById[id].Width;
            var children = childrenById[id];

            if (children.Count == 0)
            {
                subtreeSpanById[id] = selfWidth;
                return selfWidth;
            }

            const double siblingGap = 80.0;

            double total = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                if (i > 0)
                    total += siblingGap;

                total += ComputeSubtreeSpan(children[i]);
            }

            var span = Math.Max(selfWidth, total);
            subtreeSpanById[id] = span;
            return span;
        }

        foreach (var root in roots)
            ComputeSubtreeSpan(root);

        void AssignCenters(string id, double left)
        {
            var children = childrenById[id];

            if (children.Count == 0)
            {
                xCenterById[id] = left + subtreeSpanById[id] / 2.0;
                return;
            }

            const double siblingGap = 80.0;

            double currentLeft = left;
            foreach (var child in children)
            {
                AssignCenters(child, currentLeft);
                currentLeft += subtreeSpanById[child] + siblingGap;
            }

            var first = children.First();
            var last = children.Last();

            var center = (xCenterById[first] + xCenterById[last]) / 2.0;
            xCenterById[id] = center;
        }

        double rootLeft = 100.0;
        const double rootGap = 140.0;

        foreach (var root in roots)
        {
            AssignCenters(root, rootLeft);
            rootLeft += subtreeSpanById[root] + rootGap;
        }

        foreach (var node in doc.Nodes.OrderBy(n => orderIndex[n.Id.Value]))
        {
            if (!xCenterById.ContainsKey(node.Id.Value))
            {
                xCenterById[node.Id.Value] = rootLeft;
                rootLeft += sizeById[node.Id.Value].Width + rootGap;
            }
        }

        var maxDepth = depthById.Values.DefaultIfEmpty(0).Max();

        var maxHeightByDepth = Enumerable.Range(0, maxDepth + 1)
            .ToDictionary(
                d => d,
                d => doc.Nodes
                    .Where(n => depthById[n.Id.Value] == d)
                    .Select(n => sizeById[n.Id.Value].Height)
                    .DefaultIfEmpty(70)
                    .Max());

        var axisOffsetByDepth = new Dictionary<int, double>();
        const double baseAxis = 100.0;
        const double levelGap = 90.0;

        double currentAxis = baseAxis;
        for (int d = 0; d <= maxDepth; d++)
        {
            axisOffsetByDepth[d] = currentAxis;
            currentAxis += maxHeightByDepth[d] + levelGap;
        }

        foreach (var node in doc.Nodes)
        {
            var id = node.Id.Value;
            var size = sizeById[id];
            var depth = depthById[id];
            var center = xCenterById[id];

            switch (doc.Direction)
            {
                case FlowDirection.TB:
                    node.X = center - size.Width / 2.0;
                    node.Y = axisOffsetByDepth[depth];
                    break;

                case FlowDirection.BT:
                    node.X = center - size.Width / 2.0;
                    node.Y = axisOffsetByDepth[maxDepth - depth];
                    break;

                case FlowDirection.RL:
                    node.X = axisOffsetByDepth[maxDepth - depth];
                    node.Y = center - size.Height / 2.0;
                    break;

                case FlowDirection.LR:
                default:
                    node.X = axisOffsetByDepth[depth];
                    node.Y = center - size.Height / 2.0;
                    break;
            }
        }
    }

    private static (double Width, double Height) EstimateNodeSize(Node node)
    {
        const double minWidth = 140;
        const double minHeight = 60;
        const double maxTextWidth = 180;
        const double horizontalPadding = 28;
        const double verticalPadding = 22;
        const double estimatedCharWidth = 7.2;
        const double estimatedLineHeight = 18.0;

        var text = string.IsNullOrWhiteSpace(node.Label) ? "Node" : node.Label;
        var estimatedTextWidth = Math.Min(maxTextWidth, Math.Max(40, text.Length * estimatedCharWidth));
        var estimatedLines = Math.Max(1, (int)Math.Ceiling((text.Length * estimatedCharWidth) / maxTextWidth));
        var estimatedTextHeight = estimatedLines * estimatedLineHeight;

        double width = Math.Max(minWidth, estimatedTextWidth + horizontalPadding);
        double height = Math.Max(minHeight, estimatedTextHeight + verticalPadding);

        switch (node.VisualStyle)
        {
            case NodeVisualStyle.Decision:
                width = Math.Max(width + 30, 170);
                height = Math.Max(height + 10, 80);
                break;

            case NodeVisualStyle.Circle:
                width = Math.Max(width + 20, 150);
                height = Math.Max(height + 16, 78);
                break;
        }

        return (width, height);
    }
}
