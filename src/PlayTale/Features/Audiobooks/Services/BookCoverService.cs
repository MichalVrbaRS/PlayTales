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
