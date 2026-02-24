namespace PlayTale.Features.Audiobooks.State;

public sealed class AudiobooksFeatureState
{
    public bool IsInitialized { get; set; }

    public Guid? SelectedBookId { get; set; }

    public Guid? SelectedChapterId { get; set; }
}
