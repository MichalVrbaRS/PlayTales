using SQLite;

namespace PlayTale.Features.Audiobooks.Storage;

[Table("Chapters")]
public sealed class ChapterRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    [NotNull]
    public string BookId { get; set; } = string.Empty;

    [NotNull]
    public string Title { get; set; } = string.Empty;

    [Indexed]
    public int OrderIndex { get; set; }

    public double DurationSeconds { get; set; }

    [NotNull]
    public string FilePath { get; set; } = string.Empty;

    public string? SourceUri { get; set; }

    public string? SecurityScopedBookmarkBase64 { get; set; }

    public double LastPositionSeconds { get; set; }
}
