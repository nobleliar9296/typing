using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Coaching;

public sealed class TypingCoach
{
    public DailyPracticePlan BuildDailyPlan(
        CoachingStats stats,
        TrainingFocus focus,
        int targetSessionMinutes,
        int targetEssayWords)
    {
        var safeMinutes = Math.Clamp(targetSessionMinutes, 5, 60);
        var steps = new List<PracticePlanStep>();
        var accuracyTarget = stats.GoalTargetAccuracyPercent / 100.0;

        if (stats.SessionCount < 5)
        {
            steps.Add(Step(1, "Build baseline", "Complete a short paragraph session so the coach has enough local data.", LessonMode.Paragraph, 220, Math.Min(8, safeMinutes)));
            steps.Add(Step(2, "Balanced review", "Use Adaptive mode to expose current weak spots.", LessonMode.Adaptive, 220, Math.Min(7, safeMinutes)));
            return Plan(focus, "Build a baseline with a few short saved sessions.", steps);
        }

        if (focus == TrainingFocus.Accuracy || stats.Accuracy < accuracyTarget)
        {
            steps.Add(Step(1, "Accuracy control", FormatTarget("Run Weak Keys", stats.WeakestKey), LessonMode.WeakKeys, 220, safeMinutes / 2));
            steps.Add(Step(2, "Confirm in context", "Finish with one short paragraph while keeping accuracy clean.", LessonMode.Paragraph, 220, safeMinutes - steps[0].Minutes));
            return Plan(focus, "Prioritize accuracy before pushing speed.", steps);
        }

        if (focus == TrainingFocus.EssayEndurance)
        {
            var targetCharacters = Math.Clamp(targetEssayWords, 100, 3000) * 5;
            steps.Add(Step(1, "Essay endurance", "Type one long paragraph set at controlled pace.", LessonMode.Paragraph, targetCharacters, safeMinutes));
            return Plan(focus, "Build long-form endurance without chasing bursts.", steps);
        }

        if (focus == TrainingFocus.ExamPractice)
        {
            steps.Add(Step(1, "Clean start", "Warm up with Weak Keys for accuracy.", LessonMode.WeakKeys, 220, Math.Max(5, safeMinutes / 3)));
            steps.Add(Step(2, "Timed flow", "Run Paragraph mode and hold steady net WPM.", LessonMode.Paragraph, 1250, safeMinutes - steps[0].Minutes));
            return Plan(focus, "Simulate a focused timed typing block.", steps);
        }

        if (focus == TrainingFocus.Speed || stats.AverageNetWpm < stats.GoalTargetNetWpm)
        {
            steps.Add(Step(1, "Flow practice", "Use Paragraph mode to keep words moving.", LessonMode.Paragraph, 1250, safeMinutes / 2));
            steps.Add(Step(2, "Smooth transitions", FormatTarget("Use Weak Bigrams", stats.SlowestBigram), LessonMode.WeakBigrams, 220, safeMinutes - steps[0].Minutes));
            return Plan(focus, "Push speed through smoother transitions.", steps);
        }

        steps.Add(Step(1, "Maintain control", "Run Review mode and keep pace comfortable.", LessonMode.Review, 220, safeMinutes / 2));
        steps.Add(Step(2, "Paragraph finish", "End with one paragraph session at target pace.", LessonMode.Paragraph, 220, safeMinutes - steps[0].Minutes));
        return Plan(focus, "Maintain accuracy and speed with a balanced block.", steps);
    }

    private static DailyPracticePlan Plan(TrainingFocus focus, string summary, IReadOnlyList<PracticePlanStep> steps)
    {
        return new DailyPracticePlan(focus, summary, steps.Sum(step => step.Minutes), steps);
    }

    private static PracticePlanStep Step(
        int order,
        string title,
        string description,
        LessonMode mode,
        int targetCharacters,
        int minutes)
    {
        return new PracticePlanStep(
            order,
            title,
            description,
            mode,
            LessonLengthKind.Characters,
            Math.Max(20, targetCharacters),
            Math.Max(1, minutes));
    }

    private static string FormatTarget(string prefix, string? target)
    {
        return string.IsNullOrWhiteSpace(target) ? $"{prefix} for the weakest current targets." : $"{prefix}, starting with {target}.";
    }
}

