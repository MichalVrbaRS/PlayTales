namespace PlayTale.Features.Audiobooks.Models;

public sealed class PlaybackState
{
    public Guid? BookId { get; init; }

    public Guid? ChapterId { get; init; }

    public double PositionSeconds { get; init; }

    public double DurationSeconds { get; init; }

    public double PlaybackSpeed { get; init; } = 1.0;

    public bool IsPlaying { get; init; }
}
