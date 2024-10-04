using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using ecocraft.Models;

namespace ecocraft.Diagram;

public class TagNode: NodeModel
{
    public TagNode(Point? position = null) : base(position) { }
    
    public ItemOrTag ItemOrTag { get; set; }
}