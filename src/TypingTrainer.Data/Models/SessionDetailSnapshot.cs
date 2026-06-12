namespace TypingTrainer.Data.Models;

public sealed record SessionDetailSnapshot(
    StoredPracticeSession Session,
    IReadOnlyList<StoredKeyEvent> Events,
    IReadOnlyList<SessionTimelinePoint> Timeline,
    IReadOnlyList<SessionDetailMistakeRow> Mistakes,
    IReadOnlyList<CharacterAnalyticsRow> SlowestKeys,
    IReadOnlyList<BigramAnalyticsRow> SlowestBigrams);

