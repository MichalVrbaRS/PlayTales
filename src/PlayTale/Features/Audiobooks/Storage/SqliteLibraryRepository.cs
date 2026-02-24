using PlayTale.Features.Audiobooks.Models;
using PlayTale.Features.Audiobooks.Services;

namespace PlayTale.Features.Audiobooks.Storage;

public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private readonly AudiobooksDatabase _database;

    public SqliteLibraryRepository(AudiobooksDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<Book>> GetBooksAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);

        var records = await connection
            .Table<BookRecord>()
            .OrderByDescending(x => x.UpdatedAtUtcTicks)
            .ToListAsync();

        return records.Select(x => x.ToDomain()).ToList();
    }

    public async Task<Book?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);
        var key = bookId.ToString("D");

        var record = await connection.FindAsync<BookRecord>(key);
        return record?.ToDomain();
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);
        var key = bookId.ToString("D");

        var records = await connection
            .Table<ChapterRecord>()
            .Where(x => x.BookId == key)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

        return records.Select(x => x.ToDomain()).ToList();
    }

    public async Task SaveBookAsync(Book book, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);

        await connection.InsertOrReplaceAsync(book.ToRecord());
    }

    public async Task SaveChaptersAsync(Guid bookId, IReadOnlyList<Chapter> chapters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);
        var key = bookId.ToString("D");

        await connection.ExecuteAsync("DELETE FROM Chapters WHERE BookId = ?", key);

        if (chapters.Count == 0)
        {
            return;
        }

        var records = chapters.Select(x => x.ToRecord(bookId)).ToList();
        await connection.InsertAllAsync(records);
    }

    public async Task SaveProgressAsync(PlaybackProgress progress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);

        var record = progress.ToRecord();
        await connection.InsertOrReplaceAsync(record);

        await connection.ExecuteAsync(
            "UPDATE Books SET LastChapterIndex = ?, LastPositionSeconds = ?, UpdatedAtUtcTicks = ? WHERE Id = ?",
            progress.ChapterIndex,
            progress.PositionSeconds,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            progress.BookId.ToString("D"));

        await connection.ExecuteAsync(
            "UPDATE Chapters SET LastPositionSeconds = ? WHERE Id = ?",
            progress.PositionSeconds,
            progress.ChapterId.ToString("D"));
    }

    public async Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);
        var key = bookId.ToString("D");

        var appImportsRoot = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "imports");
        var chapterRecords = await connection
            .Table<ChapterRecord>()
            .Where(x => x.BookId == key)
            .ToListAsync();
        var chapterPaths = chapterRecords.Select(x => x.FilePath).ToList();

        await connection.ExecuteAsync("DELETE FROM Chapters WHERE BookId = ?", key);
        await connection.ExecuteAsync("DELETE FROM PlaybackProgress WHERE BookId = ?", key);
        await connection.ExecuteAsync("DELETE FROM Books WHERE Id = ?", key);

        foreach (var chapterPath in chapterPaths)
        {
            if (string.IsNullOrWhiteSpace(chapterPath))
            {
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(chapterPath);
                if (fullPath.StartsWith(appImportsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Keep delete flow non-blocking even if cleanup fails.
            }
        }
    }
}
