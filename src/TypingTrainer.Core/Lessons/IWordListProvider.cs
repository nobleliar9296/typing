namespace TypingTrainer.Core.Lessons;

public interface IWordListProvider
{
    IReadOnlyList<string> GetCommonWords();
}
