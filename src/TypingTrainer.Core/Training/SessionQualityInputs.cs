namespace TypingTrainer.Core.Training;

public sealed record SessionQualityInputs(
    double Accuracy,
    double NetWpm,
    double TargetNetWpm,
    double? Consistency,
    double CompletionRatio,
    double ControlRatio);
