using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class ContentAnalyzerTests
{
    [TestMethod]
    public void ContentAnalyzer_CalculatesCharacterSet()
    {
        var item = ContentAnalyzer.CreateParagraph(
            "test",
            "Test",
            "abc cab",
            "test",
            "test");

        CollectionAssert.AreEquivalent(
            new[] { 'a', 'b', 'c' },
            item.CharacterSet.ToArray());
    }

    [TestMethod]
    public void ContentAnalyzer_CalculatesWordCount()
    {
        var item = ContentAnalyzer.CreateParagraph(
            "test",
            "Test",
            "one two three",
            "test",
            "test");

        Assert.AreEqual(3, item.WordCount);
    }

    [TestMethod]
    public void ContentAnalyzer_DetectsCapitalLetters()
    {
        var item = ContentAnalyzer.CreateParagraph(
            "test",
            "Test",
            "Hello world",
            "test",
            "test");

        Assert.IsTrue(item.ContainsCapitalLetters);
    }

    [TestMethod]
    public void ContentAnalyzer_DetectsNumbers()
    {
        var item = ContentAnalyzer.CreateParagraph(
            "test",
            "Test",
            "room 101",
            "test",
            "test");

        Assert.IsTrue(item.ContainsNumbers);
    }

    [TestMethod]
    public void ContentAnalyzer_DetectsPunctuation()
    {
        var item = ContentAnalyzer.CreateParagraph(
            "test",
            "Test",
            "hello, world",
            "test",
            "test");

        Assert.IsTrue(item.ContainsPunctuation);
    }
}
