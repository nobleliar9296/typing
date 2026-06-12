using TypingTrainer.Core.Coaching;
using TypingTrainer.Core.Content;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.Services;

public sealed class LessonService : ILessonService
{
    private readonly ISkillProfileQueryService _skillProfileQueryService;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IContentQueryService _contentQueryService;
    private readonly ILessonGenerator _adaptiveLessonGenerator;

    public LessonService(
        ISkillProfileQueryService skillProfileQueryService,
        IAppSettingsRepository appSettingsRepository,
        IContentQueryService contentQueryService,
        ILessonGenerator adaptiveLessonGenerator,
        ILessonGenerator paragraphLessonGenerator)
    {
        _skillProfileQueryService = skillProfileQueryService;
        _appSettingsRepository = appSettingsRepository;
        _contentQueryService = contentQueryService;
        _adaptiveLessonGenerator = adaptiveLessonGenerator;
    }

    public async Task<LessonMode> GetDefaultLessonModeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(settings.DefaultLessonMode, AppSettings.AutoLessonMode, StringComparison.OrdinalIgnoreCase))
        {
            return ParseLessonMode(settings.DefaultLessonMode, LessonMode.Adaptive);
        }

        return settings.UseImportedContent
            && await _contentQueryService.HasEnabledImportedContentAsync(cancellationToken).ConfigureAwait(false)
                ? LessonMode.Paragraph
                : LessonMode.Adaptive;
    }

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return _appSettingsRepository.GetSettingsAsync(cancellationToken);
    }

    public async Task<LessonGenerationResult> GenerateNextLessonAsync(
        LessonMode mode,
        int? targetCharactersOverride = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _appSettingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var skillProfile = await _skillProfileQueryService
                .GetUserSkillProfileAsync(cancellationToken)
                .ConfigureAwait(false);
            var targetCharacters = targetCharactersOverride is int target
                ? Math.Max(20, target)
                : Math.Max(20, settings.LessonLengthCharacters);

            if (mode == LessonMode.Clipboard)
            {
                return _adaptiveLessonGenerator.Generate(
                    skillProfile,
                    CreateOptions(LessonMode.Adaptive, settings, targetCharacters));
            }

            if (mode == LessonMode.Paragraph)
            {
                var paragraphs = await _contentQueryService.GetParagraphsAsync(
                    new ParagraphPracticeQuery(
                        targetCharacters,
                        settings.AllowCapitalLetters,
                        settings.AllowNumbers,
                        settings.AllowPunctuation,
                        settings.UseImportedContent,
                        settings.UseBuiltInContent),
                    cancellationToken).ConfigureAwait(false);

                if (paragraphs.Count > 0)
                {
                    var text = TrimToTargetLength(string.Join("\n\n", paragraphs.Select(paragraph => paragraph.Text)), targetCharacters);
                    return new LessonGenerationResult(
                        text,
                        text.Where(character => !char.IsWhiteSpace(character)).ToHashSet(),
                        Array.Empty<char>(),
                        Array.Empty<string>(),
                        "Paragraph practice",
                        GetContentTitle(paragraphs),
                        GetContentSource(paragraphs));
                }
            }

            return _adaptiveLessonGenerator.Generate(
                skillProfile,
                CreateOptions(mode == LessonMode.Paragraph ? LessonMode.Adaptive : mode, settings, targetCharacters));
        }
        catch
        {
            return new FixedLessonGenerator().Generate(
                SkillProfileDefaults.Empty(),
                CreateOptions(LessonMode.Fixed, AppSettings.Defaults, FixedLessonGenerator.FixedLessonText.Length));
        }
    }

    public async Task<LessonGenerationResult> GenerateClipboardLessonAsync(
        string clipboardText,
        int? targetCharactersOverride = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var targetCharacters = targetCharactersOverride is int target
            ? Math.Max(20, target)
            : Math.Max(20, settings.LessonLengthCharacters);
        var normalized = NormalizePracticeText(
            clipboardText,
            settings.NormalizeImportedTextToAscii,
            settings.NormalizeImportedWhitespace,
            settings.LowercaseImportedText);
        var text = TrimToTargetLength(normalized, targetCharacters);

        if (string.IsNullOrWhiteSpace(text))
        {
            text = FixedLessonGenerator.FixedLessonText;
        }

        return new LessonGenerationResult(
            text,
            text.Where(character => !char.IsWhiteSpace(character)).ToHashSet(),
            Array.Empty<char>(),
            Array.Empty<string>(),
            "Clipboard practice",
            "Clipboard",
            "One-off local text");
    }

    public async Task<LessonGenerationResult> GenerateReviewLessonAsync(
        SessionReview review,
        int targetCharacters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _appSettingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var profile = new SessionReviewGenerator().CreatePracticeProfile(review);
            var mode = review.FocusBigrams.Count > 0 ? LessonMode.WeakBigrams : LessonMode.WeakKeys;
            var seed = CreateStableSeed(
                review.CorrectedErrors,
                review.UncorrectedErrors,
                string.Join(string.Empty, review.FocusCharacters),
                string.Join('|', review.FocusBigrams));
            var lesson = _adaptiveLessonGenerator.Generate(
                profile,
                new LessonGenerationOptions(
                    mode,
                    LessonLengthKind.Characters,
                    Math.Clamp(targetCharacters, 80, 800),
                    KeyboardLayoutRepository.Qwerty,
                    seed,
                    settings.AllowCapitalLetters,
                    settings.AllowNumbers,
                    settings.AllowPunctuation,
                    settings.GoalTrainingFocus,
                    settings.DifficultyPreset));

            return lesson with
            {
                Reason = review.FocusBigrams.Count > 0
                    ? $"Review practice focused on {string.Join(", ", review.FocusBigrams)}"
                    : $"Review practice focused on {string.Join(", ", review.FocusCharacters)}",
                ContentTitle = "Practice These Mistakes",
                ContentSource = "Last session review"
            };
        }
        catch
        {
            return new FixedLessonGenerator().Generate(
                SkillProfileDefaults.Empty(),
                CreateOptions(LessonMode.Fixed, AppSettings.Defaults, FixedLessonGenerator.FixedLessonText.Length));
        }
    }

    public Task<LessonGenerationResult> GenerateMistakeReplayLessonAsync(
        SessionReview review,
        IReadOnlyList<TypingInputEvent> events,
        int targetCharacters,
        CancellationToken cancellationToken = default)
    {
        var seed = CreateStableSeed(
            review.CorrectedErrors,
            review.UncorrectedErrors,
            string.Join(string.Empty, review.FocusCharacters),
            string.Join('|', review.FocusBigrams),
            events.Count);
        var lesson = new MistakeReplayGenerator().Generate(
            review,
            events,
            targetCharacters,
            seed);

        return Task.FromResult(lesson);
    }

    private static LessonGenerationOptions CreateOptions(LessonMode mode, AppSettings settings, int targetCharacters)
    {
        return new LessonGenerationOptions(
            mode,
            LessonLengthKind.Characters,
            mode == LessonMode.Fixed ? FixedLessonGenerator.FixedLessonText.Length : Math.Max(20, targetCharacters),
            KeyboardLayoutRepository.Qwerty,
            RandomSeed: null,
            settings.AllowCapitalLetters,
            settings.AllowNumbers,
            settings.AllowPunctuation,
            settings.GoalTrainingFocus,
            settings.DifficultyPreset);
    }

    private static string NormalizePracticeText(
        string text,
        bool normalizeToAscii,
        bool normalizeWhitespace,
        bool lowercase)
    {
        var normalized = normalizeToAscii ? AsciiTextNormalizer.ToAscii(text) : text;
        if (lowercase)
        {
            normalized = normalized.ToLowerInvariant();
        }

        return normalizeWhitespace
            ? string.Join(' ', normalized.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            : normalized.Trim();
    }

    private static string TrimToTargetLength(string text, int targetCharacters)
    {
        if (targetCharacters <= 0 || text.Length <= targetCharacters)
        {
            return text;
        }

        var targetIndex = Math.Min(targetCharacters, text.Length - 1);
        var lastBreak = text.LastIndexOf("\n\n", targetIndex, StringComparison.Ordinal);
        if (lastBreak >= Math.Max(20, targetCharacters / 2))
        {
            return text[..lastBreak].Trim();
        }

        var lastSpace = text.LastIndexOf(' ', targetIndex);
        return lastSpace >= Math.Max(20, targetCharacters / 2)
            ? text[..lastSpace].Trim()
            : text[..targetCharacters].Trim();
    }

    private static string GetContentTitle(IReadOnlyList<TypingTrainer.Core.Content.PracticeContentItem> paragraphs)
    {
        if (paragraphs.Count == 1)
        {
            return paragraphs[0].Title;
        }

        return $"{paragraphs[0].Title} + {paragraphs.Count - 1} more";
    }

    private static string GetContentSource(IReadOnlyList<TypingTrainer.Core.Content.PracticeContentItem> paragraphs)
    {
        var distinctSources = paragraphs
            .Select(paragraph => paragraph.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return distinctSources.Length == 1
            ? distinctSources[0]
            : "Mixed local content";
    }

    private static LessonMode ParseLessonMode(string value, LessonMode fallback)
    {
        return Enum.TryParse<LessonMode>(value, ignoreCase: true, out var mode)
            ? mode
            : fallback;
    }

    private static int CreateStableSeed(params object[] values)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in values)
            {
                var text = value?.ToString() ?? string.Empty;
                foreach (var character in text)
                {
                    hash = (hash * 31) + character;
                }
            }

            return hash;
        }
    }
}
