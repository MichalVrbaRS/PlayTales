#if ANDROID
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Views;
using AudioPlaybackState = PlayTale.Features.Audiobooks.Models.PlaybackState;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class AndroidPlatformMediaSessionService : Java.Lang.Object, IPlatformMediaSessionService
{
    private readonly MediaSession _mediaSession;
    private bool _configured;

    public AndroidPlatformMediaSessionService()
    {
        var context = Android.App.Application.Context;
        _mediaSession = new MediaSession(context, "PlayTaleMediaSession");
        _mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
        _mediaSession.SetCallback(new SessionCallback(this));
    }

    public event EventHandler? PlayRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? TogglePlayPauseRequested;

    public event EventHandler<double>? SeekRequested;

    public event EventHandler<double>? SkipRequested;

    public Task ConfigureForBackgroundPlaybackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _mediaSession.Active = true;
        _configured = true;
        return Task.CompletedTask;
    }

    public void UpdateNowPlaying(AudioPlaybackState state, string? title, string? subtitle)
    {
        EnsureConfigured();

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString("android.media.metadata.ALBUM", title ?? "Audiobook");
        metadataBuilder.PutString("android.media.metadata.ARTIST", title ?? "PlayTale");
        metadataBuilder.PutString("android.media.metadata.TITLE", subtitle ?? "Chapter");
        metadataBuilder.PutLong("android.media.metadata.DURATION", (long)Math.Max(0, state.DurationSeconds * 1000));
        var metadata = metadataBuilder.Build();

        var actions = Android.Media.Session.PlaybackState.ActionPlay
                      | Android.Media.Session.PlaybackState.ActionPause
                      | Android.Media.Session.PlaybackState.ActionPlayPause
                      | Android.Media.Session.PlaybackState.ActionSeekTo
                      | Android.Media.Session.PlaybackState.ActionFastForward
                      | Android.Media.Session.PlaybackState.ActionRewind
                      | Android.Media.Session.PlaybackState.ActionSkipToNext
                      | Android.Media.Session.PlaybackState.ActionSkipToPrevious;

        var playbackStateBuilder = new Android.Media.Session.PlaybackState.Builder();
        playbackStateBuilder.SetActions(actions);
        playbackStateBuilder.SetState(
            state.IsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused,
            (long)Math.Max(0, state.PositionSeconds * 1000),
            state.IsPlaying ? (float)Math.Max(0.1, state.PlaybackSpeed) : 0f);
        var playbackState = playbackStateBuilder.Build();

        _mediaSession.SetMetadata(metadata);
        _mediaSession.SetPlaybackState(playbackState);
    }

    public void ClearNowPlaying()
    {
        if (!_configured)
        {
            return;
        }

        var playbackStateBuilder = new Android.Media.Session.PlaybackState.Builder();
        playbackStateBuilder.SetActions(Android.Media.Session.PlaybackState.ActionPlay | Android.Media.Session.PlaybackState.ActionPlayPause);
        playbackStateBuilder.SetState(PlaybackStateCode.Stopped, 0, 0);
        var playbackState = playbackStateBuilder.Build();

        _mediaSession.SetPlaybackState(playbackState);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mediaSession.Release();
        }

        base.Dispose(disposing);
    }

    private void EnsureConfigured()
    {
        if (_configured)
        {
            return;
        }

        _mediaSession.Active = true;
        _configured = true;
    }

    private sealed class SessionCallback : MediaSession.Callback
    {
        private readonly AndroidPlatformMediaSessionService _owner;

        public SessionCallback(AndroidPlatformMediaSessionService owner)
        {
            _owner = owner;
        }

        public override void OnPlay()
        {
            _owner.PlayRequested?.Invoke(_owner, EventArgs.Empty);
        }

        public override void OnPause()
        {
            _owner.PauseRequested?.Invoke(_owner, EventArgs.Empty);
        }

        public override void OnStop()
        {
            _owner.PauseRequested?.Invoke(_owner, EventArgs.Empty);
        }

        public override void OnSeekTo(long pos)
        {
            _owner.SeekRequested?.Invoke(_owner, Math.Max(0, pos / 1000d));
        }

        public override void OnFastForward()
        {
            _owner.SkipRequested?.Invoke(_owner, 30);
        }

        public override void OnRewind()
        {
            _owner.SkipRequested?.Invoke(_owner, -15);
        }

        public override void OnSkipToNext()
        {
            _owner.SkipRequested?.Invoke(_owner, 30);
        }

        public override void OnSkipToPrevious()
        {
            _owner.SkipRequested?.Invoke(_owner, -15);
        }

        public override bool OnMediaButtonEvent(Intent? mediaButtonEvent)
        {
            if (mediaButtonEvent is null)
            {
                return false;
            }

            KeyEvent? keyEvent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
#pragma warning disable CA1416
                keyEvent = mediaButtonEvent.GetParcelableExtra(Intent.ExtraKeyEvent, Java.Lang.Class.FromType(typeof(KeyEvent))) as KeyEvent;
#pragma warning restore CA1416
            }
            else
            {
#pragma warning disable CA1422
                keyEvent = mediaButtonEvent.GetParcelableExtra(Intent.ExtraKeyEvent) as KeyEvent;
#pragma warning restore CA1422
            }

            if (keyEvent is null || keyEvent.Action != KeyEventActions.Down)
            {
                return base.OnMediaButtonEvent(mediaButtonEvent);
            }

            if (keyEvent.KeyCode == Keycode.MediaPlayPause)
            {
                _owner.TogglePlayPauseRequested?.Invoke(_owner, EventArgs.Empty);
                return true;
            }

            return base.OnMediaButtonEvent(mediaButtonEvent);
        }
    }
}
#endif
