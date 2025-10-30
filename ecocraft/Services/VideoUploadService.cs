using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ecocraft.Models;

namespace ecocraft.Services;

public class VideoUploadService
{
    private readonly string _uploadRoot;

    public VideoUploadService(IWebHostEnvironment env)
    {
        _uploadRoot = Path.Combine(env.WebRootPath ?? "wwwroot", "videos");

        if (!Directory.Exists(_uploadRoot))
            Directory.CreateDirectory(_uploadRoot);
    }

    public List<string> GetFiles(Server server)
    {
        var serverPath = Path.Combine(_uploadRoot, server.Id.ToString());

        if (!Directory.Exists(serverPath))
            Directory.CreateDirectory(serverPath);

        return Directory.GetFiles(serverPath)
            .Select(Path.GetFileName)
            .ToList()!;
    }

    public void DeleteFile(Server server, string file)
    {
        var fullPath = Path.Combine(_uploadRoot, server.Id.ToString(), file);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    public static string ToUrlSafe(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        string cleaned = sb.ToString().Normalize(NormalizationForm.FormC);
        cleaned = cleaned.ToLowerInvariant();
        cleaned = Regex.Replace(cleaned, @"[^a-z0-9]+", "-");
        cleaned = Regex.Replace(cleaned, @"^-+|-+$", "");
        cleaned = Regex.Replace(cleaned, @"-+", "-");

        return cleaned;
    }

    public async Task<string> SaveFileAsync(
        Server server,
        Stream browserFileStream,
        string originalFileName,
        string contentType,
        long length)
    {
        if (!originalFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
            contentType != "video/mp4")
            throw new InvalidOperationException("Unauthorized format. MP4 only.");

        const long maxBytes = 20L * 1024L * 1024L;
        if (length > maxBytes)
            throw new InvalidOperationException("File too large (>20 Mo).");

        var safeFileName = Path.GetFileNameWithoutExtension(originalFileName);
        var finalName = $"{ToUrlSafe(safeFileName)}.mp4";

        var finalPath = Path.Combine(_uploadRoot, server.Id.ToString(), finalName);

        await using (var fs = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
        {
            await browserFileStream.CopyToAsync(fs);
        }

        return $"/videos/{server.Id.ToString()}/{finalName}";
    }

    public async Task<string> Mp3StreamToMp4Async(
        Server server,
        Stream mp3Stream,
        string originalFileName,
        string contentType,
        long length)
    {
        if (!originalFileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || contentType != "audio/mpeg")
            throw new InvalidOperationException("Unauthorized format. MP3 only.");

        const long maxBytes = 10L * 1024L * 1024L;
        if (length > maxBytes)
            throw new InvalidOperationException("File too large (>10 Mo).");

        if (mp3Stream == null) throw new ArgumentNullException(nameof(mp3Stream));

        var safeFileName = Path.GetFileNameWithoutExtension(originalFileName);
        var finalName = $"{ToUrlSafe(safeFileName)}.mp4";

        var outputMp4Path = Path.Combine(_uploadRoot, server.Id.ToString(), finalName);

        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");

        await using (var fs = File.Create(tmp))
            await mp3Stream.CopyToAsync(fs);

        try
        {
            var args = string.Join(" ",
                "-y",
                $"-f lavfi -i color=c=black:s={1920}x{1080}",
                $"-i \"{tmp}\"",
                "-map 0:v:0 -map 1:a:0",
                "-c:v libx264 -preset veryfast -tune stillimage",
                "-vf format=yuv420p",
                $"-c:a aac -b:a {192}k",
                "-shortest -pix_fmt yuv420p -movflags +faststart",
                $"\"{outputMp4Path}\""
            );

            await RunFfmpegAsync(args);
            return outputMp4Path;
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* je pleure en silence */ }
        }
    }

    private async Task RunFfmpegAsync(string arguments)
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        var si = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = new Process { StartInfo = si };
        if (!p.Start()) throw new InvalidOperationException("Impossible de démarrer ffmpeg.");

        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        await Task.WhenAll(p.WaitForExitAsync(), so, se);
        if (p.ExitCode != 0) throw new InvalidOperationException($"ffmpeg a échoué ({p.ExitCode}). {await se}");
    }
}
