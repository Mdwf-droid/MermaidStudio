using MermaidStudio.Domain.Diagrams;

namespace MermaidStudio.Domain.Mermaid;

public interface IMermaidExporter
{
    string Export(DiagramDocument document);
}
