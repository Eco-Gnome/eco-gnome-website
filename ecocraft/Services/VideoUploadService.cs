using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    public List<string> GetFiles()
    {
        // On renvoie les noms de fichier seulement, pas les chemins complets
        return Directory.GetFiles(_uploadRoot)
                        .Select(Path.GetFileName)
                        .ToList();
    }

    public void DeleteFile(string file)
    {
        // Là tu faisais Directory.Delete sur un fichier → ça essaye de supprimer un dossier.
        // Il faut File.Delete.
        var fullPath = Path.Combine(_uploadRoot, file);
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

    // Nouveau: on prend juste le nom et le stream
    public async Task<string> SaveFileAsync(Stream browserFileStream, string originalFileName, string contentType, long length)
    {
        // vérif type
        if (!originalFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
            contentType != "video/mp4")
            throw new InvalidOperationException("Format non autorisé. MP4 uniquement.");

        const long maxBytes = 20L * 1024L * 1024L;
        if (length > maxBytes)
            throw new InvalidOperationException("Fichier trop gros (>20 Mo).");

        var safeFileName = Path.GetFileNameWithoutExtension(originalFileName);
        var finalName = $"{ToUrlSafe(safeFileName)}.mp4";

        var finalPath = Path.Combine(_uploadRoot, finalName);

        await using (var fs = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
        {
            await browserFileStream.CopyToAsync(fs);
        }

        return $"/videos/{finalName}";
    }
}
