using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using Cpp2IL.Core.Extensions;
using ecocraft.Models;

namespace ecocraft.Services;

public static class AssetRipperService
{
    private static bool _isRunning = false;

    private const string ExportPath = "Exports/ModIcons";
    private const string TexturesPath = "ExportedProject/Assets/Texture2D";
    private const string SpritePath = "ExportedProject/Assets/Sprite";
    private const string IconPath = "wwwroot/assets";
    private const string CommunityIconPath = "mod-icons";

    public static List<string> ExtractModIcons(string filePath, Server? server = null)
    {
        TryLock();
        try
        {
            var exportPath = ExtractUnityFiles(filePath);
            return LoadModIcons(exportPath, server);
        }
        finally
        {
            UnLock();
        }
    }

    private static void TryLock()
    {
        if (_isRunning)
        {
            throw new Exception("Only one upload process is allowed at a time. Please wait a few seconds and try again.");
        }

        _isRunning = true;
    }

    private static void UnLock()
    {
        _isRunning = false;
    }

    private static string ExtractUnityFiles(string path)
    {
        LibraryConfiguration settings = new();
        ExportHandler export = new ExportHandler(settings);
        var gameData = export.LoadAndProcess([path]);

        if (Directory.Exists(ExportPath))
        {
            Directory.Delete(ExportPath, recursive: true);
        }
        Directory.CreateDirectory(ExportPath);
        export.Export(gameData, ExportPath);

        return ExportPath;
    }

    private static List<string> LoadModIcons(string folderPath, Server? server = null)
    {
        var scene = Directory.EnumerateFiles(folderPath, "*.unity", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new FileNotFoundException($"No .unity file found in «{folderPath}».");

        Console.WriteLine($"Found scene file {scene}!");

        var gameObjects = UnityStructureParser.ParseFile(scene);

        // gameObjects.ForEach(go => DebugUnityScene(go));

        var itemNameSpriteGuidAssociation = UnityStructureParser.FindItemNameSpriteGuidAssociation(gameObjects);
        var texturePathGuidAssociation = UnityStructureParser.FindMetaPathGuidAssociation(Path.Combine(ExportPath, TexturesPath));
        var sprites = UnityStructureParser.RetrieveSprites(itemNameSpriteGuidAssociation, UnityStructureParser.FindMetaPathGuidAssociation(Path.Combine(ExportPath, SpritePath)));

        Directory.CreateDirectory(Path.Combine(IconPath, server is not null ? server.Id.ToString() : CommunityIconPath));

        List<string> successList = [];

        foreach (var sprite in sprites)
        {
            if (!texturePathGuidAssociation.TryGetValue(sprite.TextureId, out var inputPath))
            {
                Console.WriteLine($"Missing texture {sprite.TextureId} for item {sprite.Name}");
                continue;
            }

            var outputPath = Path.Combine(IconPath, server is not null ? server.Id.ToString() : CommunityIconPath, $"{sprite.Name}.png");
            UnityStructureParser.ResizeImageTo64(inputPath, outputPath, sprite);
            successList.Add(sprite.Name);
        }

        return successList;
    }

    private static void DebugUnityScene(GameObject go, int indent = 0)
    {
        Console.WriteLine(" ".Repeat(indent) + go);

        foreach (var comp in go.Components)
        {
            Console.WriteLine(" ".Repeat(indent) + "- " + comp);
        }

        foreach (var child in go.Children)
        {
            DebugUnityScene(child, indent + 4);
        }
    }
}
