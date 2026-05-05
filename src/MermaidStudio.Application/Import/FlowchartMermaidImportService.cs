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

        ApplyDeterministicLayout(document);
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

        id = label = "";
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
            id = label = "";
            style = NodeVisualStyle.Rectangle;
            return false;
        }

        id = m.Groups["id"].Value;
        label = m.Groups["quoted"].Success ? m.Groups["quoted"].Value :
                m.Groups["raw"].Success ? m.Groups["raw"].Value : "";
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

    private static void ApplyDeterministicLayout(DiagramDocument doc)
    {
        const double baseX = 100;
        const double baseY = 80;
        const double deltaX = 240;
        const double deltaY = 140;

        // ✅ S18 fix final :
        // respect visuel simple de la direction du diagramme
        switch (doc.Direction)
        {
            case FlowDirection.TB:
            case FlowDirection.BT:
                // colonne verticale
                for (int i = 0; i < doc.Nodes.Count; i++)
                {
                    doc.Nodes[i].X = baseX;
                    doc.Nodes[i].Y = baseY + i * deltaY;
                }
                break;

            case FlowDirection.RL:
            case FlowDirection.LR:
            default:
                // ligne horizontale
                for (int i = 0; i < doc.Nodes.Count; i++)
                {
                    doc.Nodes[i].X = baseX + i * deltaX;
                    doc.Nodes[i].Y = baseY;
                }
                break;
        }
    }
}
