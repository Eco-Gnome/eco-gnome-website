using System.IO.Compression;
using System.Text;

namespace ecocraft.BuildingPlanner.Model;

// Plan dans l'URL : « p1. » + Base64Url(Brotli(JSON)). Au-delà de MaxUrlPayloadLength le lien passe par un
// identifiant de plan sauvegardé (?id=).
public static class PlanUrlCodec
{
    public const string Prefix = "p1.";
    public const int MaxUrlPayloadLength = 2000;

    public static string Encode(PlanDocument document)
    {
        var json = Encoding.UTF8.GetBytes(PlanDocumentJson.Serialize(document));
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(json, 0, json.Length);
        }
        return Prefix + ToBase64Url(output.ToArray());
    }

    public static PlanDocument? TryDecode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        try
        {
            var compressed = FromBase64Url(payload.AsSpan(Prefix.Length));
            using var input = new MemoryStream(compressed);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);

            // Lecture bornée : la taille n'est connue qu'après décompression, un payload hostile gonflerait sans limite.
            var buffer = new byte[PlanValidator.MaxDocumentBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = brotli.Read(buffer, length, buffer.Length - length)) > 0) length += read;
            if (length > PlanValidator.MaxDocumentBytes) return null;
            return PlanDocumentJson.TryParse(Encoding.UTF8.GetString(buffer, 0, length));
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(ReadOnlySpan<char> text)
    {
        var builder = new StringBuilder(text.Length + 3);
        foreach (var c in text) builder.Append(c switch { '-' => '+', '_' => '/', _ => c });
        while (builder.Length % 4 != 0) builder.Append('=');
        return Convert.FromBase64String(builder.ToString());
    }
}
