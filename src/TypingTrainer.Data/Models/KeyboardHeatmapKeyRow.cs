namespace TypingTrainer.Data.Models;

public sealed record KeyboardHeatmapKeyRow(
    string KeyLabel,
    char Character,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double WeaknessScore);

