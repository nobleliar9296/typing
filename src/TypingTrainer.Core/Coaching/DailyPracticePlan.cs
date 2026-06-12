namespace TypingTrainer.Core.Coaching;

public sealed record DailyPracticePlan(
    TrainingFocus Focus,
    string Summary,
    int EstimatedMinutes,
    IReadOnlyList<PracticePlanStep> Steps);

