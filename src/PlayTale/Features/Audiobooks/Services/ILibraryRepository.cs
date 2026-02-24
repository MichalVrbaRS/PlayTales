using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface ILibraryRepository
{
    Task<IReadOnlyList<Book>> GetBooksAsync(CancellationToken cancellationToken = default);

    Task<Book?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task SaveBookAsync(Book book, CancellationToken cancellationToken = default);

    Task SaveChaptersAsync(Guid bookId, IReadOnlyList<Chapter> chapters, CancellationToken cancellationToken = default);

    Task SaveProgressAsync(PlaybackProgress progress, CancellationToken cancellationToken = default);

    Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default);
}
