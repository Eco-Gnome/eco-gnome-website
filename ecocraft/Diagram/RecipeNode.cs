using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using ecocraft.Models;

namespace ecocraft.Diagram;

public class RecipeNode: NodeModel
{
    public RecipeNode(Point? position = null) : base(position) { }
    
    public Recipe Recipe { get; set; }
}