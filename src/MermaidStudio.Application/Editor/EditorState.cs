using MermaidStudio.Domain.Core;

namespace MermaidStudio.Application.Editor;

public sealed class EditorState
{
    public EditorTool ActiveTool { get; set; } = EditorTool.CreateNode;
    public EntityId? SelectedNodeId { get; set; }
    public EntityId? SelectedEdgeId { get; set; }
}
