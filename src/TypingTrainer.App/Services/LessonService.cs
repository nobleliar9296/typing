using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Skill;
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _appSettingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var skillProfile = await _skillProfileQueryService
                .GetUserSkillProfileAsync(cancellationToken)
                .ConfigureAwait(false);

            if (mode == LessonMode.Paragraph)
            {
                var paragraph = await _contentQueryService.GetNextParagraphAsync(
                    new ParagraphPracticeQuery(
                        settings.LessonLengthCharacters,
                        settings.AllowCapitalLetters,
                        settings.AllowNumbers,
                        settings.AllowPunctuation,
                        settings.UseImportedContent,
                        settings.UseBuiltInContent),
                    cancellationToken).ConfigureAwait(false);

                if (paragraph is not null)
                {
                    return new LessonGenerationResult(
                        paragraph.Text,
                        paragraph.CharacterSet,
                        Array.Empty<char>(),
                        Array.Empty<string>(),
                        "Paragraph practice",
                        paragraph.Title,
                        paragraph.Source);
                }
            }

            return _adaptiveLessonGenerator.Generate(
                skillProfile,
                CreateOptions(mode == LessonMode.Paragraph ? LessonMode.Adaptive : mode, settings));
        }
        catch
        {
            return new FixedLessonGenerator().Generate(
                SkillProfileDefaults.Empty(),
                CreateOptions(LessonMode.Fixed, AppSettings.Defaults));
        }
    }

    private static LessonGenerationOptions CreateOptions(LessonMode mode, AppSettings settings)
    {
        return new LessonGenerationOptions(
            mode,
            LessonLengthKind.Characters,
            mode == LessonMode.Fixed ? FixedLessonGenerator.FixedLessonText.Length : Math.Max(20, settings.LessonLengthCharacters),
            KeyboardLayoutRepository.Qwerty,
            RandomSeed: null,
            settings.AllowCapitalLetters,
            settings.AllowNumbers,
            settings.AllowPunctuation);
    }

    private static LessonMode ParseLessonMode(string value, LessonMode fallback)
    {
        return Enum.TryParse<LessonMode>(value, ignoreCase: true, out var mode)
            ? mode
            : fallback;
    }
}
