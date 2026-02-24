using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public interface IImportService
{
    Task<IReadOnlyList<ImportSource>> PickFolderOrFilesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> ScanChaptersAsync(ImportSource source, CancellationToken cancellationToken = default);
}
