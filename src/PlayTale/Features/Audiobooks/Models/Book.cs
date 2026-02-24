namespace PlayTale.Features.Audiobooks.Models;

public sealed class Book
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    public string? Author { get; init; }

    public string? CoverPath { get; init; }

    public int LastChapterIndex { get; init; }

    public double LastPositionSeconds { get; init; }
}
