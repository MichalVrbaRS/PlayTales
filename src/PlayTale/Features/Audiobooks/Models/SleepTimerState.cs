namespace PlayTale.Features.Audiobooks.Models;

public sealed class SleepTimerState
{
    public SleepTimerMode Mode { get; init; } = SleepTimerMode.Off;

    public bool IsActive { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public int? RemainingChapters { get; init; }
}
