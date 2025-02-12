using SkiaSharp;

namespace ecocraft.Models.MapGenerator;

public class Contribution( string name, string icon, SKColor defaultColor, string sublayerOf = "")
{
    public string Name { get; private set; } = name;
    public string CanvasName { get; private set; } = name + "Canvas";
    public string ImageName { get; private set; } = name + "Image";
    public string Icon { get; private set; } = icon;
    public SKColor DefaultColor { get; private set; } = defaultColor;
    public string SubLayerOf { get; private set; } = sublayerOf;
}
