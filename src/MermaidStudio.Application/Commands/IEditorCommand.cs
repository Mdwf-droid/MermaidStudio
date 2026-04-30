namespace MermaidStudio.Application.Commands;

public interface IEditorCommand
{
    string Name { get; }
    void Execute();
    void Undo();
}
