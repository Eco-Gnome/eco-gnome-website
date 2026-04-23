using SkiaSharp;

namespace ecocraft.Models.MapGenerator;

public class Biome( string name, SKColor color, decimal minHeight, decimal maxHeight, decimal minTemperature, decimal maxTemperature, decimal minMoisture, decimal maxMoisture)
{
    public string Name { get; private set; } = name;
    public SKColor Color { get; private set; } = color;
    public decimal MinHeight { get; private set; } = minHeight;
    public decimal MaxHeight { get; private set; } = maxHeight;
    public decimal MinTemperature { get; private set; } = minTemperature;
    public decimal MaxTemperature { get; private set; } = maxTemperature;
    public decimal MinMoisture { get; private set; } = minMoisture;
    public decimal MaxMoisture { get; private set; } = maxMoisture;
}
