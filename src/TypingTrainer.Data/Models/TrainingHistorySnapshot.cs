namespace TypingTrainer.Data.Models;

public sealed record TrainingHistorySnapshot(
    QualitySummary QualitySummary,
    IReadOnlyList<PersonalBestRow> PersonalBests,
    IReadOnlyList<PracticeCalendarDay> CalendarDays,
    IReadOnlyList<RecentQualityRow> RecentQuality);
