using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface IPlatformMediaSessionService
{
    event EventHandler? PlayRequested;

    event EventHandler? PauseRequested;

    event EventHandler? TogglePlayPauseRequested;

    event EventHandler<double>? SeekRequested;

    event EventHandler<double>? SkipRequested;

    Task ConfigureForBackgroundPlaybackAsync(CancellationToken cancellationToken = default);

    void UpdateNowPlaying(PlaybackState state, string? title, string? subtitle);

    void ClearNowPlaying();
}
