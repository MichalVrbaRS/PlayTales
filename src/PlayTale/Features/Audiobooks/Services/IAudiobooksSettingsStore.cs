namespace PlayTale.Features.Audiobooks.Services;

public interface IAudiobooksSettingsStore
{
    Task<double> GetPlaybackSpeedAsync(CancellationToken cancellationToken = default);

    Task SetPlaybackSpeedAsync(double speed, CancellationToken cancellationToken = default);

    Task<Guid?> GetLastOpenedBookIdAsync(CancellationToken cancellationToken = default);

    Task SetLastOpenedBookIdAsync(Guid? bookId, CancellationToken cancellationToken = default);
}
