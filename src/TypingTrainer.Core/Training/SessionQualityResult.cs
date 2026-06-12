namespace TypingTrainer.Core.Training;

public sealed record SessionQualityResult(
    double Score,
    string Grade,
    double AccuracyComponent,
    double SpeedComponent,
    double ConsistencyComponent,
    double ControlComponent);
