using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface IAudioService
{
    PlaybackState CurrentState { get; }

    event EventHandler<PlaybackState>? PlaybackStateChanged;

    event EventHandler? PlaybackEnded;

    Task PlayAsync(Chapter chapter, double startPositionSeconds = 0, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SeekAsync(double positionSeconds, CancellationToken cancellationToken = default);

    Task SkipByAsync(double deltaSeconds, CancellationToken cancellationToken = default);

    Task SetPlaybackSpeedAsync(double speed, CancellationToken cancellationToken = default);
}
