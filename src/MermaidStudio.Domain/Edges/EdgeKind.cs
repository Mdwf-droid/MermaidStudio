namespace MermaidStudio.Domain.Edges;

public enum EdgeKind
{
    Default,
    Dashed,
    Dotted,
    Labeled,

    // ✅ R2.A : le document doit pouvoir porter le style déjà utilisé par l’UI
    Thick
}
