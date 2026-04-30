namespace MermaidStudio.Application.Commands;

public sealed class CommandStack
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var cmd)) return;
        cmd.Undo();
        _redo.Push(cmd);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var cmd)) return;
        cmd.Execute();
        _undo.Push(cmd);
    }
}
