using SQLite;

namespace PlayTale.Features.Audiobooks.Storage;

[Table("AppSettings")]
public sealed class AppSettingRecord
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [NotNull]
    public string Value { get; set; } = string.Empty;
}
