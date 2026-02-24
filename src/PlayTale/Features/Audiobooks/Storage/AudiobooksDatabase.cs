using SQLite;

namespace PlayTale.Features.Audiobooks.Storage;

public sealed class AudiobooksDatabase
{
    private const int CurrentSchemaVersion = 1;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string _databasePath;
    private SQLiteAsyncConnection? _connection;

    public AudiobooksDatabase()
    {
        _databasePath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "audiobooks.db3");
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var connection = new SQLiteAsyncConnection(
                _databasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await EnsureSchemaAsync(connection, cancellationToken);
            _connection = connection;
            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task EnsureSchemaAsync(SQLiteAsyncConnection connection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;");
        if (version >= CurrentSchemaVersion)
        {
            return;
        }

        await connection.CreateTableAsync<BookRecord>();
        await connection.CreateTableAsync<ChapterRecord>();
        await connection.CreateTableAsync<PlaybackProgressRecord>();
        await connection.CreateTableAsync<AppSettingRecord>();

        await connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion};");
    }
}
