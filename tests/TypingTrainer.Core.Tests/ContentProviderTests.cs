using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;
using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class ContentProviderTests
{
    [TestMethod]
    public void BuiltInWordListProvider_ReturnsNonEmptyWords()
    {
        var words = new BuiltInWordListProvider().GetCommonWords();

        Assert.IsTrue(words.Count > 0);
        Assert.IsTrue(words.All(word => !string.IsNullOrWhiteSpace(word)));
    }

    [TestMethod]
    public void BuiltInParagraphProvider_ReturnsNonEmptyParagraphs()
    {
        var paragraphs = new BuiltInParagraphProvider().GetContentItems();

        Assert.IsTrue(paragraphs.Count > 0);
        Assert.IsTrue(paragraphs.All(item => item.Kind == PracticeContentKind.Paragraph));
        Assert.IsTrue(paragraphs.All(item => !string.IsNullOrWhiteSpace(item.Text)));
    }
}
