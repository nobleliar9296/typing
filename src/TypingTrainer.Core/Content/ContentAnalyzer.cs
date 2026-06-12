namespace TypingTrainer.Core.Content;

public static class ContentAnalyzer
{
    private static readonly char[] WhitespaceSeparators = [' ', '\t', '\r', '\n'];

    public static PracticeContentItem CreateParagraph(
        string id,
        string title,
        string text,
        string source,
        string license)
    {
        return CreateContentItem(
            id,
            PracticeContentKind.Paragraph,
            title,
            text,
            source,
            license);
    }

    public static PracticeContentItem AnalyzeParagraph(
        string id,
        string packId,
        string title,
        string text,
        string source)
    {
        return CreateContentItem(
            id,
            PracticeContentKind.Paragraph,
            title,
            text,
            source,
            "Imported",
            packId,
            DateTimeOffset.UtcNow,
            forceAscii: true);
    }

    public static PracticeContentItem CreateWord(
        string word,
        string source,
        string license)
    {
        var normalizedWord = NormalizeWhitespace(word).ToLowerInvariant();
        return CreateContentItem(
            $"word-{normalizedWord}",
            PracticeContentKind.Word,
            normalizedWord,
            normalizedWord,
            source,
            license);
    }

    private static PracticeContentItem CreateContentItem(
        string id,
        PracticeContentKind kind,
        string title,
        string text,
        string source,
        string license,
        string? packId = null,
        DateTimeOffset? createdAtUtc = null,
        bool forceAscii = false)
    {
        var normalizedText = NormalizeWhitespace(forceAscii ? AsciiTextNormalizer.ToAscii(text) : text);
        var words = SplitWords(normalizedText);
        var containsCapitalLetters = normalizedText.Any(char.IsUpper);
        var containsNumbers = normalizedText.Any(char.IsDigit);
        var containsPunctuation = normalizedText.Any(char.IsPunctuation);
        var averageWordLength = words.Length == 0 ? 0 : words.Average(word => word.Length);

        return new PracticeContentItem(
            id,
            kind,
            title,
            normalizedText,
            "en",
            source,
            license,
            Array.Empty<string>(),
            normalizedText
                .Where(character => !char.IsWhiteSpace(character))
                .Distinct()
                .ToHashSet(),
            words.Length,
            normalizedText.Length,
            containsCapitalLetters,
            containsNumbers,
                containsPunctuation,
                averageWordLength,
                CalculateDifficulty(
                    containsPunctuation,
                    containsCapitalLetters,
                    containsNumbers,
                    averageWordLength),
            packId,
            createdAtUtc);
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', SplitWords(text));
    }

    private static string[] SplitWords(string text)
    {
        return text
            .Trim()
            .Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
    }

    private static double CalculateDifficulty(
        bool containsPunctuation,
        bool containsCapitalLetters,
        bool containsNumbers,
        double averageWordLength)
    {
        var punctuationPenalty = containsPunctuation ? 1.0 : 0.0;
        var capitalizationPenalty = containsCapitalLetters ? 1.0 : 0.0;
        var numberPenalty = containsNumbers ? 1.0 : 0.0;
        var averageWordLengthPenalty = Clamp((averageWordLength - 4.0) / 8.0, 0, 1);

        return (0.35 * punctuationPenalty)
            + (0.25 * capitalizationPenalty)
            + (0.20 * numberPenalty)
            + (0.20 * averageWordLengthPenalty);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
