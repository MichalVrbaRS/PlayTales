using Microsoft.Extensions.DependencyInjection;
using Plugin.Maui.Audio;
using PlayTale.Features.Audiobooks.State;
using PlayTale.Features.Audiobooks.Storage;

namespace PlayTale.Features.Audiobooks.Services;

public static class AudiobooksServiceCollectionExtensions
{
    public static IServiceCollection AddAudiobooksFeature(this IServiceCollection services)
    {
        services.AddSingleton<AudiobooksDatabase>();
        services.AddSingleton<AudiobooksFeatureState>();
#if IOS
        services.AddSingleton<IPlatformMediaSessionService, IosPlatformMediaSessionService>();
#else
        services.AddSingleton<IPlatformMediaSessionService, NoOpPlatformMediaSessionService>();
#endif
        services.AddSingleton<IAudioManager>(_ => AudioManager.Current);
        services.AddSingleton<IAudioService, MauiAudioService>();
        services.AddSingleton<IImportService, PlatformImportService>();
        services.AddSingleton<ILibraryRepository, SqliteLibraryRepository>();
        services.AddSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<IAudiobooksSettingsStore, SqliteAudiobooksSettingsStore>();
        services.AddSingleton<IPlaybackProgressTracker, PlaybackProgressTracker>();
        services.AddSingleton<ISleepTimerService, SleepTimerService>();
        return services;
    }
}
