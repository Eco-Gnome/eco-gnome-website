using SkiaSharp;

namespace ecocraft.Models.MapGenerator;

public class Contribution( string name, string icon, SKColor defaultColor, SKColor? selectedColor = null, string sublayerOf = "")
{
    public string Name { get; private set; } = name;
    public string CanvasName { get; private set; } = name + "Canvas";
    public string ImageName { get; private set; } = name + "Image";
    public string Icon { get; private set; } = icon;
    public SKColor DefaultColor { get; private set; } = defaultColor;
    public SKColor? SelectedColor { get; private set; } = selectedColor;
    public string SubLayerOf { get; private set; } = sublayerOf;
}
