using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class SleepTimerService : ISleepTimerService, IDisposable
{
    private readonly IAudioService _audioService;
    private readonly IPlaybackProgressTracker _progressTracker;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private CancellationTokenSource? _timerCts;

    public SleepTimerService(IAudioService audioService, IPlaybackProgressTracker progressTracker)
    {
        _audioService = audioService;
        _progressTracker = progressTracker;
        CurrentState = new SleepTimerState
        {
            Mode = SleepTimerMode.Off,
            IsActive = false
        };
    }

    public SleepTimerState CurrentState { get; private set; }

    public event EventHandler<SleepTimerState>? SleepTimerStateChanged;

    public async Task StartForDurationAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            CancelActiveTimer_NoLock();

            var endsAt = DateTimeOffset.UtcNow.Add(duration);
            _timerCts = new CancellationTokenSource();

            PublishState(new SleepTimerState
            {
                Mode = SleepTimerMode.Duration,
                IsActive = true,
                EndsAtUtc = endsAt
            });

            _ = RunDurationTimerAsync(endsAt, _timerCts.Token);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task StartUntilEndOfChapterAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            CancelActiveTimer_NoLock();

            PublishState(new SleepTimerState
            {
                Mode = SleepTimerMode.EndOfChapter,
                IsActive = true
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task StartForChapterCountAsync(int chapterCount, CancellationToken cancellationToken = default)
    {
        if (chapterCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chapterCount), "Chapter count must be greater than zero.");
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            CancelActiveTimer_NoLock();

            PublishState(new SleepTimerState
            {
                Mode = SleepTimerMode.ChapterCount,
                IsActive = true,
                RemainingChapters = chapterCount
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            CancelActiveTimer_NoLock();
            PublishState(new SleepTimerState
            {
                Mode = SleepTimerMode.Off,
                IsActive = false
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void OnChapterCompleted()
    {
        _ = HandleChapterCompletedAsync();
    }

    private async Task HandleChapterCompletedAsync()
    {
        var shouldTrigger = false;

        await _syncLock.WaitAsync();
        try
        {
            if (!CurrentState.IsActive)
            {
                return;
            }

            if (CurrentState.Mode == SleepTimerMode.EndOfChapter)
            {
                shouldTrigger = true;
            }
            else if (CurrentState.Mode == SleepTimerMode.ChapterCount)
            {
                var remaining = CurrentState.RemainingChapters ?? 0;
                if (remaining <= 1)
                {
                    shouldTrigger = true;
                }
                else
                {
                    PublishState(new SleepTimerState
                    {
                        Mode = SleepTimerMode.ChapterCount,
                        IsActive = true,
                        RemainingChapters = remaining - 1
                    });
                }
            }
        }
        finally
        {
            _syncLock.Release();
        }

        if (shouldTrigger)
        {
            await TriggerTimerActionAsync();
        }
    }

    private async Task RunDurationTimerAsync(DateTimeOffset endsAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            var delay = endsAtUtc - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            await TriggerTimerActionAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TriggerTimerActionAsync()
    {
        await _audioService.PauseAsync();
        await _progressTracker.FlushAsync();
        await CancelAsync();
    }

    private void CancelActiveTimer_NoLock()
    {
        if (_timerCts is null)
        {
            return;
        }

        _timerCts.Cancel();
        _timerCts.Dispose();
        _timerCts = null;
    }

    private void PublishState(SleepTimerState state)
    {
        CurrentState = state;
        SleepTimerStateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        CancelActiveTimer_NoLock();
        _syncLock.Dispose();
    }
}
