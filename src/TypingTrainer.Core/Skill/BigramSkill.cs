namespace TypingTrainer.Core.Skill;

public sealed record BigramSkill(
    string Bigram,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double? AverageLatencyMs,
    double WeaknessScore);
