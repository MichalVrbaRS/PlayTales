using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Storage;

internal static class AudiobooksMappingExtensions
{
    public static Book ToDomain(this BookRecord record)
    {
        return new Book
        {
            Id = ParseGuid(record.Id),
            Title = record.Title,
            Author = record.Author,
            CoverPath = record.CoverPath,
            LastChapterIndex = record.LastChapterIndex,
            LastPositionSeconds = record.LastPositionSeconds
        };
    }

    public static BookRecord ToRecord(this Book model)
    {
        return new BookRecord
        {
            Id = model.Id.ToString("D"),
            Title = model.Title,
            Author = model.Author,
            CoverPath = model.CoverPath,
            LastChapterIndex = model.LastChapterIndex,
            LastPositionSeconds = model.LastPositionSeconds,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public static Chapter ToDomain(this ChapterRecord record)
    {
        return new Chapter
        {
            Id = ParseGuid(record.Id),
            BookId = ParseGuid(record.BookId),
            Title = record.Title,
            OrderIndex = record.OrderIndex,
            DurationSeconds = record.DurationSeconds,
            FilePath = record.FilePath,
            SourceUri = record.SourceUri,
            SecurityScopedBookmarkBase64 = record.SecurityScopedBookmarkBase64,
            LastPositionSeconds = record.LastPositionSeconds
        };
    }

    public static ChapterRecord ToRecord(this Chapter model, Guid bookId)
    {
        return new ChapterRecord
        {
            Id = model.Id.ToString("D"),
            BookId = bookId.ToString("D"),
            Title = model.Title,
            OrderIndex = model.OrderIndex,
            DurationSeconds = model.DurationSeconds,
            FilePath = model.FilePath,
            SourceUri = model.SourceUri,
            SecurityScopedBookmarkBase64 = model.SecurityScopedBookmarkBase64,
            LastPositionSeconds = model.LastPositionSeconds
        };
    }

    public static PlaybackProgressRecord ToRecord(this PlaybackProgress model)
    {
        return new PlaybackProgressRecord
        {
            BookId = model.BookId.ToString("D"),
            ChapterId = model.ChapterId.ToString("D"),
            ChapterIndex = model.ChapterIndex,
            PositionSeconds = model.PositionSeconds,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static Guid ParseGuid(string value)
    {
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
