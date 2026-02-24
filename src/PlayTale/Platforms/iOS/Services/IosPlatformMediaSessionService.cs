#if IOS
using AVFoundation;
using Foundation;
using MediaPlayer;
using PlayTale.Features.Audiobooks.Models;
using UIKit;

namespace PlayTale.Features.Audiobooks.Services;

public sealed class IosPlatformMediaSessionService : IPlatformMediaSessionService
{
    private bool _commandsConfigured;

    public event EventHandler? PlayRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? TogglePlayPauseRequested;

    public event EventHandler<double>? SeekRequested;

    public event EventHandler<double>? SkipRequested;

    public async Task ConfigureForBackgroundPlaybackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Playback);
        session.SetActive(true);

        ConfigureRemoteCommands();
        await Task.CompletedTask;
    }

    public void UpdateNowPlaying(PlaybackState state, string? title, string? subtitle)
    {
        var info = new MPNowPlayingInfo();
        SetMemberValue(info, "Title", title ?? "Audiobook");
        SetMemberValue(info, "Artist", subtitle ?? "PlayTale");
        SetMemberValue(info, "PlaybackDuration", Math.Max(0, state.DurationSeconds));
        SetMemberValue(info, "ElapsedPlaybackTime", Math.Max(0, state.PositionSeconds));
        SetMemberValue(info, "PlaybackRate", state.IsPlaying ? state.PlaybackSpeed : 0);

        var center = MPNowPlayingInfoCenter.DefaultCenter;
        var property = center.GetType().GetProperty("NowPlaying");
        property?.SetValue(center, info);
    }

    public void ClearNowPlaying()
    {
        var center = MPNowPlayingInfoCenter.DefaultCenter;
        var property = center.GetType().GetProperty("NowPlaying");
        property?.SetValue(center, null);
    }

    private void ConfigureRemoteCommands()
    {
        if (_commandsConfigured)
        {
            return;
        }

        var commandCenter = MPRemoteCommandCenter.Shared;

        commandCenter.PlayCommand.Enabled = true;
        commandCenter.PlayCommand.AddTarget(_ =>
        {
            PlayRequested?.Invoke(this, EventArgs.Empty);
            return MPRemoteCommandHandlerStatus.Success;
        });

        commandCenter.PauseCommand.Enabled = true;
        commandCenter.PauseCommand.AddTarget(_ =>
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
            return MPRemoteCommandHandlerStatus.Success;
        });

        commandCenter.TogglePlayPauseCommand.Enabled = true;
        commandCenter.TogglePlayPauseCommand.AddTarget(_ =>
        {
            TogglePlayPauseRequested?.Invoke(this, EventArgs.Empty);
            return MPRemoteCommandHandlerStatus.Success;
        });

        commandCenter.ChangePlaybackPositionCommand.Enabled = true;
        commandCenter.ChangePlaybackPositionCommand.AddTarget(commandEvent =>
        {
            if (commandEvent is MPChangePlaybackPositionCommandEvent positionEvent)
            {
                SeekRequested?.Invoke(this, Math.Max(0, positionEvent.PositionTime));
                return MPRemoteCommandHandlerStatus.Success;
            }

            return MPRemoteCommandHandlerStatus.CommandFailed;
        });

        commandCenter.SkipForwardCommand.Enabled = true;
        commandCenter.SkipForwardCommand.PreferredIntervals = new[] { 30d };
        commandCenter.SkipForwardCommand.AddTarget(_ =>
        {
            SkipRequested?.Invoke(this, 30);
            return MPRemoteCommandHandlerStatus.Success;
        });

        commandCenter.SkipBackwardCommand.Enabled = true;
        commandCenter.SkipBackwardCommand.PreferredIntervals = new[] { 15d };
        commandCenter.SkipBackwardCommand.AddTarget(_ =>
        {
            SkipRequested?.Invoke(this, -15);
            return MPRemoteCommandHandlerStatus.Success;
        });

        UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();
        _commandsConfigured = true;
    }

    private static void SetMemberValue(object target, string memberName, object value)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName);
        if (field is not null && !field.IsStatic)
        {
            field.SetValue(target, value);
        }
    }
}
#endif
