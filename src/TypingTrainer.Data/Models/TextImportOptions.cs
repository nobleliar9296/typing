namespace TypingTrainer.Data.Models;

public sealed record TextImportOptions(
    string PackName,
    int MinParagraphCharacters = 80,
    int MaxParagraphCharacters = 900,
    bool NormalizeWhitespace = true,
    bool LowercaseWhenImported = false,
    bool NormalizeToAscii = true);
