using System.Net;
using System.Text.RegularExpressions;
using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.States;

namespace MermaidStudio.Application.Import;

public sealed class StateDiagramMermaidImportService
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*stateDiagram-v2\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // state "Idle State" as Idle
    private static readonly Regex AliasStateRegex = new(
        @"^\s*state\s+""(?<label>(?:\\.|[^""])*)""\s+as\s+(?<id>[A-Za-z0-9_]+)\s*$",
        RegexOptions.Compiled);

    // Idle
    private static readonly Regex SimpleStateRegex = new(
        @"^\s*(?<id>[A-Za-z0-9_]+)\s*$",
        RegexOptions.Compiled);

    // Idle --> Running
    // Idle --> Running : start
    // [*] --> Idle
    // Running --> [*]
    private static readonly Regex TransitionRegex = new(
        @"^\s*(?<src>\[\*\]|[A-Za-z0-9_]+)\s*-->\s*(?<dst>\[\*\]|[A-Za-z0-9_]+)\s*(?::\s*(?<label>.+))?\s*$",
        RegexOptions.Compiled);

    private const string StartPseudoId = "__state_start__";
    private const string EndPseudoId = "__state_end__";

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
            Kind = DiagramKind.StateDiagram,
            Direction = FlowDirection.TB
        };

        bool headerFound = false;

        var stateMap = new Dictionary<string, StateNode>(StringComparer.Ordinal);

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
                var headerMatch = HeaderRegex.Match(line);
                if (!headerMatch.Success)
                    throw new InvalidOperationException("Le Mermaid doit commencer par 'stateDiagram-v2'.");

                headerFound = true;
                continue;
            }

            // Refus explicite des constructions hors périmètre S20
            if (line.Contains('{') && line.StartsWith("state ", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Les états composites ne sont pas supportés dans S20.");

            if (line.StartsWith("note ", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Les notes Mermaid ne sont pas supportées dans S20.");

            if (AliasStateRegex.IsMatch(line))
            {
                var m = AliasStateRegex.Match(line);
                var id = m.Groups["id"].Value;
                var label = UnescapeLabel(m.Groups["label"].Value);

                UpsertStateNode(
                    id,
                    label,
                    StateNodeKind.Normal,
                    stateMap,
                    document,
                    preserveExistingExplicitLabel: false);

                continue;
            }

            if (SimpleStateRegex.IsMatch(line))
            {
                var m = SimpleStateRegex.Match(line);
                var id = m.Groups["id"].Value;

                UpsertStateNode(
                    id,
                    id,
                    StateNodeKind.Normal,
                    stateMap,
                    document,
                    preserveExistingExplicitLabel: true);

                continue;
            }

            if (TransitionRegex.IsMatch(line))
            {
                var m = TransitionRegex.Match(line);

                var sourceId = ResolveEndpoint(
                    m.Groups["src"].Value,
                    isSource: true,
                    stateMap,
                    document);

                var targetId = ResolveEndpoint(
                    m.Groups["dst"].Value,
                    isSource: false,
                    stateMap,
                    document);

                var label = m.Groups["label"].Success
                    ? m.Groups["label"].Value.Trim()
                    : string.Empty;

                document.StateTransitions.Add(new StateTransition
                {
                    SourceStateId = new EntityId(sourceId),
                    TargetStateId = new EntityId(targetId),
                    Label = label
                });

                continue;
            }

            throw new InvalidOperationException(
                $"Syntaxe Mermaid non supportée dans S20 : '{line}'.");
        }

        if (!headerFound)
            throw new InvalidOperationException("Aucun en-tête 'stateDiagram-v2' valide n'a été trouvé.");

        ApplyVerticalStateLayout(document);
        return document;
    }

    private static string ResolveEndpoint(
        string rawValue,
        bool isSource,
        IDictionary<string, StateNode> stateMap,
        DiagramDocument document)
    {
        var value = rawValue.Trim();

        if (value == "[*]")
        {
            var pseudoId = isSource ? StartPseudoId : EndPseudoId;
            var kind = isSource ? StateNodeKind.Start : StateNodeKind.End;

            CreateOrGetPseudoNode(pseudoId, kind, stateMap, document);
            return pseudoId;
        }

        UpsertStateNode(
            value,
            value,
            StateNodeKind.Normal,
            stateMap,
            document,
            preserveExistingExplicitLabel: true);

        return value;
    }

    private static void CreateOrGetPseudoNode(
        string id,
        StateNodeKind kind,
        IDictionary<string, StateNode> stateMap,
        DiagramDocument document)
    {
        if (stateMap.ContainsKey(id))
            return;

        var node = new StateNode
        {
            Id = new EntityId(id),
            Label = string.Empty,
            Kind = kind
        };

        stateMap[id] = node;
        document.StateNodes.Add(node);
    }

    private static void UpsertStateNode(
        string id,
        string label,
        StateNodeKind kind,
        IDictionary<string, StateNode> stateMap,
        DiagramDocument document,
        bool preserveExistingExplicitLabel)
    {
        if (!stateMap.TryGetValue(id, out var node))
        {
            node = new StateNode
            {
                Id = new EntityId(id),
                Label = label,
                Kind = kind
            };

            stateMap[id] = node;
            document.StateNodes.Add(node);
            return;
        }

        if (node.Kind != kind && node.Kind == StateNodeKind.Normal)
            node.Kind = kind;

        if (kind == StateNodeKind.Normal)
        {
            if (preserveExistingExplicitLabel &&
                !string.IsNullOrWhiteSpace(node.Label) &&
                !string.Equals(node.Label, id, StringComparison.Ordinal))
            {
                return;
            }

            node.Label = label;
        }
    }

    private static string UnescapeLabel(string value)
        => value.Replace("\\\"", "\"");

    private static void ApplyVerticalStateLayout(DiagramDocument document)
    {
        if (document.StateNodes.Count == 0)
            return;

        var orderIndex = document.StateNodes
            .Select((n, i) => new { n.Id.Value, Index = i })
            .ToDictionary(x => x.Value, x => x.Index, StringComparer.Ordinal);

        var outgoing = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        var indegree = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => 0,
            StringComparer.Ordinal);

        foreach (var transition in document.StateTransitions)
        {
            var sourceId = transition.SourceStateId.Value;
            var targetId = transition.TargetStateId.Value;

            if (!outgoing.ContainsKey(sourceId) || !indegree.ContainsKey(targetId))
                continue;

            if (!outgoing[sourceId].Contains(targetId, StringComparer.Ordinal))
                outgoing[sourceId].Add(targetId);

            indegree[targetId]++;
        }

        foreach (var key in outgoing.Keys.ToList())
        {
            outgoing[key] = outgoing[key]
                .OrderBy(id => orderIndex[id])
                .ToList();
        }

        var levelById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => 0,
            StringComparer.Ordinal);

        var queue = new Queue<string>(
            indegree
                .Where(kv => kv.Value == 0)
                .OrderBy(kv => orderIndex[kv.Key])
                .Select(kv => kv.Key));

        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!processed.Add(current))
                continue;

            var currentLevel = levelById[current];

            foreach (var next in outgoing[current])
            {
                levelById[next] = Math.Max(levelById[next], currentLevel + 1);
                indegree[next]--;

                if (indegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        foreach (var node in document.StateNodes.OrderBy(n => orderIndex[n.Id.Value]))
        {
            if (!processed.Contains(node.Id.Value))
                levelById[node.Id.Value] = Math.Max(levelById[node.Id.Value], 0);
        }

        var levels = document.StateNodes
            .GroupBy(n => levelById[n.Id.Value])
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(n => orderIndex[n.Id.Value]).ToList())
            .ToList();

        const double baseX = 120;
        const double baseY = 90;
        const double deltaX = 230;
        const double deltaY = 130;

        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            var levelNodes = levels[levelIndex];

            for (int i = 0; i < levelNodes.Count; i++)
            {
                var node = levelNodes[i];
                node.X = baseX + i * deltaX;
                node.Y = baseY + levelIndex * deltaY;
            }
        }
    }
}
