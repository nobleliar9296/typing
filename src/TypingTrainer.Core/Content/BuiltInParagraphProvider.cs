namespace TypingTrainer.Core.Content;

public sealed class BuiltInParagraphProvider : IPracticeContentProvider
{
    private static readonly PracticeContentItem[] Paragraphs =
    [
        ContentAnalyzer.CreateParagraph(
            "home-row-001",
            "Home Row Steps",
            "fj dk jf kd fs sl df lk fj dk jf kd",
            "TypingTrainer built-in",
            "Original"),
        ContentAnalyzer.CreateParagraph(
            "home-row-words-001",
            "Simple Home Words",
            "sad lad asks dad fall all lads add salad",
            "TypingTrainer built-in",
            "Original"),
        ContentAnalyzer.CreateParagraph(
            "steady-practice-001",
            "Steady Practice",
            "steady hands make simple work small steps build speed over time",
            "TypingTrainer built-in",
            "Original"),
        ContentAnalyzer.CreateParagraph(
            "quiet-flow-001",
            "Quiet Flow",
            "the small dog ran over the field and found a quiet place near the water",
            "TypingTrainer built-in",
            "Original"),
        ContentAnalyzer.CreateParagraph(
            "local-focus-001",
            "Local Focus",
            "local practice keeps each lesson close to the keyboard and clear in the mind",
            "TypingTrainer built-in",
            "Original")
    ];

    public IReadOnlyList<PracticeContentItem> GetContentItems()
    {
        return Paragraphs;
    }
}
