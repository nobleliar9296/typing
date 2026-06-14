using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Learning;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Lessons;

public sealed class AdaptiveLessonGenerator : ILessonGenerator
{
    private readonly IWordListProvider _wordListProvider;
    private readonly CharacterUnlockPlanner _unlockPlanner;
    private readonly FixedLessonGenerator _fixedLessonGenerator = new();
    private static readonly QwertyCharacterToKeyMapper KeyMapper = new();
    private static readonly IReadOnlyDictionary<string, VisualKeyboardKey> LayoutKeys =
        QwertyVisualKeyboardLayout
            .Create()
            .Rows
            .SelectMany(row => row.Keys)
            .ToDictionary(key => key.Id, StringComparer.Ordinal);

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
        var learningTargets = GetUsedDueLearningTargets(skillProfile, focusCharacters, focusBigrams);

        return new LessonGenerationResult(
            text,
            unlockedCharacters,
            focusCharacters,
            focusBigrams,
            BuildReason(skillProfile, options.Mode, focusCharacters, focusBigrams),
            LearningTargets: learningTargets);
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
            .Select(word => (Item: word, Weight: ScoreWord(word, focusCharacters, focusBigrams, newestUnlockedCharacters, options)))
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
        var dueCharacters = profile.DueLearningTargets
            .Where(target => target.Type == LearningItemType.Character)
            .Select(target => target.Target)
            .Where(target => target.Length == 1)
            .Select(target => target[0])
            .Where(unlockedCharacters.Contains)
            .Take(limit)
            .ToArray();
        var candidates = profile.Characters.Values
            .Where(skill => unlockedCharacters.Contains(skill.Character))
            .Where(skill => !dueCharacters.Contains(skill.Character))
            .Where(skill => skill.ExposureCount >= 5);
        var focus = NormalizeFocus(options.TrainingFocus);

        candidates = focus switch
        {
            "Punctuation" => candidates.Where(skill => char.IsPunctuation(skill.Character)),
            "WeakLeftHand" => candidates.Where(skill => IsLeftHandCharacter(skill.Character)),
            "WeakRightHand" => candidates.Where(skill => IsRightHandCharacter(skill.Character)),
            _ => candidates
        };

        var orderedCharacters = focus switch
        {
            "SpeedFirst" => candidates
                .OrderByDescending(skill => skill.MedianLatencyMs ?? 0)
                .ThenByDescending(skill => skill.WeaknessScore),
            "AccuracyFirst" => candidates
                .OrderByDescending(skill => skill.IncorrectCount)
                .ThenBy(skill => skill.Accuracy),
            _ => candidates
                .OrderByDescending(skill => skill.WeaknessScore)
                .ThenByDescending(skill => skill.ExposureCount)
        };
        var weakCharacters = orderedCharacters
            .Take(limit)
            .Select(skill => skill.Character)
            .ToArray();

        if (weakCharacters.Length > 0)
        {
            return dueCharacters
                .Concat(weakCharacters)
                .Distinct()
                .Take(limit);
        }

        var newest = _unlockPlanner
            .GetNewestUnlockedStageCharacters(profile, options.KeyboardLayout)
            .Where(unlockedCharacters.Contains);
        newest = focus switch
        {
            "Punctuation" => newest.Where(char.IsPunctuation),
            "WeakLeftHand" => newest.Where(IsLeftHandCharacter),
            "WeakRightHand" => newest.Where(IsRightHandCharacter),
            _ => newest
        };

        return dueCharacters
            .Concat(newest)
            .Distinct()
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
        var focus = NormalizeFocus(options.TrainingFocus);
        var dueBigrams = profile.DueLearningTargets
            .Where(target => target.Type == LearningItemType.Bigram)
            .Select(target => target.Target)
            .Where(target => target.Length == 2)
            .Where(target => target.All(unlockedCharacters.Contains))
            .Take(limit)
            .ToArray();
        var candidates = profile.Bigrams.Values
            .Where(skill => skill.Bigram.Length == 2)
            .Where(skill => skill.ExposureCount >= 3)
            .Where(skill => skill.Bigram.All(unlockedCharacters.Contains))
            .Where(skill => !dueBigrams.Contains(skill.Bigram, StringComparer.Ordinal));

        candidates = focus switch
        {
            "Punctuation" => candidates.Where(skill => skill.Bigram.Any(char.IsPunctuation)),
            "WeakLeftHand" => candidates.Where(skill => skill.Bigram.Any(IsLeftHandCharacter)),
            "WeakRightHand" => candidates.Where(skill => skill.Bigram.Any(IsRightHandCharacter)),
            _ => candidates
        };

        return dueBigrams
            .Concat(candidates
            .OrderByDescending(skill => skill.WeaknessScore)
            .ThenByDescending(skill => skill.MedianLatencyMs ?? 0)
            .Take(limit)
            .Select(skill => skill.Bigram))
            .Distinct(StringComparer.Ordinal)
            .Take(limit);
    }

    private static double ScoreWord(
        string word,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams,
        IReadOnlySet<char> newlyUnlockedCharacters,
        LessonGenerationOptions options)
    {
        var score = 1.0;
        var focus = NormalizeFocus(options.TrainingFocus);

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

        score += focus switch
        {
            "Punctuation" => word.Any(char.IsPunctuation) ? 3.0 : -0.35,
            "WeakLeftHand" => word.Count(IsLeftHandCharacter) * 0.4,
            "WeakRightHand" => word.Count(IsRightHandCharacter) * 0.4,
            "SpeedFirst" => word.Length is >= 4 and <= 8 ? 1.0 : 0.0,
            "AccuracyFirst" => word.Any(focusCharacters.Contains) ? 1.0 : 0.0,
            _ => 0.0
        };

        score += options.DifficultyPreset switch
        {
            "Speed Words" => word.Any(character => char.IsPunctuation(character) || char.IsDigit(character) || char.IsUpper(character)) ? -0.75 : 0.6,
            "Symbols" => word.Any(character => char.IsPunctuation(character) || char.IsDigit(character)) ? 1.6 : -0.25,
            "Clean Copy" => word.Any(char.IsUpper) || word.Any(char.IsPunctuation) ? 0.4 : 0.0,
            _ => 0.0
        };

        if (word.GroupBy(character => character).Any(group => group.Count() > Math.Max(2, word.Length / 2)))
        {
            score -= 0.5;
        }

        return Math.Max(0.1, score);
    }

    private static string NormalizeFocus(string? value)
    {
        return value?.Replace(" ", string.Empty, StringComparison.Ordinal) switch
        {
            "Accuracy" or "AccuracyFirst" => "AccuracyFirst",
            "Speed" or "SpeedFirst" => "SpeedFirst",
            "WeakLeftHand" => "WeakLeftHand",
            "WeakRightHand" => "WeakRightHand",
            "Punctuation" => "Punctuation",
            _ => "Balanced"
        };
    }

    private static bool IsLeftHandCharacter(char character)
    {
        return IsCharacterAssignedTo(character, finger => finger is
            FingerAssignment.LeftPinky
            or FingerAssignment.LeftRing
            or FingerAssignment.LeftMiddle
            or FingerAssignment.LeftIndex
            or FingerAssignment.LeftThumb);
    }

    private static bool IsRightHandCharacter(char character)
    {
        return IsCharacterAssignedTo(character, finger => finger is
            FingerAssignment.RightPinky
            or FingerAssignment.RightRing
            or FingerAssignment.RightMiddle
            or FingerAssignment.RightIndex
            or FingerAssignment.RightThumb);
    }

    private static bool IsCharacterAssignedTo(char character, Func<FingerAssignment, bool> predicate)
    {
        var mapping = KeyMapper.Map(character);
        return mapping is not null
            && LayoutKeys.TryGetValue(mapping.KeyId, out var key)
            && predicate(key.Finger);
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

        var dueTargets = profile.DueLearningTargets
            .Where(target =>
                target.Type == LearningItemType.Character
                    ? target.Target.Length == 1 && focusCharacters.Contains(target.Target[0])
                    : focusBigrams.Contains(target.Target, StringComparer.Ordinal))
            .Select(target => FormatLearningTarget(target))
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        if (dueTargets.Length > 0)
        {
            return $"Spaced review: {string.Join(", ", dueTargets)}";
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

    private static string FormatLearningTarget(LearningTarget target)
    {
        return target.Type == LearningItemType.Character && target.Target == " "
            ? "Space"
            : target.Target;
    }

    private static IReadOnlyList<LearningTarget> GetUsedDueLearningTargets(
        UserSkillProfile profile,
        IReadOnlyList<char> focusCharacters,
        IReadOnlyList<string> focusBigrams)
    {
        return profile.DueLearningTargets
            .Where(target =>
                target.Type == LearningItemType.Character
                    ? target.Target.Length == 1 && focusCharacters.Contains(target.Target[0])
                    : focusBigrams.Contains(target.Target, StringComparer.Ordinal))
            .Take(8)
            .ToArray();
    }
}
