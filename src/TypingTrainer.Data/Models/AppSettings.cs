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
    bool ZenModeEnabled,
    int CountdownSeconds,
    bool KeySoundEnabled,
    bool MistakeSoundEnabled,
    bool ShowVisualKeyboard,
    bool ShowFingerColors,
    bool ShowFingerLabels,
    string VisualKeyboardLayout,
    int PracticeTextScalePercent,
    int VisualKeyboardScalePercent,
    int GoalTargetNetWpm,
    int GoalTargetAccuracyPercent,
    int GoalWeeklyPracticeMinutes,
    bool NormalizeImportedTextToAscii,
    bool LowercaseImportedText,
    bool NormalizeImportedWhitespace,
    string GoalTrainingFocus,
    int GoalTargetSessionMinutes,
    int GoalTargetEssayWords,
    string PracticeFontFamily,
    string PracticeLineWidth,
    string PracticeTextContrast,
    string PracticeCursorStyle,
    string ThemePreset,
    string DifficultyPreset)
{
    public const string AutoLessonMode = "Auto";
    public const string QwertyKeyboardLayout = "QWERTY";
    public const string DefaultTrainingFocus = "Balanced";
    public const string DefaultFontFamily = "Cascadia Mono";
    public const string DefaultLineWidth = "Comfortable";
    public const string DefaultTextContrast = "Normal";
    public const string DefaultCursorStyle = "Underline";
    public const string DefaultThemePreset = "System";
    public const string DefaultDifficultyPreset = "Custom";

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
        ZenModeEnabled: false,
        CountdownSeconds: 0,
        KeySoundEnabled: false,
        MistakeSoundEnabled: false,
        ShowVisualKeyboard: true,
        ShowFingerColors: true,
        ShowFingerLabels: false,
        VisualKeyboardLayout: QwertyKeyboardLayout,
        PracticeTextScalePercent: 100,
        VisualKeyboardScalePercent: 100,
        GoalTargetNetWpm: 60,
        GoalTargetAccuracyPercent: 95,
        GoalWeeklyPracticeMinutes: 75,
        NormalizeImportedTextToAscii: true,
        LowercaseImportedText: false,
        NormalizeImportedWhitespace: true,
        GoalTrainingFocus: DefaultTrainingFocus,
        GoalTargetSessionMinutes: 15,
        GoalTargetEssayWords: 1000,
        PracticeFontFamily: DefaultFontFamily,
        PracticeLineWidth: DefaultLineWidth,
        PracticeTextContrast: DefaultTextContrast,
        PracticeCursorStyle: DefaultCursorStyle,
        ThemePreset: DefaultThemePreset,
        DifficultyPreset: DefaultDifficultyPreset);
}
