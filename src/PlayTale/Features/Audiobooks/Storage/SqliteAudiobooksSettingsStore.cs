using PlayTale.Features.Audiobooks.Services;

namespace PlayTale.Features.Audiobooks.Storage;

public sealed class SqliteAudiobooksSettingsStore : IAudiobooksSettingsStore
{
    private const string PlaybackSpeedKey = "playback_speed";
    private const string LastOpenedBookIdKey = "last_opened_book_id";

    private readonly AudiobooksDatabase _database;

    public SqliteAudiobooksSettingsStore(AudiobooksDatabase database)
    {
        _database = database;
    }

    public async Task<double> GetPlaybackSpeedAsync(CancellationToken cancellationToken = default)
    {
        var rawValue = await GetValueAsync(PlaybackSpeedKey, cancellationToken);
        return double.TryParse(
            rawValue,
            System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
            System.Globalization.CultureInfo.InvariantCulture,
            out var speed)
            ? speed
            : 1.0;
    }

    public Task SetPlaybackSpeedAsync(double speed, CancellationToken cancellationToken = default)
    {
        return SetValueAsync(PlaybackSpeedKey, speed.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task<Guid?> GetLastOpenedBookIdAsync(CancellationToken cancellationToken = default)
    {
        var rawValue = await GetValueAsync(LastOpenedBookIdKey, cancellationToken);
        return Guid.TryParse(rawValue, out var id) ? id : null;
    }

    public Task SetLastOpenedBookIdAsync(Guid? bookId, CancellationToken cancellationToken = default)
    {
        if (bookId is null)
        {
            return RemoveValueAsync(LastOpenedBookIdKey, cancellationToken);
        }

        return SetValueAsync(LastOpenedBookIdKey, bookId.Value.ToString("D"), cancellationToken);
    }

    private async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);

        var row = await connection.FindAsync<AppSettingRecord>(key);
        return row?.Value;
    }

    private async Task SetValueAsync(string key, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);

        await connection.InsertOrReplaceAsync(new AppSettingRecord
        {
            Key = key,
            Value = value
        });
    }

    private async Task RemoveValueAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _database.GetConnectionAsync(cancellationToken);
        await connection.DeleteAsync<AppSettingRecord>(key);
    }
}
