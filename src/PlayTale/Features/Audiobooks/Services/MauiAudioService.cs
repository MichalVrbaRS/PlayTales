using Plugin.Maui.Audio;
using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class MauiAudioService : IAudioService
{
    private const double MinPlaybackSpeed = 0.75;
    private const double MaxPlaybackSpeed = 2.0;

    private readonly IAudioManager _audioManager;
    private readonly IPlatformMediaSessionService _platformMediaSessionService;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    private IAudioPlayer? _player;
    private Stream? _currentStream;
    private Chapter? _currentChapter;
    private string? _currentBookTitle;
    private string? _currentChapterTitleOverride;
    private CancellationTokenSource? _pollingCts;

    public MauiAudioService(IAudioManager audioManager, IPlatformMediaSessionService platformMediaSessionService)
    {
        _audioManager = audioManager;
        _platformMediaSessionService = platformMediaSessionService;
        CurrentState = new PlaybackState();

        _platformMediaSessionService.PlayRequested += OnRemotePlayRequested;
        _platformMediaSessionService.PauseRequested += OnRemotePauseRequested;
        _platformMediaSessionService.TogglePlayPauseRequested += OnRemoteToggleRequested;
        _platformMediaSessionService.SeekRequested += OnRemoteSeekRequested;
        _platformMediaSessionService.SkipRequested += OnRemoteSkipRequested;
        _ = _platformMediaSessionService.ConfigureForBackgroundPlaybackAsync();
    }

    public PlaybackState CurrentState { get; private set; }

    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    public event EventHandler? PlaybackEnded;

    public void SetNowPlayingMetadata(string? bookTitle, string? chapterTitle = null)
    {
        _currentBookTitle = string.IsNullOrWhiteSpace(bookTitle) ? null : bookTitle.Trim();
        _currentChapterTitleOverride = string.IsNullOrWhiteSpace(chapterTitle) ? null : chapterTitle.Trim();
        var labels = ResolveNowPlayingLabels();
        _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
    }

    public async Task PlayAsync(Chapter chapter, double startPositionSeconds = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        if (string.IsNullOrWhiteSpace(chapter.FilePath) || !File.Exists(chapter.FilePath))
        {
            throw new FileNotFoundException("Audio file not found.", chapter.FilePath);
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync(disposeState: false, cancellationToken);

            _currentChapter = chapter;
            _currentStream = File.OpenRead(chapter.FilePath);
            _player = _audioManager.CreatePlayer(_currentStream);

            _player.PlaybackEnded += OnPlaybackEnded;
            TrySetPlayerSpeed(_player, CurrentState.PlaybackSpeed);

            if (startPositionSeconds > 0)
            {
                TrySeek(_player, startPositionSeconds);
            }

            _player.Play();
            StartPolling();

            PublishState(new PlaybackState
            {
                BookId = chapter.BookId,
                ChapterId = chapter.Id,
                PositionSeconds = GetCurrentPositionSeconds(_player),
                DurationSeconds = GetDurationSeconds(_player),
                PlaybackSpeed = CurrentState.PlaybackSpeed <= 0 ? 1.0 : CurrentState.PlaybackSpeed,
                IsPlaying = _player.IsPlaying
            });
            var labels = ResolveNowPlayingLabels();
            _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            _player?.Pause();
            PublishStateWithPlayer();
            var labels = ResolveNowPlayingLabels();
            _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_player is null)
            {
                return;
            }

            _player.Play();
            StartPolling();
            PublishStateWithPlayer();
            var labels = ResolveNowPlayingLabels();
            _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync(disposeState: true, cancellationToken);
            PublishState(new PlaybackState
            {
                PlaybackSpeed = CurrentState.PlaybackSpeed <= 0 ? 1.0 : CurrentState.PlaybackSpeed
            });
            _platformMediaSessionService.ClearNowPlaying();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task SeekAsync(double positionSeconds, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_player is null)
            {
                return;
            }

            var target = Math.Max(0, positionSeconds);
            TrySeek(_player, target);
            PublishStateWithPlayer();
            var labels = ResolveNowPlayingLabels();
            _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task SkipByAsync(double deltaSeconds, CancellationToken cancellationToken = default)
    {
        var next = Math.Max(0, CurrentState.PositionSeconds + deltaSeconds);
        return SeekAsync(next, cancellationToken);
    }

    public async Task SetPlaybackSpeedAsync(double speed, CancellationToken cancellationToken = default)
    {
        var normalizedSpeed = Math.Clamp(speed, MinPlaybackSpeed, MaxPlaybackSpeed);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_player is not null)
            {
                TrySetPlayerSpeed(_player, normalizedSpeed);
            }

            PublishState(new PlaybackState
            {
                BookId = CurrentState.BookId,
                ChapterId = CurrentState.ChapterId,
                PositionSeconds = CurrentState.PositionSeconds,
                DurationSeconds = CurrentState.DurationSeconds,
                PlaybackSpeed = normalizedSpeed,
                IsPlaying = _player?.IsPlaying ?? false
            });
            var labels = ResolveNowPlayingLabels();
            _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task StopInternalAsync(bool disposeState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StopPolling();

        if (_player is not null)
        {
            _player.PlaybackEnded -= OnPlaybackEnded;
            _player.Stop();
            _player.Dispose();
            _player = null;
        }

        if (_currentStream is not null)
        {
            await _currentStream.DisposeAsync();
            _currentStream = null;
        }

        if (disposeState)
        {
            _currentChapter = null;
        }
    }

    private void StartPolling()
    {
        StopPolling();

        if (_player is null)
        {
            return;
        }

        _pollingCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(_pollInterval);
                while (await timer.WaitForNextTickAsync(_pollingCts.Token))
                {
                    await _syncLock.WaitAsync(_pollingCts.Token);
                    try
                    {
                        if (_player is null)
                        {
                            return;
                        }

                        PublishStateWithPlayer();
                    }
                    finally
                    {
                        _syncLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void StopPolling()
    {
        if (_pollingCts is null)
        {
            return;
        }

        _pollingCts.Cancel();
        _pollingCts.Dispose();
        _pollingCts = null;
    }

    private void PublishStateWithPlayer()
    {
        PublishState(new PlaybackState
        {
            BookId = _currentChapter?.BookId,
            ChapterId = _currentChapter?.Id,
            PositionSeconds = GetCurrentPositionSeconds(_player),
            DurationSeconds = GetDurationSeconds(_player),
            PlaybackSpeed = CurrentState.PlaybackSpeed <= 0 ? 1.0 : CurrentState.PlaybackSpeed,
            IsPlaying = _player?.IsPlaying ?? false
        });
        var labels = ResolveNowPlayingLabels();
        _platformMediaSessionService.UpdateNowPlaying(CurrentState, labels.BookTitle, labels.ChapterTitle);
    }

    private void PublishState(PlaybackState state)
    {
        CurrentState = state;
        PlaybackStateChanged?.Invoke(this, state);
    }

    private void OnPlaybackEnded(object? sender, EventArgs args)
    {
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
        PublishStateWithPlayer();
    }

    private void OnRemotePlayRequested(object? sender, EventArgs args)
    {
        _ = ResumeAsync();
    }

    private void OnRemotePauseRequested(object? sender, EventArgs args)
    {
        _ = PauseAsync();
    }

    private void OnRemoteToggleRequested(object? sender, EventArgs args)
    {
        if (CurrentState.IsPlaying)
        {
            _ = PauseAsync();
            return;
        }

        _ = ResumeAsync();
    }

    private void OnRemoteSeekRequested(object? sender, double positionSeconds)
    {
        _ = SeekAsync(positionSeconds);
    }

    private void OnRemoteSkipRequested(object? sender, double deltaSeconds)
    {
        _ = SkipByAsync(deltaSeconds);
    }

    private static double GetCurrentPositionSeconds(IAudioPlayer? player)
    {
        if (player is null)
        {
            return 0;
        }

        return player.CurrentPosition;
    }

    private static double GetDurationSeconds(IAudioPlayer? player)
    {
        if (player is null)
        {
            return 0;
        }

        return player.Duration;
    }

    private static void TrySeek(IAudioPlayer player, double positionSeconds)
    {
        player.Seek(positionSeconds);
    }

    private static void TrySetPlayerSpeed(IAudioPlayer player, double speed)
    {
        if (!player.CanSetSpeed)
        {
            return;
        }

        player.Speed = speed;
    }

    private (string BookTitle, string ChapterTitle) ResolveNowPlayingLabels()
    {
        var chapterTitle = _currentChapterTitleOverride ?? _currentChapter?.Title ?? "Chapter";
        var bookTitle = _currentBookTitle;

        if (string.IsNullOrWhiteSpace(bookTitle))
        {
            var directory = _currentChapter is null ? null : Path.GetDirectoryName(_currentChapter.FilePath);
            var inferred = string.IsNullOrWhiteSpace(directory) ? null : Path.GetFileName(directory);
            bookTitle = string.IsNullOrWhiteSpace(inferred) ? "Audiobook" : inferred;
        }

        return (bookTitle, chapterTitle);
    }
}
