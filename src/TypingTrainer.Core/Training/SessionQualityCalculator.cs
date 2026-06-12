namespace TypingTrainer.Core.Training;

public sealed class SessionQualityCalculator
{
    public const double DefaultTargetNetWpm = 60;

    public SessionQualityResult Calculate(SessionQualityInputs inputs)
    {
        var targetNetWpm = inputs.TargetNetWpm > 0
            ? inputs.TargetNetWpm
            : DefaultTargetNetWpm;
        var accuracy = Clamp01(inputs.Accuracy);
        var speed = Clamp01(inputs.NetWpm / targetNetWpm);
        var consistency = Clamp01(inputs.Consistency ?? 0.8);
        var control = Clamp01((Clamp01(inputs.CompletionRatio) * 0.7) + (Clamp01(inputs.ControlRatio) * 0.3));
        var score = Clamp(
            (accuracy * 40) + (speed * 30) + (consistency * 20) + (control * 10),
            0,
            100);

        return new SessionQualityResult(
            score,
            GetGrade(score),
            accuracy * 40,
            speed * 30,
            consistency * 20,
            control * 10);
    }

    public static string GetGrade(double score)
    {
        return score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "Needs work"
        };
    }

    private static double Clamp01(double value)
    {
        return Clamp(value, 0, 1);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return minimum;
        }

        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
