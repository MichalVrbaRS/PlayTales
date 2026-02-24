using SQLite;

namespace PlayTale.Features.Audiobooks.Storage;

[Table("Books")]
public sealed class BookRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [NotNull]
    public string Title { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? CoverPath { get; set; }

    public int LastChapterIndex { get; set; }

    public double LastPositionSeconds { get; set; }

    [Indexed]
    public long UpdatedAtUtcTicks { get; set; }
}
