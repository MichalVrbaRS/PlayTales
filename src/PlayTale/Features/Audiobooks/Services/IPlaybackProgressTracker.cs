namespace PlayTale.Features.Audiobooks.Services;

public interface IPlaybackProgressTracker
{
    Task FlushAsync(CancellationToken cancellationToken = default);
}
