using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.IO.Files;

namespace ecocraft.Services;

public class AssetRipperService
{
    public void ImportModIcons(IReadOnlyList<string> paths)
    {
        LibraryConfiguration settings = new();
        ExportHandler export = new ExportHandler(settings);
        var gameData = export.LoadAndProcess(paths);

        var exportPath = "exportPath";
        Directory.CreateDirectory(exportPath);
        settings.ExportRootPath = exportPath;
        PrimaryContentExporter.CreateDefault(gameData).Export(gameData.GameBundle, settings, LocalFileSystem.Instance);
    }
}
