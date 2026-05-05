using System.Text.Json;
using System.Text.Json.Serialization;
using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.States;

namespace MermaidStudio.Application.Persistence;

public sealed class DiagramDocumentJsonService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public string Serialize(DiagramDocument document)
    {
        var dto = new DiagramDocumentDto
        {
            Kind = document.Kind,
            Direction = document.Direction,
            Nodes = document.Nodes
                .Select(n => new DiagramNodeDto
                {
                    Id = n.Id.Value,
                    Label = n.Label,
                    X = n.X,
                    Y = n.Y,
                    VisualStyle = n.VisualStyle
                })
                .ToList(),
            Edges = document.Edges
                .Select(e => new DiagramEdgeDto
                {
                    Id = e.Id.Value,
                    SourceNodeId = e.SourceNodeId.Value,
                    TargetNodeId = e.TargetNodeId.Value,
                    Label = e.Label ?? string.Empty,
                    Kind = e.Kind,
                    Direction = e.Direction
                })
                .ToList(),
            StateNodes = document.StateNodes
                .Select(n => new DiagramStateNodeDto
                {
                    Id = n.Id.Value,
                    Label = n.Label,
                    X = n.X,
                    Y = n.Y,
                    Kind = n.Kind
                })
                .ToList(),
            StateTransitions = document.StateTransitions
                .Select(t => new DiagramStateTransitionDto
                {
                    Id = t.Id.Value,
                    SourceStateId = t.SourceStateId.Value,
                    TargetStateId = t.TargetStateId.Value,
                    Label = t.Label
                })
                .ToList()
        };

        return JsonSerializer.Serialize(dto, _jsonOptions);
    }

    public DiagramDocument Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<DiagramDocumentDto>(json, _jsonOptions)
                  ?? throw new InvalidOperationException("Le JSON ne correspond pas à un document valide.");

        var document = new DiagramDocument
        {
            Kind = dto.Kind,
            Direction = dto.Direction
        };

        if (dto.Kind == DiagramKind.Flowchart)
        {
            var nodeMap = new Dictionary<string, Node>(StringComparer.Ordinal);

            foreach (var nodeDto in dto.Nodes)
            {
                if (string.IsNullOrWhiteSpace(nodeDto.Id))
                    throw new InvalidOperationException("Un node JSON n'a pas d'identifiant.");

                if (nodeMap.ContainsKey(nodeDto.Id))
                    throw new InvalidOperationException($"Identifiant de node dupliqué dans le JSON : {nodeDto.Id}");

                var node = new Node
                {
                    Id = new EntityId(nodeDto.Id),
                    Label = string.IsNullOrWhiteSpace(nodeDto.Label) ? "Node" : nodeDto.Label,
                    X = nodeDto.X,
                    Y = nodeDto.Y,
                    VisualStyle = nodeDto.VisualStyle
                };

                document.Nodes.Add(node);
                nodeMap.Add(nodeDto.Id, node);
            }

            foreach (var edgeDto in dto.Edges)
            {
                if (string.IsNullOrWhiteSpace(edgeDto.SourceNodeId) ||
                    string.IsNullOrWhiteSpace(edgeDto.TargetNodeId))
                {
                    throw new InvalidOperationException("Un edge JSON n'a pas de source ou de cible.");
                }

                if (!nodeMap.ContainsKey(edgeDto.SourceNodeId))
                    throw new InvalidOperationException($"SourceNodeId introuvable dans le JSON : {edgeDto.SourceNodeId}");

                if (!nodeMap.ContainsKey(edgeDto.TargetNodeId))
                    throw new InvalidOperationException($"TargetNodeId introuvable dans le JSON : {edgeDto.TargetNodeId}");

                var edge = new Edge
                {
                    Id = string.IsNullOrWhiteSpace(edgeDto.Id)
                        ? EntityId.New()
                        : new EntityId(edgeDto.Id),
                    SourceNodeId = new EntityId(edgeDto.SourceNodeId),
                    TargetNodeId = new EntityId(edgeDto.TargetNodeId),
                    Label = edgeDto.Label,
                    Kind = edgeDto.Kind,
                    Direction = edgeDto.Direction
                };

                document.Edges.Add(edge);
            }

            return document;
        }

        if (dto.Kind == DiagramKind.StateDiagram)
        {
            var stateNodeMap = new Dictionary<string, StateNode>(StringComparer.Ordinal);

            foreach (var stateNodeDto in dto.StateNodes)
            {
                if (string.IsNullOrWhiteSpace(stateNodeDto.Id))
                    throw new InvalidOperationException("Un state node JSON n'a pas d'identifiant.");

                if (stateNodeMap.ContainsKey(stateNodeDto.Id))
                    throw new InvalidOperationException($"Identifiant de state node dupliqué dans le JSON : {stateNodeDto.Id}");

                var node = new StateNode
                {
                    Id = new EntityId(stateNodeDto.Id),
                    Label = stateNodeDto.Label ?? string.Empty,
                    X = stateNodeDto.X,
                    Y = stateNodeDto.Y,
                    Kind = stateNodeDto.Kind
                };

                document.StateNodes.Add(node);
                stateNodeMap.Add(stateNodeDto.Id, node);
            }

            foreach (var transitionDto in dto.StateTransitions)
            {
                if (string.IsNullOrWhiteSpace(transitionDto.SourceStateId) ||
                    string.IsNullOrWhiteSpace(transitionDto.TargetStateId))
                {
                    throw new InvalidOperationException("Une transition JSON n'a pas de source ou de cible.");
                }

                if (!stateNodeMap.ContainsKey(transitionDto.SourceStateId))
                    throw new InvalidOperationException($"SourceStateId introuvable dans le JSON : {transitionDto.SourceStateId}");

                if (!stateNodeMap.ContainsKey(transitionDto.TargetStateId))
                    throw new InvalidOperationException($"TargetStateId introuvable dans le JSON : {transitionDto.TargetStateId}");

                var transition = new StateTransition
                {
                    Id = string.IsNullOrWhiteSpace(transitionDto.Id)
                        ? EntityId.New()
                        : new EntityId(transitionDto.Id),
                    SourceStateId = new EntityId(transitionDto.SourceStateId),
                    TargetStateId = new EntityId(transitionDto.TargetStateId),
                    Label = transitionDto.Label ?? string.Empty
                };

                document.StateTransitions.Add(transition);
            }

            return document;
        }

        throw new InvalidOperationException("Type de document non supporté.");
    }

    private sealed class DiagramDocumentDto
    {
        public DiagramKind Kind { get; set; } = DiagramKind.Flowchart;
        public FlowDirection Direction { get; set; } = FlowDirection.LR;

        public List<DiagramNodeDto> Nodes { get; set; } = new();
        public List<DiagramEdgeDto> Edges { get; set; } = new();

        public List<DiagramStateNodeDto> StateNodes { get; set; } = new();
        public List<DiagramStateTransitionDto> StateTransitions { get; set; } = new();
    }

    private sealed class DiagramNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = "Node";
        public double X { get; set; }
        public double Y { get; set; }
        public NodeVisualStyle VisualStyle { get; set; } = NodeVisualStyle.Rectangle;
    }

    private sealed class DiagramEdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public EdgeKind Kind { get; set; } = EdgeKind.Default;
        public DocumentEdgeDirection Direction { get; set; } = DocumentEdgeDirection.Forward;
    }

    private sealed class DiagramStateNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public StateNodeKind Kind { get; set; } = StateNodeKind.Normal;
    }

    private sealed class DiagramStateTransitionDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceStateId { get; set; } = string.Empty;
        public string TargetStateId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
