using System.Text.Json;
using System.Text.Json.Serialization;
using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;

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
                .ToList()
        };

        return JsonSerializer.Serialize(dto, _jsonOptions);
    }

    public DiagramDocument Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<DiagramDocumentDto>(json, _jsonOptions)
                  ?? throw new InvalidOperationException("Le JSON ne correspond pas à un document valide.");

        if (dto.Kind != DiagramKind.Flowchart)
            throw new InvalidOperationException("Seuls les documents Flowchart sont supportés dans S18.");

        var document = new DiagramDocument
        {
            Kind = dto.Kind,
            Direction = dto.Direction
        };

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

    private sealed class DiagramDocumentDto
    {
        public DiagramKind Kind { get; set; } = DiagramKind.Flowchart;
        public FlowDirection Direction { get; set; } = FlowDirection.LR;
        public List<DiagramNodeDto> Nodes { get; set; } = new();
        public List<DiagramEdgeDto> Edges { get; set; } = new();
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
}
