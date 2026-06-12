using TypingTrainer.Core.Content;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Lessons;

public sealed class ParagraphLessonGenerator : ILessonGenerator
{
    private readonly IPracticeContentProvider _contentProvider;
    private readonly CharacterUnlockPlanner _unlockPlanner;
    private readonly AdaptiveLessonGenerator _fallbackGenerator;

    public ParagraphLessonGenerator(
        IPracticeContentProvider contentProvider,
        IWordListProvider wordListProvider,
        CharacterUnlockPlanner unlockPlanner)
    {
        _contentProvider = contentProvider;
        _unlockPlanner = unlockPlanner;
        _fallbackGenerator = new AdaptiveLessonGenerator(wordListProvider, unlockPlanner);
    }

    public LessonGenerationResult Generate(
        UserSkillProfile skillProfile,
        LessonGenerationOptions options)
    {
        if (options.Mode == LessonMode.Fixed)
        {
            return new FixedLessonGenerator().Generate(skillProfile, options);
        }

        var unlockedCharacters = _unlockPlanner.GetUnlockedCharacters(skillProfile, options.KeyboardLayout);
        var focusCharacters = SelectFocusCharacters(skillProfile, options, unlockedCharacters).ToArray();
        var focusBigrams = SelectFocusBigrams(skillProfile, unlockedCharacters).ToArray();
        var candidates = _contentProvider
            .GetContentItems()
            .Where(item => item.Kind == PracticeContentKind.Paragraph)
            .Where(item => IsAllowed(item, options, unlockedCharacters))
            .Select(item => (Item: item, Weight: ScoreParagraph(item, options, focusCharacters, focusBigrams)))
            .Where(candidate => candidate.Weight > 0)
            .ToArray();

        if (candidates.Length == 0)
        {
            return _fallbackGenerator.Generate(skillProfile, options with { Mode = LessonMode.Adaptive });
        }

        var sampler = new WeightedSampler<PracticeContentItem>(options.RandomSeed);
        var selected = sampler.Sample(candidates);
        var text = TrimToTargetLength(selected.Text, options);

        return new LessonGenerationResult(
            text,
            unlockedCharacters,
            focusCharacters,
            focusBigrams,
            $"Paragraph practice from {selected.Title}");
    }

    private IEnumerable<char> SelectFocusCharacters(
        UserSkillProfile profile,
        LessonGenerationOptions options,
        IReadOnlySet<char> unlockedCharacters)
    {
        var weakCharacters = profile.Characters.Values
            .Where(skill => unlockedCharacters.Contains(skill.Character))
            .Where(skill => skill.ExposureCount >= 5)
            .OrderByDescending(skill => skill.WeaknessScore)
            .ThenByDescending(skill => skill.ExposureCount)
            .Take(4)
            .Select(skill => skill.Character)
            .ToArray();

        if (weakCharacters.Length > 0)
        {
            return weakCharacters;
        }

        return _unlockPlanner
            .GetNewestUnlockedStageCharacters(profile, options.KeyboardLayout)
            .Where(unlockedCharacters.Contains)
            .Take(4);
    }

    private static IEnumerable<string> SelectFocusBigrams(
        UserSkillProfile profile,
        IReadOnlySet<char> unlockedCharacters)
    {
        return profile.Bigrams.Values
            .Where(skill => skill.Bigram.Length == 2)
            .Where(skill => skill.ExposureCount >= 3)
            .Where(skill => skill.Bigram.All(unlockedCharacters.Contains))
            .OrderByDescending(skill => skill.WeaknessScore)
            .ThenByDescending(skill => skill.MedianLatencyMs ?? 0)
            .Take(3)
            .Select(skill => skill.Bigram);
    }

    private static bool IsAllowed(
        PracticeContentItem item,
        LessonGenerationOptions options,
        IReadOnlySet<char> unlockedCharacters)
    {
        if (!options.AllowCapitalLetters && item.ContainsCapitalLetters)
        {
            return false;
        }

        if (!options.AllowNumbers && item.ContainsNumbers)
        {
            return false;
        }

        if (!options.AllowPunctuation && item.ContainsPunctuation)
        {
            return false;
        }

        foreach (var character in item.CharacterSet)
        {
            if (char.IsUpper(character))
            {
                if (!options.AllowCapitalLetters || !unlockedCharacters.Contains(char.ToLowerInvariant(character)))
                {
                    return false;
                }

                continue;
            }

            if (char.IsDigit(character))
            {
                if (!options.AllowNumbers)
                {
                    return false;
                }

                continue;
            }

            if (char.IsPunctuation(character))
            {
                if (!options.AllowPunctuation)
                {
                    return false;
                }

                continue;
            }

            if (!unlockedCharacters.Contains(character))
            {
                return false;
            }
        }

        return true;
    }

    private static double ScoreParagraph(
        PracticeContentItem item,
        LessonGenerationOptions options,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams)
    {
        var text = item.Text.ToLowerInvariant();
        var nonWhitespaceCount = Math.Max(1, text.Count(character => !char.IsWhiteSpace(character)));
        var focusCharacterHits = focusCharacters.Sum(focusCharacter => text.Count(character => character == focusCharacter));
        var focusCharacterDensity = focusCharacters.Count == 0 ? 0 : focusCharacterHits / (double)nonWhitespaceCount;
        var focusBigramHits = focusBigrams.Sum(focusBigram => CountOccurrences(text, focusBigram));
        var focusBigramDensity = focusBigrams.Count == 0 ? 0 : focusBigramHits / (double)nonWhitespaceCount;
        var lengthFitScore = CalculateLengthFitScore(item.CharacterCount, options.TargetLength);

        return Math.Max(
            0.1,
            1.0
            + (focusCharacterDensity * 2.0)
            + (focusBigramDensity * 3.0)
            + lengthFitScore
            - item.DifficultyScore);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index++;
        }

        return count;
    }

    private static double CalculateLengthFitScore(int characterCount, int targetLength)
    {
        if (targetLength <= 0)
        {
            return 0;
        }

        var distance = Math.Abs(characterCount - targetLength) / (double)targetLength;
        return 1.0 - Math.Min(1.0, distance);
    }

    private static string TrimToTargetLength(string text, LessonGenerationOptions options)
    {
        if (options.LengthKind != LessonLengthKind.Characters || options.TargetLength <= 0 || text.Length <= options.TargetLength)
        {
            return text;
        }

        var targetIndex = Math.Min(options.TargetLength, text.Length - 1);
        var lastSpace = text.LastIndexOf(' ', targetIndex);
        if (lastSpace >= Math.Max(1, options.TargetLength / 2))
        {
            return text[..lastSpace].Trim();
        }

        return text[..options.TargetLength].Trim();
    }
}
