using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class LibraryService : ILibraryService
{
    private readonly ILibraryRepository _repository;
    private readonly IImportService _importService;
    private readonly IAudiobooksSettingsStore _settingsStore;
    private readonly IBookCoverService _bookCoverService;

    public LibraryService(
        ILibraryRepository repository,
        IImportService importService,
        IAudiobooksSettingsStore settingsStore,
        IBookCoverService bookCoverService)
    {
        _repository = repository;
        _importService = importService;
        _settingsStore = settingsStore;
        _bookCoverService = bookCoverService;
    }

    public Task<IReadOnlyList<Book>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetBooksAsync(cancellationToken);
    }

    public Task<Book?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        return _repository.GetBookAsync(bookId, cancellationToken);
    }

    public Task<IReadOnlyList<Chapter>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        return _repository.GetChaptersAsync(bookId, cancellationToken);
    }

    public async Task<Book> ImportAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _importService.PickFolderOrFilesAsync(cancellationToken);
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("No files were selected for import.");
        }

        var chapters = new List<Chapter>();
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceChapters = await _importService.ScanChaptersAsync(source, cancellationToken);
            chapters.AddRange(sourceChapters);
        }

        if (chapters.Count == 0)
        {
            throw new InvalidOperationException("No supported audio files were found.");
        }

        var orderedChapters = chapters
            .OrderBy(x => x.OrderIndex)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .Select((chapter, index) => new Chapter
            {
                Id = chapter.Id,
                BookId = chapter.BookId,
                Title = chapter.Title,
                OrderIndex = index,
                DurationSeconds = chapter.DurationSeconds,
                FilePath = chapter.FilePath,
                SourceUri = chapter.SourceUri,
                SecurityScopedBookmarkBase64 = chapter.SecurityScopedBookmarkBase64,
                LastPositionSeconds = chapter.LastPositionSeconds
            })
            .ToList();

        var title = ResolveBookTitle(sources, orderedChapters);

        var duplicateBook = await FindDuplicateBookAsync(title, orderedChapters, cancellationToken);
        if (duplicateBook is not null)
        {
            await _settingsStore.SetLastOpenedBookIdAsync(duplicateBook.Id, cancellationToken);
            return duplicateBook;
        }

        var book = new Book
        {
            Id = Guid.NewGuid(),
            Title = title,
            Author = null,
            CoverPath = null,
            LastChapterIndex = 0,
            LastPositionSeconds = 0
        };

        var linkedChapters = orderedChapters
            .Select(chapter => new Chapter
            {
                Id = chapter.Id == Guid.Empty ? Guid.NewGuid() : chapter.Id,
                BookId = book.Id,
                Title = chapter.Title,
                OrderIndex = chapter.OrderIndex,
                DurationSeconds = chapter.DurationSeconds,
                FilePath = chapter.FilePath,
                SourceUri = chapter.SourceUri,
                SecurityScopedBookmarkBase64 = chapter.SecurityScopedBookmarkBase64,
                LastPositionSeconds = 0
            })
            .ToList();

        var coverPath = await _bookCoverService.ResolveCoverPathAsync(title, author: null, linkedChapters, cancellationToken);

        await _repository.SaveBookAsync(book, cancellationToken);
        if (!string.IsNullOrWhiteSpace(coverPath))
        {
            book = new Book
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                CoverPath = coverPath,
                LastChapterIndex = book.LastChapterIndex,
                LastPositionSeconds = book.LastPositionSeconds
            };
            await _repository.SaveBookAsync(book, cancellationToken);
        }
        await _repository.SaveChaptersAsync(book.Id, linkedChapters, cancellationToken);
        await _settingsStore.SetLastOpenedBookIdAsync(book.Id, cancellationToken);

        return book;
    }

    public Task SaveProgressAsync(PlaybackProgress progress, CancellationToken cancellationToken = default)
    {
        return _repository.SaveProgressAsync(progress, cancellationToken);
    }

    public async Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteBookAsync(bookId, cancellationToken);

        var lastOpenedBook = await _settingsStore.GetLastOpenedBookIdAsync(cancellationToken);
        if (lastOpenedBook == bookId)
        {
            await _settingsStore.SetLastOpenedBookIdAsync(null, cancellationToken);
        }
    }

    private static string ResolveBookTitle(IReadOnlyList<ImportSource> sources, IReadOnlyList<Chapter> chapters)
    {
        if (sources.Count == 1 && !string.IsNullOrWhiteSpace(sources[0].DisplayName))
        {
            return sources[0].DisplayName;
        }

        var firstDirectory = Path.GetDirectoryName(chapters[0].FilePath);
        if (!string.IsNullOrWhiteSpace(firstDirectory))
        {
            var directoryName = Path.GetFileName(firstDirectory);
            if (!string.IsNullOrWhiteSpace(directoryName))
            {
                return directoryName;
            }
        }

        return $"Imported {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    private async Task<Book?> FindDuplicateBookAsync(
        string candidateTitle,
        IReadOnlyList<Chapter> candidateChapters,
        CancellationToken cancellationToken)
    {
        var books = await _repository.GetBooksAsync(cancellationToken);
        var candidateSignature = BuildChapterSignature(candidateChapters);

        foreach (var book in books)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(book.Title.Trim(), candidateTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existingChapters = await _repository.GetChaptersAsync(book.Id, cancellationToken);
            var existingSignature = BuildChapterSignature(existingChapters);
            if (string.Equals(existingSignature, candidateSignature, StringComparison.Ordinal))
            {
                return book;
            }
        }

        return null;
    }

    private static string BuildChapterSignature(IReadOnlyList<Chapter> chapters)
    {
        return string.Join(
            "|",
            chapters
                .OrderBy(x => x.OrderIndex)
                .Select(x =>
                {
                    return $"{x.OrderIndex}:{NormalizeToken(x.Title)}";
                }));
    }

    private static string NormalizeToken(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
