namespace PlayTale.Features.Audiobooks.Models;

public sealed record ImportSource(
    ImportSourceType Type,
    string DisplayName,
    string? Path,
    string? SourceUri,
    string? SecurityScopedBookmarkBase64);
