namespace MermaidStudio.UI.Avalonia.Editing;

public interface IUndoableCommand
{
    string Name { get; }
    void Execute();
    void Undo();
}
