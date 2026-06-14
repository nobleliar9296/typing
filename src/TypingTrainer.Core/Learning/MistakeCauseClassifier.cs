using TypingTrainer.Core.Keyboard;

namespace TypingTrainer.Core.Learning;

public sealed class MistakeCauseClassifier
{
    private static readonly QwertyCharacterToKeyMapper Mapper = new();
    private static readonly IReadOnlyDictionary<string, (int Row, int Column, FingerAssignment Finger)> KeyPositions =
        BuildKeyPositions();

    public MistakeCause Classify(
        char expected,
        char actual,
        double? deltaPreviousMs,
        double elapsedMs,
        double sessionDurationMs)
    {
        if (expected == actual)
        {
            return MistakeCause.Other;
        }

        var expectedMapping = Mapper.Map(expected);
        var actualMapping = Mapper.Map(actual);

        if (IsShiftIssue(expectedMapping, actualMapping))
        {
            return MistakeCause.ShiftIssue;
        }

        if (char.IsPunctuation(expected))
        {
            return MistakeCause.Punctuation;
        }

        if (char.IsDigit(expected) || char.IsDigit(actual))
        {
            return MistakeCause.NumberRow;
        }

        if (sessionDurationMs > 0 && elapsedMs >= sessionDurationMs * 0.75)
        {
            return MistakeCause.Fatigue;
        }

        if (deltaPreviousMs is > 0 and < 95)
        {
            return MistakeCause.Rushed;
        }

        if (expectedMapping is not null
            && actualMapping is not null
            && KeyPositions.TryGetValue(expectedMapping.KeyId, out var expectedKey)
            && KeyPositions.TryGetValue(actualMapping.KeyId, out var actualKey))
        {
            if (expectedKey.Row == actualKey.Row
                && Math.Abs(expectedKey.Column - actualKey.Column) == 1)
            {
                return MistakeCause.AdjacentKey;
            }

            if (expectedKey.Finger == actualKey.Finger)
            {
                return MistakeCause.SameFinger;
            }

            if (IsLeftHand(expectedKey.Finger) != IsLeftHand(actualKey.Finger))
            {
                return MistakeCause.WrongHand;
            }
        }

        return MistakeCause.Other;
    }

    private static bool IsShiftIssue(
        CharacterKeyMapping? expected,
        CharacterKeyMapping? actual)
    {
        return expected?.RequiresShift == true
            && actual is not null
            && expected.KeyId == actual.KeyId
            && !actual.RequiresShift;
    }

    private static bool IsLeftHand(FingerAssignment finger)
    {
        return finger is FingerAssignment.LeftPinky
            or FingerAssignment.LeftRing
            or FingerAssignment.LeftMiddle
            or FingerAssignment.LeftIndex
            or FingerAssignment.LeftThumb;
    }

    private static IReadOnlyDictionary<string, (int Row, int Column, FingerAssignment Finger)> BuildKeyPositions()
    {
        var result = new Dictionary<string, (int Row, int Column, FingerAssignment Finger)>(StringComparer.Ordinal);
        var rows = QwertyVisualKeyboardLayout.Create().Rows;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var keys = rows[rowIndex].Keys;
            for (var columnIndex = 0; columnIndex < keys.Count; columnIndex++)
            {
                result[keys[columnIndex].Id] = (rowIndex, columnIndex, keys[columnIndex].Finger);
            }
        }

        return result;
    }
}
