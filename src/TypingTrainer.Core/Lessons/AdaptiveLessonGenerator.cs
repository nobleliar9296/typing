using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Lessons;

public sealed class AdaptiveLessonGenerator : ILessonGenerator
{
    private readonly IWordListProvider _wordListProvider;
    private readonly CharacterUnlockPlanner _unlockPlanner;
    private readonly FixedLessonGenerator _fixedLessonGenerator = new();

    public AdaptiveLessonGenerator(
        IWordListProvider wordListProvider,
        CharacterUnlockPlanner unlockPlanner)
    {
        _wordListProvider = wordListProvider;
        _unlockPlanner = unlockPlanner;
    }

    public LessonGenerationResult Generate(
        UserSkillProfile skillProfile,
        LessonGenerationOptions options)
    {
        if (options.Mode == LessonMode.Fixed)
        {
            return _fixedLessonGenerator.Generate(skillProfile, options);
        }

        var unlockedCharacters = _unlockPlanner.GetUnlockedCharacters(skillProfile, options.KeyboardLayout);
        var focusCharacters = SelectFocusCharacters(skillProfile, options, unlockedCharacters).ToArray();
        var focusBigrams = SelectFocusBigrams(skillProfile, options, unlockedCharacters).ToArray();
        var candidateWords = _wordListProvider
            .GetCommonWords()
            .Where(word => CanTypeWord(word, unlockedCharacters))
            .Distinct()
            .ToArray();
        var sampler = new WeightedSampler<string>(options.RandomSeed);
        var text = candidateWords.Length < 10
            ? GenerateFallbackDrill(options, unlockedCharacters, focusCharacters, focusBigrams, sampler)
            : GenerateFromWords(skillProfile, options, candidateWords, focusCharacters, focusBigrams, unlockedCharacters, sampler);

        return new LessonGenerationResult(
            text,
            unlockedCharacters,
            focusCharacters,
            focusBigrams,
            BuildReason(skillProfile, options.Mode, focusCharacters, focusBigrams));
    }

    private string GenerateFromWords(
        UserSkillProfile skillProfile,
        LessonGenerationOptions options,
        IReadOnlyList<string> candidateWords,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams,
        IReadOnlySet<char> unlockedCharacters,
        WeightedSampler<string> sampler)
    {
        var newestUnlockedCharacters = _unlockPlanner
            .GetNewestUnlockedStageCharacters(skillProfile, options.KeyboardLayout)
            .Where(unlockedCharacters.Contains)
            .ToHashSet();
        var weightedWords = candidateWords
            .Select(word => (Item: word, Weight: ScoreWord(word, focusCharacters, focusBigrams, newestUnlockedCharacters)))
            .ToArray();
        var words = new List<string>();

        while (!IsTargetReached(words, options))
        {
            words.Add(sampler.Sample(weightedWords));
        }

        return string.Join(' ', words).Trim();
    }

    private static string GenerateFallbackDrill(
        LessonGenerationOptions options,
        IReadOnlySet<char> unlockedCharacters,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams,
        WeightedSampler<string> sampler)
    {
        var activeCharacters = (focusCharacters.Count > 0
                ? focusCharacters
                : unlockedCharacters.OrderBy(character => character).ToArray())
            .Where(char.IsAsciiLetterLower)
            .DefaultIfEmpty('f')
            .ToArray();
        var chunks = new List<string>();

        while (!IsTargetReached(chunks, options))
        {
            if (focusBigrams.Count > 0 && sampler.Chance(0.55))
            {
                chunks.Add(focusBigrams[sampler.Next(0, focusBigrams.Count)]);
                continue;
            }

            var length = sampler.Next(2, 5);
            var characters = new char[length];

            for (var index = 0; index < length; index++)
            {
                characters[index] = activeCharacters[sampler.Next(0, activeCharacters.Length)];
            }

            chunks.Add(new string(characters));
        }

        return string.Join(' ', chunks).Trim();
    }

    private IEnumerable<char> SelectFocusCharacters(
        UserSkillProfile profile,
        LessonGenerationOptions options,
        IReadOnlySet<char> unlockedCharacters)
    {
        if (options.Mode == LessonMode.Review)
        {
            return Array.Empty<char>();
        }

        var limit = options.Mode == LessonMode.WeakKeys ? 5 : 4;
        var weakCharacters = profile.Characters.Values
            .Where(skill => unlockedCharacters.Contains(skill.Character))
            .Where(skill => skill.ExposureCount >= 5)
            .OrderByDescending(skill => skill.WeaknessScore)
            .ThenByDescending(skill => skill.ExposureCount)
            .Take(limit)
            .Select(skill => skill.Character)
            .ToArray();

        if (weakCharacters.Length > 0)
        {
            return weakCharacters;
        }

        return _unlockPlanner
            .GetNewestUnlockedStageCharacters(profile, options.KeyboardLayout)
            .Where(unlockedCharacters.Contains)
            .Take(limit);
    }

    private static IEnumerable<string> SelectFocusBigrams(
        UserSkillProfile profile,
        LessonGenerationOptions options,
        IReadOnlySet<char> unlockedCharacters)
    {
        if (options.Mode == LessonMode.Review || options.Mode == LessonMode.WeakKeys)
        {
            return Array.Empty<string>();
        }

        var limit = options.Mode == LessonMode.WeakBigrams ? 8 : 3;

        return profile.Bigrams.Values
            .Where(skill => skill.Bigram.Length == 2)
            .Where(skill => skill.ExposureCount >= 3)
            .Where(skill => skill.Bigram.All(unlockedCharacters.Contains))
            .OrderByDescending(skill => skill.WeaknessScore)
            .ThenByDescending(skill => skill.MedianLatencyMs ?? 0)
            .Take(limit)
            .Select(skill => skill.Bigram);
    }

    private static double ScoreWord(
        string word,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams,
        IReadOnlySet<char> newlyUnlockedCharacters)
    {
        var score = 1.0;

        foreach (var focusCharacter in focusCharacters)
        {
            if (word.Contains(focusCharacter))
            {
                score += 2.0;
            }
        }

        foreach (var focusBigram in focusBigrams)
        {
            if (word.Contains(focusBigram, StringComparison.Ordinal))
            {
                score += 3.0;
            }
        }

        if (word.Length is >= 3 and <= 7)
        {
            score += 0.5;
        }

        if (word.Any(newlyUnlockedCharacters.Contains))
        {
            score += 1.5;
        }

        if (word.GroupBy(character => character).Any(group => group.Count() > Math.Max(2, word.Length / 2)))
        {
            score -= 0.5;
        }

        return Math.Max(0.1, score);
    }

    private static bool CanTypeWord(string word, IReadOnlySet<char> allowedCharacters)
    {
        foreach (var character in word)
        {
            if (character == ' ')
            {
                continue;
            }

            if (!allowedCharacters.Contains(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTargetReached(IReadOnlyList<string> words, LessonGenerationOptions options)
    {
        if (words.Count == 0)
        {
            return false;
        }

        return options.LengthKind == LessonLengthKind.Words
            ? words.Count >= options.TargetLength
            : string.Join(' ', words).Length >= options.TargetLength;
    }

    private static string BuildReason(
        UserSkillProfile profile,
        LessonMode mode,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams)
    {
        if (profile.CompletedSessionCount == 0)
        {
            return "New user home-row practice";
        }

        return mode switch
        {
            LessonMode.WeakKeys => $"Weak key practice focused on {FormatFocusCharacters(focusCharacters)}",
            LessonMode.WeakBigrams => $"Weak bigram practice focused on {FormatFocusBigrams(focusBigrams)}",
            LessonMode.Review => "Balanced review",
            _ => focusCharacters.Count > 0
                ? $"Adaptive practice focused on {FormatFocusCharacters(focusCharacters)}"
                : "Balanced adaptive practice"
        };
    }

    private static string FormatFocusCharacters(IReadOnlyList<char> focusCharacters)
    {
        return focusCharacters.Count == 0
            ? "current unlocked keys"
            : string.Join(", ", focusCharacters);
    }

    private static string FormatFocusBigrams(IReadOnlyList<string> focusBigrams)
    {
        return focusBigrams.Count == 0
            ? "current unlocked transitions"
            : string.Join(", ", focusBigrams);
    }
}
