namespace PlayTale.Features.Audiobooks.Models;

public sealed record PlaybackProgress(
    Guid BookId,
    Guid ChapterId,
    int ChapterIndex,
    double PositionSeconds);
