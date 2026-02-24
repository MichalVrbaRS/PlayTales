namespace PlayTale.Features.Audiobooks.Models;

public sealed class Chapter
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid BookId { get; init; }

    public string Title { get; init; } = string.Empty;

    public int OrderIndex { get; init; }

    public double DurationSeconds { get; init; }

    public string FilePath { get; init; } = string.Empty;

    public string? SourceUri { get; init; }

    public string? SecurityScopedBookmarkBase64 { get; init; }

    public double LastPositionSeconds { get; init; }
}
