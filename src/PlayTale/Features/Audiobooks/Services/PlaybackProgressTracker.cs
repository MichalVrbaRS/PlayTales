using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class PlaybackProgressTracker : IPlaybackProgressTracker, IDisposable
{
    private const double AutoSaveIntervalSeconds = 5;

    private readonly IAudioService _audioService;
    private readonly ILibraryService _libraryService;
    private readonly SemaphoreSlim _eventLock = new(1, 1);
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private readonly Dictionary<Guid, Dictionary<Guid, int>> _chapterIndexCache = new();
    private PlaybackState? _lastObservedState;
    private PlaybackState? _lastSavedState;

    public PlaybackProgressTracker(IAudioService audioService, ILibraryService libraryService)
    {
        _audioService = audioService;
        _libraryService = libraryService;

        _audioService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioService.PlaybackEnded += OnPlaybackEnded;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var state = _audioService.CurrentState;
        return SaveProgressIfValidAsync(state, cancellationToken);
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        _ = HandlePlaybackStateChangedAsync(state);
    }

    private async Task HandlePlaybackStateChangedAsync(PlaybackState state)
    {
        await _eventLock.WaitAsync();
        try
        {
            var previous = _lastObservedState;
            _lastObservedState = state;

            if (previous is not null && HasValidProgress(previous) && IsChapterChanged(previous, state))
            {
                await SaveProgressIfValidAsync(previous);
            }

            if (HasValidProgress(state))
            {
                if (ShouldAutoSave(state))
                {
                    await SaveProgressIfValidAsync(state);
                }
                else if (previous?.IsPlaying == true && !state.IsPlaying)
                {
                    await SaveProgressIfValidAsync(state);
                }
            }
        }
        finally
        {
            _eventLock.Release();
        }
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        _ = SaveProgressIfValidAsync(_audioService.CurrentState);
    }

    private bool ShouldAutoSave(PlaybackState state)
    {
        if (!state.IsPlaying)
        {
            return false;
        }

        if (_lastSavedState is null)
        {
            return true;
        }

        if (_lastSavedState.BookId != state.BookId || _lastSavedState.ChapterId != state.ChapterId)
        {
            return true;
        }

        return state.PositionSeconds - _lastSavedState.PositionSeconds >= AutoSaveIntervalSeconds;
    }

    private static bool IsChapterChanged(PlaybackState previous, PlaybackState current)
    {
        return previous.BookId != current.BookId || previous.ChapterId != current.ChapterId;
    }

    private static bool HasValidProgress(PlaybackState state)
    {
        return state.BookId is not null && state.ChapterId is not null;
    }

    private async Task SaveProgressIfValidAsync(PlaybackState state, CancellationToken cancellationToken = default)
    {
        if (!HasValidProgress(state))
        {
            return;
        }

        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            var bookId = state.BookId!.Value;
            var chapterId = state.ChapterId!.Value;
            var chapterIndex = await GetChapterIndexAsync(bookId, chapterId, cancellationToken);
            if (chapterIndex < 0)
            {
                return;
            }

            var progress = new PlaybackProgress(
                BookId: bookId,
                ChapterId: chapterId,
                ChapterIndex: chapterIndex,
                PositionSeconds: Math.Max(0, state.PositionSeconds));

            await _libraryService.SaveProgressAsync(progress, cancellationToken);
            _lastSavedState = state;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task<int> GetChapterIndexAsync(Guid bookId, Guid chapterId, CancellationToken cancellationToken)
    {
        if (_chapterIndexCache.TryGetValue(bookId, out var chapterMap) &&
            chapterMap.TryGetValue(chapterId, out var cachedIndex))
        {
            return cachedIndex;
        }

        var chapters = await _libraryService.GetChaptersAsync(bookId, cancellationToken);
        var builtMap = chapters
            .OrderBy(x => x.OrderIndex)
            .Select((chapter, index) => new { chapter.Id, Index = index })
            .ToDictionary(x => x.Id, x => x.Index);

        _chapterIndexCache[bookId] = builtMap;
        return builtMap.TryGetValue(chapterId, out var index) ? index : -1;
    }

    public void Dispose()
    {
        _audioService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioService.PlaybackEnded -= OnPlaybackEnded;
        _eventLock.Dispose();
        _saveLock.Dispose();
    }
}
