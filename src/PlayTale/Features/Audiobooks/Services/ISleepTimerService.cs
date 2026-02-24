using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface ISleepTimerService
{
    SleepTimerState CurrentState { get; }

    event EventHandler<SleepTimerState>? SleepTimerStateChanged;

    Task StartForDurationAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    Task StartUntilEndOfChapterAsync(CancellationToken cancellationToken = default);

    Task StartForChapterCountAsync(int chapterCount, CancellationToken cancellationToken = default);

    Task CancelAsync(CancellationToken cancellationToken = default);

    void OnChapterCompleted();
}
