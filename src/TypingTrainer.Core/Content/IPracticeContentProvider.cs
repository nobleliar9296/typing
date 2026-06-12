namespace TypingTrainer.Core.Content;

public interface IPracticeContentProvider
{
    IReadOnlyList<PracticeContentItem> GetContentItems();
}
