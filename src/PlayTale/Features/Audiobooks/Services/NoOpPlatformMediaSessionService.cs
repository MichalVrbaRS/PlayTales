using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class NoOpPlatformMediaSessionService : IPlatformMediaSessionService
{
    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? TogglePlayPauseRequested;
    public event EventHandler<double>? SeekRequested;
    public event EventHandler<double>? SkipRequested;

    public Task ConfigureForBackgroundPlaybackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void UpdateNowPlaying(PlaybackState state, string? title, string? subtitle)
    {
    }

    public void ClearNowPlaying()
    {
    }
}
