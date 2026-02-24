using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface ILibraryService
{
    Task<IReadOnlyList<Book>> GetLibraryAsync(CancellationToken cancellationToken = default);

    Task<Book?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<Book> ImportAsync(CancellationToken cancellationToken = default);

    Task SaveProgressAsync(PlaybackProgress progress, CancellationToken cancellationToken = default);

    Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default);
}
