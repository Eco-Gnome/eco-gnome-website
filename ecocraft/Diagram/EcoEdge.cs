using Blazor.Diagrams.Core.Geometry;

namespace ecocraft.Diagram;

public class EcoEdge
{
    public EcoNode Source { get; set; }
    public EcoNode Target { get; set; }
    public bool IsReversed { get; set; } = false; // Indique si l'arête a été inversée pour supprimer les cycles

    public EcoEdge(EcoNode source, EcoNode target)
    {
        Source = source;
        Target = target;
    }
}
