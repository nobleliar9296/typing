using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class ParagraphChunkerTests
{
    [TestMethod]
    public void ParagraphChunker_SplitsOnBlankLines()
    {
        var paragraphs = ParagraphChunker.SplitParagraphs(
            ["first paragraph has enough text", "", "second paragraph has enough text"],
            minParagraphCharacters: 10,
            maxParagraphCharacters: 200,
            normalizeWhitespace: true,
            lowercaseWhenImported: false).ToArray();

        Assert.AreEqual(2, paragraphs.Length);
        Assert.AreEqual("first paragraph has enough text", paragraphs[0]);
        Assert.AreEqual("second paragraph has enough text", paragraphs[1]);
    }

    [TestMethod]
    public void ParagraphChunker_SplitsLongParagraphs()
    {
        var paragraph = "alpha beta gamma delta epsilon zeta eta theta iota kappa";

        var chunks = ParagraphChunker.SplitLongParagraph(paragraph, maxParagraphCharacters: 24).ToArray();

        Assert.IsTrue(chunks.Length > 1);
        Assert.IsTrue(chunks.All(chunk => chunk.Length <= 24));
    }
}
