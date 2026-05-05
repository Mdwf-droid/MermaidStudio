using System.Text;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.States;

namespace MermaidStudio.Application.Export;

public sealed class StateDiagramExportService
{
    public string Export(DiagramDocument document)
    {
        if (document.Kind != DiagramKind.StateDiagram)
            throw new InvalidOperationException("Le document fourni n'est pas un State Diagram.");

        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");

        var normalStates = document.StateNodes
            .Where(n => n.Kind == StateNodeKind.Normal)
            .OrderBy(n => n.Id.Value, StringComparer.Ordinal)
            .ToList();

        foreach (var state in normalStates)
        {
            var label = Escape(state.Label);
            sb.AppendLine($"    state \"{label}\" as {state.Id.Value}");
        }

        foreach (var transition in document.StateTransitions
                     .OrderBy(t => GetSourceExportId(t, document), StringComparer.Ordinal)
                     .ThenBy(t => GetTargetExportId(t, document), StringComparer.Ordinal))
        {
            var source = GetSourceExportId(transition, document);
            var target = GetTargetExportId(transition, document);

            if (string.IsNullOrWhiteSpace(transition.Label))
            {
                sb.AppendLine($"    {source} --> {target}");
            }
            else
            {
                sb.AppendLine($"    {source} --> {target} : {Escape(transition.Label)}");
            }
        }

        return sb.ToString();
    }

    private static string GetSourceExportId(StateTransition transition, DiagramDocument document)
    {
        var source = document.StateNodes.FirstOrDefault(n => n.Id.Equals(transition.SourceStateId));
        if (source == null)
            return transition.SourceStateId.Value;

        return source.Kind == StateNodeKind.Start ? "[*]" : source.Id.Value;
    }

    private static string GetTargetExportId(StateTransition transition, DiagramDocument document)
    {
        var target = document.StateNodes.FirstOrDefault(n => n.Id.Equals(transition.TargetStateId));
        if (target == null)
            return transition.TargetStateId.Value;

        return target.Kind == StateNodeKind.End ? "[*]" : target.Id.Value;
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"");
}
