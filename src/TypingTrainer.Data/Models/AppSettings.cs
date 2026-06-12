namespace TypingTrainer.Data.Models;

public sealed record AppSettings(
    string DefaultLessonMode,
    int LessonLengthCharacters,
    bool AllowCapitalLetters,
    bool AllowNumbers,
    bool AllowPunctuation,
    bool UseImportedContent,
    bool UseBuiltInContent,
    bool BackspaceAllowed,
    bool AutoSaveCompletedSessions,
    bool RequireCorrectKeyToAdvance,
    bool ShowVisualKeyboard,
    bool ShowFingerColors,
    bool ShowFingerLabels,
    string VisualKeyboardLayout,
    int GoalTargetNetWpm,
    int GoalTargetAccuracyPercent,
    int GoalWeeklyPracticeMinutes)
{
    public const string AutoLessonMode = "Auto";
    public const string QwertyKeyboardLayout = "QWERTY";

    public static AppSettings Defaults { get; } = new(
        AutoLessonMode,
        LessonLengthCharacters: 220,
        AllowCapitalLetters: true,
        AllowNumbers: false,
        AllowPunctuation: true,
        UseImportedContent: true,
        UseBuiltInContent: true,
        BackspaceAllowed: true,
        AutoSaveCompletedSessions: true,
        RequireCorrectKeyToAdvance: false,
        ShowVisualKeyboard: true,
        ShowFingerColors: true,
        ShowFingerLabels: false,
        VisualKeyboardLayout: QwertyKeyboardLayout,
        GoalTargetNetWpm: 60,
        GoalTargetAccuracyPercent: 95,
        GoalWeeklyPracticeMinutes: 75);
}
