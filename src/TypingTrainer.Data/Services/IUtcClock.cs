namespace TypingTrainer.Data.Services;

public interface IUtcClock
{
    DateTimeOffset UtcNow { get; }
}
