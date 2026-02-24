using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed partial class PlatformImportService : IImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3"
    };

    public Task<IReadOnlyList<ImportSource>> PickFolderOrFilesAsync(CancellationToken cancellationToken = default)
    {
        return PickPlatformSourcesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Chapter>> ScanChaptersAsync(ImportSource source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> filePaths = source.Type switch
        {
            ImportSourceType.Folder when !string.IsNullOrWhiteSpace(source.Path) && Directory.Exists(source.Path) =>
                Directory.EnumerateFiles(source.Path, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupportedAudioFile),
            ImportSourceType.Files when !string.IsNullOrWhiteSpace(source.Path) && File.Exists(source.Path) =>
                new[] { source.Path },
            _ => Array.Empty<string>()
        };

        var chapters = filePaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select((path, index) => new Chapter
            {
                Id = Guid.NewGuid(),
                BookId = Guid.Empty,
                Title = CreateChapterTitle(path),
                OrderIndex = index,
                DurationSeconds = 0,
                FilePath = path,
                SourceUri = source.SourceUri,
                SecurityScopedBookmarkBase64 = source.SecurityScopedBookmarkBase64,
                LastPositionSeconds = 0
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<Chapter>>(chapters);
    }

    private static bool IsSupportedAudioFile(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }

    private static string CreateChapterTitle(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Chapter";
        }

        return fileName.Replace('_', ' ').Trim();
    }

    internal static IReadOnlyList<ImportSource> BuildSourcesFromPaths(IReadOnlyList<(string FileName, string FullPath)> files)
    {
        var validFiles = files
            .Where(x => !string.IsNullOrWhiteSpace(x.FullPath) && File.Exists(x.FullPath))
            .Where(x => IsSupportedAudioFile(x.FullPath))
            .ToList();

        if (validFiles.Count == 0)
        {
            return Array.Empty<ImportSource>();
        }

        if (validFiles.Count == 1)
        {
            var file = validFiles[0];
            return new[]
            {
                new ImportSource(
                    ImportSourceType.Files,
                    file.FileName,
                    file.FullPath,
                    SourceUri: null,
                    SecurityScopedBookmarkBase64: null)
            };
        }

        var firstDirectory = Path.GetDirectoryName(validFiles[0].FullPath);
        var sameDirectory = !string.IsNullOrWhiteSpace(firstDirectory) &&
                            validFiles.All(x => string.Equals(Path.GetDirectoryName(x.FullPath), firstDirectory, StringComparison.OrdinalIgnoreCase));

        if (sameDirectory)
        {
            var directoryName = Path.GetFileName(firstDirectory) ?? "Imported Folder";
            return new[]
            {
                new ImportSource(
                    ImportSourceType.Folder,
                    directoryName,
                    firstDirectory,
                    SourceUri: null,
                    SecurityScopedBookmarkBase64: null)
            };
        }

        return validFiles
            .Select(file => new ImportSource(
                ImportSourceType.Files,
                file.FileName,
                file.FullPath,
                SourceUri: null,
                SecurityScopedBookmarkBase64: null))
            .ToList();
    }

    private partial Task<IReadOnlyList<ImportSource>> PickPlatformSourcesAsync(CancellationToken cancellationToken);
}
