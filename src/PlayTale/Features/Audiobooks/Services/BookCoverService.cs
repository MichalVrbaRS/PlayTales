using System.Text.Json;
using System.Text.Json.Serialization;
using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class BookCoverService : IBookCoverService
{
    private static readonly HttpClient Http = new();

    public async Task<string?> ResolveCoverPathAsync(
        string bookTitle,
        string? author,
        IReadOnlyList<Chapter> chapters,
        CancellationToken cancellationToken = default)
    {
        if (chapters.Count == 0)
        {
            return null;
        }

        var coverDirectory = ResolveCoverDirectory(chapters);
        if (string.IsNullOrWhiteSpace(coverDirectory))
        {
            return null;
        }

        var existingCoverPath = FindExistingCoverPath(coverDirectory);
        if (!string.IsNullOrWhiteSpace(existingCoverPath))
        {
            return existingCoverPath;
        }

        var embeddedBytes = TryExtractEmbeddedCoverBytes(chapters);
        if (embeddedBytes is not null)
        {
            var embeddedPath = await WriteCoverBytesAsync(coverDirectory, embeddedBytes, bookTitle, cancellationToken);
            if (!string.IsNullOrWhiteSpace(embeddedPath))
            {
                return embeddedPath;
            }
        }

        var downloadedBytes = await TryDownloadCoverBytesAsync(bookTitle, author, cancellationToken);
        if (downloadedBytes is null)
        {
            return null;
        }

        return await WriteCoverBytesAsync(coverDirectory, downloadedBytes, bookTitle, cancellationToken);
    }

    private static string? ResolveCoverDirectory(IReadOnlyList<Chapter> chapters)
    {
        var firstPath = chapters[0].FilePath;
        if (string.IsNullOrWhiteSpace(firstPath))
        {
            return null;
        }

        return Path.GetDirectoryName(firstPath);
    }

    private static byte[]? TryExtractEmbeddedCoverBytes(IReadOnlyList<Chapter> chapters)
    {
#if ANDROID
        foreach (var chapter in chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.FilePath) || !File.Exists(chapter.FilePath))
            {
                continue;
            }

            try
            {
                var bytes = TryExtractEmbeddedCoverBytesFromId3(chapter.FilePath);
                if (bytes is { Length: > 0 })
                {
                    return bytes;
                }
            }
            catch
            {
                // Ignore malformed tags and continue scanning.
            }
        }

        return null;
#else
        foreach (var chapter in chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.FilePath) || !File.Exists(chapter.FilePath))
            {
                continue;
            }

            try
            {
                using var tagFile = TagLib.File.Create(chapter.FilePath);
                var picture = tagFile.Tag.Pictures?.FirstOrDefault();
                if (picture is not null && picture.Data?.Data is { Length: > 0 } bytes)
                {
                    return bytes;
                }
            }
            catch
            {
                // Ignore malformed tags and continue scanning.
            }
        }

        return null;
#endif
    }

#if ANDROID
    private static byte[]? TryExtractEmbeddedCoverBytesFromId3(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 10)
        {
            return null;
        }

        var header = reader.ReadBytes(10);
        if (header.Length < 10 || header[0] != (byte)'I' || header[1] != (byte)'D' || header[2] != (byte)'3')
        {
            return null;
        }

        var majorVersion = header[3];
        var flags = header[5];
        var tagSize = DecodeSynchsafeInt(header.AsSpan(6, 4));
        if (tagSize <= 0 || stream.Length < 10 + tagSize)
        {
            return null;
        }

        var tagData = reader.ReadBytes(tagSize);
        if ((flags & 0x80) != 0)
        {
            tagData = RemoveUnsynchronization(tagData);
        }

        return majorVersion == 2
            ? TryExtractApicV22(tagData)
            : TryExtractApicV23OrV24(tagData, majorVersion);
    }

    private static byte[]? TryExtractApicV23OrV24(byte[] tagData, int majorVersion)
    {
        var offset = 0;
        while (offset + 10 <= tagData.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(tagData, offset, 4);
            if (string.IsNullOrWhiteSpace(id) || id.Trim('\0').Length == 0)
            {
                break;
            }

            var size = majorVersion == 4
                ? DecodeSynchsafeInt(tagData.AsSpan(offset + 4, 4))
                : DecodeBigEndianInt(tagData.AsSpan(offset + 4, 4));

            if (size <= 0 || offset + 10 + size > tagData.Length)
            {
                break;
            }

            if (id == "APIC")
            {
                var payload = tagData.AsSpan(offset + 10, size).ToArray();
                return ParseApicPayload(payload);
            }

            offset += 10 + size;
        }

        return null;
    }

    private static byte[]? TryExtractApicV22(byte[] tagData)
    {
        var offset = 0;
        while (offset + 6 <= tagData.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(tagData, offset, 3);
            if (string.IsNullOrWhiteSpace(id) || id.Trim('\0').Length == 0)
            {
                break;
            }

            var size = (tagData[offset + 3] << 16) | (tagData[offset + 4] << 8) | tagData[offset + 5];
            if (size <= 0 || offset + 6 + size > tagData.Length)
            {
                break;
            }

            if (id == "PIC")
            {
                var payload = tagData.AsSpan(offset + 6, size).ToArray();
                return ParsePicPayload(payload);
            }

            offset += 6 + size;
        }

        return null;
    }

    private static byte[]? ParseApicPayload(byte[] payload)
    {
        if (payload.Length < 4)
        {
            return null;
        }

        var textEncoding = payload[0];
        var index = 1;

        var mimeEnd = Array.IndexOf(payload, (byte)0, index);
        if (mimeEnd < 0)
        {
            return null;
        }

        index = mimeEnd + 1;
        if (index >= payload.Length)
        {
            return null;
        }

        // Picture type byte (ignored, we keep first APIC image).
        index++;
        if (index >= payload.Length)
        {
            return null;
        }

        var descEnd = FindTextTerminator(payload, index, textEncoding);
        if (descEnd < 0)
        {
            return null;
        }

        index = descEnd;
        if (textEncoding == 1 || textEncoding == 2)
        {
            index += 2;
        }
        else
        {
            index += 1;
        }

        if (index >= payload.Length)
        {
            return null;
        }

        return payload.AsSpan(index).ToArray();
    }

    private static byte[]? ParsePicPayload(byte[] payload)
    {
        if (payload.Length < 6)
        {
            return null;
        }

        var textEncoding = payload[0];
        var index = 1;

        // ID3v2.2 stores 3-byte image format (e.g., "PNG", "JPG").
        index += 3;
        if (index >= payload.Length)
        {
            return null;
        }

        // Picture type byte.
        index++;
        if (index >= payload.Length)
        {
            return null;
        }

        var descEnd = FindTextTerminator(payload, index, textEncoding);
        if (descEnd < 0)
        {
            return null;
        }

        index = descEnd;
        if (textEncoding == 1 || textEncoding == 2)
        {
            index += 2;
        }
        else
        {
            index += 1;
        }

        if (index >= payload.Length)
        {
            return null;
        }

        return payload.AsSpan(index).ToArray();
    }

    private static int FindTextTerminator(byte[] buffer, int start, byte textEncoding)
    {
        if (textEncoding == 1 || textEncoding == 2)
        {
            for (var i = start; i + 1 < buffer.Length; i += 2)
            {
                if (buffer[i] == 0 && buffer[i + 1] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        return Array.IndexOf(buffer, (byte)0, start);
    }

    private static int DecodeSynchsafeInt(ReadOnlySpan<byte> bytes)
    {
        return (bytes[0] << 21) | (bytes[1] << 14) | (bytes[2] << 7) | bytes[3];
    }

    private static int DecodeBigEndianInt(ReadOnlySpan<byte> bytes)
    {
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static byte[] RemoveUnsynchronization(byte[] input)
    {
        var output = new List<byte>(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (i + 1 < input.Length && input[i] == 0xFF && input[i + 1] == 0x00)
            {
                output.Add(0xFF);
                i++;
                continue;
            }

            output.Add(input[i]);
        }

        return output.ToArray();
    }
#endif

    private static async Task<byte[]?> TryDownloadCoverBytesAsync(string bookTitle, string? author, CancellationToken cancellationToken)
    {
        try
        {
            var query = string.IsNullOrWhiteSpace(author) ? bookTitle : $"{bookTitle} {author}";
            var url = $"https://itunes.apple.com/search?entity=audiobook&limit=1&term={Uri.EscapeDataString(query)}";
            using var response = await Http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ItunesSearchResult>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            var artworkUrl = result?.Results?.FirstOrDefault()?.ArtworkUrl100;
            if (string.IsNullOrWhiteSpace(artworkUrl))
            {
                return null;
            }

            var bestArtworkUrl = artworkUrl.Replace("100x100bb.jpg", "600x600bb.jpg", StringComparison.OrdinalIgnoreCase);
            return await Http.GetByteArrayAsync(bestArtworkUrl, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> WriteCoverBytesAsync(
        string preferredDirectory,
        byte[] bytes,
        string bookTitle,
        CancellationToken cancellationToken)
    {
        var extension = DetectCoverExtension(bytes);
        var preferredPath = Path.Combine(preferredDirectory, $"cover{extension}");
        try
        {
            var directory = Path.GetDirectoryName(preferredPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(preferredPath, bytes, cancellationToken);
            return preferredPath;
        }
        catch
        {
            try
            {
                var fallbackRoot = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "covers");
                Directory.CreateDirectory(fallbackRoot);
                var fallbackFileName = $"{SanitizeFileName(bookTitle)}{extension}";
                var fallbackPath = Path.Combine(fallbackRoot, fallbackFileName);
                await File.WriteAllBytesAsync(fallbackPath, bytes, cancellationToken);
                return fallbackPath;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "cover" : result;
    }

    private static string? FindExistingCoverPath(string directory)
    {
        var jpg = Path.Combine(directory, "cover.jpg");
        if (File.Exists(jpg))
        {
            return jpg;
        }

        var png = Path.Combine(directory, "cover.png");
        if (File.Exists(png))
        {
            return png;
        }

        return null;
    }

    private static string DetectCoverExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47)
        {
            return ".png";
        }

        return ".jpg";
    }

    private sealed class ItunesSearchResult
    {
        [JsonPropertyName("results")]
        public List<ItunesItem>? Results { get; set; }
    }

    private sealed class ItunesItem
    {
        [JsonPropertyName("artworkUrl100")]
        public string? ArtworkUrl100 { get; set; }
    }
}
