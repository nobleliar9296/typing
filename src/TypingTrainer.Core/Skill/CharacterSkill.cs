namespace TypingTrainer.Core.Skill;

public sealed record CharacterSkill(
    char Character,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double? AverageLatencyMs,
    double WeaknessScore,
    double ConfidenceScore);
