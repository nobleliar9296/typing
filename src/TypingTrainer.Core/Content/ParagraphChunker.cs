using System.Text;

namespace TypingTrainer.Core.Content;

public static class ParagraphChunker
{
    public static IEnumerable<string> SplitParagraphs(
        IEnumerable<string> lines,
        int minParagraphCharacters,
        int maxParagraphCharacters,
        bool normalizeWhitespace,
        bool lowercaseWhenImported,
        bool normalizeToAscii = true)
    {
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                foreach (var paragraph in Flush(builder, minParagraphCharacters, maxParagraphCharacters, normalizeWhitespace, lowercaseWhenImported, normalizeToAscii))
                {
                    yield return paragraph;
                }

                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line.Trim());
        }

        foreach (var paragraph in Flush(builder, minParagraphCharacters, maxParagraphCharacters, normalizeWhitespace, lowercaseWhenImported, normalizeToAscii))
        {
            yield return paragraph;
        }
    }

    public static IEnumerable<string> SplitLongParagraph(string paragraph, int maxParagraphCharacters)
    {
        if (maxParagraphCharacters <= 0 || paragraph.Length <= maxParagraphCharacters)
        {
            yield return paragraph;
            yield break;
        }

        var remaining = paragraph.Trim();
        while (remaining.Length > maxParagraphCharacters)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxParagraphCharacters);
            if (splitIndex <= 0)
            {
                splitIndex = maxParagraphCharacters;
            }

            yield return remaining[..splitIndex].Trim();
            remaining = remaining[splitIndex..].Trim();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    private static IEnumerable<string> Flush(
        StringBuilder builder,
        int minParagraphCharacters,
        int maxParagraphCharacters,
        bool normalizeWhitespace,
        bool lowercaseWhenImported,
        bool normalizeToAscii)
    {
        if (builder.Length == 0)
        {
            yield break;
        }

        var paragraph = builder.ToString();
        builder.Clear();

        paragraph = normalizeWhitespace
            ? NormalizeWhitespace(paragraph)
            : paragraph.Trim();

        if (normalizeToAscii)
        {
            paragraph = AsciiTextNormalizer.ToAscii(paragraph);
        }

        if (normalizeWhitespace)
        {
            paragraph = NormalizeWhitespace(paragraph);
        }

        if (lowercaseWhenImported)
        {
            paragraph = paragraph.ToLowerInvariant();
        }

        if (paragraph.Length < minParagraphCharacters)
        {
            yield break;
        }

        foreach (var chunk in SplitLongParagraph(paragraph, maxParagraphCharacters))
        {
            if (chunk.Length >= minParagraphCharacters)
            {
                yield return chunk;
            }
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
