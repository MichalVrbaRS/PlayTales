using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface IBookCoverService
{
    Task<string?> ResolveCoverPathAsync(
        string bookTitle,
        string? author,
        IReadOnlyList<Chapter> chapters,
        CancellationToken cancellationToken = default);
}
