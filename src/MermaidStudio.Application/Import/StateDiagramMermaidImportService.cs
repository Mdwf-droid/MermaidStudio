using System.Net;
using System.Text.RegularExpressions;
using MermaidStudio.Application.Layout;
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

    private readonly StateDiagramLayoutService _layoutService = new();

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

        _layoutService.ApplyLayout(document);
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
}
