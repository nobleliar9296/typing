namespace TypingTrainer.Core.Lessons;

public sealed class BuiltInWordListProvider : IWordListProvider
{
    public IReadOnlyList<string> GetCommonWords()
    {
        return BuiltInWordList.CommonWords;
    }
}
