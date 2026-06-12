namespace TypingTrainer.Core.Keyboard;

public sealed class QwertyCharacterToKeyMapper : ICharacterToKeyMapper
{
    private static readonly IReadOnlyDictionary<char, string> BaseCharacterKeys = new Dictionary<char, string>
    {
        ['`'] = "Backquote",
        ['1'] = "Digit1",
        ['2'] = "Digit2",
        ['3'] = "Digit3",
        ['4'] = "Digit4",
        ['5'] = "Digit5",
        ['6'] = "Digit6",
        ['7'] = "Digit7",
        ['8'] = "Digit8",
        ['9'] = "Digit9",
        ['0'] = "Digit0",
        ['-'] = "Minus",
        ['='] = "Equal",
        ['['] = "BracketLeft",
        [']'] = "BracketRight",
        ['\\'] = "Backslash",
        [';'] = "Semicolon",
        ['\''] = "Quote",
        [','] = "Comma",
        ['.'] = "Period",
        ['/'] = "Slash"
    };

    private static readonly IReadOnlyDictionary<char, string> ShiftedCharacterKeys = new Dictionary<char, string>
    {
        ['~'] = "Backquote",
        ['!'] = "Digit1",
        ['@'] = "Digit2",
        ['#'] = "Digit3",
        ['$'] = "Digit4",
        ['%'] = "Digit5",
        ['^'] = "Digit6",
        ['&'] = "Digit7",
        ['*'] = "Digit8",
        ['('] = "Digit9",
        [')'] = "Digit0",
        ['_'] = "Minus",
        ['+'] = "Equal",
        ['{'] = "BracketLeft",
        ['}'] = "BracketRight",
        ['|'] = "Backslash",
        [':'] = "Semicolon",
        ['"'] = "Quote",
        ['<'] = "Comma",
        ['>'] = "Period",
        ['?'] = "Slash"
    };

    private static readonly IReadOnlyDictionary<string, FingerAssignment> KeyFingers =
        QwertyVisualKeyboardLayout
            .Create()
            .Rows
            .SelectMany(row => row.Keys)
            .ToDictionary(key => key.Id, key => key.Finger, StringComparer.Ordinal);

    public CharacterKeyMapping? Map(char character)
    {
        if (character is >= 'a' and <= 'z')
        {
            return new CharacterKeyMapping(
                character,
                GetLetterKeyId(character),
                RequiresShift: false,
                ShiftKeyId: null);
        }

        if (character is >= 'A' and <= 'Z')
        {
            var keyId = GetLetterKeyId(char.ToLowerInvariant(character));
            return new CharacterKeyMapping(
                character,
                keyId,
                RequiresShift: true,
                ShiftKeyId: GetOppositeShiftKeyId(keyId));
        }

        if (character == ' ')
        {
            return new CharacterKeyMapping(character, "Space", RequiresShift: false, ShiftKeyId: null);
        }

        if (character is '\r' or '\n')
        {
            return new CharacterKeyMapping(character, "Enter", RequiresShift: false, ShiftKeyId: null);
        }

        if (character == '\t')
        {
            return new CharacterKeyMapping(character, "Tab", RequiresShift: false, ShiftKeyId: null);
        }

        if (BaseCharacterKeys.TryGetValue(character, out var baseKeyId))
        {
            return new CharacterKeyMapping(character, baseKeyId, RequiresShift: false, ShiftKeyId: null);
        }

        if (ShiftedCharacterKeys.TryGetValue(character, out var shiftedKeyId))
        {
            return new CharacterKeyMapping(
                character,
                shiftedKeyId,
                RequiresShift: true,
                ShiftKeyId: GetOppositeShiftKeyId(shiftedKeyId));
        }

        return null;
    }

    private static string GetLetterKeyId(char lowercaseLetter)
    {
        return $"Key{char.ToUpperInvariant(lowercaseLetter)}";
    }

    private static string? GetOppositeShiftKeyId(string keyId)
    {
        return KeyFingers.TryGetValue(keyId, out var finger)
            ? GetOppositeShiftKeyId(finger)
            : null;
    }

    private static string? GetOppositeShiftKeyId(FingerAssignment finger)
    {
        return finger switch
        {
            FingerAssignment.LeftPinky
                or FingerAssignment.LeftRing
                or FingerAssignment.LeftMiddle
                or FingerAssignment.LeftIndex => "RightShift",
            FingerAssignment.RightIndex
                or FingerAssignment.RightMiddle
                or FingerAssignment.RightRing
                or FingerAssignment.RightPinky => "LeftShift",
            _ => null
        };
    }
}
