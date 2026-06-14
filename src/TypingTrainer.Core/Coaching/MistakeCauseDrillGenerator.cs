using System.Globalization;
using System.Text;
using TypingTrainer.Core.Learning;
using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Coaching;

public sealed class MistakeCauseDrillGenerator
{
    private static readonly string[] ControlledFlowTokens =
    [
        "slow",
        "steady",
        "even",
        "pace",
        "calm",
        "hands",
        "clean",
        "flow"
    ];

    public LessonGenerationResult Generate(MistakeCauseDrillRequest request, int targetCharacters)
    {
        var safeTarget = Math.Clamp(targetCharacters, 40, 800);
        var seed = request.RandomSeed ?? CreateStableSeed(
            request.Cause,
            string.Join(string.Empty, request.TargetCharacters),
            string.Join('|', request.TargetBigrams),
            request.AllowCapitalLetters,
            request.AllowNumbers,
            request.AllowPunctuation);
        var tokens = BuildTokens(request)
            .Select(token => SanitizeText(token, request))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tokens.Length == 0)
        {
            tokens = ControlledFlowTokens;
        }

        var text = BuildText(tokens, safeTarget, seed);
        text = SanitizeText(text, request);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = BuildText(ControlledFlowTokens, safeTarget, seed);
        }

        var focusCharacters = request.TargetCharacters
            .Select(character => SanitizeText(character.ToString(), request))
            .Where(token => token.Length == 1)
            .Select(token => token[0])
            .Where(character => !char.IsWhiteSpace(character))
            .Distinct()
            .Take(6)
            .ToArray();
        var focusBigrams = request.TargetBigrams
            .Select(bigram => SanitizeText(bigram, request))
            .Where(bigram => bigram.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();

        return new LessonGenerationResult(
            text,
            text.Where(character => !char.IsWhiteSpace(character)).ToHashSet(),
            focusCharacters,
            focusBigrams,
            $"Micro-drill: {FormatCause(request.Cause)}",
            "Do This Next Drill",
            "Last session review");
    }

    private static IEnumerable<string> BuildTokens(MistakeCauseDrillRequest request)
    {
        var targets = request.TargetCharacters
            .Select(character => character.ToString())
            .Concat(request.TargetBigrams)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();

        return request.Cause switch
        {
            MistakeCause.AdjacentKey => BuildTransitionTokens(targets, "asdfghjkl"),
            MistakeCause.SameFinger => BuildTransitionTokens(targets, "rfvtgbyhnujm"),
            MistakeCause.WrongHand => BuildTransitionTokens(targets, "fjdkals"),
            MistakeCause.ShiftIssue => BuildShiftTokens(request),
            MistakeCause.Punctuation => BuildPunctuationTokens(request),
            MistakeCause.NumberRow => BuildNumberTokens(request),
            MistakeCause.Rushed => BuildControlledFlowTokens("slow", "steady", "breathe", "reset"),
            MistakeCause.Fatigue => BuildControlledFlowTokens("short", "clean", "rest", "steady"),
            _ => BuildControlledFlowTokens("focus", "clean", "even", "pace")
        };
    }

    private static IEnumerable<string> BuildTransitionTokens(IReadOnlyList<string> targets, string anchors)
    {
        if (targets.Count == 0)
        {
            targets = ["as", "df", "jk", "la"];
        }

        var anchorIndex = 0;
        foreach (var target in targets)
        {
            var anchor = anchors[anchorIndex++ % anchors.Length].ToString();
            yield return target;
            yield return $"{target}{anchor}";
            yield return $"{anchor}{target}";
            yield return target.Length == 1 ? string.Concat(target, target, anchor) : target;
        }
    }

    private static IEnumerable<string> BuildShiftTokens(MistakeCauseDrillRequest request)
    {
        if (request.AllowCapitalLetters)
        {
            yield return "I";
            yield return "The";
            yield return "You";
        }

        if (request.AllowPunctuation)
        {
            yield return "?";
            yield return "/";
            yield return ":";
            yield return ";";
        }

        yield return "shift";
        yield return "steady";
        yield return "clean";
    }

    private static IEnumerable<string> BuildPunctuationTokens(MistakeCauseDrillRequest request)
    {
        if (request.AllowPunctuation)
        {
            yield return ",";
            yield return ".";
            yield return "?";
            yield return "yes,";
            yield return "now.";
            yield return "ready?";
        }

        yield return "pause";
        yield return "space";
        yield return "then";
        yield return "flow";
    }

    private static IEnumerable<string> BuildNumberTokens(MistakeCauseDrillRequest request)
    {
        if (request.AllowNumbers)
        {
            yield return "1";
            yield return "2";
            yield return "3";
            yield return "2026";
            yield return "room2";
            yield return "step4";
        }

        yield return "top";
        yield return "row";
        yield return "reach";
        yield return "return";
    }

    private static IEnumerable<string> BuildControlledFlowTokens(params string[] tokens)
    {
        foreach (var token in tokens.Concat(ControlledFlowTokens).Distinct(StringComparer.Ordinal))
        {
            yield return token;
        }
    }

    private static string BuildText(IReadOnlyList<string> tokens, int targetCharacters, int randomSeed)
    {
        var sampler = new WeightedSampler<string>(randomSeed);
        var weighted = tokens
            .Select((token, index) => (Item: token, Weight: (double)Math.Max(1, tokens.Count - index)))
            .ToArray();
        var builder = new StringBuilder();

        while (builder.Length < targetCharacters)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(sampler.Sample(weighted));
        }

        return builder.ToString();
    }

    private static string SanitizeText(string text, MistakeCauseDrillRequest request)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsDigit(character) && !request.AllowNumbers)
            {
                builder.Append(' ');
            }
            else if (char.IsPunctuation(character) && !request.AllowPunctuation)
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        var sanitized = builder.ToString();
        if (!request.AllowCapitalLetters)
        {
            sanitized = sanitized.ToLower(CultureInfo.InvariantCulture);
        }

        return string.Join(' ', sanitized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FormatCause(MistakeCause cause)
    {
        return cause switch
        {
            MistakeCause.AdjacentKey => "adjacent key control",
            MistakeCause.SameFinger => "same-finger control",
            MistakeCause.WrongHand => "wrong-hand correction",
            MistakeCause.ShiftIssue => "shift accuracy",
            MistakeCause.Punctuation => "punctuation accuracy",
            MistakeCause.NumberRow => "number-row accuracy",
            MistakeCause.Rushed => "controlled pace",
            MistakeCause.Fatigue => "fatigue-safe flow",
            _ => "focused accuracy"
        };
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
