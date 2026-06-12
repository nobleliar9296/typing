using System.Globalization;
using System.Text;

namespace TypingTrainer.Core.Content;

public static class AsciiTextNormalizer
{
    public static string ToAscii(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            AppendAscii(builder, character);
        }

        return builder.ToString();
    }

    private static void AppendAscii(StringBuilder builder, char character)
    {
        switch (character)
        {
            case '\u00A0':
                builder.Append(' ');
                return;
            case '\u00AD':
                return;
            case '\u00AB':
            case '\u00BB':
            case '\u201C':
            case '\u201D':
            case '\u201E':
            case '\u201F':
            case '\u2033':
                builder.Append('"');
                return;
            case '\u2018':
            case '\u2019':
            case '\u201A':
            case '\u201B':
            case '\u2032':
                builder.Append('\'');
                return;
            case '\u2010':
            case '\u2011':
            case '\u2012':
            case '\u2013':
            case '\u2014':
            case '\u2212':
                builder.Append('-');
                return;
            case '\u2026':
                builder.Append("...");
                return;
            case '\u2022':
            case '\u00B7':
                builder.Append('*');
                return;
        }

        if (character <= 0x7F)
        {
            if (!char.IsControl(character) || char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }

            return;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(character);
        if (category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark)
        {
            return;
        }

        if (char.IsWhiteSpace(character))
        {
            builder.Append(' ');
        }
    }
}
