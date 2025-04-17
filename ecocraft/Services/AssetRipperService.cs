using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using Cpp2IL.Core.Extensions;

namespace ecocraft.Services;

public static class AssetRipperService
{
    private const string ExportPath = "Exports/ModIcons";
    private const string TexturesPath = "ExportedProject/Assets/Texture2D";
    private const string SpritePath = "ExportedProject/Assets/Sprite";
    private const string IconPath = "wwwroot/assets/mod-icons";
    /*private static bool _isRunning;
    private const string Port = "5001";

    private static async Task<Process> StartAssetRipper()
    {
        if (_isRunning) throw new Exception("Asset Ripper is already running.");

        Console.WriteLine("Starting Asset Ripper...");
        _isRunning = true;

        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
            ? "External/development"
            : "External/production");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(baseDir, $"AssetRipper.GUI.Free{(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "" : ".exe")}"),
            Arguments = $"--port={Port} --launch-browser=false",
            WorkingDirectory = baseDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null) throw new Exception("Asset Ripper could not be started.");

        await Task.Delay(5000);

        Console.WriteLine("Asset Ripper started");

        return process;
    }

    private static async Task WaitForLineAsync(
        Process proc,
        string token,
        int timeoutMs = 120_000)
    {
        if (proc is null) throw new ArgumentNullException(nameof(proc));

        var tcs = new TaskCompletionSource(TaskCreationOptions
            .RunContinuationsAsynchronously);

        void Handler(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            Console.WriteLine(e.Data);
            if (e.Data.Contains(token, StringComparison.Ordinal))
                tcs.TrySetResult();
        }

        proc.OutputDataReceived += Handler;
        proc.ErrorDataReceived += Handler;
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeoutMs);
        using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            try
            {
                await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException(
                    $"Timeout ({timeoutMs / 1000}s) while waiting for \"{token}\".");
            }
            finally
            {
                proc.OutputDataReceived -= Handler;
                proc.ErrorDataReceived -= Handler;
            }
        }
    }

    private static async Task PostLoadFile(string serverUrl, string filePath)
    {
        Console.WriteLine($"Loading file {filePath}...");

        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("path", filePath)
        ]);

        var rsp = await client.PostAsync("/LoadFile", content);
        rsp.EnsureSuccessStatusCode();

        Console.WriteLine("File loaded!");
    }

    private static async Task PostExport(Process process, string serverUrl, string exportPath)
    {
        Console.WriteLine("Exporting files...");

        using var client = new HttpClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = new TimeSpan(0, 5, 0);
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("path", exportPath)
        ]);

        var rspTask = client.PostAsync("/Export/UnityProject", content);

        Console.WriteLine("Waiting for end of export...");

        await Task.WhenAll(
            rspTask,
            WaitForLineAsync(process, "Finished exporting assets")
        );

        rspTask.Result.EnsureSuccessStatusCode();

        Console.WriteLine("Files exported!");
    }

    private static void KillAssetRipper(Process p)
    {
        Console.WriteLine("Killing server...");

        p.Kill(entireProcessTree: true);
        p.Dispose();

        _isRunning = false;
        Console.WriteLine("Server killed!");
    }

    public static async Task<string> ExtractUnityFiles(string path)
    {
        var process = await StartAssetRipper();

        try
        {
            await PostLoadFile($"http://localhost:{Port}", path);

            if (Directory.Exists(ExportPath))
            {
                Directory.Delete(ExportPath, true);
            }

            Directory.CreateDirectory(ExportPath);

            await PostExport(process, $"http://localhost:{Port}", Path.Combine(Directory.GetCurrentDirectory(), ExportPath));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            KillAssetRipper(process);

            throw;
        }

        KillAssetRipper(process);

        return ExportPath;
    }*/

    public static string ExtractUnityFilesViaLib(string path)
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

    public static async Task LoadModIcons(string folderPath)
    {
        var scene = Directory.EnumerateFiles(folderPath, "*.unity", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new FileNotFoundException($"Aucun fichier .unity trouvé dans «{folderPath}».");

        Console.WriteLine($"Found scene file {scene}!");

        var gameObjects = UnityStructureParser.ParseFile(scene);

        gameObjects.ForEach(go => DebugUnityScene(go));

        var itemNameSpriteGuidAssociation = UnityStructureParser.FindItemNameSpriteGuidAssociation(gameObjects);
        var texturePathGuidAssociation = UnityStructureParser.FindMetaPathGuidAssociation(Path.Combine(ExportPath, TexturesPath));
        var sprites = UnityStructureParser.RetrieveSprites(itemNameSpriteGuidAssociation, UnityStructureParser.FindMetaPathGuidAssociation(Path.Combine(ExportPath, SpritePath)));

        Directory.CreateDirectory(IconPath);

        foreach (var sprite in sprites)
        {
            if (!texturePathGuidAssociation.TryGetValue(sprite.TextureId, out var inputPath))
            {
                Console.WriteLine($"Missing texture {sprite.TextureId} for item {sprite.Name}");
                continue;
            }

            var outputPath = Path.Combine(IconPath, $"{sprite.Name}.png");

            UnityStructureParser.ResizeImageTo64(inputPath, outputPath, sprite);
        }

        await Task.Delay(1);
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
