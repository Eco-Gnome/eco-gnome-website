using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace ecocraft.Diagram;

public class EcoNode(Point? position = null) : NodeModel(position)
{
    public int Layer { get; set; }
    public double Order { get; set; }
}