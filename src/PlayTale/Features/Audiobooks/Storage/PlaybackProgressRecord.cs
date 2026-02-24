using SQLite;

namespace PlayTale.Features.Audiobooks.Storage;

[Table("PlaybackProgress")]
public sealed class PlaybackProgressRecord
{
    [PrimaryKey]
    public string BookId { get; set; } = string.Empty;

    [NotNull]
    public string ChapterId { get; set; } = string.Empty;

    public int ChapterIndex { get; set; }

    public double PositionSeconds { get; set; }

    [Indexed]
    public long UpdatedAtUtcTicks { get; set; }
}
