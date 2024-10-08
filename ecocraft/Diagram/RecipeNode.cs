using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using ecocraft.Models;

namespace ecocraft.Diagram;

public class RecipeNode(Recipe recipe, Point? position = null) : EcoNode(position)
{
    public Recipe Recipe { get; set; } = recipe;
    public List<TagNode> InputsTags = [];
    public List<ItemNode> Inputs = [];
    public List<ItemNode> Outputs = [];
}