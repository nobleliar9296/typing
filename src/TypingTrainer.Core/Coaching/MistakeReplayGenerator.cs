using System.Text;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Coaching;

public sealed class MistakeReplayGenerator
{
    public LessonGenerationResult Generate(
        SessionReview review,
        IReadOnlyList<TypingInputEvent> events,
        int targetCharacters,
        int randomSeed = 0)
    {
        var replayTokens = BuildReplayTokens(review, events).ToArray();
        if (replayTokens.Length == 0)
        {
            replayTokens = review.FocusCharacters
                .Select(character => character.ToString())
                .Concat(review.FocusBigrams)
                .DefaultIfEmpty("the")
                .ToArray();
        }

        var text = BuildText(replayTokens, Math.Clamp(targetCharacters, 40, 800), randomSeed);
        var focusCharacters = review.FocusCharacters.Take(6).ToArray();
        var focusBigrams = review.FocusBigrams.Take(8).ToArray();

        return new LessonGenerationResult(
            text,
            text.Where(character => !char.IsWhiteSpace(character)).ToHashSet(),
            focusCharacters,
            focusBigrams,
            "Exact replay of mistakes from the last session",
            "Mistake Replay",
            "Last session review");
    }

    private static IEnumerable<string> BuildReplayTokens(
        SessionReview review,
        IReadOnlyList<TypingInputEvent> events)
    {
        var incorrectExpected = events
            .Where(item => item.Kind == InputEventKind.Character && !item.IsCorrect && item.ExpectedChar is not null)
            .GroupBy(item => item.ExpectedChar!.Value)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key.ToString());
        var correctiveBackspaces = events
            .Where(item => item.Kind == InputEventKind.Backspace
                && item.ExpectedChar is not null
                && item.ActualChar is not null
                && item.ExpectedChar != item.ActualChar)
            .Select(item => item.ExpectedChar!.Value.ToString());

        return incorrectExpected
            .Concat(correctiveBackspaces)
            .Concat(review.FocusBigrams)
            .Concat(review.FocusCharacters.Select(character => character.ToString()))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal);
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

            var token = sampler.Sample(weighted);
            builder.Append(token.Length == 1 ? RepeatToken(token, sampler.Next(2, 5)) : token);
        }

        return builder.ToString();
    }

    private static string RepeatToken(string token, int count)
    {
        return string.Join(string.Empty, Enumerable.Repeat(token, count));
    }
}
